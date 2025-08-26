using UnityEngine;

public class TopDownCameraControl : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float moveSpeed = 10f; // Швидкість переміщення камери
    [SerializeField] private float panSpeed = 0.5f; // Швидкість панорамування мишею
    [SerializeField] private float smoothTime = 0.15f; // Час для згладжування руху

    [Header("Zoom Settings")]
    [SerializeField] private float zoomSpeed = 100f; // Швидкість зумування скролом
    [SerializeField] private float minZoom = 5f; // Мінімальна висота камери (максимальний зум)
    [SerializeField] private float maxZoom = 50f; // Максимальна висота камери (мінімальний зум)
    //[SerializeField] private float zoomSmoothTime = 0.1f; // Час для згладжування зуму

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 90f; // Швидкість обертання (градусів на секунду)
    [SerializeField] private float rotationSmoothTime = 0.1f; // Час для згладжування обертання

    private Vector3 targetPosition;
    private float targetYPosition; // Для зуму
    private float targetYRotation; // Для обертання

    private Vector3 currentVelocity = Vector3.zero;
    //private float currentZoomVelocity = 0f;
    private float currentRotationVelocity = 0f;

    private Vector3 lastMousePosition;
    private bool isPanning = false;

    void Start()
    {
        // Ініціалізуємо цільові значення поточними значеннями камери
        targetPosition = transform.position;
        targetYPosition = transform.position.y;
        targetYRotation = transform.eulerAngles.y; // Отримуємо поточне обертання по Y
    }

    void Update()
    {
        HandleMovementInput();
        HandlePanInput();
        HandleZoomInput();
        HandleRotationInput();

        SmoothlyUpdateCamera();
    }

    private void HandleMovementInput()
    {
        Vector3 moveDirection = Vector3.zero;

        if (Input.GetKey(KeyCode.W))
        {
            moveDirection += transform.forward; // Рух вперед відносно камери
        }
        if (Input.GetKey(KeyCode.S))
        {
            moveDirection -= transform.forward; // Рух назад відносно камери
        }
        if (Input.GetKey(KeyCode.A))
        {
            moveDirection -= transform.right; // Рух вліво відносно камери
        }
        if (Input.GetKey(KeyCode.D))
        {
            moveDirection += transform.right; // Рух вправо відносно камери
        }

        // Ігноруємо вертикальну складову, щоб рух був тільки по горизонталі
        moveDirection.y = 0;
        moveDirection.Normalize(); // Нормалізуємо, щоб діагональний рух не був швидшим

        // Оновлюємо цільову позицію для WASD
        targetPosition += moveDirection * moveSpeed * Time.deltaTime;
    }

    private void HandlePanInput()
    {
        if (Input.GetMouseButtonDown(2)) // 2 - це середня кнопка миші
        {
            isPanning = true;
            lastMousePosition = Input.mousePosition;
        }

        if (Input.GetMouseButtonUp(2))
        {
            isPanning = false;
        }

        if (isPanning)
        {
            Vector3 deltaMouse = Input.mousePosition - lastMousePosition;

            // Перетворюємо рух миші в рух камери в 3D просторі.
            // Помножте на PanSpeed та від'ємні значення для інверсії напрямку, якщо потрібно.
            // Припускаємо, що камера дивиться вниз, тому рух по X миші відповідає руху по X світу,
            // а рух по Y миші відповідає руху по Z світу (вперед/назад).
            Vector3 panDirection = new Vector3(-deltaMouse.x * panSpeed, 0, -deltaMouse.y * panSpeed);

            // Застосовуємо panDirection відносно поточного обертання камери
            targetPosition += transform.TransformDirection(panDirection);

            lastMousePosition = Input.mousePosition;
        }
    }

    private void HandleZoomInput()
    {
        float scroll = Input.GetAxis("Mouse ScrollWheel");
        if (scroll != 0)
        {
            // Змінюємо цільову Y-позицію для зуму.
            // Помножуємо на -1, бо скрол вгору зазвичай зменшує значення Y (збільшує зум).
            targetYPosition -= scroll * zoomSpeed * Time.deltaTime;
            targetYPosition = Mathf.Clamp(targetYPosition, minZoom, maxZoom);
        }
    }

    private void HandleRotationInput()
    {
        float rotationAmount = 0f;
        if (Input.GetKey(KeyCode.Q))
        {
            rotationAmount -= 1f;
        }
        if (Input.GetKey(KeyCode.E))
        {
            rotationAmount += 1f;
        }

        targetYRotation += rotationAmount * rotationSpeed * Time.deltaTime;
    }

    private void SmoothlyUpdateCamera()
    {
        // Плавно інтерполюємо позицію
        transform.position = Vector3.SmoothDamp(transform.position, new Vector3(targetPosition.x, targetYPosition, targetPosition.z), ref currentVelocity, smoothTime);

        // Плавно інтерполюємо обертання по Y
        float smoothedYRotation = Mathf.SmoothDampAngle(transform.eulerAngles.y, targetYRotation, ref currentRotationVelocity, rotationSmoothTime);
        transform.rotation = Quaternion.Euler(transform.eulerAngles.x, smoothedYRotation, transform.eulerAngles.z);
    }
}