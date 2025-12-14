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

            if (isTool)
            {
                // Тулз може ставати ТІЛЬКИ на заблоковані клітинки.
                if (!gridObject.IsLocked()) return false;
                // І якщо там ще немає іншого тулза
                if (gridObject.HasInfrastructure()) return false;
            }
            else
            {
                // Звичайні фігури потребують Buildable і Free
                if (!gridObject.CanBuild())
                {
                    return false;
                }
            }
        }
        return true;
    }

    public PlacedObject PlacePieceOnGrid(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        // Перевіряємо, чи вже є компонент, щоб не дублювати
        PlacedObject placedObject = piece.GetComponent<PlacedObject>();
        if (placedObject == null)
        {
            placedObject = piece.gameObject.AddComponent<PlacedObject>();
        }

        placedObject.PlacedObjectTypeSO = piece.PieceTypeSO;
        placedObject.Origin = origin;
        placedObject.Direction = dir;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (!IsValidGridPosition(gridPosition.x, gridPosition.y)) continue;

            GridObject obj = grid.GetGridObject(gridPosition.x, gridPosition.y);

            if (isTool)
            {
                obj.SetInfrastructureObject(placedObject);
                obj.SetLocked(false);
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
        if (piece == null) return;

        // Визначаємо компонент (звичайний чи інфраструктура)
        PlacedObject placedObject = null;

        if (piece.IsPlaced)
        {
            placedObject = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent : piece.InfrastructureComponent;
        }

        // --- FIX: Якщо piece.IsPlaced каже false, але у об'єкта все ще висить компонент PlacedObject (баг розсинхрону) ---
        if (placedObject == null)
        {
            placedObject = piece.GetComponent<PlacedObject>();
        }

        if (placedObject == null) return; // Немає чого видаляти

        // --- FIX: Безпечне отримання списку позицій ---
        if (placedObject.PlacedObjectTypeSO == null)
        {
            // Якщо SO втрачено, ми не знаємо які клітинки чистити. 
            // Видаляємо компонент і виходимо.
            DestroyPlacedObjectComponent(placedObject);
            return;
        }

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        bool isTool = placedObject.PlacedObjectTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (IsValidGridPosition(gridPosition.x, gridPosition.y))
            {
                GridObject obj = grid.GetGridObject(gridPosition.x, gridPosition.y);

                if (isTool)
                {
                    // Перевіряємо, чи це дійсно той самий об'єкт, щоб не видалити чужий
                    if (obj.GetInfrastructureObject() == placedObject)
                    {
                        obj.ClearInfrastructureObject();
                        obj.SetLocked(true);
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

        DestroyPlacedObjectComponent(placedObject);
    }

    private void DestroyPlacedObjectComponent(PlacedObject obj)
    {
        if (Application.isEditor && !Application.isPlaying)
        {
            Object.DestroyImmediate(obj);
        }
        else
        {
            Object.Destroy(obj);
        }
    }

    // --- Перевірка, чи можна підняти тулз разом з котами ---
    public bool CanPickUpToolWithPassengers(PuzzlePiece tool, out List<PuzzlePiece> passengers)
    {
        passengers = new List<PuzzlePiece>();

        if (!tool.IsPlaced || tool.InfrastructureComponent == null) return true;

        List<Vector2Int> toolCells = tool.InfrastructureComponent.GetGridPositionList();
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
                if (!toolCellSet.Contains(catCell))
                {
                    isFullySupported = false;
                    break;
                }
            }

            if (isFullySupported)
            {
                passengers.Add(piece);
            }
            else
            {
                // Є кіт, який стоїть на тулзі частково
                return false;
            }
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

                // Заповнювати треба buildable або locked (бо locked розблокуються і стануть buildable)
                bool isTargetCell = gridObject.IsBuildable() || gridObject.IsLocked();

                if (isTargetCell)
                {
                    totalRequiredCells++;

                    // Перевіряємо, чи є там кіт (персонаж)
                    // Тулзи не рахуються як "заповнення", вони лише засіб
                    bool hasCat = gridObject.IsOccupied() &&
                                  gridObject.GetPlacedObject() != null &&
                                  gridObject.GetPlacedObject().PlacedObjectTypeSO.category == PlacedObjectTypeSO.ItemCategory.Character;

                    if (hasCat)
                    {
                        occupiedRequiredCells++;
                    }
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