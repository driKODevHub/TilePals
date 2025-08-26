using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class LevelLoader : MonoBehaviour
{
    [Header("Налаштування спавну")]
    [Tooltip("Батьківський об'єкт для всіх фігур, що створюються.")]
    [SerializeField] private Transform pieceSpawnParent;

    private GridDataSO _currentLevelData;
    private List<PuzzlePiece> _spawnedPieces = new List<PuzzlePiece>();

    public void LoadLevel(GridDataSO levelData, bool loadFromSave)
    {
        ClearLevel();
        _currentLevelData = levelData;

        if (_currentLevelData == null)
        {
            Debug.LogError("Дані рівня недійсні!");
            return;
        }

        if (!_currentLevelData.isComplete)
        {
            Debug.LogError($"[Level Validation] Пропуск рівня '{_currentLevelData.name}', оскільки він не завершений. Будь ласка, виправте його в редакторі.", _currentLevelData);
            return;
        }

        GridBuildingSystem.Instance.InitializeGrid(_currentLevelData);

        if (GridVisualManager.Instance != null)
        {
            GridVisualManager.Instance.ReinitializeVisuals();
        }

        SpawnPieces();

        if (loadFromSave)
        {
            ApplySavedState();
        }
    }

    public void SaveLevelState()
    {
        LevelSaveData saveData = new LevelSaveData();
        foreach (var piece in _spawnedPieces)
        {
            if (piece == null) continue;

            if (piece.IsPlaced)
            {
                saveData.onGridPieces.Add(new PiecePlacementData
                {
                    pieceTypeName = piece.PieceTypeSO.name,
                    origin = piece.PlacedObjectComponent.Origin,
                    direction = piece.PlacedObjectComponent.Direction
                });
            }
            else if (piece.IsOffGrid)
            {
                saveData.offGridPieces.Add(new PiecePlacementData
                {
                    pieceTypeName = piece.PieceTypeSO.name,
                    origin = piece.OffGridOrigin,
                    direction = piece.CurrentDirection
                });
            }
        }
        SaveSystem.SaveLevelProgress(GameManager.Instance.CurrentLevelIndex, saveData);
    }

    private void ClearLevel()
    {
        foreach (var piece in _spawnedPieces)
        {
            if (piece != null) Destroy(piece.gameObject);
        }
        _spawnedPieces.Clear();
        CommandHistory.Clear();
        OffGridManager.Clear();
        GridBuildingSystem.Instance.ClearGrid();
    }

    private void SpawnPieces()
    {
        if (_currentLevelData.puzzlePieces == null || _currentLevelData.puzzlePieces.Count == 0) return;

        List<PlacedObjectTypeSO> piecesToSpawnTypes = new List<PlacedObjectTypeSO>(_currentLevelData.puzzlePieces);
        piecesToSpawnTypes.Shuffle();

        List<PuzzlePiece> piecesToPlace = new List<PuzzlePiece>();

        foreach (var pieceType in piecesToSpawnTypes)
        {
            if (pieceType.prefab != null)
            {
                Transform pieceTransform = Instantiate(pieceType.prefab, pieceSpawnParent);
                PuzzlePiece pieceComponent = pieceTransform.GetComponent<PuzzlePiece>();
                if (pieceComponent != null)
                {
                    PlacedObjectTypeSO.Dir randomDir = (PlacedObjectTypeSO.Dir)Random.Range(0, 4);
                    pieceComponent.SetInitialRotation(randomDir);
                    piecesToPlace.Add(pieceComponent);
                }
            }
        }

        var grid = GridBuildingSystem.Instance.GetGrid();
        float cellSize = grid.GetCellSize();

        int padding = _currentLevelData.boardToSpawnPadding;
        int radius = _currentLevelData.maxSpawnRadius;
        int attempts = _currentLevelData.placementAttempts;
        int pieceSpacing = _currentLevelData.pieceToPiecePadding;

        // --- ОНОВЛЕНО: Визначаємо заборонену зону ---
        // Тепер вона включає сам ігровий майданчик та відступ навколо нього
        RectInt forbiddenZone = new RectInt(
            -padding,
            -padding,
            grid.GetWidth() + padding * 2,
            grid.GetHeight() + padding * 2
        );

        foreach (var piece in piecesToPlace)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                // Генеруємо випадкову позицію у великій зоні навколо поля
                int x = Random.Range(forbiddenZone.xMin - radius, forbiddenZone.xMax + radius);
                int z = Random.Range(forbiddenZone.yMin - radius, forbiddenZone.yMax + radius);
                Vector2Int origin = new Vector2Int(x, z);

                // --- ОНОВЛЕНО: Надійна перевірка ---
                // Отримуємо всі клітинки, які фігура займе з урахуванням повороту
                List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);

                // Перевірка 1: Чи не залазить ЖОДНА клітинка фігури на заборонену зону?
                bool overlapsBoardArea = pieceCells.Any(cell => forbiddenZone.Contains(cell));

                if (overlapsBoardArea)
                {
                    continue; // Ця позиція не підходить, пробуємо іншу
                }

                // Перевірка 2: Чи не накладається фігура на інші фігури (з урахуванням проміжку)?
                if (OffGridManager.CanPlacePieceWithPadding(piece, origin, pieceSpacing))
                {
                    PlacePieceOffGrid(piece, origin, cellSize);
                    placed = true;
                    break; // Успіх, переходимо до наступної фігури
                }
            }

            if (!placed)
            {
                Debug.LogWarning($"Не вдалося знайти ідеальне місце для фігури '{piece.name}' за {attempts} спроб. Розміщуємо примусово.");
                bool emergencyPlaced = false;
                for (int r = padding; r < 100; r++)
                {
                    List<Vector2Int> perimeter = GetPerimeterCells(forbiddenZone.xMin - r, forbiddenZone.xMax + r, forbiddenZone.yMin - r, forbiddenZone.yMax + r);
                    foreach (var origin in perimeter)
                    {
                        if (OffGridManager.CanPlacePiece(piece, origin))
                        {
                            PlacePieceOffGrid(piece, origin, cellSize);
                            emergencyPlaced = true;
                            break;
                        }
                    }
                    if (emergencyPlaced) break;
                }
            }
        }
    }

    private void PlacePieceOffGrid(PuzzlePiece piece, Vector2Int origin, float cellSize)
    {
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = new Vector3(origin.x * cellSize, 0, origin.y * cellSize) + offset;

        piece.transform.position = finalPos;
        piece.SetOffGrid(true, origin);
        OffGridManager.PlacePiece(piece, origin);

        _spawnedPieces.Add(piece);
    }


    private List<Vector2Int> GetPerimeterCells(int minX, int maxX, int minZ, int maxZ)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = minX; x <= maxX; x++)
        {
            cells.Add(new Vector2Int(x, minZ));
            cells.Add(new Vector2Int(x, maxZ));
        }
        for (int z = minZ + 1; z < maxZ; z++)
        {
            cells.Add(new Vector2Int(minX, z));
            cells.Add(new Vector2Int(maxX, z));
        }
        return cells;
    }


    private void ApplySavedState()
    {
        LevelSaveData saveData = SaveSystem.LoadLevelProgress(GameManager.Instance.CurrentLevelIndex);
        if (saveData == null) return;

        List<PuzzlePiece> availablePieces = new List<PuzzlePiece>(_spawnedPieces);

        foreach (var pieceData in saveData.onGridPieces)
        {
            PuzzlePiece pieceToPlace = availablePieces.FirstOrDefault(p => p.PieceTypeSO.name == pieceData.pieceTypeName);
            if (pieceToPlace != null)
            {
                ICommand command = new PlaceCommand(pieceToPlace, pieceData.origin, pieceData.direction, pieceToPlace.transform.position, pieceToPlace.transform.rotation);
                command.Execute();
                availablePieces.Remove(pieceToPlace);
            }
        }

        foreach (var pieceData in saveData.offGridPieces)
        {
            PuzzlePiece pieceToPlace = availablePieces.FirstOrDefault(p => p.PieceTypeSO.name == pieceData.pieceTypeName);
            if (pieceToPlace != null)
            {
                float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
                Vector2Int rotationOffset = pieceToPlace.PieceTypeSO.GetRotationOffset(pieceData.direction);
                Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
                Vector3 finalPos = new Vector3(pieceData.origin.x * cellSize, 0, pieceData.origin.y * cellSize) + offset;

                pieceToPlace.UpdateTransform(finalPos, Quaternion.Euler(0, pieceToPlace.PieceTypeSO.GetRotationAngle(pieceData.direction), 0));
                pieceToPlace.SetOffGrid(true, pieceData.origin);
                OffGridManager.PlacePiece(pieceToPlace, pieceData.origin);
                availablePieces.Remove(pieceToPlace);
            }
        }

        CommandHistory.Clear();
    }
}

public static class ListExtensions
{
    private static System.Random rng = new System.Random();

    public static void Shuffle<T>(this IList<T> list)
    {
        int n = list.Count;
        while (n > 1)
        {
            n--;
            int k = rng.Next(n + 1);
            T value = list[k];
            list[k] = list[n];
            list[n] = value;
        }
    }
}
