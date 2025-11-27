using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;

public class CameraController : MonoBehaviour
{
    private const float MIN_FOLLOW_Y_OFFSET = 2f;
    private const float MAX_FOLLOW_Y_OFFSET = 50f;

    [SerializeField] CinemachineCamera cinemachineCamera;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 10f;
    [SerializeField] float rotationSpeed = 100f;

    [Header("Drag Pan Settings")]
    [Tooltip("Множник швидкості перетягування. 1.0 = 1:1 рух мишки до землі.")]
    [SerializeField] float dragSensitivity = 1.0f;
    [Tooltip("Чим вище значення, тим швидше камера зупиняється. Низьке значення = більше інерції (плавніше).")]
    [SerializeField] float movementSmoothing = 10f;

    private CinemachineFollow cinemachineFollow;
    private Vector3 inputMoveDirection;
    private Vector3 inputRorateDirection;
    private Vector3 targetFollowOffset;

    // Стан для перетягування та згладжування
    private bool isDragPanning = false;
    private Vector3 _targetPosition; // Цільова позиція, до якої прагне камера

    // Математична площина для рейкасту (рівень землі y=0)
    private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

    private PlayerInputActions playerInputActions;

    private void Awake()
    {
        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
    }

    private void Start()
    {
        cinemachineFollow = cinemachineCamera.GetComponent<CinemachineFollow>();
        targetFollowOffset = cinemachineFollow.FollowOffset;

        // Ініціалізуємо ціль поточною позицією
        _targetPosition = transform.position;
    }

    private void Update()
    {
        // --- БЛОКУВАННЯ КАМЕРИ ---

        // 1. Якщо гра на паузі (через PauseManager)
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        // 2. Якщо ми в меню або рівень ще не почався (через GameManager)
        if (GameManager.Instance != null && !GameManager.Instance.IsLevelActive) return;

        // -------------------------

        HandleDragPan();   // Розрахунок перетягування (змінює _targetPosition)
        HandleMovement();  // Розрахунок WASD (змінює _targetPosition)

        // --- ЗАСТОСУВАННЯ ЗГЛАДЖУВАННЯ ---
        // Рухаємо камеру від поточної позиції до цільової з використанням Lerp
        if (Vector3.Distance(transform.position, _targetPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * movementSmoothing);
        }
        else
        {
            transform.position = _targetPosition;
        }

        HandleRotation();
        HandleZoom();
    }

    private void HandleDragPan()
    {
        // Перевіряємо натискання середньої кнопки миші
        if (IsMiddleMouseButtonHeld())
        {
            isDragPanning = true;

            // Отримуємо поточну позицію миші та її дельту
            Vector2 mousePos = GetMousePosition();
            Vector2 mouseDelta = GetMouseDelta();

            // Якщо мишка не рухалась, нічого не робимо
            if (mouseDelta == Vector2.zero) return;

            // 1. Створюємо промінь від ПОТОЧНОЇ позиції миші
            Ray rayCurrent = Camera.main.ScreenPointToRay(mousePos);

            // 2. Створюємо промінь від ПОПЕРЕДНЬОЇ позиції миші (поточна - дельта)
            Ray rayPrevious = Camera.main.ScreenPointToRay(mousePos - mouseDelta);

            // 3. Знаходимо точки перетину обох променів з площиною землі (y=0)
            float enterCurrent, enterPrevious;

            if (_groundPlane.Raycast(rayCurrent, out enterCurrent) && _groundPlane.Raycast(rayPrevious, out enterPrevious))
            {
                Vector3 worldPosCurrent = rayCurrent.GetPoint(enterCurrent);
                Vector3 worldPosPrevious = rayPrevious.GetPoint(enterPrevious);

                // 4. Вектор зсуву - це різниця між тим, де мишка БУЛА на землі, і де вона Є зараз
                Vector3 worldDelta = worldPosPrevious - worldPosCurrent;

                // Застосовуємо до цільової позиції
                _targetPosition += worldDelta * dragSensitivity;
            }
        }
        else
        {
            isDragPanning = false;
        }
    }

    private void HandleMovement()
    {
        inputMoveDirection = GetMoveInputCameraDirections();

        Vector3 moveVector = transform.forward * inputMoveDirection.z + transform.right * inputMoveDirection.x;
        moveVector.y = 0;

        // Додаємо рух WASD до тієї ж цільової позиції
        _targetPosition += moveVector.normalized * moveSpeed * Time.deltaTime;
    }

    private void HandleRotation()
    {
        inputRorateDirection.y = GetCameraRotateAmount();
        transform.eulerAngles += inputRorateDirection * rotationSpeed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        // --- БЛОКУВАННЯ ЗУМУ ПРИ ПЕРЕТЯГУВАННІ ---
        if (isDragPanning) return;

        float zoomIncreaseAmount = 1f;
        targetFollowOffset.y += GetCameraZoomAmount() * zoomIncreaseAmount;

        targetFollowOffset.y = Mathf.Clamp(targetFollowOffset.y, MIN_FOLLOW_Y_OFFSET, MAX_FOLLOW_Y_OFFSET);
        float zoomSpeed = 5f;
        cinemachineFollow.FollowOffset = Vector3.Lerp(cinemachineFollow.FollowOffset, targetFollowOffset, zoomSpeed * Time.deltaTime);
    }

    // --- HELPER METHODS FOR INPUT SYSTEM AGNOSTICISM ---

    private bool IsMiddleMouseButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.middleButton.isPressed;
#else
        return Input.GetMouseButton(2);
#endif
    }

    private Vector2 GetMouseDelta()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.delta.ReadValue() : Vector2.zero;
#else
        return new Vector2(Input.GetAxis("Mouse X"), Input.GetAxis("Mouse Y"));
#endif
    }

    // --- EXISTING HELPER METHODS ---

    public Vector2 GetMousePosition()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null ? Mouse.current.position.ReadValue() : Vector2.zero;
#else
        return Input.mousePosition;
#endif
    }

    public Vector3 GetMoveInputCameraDirections()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 inputDir = playerInputActions.Player.CameraMovement.ReadValue<Vector2>();
        return new Vector3(inputDir.x, 0, inputDir.y);
#else
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
#endif
    }

    public bool IsMouseButtonDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return playerInputActions.Player.Click.WasPressedThisFrame();
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public float GetCameraRotateAmount()
    {
#if ENABLE_INPUT_SYSTEM
        return playerInputActions.Player.CameraRotate.ReadValue<float>();
#else
        float rotateAmount = 0f;

        if (Input.GetKey(KeyCode.Q))
            rotateAmount = -1f;

        else if (Input.GetKey(KeyCode.E))
            rotateAmount = 1f;
        else
            rotateAmount = 0f;

        return rotateAmount;
#endif
    }

    public float GetCameraZoomAmount()
    {
#if ENABLE_INPUT_SYSTEM
        return playerInputActions.Player.CameraZoom.ReadValue<float>();
#else
        float zoomAmount = 0f;
        if (Input.mouseScrollDelta.y > 0)
        {
            zoomAmount = -1f;
        }
        if (Input.mouseScrollDelta.y < 0)
        {
            zoomAmount = +1f;
        }

        return zoomAmount;
#endif
    }
}