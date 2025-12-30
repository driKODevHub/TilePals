using UnityEngine;

public class GiveItemCommand : ICommand
{
    private PuzzlePiece _cat;
    private PuzzlePiece _item;
    
    // Previous State of the Item
    private Vector3 _prevPosition;
    private Quaternion _prevRotation;
    private bool _wasOnGrid;
    private bool _wasOffGrid;
    private Vector2Int _prevGridOrigin;
    private Vector2Int _prevOffGridOrigin;
    private PlacedObjectTypeSO.Dir _prevDirection;
    private PlacedObject _prevPlacedObject; // If it was on grid
    
    public GiveItemCommand(PuzzlePiece cat, PuzzlePiece item, 
                           Vector3 prevPos, Quaternion prevRot, 
                           bool wasValPlaced, bool wasValOff, 
                           Vector2Int prevOrigin, PlacedObjectTypeSO.Dir prevDir)
    {
        _cat = cat;
        _item = item;
        _prevPosition = prevPos;
        _prevRotation = prevRot;
        _wasOnGrid = wasValPlaced;
        _wasOffGrid = wasValOff;
        _prevGridOrigin = prevOrigin;
        _prevDirection = prevDir;
        
        // We capture origin, but for "OnGrid", we usually reconstruct PlacedObject via PlacePieceOnGrid
        if (_wasOffGrid) _prevOffGridOrigin = prevOrigin; 
    }

    public bool Execute()
    {
        if (_cat == null || _item == null) return false;

        // 1. Cleanup Item from its previous state
        CleanupItemState();

        // 2. Attach to Cat
        _cat.AttachItem(_item);

        return true;
    }

    public void Undo()
    {
        if (_cat == null || _item == null) return;

        // 1. Detach from Cat
        PuzzlePiece detachedItem = _cat.DetachItem();
        if (detachedItem != _item) 
        {
            Debug.LogWarning("[GiveItemCommand] Detached item is different or null!");
            // Fallback just in case
            if (detachedItem == null) detachedItem = _item;
        }

        // 2. Restore to Previous State
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null) return;

        detachedItem.UpdateTransform(_prevPosition, _prevRotation);
        detachedItem.SyncDirectionFromRotation(_prevRotation); // Важливо синхронізувати напрямок

        if (_wasOnGrid)
        {
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(detachedItem, _prevGridOrigin, _prevDirection);
            detachedItem.SetPlaced(po);
        }
        else if (_wasOffGrid)
        {
            activeBoard.OffGridTracker.PlacePiece(detachedItem, _prevOffGridOrigin);
            detachedItem.SetOffGrid(true, _prevOffGridOrigin);
        }
        else
        {
            // Just floating or something?
            detachedItem.SetOffGrid(false);
            detachedItem.SetPlaced(null);
        }

        detachedItem.EnablePhysics(Vector3.zero); // Or keep kinematic if it was static?
        // Usually pieces on grid/off grid are kinematic (physics disabled)
        // If it was just picked up from physics state, we might need to know that.
        // But typically we pick up from Grid or OffGrid. 
        if (_wasOnGrid || _wasOffGrid)
        {
            detachedItem.DisablePhysics();
        }
        
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    private void CleanupItemState()
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (_item.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(_item);
            _item.SetPlaced(null);
        }
        else if (_item.IsOffGrid)
        {
            activeBoard?.OffGridTracker.RemovePiece(_item);
        }
        _item.SetOffGrid(false);
    }
}
