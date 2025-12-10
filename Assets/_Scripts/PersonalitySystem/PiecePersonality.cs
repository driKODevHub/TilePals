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
    [SerializeField] private EmotionProfileSO excitedEmotion; // Нова емоція для іграшок

    [Header("Налаштування Поведінки")]
    [SerializeField] private float timeToSleepMin = 15f;
    [SerializeField] private float timeToSleepMax = 30f;
    [SerializeField] private float timeToWakeMin = 10f;
    [SerializeField] private float timeToWakeMax = 20f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("Налаштування погляду (Idle Gaze)")]
    [Tooltip("Висота площини, куди дивиться кіт (Y координата цілі).")]
    [SerializeField] private float lookPlaneHeight = 0.5f;

    [Tooltip("Зміщення центру зони погляду відносно півота (Local Space).")]
    [SerializeField] private Vector3 lookAreaOffset = Vector3.zero;

    [SerializeField] private float idleLookIntervalMin = 1.5f;
    [SerializeField] private float idleLookIntervalMax = 4f;
    [SerializeField] private float idleLookDurationMin = 1f;
    [SerializeField] private float idleLookDurationMax = 2.5f;
    [SerializeField] private float idleLookRadius = 3f;
    [SerializeField] private float flyOverReactionRadius = 2.5f;

    [Header("Посилання на Компоненти")]
    [SerializeField] private FacialExpressionController facialController;

    private float _currentFatigue, _currentIrritation, _currentTrust;
    private bool _isHeld, _isSleeping, _isBeingPetted;
    private Coroutine _sleepCoroutine, _shakenCoroutine, _reactionCoroutine, _idleLookCoroutine, _wakeUpCoroutine;
    private PuzzlePiece _puzzlePiece;
    private EmotionProfileSO _lastPettingEmotion;
    private bool _isLookingRandomly = false;

    // Нова змінна для відстеження об'єкта уваги (іграшка в руках)
    private PuzzlePiece _attentionTarget;

    // --- DEBUG ЗМІННІ ---
    private bool _isDebugActive = false;
    private Coroutine _debugStatsCoroutine;
    private Vector3 _debugLookTarget;
    private bool _debugHasTarget;

    private enum IdleGazeState { LookAtRandom, LookAtNeighbor, LookAtPlayer, Wait, LookAtToy }

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
        if (_temperament == null) return;

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

    private void Update()
    {
        HandleDebugInput();

        if (!_isHeld && !_isSleeping && !_isBeingPetted && facialController != null)
        {
            // Пріоритет: Дивитися на іграшку/предмет уваги
            if (_attentionTarget != null)
            {
                facialController.LookAt(_attentionTarget.transform.position);
                // Можна додати емоцію збудження
                if (curiousEmotion != null && _puzzlePiece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Character)
                {
                    // Це спрощено, в ідеалі перевіряти поточну емоцію, щоб не спамити
                }
            }
            else if (_isLookingRandomly)
            {
                // Логіка в корутині
            }
            else
            {
                LookAtCursor();
            }
        }
    }

    // ... Debug methods ...
    private void HandleDebugInput()
    {
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                if (hit.transform == transform || hit.transform.IsChildOf(transform)) ToggleDebug();
            }
        }
    }
    private void ToggleDebug()
    {
        _isDebugActive = !_isDebugActive;
        if (_isDebugActive) { if (_debugStatsCoroutine == null) _debugStatsCoroutine = StartCoroutine(DebugStatsRoutine()); }
        else { if (_debugStatsCoroutine != null) StopCoroutine(_debugStatsCoroutine); _debugStatsCoroutine = null; }
    }
    private void LogDebug(string message) { if (_isDebugActive) Debug.Log($"<color=cyan>[{name}]</color> {message}"); }
    private IEnumerator DebugStatsRoutine()
    {
        while (_isDebugActive)
        {
            string state = _isSleeping ? "Sleeping" : (_isHeld ? "Held" : (_isBeingPetted ? "Being Petted" : "Idle"));
            Debug.Log($"<color=cyan>[{name}]</color> Stats -> Fatigue: {_currentFatigue:F2}, Irritation: {_currentIrritation:F2}, Trust: {_currentTrust:F2} | State: {state}");
            yield return new WaitForSeconds(1.0f);
        }
    }

    private void HandlePettingStart(PuzzlePiece piece)
    {
        if (piece == _puzzlePiece && !_isHeld)
        {
            // Гладимо ЦЬОГО кота
            LogDebug("Petting Started");
            StopAllBehaviorCoroutines();
            _isBeingPetted = true;
            _isSleeping = false;
            _isLookingRandomly = false;
            SetEmotion(neutralEmotion);
        }
        else if (piece != _puzzlePiece && piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.AttractAttention)
        {
            // Гладимо (або тримаємо) ІГРАШКУ - привертаємо увагу
            if (!_isSleeping && !_isHeld)
            {
                _attentionTarget = piece;
                SetEmotion(excitedEmotion != null ? excitedEmotion : curiousEmotion);
                StopAllBehaviorCoroutines(); // Зупиняємо рандомний погляд, щоб дивитись на іграшку
            }
        }
    }

    private void HandlePettingUpdate(PuzzlePiece piece, float mouseSpeed)
    {
        if (piece != _puzzlePiece || !_isBeingPetted) return;
        if (_temperament == null) return;

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
        if (piece == _puzzlePiece)
        {
            if (!_isBeingPetted) return;
            LogDebug("Petting Ended");
            _isBeingPetted = false;
            _lastPettingEmotion = null;
            ReturnToNeutralState();
        }
        else if (piece == _attentionTarget)
        {
            // Перестали взаємодіяти з іграшкою
            _attentionTarget = null;
            ReturnToNeutralState();
        }
    }

    public void TriggerExternalReaction(EmotionProfileSO reactionEmotion, float duration)
    {
        if (_reactionCoroutine != null) StopCoroutine(_reactionCoroutine);
        _reactionCoroutine = StartCoroutine(ShowReactionEmotion(reactionEmotion, duration));
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
        if (piece == _puzzlePiece)
        {
            LogDebug("Picked Up");
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
        else if (piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Toy ||
                 piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Food)
        {
            // Якщо підняли іграшку, кіт може зацікавитись
            if (!_isSleeping && !_isHeld && Vector3.Distance(transform.position, piece.transform.position) < idleLookRadius * 1.5f)
            {
                _attentionTarget = piece;
                SetEmotion(curiousEmotion);
                StopAllBehaviorCoroutines();
            }
        }
    }

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece == _puzzlePiece)
        {
            LogDebug("Dropped (Off Grid)");
            _isHeld = false;
            SetEmotion(droppedEmotion);
            if (facialController != null) facialController.UpdateSortingOrder(false);
            StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
        }
        else if (piece == _attentionTarget)
        {
            // Іграшку кинули
            _attentionTarget = null;
            ReturnToNeutralState();
        }
    }

    private void HandlePieceShaken(PuzzlePiece piece, float velocity)
    {
        if (piece != _puzzlePiece || _isSleeping) return;
        if (_temperament == null) return;

        float irritationGain = 0.05f * _temperament.irritationModifier;
        _currentIrritation = Mathf.Clamp01(_currentIrritation + irritationGain);

        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        _shakenCoroutine = StartCoroutine(ShowShakenEmotion());
    }

    private void HandlePiecePlaced(PuzzlePiece placedPiece)
    {
        if (placedPiece == _puzzlePiece)
        {
            LogDebug("Placed (On Grid)");
            _isHeld = false;

            if (facialController != null) facialController.UpdateSortingOrder(false);

            CheckForNeighborReaction(null);
            StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
        }
        else if (!_isHeld && !_isSleeping)
        {
            CheckForNeighborReaction(placedPiece);
        }
    }

    private void HandlePieceFlyOver(PuzzlePiece stationaryPiece)
    {
        if (stationaryPiece != _puzzlePiece || _isHeld) return;
        if (_isSleeping)
        {
            StopAllBehaviorCoroutines();
            _isSleeping = false;
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
        else if (!_isLookingRandomly)
        {
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
    }

    private void CheckForNeighborReaction(PuzzlePiece newNeighbor)
    {
        if (_temperament == null) return;

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
        LogDebug("Neutral State");
        StopAllBehaviorCoroutines();
        _isSleeping = false;
        _isLookingRandomly = false;

        SetEmotion(neutralEmotion);

        float sleepTime = Random.Range(timeToSleepMin, timeToSleepMax);
        _sleepCoroutine = StartCoroutine(SleepTimer(sleepTime));
        _idleLookCoroutine = StartCoroutine(IdleLookRoutine());
    }

    private IEnumerator ReturnToNeutralAfterDelay(float delay)
    {
        yield return new WaitForSeconds(delay);
        if (!_isHeld) ReturnToNeutralState();
    }

    private IEnumerator SleepTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (_isBeingPetted || _isHeld || _attentionTarget != null) yield break;
        StopAllBehaviorCoroutines();
        _isLookingRandomly = false;
        _isSleeping = true;
        SetEmotion(sleepingEmotion);
        _wakeUpCoroutine = StartCoroutine(WakeUpTimer(Random.Range(timeToWakeMin, timeToWakeMax)));
    }

    private IEnumerator WakeUpTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (_isHeld) yield break;
        ReturnToNeutralState();
    }

    private IEnumerator ShowShakenEmotion()
    {
        StopAllBehaviorCoroutines();
        _isLookingRandomly = false;
        SetEmotion(shakenEmotion);
        yield return new WaitForSeconds(shakenEmotionDuration);
        if (_isHeld) SetEmotion(pickedUpEmotion);
    }

    private void LookAtCursor()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 planePoint = new Vector3(0, lookPlaneHeight, 0);
        Plane plane = new Plane(Vector3.up, planePoint);

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
            if (_isHeld || _isSleeping || _isBeingPetted || _attentionTarget != null) continue;

            _isLookingRandomly = true;

            var allPieces = FindObjectsByType<PiecePersonality>(FindObjectsSortMode.None)
                            .Where(p => p != this && p.gameObject.activeInHierarchy).ToList();
            bool hasNeighbors = allPieces.Any();

            IdleGazeState nextAction = hasNeighbors
                ? (IdleGazeState)Random.Range(0, 4)
                : (IdleGazeState)Random.Range(0, 3) == 0 ? IdleGazeState.LookAtPlayer : IdleGazeState.LookAtRandom;

            if (facialController != null)
            {
                switch (nextAction)
                {
                    case IdleGazeState.LookAtRandom:
                        Vector3 lookCenter = transform.TransformPoint(lookAreaOffset);
                        lookCenter.y = lookPlaneHeight;
                        Vector3 randomDirection = Random.insideUnitSphere * idleLookRadius;
                        randomDirection.y = 0;
                        facialController.LookAt(lookCenter + randomDirection);
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

    private void OnDrawGizmosSelected()
    {
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Vector3 worldLookCenter = transform.TransformPoint(lookAreaOffset);
        Vector3 planeCenter = new Vector3(worldLookCenter.x, lookPlaneHeight, worldLookCenter.z);
        Gizmos.DrawCube(planeCenter, new Vector3(idleLookRadius * 2, 0.01f, idleLookRadius * 2));
        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawWireCube(planeCenter, new Vector3(idleLookRadius * 2, 0.01f, idleLookRadius * 2));
        Gizmos.color = Color.blue;
        Gizmos.DrawLine(transform.position, planeCenter);

        if (Application.isPlaying && _debugHasTarget)
        {
            Gizmos.color = Color.yellow;
            Vector3 eyeApproxPos = transform.position + Vector3.up * lookPlaneHeight;
            Gizmos.DrawLine(eyeApproxPos, _debugLookTarget);
            Gizmos.DrawWireSphere(_debugLookTarget, 0.2f);
        }
    }
}