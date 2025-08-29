using UnityEngine;

/// <summary>
/// ScriptableObject, що описує один конкретний стан очей (напр., "Щасливі", "Сонні").
/// Тепер використовує спрайти та підтримує маску для зіниці.
/// </summary>
[CreateAssetMenu(fileName = "EyeState_", menuName = "Puzzle/Personality/Eye State")]
public class EyeStateSO : ScriptableObject
{
    [Tooltip("Спрайт для форми ока (білок, повіки).")]
    public Sprite eyeShapeSprite;

    [Tooltip("Спрайт для зіниці.")]
    public Sprite pupilSprite;

    [Tooltip("Спрайт, що буде використовуватися як маска для зіниці. Зазвичай такий самий, як і форма ока.")]
    public Sprite eyeMaskSprite;
}
