using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Панелі Меню")]
    [SerializeField] private GameObject mainMenuPanel;
    [SerializeField] private GameObject levelSelectionPanel;
    [SerializeField] private GameObject pauseMenuPanel;
    [SerializeField] private GameObject gameHUDPanel; // Панель з кнопками гри (якщо є), або просто пустий об'єкт для групування

    // Зберігаємо, звідки ми відкрили вибір рівня (з Головного меню чи з Паузи), щоб знати куди повертатись кнопкою "Назад"
    private GameObject _previousPanelBeforeLevelSelect;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // На старті показуємо Головне Меню
        ShowMainMenu();
    }

    public void ShowMainMenu()
    {
        mainMenuPanel.SetActive(true);
        levelSelectionPanel.SetActive(false);
        pauseMenuPanel.SetActive(false);
        if (gameHUDPanel) gameHUDPanel.SetActive(false);

        // Переконуємось, що час йде (якщо ми вийшли з паузи)
        Time.timeScale = 1f;
    }

    public void ShowGameUI()
    {
        mainMenuPanel.SetActive(false);
        levelSelectionPanel.SetActive(false);
        pauseMenuPanel.SetActive(false);
        if (gameHUDPanel) gameHUDPanel.SetActive(true);
    }

    public void ShowPauseMenu()
    {
        // Пауза викликається поверх гри, тому HUD не ховаємо, або ховаємо за бажанням
        pauseMenuPanel.SetActive(true);
        // Час зупиняється в PauseManager
    }

    public void HidePauseMenu()
    {
        pauseMenuPanel.SetActive(false);
    }

    public void ShowLevelSelection()
    {
        // Запам'ятовуємо, що було відкрито до цього
        if (mainMenuPanel.activeSelf) _previousPanelBeforeLevelSelect = mainMenuPanel;
        else if (pauseMenuPanel.activeSelf) _previousPanelBeforeLevelSelect = pauseMenuPanel;
        else _previousPanelBeforeLevelSelect = mainMenuPanel; // Fallback

        mainMenuPanel.SetActive(false);
        pauseMenuPanel.SetActive(false);

        levelSelectionPanel.SetActive(true);
    }

    public void OnLevelSelectionBack()
    {
        levelSelectionPanel.SetActive(false);

        // Повертаємось туди, звідки прийшли
        if (_previousPanelBeforeLevelSelect != null)
        {
            _previousPanelBeforeLevelSelect.SetActive(true);
        }
        else
        {
            ShowMainMenu();
        }
    }
}