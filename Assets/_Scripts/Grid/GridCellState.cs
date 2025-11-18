public enum GridCellState
{
    Active,             // Default buildable, unoccupied cell
    Inactive,           // Not buildable, unoccupied cell
    Occupied,           // Cell with a placed object
    Hovered,            // Cell currently hovered by mouse (and potentially buildable/active)
    InvalidPlacement    // Cell where an attempt to place an object is invalid (e.g., out of bounds, already occupied, not buildable)
}