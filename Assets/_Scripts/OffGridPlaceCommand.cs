using UnityEngine;

public class OffGridPlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int offGridOrigin;
    private PlacedObjectTypeSO.Dir direction;
    private Vector3 previousPosition;
    private Quaternion previousRotation;

    public OffGridPlaceCommand(PuzzlePiece piece, Vector2Int offGridOrigin, PlacedObjectTypeSO.Dir direction, Vector3 prevPos, Quaternion prevRot)
    {
        this.piece = piece;
        this.offGridOrigin = offGridOrigin;
        this.direction = direction;
        this.previousPosition = prevPos;
        this.previousRotation = prevRot;
    }

    public bool Execute()
    {
        if (piece == null) return false;

        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
        }
        else if (piece.IsOffGrid)
        {
            OffGridManager.RemovePiece(piece);
            piece.SetOffGrid(false);
        }

        piece.SetInitialRotation(direction);

        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = new Vector3(offGridOrigin.x * cellSize, 0, offGridOrigin.y * cellSize) + offset;

        piece.UpdateTransform(finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));
        piece.SetOffGrid(true, offGridOrigin);

        OffGridManager.PlacePiece(piece, offGridOrigin);

        return true;
    }

    public void Undo()
    {
        if (piece == null) return;

        OffGridManager.RemovePiece(piece);

        piece.SetOffGrid(false);
        piece.UpdateTransform(previousPosition, previousRotation);

        // Відновлюємо дефолтну ротацію для візуальної коректності при відміні
        piece.SetInitialRotation(PlacedObjectTypeSO.Dir.Down);
    }
}