using UnityEngine;
using UnityEngine.InputSystem;
using System;

/// <summary>
/// Проміжний шар між InputSystem та грою. 
/// Відповідає ТІЛЬКИ за зчитування натискань і передачу їх через події.
/// </summary>
public class InputReader : MonoBehaviour
{
    public static InputReader Instance { get; private set; }

    [Header("Input Actions Asset")]
    [SerializeField] private PlayerInputActions playerInputActions; // Якщо використовуєш згенерований клас, це поле може бути не потрібним в інспекторі, ініціалізуємо в коді.

    // Події для підписки інших скриптів
    public event Action<Vector2> OnMoveInput;
    public event Action<float> OnRotateInput;
    public event Action<float> OnZoomInput;
    public event Action OnClickStarted;
    public event Action OnClickCanceled;
    public event Action OnClickPerformed; // Клік завершено (відпустили кнопку)

    // Події для геймплею (обертання фігури)
    public event Action OnRotatePieceLeft;
    public event Action OnRotatePieceRight;
    public event Action OnAlternateRotate; // Пробіл або ПКМ

    public Vector2 MousePosition => Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;

    private PlayerInputActions _inputActions;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

        _inputActions = new PlayerInputActions();

        // Підписуємось на події Input System
        _inputActions.Player.CameraMovement.performed += ctx => OnMoveInput?.Invoke(ctx.ReadValue<Vector2>());
        _inputActions.Player.CameraMovement.canceled += ctx => OnMoveInput?.Invoke(Vector2.zero);

        _inputActions.Player.CameraRotate.performed += ctx => OnRotateInput?.Invoke(ctx.ReadValue<float>());
        _inputActions.Player.CameraRotate.canceled += ctx => OnRotateInput?.Invoke(0f);

        _inputActions.Player.CameraZoom.performed += ctx => OnZoomInput?.Invoke(ctx.ReadValue<float>());

        _inputActions.Player.Click.started += ctx => OnClickStarted?.Invoke();
        _inputActions.Player.Click.canceled += ctx => OnClickCanceled?.Invoke();
        _inputActions.Player.Click.performed += ctx => OnClickPerformed?.Invoke();
    }

    private void Update()
    {
        // Додаткова обробка клавіш для обертання фігур (можна перенести в InputActions, але поки так для сумісності)
        if (Input.GetKeyDown(KeyCode.Q)) OnRotatePieceLeft?.Invoke();
        if (Input.GetKeyDown(KeyCode.E)) OnRotatePieceRight?.Invoke();
        if (Input.GetKeyDown(KeyCode.Space) || Input.GetMouseButtonDown(1)) OnAlternateRotate?.Invoke();
    }

    private void OnEnable()
    {
        _inputActions.Enable();
    }

    private void OnDisable()
    {
        _inputActions.Disable();
    }
}