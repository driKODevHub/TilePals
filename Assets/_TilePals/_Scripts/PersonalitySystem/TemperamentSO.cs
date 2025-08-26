using UnityEngine;

/// <summary>
/// ScriptableObject, що описує базовий характер (темперамент) фігури.
/// Визначає початкові параметри та реакції на події.
/// Створіть асети через меню: Create -> Puzzle/Personality/Temperament
/// </summary>
[CreateAssetMenu(fileName = "T_", menuName = "Puzzle/Personality/Temperament")]
public class TemperamentSO : ScriptableObject
{
    [Header("Основна інформація")]
    [Tooltip("Назва темпераменту, що відображається в редакторі.")]
    public string temperamentName = "Новий темперамент";

    [TextArea]
    [Tooltip("Короткий опис характеру для зручності дизайнера.")]
    public string description;

    [Header("Початкові Внутрішні Параметри (від 0 до 1)")]
    [Range(0f, 1f)]
    [Tooltip("Наскільки фігура втомлена на старті.")]
    public float initialFatigue = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Наскільки фігура роздратована на старті.")]
    public float initialIrritation = 0.1f;

    [Range(0f, 1f)]
    [Tooltip("Наскільки фігура довіряє гравцю на старті.")]
    public float initialTrust = 0.5f;

    [Header("Модифікатори Реакцій")]
    [Tooltip("Множник зміни роздратування. >1 - дратується швидше, <1 - повільніше.")]
    public float irritationModifier = 1.0f;

    [Tooltip("Множник зміни втоми. >1 - втомлюється швидше.")]
    public float fatigueModifier = 1.0f;

    [Tooltip("Множник зміни довіри. >1 - довіра зростає/падає швидше.")]
    public float trustModifier = 1.0f;
}
