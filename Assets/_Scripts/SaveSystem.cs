using UnityEngine;
using System.Collections.Generic;
using System;

[Serializable]
public class PiecePlacementData
{
    public string pieceTypeName; 
    public Vector2Int origin;
    public PlacedObjectTypeSO.Dir direction;
}

[Serializable]
public class LevelSaveData
{
    public bool isLocked;
    public bool isCompleted;
    public List<PiecePlacementData> onGridPieces = new List<PiecePlacementData>();
    public List<PiecePlacementData> offGridPieces = new List<PiecePlacementData>();
}

public static class SaveSystem
{
    private const string CurrentLevelKey = "CurrentLevelIndex";
    private const string LevelProgressKeyPrefix = "LevelProgress_";

    public static void SaveCurrentLevelIndex(int index)
    {
        PlayerPrefs.SetInt(CurrentLevelKey, index);
        PlayerPrefs.Save();
    }

    public static int LoadCurrentLevelIndex()
    {
        return PlayerPrefs.GetInt(CurrentLevelKey, 0);
    }

    public static void SaveLevelProgress(string id, LevelSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LevelProgressKeyPrefix + id, json);
        PlayerPrefs.Save();
    }

    public static void SaveLevelProgress(int index, LevelSaveData data) => SaveLevelProgress(index.ToString(), data);

    public static LevelSaveData LoadLevelProgress(string id)
    {
        string key = LevelProgressKeyPrefix + id;
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<LevelSaveData>(json);
        }
        return null;
    }

    public static LevelSaveData LoadLevelProgress(int index) => LoadLevelProgress(index.ToString());

    public static void ClearLastCompletedLevel()
    {
        PlayerPrefs.DeleteKey(CurrentLevelKey);
    }

    public static void ClearLevelProgress(string id)
    {
        PlayerPrefs.DeleteKey(LevelProgressKeyPrefix + id);
    }

    public static void ClearLevelProgress(int index) => ClearLevelProgress(index.ToString());
}
