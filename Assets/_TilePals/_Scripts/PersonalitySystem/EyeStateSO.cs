using UnityEngine;

/// <summary>
/// ScriptableObject, що описує один конкретний стан очей (напр., "Щасливі", "Сонні").
/// Містить текстури для форми ока та зіниці.
/// </summary>
[CreateAssetMenu(fileName = "EyeState_", menuName = "Puzzle/Personality/Eye State")]
public class EyeStateSO : ScriptableObject
{
    [Tooltip("Текстура для форми ока (білок, повіки).")]
    public Texture2D eyeShapeTexture;

    [Tooltip("Текстура для зіниці.")]
    public Texture2D pupilTexture;
}
