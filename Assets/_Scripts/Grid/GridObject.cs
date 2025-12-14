using UnityEngine;

public class GridObject
{
    private GridXZ<GridObject> grid;
    private int x;
    private int z;

    // Основний слот для Котів, Іграшок, Перешкод
    private PlacedObject placedObject;

    // Додатковий слот для Тулзів (підложок), які розширюють рівень
    private PlacedObject infrastructureObject;

    private bool isBuildable;
    private bool isLocked; // Чи потребує клітинка розблокування

    public GridObject(GridXZ<GridObject> grid, int x, int z)
    {
        this.grid = grid;
        this.x = x;
        this.z = z;
        this.isBuildable = true;
        this.isLocked = false;
    }

    public void SetPlacedObject(PlacedObject placedObject)
    {
        this.placedObject = placedObject;
        grid.TriggerGridObjectChanged(x, z);
    }

    public void SetInfrastructureObject(PlacedObject infraObject)
    {
        this.infrastructureObject = infraObject;
        grid.TriggerGridObjectChanged(x, z);
    }

    public PlacedObject GetPlacedObject() => placedObject;
    public PlacedObject GetInfrastructureObject() => infrastructureObject;

    public void ClearPlacedObject()
    {
        placedObject = null;
        grid.TriggerGridObjectChanged(x, z);
    }

    public void ClearInfrastructureObject()
    {
        infrastructureObject = null;
        grid.TriggerGridObjectChanged(x, z);
    }

    public void SetBuildable(bool buildable)
    {
        isBuildable = buildable;
        grid.TriggerGridObjectChanged(x, z);
    }

    public void SetLocked(bool locked)
    {
        isLocked = locked;
        grid.TriggerGridObjectChanged(x, z);
    }

    public bool IsLocked() => isLocked;

    public bool CanBuild()
    {
        // Можна будувати (ставити кота), якщо:
        // 1. Основний слот порожній
        // 2. Клітинка помічена як buildable
        // 3. Клітинка НЕ заблокована (або розблокована тулзом)
        return placedObject == null && isBuildable && !isLocked;
    }

    // Перевірка, чи можна поставити Тулз (розблоковувач)
    public bool CanPlaceInfrastructure()
    {
        // Тулз можна ставити, якщо тут ще немає іншого тулза і клітинка заблокована
        return infrastructureObject == null && isLocked;
    }

    public bool IsOccupied() => placedObject != null;
    public bool IsBuildable() => isBuildable;
    public bool HasInfrastructure() => infrastructureObject != null;

    public Vector2Int GetGridPosition() => new Vector2Int(x, z);

    public override string ToString()
    {
        string content = "";
        if (placedObject != null) content += $"<color=red>{placedObject.name}</color>\n";
        if (infrastructureObject != null) content += $"<color=green>[{infrastructureObject.name}]</color>\n";

        string status = isLocked ? "<color=orange>LOCKED</color>" : (isBuildable ? "OK" : "X");

        return $"{x}, {z}\n{status}\n{content}";
    }
}