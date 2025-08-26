using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Налаштування рівнів")]
    [Tooltip("Колекція всіх рівнів гри у правильному порядку")]
    [SerializeField] private LevelCollectionSO levelCollection;

    [Header("Посилання на компоненти сцени")]
    [SerializeField] private LevelLoader levelLoader;

    public int CurrentLevelIndex { get; private set; }
    private GameState _gameState;

    private enum GameState { Playing, LevelComplete }

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Start()
    {
        CurrentLevelIndex = SaveSystem.LoadCurrentLevelIndex();
        LoadLevel(CurrentLevelIndex, true);
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F1))
        {
            SaveSystem.ClearLastCompletedLevel();
            Debug.Log("Прогрес проходження рівнів скинуто. Перезапустіть гру, щоб почати з рівня 0.");
        }
        if (Input.GetKeyDown(KeyCode.F2))
        {
            SaveSystem.ClearLevelProgress(CurrentLevelIndex);
            Debug.Log($"Прогрес поточного рівня ({CurrentLevelIndex}) скинуто. Натисніть R, щоб перезапустити рівень і побачити зміни.");
        }

        if (Input.GetKeyDown(KeyCode.R))
        {
            RestartCurrentLevel();
        }

        if (_gameState == GameState.LevelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                LoadNextLevel();
            }
        }

        // --- ОНОВЛЕНИЙ КОД ---
        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.Mouse4)) // Кнопка "Вперед"
        {
            // Якщо Shift затиснутий, ми НЕ очищуємо прогрес.
            SwitchToNextLevel(!isShiftHeld);
        }
        if (Input.GetKeyDown(KeyCode.Mouse3)) // Кнопка "Назад"
        {
            // Якщо Shift затиснутий, ми НЕ очищуємо прогрес.
            SwitchToPreviousLevel(!isShiftHeld);
        }
    }

    public void LoadLevel(int index, bool loadFromSave)
    {
        if (levelCollection == null || levelCollection.levels.Count == 0)
        {
            Debug.LogError("Колекція рівнів (LevelCollection) не налаштована!");
            return;
        }

        if (index >= levelCollection.levels.Count)
        {
            Debug.Log("<color=green><b>Всі рівні пройдено! Вітаємо!</b></color>");
            return;
        }

        if (PuzzleManager.Instance != null)
        {
            PuzzleManager.Instance.ResetState();
        }

        CurrentLevelIndex = index;
        levelLoader.LoadLevel(levelCollection.levels[CurrentLevelIndex], loadFromSave);
        _gameState = GameState.Playing;
        Debug.Log($"Завантаження рівня {CurrentLevelIndex}");
    }

    public void RestartCurrentLevel()
    {
        Debug.Log("Перезапуск рівня...");
        SaveSystem.ClearLevelProgress(CurrentLevelIndex);
        LoadLevel(CurrentLevelIndex, false);
    }

    private void LoadNextLevel()
    {
        LoadLevel(CurrentLevelIndex + 1, false);
    }

    // --- ОНОВЛЕНІ МЕТОДИ ---
    private void SwitchToNextLevel(bool clearProgress)
    {
        int nextIndex = CurrentLevelIndex + 1;
        if (nextIndex < levelCollection.levels.Count)
        {
            string message = clearProgress ? " (прогрес буде скинуто)" : " (прогрес буде завантажено)";
            Debug.Log($"<color=orange>Перемикання на наступний рівень ({nextIndex}){message}...</color>");

            if (clearProgress)
            {
                SaveSystem.ClearLevelProgress(nextIndex);
            }
            // Якщо прогрес не очищуємо, то завантажуємо зі збереження (loadFromSave = true)
            LoadLevel(nextIndex, !clearProgress);
        }
        else
        {
            Debug.Log("<color=orange>Це останній рівень.</color>");
        }
    }

    private void SwitchToPreviousLevel(bool clearProgress)
    {
        int prevIndex = CurrentLevelIndex - 1;
        if (prevIndex >= 0)
        {
            string message = clearProgress ? " (прогрес буде скинуто)" : " (прогрес буде завантажено)";
            Debug.Log($"<color=orange>Перемикання на попередній рівень ({prevIndex}){message}...</color>");

            if (clearProgress)
            {
                SaveSystem.ClearLevelProgress(prevIndex);
            }
            LoadLevel(prevIndex, !clearProgress);
        }
        else
        {
            Debug.Log("<color=orange>Це перший рівень.</color>");
        }
    }

    public void OnLevelComplete()
    {
        if (_gameState == GameState.Playing)
        {
            _gameState = GameState.LevelComplete;
            Debug.Log("<color=green><b>РІВЕНЬ ПРОЙДЕНО!</b></color> <color=yellow>Натисніть ПРОБІЛ, щоб перейти до наступного рівня.</color>");

            SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex + 1);
            SaveSystem.ClearLevelProgress(CurrentLevelIndex);
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
