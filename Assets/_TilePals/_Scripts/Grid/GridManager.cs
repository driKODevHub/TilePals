using UnityEngine;

public class GridManager : MonoBehaviour
{
    public static GridManager Instance { get; private set; }

    public int gridWidth = 10;
    public int gridHeight = 10;
    private bool[,] grid;

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(gameObject);
        }
        else
        {
            Instance = this;
        }

        grid = new bool[gridWidth, gridHeight];
    }

    /// <summary>
    /// Перевіряє, чи можна розмістити прямокутний шейп заданих ОРИГІНАЛЬНИХ розмірів
    /// з вказаним поворотом на сітці, починаючи з (startX, startY).
    /// </summary>
    public bool CanPlaceShape(int startX, int startY, int originalShapeWidth, int originalShapeHeight, int rotationDegrees)
    {
        // Нормалізуємо кут повороту
        rotationDegrees = ((rotationDegrees % 360) + 360) % 360;

        // Перевіряємо кожну клітинку оригінального шейпа
        for (int i = 0; i < originalShapeWidth; i++) // i відповідає localX
        {
            for (int j = 0; j < originalShapeHeight; j++) // j відповідає localY
            {
                // Обчислюємо фактичні координати на сітці для цієї "міні-клітинки" шейпа
                // GetRotatedCellCoordinate перетворює локальні координати (i,j)
                // відносно "нижнього лівого кута" (0,0) оригінального шейпа
                // у відповідні локальні координати обернутого шейпа.
                Vector2Int rotatedCoord = GetRotatedCellCoordinate(i, j, originalShapeWidth, originalShapeHeight, rotationDegrees);
                int checkX = startX + rotatedCoord.x;
                int checkY = startY + rotatedCoord.y;

                // Перевіряємо, чи ця клітинка зайнята або за межами
                if (checkX < 0 || checkX >= gridWidth || checkY < 0 || checkY >= gridHeight || grid[checkX, checkY])
                {
                    return false;
                }
            }
        }
        return true;
    }

    /// <summary>
    /// Розміщує прямокутний шейп заданих ОРИГІНАЛЬНИХ розмірів
    /// з вказаним поворотом на сітці, позначаючи відповідні клітинки як зайняті.
    /// </summary>
    public void PlaceShape(int startX, int startY, int originalShapeWidth, int originalShapeHeight, int rotationDegrees)
    {
        // Нормалізуємо кут повороту
        rotationDegrees = ((rotationDegrees % 360) + 360) % 360;

        Debug.Log($"=== ВІЗУАЛЬНИЙ ПОВОРОТ (GridManager): {rotationDegrees}° ===");
        Debug.Log($"=== РОЗМІРИ СІТКИ: {gridWidth}x{gridHeight} ===");
        Debug.Log($"=== ПОЗИЦІЯ КЛІКНУ (GridManager): startX={startX}, startY={startY} ===");
        Debug.Log($"=== ОРИГІНАЛЬНИЙ ШЕЙП (GridManager): {originalShapeWidth}x{originalShapeHeight} ===");

        string occupiedCells = "";
        for (int i = 0; i < originalShapeWidth; i++)
        {
            for (int j = 0; j < originalShapeHeight; j++)
            {
                // Обчислюємо фактичні координати на сітці для цієї "міні-клітинки" шейпа
                Vector2Int rotatedCoord = GetRotatedCellCoordinate(i, j, originalShapeWidth, originalShapeHeight, rotationDegrees);
                int placeX = startX + rotatedCoord.x;
                int placeY = startY + rotatedCoord.y;

                occupiedCells += $"({placeX},{placeY}) ";

                // Позначаємо клітинку як зайняту
                grid[placeX, placeY] = true;

                // Просимо GridVisualizer оновити матеріал цього тайла
                if (GridVisualizer.Instance != null)
                {
                    GridVisualizer.Instance.UpdateTileMaterial(placeX, placeY, true);
                }
            }
        }

        Debug.Log($"=== ЗАЙНЯТІ КЛІТИНКИ ПІСЛЯ ПЛЕЙСІНГУ (GridManager): {occupiedCells.Trim()} ===");
    }

    /// <summary>
    /// Обчислює координати клітинки (localX, localY) в межах шейпа після повороту.
    /// Це перетворення враховує, що "0,0" є нижнім лівим кутом оригінального шейпа,
    /// і повертає НОВІ локальні координати (offset від 0,0 обернутого шейпа).
    /// </summary>
    private Vector2Int GetRotatedCellCoordinate(int localX, int localY, int shapeWidth, int shapeHeight, int degrees)
    {
        // Нормалізуємо кут, щоб він завжди був у діапазоні [0, 360)
        degrees = ((degrees % 360) + 360) % 360;

        switch (degrees)
        {
            case 0:
                // Без повороту: координати залишаються тими ж
                return new Vector2Int(localX, localY);

            case 90:
                // Поворот на 90° за годинниковою стрілкою (CW)
                // Новий X = (Оригінальна Висота - 1) - Оригінальний Y
                // Новий Y = Оригінальний X
                return new Vector2Int(shapeHeight - 1 - localY, localX);

            case 180:
                // Поворот на 180° (CW або CCW)
                // Новий X = (Оригінальна Ширина - 1) - Оригінальний X
                // Новий Y = (Оригінальна Висота - 1) - Оригінальний Y
                return new Vector2Int(shapeWidth - 1 - localX, shapeHeight - 1 - localY);

            case 270:
                // Поворот на 270° за годинниковою стрілкою (CW)
                // Новий X = Оригінальний Y
                // Новий Y = (Оригінальна Ширина - 1) - Оригінальний X
                return new Vector2Int(localY, shapeWidth - 1 - localX);

            default:
                Debug.LogWarning($"Unsupported rotation degrees: {degrees}. Using 0 degrees.");
                return new Vector2Int(localX, localY);
        }
    }

    public bool IsOccupied(int x, int y)
    {
        if (x < 0 || x >= gridWidth || y < 0 || y >= gridHeight)
        {
            Debug.LogWarning($"Attempted to check occupancy out of bounds: ({x}, {y})");
            return true; // Вважаємо, що за межами сітки - зайнято
        }
        return grid[x, y];
    }
}