using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.IO;
using System.Linq;

[CustomEditor(typeof(PlacedObjectTypeSO))]
public class PlacedObjectTypeSOEditor : Editor
{
    private PlacedObjectTypeSO placedObjectTypeSO;
    private const int MAX_GRID_SIZE = 10;
    private bool[,] editorShapeGrid;

    private void OnEnable()
    {
        placedObjectTypeSO = (PlacedObjectTypeSO)target;
        editorShapeGrid = new bool[MAX_GRID_SIZE, MAX_GRID_SIZE];
        if (placedObjectTypeSO.relativeOccupiedCells != null)
        {
            foreach (Vector2Int cell in placedObjectTypeSO.relativeOccupiedCells)
            {
                if (cell.x >= 0 && cell.x < MAX_GRID_SIZE && cell.y >= 0 && cell.y < MAX_GRID_SIZE)
                {
                    editorShapeGrid[cell.x, cell.y] = true;
                }
            }
        }
    }

    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Shape Editor", EditorStyles.boldLabel);
        EditorGUILayout.HelpBox("Click cells to toggle occupancy. The shape will be defined by the occupied cells starting from (0,0) as the bottom-left.", MessageType.Info);

        EditorGUILayout.Space(5);
        if (GUILayout.Button(new GUIContent("Clear Shape", "Повністю очищує сітку форми, видаляючи всі вибрані клітинки."))) ClearShape();
        if (GUILayout.Button(new GUIContent("Generate Square Shape from Object Name (WxH)", "Намагається зчитати розміри (напр. '3x5') з імені асету і створює прямокутну форму відповідного розміру."))) GenerateShapeFromObjectName();
        if (GUILayout.Button(new GUIContent("Auto-Fit Shape to Bounding Box", "Зміщує фігуру так, щоб її нижній лівий кут знаходився в координатах (0,0), видаляючи порожній простір."))) AutoFitShapeToBoundingBox();
        if (GUILayout.Button(new GUIContent("Set Prefab/Visual from Shape Dimensions", "Автоматично знаходить у проєкті префаб, назва якого відповідає розмірам фігури (напр. 'PB_Shape_3x5'), і призначає його в поля Prefab та Visual."))) SetPrefabAndVisualFromShape();
        if (GUILayout.Button(new GUIContent("Update Internal objectName String", "Оновлює внутрішнє поле 'objectName' на основі поточних розмірів фігури."))) UpdateInternalObjectNameString();

        EditorGUILayout.Space(10);
        EditorGUILayout.LabelField("Compound Collider Generator", EditorStyles.boldLabel);
        if (GUILayout.Button(new GUIContent("Generate Compound Colliders on Prefab", "Аналізує форму та створює оптимальну кількість BoxColliders на префабі.")))
        {
            GenerateCompoundColliders();
        }

        EditorGUILayout.Space(5);

        GUILayout.BeginVertical("box");
        for (int y = MAX_GRID_SIZE - 1; y >= 0; y--)
        {
            GUILayout.BeginHorizontal();
            for (int x = 0; x < MAX_GRID_SIZE; x++)
            {
                GUI.backgroundColor = editorShapeGrid[x, y] ? Color.green : Color.red;
                if (GUILayout.Button("", GUILayout.Width(25), GUILayout.Height(25)))
                {
                    editorShapeGrid[x, y] = !editorShapeGrid[x, y];
                    UpdateRelativeOccupiedCells();
                    GUI.changed = true;
                }
            }
            GUILayout.EndHorizontal();
        }
        GUILayout.EndVertical();
        GUI.backgroundColor = Color.white;

        if (GUI.changed)
        {
            EditorUtility.SetDirty(placedObjectTypeSO);
        }
    }

    // --- МЕТОД ОНОВЛЕНО ---
    private void GenerateCompoundColliders()
    {
        if (placedObjectTypeSO.prefab == null)
        {
            Debug.LogError("Prefab is not assigned in the PlacedObjectTypeSO!");
            return;
        }

        string path = AssetDatabase.GetAssetPath(placedObjectTypeSO.prefab);
        if (string.IsNullOrEmpty(path))
        {
            Debug.LogError("Could not find prefab asset path. Is it a valid prefab?");
            return;
        }

        GameObject prefabRoot = PrefabUtility.LoadPrefabContents(path);

        int prefabLayer = prefabRoot.layer;
        string containerName = "Colliders";

        // --- ВИПРАВЛЕННЯ ---
        // Надійно знаходимо та видаляємо ВСІ попередні контейнери з коллайдерами
        // Ітеруємо у зворотному порядку, оскільки ми видаляємо елементи з колекції
        for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = prefabRoot.transform.GetChild(i);
            if (child.name == containerName)
            {
                DestroyImmediate(child.gameObject);
            }
        }

        // Створюємо один новий, чистий контейнер
        Transform colliderContainer = new GameObject(containerName).transform;
        colliderContainer.SetParent(prefabRoot.transform, false);
        colliderContainer.gameObject.layer = prefabLayer;


        bool[,] shapeGrid = new bool[MAX_GRID_SIZE, MAX_GRID_SIZE];
        foreach (var cell in placedObjectTypeSO.relativeOccupiedCells)
        {
            shapeGrid[cell.x, cell.y] = true;
        }

        for (int y = 0; y < MAX_GRID_SIZE; y++)
        {
            for (int x = 0; x < MAX_GRID_SIZE; x++)
            {
                if (shapeGrid[x, y])
                {
                    int width = 1;
                    while (x + width < MAX_GRID_SIZE && shapeGrid[x + width, y])
                    {
                        width++;
                    }

                    int height = 1;
                    bool canGrow = true;
                    while (y + height < MAX_GRID_SIZE && canGrow)
                    {
                        for (int i = 0; i < width; i++)
                        {
                            if (!shapeGrid[x + i, y + height])
                            {
                                canGrow = false;
                                break;
                            }
                        }
                        if (canGrow)
                        {
                            height++;
                        }
                    }

                    var box = colliderContainer.gameObject.AddComponent<BoxCollider>();
                    float cellSize = 1f;

                    box.size = new Vector3(width * cellSize, 1f, height * cellSize);
                    box.center = new Vector3((x + width / 2f) * cellSize, 0.5f, (y + height / 2f) * cellSize);

                    for (int h = 0; h < height; h++)
                    {
                        for (int w = 0; w < width; w++)
                        {
                            shapeGrid[x + w, y + h] = false;
                        }
                    }
                }
            }
        }

        PrefabUtility.SaveAsPrefabAsset(prefabRoot, path);
        PrefabUtility.UnloadPrefabContents(prefabRoot);
        Debug.Log($"<color=green>Successfully generated compound colliders for {placedObjectTypeSO.name}</color>");
    }


    private void UpdateRelativeOccupiedCells()
    {
        placedObjectTypeSO.relativeOccupiedCells ??= new List<Vector2Int>();
        placedObjectTypeSO.relativeOccupiedCells.Clear();

        for (int x = 0; x < MAX_GRID_SIZE; x++)
        {
            for (int y = 0; y < MAX_GRID_SIZE; y++)
            {
                if (editorShapeGrid[x, y])
                {
                    placedObjectTypeSO.relativeOccupiedCells.Add(new Vector2Int(x, y));
                }
            }
        }
        UpdateInternalObjectNameString();
    }

    private void GenerateShapeFromObjectName()
    {
        string currentAssetName = placedObjectTypeSO.name;
        Match match = Regex.Match(currentAssetName, @"(\d+)x(\d+)");

        if (match.Success)
        {
            if (int.TryParse(match.Groups[1].Value, out int width) && int.TryParse(match.Groups[2].Value, out int height))
            {
                ClearShape();
                for (int y = 0; y < height && y < MAX_GRID_SIZE; y++)
                {
                    for (int x = 0; x < width && x < MAX_GRID_SIZE; x++)
                    {
                        editorShapeGrid[x, y] = true;
                    }
                }
                UpdateRelativeOccupiedCells();
            }
        }
    }

    private void ClearShape()
    {
        for (int x = 0; x < MAX_GRID_SIZE; x++)
        {
            for (int y = 0; y < MAX_GRID_SIZE; y++)
            {
                editorShapeGrid[x, y] = false;
            }
        }
        UpdateRelativeOccupiedCells();
    }

    private void AutoFitShapeToBoundingBox()
    {
        int minX = MAX_GRID_SIZE, minY = MAX_GRID_SIZE, maxX = -1, maxY = -1;
        bool hasOccupiedCells = false;

        for (int x = 0; x < MAX_GRID_SIZE; x++)
        {
            for (int y = 0; y < MAX_GRID_SIZE; y++)
            {
                if (editorShapeGrid[x, y])
                {
                    hasOccupiedCells = true;
                    minX = Mathf.Min(minX, x);
                    minY = Mathf.Min(minY, y);
                    maxX = Mathf.Max(maxX, x);
                    maxY = Mathf.Max(maxY, y);
                }
            }
        }

        if (hasOccupiedCells)
        {
            bool[,] newEditorShapeGrid = new bool[MAX_GRID_SIZE, MAX_GRID_SIZE];
            for (int x = minX; x <= maxX; x++)
            {
                for (int y = minY; y <= maxY; y++)
                {
                    if (editorShapeGrid[x, y])
                    {
                        newEditorShapeGrid[x - minX, y - minY] = true;
                    }
                }
            }
            editorShapeGrid = newEditorShapeGrid;
            UpdateRelativeOccupiedCells();
        }
    }

    private void UpdateInternalObjectNameString()
    {
        Vector2Int currentDims = placedObjectTypeSO.GetMaxDimensions();
        placedObjectTypeSO.objectName = $"Shape_{currentDims.x}x{currentDims.y}";
    }

    private void SetPrefabAndVisualFromShape()
    {
        Vector2Int currentDims = placedObjectTypeSO.GetMaxDimensions();
        string expectedPrefabName = $"PB_Shape_{currentDims.x}x{currentDims.y}";
        string[] guids = AssetDatabase.FindAssets($"{expectedPrefabName} t:GameObject");

        if (guids.Length > 0)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guids[0]);
            GameObject foundPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(assetPath);

            if (foundPrefab != null)
            {
                placedObjectTypeSO.prefab = foundPrefab.transform;
                placedObjectTypeSO.visual = foundPrefab.transform;
            }
        }
    }
}
