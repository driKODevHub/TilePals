using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Налаштування рівнів")]
    [SerializeField] private LevelCollectionSO levelCollection;
    [SerializeField] private LevelLoader levelLoader;

    // --- НОВЕ ПОЛЕ ДЛЯ UI ---
    [Header("UI Елементи")]
    [Tooltip("Об'єкт, який буде показано при завершенні рівня (напр. панель з текстом).")]
    [SerializeField] private GameObject levelCompleteScreen;

    public int CurrentLevelIndex { get; private set; }
    private enum GameState { Playing, LevelComplete }
    private GameState _gameState;

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

        CurrentLevelIndex = SaveSystem.LoadCurrentLevelIndex();
        LoadLevel(CurrentLevelIndex, true);
    }

    private void Update()
    {
        // --- НОВА ЛОГІКА ДЛЯ ПЕРЕХОДУ НА НАСТУПНИЙ РІВЕНЬ ---
        if (_gameState == GameState.LevelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SwitchToNextLevel(true); // Завантажуємо наступний рівень, очищуючи його прогрес
            }
            return; // Блокуємо інший інпут, коли рівень пройдено
        }

        // --- Стара логіка для дебагу ---
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SaveSystem.ClearLastCompletedLevel();
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SaveSystem.ClearLevelProgress(CurrentLevelIndex);
        }
        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartCurrentLevel();
        }

        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);
        if (Input.GetKeyDown(KeyCode.Mouse4)) // Next Level
        {
            SwitchToNextLevel(!isShiftHeld);
        }
        if (Input.GetKeyDown(KeyCode.Mouse3)) // Prev Level
        {
            SwitchToPreviousLevel(!isShiftHeld);
        }
    }

    public void LoadLevel(int index, bool loadFromSave)
    {
        if (levelCollection == null || levelCollection.levels.Count == 0) return;

        // Ховаємо екран завершення при завантаженні нового рівня
        if (levelCompleteScreen != null)
        {
            levelCompleteScreen.SetActive(false);
        }

        if (index >= levelCollection.levels.Count)
        {
            Debug.Log("Всі рівні пройдено!");
            // Тут можна показати екран фінальних титрів або щось подібне
            return;
        }

        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.ResetState();
        }

        CurrentLevelIndex = index;
        levelLoader.LoadLevel(levelCollection.levels[CurrentLevelIndex], loadFromSave);
        _gameState = GameState.Playing;
    }

    public void RestartCurrentLevel()
    {
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
            // Можна додати логіку для екрану "Дякуємо за гру"
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
            SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex + 1);
            SaveSystem.ClearLevelProgress(CurrentLevelIndex);
            Debug.Log("Рівень пройдено! Натисніть ПРОБІЛ, щоб продовжити.");

            // Показуємо екран завершення
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
