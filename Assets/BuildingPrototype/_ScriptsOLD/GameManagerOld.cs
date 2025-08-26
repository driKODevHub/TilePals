using UnityEngine;

public class GameManagerOld : MonoBehaviour
{
    public static GameManagerOld Instance { get; private set; }

    public GameObject shapePrefab;

    // Зроблено публічними, щоб ShapePreview міг їх прочитати
    [HideInInspector] public int originalWidth;
    [HideInInspector] public int originalHeight;

    private int currentWidth;   // Використовується для візуального розміру прев'ю та розміщеного шейпа
    private int currentHeight;  // Використовується для візуального розміру прев'ю та розміщеного шейпа
    private int currentRotationDegrees = 0; // 0, 90, 180, 270

    // Публічні властивості для доступу до поточних візуальних розмірів та повороту з інших скриптів
    public int CurrentWidth => currentWidth;
    public int CurrentHeight => currentHeight;
    public int CurrentRotationDegrees => currentRotationDegrees;

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
    }

    void Start()
    {
        RollDiceAndGenerateShape();
    }

    void Update()
    {
        float scroll = Input.mouseScrollDelta.y;
        if (scroll != 0)
        {
            if (scroll > 0)
            {
                currentRotationDegrees += 90;  // Завжди за годинниковою стрілкою
            }
            else
            {
                currentRotationDegrees -= 90;  // Завжди проти годинникової стрілки
            }

            // Нормалізуємо кут, щоб він завжди був у діапазоні [0, 360)
            currentRotationDegrees = ((currentRotationDegrees % 360) + 360) % 360;

            // Оновлюємо поточні розміри для ПРЕВ'Ю та візуалізації
            UpdateCurrentDimensionsForVisuals();

            //Debug.Log($"Rotated shape to {currentRotationDegrees} degrees. Visual Dimensions: {currentWidth} x {currentHeight}");
        }
    }

    void RollDiceAndGenerateShape()
    {
        currentRotationDegrees = 0; // Скидаємо поворот при генерації нової фігури
        originalWidth = Random.Range(1, 7);
        originalHeight = Random.Range(1, 7);


        // Оновлюємо поточні розміри для ПРЕВ'Ю та візуалізації
        UpdateCurrentDimensionsForVisuals();
    }

    // Оновлює currentWidth та currentHeight, які використовуються для Scale об'єкта та прев'ю
    // Ці розміри відображають візуальні розміри ШЕЙПА ПІСЛЯ повороту
    private void UpdateCurrentDimensionsForVisuals()
    {
        // Якщо кут 90 або 270 градусів, ширина і висота міняються місцями для візуалізації
        if (currentRotationDegrees == 90 || currentRotationDegrees == 270)
        {
            currentWidth = originalHeight;
            currentHeight = originalWidth;
        }
        else
        {
            currentWidth = originalWidth;
            currentHeight = originalHeight;
        }
    }

    public void TryPlaceShapeAt(int gridX, int gridY)
    {
        // Тепер GridManager сам обчислює, які клітинки займає шейп, враховуючи його оригінальні розміри та поворот
        if (GridManager.Instance != null && GridManager.Instance.CanPlaceShape(gridX, gridY, originalWidth, originalHeight, currentRotationDegrees))
        {
            GameObject shape = Instantiate(shapePrefab);

            // Встановлюємо позицію батьківського об'єкта шейпа.
            // Оскільки півот префаба знаходиться в його нижньому лівому куті,
            // (gridX, 0, gridY) буде точно відповідати нижньому лівому куту ШЕЙПА на сітці.
            shape.transform.position = new Vector3(gridX, 0, gridY);

            // Встановлюємо масштаб батьківського об'єкта шейпа.
            // currentWidth та currentHeight вже враховують візуальні зміни розмірів після повороту.
            // Наприклад, для 2x3 повернутого на 90, currentWidth буде 3, currentHeight буде 2.
            // Це дозволяє візуально розтягнути об'єкт відповідно до його повернутих розмірів.
            shape.transform.localScale = new Vector3(currentWidth, 1, currentHeight); // Висота 1 для плоских об'єктів

            // Застосовуємо поворот до батьківського об'єкта.
            // Обертання відбувається навколо півоту, який ми розмістили в (0,0) батьківського об'єкта.
            shape.transform.rotation = Quaternion.Euler(0, currentRotationDegrees, 0);

            // Повідомляємо GridManager про розміщення, передаючи ОРИГІНАЛЬНІ розміри та кут повороту.
            // GridManager використовує GetRotatedCellCoordinate для обчислення зайнятих клітинок.
            GridManager.Instance.PlaceShape(gridX, gridY, originalWidth, originalHeight, currentRotationDegrees);

            RollDiceAndGenerateShape();
        }
        else
        {
            Debug.Log("Can't place shape here!");
        }
    }
}