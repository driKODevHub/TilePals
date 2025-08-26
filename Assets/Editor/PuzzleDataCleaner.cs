using UnityEngine;
using UnityEditor;
using System.Collections.Generic;

public class PuzzleDataCleaner
{
    // Створюємо новий пункт меню в редакторі Unity
    [MenuItem("Tools/Puzzle Tools/Clear All Solution Data")]
    private static void ClearAllSolutionData()
    {
        // Показуємо вікно з попередженням та просимо підтвердження
        if (!EditorUtility.DisplayDialog(
                "Clear All Puzzle Solution Data?",
                "This will find every GridDataSO asset in your project and clear its generated and calculated solution data. " +
                "This action cannot be undone.",
                "Yes, Clear Data",
                "Cancel"))
        {
            return; // Користувач натиснув "Cancel"
        }

        // Знаходимо GUID'и всіх асетів типу GridDataSO у проєкті
        string[] guids = AssetDatabase.FindAssets("t:GridDataSO");
        int processedCount = 0;

        if (guids.Length == 0)
        {
            Debug.Log("No GridDataSO assets found in the project.");
            return;
        }

        Debug.Log($"Found {guids.Length} GridDataSO assets. Starting cleanup...");

        foreach (string guid in guids)
        {
            // Отримуємо шлях до файлу за його GUID
            string path = AssetDatabase.GUIDToAssetPath(guid);
            // Завантажуємо асет за шляхом
            GridDataSO gridData = AssetDatabase.LoadAssetAtPath<GridDataSO>(path);

            if (gridData != null)
            {
                // Очищуємо всі поля, пов'язані з генерацією та аналізом
                gridData.puzzleSolution?.Clear();
                gridData.puzzlePieces?.Clear();
                gridData.generatedPieceSummary?.Clear();
                gridData.solutionVariantsCount = 0;
                gridData.allFoundSolutions?.Clear();
                gridData.currentSolutionIndex = 0;

                // Позначаємо асет як "брудний", щоб Unity зберіг зміни
                EditorUtility.SetDirty(gridData);
                processedCount++;
            }
        }

        // Зберігаємо всі змінені асети на диск
        AssetDatabase.SaveAssets();
        // Оновлюємо вікно Project, щоб побачити зміни (якщо вони є)
        AssetDatabase.Refresh();

        Debug.Log($"<color=green>Cleanup complete! Processed {processedCount} GridDataSO assets.</color>");
    }
}
