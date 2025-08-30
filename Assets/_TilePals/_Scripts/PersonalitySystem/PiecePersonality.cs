using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

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
    [SerializeField] private EmotionProfileSO curiousEmotion;

    [Header("Налаштування Поведінки")]
    [Tooltip("Мінімальний та максимальний час в секундах до засинання.")]
    [SerializeField] private float timeToSleepMin = 15f;
    [SerializeField] private float timeToSleepMax = 30f;
    [Tooltip("Мінімальний та максимальний час в секундах до самостійного пробудження.")]
    [SerializeField] private float timeToWakeMin = 10f;
    [SerializeField] private float timeToWakeMax = 20f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [Tooltip("Швидкість миші, до якої рух вважається 'ніжним гладженням'.")]
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [Tooltip("Швидкість миші, вище якої рух вважається 'лоскотом'.")]
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("Налаштування погляду")]
    [SerializeField] private float idleLookIntervalMin = 1.5f;
    [SerializeField] private float idleLookIntervalMax = 4f;
    [SerializeField] private float idleLookDurationMin = 1f;
    [SerializeField] private float idleLookDurationMax = 2.5f;
    [SerializeField] private float idleLookRadius = 3f;
    [Tooltip("Радіус в 'юнітах', в якому фігура реагує на іншу фігуру, що пролітає над нею.")]
    [SerializeField] private float flyOverReactionRadius = 2.5f;


    [Header("Посилання на Компоненти")]
    [SerializeField] private FacialExpressionController facialController;

    private float _currentFatigue, _currentIrritation, _currentTrust;
    private bool _isHeld, _isSleeping, _isBeingPetted;
    private Coroutine _sleepCoroutine, _shakenCoroutine, _reactionCoroutine, _idleLookCoroutine, _wakeUpCoroutine;
    private PuzzlePiece _puzzlePiece;
    private EmotionProfileSO _lastPettingEmotion;
    private bool _isLookingRandomly = false;

    private enum IdleGazeState { LookAtRandom, LookAtNeighbor, LookAtPlayer, Wait }

    public float GetFlyOverRadius() => flyOverReactionRadius;

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
        PersonalityEventManager.OnPieceFlyOver += HandlePieceFlyOver;
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
        PersonalityEventManager.OnPieceFlyOver -= HandlePieceFlyOver;
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

        if (_puzzlePiece != null)
        {
            _puzzlePiece.SetTemperamentMaterial(_temperament.temperamentMaterial);
        }

        _currentFatigue = _temperament.initialFatigue;
        _currentIrritation = _temperament.initialIrritation;
        _currentTrust = _temperament.initialTrust;

        ReturnToNeutralState();
    }

    private void StopAllBehaviorCoroutines()
    {
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);
        if (_idleLookCoroutine != null) StopCoroutine(_idleLookCoroutine);
        if (_wakeUpCoroutine != null) StopCoroutine(_wakeUpCoroutine);
        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        if (_reactionCoroutine != null) StopCoroutine(_reactionCoroutine);
    }

    private void HandlePettingStart(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece || _isHeld) return;

        StopAllBehaviorCoroutines();
        _isBeingPetted = true;
        _isSleeping = false;
        _isLookingRandomly = false;

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
        if (!_isHeld && !_isSleeping && !_isBeingPetted && !_isLookingRandomly && facialController != null)
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

        StopAllBehaviorCoroutines();
        _isHeld = true;
        _isSleeping = false;
        _isBeingPetted = false;
        _isLookingRandomly = false;

        SetEmotion(pickedUpEmotion);
        if (facialController != null)
        {
            facialController.UpdateSortingOrder(true);
            facialController.ResetPupilPosition();
        }
    }

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        _isHeld = false;
        SetEmotion(droppedEmotion);
        if (facialController != null) facialController.UpdateSortingOrder(false);

        StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
    }

    private void HandlePieceShaken(PuzzlePiece piece, float velocity)
    {
        if (piece != _puzzlePiece || _isSleeping) return;

        float irritationGain = 0.05f * _temperament.irritationModifier;
        _currentIrritation = Mathf.Clamp01(_currentIrritation + irritationGain);

        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        _shakenCoroutine = StartCoroutine(ShowShakenEmotion());
    }

    private void HandlePieceFlyOver(PuzzlePiece stationaryPiece)
    {
        if (stationaryPiece != _puzzlePiece || _isHeld) return;

        bool wasSleeping = _isSleeping;
        if (_isSleeping)
        {
            StopAllBehaviorCoroutines();
            _isSleeping = false;
        }

        if (wasSleeping)
        {
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
        else if (!_isLookingRandomly)
        {
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
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
        if (newNeighbor != null) neighborsToCheck.Add(newNeighbor);
        else neighborsToCheck.AddRange(FindAllNeighbors());

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
                    break;
                }
            }
        }
    }

    private List<PuzzlePiece> FindAllNeighbors()
    {
        List<PuzzlePiece> neighbors = new List<PuzzlePiece>();
        if (!_puzzlePiece.IsPlaced || _puzzlePiece.PlacedObjectComponent == null) return neighbors;

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
                    PuzzlePiece neighborPiece = gridObject.GetPlacedObject().GetComponentInParent<PuzzlePiece>();
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
        if (_isHeld || _isBeingPetted) yield break;

        StopAllBehaviorCoroutines();
        _isLookingRandomly = true;

        SetEmotion(reactionEmotion);
        yield return new WaitForSeconds(duration);

        if (!_isHeld) ReturnToNeutralState();
    }

    private void ReturnToNeutralState()
    {
        StopAllBehaviorCoroutines();
        _isSleeping = false;
        _isLookingRandomly = false;

        SetEmotion(neutralEmotion);
        _sleepCoroutine = StartCoroutine(SleepTimer());
        _idleLookCoroutine = StartCoroutine(IdleLookRoutine());
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
        yield return new WaitForSeconds(Random.Range(timeToSleepMin, timeToSleepMax));
        if (_isBeingPetted || _isHeld) yield break;

        StopAllBehaviorCoroutines();
        _isLookingRandomly = false;
        _isSleeping = true;
        SetEmotion(sleepingEmotion);
        _wakeUpCoroutine = StartCoroutine(WakeUpTimer());
    }

    private IEnumerator WakeUpTimer()
    {
        yield return new WaitForSeconds(Random.Range(timeToWakeMin, timeToWakeMax));
        if (_isHeld) yield break;

        ReturnToNeutralState();
    }

    private IEnumerator ShowShakenEmotion()
    {
        StopAllBehaviorCoroutines();
        _isLookingRandomly = false;

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
            if (facialController != null) facialController.LookAt(targetPoint);
        }
    }

    private IEnumerator IdleLookRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleLookIntervalMin, idleLookIntervalMax));
            if (_isHeld || _isSleeping || _isBeingPetted) continue;

            _isLookingRandomly = true;

            // --- ОНОВЛЕНО: Використання сучасного методу FindObjectsByType ---
            var allPieces = FindObjectsByType<PiecePersonality>(FindObjectsSortMode.None).Where(p => p != this && p.gameObject.activeInHierarchy).ToList();
            bool hasNeighbors = allPieces.Any();

            IdleGazeState nextAction = hasNeighbors
                ? (IdleGazeState)Random.Range(0, 4)
                : (IdleGazeState)Random.Range(0, 3) == 0 ? IdleGazeState.LookAtPlayer : IdleGazeState.LookAtRandom;

            if (facialController != null)
            {
                switch (nextAction)
                {
                    case IdleGazeState.LookAtRandom:
                        Vector3 randomDirection = Random.insideUnitSphere * idleLookRadius;
                        randomDirection.y = 0;
                        Vector3 lookTarget = transform.position + randomDirection;
                        facialController.LookAt(lookTarget);
                        break;

                    case IdleGazeState.LookAtNeighbor:
                        if (hasNeighbors)
                        {
                            PiecePersonality targetPiece = allPieces[Random.Range(0, allPieces.Count)];
                            facialController.LookAt(targetPiece.transform.position);
                        }
                        break;

                    case IdleGazeState.LookAtPlayer:
                        facialController.ResetPupilPosition();
                        break;

                    case IdleGazeState.Wait:
                        break;
                }
            }

            yield return new WaitForSeconds(Random.Range(idleLookDurationMin, idleLookDurationMax));

            _isLookingRandomly = false;
        }
    }
}

