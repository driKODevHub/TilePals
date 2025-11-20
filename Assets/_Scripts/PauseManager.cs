using UnityEngine;
using System;

public class PauseManager : MonoBehaviour
{
    public static PauseManager Instance { get; private set; }

    public event Action OnGamePaused;
    public event Action OnGameResumed;

    public bool IsPaused { get; private set; } = false;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        // Перевіряємо натискання Escape для паузи/відновлення
        // Перевіряємо GameManager, щоб не ставити на паузу, якщо рівень вже завершено
        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (GameManager.Instance != null && GameManager.Instance.IsLevelActive)
            {
                TogglePause();
            }
        }
    }

    public void TogglePause()
    {
        if (IsPaused)
        {
            ResumeGame();
        }
        else
        {
            PauseGame();
        }
    }

    public void PauseGame()
    {
        IsPaused = true;
        Time.timeScale = 0f; // Зупиняємо час
        OnGamePaused?.Invoke();
    }

    public void ResumeGame()
    {
        IsPaused = false;
        Time.timeScale = 1f; // Відновлюємо час
        OnGameResumed?.Invoke();
    }

    /// <summary>
    /// Примусово скидає паузу (наприклад, при рестарті рівня)
    /// </summary>
    public void ResetPauseState()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}