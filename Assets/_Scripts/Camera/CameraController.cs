using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.EventSystems;

public class CameraController : MonoBehaviour
{
    public static CameraController Instance { get; private set; }

    [SerializeField]private float minFollowOffsetY = 2f;
    [SerializeField]private float maxFollowOffsetY = 50f;

    [SerializeField] CinemachineCamera cinemachineCamera;

    [Header("Feature Toggle")]
    [Tooltip("����� ��� ������ ��������� �������� ������.")]
    [SerializeField] private bool enableRotation = true;

    [Header("Movement Settings")]
    [SerializeField] float moveSpeed = 10f;
    [SerializeField] float rotationSpeed = 100f;

    [Header("Zoom Settings")]
    [Tooltip("��� (������ ������), ���� �������������� �� ������� ����.")]
    [SerializeField] float defaultZoom = 20f;

    [Header("Drag Pan Settings")]
    [Tooltip("������� �������� �������������. 1.0 = 1:1 ��� ����� �� ����.")]
    [SerializeField] float dragSensitivity = 1.0f;
    [Tooltip("��� ���� ��������, ��� ������ ������ �����������. ������ �������� = ����� ������� (�������).")]
    [SerializeField] float movementSmoothing = 10f;

    [Header("Focus Settings")]
    [Tooltip("�������� (������������) ���� ������ ��� ������������� ���������� (�����/��������). ����� = �������.")]
    [SerializeField] float focusSmoothing = 5f;

    [Header("Bounds Settings")]
    [Tooltip("����� ��������� ���� ������.")]
    [SerializeField] private bool enableBounds = true;

    [Tooltip("���� ��������, ��� ���� �������������� �������� �� ����� ���� (���� ����), ��� �� ������ '�������' �� ������ �����.")]
    [SerializeField] private bool limitByFieldOfView = true;

    [Tooltip("������� ������� �� ���� ��� ���������� ��������. �������, ���� ������ ������ �� ��� ����� ��� ��������.")]
    [SerializeField] private float viewBoundsMarginFactor = 1.0f;

    [Header("Debug")]
    [SerializeField] private bool showDebugLogs = true; // ������, ���� ������� ��������

    // --- ���˲��� ���� ��� EDITOR SCRIPT ---
    [HideInInspector] public GridDataSO activeGridData;

    // ������� ��������� ��� (�������)
    private Vector2 _boundsCenter;
    private Vector2 _boundsSize;
    private float _boundsRotationY;

    private CinemachineFollow cinemachineFollow;
    private Vector3 inputMoveDirection;
    private Vector3 inputRorateDirection;
    private Vector3 targetFollowOffset;

    private bool isDragPanning = false; public bool IsDragPanning => isDragPanning;
    private bool _isFocusing = false;
    private Vector3 _targetPosition;

    private Plane _groundPlane = new Plane(Vector3.up, Vector3.zero);

    [Header("Navigation Switcher")]
    [SerializeField] private BoardSwitcher cameraSwitcherPrefab;
    [SerializeField] private float switcherActivationDelay = 0.05f;
    private BoardSwitcher _activeSwitcher;
    private float _pushingEdgeTime;
    private bool _isEdgeLatched;
    private Vector3 _latchedDirection;
    private Vector3 _currentDragDeltaWorld; // To pass drag info to EdgeNav
    private PuzzleBoard _pendingTargetBoard;

    private PlayerInputActions playerInputActions;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;

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

    public void FocusOnBoard(PuzzleBoard board)
    {
        if (board == null) return;
        
        if (board.LevelData != null)
        {
            // Note: cameraBoundsCenter is relative to board origin? 
            // Or absolute? LevelData usually defines it relative to the board.
            // Let's assume relative to pivot.
            Vector2 absoluteCenter = board.LevelData.cameraBoundsCenter + new Vector2(board.Pivot.position.x, board.Pivot.position.z);
            SetCameraBounds(absoluteCenter, board.LevelData.cameraBoundsSize, board.LevelData.cameraBoundsYRotation);
            
            _targetPosition = new Vector3(absoluteCenter.x, transform.position.y, absoluteCenter.y);
        }
        else
        {
            _targetPosition = new Vector3(board.Pivot.position.x, transform.position.y, board.Pivot.position.z);
        }
        
        _isFocusing = true;
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
            HandleEdgeNavigation();
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
        // 1. Calculate Dynamic Margin based on Zoom
        float marginX = 0f;
        float marginZ = 0f;

        if (limitByFieldOfView)
        {
            // ������ ������� ������ ������ (zoom)
            float currentHeight = cinemachineFollow.FollowOffset.y;

            // ����������� ���������� ����� �������� �� ����
            // ��� ���� ������ -> ��� ����� �� ������ -> ��� ������ ������ ����� ������� �� ���� �����
            float visibleRadius = currentHeight * viewBoundsMarginFactor;

            // Aspect ratio correction (������ �������� ����� �� ������)
            float aspect = Camera.main != null ? Camera.main.aspect : 1.77f;

            marginX = visibleRadius * aspect; // ������
            marginZ = visibleRadius;          // ������ (�������)
        }

        Vector3 worldCenter = new Vector3(center.x, 0, center.y);
        Vector3 dir = targetPos - worldCenter;

        // Rotate into local space of the OBB
        Quaternion inverseRot = Quaternion.Euler(0, -angle, 0);
        Vector3 localPos = inverseRot * dir;

        // Calculate extents (half-sizes) minus the visual margin
        // �� ������� margin, ��� ����� ������ �� �� ����� �� ���� �������� �������, 
        // ��� "���������" �� ��� visual bounds.
        float allowedExtentX = Mathf.Max(0, (size.x / 2f) - marginX);
        float allowedExtentZ = Mathf.Max(0, (size.y / 2f) - marginZ);

        localPos.x = Mathf.Clamp(localPos.x, -allowedExtentX, allowedExtentX);
        localPos.z = Mathf.Clamp(localPos.z, -allowedExtentZ, allowedExtentZ);

        // Rotate back to world space
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
        if (!IsLeftMouseButtonHeld())
        {
            isDragPanning = false;
            return;
        }

        if (!isDragPanning)
        {
            if (EventSystem.current != null && EventSystem.current.IsPointerOverGameObject())
            {
                if (showDebugLogs) Debug.Log("Camera Drag Blocked: Pointer is over UI");
                return;
            }

            if (PuzzleManager.Instance != null && PuzzleManager.Instance.IsInteracting)
            {
                if (showDebugLogs) Debug.Log("Camera Drag Blocked: Interacting with Puzzle Piece");
                return;
            }
        }

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
            _currentDragDeltaWorld = worldDelta * dragSensitivity; // Store for EdgeNav
            _targetPosition += _currentDragDeltaWorld;
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
            targetFollowOffset.y = Mathf.Clamp(targetFollowOffset.y, minFollowOffsetY, maxFollowOffsetY);
        }
    }

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

    private void HandleEdgeNavigation()
    {
        if (!enableBounds) return;

        // 1. Calculate Total Attempted Movement Vector (World Space)
        Vector3 keyMove = (transform.forward * inputMoveDirection.z + transform.right * inputMoveDirection.x).normalized * moveSpeed * Time.deltaTime;
        Vector3 totalMove = keyMove;
        
        if (isDragPanning)
        {
            totalMove += _currentDragDeltaWorld;
            _currentDragDeltaWorld = Vector3.zero; // Consume it
        }

        // 2. Logic
        if (_isEdgeLatched)
        {
            // We are latched. We only unlatch if the user intentionally moves AWAY from the edge.
            // Check angle between 'totalMove' and '_latchedDirection'.
            
            bool isMovingAway = false;
            if (totalMove.sqrMagnitude > 0.001f)
            {
                // Dot Product: Positive = Same dir, Negative = Opposite.
                float dot = Vector3.Dot(totalMove.normalized, _latchedDirection);
                // If dot is negative, we are moving generally away.
                // Let's use a threshold to allow sliding along the edge? 
                // User said "go in reverse direction".
                if (dot < -0.1f) isMovingAway = true;
            }

            if (isMovingAway)
            {
                HideSwitcher(); // Unlatch
            }
            else
            {
                // Stay Latched. 
                // Maybe snap camera to edge to prevent jitter?
                // The main clamp handles visual constraints.
            }
        }
        else
        {
            // We are NOT latched. Check if we SHOUL be.
            if (totalMove == Vector3.zero) 
            {
                HideSwitcher();
                return;
            }

            // Predict if this move hits the edge
            // Logic: Clamp(Current + Move) != (Current + Move)
            Vector3 rawTarget = _targetPosition + totalMove;
            Vector3 clampedTarget = ClampPositionToOBB(rawTarget, _boundsCenter, _boundsSize, _boundsRotationY);

            // If we are pushing (Raw is significantly different from Clamped)
            if (Vector3.Distance(rawTarget, clampedTarget) > 0.01f)
            {
                _pushingEdgeTime += Time.deltaTime;

                if (_pushingEdgeTime >= switcherActivationDelay)
                {
                    // LATCH!
                    Vector3 pushDirection = (rawTarget - clampedTarget).normalized;
                    PuzzleBoard nearestBoard = FindNearestBoardInDirection(pushDirection);

                    if (nearestBoard != null)
                    {
                        ShowSwitcher(nearestBoard, clampedTarget, pushDirection);
                    }
                }
            }
            else
            {
                _pushingEdgeTime = 0f;
            }
        }
    }

    private PuzzleBoard FindNearestBoardInDirection(Vector3 dir)
    {
        if (LevelLoader.Instance == null) return null;
        
        var boards = LevelLoader.Instance.ActiveLocationBoards;
        var currentBoard = GridBuildingSystem.Instance.ActiveBoard; 
        
        PuzzleBoard bestBoard = null;
        float bestDist = float.MaxValue;
        
        // Use current bounds center as search origin. Flatten it.
        Vector3 currentCenter = new Vector3(_boundsCenter.x, 0, _boundsCenter.y);

        // Ensure direction is flat
        Vector3 flatDir = new Vector3(dir.x, 0, dir.z).normalized;

        foreach (var board in boards)
        {
            if (board == currentBoard) continue;
            
            // Use Grid Center instead of transform position
            Vector3 boardCenter = board.GetGridCenterWorldPosition();
            boardCenter.y = 0; // Flatten
            
            // Vector from current center to board
            Vector3 toBoard = boardCenter - currentCenter;
            toBoard.y = 0; // Ensure logic is purely 2D
            
            float dist = toBoard.magnitude;
            float angle = Vector3.Angle(flatDir, toBoard);
            
            // Stricter angle: 45 degrees (total 90 cone).
            if (angle < 45f) 
            {
                // Scoring: Favor proximity AND alignment.
                // A board perfectly aligned (0 deg) is preferred over one at 45 deg even if slightly further.
                float score = dist * (1.0f + (angle / 45.0f) * 0.5f); 

                if (score < bestDist)
                {
                    bestDist = score;
                    bestBoard = board;
                }
            }
        }
        return bestBoard;
    }

    private void ShowSwitcher(PuzzleBoard target, Vector3 position, Vector3 direction)
    {
        if (_activeSwitcher == null)
        {
            if (cameraSwitcherPrefab == null) return;
            _activeSwitcher = Instantiate(cameraSwitcherPrefab);
        }
        
        _activeSwitcher.gameObject.SetActive(true);
        _activeSwitcher.Initialize(target);
        
        // --- Refined Positioning: Visual Screen Edge ---
        // 1. Calculate Screen Direction regarding the camera center
        // Transform the push direction (world) into viewport space relative to center
        // Actually, simplest is to use the bounds center vs target logic or just the 'pushDirection' logic.
        // 'direction' is the world push vector.
        
        // We want to place the button at the edge of the screen in 'direction'.
        // Project world direction to screen space (2D).
        // A simple approximation for Top-Down/Iso:
        // Use camera.WorldToViewportPoint.
        
        Camera cam = Camera.main;
        if (cam != null)
        {
            // Project the direction vector onto the screen plane
            Vector3 camForward = cam.transform.forward;
            Vector3 camRight = cam.transform.right;
            Vector3 camUp = cam.transform.up;
            
            // Project 'direction' onto camera basis
            float x = Vector3.Dot(direction, camRight);
            float y = Vector3.Dot(direction, camUp); // For top-down, up is screen Y
            
            // Normalize 2D vector
            Vector2 screenDir = new Vector2(x, y).normalized;
            
            // Calculate Viewport Position (0-1)
            // Center is (0.5, 0.5). Edge is roughly at 0.5 distance? 
            // Let's go 0.45 to be safe inside.
            // But we need to handle aspect ratio if we want exact edge? 
            // Simplified: 0.5 + dir * 0.4.
            Vector2 viewportPos = new Vector2(0.5f, 0.5f) + screenDir * 0.4f;
            
            // Clamp to [0.1, 0.9] texturing safe area
            viewportPos.x = Mathf.Clamp(viewportPos.x, 0.1f, 0.9f);
            viewportPos.y = Mathf.Clamp(viewportPos.y, 0.1f, 0.9f);
            
            // Raycast to Ground (Y=0)
            Ray ray = cam.ViewportPointToRay(new Vector3(viewportPos.x, viewportPos.y, 0));
            float enter;
            if (_groundPlane.Raycast(ray, out enter))
            {
                Vector3 screenEdgeWorldPos = ray.GetPoint(enter);
                screenEdgeWorldPos.y = 0f; // Ensure floor
                _activeSwitcher.transform.position = screenEdgeWorldPos;
            }
            else
            {
                // Fallback
                Vector3 flatPosition = position;
                flatPosition.y = 0f;
                _activeSwitcher.transform.position = flatPosition + direction * 3.0f; 
            }
        }
        else
        {
             // Fallback
             Vector3 flatPosition = position;
             flatPosition.y = 0f;
             _activeSwitcher.transform.position = flatPosition + direction * 3.0f;
        }

        // Rotation: Point towards the target board center
        Vector3 toTarget = target.transform.position - _activeSwitcher.transform.position;
        toTarget.y = 0;
        if (toTarget != Vector3.zero)
        {
            _activeSwitcher.transform.rotation = Quaternion.LookRotation(toTarget);
        }

        _isEdgeLatched = true;
        _latchedDirection = direction;
    }

    private void HideSwitcher()
    {
        if (_activeSwitcher != null)
        {
            _activeSwitcher.gameObject.SetActive(false);
        }
        _pushingEdgeTime = 0f;
        _isEdgeLatched = false;
    }
}
