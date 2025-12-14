using UnityEngine;
using System.Collections.Generic;

public class PlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int gridPosition;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 prevPosition;
    private Quaternion prevRotation;

    // Стан "До"
    private bool wasOnGrid;
    private bool wasOffGrid;
    private Vector2Int prevGridOrigin;
    private Vector2Int prevOffGridOrigin;

    // Знімок пасажирів на момент створення команди
    private List<PuzzlePiece> passengersSnapshot;

    public PlaceCommand(PuzzlePiece piece, Vector2Int gridPosition, PlacedObjectTypeSO.Dir direction, Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> currentPassengers)
    {
        this.piece = piece;
        this.gridPosition = gridPosition;
        this.direction = direction;
        this.prevPosition = prevPos;
        this.prevRotation = prevRot;
        this.passengersSnapshot = currentPassengers != null ? new List<PuzzlePiece>(currentPassengers) : new List<PuzzlePiece>();

        this.wasOnGrid = piece.IsPlaced;
        this.wasOffGrid = piece.IsOffGrid;

        if (wasOnGrid)
        {
            this.prevGridOrigin = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent.Origin : (piece.InfrastructureComponent != null ? piece.InfrastructureComponent.Origin : Vector2Int.zero);
        }
        if (wasOffGrid) this.prevOffGridOrigin = piece.OffGridOrigin;
    }

    public bool Execute()
    {
        // 1. ОЧИЩЕННЯ: Повністю видаляємо фігуру та її пасажирів з поточного місця
        // Це критично для Redo, щоб звільнити клітинки
        CleanupCurrentState();

        // 2. РОЗМІЩЕННЯ ТУЛЗА/ФІГУРИ
        // Тут ми припускаємо, що хід валідний (бо ми його вже робили або щойно перевірили в Manager)
        // Використовуємо PlacePieceOnGrid напряму

        PlacedObject placedObjectComponent = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, gridPosition, direction);

        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            piece.SetInfrastructure(placedObjectComponent);
        else
            piece.SetPlaced(placedObjectComponent);

        // Візуальне переміщення
        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(gridPosition.x, gridPosition.y) + offset;

        piece.UpdateTransform(finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));

        // 3. ВІДНОВЛЕННЯ ПАСАЖИРІВ (Redo Logic)
        if (passengersSnapshot.Count > 0)
        {
            piece.StoredPassengers.Clear(); // Очищаємо, бо зараз висадимо

            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;

                // Переконуємось, що пасажир "чистий"
                if (p.IsPlaced) GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                else if (p.IsOffGrid) OffGridManager.RemovePiece(p);

                // Відчіпляємо фізично
                p.transform.SetParent(piece.transform.parent);

                // Розраховуємо позицію
                Vector3 pWorldPos = p.transform.position;
                GridBuildingSystem.Instance.GetGrid().GetXZ(pWorldPos, out int px, out int pz);
                Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
                Vector2Int pOrigin = new Vector2Int(px, pz) - pRotOffset;

                // СТАВИМО НА ГРІД
                var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
                p.SetPlaced(po);

                // Снеп візуала
                Vector3 snapPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
                p.UpdateTransform(snapPos, p.transform.rotation);
            }
        }

        PersonalityEventManager.RaisePiecePlaced(piece);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();

        return true;
    }

    public void Undo()
    {
        // 1. ОЧИЩЕННЯ: Знімаємо все, що поставили в Execute/Redo
        CleanupCurrentState();

        // 2. ВІДНОВЛЕННЯ ПАСАЖИРІВ В ТУЛЗ (Логіка "В кишеню")
        // Якщо тулз мав пасажирів, ми їх вже зняли з гріда в CleanupCurrentState.
        // Тепер треба їх додати в список storedPassengers тулза і прикріпити фізично.
        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                piece.AddPassenger(p); // Це робить SetParent(tool) і disable physics
            }
        }

        // 3. ПОВЕРНЕННЯ НА СТАРЕ МІСЦЕ (Фізика)
        piece.UpdateTransform(prevPosition, prevRotation);
        piece.SyncPassengersRotation();

        // 4. ВІДНОВЛЕННЯ ЛОГІКИ СТАРОГО МІСЦЯ
        if (wasOnGrid)
        {
            // Force Place на старе місце
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, prevGridOrigin, piece.CurrentDirection);

            if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                piece.SetInfrastructure(po);
                // Якщо повернули тулз на грід, пасажири мають вийти з "кишені" і стати на грід
                RestorePassengersOnGridAfterUndo(piece);
            }
            else
            {
                piece.SetPlaced(po);
            }
        }
        else if (wasOffGrid)
        {
            OffGridManager.PlacePiece(piece, prevOffGridOrigin);
            piece.SetOffGrid(true, prevOffGridOrigin);
            // Тут пасажири залишаються в кишені (AddPassenger зробив свою справу)
        }
        else
        {
            piece.SetOffGrid(false);
        }

        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    private void CleanupCurrentState()
    {
        // Цей метод гарантує, що клітинки звільняться, де б фігура не була

        // 1. Якщо це тулз і він на гріді - знімаємо його (це заблокує клітинки)
        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
            piece.SetInfrastructure(null);
        }
        else if (piece.IsOffGrid)
        {
            OffGridManager.RemovePiece(piece);
        }
        piece.SetOffGrid(false);

        // 2. Якщо є пасажири, їх теж треба зняти з гріда/offgrid
        // Вони можуть бути як на гріді (якщо тулз був placed), так і дітьми (якщо undo з offgrid)
        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                if (p.IsPlaced)
                {
                    GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                    p.SetPlaced(null);
                }
                else if (p.IsOffGrid)
                {
                    OffGridManager.RemovePiece(p);
                    p.SetOffGrid(false);
                }
            }
        }
    }

    private void RestorePassengersOnGridAfterUndo(PuzzlePiece tool)
    {
        // Цей метод викликається, коли Undo повернуло тулз НА ГРІД.
        // Пасажири зараз в storedPassengers (через крок 2 в Undo).
        // Треба їх висадити.

        var grid = GridBuildingSystem.Instance.GetGrid();
        List<PuzzlePiece> currentPassengers = new List<PuzzlePiece>(tool.StoredPassengers);

        foreach (var p in currentPassengers)
        {
            p.transform.SetParent(tool.transform.parent);
            p.SyncDirectionFromRotation(p.transform.rotation);

            Vector3 worldPos = p.transform.position;
            grid.GetXZ(worldPos, out int x, out int z);
            Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int pOrigin = new Vector2Int(x, z) - pRotOffset;

            // Force Place
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
            p.SetPlaced(po);
        }
        tool.StoredPassengers.Clear();
    }
}