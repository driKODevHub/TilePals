using UnityEngine;
using System.Collections.Generic;

[CreateAssetMenu(fileName = "LevelCollection", menuName = "Puzzle/Level Collection")]
public class LevelCollectionSO : ScriptableObject
{
    [Tooltip("ѕерет€гн≥ть сюди вс≥ ваш≥ р≥вн≥ (GridDataSO) у тому пор€дку, в €кому вони мають з'€вл€тис€ в гр≥.")]
    public List<GridDataSO> levels;
}
