using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

[RequireComponent(typeof(PuzzlePiece))]
public class PiecePersonality : MonoBehaviour
{
    private TemperamentSO _temperament;

    [Header("Р’С–Р·СѓР°Р»СЊРЅС– РґР°РЅС–")]
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

    [Header("Р§Р°СЃРѕРІС– РїР°СЂР°РјРµС‚СЂРё")]
    [SerializeField] private float timeToSleepMin = 15f;
    [SerializeField] private float timeToSleepMax = 30f;
    [SerializeField] private float timeToWakeMin = 10f;
    [SerializeField] private float timeToWakeMax = 20f;
    [SerializeField] private float shakenEmotionDuration = 1.0f;
    [SerializeField] private float gentlePettingSpeedThreshold = 200f;
    [SerializeField] private float tickleSpeedThreshold = 800f;

    [Header("РЎРѕРЅ (Р•С„РµРєС‚Рё)")]
    [SerializeField] private int sleepBlinkCount = 4;
    [SerializeField] private float sleepBlinkClosedDuration = 0.8f;
    [SerializeField] private float sleepBlinkOpenDuration = 0.4f;

    [Header("РџРѕРіР»СЏРґ С– СѓРІР°РіР° (Idle Gaze)")]
    [SerializeField] private float lookPlaneHeight = 0.5f;
    [SerializeField] private Vector3 lookAreaOffset = Vector3.zero;
    [SerializeField] private float idleLookIntervalMin = 1.5f;
    [SerializeField] private float idleLookIntervalMax = 4f;
    [SerializeField] private float idleLookDurationMin = 1f;
    [SerializeField] private float idleLookDurationMax = 2.5f;
    [SerializeField] private float idleLookRadius = 3f;
    [SerializeField] private float flyOverReactionRadius = 2.5f;
    [Range(0f, 1f)][SerializeField] private float lookAtCameraChance = 0.5f;

    [Header("РџРѕСЃРёР»Р°РЅРЅСЏ РЅР° РєРѕРјРїРѕРЅРµРЅС‚Рё")]
    [SerializeField] private FacialExpressionController facialController;
    [SerializeField] private ProceduralCatAnimator catAnimator; // NEW

    private float _currentFatigue, _currentIrritation, _currentTrust;
    private bool _isHeld, _isSleeping, _isBeingPetted;
    private Coroutine _sleepCoroutine, _shakenCoroutine, _reactionCoroutine, _idleLookCoroutine, _wakeUpCoroutine, _fallingAsleepCoroutine;
    private PuzzlePiece _puzzlePiece;
    private EmotionProfileSO _lastPettingEmotion;
    private bool _isLookingRandomly = false;
    private bool _wantsToLookAtCamera = false;
    private PuzzlePiece _attentionTarget;

    // --- DEBUG LOGIC VARIABLES ---
    private bool _isDebugStatsActive = false;
    private Coroutine _debugStatsCoroutine;
    
    // NEW: Track active emotion for debug
    private EmotionProfileSO _currentActiveEmotion; 

    private enum IdleGazeState { LookAtRandom, LookAtNeighbor, LookAtPlayer, Wait, LookAtToy }

    public float GetFlyOverRadius() => flyOverReactionRadius;

    private void Awake()
    {
        _puzzlePiece = GetComponent<PuzzlePiece>();
        if (facialController == null)
            facialController = GetComponentInChildren<FacialExpressionController>();

        // NEW: Auto-detect animator
        if (catAnimator == null) catAnimator = GetComponent<ProceduralCatAnimator>();
        
        // NEW: Auto-detect animator
        // Handled in replace_file_content hopefully, but let's be safe
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

    public bool TryReceiveItem(PuzzlePiece item)
    {
        if (_isSleeping) return false;
        if (_puzzlePiece.HasItem) return false;

        _puzzlePiece.AttachItem(item);
        SetEmotion(excitedEmotion != null ? excitedEmotion : curiousEmotion);
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

        if (_isHeld)
        {
            if (_wantsToLookAtCamera)
            {
                facialController.LookAtCamera();
            }
            else
            {
                facialController.ResetPupilPosition();
            }
            return;
        }

        if (!_isBeingPetted)
        {
            if (_puzzlePiece.HasItem)
            {
                facialController.LookAt(_puzzlePiece.GetAttachmentPoint().position, false);
            }
            else if (_attentionTarget != null)
            {
                facialController.LookAt(_attentionTarget.transform.position, false);
            }
            else if (_isLookingRandomly)
            {
                // Logic in coroutine
            }
            else
            {
                LookAtCursor();
            }
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
            _isSleeping = false;
        if (catAnimator != null) catAnimator.SetSleeping(false);
            _isLookingRandomly = false;
            SetEmotion(neutralEmotion);
        }
        else if (piece != _puzzlePiece && piece.PieceTypeSO.usageType == PlacedObjectTypeSO.UsageType.AttractAttention)
        {
            if (!_isSleeping && !_isHeld)
            {
                _attentionTarget = piece;
                SetEmotion(excitedEmotion != null ? excitedEmotion : curiousEmotion);
                StopAllBehaviorCoroutines();
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
            _isBeingPetted = false;
            if (catAnimator != null) catAnimator.SetPetting(false);
            _lastPettingEmotion = null;
            ReturnToNeutralState();
        }
        else if (piece == _attentionTarget)
        {
            _attentionTarget = null;
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
            _isHeld = false;
            SetEmotion(droppedEmotion);
            if (facialController != null) facialController.UpdateSortingOrder(false);
            StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
        }
        else if (piece == _attentionTarget)
        {
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
        StopAllBehaviorCoroutines();
        _isSleeping = false;
        if (catAnimator != null) catAnimator.SetSleeping(false);
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
        if (_isBeingPetted || _isHeld || _attentionTarget != null || _puzzlePiece.HasItem) yield break;
        
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

            if (_isHeld || _isBeingPetted || _attentionTarget != null) yield break;
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
                            facialController.LookAt(targetPiece.GetFocusPoint().transform.position);
                        }
                        break;

                    case IdleGazeState.LookAtPlayer:
                        facialController.LookAtCamera();
                        break;

                    case IdleGazeState.Wait:
                        break;
                }
            }

            yield return new WaitForSeconds(Random.Range(idleLookDurationMin, idleLookDurationMax));

            _isLookingRandomly = false;
        }
    }

    public Transform GetFocusPoint() => facialController != null ? facialController.GetFacialFocusPoint() : transform;

}

