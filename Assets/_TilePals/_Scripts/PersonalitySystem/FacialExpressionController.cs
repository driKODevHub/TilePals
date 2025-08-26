using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// Керує візуальним відображенням емоцій на обличчі фігури.
/// Цей компонент має бути розміщений на GameObject'і, що є батьківським для всіх частин обличчя.
/// </summary>
public class FacialExpressionController : MonoBehaviour
{
    /// <summary>
    /// Зв'язує тип риси обличчя (напр., Очі) з конкретним MeshRenderer'ом на префабі.
    /// </summary>
    [System.Serializable]
    public struct FacialFeatureRenderer
    {
        [Tooltip("Тип риси, за яку відповідає цей рендерер.")]
        public FacialFeatureSetSO.FeatureType featureType;

        [Tooltip("MeshRenderer (наприклад, з Quad'а), на якому буде відображатися текстура.")]
        public MeshRenderer featureRenderer;
    }

    [Header("Налаштування Рис Обличчя")]
    [Tooltip("Список усіх рендерерів, що складають обличчя цієї фігури.")]
    [SerializeField] private List<FacialFeatureRenderer> featureRenderers;

    // Словник для швидкого доступу до рендерерів за їхнім типом.
    private Dictionary<FacialFeatureSetSO.FeatureType, MeshRenderer> _rendererMap;

    private void Awake()
    {
        // Ініціалізуємо словник для швидкого пошуку.
        // Це робиться один раз при старті для оптимізації.
        _rendererMap = new Dictionary<FacialFeatureSetSO.FeatureType, MeshRenderer>();
        foreach (var feature in featureRenderers)
        {
            if (feature.featureRenderer != null)
            {
                _rendererMap[feature.featureType] = feature.featureRenderer;
            }
        }
    }

    /// <summary>
    /// Основний метод, що застосовує емоцію до обличчя.
    /// Він отримує профіль емоції та оновлює текстури на відповідних MeshRenderer'ах.
    /// </summary>
    /// <param name="emotionProfile">ScriptableObject з даними про емоцію, яку потрібно відобразити.</param>
    public void ApplyEmotion(EmotionProfileSO emotionProfile)
    {
        if (emotionProfile == null)
        {
            // Debug.LogWarning("Спроба застосувати порожній EmotionProfile. Обличчя буде приховано.", this);
            // Приховуємо всі риси, якщо профіль порожній
            foreach (var rendererPair in _rendererMap)
            {
                rendererPair.Value.enabled = false;
            }
            return;
        }

        // Спочатку вимикаємо всі рендерери, щоб приховати риси, не задіяні в новій емоції.
        foreach (var rendererPair in _rendererMap)
        {
            rendererPair.Value.enabled = false;
        }

        // Проходимо по кожному виразу в профілі емоції.
        foreach (var expression in emotionProfile.expressions)
        {
            if (expression.featureSet == null) continue;

            // Знаходимо потрібний рендерер у нашому словнику.
            if (_rendererMap.TryGetValue(expression.featureSet.feature, out MeshRenderer renderer))
            {
                // Перевіряємо, чи валідний індекс текстури.
                if (expression.textureIndex >= 0 && expression.textureIndex < expression.featureSet.textures.Count)
                {
                    Texture2D texture = expression.featureSet.textures[expression.textureIndex];
                    if (texture != null)
                    {
                        // Вмикаємо рендерер і встановлюємо нову текстуру.
                        renderer.enabled = true;
                        renderer.material.mainTexture = texture;
                    }
                    else
                    {
                        // Якщо текстура не призначена, вимикаємо рендерер.
                        renderer.enabled = false;
                    }
                }
                else
                {
                    Debug.LogWarning($"Індекс текстури {expression.textureIndex} виходить за межі для {expression.featureSet.name}", this);
                }
            }
        }
    }
}
