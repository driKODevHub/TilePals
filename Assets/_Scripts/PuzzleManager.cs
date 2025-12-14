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
    [SerializeField] private Material invalidPlacementMaterial;

    [Header("Physics Throw Settings")]
    [SerializeField] private float throwForceMultiplier = 0.5f;
    [SerializeField] private float maxThrowVelocity = 15f;
    [SerializeField] private float minThrowVelocity = 2f;

    [Header("Interaction Settings")]
    [SerializeField] private float dragThreshold = 20f;
    [SerializeField] private float clickTimeThreshold = 0.25f;

    [Header("Layers")]
    [SerializeField] private LayerMask pieceLayer;
    [SerializeField] private LayerMask offGridPlaneLayer;

    // State
    private PuzzlePiece _heldPiece;
    private PuzzlePiece _hoveredPiece;

    private PuzzlePiece _potentialInteractionPiece;
    private Vector2 _clickStartPos;
    private float _clickStartTime;
    private bool _isPettingActive;

    private Vector3 _initialPiecePosition;
    private Quaternion _initialPieceRotation;
    private bool _isLevelComplete = false;
    private List<PuzzlePiece> _piecesBeingFlownOver = new List<PuzzlePiece>();
    private List<PiecePersonality> _allPersonalities = new List<PiecePersonality>();

    // Mouse Velocity
    private Vector2 _lastMousePos;
    private Vector3 _lastWorldMousePos;
    private Vector3 _currentThrowVelocity;
    private float _currentMouseSpeed;

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
        CalculateMouseVelocity();

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

    private void CalculateMouseVelocity()
    {
        if (inputReader == null) return;

        Vector2 currentPos = inputReader.MousePosition;
        float dist = (currentPos - _lastMousePos).magnitude;
        _currentMouseSpeed = dist / Time.deltaTime;
        _lastMousePos = currentPos;

        Ray ray = Camera.main.ScreenPointToRay(currentPos);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer))
        {
            Vector3 currentWorldPos = hit.point;
            if (_lastWorldMousePos != Vector3.zero)
            {
                Vector3 velocity = (currentWorldPos - _lastWorldMousePos) / Time.deltaTime;
                _currentThrowVelocity = Vector3.Lerp(_currentThrowVelocity, velocity, Time.deltaTime * 10f);
            }
            _lastWorldMousePos = currentWorldPos;
        }
    }

    #region Input Handlers & Interaction Logic

    private void HandleHoverLogic()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (_isLevelComplete) return;
        if (_potentialInteractionPiece != null) return;

        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, pieceLayer);
        PuzzlePiece bestCandidate = null;

        if (hits.Length > 0)
        {
            float bestScore = -1f;

            foreach (var hit in hits)
            {
                PuzzlePiece p = hit.collider.GetComponentInParent<PuzzlePiece>();
                if (p == null) continue;

                float score = 0f;
                var cat = p.PieceTypeSO.category;

                // Перевірка: якщо це пасажир, він має високий пріоритет тільки якщо ми хочемо його зняти?
                // Ні, якщо це пасажир тулза, ми скоріше хочемо підняти тулз, ХІБА ЩО ми клікнули прямо в кота.
                // Але в поточній логіці, якщо ми клікаємо на тулз з котами - ми беремо тулз.
                // Якщо ми хочемо взяти кота, ми маємо клікнути на нього.

                if (p.transform.parent != null && p.transform.parent.GetComponentInParent<PuzzlePiece>() != null)
                {
                    // Це пасажир або предмет у роті
                    score = 100f;
                }
                else if (cat == PlacedObjectTypeSO.ItemCategory.Toy ||
                         cat == PlacedObjectTypeSO.ItemCategory.Food ||
                         cat == PlacedObjectTypeSO.ItemCategory.Tool)
                {
                    score = 50f;
                }
                else
                {
                    score = 10f;
                }

                score -= hit.distance * 0.1f;

                if (score > bestScore)
                {
                    bestScore = score;
                    bestCandidate = p;
                }
            }
        }

        if (bestCandidate != _hoveredPiece)
        {
            if (_hoveredPiece != null) _hoveredPiece.SetOutline(false);
            _hoveredPiece = bestCandidate;
            if (_hoveredPiece != null) _hoveredPiece.SetOutline(true);
        }
    }

    private void HandleClickStart()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;
        if (_isLevelComplete) return;
        if (Keyboard.current != null && Keyboard.current.altKey.isPressed) return;

        if (_heldPiece == null && _hoveredPiece != null)
        {
            // Якщо це Тулз, перевіряємо чи можна його взяти (або з котами, або без)
            if (GridBuildingSystem.Instance != null && _hoveredPiece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                if (!GridBuildingSystem.Instance.CanPickUpToolWithPassengers(_hoveredPiece, out _))
                {
                    if (_hoveredPiece.Visuals != null) _hoveredPiece.Visuals.PlayPlaceFailed();
                    return;
                }
            }

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
                StartPettingOrInteraction();
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
            if (TryGiveHeldItemToHoveredCat()) return;
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
                if (_hoveredPiece == _potentialInteractionPiece) PickUpPiece(_potentialInteractionPiece);
            }
            else
            {
                if (_isPettingActive) StopPetting();
                else if (_hoveredPiece == _potentialInteractionPiece) PickUpPiece(_potentialInteractionPiece);
            }

            _potentialInteractionPiece = null;
            _isPettingActive = false;
        }
    }

    private void StartPettingOrInteraction()
    {
        _isPettingActive = true;
        PersonalityEventManager.RaisePettingStart(_potentialInteractionPiece);
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

    #region Game Logic

    private bool TryGiveHeldItemToHoveredCat()
    {
        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        RaycastHit[] hits = Physics.RaycastAll(ray, 100f, pieceLayer);

        foreach (var hit in hits)
        {
            PuzzlePiece targetPiece = hit.collider.GetComponentInParent<PuzzlePiece>();
            if (targetPiece != null && targetPiece != _heldPiece)
            {
                if (targetPiece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Character)
                {
                    var personality = targetPiece.GetComponent<PiecePersonality>();
                    if (personality != null)
                    {
                        var itemType = _heldPiece.PieceTypeSO.category;
                        if (itemType == PlacedObjectTypeSO.ItemCategory.Toy || itemType == PlacedObjectTypeSO.ItemCategory.Food)
                        {
                            if (personality.TryReceiveItem(_heldPiece))
                            {
                                _heldPiece.SetOutlineLocked(false);
                                _heldPiece = null;
                                return true;
                            }
                        }
                    }
                }
            }
        }
        return false;
    }

    private void PickUpPiece(PuzzlePiece piece)
    {
        if (piece == null) return;

        // Перевірка батьківства (рот або пасажир)
        if (piece.transform.parent != null)
        {
            PuzzlePiece parentPiece = piece.transform.parent.GetComponentInParent<PuzzlePiece>();
            if (parentPiece != null)
            {
                // 1. Предмет у роті?
                PuzzlePiece detached = parentPiece.DetachItem();
                if (detached == piece)
                {
                    piece = detached;
                }
                // 2. Пасажир тулза? (Тулз не віддає пасажирів через DetachItem, вони просто діти)
                else if (parentPiece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
                {
                    // Це пасажир. Ми його забираємо.
                    // Важливо: він не має PlacedObject компонента, бо він дитина.
                    // Але нам треба його "відчепити" від тулза логічно.
                    parentPiece.StoredPassengers.Remove(piece);
                    piece.transform.SetParent(null); // Від'єднуємо від тулза
                    piece.EnablePhysics(Vector3.zero); // Вмикаємо фізику/коллайдери тимчасово, щоб працював як окремий об'єкт

                    // Він ще не на гріді, тому RemovePieceFromGrid не потрібен для нього.
                }
            }
        }

        if (lockPiecesOnLevelComplete && _isLevelComplete && piece.IsPlaced) return;
        if (GridBuildingSystem.Instance == null || GridBuildingSystem.Instance.GetGrid() == null) return;

        // --- ЛОГІКА ТУЛЗА З ПАСАЖИРАМИ ---
        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid && piece.IsPlaced)
        {
            if (GridBuildingSystem.Instance.CanPickUpToolWithPassengers(piece, out List<PuzzlePiece> passengers))
            {
                foreach (var passenger in passengers)
                {
                    // Видаляємо пасажирів з гріда
                    GridBuildingSystem.Instance.RemovePieceFromGrid(passenger);
                    passenger.SetPlaced(null);
                    // Додаємо їх у внутрішній список тулза
                    piece.AddPassenger(passenger);
                }
            }
            else
            {
                // Не можна підняти (хтось вилазить)
                if (piece.Visuals != null) piece.Visuals.PlayPlaceFailed();
                return;
            }
        }
        // ---------------------------------

        _heldPiece = piece;
        _heldPiece.SetOutlineLocked(true);
        _heldPiece.DisablePhysics();

        if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayPickup();

        _initialPiecePosition = piece.transform.position;
        _initialPieceRotation = piece.transform.rotation;
        _lastSnappedGridOrigin = null;

        Ray ray = Camera.main.ScreenPointToRay(inputReader.MousePosition);
        Vector3 hitPoint = Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer) ? hit.point : piece.transform.position;

        if (piece.IsPlaced || piece.IsOffGrid)
        {
            float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
            Vector2Int origin = Vector2Int.zero;
            if (piece.IsPlaced)
            {
                origin = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent.Origin : (piece.InfrastructureComponent != null ? piece.InfrastructureComponent.Origin : Vector2Int.zero);
            }
            else
            {
                origin = piece.OffGridOrigin;
            }

            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
            Vector3 pieceOriginWorld = piece.transform.position - new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;

            GridBuildingSystem.Instance.GetGrid().GetXZ(hitPoint, out int clickX, out int clickZ);
            GridBuildingSystem.Instance.GetGrid().GetXZ(pieceOriginWorld, out int originX, out int originZ);

            piece.ClickOffset = new Vector2Int(clickX - originX, clickZ - originZ);
        }
        else
        {
            piece.ClickOffset = Vector2Int.zero;
        }

        // Очистка стану перед переміщенням
        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
            piece.SetInfrastructure(null);
        }
        else if (piece.IsOffGrid)
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

            CanPlaceHeldPiece(logicalOrigin, out bool canOnGrid, out bool canOffGrid);
            bool isValid = canOnGrid || canOffGrid;
            _heldPiece.SetInvalidPlacementVisual(!isValid);

            if (isMouseOverGrid)
            {
                if (_lastSnappedGridOrigin == null || _lastSnappedGridOrigin.Value != logicalOrigin)
                {
                    if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayGridSnap();
                    _lastSnappedGridOrigin = logicalOrigin;
                }
            }
            else
            {
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
            ICommand cmd = new PlaceCommand(_heldPiece, origin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);
            if (cmd.Execute())
            {
                if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayPlaceSuccess();

                if (_heldPiece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
                {
                    _heldPiece.SetInfrastructure(_heldPiece.GetComponent<PlacedObject>());
                    // Розпаковуємо пасажирів на грід
                    PlacePassengersOnGrid(_heldPiece);
                }

                FinalizeDrop(cmd);
                CheckForWin();
            }
        }
        else if (canOffGrid)
        {
            ICommand cmd = new OffGridPlaceCommand(_heldPiece, origin, _heldPiece.CurrentDirection, _initialPiecePosition, _initialPieceRotation);
            PuzzlePiece droppedPiece = _heldPiece;

            if (cmd.Execute())
            {
                if (droppedPiece.Visuals != null) droppedPiece.Visuals.PlayDrop();

                // Якщо це тулз з пасажирами, вони залишаються на ньому (в OffGridManager немає поняття стеку)
                // Але візуально вони діти, тому їдуть з ним.

                float speed = _currentThrowVelocity.magnitude;
                bool shouldThrow = speed > minThrowVelocity && droppedPiece.PieceTypeSO.usePhysics;

                if (shouldThrow)
                {
                    Vector3 throwVel = Vector3.ClampMagnitude(_currentThrowVelocity * throwForceMultiplier, maxThrowVelocity);
                    throwVel.y = Mathf.Abs(throwVel.y) + 2f;
                    droppedPiece.EnablePhysics(throwVel);
                }
                else
                {
                    if (droppedPiece.PieceTypeSO.usePhysics) droppedPiece.EnablePhysics(Vector3.zero);
                }

                FinalizeDrop(cmd);
                PersonalityEventManager.RaisePieceDropped(droppedPiece);
            }
        }
        else
        {
            Debug.Log("Invalid placement position.");
            if (_heldPiece.Visuals != null) _heldPiece.Visuals.PlayPlaceFailed();
        }
    }

    private void PlacePassengersOnGrid(PuzzlePiece tool)
    {
        if (tool.StoredPassengers.Count == 0) return;

        var grid = GridBuildingSystem.Instance.GetGrid();
        float cellSize = grid.GetCellSize();

        List<PuzzlePiece> passengers = new List<PuzzlePiece>(tool.StoredPassengers);

        foreach (var p in passengers)
        {
            // Важливо: переносимо пасажира в корінь сцени/ієрархії, 
            // щоб він став незалежним від трансформа тулза
            p.transform.SetParent(tool.transform.parent);

            // Вираховуємо світову позицію де візуально стоїть кіт
            Vector3 worldPos = p.transform.position;
            grid.GetXZ(worldPos, out int x, out int z);
            Vector2Int pOrigin = new Vector2Int(x, z);

            // Враховуємо офсет повороту
            Vector2Int rotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int calculatedOrigin = pOrigin - rotOffset;

            // Спроба поставити
            if (GridBuildingSystem.Instance.CanPlacePiece(p, calculatedOrigin, p.CurrentDirection))
            {
                GridBuildingSystem.Instance.PlacePieceOnGrid(p, calculatedOrigin, p.CurrentDirection);
                p.SetPlaced(p.GetComponent<PlacedObject>());

                // Вирівнюємо ідеально по центру
                Vector3 finalPos = grid.GetWorldPosition(calculatedOrigin.x, calculatedOrigin.y) +
                                   new Vector3(rotOffset.x, 0, rotOffset.y) * cellSize;

                p.UpdateTransform(finalPos, p.transform.rotation);

                // Вмикаємо назад компоненти, якщо треба
                if (p.Movement) p.Movement.enabled = true;
                if (p.PieceCollider) p.PieceCollider.enabled = true;
            }
            else
            {
                Debug.LogError($"Error placing passenger {p.name} at {calculatedOrigin}. Tool placed, but cat is invalid!");
                // Якщо не вийшло поставити - лишаємо його "OffGrid" або просто кидаємо поряд?
                // Поки що просто викидаємо його як фізичний об'єкт
                p.SetOffGrid(false);
                p.EnablePhysics(Vector3.up * 2f);
            }
        }

        tool.StoredPassengers.Clear();
    }

    private void FinalizeDrop(ICommand command)
    {
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
            // Частково на гріді, частково ні - вважаємо OffGrid, якщо не перетинає buildable
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

            if (_heldPiece.StoredPassengers.Contains(piece)) continue;

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