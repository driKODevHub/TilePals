using UnityEngine;

public class ShapePreview : MonoBehaviour
{
    [SerializeField] private GameObject previewObject;
    [SerializeField] private Material canPlaceMaterial;
    [SerializeField] private Material cannotPlaceMaterial;

    private MeshRenderer previewRenderer;

    void Awake()
    {
        if (previewObject == null)
        {
            previewObject = this.gameObject;
            Debug.LogWarning("PreviewObject not explicitly assigned in ShapePreview. Using this GameObject as previewObject.");
        }

        // Отримуємо MeshRenderer з дочірніх об'єктів previewObject
        previewRenderer = previewObject.GetComponentInChildren<MeshRenderer>();
        if (previewRenderer == null)
        {
            Debug.LogError("PreviewObject (or its children) needs a MeshRenderer component for ShapePreview to work!");
        }

        if (canPlaceMaterial == null) Debug.LogError("CanPlaceMaterial is not assigned in ShapePreview!");
        if (cannotPlaceMaterial == null) Debug.LogError("CannotPlaceMaterial is not assigned in ShapePreview!");
    }

    void Update()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        RaycastHit hit;

        // Перевіряємо наявність GameManager та GridManager перед тим, як щось робити
        if (GameManagerOld.Instance == null || GridManager.Instance == null)
        {
            if (previewObject != null) previewObject.SetActive(false);
            return;
        }
        else
        {
            // Якщо менеджери існують, переконаємося, що previewObject активний
            if (previewObject != null && !previewObject.activeSelf) previewObject.SetActive(true);
        }

        if (Physics.Raycast(ray, out hit))
        {
            // Отримуємо координати сітки з точки зіткнення
            int x = Mathf.FloorToInt(hit.point.x);
            int y = Mathf.FloorToInt(hit.point.z);

            // Отримуємо поточні візуальні розміри та поворот з GameManager
            int displayWidth = GameManagerOld.Instance.CurrentWidth;
            int displayHeight = GameManagerOld.Instance.CurrentHeight;
            int rotationDegrees = GameManagerOld.Instance.CurrentRotationDegrees;

            // Отримуємо оригінальні розміри для логіки перевірки GridManager
            int originalShapeWidth = GameManagerOld.Instance.originalWidth;
            int originalShapeHeight = GameManagerOld.Instance.originalHeight;

            if (previewObject != null)
            {
                // Встановлюємо позицію preview точно так само, як і в GameManager
                // (x, 0.01f, y) відповідає нижньому лівому куту прев'ю, завдяки правильному налаштуванню півоту.
                previewObject.transform.position = new Vector3(x, 0.01f, y); // Трохи піднімаємо, щоб було видно над сіткою

                // Застосовуємо поворот до батьківського об'єкта прев'ю.
                previewObject.transform.rotation = Quaternion.Euler(0, rotationDegrees, 0);

                // ***** ЗМІНЕНО ТУТ: Масштабуємо ДОЧІРНІЙ ВІЗУАЛЬНИЙ ОБ'ЄКТ прев'ю *****
                if (previewRenderer != null)
                {
                    previewRenderer.transform.localScale = new Vector3(displayWidth, 0.1f, displayHeight); // Висота 0.1f для тонкого прев'ю
                }
                // ******************************************************************
            }

            // Перевіряємо, чи можна розмістити шейп, передаючи ОРИГІНАЛЬНІ розміри та кут повороту.
            // GridManager обчислить зайняті клітинки, враховуючи поворот.
            bool canPlace = GridManager.Instance.CanPlaceShape(x, y, originalShapeWidth, originalShapeHeight, rotationDegrees);

            // Оновлюємо матеріал прев'ю залежно від того, чи можна розмістити шейп
            if (previewRenderer != null && canPlaceMaterial != null && cannotPlaceMaterial != null)
            {
                previewRenderer.material = canPlace ? canPlaceMaterial : cannotPlaceMaterial;
            }
        }
        else
        {
            // Якщо промінь не зіткнувся ні з чим (миша не над сіткою), ховаємо прев'ю
            if (previewObject != null)
            {
                previewObject.SetActive(false);
            }
        }
    }
}