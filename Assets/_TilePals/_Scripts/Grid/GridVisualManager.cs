using System.Collections.Generic;
using UnityEngine;
using System;

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

    private GridXZ<GridObject> grid;
    private GameObject[,] cellVisuals;
    private PuzzlePiece currentlyHeldPiece;
    private bool isInitialized = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    // --- ЛОГІКА ПОВНІСТЮ ПЕРЕРОБЛЕНА ---
    // Тепер ініціалізація викликається ззовні (з LevelLoader)
    public void ReinitializeVisuals()
    {
        // Спочатку очищуємо старі візуальні елементи, якщо вони є
        if (isInitialized)
        {
            ClearVisuals();
        }
        Initialize();
    }

    private void Initialize()
    {
        grid = GridBuildingSystem.Instance.GetGrid();
        if (grid == null || grid.GetWidth() == 0)
        {
            enabled = false;
            return;
        }

        enabled = true;
        InitializeCellVisuals();

        // Підписуємось на події лише один раз за всю гру
        if (!isInitialized)
        {
            PuzzleManager.Instance.OnPiecePickedUp += PuzzleManager_OnPiecePickedUp;
            PuzzleManager.Instance.OnPieceDropped += PuzzleManager_OnPieceDropped;
        }

        RefreshAllCellVisuals();
        isInitialized = true;
    }

    // Новий метод для очищення старих візуальних елементів
    private void ClearVisuals()
    {
        if (cellVisuals != null)
        {
            for (int x = 0; x < cellVisuals.GetLength(0); x++)
            {
                for (int z = 0; z < cellVisuals.GetLength(1); z++)
                {
                    if (cellVisuals[x, z] != null)
                    {
                        Destroy(cellVisuals[x, z]);
                    }
                }
            }
        }
        cellVisuals = null;

        // Відписуємось від подій старої сітки, щоб уникнути помилок
        if (grid != null)
        {
            grid.OnGridObjectChanged -= Grid_OnGridObjectChanged;
        }
    }

    private void OnDestroy()
    {
        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.OnPiecePickedUp -= PuzzleManager_OnPiecePickedUp;
            PuzzleManager.Instance.OnPieceDropped -= PuzzleManager_OnPieceDropped;
        }
        if (grid != null)
        {
            grid.OnGridObjectChanged -= Grid_OnGridObjectChanged;
        }
    }

    private void PuzzleManager_OnPiecePickedUp(PuzzlePiece piece)
    {
        currentlyHeldPiece = piece;
    }

    private void PuzzleManager_OnPieceDropped(PuzzlePiece piece)
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
                Transform quad = cell.transform.Find("Quad");
                if (quad != null)
                {
                    quad.localPosition = new Vector3(cellSize / 2f, 0, cellSize / 2f);
                    quad.localScale = new Vector3(cellSize, 1, cellSize);
                }
            }
        }
        // Підписуємось на події нової сітки
        grid.OnGridObjectChanged += Grid_OnGridObjectChanged;
    }

    private void Grid_OnGridObjectChanged(object sender, GridXZ<GridObject>.OnGridObjectChangedEventArgs e)
    {
        UpdateCellVisual(e.x, e.z);
    }

    private void LateUpdate()
    {
        UpdateHoveredCellVisuals();
    }

    private void RefreshAllCellVisuals()
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
        if (x < 0 || x >= grid.GetWidth() || z < 0 || z >= grid.GetHeight() || cellVisuals == null) return;
        GridObject gridObject = grid.GetGridObject(x, z);
        GridCellState currentState = GetCellState(gridObject);
        SetCellMaterial(x, z, currentState);
    }

    private void UpdateHoveredCellVisuals()
    {
        RefreshAllCellVisuals();
        if (currentlyHeldPiece == null) return;

        PlacedObjectTypeSO placedObjectTypeSO = currentlyHeldPiece.PieceTypeSO;
        PlacedObjectTypeSO.Dir currentDir = currentlyHeldPiece.CurrentDirection;

        Vector3 rawMouseWorldPosition = GridBuildingSystem.Instance.GetMouseWorldPosition();
        grid.GetXZ(rawMouseWorldPosition, out int originX, out int originZ);
        Vector2Int origin = new Vector2Int(originX, originZ);

        List<Vector2Int> occupiedPositionsOfGhost = placedObjectTypeSO.GetGridPositionsList(origin, currentDir);
        bool canBuildEntireObject = GridBuildingSystem.Instance.CanPlacePiece(currentlyHeldPiece, origin, currentDir);

        foreach (var gridPos in occupiedPositionsOfGhost)
        {
            if (GridBuildingSystem.Instance.IsValidGridPosition(gridPos.x, gridPos.y))
            {
                SetCellMaterial(gridPos.x, gridPos.y, canBuildEntireObject ? GridCellState.Hovered : GridCellState.InvalidPlacement);
            }
        }
    }

    private GridCellState GetCellState(GridObject gridObject)
    {
        if (!gridObject.IsBuildable()) return GridCellState.Inactive;
        if (gridObject.IsOccupied()) return GridCellState.Occupied;
        return GridCellState.Active;
    }

    private void SetCellMaterial(int x, int z, GridCellState state)
    {
        if (x < 0 || x >= grid.GetWidth() || z < 0 || z >= grid.GetHeight() || cellVisuals[x, z] == null) return;
        GameObject cellVisual = cellVisuals[x, z];
        if (cellVisual == null) return;
        MeshRenderer cellRenderer = cellVisual.transform.GetComponentInChildren<MeshRenderer>();
        if (cellRenderer == null) return;

        Material materialToApply = state switch
        {
            GridCellState.Active => activeMaterial,
            GridCellState.Inactive => inactiveMaterial,
            GridCellState.Occupied => occupiedMaterial,
            GridCellState.Hovered => hoveredMaterial,
            GridCellState.InvalidPlacement => invalidPlacementMaterial,
            _ => activeMaterial,
        };
        if (materialToApply != null)
        {
            cellRenderer.material = materialToApply;
        }
    }
}
