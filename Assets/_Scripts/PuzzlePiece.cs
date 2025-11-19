using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Налаштування фігури")]
    [SerializeField] private PlacedObjectTypeSO pieceTypeSO;

    [Header("Налаштування візуалу")]
    [SerializeField] private List<MeshRenderer> meshesToColor;

    [Header("Налаштування обертання")]
    [SerializeField] private float rotationSpeed = 360f; // Трохи пришвидшимо для кращого відчуття

    [Header("Посилання на компоненти особистості")]
    [SerializeField] private FacialExpressionController facialController;

    public PlacedObjectTypeSO PieceTypeSO => pieceTypeSO;
    public FacialExpressionController FacialController => facialController;
    public PlacedObjectTypeSO.Dir CurrentDirection { get; private set; } = PlacedObjectTypeSO.Dir.Down;
    public bool IsPlaced { get; private set; } = false;
    public bool IsRotating { get; private set; } = false;
    public bool IsOffGrid { get; private set; } = false;
    public Vector2Int OffGridOrigin { get; private set; }
    public PlacedObject PlacedObjectComponent { get; private set; }

    // Зміщення відносної клітинки (на яку клікнули) до точки прив'язки фігури (0,0)
    public Vector2Int ClickOffset { get; set; }

    private Coroutine _rotationCoroutine;
    private Dictionary<MeshRenderer, Material[]> _originalMaterials;
    private bool _isInvalidMaterialApplied = false;

    private void Awake()
    {
        CacheOriginalMaterials();
        if (facialController == null)
        {
            facialController = GetComponentInChildren<FacialExpressionController>();
        }
    }

    private void CacheOriginalMaterials()
    {
        _originalMaterials = new Dictionary<MeshRenderer, Material[]>();
        if (meshesToColor == null || meshesToColor.Count == 0) return;

        foreach (var meshRenderer in meshesToColor)
        {
            if (meshRenderer != null)
            {
                _originalMaterials[meshRenderer] = meshRenderer.materials;
            }
        }
    }

    public void SetTemperamentMaterial(Material temperamentMaterial)
    {
        if (temperamentMaterial == null || meshesToColor == null) return;

        foreach (var meshRenderer in meshesToColor)
        {
            if (meshRenderer != null)
            {
                var newMaterials = new Material[meshRenderer.materials.Length];
                for (int i = 0; i < newMaterials.Length; i++)
                {
                    newMaterials[i] = temperamentMaterial;
                }
                meshRenderer.materials = newMaterials;
            }
        }
        CacheOriginalMaterials();
    }


    public void UpdatePlacementVisual(bool canPlace, Material invalidMaterial)
    {
        if (meshesToColor == null) return;

        if (!canPlace && !_isInvalidMaterialApplied)
        {
            foreach (var meshRenderer in meshesToColor)
            {
                if (meshRenderer != null)
                {
                    var newMaterials = new Material[meshRenderer.materials.Length];
                    for (int i = 0; i < newMaterials.Length; i++)
                    {
                        newMaterials[i] = invalidMaterial;
                    }
                    meshRenderer.materials = newMaterials;
                }
            }
            _isInvalidMaterialApplied = true;
        }
        else if (canPlace && _isInvalidMaterialApplied)
        {
            foreach (var meshRenderer in meshesToColor)
            {
                if (meshRenderer != null && _originalMaterials.ContainsKey(meshRenderer))
                {
                    meshRenderer.materials = _originalMaterials[meshRenderer];
                }
            }
            _isInvalidMaterialApplied = false;
        }
    }

    public void SetOffGrid(bool isOffGrid, Vector2Int origin = default)
    {
        IsOffGrid = isOffGrid;
        IsPlaced = false;
        if (isOffGrid)
        {
            OffGridOrigin = origin;
        }
    }

    public void StartSmoothRotation()
    {
        if (IsRotating) return;

        if (_rotationCoroutine != null)
        {
            StopCoroutine(_rotationCoroutine);
        }
        _rotationCoroutine = StartCoroutine(SmoothRotationCoroutine());
    }

    // --- ОНОВЛЕНИЙ МЕТОД ОБЕРТАННЯ ---
    private IEnumerator SmoothRotationCoroutine()
    {
        IsRotating = true;

        // 1. Отримуємо параметри сітки
        float cellSize = 1f;
        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        }

        // 2. Обчислюємо поточну точку 0,0 (Grid Origin) у світових координатах
        // transform.position = GridOrigin + VisualOffset
        Vector2Int currentRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 currentVisualOffset = new Vector3(currentRotationOffset.x, 0, currentRotationOffset.y) * cellSize;

        // Коригуємо Y, щоб не втратити висоту підйому фігури
        Vector3 currentGridOrigin = transform.position;
        currentGridOrigin.x -= currentVisualOffset.x;
        currentGridOrigin.z -= currentVisualOffset.z;

        // 3. Обчислюємо точку півота (Центр клітинки, за яку тримаємо)
        // Pivot = GridOrigin + ClickOffset * CellSize + HalfCellSize
        Vector3 clickOffsetVector = new Vector3(ClickOffset.x, 0, ClickOffset.y) * cellSize;
        Vector3 halfCellVector = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        Vector3 pivotPoint = currentGridOrigin + clickOffsetVector + halfCellVector;

        // 4. Налаштування обертання
        PlacedObjectTypeSO.Dir nextDirection = PlacedObjectTypeSO.GetNextDirencion(CurrentDirection);
        float targetAngle = pieceTypeSO.GetRotationAngle(nextDirection);
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        float angleToRotate = 90f;
        float totalRotation = 0f;

        while (totalRotation < angleToRotate)
        {
            float rotationAmount = Time.deltaTime * rotationSpeed;
            rotationAmount = Mathf.Min(rotationAmount, angleToRotate - totalRotation);

            // Обертаємо трансформ довкола нашої точки півота (клітинки під мишкою)
            transform.RotateAround(pivotPoint, Vector3.up, rotationAmount);

            totalRotation += rotationAmount;
            yield return null;
        }

        // 5. Фіналізація
        transform.rotation = targetRotation;
        CurrentDirection = nextDirection;

        // 6. Перерахунок ClickOffset
        // Після повороту фігура змістилася, але півот залишився на місці.
        // Нам треба знайти, які тепер координати (x, z) у цього півота відносно НОВОГО Origin фігури.

        Vector2Int newRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 newVisualOffset = new Vector3(newRotationOffset.x, 0, newRotationOffset.y) * cellSize;

        // Знаходимо де тепер теоретичний Origin
        Vector3 newGridOrigin = transform.position;
        newGridOrigin.x -= newVisualOffset.x;
        newGridOrigin.z -= newVisualOffset.z;

        // Вектор від Origin до Pivot
        Vector3 newOffsetVector = pivotPoint - newGridOrigin - halfCellVector;

        // Конвертуємо назад у координати сітки
        ClickOffset = new Vector2Int(
            Mathf.RoundToInt(newOffsetVector.x / cellSize),
            Mathf.RoundToInt(newOffsetVector.z / cellSize)
        );

        IsRotating = false;
        _rotationCoroutine = null;
    }

    public void SetPlaced(PlacedObject placedObjectComponent)
    {
        this.PlacedObjectComponent = placedObjectComponent;
        IsPlaced = (placedObjectComponent != null);
        if (IsPlaced)
        {
            IsOffGrid = false;
        }
    }

    public void UpdateTransform(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;

        float yAngle = Mathf.Round(rotation.eulerAngles.y);
        if (Mathf.Approximately(yAngle, 0)) CurrentDirection = PlacedObjectTypeSO.Dir.Down;
        else if (Mathf.Approximately(yAngle, 90)) CurrentDirection = PlacedObjectTypeSO.Dir.Left;
        else if (Mathf.Approximately(yAngle, 180)) CurrentDirection = PlacedObjectTypeSO.Dir.Up;
        else if (Mathf.Approximately(yAngle, 270)) CurrentDirection = PlacedObjectTypeSO.Dir.Right;
    }

    public void SetInitialRotation(PlacedObjectTypeSO.Dir direction)
    {
        CurrentDirection = direction;
        transform.rotation = Quaternion.Euler(0, pieceTypeSO.GetRotationAngle(direction), 0);
    }
}