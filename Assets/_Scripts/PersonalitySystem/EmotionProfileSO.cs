using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, що описує повну емоцію, комбінуючи стани різних рис обличчя.
/// </summary>
[CreateAssetMenu(fileName = "E_", menuName = "Puzzle/Personality/Emotion Profile")]
public class EmotionProfileSO : ScriptableObject
{
    [Header("Налаштування Емоції")]
    public string emotionName = "Нова емоція";

    [Tooltip("Стан очей для цієї емоції (асет EyeStateSO).")]
    public EyeStateSO eyeState;

    [Tooltip("Список станів інших рис обличчя (рот, брови і т.д.). Перетягніть сюди асети FeatureStateSO.")]
    public List<FeatureStateSO> featureStates;
}
