using UnityEngine;
using System.Collections.Generic;

// Цей клас тепер просто маркер з даними, який ми додаємо до PuzzlePiece, коли він на сітці.
// Він більше не є окремим GameObject.
public class PlacedObject : MonoBehaviour
{
    public PlacedObjectTypeSO PlacedObjectTypeSO { get; set; }
    public Vector2Int Origin { get; set; }
    public PlacedObjectTypeSO.Dir Direction { get; set; }

    public List<Vector2Int> GetGridPositionList()
    {
        return PlacedObjectTypeSO.GetGridPositionsList(Origin, Direction);
    }
}
