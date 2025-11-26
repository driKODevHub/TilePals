using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EZhex1991.EZSoftBone; // Додаємо простір імен асету
using Debug = UnityEngine.Debug;

public class LevelLoader : MonoBehaviour
{
    [Header("Налаштування спавну")]
    [SerializeField] private Transform pieceSpawnParent;

    private GridDataSO _currentLevelData;
    private List<PuzzlePiece> _spawnedPieces = new List<PuzzlePiece>();

    private const int MAX_PLACEMENT_ATTEMPTS = 500;

    public void LoadLevel(GridDataSO levelData, bool loadFromSave)
    {
        ClearLevel();
        _currentLevelData = levelData;

        if (_currentLevelData == null) return;
        if (!_currentLevelData.isComplete)
        {
            Debug.LogWarning($"Рівень {_currentLevelData.name} позначено як нерозв'язний (isComplete=false). Завантаження фігур скасовано.");
            return;
        }
        if (_currentLevelData.personalityData == null)
        {
            Debug.LogWarning($"Рівень {_currentLevelData.name} не має PersonalityData. Фігури можуть бути завантажені без характерів.");
        }

        GridBuildingSystem.Instance.InitializeGrid(_currentLevelData);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.ReinitializeVisuals();

        SpawnPieces();

        if (loadFromSave) ApplySavedState();
    }

    private void SpawnPieces()
    {
        if (_currentLevelData.puzzlePieces == null || _currentLevelData.puzzlePieces.Count == 0) return;

        var personalityMap = _currentLevelData.personalityData?.personalityMappings
            .ToDictionary(m => m.pieceType, m => m.temperament) ?? new Dictionary<PlacedObjectTypeSO, TemperamentSO>();

        List<PlacedObjectTypeSO> piecesToSpawnTypes = new List<PlacedObjectTypeSO>(_currentLevelData.puzzlePieces);
        piecesToSpawnTypes.Shuffle();

        List<PuzzlePiece> piecesToPlace = new List<PuzzlePiece>();

        foreach (var pieceType in piecesToSpawnTypes)
        {
            if (pieceType == null || pieceType.prefab == null) continue;

            Transform pieceTransform = Instantiate(pieceType.prefab, pieceSpawnParent);
            PuzzlePiece pieceComponent = pieceTransform.GetComponent<PuzzlePiece>();
            if (pieceComponent != null)
            {
                if (pieceComponent.FacialController != null && pieceType.relativeOccupiedCells.Count > 0)
                {
                    Vector2Int randomCell = pieceType.relativeOccupiedCells[Random.Range(0, pieceType.relativeOccupiedCells.Count)];
                    pieceComponent.FacialController.transform.localPosition = new Vector3(randomCell.x + 0.5f, 0.01f, randomCell.y + 0.5f);
                }

                PiecePersonality personality = pieceComponent.GetComponent<PiecePersonality>();
                if (personality != null && personalityMap.TryGetValue(pieceType, out TemperamentSO temperament))
                {
                    pieceComponent.SetTemperamentMaterial(temperament.temperamentMaterial);
                    personality.Setup(temperament);
                }

                PlacedObjectTypeSO.Dir randomDir = (PlacedObjectTypeSO.Dir)Random.Range(0, 4);
                pieceComponent.SetInitialRotation(randomDir);
                piecesToPlace.Add(pieceComponent);
            }
        }

        var grid = GridBuildingSystem.Instance.GetGrid();
        float cellSize = grid.GetCellSize();

        int padding = _currentLevelData.boardToSpawnPadding;
        int radius = _currentLevelData.maxSpawnRadius;
        int attempts = MAX_PLACEMENT_ATTEMPTS;
        int pieceSpacing = _currentLevelData.pieceToPiecePadding;

        RectInt forbiddenZone = new RectInt(-padding, -padding, grid.GetWidth() + padding * 2, grid.GetHeight() + padding * 2);

        foreach (var piece in piecesToPlace)
        {
            bool placed = false;
            for (int attempt = 0; attempt < attempts; attempt++)
            {
                int x = Random.Range(forbiddenZone.xMin - radius, forbiddenZone.xMax + radius);
                int z = Random.Range(forbiddenZone.yMin - radius, forbiddenZone.yMax + radius);
                Vector2Int origin = new Vector2Int(x, z);

                List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);

                if (pieceCells.Any(cell => forbiddenZone.Contains(cell))) continue;

                if (OffGridManager.CanPlacePieceWithPadding(piece, origin, pieceSpacing))
                {
                    PlacePieceOffGrid(piece, origin, cellSize);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                Debug.LogWarning($"Could not find ideal spot for {piece.name} in {attempts} attempts. Force placing.");
                bool emergencyPlaced = false;
                for (int r = radius; r < 50; r++)
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
                if (!emergencyPlaced) Destroy(piece.gameObject);
            }
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

    public void ClearLevel()
    {
        foreach (var piece in _spawnedPieces)
        {
            if (piece != null) Destroy(piece.gameObject);
        }
        _spawnedPieces.Clear();
        CommandHistory.Clear();
        OffGridManager.Clear();
        if (GridBuildingSystem.Instance != null) GridBuildingSystem.Instance.ClearGrid();
    }

    // --- НОВИЙ МЕТОД ДЛЯ БЕЗПЕЧНОГО ТЕЛЕПОРТУ ---
    private void TeleportPieceAndResetPhysics(PuzzlePiece piece, Vector3 position, Quaternion rotation)
    {
        // 1. Знаходимо всі компоненти EZSoftBone на фігурі (може бути кілька хвостів/вух)
        var softBones = piece.GetComponentsInChildren<EZSoftBone>();

        // 2. Вимикаємо їх, щоб вони не симулювали рух під час телепортації
        foreach (var sb in softBones)
        {
            sb.enabled = false;
        }

        // 3. Телепортуємо
        piece.UpdateTransform(position, rotation);

        // 4. Примусово оновлюємо фізику, щоб вона "прийняла" нову позицію як стартову
        foreach (var sb in softBones)
        {
            sb.enabled = true;           // Вмикаємо назад
            sb.RevertTransforms();       // Скидаємо трансформації до початкових (відносно батька)
            sb.SetRestState();           // Кажемо: "Тут твоя нова точка спокою, швидкість = 0"
        }
    }
    // ----------------------------------------------

    private void PlacePieceOffGrid(PuzzlePiece piece, Vector2Int origin, float cellSize)
    {
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = new Vector3(origin.x * cellSize, 0, origin.y * cellSize) + offset;

        // Використовуємо новий метод замість прямого присвоєння transform.position
        TeleportPieceAndResetPhysics(piece, finalPos, piece.transform.rotation);

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
            PuzzlePiece pieceToPlace = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO != null && p.PieceTypeSO.name == pieceData.pieceTypeName);
            if (pieceToPlace != null)
            {
                // При відновленні збереження ми теж використовуємо телепорт в команді PlaceCommand,
                // але PlaceCommand всередині викликає piece.UpdateTransform.
                // Можна модифікувати PlaceCommand, але простіше тут зробити Reset після виконання команди.

                ICommand command = new PlaceCommand(pieceToPlace, pieceData.origin, pieceData.direction, pieceToPlace.transform.position, pieceToPlace.transform.rotation);
                command.Execute();

                // ПРИМУСОВИЙ СКИНУТИ ФІЗИКИ ПІСЛЯ ЗАВАНТАЖЕННЯ ПОЗИЦІЇ
                TeleportPieceAndResetPhysics(pieceToPlace, pieceToPlace.transform.position, pieceToPlace.transform.rotation);

                availablePieces.Remove(pieceToPlace);
            }
        }

        foreach (var pieceData in saveData.offGridPieces)
        {
            PuzzlePiece pieceToPlace = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO != null && p.PieceTypeSO.name == pieceData.pieceTypeName);
            if (pieceToPlace != null)
            {
                float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
                Vector2Int rotationOffset = pieceToPlace.PieceTypeSO.GetRotationOffset(pieceData.direction);
                Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
                Vector3 finalPos = new Vector3(pieceData.origin.x * cellSize, 0, pieceData.origin.y * cellSize) + offset;

                // ТЕЛЕПОРТ ЗІ СКИДАННЯМ ФІЗИКИ
                TeleportPieceAndResetPhysics(pieceToPlace, finalPos, Quaternion.Euler(0, pieceToPlace.PieceTypeSO.GetRotationAngle(pieceData.direction), 0));

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