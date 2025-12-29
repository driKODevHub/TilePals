using UnityEngine;
using System;
using System.Collections.Generic;

[RequireComponent(typeof(PieceMovement), typeof(PieceVisuals))]
public class PuzzlePiece : MonoBehaviour
{
    [Header("Data")]
    [SerializeField] private PlacedObjectTypeSO pieceTypeSO;

    [Header("References")]
    [SerializeField] private FacialExpressionController facialController;

    [Header("Attachment Settings")]
    [Tooltip("РўРѕС‡РєР°, РєСѓРґРё Р±СѓРґСѓС‚СЊ РєСЂС–РїРёС‚РёСЃСЏ С–РіСЂР°С€РєРё/С—Р¶Р°.")]
    [SerializeField] private Transform attachmentPoint;

    // --- Components ---
    public PieceMovement Movement { get; private set; }
    public PieceVisuals Visuals { get; private set; }
    public Rigidbody Rb { get; private set; }
    public Collider PieceCollider { get; private set; }

    // --- State ---
    public PlacedObjectTypeSO PieceTypeSO => pieceTypeSO;
    public FacialExpressionController FacialController => facialController;
    public PlacedObjectTypeSO.Dir CurrentDirection { get; private set; } = PlacedObjectTypeSO.Dir.Down;

    public bool IsPlaced { get; private set; } = false;
    public bool IsOffGrid { get; private set; } = false;
    public PuzzleBoard OwnerBoard { get; set; } // Track which board this piece belongs to
    public Vector2Int OffGridOrigin { get; private set; }
    public PlacedObject PlacedObjectComponent { get; private set; }
    public PlacedObject InfrastructureComponent { get; private set; }
    public bool IsRotating => Movement != null && Movement.IsRotating;
    public bool IsStaticObstacle { get; set; } = false;
    public bool IsObstacle { get; set; } = false; // Movable obstacle
    public bool IsHidden { get; set; } = false;   // Hidden in a container

    // --- MOUTH ITEM (Single) ---
    public PuzzlePiece HeldItem { get; private set; }
    public bool HasItem => HeldItem != null;

    // --- PASSENGERS (For Tools/Baskets) ---
    public List<PuzzlePiece> StoredPassengers { get; private set; } = new List<PuzzlePiece>();

    public Vector2Int ClickOffset { get; set; }

    // --- FALLBACK PLACEMENT ---
    public struct EditorPlacementData
    {
        public Vector2Int origin;
        public PlacedObjectTypeSO.Dir direction;
        public bool hasData;
    }
    public EditorPlacementData EditorPlacement { get; private set; }

    public void SetEditorPlacement(Vector2Int origin, PlacedObjectTypeSO.Dir direction)
    {
        EditorPlacement = new EditorPlacementData { origin = origin, direction = direction, hasData = true };
    }

    private void Awake()
    {
        Movement = GetComponent<PieceMovement>();
        Visuals = GetComponent<PieceVisuals>();
        Rb = GetComponent<Rigidbody>();
        PieceCollider = GetComponent<Collider>();

        if (facialController == null)
            facialController = GetComponentInChildren<FacialExpressionController>();

        if (attachmentPoint == null) attachmentPoint = transform;
    }

    private void Start()
    {
        InitializePhysics();
    }

    public void Initialize(PlacedObjectTypeSO type)
    {
        pieceTypeSO = type;
        InitializePhysics();
    }

    private void InitializePhysics()
    {
        if (pieceTypeSO != null && pieceTypeSO.usePhysics)
        {
            if (Rb == null) Rb = gameObject.AddComponent<Rigidbody>();

            Rb.mass = pieceTypeSO.mass;
            Rb.isKinematic = true;
            Rb.collisionDetectionMode = CollisionDetectionMode.Continuous;

            if (PieceCollider != null)
            {
                PhysicsMaterial mat = new PhysicsMaterial();
                mat.bounciness = pieceTypeSO.bounciness;
                mat.bounceCombine = PhysicsMaterialCombine.Maximum;
                mat.dynamicFriction = 0.6f;
                mat.staticFriction = 0.6f;
                PieceCollider.material = mat;
            }
        }
        else
        {
            if (Rb != null) Rb.isKinematic = true;
        }
    }

    public void EnablePhysics(Vector3 initialVelocity)
    {
        if (Rb != null && pieceTypeSO.usePhysics)
        {
            // Р’РёРјРёРєР°С”РјРѕ Movement, С‰РѕР± РІС–РЅ РЅРµ РєРѕРЅС„Р»С–РєС‚СѓРІР°РІ Р· С„С–Р·РёРєРѕСЋ
            if (Movement) Movement.enabled = false;

            Rb.isKinematic = false;
            Rb.linearVelocity = initialVelocity;
            Rb.angularVelocity = new Vector3(
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(-5f, 5f),
                UnityEngine.Random.Range(-5f, 5f)
            );
        }
    }

    public void DisablePhysics()
    {
        if (Rb != null)
        {
            Rb.isKinematic = true;
            Rb.linearVelocity = Vector3.zero;
            Rb.angularVelocity = Vector3.zero;
        }

        // Р’РјРёРєР°С”РјРѕ Movement, С‰РѕР± РІС–РЅ РјС–Рі РїС–РґС…РѕРїРёС‚Рё РѕР±'С”РєС‚ С– РїРѕСЃС‚Р°РІРёС‚Рё Р№РѕРіРѕ РЅР° РјС–СЃС†Рµ
        if (Movement) Movement.enabled = true;

        float yAngle = transform.eulerAngles.y;
        float snappedAngle = Mathf.Round(yAngle / 90f) * 90f;
        transform.rotation = Quaternion.Euler(0, snappedAngle, 0);
        SyncDirectionFromRotation(transform.rotation);
    }

    // --- Passenger Logic (For Tools) ---
    public void AddPassenger(PuzzlePiece passenger)
    {
        if (!StoredPassengers.Contains(passenger))
        {
            StoredPassengers.Add(passenger);
            passenger.transform.SetParent(transform);

            // Р’РёРјРёРєР°С”РјРѕ С„С–Р·РёРєСѓ РїР°СЃР°Р¶РёСЂСѓ
            passenger.DisablePhysics();
            if (passenger.Movement) passenger.Movement.enabled = false;
            if (passenger.PieceCollider) passenger.PieceCollider.enabled = false;
        }
    }

    public void ReleaseAllPassengers(Transform newRoot)
    {
        foreach (var p in StoredPassengers)
        {
            if (p != null)
            {
                p.transform.SetParent(newRoot);
                if (p.Movement) p.Movement.enabled = true;
                if (p.PieceCollider) p.PieceCollider.enabled = true;

                p.SyncDirectionFromRotation(p.transform.rotation);
            }
        }
        StoredPassengers.Clear();
    }

    // --- Item Attachment Logic (Mouth) ---
    public void AttachItem(PuzzlePiece item)
    {
        if (HasItem || item == null) return;

        HeldItem = item;

        item.DisablePhysics();
        if (item.Movement != null) item.Movement.enabled = false;
        if (item.PieceCollider) item.PieceCollider.enabled = false;

        item.transform.SetParent(attachmentPoint);
        item.transform.localPosition = Vector3.zero;
        item.transform.localRotation = Quaternion.identity;

        if (item.IsPlaced) GridBuildingSystem.Instance.RemovePieceFromGrid(item);
        if (item.IsOffGrid && GridBuildingSystem.Instance.ActiveBoard != null) GridBuildingSystem.Instance.ActiveBoard.OffGridTracker.RemovePiece(item);
        item.SetOffGrid(false);
        item.SetPlaced(null);
    }

    public PuzzlePiece DetachItem()
    {
        if (!HasItem) return null;

        PuzzlePiece item = HeldItem;
        HeldItem = null;

        item.transform.SetParent(null);

        if (item.Movement != null) item.Movement.enabled = true;
        if (item.PieceCollider) item.PieceCollider.enabled = true;

        return item;
    }

    public Transform GetAttachmentPoint() => attachmentPoint;

    // --- Visual Proxy ---
    public void SetOutline(bool active)
    {
        if (Visuals != null) Visuals.SetOutline(active);
    }

    public void SetOutlineLocked(bool locked)
    {
        if (Visuals != null) Visuals.SetOutlineLocked(locked);
    }

    public void SetInvalidPlacementVisual(bool isInvalid)
    {
        if (Visuals != null) Visuals.SetInvalidPlacementVisual(isInvalid);
    }

    // --- State Management ---
    public void SetPlaced(PlacedObject placedObjectComponent)
    {
        this.PlacedObjectComponent = placedObjectComponent;
        this.InfrastructureComponent = null;
        IsPlaced = (placedObjectComponent != null);

        if (IsPlaced)
        {
            IsOffGrid = false;
            DisablePhysics();
        }
    }

    public void SetInfrastructure(PlacedObject infraComponent)
    {
        this.InfrastructureComponent = infraComponent;
        this.PlacedObjectComponent = null;
        IsPlaced = (infraComponent != null);

        if (IsPlaced)
        {
            IsOffGrid = false;
            DisablePhysics();
        }
    }

    public void SetOffGrid(bool isOffGrid, Vector2Int origin = default)
    {
        IsOffGrid = isOffGrid;
        if (isOffGrid)
        {
            IsPlaced = false;
            OffGridOrigin = origin;
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
        if (Rb != null && !Rb.isKinematic) return;

        // РЇРєС‰Рѕ Movement РєРѕРјРїРѕРЅРµРЅС‚ С–СЃРЅСѓС” С– РІС–РЅ СѓРІС–РјРєРЅРµРЅРёР№ (Р°Р±Рѕ РјРё С…РѕС‡РµРјРѕ, С‰РѕР± РІС–РЅ РѕР±СЂРѕР±РёРІ С†Рµ)
        // РђР»Рµ РѕСЃРєС–Р»СЊРєРё РјРё РІРёРјРёРєР°С”РјРѕ Movement РґР»СЏ РѕРїС‚РёРјС–Р·Р°С†С–С—, С‚СЂРµР±Р° РїРµСЂРµРІС–СЂРёС‚Рё Р»РѕРіС–РєСѓ.
        // РЇРєС‰Рѕ РјРё С‚СѓС‚, С‚Рѕ С†Рµ Р·Р°Р·РІРёС‡Р°Р№ С‚РµР»РµРїРѕСЂС‚Р°С†С–СЏ Р°Р±Рѕ Undo. 
        // РљСЂР°С‰Рµ РІРІС–РјРєРЅСѓС‚Рё Movement РЅР° РѕРґРёРЅ РєР°РґСЂ, С‰РѕР± РІС–РЅ РѕРЅРѕРІРёРІ СЃРІРѕС— Р·РјС–РЅРЅС– (_targetPosition) С– Р·Р°СЃРЅСѓРІ.

        if (Movement != null)
        {
            // РџСЂРёРјСѓСЃРѕРІРѕ РІРјРёРєР°С”РјРѕ РґР»СЏ РѕР±СЂРѕР±РєРё С‚РµР»РµРїРѕСЂС‚Р°С†С–С—, СЃРєСЂРёРїС‚ СЃР°Рј РІРёРјРєРЅРµС‚СЊСЃСЏ РІ TeleportTo
            Movement.enabled = true;
            Movement.TeleportTo(position, rotation);
        }
        else
        {
            transform.position = position;
            transform.rotation = rotation;
        }
        SyncDirectionFromRotation(rotation);
    }

    public void RotatePiece(bool clockwise, float cellSize, Action onComplete = null)
    {
        if (Movement == null || Movement.IsRotating || (Rb != null && !Rb.isKinematic)) return;

        Vector2Int currentRotationOffset = pieceTypeSO.GetRotationOffset(CurrentDirection);
        Vector3 currentVisualOffset = new Vector3(currentRotationOffset.x, 0, currentRotationOffset.y) * cellSize;

        Vector3 currentGridOrigin = transform.position;
        currentGridOrigin.x -= currentVisualOffset.x;
        currentGridOrigin.z -= currentVisualOffset.z;

        Vector3 clickOffsetVector = new Vector3(ClickOffset.x, 0, ClickOffset.y) * cellSize;
        Vector3 halfCellVector = new Vector3(cellSize * 0.5f, 0, cellSize * 0.5f);
        Vector3 pivotPoint = currentGridOrigin + clickOffsetVector + halfCellVector;

        PlacedObjectTypeSO.Dir nextDirection = clockwise
            ? PlacedObjectTypeSO.GetNextDir(CurrentDirection)
            : PlacedObjectTypeSO.GetPreviousDir(CurrentDirection);

        float angle = clockwise ? 90f : -90f;

        // Movement СЃР°Рј СѓРІС–РјРєРЅРµС‚СЊСЃСЏ РІСЃРµСЂРµРґРёРЅС– RotateAroundPivot
        Movement.RotateAroundPivot(pivotPoint, Vector3.up, angle, () => {
            CurrentDirection = nextDirection;
            RecalculateClickOffset(pivotPoint, cellSize);
            SyncPassengersRotation(); // РЎРёРЅС…СЂРѕРЅС–Р·СѓС”РјРѕ РїР°СЃР°Р¶РёСЂС–РІ РїС–СЃР»СЏ РїРѕРІРѕСЂРѕС‚Сѓ
            onComplete?.Invoke();
        });
    }

    // --- FORCE SYNC PASSENGERS ---
    public void SyncPassengersRotation()
    {
        if (StoredPassengers.Count == 0) return;

        foreach (var p in StoredPassengers)
        {
            if (p != null)
            {
                // РћРЅРѕРІР»СЋС”РјРѕ Р»РѕРіС–С‡РЅРёР№ РЅР°РїСЂСЏРјРѕРє РїР°СЃР°Р¶РёСЂР°
                p.SyncDirectionFromRotation(p.transform.rotation);

                // РћРєСЂСѓРіР»СЏС”РјРѕ Р»РѕРєР°Р»СЊРЅС– РєРѕРѕСЂРґРёРЅР°С‚Рё
                Vector3 localPos = p.transform.localPosition;
                localPos.x = (float)Math.Round(localPos.x, 2);
                localPos.y = (float)Math.Round(localPos.y, 2);
                localPos.z = (float)Math.Round(localPos.z, 2);
                p.transform.localPosition = localPos;
            }
        }
    }

    public void SyncDirectionFromRotation(Quaternion rotation)
    {
        float yAngle = Mathf.Round(rotation.eulerAngles.y);
        yAngle = (yAngle % 360 + 360) % 360;

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
