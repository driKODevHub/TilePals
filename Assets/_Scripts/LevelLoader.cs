using UnityEngine;
using System.Collections.Generic;
using System.Linq;
using EZhex1991.EZSoftBone;

public class LevelLoader : MonoBehaviour
{
    public static LevelLoader Instance { get; private set; }

    [SerializeField] private PuzzleBoard boardPrefab;
    [SerializeField] private List<PuzzlePiece> piecePrefabs;
    [SerializeField] private Transform pieceParent;

    private List<PuzzleBoard> _activeLocationBoards = new List<PuzzleBoard>();
    private PuzzleBoard _currentBoard;
    private LevelCollectionSO _currentLocation;
    
    private int _currentLevelIndex = -1;
    private List<PuzzlePiece> _allSpawnedPieces = new List<PuzzlePiece>();

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public List<PuzzleBoard> ActiveLocationBoards => _activeLocationBoards;

    #region Backward Compatibility for GameManager

    public void ClearLevel()
    {
        // Destroy old boards
        foreach (var board in _activeLocationBoards)
        {
            if (board != null) Destroy(board.gameObject);
        }
        _activeLocationBoards.Clear();
        _allSpawnedPieces.Clear();
        _currentBoard = null;
    }

    public void SaveLevelState() => SaveCurrentLocationState();

    public void LoadLevel(GridDataSO levelData, bool loadFromSave)
    {
        // For compatibility, we create a temporary LocationSO
        LevelCollectionSO dummyLocation = ScriptableObject.CreateInstance<LevelCollectionSO>();
        dummyLocation.name = "SingleLevel_" + levelData.name;
        dummyLocation.levels = new List<GridDataSO> { levelData };
        LoadLocation(dummyLocation, loadFromSave);
    }

    public int GetCurrentLevelIndex() => _currentLevelIndex;

    #endregion

    public void LoadLocation(LevelCollectionSO location, bool loadFromSave, int forceIndex = -1)
    {
        _currentLocation = location;
        ClearLevel();

        if (location == null || location.levels == null || location.levels.Count == 0) return;

        // Determine which levels to spawn
        // Logic: Spawn ALL completed levels + the first incomplete one (current)
        // If forceIndex is provided, ensure that one is spawned and set as current.

        int determinedCurrentIndex = -1;

        for (int i = 0; i < location.levels.Count; i++)
        {
            string boardId = $"{location.name}_{i}";
            LevelSaveData saveData = SaveSystem.LoadLevelProgress(boardId);
            bool isCompleted = saveData != null && saveData.isCompleted;
            
            // If we haven't found a current level yet, and this one is NOT completed, it's the current one.
            bool isCurrent = false;

            if (forceIndex != -1)
            {
                isCurrent = (i == forceIndex);
            }
            else if (determinedCurrentIndex == -1)
            {
                if (!isCompleted)
                {
                    determinedCurrentIndex = i;
                    isCurrent = true;
                }
            }

            // Spawn conditions:
            // 1. Level is completed
            // 2. Level is the determined "current" level
            // 3. Level is effectively the "current" if we ran out of levels (all completed -> last one?) 
            //    (Actually logic below handles determinedCurrentIndex fallback)

            if (isCompleted || isCurrent)
            {
                // Instantiate Board
                InstantiateBoardAtIndex(i, loadFromSave);
                if (isCurrent) 
                {
                    _currentLevelIndex = i;
                    // Will set _currentBoard inside InstantiateBoardAtIndex... 
                    // But we iterate all. We need to set active board to the current one at the end?
                    // Let's refine InstantiateBoardAtIndex to return board.
                }
            }
        }

        // If all levels are completed and no force index, maybe focus the last one?
        if (determinedCurrentIndex == -1 && forceIndex == -1)
        {
            // All completed. Set last one as current/focus?
            _currentLevelIndex = location.levels.Count - 1;
            // Ensure last one is active
            var lastBoard = _activeLocationBoards.LastOrDefault();
            if (lastBoard != null) SetCurrentBoard(lastBoard);
        }
        else
        {
             // Find the board matching _currentLevelIndex and set it active
             var currentBoard = _activeLocationBoards.FirstOrDefault(b => b.boardId == $"{location.name}_{_currentLevelIndex}");
             if (currentBoard != null) SetCurrentBoard(currentBoard);
        }
    }
    
    private PuzzleBoard InstantiateBoardAtIndex(int index, bool loadFromSave)
    {
         if (_currentLocation == null || index < 0 || index >= _currentLocation.levels.Count) return null;
         
         GridDataSO levelData = _currentLocation.levels[index];
         if (levelData == null) return null;
         
         // 1. Create Board at World Position
         PuzzleBoard board = Instantiate(boardPrefab, levelData.worldPosition, Quaternion.identity, transform);
         board.name = $"Board_{index}_{levelData.name}";
         board.boardId = $"{_currentLocation.name}_{index}"; 
         
         _activeLocationBoards.Add(board);
         board.InitializeBoard(levelData);

         LevelSaveData saveData = loadFromSave ? SaveSystem.LoadLevelProgress(board.boardId) : null;
         
         // 2. Spawn Content
         SpawnObstacles(board, levelData);
         SpawnPieces(board, levelData);
         
         FinalizePlacement(board, saveData);
         
         return board;
    }
    
    // Kept for direct calls, but creates a single board if not exists?
    public void LoadLevelAtIndex(int index, bool loadFromSave)
    {
         // This method was previously clearing everything. Now it should probably just ensure the level is spawned?
         // If we follow the rule "Spawn next", we can just call Instantiate.
         
         // Check if already exists
         if (_activeLocationBoards.Any(b => b.boardId == $"{_currentLocation.name}_{index}")) return;
         
         var board = InstantiateBoardAtIndex(index, loadFromSave);
         if (board != null)
         {
             SetCurrentBoard(board);
             _currentLevelIndex = index;
         }
    }
    
    public void RestartCurrentLevel()
    {
        // Reload location to reset everything? Or just reset current board?
        // Simpler: Reload Location
        if (_currentLocation != null) LoadLocation(_currentLocation, false, _currentLevelIndex);
    }

    public void LoadNextLevel()
    {
        if (_currentLocation == null) return;
        
        int nextIndex = _currentLevelIndex + 1;
        if (nextIndex < _currentLocation.levels.Count)
        {
             // Instantiate next level
             var board = InstantiateBoardAtIndex(nextIndex, true);
             if (board != null)
             {
                 _currentLevelIndex = nextIndex;
                 SetCurrentBoard(board);
             }
        }
    }

    private void SetCurrentBoard(PuzzleBoard board)
    {
        _currentBoard = board;
        GridBuildingSystem.Instance.SetActiveBoard(_currentBoard);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.ReinitializeVisuals();
        
        // Focus Camera
        if (CameraController.Instance != null) CameraController.Instance.FocusOnBoard(board);
    }

    private void SpawnObstacles(PuzzleBoard board, GridDataSO data)
    {
        if (data.staticObstacles == null) return;
        
        foreach (var obsData in data.staticObstacles)
        {
            if (obsData.pieceType == null) continue;
            
            PuzzlePiece piece = Instantiate(obsData.pieceType.prefab).GetComponent<PuzzlePiece>();
            piece.IsStaticObstacle = true;
            piece.IsObstacle = obsData.isObstacle;
            piece.IsHidden = obsData.isHidden;
            piece.OwnerBoard = board; // Assign board ownership
            
            // Store default data for fallback
            piece.SetEditorPlacement(obsData.position, obsData.direction, obsData.startOnGrid);
            piece.StartOnGrid = obsData.startOnGrid;
            board.RegisterPiece(piece);
            _allSpawnedPieces.Add(piece);
        }
    }

    private void SpawnPieces(PuzzleBoard board, GridDataSO data)
    {
        List<GridDataSO.GeneratedPieceData> piecesToSpawn = new List<GridDataSO.GeneratedPieceData>();
        
        // Prefer baked solution as it contains startOnGrid and positions
        if (data.puzzleSolution != null && data.puzzleSolution.Count > 0)
        {
            piecesToSpawn.AddRange(data.puzzleSolution);
        }
        else if (data.puzzlePieces != null)
        {
            foreach (var type in data.puzzlePieces) piecesToSpawn.Add(new GridDataSO.GeneratedPieceData { pieceType = type });
        }

        if (data.levelItems != null) piecesToSpawn.AddRange(data.levelItems);
        if (piecesToSpawn.Count == 0) return;

        var personalityMap = data.personalityData?.personalityMappings
            .ToDictionary(m => m.pieceType, m => m.temperament) ?? new Dictionary<PlacedObjectTypeSO, TemperamentSO>();

        List<PuzzlePiece> boardPieces = new List<PuzzlePiece>();
        foreach (var pData in piecesToSpawn)
        {
            if (pData.pieceType == null || pData.pieceType.prefab == null) continue;
            GameObject pieceObj = Instantiate(pData.pieceType.prefab, Vector3.zero, Quaternion.identity, pieceParent).gameObject;
            PuzzlePiece piece = pieceObj.GetComponent<PuzzlePiece>();
            piece.IsObstacle = pData.isObstacle;
            piece.IsHidden = pData.isHidden;
            piece.OwnerBoard = board; // Assign board ownership
            board.RegisterPiece(piece);
            _allSpawnedPieces.Add(piece);
            boardPieces.Add(piece);

            if (personalityMap.TryGetValue(pData.pieceType, out TemperamentSO temperament))
            {
                var personality = piece.GetComponent<PiecePersonality>();
                if (personality != null) personality.Setup(temperament);
            }

            // Safety Check: Even if startOnGrid is true, if a PuzzleShape overlaps a lockedCell, force it off-grid.
            // This fixes old data where shapes were saved as on-grid on tools/locked areas.
            bool finalStartOnGrid = pData.startOnGrid;
            if (finalStartOnGrid && !pData.isObstacle && pData.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape)
            {
                var positions = pData.pieceType.GetGridPositionsList(pData.position, pData.direction);
                if (positions.Any(pos => data.lockedCells.Contains(pos)))
                {
                    finalStartOnGrid = false;
                }
            }

            piece.StartOnGrid = finalStartOnGrid;
            piece.SetEditorPlacement(pData.position, pData.direction, finalStartOnGrid);
        }
    }

    private void PlacePiecesAroundBoard(PuzzleBoard board, GridDataSO data, List<PuzzlePiece> pieces)
    {
        float cellSize = board.Grid.GetCellSize();
        int margin = data.boardToSpawnPadding;
        int pieceSpacing = data.pieceToPiecePadding;

        RectInt forbiddenZone = new RectInt(-margin, -margin, data.width + margin * 2, data.height + margin * 2);
        int radius = Mathf.Max(data.width, data.height) + margin;
        
        var piecesToAutoPlace = pieces.Where(p => !p.IsPlaced && !p.IsOffGrid && !p.IsStaticObstacle).ToList();
        
        // Use our extension method
        piecesToAutoPlace.Shuffle();

        foreach (var piece in piecesToAutoPlace)
        {
            bool placed = false;
            for (int attempt = 0; attempt < 100; attempt++)
            {
                int rx = Random.Range(-radius, data.width + radius);
                int rz = Random.Range(-radius, data.height + radius);
                Vector2Int origin = new Vector2Int(rx, rz);
                if (forbiddenZone.Contains(origin)) continue;
                List<Vector2Int> pieceCells = piece.PieceTypeSO.GetGridPositionsList(origin, piece.CurrentDirection);
                if (pieceCells.Any(cell => forbiddenZone.Contains(cell))) continue;

                if (board.OffGridTracker.CanPlacePieceWithPadding(piece, origin, pieceSpacing))
                {
                    PlacePieceOffGrid(board, piece, origin, cellSize);
                    placed = true;
                    break;
                }
            }

            if (!placed)
            {
                bool emergencyPlaced = false;
                for (int r = radius; r < radius + 50; r++)
                {
                    List<Vector2Int> perimeter = GetPerimeterCells(forbiddenZone.xMin - r, forbiddenZone.xMax + r, forbiddenZone.yMin - r, forbiddenZone.yMax + r);
                    foreach (var origin in perimeter)
                    {
                        if (board.OffGridTracker.CanPlacePiece(piece, origin))
                        {
                            PlacePieceOffGrid(board, piece, origin, cellSize);
                            emergencyPlaced = true;
                            break;
                        }
                    }
                    if (emergencyPlaced) break;
                }
                if (!emergencyPlaced) 
                {
                    Debug.LogWarning($"LevelLoader: Could not find spot for piece {piece.name}, destroying.");
                    Destroy(piece.gameObject);
                }
            }
        }
    }

    private List<Vector2Int> GetPerimeterCells(int xMin, int xMax, int zMin, int zMax)
    {
        List<Vector2Int> cells = new List<Vector2Int>();
        for (int x = xMin; x <= xMax; x++) { cells.Add(new Vector2Int(x, zMin)); cells.Add(new Vector2Int(x, zMax)); }
        for (int z = zMin + 1; z < zMax; z++) { cells.Add(new Vector2Int(xMin, z)); cells.Add(new Vector2Int(xMax, z)); }
        return cells;
    }

    public void SaveCurrentLocationState()
    {
        foreach (var board in _activeLocationBoards) SaveBoardState(board);
    }

    private void SaveBoardState(PuzzleBoard board)
    {
        if (board == null || string.IsNullOrEmpty(board.boardId)) return;
        LevelSaveData saveData = new LevelSaveData();
        saveData.isLocked = board.isLocked;
        saveData.isCompleted = board.isCompleted;

        foreach (var piece in board.GetSpawnedPieces())
        {
            if (piece == null) continue;
            
            if (piece.IsPlaced)
            {
                // Note: For Tools, PlacedObjectComponent might be null if strictly Infrastructure.
                // But GridBuildingSystem.PlacePieceOnGridExplicit adds PlacedObject component anyway.
                PlacedObject placedObj = piece.GetComponent<PlacedObject>();
                if (placedObj != null)
                {
                    saveData.onGridPieces.Add(new PiecePlacementData 
                    { 
                        pieceTypeName = piece.PieceTypeSO.name, 
                        origin = placedObj.Origin, 
                        direction = placedObj.Direction,
                        heldItemTypeName = piece.HasItem ? piece.HeldItem.PieceTypeSO.name : ""
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
        SaveSystem.SaveLevelProgress(board.boardId, saveData);
    }

    public void ClearLocation()
    {
        foreach (var board in _activeLocationBoards)
        {
            if (board != null) board.Clear();
            if (board != null) Destroy(board.gameObject);
        }
        _activeLocationBoards.Clear();
        _allSpawnedPieces.Clear();
        CommandHistory.Clear();
    }

    public void ClearAllBoards()
    {
        // Clear all boards and pieces
        foreach (var board in _activeLocationBoards)
        {
            if (board != null) board.Clear();
            if (board != null) Destroy(board.gameObject);
        }
        _activeLocationBoards.Clear();
        _allSpawnedPieces.Clear();
        CommandHistory.Clear();
    }

    public void ClearBoardsAfter(int targetIndex)
    {
        // Remove boards with index >= targetIndex
        for (int i = _activeLocationBoards.Count - 1; i >= 0; i--)
        {
            var board = _activeLocationBoards[i];
            if (board == null) continue;
            
            // Extract index from boardId (format: "LocationName_Index")
            string[] parts = board.boardId.Split('_');
            if (parts.Length >= 2 && int.TryParse(parts[parts.Length - 1], out int boardIndex))
            {
                if (boardIndex >= targetIndex)
                {
                    board.Clear();
                    Destroy(board.gameObject);
                    _activeLocationBoards.RemoveAt(i);
                }
            }
        }
        
        // Clean up pieces that no longer have a board
        _allSpawnedPieces.RemoveAll(p => p == null || p.OwnerBoard == null);
        CommandHistory.Clear();
    }

    private void TeleportPieceAndResetPhysics(PuzzlePiece piece, Vector3 position, Quaternion rotation)
    {
        var softBones = piece.GetComponentsInChildren<EZSoftBone>();
        foreach (var sb in softBones) sb.enabled = false;
        piece.UpdateTransform(position, rotation);
        foreach (var sb in softBones) { sb.enabled = true; sb.RevertTransforms(); sb.SetRestState(); }
    }

    private void PlacePieceOffGrid(PuzzleBoard board, PuzzlePiece piece, Vector2Int origin, float cellSize)
    {
        if (board == null) return;
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.CurrentDirection);
        Vector3 worldPos = board.Grid.GetWorldPosition(origin.x, origin.y);
        Vector3 finalPos = worldPos + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        TeleportPieceAndResetPhysics(piece, finalPos, piece.transform.rotation);
        piece.SetOffGrid(true, origin);
        board.OffGridTracker.PlacePiece(piece, origin);
    }

    private void FinalizePlacement(PuzzleBoard board, LevelSaveData saveData)
    {
        if (board == null) return;
        List<PuzzlePiece> availablePieces = new List<PuzzlePiece>(board.GetSpawnedPieces());
        
        // 1. Restore from Save (if exists)
        if (saveData != null)
        {
            board.isLocked = saveData.isLocked;
            board.isCompleted = saveData.isCompleted;

            var toolsToPlace = new List<PiecePlacementData>();
            var othersToPlace = new List<PiecePlacementData>();

            foreach (var data in saveData.onGridPieces)
            {
                var p = availablePieces.FirstOrDefault(ap => ap != null && ap.PieceTypeSO.name == data.pieceTypeName);
                if (p != null && p.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid) toolsToPlace.Add(data);
                else othersToPlace.Add(data);
            }
            
            foreach (var pieceData in toolsToPlace) PlacePieceFromSave(board, pieceData, availablePieces);
            foreach (var pieceData in othersToPlace) PlacePieceFromSave(board, pieceData, availablePieces);
            foreach (var pieceData in saveData.offGridPieces) PlacePieceFromSaveOffGrid(board, pieceData, availablePieces);
        }

        // 2. Handle remaining pieces (Fallback to Editor Data or Random)
        float cellSize = board.Grid.GetCellSize();
        List<PuzzlePiece> needsRandomPlacement = new List<PuzzlePiece>();

        var remaining = new List<PuzzlePiece>(availablePieces);
        foreach (var piece in remaining)
        {
            if (piece.EditorPlacement.hasData)
            {
                // Fallback to editor position
                Vector3 targetWorldPos = board.Grid.GetWorldPosition(piece.EditorPlacement.origin.x, piece.EditorPlacement.origin.y);
                Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(piece.EditorPlacement.direction);
                targetWorldPos += new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
                
                if (piece.EditorPlacement.startOnGrid)
                {
                    var po = GridBuildingSystem.Instance.PlacePieceOnGridExplicit(board, piece, piece.EditorPlacement.origin, piece.EditorPlacement.direction);
                    if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
                        piece.SetInfrastructure(po);
                    else
                        piece.SetPlaced(po);
                    
                    TeleportPieceAndResetPhysics(piece, targetWorldPos, piece.transform.rotation);
                    availablePieces.Remove(piece);
                }
                else
                {
                    // It has data but is NOT on grid -> treat as regular piece for random placement
                    needsRandomPlacement.Add(piece);
                }
           }
            else
            {
                needsRandomPlacement.Add(piece);
            }
        }

        // 3. Random placement for generated pieces
        if (needsRandomPlacement.Count > 0)
        {
            PlacePiecesAroundBoard(board, board.LevelData, needsRandomPlacement);
        }

        CommandHistory.Clear();
    }

    private void PlacePieceFromSave(PuzzleBoard board, PiecePlacementData data, List<PuzzlePiece> availablePieces)
    {
        PuzzlePiece piece = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.pieceTypeName);
        if (piece != null)
        {
            Vector3 targetWorldPos = board.Grid.GetWorldPosition(data.origin.x, data.origin.y);
            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(data.direction);
            targetWorldPos += new Vector3(rotationOffset.x, 0, rotationOffset.y) * board.Grid.GetCellSize();
            piece.UpdateTransform(targetWorldPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(data.direction), 0));
            var po = GridBuildingSystem.Instance.PlacePieceOnGridExplicit(board, piece, data.origin, data.direction);
            if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
                piece.SetInfrastructure(po);
            else
                piece.SetPlaced(po);
            
            TeleportPieceAndResetPhysics(piece, targetWorldPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(data.direction), 0));
            availablePieces.Remove(piece);

            // Restore Held Item
            if (!string.IsNullOrEmpty(data.heldItemTypeName))
            {
                PuzzlePiece heldItem = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.heldItemTypeName);
                if (heldItem != null)
                {
                    piece.AttachItem(heldItem);
                    availablePieces.Remove(heldItem);
                }
            }
        }
    }

    private void PlacePieceFromSaveOffGrid(PuzzleBoard board, PiecePlacementData data, List<PuzzlePiece> availablePieces)
    {
        PuzzlePiece piece = availablePieces.FirstOrDefault(p => p != null && p.PieceTypeSO.name == data.pieceTypeName);
        if (piece != null && board != null)
        {
            float cellSize = board.Grid.GetCellSize();
            Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(data.direction);
            Vector3 worldOrigin = board.Grid.GetWorldPosition(data.origin.x, data.origin.y);
            Vector3 finalPos = worldOrigin + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            TeleportPieceAndResetPhysics(piece, finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(data.direction), 0));
            piece.SetOffGrid(true, data.origin);
            board.OffGridTracker.PlacePiece(piece, data.origin);
            availablePieces.Remove(piece);
        }
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
