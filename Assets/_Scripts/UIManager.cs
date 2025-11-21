using UnityEngine;

public class UIManager : MonoBehaviour
{
    public static UIManager Instance { get; private set; }

    [Header("Canvas Reference")]
    [Tooltip("Трансформ Канвасу, куди будуть спавнитись вікна.")]
    [SerializeField] private Transform uiCanvasTransform;

    [Header("UI Prefabs")]
    [SerializeField] private GameObject mainMenuPrefab;
    [SerializeField] private GameObject levelSelectionPrefab;
    [SerializeField] private GameObject pauseMenuPrefab;
    [SerializeField] private GameObject gameHUDPrefab;

    // Приватні інстанси (створені об'єкти)
    private MainMenuUI _mainMenuInstance;
    private LevelSelectionUI _levelSelectionInstance;
    private PauseMenuUI _pauseMenuInstance;
    private GameObject _gameHUDInstance;

    private bool _wasPauseMenuOpenBeforeLevelSelect = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        // Спавнимо всі вікна одразу, ініціалізуємо їх та ховаємо
        SpawnAllUI();
    }

    private void Start()
    {
        // На старті показуємо головне меню
        ShowMainMenu();
    }

    private void SpawnAllUI()
    {
        if (uiCanvasTransform == null)
        {
            Debug.LogError("UIManager: UI Canvas Transform is not assigned!");
            return;
        }

        // 1. Main Menu
        if (mainMenuPrefab)
        {
            GameObject obj = Instantiate(mainMenuPrefab, uiCanvasTransform);
            _mainMenuInstance = obj.GetComponent<MainMenuUI>();
            if (_mainMenuInstance == null) Debug.LogError("Main Menu Prefab is missing MainMenuUI script!");
            obj.SetActive(false); // Ховаємо
        }

        // 2. Level Selection
        if (levelSelectionPrefab)
        {
            GameObject obj = Instantiate(levelSelectionPrefab, uiCanvasTransform);
            _levelSelectionInstance = obj.GetComponent<LevelSelectionUI>();
            if (_levelSelectionInstance == null) Debug.LogError("Level Selection Prefab is missing LevelSelectionUI script!");
            obj.SetActive(false);
        }

        // 3. Pause Menu
        if (pauseMenuPrefab)
        {
            GameObject obj = Instantiate(pauseMenuPrefab, uiCanvasTransform);
            _pauseMenuInstance = obj.GetComponent<PauseMenuUI>();
            // Важливо: PauseMenu має бути останнім у ієрархії (поверх інших), тому SetAsLastSibling
            obj.transform.SetAsLastSibling();
            if (_pauseMenuInstance == null) Debug.LogError("Pause Menu Prefab is missing PauseMenuUI script!");
            obj.SetActive(false);
        }

        // 4. Game HUD
        if (gameHUDPrefab)
        {
            _gameHUDInstance = Instantiate(gameHUDPrefab, uiCanvasTransform);
            _gameHUDInstance.transform.SetAsFirstSibling(); // HUD зазвичай знизу (під меню паузи)
            _gameHUDInstance.SetActive(false);
        }
    }

    public void ShowMainMenu()
    {
        if (_mainMenuInstance) _mainMenuInstance.SetActive(true);
        if (_levelSelectionInstance) _levelSelectionInstance.SetActive(false);
        if (_pauseMenuInstance) _pauseMenuInstance.SetActive(false);
        if (_gameHUDInstance) _gameHUDInstance.SetActive(false);

        Time.timeScale = 1f;
    }

    public void ShowGameUI()
    {
        if (_mainMenuInstance) _mainMenuInstance.SetActive(false);
        if (_levelSelectionInstance) _levelSelectionInstance.SetActive(false);
        if (_pauseMenuInstance) _pauseMenuInstance.SetActive(false);
        if (_gameHUDInstance) _gameHUDInstance.SetActive(true);
    }

    public void ShowPauseMenu()
    {
        if (_pauseMenuInstance) _pauseMenuInstance.SetActive(true);
    }

    public void HidePauseMenu()
    {
        if (_pauseMenuInstance) _pauseMenuInstance.SetActive(false);
    }

    public void ShowLevelSelection()
    {
        // Запам'ятовуємо, чи була відкрита пауза
        _wasPauseMenuOpenBeforeLevelSelect = (_pauseMenuInstance != null && _pauseMenuInstance.IsActive);

        if (_mainMenuInstance) _mainMenuInstance.SetActive(false);
        if (_pauseMenuInstance) _pauseMenuInstance.SetActive(false); // Тимчасово ховаємо паузу

        if (_levelSelectionInstance) _levelSelectionInstance.SetActive(true);
    }

    public void OnLevelSelectionBack()
    {
        if (_levelSelectionInstance) _levelSelectionInstance.SetActive(false);

        if (_wasPauseMenuOpenBeforeLevelSelect)
        {
            // Повертаємось в меню паузи
            if (_pauseMenuInstance) _pauseMenuInstance.SetActive(true);
        }
        else
        {
            // Повертаємось в головне меню
            if (_mainMenuInstance) _mainMenuInstance.SetActive(true);
        }
    }
}