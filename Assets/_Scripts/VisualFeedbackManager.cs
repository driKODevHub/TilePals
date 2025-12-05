using UnityEngine;
using UnityEngine.Rendering.Universal; // Потрібно для доступу до URP даних
using System.Collections.Generic;
using System.Reflection;
using System.Linq;

/// <summary>
/// Керує глобальними візуальними ефектами через підміну налаштувань URP Renderer Features.
/// Дозволяє змінювати пресети Outline/Fill глобально, не використовуючи додаткові шари.
/// </summary>
public class VisualFeedbackManager : MonoBehaviour
{
    public static VisualFeedbackManager Instance { get; private set; }

    [System.Serializable]
    public class FeatureSwapConfig
    {
        [Tooltip("Назва фічі. Скрипт спробує знайти фічу, яка МІСТИТЬ цю назву.")]
        public string featureName;

        [Tooltip("Назва поля змінної налаштувань у скрипті фічі. Зазвичай це 'settings' (lowercase).")]
        public string settingsFieldName = "settings";

        [Header("Presets")]
        [Tooltip("Стандартні налаштування (коли можна ставити).")]
        public ScriptableObject normalSettings;

        [Tooltip("Налаштування для помилки (коли не можна ставити).")]
        public ScriptableObject invalidSettings;

        // Внутрішні кешовані дані для швидкого доступу через Reflection
        [HideInInspector] public ScriptableRendererFeature cachedFeature;
        [HideInInspector] public FieldInfo cachedFieldInfo;
    }

    [Header("URP Setup")]
    [Tooltip("Перетягніть сюди файл Universal Renderer Data (напр. PC_Renderer).")]
    [SerializeField] private ScriptableRendererData rendererData;

    [Header("Configuration")]
    [SerializeField] private List<FeatureSwapConfig> featuresToSwap;

    private bool _isCurrentlyInvalid = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        CacheReflectionData();
    }

    private void OnDisable()
    {
        // Завжди повертаємо нормальний стан при вимиканні, щоб не "зламати" едітор
        ResetToNormal();
    }

    private void OnDestroy()
    {
        ResetToNormal();
    }

    // --- НОВА КНОПКА ДЛЯ ПОШУКУ ІМЕН ---
    [ContextMenu("Debug: Print All Feature Names")]
    public void PrintAllFeatureNames()
    {
        if (rendererData == null)
        {
            Debug.LogError("VisualFeedbackManager: Renderer Data is missing! Assign it in Inspector.");
            return;
        }

        Debug.Log("<b>--- AVAILABLE RENDERER FEATURES ---</b>");
        int i = 0;
        foreach (var feature in rendererData.rendererFeatures)
        {
            if (feature != null)
                Debug.Log($"[{i}] Name: '<b>{feature.name}</b>' | Type: {feature.GetType().Name}");
            else
                Debug.Log($"[{i}] NULL FEATURE (Check Renderer Data list for missing scripts)");
            i++;
        }
        Debug.Log("<b>-----------------------------------</b>");
        Debug.Log("Використовуй жирний текст 'Name' для поля Feature Name в інспекторі.");
    }
    // -----------------------------------

    /// <summary>
    /// Знаходить фічі та поля налаштувань через Reflection, щоб не робити це щокадру.
    /// </summary>
    private void CacheReflectionData()
    {
        if (rendererData == null)
        {
            Debug.LogError("VisualFeedbackManager: Renderer Data is missing!");
            return;
        }

        // Для дебагу збережемо всі доступні імена
        List<string> availableNames = rendererData.rendererFeatures
            .Where(f => f != null)
            .Select(f => f.name)
            .ToList();

        foreach (var config in featuresToSwap)
        {
            // 1. Спроба знайти фічу
            ScriptableRendererFeature feature = null;

            // Спочатку шукаємо точний збіг
            feature = rendererData.rendererFeatures.FirstOrDefault(f => f != null && f.name == config.featureName);

            // Якщо не знайшли, шукаємо частковий збіг (наприклад, "Wide Outline" знайде "Full Screen Wide Outline")
            if (feature == null)
            {
                feature = rendererData.rendererFeatures.FirstOrDefault(f => f != null && f.name.Contains(config.featureName));
            }

            if (feature == null)
            {
                Debug.LogWarning($"VisualFeedbackManager: Could not find Feature with name containing '{config.featureName}'.\nAvailable features: {string.Join(", ", availableNames)}");
                continue;
            }

            config.cachedFeature = feature;

            // 2. Знаходимо поле налаштувань (settings) у класі цієї фічі
            var type = feature.GetType();

            // Шукаємо 'settings', 'Settings', 'currentSettings', '_settings'
            FieldInfo field = type.GetField(config.settingsFieldName, BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) field = type.GetField("Settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            if (field == null) field = type.GetField("m_Settings", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);

            if (field == null)
            {
                Debug.LogError($"VisualFeedbackManager: Feature found ('{feature.name}'), but could not find Settings field '{config.settingsFieldName}'. Check the script source code for the variable name.");
                continue;
            }

            config.cachedFieldInfo = field;
        }
    }

    /// <summary>
    /// Встановлює глобальний стан візуалізації (Валідне / Невалідне).
    /// </summary>
    public void SetInvalidState(bool isInvalid)
    {
        // Оптимізація: не робимо нічого, якщо стан не змінився
        if (_isCurrentlyInvalid == isInvalid) return;

        _isCurrentlyInvalid = isInvalid;
        ApplySettings(isInvalid);
    }

    private void ResetToNormal()
    {
        if (_isCurrentlyInvalid)
        {
            ApplySettings(false); // Force normal
            _isCurrentlyInvalid = false;
        }
    }

    private void ApplySettings(bool useInvalid)
    {
        foreach (var config in featuresToSwap)
        {
            if (config.cachedFeature == null || config.cachedFieldInfo == null) continue;

            ScriptableObject targetSettings = useInvalid ? config.invalidSettings : config.normalSettings;

            if (targetSettings != null)
            {
                try
                {
                    // "Магічна" підміна налаштувань
                    config.cachedFieldInfo.SetValue(config.cachedFeature, targetSettings);

                    // Повідомляємо фічі, що вона змінилася (де-які фічі потребують переініціалізації)
                    config.cachedFeature.SetActive(config.cachedFeature.isActive);
                }
                catch (System.Exception e)
                {
                    Debug.LogError($"Error swapping settings for {config.featureName}: {e.Message}");
                }
            }
        }

        // Примушуємо Unity перерендерити вигляд (інколи потрібно для Editor)
#if UNITY_EDITOR
        UnityEditorInternal.InternalEditorUtility.RepaintAllViews();
#endif
    }
}