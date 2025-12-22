using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, який визначає профіль емоції.
/// Зв'язує назву емоції з набором візуальних станів (CatFeatureType).
/// </summary>
[CreateAssetMenu(fileName = "E_", menuName = "Puzzle/Personality/Emotion Profile")]
public class EmotionProfileSO : ScriptableObject
{
    [Header("Візуальні налаштування")]
    public string emotionName = "Назва емоції";

    [Tooltip("Список частин обличчя, які активуються для цієї емоції.")]
    public List<FeatureStateSO> featureStates;
}
