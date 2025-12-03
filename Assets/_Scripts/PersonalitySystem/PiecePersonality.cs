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
    [SerializeField] private float timeToSleepMin = 15f;
    [SerializeField] private float timeToSleepMax = 30f;
    [SerializeField] private float timeToWakeMin = 10f;
    [SerializeField] private float timeToWakeMax = 20f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("Налаштування погляду")]
    [Tooltip("Висота площини очей над землею. ВАЖЛИВО: Налаштуйте це значення, щоб воно співпадало з реальною висотою очей кота (Блакитний квадрат у Gizmos), інакше він буде дивитись вгору.")]
    [SerializeField] private float lookPlaneHeight = 0.5f;

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

    // --- DEBUG ЗМІННІ ---
    private bool _isDebugActive = false;
    private Coroutine _debugStatsCoroutine;
    private Vector3 _debugLookTarget;
    private bool _debugHasTarget;

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

    // --- DEBUG LOGIC ---
    private void Update()
    {
        HandleDebugInput();

        if (!_isHeld && !_isSleeping && !_isBeingPetted && facialController != null)
        {
            if (_isLookingRandomly)
            {
                // Логіка в корутині
            }
            else
            {
                LookAtCursor();
            }
        }
    }

    private void HandleDebugInput()
    {
        // Перевірка на Alt + Left Click
        if (Input.GetMouseButtonDown(0) && (Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt)))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Перевіряємо, чи клікнули ми по цьому об'єкту або його дітям
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    ToggleDebug();
                }
            }
        }
    }

    private void ToggleDebug()
    {
        _isDebugActive = !_isDebugActive;
        Debug.Log($"<color=orange>[{name}] DEBUG: {(_isDebugActive ? "ON" : "OFF")}</color>");

        if (_isDebugActive)
        {
            if (_debugStatsCoroutine == null) _debugStatsCoroutine = StartCoroutine(DebugStatsRoutine());
        }
        else
        {
            if (_debugStatsCoroutine != null) StopCoroutine(_debugStatsCoroutine);
            _debugStatsCoroutine = null;
        }
    }

    private void LogDebug(string message)
    {
        if (_isDebugActive)
        {
            Debug.Log($"<color=cyan>[{name}]</color> {message}");
        }
    }

    private IEnumerator DebugStatsRoutine()
    {
        while (_isDebugActive)
        {
            string state = _isSleeping ? "Sleeping" : (_isHeld ? "Held" : (_isBeingPetted ? "Being Petted" : "Idle"));
            LogDebug($"Stats -> Fatigue: {_currentFatigue:F2}, Irritation: {_currentIrritation:F2}, Trust: {_currentTrust:F2} | State: {state}");
            yield return new WaitForSeconds(1.0f);
        }
    }
    // -------------------

    private void HandlePettingStart(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece || _isHeld) return;

        LogDebug("Petting Started");
        StopAllBehaviorCoroutines();
        _isBeingPetted = true;
        _isSleeping = false;
        _isLookingRandomly = false;

        SetEmotion(neutralEmotion);
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
            LogDebug($"Petting Emotion Switch: {targetEmotion.name}");
            SetEmotion(targetEmotion);
            _lastPettingEmotion = targetEmotion;
        }
    }

    private void HandlePettingEnd(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece || !_isBeingPetted) return;

        LogDebug("Petting Ended");
        _isBeingPetted = false;
        _lastPettingEmotion = null;
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
            // Логуємо зміну емоції, тільки якщо це нова емоція (опціонально можна перевіряти попередню)
            // Але для дебагу корисно бачити всі виклики
            // LogDebug($"Set Emotion: {emotion.emotionName}"); 
            facialController.ApplyEmotion(emotion);
        }
    }

    private void HandlePiecePickedUp(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

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

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        LogDebug("Dropped");
        _isHeld = false;
        SetEmotion(droppedEmotion);
        if (facialController != null) facialController.UpdateSortingOrder(false);

        StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
    }

    private void HandlePieceShaken(PuzzlePiece piece, float velocity)
    {
        if (piece != _puzzlePiece || _isSleeping) return;
        if (_temperament == null) return;

        float irritationGain = 0.05f * _temperament.irritationModifier;
        _currentIrritation = Mathf.Clamp01(_currentIrritation + irritationGain);
        LogDebug($"Shaken! Velocity: {velocity:F1}, Irritation +{irritationGain:F3}");

        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        _shakenCoroutine = StartCoroutine(ShowShakenEmotion());
    }

    private void HandlePieceFlyOver(PuzzlePiece stationaryPiece)
    {
        if (stationaryPiece != _puzzlePiece || _isHeld) return;

        bool wasSleeping = _isSleeping;
        if (_isSleeping)
        {
            LogDebug("Woke up by FlyOver!");
            StopAllBehaviorCoroutines();
            _isSleeping = false;
        }

        if (wasSleeping)
        {
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
        else if (!_isLookingRandomly)
        {
            LogDebug("Curious about FlyOver");
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
                    LogDebug($"Neighbor Synergy Triggered with {neighbor.name}!");
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
        LogDebug("Returning to Neutral State");
        StopAllBehaviorCoroutines();
        _isSleeping = false;
        _isLookingRandomly = false;

        SetEmotion(neutralEmotion);

        float sleepTime = Random.Range(timeToSleepMin, timeToSleepMax);
        LogDebug($"Will sleep in {sleepTime:F1}s");
        _sleepCoroutine = StartCoroutine(SleepTimer(sleepTime));

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

    private IEnumerator SleepTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (_isBeingPetted || _isHeld) yield break;

        LogDebug("Fell Asleep Zzz...");
        StopAllBehaviorCoroutines();
        _isLookingRandomly = false;
        _isSleeping = true;
        SetEmotion(sleepingEmotion);

        float wakeTime = Random.Range(timeToWakeMin, timeToWakeMax);
        LogDebug($"Will wake up in {wakeTime:F1}s");
        _wakeUpCoroutine = StartCoroutine(WakeUpTimer(wakeTime));
    }

    private IEnumerator WakeUpTimer(float time)
    {
        yield return new WaitForSeconds(time);
        if (_isHeld) yield break;

        LogDebug("Woke Up Naturally");
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
        Vector3 planePoint = new Vector3(0, lookPlaneHeight, 0); // Фіксована висота площини
        Plane plane = new Plane(Vector3.up, planePoint);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);

            _debugLookTarget = targetPoint;
            _debugHasTarget = true;

            if (facialController != null) facialController.LookAt(targetPoint);
        }
        else
        {
            _debugHasTarget = false;
        }
    }

    private IEnumerator IdleLookRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleLookIntervalMin, idleLookIntervalMax));
            if (_isHeld || _isSleeping || _isBeingPetted) continue;

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
                        Vector3 randomDirection = Random.insideUnitSphere * idleLookRadius;
                        randomDirection.y = 0;
                        Vector3 lookTarget = transform.position + randomDirection;
                        _debugLookTarget = lookTarget; _debugHasTarget = true;
                        facialController.LookAt(lookTarget);
                        break;

                    case IdleGazeState.LookAtNeighbor:
                        if (hasNeighbors)
                        {
                            PiecePersonality targetPiece = allPieces[Random.Range(0, allPieces.Count)];
                            _debugLookTarget = targetPiece.transform.position; _debugHasTarget = true;
                            facialController.LookAt(targetPiece.transform.position);
                        }
                        break;

                    case IdleGazeState.LookAtPlayer:
                        facialController.ResetPupilPosition();
                        _debugHasTarget = false;
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
        // 1. Площина погляду
        Gizmos.color = new Color(0, 1, 1, 0.2f);
        Vector3 planeCenter = new Vector3(transform.position.x, lookPlaneHeight, transform.position.z);
        Gizmos.DrawCube(planeCenter, new Vector3(5, 0.01f, 5));

        Gizmos.color = new Color(0, 1, 1, 0.5f);
        Gizmos.DrawWireCube(planeCenter, new Vector3(5, 0.01f, 5));

        // 2. Лінія погляду
        if (Application.isPlaying && _debugHasTarget)
        {
            Gizmos.color = Color.yellow;
            Vector3 eyeApproxPos = transform.position + Vector3.up * lookPlaneHeight;
            Gizmos.DrawLine(eyeApproxPos, _debugLookTarget);
            Gizmos.DrawWireSphere(_debugLookTarget, 0.2f);
        }
    }
}