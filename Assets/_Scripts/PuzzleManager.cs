using UnityEngine;
using System.Collections.Generic;
using System;
using System.Linq;

/// <summary>
/// Об'єднаний клас, що керує всією ігровою логікою та взаємодією.
/// </summary>
public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    [Header("Game Flow Settings")]
    [Tooltip("Якщо істина, гравці не зможуть піднімати фігури з сітки після завершення рівня.")]
    [SerializeField] private bool lockPiecesOnLevelComplete = true;

    [Tooltip("Якщо істина, рух фігури за межами ігрового поля буде плавним. Якщо хиба - фігура буде 'стрибати' по невидимій сітці.")]
    [SerializeField] private bool smoothMovementOffGrid = true;

    [Header("Налаштування руху фігур")]
    [SerializeField] private float pieceFollowSpeed = 25f;
    [SerializeField] private float pieceHeightWhenHeld = 1.0f;
    [SerializeField] private float shakenVelocityThreshold = 15f;

    [Header("Налаштування візуалу")]
    [SerializeField] private Material invalidPlacementMaterial;

    [Header("Налаштування шарів (Layers)")]
    [SerializeField] private LayerMask pieceLayer;
    [SerializeField] private LayerMask offGridPlaneLayer;

    // Ігрові стани
    private PuzzlePiece _heldPiece;
    private PuzzlePiece _pieceUnderMouse;
    private PuzzlePiece _pieceBeingPetted;

    // Змінні для логіки
    private Vector3 _initialPiecePosition;
    private Quaternion _initialPieceRotation;
    private Vector3 _lastMousePosition;
    private float _mouseSpeed;
    private Vector3 _lastHeldPiecePosition;
    private float _heldPieceVelocity;
    private bool _isLevelComplete = false;
    private bool _justPlacedPiece = false;

    private List<PuzzlePiece> _piecesBeingFlownOver = new List<PuzzlePiece>();
    private List<PiecePersonality> _allPersonalities = new List<PiecePersonality>();


    // Події для старої логіки (GridVisualManager)
    public event Action<PuzzlePiece> OnPiecePickedUp;
    public event Action<PuzzlePiece> OnPieceDropped;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetState()
    {
        _heldPiece = null;
        _pieceUnderMouse = null;
        _pieceBeingPetted = null;
        _isLevelComplete = false;
        _piecesBeingFlownOver.Clear();

        // --- ОНОВЛЕНО: Використання сучасного методу FindObjectsByType ---
        _allPersonalities = FindObjectsByType<PiecePersonality>(FindObjectsSortMode.None).ToList();
    }

    private void LateUpdate()
    {
        _justPlacedPiece = false;
    }

    private void Update()
    {
        // --- БЛОКУВАННЯ ВВОДУ ПРИ ПАУЗІ ---
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        UpdateMouseState();

        if (_heldPiece != null)
        {
            HandleHeldPieceInput();
        }
        else
        {
            HandleIdleInput();
            if (_piecesBeingFlownOver.Count > 0) _piecesBeingFlownOver.Clear();
        }
    }

    private void UpdateMouseState()
    {
        _mouseSpeed = (Input.mousePosition - _lastMousePosition).magnitude / Time.deltaTime;
        _lastMousePosition = Input.mousePosition;

        _pieceUnderMouse = null;
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer))
        {
            PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
            if (piece != null)
            {
                _pieceUnderMouse = piece;
            }
        }
    }

    private void HandleIdleInput()
    {
        // Додаткова перевірка на паузу (хоча Update вже блокує, це для безпеки)
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        if (Input.GetMouseButtonDown(0))
        {
            if (_pieceUnderMouse != null && !_justPlacedPiece)
            {
                _pieceBeingPetted = _pieceUnderMouse;
                if (!_pieceUnderMouse.IsPlaced)
                {
                    PersonalityEventManager.RaisePettingStart(_pieceBeingPetted);
                }
            }
        }

        if (Input.GetMouseButton(0))
        {
            if (_pieceBeingPetted != null && !_pieceBeingPetted.IsPlaced)
            {
                if (_pieceUnderMouse == _pieceBeingPetted)
                {
                    PersonalityEventManager.RaisePettingUpdate(_pieceBeingPetted, _mouseSpeed);
                }
                else
                {
                    PersonalityEventManager.RaisePettingEnd(_pieceBeingPetted);
                    _pieceBeingPetted = null;
                }
            }
        }

        if (Input.GetMouseButtonUp(0))
        {
            if (_pieceBeingPetted != null)
            {
                if (_pieceUnderMouse == _pieceBeingPetted)
                {
                    if (!_pieceBeingPetted.IsPlaced)
                    {
                        PersonalityEventManager.RaisePettingEnd(_pieceBeingPetted);
                    }
                    Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
                    if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer))
                    {
                        PickUpPiece(_pieceBeingPetted, hit.point);
                    }
                    else
                    {
                        PickUpPiece(_pieceBeingPetted, _pieceBeingPetted.transform.position); // Fallback
                    }
                }
                else if (!_pieceBeingPetted.IsPlaced)
                {
                    PersonalityEventManager.RaisePettingEnd(_pieceBeingPetted);
                }
            }
            _pieceBeingPetted = null;
        }
    }

    private void PickUpPiece(PuzzlePiece piece, Vector3 hitPoint)
    {
        // --- ПЕРЕВІРКА НА БЛОКУВАННЯ ПІСЛЯ ЗАВЕРШЕННЯ РІВНЯ ---
        if (lockPiecesOnLevelComplete && _isLevelComplete && piece.IsPlaced)
        {
            return;
        }
        // -----------------------------------------------------

        if (_heldPiece != null) return;

        _heldPiece = piece;
        _initialPiecePosition = piece.transform.position;
        _initialPieceRotation = piece.transform.rotation;
        _lastHeldPiecePosition = piece.transform.position;
        _heldPieceVelocity = 0f;

        // --- ЛОГІКА ОБЧИСЛЕННЯ ЗМІЩЕННЯ КЛІКУ ---
        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector3 pieceOriginWorld = piece.transform.position - new Vector3(piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection).x, 0, piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection).y) * cellSize;

        GridBuildingSystem.Instance.GetGrid().GetXZ(hitPoint, out int clickX, out int clickZ);
        GridBuildingSystem.Instance.GetGrid().GetXZ(pieceOriginWorld, out int originX, out int originZ);

        // Зберігаємо зміщення ПРЯМО у фігуру
        piece.ClickOffset = new Vector2Int(clickX - originX, clickZ - originZ);
        // ----------------------------------------

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

    private void HandleHeldPieceInput()
    {
        HandlePieceMovement();

        if (Input.GetMouseButtonDown(0) && !_heldPiece.IsRotating)
        {
            TryToPlaceOrDropPiece();
        }

        if ((Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1)) && !_heldPiece.IsRotating)
        {
            _heldPiece.StartSmoothRotation();
        }
    }

    private void HandlePieceMovement()
    {
        if (_heldPiece == null || _heldPiece.IsRotating) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer))
        {
            var grid = GridBuildingSystem.Instance.GetGrid();
            float cellSize = grid.GetCellSize();

            grid.GetXZ(hit.point, out int cursorX, out int cursorZ);

            // 1. Обчислюємо ЛОГІЧНУ позицію (Origin) для перевірок валідності. Вона завжди дискретна.
            Vector2Int logicalOrigin = new Vector2Int(cursorX, cursorZ) - _heldPiece.ClickOffset;

            // 2. Обчислюємо ВІЗУАЛЬНУ позицію.
            Vector3 targetPosition;

            // Перевіряємо, чи мишка знаходиться над ігровим полем
            bool isMouseOverGrid = GridBuildingSystem.Instance.IsValidGridPosition(cursorX, cursorZ);

            // Розрахунок зміщення від повороту фігури
            Vector2Int rotationOffset = _heldPiece.PieceTypeSO.GetRotationOffset(_heldPiece.CurrentDirection);
            Vector3 rotationVisualOffset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;

            // --- УМОВА ПЕРЕМИКАННЯ РЕЖИМІВ РУХУ ---
            if (isMouseOverGrid || !smoothMovementOffGrid)
            {
                // Режим SNAPPING (працює якщо ми на гріду АБО якщо плавний рух вимкнено)
                // Використовуємо жорстку прив'язку до клітинок
                Vector3 snappedGridPos = grid.GetWorldPosition(logicalOrigin.x, logicalOrigin.y);
                targetPosition = snappedGridPos + rotationVisualOffset;
            }
            else
            {
                // Режим SMOOTH MOVEMENT (працює тільки поза грідом і якщо увімкнено)

                // Позиція миші у площині Y=0
                Vector3 mouseWorldPos = hit.point;
                mouseWorldPos.y = 0;

                // Вектор від Origin фігури до центру клітинки, за яку ми тримаємо
                Vector3 clickOffsetVector = new Vector3(_heldPiece.ClickOffset.x, 0, _heldPiece.ClickOffset.y) * cellSize;
                Vector3 centerOfCellOffset = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);

                // Origin = MousePos - (OffsetToClickedCellCenter)
                Vector3 smoothOrigin = mouseWorldPos - clickOffsetVector - centerOfCellOffset;

                targetPosition = smoothOrigin + rotationVisualOffset;
            }

            // Додаємо висоту підйому
            targetPosition.y = pieceHeightWhenHeld;

            // Рухаємо фігуру
            _heldPiece.transform.position = Vector3.Lerp(_heldPiece.transform.position, targetPosition, Time.deltaTime * pieceFollowSpeed);

            // Розрахунок швидкості для ефекту трусіння
            _heldPieceVelocity = (_heldPiece.transform.position - _lastHeldPiecePosition).magnitude / Time.deltaTime;
            _lastHeldPiecePosition = _heldPiece.transform.position;

            if (_heldPieceVelocity > shakenVelocityThreshold)
            {
                PersonalityEventManager.RaisePieceShaken(_heldPiece, _heldPieceVelocity);
            }

            CheckForFlyOver();

            // Перевірка валідності (використовуємо ЛОГІЧНИЙ Origin, навіть якщо візуал плавний)
            bool canPlaceOnGrid, canPlaceOffGrid;
            CanPlaceHeldPiece(logicalOrigin, out canPlaceOnGrid, out canPlaceOffGrid);

            _heldPiece.UpdatePlacementVisual(canPlaceOnGrid || canPlaceOffGrid, invalidPlacementMaterial);
        }
    }

    private void CheckForFlyOver()
    {
        var currentlyOver = new List<PuzzlePiece>();

        foreach (var personality in _allPersonalities)
        {
            if (personality == null || personality.GetComponent<PuzzlePiece>() == _heldPiece) continue;

            float flyOverRadius = personality.GetFlyOverRadius();
            Vector3 heldPiecePosition = _heldPiece.transform.position;
            Vector3 stationaryPiecePosition = personality.transform.position;

            heldPiecePosition.y = 0;
            stationaryPiecePosition.y = 0;

            if (Vector3.Distance(heldPiecePosition, stationaryPiecePosition) < flyOverRadius)
            {
                PuzzlePiece stationaryPiece = personality.GetComponent<PuzzlePiece>();
                currentlyOver.Add(stationaryPiece);
                if (!_piecesBeingFlownOver.Contains(stationaryPiece))
                {
                    PersonalityEventManager.RaisePieceFlyOver(stationaryPiece);
                }
            }
        }
        _piecesBeingFlownOver = currentlyOver;
    }

    private void TryToPlaceOrDropPiece()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (!Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer)) return;

        var grid = GridBuildingSystem.Instance.GetGrid();
        grid.GetXZ(hit.point, out int cursorX, out int cursorZ);

        // --- ВИКОРИСТОВУЄМО ЗМІЩЕННЯ З ФІГУРИ ---
        Vector2Int origin = new Vector2Int(cursorX, cursorZ) - _heldPiece.ClickOffset;
        // ----------------------------------------

        bool canPlaceOnGrid, canPlaceOffGrid;
        CanPlaceHeldPiece(origin, out canPlaceOnGrid, out canPlaceOffGrid);

        if (canPlaceOnGrid)
        {
            TryPlaceOnGrid(origin);
        }
        else if (canPlaceOffGrid)
        {
            TryPlaceOffGrid(origin);
        }
        else
        {
            UnityEngine.Debug.Log("<color=red>Неможливо розмістити фігуру тут!</color>");
        }
    }

    private void TryPlaceOnGrid(Vector2Int origin)
    {
        ICommand placeCommand = new PlaceCommand(_heldPiece, origin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);
        if (placeCommand.Execute())
        {
            PuzzlePiece placedPiece = _heldPiece;
            _heldPiece.UpdatePlacementVisual(true, invalidPlacementMaterial);
            CommandHistory.AddCommand(placeCommand);

            OnPieceDropped?.Invoke(placedPiece);

            _heldPiece = null;
            _justPlacedPiece = true;
            CheckForWin();
            GameManager.Instance.SaveCurrentProgress();
        }
    }

    private void TryPlaceOffGrid(Vector2Int offGridOrigin)
    {
        ICommand placeCommand = new OffGridPlaceCommand(_heldPiece, offGridOrigin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);

        if (placeCommand.Execute())
        {
            PuzzlePiece placedPiece = _heldPiece;
            _heldPiece.UpdatePlacementVisual(true, invalidPlacementMaterial);
            CommandHistory.AddCommand(placeCommand);

            OnPieceDropped?.Invoke(placedPiece);
            PersonalityEventManager.RaisePieceDropped(placedPiece);

            _heldPiece = null;
            _justPlacedPiece = true;
            GameManager.Instance.SaveCurrentProgress();
        }
    }

    private void CanPlaceHeldPiece(Vector2Int origin, out bool canPlaceOnGrid, out bool canPlaceOffGrid)
    {
        canPlaceOnGrid = false;
        canPlaceOffGrid = false;

        if (_heldPiece == null) return;

        var grid = GridBuildingSystem.Instance.GetGrid();

        List<Vector2Int> pieceCells = _heldPiece.PieceTypeSO.GetGridPositionsList(origin, _heldPiece.CurrentDirection);

        bool allOnGrid = pieceCells.All(cell => grid.GetGridObject(cell.x, cell.y) != null);
        bool allOffGrid = pieceCells.All(cell => grid.GetGridObject(cell.x, cell.y) == null);

        if (allOnGrid)
        {
            canPlaceOnGrid = GridBuildingSystem.Instance.CanPlacePiece(_heldPiece, origin, _heldPiece.CurrentDirection);
        }
        else if (allOffGrid)
        {
            canPlaceOffGrid = OffGridManager.CanPlacePiece(_heldPiece, origin);
        }
        else
        {
            bool occupiesAnyBuildableCell = pieceCells.Any(cell => {
                GridObject gridObj = grid.GetGridObject(cell.x, cell.y);
                return gridObj != null && gridObj.IsBuildable();
            });

            if (occupiesAnyBuildableCell)
            {
                canPlaceOnGrid = false;
                canPlaceOffGrid = false;
            }
            else
            {
                canPlaceOffGrid = OffGridManager.CanPlacePiece(_heldPiece, origin);
            }
        }
    }

    private void CheckForWin()
    {
        if (_isLevelComplete) return;
        float fillPercentage = GridBuildingSystem.Instance.CalculateGridFillPercentage();
        if (Mathf.Approximately(fillPercentage, 100f))
        {
            _isLevelComplete = true;
            GameManager.Instance.OnLevelComplete();
        }
    }
}