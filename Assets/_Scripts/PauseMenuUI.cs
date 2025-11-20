using UnityEngine;
using UnityEngine.UI;

public class PauseMenuUI : MonoBehaviour
{
    [Header("UI References")]
    [Tooltip("Батьківський об'єкт візуалу меню (Panel), який буде вмикатися/вимикатися.")]
    [SerializeField] private GameObject pauseMenuContainer;

    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton; // Нова кнопка
    [SerializeField] private Button mainMenuButton;    // Нова кнопка
    [SerializeField] private Button quitButton;

    private void Start()
    {
        // 1. Resume
        resumeButton.onClick.AddListener(() =>
        {
            PauseManager.Instance.ResumeGame();
        });

        // 2. Restart
        restartButton.onClick.AddListener(() =>
        {
            PauseManager.Instance.ResumeGame();
            GameManager.Instance.RestartCurrentLevel();
        });

        // 3. Level Selection (з Паузи)
        if (levelSelectButton != null)
        {
            levelSelectButton.onClick.AddListener(() =>
            {
                // Не знімаємо паузу повністю, просто перемикаємо UI
                UIManager.Instance.ShowLevelSelection();
            });
        }

        // 4. Main Menu
        if (mainMenuButton != null)
        {
            mainMenuButton.onClick.AddListener(() =>
            {
                PauseManager.Instance.ResumeGame(); // Відновлюємо час перед виходом
                GameManager.Instance.ReturnToMainMenu();
            });
        }

        // 5. Quit
        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });

        // Підписка на події менеджера паузи
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnGamePaused += Show;
            PauseManager.Instance.OnGameResumed += Hide;
        }

        // Ховаємо меню на старті, бо цим керує UIManager/PauseManager
        Hide();
    }

    private void OnDestroy()
    {
        if (PauseManager.Instance != null)
        {
            PauseManager.Instance.OnGamePaused -= Show;
            PauseManager.Instance.OnGameResumed -= Hide;
        }
    }

    private void Show()
    {
        if (pauseMenuContainer != null)
        {
            pauseMenuContainer.SetActive(true);
            // При відкритті паузи ми кажемо UIManager, що ми тут (хоча UIManager сам це викликав, це для безпеки)
        }
    }

    private void Hide()
    {
        if (pauseMenuContainer != null)
            pauseMenuContainer.SetActive(false);
    }
}