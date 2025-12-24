using UnityEngine;
using EZhex1991.EZSoftBone;

public class ProceduralCatAnimator : MonoBehaviour
{
    [Header("Головні посилання (Main References)")]
    [Tooltip("Головна кістка тіла (Spine/Pelvis), яка буде рухатись.")]
    [SerializeField] private Transform bodyRoot;
    [Tooltip("SoftBone контролер для хвоста.")]
    [SerializeField] private EZSoftBone softBoneController;

    [Header("Налаштування Сну")]
    [Tooltip("Коефіцієнт інтенсивності дихання під час сну.")]
    [SerializeField] [Range(0f, 1f)] private float sleepMotionMultiplier = 0.5f;

    [Header("Налаштування Петтингу (Погладжування)")]
    [Tooltip("Сила фізичного зсуву тіла.")]
    [SerializeField] private float pettingImpactStrength = 0.1f; // Reduced from 0.5
    [Tooltip("Максимальний кут нахилу при погладжуванні.")]
    [SerializeField] private float maxPettingTilt = 8f; // Subtle default
    [Tooltip("Сила, яка передається в SoftBones хвоста.")]
    [SerializeField] private float pettingTailForceMultiplier = 1.5f;
    [Tooltip("Швидкість повернення тіла в нейтральний стан.")]
    [SerializeField] private float pettingReturnSpeed = 4f;

    [Header("Параметри Idle руху (Breathing)")]
    [SerializeField] private float idleSpeed = 0.5f;
    [SerializeField] private Vector3 bodyPosAxis = Vector3.up;
    [SerializeField] private float bodyPosAmount = 0.02f;
    [SerializeField] private Vector3 bodyRotAxis = new Vector3(0, 0, 1);
    [SerializeField] private float bodyRotAmount = 2f;
    [SerializeField] private Vector3 bodyScaleAxis = Vector3.one;
    [SerializeField] private float bodyScaleAmount = 0.01f;

    [Header("Хвіст (Tail Physics)")]
    [SerializeField] private bool enableTailWag = true;
    [SerializeField] private Vector3 wagLocalAxis = Vector3.right;
    [SerializeField] private float wagSpeed = 3f;
    [SerializeField] private float wagStrength = 3f;
    [Range(0f, 10f)] [SerializeField] private float tailWaviness = 2f;
    [Range(0.1f, 5f)] [SerializeField] private float forceDistribution = 3f;

    private float _timeOffset;
    private bool _isPetting;
    private bool _isSleeping;

    private Vector3 _startLocalPos;
    private Quaternion _startLocalRot;
    private Vector3 _startLocalScale;

    private Vector3 _pettingPosOffset;
    private Quaternion _pettingRotOffset = Quaternion.identity;
    private Quaternion _targetPettingRot = Quaternion.identity;
    private Vector3 _externalTailForce;
    
    // Smoothing & Timers
    private float _currentMotionScale = 1f;
    private float _internalBreathingTime;
    private float _internalTailTime;

    private void Awake()
    {
        float randomOffset = Random.Range(0f, 100f);
        _internalBreathingTime = randomOffset;
        _internalTailTime = randomOffset;

        // NEW: Auto-detect bodyRoot if missing
        if (bodyRoot == null)
        {
            // 1. Try common names
            bodyRoot = transform.Find("Armature");
            if (bodyRoot == null) bodyRoot = transform.Find("Skeleton");
            
            // 2. Try the root bone of the first SkinnedMeshRenderer
            if (bodyRoot == null)
            {
                var smr = GetComponentInChildren<SkinnedMeshRenderer>();
                if (smr != null) bodyRoot = smr.rootBone;
            }

            // 3. Fallback to first child
            if (bodyRoot == null && transform.childCount > 0)
                bodyRoot = transform.GetChild(0);

            if (bodyRoot != null)
                Debug.Log($"[ProceduralCatAnimator] Auto-set BodyRoot to {bodyRoot.name} on {name}", gameObject);
        }

        if (bodyRoot)
        {
            _startLocalPos = bodyRoot.localPosition;
            _startLocalRot = bodyRoot.localRotation;
            _startLocalScale = bodyRoot.localScale;
        }
    }

    private void OnEnable()
    {
        if (softBoneController != null)
        {
            softBoneController.customForce += GetCombinedTailForce;
        }
    }

    private void OnDisable()
    {
        if (softBoneController != null)
        {
            softBoneController.customForce -= GetCombinedTailForce;
        }
    }

    private void Update()
    {
        UpdateMotionScale();
        
        // Accumulate time incrementally to prevent phase jumps during speed changes
        float dt = Time.deltaTime * _currentMotionScale;
        _internalBreathingTime += dt * idleSpeed;
        _internalTailTime += dt * wagSpeed;

        AnimateBody();
        ResetPettingImpact();
    }

    private void UpdateMotionScale()
    {
        float targetScale = _isSleeping ? sleepMotionMultiplier : 1f;
        _currentMotionScale = Mathf.MoveTowards(_currentMotionScale, targetScale, Time.deltaTime * 2f);
    }

    private void AnimateBody()
    {
        if (bodyRoot == null) return;

        float sineWave = Mathf.Sin(_internalBreathingTime);
        float cosWave = Mathf.Cos(_internalBreathingTime);

        // 1. Position with Scale Compensation
        // Offset is calculated in "Unity World Meters" relative to the script host
        Vector3 worldIdlePos = bodyPosAxis * (sineWave * bodyPosAmount * _currentMotionScale);
        Vector3 totalOffset = worldIdlePos + _pettingPosOffset;

        // CRITICAL: Convert offset to bodyRoot.parent's local space.
        // If parent has scale 100 (Skeleton/Armature), this divides the offset by 100, 
        // preventing the "crazy jump" issue.
        if (bodyRoot.parent != null)
        {
            bodyRoot.localPosition = _startLocalPos + bodyRoot.parent.InverseTransformVector(transform.TransformVector(totalOffset));
        }
        else
        {
            bodyRoot.localPosition = _startLocalPos + totalOffset;
        }

        // 2. Rotation (Angles are scale-independent in local space)
        Quaternion idleRot = Quaternion.AngleAxis(cosWave * bodyRotAmount * _currentMotionScale, bodyRotAxis);
        bodyRoot.localRotation = _startLocalRot * idleRot * _pettingRotOffset;

        // 3. Scale
        Vector3 idleScale = bodyScaleAxis * (sineWave * bodyScaleAmount * _currentMotionScale);
        bodyRoot.localScale = _startLocalScale + idleScale;
    }

    private void ResetPettingImpact()
    {
        _pettingPosOffset = Vector3.Lerp(_pettingPosOffset, Vector3.zero, Time.deltaTime * pettingReturnSpeed);
        
        // Return target to identity
        _targetPettingRot = Quaternion.Slerp(_targetPettingRot, Quaternion.identity, Time.deltaTime * pettingReturnSpeed);
        // Smoothly follow the target
        _pettingRotOffset = Quaternion.Slerp(_pettingRotOffset, _targetPettingRot, Time.deltaTime * pettingReturnSpeed * 1.5f);
        
        _externalTailForce = Vector3.Lerp(_externalTailForce, Vector3.zero, Time.deltaTime * pettingReturnSpeed);
    }

    public void ApplyPettingImpact(Vector3 worldDelta, Vector3 hitPoint)
    {
        if (_isSleeping) return;

        // 1. Body Shift
        Vector3 localDelta = transform.InverseTransformDirection(worldDelta);
        _pettingPosOffset += localDelta * (pettingImpactStrength * 0.5f);
        
        // Clamp translation (max 10cm)
        _pettingPosOffset = Vector3.ClampMagnitude(_pettingPosOffset, 0.1f);
        _pettingPosOffset.y = Mathf.Clamp(_pettingPosOffset.y, -0.03f, 0.03f);

        // 2. Axis-Agnostic Body Tilt
        // We find the local axes of the bone that correspond to the Cat's logic "Forward" and "Right"
        Vector3 boneLocalForward = bodyRoot.InverseTransformDirection(transform.forward);
        Vector3 boneLocalRight = bodyRoot.InverseTransformDirection(transform.right);

        // Calculate pitch (X) and roll (Z) targets based on movement
        float rollTarget = -localDelta.x * maxPettingTilt * 2f;
        float pitchTarget = localDelta.z * maxPettingTilt * 2f;

        // Create target rotation relative to bone's current orientation
        Quaternion rollQ = Quaternion.AngleAxis(rollTarget, boneLocalForward);
        Quaternion pitchQ = Quaternion.AngleAxis(pitchTarget, boneLocalRight);
        
        // Set target (not multiplying to avoid 'spinning out of control')
        _targetPettingRot = rollQ * pitchQ;

        // 3. Tail Force
        _externalTailForce += worldDelta * pettingTailForceMultiplier;
        _externalTailForce = Vector3.ClampMagnitude(_externalTailForce, 8f);
    }

    private Vector3 GetCombinedTailForce(float normalizedLength)
    {
        Vector3 wag = CalculateWagForce(normalizedLength);
        
        // Distribution of petting force (more at the tip)
        float distribution = Mathf.Pow(normalizedLength, forceDistribution);
        Vector3 petting = _externalTailForce * distribution;

        return wag + petting;
    }

    private Vector3 CalculateWagForce(float normalizedLength)
    {
        if (!enableTailWag) return Vector3.zero;

        float phaseOffset = normalizedLength * tailWaviness;
        float wave = Mathf.Sin(_internalTailTime - phaseOffset);

        Vector3 worldDirection = transform.TransformDirection(wagLocalAxis);
        Vector3 force = worldDirection * (wave * wagStrength * _currentMotionScale);
        float distributionFactor = Mathf.Pow(normalizedLength, forceDistribution);

        return force * distributionFactor;
    }

    public void SetSleeping(bool sleeping) => _isSleeping = sleeping;
    public void SetPetting(bool petting) => _isPetting = petting;
}
