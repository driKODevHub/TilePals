using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(PuzzlePiece))]
public class PiecePersonality : MonoBehaviour
{
    private TemperamentSO _temperament;

    [Header("Візуальні дані")]
    [SerializeField] private EmotionProfileSO neutralEmotion;
    [SerializeField] private EmotionProfileSO sleepingEmotion;
    [SerializeField] private EmotionProfileSO pickedUpEmotion;
    [SerializeField] private EmotionProfileSO droppedEmotion;
    [SerializeField] private EmotionProfileSO shakenEmotion;
    [SerializeField] private EmotionProfileSO pettingGentleEmotion;
    [SerializeField] private EmotionProfileSO pettingTickleEmotion;
    [SerializeField] private EmotionProfileSO pettingAnnoyedEmotion;
    [SerializeField] private EmotionProfileSO curiousEmotion;
    [SerializeField] private EmotionProfileSO excitedEmotion;

    [Header("Часові параметри")]
    [SerializeField] private float timeToSleepMin = 15f;
    [SerializeField] private float timeToSleepMax = 30f;
    [SerializeField] private float timeToWakeMin = 10f;
    [SerializeField] private float timeToWakeMax = 20f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("Сон (Ефекти)")]
    [SerializeField] private int sleepBlinkCount = 4;
    [SerializeField] private float sleepBlinkClosedDuration = 0.8f;
    [SerializeField] private float sleepBlinkOpenDuration = 0.4f;

    [Header("Погляд і увага (Idle Gaze)")]
    [SerializeField] private float lookPlaneHeight = 0.5f;
    [SerializeField] private Vector3 lookAreaOffset = Vector3.zero;
    [SerializeField] private float idleLookIntervalMin = 1.5f;
    [SerializeField] private float idleLookIntervalMax = 4f;
    [SerializeField] private float idleLookDurationMin = 1f;
    [SerializeField] private float idleLookDurationMax = 2.5f;
    [SerializeField] private float idleLookRadius = 3f;
    [SerializeField] private float flyOverReactionRadius = 2.5f;
    [Range(0f, 1f)][SerializeField] private float lookAtCameraChance = 0.5f;

    [Header("Посилання на компоненти")]
    [SerializeField] private FacialExpressionController facialController;
    [SerializeField] private ProceduralCatAnimator catAnimator; // NEW

    private float _currentFatigue, _currentIrritation, _currentTrust;
    private bool _isHeld, _isSleeping, _isBeingPetted;
    private Coroutine _sleepCoroutine, _shakenCoroutine, _reactionCoroutine, _idleLookCoroutine, _wakeUpCoroutine, _fallingAsleepCoroutine;
    private enum IdleGazeState { LookAtRandom, LookAtNeighbor, LookAtPlayer, Wait, LookAtToy }

    public enum GazePriority
    {
        Idle = 0,           // Random looks, cursor
        Neighbor = 1,       // Looking at neighbor
        Reaction = 2,       // Flyover reaction
        Tap = 3,            // Tap/Rustle (Player interaction)
        Petting = 4         // Petting (Highest priority)
    }

    [System.Serializable]
    public class GazeTarget
    {
        public Vector3 position;
        public Transform targetTransform;
        public GazePriority priority;
        public float expirationTime;
        public bool isCamera;

        public GazeTarget(Vector3 pos, GazePriority prio, float duration = -1f)
        {
            position = pos;
            priority = prio;
            expirationTime = duration > 0 ? Time.time + duration : -1f;
            isCamera = false;
        }

        public GazeTarget(Transform target, GazePriority prio, float duration = -1f)
        {
            targetTransform = target;
            priority = prio;
            expirationTime = duration > 0 ? Time.time + duration : -1f;
            isCamera = false;
        }

        public static GazeTarget Camera(GazePriority prio, float duration = -1f)
        {
            var gt = new GazeTarget(Vector3.zero, prio, duration);
            gt.isCamera = true;
            return gt;
        }

        public bool IsExpired => expirationTime > 0 && Time.time > expirationTime;
        public Vector3 GetPosition() => targetTransform != null ? targetTransform.position : position;
    }

    private GazeTarget _currentGazeTarget;
    private PuzzlePiece _puzzlePiece;
    private EmotionProfileSO _lastPettingEmotion;
    private bool _isLookingRandomly = false;
    private bool _wantsToLookAtCamera = false;
    private PuzzlePiece _attentionTarget; // Keep for legacy compatibility if needed, but transition logic to GazeTarget

    // --- DEBUG LOGIC VARIABLES ---
    private bool _isDebugStatsActive = false;
    private Coroutine _debugStatsCoroutine;
    
    // NEW: Track active emotion for debug
    private EmotionProfileSO _currentActiveEmotion; 
    private bool _isIndifferent;

    public float GetFlyOverRadius() => flyOverReactionRadius;

    private void Awake()
    {
        _puzzlePiece = GetComponent<PuzzlePiece>();
        if (facialController == null)
            facialController = GetComponentInChildren<FacialExpressionController>();

        // NEW: Auto-detect animator
        if (catAnimator == null)
            catAnimator = GetComponent<ProceduralCatAnimator>();
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
        PersonalityEventManager.OnFloorTap += HandleFloorTap;
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
        PersonalityEventManager.OnFloorTap -= HandleFloorTap;
    }

    public Vector3 GetGridCenterWorldPosition()
    {
        if (_puzzlePiece == null || _puzzlePiece.PieceTypeSO == null) return transform.position;

        // Grid cell size
        float cellSize = 1f; // Default fallback
        if (GridBuildingSystem.Instance != null && GridBuildingSystem.Instance.GetGrid() != null)
            cellSize = GridBuildingSystem.Instance.GetGrid().GetCellSize();

        // PlacedObjectTypeSO provides better center logic based on rotation
        Vector3 centerOffset = _puzzlePiece.PieceTypeSO.GetBoundsCenterOffset(_puzzlePiece.CurrentDirection) * cellSize;
        Vector2Int rotationOffset = _puzzlePiece.PieceTypeSO.GetRotationOffset(_puzzlePiece.CurrentDirection);

        // Calculate the "true origin" (un-pivoted cell position)
        Vector3 originPos = transform.position - new Vector3(rotationOffset.x, 0, rotationOffset.y) * cellSize;

        return originPos + centerOffset;
    }

    private void SetGazeTarget(GazeTarget target)
    {
        if (_currentGazeTarget == null || _currentGazeTarget.IsExpired || target.priority >= _currentGazeTarget.priority)
        {
            _currentGazeTarget = target;
        }
    }

    public void Setup(TemperamentSO newTemperament)
    {
        _temperament = newTemperament;
        if (_temperament == null) return;

        _currentFatigue = _temperament.initialFatigue;
        _currentIrritation = _temperament.initialIrritation;
        _currentTrust = _temperament.initialTrust;
        _isIndifferent = _temperament.isIndifferent;

        ReturnToNeutralState();
    }

    public bool CanReceiveItem(PuzzlePiece item)
    {
        if (_isSleeping) return false;
        if (_puzzlePiece.HasItem) return false;
        return true;
    }

    public void OnItemReceived(PuzzlePiece item)
    {
        SetEmotion(excitedEmotion != null ? excitedEmotion : curiousEmotion);
    }

    public bool TryReceiveItem(PuzzlePiece item)
    {
        if (!CanReceiveItem(item)) return false;

        _puzzlePiece.AttachItem(item);
        OnItemReceived(item);
        return true;
    }

    private void StopAllBehaviorCoroutines()
    {
        if (_sleepCoroutine != null) { StopCoroutine(_sleepCoroutine); _sleepCoroutine = null; }
        if (_idleLookCoroutine != null) { StopCoroutine(_idleLookCoroutine); _idleLookCoroutine = null; }
        if (_wakeUpCoroutine != null) { StopCoroutine(_wakeUpCoroutine); _wakeUpCoroutine = null; }
        if (_shakenCoroutine != null) { StopCoroutine(_shakenCoroutine); _shakenCoroutine = null; }
        if (_reactionCoroutine != null) { StopCoroutine(_reactionCoroutine); _reactionCoroutine = null; }
        if (_fallingAsleepCoroutine != null) { StopCoroutine(_fallingAsleepCoroutine); _fallingAsleepCoroutine = null; }
    }

    private void Update()
    {
        // --- NEW: Handle Debug Input (Alt + Click) ---
        HandleDebugInput();

        // BLOCK: Do not look around if sleeping or in the process of falling asleep
        if (_isSleeping || _fallingAsleepCoroutine != null) return;

        if (facialController == null) return;

        // Cleanup expired target
        if (_currentGazeTarget != null && _currentGazeTarget.IsExpired)
            _currentGazeTarget = null;

        if (_isHeld)
        {
            if (_wantsToLookAtCamera) facialController.LookAtCamera();
            else facialController.ResetPupilPosition();
            return;
        }

        // Determine best gaze target
        GazeTarget bestTarget = null;

        if (_isBeingPetted)
        {
            // Cursor is best when petting (high priority)
            bestTarget = new GazeTarget(Vector3.zero, GazePriority.Petting);
        }
        else if (_currentGazeTarget != null)
        {
            // Indifferent cats ignore low-priority targets unless they are "briefly awake"
            if (!_isIndifferent || _currentGazeTarget.priority > GazePriority.Neighbor)
            {
                bestTarget = _currentGazeTarget;
            }
        }
        else if (_puzzlePiece.HasItem && !_isIndifferent)
        {
            bestTarget = new GazeTarget(_puzzlePiece.GetAttachmentPoint().position, GazePriority.Neighbor);
        }

        // Apply best target
        if (bestTarget != null)
        {
            if (bestTarget.priority == GazePriority.Petting || (bestTarget.priority == GazePriority.Idle && bestTarget.targetTransform == null && !bestTarget.isCamera))
            {
                LookAtCursor();
            }
            else if (bestTarget.isCamera)
            {
                facialController.LookAtCamera();
            }
            else
            {
                facialController.LookAt(bestTarget.GetPosition(), false);
            }
        }
        else if (!_isLookingRandomly)
        {
            LookAtCursor();
        }
    }

    // --- DEBUG METHODS ---
    private void HandleDebugInput()
    {
        // Check for Alt + Left Click
        if (Input.GetKey(KeyCode.LeftAlt) && Input.GetMouseButtonDown(0))
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            if (Physics.Raycast(ray, out RaycastHit hit))
            {
                // Check if we clicked on this cat or its children
                if (hit.transform == transform || hit.transform.IsChildOf(transform))
                {
                    ToggleDebugStats();
                }
            }
        }
    }

    private void ToggleDebugStats()
    {
        _isDebugStatsActive = !_isDebugStatsActive;

        if (_isDebugStatsActive)
        {
            if (_debugStatsCoroutine != null) StopCoroutine(_debugStatsCoroutine);
            _debugStatsCoroutine = StartCoroutine(DebugStatsRoutine());
            Debug.Log($"<color=green><b>[DEBUG] Stats ENABLED for {name}</b></color>");
        }
        else
        {
            if (_debugStatsCoroutine != null) StopCoroutine(_debugStatsCoroutine);
            Debug.Log($"<color=red><b>[DEBUG] Stats DISABLED for {name}</b></color>");
        }
    }

    private IEnumerator DebugStatsRoutine()
    {
        while (_isDebugStatsActive)
        {
            float irritationPercent = _currentIrritation * 100f;
            float fatiguePercent = _currentFatigue * 100f;
            float trustPercent = _currentTrust * 100f;

            // Determine color based on status
            string statusColor = "white";
            string statusText = "Idle";

            if (_isSleeping) { statusColor = "blue"; statusText = "Sleeping"; }
            else if (_isHeld) { statusColor = "yellow"; statusText = "Held"; }
            else if (_isBeingPetted) { statusColor = "green"; statusText = "Being Petted"; }

            string emotionName = _currentActiveEmotion != null ? _currentActiveEmotion.emotionName : "None";

            string log = $"<color=cyan><b>[{name}]</b></color> " +
                         $"Status: <color={statusColor}><b>{statusText}</b></color> | " +
                         $"Emotion: <b>{emotionName}</b> | " +
                         $"Fatigue: <b>{fatiguePercent:F0}%</b> | " +
                         $"Irritation: <b>{irritationPercent:F0}%</b> | " +
                         $"Trust: <b>{trustPercent:F0}%</b>";

            Debug.Log(log);

            yield return new WaitForSeconds(1.0f);
        }
    }

    // --- Event Handlers ---

    private void HandlePettingStart(PuzzlePiece piece)
    {
        if (piece == _puzzlePiece && !_isHeld)
        {
            StopAllBehaviorCoroutines();
            _isBeingPetted = true;
            if (catAnimator != null) catAnimator.SetPetting(true);
            _isSleeping = false;
            if (catAnimator != null) catAnimator.SetSleeping(false);
            _isLookingRandomly = false;
            SetEmotion(pettingGentleEmotion);
        }
        else if (piece != _puzzlePiece && piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.AttractAttention)
        {
            if (!_isSleeping && !_isHeld)
            {
                SetGazeTarget(new GazeTarget(piece.transform, GazePriority.Reaction, 5.0f));
                SetEmotion(excitedEmotion != null ? excitedEmotion : curiousEmotion);
                StopAllBehaviorCoroutines();
            }
        }
    }

    private void HandlePettingUpdate(PuzzlePiece piece, float mouseSpeed, Vector3 worldDelta, Vector3 hitPoint)
    {
        if (piece != _puzzlePiece || !_isBeingPetted) return;
        if (_temperament == null) return;

        if (catAnimator != null) catAnimator.ApplyPettingImpact(worldDelta, hitPoint);

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
            _isBeingPetted = false;
            if (catAnimator != null) catAnimator.SetPetting(false);
            _lastPettingEmotion = null;
            ReturnToNeutralState();
        }
        else if (_currentGazeTarget != null && piece.transform == _currentGazeTarget.targetTransform)
        {
            _currentGazeTarget = null;
            ReturnToNeutralState();
        }
    }

    private void HandlePiecePickedUp(PuzzlePiece piece)
    {
        if (piece == _puzzlePiece)
        {
            StopAllBehaviorCoroutines();
            _isHeld = true;
            _isSleeping = false;
        if (catAnimator != null) catAnimator.SetSleeping(false);
            _isBeingPetted = false;
            if (catAnimator != null) catAnimator.SetPetting(false);
            _isLookingRandomly = false;
            _wantsToLookAtCamera = Random.value < lookAtCameraChance;
            SetEmotion(pickedUpEmotion);
            if (facialController != null)
            {
                facialController.UpdateSortingOrder(true);
                if (_wantsToLookAtCamera) facialController.LookAtCamera();
                else facialController.ResetPupilPosition();
            }
        }
        else if (piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Toy ||
                 piece.PieceTypeSO.category == PlacedObjectTypeSO.ItemCategory.Food)
        {
            if (!_isSleeping && !_isHeld && Vector3.Distance(transform.position, piece.transform.position) < idleLookRadius * 1.5f)
            {
                SetGazeTarget(new GazeTarget(piece.transform, GazePriority.Neighbor, 4.0f));
                SetEmotion(curiousEmotion);
                StopAllBehaviorCoroutines();
            }
        }
    }

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece == _puzzlePiece)
        {
            _isHeld = false;
            SetEmotion(droppedEmotion);
            if (facialController != null) facialController.UpdateSortingOrder(false);
            StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
        }
        else if (_currentGazeTarget != null && piece.transform == _currentGazeTarget.targetTransform)
        {
            _currentGazeTarget = null;
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
        if (catAnimator != null) catAnimator.SetSleeping(false);
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
        else if (!_isLookingRandomly)
        {
            SetGazeTarget(new GazeTarget(stationaryPiece.transform.position, GazePriority.Reaction, 2.0f));
            StartCoroutine(ShowReactionEmotion(curiousEmotion, 2.0f));
        }
    }

    private void HandleFloorTap(Vector3 position, float radius, float strength)
    {
        if (_isHoldingItem() || _isHeld) return;

        // Temperament and state influence on sensitivity
        float sensitivity = 1.0f;
        if (_temperament != null)
        {
            // Irritated/Excited cats might be more sensitive
            sensitivity += _currentIrritation * 0.5f;
            // Sleepy/Fatigued cats might be less sensitive
            sensitivity -= _currentFatigue * 0.3f;
        }

        Vector3 myCenter = GetGridCenterWorldPosition();
        float dist = Vector3.Distance(myCenter, position);

        if (dist <= radius * sensitivity)
        {
            // Wake up if sleeping
            if (_isSleeping)
            {
                ReturnToNeutralState();
            }

            // Duration based on distance: far = 3s, close = 5s
            float duration = 3f + (1f - dist / (radius * sensitivity)) * 2f;
            duration *= strength; // Factor in the tap strength

            SetGazeTarget(new GazeTarget(position, GazePriority.Tap, duration));

            if (!_isBeingPetted) TriggerExternalReaction(curiousEmotion, 2.0f);
        }
    }

    private bool _isHoldingItem() => _puzzlePiece != null && _puzzlePiece.HasItem;

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
        StopAllBehaviorCoroutines();
        _isSleeping = false;
        if (catAnimator != null) catAnimator.SetSleeping(false);
        _isLookingRandomly = false;

        if (_isIndifferent && _temperament.indifferentEmotion != null)
            SetEmotion(_temperament.indifferentEmotion);
        else
            SetEmotion(neutralEmotion);

        float sleepTime = Random.Range(timeToSleepMin, timeToSleepMax);
        if (_isIndifferent) sleepTime *= 0.3f; // Sleep much faster

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
        if (_isBeingPetted || _isHeld || (_currentGazeTarget != null && !_currentGazeTarget.IsExpired) || _puzzlePiece.HasItem) yield break;
        
        StopAllBehaviorCoroutines();
        _fallingAsleepCoroutine = StartCoroutine(SleepBlinkSequence());
    }

    private IEnumerator SleepBlinkSequence()
    {
        _isLookingRandomly = false; // Stop looking around while falling asleep
        if (facialController != null) facialController.ResetPupilPosition(); // Center eyes at start
        
        for (int i = 0; i < sleepBlinkCount; i++)
        {
            // Close eyes slowly
            if (facialController != null) 
                facialController.TriggerBlink(sleepBlinkClosedDuration * (1f + (float)i/sleepBlinkCount));
            
            yield return new WaitForSeconds(sleepBlinkClosedDuration * (1f + (float)i/sleepBlinkCount));
            
            // Short interval open eyes
            yield return new WaitForSeconds(sleepBlinkOpenDuration);
            
            // Randomly reset gaze while falling asleep (eyes "wander" to center)
            if (facialController != null && Random.value < 0.5f) 
                facialController.ResetPupilPosition();

            if (_isHeld || _isBeingPetted || (_currentGazeTarget != null && !_currentGazeTarget.IsExpired)) yield break;
        }

        // Finally sleep
        _isSleeping = true;
        if (catAnimator != null) catAnimator.SetSleeping(true);
        SetEmotion(sleepingEmotion);
        _wakeUpCoroutine = StartCoroutine(WakeUpTimer(Random.Range(timeToWakeMin, timeToWakeMax)));
        _fallingAsleepCoroutine = null;
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

    public void TriggerExternalReaction(EmotionProfileSO reactionEmotion, float duration)
    {
        if (_reactionCoroutine != null) StopCoroutine(_reactionCoroutine);
        _reactionCoroutine = StartCoroutine(ShowReactionEmotion(reactionEmotion, duration));
    }

    public void SetEmotion(EmotionProfileSO emotion)
    {
        if (facialController != null && emotion != null)
        {
            _currentActiveEmotion = emotion;
            facialController.ApplyEmotion(emotion);
        }
    }

    private void LookAtCursor()
    {
        Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
        Vector3 planePoint = new Vector3(0, lookPlaneHeight, 0);
        Plane plane = new Plane(Vector3.up, planePoint);

        if (plane.Raycast(ray, out float distance))
        {
            Vector3 targetPoint = ray.GetPoint(distance);
            if (facialController != null) facialController.LookAt(targetPoint, false);
        }
    }

    private IEnumerator IdleLookRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(idleLookIntervalMin, idleLookIntervalMax));
            if (_isHeld || _isSleeping || _isBeingPetted) continue;
            
            // Indifferent cats only wake up occasionally for a very short look
            if (_isIndifferent && (_currentGazeTarget == null || _currentGazeTarget.IsExpired))
            {
                if (Random.value > 0.15f) // 15% chance to wake up briefly
                {
                    yield return new WaitForSeconds(Random.Range(2f, 5f));
                    continue;
                }
            }
            else if (_currentGazeTarget != null && !_currentGazeTarget.IsExpired)
            {
                continue;
            }

            _isLookingRandomly = true;

            var allPieces = FindObjectsByType<PiecePersonality>(FindObjectsSortMode.None)
                            .Where(p => p != this && p.gameObject.activeInHierarchy).ToList();
            bool hasNeighbors = allPieces.Any();

            IdleGazeState nextAction = hasNeighbors
                ? (IdleGazeState)Random.Range(0, 4)
                : (IdleGazeState)Random.Range(0, 3) == 0 ? IdleGazeState.LookAtPlayer : IdleGazeState.LookAtRandom;

            if (facialController != null)
            {
                // If indifferent, we forced a wake-up, so let's use a short duration
                float duration = _isIndifferent ? Random.Range(2f, 4f) : idleLookDurationMax;

                switch (nextAction)
                {
                    case IdleGazeState.LookAtRandom:
                        Vector3 lookCenter = transform.TransformPoint(lookAreaOffset);
                        lookCenter.y = lookPlaneHeight;
                        Vector3 randomDirection = Random.insideUnitSphere * idleLookRadius;
                        randomDirection.y = 0;
                        SetGazeTarget(new GazeTarget(lookCenter + randomDirection, GazePriority.Idle, duration));
                        break;

                    case IdleGazeState.LookAtNeighbor:
                        if (hasNeighbors)
                        {
                            PiecePersonality targetPiece = allPieces[Random.Range(0, allPieces.Count)];
                            SetGazeTarget(new GazeTarget(targetPiece.GetFocusPoint(), GazePriority.Neighbor, duration));
                        }
                        break;

                    case IdleGazeState.LookAtPlayer:
                        SetGazeTarget(GazeTarget.Camera(GazePriority.Idle, duration));
                        break;

                    case IdleGazeState.Wait:
                        break;
                }
            }

            yield return new WaitForSeconds(_isIndifferent ? Random.Range(2f, 4f) : Random.Range(idleLookDurationMin, idleLookDurationMax));

            _isLookingRandomly = false;
        }
    }

    public Transform GetFocusPoint() => facialController != null ? facialController.GetFacialFocusPoint() : transform;

}

