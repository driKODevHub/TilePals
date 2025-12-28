using UnityEngine;
using System.Collections.Generic;
using TilePals.Grid;

public class PuzzleBoard : MonoBehaviour
{
    [Header("Configuration")]
    [Tooltip("Data for this specific puzzle level")]
    [SerializeField] private GridDataSO levelData;
    
    [Header("State")]
    public bool isLocked = false;
    public bool isCompleted = false;

    [Header("Persistence")]
    public string boardId;

    [Header("Visuals")]
    [SerializeField] private Transform spawnedEnvironment;
    [SerializeField] private Transform originPivot;

    // The Grid instance for this board
    public GridXZ<GridObject> Grid { get; private set; }
    
    // Public Accessors
    public GridDataSO LevelData => levelData;
    public Transform Pivot => originPivot != null ? originPivot : transform;
    public OffGridHandler OffGridTracker { get; private set; } = new OffGridHandler();

    // To track pieces belonging to this board
    private List<PuzzlePiece> spawnedPieces = new List<PuzzlePiece>();

    // Derived properties
    public Vector3 OriginPosition => Pivot.position;
    public Quaternion OriginRotation => Pivot.rotation;

    private void Awake()
    {
        if (levelData != null)
        {
            InitializeBoard(levelData);
        }
    }

    public void InitializeBoard(GridDataSO data)
    {
        levelData = data;
        
        if (levelData == null) return;

        // 1. Manage Environment Visuals
        if (data.environmentPrefab != null)
        {
            if (spawnedEnvironment != null) Destroy(spawnedEnvironment.gameObject);
            
            GameObject envObj = Instantiate(data.environmentPrefab, transform.position + data.levelSpawnOffset, Quaternion.identity, transform);
            spawnedEnvironment = envObj.transform;

            // 2. Find Grid Anchor
            GridAnchor anchor = envObj.GetComponentInChildren<GridAnchor>();
            if (anchor != null)
            {
                originPivot = anchor.AnchorPoint;
            }
            else
            {
                Debug.LogWarning($"PuzzleBoard: No GridAnchor found in environment prefab for level {data.name}. Using board transform.");
                originPivot = transform;
            }
        }

        // 3. Create the GridXZ relative to our Origin
        Grid = new GridXZ<GridObject>(
            levelData.width, 
            levelData.height, 
            levelData.cellSize, 
            OriginPosition, 
            (GridXZ<GridObject> g, int x, int z) => new GridObject(g, x, z)
        );

        // 4. Populate initial state (blocked cells, buildable state, etc)
        for (int x = 0; x < levelData.width; x++)
        {
            for (int z = 0; z < levelData.height; z++)
            {
                bool isBuildable = levelData.buildableCells.Contains(new Vector2Int(x, z));
                Grid.GetGridObject(x, z).SetBuildable(isBuildable);
            }
        }

        foreach (var lockedPos in levelData.lockedCells)
        {
            var gridObj = Grid.GetGridObject(lockedPos.x, lockedPos.y);
            if (gridObj != null) gridObj.SetLocked(true);
        }
    }

    public void RegisterPiece(PuzzlePiece piece)
    {
        if (!spawnedPieces.Contains(piece)) spawnedPieces.Add(piece);
    }
    
    public void UnregisterPiece(PuzzlePiece piece)
    {
        if (spawnedPieces.Contains(piece)) spawnedPieces.Remove(piece);
    }

    public List<PuzzlePiece> GetSpawnedPieces() => spawnedPieces;

    public void SetLocked(bool state)
    {
        isLocked = state;
    }

    public void SetCompleted(bool state)
    {
        isCompleted = state;
    }

    public void Clear()
    {
        foreach (var piece in spawnedPieces)
        {
            if (piece != null) Destroy(piece.gameObject);
        }
        spawnedPieces.Clear();
        
        if (spawnedEnvironment != null) Destroy(spawnedEnvironment.gameObject);
        
        if (OffGridTracker != null) OffGridTracker.Clear();
    }

    private void OnDrawGizmos()
    {
        if (levelData != null && Pivot != null)
        {
            Gizmos.color = isCompleted ? Color.green : (isLocked ? Color.red : Color.white);
            Vector3 origin = Pivot.position;
            
            float w = levelData.width * levelData.cellSize;
            float h = levelData.height * levelData.cellSize;
            
            Vector3 p0 = origin;
            Vector3 p1 = origin + new Vector3(w, 0, 0);
            Vector3 p2 = origin + new Vector3(w, 0, h);
            Vector3 p3 = origin + new Vector3(0, 0, h);

            Gizmos.DrawLine(p0, p1);
            Gizmos.DrawLine(p1, p2);
            Gizmos.DrawLine(p2, p3);
            Gizmos.DrawLine(p3, p0);
        }
    }
}
