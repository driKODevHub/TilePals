using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "T_", menuName = "Puzzle/Personality/Temperament")]
public class TemperamentSO : ScriptableObject
{
    [Serializable]
    public struct SynergyRule
    {
        [Tooltip("Temperament of the neighbor to react to.")]
        public TemperamentSO neighborTemperament;
        [Tooltip("My reaction to this neighbor.")]
        public EmotionProfileSO myReaction;
        [Tooltip("The neighbor's reaction to me.")]
        public EmotionProfileSO neighborReaction;
        [Tooltip("Reaction duration in seconds.")]
        public float reactionDuration;
    }

    [Header("Basic Information")]
    public string temperamentName = "New Temperament";
    [TextArea] public string description;

    

    [Header("Initial Stats (Range 0 to 1)")]
    [Range(0f, 1f)] public float initialFatigue = 0.1f;
    [Range(0f, 1f)] public float initialIrritation = 0.1f;
    [Range(0f, 1f)] public float initialTrust = 0.5f;

    [Header("Modificators")]
    public float irritationModifier = 1.0f;
    public float fatigueModifier = 1.0f;
    public float trustModifier = 1.0f;

    [Header("Synergy Rules")]
    [Tooltip("List of rules for how this temperament reacts to others.")]
    public List<SynergyRule> synergyRules;

    [Header("Indifferent Settings (Sleepy Cats)")]
    [Tooltip("If true, the cat will be indifferent/sleepy by default, having closed eyes and ignoring low-priority targets.")]
    public bool isIndifferent;
    [Tooltip("The emotion used for the indifferent state (usually closed eyes).")]
    public EmotionProfileSO indifferentEmotion;
}