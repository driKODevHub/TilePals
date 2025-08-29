using UnityEngine;

/// <summary>
/// Універсальний ScriptableObject для опису стану однієї риси обличчя (рот, брови і т.д.).
/// Тепер використовує спрайти замість текстур.
/// </summary>
[CreateAssetMenu(fileName = "FeatureState_", menuName = "Puzzle/Personality/Feature State")]
public class FeatureStateSO : ScriptableObject
{
    public enum FeatureType
    {
        Mouth,
        Brows,
        Blush
    }

    [Tooltip("Тип риси, яку описує цей стан (напр., Рот).")]
    public FeatureType feature;

    [Tooltip("Спрайт для цього конкретного виразу (напр., спрайт усмішки).")]
    public Sprite expressionSprite;
}
