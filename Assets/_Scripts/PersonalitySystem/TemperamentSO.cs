using UnityEngine;
using System.Collections.Generic;
using System;

[CreateAssetMenu(fileName = "T_", menuName = "Puzzle/Personality/Temperament")]
public class TemperamentSO : ScriptableObject
{
    [Serializable]
    public struct SynergyRule
    {
        [Tooltip("����������� �����, �� ���� ���� �������.")]
        public TemperamentSO neighborTemperament;
        [Tooltip("������, ��� ������ � ��� ������� � ��� ������.")]
        public EmotionProfileSO myReaction;
        [Tooltip("������, ��� � ������� �������� ����� � �������.")]
        public EmotionProfileSO neighborReaction;
        [Tooltip("��� (� ��������), �������� ����� ��������� �� �������.")]
        public float reactionDuration;
    }

    [Header("������� ����������")]
    public string temperamentName = "����� �����������";
    [TextArea] public string description;

    [Header("³������� �����������")]
    // --- �̲����: � Color �� Material ---
    [Tooltip("�������, ���� ���� ���������� ���� � ��� �������������.")]
    public Material temperamentMaterial;

    [Header("�������� ������� ��������� (�� 0 �� 1)")]
    [Range(0f, 1f)] public float initialFatigue = 0.1f;
    [Range(0f, 1f)] public float initialIrritation = 0.1f;
    [Range(0f, 1f)] public float initialTrust = 0.5f;

    [Header("����������� �������")]
    public float irritationModifier = 1.0f;
    public float fatigueModifier = 1.0f;
    public float trustModifier = 1.0f;

    [Header("������� �����䳿 � �������")]
    [Tooltip("������ ������, �� ��� ����������� ����� �� �������� � ������.")]
    public List<SynergyRule> synergyRules;

    [Header("Indifferent Settings (Sleepy Cats)")]
    [Tooltip("If true, the cat will be indifferent/sleepy by default, having closed eyes and ignoring low-priority targets.")]
    public bool isIndifferent;
    [Tooltip("The emotion used for the indifferent state (usually closed eyes).")]
    public EmotionProfileSO indifferentEmotion;
}

