using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Налаштування рівнів")]
    [SerializeField] private LevelCollectionSO levelCollection;
    [SerializeField] private LevelLoader levelLoader;

    [Header("UI Елементи")]
    [Tooltip("Об'єкт, який буде показано при завершенні рівня (напр. панель з текстом).")]
    [SerializeField] private GameObject levelCompleteScreen;

    public int CurrentLevelIndex { get; private set; }

    // --- ДОДАНО: Публічна властивість для перевірки стану гри ---
    public bool IsLevelActive => _gameState == GameState.Playing;

    private enum GameState { MainMenu, Playing, LevelComplete } // Додано MainMenu
    private GameState _gameState;

    // --- НОВЕ ПОЛЕ ДЛЯ ДЕБАГУ ---
    private bool _isDebugTextVisible = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        // Ховаємо екран завершення на старті
        if (levelCompleteScreen != null)
        {
            levelCompleteScreen.SetActive(false);
        }

        // --- ЗМІНА: На старті ми не вантажимо рівень автоматично, а йдемо в меню ---
        _gameState = GameState.MainMenu;

        // Очищаємо сцену (якщо раптом щось було)
        levelLoader.ClearLevel();

        // UIManager на своєму Start() покаже MainMenu
    }

    private void Update()
    {
        // Якщо ми в меню, ігноруємо інпут гри
        if (_gameState == GameState.MainMenu) return;

        // --- НОВА ЛОГІКА ДЛЯ ПЕРЕХОДУ НА НАСТУПНИЙ РІВЕНЬ ---
        if (_gameState == GameState.LevelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SwitchToNextLevel(true); // Завантажуємо наступний рівень, очищуючи його прогрес
            }
        }

        // --- Логіка для дебагу та управління ---
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.F1)) SaveSystem.ClearLastCompletedLevel();
        if (Input.GetKeyDown(KeyCode.F2)) SaveSystem.ClearLevelProgress(CurrentLevelIndex);

        if (Input.GetKeyDown(KeyCode.F3)) // --- ПЕРЕМИКАННЯ ДЕБАГ-ТЕКСТУ ---
        {
            _isDebugTextVisible = !_isDebugTextVisible;
            if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
            {
                GridBuildingSystem.Instance.GetGrid().SetDebugTextVisibility(_isDebugTextVisible);
            }
        }

        if (Input.GetKeyDown(KeyCode.R)) RestartCurrentLevel();

        // --- ЛОГІКА UNDO/REDO (працює лише під час гри та коли НЕМАЄ паузи) ---
        if (_gameState == GameState.Playing)
        {
            // Блокуємо Undo/Redo, якщо гра на паузі
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

            if (Input.GetKeyDown(KeyCode.Z))
            {
                CommandHistory.Undo();
                SaveCurrentProgress();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                CommandHistory.Redo();
                SaveCurrentProgress();
            }
        }

        // Чіти переходу між рівнями
        if (Input.GetKeyDown(KeyCode.Mouse4)) SwitchToNextLevel(!isShiftHeld);
        if (Input.GetKeyDown(KeyCode.Mouse3)) SwitchToPreviousLevel(!isShiftHeld);
    }

    // --- НОВІ ПУБЛІЧНІ МЕТОДИ ДЛЯ МЕНЮ ---

    public void StartGameAtLevel(int levelIndex, bool loadFromSave)
    {
        // Переконуємось, що індекс валідний
        if (levelIndex < 0 || levelIndex >= levelCollection.levels.Count)
            levelIndex = 0;

        LoadLevel(levelIndex, loadFromSave);
        UIManager.Instance.ShowGameUI();
    }

    public void ReturnToMainMenu()
    {
        _gameState = GameState.MainMenu;

        // Очищаємо поточний рівень
        levelLoader.ClearLevel();

        // Показуємо UI меню
        UIManager.Instance.ShowMainMenu();
    }

    // ---------------------------------------

    public void LoadLevel(int index, bool loadFromSave)
    {
        // Скидаємо паузу
        if (PauseManager.Instance != null) PauseManager.Instance.ResetPauseState();
        else Time.timeScale = 1f;

        if (levelCollection == null || levelCollection.levels.Count == 0) return;

        if (levelCompleteScreen != null) levelCompleteScreen.SetActive(false);

        if (index >= levelCollection.levels.Count)
        {
            Debug.Log("Всі рівні пройдено!");
            ReturnToMainMenu(); // Повертаємось в меню, якщо пройшли все
            return;
        }

        if (PuzzleManager.Instance != null) PuzzleManager.Instance.ResetState();

        CurrentLevelIndex = index;
        // Зберігаємо індекс як "Останній зіграний"
        SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex);

        levelLoader.LoadLevel(levelCollection.levels[CurrentLevelIndex], loadFromSave);
        _gameState = GameState.Playing;

        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            GridBuildingSystem.Instance.GetGrid().SetDebugTextVisibility(_isDebugTextVisible);
        }
    }

    public void RestartCurrentLevel()
    {
        if (PauseManager.Instance != null) PauseManager.Instance.ResetPauseState();
        else Time.timeScale = 1f;

        SaveSystem.ClearLevelProgress(CurrentLevelIndex);
        LoadLevel(CurrentLevelIndex, false);
    }

    private void SwitchToNextLevel(bool clearProgress)
    {
        int nextIndex = CurrentLevelIndex + 1;
        if (nextIndex < levelCollection.levels.Count)
        {
            if (clearProgress) SaveSystem.ClearLevelProgress(nextIndex);
            LoadLevel(nextIndex, !clearProgress);
        }
        else
        {
            Debug.Log("Це був останній рівень!");
            ReturnToMainMenu();
        }
    }

    private void SwitchToPreviousLevel(bool clearProgress)
    {
        int prevIndex = CurrentLevelIndex - 1;
        if (prevIndex >= 0)
        {
            if (clearProgress) SaveSystem.ClearLevelProgress(prevIndex);
            LoadLevel(prevIndex, !clearProgress);
        }
    }

    public void OnLevelComplete()
    {
        if (_gameState == GameState.Playing)
        {
            _gameState = GameState.LevelComplete;

            // Зберігаємо наступний рівень як поточний (щоб кнопка Continue в меню вела на нього)
            if (CurrentLevelIndex + 1 < levelCollection.levels.Count)
            {
                SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex + 1);
            }

            SaveSystem.ClearLevelProgress(CurrentLevelIndex); // Очищаємо прогрес пройденого
            Debug.Log("Рівень пройдено! Натисніть ПРОБІЛ, щоб продовжити.");

            if (levelCompleteScreen != null)
            {
                levelCompleteScreen.SetActive(true);
            }
        }
    }

    public void SaveCurrentProgress()
    {
        if (_gameState == GameState.Playing)
        {
            levelLoader.SaveLevelState();
        }
    }
}