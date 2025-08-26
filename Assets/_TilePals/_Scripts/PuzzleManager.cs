using UnityEngine;
using System.Collections.Generic;
using System;
using System.Collections;

public class PuzzleManager : MonoBehaviour
{
    public static PuzzleManager Instance { get; private set; }

    private enum PlacementValidity { OnBuildableGrid, OffGrid, OnNonBuildableAndOffGrid, Invalid }

    [Header("Налаштування руху фігур")]
    [SerializeField] private float pieceFollowSpeed = 20f;
    [SerializeField] private float pieceHeightWhenHeld = 0.5f;
    // НОВЕ ПОЛЕ: Поріг швидкості для реакції на "мотиляння".
    [SerializeField] private float shakenVelocityThreshold = 15f;


    [Header("Налаштування візуалу")]
    [SerializeField] private Material invalidPlacementMaterial;

    [Header("Налаштування шарів (Layers)")]
    [SerializeField] private LayerMask pieceLayer;
    [SerializeField] private LayerMask offGridPlaneLayer;

    private PuzzlePiece heldPiece = null;
    private Vector3 initialPiecePosition;
    private Quaternion initialPieceRotation;
    private bool _isLevelComplete = false;

    private Vector3 _lastHeldPiecePosition;
    private float _heldPieceVelocity;

    public event Action<PuzzlePiece> OnPiecePickedUp;
    public event Action<PuzzlePiece> OnPieceDropped;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    public void ResetState()
    {
        _isLevelComplete = false;
        heldPiece = null;
    }

    private void Update()
    {
        if (_isLevelComplete) return;

        HandlePieceMovement();
        HandleInput();
    }

    public PuzzlePiece GetHeldPiece() => heldPiece;

    private void HandleInput()
    {
        if (heldPiece == null)
        {
            if (Input.GetMouseButtonDown(0)) TryToPickUpPiece();
        }
        else
        {
            if (Input.GetMouseButtonDown(0) && !heldPiece.IsRotating)
            {
                TryToPlaceOrDropPiece();
            }

            if (heldPiece != null && !heldPiece.IsRotating && Input.GetKeyDown(KeyCode.Space))
            {
                heldPiece.StartSmoothRotation();
            }
        }

        if (Input.GetKeyDown(KeyCode.Z)) CommandHistory.Undo();
        if (Input.GetKeyDown(KeyCode.C)) CommandHistory.Redo();
    }

    private void TryToPickUpPiece()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, pieceLayer))
        {
            PuzzlePiece piece = hit.collider.GetComponentInParent<PuzzlePiece>();
            if (piece != null) PickUpPiece(piece);
        }
    }

    private void TryToPlaceOrDropPiece()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer))
        {
            var grid = GridBuildingSystem.Instance.GetGrid();
            grid.GetXZ(hit.point, out int x, out int z);
            Vector2Int origin = new Vector2Int(x, z);

            PlacementValidity validity = GetPlacementValidity(origin);

            switch (validity)
            {
                case PlacementValidity.OnBuildableGrid:
                    TryPlaceOnGrid(origin);
                    break;
                case PlacementValidity.OffGrid:
                case PlacementValidity.OnNonBuildableAndOffGrid:
                    TryPlaceOffGrid(origin);
                    break;
                case PlacementValidity.Invalid:
                    Debug.Log("Неможливо розмістити: фігура перетинає недозволені зони.");
                    break;
            }
        }
    }

    private void HandlePieceMovement()
    {
        if (heldPiece == null || heldPiece.IsRotating) return;

        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        if (Physics.Raycast(ray, out RaycastHit hit, 100f, offGridPlaneLayer))
        {
            var grid = GridBuildingSystem.Instance.GetGrid();
            float cellSize = grid.GetCellSize();

            grid.GetXZ(hit.point, out int x, out int z);
            Vector2Int origin = new Vector2Int(x, z);

            Vector2Int rotationOffset = heldPiece.PieceTypeSO.GetRotationOffset(heldPiece.CurrentDirection);
            Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
            Vector3 snappedPosition = new Vector3(origin.x * cellSize, 0, origin.y * cellSize);
            Vector3 targetPosition = new Vector3(snappedPosition.x, pieceHeightWhenHeld, snappedPosition.z) + offset;
            heldPiece.transform.position = Vector3.Lerp(heldPiece.transform.position, targetPosition, Time.deltaTime * pieceFollowSpeed);

            _heldPieceVelocity = (heldPiece.transform.position - _lastHeldPiecePosition).magnitude / Time.deltaTime;
            _lastHeldPiecePosition = heldPiece.transform.position;

            // ОНОВЛЕНО: Перевіряємо швидкість і викликаємо подію, якщо потрібно
            if (_heldPieceVelocity > shakenVelocityThreshold)
            {
                PersonalityEventManager.RaisePieceShaken(heldPiece, _heldPieceVelocity);
            }

            PlacementValidity validity = GetPlacementValidity(origin);
            bool canPlace = false;

            switch (validity)
            {
                case PlacementValidity.OnBuildableGrid:
                    canPlace = GridBuildingSystem.Instance.CanPlacePiece(heldPiece, origin, heldPiece.CurrentDirection);
                    break;
                case PlacementValidity.OffGrid:
                case PlacementValidity.OnNonBuildableAndOffGrid:
                    canPlace = OffGridManager.CanPlacePiece(heldPiece, origin);
                    break;
                case PlacementValidity.Invalid:
                    canPlace = false;
                    break;
            }

            if (invalidPlacementMaterial != null)
            {
                heldPiece.UpdatePlacementVisual(canPlace, invalidPlacementMaterial);
            }
        }
    }

    private PlacementValidity GetPlacementValidity(Vector2Int origin)
    {
        List<Vector2Int> pieceCells = heldPiece.PieceTypeSO.GetGridPositionsList(origin, heldPiece.CurrentDirection);
        if (pieceCells.Count == 0) return PlacementValidity.Invalid;

        var grid = GridBuildingSystem.Instance.GetGrid();
        int buildableOnGridCount = 0;
        int nonBuildableOnGridCount = 0;
        int offGridCount = 0;

        foreach (var cell in pieceCells)
        {
            GridObject gridObject = grid.GetGridObject(cell.x, cell.y);
            if (gridObject == null) { offGridCount++; }
            else if (gridObject.IsBuildable()) { buildableOnGridCount++; }
            else { nonBuildableOnGridCount++; }
        }

        int totalCells = pieceCells.Count;
        if (buildableOnGridCount == totalCells) return PlacementValidity.OnBuildableGrid;
        if (offGridCount == totalCells) return PlacementValidity.OffGrid;
        if (buildableOnGridCount == 0 && nonBuildableOnGridCount > 0 && offGridCount > 0) return PlacementValidity.OnNonBuildableAndOffGrid;
        return PlacementValidity.Invalid;
    }

    public void PickUpPiece(PuzzlePiece piece)
    {
        if (heldPiece != null) return;

        heldPiece = piece;
        initialPiecePosition = piece.transform.position;
        initialPieceRotation = piece.transform.rotation;

        _lastHeldPiecePosition = piece.transform.position;
        _heldPieceVelocity = 0f;

        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
        }
        else
        {
            OffGridManager.RemovePiece(piece);
            piece.SetOffGrid(false);
        }

        OnPiecePickedUp?.Invoke(piece);
        PersonalityEventManager.RaisePiecePickedUp(piece);
    }

    private void TryPlaceOnGrid(Vector2Int origin)
    {
        ICommand placeCommand = new PlaceCommand(heldPiece, origin, heldPiece.CurrentDirection, initialPiecePosition, initialPieceRotation);

        if (placeCommand.Execute())
        {
            PuzzlePiece placedPiece = heldPiece;
            heldPiece.UpdatePlacementVisual(true, invalidPlacementMaterial);
            CommandHistory.AddCommand(placeCommand);
            OnPieceDropped?.Invoke(placedPiece);
            // ОНОВЛЕНО: Викликаємо просту подію без швидкості
            PersonalityEventManager.RaisePieceDropped(placedPiece);
            heldPiece = null;
            CheckForWin();
            GameManager.Instance.SaveCurrentProgress();
        }
        else
        {
            Debug.Log("Неможливо розмістити фігуру на ігровій сітці!");
        }
    }

    private void TryPlaceOffGrid(Vector2Int offGridOrigin)
    {
        if (!OffGridManager.CanPlacePiece(heldPiece, offGridOrigin))
        {
            Debug.Log("Неможливо розмістити фігуру тут, місце зайняте!");
            return;
        }

        PuzzlePiece placedPiece = heldPiece;
        heldPiece.UpdatePlacementVisual(true, invalidPlacementMaterial);

        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector2Int rotationOffset = heldPiece.PieceTypeSO.GetRotationOffset(heldPiece.CurrentDirection);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = new Vector3(offGridOrigin.x * cellSize, 0, offGridOrigin.y * cellSize) + offset;

        heldPiece.transform.position = finalPos;
        heldPiece.SetOffGrid(true, offGridOrigin);
        OffGridManager.PlacePiece(heldPiece, offGridOrigin);

        OnPieceDropped?.Invoke(placedPiece);
        // ОНОВЛЕНО: Викликаємо просту подію без швидкості
        PersonalityEventManager.RaisePieceDropped(placedPiece);
        heldPiece = null;
        GameManager.Instance.SaveCurrentProgress();
    }

    private void CheckForWin()
    {
        if (_isLevelComplete) return;
        float fillPercentage = GridBuildingSystem.Instance.CalculateGridFillPercentage();
        Debug.Log($"Поле заповнено на: {fillPercentage:F2}%");

        if (Mathf.Approximately(fillPercentage, 100f))
        {
            _isLevelComplete = true;
            GameManager.Instance.OnLevelComplete();
            Debug.LogWarning("ПЕРЕМОГА! Рівень пройдено!");
        }
    }
}
