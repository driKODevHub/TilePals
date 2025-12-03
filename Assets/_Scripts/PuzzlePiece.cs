using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class PuzzlePiece : MonoBehaviour
{
    [Header("Налаштування фігури")]
    [SerializeField] private PlacedObjectTypeSO pieceTypeSO;

    [Header("Налаштування візуалу (Колір/Темперамент)")]
    [SerializeField] private List<MeshRenderer> meshesToColor;

    [Header("Налаштування Аутлайну (Linework)")]
    [Tooltip("Виберіть шар, на якому працює ефект аутлайну.")]
    [SerializeField] private LayerMask outlineLayerMask;
    [Tooltip("Звичайні MeshRenderers, які мають підсвічуватись.")]
    [SerializeField] private List<MeshRenderer> outlineMeshRenderers;
    [Tooltip("SkinnedMeshRenderers (хвости, вуха), які мають підсвічуватись.")]
    [SerializeField] private List<SkinnedMeshRenderer> outlineSkinnedMeshRenderers;

    [Header("Налаштування обертання")]
    [SerializeField] private float rotationSpeed = 360f;

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

    // --- ЗМІННІ ДЛЯ АУТЛАЙНУ ---
    private int _outlineLayerIndex;
    private Dictionary<Renderer, int> _originalLayers = new Dictionary<Renderer, int>();
    private bool _isOutlineActive = false;
    private bool _isOutlineLocked = false;
    // ----------------------------

    private Coroutine _rotationCoroutine;
    private Dictionary<MeshRenderer, Material[]> _originalMaterials;
    private bool _isInvalidMaterialApplied = false;

    private void Awake()
    {
        CacheOriginalMaterials();
        InitializeOutlineData();

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

    // --- ЛОГІКА АУТЛАЙНУ ---
    private void InitializeOutlineData()
    {
        // Перетворюємо бітову маску в індекс шару (0-31)
        int maskValue = outlineLayerMask.value;
        if (maskValue > 0)
        {
            _outlineLayerIndex = 0;
            while ((maskValue & 1) == 0)
            {
                maskValue >>= 1;
                _outlineLayerIndex++;
            }
        }
        else
        {
            Debug.LogWarning($"Outline Layer Mask is not set for {name}. Defaulting to layer 0.");
            _outlineLayerIndex = 0;
        }

        CacheRendererLayers(outlineMeshRenderers);
        CacheRendererLayers(outlineSkinnedMeshRenderers);
    }

    private void CacheRendererLayers<T>(List<T> renderers) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null && !_originalLayers.ContainsKey(r))
            {
                _originalLayers[r] = r.gameObject.layer;
            }
        }
    }

    public void SetOutline(bool isActive)
    {
        if (_isOutlineLocked && !isActive) return;
        if (_isOutlineActive == isActive) return;

        _isOutlineActive = isActive;
        ApplyLayerState(isActive);
    }

    public void SetOutlineLocked(bool isLocked)
    {
        _isOutlineLocked = isLocked;

        if (isLocked)
        {
            SetOutline(true);
        }
        else
        {
            _isOutlineActive = false;
            ApplyLayerState(false);
        }
    }

    private void ApplyLayerState(bool isOutlineOn)
    {
        SetRenderersLayer(outlineMeshRenderers, isOutlineOn);
        SetRenderersLayer(outlineSkinnedMeshRenderers, isOutlineOn);
    }

    private void SetRenderersLayer<T>(List<T> renderers, bool isOutlineOn) where T : Renderer
    {
        if (renderers == null) return;

        foreach (var r in renderers)
        {
            if (r == null) continue;

            if (isOutlineOn)
            {
                r.gameObject.layer = _outlineLayerIndex;
            }
            else
            {
                if (_originalLayers.TryGetValue(r, out int originalLayer))
                {
                    r.gameObject.layer = originalLayer;
                }
            }
        }
    }
    // -----------------------

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

    // --- ОНОВЛЕНО ДЛЯ ПІДТРИМКИ НАПРЯМКУ ОБЕРТАННЯ ---
    public void StartSmoothRotation(bool isClockwise = true)
    {
        if (IsRotating) return;

        if (_rotationCoroutine != null)
        {
            StopCoroutine(_rotationCoroutine);
        }
        _rotationCoroutine = StartCoroutine(SmoothRotationCoroutine(isClockwise));
    }

    private IEnumerator SmoothRotationCoroutine(bool isClockwise)
    {
        IsRotating = true;

        float cellSize = 1f;
        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        }

        Vector2Int currentRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 currentVisualOffset = new Vector3(currentRotationOffset.x, 0, currentRotationOffset.y) * cellSize;

        Vector3 currentGridOrigin = transform.position;
        currentGridOrigin.x -= currentVisualOffset.x;
        currentGridOrigin.z -= currentVisualOffset.z;

        Vector3 clickOffsetVector = new Vector3(ClickOffset.x, 0, ClickOffset.y) * cellSize;
        Vector3 halfCellVector = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        Vector3 pivotPoint = currentGridOrigin + clickOffsetVector + halfCellVector;

        // Визначаємо наступний напрямок та кут
        PlacedObjectTypeSO.Dir nextDirection;
        float angleToRotate;

        if (isClockwise)
        {
            nextDirection = PlacedObjectTypeSO.GetNextDirencion(CurrentDirection);
            angleToRotate = 90f;
        }
        else
        {
            nextDirection = PlacedObjectTypeSO.GetPreviousDir(CurrentDirection);
            angleToRotate = -90f;
        }

        float targetAngle = pieceTypeSO.GetRotationAngle(nextDirection);
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        float totalRotation = 0f;
        float absAngleToRotate = Mathf.Abs(angleToRotate);

        while (totalRotation < absAngleToRotate)
        {
            float rotationStep = Time.deltaTime * rotationSpeed;
            rotationStep = Mathf.Min(rotationStep, absAngleToRotate - totalRotation);

            // Обертаємо навколо півота
            transform.RotateAround(pivotPoint, Vector3.up, isClockwise ? rotationStep : -rotationStep);

            totalRotation += rotationStep;
            yield return null;
        }

        // Жорстко ставимо фінальну ротацію
        transform.rotation = targetRotation;
        CurrentDirection = nextDirection;

        // Перераховуємо зміщення
        Vector2Int newRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 newVisualOffset = new Vector3(newRotationOffset.x, 0, newRotationOffset.y) * cellSize;

        Vector3 newGridOrigin = transform.position;
        newGridOrigin.x -= newVisualOffset.x;
        newGridOrigin.z -= newVisualOffset.z;

        Vector3 newOffsetVector = pivotPoint - newGridOrigin - halfCellVector;

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