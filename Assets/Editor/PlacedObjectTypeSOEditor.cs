using UnityEngine;
using UnityEditor;
using System.Collections.Generic;
using System.Text.RegularExpressions;

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
            foreach (var cell in placedObjectTypeSO.relativeOccupiedCells)
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
        serializedObject.Update();

        DrawPropertiesExcluding(serializedObject, "m_Script", "relativeOccupiedCells");

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Shape Editor", EditorStyles.boldLabel);

        // --- BUTTONS ---
        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Generate from Name (WxH)"))
        {
            GenerateShapeFromObjectName();
        }
        if (GUILayout.Button("Clear Shape"))
        {
            ClearShape();
        }
        EditorGUILayout.EndHorizontal();

        EditorGUILayout.BeginHorizontal();
        if (GUILayout.Button("Auto-Fit to Bounds"))
        {
            AutoFitShapeToBoundingBox();
        }
        if (GUILayout.Button("Set Prefab/Visual from Name"))
        {
            SetPrefabAndVisualFromShape();
        }
        EditorGUILayout.EndHorizontal();

        // --- GENERATE COLLIDER BUTTON ---
        GUI.backgroundColor = new Color(0.7f, 1f, 0.7f);
        if (GUILayout.Button("Generate Compound Colliders (Box)"))
        {
            GenerateCompoundColliders();
        }
        GUI.backgroundColor = Color.white;
        // -------------------------------

        EditorGUILayout.Space();

        // --- GRID DRAWING ---
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

        serializedObject.ApplyModifiedProperties();
    }

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

        for (int i = prefabRoot.transform.childCount - 1; i >= 0; i--)
        {
            Transform child = prefabRoot.transform.GetChild(i);
            if (child.name == containerName)
            {
                DestroyImmediate(child.gameObject);
            }
        }

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

    // --- PREVIEW IMPLEMENTATION ---
    public override bool HasPreviewGUI()
    {
        return true;
    }

    public override void OnPreviewGUI(Rect r, GUIStyle background)
    {
        if (placedObjectTypeSO == null || placedObjectTypeSO.relativeOccupiedCells == null) return;

        // Draw shape in the preview window (bottom area of object picker)
        Vector2Int dims = placedObjectTypeSO.GetMaxDimensions();
        if (dims.x == 0 || dims.y == 0) return;

        // Calculate cell size to best fit the rect
        float padding = 10f;
        float availableWidth = r.width - padding * 2;
        float availableHeight = r.height - padding * 2;
        
        float cellW = availableWidth / dims.x;
        float cellH = availableHeight / dims.y;
        float cellSize = Mathf.Min(cellW, cellH, 40f); // Cap max size

        float totalW = dims.x * cellSize;
        float totalH = dims.y * cellSize;

        // Center content
        float startX = r.x + (r.width - totalW) / 2;
        float startY = r.y + (r.height - totalH) / 2;

        for (int x = 0; x < dims.x; x++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                // Invert Y for drawing top-down
                int gridY = dims.y - 1 - y;
                // Check if this cell is occupied
                if (placedObjectTypeSO.relativeOccupiedCells.Contains(new Vector2Int(x, gridY)))
                {
                    Rect cellRect = new Rect(startX + x * cellSize, startY + y * cellSize, cellSize - 1, cellSize - 1);
                    EditorGUI.DrawRect(cellRect, Color.green);
                }
            }
        }
        
        // Label
        GUIStyle style = new GUIStyle(EditorStyles.label);
        style.alignment = TextAnchor.LowerCenter;
        style.normal.textColor = Color.white;
        Rect labelRect = new Rect(r.x, r.yMax - 20, r.width, 20);
        GUI.Label(labelRect, $"{dims.x}x{dims.y}", style);
    }

    public override Texture2D RenderStaticPreview(string assetPath, Object[] subAssets, int width, int height)
    {
        if (placedObjectTypeSO == null || placedObjectTypeSO.relativeOccupiedCells == null) return null;

        Vector2Int dims = placedObjectTypeSO.GetMaxDimensions();
        if (dims.x <= 0 || dims.y <= 0) return null;

        Texture2D tex = new Texture2D(width, height);
        EditorUtility.CopySerialized(EditorGUIUtility.whiteTexture, tex);

        // Fill with transparent or dark background
        Color[] fillPixels = new Color[width * height];
        for (int i = 0; i < fillPixels.Length; i++) fillPixels[i] = new Color(0, 0, 0, 0); // Transparent
        tex.SetPixels(fillPixels);

        // Draw Logic
        // Scale shape to fit in the icon (width x height)
        // Add some padding
        int padding = 4;
        int safeW = width - padding * 2;
        int safeH = height - padding * 2;

        float cellW = (float)safeW / dims.x;
        float cellH = (float)safeH / dims.y;
        float cellSize = Mathf.Min(cellW, cellH);

        int totalShapeW = Mathf.RoundToInt(dims.x * cellSize);
        int totalShapeH = Mathf.RoundToInt(dims.y * cellSize);

        int startX = padding + (safeW - totalShapeW) / 2;
        int startY = padding + (safeH - totalShapeH) / 2;

        Color shapeColor = new Color(0.2f, 0.8f, 0.2f, 1f); // Greenish

        for (int x = 0; x < dims.x; x++)
        {
            for (int y = 0; y < dims.y; y++)
            {
                if (placedObjectTypeSO.relativeOccupiedCells.Contains(new Vector2Int(x, y)))
                {
                    // Draw cell pixels
                    int pX = startX + Mathf.RoundToInt(x * cellSize);
                    int pY = startY + Mathf.RoundToInt(y * cellSize);
                    int size = Mathf.RoundToInt(cellSize) - 1;
                    if (size < 1) size = 1;

                    for (int px = pX; px < pX + size; px++)
                    {
                        for (int py = pY; py < pY + size; py++)
                        {
                            if (px >= 0 && px < width && py >= 0 && py < height)
                                tex.SetPixel(px, py, shapeColor);
                        }
                    }
                }
            }
        }

        tex.Apply();
        return tex;
    }
}
