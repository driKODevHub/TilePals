using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;

[CustomEditor(typeof(GridDataSO))]
public class GridDataSOEditor : Editor
{
    private GridDataSO gridDataSO;
    private bool[,] editorGridCells;

    private bool showRequiredPieces = true;
    private bool showAllPieces = true;
    private bool enableDebugLogs = false;

    private static int smallPieceMaxCells = 3;
    private static int mediumPieceMaxCells = 5;
    private static int desiredSmallFillers = 5;
    private static int desiredMediumFillers = 5;
    private static int desiredLargeFillers = 5;
    private float generationTimeout = 10f;

    private bool useCalculationTimeout = true;
    private float calculationTimeout = 30f;
    private int maxSolutionsToStore = 500;
    private int solutionToShowIndex = 1;

    private bool findAllPermutations = false;
    private HashSet<string> _uniqueSolutionHashes;

    private Stopwatch stopwatch;
    private int _solutionCounter;
    private List<GridDataSO.SolutionWrapper> _allSolutionsList;
    private GUIStyle labelStyle;

    private bool _calculationStopped;

    private int _globalMaxCount = 1;


    private void OnEnable()
    {
        gridDataSO = (GridDataSO)target;
        if (gridDataSO.width <= 0) gridDataSO.width = 1;
        if (gridDataSO.height <= 0) gridDataSO.height = 1;
        InitializeEditorGrid();
        solutionToShowIndex = gridDataSO.currentSolutionIndex + 1;

        labelStyle = new GUIStyle
        {
            alignment = TextAnchor.MiddleCenter,
            fontSize = 10,
            fontStyle = FontStyle.Bold
        };
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        int oldWidth = gridDataSO.width;
        int oldHeight = gridDataSO.height;

        DrawPropertiesExcluding(serializedObject, "m_Script", "puzzlePieces", "generatedPieceSummary", "puzzleSolution", "availablePieceTypesForGeneration", "generatorPieceConfig", "solutionVariantsCount", "allFoundSolutions", "currentSolutionIndex", "isComplete", "personalityData");

        if (gridDataSO.width != oldWidth || gridDataSO.height != oldHeight)
        {
            InitializeEditorGrid();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Buildable Cells Editor", EditorStyles.boldLabel);
        DrawBuildableCellsEditor();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Personality Settings", EditorStyles.boldLabel);
        DrawPersonalityEditor();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("Puzzle Generator", EditorStyles.boldLabel);

        EditorGUILayout.PropertyField(serializedObject.FindProperty("availablePieceTypesForGeneration"), true);
        if (GUILayout.Button(new GUIContent("Update & Manage Generator Pieces", "Синхронізує список фігур нижче з тими, що вказані у 'Available Piece Types'. Додає нові та зберігає налаштування для існуючих."))) UpdateGeneratorPieceConfig();

        DrawMaxCountControls();

        EditorGUILayout.Space();
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("Deselect All Required", "Знімає позначку 'Is Required' з усіх фігур у списку."))) DeselectAllRequired();
        if (GUILayout.Button(new GUIContent("Reset All Counts", "Встановлює значення 'Count' на 1 для всіх обов'язкових фігур."))) ResetAllCounts();
        EditorGUILayout.EndHorizontal();

        showRequiredPieces = EditorGUILayout.Foldout(showRequiredPieces, "Required Pieces", true);
        if (showRequiredPieces) DrawGeneratorPieceConfigList(true);
        showAllPieces = EditorGUILayout.Foldout(showAllPieces, "All Available Pieces", true);
        if (showAllPieces) DrawGeneratorPieceConfigList(false);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Generator Settings", EditorStyles.boldLabel);
        generationTimeout = EditorGUILayout.FloatField(new GUIContent("Generation Timeout (Sec)", "Максимальний час у секундах, який генератор витратить на пошук ОДНОГО рішення."), generationTimeout);
        enableDebugLogs = EditorGUILayout.Toggle(new GUIContent("Enable Debug Logs", "Вмикає вивід детальної інформації про процес генерації в консоль. Може сповільнювати роботу."), enableDebugLogs);
        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Filler Piece Distribution Control", EditorStyles.boldLabel);
        smallPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Small Piece Max Cells", "Максимальна кількість клітинок, щоб фігура вважалася 'маленькою'."), smallPieceMaxCells);
        mediumPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Medium Piece Max Cells", "Максимальна кількість клітинок, щоб фігура вважалася 'середньою'."), mediumPieceMaxCells);
        desiredSmallFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Small Fillers", "Скільки маленьких допоміжних фігур буде використано при генерації."), desiredSmallFillers, 0, 20);
        desiredMediumFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Medium Fillers", "Скільки середніх допоміжних фігур буде використано при генерації."), desiredMediumFillers, 0, 20);
        desiredLargeFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Large Fillers", "Скільки великих допоміжних фігур буде використано при генерації."), desiredLargeFillers, 0, 20);

        if (GUILayout.Button(new GUIContent("Generate Puzzle Solution", "Запускає повну генерацію пазла з нуля на основі поточних налаштувань.")))
        {
            GeneratePuzzle();
        }

        if (gridDataSO.puzzleSolution != null && gridDataSO.puzzleSolution.Count > 0)
        {
            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generated Puzzle Preview", EditorStyles.boldLabel);
            EditorGUILayout.HelpBox("Right-click on a piece to remove it.", MessageType.Info);
            DrawPuzzlePreview();

            EditorGUILayout.Space(5);
            EditorGUILayout.LabelField("Manual Editing", EditorStyles.boldLabel);
            if (!gridDataSO.isComplete)
            {
                EditorGUILayout.HelpBox("Puzzle is incomplete! You can try to fill the empty space or regenerate the whole puzzle.", MessageType.Warning);
                if (GUILayout.Button(new GUIContent("Fill Empty Space", "Намагається заповнити порожні місця на полі, не змінюючи існуючі фігури.")))
                {
                    FillEmptySpace();
                }
            }
            if (GUILayout.Button(new GUIContent("Clear All Pieces", "Видаляє всі фігури з поточного рішення.")))
            {
                if (EditorUtility.DisplayDialog("Clear All Pieces?", "Are you sure you want to remove all pieces from this puzzle solution?", "Yes", "No"))
                {
                    ClearAllPieces();
                }
            }


            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Generated Piece Summary", EditorStyles.boldLabel);
            DrawPieceSummary();

            EditorGUILayout.Space(10);
            EditorGUILayout.LabelField("Solution Analysis", EditorStyles.boldLabel);

            string variantsLabel = $"Found: {gridDataSO.solutionVariantsCount} (Stored: {gridDataSO.allFoundSolutions.Count})";
            EditorGUILayout.LabelField(variantsLabel);

            useCalculationTimeout = EditorGUILayout.Toggle(new GUIContent("Use Timeout", "Якщо увімкнено, розрахунок зупиниться після вказаного часу, зберігши знайдені результати."), useCalculationTimeout);
            if (useCalculationTimeout)
            {
                calculationTimeout = EditorGUILayout.FloatField(new GUIContent("Calculation Timeout (Sec)", "Максимальний час у секундах на пошук всіх рішень."), calculationTimeout);
            }

            maxSolutionsToStore = EditorGUILayout.IntField(new GUIContent("Max Solutions to Store", "Обмеження на кількість рішень, що зберігаються в пам'яті, щоб уникнути зависання редактора."), maxSolutionsToStore);

            findAllPermutations = EditorGUILayout.Toggle(new GUIContent("Find All Permutations", "Якщо увімкнено, зберігає абсолютно всі рішення, включаючи перестановки однакових фігур. Якщо вимкнено - зберігає тільки візуально унікальні розкладки."), findAllPermutations);

            if (GUILayout.Button(new GUIContent("Calculate Solution Variants", "Запускає пошук всіх можливих варіантів вирішення пазла з поточним набором фігур.")))
            {
                CalculateSolutions();
            }

            if (gridDataSO.allFoundSolutions != null && gridDataSO.allFoundSolutions.Count > 1)
            {
                DrawSolutionNavigator();
            }
        }

        serializedObject.ApplyModifiedProperties();
    }

    private void DrawMaxCountControls()
    {
        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Керування кількістю фігур", EditorStyles.boldLabel);

        _globalMaxCount = EditorGUILayout.IntField(
            new GUIContent("Глобальний ліміт", "Значення, яке буде застосовано до всіх фігур за допомогою кнопок нижче."),
            _globalMaxCount);
        if (_globalMaxCount < 1) _globalMaxCount = 1;

        EditorGUILayout.BeginHorizontal();

        if (GUILayout.Button(new GUIContent("Встановити для всіх", $"Встановлює 'Max Count' для всіх фігур у списку 'Generator Piece Config' на {_globalMaxCount}.")))
        {
            if (gridDataSO.generatorPieceConfig != null)
            {
                for (int i = 0; i < gridDataSO.generatorPieceConfig.Count; i++)
                {
                    var config = gridDataSO.generatorPieceConfig[i];
                    config.maxCount = _globalMaxCount;
                    gridDataSO.generatorPieceConfig[i] = config;
                }
                EditorUtility.SetDirty(gridDataSO);
                UnityEngine.Debug.Log($"Встановлено ліміт {_globalMaxCount} для всіх фігур.");
            }
        }

        if (GUILayout.Button(new GUIContent("Скинути (всі по 1)", "Встановлює 'Max Count' для всіх фігур на стандартне значення 1.")))
        {
            if (gridDataSO.generatorPieceConfig != null)
            {
                for (int i = 0; i < gridDataSO.generatorPieceConfig.Count; i++)
                {
                    var config = gridDataSO.generatorPieceConfig[i];
                    config.maxCount = 1;
                    gridDataSO.generatorPieceConfig[i] = config;
                }
                EditorUtility.SetDirty(gridDataSO);
                UnityEngine.Debug.Log("Скинуто ліміти для всіх фігур. Тепер кожна фігура унікальна.");
            }
        }
        EditorGUILayout.EndHorizontal();
    }


    #region Personality Editor
    private void DrawPersonalityEditor()
    {
        SerializedProperty personalityProp = serializedObject.FindProperty("personalityData");
        EditorGUILayout.PropertyField(personalityProp);

        if (personalityProp.objectReferenceValue == null)
        {
            if (GUILayout.Button("Create and Assign Personality Data"))
            {
                CreatePersonalityData();
            }
            EditorGUILayout.HelpBox("Налаштування характерів для рівня не створено. Натисніть кнопку, щоб створити.", MessageType.Warning);
            return;
        }

        EditorGUILayout.BeginVertical("box");

        LevelPersonalitySO personalityData = (LevelPersonalitySO)personalityProp.objectReferenceValue;
        SerializedObject personalitySerializedObject = new SerializedObject(personalityData);
        SerializedProperty mappingsProp = personalitySerializedObject.FindProperty("personalityMappings");

        var requiredPieceTypes = gridDataSO.generatedPieceSummary.Select(s => s.pieceType).Where(p => p != null).Distinct().ToList();
        var mappedPieceTypes = personalityData.personalityMappings.Where(m => m.pieceType != null).Select(m => m.pieceType).ToList();

        bool isSynced = requiredPieceTypes.Count == mappedPieceTypes.Count && requiredPieceTypes.All(mappedPieceTypes.Contains);

        if (!isSynced)
        {
            EditorGUILayout.HelpBox("Склад фігур у рівні не співпадає з налаштуваннями характерів!", MessageType.Error);
            // --- ОНОВЛЕНА КНОПКА ---
            if (GUILayout.Button("Примусово синхронізувати характери"))
            {
                SyncPersonalityMappings(personalityData, requiredPieceTypes);
            }
        }
        else
        {
            EditorGUILayout.HelpBox("Характери синхронізовано.", MessageType.Info);
        }

        for (int i = 0; i < mappingsProp.arraySize; i++)
        {
            SerializedProperty mappingElement = mappingsProp.GetArrayElementAtIndex(i);
            SerializedProperty pieceTypeProp = mappingElement.FindPropertyRelative("pieceType");
            SerializedProperty temperamentProp = mappingElement.FindPropertyRelative("temperament");

            EditorGUILayout.BeginHorizontal();
            GUI.enabled = false;
            EditorGUILayout.ObjectField(pieceTypeProp.objectReferenceValue, typeof(PlacedObjectTypeSO), false, GUILayout.Width(150));
            GUI.enabled = true;
            EditorGUILayout.PropertyField(temperamentProp, GUIContent.none);
            EditorGUILayout.EndHorizontal();
        }

        EditorGUILayout.Space();
        if (GUILayout.Button("Рандомізувати непризначені"))
        {
            RandomizeUnassignedTemperaments(personalityData);
        }

        personalitySerializedObject.ApplyModifiedProperties();
        EditorGUILayout.EndVertical();
    }

    private void CreatePersonalityData()
    {
        LevelPersonalitySO newPersonalityData = CreateInstance<LevelPersonalitySO>();

        string path = AssetDatabase.GetAssetPath(gridDataSO);
        if (string.IsNullOrEmpty(path))
        {
            path = "Assets/";
        }
        string dirPath = System.IO.Path.GetDirectoryName(path);
        string personalityPath = AssetDatabase.GenerateUniqueAssetPath(System.IO.Path.Combine(dirPath, $"{gridDataSO.name}_Personality.asset"));

        AssetDatabase.CreateAsset(newPersonalityData, personalityPath);
        AssetDatabase.SaveAssets();

        gridDataSO.personalityData = newPersonalityData;
        EditorUtility.SetDirty(gridDataSO);
    }

    private void SyncPersonalityMappings(LevelPersonalitySO pData, List<PlacedObjectTypeSO> requiredPieces)
    {
        var currentMappings = pData.personalityMappings.ToDictionary(m => m.pieceType, m => m.temperament);
        pData.personalityMappings.Clear();

        foreach (var pieceType in requiredPieces)
        {
            currentMappings.TryGetValue(pieceType, out TemperamentSO temperament);
            pData.personalityMappings.Add(new LevelPersonalitySO.PersonalityMapping
            {
                pieceType = pieceType,
                temperament = temperament
            });
        }
        EditorUtility.SetDirty(pData);
    }

    private void RandomizeUnassignedTemperaments(LevelPersonalitySO pData)
    {
        string[] guids = AssetDatabase.FindAssets("t:TemperamentSO");
        if (guids.Length == 0)
        {
            UnityEngine.Debug.LogWarning("Не знайдено жодного асету TemperamentSO для рандомізації.");
            return;
        }

        List<TemperamentSO> allTemperaments = guids
            .Select(guid => AssetDatabase.LoadAssetAtPath<TemperamentSO>(AssetDatabase.GUIDToAssetPath(guid)))
            .ToList();

        for (int i = 0; i < pData.personalityMappings.Count; i++)
        {
            var mapping = pData.personalityMappings[i];
            if (mapping.temperament == null)
            {
                mapping.temperament = allTemperaments[Random.Range(0, allTemperaments.Count)];
                pData.personalityMappings[i] = mapping;
            }
        }
        EditorUtility.SetDirty(pData);
    }
    #endregion

    #region Buildable Cells Editor
    private void InitializeEditorGrid()
    {
        editorGridCells = new bool[gridDataSO.width, gridDataSO.height];
        if (gridDataSO.buildableCells != null)
        {
            foreach (Vector2Int cell in gridDataSO.buildableCells)
            {
                if (cell.x >= 0 && cell.x < gridDataSO.width && cell.y >= 0 && cell.y < gridDataSO.height)
                {
                    editorGridCells[cell.x, cell.y] = true;
                }
            }
        }
    }

    private void DrawBuildableCellsEditor()
    {
        EditorGUILayout.HelpBox("Click cells to toggle their buildable status. Green means buildable, Red means not buildable.", MessageType.Info);
        GUILayout.BeginHorizontal();
        if (GUILayout.Button("Select All Buildable")) SelectAllBuildableCells(true);
        if (GUILayout.Button("Deselect All Buildable")) SelectAllBuildableCells(false);
        GUILayout.EndHorizontal();
        EditorGUILayout.Space(5);
        GUILayout.BeginVertical("box");
        for (int z = gridDataSO.height - 1; z >= 0; z--)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < gridDataSO.width; x++)
            {
                GUI.backgroundColor = editorGridCells[x, z] ? Color.green : Color.red;
                if (GUILayout.Button("", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    editorGridCells[x, z] = !editorGridCells[x, z];
                    UpdateBuildableCellsList();
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUI.backgroundColor = Color.white;
    }

    private void UpdateBuildableCellsList()
    {
        var buildableCellsProp = serializedObject.FindProperty("buildableCells");
        buildableCellsProp.ClearArray();
        int index = 0;
        for (int x = 0; x < gridDataSO.width; x++)
        {
            for (int z = 0; z < gridDataSO.height; z++)
            {
                if (editorGridCells[x, z])
                {
                    buildableCellsProp.InsertArrayElementAtIndex(index);
                    buildableCellsProp.GetArrayElementAtIndex(index).vector2IntValue = new Vector2Int(x, z);
                    index++;
                }
            }
        }
    }

    private void SelectAllBuildableCells(bool select)
    {
        for (int x = 0; x < gridDataSO.width; x++)
        {
            for (int z = 0; z < gridDataSO.height; z++)
            {
                editorGridCells[x, z] = select;
            }
        }
        UpdateBuildableCellsList();
    }
    #endregion

    #region Solution Counting & Navigation Logic

    private void CalculateSolutions()
    {
        if (gridDataSO.puzzlePieces == null || gridDataSO.puzzlePieces.Count == 0)
        {
            UnityEngine.Debug.LogWarning("First, generate a puzzle solution.");
            return;
        }

        bool[,] initialGrid = new bool[gridDataSO.width, gridDataSO.height];
        foreach (var cell in gridDataSO.buildableCells)
        {
            if (cell.x < gridDataSO.width && cell.y < gridDataSO.height)
                initialGrid[cell.x, cell.y] = true;
        }

        var pieceCounts = gridDataSO.puzzlePieces.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
        var uniquePieces = pieceCounts.Keys.OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();

        _solutionCounter = 0;
        _allSolutionsList = new List<GridDataSO.SolutionWrapper>();
        _uniqueSolutionHashes = new HashSet<string>();
        _calculationStopped = false;
        stopwatch = Stopwatch.StartNew();

        CountSolutionsRecursive(initialGrid, new List<GridDataSO.GeneratedPieceData>(), pieceCounts, uniquePieces);

        stopwatch.Stop();

        int previousSolutionIndex = gridDataSO.currentSolutionIndex;

        serializedObject.FindProperty("solutionVariantsCount").intValue = _solutionCounter;

        var allFoundSolutionsProp = serializedObject.FindProperty("allFoundSolutions");
        allFoundSolutionsProp.ClearArray();
        for (int i = 0; i < _allSolutionsList.Count; i++)
        {
            allFoundSolutionsProp.InsertArrayElementAtIndex(i);
            var solutionProp = allFoundSolutionsProp.GetArrayElementAtIndex(i).FindPropertyRelative("solution");
            var solutionData = _allSolutionsList[i].solution;
            solutionProp.ClearArray();
            for (int j = 0; j < solutionData.Count; j++)
            {
                solutionProp.InsertArrayElementAtIndex(j);
                var pieceDataProp = solutionProp.GetArrayElementAtIndex(j);
                pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue = solutionData[j].pieceType;
                pieceDataProp.FindPropertyRelative("position").vector2IntValue = solutionData[j].position;
                pieceDataProp.FindPropertyRelative("direction").enumValueIndex = (int)solutionData[j].direction;
            }
        }

        string messageType = !findAllPermutations ? "unique layouts" : "total permutations";
        string stopMessage = _calculationStopped ? " (stopped by timeout)" : "";
        UnityEngine.Debug.Log($"<color=cyan>Calculation finished in {stopwatch.Elapsed.TotalSeconds:F2} sec{stopMessage}. Found {_solutionCounter} {messageType}. Stored: {_allSolutionsList.Count}.</color>");

        SetSolutionIndex(Mathf.Min(previousSolutionIndex, _allSolutionsList.Count - 1));
    }

    private void CountSolutionsRecursive(bool[,] currentGrid, List<GridDataSO.GeneratedPieceData> currentSolution, Dictionary<PlacedObjectTypeSO, int> pieceCounts, List<PlacedObjectTypeSO> uniquePieces)
    {
        if (_calculationStopped) return;

        if (useCalculationTimeout && stopwatch.Elapsed.TotalSeconds > calculationTimeout)
        {
            _calculationStopped = true;
            return;
        }

        Vector2Int? emptyCell = FindFirstEmptyCell(currentGrid);
        if (emptyCell == null)
        {
            if (!findAllPermutations)
            {
                string hash = GenerateSolutionHash(currentSolution);
                if (_uniqueSolutionHashes.Contains(hash))
                {
                    return;
                }
                _uniqueSolutionHashes.Add(hash);
            }

            _solutionCounter++;
            if (_allSolutionsList.Count < maxSolutionsToStore)
            {
                _allSolutionsList.Add(new GridDataSO.SolutionWrapper(new List<GridDataSO.GeneratedPieceData>(currentSolution)));
            }
            return;
        }

        foreach (var pieceType in uniquePieces)
        {
            if (pieceCounts[pieceType] > 0)
            {
                foreach (PlacedObjectTypeSO.Dir dir in System.Enum.GetValues(typeof(PlacedObjectTypeSO.Dir)))
                {
                    for (int x = -pieceType.GetMaxDimensions().x; x < gridDataSO.width; x++)
                    {
                        for (int z = -pieceType.GetMaxDimensions().y; z < gridDataSO.height; z++)
                        {
                            Vector2Int startPosition = new Vector2Int(x, z);
                            List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);

                            if (piecePositions.Contains(emptyCell.Value) && CanPlace(currentGrid, piecePositions))
                            {
                                Place(currentGrid, piecePositions, false);
                                pieceCounts[pieceType]--;
                                currentSolution.Add(new GridDataSO.GeneratedPieceData { pieceType = pieceType, position = startPosition, direction = dir });

                                CountSolutionsRecursive(currentGrid, currentSolution, pieceCounts, uniquePieces);

                                Place(currentGrid, piecePositions, true);
                                pieceCounts[pieceType]++;
                                currentSolution.RemoveAt(currentSolution.Count - 1);

                                if (_calculationStopped) return;
                            }
                        }
                    }
                }
            }
        }
    }

    private string GenerateSolutionHash(List<GridDataSO.GeneratedPieceData> solution)
    {
        var sortedPieces = solution.OrderBy(p => p.position.x).ThenBy(p => p.position.y);

        StringBuilder stringBuilder = new StringBuilder();
        foreach (var piece in sortedPieces)
        {
            stringBuilder.Append($"{piece.pieceType.name}({piece.position.x},{piece.position.y})D{(int)piece.direction};");
        }
        return stringBuilder.ToString();
    }

    private void DrawSolutionNavigator()
    {
        EditorGUILayout.LabelField("Solution Navigator", EditorStyles.boldLabel);
        int storedCount = gridDataSO.allFoundSolutions.Count;
        EditorGUILayout.LabelField($"Displaying solution: {gridDataSO.currentSolutionIndex + 1} / {storedCount}");

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("<< First", "Перейти до першого знайденого рішення"))) SetSolutionIndex(0);
        if (GUILayout.Button(new GUIContent("< Prev", "Перейти до попереднього рішення"))) SetSolutionIndex(gridDataSO.currentSolutionIndex - 1);
        if (GUILayout.Button(new GUIContent("Next >", "Перейти до наступного рішення"))) SetSolutionIndex(gridDataSO.currentSolutionIndex + 1);
        if (GUILayout.Button(new GUIContent("Last >>", "Перейти до останнього збереженого рішення"))) SetSolutionIndex(storedCount - 1);
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button(new GUIContent("-10", "Перейти на 10 рішень назад"))) SetSolutionIndex(gridDataSO.currentSolutionIndex - 10);
        if (GUILayout.Button(new GUIContent("+10", "Перейти на 10 рішень вперед"))) SetSolutionIndex(gridDataSO.currentSolutionIndex + 10);
        if (GUILayout.Button(new GUIContent("Random", "Перейти до випадкового рішення"))) SetSolutionIndex(Random.Range(0, storedCount));
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        solutionToShowIndex = EditorGUILayout.IntField(new GUIContent("Go to solution:", "Введіть номер рішення та натисніть 'Go'"), solutionToShowIndex);
        if (GUILayout.Button("Go"))
        {
            solutionToShowIndex = Mathf.Clamp(solutionToShowIndex, 1, storedCount);
            SetSolutionIndex(solutionToShowIndex - 1);
        }
        EditorGUILayout.EndHorizontal();
    }

    private void SetSolutionIndex(int newIndex)
    {
        int storedCount = gridDataSO.allFoundSolutions?.Count ?? 0;
        if (storedCount == 0) return;

        int clampedIndex = Mathf.Clamp(newIndex, 0, storedCount - 1);

        serializedObject.FindProperty("currentSolutionIndex").intValue = clampedIndex;
        solutionToShowIndex = clampedIndex + 1;

        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        var newSolutionLayout = gridDataSO.allFoundSolutions[clampedIndex].solution;

        puzzleSolutionProp.ClearArray();

        for (int i = 0; i < newSolutionLayout.Count; i++)
        {
            puzzleSolutionProp.InsertArrayElementAtIndex(i);
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);

            pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue = newSolutionLayout[i].pieceType;
            pieceDataProp.FindPropertyRelative("position").vector2IntValue = newSolutionLayout[i].position;
            pieceDataProp.FindPropertyRelative("direction").enumValueIndex = (int)newSolutionLayout[i].direction;
        }

        serializedObject.ApplyModifiedProperties();

        Repaint();
    }

    #endregion

    #region Puzzle Generation Logic

    private void GeneratePuzzle()
    {
        stopwatch = Stopwatch.StartNew();

        var requiredPiecesConfig = gridDataSO.generatorPieceConfig?.Where(c => c.isRequired && c.pieceType != null).ToList();
        var allAvailableFillersConfig = gridDataSO.generatorPieceConfig?.Where(c => !c.isRequired && c.pieceType != null).ToList();

        if ((requiredPiecesConfig == null || requiredPiecesConfig.Count == 0) && (allAvailableFillersConfig == null || allAvailableFillersConfig.Count == 0))
        {
            UnityEngine.Debug.LogError("No pieces specified for generation! Update the piece list.");
            return;
        }

        List<PlacedObjectTypeSO> curatedFillerPieces = new List<PlacedObjectTypeSO>();
        if (allAvailableFillersConfig != null)
        {
            var allAvailableFillers = allAvailableFillersConfig.Select(c => c.pieceType).ToList();
            var smallFillers = allAvailableFillers.Where(p => p.relativeOccupiedCells.Count <= smallPieceMaxCells).ToList();
            var mediumFillers = allAvailableFillers.Where(p => p.relativeOccupiedCells.Count > smallPieceMaxCells && p.relativeOccupiedCells.Count <= mediumPieceMaxCells).ToList();
            var largeFillers = allAvailableFillers.Where(p => p.relativeOccupiedCells.Count > mediumPieceMaxCells).ToList();

            for (int i = 0; i < desiredSmallFillers && smallFillers.Count > 0; i++) curatedFillerPieces.Add(smallFillers[Random.Range(0, smallFillers.Count)]);
            for (int i = 0; i < desiredMediumFillers && mediumFillers.Count > 0; i++) curatedFillerPieces.Add(mediumFillers[Random.Range(0, mediumFillers.Count)]);
            for (int i = 0; i < desiredLargeFillers && largeFillers.Count > 0; i++) curatedFillerPieces.Add(largeFillers[Random.Range(0, largeFillers.Count)]);
        }

        bool[,] tempGrid = new bool[gridDataSO.width, gridDataSO.height];
        foreach (var cell in gridDataSO.buildableCells)
        {
            if (cell.x < gridDataSO.width && cell.y < gridDataSO.height)
                tempGrid[cell.x, cell.y] = true;
        }

        List<GridDataSO.GeneratedPieceData> solution = new List<GridDataSO.GeneratedPieceData>();
        List<PlacedObjectTypeSO> requiredPiecesToPlace = new List<PlacedObjectTypeSO>();
        if (requiredPiecesConfig != null)
        {
            foreach (var req in requiredPiecesConfig)
            {
                for (int i = 0; i < req.requiredCount; i++) requiredPiecesToPlace.Add(req.pieceType);
            }
        }

        var pieceCountsInSolution = new Dictionary<PlacedObjectTypeSO, int>();
        foreach (var piece in requiredPiecesToPlace)
        {
            pieceCountsInSolution[piece] = pieceCountsInSolution.GetValueOrDefault(piece, 0) + 1;
        }


        var orderedFillerPieces = curatedFillerPieces.OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();
        bool success = SolvePuzzleRecursiveGeneration(tempGrid, solution, requiredPiecesToPlace, orderedFillerPieces, pieceCountsInSolution);
        stopwatch.Stop();

        if (success)
        {
            UnityEngine.Debug.Log($"<color=green>Puzzle generated successfully in {stopwatch.Elapsed.TotalSeconds:F3} seconds!</color>");

            var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
            puzzleSolutionProp.ClearArray();
            for (int i = 0; i < solution.Count; i++)
            {
                puzzleSolutionProp.InsertArrayElementAtIndex(i);
                var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
                pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue = solution[i].pieceType;
                pieceDataProp.FindPropertyRelative("position").vector2IntValue = solution[i].position;
                pieceDataProp.FindPropertyRelative("direction").enumValueIndex = (int)solution[i].direction;
            }

            serializedObject.FindProperty("solutionVariantsCount").intValue = 1;
            serializedObject.FindProperty("currentSolutionIndex").intValue = 0;

            var allFoundSolutionsProp = serializedObject.FindProperty("allFoundSolutions");
            allFoundSolutionsProp.ClearArray();
            allFoundSolutionsProp.InsertArrayElementAtIndex(0);
            var solutionProp = allFoundSolutionsProp.GetArrayElementAtIndex(0).FindPropertyRelative("solution");
            solutionProp.ClearArray();
            for (int j = 0; j < solution.Count; j++)
            {
                solutionProp.InsertArrayElementAtIndex(j);
                var pieceDataProp = solutionProp.GetArrayElementAtIndex(j);
                pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue = solution[j].pieceType;
                pieceDataProp.FindPropertyRelative("position").vector2IntValue = solution[j].position;
                pieceDataProp.FindPropertyRelative("direction").enumValueIndex = (int)solution[j].direction;
            }

            solutionToShowIndex = 1;
            UpdatePuzzleState();
        }
        else
        {
            UnityEngine.Debug.LogError($"Failed to generate puzzle. Timeout ({generationTimeout} sec) or no solution found.");
            ClearAllPieces();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private bool SolvePuzzleRecursiveGeneration(bool[,] currentGrid, List<GridDataSO.GeneratedPieceData> solution, List<PlacedObjectTypeSO> required, List<PlacedObjectTypeSO> fillers, Dictionary<PlacedObjectTypeSO, int> pieceCounts)
    {
        if (stopwatch.Elapsed.TotalSeconds > generationTimeout) return false;
        Vector2Int? emptyCell = FindFirstEmptyCell(currentGrid);
        if (emptyCell == null) return required.Count == 0;

        var piecesToTry = new List<PlacedObjectTypeSO>();
        bool tryingRequired = required.Count > 0;
        if (tryingRequired)
        {
            piecesToTry.AddRange(required.Distinct().OrderByDescending(p => p.relativeOccupiedCells.Count));
        }
        else
        {
            piecesToTry.AddRange(fillers);
        }

        foreach (var pieceType in piecesToTry)
        {
            if (pieceType.relativeOccupiedCells == null || pieceType.relativeOccupiedCells.Count == 0) continue;

            if (!tryingRequired)
            {
                var config = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == pieceType);
                int maxCount = config.pieceType != null ? config.maxCount : int.MaxValue;
                if (pieceCounts.GetValueOrDefault(pieceType, 0) >= maxCount)
                {
                    continue;
                }
            }

            var shuffledDirs = System.Enum.GetValues(typeof(PlacedObjectTypeSO.Dir)).Cast<PlacedObjectTypeSO.Dir>().OrderBy(d => Random.value);
            foreach (PlacedObjectTypeSO.Dir dir in shuffledDirs)
            {
                for (int x = -pieceType.GetMaxDimensions().x; x < gridDataSO.width; x++)
                {
                    for (int z = -pieceType.GetMaxDimensions().y; z < gridDataSO.height; z++)
                    {
                        Vector2Int startPosition = new Vector2Int(x, z);
                        List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);
                        if (piecePositions.Contains(emptyCell.Value) && CanPlace(currentGrid, piecePositions))
                        {
                            var nextRequired = new List<PlacedObjectTypeSO>(required);
                            if (tryingRequired) nextRequired.Remove(pieceType);

                            Place(currentGrid, piecePositions, false);
                            solution.Add(new GridDataSO.GeneratedPieceData { pieceType = pieceType, position = startPosition, direction = dir });
                            pieceCounts[pieceType] = pieceCounts.GetValueOrDefault(pieceType, 0) + 1;

                            if (SolvePuzzleRecursiveGeneration(currentGrid, solution, nextRequired, fillers, pieceCounts)) return true;

                            Place(currentGrid, piecePositions, true);
                            solution.RemoveAt(solution.Count - 1);
                            pieceCounts[pieceType]--;
                        }
                    }
                }
            }
        }
        return false;
    }

    #endregion

    #region Helper & UI Methods

    private void DrawPuzzlePreview()
    {
        float cellSize = 20f;
        Rect previewRect = GUILayoutUtility.GetRect(gridDataSO.width * cellSize, gridDataSO.height * cellSize);

        HandlePreviewMouseInput(previewRect, cellSize);

        if (Event.current.type != EventType.Repaint) return;

        for (int z = 0; z < gridDataSO.height; z++)
        {
            for (int x = 0; x < gridDataSO.width; x++)
            {
                Color bgColor = gridDataSO.buildableCells.Contains(new Vector2Int(x, z)) ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
                Rect cellRect = new Rect(previewRect.x + x * cellSize, previewRect.y + (gridDataSO.height - 1 - z) * cellSize, cellSize - 1, cellSize - 1);
                EditorGUI.DrawRect(cellRect, bgColor);
            }
        }

        if (gridDataSO.puzzleSolution != null)
        {
            var totalPieceCounts = gridDataSO.generatedPieceSummary.ToDictionary(s => s.pieceType, s => s.count);
            var pieceInstanceCounter = new Dictionary<PlacedObjectTypeSO, int>();

            foreach (var pieceData in gridDataSO.puzzleSolution)
            {
                if (pieceData.pieceType == null) continue;

                var colorEntry = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == pieceData.pieceType);

                Color pieceColor = colorEntry.pieceType != null ? colorEntry.color : Color.magenta;

                List<Vector2Int> positions = pieceData.pieceType.GetGridPositionsList(pieceData.position, pieceData.direction);
                foreach (var pos in positions)
                {
                    if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height)
                    {
                        Rect cellRect = new Rect(previewRect.x + pos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - pos.y) * cellSize, cellSize - 1, cellSize - 1);
                        EditorGUI.DrawRect(cellRect, pieceColor);
                    }
                }

                if (totalPieceCounts.TryGetValue(pieceData.pieceType, out int totalCount) && totalCount > 1)
                {
                    if (!pieceInstanceCounter.ContainsKey(pieceData.pieceType))
                    {
                        pieceInstanceCounter[pieceData.pieceType] = 0;
                    }
                    pieceInstanceCounter[pieceData.pieceType]++;
                    int instanceId = pieceInstanceCounter[pieceData.pieceType];

                    if (positions.Count > 0)
                    {
                        Vector2Int labelPos = positions[0];
                        Rect labelCellRect = new Rect(previewRect.x + labelPos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - labelPos.y) * cellSize, cellSize, cellSize);

                        float brightness = (pieceColor.r * 0.299f + pieceColor.g * 0.587f + pieceColor.b * 0.114f);
                        labelStyle.normal.textColor = brightness > 0.5f ? Color.black : Color.white;

                        GUI.Label(labelCellRect, instanceId.ToString(), labelStyle);
                    }
                }
            }
        }
    }


    private void UpdatePuzzleState()
    {
        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        var puzzlePiecesProp = serializedObject.FindProperty("puzzlePieces");
        var summaryProp = serializedObject.FindProperty("generatedPieceSummary");

        var currentSolution = new List<GridDataSO.GeneratedPieceData>();
        for (int i = 0; i < puzzleSolutionProp.arraySize; i++)
        {
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
            var pieceData = new GridDataSO.GeneratedPieceData
            {
                pieceType = (PlacedObjectTypeSO)pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue,
                position = pieceDataProp.FindPropertyRelative("position").vector2IntValue,
                direction = (PlacedObjectTypeSO.Dir)pieceDataProp.FindPropertyRelative("direction").enumValueIndex
            };
            if (pieceData.pieceType != null)
                currentSolution.Add(pieceData);
        }

        var pieceTypes = currentSolution.Select(s => s.pieceType).ToList();
        var summary = currentSolution
            .GroupBy(s => s.pieceType)
            .Select(group => new GridDataSO.PieceCount { pieceType = group.Key, count = group.Count() })
            .OrderByDescending(s => s.pieceType.relativeOccupiedCells.Count)
            .ToList();

        puzzlePiecesProp.ClearArray();
        for (int i = 0; i < pieceTypes.Count; i++)
        {
            puzzlePiecesProp.InsertArrayElementAtIndex(i);
            puzzlePiecesProp.GetArrayElementAtIndex(i).objectReferenceValue = pieceTypes[i];
        }

        summaryProp.ClearArray();
        for (int i = 0; i < summary.Count; i++)
        {
            summaryProp.InsertArrayElementAtIndex(i);
            var summaryElement = summaryProp.GetArrayElementAtIndex(i);
            summaryElement.FindPropertyRelative("pieceType").objectReferenceValue = summary[i].pieceType;
            summaryElement.FindPropertyRelative("count").intValue = summary[i].count;
        }

        int buildableCellCount = gridDataSO.buildableCells.Count;
        int occupiedCellCount = 0;
        foreach (var piece in currentSolution)
        {
            occupiedCellCount += piece.pieceType.relativeOccupiedCells.Count;
        }

        // --- ОНОВЛЕНА ЛОГІКА ---
        // Завжди оновлюємо прапорець isComplete на основі поточного стану
        serializedObject.FindProperty("isComplete").boolValue = (buildableCellCount == occupiedCellCount);
    }

    private Vector2Int? FindFirstEmptyCell(bool[,] grid)
    {
        for (int y = 0; y < gridDataSO.height; y++)
        {
            for (int x = 0; x < gridDataSO.width; x++)
            {
                if (grid[x, y]) return new Vector2Int(x, y);
            }
        }
        return null;
    }

    private void DrawPieceSummary()
    {
        var summaryProp = serializedObject.FindProperty("generatedPieceSummary");
        for (int i = 0; i < summaryProp.arraySize; i++)
        {
            var summaryElement = summaryProp.GetArrayElementAtIndex(i);
            var pieceType = (PlacedObjectTypeSO)summaryElement.FindPropertyRelative("pieceType").objectReferenceValue;
            var count = summaryElement.FindPropertyRelative("count").intValue;

            if (pieceType == null) continue;

            EditorGUILayout.BeginHorizontal();
            Rect colorRect = GUILayoutUtility.GetRect(18, 18);
            if (Event.current.type == EventType.Repaint)
            {
                var colorEntry = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == pieceType);
                Color pieceColor = colorEntry.pieceType != null ? colorEntry.color : Color.magenta;
                EditorGUI.DrawRect(colorRect, pieceColor);
            }
            EditorGUILayout.ObjectField(pieceType, typeof(PlacedObjectTypeSO), false, GUILayout.Width(150));
            EditorGUILayout.LabelField($"x {count}");

            if (GUILayout.Button(new GUIContent("X", "Видалити один екземпляр цієї фігури"), GUILayout.Width(25)))
            {
                RemovePieceByType(pieceType);
                GUI.changed = true;
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private bool CanPlace(bool[,] grid, List<Vector2Int> positions)
    {
        foreach (var pos in positions)
        {
            if (pos.x < 0 || pos.x >= gridDataSO.width || pos.y < 0 || pos.y >= gridDataSO.height || !grid[pos.x, pos.y])
            {
                return false;
            }
        }
        return true;
    }

    private void Place(bool[,] grid, List<Vector2Int> positions, bool value)
    {
        foreach (var pos in positions)
        {
            grid[pos.x, pos.y] = value;
        }
    }

    private void DeselectAllRequired()
    {
        var prop = serializedObject.FindProperty("generatorPieceConfig");
        if (prop == null) return;
        for (int i = 0; i < prop.arraySize; i++)
        {
            prop.GetArrayElementAtIndex(i).FindPropertyRelative("isRequired").boolValue = false;
        }
    }

    private void ResetAllCounts()
    {
        var prop = serializedObject.FindProperty("generatorPieceConfig");
        if (prop == null) return;
        for (int i = 0; i < prop.arraySize; i++)
        {
            prop.GetArrayElementAtIndex(i).FindPropertyRelative("requiredCount").intValue = 1;
        }
    }

    private void UpdateGeneratorPieceConfig()
    {
        if (gridDataSO.availablePieceTypesForGeneration == null) return;
        var currentConfigs = gridDataSO.generatorPieceConfig ?? new List<GridDataSO.GeneratorPieceConfig>();
        var newConfigs = new List<GridDataSO.GeneratorPieceConfig>();
        var availableTypes = gridDataSO.availablePieceTypesForGeneration.Where(p => p != null).Distinct();
        foreach (var pieceType in availableTypes)
        {
            var existingConfig = currentConfigs.FirstOrDefault(c => c.pieceType == pieceType);
            if (existingConfig.pieceType != null)
            {
                newConfigs.Add(existingConfig);
            }
            else
            {
                newConfigs.Add(new GridDataSO.GeneratorPieceConfig { pieceType = pieceType, isRequired = false, requiredCount = 1, maxCount = 1, color = Random.ColorHSV(0f, 1f, 0.8f, 1f, 0.9f, 1f) });
            }
        }

        var prop = serializedObject.FindProperty("generatorPieceConfig");
        prop.ClearArray();
        for (int i = 0; i < newConfigs.Count; i++)
        {
            prop.InsertArrayElementAtIndex(i);
            var element = prop.GetArrayElementAtIndex(i);
            element.FindPropertyRelative("pieceType").objectReferenceValue = newConfigs[i].pieceType;
            element.FindPropertyRelative("isRequired").boolValue = newConfigs[i].isRequired;
            element.FindPropertyRelative("requiredCount").intValue = newConfigs[i].requiredCount;
            element.FindPropertyRelative("maxCount").intValue = newConfigs[i].maxCount;
            element.FindPropertyRelative("color").colorValue = newConfigs[i].color;
        }
    }

    private void DrawGeneratorPieceConfigList(bool onlyRequired)
    {
        SerializedProperty listProp = serializedObject.FindProperty("generatorPieceConfig");
        if (listProp == null) return;

        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty elementProp = listProp.GetArrayElementAtIndex(i);
            if (onlyRequired && !elementProp.FindPropertyRelative("isRequired").boolValue) continue;

            EditorGUILayout.BeginVertical("box");
            SerializedProperty pieceTypeProp = elementProp.FindPropertyRelative("pieceType");
            EditorGUILayout.ObjectField(pieceTypeProp, GUIContent.none);
            if (pieceTypeProp.objectReferenceValue != null)
            {
                EditorGUILayout.BeginHorizontal();
                DrawPieceShapePreview((PlacedObjectTypeSO)pieceTypeProp.objectReferenceValue);
                EditorGUILayout.BeginVertical();
                EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("color"));
                EditorGUILayout.PropertyField(elementProp.FindPropertyRelative("isRequired"), new GUIContent("Is Required"));
                if (elementProp.FindPropertyRelative("isRequired").boolValue)
                {
                    SerializedProperty countProp = elementProp.FindPropertyRelative("requiredCount");
                    EditorGUILayout.PropertyField(countProp, new GUIContent("Count"));
                    if (countProp.intValue < 1) countProp.intValue = 1;
                }
                SerializedProperty maxCountProp = elementProp.FindPropertyRelative("maxCount");
                EditorGUILayout.PropertyField(maxCountProp, new GUIContent("Max Count"));
                if (maxCountProp.intValue < 1) maxCountProp.intValue = 1;

                EditorGUILayout.EndVertical();
                EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.EndVertical();
            EditorGUILayout.Space(5);
        }
    }

    private void DrawPieceShapePreview(PlacedObjectTypeSO pieceType)
    {
        if (pieceType.relativeOccupiedCells == null || pieceType.relativeOccupiedCells.Count == 0) return;
        Vector2Int dims = pieceType.GetMaxDimensions();
        float cellSize = 12f;
        Rect previewRect = GUILayoutUtility.GetRect(dims.x * cellSize, dims.y * cellSize, GUILayout.Width(dims.x * cellSize), GUILayout.Height(dims.y * cellSize));
        if (Event.current.type != EventType.Repaint) return;
        foreach (var cell in pieceType.relativeOccupiedCells)
        {
            Rect cellRect = new Rect(previewRect.x + cell.x * cellSize, previewRect.y + (dims.y - 1 - cell.y) * cellSize, cellSize - 1, cellSize - 1);
            EditorGUI.DrawRect(cellRect, Color.gray);
        }
    }

    private void HandlePreviewMouseInput(Rect previewRect, float cellSize)
    {
        Event e = Event.current;
        if (e.type == EventType.MouseDown && e.button == 1 && previewRect.Contains(e.mousePosition))
        {
            Vector2 localPos = e.mousePosition - previewRect.position;
            int x = Mathf.FloorToInt(localPos.x / cellSize);
            int z = gridDataSO.height - 1 - Mathf.FloorToInt(localPos.y / cellSize);
            Vector2Int clickedCell = new Vector2Int(x, z);

            RemovePieceAt(clickedCell);
            e.Use();
        }
    }

    private void RemovePieceAt(Vector2Int gridPosition)
    {
        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        int pieceIndexToRemove = -1;

        for (int i = 0; i < puzzleSolutionProp.arraySize; i++)
        {
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
            var pieceType = (PlacedObjectTypeSO)pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue;
            var origin = pieceDataProp.FindPropertyRelative("position").vector2IntValue;
            var dir = (PlacedObjectTypeSO.Dir)pieceDataProp.FindPropertyRelative("direction").enumValueIndex;

            if (pieceType.GetGridPositionsList(origin, dir).Contains(gridPosition))
            {
                pieceIndexToRemove = i;
                break;
            }
        }

        if (pieceIndexToRemove != -1)
        {
            puzzleSolutionProp.DeleteArrayElementAtIndex(pieceIndexToRemove);
            UpdatePuzzleState();
        }
    }

    private void RemovePieceByType(PlacedObjectTypeSO pieceType)
    {
        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        int pieceIndexToRemove = -1;

        for (int i = 0; i < puzzleSolutionProp.arraySize; i++)
        {
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
            var currentPieceType = (PlacedObjectTypeSO)pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue;
            if (currentPieceType == pieceType)
            {
                pieceIndexToRemove = i;
                break;
            }
        }

        if (pieceIndexToRemove != -1)
        {
            puzzleSolutionProp.DeleteArrayElementAtIndex(pieceIndexToRemove);
            UpdatePuzzleState();
        }
    }

    private void ClearAllPieces()
    {
        serializedObject.FindProperty("puzzleSolution").ClearArray();
        UpdatePuzzleState();
    }

    private void FillEmptySpace()
    {
        bool[,] currentGrid = new bool[gridDataSO.width, gridDataSO.height];
        foreach (var cell in gridDataSO.buildableCells)
        {
            currentGrid[cell.x, cell.y] = true;
        }

        var pieceCountsInSolution = new Dictionary<PlacedObjectTypeSO, int>();
        foreach (var pieceData in gridDataSO.puzzleSolution)
        {
            pieceCountsInSolution[pieceData.pieceType] = pieceCountsInSolution.GetValueOrDefault(pieceData.pieceType, 0) + 1;
            foreach (var pos in pieceData.pieceType.GetGridPositionsList(pieceData.position, pieceData.direction))
            {
                if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height)
                {
                    currentGrid[pos.x, pos.y] = false;
                }
            }
        }

        var existingPieceTypes = new HashSet<PlacedObjectTypeSO>(gridDataSO.puzzleSolution.Select(p => p.pieceType));
        var allAvailableFillers = gridDataSO.availablePieceTypesForGeneration.Where(p => p != null).ToList();

        var priorityFillers = allAvailableFillers.Where(p => !existingPieceTypes.Contains(p)).OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();
        var otherFillers = allAvailableFillers.Where(p => existingPieceTypes.Contains(p)).OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();

        var combinedFillers = priorityFillers.Concat(otherFillers).ToList();

        stopwatch = Stopwatch.StartNew();
        List<GridDataSO.GeneratedPieceData> filledPieces = new List<GridDataSO.GeneratedPieceData>();
        bool success = SolvePuzzleRecursiveGeneration(currentGrid, filledPieces, new List<PlacedObjectTypeSO>(), combinedFillers, pieceCountsInSolution);

        if (success)
        {
            UnityEngine.Debug.Log($"<color=green>Successfully filled empty space in {stopwatch.Elapsed.TotalSeconds:F3} seconds!</color>");
            var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
            int initialCount = puzzleSolutionProp.arraySize;
            for (int i = 0; i < filledPieces.Count; i++)
            {
                int newIndex = initialCount + i;
                puzzleSolutionProp.InsertArrayElementAtIndex(newIndex);
                var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(newIndex);
                pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue = filledPieces[i].pieceType;
                pieceDataProp.FindPropertyRelative("position").vector2IntValue = filledPieces[i].position;
                pieceDataProp.FindPropertyRelative("direction").enumValueIndex = (int)filledPieces[i].direction;
            }
            UpdatePuzzleState();
        }
        else
        {
            UnityEngine.Debug.LogError("Failed to fill the empty space. No solution found with the available pieces.");
        }
    }

    #endregion
}

