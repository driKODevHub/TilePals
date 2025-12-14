using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    private const float MIN_FOLLOW_Y_OFFSET = 2f;
    private const float MAX_FOLLOW_Y_OFFSET = 50f;

    [SerializeField] CinemachineCamera cinemachineCamera;

    [Header("Feature Toggle")]
    [Tooltip("Вмикає або вимикає можливість обертати камеру.")]
    [SerializeField] private bool enableRotation = true;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 10f;
    [SerializeField] float rotationSpeed = 100f;

    [Header("Zoom Settings")]
    [Tooltip("Зум (висота камери), який встановлюється на початку рівня.")]
    [SerializeField] float defaultZoom = 20f;

    [Header("Drag Pan Settings")]
    [Tooltip("Множник швидкості перетягування. 1.0 = 1:1 рух мишки до землі.")]
    [SerializeField] float dragSensitivity = 1.0f;
    [Tooltip("Чим вище значення, тим швидше камера зупиняється. Низьке значення = більше інерції (плавніше).")]
    [SerializeField] float movementSmoothing = 10f;

    [Header("Focus Settings")]
    [Tooltip("Швидкість (згладжування) руху камери при автоматичному фокусуванні (старт/перемога). Менше = плавніше.")]
    [SerializeField] float focusSmoothing = 5f;

    [Header("Bounds Settings")]
    [Tooltip("Вмикає обмеження руху камери.")]
    [SerializeField] private bool enableBounds = true;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true; // Вимкни, коли знайдеш проблему

    // --- ПУБЛІЧНЕ ПОЛЕ ДЛЯ EDITOR SCRIPT ---
    [HideInInspector] public GridDataSO activeGridData;

    // Приватні параметри меж (кешовані)
    private Vector2 _boundsCenter;
    private Vector2 _boundsSize;
    private float _boundsRotationY;

    private CinemachineFollow cinemachineFollow;
    private Vector3 inputMoveDirection;
    private Vector3 inputRorateDirection;
    private Vector3 targetFollowOffset;

    private bool isDragPanning = false;
    private bool _isFocusing = false;
    private Vector3 _targetPosition;

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

        _targetPosition = transform.position;

        SetCameraBounds(Vector2.zero, new Vector2(50, 50), 0f);
    }

    public void SetCameraBounds(Vector2 center, Vector2 size, float rotationY)
    {
        _boundsCenter = center;
        _boundsSize = size;
        _boundsRotationY = rotationY;
    }

    public void FocusOnLevel(bool immediate)
    {
        _targetPosition = new Vector3(_boundsCenter.x, transform.position.y, _boundsCenter.y);
        targetFollowOffset.y = defaultZoom;

        if (immediate)
        {
            transform.position = _targetPosition;
            cinemachineFollow.FollowOffset = targetFollowOffset;
            _isFocusing = false;
        }
        else
        {
            _isFocusing = true;
        }
    }

    private void Update()
    {
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        if (GameManager.Instance != null && GameManager.Instance.IsLevelActive)
        {
            HandleResetCamera();
            HandleDragPan();
            HandleMovement();
            HandleRotation();
            HandleZoom();
        }
        else
        {
            isDragPanning = false;
        }

        // --- PHYSICS ---
        if (enableBounds)
        {
            _targetPosition = ClampPositionToOBB(_targetPosition, _boundsCenter, _boundsSize, _boundsRotationY);
        }

        float currentSmoothing = _isFocusing ? focusSmoothing : movementSmoothing;

        if (Vector3.Distance(transform.position, _targetPosition) > 0.001f)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * currentSmoothing);
        }
        else
        {
            transform.position = _targetPosition;
            _isFocusing = false;
        }

        cinemachineFollow.FollowOffset = Vector3.Lerp(cinemachineFollow.FollowOffset, targetFollowOffset, 5f * Time.deltaTime);
    }

    private Vector3 ClampPositionToOBB(Vector3 targetPos, Vector2 center, Vector2 size, float angle)
    {
        Vector3 worldCenter = new Vector3(center.x, 0, center.y);
        Vector3 dir = targetPos - worldCenter;

        Quaternion inverseRot = Quaternion.Euler(0, -angle, 0);
        Vector3 localPos = inverseRot * dir;

        float extentsX = size.x / 2f;
        float extentsZ = size.y / 2f;

        localPos.x = Mathf.Clamp(localPos.x, -extentsX, extentsX);
        localPos.z = Mathf.Clamp(localPos.z, -extentsZ, extentsZ);

        Quaternion rot = Quaternion.Euler(0, angle, 0);
        Vector3 resultPos = worldCenter + (rot * localPos);

        resultPos.y = targetPos.y;

        return resultPos;
    }

    private void HandleResetCamera()
    {
        if (IsMiddleMouseButtonDownThisFrame())
        {
            FocusOnLevel(false);
        }
    }

    private void HandleDragPan()
    {
        // 1. Перевіряємо, чи затиснута ЛКМ
        if (!IsLeftMouseButtonHeld())
        {
            isDragPanning = false;
            return;
        }

        // 2. Якщо ми ще не почали драг (тільки натиснули), перевіряємо чи можна почати
        if (!isDragPanning)
        {
            // ПЕРЕВІРКА 1: UI
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (showDebugLogs) Debug.Log("Camera Drag Blocked: Pointer is over UI");
                return;
            }

            // ПЕРЕВІРКА 2: Взаємодія з грою (кіт, тулз)
            if (PuzzleManager.Instance != null && PuzzleManager.Instance.IsInteracting)
            {
                if (showDebugLogs) Debug.Log("Camera Drag Blocked: Interacting with Puzzle Piece");
                return;
            }
        }

        // 3. Логіка драгу
        isDragPanning = true;
        _isFocusing = false;

        Vector2 mousePos = GetMousePosition();
        Vector2 mouseDelta = GetMouseDelta();

        if (mouseDelta == Vector2.zero) return;

        Ray rayCurrent = Camera.main.ScreenPointToRay(mousePos);
        Ray rayPrevious = Camera.main.ScreenPointToRay(mousePos - mouseDelta);

        float enterCurrent, enterPrevious;

        if (_groundPlane.Raycast(rayCurrent, out enterCurrent) && _groundPlane.Raycast(rayPrevious, out enterPrevious))
        {
            Vector3 worldPosCurrent = rayCurrent.GetPoint(enterCurrent);
            Vector3 worldPosPrevious = rayPrevious.GetPoint(enterPrevious);
            Vector3 worldDelta = worldPosPrevious - worldPosCurrent;

            _targetPosition += worldDelta * dragSensitivity;
        }
    }

    private void HandleMovement()
    {
        inputMoveDirection = GetMoveInputCameraDirections();

        if (inputMoveDirection != Vector3.zero)
        {
            _isFocusing = false;
            Vector3 moveVector = transform.forward * inputMoveDirection.z + transform.right * inputMoveDirection.x;
            moveVector.y = 0;
            _targetPosition += moveVector.normalized * moveSpeed * Time.deltaTime;
        }
    }

    private void HandleRotation()
    {
        if (!enableRotation) return;
        inputRorateDirection.y = GetCameraRotateAmount();
        transform.eulerAngles += inputRorateDirection * rotationSpeed * Time.deltaTime;
    }

    private void HandleZoom()
    {
        if (isDragPanning) return;
        float zoomAmount = GetCameraZoomAmount();
        if (Mathf.Abs(zoomAmount) > 0.01f)
        {
            _isFocusing = false;
            float zoomIncreaseAmount = 1f;
            targetFollowOffset.y += zoomAmount * zoomIncreaseAmount;
            targetFollowOffset.y = Mathf.Clamp(targetFollowOffset.y, MIN_FOLLOW_Y_OFFSET, MAX_FOLLOW_Y_OFFSET);
        }
    }

    // --- HELPER METHODS ---
    private bool IsLeftMouseButtonHeld()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.leftButton.isPressed;
#else
        return Input.GetMouseButton(0);
#endif
    }

    private bool IsMiddleMouseButtonDownThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        return Mouse.current != null && Mouse.current.middleButton.wasPressedThisFrame;
#else
        return Input.GetMouseButtonDown(2);
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

    public float GetCameraRotateAmount()
    {
        float rotateAmount = 0f;
#if ENABLE_INPUT_SYSTEM
        if (Keyboard.current != null)
        {
            if (Keyboard.current.leftBracketKey.isPressed) rotateAmount = -1f;
            else if (Keyboard.current.rightBracketKey.isPressed) rotateAmount = 1f;
        }
#else
        if (Input.GetKey(KeyCode.LeftBracket)) rotateAmount = -1f;
        else if (Input.GetKey(KeyCode.RightBracket)) rotateAmount = 1f;
#endif
        return rotateAmount;
    }

    public float GetCameraZoomAmount()
    {
#if ENABLE_INPUT_SYSTEM
        return playerInputActions.Player.CameraZoom.ReadValue<float>();
#else
        float zoomAmount = 0f;
        if (Input.mouseScrollDelta.y > 0) zoomAmount = -1f;
        if (Input.mouseScrollDelta.y < 0) zoomAmount = +1f;
        return zoomAmount;
#endif
    }
}