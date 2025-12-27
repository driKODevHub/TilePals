// PlacedObjectTypeSO.cs
using System.Collections.Generic;
using UnityEngine;
using System;

[CreateAssetMenu(fileName = "PlacedObjectType", menuName = "GridBuildingSystem/PlacedObjectType", order = 1)]
public class PlacedObjectTypeSO : ScriptableObject
{
    // --- ENUMS ---
    public enum ItemCategory
    {
        PuzzleShape, // Фігури (основний геймплей)
        Prop,       
        Toy,        
        Food,       
        Tool        // Інструменти (зазвичай для UnlockGrid)
    }

    public enum UsageType
    {
        None,
        HoldInMouth,    
        AttractAttention, 
        UnlockGrid,     
        Bounce          
    }

    // --- Static Helpers ---
    public static Dir GetNextDir(Dir dir)
    {
        return dir switch
        {
            Dir.Down => Dir.Left,
            Dir.Left => Dir.Up,
            Dir.Up => Dir.Right,
            Dir.Right => Dir.Down,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }

    public static Dir GetPreviousDir(Dir dir)
    {
        return dir switch
        {
            Dir.Down => Dir.Right,
            Dir.Right => Dir.Up,
            Dir.Up => Dir.Left,
            Dir.Left => Dir.Down,
            _ => throw new ArgumentOutOfRangeException(nameof(dir), dir, null)
        };
    }

    public enum Dir
    {
        Down,
        Left,
        Up,
        Right
    }

    [Header("Basic Info")]
    public string objectName;
    public Transform prefab;
    public Transform visual;
    public List<Vector2Int> relativeOccupiedCells;

    [Header("Item Settings")]
    public ItemCategory category = ItemCategory.PuzzleShape;
    public UsageType usageType = UsageType.None;

    [Header("Physics Settings (For Non-PuzzleShapes)")]
    public bool usePhysics = false;
    public float mass = 1.0f;
    public float bounciness = 0.5f;
    public float throwForceMultiplier = 1.0f;

    [Header("Grid Expansion (For Tools)")]
    public int unlockRadius = 1;

    public Vector2Int size => GetMaxDimensions();

    public Vector2Int GetMaxDimensions()
    {
        int maxX = 0;
        int maxY = 0;
        if (relativeOccupiedCells == null || relativeOccupiedCells.Count == 0)
        {
            return Vector2Int.zero;
        }

        foreach (Vector2Int cell in relativeOccupiedCells)
        {
            if (cell.x > maxX) maxX = cell.x;
            if (cell.y > maxY) maxY = cell.y;
        }
        return new Vector2Int(maxX + 1, maxY + 1);
    }

    public Vector3 GetBoundsCenterOffset(Dir direction)
    {
        Vector2Int dims = GetMaxDimensions();
        float width = dims.x;
        float height = dims.y;

        return direction switch
        {
            Dir.Down or Dir.Up => new Vector3(width / 2f, 0, height / 2f),
            Dir.Left or Dir.Right => new Vector3(height / 2f, 0, width / 2f),
            _ => Vector3.zero,
        };
    }

    public int GetRotationAngle(Dir direction)
    {
        return direction switch
        {
            Dir.Down => 0,
            Dir.Left => 90,
            Dir.Up => 180,
            Dir.Right => 270,
            _ => throw new ArgumentOutOfRangeException(nameof(direction), direction, null)
        };
    }

    public Vector2Int GetRotationOffset(Dir direction)
    {
        Vector2Int dims = GetMaxDimensions();
        int width = dims.x;
        int height = dims.y;

        switch (direction)
        {
            case Dir.Down:
                return new Vector2Int(0, 0);
            case Dir.Left:
                return new Vector2Int(0, width);
            case Dir.Up:
                return new Vector2Int(width, height);
            case Dir.Right:
                return new Vector2Int(height, 0);
            default:
                throw new ArgumentOutOfRangeException(nameof(direction), direction, null);
        }
    }

    public List<Vector2Int> GetGridPositionsList(Vector2Int offset, Dir direction)
    {
        List<Vector2Int> gridPositionList = new();

        Vector2Int originalDims = GetMaxDimensions();
        int originalWidth = originalDims.x;
        int originalHeight = originalDims.y;

        foreach (Vector2Int cell in relativeOccupiedCells)
        {
            Vector2Int rotatedCell = cell;
            switch (direction)
            {
                case Dir.Down:
                    break;
                case Dir.Left:
                    rotatedCell = new Vector2Int(cell.y, originalWidth - 1 - cell.x);
                    break;
                case Dir.Up:
                    rotatedCell = new Vector2Int(originalWidth - 1 - cell.x, originalHeight - 1 - cell.y);
                    break;
                case Dir.Right:
                    rotatedCell = new Vector2Int(originalHeight - 1 - cell.y, cell.x);
                    break;
            }
            gridPositionList.Add(offset + rotatedCell);
        }
        return gridPositionList;
    }
}
