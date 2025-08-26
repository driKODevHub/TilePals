using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, що описує повну емоцію.
/// Тепер використовує прямі посилання на стани рис обличчя.
/// </summary>
[CreateAssetMenu(fileName = "E_", menuName = "Puzzle/Personality/Emotion Profile")]
public class EmotionProfileSO : ScriptableObject
{
    [System.Serializable]
    public struct FeatureExpression
    {
        [Tooltip("Набір текстур, з якого обирається вираз (напр., 'FFS_Mouth_Default').")]
        public FacialFeatureSetSO featureSet;

        [Tooltip("Індекс текстури в списку 'textures' відповідного FacialFeatureSetSO.")]
        public int textureIndex;
    }

    [Header("Налаштування Емоції")]
    public string emotionName = "Нова емоція";

    [Tooltip("Стан очей для цієї емоції.")]
    public EyeStateSO eyeState;

    [Tooltip("Інші риси обличчя (рот, брови) для цієї емоції.")]
    public List<FeatureExpression> otherExpressions;
}
