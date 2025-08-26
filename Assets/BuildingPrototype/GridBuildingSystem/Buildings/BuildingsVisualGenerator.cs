using System.Collections.Generic;
using UnityEngine;
using UnityEditor; // Потрібно для EditorUtility.SetDirty та генерації з кнопки в редакторі

public class BuildingsVisualGenerator : MonoBehaviour
{
    /// <summary>
    /// ScriptableObject, що визначає форму та властивості об'єкта, для якого генерується візуал.
    /// Цей об'єкт все ще потрібен, щоб стратегія знала, яку область генерувати.
    /// </summary>
    public PlacedObjectTypeSO placedObjectTypeSO;

    /// <summary>
    /// Основна стратегія генерації візуалу.
    /// ЦЕ ПОЛЕ НЕ ПОВИННО ВИКОРИСТОВУВАТИ [SerializeReference], оскільки IVisualGeneratorStrategy
    /// реалізується ScriptableObject'ами.
    /// </summary>
    [SerializeField]
    private IVisualGeneratorStrategy _generationStrategy;

    /// <summary>
    /// Публічна властивість для доступу та встановлення стратегії з інших скриптів.
    /// При зміні стратегії автоматично запускається перегенерація візуалу.
    /// </summary>
    public IVisualGeneratorStrategy GenerationStrategy
    {
        get => _generationStrategy;
        set
        {
            if (value == null)
            {
                // Повідомлення про помилку, але НЕ ЗМІНЮЄМО стратегію, якщо вона null,
                // щоб уникнути втрати призначеного об'єкта.
                Debug.LogError("BuildingsVisualGenerator: Attempting to set a null generation strategy. Strategy will not be changed.");
                return;
            }
            if (_generationStrategy != value) // Оновлюємо лише якщо стратегія дійсно інша
            {
                _generationStrategy = value;
                Debug.Log($"BuildingsVisualGenerator: Generation strategy set to '{(_generationStrategy as ScriptableObject)?.name ?? "Unknown Strategy"}'.");
                // НЕ викликаємо GenerateGridVisual тут.
                // Генерація буде викликатися вручну з інспектора або коли це дійсно потрібно.
            }
        }
    }

    // --- ЦІ ПОЛЯ БІЛЬШЕ НЕ ПОТРІБНІ В ЦЬОМУ КЛАСІ, ЇХНЯ ЛОГІКА ПЕРЕНЕСЕНА В СТРАТЕГІЇ ---
    // public Material[] randomMaterials;
    // public float overallOuterPadding = 0.1f;
    // public float cubeSize = 1f;
    // ---------------------------------------------------------------------------------

    [Tooltip("Шар, який буде призначений згенерованим GameObjects візуалу.")]
    public int generatedVisualLayer = 10; // Шар за замовчуванням, можна налаштувати

    /// <summary>
    /// Генерує візуал сітки на основі вибраної стратегії генерації.
    /// </summary>
    public void GenerateGridVisual()
    {
        // Очищаємо попередній візуал перед генерацією нового
        ClearGeneratedVisual();

        if (placedObjectTypeSO == null)
        {
            Debug.LogError("BuildingsVisualGenerator: placedObjectTypeSO is not assigned. Cannot generate visual.");
            return;
        }

        if (_generationStrategy == null)
        {
            Debug.LogError("BuildingsVisualGenerator: No generation strategy assigned. Please assign one in the Inspector.");
            return;
        }

        // Повністю делегуємо генерацію візуалу обраній стратегії.
        // Стратегія поверне GameObject, який потрібно буде зробити дочірнім.
        GameObject generatedVisual = _generationStrategy.GenerateVisual(transform, placedObjectTypeSO);

        if (generatedVisual != null)
        {
            generatedVisual.name = "GeneratedGridVisual";
            // Стратегія відповідає за позиціонування та масштабування свого візуалу відносно
            // батьківського transform, який ми передали.
            // Стара логіка з 'overallOuterPadding' або 'cubeSize' тут більше не потрібна.

            // Встановлюємо шар для всієї ієрархії згенерованого візуалу
            SetLayerRecursive(generatedVisual, generatedVisualLayer);
        }
    }

    /// <summary>
    /// Очищає всі згенеровані дочірні об'єкти з іменем "GeneratedGridVisual".
    /// </summary>
    public void ClearGeneratedVisual()
    {
        List<GameObject> childrenToDestroy = new List<GameObject>();
        for (int i = 0; i < transform.childCount; i++)
        {
            GameObject child = transform.GetChild(i).gameObject;
            if (child.name == "GeneratedGridVisual")
            {
                childrenToDestroy.Add(child);
            }
        }

        foreach (GameObject child in childrenToDestroy)
        {
            // Використовуємо DestroyImmediate в режимі редактора для запобігання помилок при збереженні асетів.
            // Використовуємо Destroy під час виконання.
            if (Application.isEditor)
            {
                DestroyImmediate(child);
            }
            else
            {
                Destroy(child);
            }
        }

        // Позначаємо об'єкт як змінений, якщо зміни були внесені в редаторі.
#if UNITY_EDITOR
        EditorUtility.SetDirty(this);
#endif
    }

    // Допоміжний метод для рекурсивного встановлення шару GameObject та його дочірніх елементів.
    private void SetLayerRecursive(GameObject targetGameObject, int layer)
    {
        targetGameObject.layer = layer;
        foreach (Transform child in targetGameObject.transform)
        {
            SetLayerRecursive(child.gameObject, layer);
        }
    }
}

#if UNITY_EDITOR
[CustomEditor(typeof(BuildingsVisualGenerator))]
public class GridVisualGeneratorEditor : Editor
{
    // Поле для зберігання обраної стратегії в редакторі, але без автоматичного присвоєння
    private IVisualGeneratorStrategy _selectedStrategyInEditor;

    private void OnEnable()
    {
        BuildingsVisualGenerator generator = (BuildingsVisualGenerator)target;
        // Ініціалізуємо _selectedStrategyInEditor поточною стратегією генератора при включенні інспектора
        _selectedStrategyInEditor = generator.GenerationStrategy;
    }

    public override void OnInspectorGUI()
    {
        // Малюємо стандартні поля інспектора (включаючи placedObjectTypeSO)
        DrawDefaultInspector();

        BuildingsVisualGenerator generator = (BuildingsVisualGenerator)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Налаштування Стратегії Генерації:", EditorStyles.boldLabel);

        // --- Замінюємо ObjectField на вибір за допомогою кнопок ---

        // Знайдемо всі ScriptableObject, які реалізують IVisualGeneratorStrategy
        // (Це може бути трохи ресурсомістким, якщо у вас дуже багато ScriptableObject'ів,
        // але для розумної кількості це нормально)
        // Для більш оптимізованого підходу можна кешувати ці стратегії.
        string[] guids = AssetDatabase.FindAssets("t:ScriptableObject");
        List<IVisualGeneratorStrategy> availableStrategies = new List<IVisualGeneratorStrategy>();

        foreach (string guid in guids)
        {
            string assetPath = AssetDatabase.GUIDToAssetPath(guid);
            ScriptableObject so = AssetDatabase.LoadAssetAtPath<ScriptableObject>(assetPath);
            if (so is IVisualGeneratorStrategy strategy)
            {
                availableStrategies.Add(strategy);
            }
        }

        // Кнопки для кожної доступної стратегії
        foreach (IVisualGeneratorStrategy strategy in availableStrategies)
        {
            bool isCurrent = (generator.GenerationStrategy == strategy);
            GUI.enabled = !isCurrent; // Вимикаємо кнопку, якщо це вже поточна стратегія

            // Використовуємо Tooltip для підказки про те, яка стратегія зараз активна
            string buttonText = strategy.ToString();
            if (isCurrent)
            {
                buttonText += " (Активна)";
            }

            if (GUILayout.Button(buttonText))
            {
                // Встановлюємо стратегію через властивість
                generator.GenerationStrategy = strategy;
                // Позначаємо об'єкт як змінений, щоб зміни збереглися
                EditorUtility.SetDirty(generator);
            }
            GUI.enabled = true; // Знову вмикаємо кнопки
        }

        // Кнопка для очищення стратегії (встановлення на null)
        EditorGUILayout.Space();
        if (generator.GenerationStrategy != null)
        {
            if (GUILayout.Button("Очистити Стратегію (Встановити на None)"))
            {
                generator.GenerationStrategy = null; // Встановлюємо null
                EditorUtility.SetDirty(generator);
            }
        }


        EditorGUILayout.Space();

        // Ці кнопки залишаються окремими для ручної генерації/очищення
        if (GUILayout.Button("Згенерувати Візуал Сітки"))
        {
            generator.GenerateGridVisual();
        }

        if (GUILayout.Button("Очистити Згенерований Візуал"))
        {
            generator.ClearGeneratedVisual();
        }

        // Це важливо: якщо ви зробили зміни в інспекторі, але не через DrawDefaultInspector,
        // потрібно позначити об'єкт як "dirty", щоб Unity його зберіг.
        if (GUI.changed)
        {
            EditorUtility.SetDirty(generator);
        }
    }
}
#endif