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
    public enum SolutionSelectionCriterion
    {
        FirstFound,
        FewestPieces,
        MostPieces
    }
    private int generationIterations = 10;
    private SolutionSelectionCriterion selectionCriterion = SolutionSelectionCriterion.FewestPieces;

    private GridDataSO gridDataSO;

    // --- Editor Grid State ---
    private bool[,] editorGridCells;   // Buildable (Green)
    private bool[,] editorLockedCells; // Locked (Orange)

    // --- Default Foldout State: Collapsed ---
    private bool showRequiredPieces = false;
    private bool showAllPieces = false;
    private bool showLevelItems = false; // Новий список для айтемів
    private bool showObstaclesEditor = false;
    private PlacedObjectTypeSO selectedObstacleType;
    private PlacedObjectTypeSO.Dir selectedObstacleDir = PlacedObjectTypeSO.Dir.Down;
    private bool obstaclePaintMode = false;
    private bool paintAsObstacle = true;

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

    // Стан для малювання
    private bool _isDragging = false;
    private bool _dragSetState = false; // Який стан ставимо (true/false) при драгу
    private int _dragButton = -1; // Яка кнопка затиснута (0 - LMB, 2 - MMB)

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

    private void OnSceneGUI()
    {
        if (gridDataSO == null) return;

        Vector3 center = new Vector3(gridDataSO.cameraBoundsCenter.x, 0, gridDataSO.cameraBoundsCenter.y);
        Quaternion rotation = Quaternion.Euler(0, gridDataSO.cameraBoundsYRotation, 0);
        Vector3 size = new Vector3(gridDataSO.cameraBoundsSize.x, 0, gridDataSO.cameraBoundsSize.y);

        Handles.color = Color.yellow;

        Matrix4x4 oldMatrix = Handles.matrix;
        Handles.matrix = Matrix4x4.TRS(center, rotation, Vector3.one);
        Handles.DrawWireCube(Vector3.zero, size);
        Handles.matrix = oldMatrix;

        EditorGUI.BeginChangeCheck();
        Vector3 newCenter = Handles.PositionHandle(center, Quaternion.identity);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Move Camera Bounds Center");
            gridDataSO.cameraBoundsCenter = new Vector2(newCenter.x, newCenter.z);
            EditorUtility.SetDirty(gridDataSO);
        }

        EditorGUI.BeginChangeCheck();
        Quaternion newRotation = Handles.Disc(rotation, center, Vector3.up, Mathf.Max(size.x, size.z) / 2 + 2, false, 0);
        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Rotate Camera Bounds");
            gridDataSO.cameraBoundsYRotation = newRotation.eulerAngles.y;
            EditorUtility.SetDirty(gridDataSO);
        }

        EditorGUI.BeginChangeCheck();
        Vector3 newSizeVector = Handles.ScaleHandle(
            new Vector3(gridDataSO.cameraBoundsSize.x, 1, gridDataSO.cameraBoundsSize.y),
            center,
            rotation,
            HandleUtility.GetHandleSize(center) * 1.5f
        );

        if (EditorGUI.EndChangeCheck())
        {
            Undo.RecordObject(gridDataSO, "Resize Camera Bounds");
            gridDataSO.cameraBoundsSize = new Vector2(Mathf.Abs(newSizeVector.x), Mathf.Abs(newSizeVector.z));
            EditorUtility.SetDirty(gridDataSO);
        }

        Handles.Label(center + rotation * (Vector3.right * (size.x / 2 + 1)), $"Bounds ({gridDataSO.cameraBoundsYRotation:F0}°)");
    }

    public override void OnInspectorGUI()
    {
        serializedObject.Update();

        int oldWidth = gridDataSO.width;
        int oldHeight = gridDataSO.height;

        DrawPropertiesExcluding(serializedObject, "m_Script", "puzzlePieces", "levelItems", "generatedPieceSummary", "puzzleSolution", "availablePieceTypesForGeneration", "generatorPieceConfig", "solutionVariantsCount", "allFoundSolutions", "currentSolutionIndex", "isComplete", "personalityData", "width", "height", "buildableCells", "lockedCells", "generatedObstacles");

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Environment & Spawning", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("environmentPrefab"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("levelSpawnOffset"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSize"));

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Camera Settings (Boundaries)", EditorStyles.boldLabel);
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraBoundsCenter"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraBoundsSize"));
        EditorGUILayout.PropertyField(serializedObject.FindProperty("cameraBoundsYRotation"));

        if (obstaclePaintMode) Repaint();

        EditorGUILayout.Space(20);
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.LabelField("1. Level Preview", EditorStyles.boldLabel);
        EditorGUI.BeginChangeCheck();
        bool newPaintMode = GUILayout.Toggle(obstaclePaintMode, " PAINT MODE", "Button", GUILayout.Width(100), GUILayout.Height(20));
        if (EditorGUI.EndChangeCheck()) {
            obstaclePaintMode = newPaintMode;
            Repaint();
        }
        EditorGUILayout.EndHorizontal();

        DrawPuzzlePreview();

        if (obstaclePaintMode) {
            DrawManualObjectPainter();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("2. Piece Summary (Live Inventory)", EditorStyles.boldLabel);
        serializedObject.Update();

        // --- LEVEL VALIDATION CHECK ---
        ValidateLevelSolvability();
        
        EditorGUILayout.Space(10);
        DrawPieceSummary();

        EditorGUILayout.Space(10);
        DrawGridDimensions();
        EditorGUILayout.LabelField("3. Topology (Hold Click to Paint)", EditorStyles.boldLabel);
        DrawBuildableCellsEditor();

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("4. Puzzle Content & Generation", EditorStyles.boldLabel);
        
        DrawPersonalityEditor();

        EditorGUILayout.PropertyField(serializedObject.FindProperty("availablePieceTypesForGeneration"), true);
        
        if (GUILayout.Button(new GUIContent("Update Generator Piece Config"))) UpdateGeneratorPieceConfig();
        
        showRequiredPieces = EditorGUILayout.Foldout(showRequiredPieces, "Required Pieces (Generator)", true);
        if (showRequiredPieces) DrawGeneratorPieceConfigList(true);

        EditorGUILayout.Space(10);
        showLevelItems = EditorGUILayout.Foldout(showLevelItems, "Manual Items (Spawn Always/Static)", true);
        if (showLevelItems) {
            DrawManualItemsList("levelItems");
            DrawManualItemsList("staticObstacles");
        }

        EditorGUILayout.Space(20);
        EditorGUILayout.LabelField("5. Generator Controls", EditorStyles.boldLabel);

        generationIterations = EditorGUILayout.IntField(new GUIContent("Generation Iterations"), generationIterations);
        if (generationIterations < 1) generationIterations = 1;
        selectionCriterion = (SolutionSelectionCriterion)EditorGUILayout.EnumPopup(new GUIContent("Selection Criterion"), selectionCriterion);

        generationTimeout = EditorGUILayout.FloatField(new GUIContent("Total Timeout (Sec)"), generationTimeout);
        enableDebugLogs = EditorGUILayout.Toggle(new GUIContent("Enable Debug Logs"), enableDebugLogs);

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Filler Piece Distribution Control", EditorStyles.boldLabel);
        smallPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Small Piece Max Cells"), smallPieceMaxCells);
        mediumPieceMaxCells = EditorGUILayout.IntField(new GUIContent("Medium Piece Max Cells"), mediumPieceMaxCells);
        desiredSmallFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Small Fillers"), desiredSmallFillers, 0, 20);
        desiredMediumFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Medium Fillers"), desiredMediumFillers, 0, 20);
        desiredLargeFillers = EditorGUILayout.IntSlider(new GUIContent("Desired Large Fillers"), desiredLargeFillers, 0, 20);

        EditorGUILayout.Space();
        DrawComplexityIndicator();
        EditorGUILayout.Space();

        if (GUILayout.Button(new GUIContent("Generate Puzzle Solution", "᪠  ?     ᭮? 筨 㢠.")))
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

        if (!gridDataSO.isComplete)
        {
            EditorGUILayout.HelpBox("Puzzle is incomplete (geometry not filled)! You can fill the empty space manually or regenerate.", MessageType.Warning);
            if (GUILayout.Button("Fill Empty Space")) FillEmptySpace();
        }

        if (GUILayout.Button("Clear All Generated Pieces"))
        {
            if (EditorUtility.DisplayDialog("Clear All Pieces?", "Remove all pieces from the current solution?", "Yes", "No")) ClearAllPieces();
        }

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Solution Analysis", EditorStyles.boldLabel);

        string variantsLabel = $"Found: {gridDataSO.solutionVariantsCount} (Stored: {gridDataSO.allFoundSolutions?.Count ?? 0})";
        EditorGUILayout.LabelField(variantsLabel);

        useCalculationTimeout = EditorGUILayout.Toggle("Use Timeout", useCalculationTimeout);
        if (useCalculationTimeout)
        {
            calculationTimeout = EditorGUILayout.FloatField("Calculation Timeout (Sec)", calculationTimeout);
        }

        maxSolutionsToStore = EditorGUILayout.IntField("Max Solutions to Store", maxSolutionsToStore);
        findAllPermutations = EditorGUILayout.Toggle("Find All Permutations", findAllPermutations);

        if (GUILayout.Button("Calculate Solution Variants")) CalculateSolutions();

        if (gridDataSO.allFoundSolutions != null && gridDataSO.allFoundSolutions.Count > 1)
        {
            DrawSolutionNavigator();
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
        AssetDatabase.SaveAssets();
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
        AssetDatabase.SaveAssets();
    }
    #endregion

    #region Buildable & Locked Cells Editor
    private void InitializeEditorGrid()
    {
        if (gridDataSO.width <= 0 || gridDataSO.height <= 0) return;

        editorGridCells = new bool[gridDataSO.width, gridDataSO.height];
        editorLockedCells = new bool[gridDataSO.width, gridDataSO.height];

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

        if (gridDataSO.lockedCells != null)
        {
            foreach (Vector2Int cell in gridDataSO.lockedCells)
            {
                if (cell.x >= 0 && cell.x < gridDataSO.width && cell.y >= 0 && cell.y < gridDataSO.height)
                {
                    editorLockedCells[cell.x, cell.y] = true;
                }
            }
        }
    }

    private void DrawBuildableCellsEditor()
    {
        EditorGUILayout.HelpBox("LMB to Paint Buildable (Green).\nMiddle Click to Paint Locked (Orange).", MessageType.Info);

        GUILayout.BeginHorizontal();
        if (GUILayout.Button("All Buildable")) SelectAllBuildableCells(true);
        if (GUILayout.Button("None Buildable")) SelectAllBuildableCells(false);
        if (GUILayout.Button("All Locked")) SelectAllLockedCells(true);
        if (GUILayout.Button("None Locked")) SelectAllLockedCells(false);
        GUILayout.EndHorizontal();

        EditorGUILayout.Space(5);
        GUILayout.BeginVertical("box");

        Event e = Event.current;

        for (int z = gridDataSO.height - 1; z >= 0; z--)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < gridDataSO.width; x++)
            {
                Color cellColor = new Color(0.2f, 0.2f, 0.2f);
                if (editorGridCells[x, z]) cellColor = new Color(0.4f, 1f, 0.4f);
                if (editorLockedCells[x, z]) cellColor = new Color(1f, 0.5f, 0f);

                Rect btnRect = GUILayoutUtility.GetRect(25, 25, GUILayout.Width(25), GUILayout.Height(25));
                EditorGUI.DrawRect(btnRect, cellColor);

                if (e.type == EventType.Repaint)
                {
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, btnRect.width, 1), Color.black);
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.y, 1, btnRect.height), Color.black);
                    EditorGUI.DrawRect(new Rect(btnRect.xMax - 1, btnRect.y, 1, btnRect.height), Color.black);
                    EditorGUI.DrawRect(new Rect(btnRect.x, btnRect.yMax - 1, btnRect.width, 1), Color.black);
                }

                if (btnRect.Contains(e.mousePosition))
                {
                    if (e.type == EventType.MouseDown)
                    {
                        _isDragging = true;
                        if (e.button == 0) // LMB
                        {
                            _dragButton = 0;
                            _dragSetState = !editorGridCells[x, z];
                            editorGridCells[x, z] = _dragSetState;
                            UpdateBuildableCellsList();
                            e.Use();
                        }
                        else if (e.button == 2) // MMB
                        {
                            _dragButton = 2;
                            _dragSetState = !editorLockedCells[x, z];
                            editorLockedCells[x, z] = _dragSetState;
                            if (_dragSetState) editorGridCells[x, z] = true;
                            UpdateBuildableCellsList();
                            e.Use();
                        }
                    }
                    else if (e.type == EventType.MouseDrag && _isDragging)
                    {
                        if (_dragButton == 0)
                        {
                            if (editorGridCells[x, z] != _dragSetState)
                            {
                                editorGridCells[x, z] = _dragSetState;
                                UpdateBuildableCellsList();
                                Repaint();
                            }
                            e.Use();
                        }
                        else if (_dragButton == 2)
                        {
                            if (editorLockedCells[x, z] != _dragSetState)
                            {
                                editorLockedCells[x, z] = _dragSetState;
                                if (_dragSetState) editorGridCells[x, z] = true;
                                UpdateBuildableCellsList();
                                Repaint();
                            }
                            e.Use();
                        }
                    }
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();

        if (e.type == EventType.MouseUp)
        {
            _isDragging = false;
            _dragButton = -1;
        }
    }

    private void UpdateBuildableCellsList()
    {
        var buildableCellsProp = serializedObject.FindProperty("buildableCells");
        var lockedCellsProp = serializedObject.FindProperty("lockedCells");

        buildableCellsProp.ClearArray();
        lockedCellsProp.ClearArray();

        int bIndex = 0;
        int lIndex = 0;

        for (int x = 0; x < gridDataSO.width; x++)
        {
            for (int z = 0; z < gridDataSO.height; z++)
            {
                if (editorGridCells[x, z])
                {
                    buildableCellsProp.InsertArrayElementAtIndex(bIndex);
                    buildableCellsProp.GetArrayElementAtIndex(bIndex).vector2IntValue = new Vector2Int(x, z);
                    bIndex++;
                }

                if (editorLockedCells[x, z])
                {
                    lockedCellsProp.InsertArrayElementAtIndex(lIndex);
                    lockedCellsProp.GetArrayElementAtIndex(lIndex).vector2IntValue = new Vector2Int(x, z);
                    lIndex++;
                }
            }
        }
        serializedObject.ApplyModifiedProperties();
        UpdatePuzzleState();
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

    private void SelectAllLockedCells(bool select)
    {
        for (int x = 0; x < gridDataSO.width; x++)
        {
            for (int z = 0; z < gridDataSO.height; z++)
            {
                editorLockedCells[x, z] = select;
                if (select) editorGridCells[x, z] = true;
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

        var relevantPieces = gridDataSO.puzzlePieces.Where(p => p != null).ToList();

        var pieceCounts = relevantPieces.GroupBy(p => p).ToDictionary(g => g.Key, g => g.Count());
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
                var pieceData = solutionProp.GetArrayElementAtIndex(j);
                pieceData.FindPropertyRelative("pieceType").objectReferenceValue = solutionData[j].pieceType;
                pieceData.FindPropertyRelative("position").vector2IntValue = solutionData[j].position;
                pieceData.FindPropertyRelative("direction").enumValueIndex = (int)solutionData[j].direction;
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
                foreach (PlacedObjectTypeSO.Dir dir in System.Enum.GetValues(typeof(PlacedObjectTypeSO.Dir)))
                {
                    var relativeCells = pieceType.relativeOccupiedCells;

                    foreach (var occupiedCell in relativeCells)
                    {
                        Vector2Int rotatedCell = GetRotatedCell(occupiedCell, dir, pieceType.GetMaxDimensions());
                        Vector2Int startPosition = emptyCell.Value - rotatedCell;

                        List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);

                        if (CanPlace(currentGrid, piecePositions))
                        {
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
        var allConfig = gridDataSO.generatorPieceConfig ?? new List<GridDataSO.GeneratorPieceConfig>();

        var validConfigs = allConfig.Where(c => c.pieceType != null).ToList();

        var requiredPiecesConfig = validConfigs.Where(c => c.isRequired).ToList();
        var allAvailableFillersConfig = validConfigs.Where(c => !c.isRequired).ToList();

        if ((requiredPiecesConfig.Count == 0) && (allAvailableFillersConfig.Count == 0))
        {
            Debug.LogError("No valid pieces specified for generation! Update the piece list.");
            return null;
        }

        List<PlacedObjectTypeSO> curatedFillerPieces = new List<PlacedObjectTypeSO>();
        if (allAvailableFillersConfig.Count > 0)
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

        if (gridDataSO.lockedCells != null)
        {
            foreach (var cell in gridDataSO.lockedCells)
            {
                if (cell.x < gridDataSO.width && cell.y < gridDataSO.height)
                    tempGrid[cell.x, cell.y] = true;
            }
        }

        // --- NEW: Occupy cells with Manual Items and Static Obstacles ---
        void OccupyManualCells(IEnumerable<GridDataSO.GeneratedPieceData> pieceList) {
            if (pieceList == null) return;
            foreach (var piece in pieceList) {
                if (piece.pieceType == null) continue;
                // ONLY occupy cells if they are actually intended to stay on grid
                if (!piece.startOnGrid) continue; 

                var positions = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                foreach (var pos in positions) {
                    if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height) {
                        tempGrid[pos.x, pos.y] = false; // Mark as occupied
                    }
                }
            }
        }
        OccupyManualCells(gridDataSO.levelItems);
        // --- MODIFIED: Shapes ignore obstacles. For generation purposes, we skip OccupyManualCells for staticObstacles ---
        // OccupyManualCells(gridDataSO.staticObstacles);

        List<GridDataSO.GeneratedPieceData> solution = new List<GridDataSO.GeneratedPieceData>();
        List<PlacedObjectTypeSO> requiredPiecesToPlace = new List<PlacedObjectTypeSO>();

        foreach (var req in requiredPiecesConfig)
        {
            for (int i = 0; i < req.requiredCount; i++) requiredPiecesToPlace.Add(req.pieceType);
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
            piecesToTry.AddRange(fillers.OrderBy(p => Random.value));
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
                foreach (var occupiedCell in pieceType.relativeOccupiedCells)
                {
                    Vector2Int rotatedCell = GetRotatedCell(occupiedCell, dir, pieceType.GetMaxDimensions());
                    Vector2Int startPosition = emptyCell.Value - rotatedCell;

                    List<Vector2Int> piecePositions = pieceType.GetGridPositionsList(startPosition, dir);

                    if (CanPlace(currentGrid, piecePositions))
                    {
                        bool alreadyProcessed = solution.Any(p =>
                            p.pieceType == pieceType &&
                            p.position == startPosition &&
                            p.direction == dir);

                        if (alreadyProcessed) continue;

                        var nextRequired = new List<PlacedObjectTypeSO>(required);
                        if (tryingRequired) nextRequired.Remove(pieceType);

                        Place(currentGrid, piecePositions, false);
                        bool overlapsLocked = piecePositions.Any(p => gridDataSO.lockedCells.Contains(p));
                        solution.Add(new GridDataSO.GeneratedPieceData { 
                            pieceType = pieceType, 
                            position = startPosition, 
                            direction = dir,
                            startOnGrid = !overlapsLocked 
                        });
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
            pieceDataProp.FindPropertyRelative("isObstacle").boolValue = solution[i].isObstacle;
            pieceDataProp.FindPropertyRelative("isHidden").boolValue = solution[i].isHidden;
            pieceDataProp.FindPropertyRelative("startOnGrid").boolValue = solution[i].startOnGrid;
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
            pieceDataProp.FindPropertyRelative("isObstacle").boolValue = solution[j].isObstacle;
            pieceDataProp.FindPropertyRelative("isHidden").boolValue = solution[j].isHidden;
            pieceDataProp.FindPropertyRelative("startOnGrid").boolValue = solution[j].startOnGrid;
        }

        serializedObject.ApplyModifiedProperties();

        solutionToShowIndex = 1;
        UpdatePuzzleState();
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
        if (obstaclePaintMode) EditorGUILayout.Space(25); // Make room for the banner
        Rect previewRect = GUILayoutUtility.GetRect(gridDataSO.width * cellSize, gridDataSO.height * cellSize);

        HandlePreviewMouseInput(previewRect, cellSize);

        if (Event.current.type != EventType.Repaint) return;

        // 1. Draw background (buildable/locked cells)
        for (int z = 0; z < gridDataSO.height; z++)
        {
            for (int x = 0; x < gridDataSO.width; x++)
            {
                Color bgColor = new Color(0.2f, 0.2f, 0.2f);
                if (gridDataSO.buildableCells.Contains(new Vector2Int(x, z))) bgColor = new Color(0.4f, 1f, 0.4f);
                if (gridDataSO.lockedCells.Contains(new Vector2Int(x, z))) bgColor = new Color(1f, 0.5f, 0f);

                Rect cellRect = new Rect(previewRect.x + x * cellSize, previewRect.y + (gridDataSO.height - 1 - z) * cellSize, cellSize - 1, cellSize - 1);
                EditorGUI.DrawRect(cellRect, bgColor);
            }
        }

        // 2. Draw puzzle solution pieces (Solution + Manual Shapes)
        var allShapes = (gridDataSO.puzzleSolution ?? new List<GridDataSO.GeneratedPieceData>()).ToList();
        if (gridDataSO.levelItems != null) {
            allShapes.AddRange(gridDataSO.levelItems.Where(p => p.pieceType != null && p.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape && !p.isObstacle));
        }

        var pieceInstanceTracker = new Dictionary<PlacedObjectTypeSO, int>();
        int sequenceCounter = 0;

        foreach (var pieceData in allShapes) {
            if (pieceData.pieceType == null) continue;
            sequenceCounter++;

            var colorEntry = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == pieceData.pieceType);
            Color pieceColor = colorEntry.pieceType != null ? colorEntry.color : Color.magenta;
            List<Vector2Int> positions = pieceData.pieceType.GetGridPositionsList(pieceData.position, pieceData.direction);

            foreach (var pos in positions) {
                if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height) {
                    Rect cellRect = new Rect(previewRect.x + pos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - pos.y) * cellSize, cellSize - 1, cellSize - 1);
                    EditorGUI.DrawRect(cellRect, pieceColor);
                }
            }

            float brightness = (pieceColor.r * 0.299f + pieceColor.g * 0.587f + pieceColor.b * 0.114f);
            Color textColor = brightness > 0.5f ? Color.black : Color.white;

            if (positions.Count > 0) {
                Vector2Int firstCellPos = positions[0];
                if (firstCellPos.x >= 0 && firstCellPos.x < gridDataSO.width && firstCellPos.y >= 0 && firstCellPos.y < gridDataSO.height) {
                    if (!pieceInstanceTracker.ContainsKey(pieceData.pieceType)) pieceInstanceTracker[pieceData.pieceType] = 0;
                    pieceInstanceTracker[pieceData.pieceType]++;
                    int instanceId = pieceInstanceTracker[pieceData.pieceType];

                    var summaryEntry = gridDataSO.generatedPieceSummary.FirstOrDefault(s => s.pieceType == pieceData.pieceType);
                    int totalCount = (summaryEntry.pieceType != null) ? summaryEntry.count : 0;

                    Rect labelCellRect = new Rect(previewRect.x + firstCellPos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - firstCellPos.y) * cellSize, cellSize, cellSize);

                    string labelText = (totalCount > 1) ? instanceId.ToString() : sequenceCounter.ToString();
                    labelStyle.normal.textColor = textColor;
                    labelStyle.alignment = TextAnchor.MiddleCenter;
                    GUI.Label(labelCellRect, labelText, labelStyle);

                    // --- NEW: Draw 'G' indicator for On-Grid spawning ---
                    if (pieceData.startOnGrid) {
                        labelStyle.alignment = TextAnchor.UpperRight;
                        GUI.Label(labelCellRect, "G", labelStyle);
                    }
                }
            }
        }

        // 3. Draw manual items & static obstacles (OVERLAY)
        void DrawManualOverlay(IEnumerable<GridDataSO.GeneratedPieceData> list) {
            if (list == null) return;
            foreach (var piece in list) {
                if (piece.pieceType == null) continue;

                // Shapes are already drawn in Step 2 for numbering sequence
                bool isShape = piece.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape && !piece.isObstacle;
                if (isShape && list == gridDataSO.levelItems) continue;

                List<Vector2Int> positions = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                
                // For items, use their config color but with lower alpha and white border
                Color baseColor = Color.white;
                if (!piece.isObstacle) {
                    var colorEntry = gridDataSO.generatorPieceConfig.FirstOrDefault(c => c.pieceType == piece.pieceType);
                    baseColor = colorEntry.pieceType != null ? colorEntry.color : Color.white;
                    baseColor.a = 0.6f;
                } else {
                    baseColor = new Color(1f, 1f, 1f, 0.45f);
                }

                foreach (var pos in positions) {
                    if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height) {
                        Rect cellRect = new Rect(previewRect.x + pos.x * cellSize, previewRect.y + (gridDataSO.height - 1 - pos.y) * cellSize, cellSize - 1, cellSize - 1);
                        
                        if (piece.isObstacle) DrawHatchedRect(cellRect, baseColor);
                        else EditorGUI.DrawRect(cellRect, baseColor);

                        Handles.color = piece.isObstacle ? Color.black : Color.white; 
                        Handles.DrawWireCube(cellRect.center, cellRect.size);

                        // 'G' indicator for non-shapes too
                        if (piece.startOnGrid && pos == piece.position) {
                            labelStyle.normal.textColor = piece.isObstacle ? Color.black : Color.white;
                            labelStyle.alignment = TextAnchor.UpperRight;
                            GUI.Label(cellRect, "G", labelStyle);
                        }
                    }
                }
            }
        }

        DrawManualOverlay(gridDataSO.levelItems);
        DrawManualOverlay(gridDataSO.staticObstacles);

        // 4. Draw ghost preview & feedback panel
        if (obstaclePaintMode)
        {
            Vector2 mousePos = Event.current.mousePosition;
            bool isHovering = previewRect.Contains(mousePos);
            bool isBlocked = false;
            
            if (isHovering && selectedObstacleType != null)
            {
                Vector2 localPos = mousePos - previewRect.position;
                int gx = Mathf.FloorToInt(localPos.x / cellSize);
                int gz = gridDataSO.height - 1 - Mathf.FloorToInt(localPos.y / cellSize);
                Vector2Int hoverCell = new Vector2Int(gx, gz);

                // --- NEW: Calculate Anchor Offset (keep piece under mouse) ---
                Vector2Int anchorOffset = Vector2Int.zero;
                if (selectedObstacleType.relativeOccupiedCells != null && selectedObstacleType.relativeOccupiedCells.Count > 0) {
                    anchorOffset = GetRotatedCell(selectedObstacleType.relativeOccupiedCells[0], selectedObstacleDir, selectedObstacleType.GetMaxDimensions());
                }
                Vector2Int placementOrigin = hoverCell - anchorOffset;

                List<Vector2Int> ghostCells = selectedObstacleType.GetGridPositionsList(placementOrigin, selectedObstacleDir);
                isBlocked = false;
                bool isRuleViolation = false;

                // --- 1. DETERMINE PLACEMENT RULES BASED ON TYPE ---
                bool isTool = selectedObstacleType.category == PlacedObjectTypeSO.ItemCategory.Tool || selectedObstacleType.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;
                
                // --- 2. ENFORCE STRICT PLACEMENT RULES ---
                // NOTE: If NOT painting as Obstacle and NOT isTool, we might want to spawn it Off-Grid.
                // For simplicity: If "On Grid" toggle is ever added to Ghost, we'd check it here.
                // For now, let's assume if it spawns off-grid (controlled in list), it can be placed anywhere.
                // BUT ghost doesn't know the list yet. 
                // Suggestion: In Paint Mode, always validate, but if user wants to place something purely for off-grid, 
                // they can toggle it in the list after placement.
                
                bool isAltPressed = Event.current.alt || Event.current.control;
                foreach (var g in ghostCells) {
                    bool isBuildable = gridDataSO.buildableCells.Contains(g);
                    bool isLocked = gridDataSO.lockedCells.Contains(g);

                    if (isTool) {
                        // TOOLS: MUST be on Locked.
                        if (!isLocked) { isRuleViolation = true; break; }
                    } else if (paintAsObstacle) {
                        // OBSTACLES: FORBIDDEN on Locked. MUST be on Buildable.
                        if (isLocked) { isRuleViolation = true; break; }
                        if (!isBuildable) { isRuleViolation = true; break; }
                    } else if (!isAltPressed) {
                        // SNAPPED SHAPES: FORBIDDEN on Locked. MUST be on Buildable.
                        if (isLocked) { isRuleViolation = true; break; }
                        if (!isBuildable) { isRuleViolation = true; break; }
                    } else {
                        // OFF-GRID SHAPES (Alt/Ctrl): Allowed on Buildable OR Locked.
                        if (!isBuildable && !isLocked) { isRuleViolation = true; break; }
                    }
                    
                    if (g.x < 0 || g.x >= gridDataSO.width || g.y < 0 || g.y >= gridDataSO.height) {
                        isRuleViolation = true; break;
                    }
                }

                // --- 3. LAYERED OVERLAP CHECK ---
                if (!isTool) 
                {
                    // Define what we collide with based on what we are painting
                    IEnumerable<GridDataSO.GeneratedPieceData> collisionLayer;

                    if (paintAsObstacle) {
                        // OBSTACLES: Only collide with other Obstacles. Can overlap Shapes.
                        collisionLayer = gridDataSO.staticObstacles ?? new List<GridDataSO.GeneratedPieceData>();
                    } else {
                        // PUZZLE SHAPES: Collide with Solutions and Manual Shapes only. Ignore Obstacles.
                        var solutionList = (gridDataSO.puzzleSolution != null) ? gridDataSO.puzzleSolution.ToList() : new List<GridDataSO.GeneratedPieceData>();
                        collisionLayer = solutionList
                                        .Concat(gridDataSO.levelItems ?? new List<GridDataSO.GeneratedPieceData>())
                                        .Where(p => !p.isObstacle);
                    }

                    foreach(var g in ghostCells) {
                        foreach(var existing in collisionLayer) {
                            if (existing.pieceType != null) {
                                var occ = existing.pieceType.GetGridPositionsList(existing.position, existing.direction);
                                if (occ.Contains(g)) {
                                    isBlocked = true; break;
                                }
                            }
                        }
                        if (isBlocked) break;
                    }
                }
                
                if (isRuleViolation) isBlocked = true;

                Color ghostColor = isBlocked ? new Color(1f, 0.1f, 0.1f, 0.75f) : new Color(1f, 1f, 0.5f, 0.65f);

                foreach (var gCell in ghostCells)
                {
                    if (gCell.x >= 0 && gCell.x < gridDataSO.width && gCell.y >= 0 && gCell.y < gridDataSO.height)
                    {
                        Rect cellRect = new Rect(previewRect.x + gCell.x * cellSize, previewRect.y + (gridDataSO.height - 1 - gCell.y) * cellSize, cellSize - 1, cellSize - 1);
                        if (!isBlocked) DrawHatchedRect(cellRect, ghostColor);
                        else EditorGUI.DrawRect(cellRect, ghostColor);
                        
                        Handles.color = isBlocked ? Color.red : Color.yellow;
                        Handles.DrawWireCube(cellRect.center, cellRect.size);
                    }
                }
            }
        }

        // --- PAINT MODE ACTIVE BANNER ---
        if (obstaclePaintMode)
        {
            Rect bannerRect = new Rect(previewRect.x, previewRect.y - 22, previewRect.width, 20);
            EditorGUI.DrawRect(bannerRect, new Color(0.8f, 0.6f, 0.0f, 0.95f));
            GUIStyle bannerStyle = new GUIStyle(EditorStyles.boldLabel);
            bannerStyle.normal.textColor = Color.white;
            bannerStyle.alignment = TextAnchor.MiddleCenter;
            bannerStyle.fontStyle = FontStyle.Bold;
            string modeName = "";
            if (paintAsObstacle) modeName = "[GRID OBSTACLE]";
            else {
                if (selectedObstacleType != null && selectedObstacleType.category == PlacedObjectTypeSO.ItemCategory.Tool) modeName = "[TOOL]";
                else modeName = "[PUZZLE SHAPE]";
            }
            GUI.Label(bannerRect, $"PAINT MODE: {modeName} - {(selectedObstacleType ? selectedObstacleType.objectName : "NONE")} (Press 'R' to Rotate)", bannerStyle);
        }
    }

    private void UpdatePuzzleState()
    {
        serializedObject.Update();

        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        var puzzlePiecesProp = serializedObject.FindProperty("puzzlePieces");
        var summaryProp = serializedObject.FindProperty("generatedPieceSummary");

        // 1. Оновлюємо список PuzzlePieces для гри
        puzzlePiecesProp.ClearArray();
        var currentSolution = new List<GridDataSO.GeneratedPieceData>();

        for (int i = 0; i < puzzleSolutionProp.arraySize; i++)
        {
            var pieceDataProp = puzzleSolutionProp.GetArrayElementAtIndex(i);
            var pieceType = (PlacedObjectTypeSO)pieceDataProp.FindPropertyRelative("pieceType").objectReferenceValue;
            var pos = pieceDataProp.FindPropertyRelative("position").vector2IntValue;
            var dir = (PlacedObjectTypeSO.Dir)pieceDataProp.FindPropertyRelative("direction").enumValueIndex;

            var pieceData = new GridDataSO.GeneratedPieceData { pieceType = pieceType, position = pos, direction = dir };
            if (pieceType != null)
            {
                currentSolution.Add(pieceData);
                // Додаємо в список спавну
                puzzlePiecesProp.InsertArrayElementAtIndex(i);
                puzzlePiecesProp.GetArrayElementAtIndex(i).objectReferenceValue = pieceType;
            }
        }

        // --- NEW: Collect all pieces for a consolidated Summary ---
        var allPiecesForSummary = new List<GridDataSO.GeneratedPieceData>(currentSolution);
        if (gridDataSO.levelItems != null) allPiecesForSummary.AddRange(gridDataSO.levelItems);
        if (gridDataSO.staticObstacles != null) allPiecesForSummary.AddRange(gridDataSO.staticObstacles);

        // 2. Оновлюємо Summary (для редактора та гри)
        var summary = allPiecesForSummary
            .Where(s => s.pieceType != null)
            .GroupBy(s => s.pieceType)
            .Select(group => new GridDataSO.PieceCount { pieceType = group.Key, count = group.Count() })
            .OrderByDescending(s => s.pieceType.relativeOccupiedCells.Count)
            .ToList();

        summaryProp.ClearArray();
        for (int i = 0; i < summary.Count; i++)
        {
            summaryProp.InsertArrayElementAtIndex(i);
            var summaryElement = summaryProp.GetArrayElementAtIndex(i);
            summaryElement.FindPropertyRelative("pieceType").objectReferenceValue = summary[i].pieceType;
            summaryElement.FindPropertyRelative("count").intValue = summary[i].count;
        }

        int targetCellCount = gridDataSO.buildableCells.Union(gridDataSO.lockedCells).Count();
        int occupiedCellCount = 0;

        // 3. Перевіряємо IsComplete
        // Логіка: Всі Buildable/Locked клітинки повинні бути покриті Character-ами.
        
        void CountOccupiedBuildable(IEnumerable<GridDataSO.GeneratedPieceData> pieceList) {
            if (pieceList == null) return;
            var targetSet = new HashSet<Vector2Int>(gridDataSO.buildableCells.Concat(gridDataSO.lockedCells));
            foreach (var piece in pieceList) {
                if (piece.pieceType != null && piece.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape && !piece.isObstacle) {
                    var positions = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                    foreach (var pos in positions) {
                        if (targetSet.Contains(pos)) {
                            occupiedCellCount++;
                        }
                    }
                }
            }
        }

        CountOccupiedBuildable(currentSolution);
        CountOccupiedBuildable(gridDataSO.levelItems);

        serializedObject.FindProperty("isComplete").boolValue = (occupiedCellCount >= targetCellCount);

        serializedObject.ApplyModifiedProperties();

        // Примусово зберігаємо асет
        EditorUtility.SetDirty(gridDataSO);
        AssetDatabase.SaveAssets();
        Repaint();
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
        // 1. Collect all pieces from Solution, LevelItems and StaticObstacles
        var summary = new Dictionary<PlacedObjectTypeSO, int>();
        var hiddenTypes = new HashSet<PlacedObjectTypeSO>();
        var obstacleTypes = new HashSet<PlacedObjectTypeSO>();
        var solutionTypes = new HashSet<PlacedObjectTypeSO>();
        var itemTypes = new HashSet<PlacedObjectTypeSO>();

        void AddToSummary(IEnumerable<GridDataSO.GeneratedPieceData> list, HashSet<PlacedObjectTypeSO> set) {
            if (list == null) return;
            foreach (var item in list) {
                if (item.pieceType == null) continue;
                if (!summary.ContainsKey(item.pieceType)) summary[item.pieceType] = 0;
                summary[item.pieceType]++;
                if (item.isHidden) hiddenTypes.Add(item.pieceType);
                if (item.isObstacle) obstacleTypes.Add(item.pieceType);
                if (set != null) set.Add(item.pieceType);
            }
        }

        AddToSummary(gridDataSO.puzzleSolution, solutionTypes);
        AddToSummary(gridDataSO.levelItems, itemTypes);
        AddToSummary(gridDataSO.staticObstacles, null); // Static are always obstacles

        if (summary.Count == 0) {
            EditorGUILayout.HelpBox("No pieces placed or generated yet.", MessageType.None);
            return;
        }

        var generatorConfigProp = serializedObject.FindProperty("generatorPieceConfig");

        foreach (var kvp in summary)
        {
            var pieceType = kvp.Key;
            var count = kvp.Value;

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

            EditorGUILayout.BeginHorizontal("box");
            
            // --- PREVIEW (Fixed Width Container) ---
            EditorGUILayout.BeginVertical(GUILayout.Width(42));
            DrawPieceShapePreview(pieceType);
            EditorGUILayout.EndVertical();
            if (configElementProp != null) {
                var colorProp = configElementProp.FindPropertyRelative("color");
                EditorGUI.BeginChangeCheck();
                var newColor = EditorGUILayout.ColorField(GUIContent.none, colorProp.colorValue, true, true, false, GUILayout.Width(45), GUILayout.Height(18));
                if (EditorGUI.EndChangeCheck()) {
                    colorProp.colorValue = newColor;
                    EditorUtility.SetDirty(gridDataSO);
                }
            } else {
                Rect colorRect = GUILayoutUtility.GetRect(45, 18, GUILayout.Width(45));
                EditorGUI.DrawRect(colorRect, Color.gray);
            }

            // Pick / Paint ICON
            GUI.color = (obstaclePaintMode && selectedObstacleType == pieceType) ? Color.yellow : Color.white;
            if (GUILayout.Button(new GUIContent("? ", "Pick this piece for Obstacle Painter (Hotkeys: LMB Paint, R Rotate)"), GUILayout.Width(30), GUILayout.Height(18))) {
                selectedObstacleType = pieceType;
                obstaclePaintMode = true;
                GUI.changed = true;
            }
            GUI.color = Color.white;

            EditorGUILayout.LabelField(pieceType.objectName, EditorStyles.boldLabel, GUILayout.Width(130));
            
            // --- SOURCE LABELS ---
            string source = "";
            var cat = pieceType.category;
            var usage = pieceType.usageType;
            
            if (cat == PlacedObjectTypeSO.ItemCategory.Tool || usage == PlacedObjectTypeSO.UsageType.UnlockGrid) {
                source = "[TOOL]";
            } else if (cat == PlacedObjectTypeSO.ItemCategory.PuzzleShape) {
                if (solutionTypes.Contains(pieceType)) source = "[PUZZLE SHAPE] (Auto)";
                else source = "[PUZZLE SHAPE] (Manual)";
            } else {
                // Props, Toys, Food are all obstacles when on grid
                source = "[GRID OBSTACLE]";
            }

            labelStyle.normal.textColor = Color.gray;
            EditorGUILayout.LabelField(source, labelStyle, GUILayout.Width(180));
            GUI.color = Color.white;

            // Flags
            string flags = "";
            if (obstacleTypes.Contains(pieceType)) flags += "O";
            if (hiddenTypes.Contains(pieceType)) flags += "H";
            if (flags != "") {
                GUI.color = Color.cyan;
                EditorGUILayout.LabelField($"[{flags}]", EditorStyles.miniLabel, GUILayout.Width(30));
                GUI.color = Color.white;
            } else {
                EditorGUILayout.Space(34, false);
            }

            EditorGUILayout.LabelField($"x {count}", EditorStyles.boldLabel, GUILayout.Width(35));

            if (GUILayout.Button(new GUIContent("Wipe", "Remove ALL instances of this piece from EVERYTHING (Solution, Items, Static)"), GUILayout.Width(45)))
            {
                if (EditorUtility.DisplayDialog("Wipe Piece?", $"Completely remove all {count} instances of {pieceType.objectName}?", "Wipe", "Cancel")) {
                    RemovePieceByTypeFromAll(pieceType);
                    GUI.changed = true;
                }
            }

            EditorGUILayout.EndHorizontal();
        }
    }

    private void RemovePieceByTypeFromAll(PlacedObjectTypeSO pieceType)
    {
        gridDataSO.puzzleSolution?.RemoveAll(p => p.pieceType == pieceType);
        gridDataSO.levelItems?.RemoveAll(p => p.pieceType == pieceType);
        gridDataSO.staticObstacles?.RemoveAll(p => p.pieceType == pieceType);
        
        serializedObject.Update();
        UpdatePuzzleState();
    }

    private void DrawManualItemsList(string propName)
    {
        SerializedProperty listProp = serializedObject.FindProperty(propName);
        if (listProp == null) return;

        EditorGUILayout.BeginVertical("box");
        for (int i = 0; i < listProp.arraySize; i++)
        {
            SerializedProperty productProp = listProp.GetArrayElementAtIndex(i);
            EditorGUILayout.BeginHorizontal();
            
            var pType = (PlacedObjectTypeSO)productProp.FindPropertyRelative("pieceType").objectReferenceValue;
            if (pType != null) {
                 EditorGUILayout.BeginVertical(GUILayout.Width(42));
                 DrawPieceShapePreview(pType);
                 EditorGUILayout.EndVertical();
            }

            EditorGUILayout.PropertyField(productProp.FindPropertyRelative("pieceType"), GUIContent.none, GUILayout.Width(120));
            EditorGUILayout.PropertyField(productProp.FindPropertyRelative("position"), GUIContent.none, GUILayout.Width(80));
            EditorGUILayout.PropertyField(productProp.FindPropertyRelative("direction"), GUIContent.none, GUILayout.Width(60));
            
            // Small toggles for flags
            SerializedProperty obsProp = productProp.FindPropertyRelative("isObstacle");
            obsProp.boolValue = GUILayout.Toggle(obsProp.boolValue, new GUIContent("O", "Is Obstacle"), "Button", GUILayout.Width(20));
            
            SerializedProperty hiddenProp = productProp.FindPropertyRelative("isHidden");
            hiddenProp.boolValue = GUILayout.Toggle(hiddenProp.boolValue, new GUIContent("H", "Is Hidden"), "Button", GUILayout.Width(20));

            SerializedProperty onGridProp = productProp.FindPropertyRelative("startOnGrid");
            onGridProp.boolValue = GUILayout.Toggle(onGridProp.boolValue, new GUIContent("G", "Spawn On Grid"), "Button", GUILayout.Width(20));

            if (GUILayout.Button("-", GUILayout.Width(20))) {
                listProp.DeleteArrayElementAtIndex(i);
                break;
            }
            EditorGUILayout.EndHorizontal();
        }
        if (GUILayout.Button("+ Add Manual Piece", EditorStyles.miniButton)) {
            listProp.arraySize++;
        }
        EditorGUILayout.EndVertical();
    }


    private void ClearAllPieces()
    {
        serializedObject.FindProperty("puzzleSolution").ClearArray();

        // --- FIX: Зберігаємо зміни ---
        serializedObject.ApplyModifiedProperties();

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
        foreach (var cell in gridDataSO.lockedCells)
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
                    currentGrid[pos.x, pos.y] = false; // Вже зайнято
                }
            }
        }

        // --- NEW: Occupy cells with Manual Items and Static Obstacles in FillEmptySpace ---
        void OccupyManualCellsFill(IEnumerable<GridDataSO.GeneratedPieceData> pieceList) {
            if (pieceList == null) return;
            foreach (var piece in pieceList) {
                if (piece.pieceType == null) continue;
                pieceCountsInSolution[piece.pieceType] = pieceCountsInSolution.GetValueOrDefault(piece.pieceType, 0) + 1;
                var positions = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                foreach (var pos in positions) {
                    if (pos.x >= 0 && pos.x < gridDataSO.width && pos.y >= 0 && pos.y < gridDataSO.height) {
                        currentGrid[pos.x, pos.y] = false; // Already occupied
                    }
                }
            }
        }
        OccupyManualCellsFill(gridDataSO.levelItems);
        // OccupyManualCellsFill(gridDataSO.staticObstacles); // Shapes ignore obstacles


        var existingPieceTypes = new HashSet<PlacedObjectTypeSO>(gridDataSO.puzzleSolution.Select(p => p.pieceType));
        // Беремо тільки характери для заповнення
        var allAvailableFillers = gridDataSO.availablePieceTypesForGeneration
            .Where(p => p != null && p.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape)
            .ToList();

        var priorityFillers = allAvailableFillers.Where(p => !existingPieceTypes.Contains(p)).OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();
        var otherFillers = allAvailableFillers.Where(p => existingPieceTypes.Contains(p)).OrderByDescending(p => p.relativeOccupiedCells.Count).ToList();

        var combinedFillers = priorityFillers.Concat(otherFillers).ToList();

        var fillStopwatch = Stopwatch.StartNew();
        List<GridDataSO.GeneratedPieceData> filledPieces = new List<GridDataSO.GeneratedPieceData>();

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

            // --- FIX: Зберігаємо зміни ---
            serializedObject.ApplyModifiedProperties();

            UpdatePuzzleState();
        }
        else
        {
            Debug.LogError("Failed to fill the empty space. No solution found with the available pieces.");
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
            
            if (pieceTypeProp.objectReferenceValue != null)
            {
                EditorGUILayout.BeginHorizontal();
                EditorGUILayout.BeginVertical(GUILayout.Width(42));
                DrawPieceShapePreview((PlacedObjectTypeSO)pieceTypeProp.objectReferenceValue);
                EditorGUILayout.EndVertical();
                
                EditorGUILayout.BeginVertical();
                EditorGUILayout.ObjectField(pieceTypeProp, GUIContent.none);
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
            else
            {
                EditorGUILayout.ObjectField(pieceTypeProp, GUIContent.none);
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

        // --- NEW: FORCED REPAINT FOR GHOST PREVIEW ---
        if (previewRect.Contains(e.mousePosition))
        {
            if (e.type == EventType.MouseMove || e.type == EventType.MouseDrag)
            {
                Repaint();
            }
        }

        // --- ROTATION HOTKEY ---
        if (obstaclePaintMode && e.type == EventType.KeyDown && e.keyCode == KeyCode.R)
        {
            selectedObstacleDir = PlacedObjectTypeSO.GetNextDir(selectedObstacleDir);
            e.Use();
            Repaint();
        }

        if (obstaclePaintMode && (e.type == EventType.MouseDown || e.type == EventType.MouseDrag) && e.button == 0 && previewRect.Contains(e.mousePosition))
        {
            Vector2 localPos = e.mousePosition - previewRect.position;
            int x = Mathf.FloorToInt(localPos.x / cellSize);
            int z = gridDataSO.height - 1 - Mathf.FloorToInt(localPos.y / cellSize);
            Vector2Int clickedCell = new Vector2Int(x, z);
            
            // Check if Alt or Ctrl is held to place as "Off-Grid Always"
            bool placeOffGrid = e.alt || e.control;
            PlaceManualPieceRefined(clickedCell, placeOffGrid);
            e.Use();
        }
        if (e.type == EventType.MouseDown && e.button == 1 && previewRect.Contains(e.mousePosition))
        {
            Vector2 localPos = e.mousePosition - previewRect.position;
            int x = Mathf.FloorToInt(localPos.x / cellSize);
            int z = gridDataSO.height - 1 - Mathf.FloorToInt(localPos.y / cellSize);
            Vector2Int clickedCell = new Vector2Int(x, z);

            if (paintAsObstacle) {
                RemoveManualEntryAt(clickedCell, true); // Only Obstacles
            } else {
                RemovePieceAt(clickedCell); // Auto pieces are also shapes
                RemoveManualEntryAt(clickedCell, false); // Only Manual Items
            }
            e.Use();
            Repaint();
        }
    }

    private void RemovePieceAt(Vector2Int gridPosition)
    {
        var puzzleSolutionProp = serializedObject.FindProperty("puzzleSolution");
        int pieceIndexToRemove = -1;

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

            // --- FIX: Зберігаємо зміни перед оновленням ---
            serializedObject.ApplyModifiedProperties();

            UpdatePuzzleState();
            Repaint();
            Debug.Log($"Видалено фігуру на позиції {gridPosition}.");
        }
        else
        {
            Debug.LogWarning($"На клітинці {gridPosition} не знайдено фігури для видалення.");
        }
    }

    #endregion
    private void DrawManualObjectPainter()
    {
        EditorGUILayout.BeginVertical("box");
        EditorGUILayout.LabelField("Manual Object Painter Settings", EditorStyles.miniBoldLabel);
        
        EditorGUILayout.BeginHorizontal();
        EditorGUILayout.PrefixLabel("Object Type");
        selectedObstacleType = (PlacedObjectTypeSO)EditorGUILayout.ObjectField(selectedObstacleType, typeof(PlacedObjectTypeSO), false);
        EditorGUILayout.EndHorizontal();

        selectedObstacleDir = (PlacedObjectTypeSO.Dir)EditorGUILayout.EnumPopup("Direction", selectedObstacleDir);
        
        EditorGUILayout.BeginHorizontal();
        paintAsObstacle = EditorGUILayout.ToggleLeft(new GUIContent(" Paint as [GRID OBSTACLE]", "Obstacles block movement and are fixed on grid."), paintAsObstacle);
        if (!paintAsObstacle) {
            EditorGUILayout.LabelField("-> Mode: [PUZZLE SHAPE]", EditorStyles.miniLabel);
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.HelpBox("TIP: [?] = Pick. 'R' = Rotate. RMB = Remove (by Mode).\nTOOLS: Only Locked (Orange). SNAPPED (Default): Only Buildable (Green).\nOFF-GRID (Alt/Ctrl): Buildable (Green) OR Locked (Orange).", MessageType.None);

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear [GRID OBSTACLES]")) {
            if (EditorUtility.DisplayDialog("Clear Obstacles?", "Remove all manually painted obstacles?", "Yes", "No")) {
                Undo.RecordObject(gridDataSO, "Clear Obstacles");
                gridDataSO.staticObstacles.Clear();
                EditorUtility.SetDirty(gridDataSO);
            }
        }
        if (GUILayout.Button("Clear [PUZZLE SHAPES] (Manual)")) {
            if (EditorUtility.DisplayDialog("Clear Manual Shapes?", "Remove all manual puzzle pieces?", "Yes", "No")) {
                Undo.RecordObject(gridDataSO, "Clear Manual Pieces");
                gridDataSO.levelItems.Clear();
                EditorUtility.SetDirty(gridDataSO);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Clear ALL MANUAL (Obstacles + Manual Shapes)")) {
            if (EditorUtility.DisplayDialog("Clear All Manual?", "Remove ALL hand-placed objects?", "Yes", "No")) {
                Undo.RecordObject(gridDataSO, "Clear All Manual");
                gridDataSO.staticObstacles.Clear();
                gridDataSO.levelItems.Clear();
                EditorUtility.SetDirty(gridDataSO);
            }
        }
        if (GUILayout.Button("Clear [PUZZLE SHAPES] (Auto)")) {
            if (EditorUtility.DisplayDialog("Clear Auto Shapes?", "Remove all auto-generated pieces?", "Yes", "No")) {
                Undo.RecordObject(gridDataSO, "Clear Auto Pieces");
                gridDataSO.puzzleSolution = new List<GridDataSO.GeneratedPieceData>();
                EditorUtility.SetDirty(gridDataSO);
            }
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginVertical("box");
        GUI.color = new Color(1f, 0.4f, 0.4f);
        if (GUILayout.Button("WIPE EVERYTHING (Auto + Manual + Obstacles)", GUILayout.Height(25))) {
            if (EditorUtility.DisplayDialog("Wipe All?", "Delete EVERYTHING from the grid?", "Yes", "No")) {
                Undo.RecordObject(gridDataSO, "Wipe All Pieces");
                gridDataSO.staticObstacles.Clear();
                gridDataSO.levelItems.Clear();
                gridDataSO.puzzleSolution = new List<GridDataSO.GeneratedPieceData>();
                EditorUtility.SetDirty(gridDataSO);
            }
        }
        GUI.color = Color.white;
        EditorGUILayout.EndVertical();
        EditorGUILayout.EndVertical();
    }

    private void PlaceManualPieceRefined(Vector2Int anchorCell, bool placeOffGrid = false)
    {
        if (selectedObstacleType == null) return;

        // --- NEW: Calculate Anchor Offset ---
        Vector2Int anchorOffset = Vector2Int.zero;
        if (selectedObstacleType.relativeOccupiedCells != null && selectedObstacleType.relativeOccupiedCells.Count > 0) {
            anchorOffset = GetRotatedCell(selectedObstacleType.relativeOccupiedCells[0], selectedObstacleDir, selectedObstacleType.GetMaxDimensions());
        }
        Vector2Int origin = anchorCell - anchorOffset;
        
        // --- 1. DETERMINE PLACEMENT RULES BASED ON TYPE ---
        List<Vector2Int> newPositions = selectedObstacleType.GetGridPositionsList(origin, selectedObstacleDir);
        bool isTool = selectedObstacleType.category == PlacedObjectTypeSO.ItemCategory.Tool || selectedObstacleType.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid;

        // --- 2. ENFORCE BOUNDARIES (Always) ---
        foreach (var p in newPositions) {
            if (p.x < 0 || p.x >= gridDataSO.width || p.y < 0 || p.y >= gridDataSO.height) return;
        }

        // --- 3. ENFORCE STRICT PLACEMENT RULES (Only if on-grid) ---
        if (isTool) {
            foreach (var p in newPositions) {
                if (!gridDataSO.lockedCells.Contains(p)) return; // Tools only on Locked
            }
        } else if (paintAsObstacle) {
            foreach (var p in newPositions) {
                if (gridDataSO.lockedCells.Contains(p)) return; // Obstacles FORBIDDEN on Locked
                if (!gridDataSO.buildableCells.Contains(p)) return; // Obstacles ONLY on Buildable
            }
        } else if (!placeOffGrid) {
            // Snapped shapes FORBIDDEN on Locked, ONLY on Buildable
            foreach (var p in newPositions) {
                if (gridDataSO.lockedCells.Contains(p)) return;
                if (!gridDataSO.buildableCells.Contains(p)) return;
            }
        } else {
            // Off-grid shapes allowed on Buildable or Locked
            foreach (var p in newPositions) {
                if (!gridDataSO.buildableCells.Contains(p) && !gridDataSO.lockedCells.Contains(p)) return;
            }
        }

        if (!isTool)
        {
            // --- 3. LAYERED OVERLAP CHECK ---
             IEnumerable<GridDataSO.GeneratedPieceData> collisionLayer;

            if (paintAsObstacle) {
                // OBSTACLES: Only collide with other Obstacles. Can overlap Shapes.
                collisionLayer = gridDataSO.staticObstacles ?? new List<GridDataSO.GeneratedPieceData>();
            } else {
                // PUZZLE SHAPES: Collide with Solutions and Manual Shapes only. Ignore Obstacles.
                var solutionList = (gridDataSO.puzzleSolution != null) ? gridDataSO.puzzleSolution.ToList() : new List<GridDataSO.GeneratedPieceData>();
                collisionLayer = solutionList
                            .Concat(gridDataSO.levelItems ?? new List<GridDataSO.GeneratedPieceData>())
                            .Where(p => !p.isObstacle);
            }

            foreach (var existing in collisionLayer)
            {
                if (existing.pieceType == null) continue;
                var existingPositions = existing.pieceType.GetGridPositionsList(existing.position, existing.direction);
                foreach (var p in newPositions)
                {
                    if (existingPositions.Contains(p)) return; // Occupied
                }
            }
        }

        Undo.RecordObject(gridDataSO, "Place Manual Piece");
        
        var newData = new GridDataSO.GeneratedPieceData
        {
            pieceType = selectedObstacleType,
            position = origin,
            direction = selectedObstacleDir,
            isObstacle = paintAsObstacle,
            startOnGrid = !placeOffGrid
        };

        if (paintAsObstacle) gridDataSO.staticObstacles.Add(newData);
        else gridDataSO.levelItems.Add(newData);

        EditorUtility.SetDirty(gridDataSO);
        UpdatePuzzleState(); // Force Refresh Summary and Completion
        Repaint();
    }

    private void ValidateLevelSolvability()
    {
        if (gridDataSO == null) return;

        var targetCells = new HashSet<Vector2Int>(gridDataSO.buildableCells.Concat(gridDataSO.lockedCells));
        int totalTargetCount = targetCells.Count;
        
        int occupiedTarget = 0;

        // Check Puzzle Solution (Shapes only)
        if (gridDataSO.puzzleSolution != null)
        {
            foreach (var piece in gridDataSO.puzzleSolution)
            {
                if (piece.pieceType == null) continue;
                if (piece.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape)
                {
                    var cells = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                    foreach(var c in cells) {
                         if (targetCells.Contains(c)) occupiedTarget++;
                    }
                }
            }
        }

        // --- NEW: Check Manual Items (Shapes only) ---
        if (gridDataSO.levelItems != null)
        {
            foreach (var piece in gridDataSO.levelItems)
            {
                if (piece.pieceType == null || piece.isObstacle) continue;
                if (piece.pieceType.category == PlacedObjectTypeSO.ItemCategory.PuzzleShape)
                {
                    var cells = piece.pieceType.GetGridPositionsList(piece.position, piece.direction);
                    foreach(var c in cells) {
                         if (targetCells.Contains(c)) occupiedTarget++;
                    }
                }
            }
        }
        
        bool buildableCovered = occupiedTarget >= totalTargetCount;

        if (!buildableCovered)
        {
            EditorGUILayout.HelpBox($"Level Incomplete! Grid Coverage: {occupiedTarget}/{totalTargetCount} ({(float)occupiedTarget/Mathf.Max(1, totalTargetCount):P0}).\nGenerate a solution or add manual pieces.", MessageType.Error);
        }
        else if (totalTargetCount == 0) {
             EditorGUILayout.HelpBox("Grid is empty! Paint some Buildable/Locked cells.", MessageType.Warning);
        }
        else
        {
             EditorGUILayout.HelpBox("Level Valid: 100% Solvable.", MessageType.Info);
        }
    }



    private void DrawGridDimensions()
    {
         EditorGUILayout.BeginVertical("box");
         EditorGUILayout.PropertyField(serializedObject.FindProperty("width"));
         EditorGUILayout.PropertyField(serializedObject.FindProperty("height"));
         EditorGUILayout.PropertyField(serializedObject.FindProperty("cellSize"));
         EditorGUILayout.EndVertical();
    }

    private void RemoveManualEntryAt(Vector2Int gridPosition, bool onlyObstacles)
    {
        Undo.RecordObject(gridDataSO, "Remove Manual Entry");

        bool removed = false;
        if (onlyObstacles) {
            // Try removing from obstacles
            for (int i = gridDataSO.staticObstacles.Count - 1; i >= 0; i--)
            {
                var p = gridDataSO.staticObstacles[i];
                if (p.pieceType != null && p.pieceType.GetGridPositionsList(p.position, p.direction).Contains(gridPosition))
                {
                    gridDataSO.staticObstacles.RemoveAt(i);
                    removed = true;
                }
            }
        } else {
            // Try removing from items
            for (int i = gridDataSO.levelItems.Count - 1; i >= 0; i--)
            {
                var p = gridDataSO.levelItems[i];
                if (p.pieceType != null && p.pieceType.GetGridPositionsList(p.position, p.direction).Contains(gridPosition))
                {
                    gridDataSO.levelItems.RemoveAt(i);
                    removed = true;
                }
            }
        }

        if (removed)
        {
            EditorUtility.SetDirty(gridDataSO);
            UpdatePuzzleState(); // Force Refresh Summary and Completion
            Repaint();
        }
    }

    private void DrawHatchedRect(Rect rect, Color color)
    {
        // Draw base transparent fill
        EditorGUI.DrawRect(rect, new Color(color.r, color.g, color.b, color.a * 0.3f));

        // Draw diagonal lines at 45 degrees
        Handles.color = color;
        float spacing = 4f;
        for (float i = -rect.height; i < rect.width; i += spacing)
        {
            Vector3 start = new Vector3(Mathf.Clamp(rect.x + i, rect.x, rect.x + rect.width), 
                                        Mathf.Clamp(rect.y + i, rect.y, rect.y + rect.height), 0);
            Vector3 end = new Vector3(Mathf.Clamp(rect.x + rect.height + i, rect.x, rect.x + rect.width), 
                                      Mathf.Clamp(rect.y + rect.height + i, rect.y, rect.y + rect.height), 0);
            
            // This is a bit simplified for 45 deg in a rect, but basically:
            float x0 = rect.x + i;
            float y0 = rect.yMax;
            float x1 = rect.x + i + rect.height;
            float y1 = rect.yMin;

            // Clip to rect
            float xStart = Mathf.Max(rect.x, x0);
            float yStart = y0 - (xStart - x0);
            float xEnd = Mathf.Min(rect.xMax, x1);
            float yEnd = y1 + (x1 - xEnd);

            if (xStart < rect.xMax && xEnd > rect.x)
                Handles.DrawLine(new Vector3(xStart, yStart, 0), new Vector3(xEnd, yEnd, 0));
        }
    }
}
