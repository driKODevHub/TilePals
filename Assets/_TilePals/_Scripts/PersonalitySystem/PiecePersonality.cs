using UnityEngine;
using System.Collections;
using System.Collections.Generic;

[RequireComponent(typeof(PuzzlePiece))]
public class PiecePersonality : MonoBehaviour
{
    private TemperamentSO _temperament;

    [Header("Профілі Емоцій")]
    [SerializeField] private EmotionProfileSO neutralEmotion;
    [SerializeField] private EmotionProfileSO sleepingEmotion;
    [SerializeField] private EmotionProfileSO pickedUpEmotion;
    [SerializeField] private EmotionProfileSO droppedEmotion;
    [SerializeField] private EmotionProfileSO shakenEmotion;
    [SerializeField] private EmotionProfileSO pettingGentleEmotion;
    [SerializeField] private EmotionProfileSO pettingTickleEmotion;
    [SerializeField] private EmotionProfileSO pettingAnnoyedEmotion;

    [Header("Налаштування Поведінки")]
    [SerializeField] private float timeToSleep = 10f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [Tooltip("Швидкість миші, до якої рух вважається 'ніжним гладженням'.")]
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [Tooltip("Швидкість миші, вище якої рух вважається 'лоскотом'.")]
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("Посилання на Компоненти")]
    [SerializeField] private FacialExpressionController facialController;

    // --- ВИДАЛЕНО: поле для індикатора більше не потрібне ---
    // [SerializeField] private MeshRenderer temperamentIndicator;

    private float _currentFatigue, _currentIrritation, _currentTrust;
    private bool _isHeld, _isSleeping, _isBeingPetted;
    private Coroutine _sleepCoroutine, _shakenCoroutine, _reactionCoroutine;
    private PuzzlePiece _puzzlePiece;
    private EmotionProfileSO _lastPettingEmotion;

    private void Awake()
    {
        _puzzlePiece = GetComponent<PuzzlePiece>();
        if (facialController == null)
            facialController = GetComponentInChildren<FacialExpressionController>();
    }

    private void OnEnable()
    {
        PersonalityEventManager.OnPiecePickedUp += HandlePiecePickedUp;
        PersonalityEventManager.OnPieceDropped += HandlePieceDropped;
        PersonalityEventManager.OnPieceShaken += HandlePieceShaken;
        PersonalityEventManager.OnPiecePlaced += HandlePiecePlaced;
        PersonalityEventManager.OnPettingStart += HandlePettingStart;
        PersonalityEventManager.OnPettingUpdate += HandlePettingUpdate;
        PersonalityEventManager.OnPettingEnd += HandlePettingEnd;
    }

    private void OnDisable()
    {
        PersonalityEventManager.OnPiecePickedUp -= HandlePiecePickedUp;
        PersonalityEventManager.OnPieceDropped -= HandlePieceDropped;
        PersonalityEventManager.OnPieceShaken -= HandlePieceShaken;
        PersonalityEventManager.OnPiecePlaced -= HandlePiecePlaced;
        PersonalityEventManager.OnPettingStart -= HandlePettingStart;
        PersonalityEventManager.OnPettingUpdate -= HandlePettingUpdate;
        PersonalityEventManager.OnPettingEnd -= HandlePettingEnd;
    }

    public void Setup(TemperamentSO newTemperament)
    {
        _temperament = newTemperament;
        if (_temperament == null)
        {
            Debug.LogError("Спроба ініціалізувати фігуру без темпераменту!", this);
            this.enabled = false;
            return;
        }

        // --- ОНОВЛЕНИЙ КОД ---
        // Передаємо матеріал до компонента PuzzlePiece для коректного застосування
        if (_puzzlePiece != null)
        {
            _puzzlePiece.SetTemperamentMaterial(_temperament.temperamentMaterial);
        }

        _currentFatigue = _temperament.initialFatigue;
        _currentIrritation = _temperament.initialIrritation;
        _currentTrust = _temperament.initialTrust;

        ReturnToNeutralState();
    }

    // ... (решта коду залишається без змін)

    private void HandlePettingStart(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece || _isHeld) return;

        _isBeingPetted = true;
        _isSleeping = false;
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(neutralEmotion);
    }

    private void HandlePettingUpdate(PuzzlePiece piece, float mouseSpeed)
    {
        if (piece != _puzzlePiece || !_isBeingPetted) return;

        EmotionProfileSO targetEmotion = null;

        if (mouseSpeed > tickleSpeedThreshold)
        {
            targetEmotion = _currentIrritation < 0.7f ? pettingTickleEmotion : pettingAnnoyedEmotion;
            _currentIrritation = Mathf.Clamp01(_currentIrritation + 0.005f * _temperament.irritationModifier);
        }
        else if (mouseSpeed > gentlePettingSpeedThreshold)
        {
            targetEmotion = pettingGentleEmotion;
            _currentTrust = Mathf.Clamp01(_currentTrust + 0.002f * _temperament.trustModifier);
            _currentIrritation = Mathf.Clamp01(_currentIrritation - 0.002f);
        }

        if (targetEmotion != null && targetEmotion != _lastPettingEmotion)
        {
            SetEmotion(targetEmotion);
            _lastPettingEmotion = targetEmotion;
        }
    }

    private void HandlePettingEnd(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece || !_isBeingPetted) return;

        _isBeingPetted = false;
        _lastPettingEmotion = null;
    }

    public void TriggerExternalReaction(EmotionProfileSO reactionEmotion, float duration)
    {
        if (_reactionCoroutine != null) StopCoroutine(_reactionCoroutine);
        _reactionCoroutine = StartCoroutine(ShowReactionEmotion(reactionEmotion, duration));
    }

    private void Update()
    {
        if (!_isHeld && !_isSleeping && !_isBeingPetted && facialController != null)
        {
            LookAtCursor();
        }
    }

    public void SetEmotion(EmotionProfileSO emotion)
    {
        if (facialController != null && emotion != null)
        {
            facialController.ApplyEmotion(emotion);
        }
    }

    private void HandlePiecePickedUp(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        _isHeld = true;
        _isSleeping = false;
        _isBeingPetted = false;
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(pickedUpEmotion);
        facialController.UpdateSortingOrder(true);
    }

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        _isHeld = false;
        SetEmotion(droppedEmotion);
        facialController.UpdateSortingOrder(false);
        StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
    }

    private void HandlePieceShaken(PuzzlePiece piece, float velocity)
    {
        if (piece != _puzzlePiece || _isSleeping) return;

        float irritationGain = 0.05f * _temperament.irritationModifier;
        _currentIrritation = Mathf.Clamp01(_currentIrritation + irritationGain);
        Debug.Log($"{_temperament.name} роздратований від тряски! Нове роздратування: {_currentIrritation:F2}");

        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        _shakenCoroutine = StartCoroutine(ShowShakenEmotion());
    }

    private void HandlePiecePlaced(PuzzlePiece placedPiece)
    {
        if (placedPiece != _puzzlePiece && !_isHeld && !_isSleeping)
        {
            CheckForNeighborReaction(placedPiece);
        }
        else if (placedPiece == _puzzlePiece)
        {
            CheckForNeighborReaction(null);
        }
    }

    private void CheckForNeighborReaction(PuzzlePiece newNeighbor)
    {
        List<PuzzlePiece> neighborsToCheck = new List<PuzzlePiece>();
        if (newNeighbor != null)
        {
            neighborsToCheck.Add(newNeighbor);
        }
        else
        {
            neighborsToCheck.AddRange(FindAllNeighbors());
        }

        foreach (var neighbor in neighborsToCheck)
        {
            if (neighbor == null) continue;

            var neighborPersonality = neighbor.GetComponent<PiecePersonality>();
            if (neighborPersonality == null || neighborPersonality._temperament == null) continue;

            foreach (var rule in _temperament.synergyRules)
            {
                if (rule.neighborTemperament == neighborPersonality._temperament)
                {
                    this.TriggerExternalReaction(rule.myReaction, rule.reactionDuration);
                    neighborPersonality.TriggerExternalReaction(rule.neighborReaction, rule.reactionDuration);

                    Debug.Log($"{_temperament.name} реагує на {neighborPersonality._temperament.name}!");
                    break;
                }
            }
        }
    }

    private List<PuzzlePiece> FindAllNeighbors()
    {
        List<PuzzlePiece> neighbors = new List<PuzzlePiece>();
        if (!_puzzlePiece.IsPlaced) return neighbors;

        var grid = GridBuildingSystem.Instance.GetGrid();
        List<Vector2Int> myCells = _puzzlePiece.PlacedObjectComponent.GetGridPositionList();
        HashSet<PuzzlePiece> foundNeighbors = new HashSet<PuzzlePiece>();

        foreach (var cell in myCells)
        {
            Vector2Int[] neighborCoords = {
                new Vector2Int(cell.x + 1, cell.y), new Vector2Int(cell.x - 1, cell.y),
                new Vector2Int(cell.x, cell.y + 1), new Vector2Int(cell.x, cell.y - 1)
            };

            foreach (var coord in neighborCoords)
            {
                GridObject gridObject = grid.GetGridObject(coord.x, coord.y);
                if (gridObject != null && gridObject.IsOccupied())
                {
                    PuzzlePiece neighborPiece = gridObject.GetPlacedObject().GetComponent<PuzzlePiece>();
                    if (neighborPiece != null && neighborPiece != _puzzlePiece)
                    {
                        foundNeighbors.Add(neighborPiece);
                    }
                }
            }
        }
        return new List<PuzzlePiece>(foundNeighbors);
    }

    private IEnumerator ShowReactionEmotion(EmotionProfileSO reactionEmotion, float duration)
    {
        if (_isSleeping || _isBeingPetted) yield break;
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(reactionEmotion);
        yield return new WaitForSeconds(duration);

        if (_isHeld) SetEmotion(pickedUpEmotion);
        else if (_isSleeping) SetEmotion(sleepingEmotion);
        else ReturnToNeutralState();
    }

    private void ReturnToNeutralState()
    {
        _isSleeping = false;
        SetEmotion(neutralEmotion);
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);
        _sleepCoroutine = StartCoroutine(SleepTimer());
    }

    private IEnumerator ReturnToNeutralAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isHeld)
        {
            ReturnToNeutralState();
        }
    }

    private IEnumerator SleepTimer()
    {
        yield return new WaitForSeconds(timeToSleep);
        if (_isBeingPetted || _isHeld) yield break;
        _isSleeping = true;
        SetEmotion(sleepingEmotion);
    }

    private IEnumerator ShowShakenEmotion()
    {
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(shakenEmotion);
        yield return new WaitForSeconds(shakenEmotionDuration);

        if (_isHeld)
        {
            SetEmotion(pickedUpEmotion);
        }
    }

    private void LookAtCursor()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Plane plane = new Plane(Vector3.up, transform.position);
        if (plane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);
            facialController.LookAt(targetPoint);
        }
    }
}
