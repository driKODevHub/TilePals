using UnityEngine;
using System.Collections.Generic;

/// <summary>
/// ScriptableObject, що зберігає набір текстур для однієї риси обличчя (очі, рот).
/// Створіть асети через меню: Create -> Puzzle/Personality/Facial Feature Set
/// </summary>
[CreateAssetMenu(fileName = "FFS_", menuName = "Puzzle/Personality/Facial Feature Set")]
public class FacialFeatureSetSO : ScriptableObject
{
    public enum FeatureType
    {
        Eyes,
        Mouth,
        Brows,
        Blush // Наприклад, рум'янець
    }

    [Header("Тип Риси Обличчя")]
    public FeatureType feature;

    [Header("Список Текстур")]
    [Tooltip("Список можливих текстур для цієї риси обличчя.")]
    public List<Texture2D> textures;
}
