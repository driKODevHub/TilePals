using UnityEngine;
using System.Collections.Generic;

public static class OffGridManager
{
    private static HashSet<Vector2Int> _occupiedCells = new HashSet<Vector2Int>();
    private static Dictionary<PuzzlePiece, List<Vector2Int>> _pieceCellMap = new Dictionary<PuzzlePiece, List<Vector2Int>>();

    public static void Clear()
    {
        _occupiedCells.Clear();
        _pieceCellMap.Clear();
    }

    public static bool CanPlacePiece(PuzzlePiece piece, Vector2Int origin)
    {
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

    /// <summary>
    /// ВИПРАВЛЕНИЙ МЕТОД: Тепер приймає 'padding' для динамічної перевірки буферної зони.
    /// </summary>
    public static bool CanPlacePieceWithPadding(PuzzlePiece piece, Vector2Int origin, int padding)
    {
        List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);

        foreach (var cell in pieceCells)
        {
            // Перевіряємо квадрат навколо кожної клітинки фігури з заданим відступом
            for (int dx = -padding; dx <= padding; dx++)
            {
                for (int dy = -padding; dy <= padding; dy++)
                {
                    if (_occupiedCells.Contains(new Vector2Int(cell.x + dx, cell.y + dy)))
                    {
                        return false; // Знайдено перетин з буферною зоною іншої фігури
                    }
                }
            }
        }
        return true;
    }

    public static void PlacePiece(PuzzlePiece piece, Vector2Int origin)
    {
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

    public static void RemovePiece(PuzzlePiece piece)
    {
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
