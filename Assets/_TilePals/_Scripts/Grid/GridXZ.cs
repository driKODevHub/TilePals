using System;
using System.Collections.Generic;
using UnityEngine;
using CodeMonkey.Utils;
using Object = UnityEngine.Object;

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

    // --- НОВЕ ПОЛЕ ---
    // Зберігаємо посилання на створені дебаг-об'єкти
    private List<GameObject> _debugTextObjects;

    public GridXZ(int width, int height, float cellSize, Vector3 originPosition, Func<GridXZ<TGridObject>, int, int, TGridObject> createGridObject)
    {
        this.width = width;
        this.height = height;
        this.cellSize = cellSize;
        this.originPosition = originPosition;

        gridArray = new TGridObject[width, height];

        for (int x = 0; x < gridArray.GetLength(0); x++)
        {
            for (int z = 0; z < gridArray.GetLength(1); z++)
            {
                gridArray[x, z] = createGridObject(this, x, z);
            }
        }

        bool showDebug = true;
        if (showDebug)
        {
            _debugTextObjects = new List<GameObject>(); // Ініціалізуємо список
            TextMesh[,] debugTextArray = new TextMesh[width, height];

            for (int x = 0; x < gridArray.GetLength(0); x++)
            {
                for (int z = 0; z < gridArray.GetLength(1); z++)
                {
                    GameObject textGameObject = UtilsClass.CreateWorldText(
                        gridArray[x, z]?.ToString(),
                        null,
                        GetWorldPosition(x, z) + new Vector3(cellSize, 0, cellSize) * .5f,
                        35,
                        Color.white,
                        TextAnchor.MiddleCenter,
                        TextAlignment.Center
                    ).gameObject;

                    textGameObject.transform.localScale = Vector3.one * 0.03f;
                    debugTextArray[x, z] = textGameObject.GetComponent<TextMesh>();

                    // Додаємо об'єкт до списку для подальшого видалення
                    _debugTextObjects.Add(textGameObject);
                }
            }

            OnGridObjectChanged += (object sender, OnGridObjectChangedEventArgs eventArgs) => {
                if (debugTextArray[eventArgs.x, eventArgs.z] != null)
                    debugTextArray[eventArgs.x, eventArgs.z].text = gridArray[eventArgs.x, eventArgs.z]?.ToString();
            };
        }
    }

    // --- НОВИЙ МЕТОД ---
    // Метод для очищення дебаг-тексту
    public void ClearDebugText()
    {
        if (_debugTextObjects == null) return;

        foreach (var textObject in _debugTextObjects)
        {
            Object.Destroy(textObject);
        }
        _debugTextObjects.Clear();
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
}
