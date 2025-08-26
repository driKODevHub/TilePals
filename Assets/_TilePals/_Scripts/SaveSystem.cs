using UnityEngine;
using System.Collections.Generic;
using System;

// Структури даних для збереження стану рівня
[Serializable]
public class PiecePlacementData
{
    public string pieceTypeName; // Зберігаємо ім'я ScriptableObject'а фігури
    public Vector2Int origin;
    public PlacedObjectTypeSO.Dir direction;
}

[Serializable]
public class LevelSaveData
{
    public List<PiecePlacementData> onGridPieces = new List<PiecePlacementData>();
    public List<PiecePlacementData> offGridPieces = new List<PiecePlacementData>();
}

public static class SaveSystem
{
    private const string CurrentLevelKey = "CurrentLevelIndex";
    private const string LevelProgressKeyPrefix = "LevelProgress_";

    // Збереження/завантаження індексу поточного рівня
    public static void SaveCurrentLevelIndex(int index)
    {
        PlayerPrefs.SetInt(CurrentLevelKey, index);
        PlayerPrefs.Save();
    }

    public static int LoadCurrentLevelIndex()
    {
        return PlayerPrefs.GetInt(CurrentLevelKey, 0);
    }

    // Збереження/завантаження прогресу конкретного рівня
    public static void SaveLevelProgress(int levelIndex, LevelSaveData data)
    {
        string json = JsonUtility.ToJson(data);
        PlayerPrefs.SetString(LevelProgressKeyPrefix + levelIndex, json);
        PlayerPrefs.Save();
    }

    public static LevelSaveData LoadLevelProgress(int levelIndex)
    {
        string key = LevelProgressKeyPrefix + levelIndex;
        if (PlayerPrefs.HasKey(key))
        {
            string json = PlayerPrefs.GetString(key);
            return JsonUtility.FromJson<LevelSaveData>(json);
        }
        return new LevelSaveData(); // Повертаємо порожні дані, якщо збереження немає
    }

    // Інструменти для очищення збережень
    public static void ClearLastCompletedLevel()
    {
        PlayerPrefs.DeleteKey(CurrentLevelKey);
        Debug.Log("Очищено збереження останнього пройденого рівня.");
    }

    public static void ClearLevelProgress(int levelIndex)
    {
        PlayerPrefs.DeleteKey(LevelProgressKeyPrefix + levelIndex);
        Debug.Log($"Очищено прогрес для рівня {levelIndex}.");
    }
}
