using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Linq;
using System.Diagnostics;
using System.Text;
using Debug = UnityEngine.Debug;

[CustomEditor(typeof(GridDataSO))]
public class GridDataSOEditor : Editor
{
    // ... (всі змінні та OnEnable залишаються без змін) ...
    public enum SolutionSelectionCriterion
    {
        FirstFound,
        FewestPieces,
        MostPieces
    }
    private int generationIterations = 10;
    private SolutionSelectionCriterion selectionCriterion = SolutionSelectionCriterion.FewestPieces;


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
    private float generationTimeout = 20f;

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
    private GUIStyle sequenceLabelStyle;

    private bool _calculationStopped;

    private int _globalMaxCount = 1;

    private static readonly List<Color> colorPalette = new List<Color>
    {
        new Color(1.0f, 0.4f, 0.4f),
        new Color(0.4f, 1.0f, 0.4f),
        new Color(0.4f, 0.4f, 1.0f),
        new Color(1.0f, 1.0f, 0.4f),
        new Color(1.0f, 0.4f, 1.0f),
        new Color(0.4f, 1.0f, 1.0f),
        new Color(1.0f, 0.6f, 0.2f),
        new Color(0.6f, 0.4f, 1.0f),
        new Color(0.2f, 0.8f, 0.6f),
        new Color(1.0f, 0.5f, 0.7f),
        new Color(0.7f, 0.9f, 0.2f),
        new Color(0.5f, 0.7f, 1.0f)
    };
    private static int colorIndex = 0;


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

        sequenceLabelStyle = new GUIStyle
        {
            alignment = TextAnchor.LowerRight,
            fontSize = 9,
            fontStyle = FontStyle.BoldAndItalic,
            padding = new RectOffset(0, 2, 0, 2)
        };
    }

    // --- ОНОВЛЕНИЙ SCENE GUI З ПІДТРИМКОЮ ПОВОРОТУ ---
    private void OnSceneGUI()
    {
        if (gridDataSO == null) return;

        // Підготовка даних
        Vector3 center = new Vector3(gridDataSO.cameraBoundsCenter.x, 0, gridDataSO.cameraBoundsCenter.y);
        Quaternion rotation = Quaternion.Euler(0, gridDataSO.cameraBoundsYRotation, 0);
        Vector3 size = new Vector3(gridDataSO.cameraBoundsSize.x, 0, gridDataSO.cameraBoundsSize.y);

        Handles.color = Color.yellow;

        // Малюємо повернутий прямокутник за допомогою матриці
        Matrix4x4 oldMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Handles.DrawWireCube(Vector3.zero, size);
        Handles.matrix = oldMatrix; // Скидаємо матрицю

        // --- 1. HANDLE ПЕРЕМІЩЕННЯ (Position) ---
        // Малюємо стрілки переміщення в центрі (вони будуть орієнтовані по світу, що зручно)
        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity); // Або rotation, якщо хочеш локальні стрілки
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Move Camera Bounds Center");
            gridDataSO.cameraBoundsCenter = new Vector2(newCenter.x, newCenter.z);
            EditorUtility.SetDirty(gridDataSO);
        }

        // --- 2. HANDLE ПОВОРОТУ (Rotation) ---
        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.Disc(rotation, center, Vector3.up, Mathf.Max(size.x, size.z) / 2 + 2, false, 0);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Rotate Camera Bounds");
            gridDataSO.cameraBoundsYRotation = newRotation.eulerAngles.y;
            EditorUtility.SetDirty(gridDataSO);
        }

        // --- 3. HANDLE МАСШТАБУ (Scale/Size) ---
        // Малюємо скейлер у локальній системі координат бокса
        EditorGUI.BeginChangeCheck();
        // Скейл хендл малюємо так, щоб він крутився разом з боксом
        Vector3 newSizeVector = Handles.ScaleHandle(
            new Vector3(gridDataSO.cameraBoundsSize.x, 1, gridDataSO.cameraBoundsSize.y),
            center,
            rotation,
            HandleUtility.GetHandleSize(center) * 1.5f
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Resize Camera Bounds");
            // Беремо абсолютні значення, щоб розмір не став від'ємним
            gridDataSO.cameraBoundsSize = new Vector2(Mathf.Abs(newSizeVector.x), Mathf.Abs(newSizeVector.z));
            EditorUtility.SetDirty(gridDataSO);
        }

        // Лейбл
        Handles.Label(center + rotation * (Vector3.right * (size.x / 2 + 1)), $"Bounds ({gridDataSO.cameraBoundsYRotation:F0}°)");
    }
    // ---------------------------------------------

    public override void OnInspectorGUI()
    {
        // ... (решта коду OnInspectorGUI залишається такою ж, як в попередньому файлі) ...
        serializedObject.Update();

        int oldWidth = gridDataSO.width;
        int oldHeight = gridDataSO.height;

        DrawPropertiesExcluding(serializedObject, "m_Script", "puzzlePieces", "generatedPieceSummary", "puzzleSolution", "availablePieceTypesForGeneration", "generatorPieceConfig", "solutionVariantsCount", "allFoundSolutions", "currentSolutionIndex", "isComplete", "personalityData", "width", "height");

        EditorGUILayout.PropertyField(serializedObject.FindProperty("width"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));

        serializedObject.ApplyModifiedProperties();

        if (gridDataSO.width != oldWidth || gridDataSO.height != oldHeight)
        {
            InitializeEditorGrid();
        }

        serializedObject.Update();


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
        if (GUILayout.Button(new GUIContent("Randomize All Colors", "Призначає фігурам нові кольори з палітри у випадковому порядку."))) RandomizeAllColors();


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

        generationIterations = EditorGUILayout.IntField(new GUIContent("Generation Iterations", "Кількість спроб для пошуку найкращого рішення."), generationIterations);
        if (generationIterations < 1) generationIterations = 1;
        selectionCriterion = (SolutionSelectionCriterion)EditorGUILayout.EnumPopup(new GUIContent("Selection Criterion", "Критерій для вибору найкращого рішення з усіх знайдених."), selectionCriterion);

        generationTimeout = EditorGUILayout.FloatField(new GUIContent("Total Timeout (Sec)", "Максимальний загальний час у секундах на пошук рішення."), generationTimeout);
        enableDebugLogs = EditorGUILayout.Toggle(new GUIContent("Enable Debug Logs", "Вмикає вивід детальної інформації про процес генерації в консоль. Може сповільнювати роботу."), enableDebugLogs);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Filler Piece Distribution Control", EditorStyles.boldLabel);
        smallPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Small Piece Max Cells", "Максимальна кількість клітинок, щоб фігура вважалася 'маленькою'."), smallPieceMaxCells);
        mediumPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Medium Piece Max Cells", "Максимальна кількість клітинок, щоб фігура вважалася 'середньою'."), mediumPieceMaxCells);
        desiredSmallFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Small Fillers", "Скільки маленьких допоміжних фігур буде використано при генерації."), desiredSmallFillers, 0, 20);
        desiredMediumFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Medium Fillers", "Скільки середніх допоміжних фігур буде використано при генерації."), desiredMediumFillers, 0, 20);
        desiredLargeFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Large Fillers", "Скільки великих допоміжних фігур буде використано при генерації."), desiredLargeFillers, 0, 20);

        EditorGUILayout.Space();
        DrawComplexityIndicator();
        EditorGUILayout.Space();

        if (GUILayout.Button(new GUIContent("Generate Puzzle Solution", "Запускає повну генерацію пазла з нуля на основі поточних налаштувань.")))
        {
            var (complexity, _) = CalculateComplexity();
            if (complexity >= 2)
            {
                if (EditorUtility.DisplayDialog("High Complexity Warning",
                    "The current settings have high or extreme complexity. Generation may take a very long time or freeze the editor.\n\nAre you sure you want to continue?",
                    "Yes, Generate", "Cancel"))
                {
                    GeneratePuzzle();
                }
            }
            else
            {
                GeneratePuzzle();
            }
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

            string variantsLabel = $"Found: {gridDataSO.solutionVariantsCount} (Stored: {gridDataSO.allFoundSolutions?.Count ?? 0})";
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
    // ... решта методів класу GridDataSOEditor (DrawMaxCountControls, DrawPersonalityEditor тощо) залишаються такими ж
    // ... просто встав весь попередній код класу після OnInspectorGUI

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
                Debug.Log($"Встановлено ліміт {_globalMaxCount} для всіх фігур.");
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
                Debug.Log("Скинуто ліміти для всіх фігур. Тепер кожна фігура унікальна.");
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

        // Використовуємо згенеровані фігури, щоб знати, які характери потрібні
        var requiredPieceTypes = gridDataSO.puzzlePieces.Where(p => p != null).Distinct().ToList();
        var mappedPieceTypes = personalityData.personalityMappings.Where(m => m.pieceType != null).Select(m => m.pieceType).ToList();

        bool isSynced = requiredPieceTypes.Count == mappedPieceTypes.Count && requiredPieceTypes.All(mappedPieceTypes.Contains);

        if (!isSynced)
        {
            EditorGUILayout.HelpBox("Склад фігур у рівні не співпадає з налаштуваннями характерів! Натисніть кнопку, щоб синхронізувати.", MessageType.Error);
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
        AssetDatabase.SaveAssets(); // Зберігаємо, щоб зміни відобразилися
    }

    private void RandomizeUnassignedTemperaments(LevelPersonalitySO pData)
    {
        string[] guids = AssetDatabase.FindAssets("t:TemperamentSO");
        if (guids.Length == 0)
        {
            Debug.LogWarning("Не знайдено жодного асету TemperamentSO для рандомізації.");
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
        AssetDatabase.SaveAssets(); // Зберігаємо, щоб зміни відобразилися
    }
    #endregion

    #region Buildable Cells Editor
    private void InitializeEditorGrid()
    {
        if (gridDataSO.width <= 0 || gridDataSO.height <= 0) return;

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
        serializedObject.ApplyModifiedProperties();
        UpdatePuzzleState(); // Оновлюємо стан пазла, щоб відобразити зміни в сітці
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
            Debug.LogWarning("First, generate a puzzle solution.");
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

        serializedObject.Update();

        var solutionVariantsCountProp = serializedObject.FindProperty("solutionVariantsCount");
        solutionVariantsCountProp.intValue = _solutionCounter;

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

        serializedObject.ApplyModifiedProperties();

        string messageType = !findAllPermutations ? "unique layouts" : "total permutations";
        string stopMessage = _calculationStopped ? " (stopped by timeout)" : "";
        Debug.Log($"<color=cyan>Calculation finished in {stopwatch.Elapsed.TotalSeconds:F2} sec{stopMessage}. Found {_solutionCounter} {messageType}. Stored: {_allSolutionsList.Count}.</color>");

        SetSolutionIndex(0);
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
                // Для кожного типу фігури, перебираємо всі можливі позиції, де ця фігура
                // покриває emptyCell.Value
                foreach (PlacedObjectTypeSO.Dir dir in System.Enum.GetValues(typeof(PlacedObjectTypeSO.Dir)))
                {
                    // Отримуємо список всіх клітинок, які належать фігурі, незалежно від її розміру
                    var relativeCells = pieceType.relativeOccupiedCells;

                    // Перебираємо кожну клітинку фігури, щоб знайти її початкову позицію (origin),
                    // якщо вона має покрити emptyCell.Value
                    foreach (var occupiedCell in relativeCells)
                    {
                        Vector2Int rotatedCell = GetRotatedCell(occupiedCell, dir, pieceType.GetMaxDimensions());
                        Vector2Int startPosition = emptyCell.Value - rotatedCell;

                        List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);

                        if (CanPlace(currentGrid, piecePositions))
                        {
                            // Уникнення дублювання при рекурсії, якщо різні відносні клітинки
                            // дають однаковий startPosition та напрямок
                            bool alreadyProcessed = currentSolution.Any(p =>
                                p.pieceType == pieceType &&
                                p.position == startPosition &&
                                p.direction == dir);

                            if (alreadyProcessed) continue;

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

    private string GenerateSolutionHash(List<GridDataSO.GeneratedPieceData> solution)
    {
        // Хешування для унікальних рішень
        var sortedPieces = solution.OrderBy(p => p.pieceType.name).ThenBy(p => p.position.x).ThenBy(p => p.position.y);

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
        var allFoundSolutionsProp = serializedObject.FindProperty("allFoundSolutions");
        int storedCount = allFoundSolutionsProp.arraySize;
        if (storedCount == 0) return;

        int clampedIndex = Mathf.Clamp(newIndex, 0, storedCount - 1);

        serializedObject.Update();

        var currentSolutionIndexProp = serializedObject.FindProperty("currentSolutionIndex");
        currentSolutionIndexProp.intValue = clampedIndex;
        solutionToShowIndex = clampedIndex + 1;

        var sourceSolutionProp = allFoundSolutionsProp.GetArrayElementAtIndex(clampedIndex).FindPropertyRelative("solution");
        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");

        puzzleSolutionProp.ClearArray();
        for (int i = 0; i < sourceSolutionProp.arraySize; i++)
        {
            var sourceElement = sourceSolutionProp.GetArrayElementAtIndex(i);
            puzzleSolutionProp.InsertArrayElementAtIndex(i);
            var destElement = puzzleSolutionProp.GetArrayElementAtIndex(i);

            destElement.FindPropertyRelative("pieceType").objectReferenceValue = sourceElement.FindPropertyRelative("pieceType").objectReferenceValue;
            destElement.FindPropertyRelative("position").vector2IntValue = sourceElement.FindPropertyRelative("position").vector2IntValue;
            destElement.FindPropertyRelative("direction").enumValueIndex = sourceElement.FindPropertyRelative("direction").enumValueIndex;
        }

        serializedObject.ApplyModifiedProperties();
        UpdatePuzzleState();
        Repaint();
    }


    #endregion

    #region Puzzle Generation Logic

    private void GeneratePuzzle()
    {
        stopwatch = Stopwatch.StartNew();
        List<List<GridDataSO.GeneratedPieceData>> foundSolutions = new List<List<GridDataSO.GeneratedPieceData>>();

        Debug.Log($"Starting generation with {generationIterations} iterations...");

        for (int i = 0; i < generationIterations; i++)
        {
            if (stopwatch.Elapsed.TotalSeconds > generationTimeout)
            {
                Debug.LogWarning($"Total generation timeout of {generationTimeout}s reached. Stopping at iteration {i + 1}.");
                break;
            }

            if (enableDebugLogs) Debug.Log($"--- Iteration {i + 1} ---");

            var solution = FindSingleSolution(stopwatch);
            if (solution != null)
            {
                foundSolutions.Add(solution);
            }
            if (selectionCriterion == SolutionSelectionCriterion.FirstFound && foundSolutions.Count > 0)
            {
                if (enableDebugLogs) Debug.Log("First solution found, stopping generation as per criterion.");
                break;
            }
        }

        stopwatch.Stop();

        if (foundSolutions.Count > 0)
        {
            List<GridDataSO.GeneratedPieceData> bestSolution = null;

            switch (selectionCriterion)
            {
                case SolutionSelectionCriterion.FirstFound:
                    bestSolution = foundSolutions[0];
                    break;
                case SolutionSelectionCriterion.FewestPieces:
                    bestSolution = foundSolutions.OrderBy(s => s.Count).FirstOrDefault();
                    break;
                case SolutionSelectionCriterion.MostPieces:
                    bestSolution = foundSolutions.OrderByDescending(s => s.Count).FirstOrDefault();
                    break;
            }

            Debug.Log($"<color=green>Puzzle generation finished in {stopwatch.Elapsed.TotalSeconds:F3}s. Found {foundSolutions.Count} solutions. Selected best with {bestSolution.Count} pieces based on '{selectionCriterion}'.</color>");

            SaveSolutionToSO(bestSolution);
        }
        else
        {
            Debug.LogError($"Failed to generate any puzzle solution after {generationIterations} iterations. Total time: {stopwatch.Elapsed.TotalSeconds:F3}s.");
            ClearAllPieces();
        }

        serializedObject.ApplyModifiedProperties();
    }

    private List<GridDataSO.GeneratedPieceData> FindSingleSolution(Stopwatch totalStopwatch)
    {
        var requiredPiecesConfig = gridDataSO.generatorPieceConfig?.Where(c => c.isRequired && c.pieceType != null).ToList();
        var allAvailableFillersConfig = gridDataSO.generatorPieceConfig?.Where(c => !c.isRequired && c.pieceType != null).ToList();

        if ((requiredPiecesConfig == null || requiredPiecesConfig.Count == 0) && (allAvailableFillersConfig == null || allAvailableFillersConfig.Count == 0))
        {
            Debug.LogError("No pieces specified for generation! Update the piece list.");
            return null;
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

        bool success = SolvePuzzleRecursiveGeneration(tempGrid, solution, requiredPiecesToPlace, orderedFillerPieces, pieceCountsInSolution, totalStopwatch);

        if (success)
        {
            return solution;
        }
        else
        {
            if (enableDebugLogs) Debug.LogWarning($"Single solution search failed or timed out.");
            return null;
        }
    }

    private bool SolvePuzzleRecursiveGeneration(bool[,] currentGrid, List<GridDataSO.GeneratedPieceData> solution, List<PlacedObjectTypeSO> required, List<PlacedObjectTypeSO> fillers, Dictionary<PlacedObjectTypeSO, int> pieceCounts, Stopwatch activeStopwatch)
    {
        if (activeStopwatch.Elapsed.TotalSeconds > generationTimeout) return false;
        if (enableDebugLogs) Debug.Log($"Depth: {solution.Count}, Required Left: {required.Count}, Total Time: {activeStopwatch.Elapsed.TotalSeconds:F2}s");

        Vector2Int? emptyCell = FindFirstEmptyCell(currentGrid);
        if (emptyCell == null) return required.Count == 0;

        var piecesToTry = new List<PlacedObjectTypeSO>();
        bool tryingRequired = required.Count > 0;

        // Порядок вибору фігур: спочатку всі необхідні (required), потім усі наповнювачі (fillers)
        if (tryingRequired)
        {
            // Обов'язкові фігури: сортуємо за розміром, щоб спробувати найбільші в першу чергу
            piecesToTry.AddRange(required.Distinct().OrderByDescending(p => p.relativeOccupiedCells.Count));
        }
        else
        {
            // Наповнювачі: сортуємо випадково, щоб збільшити варіативність
            piecesToTry.AddRange(fillers.OrderBy(p => Random.value));
        }


        foreach (var pieceType in piecesToTry)
        {
            if (pieceType.relativeOccupiedCells == null || pieceType.relativeOccupiedCells.Count == 0) continue;

            if (!tryingRequired)
            {
                // Перевірка ліміту для наповнювачів
                var config = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == pieceType);
                int maxCount = config.pieceType != null ? config.maxCount : int.MaxValue;
                if (pieceCounts.GetValueOrDefault(pieceType, 0) >= maxCount)
                {
                    continue;
                }
            }

            var shuffledDirs = System.Enum.GetValues(typeof(PlacedObjectTypeSO.Dir)).Cast<PlacedObjectTypeSO.Dir>().OrderBy(d => Random.value);

            // Перебираємо всі можливі клітинки фігури та напрямки для покриття emptyCell
            foreach (PlacedObjectTypeSO.Dir dir in shuffledDirs)
            {
                foreach (var occupiedCell in pieceType.relativeOccupiedCells)
                {
                    Vector2Int rotatedCell = GetRotatedCell(occupiedCell, dir, pieceType.GetMaxDimensions());
                    Vector2Int startPosition = emptyCell.Value - rotatedCell;

                    List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);

                    if (CanPlace(currentGrid, piecePositions))
                    {
                        // Уникнення дублювання при рекурсії, якщо різні відносні клітинки
                        // дають однаковий startPosition та напрямок
                        bool alreadyProcessed = solution.Any(p =>
                            p.pieceType == pieceType &&
                            p.position == startPosition &&
                            p.direction == dir);

                        if (alreadyProcessed) continue;

                        var nextRequired = new List<PlacedObjectTypeSO>(required);
                        if (tryingRequired) nextRequired.Remove(pieceType);

                        Place(currentGrid, piecePositions, false);
                        solution.Add(new GridDataSO.GeneratedPieceData { pieceType = pieceType, position = startPosition, direction = dir });
                        pieceCounts[pieceType] = pieceCounts.GetValueOrDefault(pieceType, 0) + 1;

                        if (SolvePuzzleRecursiveGeneration(currentGrid, solution, nextRequired, fillers, pieceCounts, activeStopwatch)) return true;

                        Place(currentGrid, piecePositions, true);
                        solution.RemoveAt(solution.Count - 1);
                        pieceCounts[pieceType]--;
                    }
                }
            }
        }
        return false;
    }

    private Vector2Int GetRotatedCell(Vector2Int cell, PlacedObjectTypeSO.Dir direction, Vector2Int originalDims)
    {
        int originalWidth = originalDims.x;
        int originalHeight = originalDims.y;

        switch (direction)
        {
            case PlacedObjectTypeSO.Dir.Down:
                return cell;
            case PlacedObjectTypeSO.Dir.Left:
                return new Vector2Int(cell.y, originalWidth - 1 - cell.x);
            case PlacedObjectTypeSO.Dir.Up:
                return new Vector2Int(originalWidth - 1 - cell.x, originalHeight - 1 - cell.y);
            case PlacedObjectTypeSO.Dir.Right:
                return new Vector2Int(originalHeight - 1 - cell.y, cell.x);
        }
        return cell;
    }

    private void SaveSolutionToSO(List<GridDataSO.GeneratedPieceData> solution)
    {
        serializedObject.Update();

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

        serializedObject.ApplyModifiedProperties();

        solutionToShowIndex = 1;
        UpdatePuzzleState(); // !!! ОНОВЛЮЄМО СТАН ПАЗЛА ПІСЛЯ ЗБЕРЕЖЕННЯ !!!
    }


    #endregion

    #region Helper & UI Methods

    private (int, float) CalculateComplexity()
    {
        int buildableCells = gridDataSO.buildableCells.Count;
        int availablePieceTypes = gridDataSO.availablePieceTypesForGeneration?.Count(p => p != null) ?? 0;

        float complexityScore = buildableCells * Mathf.Pow(availablePieceTypes, 1.2f);

        int complexityLevel = 0; // 0=Low, 1=Medium, 2=High, 3=Extreme
        if (complexityScore > 1000) complexityLevel = 1;
        if (complexityScore > 3000) complexityLevel = 2;
        if (complexityScore > 7000) complexityLevel = 3;

        return (complexityLevel, complexityScore);
    }

    private void DrawComplexityIndicator()
    {
        var (complexityLevel, complexityScore) = CalculateComplexity();
        string levelText = "Low";
        Color textColor = Color.green;

        switch (complexityLevel)
        {
            case 1: levelText = "Medium"; textColor = Color.yellow; break;
            case 2: levelText = "High"; textColor = new Color(1.0f, 0.6f, 0.0f); break; // Orange
            case 3: levelText = "Extreme"; textColor = Color.red; break;
        }

        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.normal.textColor = textColor;
        style.fontStyle = FontStyle.Bold;

        EditorGUILayout.LabelField(new GUIContent("Estimated Complexity", "A rough estimate based on grid size and number of available piece types. High complexity may lead to long generation times."), new GUIContent(levelText, $"Score: {complexityScore:F0}"), style);
    }


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
                // Малюємо лише забудовувані клітинки сірого кольору, інші - темно-сірого
                Color bgColor = gridDataSO.buildableCells.Contains(new Vector2Int(x, z)) ? new Color(0.5f, 0.5f, 0.5f) : new Color(0.2f, 0.2f, 0.2f);
                Rect cellRect = new Rect(previewRect.x + x * cellSize, previewRect.y + (gridDataSO.height - 1 - z) * cellSize, cellSize - 1, cellSize - 1);
                EditorGUI.DrawRect(cellRect, bgColor);
            }
        }

        if (gridDataSO.puzzleSolution != null)
        {
            // Створюємо мапу, щоб зберігати ID інстансу для кожної фігури
            var pieceInstanceTracker = new Dictionary<PlacedObjectTypeSO, int>();

            // Створюємо копію списку, щоб нумерувати фігури послідовно
            List<GridDataSO.GeneratedPieceData> piecesInOrder = gridDataSO.puzzleSolution.ToList();

            int sequenceCounter = 0;

            foreach (var pieceData in piecesInOrder)
            {
                if (pieceData.pieceType == null) continue;

                sequenceCounter++;

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

                // Визначаємо колір тексту (чорний/білий) для контрасту
                float brightness = (pieceColor.r * 0.299f + pieceColor.g * 0.587f + pieceColor.b * 0.114f);
                Color textColor = brightness > 0.5f ? Color.black : Color.white;

                if (positions.Count > 0)
                {
                    // Обчислюємо ID інстансу
                    if (!pieceInstanceTracker.ContainsKey(pieceData.pieceType))
                    {
                        pieceInstanceTracker[pieceData.pieceType] = 0;
                    }
                    pieceInstanceTracker[pieceData.pieceType]++;
                    int instanceId = pieceInstanceTracker[pieceData.pieceType];

                    // Обчислюємо загальну кількість фігур цього типу
                    var summaryEntry = gridDataSO.generatedPieceSummary.FirstOrDefault(s => s.pieceType == pieceData.pieceType);
                    int totalCount = summaryEntry.count;


                    // Виводимо номер у центрі першої клітинки фігури
                    Vector2Int firstCellPos = positions[0];
                    Rect labelCellRect = new Rect(previewRect.x + firstCellPos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - firstCellPos.y) * cellSize, cellSize, cellSize);

                    // Якщо фігура унікальна, виводимо її порядковий номер (sequenceCounter)
                    // Якщо фігура дублюється (totalCount > 1), виводимо її ID інстансу (1, 2, 3...)
                    string labelText = (totalCount > 1) ? instanceId.ToString() : sequenceCounter.ToString();

                    labelStyle.normal.textColor = textColor;
                    labelStyle.alignment = TextAnchor.MiddleCenter; // Виводимо по центру
                    GUI.Label(labelCellRect, labelText, labelStyle);
                }
            }
        }
    }


    private void UpdatePuzzleState()
    {
        serializedObject.Update();

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
            if (piece.pieceType != null)
                occupiedCellCount += piece.pieceType.relativeOccupiedCells.Count;
        }

        serializedObject.FindProperty("isComplete").boolValue = (buildableCellCount == occupiedCellCount);

        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(gridDataSO); // Позначаємо SO як змінений
        AssetDatabase.SaveAssets(); // Зберігаємо асет
        Repaint(); // Забезпечуємо оновлення вікна Інспектора
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
        var generatorConfigProp = serializedObject.FindProperty("generatorPieceConfig");

        for (int i = 0; i < summaryProp.arraySize; i++)
        {
            var summaryElement = summaryProp.GetArrayElementAtIndex(i);
            var pieceType = (PlacedObjectTypeSO)summaryElement.FindPropertyRelative("pieceType").objectReferenceValue;
            var count = summaryElement.FindPropertyRelative("count").intValue;

            if (pieceType == null) continue;

            // --- ЗНАХОДИМО ВІДПОВІДНИЙ ЕЛЕМЕНТ КОНФІГУРАЦІЇ ---
            SerializedProperty configElementProp = null;
            for (int j = 0; j < generatorConfigProp.arraySize; j++)
            {
                var currentConfigProp = generatorConfigProp.GetArrayElementAtIndex(j);
                if (currentConfigProp.FindPropertyRelative("pieceType").objectReferenceValue == pieceType)
                {
                    configElementProp = currentConfigProp;
                    break;
                }
            }

            EditorGUILayout.BeginHorizontal();

            if (configElementProp != null)
            {
                var colorProp = configElementProp.FindPropertyRelative("color");
                EditorGUI.BeginChangeCheck();
                // --- ВИКОРИСТОВУЄМО ColorField, АЛЕ ХОВАЄМО ЙОГО МІТКУ ---
                var newColor = EditorGUILayout.ColorField(GUIContent.none, colorProp.colorValue, true, true, false, GUILayout.Width(40));
                if (EditorGUI.EndChangeCheck())
                {
                    colorProp.colorValue = newColor;
                    EditorUtility.SetDirty(gridDataSO);
                }
            }
            else
            {
                // Fallback, якщо конфігурацію не знайдено
                Rect colorRect = GUILayoutUtility.GetRect(18, 18, GUILayout.Width(40));
                EditorGUI.DrawRect(colorRect, Color.magenta);
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

        colorIndex = 0;

        foreach (var pieceType in availableTypes)
        {
            var existingConfig = currentConfigs.FirstOrDefault(c => c.pieceType == pieceType);
            if (existingConfig.pieceType != null)
            {
                newConfigs.Add(existingConfig);
            }
            else
            {
                Color newColor = colorPalette[colorIndex % colorPalette.Count];
                colorIndex++;
                newConfigs.Add(new GridDataSO.GeneratorPieceConfig { pieceType = pieceType, isRequired = false, requiredCount = 1, maxCount = 1, color = newColor });
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
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(gridDataSO);
    }

    private void RandomizeAllColors()
    {
        var prop = serializedObject.FindProperty("generatorPieceConfig");
        if (prop == null) return;

        var shuffledPalette = colorPalette.OrderBy(c => Random.value).ToList();

        for (int i = 0; i < prop.arraySize; i++)
        {
            prop.GetArrayElementAtIndex(i).FindPropertyRelative("color").colorValue = shuffledPalette[i % shuffledPalette.Count];
        }
        serializedObject.ApplyModifiedProperties();
        EditorUtility.SetDirty(gridDataSO);
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
        // !!! ВИДАЛЕННЯ ФІГУРИ ПРАВОЮ КНОПКОЮ МИШІ !!!
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

        // Потрібно видалити лише одну фігуру, яка займає цю клітинку. 
        // Починаємо з кінця, щоб видаляти останні додані елементи.
        for (int i = puzzleSolutionProp.arraySize - 1; i >= 0; i--)
        {
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
            var pieceType = (PlacedObjectTypeSO)pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue;

            if (pieceType != null)
            {
                var origin = pieceDataProp.FindPropertyRelative("position").vector2IntValue;
                var dir = (PlacedObjectTypeSO.Dir)pieceDataProp.FindPropertyRelative("direction").enumValueIndex;

                if (pieceType.GetGridPositionsList(origin, dir).Contains(gridPosition))
                {
                    pieceIndexToRemove = i;
                    break;
                }
            }
        }

        if (pieceIndexToRemove != -1)
        {
            puzzleSolutionProp.DeleteArrayElementAtIndex(pieceIndexToRemove);
            UpdatePuzzleState(); // Обов'язкове оновлення стану
            Repaint(); // !!! ПРИМУСОВЕ ПЕРЕМАЛЬОВУВАННЯ ДЛЯ ВИДАЛЕННЯ ВІЗУАЛУ !!!
            Debug.Log($"Видалено фігуру на позиції {gridPosition}.");
        }
        else
        {
            Debug.LogWarning($"На клітинці {gridPosition} не знайдено фігури для видалення.");
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
        Debug.Log("Усі фігури з рішення очищено.");
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

        var fillStopwatch = Stopwatch.StartNew();
        List<GridDataSO.GeneratedPieceData> filledPieces = new List<GridDataSO.GeneratedPieceData>();

        // Використовуємо рекурсивне вирішення, але без обов'язкових фігур (required)
        bool success = SolvePuzzleRecursiveGeneration(currentGrid, filledPieces, new List<PlacedObjectTypeSO>(), combinedFillers, pieceCountsInSolution, fillStopwatch);

        if (success)
        {
            Debug.Log($"<color=green>Successfully filled empty space in {fillStopwatch.Elapsed.TotalSeconds:F3} seconds!</color>");
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
            Debug.LogError("Failed to fill the empty space. No solution found with the available pieces.");
        }
    }

    #endregion
}