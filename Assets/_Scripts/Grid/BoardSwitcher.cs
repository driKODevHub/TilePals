using UnityEngine;

public class BoardSwitcher : MonoBehaviour
{
    public PuzzleBoard targetBoard;
    public GameObject highlightVisual;

    private void Awake()
    {
        if (targetBoard == null)
        {
            targetBoard = GetComponentInParent<PuzzleBoard>();
        }
    }

    private void Start()
    {
        if (highlightVisual != null) highlightVisual.SetActive(false);
    }

    private void OnMouseEnter()
    {
        if (highlightVisual != null) highlightVisual.SetActive(true);
    }

    private void OnMouseExit()
    {
        if (highlightVisual != null) highlightVisual.SetActive(false);
    }

    private void OnMouseDown()
    {
        if (targetBoard != null)
        {
            GridBuildingSystem.Instance.SetActiveBoard(targetBoard);
            if (CameraController.Instance != null) CameraController.Instance.FocusOnBoard(targetBoard);
            Debug.Log($"Switched active board to {targetBoard.boardId}");
        }
    }

    public void Initialize(PuzzleBoard target)
    {
        targetBoard = target;
        if (highlightVisual != null) highlightVisual.SetActive(false); // Reset visual
    }
}
