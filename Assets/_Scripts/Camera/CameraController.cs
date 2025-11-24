using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;


public class CameraController : MonoBehaviour
{
    private const float MIN_FOLLOW_Y_OFFSET = 2f;
    private const float MAX_FOLLOW_Y_OFFSET = 50f;

    [SerializeField] CinemachineCamera cinemachineCamera;

    [SerializeField] float moveSpeed;
    [SerializeField] float rotationSpeed;

    private CinemachineFollow cinemachineFollow;
    private Vector3 inputMoveDirection;
    private Vector3 inputRorateDirection;
    private Vector3 targetFollowOffset;

    private void Awake()
    {

        playerInputActions = new PlayerInputActions();
        playerInputActions.Player.Enable();
    }

    private void Start()
    {
        cinemachineFollow = cinemachineCamera.GetComponent<CinemachineFollow>();
        targetFollowOffset = cinemachineFollow.FollowOffset;
    }

    private void Update()
    {
        // --- ЅЋќ ”¬јЌЌя  јћ≈–» ѕ–» ѕј”«≤ ---
        if (PauseManager.Instance != null && PauseManager.Instance.IsPaused) return;

        HandleMovement();
        HandleRotation();
        HandleZoom();
    }

    private void HandleMovement()
    {
        inputMoveDirection = GetMoveInputCameraDirections();

        Vector3 moveVector = transform.forward * inputMoveDirection.z + transform.right * inputMoveDirection.x;
        // ¬икористовуЇмо unscaledDeltaTime, щоб камера не застр€гала, €кщо ми захочемо ан≥мувати щось у меню, 
        // але оск≥льки ми повертаЇмось з Update при IsPaused, тут можна лишити deltaTime
        transform.position += moveVector * moveSpeed * Time.deltaTime;
    }
    private void HandleRotation()
    {
        inputRorateDirection.y = GetCameraRotateAmount();

        transform.eulerAngles += inputRorateDirection * rotationSpeed * Time.deltaTime;
    }
    private void HandleZoom()
    {
        float zoomIncreaseAmount = 1f;
        targetFollowOffset.y += GetCameraZoomAmount() * zoomIncreaseAmount;

        targetFollowOffset.y = Mathf.Clamp(targetFollowOffset.y, MIN_FOLLOW_Y_OFFSET, MAX_FOLLOW_Y_OFFSET);
        float zoomSpeed = 5f;
        cinemachineFollow.FollowOffset = Vector3.Lerp(cinemachineFollow.FollowOffset, targetFollowOffset, zoomSpeed * Time.deltaTime);
    }



    private PlayerInputActions playerInputActions;


    public Vector2 GetMousePosition()
    {
#if USE_NEW_INPUT_SYSTEM
        return Mouse.current.position.ReadValue();
#else
        return Input.mousePosition;
#endif
    }

    public Vector3 GetMoveInputCameraDirections()
    {
#if USE_NEW_INPUT_SYSTEM
        Vector2 inputDir = playerInputActions.Player.CameraMovement.ReadValue<Vector2>();
        return new Vector3(inputDir.x, 0, inputDir.y);
#else
        return new Vector3(Input.GetAxisRaw("Horizontal"), 0f, Input.GetAxisRaw("Vertical"));
#endif
    }

    public bool IsMouseButtonDownThisFrame()
    {
#if USE_NEW_INPUT_SYSTEM
        return playerInputActions.Player.Click.WasPressedThisFrame();
#else
        return Input.GetMouseButtonDown(0);
#endif
    }

    public float GetCameraRotateAmount()
    {
#if USE_NEW_INPUT_SYSTEM
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
#if USE_NEW_INPUT_SYSTEM
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