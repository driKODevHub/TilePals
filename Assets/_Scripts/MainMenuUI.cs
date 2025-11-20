using UnityEngine;
using UnityEngine.UI;

public class MainMenuUI : MonoBehaviour
{
    [Header("Buttons")]
    [SerializeField] private Button continueButton;
    [SerializeField] private Button levelSelectButton;
    [SerializeField] private Button quitButton;

    private void Start()
    {
        // Кнопка "Продовжити"
        continueButton.onClick.AddListener(() =>
        {
            // Завантажуємо останній збережений рівень
            int lastLevelIndex = SaveSystem.LoadCurrentLevelIndex();
            // Переходимо в гру, завантажуючи збереження (loadFromSave = true)
            GameManager.Instance.StartGameAtLevel(lastLevelIndex, true);
        });

        // Кнопка "Вибір Рівнів"
        levelSelectButton.onClick.AddListener(() =>
        {
            UIManager.Instance.ShowLevelSelection();
        });

        // Кнопка "Вихід"
        quitButton.onClick.AddListener(() =>
        {
            Application.Quit();
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#endif
        });
    }

    private void OnEnable()
    {
        // Перевіряємо, чи є збереження, щоб активувати/деактивувати кнопку "Продовжити"
        // Якщо це перший запуск (рівень 0 і немає збережених даних про фігури), можна зробити перевірку.
        // Для спрощення: кнопка завжди активна, просто вантажить останній відомий індекс.

        // Але якщо хочеш заблокувати, якщо гравець ще не грав:
        // bool hasSave = PlayerPrefs.HasKey("CurrentLevelIndex");
        // continueButton.interactable = hasSave;
    }
}