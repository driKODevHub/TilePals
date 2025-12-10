using UnityEngine;
using System;
using System.Collections.Generic;

/// <summary>
/// Відповідає за логіку використання предметів (Tools, Toys, Food).
/// </summary>
public class ItemUsageManager : MonoBehaviour
{
    public static ItemUsageManager Instance { get; private set; }

    public event Action<PuzzlePiece> OnItemUsed;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    /// <summary>
    /// Спроба використати предмет. Повертає true, якщо використання успішне.
    /// </summary>
    public bool TryUseItem(PuzzlePiece item)
    {
        if (item == null || item.PieceTypeSO == null) return false;

        bool used = false;

        switch (item.PieceTypeSO.usageType)
        {
            case PlacedObjectTypeSO.UsageType.UnlockGrid:
                used = ApplyUnlockTool(item);
                break;

            case PlacedObjectTypeSO.UsageType.AttractAttention:
            case PlacedObjectTypeSO.UsageType.HoldInMouth:
                // Ці предмети працюють пасивно або через PersonalityEventManager
                // Але можна додати специфічний ефект при кліку "Use"
                PersonalityEventManager.RaisePettingStart(item); // Наприклад, привернути увагу
                used = true;
                break;

            case PlacedObjectTypeSO.UsageType.Bounce:
                // Просто фізичний об'єкт, специфічної дії "Use" немає,
                // але можна додати звук squeak
                used = true;
                break;
        }

        if (used)
        {
            OnItemUsed?.Invoke(item);

            // Якщо це одноразовий інструмент, його можна знищити (опціонально)
            // Destroy(item.gameObject); 
        }

        return used;
    }

    private bool ApplyUnlockTool(PuzzlePiece tool)
    {
        // Логіка: Розблокувати клітинки навколо інструменту
        var grid = GridBuildingSystem.Instance.GetGrid();
        Vector2Int center = tool.IsPlaced ? tool.PlacedObjectComponent.Origin : tool.OffGridOrigin;

        int radius = tool.PieceTypeSO.unlockRadius;
        bool anyUnlocked = false;

        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                int checkX = center.x + x;
                int checkY = center.y + z;

                if (GridBuildingSystem.Instance.IsValidGridPosition(checkX, checkY))
                {
                    GridObject obj = grid.GetGridObject(checkX, checkY);
                    if (!obj.IsBuildable())
                    {
                        obj.SetBuildable(true);
                        anyUnlocked = true;

                        // Тут можна додати візуальний ефект розблокування (партикали)
                    }
                }
            }
        }

        if (anyUnlocked)
        {
            Debug.Log($"Tool used! Grid expanded around {center}.");
            return true;
        }

        return false;
    }
}