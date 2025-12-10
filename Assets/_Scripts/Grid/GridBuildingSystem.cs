using System.Collections.Generic;
using UnityEngine;

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
        bool isTool = piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Tool;

        foreach (Vector2Int gridPosition in gridPositionList)
        {
            // Перевірка меж сітки
            if (!IsValidGridPosition(gridPosition.x, gridPosition.y)) return false;

            GridObject gridObject = grid.GetGridObject(gridPosition.x, gridPosition.y);

            // Якщо це інструмент, він може ставати на "неактивні" клітинки, щоб їх розблокувати
            if (isTool)
            {
                // Інструмент не може ставати тільки на вже зайняті клітинки
                if (gridObject.IsOccupied()) return false;
            }
            else
            {
                // Звичайні фігури потребують Buildable і Free
                if (gridObject == null || !gridObject.CanBuild())
                {
                    return false;
                }
            }
        }
        return true;
    }

    public PlacedObject PlacePieceOnGrid(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir dir)
    {
        PlacedObject placedObject = piece.gameObject.AddComponent<PlacedObject>();
        placedObject.PlacedObjectTypeSO = piece.PieceTypeSO;
        placedObject.Origin = origin;
        placedObject.Direction = dir;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        foreach (Vector2Int gridPosition in gridPositionList)
        {
            grid.GetGridObject(gridPosition.x, gridPosition.y).SetPlacedObject(placedObject);
        }
        return placedObject;
    }

    public void RemovePieceFromGrid(PuzzlePiece piece)
    {
        PlacedObject placedObject = piece.PlacedObjectComponent;
        if (placedObject == null) return;

        List<Vector2Int> gridPositionList = placedObject.GetGridPositionList();
        foreach (Vector2Int gridPosition in gridPositionList)
        {
            if (IsValidGridPosition(gridPosition.x, gridPosition.y))
            {
                grid.GetGridObject(gridPosition.x, gridPosition.y).ClearPlacedObject();
            }
        }

        if (Application.isEditor && !Application.isPlaying)
        {
            Object.DestroyImmediate(placedObject);
        }
        else
        {
            Object.Destroy(placedObject);
        }
    }

    public float CalculateGridFillPercentage()
    {
        if (grid == null) return 0f;
        int totalBuildableCells = 0;
        int occupiedBuildableCells = 0;
        for (int x = 0; x < grid.GetWidth(); x++)
        {
            for (int z = 0; z < grid.GetHeight(); z++)
            {
                GridObject gridObject = grid.GetGridObject(x, z);
                if (gridObject.IsBuildable())
                {
                    totalBuildableCells++;
                    if (gridObject.IsOccupied())
                    {
                        // Вважаємо зайнятим, тільки якщо це НЕ інструмент (інструменти тимчасові)
                        var obj = gridObject.GetPlacedObject();
                        if (obj != null && obj.PlacedObjectTypeSO.category != PlacedObjectTypeSO.ItemCategory.Tool)
                        {
                            occupiedBuildableCells++;
                        }
                    }
                }
            }
        }
        return totalBuildableCells == 0 ? 0f : (float)occupiedBuildableCells / totalBuildableCells * 100f;
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