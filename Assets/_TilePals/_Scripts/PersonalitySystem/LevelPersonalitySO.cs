using UnityEngine;
using System.Collections.Generic;
using System;

/// <summary>
/// ScriptableObject, що зберігає налаштування характерів для одного конкретного рівня.
/// </summary>
[CreateAssetMenu(fileName = "Level_Personality", menuName = "Puzzle/Personality/Level Personality Map")]
public class LevelPersonalitySO : ScriptableObject
{
    [Serializable]
    public struct PersonalityMapping
    {
        public PlacedObjectTypeSO pieceType;
        public TemperamentSO temperament;
    }

    public List<PersonalityMapping> personalityMappings = new List<PersonalityMapping>();
}
