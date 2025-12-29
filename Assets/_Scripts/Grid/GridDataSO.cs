using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "GridData", menuName = "GridBuildingSystem/Grid Data", order = 2)]
public class GridDataSO : ScriptableObject
{
    public event Action OnValuesChanged;

    private void OnValidate()
    {
        OnValuesChanged?.Invoke();
    }

    public void TriggerOnValuesChanged()
    {
        OnValuesChanged?.Invoke();
    }

    [Serializable]
    public class GeneratedPieceData
    {
        public PlacedObjectTypeSO pieceType;
        public Vector2Int position;
        public PlacedObjectTypeSO.Dir direction;
        public bool isObstacle; // Items that occupy grid but don't count towards win
        public bool isHidden;   // Pieces that are hidden in containers
        public bool startOnGrid = true; // Whether to spawn exactly at coordinates or randomly off-grid
    }

    [Serializable]
    public struct PieceCount
    {
        public PlacedObjectTypeSO pieceType;
        public int count;
    }

    [Serializable]
    public struct GeneratorPieceConfig
    {
        public PlacedObjectTypeSO pieceType;
        public bool isRequired;
        [Min(1)] public int requiredCount;
        public Color color;
        [Min(1)] public int maxCount;
        public bool isObstacle;
    }

    [Serializable]
    public class SolutionWrapper
    {
        public List<GeneratedPieceData> solution;
        public SolutionWrapper(List<GeneratedPieceData> solutionData) { this.solution = solutionData; }
    }

    [Header("Environment & Spawning")]
    public GameObject environmentPrefab;
    public Vector3 levelSpawnOffset;

    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;

    public List<Vector2Int> buildableCells = new List<Vector2Int>();
    public List<Vector2Int> lockedCells = new List<Vector2Int>();

    [Header("Camera Settings (Boundaries)")]
    public Vector2 cameraBoundsCenter;
    public Vector2 cameraBoundsSize = new Vector2(20, 20);
    public float cameraBoundsYRotation = 0f;

    [Header("Piece Spawning Settings")]
    [Range(1, 10)] public int boardToSpawnPadding = 2;
    [Range(1, 5)] public int pieceToPiecePadding = 1;
    [Range(1, 20)] public int maxSpawnRadius = 5;
    public int placementAttempts = 100;

    [Header("Personality Settings")]
    public LevelPersonalitySO personalityData;

    [Header("Puzzle Generator Setup")]
    public List<PlacedObjectTypeSO> availablePieceTypesForGeneration;
    public List<GeneratorPieceConfig> generatorPieceConfig;

    [Header("Level Items (Spawn Always)")]
    [Tooltip("Pieces that always spawn in the level (utility, deco).")]
    public List<GeneratedPieceData> levelItems = new List<GeneratedPieceData>();

    [Header("Obstacles (Static pieces)")]
    public List<GeneratedPieceData> staticObstacles = new List<GeneratedPieceData>();

    [Header("Generated Puzzle Data (Read-Only)")]
    public List<PlacedObjectTypeSO> puzzlePieces; 
    public List<PieceCount> generatedPieceSummary;
    public List<GeneratedPieceData> puzzleSolution;

    public bool isComplete = true;

    [Header("Solution Analysis")]
    public int solutionVariantsCount;

    [HideInInspector]
    public List<SolutionWrapper> allFoundSolutions;

    [HideInInspector]
    public int currentSolutionIndex;

    public void InitializeDefaultBuildableCells()
    {
        buildableCells.Clear();
        lockedCells.Clear();
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                buildableCells.Add(new Vector2Int(x, z));
            }
        }
    }
}
