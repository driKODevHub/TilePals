using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq; // NEW: Added to fix Any() compilation error
using UnityEngine.Serialization; // NEW: For refactoring data preservation
using EZhex1991.EZSoftBone; // NEW: Physics bones support

public class FacialExpressionController : MonoBehaviour
{
    // --- PREFS ---
    public enum FacingDirection { Z_Forward_Y_Up, Y_Forward_Z_Up }

    [System.Serializable]
    public class EyeRig
    {
        public Transform referencePivot; // Корінь (Pivot), відносно якого все обертається
        
        [FormerlySerializedAs("eyeBone")]
        public Transform pupilBone;      // Сама кістка зіниці (яка рухається поглядом)
        public Transform eyeOuterBone;   // Зовнішня кістка ока (яка скейлиться при морганні)

        public GameObject blinkObject;   // Об'єкт повіки (якщо є)

        [Header("Limits")]
        public bool useEllipticalClamp = true;
        [Range(0f, 2f)] public float limitLeft = 0.5f;
        [Range(0f, 2f)] public float limitRight = 0.5f;
        [Range(0f, 2f)] public float limitUp = 0.5f;
        [Range(0f, 2f)] public float limitDown = 0.5f;

        [Header("Resting State")]
        public Vector2 restPosition = Vector2.zero;

        // Runtime State
        [HideInInspector] public Quaternion currentWorldRotation;
        
        public bool autoScale = true;
        public float scaleMultiplier = 1.0f;
    }

    [System.Serializable]
    public class FaceRig
    {
        public Transform referencePivot;
        public Transform rootBone;
        public Transform facialFocusPoint; // Точка фокусу для інших

        [Header("Movement")]
        public FacingDirection facingDirection = FacingDirection.Z_Forward_Y_Up;
        public float moveSensitivity = 0.5f;
        public float smoothTime = 0.1f;

        [Header("Limits")]
        public bool useEllipticalClamp = true;
        [Range(0f, 2f)] public float limitLeft = 0.5f;
        [Range(0f, 2f)] public float limitRight = 0.5f;
        [Range(0f, 2f)] public float limitUp = 0.5f;
        [Range(0f, 2f)] public float limitDown = 0.5f;

        [Header("Resting State")]
        public Vector2 restPosition = Vector2.zero;

        // Runtime State
        [HideInInspector] public Vector3 currentLocalPosition;
        [HideInInspector] public Vector3 currentVelocity;
        
        [Header("Scaling")]
        public bool autoScale = true;
        public float scaleMultiplier = 1.0f;
    }

    // --- NEW: FEATURE BINDINGS (Enum -> Objects) ---
    [System.Serializable]
    public class FeatureBinding
    {
        public CatFeatureType featureType;
        public List<GameObject> targetObjects;
    }

    [System.Serializable]
    public class BoneState
    {
        public Transform boneTransform;
        public Vector3 targetPosition;
        public Vector3 targetEulerAngles;
        public Vector3 targetScale = Vector3.one;
    }

    [System.Serializable]
    public class BoneBinding
    {
        public CatFeatureType featureType;
        public List<BoneState> boneStates;
    }

    [System.Serializable]
    public class ParticleBinding
    {
        public CatFeatureType featureType;
        public ParticleSystem particlePrefab; // For instantiation
        public ParticleSystem sceneParticle;  // For existing scene object
        public bool instantiate = true;
        public Vector3 spawnOffset;           // Offset relative to this controller/head
        public bool shouldLoop = false;
    }

    // --- CONFIGURATION ---

    [Header("--- EYES CONFIGURATION ---")]
    [Tooltip("Напрямок 'прямо' для очей.")]
    [SerializeField] private FacingDirection eyesFacingDirection = FacingDirection.Z_Forward_Y_Up;
    [SerializeField] private float eyesLookSensitivity = 1.0f;
    [SerializeField] private float eyesRotationSpeed = 30f;
    [Range(0f, 1f)][SerializeField] private float eyesDamping = 0.1f;

    [SerializeField] private EyeRig leftEye;
    [SerializeField] private EyeRig rightEye;

    [Header("--- FACE CONFIGURATION ---")]
    [SerializeField] private FaceRig faceRig;

    [Header("--- BLINKING ---")]
    [SerializeField] private float blinkIntervalMin = 3f;
    [SerializeField] private float blinkIntervalMax = 7f;
    [SerializeField] private float blinkDuration = 0.15f;
    [SerializeField] private bool hideObjectOnBlink = true;
    [Range(0f, 1f)][SerializeField] private float blinkOnLookChance = 0.7f;
    [Range(0f, 1f)][SerializeField] private float resetGazeOnBlinkChance = 0.3f;
    [SerializeField] private float gazeShiftThreshold = 1.0f;

    [Header("--- 3D FEATURES BINDING ---")]
    [Tooltip("Прив'язка типів до конкретних об'єктів на сцені.")]
    public List<FeatureBinding> featureBindings;
    [Header("--- BONES & PHYSICS ---")]
    [SerializeField] private float boneTransitionSpeed = 5f; // Speed of transitioning between bone poses
    public List<BoneBinding> boneBindings; // NEW
    [SerializeField] private List<ParticleBinding> particleBindings; // NEW
    
    [Header("--- SQUASH & STRETCH BLINK ---")]
    [Tooltip("Швидкість 'сплющення' ока при морганні.")]
    [SerializeField] private float blinkSquashSpeed = 25f;
    [Tooltip("Сила розтягування по горизонталі для компенсації об'єму (0 = без розтягування).")]
    [Range(0f, 1f)][SerializeField] private float blinkStretchAmount = 0.2f;

    [Header("--- DEBUG / GIZMOS ---")]
    [Range(0.00001f, 2f)][SerializeField] private float gizmoScaleRest = 0.0005f;
    [SerializeField] private bool showGizmos = true;

    // --- STATE ---
    private Coroutine _blinkingCoroutine;
    private Vector3 _currentWorldLookTarget;
    private Vector3 _lastWorldLookTarget;
    private bool _isLookingAtSomething = false;
    private bool _isTrackingCamera = false; // NEW
    private List<GameObject> _activeInstantiatedParticles = new List<GameObject>(); // NEW: Track particles
    private CatFeatureType _currentEyeFeature = CatFeatureType.None; // NEW: Track active eye for blinking
    private Coroutine _manualBlinkCoroutine;
    private Coroutine _blinkAnimationCoroutine; // NEW
    private float _currentBlinkScale = 1f; // NEW
    private List<BoneBinding> _activeBoneBindings = new List<BoneBinding>(); // NEW: To persist poses across Animator updates
    
    private struct BoneTransformData
    {
        public Vector3 position;
        public Quaternion rotation;
        public Vector3 scale;
    }
    private Dictionary<Transform, BoneTransformData> _defaultBonePoses = new Dictionary<Transform, BoneTransformData>();

    private class BoneTarget
    {
        public Vector3 currentPosition;
        public Quaternion currentRotation;
        public Vector3 currentScale;

        public Vector3 targetPosition;
        public Quaternion targetRotation;
        public Vector3 targetScale;
        
        public bool isDirty = false;
        public bool isInitialized = false;
    }
    private Dictionary<Transform, BoneTarget> _boneTargets = new Dictionary<Transform, BoneTarget>();
    private bool _anyBoneDirty = false;
    private HashSet<CatFeatureType> _activeBoneTypes = new HashSet<CatFeatureType>(); // NEW: Track currently active bone types

    public bool IsLookingAtSomething => _isLookingAtSomething;

    private void Start()
    {
        InitializeEyeRotation(leftEye);
        InitializeEyeRotation(rightEye);
        InitializeFacePosition(faceRig);

        // Capture default rig poses for all custom bones
        if (boneBindings != null)
        {
            foreach (var binding in boneBindings)
            {
                if (binding.boneStates == null) continue;
                foreach (var state in binding.boneStates)
                {
                    if (state.boneTransform != null && !_defaultBonePoses.ContainsKey(state.boneTransform))
                    {
                        _defaultBonePoses[state.boneTransform] = new BoneTransformData
                        {
                            position = state.boneTransform.localPosition,
                            rotation = state.boneTransform.localRotation,
                            scale = state.boneTransform.localScale
                        };
                    }
                }
            }
        }

        if (_blinkingCoroutine != null) StopCoroutine(_blinkingCoroutine);
        _blinkingCoroutine = StartCoroutine(BlinkRoutine());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            // Auto-assign eyeOuterBone if it's the parent of pupilBone (Eye.L is usually parent of Pupil.L)
            AutoAssignEyeBones(leftEye);
            AutoAssignEyeBones(rightEye);

            ForceUpdateEyeInEditor(leftEye);
            ForceUpdateEyeInEditor(rightEye);
            
            // Initialization for calculating scale in Editor
            if (leftEye.autoScale && leftEye.referencePivot != null) leftEye.scaleMultiplier = leftEye.referencePivot.lossyScale.x;
            if (rightEye.autoScale && rightEye.referencePivot != null) rightEye.scaleMultiplier = rightEye.referencePivot.lossyScale.x;
            if (faceRig.autoScale && faceRig.referencePivot != null) faceRig.scaleMultiplier = faceRig.referencePivot.lossyScale.x;
        }
    }
#endif

    private void ForceUpdateEyeInEditor(EyeRig rig)
    {
        if (rig.referencePivot == null || rig.pupilBone == null) return;
        rig.currentWorldRotation = GetRestingRotation(rig);
        ApplyEyePosition(rig);
    }

    private void AutoAssignEyeBones(EyeRig rig)
    {
        if (rig.pupilBone != null && rig.eyeOuterBone == null)
        {
            if (rig.pupilBone.parent != null && (rig.pupilBone.parent.name.Contains("Eye") || rig.pupilBone.parent.name.Contains("Bone")))
            {
                rig.eyeOuterBone = rig.pupilBone.parent;
            }
        }
    }

    private void InitializeEyeRotation(EyeRig rig)
    {
        if (rig.referencePivot != null)
        {
            // NEW: Calculate scale multiplier based on parent scale
            if (rig.autoScale) rig.scaleMultiplier = rig.referencePivot.lossyScale.x;
            rig.currentWorldRotation = GetRestingRotation(rig);
        }
    }

    private void InitializeFacePosition(FaceRig rig)
    {
        if (rig.rootBone != null)
        {
            // NEW: Calculate scale multiplier
            if (rig.autoScale)
            {
                if (rig.referencePivot != null)
                    rig.scaleMultiplier = rig.referencePivot.lossyScale.x;
                else
                    rig.scaleMultiplier = 1.0f;
            }

            rig.currentLocalPosition = Vector3.zero;
        }
    }

    private void LateUpdate()
    {
        // 1. Re-apply bone transforms every frame to override Animator
        ApplyActiveBoneBindings();

        // 2. Update Eyes and Gaze
        UpdateEye(leftEye);
        UpdateEye(rightEye);
        UpdateFace(faceRig);
    }

    // --- PUBLIC API ---

    public void LookAt(Vector3 worldPosition, bool allowBlinkOnShift = true)
    {
        _isTrackingCamera = false; // Disable camera tracking if specific point is given

        // Check if the target shifted significantly to trigger a blink
        if (allowBlinkOnShift && (!_isLookingAtSomething || Vector3.Distance(worldPosition, _lastWorldLookTarget) > gazeShiftThreshold))
        {
            if (Random.value < blinkOnLookChance)
            {
                TriggerBlink();
            }
            _lastWorldLookTarget = worldPosition;
        }

        if (!allowBlinkOnShift) _lastWorldLookTarget = worldPosition; // Keep last target updated even if not blinking

        _currentWorldLookTarget = worldPosition;
        _isLookingAtSomething = true;
    }

    public void LookAtCamera()
    {
        _isLookingAtSomething = true;
        _isTrackingCamera = true;
    }

    public void ResetPupilPosition()
    {
        _isLookingAtSomething = false;
        _isTrackingCamera = false;
    }

    /// <summary>
    /// Застосовує профіль емоції: вимикає всі відомі прив'язки, потім вмикає потрібні.
    /// </summary>
    public void ApplyEmotion(EmotionProfileSO emotionProfileSO)
    {
        if (emotionProfileSO == null) return;

        // 1. Reset Everything
        ClearParticles(); // NEW: Destroy/Stop particles
        _activeBoneBindings.Clear(); // NEW: Reset tracked bone poses
        // REMOVED: _activeBoneTypes.Clear() - we keep state across emotions to prevent redundant transitions

        // Reset all tracked bone targets to their rig-default state first.
        // If the new emotion specifies a custom pose, it will overwrite this in ActivateBoneFeatures.
        foreach (var pair in _boneTargets)
        {
            if (_defaultBonePoses.TryGetValue(pair.Key, out var defaultPose))
            {
                pair.Value.targetPosition = defaultPose.position;
                pair.Value.targetRotation = defaultPose.rotation;
                pair.Value.targetScale = defaultPose.scale;
                pair.Value.isDirty = true;
                _anyBoneDirty = true;
            }
        }
        
        if (featureBindings != null)
        {
            foreach (var binding in featureBindings)
            {
                if (binding.targetObjects != null)
                {
                    foreach (var obj in binding.targetObjects)
                    {
                        if (obj != null) obj.SetActive(false);
                    }
                }
            }
        }

        // 2. Apply New Emotion
        if (emotionProfileSO.activeFeatures != null)
        {
            foreach (var featureType in emotionProfileSO.activeFeatures)
            {
                // Track current eye feature for blink logic
                if (featureType.ToString().StartsWith("Eye_"))
                {
                    _currentEyeFeature = featureType;
                }

                ActivateFeature(featureType);
                ActivateBoneFeatures(featureType);     // NEW
                ActivateParticleFeatures(featureType); // NEW
            }
        }
    }

    private void ActivateFeature(CatFeatureType type)
    {
        if (featureBindings == null || type == CatFeatureType.None) return;

        foreach (var binding in featureBindings)
        {
            if (binding.featureType == type && binding.targetObjects != null)
            {
                foreach (var obj in binding.targetObjects)
                {
                    if (obj != null) obj.SetActive(true);
                }
            }
        }
    }

    private void ActivateBoneFeatures(CatFeatureType type)
    {
        if (boneBindings == null || type == CatFeatureType.None) return;

        string prefix = type.ToString().Split('_')[0];
        bool alreadyActive = _activeBoneTypes.Contains(type);

        // Find bindings for this type
        var activeBindings = boneBindings.FindAll(b => b.featureType == type);
        if (activeBindings.Count == 0) return;

        // Update targets
        foreach (var binding in activeBindings)
        {
            if (binding.boneStates == null) continue;
            foreach (var state in binding.boneStates)
            {
                if (state.boneTransform == null) continue;
                
                if (!_boneTargets.TryGetValue(state.boneTransform, out var target))
                {
                    target = new BoneTarget();
                    _boneTargets[state.boneTransform] = target;
                }
                
                if (!target.isInitialized)
                {
                    target.currentPosition = state.boneTransform.localPosition;
                    target.currentRotation = state.boneTransform.localRotation;
                    target.currentScale = state.boneTransform.localScale;
                    target.isInitialized = true;
                }

                target.targetPosition = state.targetPosition;
                target.targetRotation = Quaternion.Euler(state.targetEulerAngles);
                target.targetScale = state.targetScale;

                if (alreadyActive)
                {
                    // Snap: No transition if same type
                    target.currentPosition = target.targetPosition;
                    target.currentRotation = target.targetRotation;
                    target.currentScale = target.targetScale;
                    target.isDirty = false;
                }
                else
                {
                    // Transition: Mark for Lerp
                    target.isDirty = true;
                    _anyBoneDirty = true;
                }
            }
        }

        // Register new type and clean up old ones with same prefix
        _activeBoneTypes.RemoveWhere(t => t.ToString().StartsWith(prefix));
        _activeBoneTypes.Add(type);

        _activeBoneBindings.AddRange(activeBindings);
    }

    private void ApplyActiveBoneBindings()
    {
        bool stillDirty = false;
        float step = boneTransitionSpeed * Time.deltaTime;
        var softBones = GetComponentsInChildren<EZSoftBone>();

        foreach (var pair in _boneTargets)
        {
            Transform t = pair.Key;
            BoneTarget target = pair.Value;
            if (t == null) continue;

            if (target.isDirty)
            {
                // Interpolate internal state
                target.currentPosition = Vector3.Lerp(target.currentPosition, target.targetPosition, step);
                target.currentRotation = Quaternion.Slerp(target.currentRotation, target.targetRotation, step);
                target.currentScale = Vector3.Lerp(target.currentScale, target.targetScale, step);

                // Check if settled
                float dist = Vector3.Distance(target.currentPosition, target.targetPosition) + 
                             Quaternion.Angle(target.currentRotation, target.targetRotation);
                
                if (dist > 0.01f) stillDirty = true;
                else 
                {
                    target.isDirty = false;
                    target.currentPosition = target.targetPosition;
                    target.currentRotation = target.targetRotation;
                    target.currentScale = target.targetScale;
                }
            }

            // ALWAYS apply the currentInterpolated state to override Animator
            t.localPosition = target.currentPosition;
            t.localRotation = target.currentRotation;
            t.localScale = target.currentScale;
        }

        // If we were dirty and are moving, we must tell EZSoftBone to update its "Rest Pose"
        // But we only do this if we are currently transitioning to avoid constant re-init jitter
        if (_anyBoneDirty)
        {
            foreach (var sb in softBones)
            {
                if (sb.enabled) sb.InitStructures();
            }
        }

        _anyBoneDirty = stillDirty;
    }

    private void ActivateParticleFeatures(CatFeatureType type)
    {
        if (particleBindings == null || type == CatFeatureType.None) return;

        foreach (var binding in particleBindings)
        {
            if (binding.featureType == type)
            {
                // Option A: Instantiate
                if (binding.instantiate && binding.particlePrefab != null)
                {
                    // Calculate Spawn Position (Relative to head/controller)
                    Vector3 spawnPos = transform.TransformPoint(binding.spawnOffset);
                    Quaternion spawnRot = transform.rotation; // Aligned with head

                    var p = Instantiate(binding.particlePrefab, spawnPos, spawnRot, transform);
                    _activeInstantiatedParticles.Add(p.gameObject);
                    p.Play();
                }
                // Option B: Scene Object
                else if (!binding.instantiate && binding.sceneParticle != null)
                {
                    binding.sceneParticle.gameObject.SetActive(true);
                    binding.sceneParticle.Play();
                }
            }
        }
    }

    private void ClearParticles()
    {
        // 1. Destroy Instantiated
        foreach (var p in _activeInstantiatedParticles)
        {
            if (p != null) Destroy(p);
        }
        _activeInstantiatedParticles.Clear();

        // 2. Stop Scene Particles
        if (particleBindings != null)
        {
            foreach (var binding in particleBindings)
            {
                if (!binding.instantiate && binding.sceneParticle != null)
                {
                    // If looping, we might want to stop/disable
                    if (binding.shouldLoop || binding.sceneParticle.gameObject.activeSelf)
                    {
                        binding.sceneParticle.Stop();
                        binding.sceneParticle.gameObject.SetActive(false);
                    }
                }
            }
        }
    }

    public void UpdateSortingOrder(bool isHeld) { }


    // ===================================================================================
    // LOGIC: EYES
    // ===================================================================================

    private Quaternion GetRestingRotation(EyeRig rig)
    {
        if (rig.referencePivot == null) return Quaternion.identity;

        float yaw = rig.restPosition.x * 45f;
        float pitch = -rig.restPosition.y * 45f;

        if (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up)
            return rig.referencePivot.rotation * Quaternion.Euler(pitch, yaw, 0);
        else
            return rig.referencePivot.rotation * Quaternion.Euler(pitch, 0, yaw);
    }

    private void UpdateEye(EyeRig rig)
    {
        if (rig.pupilBone == null || rig.referencePivot == null) return;

        Quaternion targetRotation;

        if (_isLookingAtSomething)
        {
            if (_isTrackingCamera && Camera.main != null)
            {
                // Замість позиції камери (лінзи), дивимось в "центр екрану" на певній відстані.
                // Це створює відчуття погляду в очі гравцю, який дивиться в центр монітора.
                float distance = Vector3.Distance(Camera.main.transform.position, rig.referencePivot.position);
                _currentWorldLookTarget = Camera.main.ViewportToWorldPoint(new Vector3(0.5f, 0.5f, distance));
            }

            Vector3 directionToTarget = _currentWorldLookTarget - rig.referencePivot.position;
            if (directionToTarget.sqrMagnitude < 0.001f) directionToTarget = (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up) ? rig.referencePivot.forward : rig.referencePivot.up;

            if (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up)
            {
                targetRotation = Quaternion.LookRotation(directionToTarget, rig.referencePivot.up);
            }
            else
            {
                targetRotation = Quaternion.LookRotation(directionToTarget, rig.referencePivot.forward) * Quaternion.Euler(90, 0, 0);
            }

            float effectiveDamping = eyesDamping;
            float step = eyesRotationSpeed * Time.deltaTime * (1f - effectiveDamping);
            rig.currentWorldRotation = Quaternion.Slerp(rig.currentWorldRotation, targetRotation, step);
        }
        else
        {
            targetRotation = GetRestingRotation(rig);
            // SNAP: Миттєво прилипаємо до дефолтної позиції, щоб не було розсинхрону при процедурному диханні
            rig.currentWorldRotation = targetRotation;
        }

        ApplyEyePosition(rig);
    }

    private void ApplyEyePosition(EyeRig rig)
    {
        Vector3 stabilizedLookDir = rig.currentWorldRotation * ((eyesFacingDirection == FacingDirection.Z_Forward_Y_Up) ? Vector3.forward : Vector3.up);
        Vector3 localDir = rig.referencePivot.InverseTransformDirection(stabilizedLookDir);

        float x, y;
        if (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up)
        {
            x = localDir.x;
            y = localDir.y;
        }
        else
        {
            x = localDir.x;
            y = localDir.z;
        }

        // Apply Sensitivity
        x *= eyesLookSensitivity;
        y *= eyesLookSensitivity;

        // Apply Logic Limits
        float lLeft = rig.limitLeft * rig.scaleMultiplier;
        float lRight = rig.limitRight * rig.scaleMultiplier;
        float lUp = rig.limitUp * rig.scaleMultiplier;
        float lDown = rig.limitDown * rig.scaleMultiplier;

        if (rig.useEllipticalClamp)
        {
            float normX = x > 0 ? (x / lRight) : (x / lLeft);
            float normY = y > 0 ? (y / lUp) : (y / lDown);
            Vector2 v = new Vector2(normX, normY);
            if (v.sqrMagnitude > 1) v = v.normalized;
            x = v.x > 0 ? v.x * lRight : v.x * lLeft;
            y = v.y > 0 ? v.y * lUp : v.y * lDown;
        }
        else
        {
            x = Mathf.Clamp(x, -lLeft, lRight);
            y = Mathf.Clamp(y, -lDown, lUp);
        }

        Vector3 finalLocalPos;
        if (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up)
            finalLocalPos = new Vector3(x, y, 0);
        else
            finalLocalPos = new Vector3(x, 0, y);

        rig.pupilBone.position = rig.referencePivot.TransformPoint(finalLocalPos);
        
        // --- NEW: Squash & Stretch Scaling ---
        // Скейлимо зовнішню кістку ока
        if (rig.eyeOuterBone != null)
        {
            float squashX = 1f + (1f - _currentBlinkScale) * blinkStretchAmount;
            float squashZ = _currentBlinkScale;
            rig.eyeOuterBone.localScale = new Vector3(squashX, 1f, squashZ);
            
            // --- Pupil Scale Compensation ---
            // Сама зіниця (pupilBone) має лишатись круглою
            float invSquashX = 1f / Mathf.Max(0.01f, squashX);
            float invSquashZ = 1f / Mathf.Max(0.01f, squashZ);
            rig.pupilBone.localScale = new Vector3(invSquashX, 1f, invSquashZ);
        }
        else
        {
            // Fallback: якщо нема зовнішньої кістки, скейлимо саму зіницю (стара логіка)
            float squashX = 1f + (1f - _currentBlinkScale) * blinkStretchAmount;
            float squashZ = _currentBlinkScale;
            rig.pupilBone.localScale = new Vector3(squashX, 1f, squashZ);
        }
        
        // Reverting to referencePivot.rotation because the model uses position for pupil tracking.
        // Rotating the bone leads to pupil flipping/skipping on the surface.
        rig.pupilBone.rotation = rig.referencePivot.rotation;
    }

    // ===================================================================================
    // LOGIC: FACE
    // ===================================================================================

    private void UpdateFace(FaceRig rig)
    {
        if (rig.rootBone == null || rig.referencePivot == null) return;

        Vector3 targetLocalPos = Vector3.zero;

        // 1. Calculate Target Inputs
        if (_isLookingAtSomething)
        {
            Vector3 dirToTarget = _currentWorldLookTarget - rig.referencePivot.position;
            Vector3 localDir = rig.referencePivot.InverseTransformDirection(dirToTarget).normalized;

            float x, y;
            if (rig.facingDirection == FacingDirection.Z_Forward_Y_Up)
            {
                x = localDir.x;
                y = localDir.y;
            }
            else
            {
                x = localDir.x;
                y = localDir.z; // Y is forward but maps to Z
            }

            // Sensitivity
            x *= rig.moveSensitivity;
            y *= rig.moveSensitivity;

            // 2. Apply Limits
            float lLeft = rig.limitLeft * rig.scaleMultiplier;
            float lRight = rig.limitRight * rig.scaleMultiplier;
            float lUp = rig.limitUp * rig.scaleMultiplier;
            float lDown = rig.limitDown * rig.scaleMultiplier;

            if (rig.useEllipticalClamp)
            {
                float normX = x > 0 ? (x / lRight) : (x / lLeft);
                float normY = y > 0 ? (y / lUp) : (y / lDown);
                Vector2 v = new Vector2(normX, normY);

                if (v.sqrMagnitude > 1) v = v.normalized;

                x = v.x > 0 ? v.x * lRight : v.x * lLeft;
                y = v.y > 0 ? v.y * lUp : v.y * lDown;
            }
            else
            {
                x = Mathf.Clamp(x, -lLeft, lRight);
                y = Mathf.Clamp(y, -lDown, lUp);
            }

            // 3. Construct Target Vector
            if (rig.facingDirection == FacingDirection.Z_Forward_Y_Up)
                targetLocalPos = new Vector3(x, y, 0);
            else
                targetLocalPos = new Vector3(x, 0, y);
        }
        else
        {
            // REST POSITION
            float rX = rig.restPosition.x * rig.scaleMultiplier;
            float rY = rig.restPosition.y * rig.scaleMultiplier;

            if (rig.facingDirection == FacingDirection.Z_Forward_Y_Up)
                targetLocalPos = new Vector3(rX, rY, 0);
            else
                targetLocalPos = new Vector3(rX, 0, rY);
        }

        // 4. Smooth Damp
        rig.currentLocalPosition = Vector3.SmoothDamp(
            rig.currentLocalPosition,
            targetLocalPos,
            ref rig.currentVelocity,
            rig.smoothTime
        );

        // 5. Apply
        rig.rootBone.position = rig.referencePivot.TransformPoint(rig.currentLocalPosition);
    }

    public Transform GetFacialFocusPoint() => faceRig.facialFocusPoint;

    public void TriggerBlink(float customDuration = -1f)
    {
        if (_blinkAnimationCoroutine != null) StopCoroutine(_blinkAnimationCoroutine);
        _blinkAnimationCoroutine = StartCoroutine(PerformBlinkAnimation(customDuration > 0 ? customDuration : blinkDuration));
    }

    private IEnumerator PerformBlinkAnimation(float duration)
    {
        // 1. Closing Squash
        while (_currentBlinkScale > 0.05f)
        {
            _currentBlinkScale = Mathf.MoveTowards(_currentBlinkScale, 0f, Time.deltaTime * blinkSquashSpeed);
            yield return null;
        }
        _currentBlinkScale = 0f;

        // 2. Swap Visuals
        SetBlinkState(true);
        _currentBlinkScale = 1f; // Reset scale for "closed" visual objects

        // 3. Wait in closed state
        yield return new WaitForSeconds(duration);

        // 4. Opening Stretch
        _currentBlinkScale = 0f; // Start from zero for "open" visual
        SetBlinkState(false);

        while (_currentBlinkScale < 0.95f)
        {
            _currentBlinkScale = Mathf.MoveTowards(_currentBlinkScale, 1f, Time.deltaTime * blinkSquashSpeed);
            yield return null;
        }
        _currentBlinkScale = 1f;
        _blinkAnimationCoroutine = null;
    }
    

    // ===================================================================================
    // LOGIC: BLINKING
    // ===================================================================================

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkIntervalMin, blinkIntervalMax));

            // NEW: Skip blinking if eyes are already closed or "happy" (^^ shape)
            if (_currentEyeFeature == CatFeatureType.Eye_Closed || 
                _currentEyeFeature == CatFeatureType.Eye_Sleep || 
                _currentEyeFeature == CatFeatureType.Eye_Happy)
            {
                continue;
            }

            TriggerBlink();
            // Wait for blink to roughly finish to not overlay timers
            yield return new WaitForSeconds(blinkDuration + 0.2f); 

            // NEW: Randomly reset gaze to forward/neutral after a blink
            if (_isLookingAtSomething && Random.value < resetGazeOnBlinkChance)
            {
                ResetPupilPosition();
            }
        }
    }

    private void SetBlinkState(bool isBlinking)
    {
        // 1. Legacy support (if needed)
        bool shouldBeActive = hideObjectOnBlink ? !isBlinking : isBlinking;
        if (leftEye.blinkObject) leftEye.blinkObject.SetActive(shouldBeActive);
        if (rightEye.blinkObject) rightEye.blinkObject.SetActive(shouldBeActive);

        // 2. New 3D Swapping Logic
        if (_currentEyeFeature != CatFeatureType.None && _currentEyeFeature != CatFeatureType.Eye_Closed)
        {
            if (isBlinking)
            {
                // Toggle OFF current eyes, Toggle ON closed eyes
                SetFeatureObjectsActive(_currentEyeFeature, false);
                SetFeatureObjectsActive(CatFeatureType.Eye_Closed, true);
            }
            else
            {
                // Toggle OFF closed eyes, Toggle ON current eyes
                SetFeatureObjectsActive(CatFeatureType.Eye_Closed, false);
                SetFeatureObjectsActive(_currentEyeFeature, true);
            }
        }
    }

    private void SetFeatureObjectsActive(CatFeatureType type, bool active)
    {
        if (featureBindings == null || type == CatFeatureType.None) return;

        foreach (var binding in featureBindings)
        {
            if (binding.featureType == type && binding.targetObjects != null)
            {
                foreach (var obj in binding.targetObjects)
                {
                    if (obj != null) obj.SetActive(active);
                }
            }
        }
    }

    // ===================================================================================
    // GIZMOS
    // ===================================================================================

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;

        DrawEyeGizmos(leftEye);
        DrawEyeGizmos(rightEye);
        DrawFaceGizmos(faceRig);

        if (_isLookingAtSomething)
        {
            Gizmos.color = Color.magenta;
            if (leftEye.referencePivot) Gizmos.DrawLine(leftEye.referencePivot.position, _currentWorldLookTarget);
            if (rightEye.referencePivot) Gizmos.DrawLine(rightEye.referencePivot.position, _currentWorldLookTarget);
            if (faceRig.referencePivot) Gizmos.DrawLine(faceRig.referencePivot.position, _currentWorldLookTarget);
        }

        // NEW: Draw Particle Spawn Points
        if (particleBindings != null)
        {
            Gizmos.color = new Color(1f, 0.5f, 0f, 0.7f); // Orange
            foreach (var pb in particleBindings)
            {
                if (pb.instantiate)
                {
                    Vector3 spawnPos = transform.TransformPoint(pb.spawnOffset);
                    Gizmos.DrawWireSphere(spawnPos, gizmoScaleRest * 200f); // Make it visible
                    Gizmos.DrawSphere(spawnPos, gizmoScaleRest * 50f);
                }
            }
        }
    }

    private void DrawEyeGizmos(EyeRig rig)
    {
        if (rig.referencePivot == null) return;

        Gizmos.matrix = rig.referencePivot.localToWorldMatrix;

        float lLeft = rig.limitLeft * rig.scaleMultiplier;
        float lRight = rig.limitRight * rig.scaleMultiplier;
        float lUp = rig.limitUp * rig.scaleMultiplier;
        float lDown = rig.limitDown * rig.scaleMultiplier;

        // Frame
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        DrawFrame(eyesFacingDirection, lLeft, lRight, lUp, lDown);

        // Forward Line
        Gizmos.color = Color.blue;
        Vector3 fwdDir = (eyesFacingDirection == FacingDirection.Z_Forward_Y_Up) ? Vector3.forward : Vector3.up;
        Gizmos.DrawLine(Vector3.zero, fwdDir * (Mathf.Max(lUp, lRight) * 5f));

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawFaceGizmos(FaceRig rig)
    {
        if (rig.referencePivot == null) return;

        Gizmos.matrix = rig.referencePivot.localToWorldMatrix;

        float lLeft = rig.limitLeft * rig.scaleMultiplier;
        float lRight = rig.limitRight * rig.scaleMultiplier;
        float lUp = rig.limitUp * rig.scaleMultiplier;
        float lDown = rig.limitDown * rig.scaleMultiplier;

        // Frame
        Gizmos.color = new Color(1, 0.92f, 0.016f, 0.5f);
        DrawFrame(rig.facingDirection, lLeft, lRight, lUp, lDown);

        Gizmos.matrix = Matrix4x4.identity;
    }

    private void DrawFrame(FacingDirection dir, float l, float r, float u, float d)
    {
        Vector3 tl, tr, br, bl;

        if (dir == FacingDirection.Z_Forward_Y_Up)
        {
            tl = new Vector3(-l, u, 0); tr = new Vector3(r, u, 0);
            br = new Vector3(r, -d, 0); bl = new Vector3(-l, -d, 0);
        }
        else
        {
            tl = new Vector3(-l, 0, u); tr = new Vector3(r, 0, u);
            br = new Vector3(r, 0, -d); bl = new Vector3(-l, 0, -d);
        }

        Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);
    }
}
