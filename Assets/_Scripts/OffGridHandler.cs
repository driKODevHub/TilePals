using UnityEngine;
using System.Collections.Generic;

public class OffGridHandler
{
    private HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
    private Dictionary<PuzzlePiece, List<Vector2Int>> _pieceCellMap = new Dictionary<PuzzlePiece, List<Vector2Int>>();

    public void Clear()
    {
        _occupiedCells.Clear();
        _pieceCellMap.Clear();
    }

    public bool CanPlacePiece(PuzzlePiece piece, Vector2Int origin)
    {
        if (piece == null || piece.PieceTypeSO == null) return false;
        List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);
        foreach (var cell in pieceCells)
        {
            if (_occupiedCells.Contains(cell))
            {
                return false;
            }
        }
        return true;
    }

    public bool CanPlacePieceWithPadding(PuzzlePiece piece, Vector2Int origin, int padding)
    {
        if (piece == null || piece.PieceTypeSO == null) return false;
        List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);

        foreach (var cell in pieceCells)
        {
            for (int dx = -padding; dx <= padding; dx++)
            {
                for (int dy = -padding; dy <= padding; dy++)
                {
                    if (_occupiedCells.Contains(new Vector2Int(cell.x + dx, cell.y + dy)))
                    {
                        return false; 
                    }
                }
            }
        }
        return true;
    }

    public void PlacePiece(PuzzlePiece piece, Vector2Int origin)
    {
        if (piece == null || piece.PieceTypeSO == null) return;
        if (_pieceCellMap.ContainsKey(piece))
        {
            RemovePiece(piece);
        }

        List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);
        _pieceCellMap[piece] = pieceCells;

        foreach (var cell in pieceCells)
        {
            _occupiedCells.Add(cell);
        }
    }

    public void RemovePiece(PuzzlePiece piece)
    {
        if (piece == null) return;
        if (_pieceCellMap.TryGetValue(piece, out List<Vector2Int> pieceCells))
        {
            foreach (var cell in pieceCells)
            {
                _occupiedCells.Remove(cell);
            }
            _pieceCellMap.Remove(piece);
        }
    }
}