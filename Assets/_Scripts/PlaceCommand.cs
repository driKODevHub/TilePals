using UnityEngine;
using System.Collections.Generic;

public class PlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int origin;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 prevPosition;
    private Quaternion prevRotation;
    private bool wasOnGrid;
    private bool wasOffGrid;
    private Vector2Int prevGridOrigin;
    private Vector2Int prevOffGridOrigin;
    
    // Additional snapshot for direction flow
    private PlacedObjectTypeSO.Dir prevDirection;

    private List<PuzzlePiece> passengersSnapshot;

    public PlaceCommand(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir direction,
                        Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> passengers,
                        bool wasPlaced, bool wasOff, Vector2Int prevOrigin, PlacedObjectTypeSO.Dir prevDir)
    {
        this.piece = piece;
        this.origin = origin;
        this.direction = direction;

        this.prevPosition = prevPos;
        this.prevRotation = prevRot;
        this.wasOnGrid = wasPlaced;
        this.wasOffGrid = wasOff;
        this.prevDirection = prevDir;

        // Ensure we store correct previous state
        if (wasOnGrid) this.prevGridOrigin = prevOrigin;
        if (wasOffGrid) this.prevOffGridOrigin = prevOrigin;

        // Snapshot passengers to restore parent-child relationship on Undo
        this.passengersSnapshot = passengers != null ? new List<PuzzlePiece>(passengers) : new List<PuzzlePiece>();
    }

    public bool Execute()
    {
        // 1. Placing Piece Logic
        // Note: GridBuildingSystem.PlacePieceOnGrid handles registration to grid objects
        
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null) return false;

        var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, origin, direction);
        if (po == null) return false;

        // 2. Setup Piece State
        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
        {
            piece.SetInfrastructure(po);
            // Handling tools placement
        }
        else
        {
            piece.SetPlaced(po);
        }

        piece.SetOffGrid(false);
        
        // 3. Update Transform
        float cellSize = activeBoard.Grid.GetCellSize();
        // Use Grid World Position to ensure Board pivot is respected
        // Note: targetPosition passed into constructor might be stale if we relied on it. 
        // Better to recalculate snap position here based on Grid.
        Vector3 cellWorldPos = activeBoard.Grid.GetWorldPosition(origin.x, origin.y);
        
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
        Vector3 snapPos = cellWorldPos + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        
        piece.UpdateTransform(snapPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));

        // 4. Handle Passengers (if any were stored)
        if (passengersSnapshot.Count > 0)
        {
            piece.StoredPassengers.Clear();

            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;

                p.SyncDirectionFromRotation(p.transform.rotation);
                p.transform.SetParent(piece.transform.parent);

                // Calculate where passenger lands relative to grid
                Vector3 pWorldPos = p.transform.position;
                activeBoard.Grid.GetXZ(pWorldPos, out int px, out int pz);
                
                Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
                Vector2Int pOrigin = new Vector2Int(px, pz) - pRotOffset;

                var poPassenger = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
                p.SetPlaced(poPassenger);

                Vector3 pSnapPos = activeBoard.Grid.GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
                p.UpdateTransform(pSnapPos, p.transform.rotation);
            }
        }

        PersonalityEventManager.RaisePiecePlaced(piece);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();

        return true;
    }

    public void Undo()
    {
        CleanupCurrentState();
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null) return; // Should not happen during Undo context usually

        // Restore passengers to Stored
        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                piece.AddPassenger(p); 
            }
        }

        piece.UpdateTransform(prevPosition, prevRotation);
        if (piece.PieceTypeSO.category != PlacedObjectTypeSO.ItemCategory.Tool && piece.PieceTypeSO.category != PlacedObjectTypeSO.ItemCategory.Food && piece.PieceTypeSO.category != PlacedObjectTypeSO.ItemCategory.Toy)
             piece.SyncPassengersRotation(); // Sync only for cats? Or logic depends on piece type. 

        if (wasOnGrid)
        {
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, prevGridOrigin, prevDirection);

            if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                piece.SetInfrastructure(po);
                RestorePassengersOnGridAfterUndo(piece);
            }
            else
            {
                piece.SetPlaced(po);
            }
        }
        else if (wasOffGrid)
        {
            activeBoard.OffGridTracker.PlacePiece(piece, prevOffGridOrigin);
            piece.SetOffGrid(true, prevOffGridOrigin);
        }
        else
        {
            piece.SetOffGrid(false);
        }

        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    private void CleanupCurrentState()
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;

        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
            piece.SetInfrastructure(null);
        }
        else if (piece.IsOffGrid)
        {
            activeBoard?.OffGridTracker.RemovePiece(piece);
        }
        piece.SetOffGrid(false);

        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                if (p.IsPlaced)
                {
                    GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                    p.SetPlaced(null);
                }
                else if (p.IsOffGrid)
                {
                    activeBoard?.OffGridTracker.RemovePiece(p);
                    p.SetOffGrid(false);
                }
            }
        }
    }

    private void RestorePassengersOnGridAfterUndo(PuzzlePiece tool)
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null || activeBoard.Grid == null) return;
        
        var grid = activeBoard.Grid;
        float cellSize = grid.GetCellSize();
        
        List<PuzzlePiece> currentPassengers = new List<PuzzlePiece>(tool.StoredPassengers);

        foreach (var p in currentPassengers)
        {
            p.transform.SetParent(tool.transform.parent);
            p.SyncDirectionFromRotation(p.transform.rotation);

            Vector3 worldPos = p.transform.position;
            grid.GetXZ(worldPos, out int x, out int z);
            Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int pOrigin = new Vector2Int(x, z) - pRotOffset;

            // Force Place
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
            p.SetPlaced(po);
            
            Vector3 snapPos = grid.GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
            p.UpdateTransform(snapPos, p.transform.rotation);
        }
        tool.StoredPassengers.Clear();
    }
}
