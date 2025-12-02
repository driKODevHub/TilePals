using UnityEngine;

/// <summary>
/// Заглушка для майбутньої системи 3D емоцій.
/// Поки що використовується для збереження сумісності з EmotionProfileSO.
/// </summary>
[CreateAssetMenu(fileName = "EyeState_", menuName = "Puzzle/Personality/Eye State")]
public class EyeStateSO : ScriptableObject
{
    [Header("3D Emotion Settings")]
    [Tooltip("Наприклад, ім'я blendshape для очей або посилання на меш.")]
    public string shapeName;

    // Тут пізніше будуть додаткові параметри для емоцій (Blendshapes, Material offsets тощо)
}