using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, що описує повну емоцію, комбінуючи текстури з різних наборів.
/// Створіть асети через меню: Create -> Puzzle/Personality/Emotion Profile
/// </summary>
[CreateAssetMenu(fileName = "E_", menuName = "Puzzle/Personality/Emotion Profile")]
public class EmotionProfileSO : ScriptableObject
{
    [System.Serializable]
    public struct FeatureExpression
    {
        [Tooltip("Набір текстур, з якого обирається вираз (напр., 'FFS_Eyes_Default').")]
        public FacialFeatureSetSO featureSet;

        [Tooltip("Індекс текстури в списку 'textures' відповідного FacialFeatureSetSO.")]
        public int textureIndex;
    }

    [Header("Налаштування Емоції")]
    [Tooltip("Назва емоції (напр., 'Happy', 'Sad', 'Sleeping').")]
    public string emotionName = "Нова емоція";

    [Tooltip("Список виразів, які разом формують цю емоцію.")]
    public List<FeatureExpression> expressions;
}
