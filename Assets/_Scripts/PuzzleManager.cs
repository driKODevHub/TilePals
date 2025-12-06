using UnityEngine;
using UnityEngine.InputSystem;
using System.Collections.Generic;
using System;
using System.Linq;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [Header("Dependencies")]
    [SerializeField] private InputReader inputReader;

    [Header("Game Flow Settings")]
    [SerializeField] private bool lockPiecesOnLevelComplete = true;
    [SerializeField] private bool smoothMovementOffGrid = true;

    [Header("Placement Settings")]
    [SerializeField] private float pieceHeightWhenHeld = 1.0f;
    [SerializeField] private float shakenVelocityThreshold = 15f;
    [SerializeField] private Material invalidPlacementMaterial; // Залишено для сумісності

    [Header("Interaction Settings")]
    [Tooltip("Мінімальна відстань (у пікселях), яку треба пройти мишкою з затиснутою кнопкою, щоб це вважалося Петінгом.")]
    [SerializeField] private float dragThreshold = 20f;

    [Tooltip("Максимальний час (у секундах) для швидкого кліку.")]
    [SerializeField] private float clickTimeThreshold = 0.25f;

    [Header("Layers")]
    [SerializeField] private LayerMask pieceLayer;
    [SerializeField] private LayerMask offGridPlaneLayer;

    // State
    private PuzzlePiece _heldPiece;
    private PuzzlePiece _hoveredPiece;

    // Interaction State
    private PuzzlePiece _potentialInteractionPiece;
    private Vector2 _clickStartPos;
    private float _clickStartTime;
    private bool _isPettingActive;

    // Logic Variables
    private Vector3 _initialPiecePosition;
    private Quaternion _initialPieceRotation;
    private bool _isLevelComplete = false;
    private List<PuzzlePiece> _piecesBeingFlownOver = new List<PuzzlePiece>();
    private List<PiecePersonality> _allPersonalities = new List<PiecePersonality>();

    // Mouse Velocity Calculation
    private Vector2 _lastMousePos;
    private float _currentMouseSpeed;

    // --- SNAP STATE TRACKING (UPDATED) ---
    // Зберігаємо останню валідну позицію на сітці, де був звук снепу.
    // Nullable тип дозволяє легко скидати стан.
    private Vector2Int? _lastSnappedGridOrigin = null;

    public event Action<PuzzlePiece> OnPiecePickedUp;
    public event Action<PuzzlePiece> OnPieceDropped;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (inputReader == null)
            inputReader = FindFirstObjectByType<InputReader>();
    }

    private void Start()
    {
        if (inputReader != null)
        {
            inputReader.OnClickStarted += HandleClickStart;
            inputReader.OnClickCanceled += HandleClickEnd;

            inputReader.OnRotatePieceLeft += () => TryRotateHeldPiece(false);
            inputReader.OnRotatePieceRight += () => TryRotateHeldPiece(true);
            inputReader.OnAlternateRotate += () => TryRotateHeldPiece(true);
        }
    }

    private void OnDestroy()
    {
        if (inputReader != null)
        {
            inputReader.OnClickStarted -= HandleClickStart;
            inputReader.OnClickCanceled -= HandleClickEnd;
        }
    }

    public void ResetState()
    {
        _heldPiece = null;
        if (_hoveredPiece != null)
        {
            _hoveredPiece.SetOutline(false);
            _hoveredPiece = null;
        }
        _potentialInteractionPiece = null;
        _isPettingActive = false;

        _isLevelComplete = false;
        _piecesBeingFlownOver.Clear();
        _allPersonalities = FindObjectsByType<PiecePersonality>(FindObjectsSortMode.None).ToList();
    }

    private void Update()
    {
        CalculateMouseSpeed();

        if (_heldPiece == null)
        {
            HandleHoverLogic();
            HandleInteractionLogic();
        }
        else
        {
            UpdateHeldPiecePosition();
            CheckForFlyOver();
        }
    }

    private void CalculateMouseSpeed()
    {
        if (inputReader == null) return;
        Vector2 currentPos = inputReader.MousePosition;
        float dist = (currentPos - _lastMousePos).magnitude;
        _currentMouseSpeed = dist / Time.deltaTime;
        _lastMousePos = currentPos;
    }

    #region Input Handlers & Interaction Logic

    private void HandleHoverLogic()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (_isLevelComplete) return;

        if (_potentialInteractionPiece != null) return;

        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer))
        {
            PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
            if (piece != _hoveredPiece)
            {
                if (_hoveredPiece != null) _hoveredPiece.SetOutline(false);
                _hoveredPiece = piece;
                if (_hoveredPiece != null) _hoveredPiece.SetOutline(true);
            }
        }
        else
        {
            if (_hoveredPiece != null)
            {
                _hoveredPiece.SetOutline(false);
                _hoveredPiece = null;
            }
        }
    }

    private void HandleClickStart()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (_isLevelComplete) return;

        if (Keyboard.current != null && Keyboard.current.altKey.isPressed) return;

        if (_heldPiece == null && _hoveredPiece != null)
        {
            _potentialInteractionPiece = _hoveredPiece;
            _clickStartPos = inputReader.MousePosition;
            _clickStartTime = Time.time;
            _isPettingActive = false;
        }
    }

    private void HandleInteractionLogic()
    {
        if (_potentialInteractionPiece == null) return;

        if (!_isPettingActive)
        {
            float dragDistance = Vector2.Distance(inputReader.MousePosition, _clickStartPos);
            if (dragDistance > dragThreshold)
            {
                StartPetting();
            }
        }

        if (_isPettingActive)
        {
            PersonalityEventManager.RaisePettingUpdate(_potentialInteractionPiece, _currentMouseSpeed);
        }
    }

    private void HandleClickEnd()
    {
        if (_heldPiece != null && !_heldPiece.IsRotating)
        {
            TryToPlaceOrDropPiece();
            return;
        }

        if (_potentialInteractionPiece != null)
        {
            float pressDuration = Time.time - _clickStartTime;
            bool isFastClick = pressDuration <= clickTimeThreshold;

            if (isFastClick)
            {
                if (_isPettingActive) StopPetting();

                if (_hoveredPiece == _potentialInteractionPiece)
                {
                    PickUpPiece(_potentialInteractionPiece);
                }
            }
            else
            {
                if (_isPettingActive)
                {
                    StopPetting();
                }
                else
                {
                    if (_hoveredPiece == _potentialInteractionPiece)
                    {
                        PickUpPiece(_potentialInteractionPiece);
                    }
                }
            }

            _potentialInteractionPiece = null;
            _isPettingActive = false;
        }
    }

    private void StartPetting()
    {
        if (!_potentialInteractionPiece.IsPlaced)
        {
            _isPettingActive = true;
            PersonalityEventManager.RaisePettingStart(_potentialInteractionPiece);
        }
    }

    private void StopPetting()
    {
        if (_potentialInteractionPiece != null)
        {
            PersonalityEventManager.RaisePettingEnd(_potentialInteractionPiece);
        }
        _isPettingActive = false;
    }

    #endregion

    #region Game Logic (PickUp, Drop, Move)

    private void PickUpPiece(PuzzlePiece piece)
    {
        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        Vector3 hitPoint;
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer)) hitPoint = hit.point;
        else hitPoint = piece.transform.position;

        DoPickUpLogic(piece, hitPoint);
    }

    private void DoPickUpLogic(PuzzlePiece piece, Vector3 hitPoint)
    {
        if (piece == null) return;
        if (lockPiecesOnLevelComplete && _isLevelComplete && piece.IsPlaced) return;

        if (GridBuildingSystem.Instance == null || GridBuildingSystem.Instance.GetGrid() == null)
        {
            Debug.LogError("Grid not initialized!");
            return;
        }

        _heldPiece = piece;
        _heldPiece.SetOutlineLocked(true);
        if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayPickup();

        _initialPiecePosition = piece.transform.position;
        _initialPieceRotation = piece.transform.rotation;

        // Скидаємо трекер снепінгу, щоб перший вхід на грід теж тригернув звук
        _lastSnappedGridOrigin = null;

        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
        Vector3 pieceOriginWorld = piece.transform.position - new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;

        GridBuildingSystem.Instance.GetGrid().GetXZ(hitPoint, out int clickX, out int clickZ);
        GridBuildingSystem.Instance.GetGrid().GetXZ(pieceOriginWorld, out int originX, out int originZ);

        piece.ClickOffset = new Vector2Int(clickX - originX, clickZ - originZ);

        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
        }
        else
        {
            OffGridManager.RemovePiece(piece);
            piece.SetOffGrid(false);
        }

        OnPiecePickedUp?.Invoke(piece);
        PersonalityEventManager.RaisePiecePickedUp(piece);
    }

    private void UpdateHeldPiecePosition()
    {
        if (_heldPiece == null || _heldPiece.IsRotating) return;

        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer))
        {
            if (GridBuildingSystem.Instance == null) return;

            var grid = GridBuildingSystem.Instance.GetGrid();
            if (grid == null) return;

            float cellSize = grid.GetCellSize();
            grid.GetXZ(hit.point, out int cursorX, out int cursorZ);

            Vector2Int logicalOrigin = new Vector2Int(cursorX, cursorZ) - _heldPiece.ClickOffset;
            Vector3 targetPosition;

            bool isMouseOverGrid = GridBuildingSystem.Instance.IsValidGridPosition(cursorX, cursorZ);
            Vector2Int rotationOffset = _heldPiece.PieceTypeSO.GetRotationOffset(_heldPiece.CurrentDirection);
            Vector3 rotationVisualOffset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;

            if (isMouseOverGrid || !smoothMovementOffGrid)
            {
                Vector3 snappedGridPos = grid.GetWorldPosition(logicalOrigin.x, logicalOrigin.y);
                targetPosition = snappedGridPos + rotationVisualOffset;
            }
            else
            {
                Vector3 mouseWorldPos = hit.point;
                mouseWorldPos.y = 0;
                Vector3 clickOffsetVector = new Vector3(_heldPiece.ClickOffset.x, 0, _heldPiece.ClickOffset.y) * cellSize;
                Vector3 centerOfCellOffset = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
                Vector3 smoothOrigin = mouseWorldPos - clickOffsetVector - centerOfCellOffset;
                targetPosition = smoothOrigin + rotationVisualOffset;
            }

            targetPosition.y = pieceHeightWhenHeld;

            if (_heldPiece.Movement != null)
            {
                _heldPiece.Movement.SetTargetPosition(targetPosition);

                if (_heldPiece.Movement.CurrentVelocity > shakenVelocityThreshold)
                {
                    PersonalityEventManager.RaisePieceShaken(_heldPiece, _heldPiece.Movement.CurrentVelocity);
                }
            }
            else
            {
                _heldPiece.transform.position = Vector3.Lerp(_heldPiece.transform.position, targetPosition, Time.deltaTime * 25f);
            }

            // --- ПЕРЕВІРКА ВАЛІДНОСТІ ТА СНЕПІНГУ ---
            CanPlaceHeldPiece(logicalOrigin, out bool canOnGrid, out bool canOffGrid);

            // Візуалізація (Червоний/Синій аутлайн)
            bool isValid = canOnGrid || canOffGrid;
            _heldPiece.SetInvalidPlacementVisual(!isValid);

            // --- ОНОВЛЕНА ЛОГІКА СНЕПІНГУ ---
            if (canOnGrid)
            {
                // Якщо ми не зберегли позицію АБО нова позиція відрізняється від збереженої
                // (тобто ми перемістилися на нову валідну клітинку)
                if (_lastSnappedGridOrigin == null || _lastSnappedGridOrigin.Value != logicalOrigin)
                {
                    if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayGridSnap();
                    _lastSnappedGridOrigin = logicalOrigin; // Запам'ятовуємо нову позицію
                }
            }
            else
            {
                // Якщо ми пішли з валідної зони (або стали червоним), скидаємо трекер.
                // Це дозволить зіграти звук знову, якщо ми повернемося навіть на ту саму клітинку.
                _lastSnappedGridOrigin = null;
            }
        }
    }

    private void TryToPlaceOrDropPiece()
    {
        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer)) return;

        var grid = GridBuildingSystem.Instance.GetGrid();
        grid.GetXZ(hit.point, out int cursorX, out int cursorZ);

        Vector2Int origin = new Vector2Int(cursorX, cursorZ) - _heldPiece.ClickOffset;

        CanPlaceHeldPiece(origin, out bool canOnGrid, out bool canOffGrid);

        if (canOnGrid)
        {
            // Успішне розміщення на ГРІДІ
            ICommand cmd = new PlaceCommand(_heldPiece, origin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);
            if (cmd.Execute())
            {
                if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayPlaceSuccess();
                FinalizeDrop(cmd);
                CheckForWin();
            }
        }
        else if (canOffGrid)
        {
            // Успішний дроп в OffGrid
            ICommand cmd = new OffGridPlaceCommand(_heldPiece, origin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);
            PuzzlePiece droppedPiece = _heldPiece;

            if (cmd.Execute())
            {
                if (droppedPiece.Visuals != null) droppedPiece.Visuals.PlayDrop();
                FinalizeDrop(cmd);
                PersonalityEventManager.RaisePieceDropped(droppedPiece);
            }
        }
        else
        {
            // --- ПОМИЛКА РОЗМІЩЕННЯ ---
            Debug.Log("Invalid placement position.");
            if (_heldPiece.Visuals != null)
            {
                _heldPiece.Visuals.PlayPlaceFailed();
            }
        }
    }

    private void FinalizeDrop(ICommand command)
    {
        // Скидаємо візуал валідності (повертаємо нормальний колір)
        _heldPiece.SetInvalidPlacementVisual(false);
        _heldPiece.SetOutlineLocked(false);

        CommandHistory.AddCommand(command);
        OnPieceDropped?.Invoke(_heldPiece);

        _heldPiece = null;
        GameManager.Instance.SaveCurrentProgress();
    }

    private void TryRotateHeldPiece(bool clockwise)
    {
        if (_heldPiece != null && !_heldPiece.IsRotating)
        {
            // --- ФІДБЕК ОБЕРТАННЯ ---
            if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayRotate();

            float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
            _heldPiece.RotatePiece(clockwise, cellSize);
        }
    }

    private void CanPlaceHeldPiece(Vector2Int origin, out bool canOnGrid, out bool canOffGrid)
    {
        canOnGrid = false;
        canOffGrid = false;
        if (_heldPiece == null) return;

        var grid = GridBuildingSystem.Instance.GetGrid();
        List<Vector2Int> pieceCells = _heldPiece.PieceTypeSO.GetGridPositionsList(origin, _heldPiece.CurrentDirection);

        bool allOnGrid = pieceCells.All(cell => grid.GetGridObject(cell.x, cell.y) != null);
        bool allOffGrid = pieceCells.All(cell => grid.GetGridObject(cell.x, cell.y) == null);

        if (allOnGrid)
        {
            canOnGrid = GridBuildingSystem.Instance.CanPlacePiece(_heldPiece, origin, _heldPiece.CurrentDirection);
        }
        else if (allOffGrid)
        {
            canOffGrid = OffGridManager.CanPlacePiece(_heldPiece, origin);
        }
        else
        {
            bool occupiesBuildable = pieceCells.Any(cell => {
                GridObject go = grid.GetGridObject(cell.x, cell.y);
                return go != null && go.IsBuildable();
            });
            if (!occupiesBuildable) canOffGrid = OffGridManager.CanPlacePiece(_heldPiece, origin);
        }
    }

    private void CheckForFlyOver()
    {
        if (_allPersonalities.Count == 0) return;

        var currentOver = new List<PuzzlePiece>();
        Vector3 heldPos = _heldPiece.transform.position; heldPos.y = 0;

        foreach (var p in _allPersonalities)
        {
            if (p == null) continue;
            PuzzlePiece piece = p.GetComponent<PuzzlePiece>();
            if (piece == _heldPiece) continue;

            Vector3 otherPos = piece.transform.position; otherPos.y = 0;
            if (Vector3.Distance(heldPos, otherPos) < p.GetFlyOverRadius())
            {
                currentOver.Add(piece);
                if (!_piecesBeingFlownOver.Contains(piece))
                {
                    PersonalityEventManager.RaisePieceFlyOver(piece);
                }
            }
        }
        _piecesBeingFlownOver = currentOver;
    }

    private void CheckForWin()
    {
        if (!_isLevelComplete && GridBuildingSystem.Instance.CalculateGridFillPercentage() > 99.9f)
        {
            _isLevelComplete = true;
            GameManager.Instance.OnLevelComplete();
        }
    }

    #endregion

    public void SaveCurrentProgress() => GameManager.Instance.SaveCurrentProgress();
    public void OnLevelComplete() => GameManager.Instance.OnLevelComplete();
}