using UnityEngine;

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
        // --- ВИПРАВЛЕННЯ БАГУ REDO ---
        // Перед тим як поставити фігуру на нове місце, ми мусимо переконатися, 
        // що вона не займає старе місце (це критично для ланцюжків Redo).
        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
        }
        else if (piece.IsOffGrid)
        {
            OffGridManager.RemovePiece(piece);
            piece.SetOffGrid(false);
        }
        // -----------------------------

        if (GridBuildingSystem.Instance.CanPlacePiece(piece, gridPosition, direction))
        {
            PlacedObject placedObjectComponent = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, gridPosition, direction);
            piece.SetPlaced(placedObjectComponent);

            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
            float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
            Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            Vector3 finalPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(gridPosition.x, gridPosition.y) + offset;

            piece.UpdateTransform(finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));

            PersonalityEventManager.RaisePiecePlaced(piece);

            return true;
        }
        return false;
    }

    public void Undo()
    {
        GridBuildingSystem.Instance.RemovePieceFromGrid(piece);

        if (piece.PlacedObjectComponent != null)
        {
            Object.Destroy(piece.PlacedObjectComponent);
        }

        piece.SetPlaced(null);
        piece.UpdateTransform(previousPosition, previousRotation);
    }
}