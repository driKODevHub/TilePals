using UnityEngine;
using UnityEngine.UI;

// Main Menu UI Controller
public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button quitButton;

    private void Awake()
    {
        if (continueButton)
        {
            continueButton.onClick.AddListener(() =>
            {
                int lastLocationIndex = SaveSystem.LoadCurrentLevelIndex();
                GameManager.Instance.SelectLocation(lastLocationIndex);
            });
        }

        if (levelSelectButton)
        {
            levelSelectButton.onClick.AddListener(() =>
            {
                UIManager.Instance.ShowLevelSelection();
            });
        }

        if (quitButton)
        {
            quitButton.onClick.AddListener(() =>
            {
                Application.Quit();
#if UNITY_EDITOR
                UnityEditor.EditorApplication.isPlaying = false;
#endif
            });
        }
    }

    public void SetActive(bool isActive)
    {
        gameObject.SetActive(isActive);
    }
}
