using UnityEngine;
using UnityEngine.UI;
using TMPro; // Якщо використовуєш TextMeshPro, інакше використовуй Text
using System.Collections.Generic;

public class LevelSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelCollectionSO levelCollection;
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Button backButton;

    private List<GameObject> _spawnedButtons = new List<GameObject>();

    private void Start()
    {
        backButton.onClick.AddListener(() =>
        {
            UIManager.Instance.OnLevelSelectionBack();
        });

        GenerateLevelButtons();
    }

    private void GenerateLevelButtons()
    {
        // Очищення старих кнопок (якщо ми будемо регенерувати)
        foreach (var btn in _spawnedButtons) Destroy(btn);
        _spawnedButtons.Clear();

        if (levelCollection == null) return;

        for (int i = 0; i < levelCollection.levels.Count; i++)
        {
            int levelIndex = i; // Локальна копія для замикання (closure)
            GridDataSO levelData = levelCollection.levels[i];

            GameObject btnObj = Instantiate(levelButtonPrefab, buttonsContainer);
            _spawnedButtons.Add(btnObj);

            // Налаштування тексту кнопки (підлаштуй під свою структуру префабу)
            // Якщо використовуєш звичайний Text:
            // btnObj.GetComponentInChildren<Text>().text = (i + 1).ToString();
            // Якщо TextMeshPro:
            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = (i + 1).ToString(); // Або levelData.name, якщо хочеш повну назву
            }

            Button btnComp = btnObj.GetComponent<Button>();
            btnComp.onClick.AddListener(() =>
            {
                OnLevelButtonClicked(levelIndex);
            });
        }
    }

    private void OnLevelButtonClicked(int levelIndex)
    {
        // При виборі конкретного рівня з меню, ми скидаємо прогрес цього рівня (починаємо спочатку)
        SaveSystem.ClearLevelProgress(levelIndex);

        // Запускаємо гру
        GameManager.Instance.StartGameAtLevel(levelIndex, false);
    }
}