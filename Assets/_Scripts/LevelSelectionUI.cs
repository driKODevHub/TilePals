using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections.Generic;

// Вішайте цей скрипт на КОРІНЬ префабу LevelSelection
public class LevelSelectionUI : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private LevelCollectionSO levelCollection;
    [SerializeField] private Transform buttonsContainer;
    [SerializeField] private GameObject levelButtonPrefab;
    [SerializeField] private Button backButton;

    private List<GameObject> _spawnedButtons = new List<GameObject>();
    private bool _isInitialized = false;

    private void Awake()
    {
        if (backButton)
        {
            backButton.onClick.AddListener(() =>
            {
                UIManager.Instance.OnLevelSelectionBack();
            });
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);

        // Генеруємо кнопки при першому відкритті
        if (isActive && !_isInitialized)
        {
            GenerateLevelButtons();
            _isInitialized = true;
        }
    }

    private void GenerateLevelButtons()
    {
        foreach (var btn in _spawnedButtons) Destroy(btn);
        _spawnedButtons.Clear();

        if (levelCollection == null) return;

        for (int i = 0; i < levelCollection.levels.Count; i++)
        {
            int levelIndex = i;
            GridDataSO levelData = levelCollection.levels[i];

            GameObject btnObj = Instantiate(levelButtonPrefab, buttonsContainer);
            _spawnedButtons.Add(btnObj);

            TextMeshProUGUI btnText = btnObj.GetComponentInChildren<TextMeshProUGUI>();
            if (btnText != null)
            {
                btnText.text = (i + 1).ToString();
            }
            else
            {
                Text legacyText = btnObj.GetComponentInChildren<Text>();
                if (legacyText != null) legacyText.text = (i + 1).ToString();
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
        SaveSystem.ClearLevelProgress(levelIndex);
        GameManager.Instance.StartGameAtLevel(levelIndex, false);
    }
}