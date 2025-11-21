using UnityEngine;
using UnityEngine.UI;

// Вішайте цей скрипт на КОРІНЬ префабу PauseMenu
public class PauseMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button resumeButton;
    [SerializeField] private Button restartButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button mainMenuButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (resumeButton) resumeButton.onClick.AddListener(() => PauseManager.Instance.ResumeGame());

        if (restartButton) restartButton.onClick.AddListener(() =>
        {
            PauseManager.Instance.ResumeGame();
            GameManager.Instance.RestartCurrentLevel();
        });

        if (levelSelectButton) levelSelectButton.onClick.AddListener(() => UIManager.Instance.ShowLevelSelection());

        if (mainMenuButton) mainMenuButton.onClick.AddListener(() =>
        {
            PauseManager.Instance.ResumeGame();
            GameManager.Instance.ReturnToMainMenu();
        });

        if (quitButton) quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }

    // Публічна властивість, щоб UIManager міг перевірити стан
    public bool IsActive => gameObject.activeSelf;
}