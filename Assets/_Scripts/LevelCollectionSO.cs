using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LevelCollection", menuName = "Puzzle/Level Collection")]
public class LevelCollectionSO : ScriptableObject
{
    [Header("General Info")]
    public string locationName = "New Location";

    [Tooltip("The list of puzzle steps (GridDataSO) in this location.")]
    public List<GridDataSO> levels;
}
