using System;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using Object = UnityEngine.Object;

public class FaceCamera : MonoBehaviour {
    private Transform cam;
    void Start() { cam = Camera.main?.transform; }
    void LateUpdate() { if (cam != null) transform.forward = cam.forward; }
}

public class GridXZ<TGridObject>
{
    public event EventHandler<OnGridObjectChangedEventArgs> OnGridObjectChanged;
    public class OnGridObjectChangedEventArgs : EventArgs
    {
        public int x;
        public int z;
    }

    private int width;
    private int height;
    private float cellSize;
    private Vector3 originPosition;
    private TGridObject[,] gridArray;

    // --- ПОЛЕ ДЛЯ ДЕБАГУ ---
    private List<GameObject> _debugTextObjects;
    private TextMesh[,] _debugTextArray;
    private bool _showDebug = false;
    private Transform _debugParent;

    public GridXZ(int width, int height, float cellSize, Vector3 originPosition, Transform debugParent, Func<GridXZ<TGridObject>, int, int, TGridObject> createGridObject)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.originPosition = originPosition;
        this._debugParent = debugParent;

        gridArray = new TGridObject[width, height];

        for (int x = 0; x < gridArray.GetLength(0); x++)
        {
            for (int z = 0; z < gridArray.GetLength(1); z++)
            {
                gridArray[x, z] = createGridObject(this, x, z);
            }
        }

        // --- ІНІЦІАЛІЗАЦІЯ ДЕБАГ-ТЕКСТУ ---
        InitializeDebugText();
    }

    // --- НОВИЙ ПРИВАТНИЙ МЕТОД: Ініціалізація дебаг-тексту ---
    private void InitializeDebugText()
    {
        _debugTextObjects = new List<GameObject>();
        _debugTextArray = new TextMesh[width, height];

        OnGridObjectChanged += (object sender, OnGridObjectChangedEventArgs eventArgs) => {
            if (_showDebug && eventArgs.x >= 0 && eventArgs.x < width && eventArgs.z >= 0 && eventArgs.z < height)
            {
                if (_debugTextArray != null && _debugTextArray[eventArgs.x, eventArgs.z] != null)
                {
                    var textMesh = _debugTextArray[eventArgs.x, eventArgs.z];
                    textMesh.text = gridArray[eventArgs.x, eventArgs.z]?.ToString();
                    FitToCell(textMesh.gameObject);
                }
            }
        };

        if (_showDebug) ShowDebugText();
    }

    // --- НАЛАШТУВАННЯ ДЕБАГУ ---
    private const float DEBUG_TEXT_MAX_SCALE = 0.06f; // Можна змінювати цей коефіцієнт
    private const float DEBUG_TEXT_MAX_WIDTH_PERCENT = 0.9f;

    private void FitToCell(GameObject textObj)
    {
        if (textObj == null) return;
        
        TextMesh textMesh = textObj.GetComponent<TextMesh>();
        if (textMesh != null)
        {
            // Simple "column" wrap: replace underscores/spaces with newlines
            // Also adds a newline after every 15 characters if no separators
            textMesh.text = WrapText(textMesh.text, 12);
        }

        textObj.transform.localScale = Vector3.one * (cellSize * DEBUG_TEXT_MAX_SCALE); 
        
        MeshRenderer renderer = textObj.GetComponent<MeshRenderer>();
        if (renderer == null) return;

        float maxWidth = cellSize * DEBUG_TEXT_MAX_WIDTH_PERCENT;
        float currentWidth = renderer.bounds.size.x;

        if (currentWidth > maxWidth)
        {
            float scaleFactor = maxWidth / currentWidth;
            textObj.transform.localScale *= scaleFactor;
        }
    }

    private string WrapText(string input, int maxLineLength)
    {
        if (string.IsNullOrEmpty(input)) return "";
        
        // Don't wrap if it looks like coordinates (e.g., "0,1" or "2,3")
        // Simple check: if it contains only digits, commas, and spaces, keep it as-is
        bool looksLikeCoordinates = System.Text.RegularExpressions.Regex.IsMatch(input, @"^[\d,\s]+$");
        if (looksLikeCoordinates) return input;
        
        // For other text (like POT_TilePall_3x2), wrap by replacing separators
        string result = input.Replace("_", "\n").Replace(" ", "\n");
        return result;
    }

    private void ShowDebugText()
    {
        ClearDebugText(); 

        int createdCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int z = 0; z < height; z++)
            {
                // We pass NULL as parent to utility to ensure it treats the position as World Coordinates.
                // We also add Y=0.2f to avoid Z-fighting with the floor.
                Vector3 worldPos = GetWorldPosition(x, z) + new Vector3(cellSize, 0.4f, cellSize) * .5f;
                
                GameObject textGameObject = UtilsClass.CreateWorldText(
                    gridArray[x, z]?.ToString(),
                    null, 
                    worldPos,
                    35,
                    Color.white,
                    TextAnchor.MiddleCenter,
                    TextAlignment.Center
                ).gameObject;

                // Now we attach it to our parent but KEEP world position (true)
                if (_debugParent != null) textGameObject.transform.SetParent(_debugParent, true);

                textGameObject.AddComponent<FaceCamera>();
                FitToCell(textGameObject);

                _debugTextArray[x, z] = textGameObject.GetComponent<TextMesh>();
                _debugTextObjects.Add(textGameObject);
                createdCount++;
            }
        }
    }

    // --- ПУБЛІЧНИЙ МЕТОД: Перемикання видимості дебаг-тексту (Fix CS1061) ---
    public void SetDebugTextVisibility(bool isVisible)
    {
        if (_showDebug == isVisible) return;
        _showDebug = isVisible;

        if (_showDebug) ShowDebugText();
        else ClearDebugText();
    }

    public void ClearDebugText()
    {
        if (_debugTextObjects == null) return;

        foreach (var textObject in _debugTextObjects)
        {
            if (textObject != null) Object.Destroy(textObject);
        }
        _debugTextObjects.Clear();
        _debugTextArray = new TextMesh[width, height];
    }


    public int GetWidth()
    {
        return width;
    }

    public int GetHeight()
    {
        return height;
    }

    public float GetCellSize()
    {
        return cellSize;
    }

    public Vector3 GetWorldPosition(int x, int z)
    {
        return new Vector3(x, 0, z) * cellSize + originPosition;
    }

    public void GetXZ(Vector3 worldPosition, out int x, out int z)
    {
        x = Mathf.FloorToInt((worldPosition - originPosition).x / cellSize);
        z = Mathf.FloorToInt((worldPosition - originPosition).z / cellSize);
    }

    public void SetGridObject(int x, int z, TGridObject value)
    {
        if (x >= 0 && z >= 0 && x < width && z < height)
        {
            gridArray[x, z] = value;
            TriggerGridObjectChanged(x, z);
        }
    }

    public void TriggerGridObjectChanged(int x, int z)
    {
        OnGridObjectChanged?.Invoke(this, new OnGridObjectChangedEventArgs { x = x, z = z });
    }

    public void SetGridObject(Vector3 worldPosition, TGridObject value)
    {
        GetXZ(worldPosition, out int x, out int z);
        SetGridObject(x, z, value);
    }

    public TGridObject GetGridObject(int x, int z)
    {
        if (x >= 0 && z >= 0 && x < width && z < height)
        {
            return gridArray[x, z];
        }
        else
        {
            return default(TGridObject);
        }
    }

    public TGridObject GetGridObject(Vector3 worldPosition)
    {
        int x, z;
        GetXZ(worldPosition, out x, out z);
        return GetGridObject(x, z);
    }

    public Vector2Int ValidateGridPosition(Vector2Int gridPosition)
    {
        return new Vector2Int(
            Mathf.Clamp(gridPosition.x, 0, width - 1),
            Mathf.Clamp(gridPosition.y, 0, height - 1)
        );
    }

    public bool IsValidGridPosition(Vector2Int gridPosition)
    {
        return gridPosition.x >= 0 && gridPosition.y >= 0 && gridPosition.x < width && gridPosition.y < height;
    }
}