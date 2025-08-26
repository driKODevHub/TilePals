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
    [SerializeField] private float rotationSpeed = 270f;

    public PlacedObjectTypeSO PieceTypeSO => pieceTypeSO;
    public PlacedObjectTypeSO.Dir CurrentDirection { get; private set; } = PlacedObjectTypeSO.Dir.Down;
    public bool IsPlaced { get; private set; } = false;
    public bool IsRotating { get; private set; } = false;

    public bool IsOffGrid { get; private set; } = false;

    public Vector2Int OffGridOrigin { get; private set; }

    public PlacedObject PlacedObjectComponent { get; private set; }
    private Coroutine _rotationCoroutine;

    private Dictionary<MeshRenderer, Material[]> _originalMaterials;
    private bool _isInvalidMaterialApplied = false;

    private void Awake()
    {
        CacheOriginalMaterials();
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

    private IEnumerator SmoothRotationCoroutine()
    {
        IsRotating = true;

        PlacedObjectTypeSO.Dir nextDirection = PlacedObjectTypeSO.GetNextDirencion(CurrentDirection);
        float targetAngle = pieceTypeSO.GetRotationAngle(nextDirection);
        Quaternion targetRotation = Quaternion.Euler(0, targetAngle, 0);

        float cellSize = 1f;
        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
        {
            cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        }
        Vector3 centerOffset = pieceTypeSO.GetBoundsCenterOffset(CurrentDirection) * cellSize;
        Vector3 pivotPoint = transform.position + transform.rotation * centerOffset;

        float totalRotation = 0f;
        float angleToRotate = 90f;

        while (totalRotation < angleToRotate)
        {
            float rotationAmount = Time.deltaTime * rotationSpeed;
            rotationAmount = Mathf.Min(rotationAmount, angleToRotate - totalRotation);

            transform.RotateAround(pivotPoint, Vector3.up, rotationAmount);

            totalRotation += rotationAmount;
            yield return null;
        }

        transform.rotation = targetRotation;

        CurrentDirection = nextDirection;
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

    /// <summary>
    /// НОВИЙ МЕТОД: Встановлює початковий поворот фігури при її створенні.
    /// </summary>
    public void SetInitialRotation(PlacedObjectTypeSO.Dir direction)
    {
        CurrentDirection = direction;
        transform.rotation = Quaternion.Euler(0, pieceTypeSO.GetRotationAngle(direction), 0);
    }
}
