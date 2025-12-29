using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GridBuildingSystem : MonoBehaviour
{
    public static GridBuildingSystem Instance { get; private set; }

    [SerializeField] private LayerMask whatIsGround;
    private GridDataSO gridData;
    private GridXZ<GridObject> grid;
    public PuzzleBoard ActiveBoard => activeBoard;
    private PuzzleBoard activeBoard;

    public void SetActiveBoard(PuzzleBoard board)
    {
        this.activeBoard = board;
        this.grid = board.Grid;
        if (CameraController.Instance != null) CameraController.Instance.FocusOnBoard(board);
    }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void InitializeGrid(GridDataSO data)
    {
        if (grid != null)
        {
            grid.ClearDebugText();
            grid = null;
        }

        this.gridData = data;
        if (gridData == null)
        {
            grid = new GridXZ<GridObject>(10, 10, 1f, Vector3.zero, (g, x, z) => new GridObject(g, x, z));
        }
        else
        {
            grid = new GridXZ<GridObject>(gridData.width, gridData.height, gridData.cellSize, Vector3.zero, (g, x, z) => new GridObject(g, x, z));

            for (int x = 0; x < grid.GetWidth(); x++)
            {
                for (int z = 0; z < grid.GetHeight(); z++)
                {
                    bool isBuildable = gridData.buildableCells.Contains(new Vector2Int(x, z));
                    grid.GetGridObject(x, z).SetBuildable(isBuildable);
                }
            }

            if (gridData.lockedCells != null)
            {
                foreach (var lockedPos in gridData.lockedCells)
                {
                    if (IsValidGridPosition(lockedPos.x, lockedPos.y))
                    {
                        grid.GetGridObject(lockedPos.x, lockedPos.y).SetLocked(true);
                    }
                }
            }
        }
    }

    public void ClearGrid()
    {
        if (grid != null)
        {
            grid.ClearDebugText();
            grid = null;
        }
        grid = new GridXZ<GridObject>(0, 0, 1f, Vector3.zero, (g, x, z) => new GridObject(g, x, z));
    }

    public bool CanPlacePiece(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        List<Vector2Int> gridPositionList = piece.PieceTypeSO.GetGridPositionsList(origin, dir);
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (!IsValidGridPosition(gridPosition.x, gridPosition.y)) return false;

            GridObject gridObject = grid.GetGridObject(gridPosition.x, gridPosition.y);

            // Ігноруємо колізію з самим собою (якщо ми перевіряємо те саме місце, де вже стоїмо)
            PlacedObject currentPlaced = gridObject.GetPlacedObject();
            if (currentPlaced != null && currentPlaced.GetComponent<PuzzlePiece>() == piece)
            {
                continue;
            }

            PlacedObject currentInfra = gridObject.GetInfrastructureObject();
            if (currentInfra != null && currentInfra.GetComponent<PuzzlePiece>() == piece)
            {
                continue;
            }

            if (isTool)
            {
                if (!gridObject.IsLocked()) return false; // Тулз ставиться тільки на Locked
                if (gridObject.HasInfrastructure()) return false;
                if (gridObject.IsOccupied()) return false; // Не можна ставити тулз під кота
            }
            else
            {
                if (!gridObject.CanBuild()) return false;
            }
        }
        return true;
    }

    public PlacedObject PlacePieceOnGrid(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        return PlacePieceOnGridExplicit(activeBoard, piece, origin, dir);
    }

    public PlacedObject PlacePieceOnGridExplicit(PuzzleBoard targetBoard, PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        if (targetBoard == null) return null;
        var targetGrid = targetBoard.Grid;

        PlacedObject placedObject = piece.GetComponent<PlacedObject>();
        if (placedObject == null) placedObject = piece.gameObject.AddComponent<PlacedObject>();

        placedObject.PlacedObjectTypeSO = piece.PieceTypeSO;
        placedObject.Origin = origin;
        placedObject.Direction = dir;
        
        // ВАЖЛИВО: Прив'язуємо фігуру до дошки
        piece.OwnerBoard = targetBoard;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            GridObject obj = targetGrid.GetGridObject(gridPosition.x, gridPosition.y);
            if (obj == null) continue;

            if (isTool)
            {
                obj.SetInfrastructureObject(placedObject);
                obj.SetLocked(false); // Тулз розблоковує клітинку
            }
            else
            {
                obj.SetPlacedObject(placedObject);
            }
        }
        
        return placedObject;
    }

    public void RemovePieceFromGrid(PuzzlePiece piece)
    {
        // 1. Спробуємо взяти дошку з самої фігури
        PuzzleBoard targetBoard = piece.OwnerBoard;
        
        // 2. Якщо немає, спробуємо знайти дошку за координатами фігури (повільніше, але надійно)
        if (targetBoard == null && GridBuildingSystem.Instance.ActiveBoard != null)
        {
             targetBoard = GridBuildingSystem.Instance.ActiveBoard;
             // Можна додати перевірку, чи фігура дійсно на цьому гріді, але поки вважаємо ActiveBoard дефолтним
        }

        RemovePieceFromGridExplicit(targetBoard, piece);
    }

    public void RemovePieceFromGridExplicit(PuzzleBoard targetBoard, PuzzlePiece piece)
    {
        if (targetBoard == null || targetBoard.Grid == null) return;
        var targetGrid = targetBoard.Grid;

        PlacedObject placedObject = piece.GetComponent<PlacedObject>();
        if (placedObject == null) placedObject = piece.InfrastructureComponent;
        if (placedObject == null) placedObject = piece.PlacedObjectComponent;

        if (placedObject == null || piece.PieceTypeSO == null) return;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (IsValidGridPosition(gridPosition.x, gridPosition.y))
            {
                GridObject obj = targetGrid.GetGridObject(gridPosition.x, gridPosition.y);
                
                if (isTool)
                {
                    var infrastructure = obj.GetInfrastructureObject();
                    // Robust check: if it's a tool, we clear it and RELOCK regardless of minor mismatch
                    // if the grid says it's this piece or IF the grid has NO infrastructure but we think it should.
                    bool isMismatched = (infrastructure != null && infrastructure != placedObject && infrastructure.GetComponent<PuzzlePiece>() != piece);
                    
                    if (!isMismatched)
                    {
                        obj.ClearInfrastructureObject();
                        obj.SetLocked(true); // Always relock
                    }
                    else
                    {
                        Debug.LogWarning($"[Grid] RemovePiece (Tool): Mismatch at {gridPosition} on board {targetBoard.boardId}. Grid has {infrastructure}, removing {placedObject}. Skipping relock for safety.");
                    }
                }
                else
                {
                    var placed = obj.GetPlacedObject();
                    bool isSameObj = (placed == placedObject || (placed != null && placed.GetComponent<PuzzlePiece>() == piece));

                    if (isSameObj)
                    {
                        obj.ClearPlacedObject();
                    }
                    else if (placed != null)
                    {
                         Debug.LogWarning($"[Grid] RemovePiece: Mismatch at {gridPosition} on board {targetBoard.boardId}. Grid has {placed}, removing {placedObject}");
                    }
                }
            }
        }

        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    public bool CanPickUpToolWithPassengers(PuzzlePiece tool, out List<PuzzlePiece> passengers)
    {
        passengers = new List<PuzzlePiece>();
        
        // Use the board the tool belongs to
        PuzzleBoard board = tool.OwnerBoard != null ? tool.OwnerBoard : activeBoard;
        if (board == null || board.Grid == null) return true;
        var toolGrid = board.Grid;

        PlacedObject infraComp = tool.InfrastructureComponent;
        if (infraComp == null) infraComp = tool.GetComponent<PlacedObject>();

        if (!tool.IsPlaced || infraComp == null) return true;

        List<Vector2Int> toolCells = infraComp.GetGridPositionList();
        HashSet<Vector2Int> toolCellSet = new HashSet<Vector2Int>(toolCells);
        HashSet<PuzzlePiece> uniqueObjectsOnTop = new HashSet<PuzzlePiece>();

        foreach (var cell in toolCells)
        {
            if (!IsValidGridPosition(toolGrid, cell.x, cell.y)) continue;
            GridObject obj = toolGrid.GetGridObject(cell.x, cell.y);
            if (obj.IsOccupied())
            {
                PlacedObject po = obj.GetPlacedObject();
                if (po != null)
                {
                    PuzzlePiece pieceOnTop = po.GetComponent<PuzzlePiece>();
                    if (pieceOnTop != null) uniqueObjectsOnTop.Add(pieceOnTop);
                }
            }
        }

        foreach (var piece in uniqueObjectsOnTop)
        {
            if (piece.PlacedObjectComponent == null) continue;
            List<Vector2Int> catCells = piece.PlacedObjectComponent.GetGridPositionList();
            bool isFullySupported = true;
            foreach (var catCell in catCells)
            {
                if (!toolCellSet.Contains(catCell)) { isFullySupported = false; break; }
            }
            if (isFullySupported) passengers.Add(piece);
            else return false;
        }
        return true;
    }

    public float CalculateGridFillPercentage()
    {
        if (activeBoard == null || activeBoard.Grid == null) return 0f;
        var targetGrid = activeBoard.Grid;
        
        int totalRequiredCells = 0;
        int occupiedRequiredCells = 0;

        for (int x = 0; x < targetGrid.GetWidth(); x++)
        {
            for (int z = 0; z < targetGrid.GetHeight(); z++)
            {
                GridObject gridObject = targetGrid.GetGridObject(x, z);
                // A cell is a target if it's buildable OR if it's currently locked OR if it HAS infrastructure (meaning it was unlocked by a tool)
                bool isTargetCell = gridObject.IsBuildable() || gridObject.IsLocked() || gridObject.HasInfrastructure();
                if (isTargetCell)
                {
                    totalRequiredCells++;
                    
                    bool hasRequiredCat = false;
                    PlacedObject po = gridObject.GetPlacedObject();
                    if (po != null)
                    {
                        PuzzlePiece piece = po.GetComponent<PuzzlePiece>();
                        if (piece != null && !piece.IsObstacle && 
                            piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape)
                        {
                            hasRequiredCat = true;
                        }
                    }
                    
                    if (hasRequiredCat) occupiedRequiredCells++;
                }
            }
        }
        return totalRequiredCells == 0 ? 0f : (float)occupiedRequiredCells / totalRequiredCells * 100f;
    }

    public GridXZ<GridObject> GetGrid() => grid;

    public bool IsValidGridPosition(int x, int z)
    {
        return IsValidGridPosition(grid, x, z);
    }

    public bool IsValidGridPosition(GridXZ<GridObject> targetGrid, int x, int z)
    {
        if (targetGrid == null) return false;
        return x >= 0 && x < targetGrid.GetWidth() && z >= 0 && z < targetGrid.GetHeight();
    }

    public Vector3 GetMouseWorldPosition()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        return Physics.Raycast(ray, out RaycastHit hit, 100f, whatIsGround) ? hit.point : Vector3.zero;
    }

    public Vector3 GetMouseWorldSnappedPosition()
    {
        Vector3 mousePosition = GetMouseWorldPosition();
        grid.GetXZ(mousePosition, out int x, out int z);
        return grid.GetWorldPosition(x, z);
    }
}
