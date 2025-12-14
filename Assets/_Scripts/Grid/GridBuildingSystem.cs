using System.Collections.Generic;
using UnityEngine;
using System.Linq;

public class GridBuildingSystem : MonoBehaviour
{
    public static GridBuildingSystem Instance { get; private set; }

    [SerializeField] private LayerMask whatIsGround;
    private GridDataSO gridData;
    private GridXZ<GridObject> grid;

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
        PlacedObject placedObject = piece.GetComponent<PlacedObject>();
        if (placedObject == null) placedObject = piece.gameObject.AddComponent<PlacedObject>();

        placedObject.PlacedObjectTypeSO = piece.PieceTypeSO;
        placedObject.Origin = origin;
        placedObject.Direction = dir;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            GridObject obj = grid.GetGridObject(gridPosition.x, gridPosition.y);
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
        PlacedObject placedObject = piece.GetComponent<PlacedObject>();
        // Також перевіряємо InfrastructureComponent, якщо PlacedObjectComponent null
        if (placedObject == null) placedObject = piece.InfrastructureComponent;
        if (placedObject == null) placedObject = piece.PlacedObjectComponent;

        if (placedObject == null || piece.PieceTypeSO == null) return;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (IsValidGridPosition(gridPosition.x, gridPosition.y))
            {
                GridObject obj = grid.GetGridObject(gridPosition.x, gridPosition.y);
                if (isTool)
                {
                    if (obj.GetInfrastructureObject() == placedObject)
                    {
                        obj.ClearInfrastructureObject();
                        obj.SetLocked(true); // Забираємо тулз -> Блокуємо клітинку
                    }
                }
                else
                {
                    if (obj.GetPlacedObject() == placedObject)
                    {
                        obj.ClearPlacedObject();
                    }
                }
            }
        }

        // Не знищуємо компонент PlacedObject відразу, якщо це в контексті гри, 
        // бо він може знадобитися для логіки. Але очищаємо посилання в piece.
        // piece.SetPlaced(null); // Це робиться в командах/менеджері
    }

    public bool CanPickUpToolWithPassengers(PuzzlePiece tool, out List<PuzzlePiece> passengers)
    {
        passengers = new List<PuzzlePiece>();
        PlacedObject infraComp = tool.InfrastructureComponent;
        if (infraComp == null) infraComp = tool.GetComponent<PlacedObject>();

        if (!tool.IsPlaced || infraComp == null) return true;

        List<Vector2Int> toolCells = infraComp.GetGridPositionList();
        HashSet<Vector2Int> toolCellSet = new HashSet<Vector2Int>(toolCells);
        HashSet<PuzzlePiece> uniqueObjectsOnTop = new HashSet<PuzzlePiece>();

        foreach (var cell in toolCells)
        {
            if (!IsValidGridPosition(cell.x, cell.y)) continue;
            GridObject obj = grid.GetGridObject(cell.x, cell.y);
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
        if (grid == null) return 0f;
        int totalRequiredCells = 0;
        int occupiedRequiredCells = 0;

        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int z = 0; z < grid.GetHeight(); z++)
            {
                GridObject gridObject = grid.GetGridObject(x, z);
                bool isTargetCell = gridObject.IsBuildable() || gridObject.IsLocked();
                if (isTargetCell)
                {
                    totalRequiredCells++;
                    bool hasCat = gridObject.IsOccupied() &&
                                  gridObject.GetPlacedObject() != null &&
                                  gridObject.GetPlacedObject().PlacedObjectTypeSO.category == PlacedObjectTypeSO.ItemCategory.Character;
                    if (hasCat) occupiedRequiredCells++;
                }
            }
        }
        return totalRequiredCells == 0 ? 0f : (float)occupiedRequiredCells / totalRequiredCells * 100f;
    }

    public GridXZ<GridObject> GetGrid() => grid;

    public bool IsValidGridPosition(int x, int z)
    {
        if (grid == null) return false;
        return x >= 0 && x < grid.GetWidth() && z >= 0 && z < grid.GetHeight();
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