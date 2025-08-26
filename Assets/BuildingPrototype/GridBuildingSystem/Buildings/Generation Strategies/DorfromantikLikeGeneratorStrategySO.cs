using System.Collections.Generic;
using UnityEngine;
using Random = UnityEngine.Random; // Явно вказуємо, що Random з UnityEngine

[CreateAssetMenu(fileName = "DorfromantikLikeStrategy", menuName = "VisualGeneration/Dorfromantik-Like Strategy", order = 2)]
public class DorfromantikLikeGeneratorStrategySO : ScriptableObject, IVisualGeneratorStrategy
{
    // Опис типів синергій
    public enum SynergyType
    {
        None,        // Для клітинок, які не належать до жодної специфічної синергії, або де ще не визначено
        Forest,      // Ліс: дерева (хвойні, листяні)
        Field,       // Поле: відкриті простори
        Water,       // Водойма: вода нижче рівня землі
        Settlement,  // Поселення: будівлі
        Road         // Дорога: залізничні колії
    }

    [Header("General Settings")]
    [Tooltip("Розмір одного тайла/клітинки у світових одиницях. Вся генерація базується на цьому розмірі.")]
    public float cellSize = 1f; // Розмір 1 тайлу - 1 юніт

    [Header("Materials")]
    public Material grassMaterial;
    public Material dirtMaterial;
    public Material stoneMaterial;
    public Material sandMaterial;
    public Material waterMaterial;
    public Material trunkMaterial;
    public Material leafMaterial;
    public Material coniferousLeafMaterial; // Окремий матеріал для хвойних листя
    public Material buildingWallMaterial;
    public Material buildingRoofMaterial;
    public Material roadMaterial;
    public Material defaultMaterial; // Запасний матеріал на випадок, якщо якийсь не призначено

    [Header("Ground Generation Settings")]
    [Tooltip("Чи випадково змінювати висоту землі для кожної клітинки.")]
    public bool randomizeGroundHeight = true; // За замовчуванням рандомізуємо висоту
    [Tooltip("Мінімальна висота землі (повинна бути вище рівня води за замовчуванням).")]
    [Range(0.2f, 0.5f)]
    public float minGroundHeight = 0.3f; // Збільшена мінімальна висота землі
    [Tooltip("Максимальна висота землі, якщо рандомізація увімкнена.")]
    [Range(0.3f, 0.8f)]
    public float maxRandomGroundHeight = 0.6f; // Збільшена максимальна висота землі

    [Header("Synergy Clustering Settings")]
    [Tooltip("Шанс того, що кластер продовжить зростати в сусідню клітинку.")]
    [Range(0.0f, 1.0f)]
    public float clusterGrowChance = 0.7f; // Ймовірність, що кластер продовжить зростати
    [Tooltip("Вага для шансу появи лісової синергії.")]
    public int forestSynergyWeight = 3;
    [Tooltip("Вага для шансу появи польової синергії.")]
    public int fieldSynergyWeight = 2;
    [Tooltip("Вага для шансу появи водної синергії.")]
    public int waterSynergyWeight = 1;
    [Tooltip("Вага для шансу появи синергії поселення.")]
    public int settlementSynergyWeight = 2;
    [Tooltip("Вага для шансу появи дорожньої синергії.")]
    public int roadSynergyWeight = 1;

    [Header("Forest Settings")]
    [Tooltip("Шанс появи дерева на клітинці лісової синергії (якщо дозволено min/max).")]
    [Range(0f, 1f)]
    public float treeSpawnChancePerCell = 0.8f;
    [Tooltip("Мінімальна кількість дерев на клітинці лісової синергії.")]
    public int minTreesPerCell = 3; // Тепер до 6 дерев
    [Tooltip("Максимальна кількість дерев на клітинці лісової синергії.")]
    public int maxTreesPerCell = 6;
    [Tooltip("Базовий масштаб окремого дерева в межах клітинки.")]
    [Range(0.05f, 0.3f)]
    public float individualTreeBaseScale = 0.15f; // Менший базовий масштаб для кожного дерева
    [Tooltip("Мінімальна висота стовбура дерева (кількість базових одиниць).")]
    public int minTrunkHeight = 1;
    [Tooltip("Максимальна висота стовбура дерева (кількість базових одиниць).")]
    public int maxTrunkHeight = 3;
    [Tooltip("Шанс, що дерево буде хвойним.")]
    [Range(0f, 1f)]
    public float coniferousTreeChance = 0.4f;
    [Tooltip("Базовий масштаб листя листяного дерева (множиться на individualTreeBaseScale).")]
    public float deciduousLeavesBaseScale = 1.2f;
    [Tooltip("Кількість шарів листя для хвойного дерева.")]
    public int coniferousLeavesLayers = 3;
    [Tooltip("Базовий масштаб для нижнього шару листя хвойного дерева (множиться на individualTreeBaseScale).")]
    public float coniferousLeavesLayerScale = 1.0f;
    [Tooltip("Наскільки зменшується масштаб листя з кожним вищим шаром для хвойного дерева.")]
    public float coniferousLeavesScaleDecreaseFactor = 0.2f;

    [Header("Settlement Settings")]
    [Tooltip("Шанс появи будівлі на клітинці синергії поселення (якщо дозволено min/max).")]
    [Range(0f, 1f)]
    public float buildingSpawnChancePerCell = 0.7f;
    [Tooltip("Мінімальна кількість будівель на клітинці.")]
    public int minBuildingsPerCell = 1;
    [Tooltip("Максимальна кількість будівель на клітинці.")]
    public int maxBuildingsPerCell = 6; // Тепер до 6 будівель
    [Tooltip("Базовий масштаб окремої будівлі (X, Z) в межах клітинки.")]
    [Range(0.05f, 0.3f)]
    public float individualBuildingBaseScale = 0.15f; // Менший базовий масштаб для кожної будівлі
    [Tooltip("Мінімальна випадкова висота стін окремої будівлі.")]
    [Range(0.001f, 0.8f)]
    public float individualBuildingMinHeight = 0.2f; // Менша висота
    [Tooltip("Максимальна випадкова висота стін окремої будівлі.")]
    [Range(0.002f, 1.5f)]
    public float individualBuildingMaxHeight = 0.8f; // Менша висота
    [Tooltip("Відносна висота даху окремої будівлі.")]
    [Range(0.05f, 0.5f)]
    public float individualRoofHeight = 0.2f; // Менша висота даху
    [Tooltip("Наскільки дах виступає за краї стін окремої будівлі.")]
    [Range(0.01f, 0.1f)]
    public float individualRoofOverhang = 0.05f; // Менший виступ даху

    [Header("Road Settings")]
    [Tooltip("Ширина дорожнього полотна.")]
    [Range(0.1f, 0.9f)]
    public float roadWidth = 0.6f;
    [Tooltip("Висота дорожнього полотна.")]
    [Range(0.01f, 0.2f)]
    public float roadHeight = 0.05f;
    [Tooltip("Вертикальне зміщення дороги над поверхнею землі.")]
    public float roadOffsetAboveGround = 0.01f;

    [Header("Water Settings")]
    [Tooltip("Вертикальне зміщення води відносно рівня землі (від'ємне значення означає нижче).")]
    [Range(-0.5f, 0.0f)]
    public float waterLevelOffset = -0.4f; // Значно нижче відносно рівня землі
    [Tooltip("Глибина водойми.")]
    [Range(0.05f, 0.5f)]
    public float waterDepth = 0.2f; // Зменшена глибина води


    // Словник для зберігання інформації про синергію для кожної клітинки
    // Ця інформація буде доступна для "майбутнього будівництва"
    private Dictionary<Vector2Int, SynergyType> _cellSynergies;
    public IReadOnlyDictionary<Vector2Int, SynergyType> CellSynergies => _cellSynergies; // Публічний доступ тільки для читання

    // --- Реалізація IVisualGeneratorStrategy ---
    public GameObject GenerateVisual(Transform parentTransform, PlacedObjectTypeSO placedObjectTypeSO)
    {
        if (placedObjectTypeSO == null)
        {
            Debug.LogError("DorfromantikLikeGeneratorStrategySO: PlacedObjectTypeSO is not assigned.");
            return null;
        }

        GameObject visualContainer = new GameObject("DorfromantikVisual");
        visualContainer.transform.SetParent(parentTransform);
        visualContainer.transform.localPosition = Vector3.zero;
        visualContainer.transform.localRotation = Quaternion.identity;

        List<Vector2Int> gridPositions = placedObjectTypeSO.GetGridPositionsList(Vector2Int.zero, PlacedObjectTypeSO.Dir.Down);
        HashSet<Vector2Int> occupiedCellsHash = new HashSet<Vector2Int>(gridPositions);
        Vector2Int buildingDimensions = placedObjectTypeSO.GetMaxDimensions();

        // Ініціалізація словника синергій
        _cellSynergies = new Dictionary<Vector2Int, SynergyType>();
        for (int x = 0; x < buildingDimensions.x; x++)
        {
            for (int z = 0; z < buildingDimensions.y; z++)
            {
                Vector2Int cell = new Vector2Int(x, z);
                if (occupiedCellsHash.Contains(cell))
                {
                    _cellSynergies[cell] = SynergyType.None; // Початково всі зайняті клітинки без синергії
                }
            }
        }

        // Крок 1: Кластеризація синергій
        GenerateSynergyClusters(occupiedCellsHash);

        // Масив для зберігання висот згенерованої землі
        float[,] groundHeights = new float[buildingDimensions.x, buildingDimensions.y];

        // Крок 2: Генерація землі та базових поверхонь
        foreach (Vector2Int cell in gridPositions)
        {
            // Визначаємо висоту землі тільки для клітинок, які не є водою.
            // Вода буде генеруватися окремо і мати свою "висоту дна"
            float currentGroundHeight = 0f;
            SynergyType groundSynergy = _cellSynergies.ContainsKey(cell) ? _cellSynergies[cell] : SynergyType.None;

            if (groundSynergy != SynergyType.Water)
            {
                currentGroundHeight = randomizeGroundHeight ? Random.Range(minGroundHeight, maxRandomGroundHeight) : minGroundHeight;

                GameObject groundCube = GameObject.CreatePrimitive(PrimitiveType.Cube);
                groundCube.transform.SetParent(visualContainer.transform);
                groundCube.transform.localPosition = new Vector3(cell.x * cellSize + cellSize / 2, currentGroundHeight / 2, cell.y * cellSize + cellSize / 2);
                groundCube.transform.localScale = new Vector3(cellSize, currentGroundHeight, cellSize);

                Material groundMat = defaultMaterial;
                switch (groundSynergy)
                {
                    case SynergyType.Field:
                    case SynergyType.Forest:
                        groundMat = grassMaterial; // Завжди зелене поле для лісу та полів
                        break;
                    case SynergyType.Settlement:
                        groundMat = dirtMaterial; // Брудна земля для поселень
                        break;
                    case SynergyType.Road:
                        groundMat = dirtMaterial; // Брудна земля під дорогами
                        break;
                    case SynergyType.None:
                    default:
                        groundMat = grassMaterial; // За замовчуванням зелене поле
                        break;
                }
                groundCube.GetComponent<Renderer>().material = groundMat != null ? groundMat : defaultMaterial;
            }
            else
            {
                // Для води, базова "земля" буде на рівні води, або її взагалі не буде (буде лише водна площина)
                // Якщо ми хочемо показати "дно", то можна тут згенерувати площину піску на рівні waterLevelOffset - waterDepth
                // Але в даному випадку, ми просто не генеруємо GroundCube для Water Synergy,
                // а Water буде генеруватися окремо як площина
            }
            groundHeights[cell.x, cell.y] = currentGroundHeight; // Зберігаємо висоту для подальшого використання
        }

        // Крок 3: Генерація деталей на основі синергій
        foreach (Vector2Int cell in gridPositions)
        {
            SynergyType synergy = _cellSynergies.ContainsKey(cell) ? _cellSynergies[cell] : SynergyType.None;
            float currentGroundHeight = groundHeights[cell.x, cell.y];

            Vector3 cellCenterWorldPos = new Vector3(cell.x * cellSize + cellSize / 2, 0, cell.y * cellSize + cellSize / 2);

            switch (synergy)
            {
                case SynergyType.Forest:
                    if (Random.value < treeSpawnChancePerCell)
                    {
                        GenerateTree(visualContainer.transform, cellCenterWorldPos, currentGroundHeight);
                    }
                    break;
                case SynergyType.Field:
                    // Поля можуть бути просто землею або мати дрібні випадкові деталі, поки що просто земля
                    break;
                case SynergyType.Water:
                    // Вода генерується окремо, її "дно" буде на waterLevelOffset
                    GenerateWater(visualContainer.transform, cellCenterWorldPos, currentGroundHeight);
                    break;
                case SynergyType.Settlement:
                    if (Random.value < buildingSpawnChancePerCell)
                    {
                        GenerateBuilding(visualContainer.transform, cellCenterWorldPos, currentGroundHeight);
                    }
                    break;
                case SynergyType.Road:
                    GenerateRoad(visualContainer.transform, cellCenterWorldPos, currentGroundHeight);
                    break;
                case SynergyType.None:
                    // Якщо синергія не визначена, це може бути просто земля
                    break;
            }
        }

        SetLayerRecursive(visualContainer, parentTransform.gameObject.layer); // Встановлюємо шар для всього згенерованого візуалу

        return visualContainer;
    }

    // --- Допоміжні методи для генерації ---

    private void GenerateSynergyClusters(HashSet<Vector2Int> occupiedCells)
    {
        List<Vector2Int> unassignedCellsList = new List<Vector2Int>(occupiedCells);

        List<SynergyType> weightedSynergyTypes = new List<SynergyType>();
        for (int i = 0; i < forestSynergyWeight; i++) weightedSynergyTypes.Add(SynergyType.Forest);
        for (int i = 0; i < fieldSynergyWeight; i++) weightedSynergyTypes.Add(SynergyType.Field);
        for (int i = 0; i < waterSynergyWeight; i++) weightedSynergyTypes.Add(SynergyType.Water);
        for (int i = 0; i < settlementSynergyWeight; i++) weightedSynergyTypes.Add(SynergyType.Settlement);
        for (int i = 0; i < roadSynergyWeight; i++) weightedSynergyTypes.Add(SynergyType.Road);

        if (weightedSynergyTypes.Count == 0)
        {
            Debug.LogWarning("No synergy weights set, defaulting to Field synergy for all cells.");
            for (int i = 0; i < 5; i++) weightedSynergyTypes.Add(SynergyType.Field); // Fallback
        }


        while (unassignedCellsList.Count > 0)
        {
            int randomIndex = Random.Range(0, unassignedCellsList.Count);
            Vector2Int startCell = unassignedCellsList[randomIndex];

            SynergyType chosenSynergyType = weightedSynergyTypes[Random.Range(0, weightedSynergyTypes.Count)];

            Queue<Vector2Int> queue = new Queue<Vector2Int>();
            queue.Enqueue(startCell);

            // Якщо клітинка вже була призначена іншим кластером, пропускаємо
            if (_cellSynergies.ContainsKey(startCell) && _cellSynergies[startCell] != SynergyType.None)
            {
                unassignedCellsList.Remove(startCell); // Видаляємо її, щоб не обробляти знову
                continue;
            }

            _cellSynergies[startCell] = chosenSynergyType;
            unassignedCellsList.Remove(startCell);

            while (queue.Count > 0)
            {
                Vector2Int current = queue.Dequeue();

                // Перевіряємо сусідні клітинки
                Vector2Int[] neighbors = new Vector2Int[]
                {
                    current + new Vector2Int(1, 0),
                    current + new Vector2Int(-1, 0),
                    current + new Vector2Int(0, 1),
                    current + new Vector2Int(0, -1)
                };

                foreach (Vector2Int neighbor in neighbors)
                {
                    if (unassignedCellsList.Contains(neighbor) && Random.value < clusterGrowChance)
                    {
                        // Перевіряємо, чи клітинка вже не була зайнята іншим кластером у цьому ж проході
                        if (_cellSynergies.ContainsKey(neighbor) && _cellSynergies[neighbor] != SynergyType.None)
                        {
                            unassignedCellsList.Remove(neighbor); // Вже зайнята, не чіпаємо
                            continue;
                        }

                        _cellSynergies[neighbor] = chosenSynergyType;
                        unassignedCellsList.Remove(neighbor);
                        queue.Enqueue(neighbor);
                    }
                }
            }
        }
    }

    private void GenerateTree(Transform parent, Vector3 cellCenter, float groundHeight)
    {
        int numTrees = Random.Range(minTreesPerCell, maxTreesPerCell + 1);
        for (int i = 0; i < numTrees; i++)
        {
            // Випадкове зміщення всередині клітинки для розміщення кількох дерев
            float offsetX = Random.Range(-0.4f, 0.4f) * cellSize;
            float offsetZ = Random.Range(-0.4f, 0.4f) * cellSize;
            Vector3 treeLocalCenter = new Vector3(cellCenter.x + offsetX, groundHeight, cellCenter.z + offsetZ);

            bool isConiferous = Random.value < coniferousTreeChance;
            int trunkH = Random.Range(minTrunkHeight, maxTrunkHeight + 1); // Висота стовбура в "одиницях"

            // Масштабуємо стовбур відносно individualTreeBaseScale та cellSize
            float trunkWidth = individualTreeBaseScale * cellSize;
            float actualTrunkHeight = trunkH * individualTreeBaseScale * cellSize * 0.5f; // Висота стовбура також масштабується

            // Стовбур
            GameObject trunk = GameObject.CreatePrimitive(PrimitiveType.Cube);
            trunk.transform.SetParent(parent);
            trunk.transform.localPosition = new Vector3(
                treeLocalCenter.x,
                groundHeight + actualTrunkHeight / 2f,
                treeLocalCenter.z
            );
            trunk.transform.localScale = new Vector3(trunkWidth, actualTrunkHeight, trunkWidth);
            trunk.GetComponent<Renderer>().material = trunkMaterial != null ? trunkMaterial : defaultMaterial;

            // Листя
            if (isConiferous)
            {
                float currentLayerScale = coniferousLeavesLayerScale * individualTreeBaseScale;
                float currentHeightOffset = groundHeight + actualTrunkHeight;
                for (int j = 0; j < coniferousLeavesLayers; j++)
                {
                    GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
                    leaves.transform.SetParent(parent);
                    leaves.transform.localPosition = new Vector3(
                        treeLocalCenter.x,
                        currentHeightOffset + (currentLayerScale * cellSize * 0.8f) / 2f, // Коригований коефіцієнт масштабу
                        treeLocalCenter.z
                    );
                    leaves.transform.localScale = new Vector3(currentLayerScale * cellSize, currentLayerScale * cellSize * 0.8f, currentLayerScale * cellSize);
                    leaves.GetComponent<Renderer>().material = coniferousLeafMaterial != null ? coniferousLeafMaterial : defaultMaterial;

                    currentHeightOffset += currentLayerScale * cellSize * 0.8f; // Переміщуємося вгору
                    currentLayerScale = Mathf.Max(0.01f, currentLayerScale - coniferousLeavesScaleDecreaseFactor * individualTreeBaseScale); // Зменшуємо розмір для наступного шару
                }
            }
            else // Листяне
            {
                GameObject leaves = GameObject.CreatePrimitive(PrimitiveType.Cube);
                leaves.transform.SetParent(parent);
                float randomLeavesOffsetX = Random.Range(-0.1f, 0.1f) * cellSize; // Менше випадкове зміщення для листя
                float randomLeavesOffsetZ = Random.Range(-0.1f, 0.1f) * cellSize;
                leaves.transform.localPosition = new Vector3(
                    treeLocalCenter.x + randomLeavesOffsetX,
                    groundHeight + actualTrunkHeight + (deciduousLeavesBaseScale * individualTreeBaseScale * cellSize * 0.8f) / 2f, // Коригований коефіцієнт масштабу
                    treeLocalCenter.z + randomLeavesOffsetZ
                );
                leaves.transform.localScale = new Vector3(deciduousLeavesBaseScale * individualTreeBaseScale * cellSize, deciduousLeavesBaseScale * individualTreeBaseScale * cellSize * 0.8f, deciduousLeavesBaseScale * individualTreeBaseScale * cellSize); // Застосовуємо individualTreeBaseScale
                leaves.GetComponent<Renderer>().material = leafMaterial != null ? leafMaterial : defaultMaterial;
            }
        }
    }

    private void GenerateBuilding(Transform parent, Vector3 cellCenter, float groundHeight)
    {
        int numBuildings = Random.Range(minBuildingsPerCell, maxBuildingsPerCell + 1);
        for (int i = 0; i < numBuildings; i++)
        {
            float offsetX = Random.Range(-0.4f, 0.4f) * cellSize;
            float offsetZ = Random.Range(-0.4f, 0.4f) * cellSize;
            Vector3 buildingLocalCenter = new Vector3(cellCenter.x + offsetX, groundHeight, cellCenter.z + offsetZ);

            float wallH = Random.Range(individualBuildingMinHeight, individualBuildingMaxHeight);
            float baseS = individualBuildingBaseScale * cellSize;

            // Основа будівлі (стіни) - робимо значно нижче, як ви просили
            GameObject walls = GameObject.CreatePrimitive(PrimitiveType.Cube);
            walls.transform.SetParent(parent);
            walls.transform.localPosition = new Vector3(
                buildingLocalCenter.x,
                groundHeight + wallH / 2f, // Половина висоти стін
                buildingLocalCenter.z
            );
            walls.transform.localScale = new Vector3(baseS, wallH, baseS);
            walls.GetComponent<Renderer>().material = buildingWallMaterial != null ? buildingWallMaterial : defaultMaterial;

            // Дах - Трикутна призма (два похилих куба)
            float roofHeight = individualRoofHeight;
            float roofOverhang = individualRoofOverhang;

            // Ліва частина даху
            GameObject roofLeft = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofLeft.transform.SetParent(parent);
            roofLeft.transform.localScale = new Vector3(baseS + roofOverhang * 2, roofHeight, (baseS + roofOverhang * 2) * 0.55f); // Зменшуємо Z-розмір для створення ската
            roofLeft.transform.localPosition = new Vector3(
                buildingLocalCenter.x,
                groundHeight + wallH + roofHeight * 0.5f,
                buildingLocalCenter.z - (baseS + roofOverhang * 2) * 0.225f // Зміщення по Z для формування даху
            );
            roofLeft.transform.localRotation = Quaternion.Euler(15, 0, 0); // Нахил даху
            roofLeft.GetComponent<Renderer>().material = buildingRoofMaterial != null ? buildingRoofMaterial : defaultMaterial;

            // Права частина даху
            GameObject roofRight = GameObject.CreatePrimitive(PrimitiveType.Cube);
            roofRight.transform.SetParent(parent);
            roofRight.transform.localScale = new Vector3(baseS + roofOverhang * 2, roofHeight, (baseS + roofOverhang * 2) * 0.55f); // Зменшуємо Z-розмір
            roofRight.transform.localPosition = new Vector3(
                buildingLocalCenter.x,
                groundHeight + wallH + roofHeight * 0.5f,
                buildingLocalCenter.z + (baseS + roofOverhang * 2) * 0.225f // Зміщення по Z
            );
            roofRight.transform.localRotation = Quaternion.Euler(-15, 0, 0); // Нахил даху в інший бік
            roofRight.GetComponent<Renderer>().material = buildingRoofMaterial != null ? buildingRoofMaterial : defaultMaterial;
        }
    }

    private void GenerateRoad(Transform parent, Vector3 cellCenter, float groundHeight)
    {
        GameObject roadSegment = GameObject.CreatePrimitive(PrimitiveType.Cube);
        roadSegment.transform.SetParent(parent);
        roadSegment.transform.localPosition = new Vector3(
            cellCenter.x,
            groundHeight + roadHeight / 2f + roadOffsetAboveGround,
            cellCenter.z
        );
        roadSegment.transform.localScale = new Vector3(roadWidth * cellSize, roadHeight, cellSize); // Дорога йде вздовж однієї осі
        roadSegment.GetComponent<Renderer>().material = roadMaterial != null ? roadMaterial : defaultMaterial;
    }

    private void GenerateWater(Transform parent, Vector3 cellCenter, float groundHeight)
    {
        // Для води, ми не генеруємо "землю", а генеруємо саму водну площину з відповідною глибиною.
        // Пісок (дно) буде "під" водою, а не на її рівні.
        GameObject waterPlane = GameObject.CreatePrimitive(PrimitiveType.Cube);
        waterPlane.transform.SetParent(parent);
        waterPlane.transform.localPosition = new Vector3(
            cellCenter.x,
            groundHeight + waterLevelOffset + waterDepth / 2f, // Позиція води
            cellCenter.z
        );
        waterPlane.transform.localScale = new Vector3(cellSize, waterDepth, cellSize);
        waterPlane.GetComponent<Renderer>().material = waterMaterial != null ? waterMaterial : defaultMaterial;

        // Додатково, можна згенерувати "дно" з піску, якщо воно не перетинається з іншою землею.
        // Це робиться за допомогою окремого куба під водою.
        GameObject sandBed = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sandBed.transform.SetParent(parent);
        float sandBedHeight = waterDepth * 0.5f; // Наприклад, половина глибини води для піщаного дна
        sandBed.transform.localPosition = new Vector3(
            cellCenter.x,
            groundHeight + waterLevelOffset - sandBedHeight / 2f, // Позиція дна
            cellCenter.z
        );
        sandBed.transform.localScale = new Vector3(cellSize, sandBedHeight, cellSize);
        sandBed.GetComponent<Renderer>().material = sandMaterial != null ? sandMaterial : defaultMaterial;
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