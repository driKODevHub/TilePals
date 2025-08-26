using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // Явно вказуємо, що Random з UnityEngine
// UnityEditor не потрібен тут, бо це ScriptableObject, а не компонент з кнопками в редакторі

[CreateAssetMenu(fileName = "RandomCubeGeneratorStrategy", menuName = "VisualGeneration/Random Cube Strategy", order = 1)]
public class RandomCubeGeneratorStrategySO : ScriptableObject, IVisualGeneratorStrategy
{
    [Header("Material Settings")]
    [Tooltip("Масив матеріалів, з яких буде випадковим чином обиратися матеріал для згенерованих кубиків.")]
    public Material[] randomMaterials; // Масив матеріалів для випадкового вибору

    [Header("Overall Grid Settings")]
    [Tooltip("Відступ від зовнішніх країв загальної форми будівлі. Визначає 'поля' навколо згенерованої сітки.")]
    public float overallOuterPadding = 0.1f; // Відступ від зовнішніх країв загальної форми будівлі
    [Tooltip("Базовий розмір однієї клітинки гріда (кубика) у світових одиницях.")]
    public float cubeSize = 1f; // Базовий розмір кубика, якщо 1 юніт гріда = 1 юніт світу

    [Range(0.8f, 1.2f)]
    [Tooltip("Коефіцієнт масштабування для основних кубиків. Дозволяє їм бути трохи меншими, більшими або точно за розміром клітинки.")]
    public float internalCubeScaleFactor = 1.0f; // Масштабування внутрішніх кубиків (1.0 = повний розмір клітинки, >1.0 для перекриття)

    [Header("Random Height Settings")]
    [Tooltip("Максимальна випадкова висота основних кубиків у одиницях гріда (кратна cubeSize).")]
    public int maxRandomHeightUnits = 3; // Максимальна висота в одиницях гріда
    [Range(0f, 1f)]
    [Tooltip("Ймовірність того, що основний кубик матиме 'малу' висоту (менше 1 юніта cubeSize).")]
    public float smallHeightProbability = 0.8f; // Ймовірність малої висоти (менше 1 юніта)

    // Параметри для детальних кубів
    [Header("Detail Cube Settings")]
    [Range(0f, 1f)]
    [Tooltip("Шанс появи КОЖНОГО додаткового (детального) кубика на клітинці.")]
    public float detailCubeSpawnChance = 0.3f; // Шанс появи КОЖНОГО додаткового кубика

    [Range(1, 5)]
    [Tooltip("Максимальна кількість додаткових (детальних) об'єктів, які можуть бути згенеровані на одній клітинці гріда.")]
    public int maxDetailCubesPerCell = 1;

    [Range(0.1f, 1.2f)]
    [Tooltip("Коефіцієнт масштабування розміру (X, Z) детальних кубиків відносно основного cubeSize.")]
    public float detailCubeSizeMultiplier = 0.5f; // Розмір додаткового кубика відносно основного cubeSize

    [Range(0.05f, 5f)]
    [Tooltip("Мінімальна випадкова висота для детальних кубиків.")]
    public float minDetailCubeHeight = 0.2f;
    [Range(0.1f, 5f)]
    [Tooltip("Максимальна випадкова висота для детальних кубиків.")]
    public float maxDetailCubeHeight = 1.0f;

    [Tooltip("Вертикальне зміщення детального кубика над поверхнею базового куба або гріда.")]
    public float detailCubeOffsetAboveBase = 0.05f; // Зміщення над поверхнею базового куба/гріда (вертикальне)

    [Range(0f, 1f)]
    [Tooltip("Коефіцієнт, що визначає максимальне випадкове зміщення центру детального кубика в межах його клітинки (0 = завжди по центру, 1 = максимальне зміщення).")]
    public float detailCubeRandomPositionFactor = 0.0f;

    [Tooltip("Якщо увімкнено, детальні кубики будуть генеруватися тільки на клітинках, які зайняті об'єктом PlacedObjectTypeSO." +
             "Якщо вимкнено, можуть генеруватися по всій bounding box об'єкта.")]
    public bool detailsOnOccupiedCellsOnly = false; // Чи генерувати деталі тільки на клітинках, зайнятих PlacedObjectTypeSO


    // Реалізація інтерфейсу IVisualGeneratorStrategy
    public GameObject GenerateVisual(Transform parentTransform, PlacedObjectTypeSO placedObjectTypeSO)
    {
        if (placedObjectTypeSO == null)
        {
            Debug.LogError("RandomCubeGeneratorStrategySO: placedObjectTypeSO is not assigned when calling GenerateVisual.");
            return null;
        }
        if (placedObjectTypeSO.relativeOccupiedCells == null || placedObjectTypeSO.relativeOccupiedCells.Count == 0)
        {
            Debug.LogWarning("RandomCubeGeneratorStrategySO: relativeOccupiedCells is empty for " + placedObjectTypeSO.objectName);
            return null;
        }

        GameObject visualContainer = new GameObject("GeneratedVisualRoot");
        visualContainer.transform.SetParent(parentTransform);
        visualContainer.transform.localPosition = Vector3.zero;
        visualContainer.transform.localRotation = Quaternion.identity;

        // Отримуємо список позицій, які займає основний об'єкт
        List<Vector2Int> gridPositions = placedObjectTypeSO.GetGridPositionsList(Vector2Int.zero, PlacedObjectTypeSO.Dir.Down);
        // Створюємо HashSet для швидкого пошуку зайнятих клітинок
        HashSet<Vector2Int> occupiedCellsHash = new HashSet<Vector2Int>(gridPositions);

        // Отримуємо максимальні розміри об'єкта PlacedObjectTypeSO для визначення bounding box
        Vector2Int buildingDimensions = placedObjectTypeSO.GetMaxDimensions();

        // Обчислюємо теоретичні розміри сітки, якщо б кубики були cubeSize і торкалися
        float theoreticalGridWidth = buildingDimensions.x * cubeSize;
        float theoreticalGridDepth = buildingDimensions.y * cubeSize;

        // Обчислюємо цільові розміри для візуального контейнера, враховуючи загальний зовнішній відступ
        float targetVisualWidth = theoreticalGridWidth - 2 * overallOuterPadding;
        float targetVisualDepth = theoreticalGridDepth - 2 * overallOuterPadding;

        // Переконуємося, що розміри не стають від'ємними (якщо padding занадто великий)
        targetVisualWidth = Mathf.Max(0.01f, targetVisualWidth);
        targetVisualDepth = Mathf.Max(0.01f, targetVisualDepth);

        // Обчислюємо загальний коефіцієнт масштабування для visualContainer
        float overallScaleX = targetVisualWidth / theoreticalGridWidth;
        float overallScaleZ = targetVisualDepth / theoreticalGridDepth;

        // Застосовуємо загальне зміщення та масштабування до visualContainer
        visualContainer.transform.localPosition = new Vector3(overallOuterPadding, 0, overallOuterPadding);
        visualContainer.transform.localScale = new Vector3(overallScaleX, 1f, overallScaleZ); // Масштабуємо лише X та Z

        // Масив для зберігання висот згенерованих базових кубів
        float[,] baseCubeHeights = new float[buildingDimensions.x, buildingDimensions.y];

        // Фактичний розмір окремих кубиків (X,Z) після застосування internalCubeScaleFactor
        float effectiveBaseCubeLocalSize = cubeSize * internalCubeScaleFactor;

        // ПЕРШИЙ ПРОХІД: Генерація основних кубів та запис їх висот
        foreach (Vector2Int cell in gridPositions)
        {
            // Випадкова висота для основного кубика
            float randomHeight = 0;
            if (Random.value < smallHeightProbability)
            {
                randomHeight = Random.Range(0.1f, 0.9f) * cubeSize;
            }
            else
            {
                randomHeight = Random.Range(1, maxRandomHeightUnits + 1) * cubeSize;
            }

            // Генеруємо основний кубик
            GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
            cube.transform.SetParent(visualContainer.transform);

            // Позиціонуємо кубик відносно центру клітинки. Батьківський об'єкт візьме на себе масштабування.
            cube.transform.localPosition = new Vector3(cell.x * cubeSize + cubeSize / 2, randomHeight / 2, cell.y * cubeSize + cubeSize / 2);
            cube.transform.localScale = new Vector3(effectiveBaseCubeLocalSize, randomHeight, effectiveBaseCubeLocalSize);

            // Призначаємо випадковий матеріал з масиву
            if (randomMaterials != null && randomMaterials.Length > 0)
            {
                cube.GetComponent<Renderer>().material = randomMaterials[Random.Range(0, randomMaterials.Length)];
            }
            else
            {
                Debug.LogWarning("Random Materials array is empty or not assigned. Cubes will use default material.");
            }

            // Зберігаємо висоту згенерованого основного кубика для подальшого використання деталями
            baseCubeHeights[cell.x, cell.y] = randomHeight;
        }

        // ДРУГИЙ ПРОХІД: Генерація детальних кубів у межах bounding box об'єкта
        float effectiveDetailCubeLocalSize = cubeSize * detailCubeSizeMultiplier; // Розмір детального кубика (X,Z)

        for (int x = 0; x < buildingDimensions.x; x++)
        {
            for (int y = 0; y < buildingDimensions.y; y++)
            {
                Vector2Int currentCell = new Vector2Int(x, y);

                // Якщо активовано 'detailsOnOccupiedCellsOnly' і поточна клітинка не зайнята основним об'єктом, пропускаємо її
                if (detailsOnOccupiedCellsOnly && !occupiedCellsHash.Contains(currentCell))
                {
                    continue;
                }

                // Цикл для генерації кількох детальних кубиків на одній клітинці
                for (int i = 0; i < maxDetailCubesPerCell; i++)
                {
                    // Шанс появи кожного детального кубика
                    if (Random.value < detailCubeSpawnChance)
                    {
                        float detailCubeBaseY = 0f;
                        // Визначаємо базову висоту для детального кубика: або над основним кубиком, або над землею
                        if (occupiedCellsHash.Contains(currentCell))
                        {
                            detailCubeBaseY = baseCubeHeights[x, y];
                        }

                        // Випадкова висота для детального кубика
                        float randomDetailHeight = Random.Range(minDetailCubeHeight, maxDetailCubeHeight);

                        // Розрахунок випадкового зміщення в межах клітинки
                        // Доступний діапазон для центру кубика всередині клітинки cubeSize
                        float availableOffsetRange = (cubeSize - effectiveDetailCubeLocalSize);
                        float randomOffsetX = Random.Range(-availableOffsetRange / 2, availableOffsetRange / 2) * detailCubeRandomPositionFactor;
                        float randomOffsetZ = Random.Range(-availableOffsetRange / 2, availableOffsetRange / 2) * detailCubeRandomPositionFactor;


                        // Генеруємо детальний кубик
                        GameObject detailCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                        detailCube.transform.SetParent(visualContainer.transform);

                        // Позиціонуємо детальний кубик
                        detailCube.transform.localPosition = new Vector3(
                            currentCell.x * cubeSize + cubeSize / 2 + randomOffsetX,
                            detailCubeBaseY + randomDetailHeight / 2 + detailCubeOffsetAboveBase,
                            currentCell.y * cubeSize + cubeSize / 2 + randomOffsetZ
                        );
                        detailCube.transform.localScale = new Vector3(effectiveDetailCubeLocalSize, randomDetailHeight, effectiveDetailCubeLocalSize);

                        // Призначаємо випадковий матеріал (використовуємо той самий масив randomMaterials)
                        if (randomMaterials != null && randomMaterials.Length > 0)
                        {
                            detailCube.GetComponent<Renderer>().material = randomMaterials[Random.Range(0, randomMaterials.Length)];
                        }
                        else
                        {
                            Debug.LogWarning("Random Materials array is empty or not assigned for detail cubes. Cubes will use default material.");
                        }
                    }
                } // Кінець циклу для maxDetailCubesPerCell
            }
        }
        SetLayerRecursive(visualContainer, parentTransform.gameObject.layer); // Встановлюємо шар для всього згенерованого візуалу

        return visualContainer;
    }

    // Допоміжний метод для рекурсивного встановлення шару
    private void SetLayerRecursive(GameObject targetGameObject, int layer)
    {
        targetGameObject.layer = layer;
        foreach (Transform child in targetGameObject.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}