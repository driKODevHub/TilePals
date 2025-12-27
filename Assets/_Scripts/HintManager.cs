using UnityEngine;
using System.Collections.Generic;
using System.Linq;

public class HintManager : MonoBehaviour
{
    public static HintManager Instance { get; private set; }

    [SerializeField] private float hintDuration = 3f;

    private float _hintTimer = 0f;

    private void Awake()
    {
        if (Instance != null) { Destroy(gameObject); return; }
        Instance = this;
    }

    private void Update()
    {
        if (_hintTimer > 0)
        {
            _hintTimer -= Time.deltaTime;
            if (_hintTimer <= 0)
            {
                ClearHint();
            }
        }
    }

    public void ShowHint()
    {
        var activeBoard = GridBuildingSystem.Instance.ActiveBoard;
        if (activeBoard == null || activeBoard.LevelData == null) return;

        var solution = activeBoard.LevelData.puzzleSolution;
        if (solution == null || solution.Count == 0) return;

        // Logic: find a piece that is not correctly placed or not placed at all
        // For simplicity, we'll pick first one that doesn't match a placed object.
        
        GridDataSO.GeneratedPieceData hintPiece = null;
        
        foreach (var solData in solution)
        {
            if (solData.pieceType == null) continue;
            
            // Check if there is an object of this type at this position in current active board grid
            var gridObj = activeBoard.Grid.GetGridObject(solData.position.x, solData.position.y);
            if (gridObj == null) continue;
            
            var placed = gridObj.GetPlacedObject();
            if (placed == null) 
            {
                hintPiece = solData;
                break;
            }
            
            // If it exists, check if it's the right type and direction
            // Note: PlacedObject needs to store direction too or PuzzlePiece needs to be checked
            var piece = placed.GetComponent<PuzzlePiece>();
            if (piece == null || piece.PieceTypeSO != solData.pieceType || piece.CurrentDirection != solData.direction)
            {
                hintPiece = solData;
                break;
            }
        }

        if (hintPiece != null)
        {
            var cells = hintPiece.pieceType.GetGridPositionsList(hintPiece.position, hintPiece.direction);
            GridVisualManager.Instance.SetHintCells(cells);
            _hintTimer = hintDuration;
            Debug.Log($"Showing hint for {hintPiece.pieceType.name} at {hintPiece.position}");
        }
    }

    public void ClearHint()
    {
        _hintTimer = 0;
        if (GridVisualManager.Instance != null)
        {
            GridVisualManager.Instance.SetHintCells(null);
        }
    }
}
