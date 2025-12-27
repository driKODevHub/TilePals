using System.Collections.Generic;
using UnityEngine;

public class GridVisualManager : MonoBehaviour
{
    public static GridVisualManager Instance { get; private set; }

    [Header("Visuals")]
    [SerializeField] private GameObject cellPrefab;
    [SerializeField] private Material activeMaterial;
    [SerializeField] private Material inactiveMaterial;
    [SerializeField] private Material occupiedMaterial;
    [SerializeField] private Material hoveredMaterial;
    [SerializeField] private Material invalidPlacementMaterial;
    [SerializeField] private Material lockedMaterial;
    [SerializeField] private Material hintMaterial;

    private GridXZ<GridObject> grid;
    private GameObject[,] cellVisuals;
    private PuzzlePiece currentlyHeldPiece;
    private HashSet<Vector2Int> highlightedHintCells = new HashSet<Vector2Int>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Не ініціалізуємось тут автоматично, чекаємо команди від LevelLoader або GameManager
        // Але про всяк випадок пробуємо, якщо рівень вже є
        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            ReinitializeVisuals();
        }
    }

    public void ReinitializeVisuals()
    {
        // 1. Очищаємо старі візуали та відписуємось від старого гріда
        ClearVisuals();

        // 2. Отримуємо новий грід
        if (GridBuildingSystem.Instance == null) return;
        grid = GridBuildingSystem.Instance.GetGrid();

        if (grid == null || grid.GetWidth() == 0) return;

        // 3. Створюємо нові візуали
        InitializeCellVisuals();

        // 4. Підписуємось на події
        SubscribeToEvents();

        // 5. Оновлюємо картинку
        RefreshAllCellVisuals();
    }

    private void SubscribeToEvents()
    {
        // Відписуємось про всяк випадок, щоб не дублювати
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.OnPiecePickedUp -= HandlePiecePickedUp;
            PuzzleManager.Instance.OnPieceDropped -= HandlePieceDropped;

            PuzzleManager.Instance.OnPiecePickedUp += HandlePiecePickedUp;
            PuzzleManager.Instance.OnPieceDropped += HandlePieceDropped;
        }

        if (grid != null)
        {
            grid.OnGridObjectChanged -= Grid_OnGridObjectChanged;
            grid.OnGridObjectChanged += Grid_OnGridObjectChanged;
        }
    }

    private void ClearVisuals()
    {
        // Відписуємось від старого гріда (якщо він був)
        if (grid != null)
        {
            grid.OnGridObjectChanged -= Grid_OnGridObjectChanged;
        }

        if (cellVisuals != null)
        {
            foreach (var cell in cellVisuals)
            {
                if (cell != null) Destroy(cell);
            }
        }
        cellVisuals = null;
    }

    private void OnDestroy()
    {
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.OnPiecePickedUp -= HandlePiecePickedUp;
            PuzzleManager.Instance.OnPieceDropped -= HandlePieceDropped;
        }
        if (grid != null) grid.OnGridObjectChanged -= Grid_OnGridObjectChanged;
    }

    private void HandlePiecePickedUp(PuzzlePiece piece) => currentlyHeldPiece = piece;
    private void HandlePieceDropped(PuzzlePiece piece)
    {
        currentlyHeldPiece = null;
        RefreshAllCellVisuals();
    }

    private void InitializeCellVisuals()
    {
        int gridWidth = grid.GetWidth();
        int gridHeight = grid.GetHeight();
        float cellSize = grid.GetCellSize();
        cellVisuals = new GameObject[gridWidth, gridHeight];

        for (int x = 0; x < gridWidth; x++)
        {
            for (int z = 0; z < gridHeight; z++)
            {
                Vector3 worldPosition = grid.GetWorldPosition(x, z);
                GameObject cell = Instantiate(cellPrefab, worldPosition, Quaternion.identity, transform);
                cell.name = $"Cell_{x}_{z}";
                cellVisuals[x, z] = cell;

                // Налаштування розміру квада
                Transform quad = cell.transform.Find("Quad");
                if (quad != null)
                {
                    quad.localPosition = new Vector3(cellSize / 2f, 0, cellSize / 2f);
                    quad.localScale = new Vector3(cellSize, 1, cellSize);
                }
            }
        }
    }

    private void Grid_OnGridObjectChanged(object sender, GridXZ<GridObject>.OnGridObjectChangedEventArgs e)
    {
        UpdateCellVisual(e.x, e.z);
    }

    private void LateUpdate()
    {
        UpdateHoveredCellVisuals();
    }

    public void SetHintCells(IEnumerable<Vector2Int> cells)
    {
        highlightedHintCells.Clear();
        if (cells != null)
        {
            foreach (var c in cells) highlightedHintCells.Add(c);
        }
        RefreshAllCellVisuals();
    }
    public void RefreshAllCellVisuals()
    {
        if (grid == null || cellVisuals == null) return;
        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int z = 0; z < grid.GetHeight(); z++)
            {
                UpdateCellVisual(x, z);
            }
        }
    }

    private void UpdateCellVisual(int x, int z)
    {
        if (cellVisuals == null || x < 0 || x >= cellVisuals.GetLength(0) || z < 0 || z >= cellVisuals.GetLength(1)) return;

        GameObject cellVisual = cellVisuals[x, z];
        if (cellVisual == null) return;

        GridObject gridObject = grid.GetGridObject(x, z);
        GridCellState currentState = GetCellState(gridObject);
        if (highlightedHintCells.Contains(new Vector2Int(x, z)) && (currentState == GridCellState.Active || currentState == GridCellState.Occupied))
        {
            currentState = GridCellState.Hint;
        }
        SetCellMaterial(cellVisual, currentState);
    }

    private void UpdateHoveredCellVisuals()
    {
        if (currentlyHeldPiece == null) return;

        // Оновлюємо весь грід до базового стану, щоб стерти старий ховер
        RefreshAllCellVisuals();

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, LayerMask.GetMask("OffGridPlane")))
        {
            grid.GetXZ(hit.point, out int cursorX, out int cursorZ);

            Vector2Int clickOffset = currentlyHeldPiece.ClickOffset;
            Vector2Int origin = new Vector2Int(cursorX, cursorZ) - clickOffset;

            List<Vector2Int> occupiedPositionsOfGhost = currentlyHeldPiece.PieceTypeSO.GetGridPositionsList(origin, currentlyHeldPiece.CurrentDirection);
            bool canBuildEntireObject = GridBuildingSystem.Instance.CanPlacePiece(currentlyHeldPiece, origin, currentlyHeldPiece.CurrentDirection);

            foreach (var gridPos in occupiedPositionsOfGhost)
            {
                if (GridBuildingSystem.Instance.IsValidGridPosition(gridPos.x, gridPos.y))
                {
                    // Знаходимо візуал
                    if (cellVisuals != null && gridPos.x >= 0 && gridPos.x < cellVisuals.GetLength(0) && gridPos.y >= 0 && gridPos.y < cellVisuals.GetLength(1))
                    {
                        GameObject cellVisual = cellVisuals[gridPos.x, gridPos.y];
                        if (cellVisual != null)
                        {
                            SetCellMaterial(cellVisual, canBuildEntireObject ? GridCellState.Hovered : GridCellState.InvalidPlacement);
                        }
                    }
                }
            }
        }
    }

    private GridCellState GetCellState(GridObject gridObject)
    {
        if (gridObject.IsLocked()) return GridCellState.Locked;
        if (!gridObject.IsBuildable()) return GridCellState.Inactive;
        if (gridObject.IsOccupied()) return GridCellState.Occupied;
        return GridCellState.Active;
    }

    private void SetCellMaterial(GameObject cellVisual, GridCellState state)
    {
        MeshRenderer cellRenderer = cellVisual.transform.GetComponentInChildren<MeshRenderer>();
        if (cellRenderer == null) return;

        Material materialToApply = state switch
        {
            GridCellState.Active => activeMaterial,
            GridCellState.Inactive => inactiveMaterial,
            GridCellState.Occupied => occupiedMaterial,
            GridCellState.Locked => lockedMaterial,
            GridCellState.Hovered => hoveredMaterial,
            GridCellState.InvalidPlacement => invalidPlacementMaterial,
            GridCellState.Hint => hintMaterial,
            _ => activeMaterial,
        };

        if (cellRenderer.sharedMaterial != materialToApply)
        {
            cellRenderer.material = materialToApply;
        }
    }
}
