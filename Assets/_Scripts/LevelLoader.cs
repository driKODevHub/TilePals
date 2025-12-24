using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EZhex1991.EZSoftBone;
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

        // Ініціалізація гріда (включаючи locked клітинки)
        GridBuildingSystem.Instance.InitializeGrid(_currentLevelData);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.ReinitializeVisuals();

        // 1. Спавнимо фігури (фізично створюємо об'єкти)
        SpawnPieces();

        // 2. Якщо є збереження - застосовуємо
        if (loadFromSave) ApplySavedState();
    }

    private void SpawnPieces()
    {
        List<PlacedObjectTypeSO> piecesToSpawnTypes = new List<PlacedObjectTypeSO>();

        if (_currentLevelData.puzzlePieces != null)
            piecesToSpawnTypes.AddRange(_currentLevelData.puzzlePieces);

        if (_currentLevelData.levelItems != null)
        {
            foreach (var item in _currentLevelData.levelItems)
            {
                if (item.pieceType != null)
                    piecesToSpawnTypes.Add(item.pieceType);
            }
        }

        if (piecesToSpawnTypes.Count == 0) return;

        var personalityMap = _currentLevelData.personalityData?.personalityMappings
            .ToDictionary(m => m.pieceType, m => m.temperament) ?? new Dictionary<PlacedObjectTypeSO, TemperamentSO>();

        // Використовуємо метод розширення Shuffle (визначений нижче)
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
                    
                    personality.Setup(temperament);
                }

                PlacedObjectTypeSO.Dir randomDir = (PlacedObjectTypeSO.Dir)Random.Range(0, 4);
                pieceComponent.SetInitialRotation(randomDir);
                piecesToPlace.Add(pieceComponent);
            }
        }

        PlacePiecesAroundBoard(piecesToPlace);
    }

    private void PlacePiecesAroundBoard(List<PuzzlePiece> pieces)
    {
        var grid = GridBuildingSystem.Instance.GetGrid();
        float cellSize = grid.GetCellSize();

        int padding = _currentLevelData.boardToSpawnPadding;
        int radius = _currentLevelData.maxSpawnRadius;
        int pieceSpacing = _currentLevelData.pieceToPiecePadding;

        RectInt forbiddenZone = new RectInt(-padding, -padding, grid.GetWidth() + padding * 2, grid.GetHeight() + padding * 2);

        foreach (var piece in pieces)
        {
            bool placed = false;
            for (int attempt = 0; attempt < MAX_PLACEMENT_ATTEMPTS; attempt++)
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
                // Fallback placement logic (perimeter search)
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
                // Безпечне отримання компонента PlacedObject або Infrastructure
                PlacedObject placedObj = piece.PlacedObjectComponent;
                if (placedObj == null) placedObj = piece.InfrastructureComponent;
                if (placedObj == null) placedObj = piece.GetComponent<PlacedObject>(); // Final check

                if (placedObj != null)
                {
                    saveData.onGridPieces.Add(new PiecePlacementData
                    {
                        pieceTypeName = piece.PieceTypeSO.name,
                        origin = placedObj.Origin,
                        direction = placedObj.Direction
                    });
                }
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

    private void TeleportPieceAndResetPhysics(PuzzlePiece piece, Vector3 position, Quaternion rotation)
    {
        var softBones = piece.GetComponentsInChildren<EZSoftBone>();
        foreach (var sb in softBones) sb.enabled = false;

        piece.UpdateTransform(position, rotation);

        foreach (var sb in softBones)
        {
            sb.enabled = true;
            sb.RevertTransforms();
            sb.SetRestState();
        }
    }

    private void PlacePieceOffGrid(PuzzlePiece piece, Vector2Int origin, float cellSize)
    {
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = new Vector3(origin.x * cellSize, 0, origin.y * cellSize) + offset;

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

        // --- СОРТУВАННЯ: Спочатку ставимо Тулзи (Infrastructure), потім Котів ---
        var toolsToPlace = new List<PiecePlacementData>();
        var othersToPlace = new List<PiecePlacementData>();

        foreach (var data in saveData.onGridPieces)
        {
            var samplePiece = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.pieceTypeName);
            if (samplePiece != null && samplePiece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                toolsToPlace.Add(data);
            }
            else
            {
                othersToPlace.Add(data);
            }
        }

        // 1. Ставимо Тулзи
        foreach (var pieceData in toolsToPlace)
        {
            PlacePieceFromSave(pieceData, availablePieces);
        }

        // 2. Ставимо Все Інше (Котів на Тулзи)
        foreach (var pieceData in othersToPlace)
        {
            PlacePieceFromSave(pieceData, availablePieces);
        }

        // 3. Відновлюємо OffGrid
        foreach (var pieceData in saveData.offGridPieces)
        {
            PlacePieceFromSaveOffGrid(pieceData, availablePieces);
        }

        CommandHistory.Clear();
    }

    private void PlacePieceFromSave(PiecePlacementData data, List<PuzzlePiece> availablePieces)
    {
        PuzzlePiece piece = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.pieceTypeName);
        if (piece != null)
        {
            ICommand command = new PlaceCommand(piece, data.origin, data.direction, piece.transform.position, piece.transform.rotation, null);
            command.Execute();
            TeleportPieceAndResetPhysics(piece, piece.transform.position, piece.transform.rotation);
            availablePieces.Remove(piece);
        }
    }

    private void PlacePieceFromSaveOffGrid(PiecePlacementData data, List<PuzzlePiece> availablePieces)
    {
        PuzzlePiece piece = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.pieceTypeName);
        if (piece != null)
        {
            float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(data.direction);
            Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            Vector3 finalPos = new Vector3(data.origin.x * cellSize, 0, data.origin.y * cellSize) + offset;

            TeleportPieceAndResetPhysics(piece, finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(data.direction), 0));
            piece.SetOffGrid(true, data.origin);
            OffGridManager.PlacePiece(piece, data.origin);
            availablePieces.Remove(piece);
        }
    }
}

// --- ОСЬ ЦЕЙ КЛАС БУВ ПРОПУЩЕНИЙ ---
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
