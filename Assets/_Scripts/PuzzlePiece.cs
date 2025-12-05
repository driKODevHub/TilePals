using UnityEngine;
using System; // <--- ДОДАНО: Потрібно для Action
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Основний контролер фігури. (LOGIC ONLY)
/// Зберігає стан, дані та координує Visuals і Movement.
/// </summary>
[RequireComponent(typeof(PieceMovement), typeof(PieceVisuals))]
public class PuzzlePiece : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlacedObjectTypeSO pieceTypeSO;

    [Header("References")]
    [SerializeField] private FacialExpressionController facialController; // Залишаємо поки тут, бо це частина "Особистості"

    // --- Components ---
    public PieceMovement Movement { get; private set; }
    public PieceVisuals Visuals { get; private set; }

    // --- State ---
    public PlacedObjectTypeSO PieceTypeSO => pieceTypeSO;
    public FacialExpressionController FacialController => facialController;
    public PlacedObjectTypeSO.Dir CurrentDirection { get; private set; } = PlacedObjectTypeSO.Dir.Down;
    public bool IsPlaced { get; private set; } = false;
    public bool IsOffGrid { get; private set; } = false;
    public Vector2Int OffGridOrigin { get; private set; }
    public PlacedObject PlacedObjectComponent { get; private set; }
    public bool IsRotating => Movement != null && Movement.IsRotating;

    // Логічне зміщення кліку
    public Vector2Int ClickOffset { get; set; }

    private void Awake()
    {
        Movement = GetComponent<PieceMovement>();
        Visuals = GetComponent<PieceVisuals>();

        if (facialController == null)
            facialController = GetComponentInChildren<FacialExpressionController>();
    }

    public void Initialize(PlacedObjectTypeSO type)
    {
        pieceTypeSO = type;
    }

    // --- Visual Proxy ---
    public void SetTemperamentMaterial(Material mat)
    {
        if (Visuals != null) Visuals.SetTemperamentMaterial(mat);
    }

    public void SetOutline(bool active)
    {
        if (Visuals != null) Visuals.SetOutline(active);
    }

    public void SetOutlineLocked(bool locked)
    {
        if (Visuals != null) Visuals.SetOutlineLocked(locked);
    }

    public void UpdatePlacementVisual(bool isValid, Material invalidMat)
    {
        if (Visuals != null) Visuals.SetInvalidPlacementVisual(!isValid, invalidMat);
    }

    // --- State Management ---
    public void SetPlaced(PlacedObject placedObjectComponent)
    {
        this.PlacedObjectComponent = placedObjectComponent;
        IsPlaced = (placedObjectComponent != null);
        if (IsPlaced)
        {
            IsOffGrid = false;
            if (Visuals != null) Visuals.OnDropFeedback?.Invoke(); // Trigger Drop/Place feedback
        }
    }

    public void SetOffGrid(bool isOffGrid, Vector2Int origin = default)
    {
        IsOffGrid = isOffGrid;
        IsPlaced = false;
        if (isOffGrid)
        {
            OffGridOrigin = origin;
            if (Visuals != null) Visuals.OnDropFeedback?.Invoke();
        }
    }

    public void SetInitialRotation(PlacedObjectTypeSO.Dir direction)
    {
        CurrentDirection = direction;
        transform.rotation = Quaternion.Euler(0, pieceTypeSO.GetRotationAngle(direction), 0);
    }

    // --- Movement Logic Wrapper ---
    public void UpdateTransform(Vector3 position, Quaternion rotation)
    {
        if (Movement != null)
        {
            Movement.TeleportTo(position, rotation);
        }
        else
        {
            // Fallback, якщо компонента немає (наприклад, при ініціалізації)
            transform.position = position;
            transform.rotation = rotation;
        }
        SyncDirectionFromRotation(rotation);
    }

    public void RotatePiece(bool clockwise, float cellSize, Action onComplete = null)
    {
        if (Movement == null || Movement.IsRotating) return;

        // Розрахунок точки обертання (Pivot)
        Vector2Int currentRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 currentVisualOffset = new Vector3(currentRotationOffset.x, 0, currentRotationOffset.y) * cellSize;

        Vector3 currentGridOrigin = transform.position;
        // Корекція позиції, щоб знайти 0,0 фігури
        currentGridOrigin.x -= currentVisualOffset.x;
        currentGridOrigin.z -= currentVisualOffset.z;

        Vector3 clickOffsetVector = new Vector3(ClickOffset.x, 0, ClickOffset.y) * cellSize;
        Vector3 halfCellVector = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        Vector3 pivotPoint = currentGridOrigin + clickOffsetVector + halfCellVector;

        // Визначаємо новий напрямок
        PlacedObjectTypeSO.Dir nextDirection = clockwise
            ? PlacedObjectTypeSO.GetNextDirencion(CurrentDirection)
            : PlacedObjectTypeSO.GetPreviousDir(CurrentDirection);

        float angle = clockwise ? 90f : -90f;

        Movement.RotateAroundPivot(pivotPoint, Vector3.up, angle, () => {
            // Після завершення фізичного обертання оновлюємо логічні дані
            CurrentDirection = nextDirection;
            RecalculateClickOffset(pivotPoint, cellSize);
            onComplete?.Invoke();
        });
    }

    private void SyncDirectionFromRotation(Quaternion rotation)
    {
        float yAngle = Mathf.Round(rotation.eulerAngles.y);
        if (Mathf.Approximately(yAngle, 0)) CurrentDirection = PlacedObjectTypeSO.Dir.Down;
        else if (Mathf.Approximately(yAngle, 90)) CurrentDirection = PlacedObjectTypeSO.Dir.Left;
        else if (Mathf.Approximately(yAngle, 180)) CurrentDirection = PlacedObjectTypeSO.Dir.Up;
        else if (Mathf.Approximately(yAngle, 270)) CurrentDirection = PlacedObjectTypeSO.Dir.Right;
    }

    private void RecalculateClickOffset(Vector3 pivotPoint, float cellSize)
    {
        Vector2Int newRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 newVisualOffset = new Vector3(newRotationOffset.x, 0, newRotationOffset.y) * cellSize;

        Vector3 newGridOrigin = transform.position;
        newGridOrigin.x -= newVisualOffset.x;
        newGridOrigin.z -= newVisualOffset.z;

        Vector3 halfCellVector = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        Vector3 newOffsetVector = pivotPoint - newGridOrigin - halfCellVector;

        ClickOffset = new Vector2Int(
            Mathf.RoundToInt(newOffsetVector.x / cellSize),
            Mathf.RoundToInt(newOffsetVector.z / cellSize)
        );
    }
}