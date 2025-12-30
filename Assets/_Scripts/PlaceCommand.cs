using UnityEngine;
using System.Collections.Generic;

public class PlaceCommand : ICommand
{
    private PuzzlePiece piece;
    private Vector2Int origin;
    private PlacedObjectTypeSO.Dir direction;

    private Vector3 prevPosition;
    private Quaternion prevRotation;
    private bool wasOnGrid;
    private bool wasOffGrid;
    private Vector2Int prevGridOrigin;
    private Vector2Int prevOffGridOrigin;
    
    // Additional snapshot for direction flow
    private PlacedObjectTypeSO.Dir prevDirection;

    private List<PuzzlePiece> passengersSnapshot;

    public PlaceCommand(PuzzlePiece piece, Vector2Int origin, PlacedObjectTypeSO.Dir direction,
                        Vector3 prevPos, Quaternion prevRot, List<PuzzlePiece> passengers,
                        bool wasPlaced, bool wasOff, Vector2Int prevOrigin, PlacedObjectTypeSO.Dir prevDir)
    {
        this.piece = piece;
        this.origin = origin;
        this.direction = direction;

        this.prevPosition = prevPos;
        this.prevRotation = prevRot;
        this.wasOnGrid = wasPlaced;
        this.wasOffGrid = wasOff;
        this.prevDirection = prevDir;

        // Ensure we store correct previous state
        if (wasOnGrid) this.prevGridOrigin = prevOrigin;
        if (wasOffGrid) this.prevOffGridOrigin = prevOrigin;

        // Snapshot passengers to restore parent-child relationship on Undo
        this.passengersSnapshot = passengers != null ? new List<PuzzlePiece>(passengers) : new List<PuzzlePiece>();
    }

    public bool Execute()
    {
        // 1. Placing Piece Logic
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null) return false;

        // Ensure we are not already on grid elsewhere (important for Redo/Move)
        CleanupCurrentState();

        var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, origin, direction);
        if (po == null) return false;

        // 2. Setup Piece State
        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
        {
            piece.SetInfrastructure(po);
            // Тулз сам по собі не "забирає" пасажирів при постановці, 
            // якщо тільки ми не реалізуємо логіку "поставити корзину на котів".
            // Поки що залишаємо як є - пасажири додаються коли ставимо КІТІВ на ТУЛЗ.
        }
        else
        {
            piece.SetPlaced(po);
            
            // Check if we are landing ON A TOOL (Logic: All cells must belong to the SAME tool)
            PuzzleBoard board = GridBuildingSystem.Instance.ActiveBoard; 
            if (board != null)
            {
                List<Vector2Int> cells = piece.PlacedObjectComponent.GetGridPositionList();
                PuzzlePiece potentialTool = null;
                bool allCellsOnSameTool = true;
                bool foundAnyTool = false;

                foreach(var cell in cells) 
                {
                    if (!board.Grid.IsValidGridPosition(cell)) { allCellsOnSameTool = false; break; }
                    var obj = board.Grid.GetGridObject(cell.x, cell.y);
                    var infra = obj.GetInfrastructureObject();
                    
                    if (infra == null) 
                    { 
                        // Якщо хоч одна клітинка не має інфраструктури (тулза), то ми не "в тулзі"
                        allCellsOnSameTool = false; 
                        break; 
                    }

                    var toolPiece = infra.GetComponent<PuzzlePiece>();
                    if (toolPiece == null) 
                    { 
                        allCellsOnSameTool = false; 
                        break; 
                    }

                    if (potentialTool == null) 
                    {
                        potentialTool = toolPiece;
                        foundAnyTool = true;
                    }
                    else if (potentialTool != toolPiece) 
                    { 
                        // Різні тулзи під однією фігурою
                        allCellsOnSameTool = false; 
                        Debug.Log($"[PlaceCommand] Piece overlaps multiple tools: {potentialTool.name} and {toolPiece.name}");
                        break; 
                    }
                }

                if (foundAnyTool && allCellsOnSameTool && potentialTool != null)
                {
                    potentialTool.AddPassenger(piece);
                }

            }
        }

        piece.SetOffGrid(false);
        
        // 3. Update Transform
        float cellSize = activeBoard.Grid.GetCellSize();
        Vector3 cellWorldPos = activeBoard.Grid.GetWorldPosition(origin.x, origin.y);
        
        Vector2Int rotationOffset = piece.PieceTypeSO.GetRotationOffset(direction);
        Vector3 snapPos = cellWorldPos + new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;
        
        // Оновлюємо трансформ. Якщо це пасажир, він вже прикріплений до батька в AddPassenger?, 
        // АЛЕ AddPassenger робить SetParent.
        // UpdateTransform може збити локальні координати, якщо ми не обережні.
        // Проте тут ми ставимо глобальну позицію, що правильно для початкового розміщення.
        piece.UpdateTransform(snapPos, Quaternion.Euler(0, piece.PieceTypeSO.GetRotationAngle(direction), 0));

        // 4. Handle Passengers (if any were stored - e.g. moving a tool with passengers)
        if (passengersSnapshot != null && passengersSnapshot.Count > 0)
        {
            // Ми перемістили тулз. Пасажири вже в StoredPassengers (якщо ми їх не чистили).
            // АЛЕ в PickUpPiece ми їх могли очистити з гріда.
            // Тут треба їх повернути НА ГРІД.

            piece.StoredPassengers.Clear(); // Очищаємо, щоб перезаповнити з snapshot (надійніше)

            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;

                // AddPassenger сам встановить батьківство зі збереженням scale
                piece.AddPassenger(p); // Додаємо в список і фіксуємо батьківство

                // Повертаємо на грід
                // Логіка: позиція пасажира відносно тулза має зберегтися.
                // Ми знаємо поточну позицію тулза (origin).
                // Але простіше взяти поточну world position пасажира і знайти клітинку.
                
                // ВАЖЛИВО: Оновити ротацію visual ДО розрахунку гріда, якщо вона змінилася?
                // Ні, ротація тулза змінилася, пасажири обернулися разом з ним (бо діти).
                // Тому їх world position і rotation правильні візуально.
                
                p.SyncDirectionFromRotation(p.transform.rotation); // Синхронізуємо логічний напрям

                Vector3 pWorldPos = p.transform.position;
                Vector3 gridOriginPos = activeBoard.Grid.GetWorldPosition(0, 0); // Get origin of grid
                // cellSize already defined in scope
                // float cellSize = activeBoard.Grid.GetCellSize();
                
                // Використовуємо RoundToInt для уникнення проблем з плаваючою комою (0.99 -> 1, not 0)
                int px = Mathf.RoundToInt((pWorldPos.x - gridOriginPos.x) / cellSize);
                int pz = Mathf.RoundToInt((pWorldPos.z - gridOriginPos.z) / cellSize);
                
                // Перевірка
                // activeBoard.Grid.GetXZ(pWorldPos, out int px, out int pz); // OLD WAY causing drift

                Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
                Vector2Int pOrigin = new Vector2Int(px, pz) - pRotOffset;

                var poPassenger = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
                p.SetPlaced(poPassenger);
                
                // Вирівнюємо ідеально по гріду, щоб уникнути дріфту
                Vector3 pSnapPos = activeBoard.Grid.GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
                
                // Використовуємо локальну корекцію, щоб не смикати
                p.UpdateTransform(pSnapPos, p.transform.rotation);
            }
        }

        PersonalityEventManager.RaisePiecePlaced(piece);
        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();

        return true;
    }

    public void Undo()
    {
        CleanupCurrentState(); // Забираємо з гріда
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null) return;

        // Повертаємо фігуру на попередню позицію
        piece.UpdateTransform(prevPosition, prevRotation);
        piece.SyncDirectionFromRotation(prevRotation);

        // ВІДНОВЛЕННЯ ПАСАЖИРІВ (Якщо це ТУЛЗ)
        if (passengersSnapshot != null && passengersSnapshot.Count > 0)
        {
            // Очищаємо поточних, якщо є (хоча Cleanup мав би забрати)
            piece.StoredPassengers.Clear();

            foreach (var p in passengersSnapshot)
            {
                if (p == null) continue;
                
                // AddPassenger сам встановить батьківство зі збереженням scale
                piece.AddPassenger(p);
            }
            // Синхронізуємо їх позиції/ротації, оскільки батько стрибнув
            piece.SyncPassengersRotation(); 
        }

        if (wasOnGrid)
        {
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(piece, prevGridOrigin, prevDirection);

            if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
            {
                piece.SetInfrastructure(po);
                // Тепер треба повернути пасажирів НА ГРІД (логічно), бо ми відкотили "PickUp -> Drop" (або Move).
                // Якщо це Undo Placement, то ми тулз ЗАБИРАЄМО (Cleanup зробив це). 
                // А стоп... Цей Undo викликається коли ми хочемо СКАСУВАТИ дію PlaceCommand.
                // Дія: Ми взяли фігуру (вона була десь) і поставили сюди.
                // Undo: Забрати звідси і повернути "де була".
                
                // Якщо фігура була "on grid" раніше (PickAndMove), то ми її повернули на prevGridOrigin.
                // І пасажирів теж треба "поставити" на старе місце.
                RestorePassengersOnGridAfterUndo(piece);
            }
            else
            {
                piece.SetPlaced(po);
            }
        }
        else if (wasOffGrid)
        {
            // Якщо була OffGrid, то пасажири (якщо є) просто висять на ній в OffGrid.
            activeBoard.OffGridTracker.PlacePiece(piece, prevOffGridOrigin);
            piece.SetOffGrid(true, prevOffGridOrigin);
        }
        else
        {
            piece.SetOffGrid(false);
        }

        if (GridVisualManager.Instance != null) GridVisualManager.Instance.RefreshAllCellVisuals();
    }

    private void CleanupCurrentState()
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;

        if (piece.IsPlaced)
        {
            GridBuildingSystem.Instance.RemovePieceFromGrid(piece);
            piece.SetPlaced(null);
            piece.SetInfrastructure(null);
        }
        else if (piece.IsOffGrid)
        {
            activeBoard?.OffGridTracker.RemovePiece(piece);
        }
        piece.SetOffGrid(false);

        // Якщо це ТУЛЗ, нам треба також забрати з гріда його ПАСАЖИРІВ, 
        // бо ми зараз будемо переміщати тулз назад.
        if (piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.UnlockGrid)
        {
             // Копіюємо список, бо RemovePieceFromGrid може не чіпати StoredPassengers,
             // але ми хочемо бути певні.
             var passengers = new List<PuzzlePiece>(piece.StoredPassengers);
             foreach(var p in passengers)
             {
                 if(p.IsPlaced) 
                 {
                     GridBuildingSystem.Instance.RemovePieceFromGrid(p);
                     p.SetPlaced(null);
                 }
                 // Не видаляємо з StoredPassengers, бо вони нам треба для переміщення разом з тулзом
             }
        }
    }

    private void RestorePassengersOnGridAfterUndo(PuzzlePiece tool)
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null || activeBoard.Grid == null) return;
        
        var grid = activeBoard.Grid;
        float cellSize = grid.GetCellSize();
        
        // Пасажири вже є дітьми тулза і знаходяться в правильній локальній позиції (після UpdateTransform батька).
        // Нам треба просто зареєструвати їх на гріді.

        foreach (var p in tool.StoredPassengers)
        {
            if (p == null) continue;

            p.SyncDirectionFromRotation(p.transform.rotation);

            Vector3 worldPos = p.transform.position;
            Vector3 gridOriginPos = activeBoard.Grid.GetWorldPosition(0, 0); 
            // float cellSize = grid.GetCellSize(); // Already defined in scope
            
            // RoundToInt fix
            int x = Mathf.RoundToInt((worldPos.x - gridOriginPos.x) / cellSize);
            int z = Mathf.RoundToInt((worldPos.z - gridOriginPos.z) / cellSize);
            
            Vector2Int pRotOffset = p.PieceTypeSO.GetRotationOffset(p.CurrentDirection);
            Vector2Int pOrigin = new Vector2Int(x, z) - pRotOffset;

            // Force Place
            var po = GridBuildingSystem.Instance.PlacePieceOnGrid(p, pOrigin, p.CurrentDirection);
            p.SetPlaced(po);
            
            // Snap visual perfectly just in case
            Vector3 snapPos = grid.GetWorldPosition(pOrigin.x, pOrigin.y) +
                                  new Vector3(pRotOffset.x, 0, pRotOffset.y) * cellSize;
            p.UpdateTransform(snapPos, p.transform.rotation);
        }
    }
}
