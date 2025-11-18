using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "GridData", menuName = "GridBuildingSystem/Grid Data", order = 2)]
public class GridDataSO : ScriptableObject
{
    // --- Внутрішні структури даних для серіалізації ---
    [Serializable]
    public class GeneratedPieceData
    {
        public PlacedObjectTypeSO pieceType;
        public Vector2Int position;
        public PlacedObjectTypeSO.Dir direction;
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

        // --- НОВЕ ПОЛЕ ---
        [Tooltip("Максимальна кількість таких фігур у згенерованому пазлі. 1 = унікальна.")]
        [Min(1)] public int maxCount;
    }

    [Serializable]
    public class SolutionWrapper
    {
        public List<GeneratedPieceData> solution;
        public SolutionWrapper(List<GeneratedPieceData> solutionData) { this.solution = solutionData; }
    }


    [Header("Grid Settings")]
    public int width = 10;
    public int height = 10;
    public float cellSize = 1f;
    public List<Vector2Int> buildableCells = new List<Vector2Int>();

    [Header("Piece Spawning Settings")]
    [Tooltip("Мінімальний гарантований відступ від краю ігрового поля (в клітинках).")]
    [Range(1, 10)]
    public int boardToSpawnPadding = 2;

    [Tooltip("Мінімальний гарантований проміжок між фігурами (в клітинках).")]
    [Range(1, 5)]
    public int pieceToPiecePadding = 1;

    [Tooltip("Наскільки далеко від поля можуть з'являтися фігури. Зменште для більш щільного спавну.")]
    [Range(1, 20)]
    public int maxSpawnRadius = 5;

    [Tooltip("Кількість спроб знайти випадкове місце для кожної фігури.")]
    public int placementAttempts = 100;

    [Header("Personality Settings")]
    [Tooltip("Асет, що зберігає налаштування характерів для цього рівня.")]
    public LevelPersonalitySO personalityData;


    [Header("Puzzle Generator Setup")]
    public List<PlacedObjectTypeSO> availablePieceTypesForGeneration;
    public List<GeneratorPieceConfig> generatorPieceConfig;

    [Header("Generated Puzzle Data (Read-Only)")]
    public List<PlacedObjectTypeSO> puzzlePieces;
    public List<PieceCount> generatedPieceSummary;
    public List<GeneratedPieceData> puzzleSolution;

    [Tooltip("Is the puzzle currently in a complete, solvable state? This is set to false if pieces are manually removed.")]
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
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                buildableCells.Add(new Vector2Int(x, z));
            }
        }
    }
}
