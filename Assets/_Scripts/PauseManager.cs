using UnityEngine;
using UnityEngine.InputSystem;
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
        // Якщо ми в головному меню (рівень не активний) - пауза недоступна
        if (GameManager.Instance == null || !GameManager.Instance.IsLevelActive) return;

        // Перевірка на натискання ESC (підтримує і стару, і нову систему)
        bool escapePressed = false;

        // Old Input
        if (Input.GetKeyDown(KeyCode.Escape)) escapePressed = true;

        // New Input (безпечна перевірка)
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null && Keyboard.current.escapeKey.wasPressedThisFrame) escapePressed = true;
#endif

        if (escapePressed)
        {
            TogglePause();
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
        if (IsPaused) return;

        IsPaused = true;
        Time.timeScale = 0f;
        OnGamePaused?.Invoke();

        // Прямий виклик UI для надійності
        if (UIManager.Instance != null) UIManager.Instance.ShowPauseMenu();
    }

    public void ResumeGame()
    {
        if (!IsPaused) return;

        IsPaused = false;
        Time.timeScale = 1f;
        OnGameResumed?.Invoke();

        if (UIManager.Instance != null) UIManager.Instance.HidePauseMenu();
    }

    public void ResetPauseState()
    {
        IsPaused = false;
        Time.timeScale = 1f;
    }
}