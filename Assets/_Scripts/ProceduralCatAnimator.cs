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
    [Tooltip("Сила фізичного відклику тіла на рух мишки.")]
    [SerializeField] private float pettingImpactStrength = 0.5f;
    [Tooltip("Сила, яка передається в SoftBones хвоста.")]
    [SerializeField] private float pettingTailForceMultiplier = 2.0f;
    [Tooltip("Швидкість повернення тіла в нейтральний стан.")]
    [SerializeField] private float pettingReturnSpeed = 5f;

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
    private Vector3 _externalTailForce;
    
    // Lerping multipliers
    private float _currentMotionScale = 1f;

    private void Awake()
    {
        _timeOffset = Random.Range(0f, 100f);
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

        float time = (Time.time + _timeOffset) * _currentMotionScale;
        float sineWave = Mathf.Sin(time * idleSpeed);
        float cosWave = Mathf.Cos(time * idleSpeed);

        // Idle movement
        Vector3 idlePos = bodyPosAxis * (sineWave * bodyPosAmount * _currentMotionScale);
        Quaternion idleRot = Quaternion.AngleAxis(cosWave * bodyRotAmount * _currentMotionScale, bodyRotAxis);
        Vector3 idleScale = bodyScaleAxis * (sineWave * bodyScaleAmount * _currentMotionScale);

        // Combine with Petting Impact
        bodyRoot.localPosition = _startLocalPos + idlePos + _pettingPosOffset;
        bodyRoot.localRotation = _startLocalRot * idleRot * _pettingRotOffset;
        bodyRoot.localScale = _startLocalScale + idleScale;
    }

    private void ResetPettingImpact()
    {
        _pettingPosOffset = Vector3.Lerp(_pettingPosOffset, Vector3.zero, Time.deltaTime * pettingReturnSpeed);
        _pettingRotOffset = Quaternion.Slerp(_pettingRotOffset, Quaternion.identity, Time.deltaTime * pettingReturnSpeed);
        _externalTailForce = Vector3.Lerp(_externalTailForce, Vector3.zero, Time.deltaTime * pettingReturnSpeed);
    }

    public void ApplyPettingImpact(Vector3 worldDelta, Vector3 hitPoint)
    {
        if (_isSleeping) return;

        // 1. Body Shift
        Vector3 localDelta = transform.InverseTransformDirection(worldDelta);
        _pettingPosOffset += localDelta * pettingImpactStrength;
        _pettingPosOffset = Vector3.ClampMagnitude(_pettingPosOffset, 0.1f);

        // 2. Body Tilt (Rotate away from/with movement)
        float tiltAngle = localDelta.x * 20f; // Tilt based on horizontal movement
        _pettingRotOffset *= Quaternion.Euler(0, 0, -tiltAngle);

        // 3. Tail Force
        _externalTailForce += worldDelta * pettingTailForceMultiplier;
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

        float time = (Time.time + _timeOffset) * _currentMotionScale;
        float phaseOffset = normalizedLength * tailWaviness;
        float wave = Mathf.Sin((time * wagSpeed) - phaseOffset);

        Vector3 worldDirection = transform.TransformDirection(wagLocalAxis);
        Vector3 force = worldDirection * (wave * wagStrength * _currentMotionScale);
        float distributionFactor = Mathf.Pow(normalizedLength, forceDistribution);

        return force * distributionFactor;
    }

    public void SetSleeping(bool sleeping) => _isSleeping = sleeping;
    public void SetPetting(bool petting) => _isPetting = petting;
}
