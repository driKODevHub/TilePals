using UnityEngine;
using System.Collections.Generic;

public class OffGridPlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int offGridOrigin;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 prevPosition;
    private Quaternion prevRotation;
    private bool wasOnGrid;
    private bool wasOffGrid;
    private Vector2Int prevGridOrigin;
    private Vector2Int prevOffGridOrigin;

    private List<PuzzlePiece> passengers;

    public OffGridPlaceCommand(PuzzlePiece piece, Vector2Int offGridOrigin, PlacedObjectTypeSO.Dir direction, Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> passengers)
    {
        this.piece = piece;
        this.offGridOrigin = offGridOrigin;
        this.direction = direction;
        this.prevPosition = prevPos;
        this.prevRotation = prevRot;
        this.passengers = passengers != null ? new List<PuzzlePiece>(passengers) : new List<PuzzlePiece>();

        this.wasOnGrid = piece.IsPlaced;
        this.wasOffGrid = piece.IsOffGrid;

        if (wasOnGrid) this.prevGridOrigin = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent.Origin : (piece.InfrastructureComponent != null ? piece.InfrastructureComponent.Origin : Vector2Int.zero);
        if (wasOffGrid) this.prevOffGridOrigin = piece.OffGridOrigin;
    }

    public bool Execute()
    {
        // 1. Прибираємо зі старого місця
        if (piece.IsPlaced) GridBuildingSystem.Instance.RemovePieceFromGrid(piece); // Це заблокує клітинки, якщо був тулз
        else if (piece.IsOffGrid) OffGridManager.RemovePiece(piece);
        piece.SetOffGrid(false);

        // 2. ВІДНОВЛЮЄМО ПАСАЖИРІВ НА БОРТ (Redo Logic)
        // Якщо ми ставимо тулз в OffGrid, пасажири повинні бути дітьми (StoredPassengers)
        if (passengers.Count > 0)
        {
            foreach (var p in passengers)
            {
                if (p.IsPlaced) GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                else if (p.IsOffGrid) OffGridManager.RemovePiece(p);

                piece.AddPassenger(p); // Забираємо на борт
            }
        }

        // 3. Ставимо в OffGrid
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
        // 1. Прибираємо з OffGrid
        OffGridManager.RemovePiece(piece);
        piece.SetOffGrid(false);

        // 2. Повертаємо фізично на старе місце
        piece.UpdateTransform(prevPosition, prevRotation);
        piece.SyncDirectionFromRotation(prevRotation);

        // 3. Відновлюємо логічний стан
        if (wasOnGrid)
        {
            // Якщо був на гріді -> ставимо на грід (це РОЗБЛОКУЄ клітинки)
            if (GridBuildingSystem.Instance.CanPlacePiece(piece, prevGridOrigin, piece.CurrentDirection))
            {
                var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, prevGridOrigin, piece.CurrentDirection);

                if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
                {
                    piece.SetInfrastructure(po);
                    // Висаджуємо пасажирів назад на грід
                    RestorePassengersOnGridAfterUndo(piece);
                }
                else
                {
                    piece.SetPlaced(po);
                }
            }
        }
        else if (wasOffGrid)
        {
            OffGridManager.PlacePiece(piece, prevOffGridOrigin);
            piece.SetOffGrid(true, prevOffGridOrigin);
            // Пасажири залишаються на борту
        }
    }

    private void RestorePassengersOnGridAfterUndo(PuzzlePiece tool)
    {
        var grid = GridBuildingSystem.Instance.GetGrid();
        // Копіюємо список
        List<PuzzlePiece> currentPassengers = new List<PuzzlePiece>(tool.StoredPassengers);

        foreach (var p in currentPassengers)
        {
            p.transform.SetParent(tool.transform.parent);
            p.SyncDirectionFromRotation(p.transform.rotation);

            Vector3 worldPos = p.transform.position;
            grid.GetXZ(worldPos, out int x, out int z);

            Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int pOrigin = new Vector2Int(x, z) - pRotOffset;

            if (GridBuildingSystem.Instance.CanPlacePiece(p, pOrigin, p.CurrentDirection))
            {
                var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
                p.SetPlaced(po);
            }
        }
        tool.StoredPassengers.Clear();
    }
}