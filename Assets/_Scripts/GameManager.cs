using UnityEngine;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Налаштування рівнів")]
    [SerializeField] private LevelCollectionSO levelCollection;
    [SerializeField] private LevelLoader levelLoader;

    [Header("Камера")]
    [Tooltip("Посилання на контролер камери в сцені.")]
    [SerializeField] private CameraController cameraController;

    [Header("UI Елементи")]
    [Tooltip("Об'єкт, який буде показано при завершенні рівня (напр. панель з текстом).")]
    [SerializeField] private GameObject levelCompleteScreen;

    public int CurrentLevelIndex { get; private set; }

    public bool IsLevelActive => _gameState == GameState.Playing;

    private enum GameState { MainMenu, Playing, LevelComplete }
    private GameState _gameState;

    private bool _isDebugTextVisible = false;
    private GridDataSO _activeLevelData;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        if (cameraController == null)
        {
            cameraController = FindFirstObjectByType<CameraController>();
        }
    }

    private void Start()
    {
        if (levelCompleteScreen != null)
        {
            levelCompleteScreen.SetActive(false);
        }

        _gameState = GameState.MainMenu;
        levelLoader.ClearLevel();
    }

    private void OnDestroy()
    {
        if (_activeLevelData != null)
        {
            _activeLevelData.OnValuesChanged -= OnLevelSettingsChanged;
        }
    }

    private void Update()
    {
        if (_gameState == GameState.MainMenu) return;

        if (_gameState == GameState.LevelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                SwitchToNextLevel(true);
            }
        }

        bool isShiftHeld = Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift);

        if (Input.GetKeyDown(KeyCode.F1)) SaveSystem.ClearLastCompletedLevel();
        if (Input.GetKeyDown(KeyCode.F2)) SaveSystem.ClearLevelProgress(CurrentLevelIndex);

        if (Input.GetKeyDown(KeyCode.F3))
        {
            _isDebugTextVisible = !_isDebugTextVisible;
            if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
            {
                GridBuildingSystem.Instance.GetGrid().SetDebugTextVisibility(_isDebugTextVisible);
            }
        }

        if (Input.GetKeyDown(KeyCode.R)) RestartCurrentLevel();

        if (_gameState == GameState.Playing)
        {
            if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

            if (Input.GetKeyDown(KeyCode.Z))
            {
                CommandHistory.Undo();
                SaveCurrentProgress();
                // Примусово оновлюємо візуал після Undo
                if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                CommandHistory.Redo();
                SaveCurrentProgress();
                if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
            }
        }

        if (Input.GetKeyDown(KeyCode.Mouse4)) SwitchToNextLevel(!isShiftHeld);
        if (Input.GetKeyDown(KeyCode.Mouse3)) SwitchToPreviousLevel(!isShiftHeld);
    }

    public void StartGameAtLevel(int levelIndex, bool loadFromSave)
    {
        if (levelIndex < 0 || levelIndex >= levelCollection.levels.Count)
            levelIndex = 0;

        LoadLevel(levelIndex, loadFromSave);
        UIManager.Instance.ShowGameUI();
    }

    public void ReturnToMainMenu()
    {
        _gameState = GameState.MainMenu;
        levelLoader.ClearLevel();
        UIManager.Instance.ShowMainMenu();
    }

    public void LoadLevel(int index, bool loadFromSave)
    {
        if (PauseManager.Instance != null) PauseManager.Instance.ResetPauseState();
        else Time.timeScale = 1f;

        if (levelCollection == null || levelCollection.levels.Count == 0) return;

        if (levelCompleteScreen != null) levelCompleteScreen.SetActive(false);

        if (index >= levelCollection.levels.Count)
        {
            Debug.Log("Всі рівні пройдено!");
            ReturnToMainMenu();
            return;
        }

        if (_activeLevelData != null)
        {
            _activeLevelData.OnValuesChanged -= OnLevelSettingsChanged;
        }

        CurrentLevelIndex = index;
        SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex);

        _activeLevelData = levelCollection.levels[CurrentLevelIndex];
        _activeLevelData.OnValuesChanged += OnLevelSettingsChanged;

        // 1. Очищаємо все старе
        if (PuzzleManager.Instance != null) PuzzleManager.Instance.ResetState();
        CommandHistory.Clear();

        // 2. Завантажуємо рівень (Це ініціалізує грід)
        levelLoader.LoadLevel(_activeLevelData, loadFromSave);

        // 3. Примусово оновлюємо GridVisualManager після створення нового гріда
        if (GridVisualManager.Instance != null)
        {
            GridVisualManager.Instance.ReinitializeVisuals();
        }

        // 4. Налаштовуємо камеру
        if (cameraController != null)
        {
            cameraController.activeGridData = _activeLevelData;
            ApplyCameraSettings();
            cameraController.FocusOnLevel(false);
        }

        _gameState = GameState.Playing;

        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            GridBuildingSystem.Instance.GetGrid().SetDebugTextVisibility(_isDebugTextVisible);
        }
    }

    private void OnLevelSettingsChanged()
    {
        if (_gameState == GameState.Playing && _activeLevelData != null)
        {
            ApplyCameraSettings();
        }
    }

    private void ApplyCameraSettings()
    {
        if (cameraController != null && _activeLevelData != null)
        {
            cameraController.SetCameraBounds(
                _activeLevelData.cameraBoundsCenter,
                _activeLevelData.cameraBoundsSize,
                _activeLevelData.cameraBoundsYRotation
            );
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

            if (cameraController != null)
            {
                cameraController.FocusOnLevel(false);
            }

            if (CurrentLevelIndex + 1 < levelCollection.levels.Count)
            {
                SaveSystem.SaveCurrentLevelIndex(CurrentLevelIndex + 1);
            }

            SaveSystem.ClearLevelProgress(CurrentLevelIndex);
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