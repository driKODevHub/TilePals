using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "T_", menuName = "Puzzle/Personality/Temperament")]
public class TemperamentSO : ScriptableObject
{
    [Serializable]
    public struct SynergyRule
    {
        [Tooltip("Темперамент сусіда, на який буде реакція.")]
        public TemperamentSO neighborTemperament;
        [Tooltip("Емоція, яку покажу Я при зустрічі з цим сусідом.")]
        public EmotionProfileSO myReaction;
        [Tooltip("Емоція, яку я попрошу показати сусіда у відповідь.")]
        public EmotionProfileSO neighborReaction;
        [Tooltip("Час (в секундах), протягом якого тримається ця реакція.")]
        public float reactionDuration;
    }

    [Header("Основна інформація")]
    public string temperamentName = "Новий темперамент";
    [TextArea] public string description;

    [Header("Візуальне відображення")]
    // --- ЗМІНЕНО: з Color на Material ---
    [Tooltip("Матеріал, який буде призначено фігурі з цим темпераментом.")]
    public Material temperamentMaterial;

    [Header("Початкові Внутрішні Параметри (від 0 до 1)")]
    [Range(0f, 1f)] public float initialFatigue = 0.1f;
    [Range(0f, 1f)] public float initialIrritation = 0.1f;
    [Range(0f, 1f)] public float initialTrust = 0.5f;

    [Header("Модифікатори Реакцій")]
    public float irritationModifier = 1.0f;
    public float fatigueModifier = 1.0f;
    public float trustModifier = 1.0f;

    [Header("Правила Взаємодії з Сусідами")]
    [Tooltip("Список правил, як цей темперамент реагує на сусідство з іншими.")]
    public List<SynergyRule> synergyRules;
}
