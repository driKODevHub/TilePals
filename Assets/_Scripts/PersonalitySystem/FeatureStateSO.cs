using UnityEngine;

/// <summary>
/// Окремий візуальний стан для частини обличчя (наприклад, "Рот сміється" або "Очі заплющені").
/// Вибирається через глобальний Enum CatFeatureType.
/// </summary>
[CreateAssetMenu(fileName = "FeatureState_", menuName = "Puzzle/Personality/Feature State")]
public class FeatureStateSO : ScriptableObject
{
    [Tooltip("Виберіть тип частини обличчя з глобального списку.")]
    public CatFeatureType featureType;
}
