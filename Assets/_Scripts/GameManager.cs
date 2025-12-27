using UnityEngine;
using System.Collections.Generic;

public class GameManager : MonoBehaviour
{
    public static GameManager Instance { get; private set; }

    [Header("Master Data")]
    [SerializeField] private List<LevelCollectionSO> availableLocations;
    [SerializeField] private LevelLoader levelLoader;

    [Header("Camera")]
    [Tooltip("Controller for the camera movement.")]
    [SerializeField] private CameraController cameraController;

    [Header("UI Feedback")]
    [Tooltip("Object that will be enabled on level completion.")]
    [SerializeField] private GameObject levelCompleteScreen;

    public int CurrentLevelIndex { get; private set; }
    public LevelCollectionSO CurrentLocation => _currentLocation;

    public bool IsLevelActive => _gameState == GameState.Playing;

    private enum GameState { MainMenu, Playing, LevelComplete }
    private GameState _gameState;

    private bool _isDebugTextVisible = false;
    private GridDataSO _activeLevelData;
    private LevelCollectionSO _currentLocation;

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

    private void Update()
    {
        if (_gameState == GameState.MainMenu) return;

        if (_gameState == GameState.LevelComplete)
        {
            if (Input.GetKeyDown(KeyCode.Space))
            {
                // Continue logic? For now back to menu or next step
            }
        }

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
                if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
            }

            if (Input.GetKeyDown(KeyCode.X))
            {
                CommandHistory.Redo();
                SaveCurrentProgress();
                if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
            }
        }
    }

    // --- SENIOR API ---

    public List<LevelCollectionSO> GetAvailableLocations() => availableLocations;

    public void SelectLocation(int index)
    {
        if (index < 0 || index >= availableLocations.Count) return;
        StartLocation(availableLocations[index], true);
    }

    public void StartLocation(LevelCollectionSO location, bool loadFromSave)
    {
        _currentLocation = location;
        LoadLocation(location, loadFromSave);
        UIManager.Instance.ShowGameUI();
    }

    public void ReturnToMainMenu()
    {
        _gameState = GameState.MainMenu;
        levelLoader.ClearLevel();
        UIManager.Instance.ShowMainMenu();
    }

    public void LoadLocation(LevelCollectionSO location, bool loadFromSave)
    {
        if (PauseManager.Instance != null) PauseManager.Instance.ResetPauseState();
        else Time.timeScale = 1f;

        if (location == null || location.levels == null || location.levels.Count == 0) return;

        if (levelCompleteScreen != null) levelCompleteScreen.SetActive(false);

        _currentLocation = location;
        _activeLevelData = null; 

        if (PuzzleManager.Instance != null) PuzzleManager.Instance.ResetState();
        CommandHistory.Clear();

        levelLoader.LoadLocation(location, loadFromSave);

        if (GridVisualManager.Instance != null)
        {
            GridVisualManager.Instance.ReinitializeVisuals();
        }
        
        _gameState = GameState.Playing;
    }

    public void RestartCurrentLevel()
    {
        if (_currentLocation != null)
        {
            LoadLocation(_currentLocation, false); // Reload without save to reset pieces if needed
        }
    }

    public void OnLevelComplete()
    {
        // This is called when a SINGLE steps is finished? 
        // Or the whole location? 
        // Logic should probably check if more steps are available.
        Debug.Log("GameManager: Level Step Complete!");
        // UI feedback...
    }

    public void SaveCurrentProgress()
    {
        if (_gameState == GameState.Playing)
        {
            levelLoader.SaveLevelState();
        }
    }
}
