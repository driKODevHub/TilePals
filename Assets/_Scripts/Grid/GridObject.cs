using UnityEngine;

public class GridObject
{
    private GridXZ<GridObject> grid;
    private int x;
    private int z;
    private PlacedObject placedObject;
    private bool isBuildable;

    public GridObject(GridXZ<GridObject> grid, int x, int z)
    {
        this.grid = grid;
        this.x = x;
        this.z = z;
        this.isBuildable = true; // Default to buildable
    }

    public void SetPlacedObject(PlacedObject placedObject)
    {
        this.placedObject = placedObject;
        grid.TriggerGridObjectChanged(x, z);
    }

    public PlacedObject GetPlacedObject()
    {
        return placedObject;
    }

    public void ClearPlacedObject()
    {
        if (placedObject != null)
            placedObject = null;
        grid.TriggerGridObjectChanged(x, z);
    }

    public void SetBuildable(bool buildable)
    {
        isBuildable = buildable;
        grid.TriggerGridObjectChanged(x, z);
    }

    public bool CanBuild()
    {
        return placedObject == null && isBuildable;
    }

    public bool IsOccupied()
    {
        return placedObject != null;
    }

    public bool IsBuildable()
    {
        return isBuildable;
    }

    public Vector2Int GetGridPosition()
    {
        return new Vector2Int(x, z);
    }

    // Метод ToString(), перенесений з GridBuildingSystem
    public override string ToString()
    {
        string colorTag = placedObject != null ? "<color=red>" : (isBuildable ? "<color=white>" : "<color=black>");

        string objectInfo = "";
        if (placedObject != null)
        {
            string objectName = placedObject.name;
            if (objectName.EndsWith("(Clone)"))
            {
                objectName = objectName.Substring(0, objectName.Length - "(Clone)".Length);
            }
            objectInfo = $"\n\n<size=40>{objectName}</size>";
        }

        return $"{colorTag}<size=50>{x}, {z}</size>{objectInfo}</color>";
    }
}