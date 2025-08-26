using UnityEngine;

// Інтерфейс для всіх команд


public class PlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int gridPosition;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 previousPosition;
    private Quaternion previousRotation;

    public PlaceCommand(PuzzlePiece piece, Vector2Int gridPosition, PlacedObjectTypeSO.Dir direction, Vector3 prevPos, Quaternion prevRot)
    {
        this.piece = piece;
        this.gridPosition = gridPosition;
        this.direction = direction;
        this.previousPosition = prevPos;
        this.previousRotation = prevRot;
    }

    public bool Execute()
    {
        if (GridBuildingSystem.Instance.CanPlacePiece(piece, gridPosition, direction))
        {
            // Розміщуємо фігуру і отримуємо посилання на створений компонент
            PlacedObject placedObjectComponent = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, gridPosition, direction);

            // --- ВИПРАВЛЕНО ---
            // Передаємо компонент, а не bool
            piece.SetPlaced(placedObjectComponent);

            // Вирівнюємо візуал фігури точно по сітці
            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
            float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
            Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            Vector3 finalPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(gridPosition.x, gridPosition.y) + offset;

            piece.UpdateTransform(finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));
            return true;
        }
        return false;
    }

    public void Undo()
    {
        // Прибираємо фігуру з сітки
        GridBuildingSystem.Instance.RemovePieceFromGrid(piece);

        // --- ВИПРАВЛЕНО ---
        // Передаємо null, щоб позначити, що фігура більше не розміщена
        piece.SetPlaced(null);

        // Повертаємо фігуру на її попереднє місце
        piece.UpdateTransform(previousPosition, previousRotation);
    }
}
