using UnityEngine;
using System.Collections.Generic;

public class PlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int gridPosition;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 prevPosition;
    private Quaternion prevRotation;
    private PlacedObjectTypeSO.Dir prevDirection; // STORE PREVIOUS DIRECTION
    private Vector2Int prevGridOrigin; 
    private Vector2Int prevOffGridOrigin;
    private bool wasOnGrid;
    private bool wasOffGrid;

    // Snapshot of passengers (cats/items) on the tool BEFORE the move
    private List<PuzzlePiece> passengersSnapshot;

    // Default Constructor - NOW TAKES INITIAL STATE
    public PlaceCommand(PuzzlePiece piece, Vector2Int gridPosition, PlacedObjectTypeSO.Dir direction, bool initialWasPlaced, bool initialWasOffGrid)
    {
        this.piece = piece;
        this.gridPosition = gridPosition;
        this.direction = direction;
        
        this.prevPosition = piece.transform.position;
        this.prevRotation = piece.transform.rotation;
        this.prevDirection = piece.CurrentDirection; // Default from piece
        
        this.passengersSnapshot = piece.StoredPassengers != null ? new List<PuzzlePiece>(piece.StoredPassengers) : new List<PuzzlePiece>();

        this.wasOnGrid = initialWasPlaced;
        this.wasOffGrid = initialWasOffGrid;

        if (wasOnGrid)
        {
            this.prevGridOrigin = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent.Origin : 
                (piece.InfrastructureComponent != null ? piece.InfrastructureComponent.Origin : Vector2Int.zero);
        }
        if (wasOffGrid) this.prevOffGridOrigin = piece.OffGridOrigin;
    }

    // Comprehensive Constructor
    public PlaceCommand(PuzzlePiece piece, Vector2Int gridPosition, PlacedObjectTypeSO.Dir direction, 
                        Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> currentPassengers,
                        bool initialWasPlaced, bool initialWasOffGrid, Vector2Int? initialOrigin,
                        PlacedObjectTypeSO.Dir initialDirection) 
    {
        this.piece = piece;
        this.gridPosition = gridPosition;
        this.direction = direction;
        
        this.prevPosition = prevPos;
        this.prevRotation = prevRot;
        this.prevDirection = initialDirection; // USE PROVIDED DIRECTION
        
        this.passengersSnapshot = currentPassengers != null ? new List<PuzzlePiece>(currentPassengers) : new List<PuzzlePiece>();

        this.wasOnGrid = initialWasPlaced;
        this.wasOffGrid = initialWasOffGrid;

        if (wasOnGrid)
        {
            if (initialOrigin.HasValue)
            {
                this.prevGridOrigin = initialOrigin.Value;
            }
            else
            {
                this.prevGridOrigin = piece.PlacedObjectComponent != null ? piece.PlacedObjectComponent.Origin : 
                    (piece.InfrastructureComponent != null ? piece.InfrastructureComponent.Origin : Vector2Int.zero);
            }
        }
        if (wasOffGrid)
        {
            if (initialOrigin.HasValue)
            {
                 this.prevOffGridOrigin = initialOrigin.Value;
            }
            else
            {
                 this.prevOffGridOrigin = piece.OffGridOrigin;
            }
        }
    }

    // Constructor for LevelLoader (Backwards compatibility / Defaults)
    public PlaceCommand(PuzzlePiece piece, Vector2Int gridPosition, PlacedObjectTypeSO.Dir direction, Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> currentPassengers)
        : this(piece, gridPosition, direction, prevPos, prevRot, currentPassengers, false, false, null, PlacedObjectTypeSO.Dir.Down)
    {
    }

    public bool Execute()
    {
        // 1. Очистка поточного стану (якщо це Redo)
        CleanupCurrentState();

        // 2. Тимчасово приєднуємо пасажирів, щоб вони рухалися разом з тулзою
        // Це критично для коректного переміщення, бо TeleportTo миттєвий
        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;

                // Переконуємось, що пасажир не на гріді логічно
                if (p.IsPlaced) GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                else if (p.IsOffGrid) OffGridManager.RemovePiece(p);

                // Приєднуємо до тулзи (AddPassenger також вимикає фізику)
                // Це гарантує, що коли тулза стрибне, пасажири стрибнуть з нею
                piece.AddPassenger(p);
            }
        }

        // 3. Розміщуємо Тулзу на гріді (Логічно)
        PlacedObject placedObjectComponent = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, gridPosition, direction);

        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            piece.SetInfrastructure(placedObjectComponent);
        else
            piece.SetPlaced(placedObjectComponent);

        // Обчислюємо нову візуальну позицію
        float cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
        Vector3 offset = new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        Vector3 finalPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(gridPosition.x, gridPosition.y) + offset;

        // 4. Фізично переміщуємо тулзу (і приєднаних пасажирів)
        piece.UpdateTransform(finalPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));

        // 5. Розміщуємо пасажирів на нових місцях (Логічно та візуально від'єднуємо)
        if (passengersSnapshot.Count > 0)
        {
            // Очищаємо список в тулзі, бо зараз ми їх "висаджуємо" на грід
            piece.StoredPassengers.Clear();

            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;

                // Оновлюємо логічний напрямок пасажира відповідно до його візуального повороту
                // Це важливо, якщо тулза була повернута в процесі
                p.SyncDirectionFromRotation(p.transform.rotation);

                // Від'єднуємо від тулзи (логічно батьком стає загальний контейнер або нічого, залежно від реалізації SetParent(null) або іншого)
                // В даному випадку, ми просто змінюємо parent на той, що у тулзи (GridArea або World)
                p.transform.SetParent(piece.transform.parent);

                // Обчислюємо координати гріда на основі нової світової позиції
                Vector3 pWorldPos = p.transform.position;
                GridBuildingSystem.Instance.GetGrid().GetXZ(pWorldPos, out int px, out int pz);
                
                // Враховуємо офсет повороту самого кота
                Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
                Vector2Int pOrigin = new Vector2Int(px, pz) - pRotOffset;

                // Розміщуємо на гріді
                var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
                p.SetPlaced(po);

                // Фінальне ідеальне вирівнювання (Snap) по центру клітинки
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
        CleanupCurrentState();

        // Відновлюємо пасажирів (приєднуємо до тулзи перед стрибком назад)
        if (passengersSnapshot.Count > 0)
        {
            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                piece.AddPassenger(p); 
            }
        }

        // Повертаємо тулзу назад (разом з пасажирами)
        piece.UpdateTransform(prevPosition, prevRotation);
        piece.SyncPassengersRotation(); // Синхронізуємо поворот після повернення

        if (wasOnGrid)
        {
            // Force Place на старе місце використовуючи ПОПЕРЕДНІЙ напрямок (для коректного розвороту)
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, prevGridOrigin, prevDirection);

            if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                piece.SetInfrastructure(po);
                // Розміщуємо пасажирів назад на грід (від'єднуємо від тулзи)
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
            // Якщо OffGrid, то пасажири залишаються на тулзі (логіка OffGrid зазвичай тримає їх разом)
        }
        else
        {
            piece.SetOffGrid(false);
        }

        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    private void CleanupCurrentState()
    {
        // Знімаємо тулзу з поточної позиції
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

        // Знімаємо пасажирів з їх поточних позицій
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
        var grid = GridBuildingSystem.Instance.GetGrid();
        float cellSize = grid.GetCellSize();
        
        // Копіюємо список, бо будемо змінювати StoredPassengers
        List<PuzzlePiece> currentPassengers = new List<PuzzlePiece>(tool.StoredPassengers);

        foreach (var p in currentPassengers)
        {
            // Від'єднуємо від тулзи
            p.transform.SetParent(tool.transform.parent);
            p.SyncDirectionFromRotation(p.transform.rotation);

            Vector3 worldPos = p.transform.position;
            grid.GetXZ(worldPos, out int x, out int z);
            Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int pOrigin = new Vector2Int(x, z) - pRotOffset;

            // Force Place
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
            p.SetPlaced(po);
            
            // Забезпечуємо візуальне вирівнювання і тут
             Vector3 snapPos = GridBuildingSystem.Instance.GetGrid().GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
            p.UpdateTransform(snapPos, p.transform.rotation);
        }
        tool.StoredPassengers.Clear();
    }
}
