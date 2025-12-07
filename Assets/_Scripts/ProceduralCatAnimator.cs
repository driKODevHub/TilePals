using UnityEngine;
using EZhex1991.EZSoftBone;

public class ProceduralCatAnimator : MonoBehaviour
{
    [Header("Головні Посилання (Main References)")]
    [Tooltip("Головна кістка тіла (Spine/Pelvis), яку будемо хитати.")]
    [SerializeField] private Transform bodyRoot;
    [Tooltip("SoftBone компонент цього кота (для фізики хвоста).")]
    [SerializeField] private EZSoftBone softBoneController;

    [Header("Стан (Debug State)")]
    [Tooltip("Чи гладять зараз кота?")]
    [SerializeField] private bool _isShowPettingState;

    // --- IDLE (СПОКІЙ) ---
    [Header("Анімація Тіла (Idle Body Motion)")]
    [Tooltip("Швидкість дихання/хитання у спокійному стані.")]
    [SerializeField] private float idleSpeed = 0.5f;

    [Space(5)]
    [Tooltip("Вісь РУХУ (Position). Y=1 означає рух вгору-вниз.")]
    [SerializeField] private Vector3 bodyPosAxis = Vector3.up;
    [Tooltip("Сила РУХУ (в метрах). 0 = не рухати.")]
    [SerializeField] private float bodyPosAmount = 0f;

    [Space(5)]
    [Tooltip("Вісь ОБЕРТАННЯ (Rotation). Z=1 означає хитання вліво-вправо.")]
    [SerializeField] private Vector3 bodyRotAxis = new Vector3(0, 0, 1);
    [Tooltip("Сила ОБЕРТАННЯ (в градусах).")]
    [SerializeField] private float bodyRotAmount = 3f;

    [Space(5)]
    [Tooltip("Вісь МАСШТАБУ (Scale). (1,1,1) = рівномірне збільшення.")]
    [SerializeField] private Vector3 bodyScaleAxis = Vector3.one;
    [Tooltip("Сила МАСШТАБУВАННЯ. 0 = вимкнено. 0.02 = 2% зміни розміру при диханні.")]
    [SerializeField] private float bodyScaleAmount = 0f;

    // --- TAIL (ХВІСТ) ---
    [Header("Фізика Хвоста (Tail Physics Wag)")]
    [SerializeField] private bool enableTailWag = true;

    [Tooltip("Напрямок сили виляння. X=1 (вліво-вправо), Y=1 (вгору-вниз).")]
    [SerializeField] private Vector3 wagLocalAxis = Vector3.right;

    [Tooltip("Швидкість махання хвостом.")]
    [SerializeField] private float wagSpeed = 3f;

    [Tooltip("Сила поштовху хвоста. Чим більше, тим сильніше його заносить.")]
    [SerializeField] private float wagStrength = 3f;

    [Tooltip("Ефект 'змійки'. 0 = прямий хвіст. Більше значення = хвіст йде хвилею.")]
    [Range(0f, 10f)]
    [SerializeField] private float tailWaviness = 2f;

    [Tooltip("Куди прикладати силу. 1 = рівномірно. 3 = тільки кінчик.")]
    [Range(0.1f, 5f)]
    [SerializeField] private float forceDistribution = 3f;

    // --- PETTING (ГЛАДЖЕННЯ) ---
    [Header("Реакція на Гладження (Petting Reaction)")]
    [Tooltip("У скільки разів швидше рухається ТІЛО.")]
    [SerializeField] private float petBodySpeedMult = 5f;
    [Tooltip("У скільки разів сильніша амплітуда ТІЛА.")]
    [SerializeField] private float petBodyAmpMult = 1.5f;
    [Space(5)]
    [Tooltip("У скільки разів швидше махає ХВІСТ.")]
    [SerializeField] private float petTailSpeedMult = 10f;
    [Tooltip("У скільки разів сильніше махає ХВІСТ.")]
    [SerializeField] private float petTailAmpMult = 1.2f;
    [Space(5)]
    [Tooltip("Як швидко змінюється стан (200 = майже миттєво).")]
    [SerializeField] private float transitionSpeed = 200f;

    // Внутрішні змінні
    private float _timeOffset;
    private bool _isPetting;
    private bool _isSleeping;

    // Згладжені множники
    private float _curBodySpeedM = 1f;
    private float _curBodyAmpM = 1f;
    private float _curTailSpeedM = 1f;
    private float _curTailAmpM = 1f;

    // Початкові трансформи
    private Vector3 _startLocalPos;
    private Quaternion _startLocalRot;
    private Vector3 _startLocalScale;

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
        PersonalityEventManager.OnPettingStart += OnPettingStart;
        PersonalityEventManager.OnPettingEnd += OnPettingEnd;

        if (softBoneController != null)
        {
            softBoneController.customForce += CalculateWagForce;
        }
    }

    private void OnDisable()
    {
        PersonalityEventManager.OnPettingStart -= OnPettingStart;
        PersonalityEventManager.OnPettingEnd -= OnPettingEnd;

        if (softBoneController != null)
        {
            softBoneController.customForce -= CalculateWagForce;
        }
    }

    private void Update()
    {
        _isShowPettingState = _isPetting;

        if (_isSleeping) return;

        UpdateStateValues();
        AnimateBody();
    }

    private void UpdateStateValues()
    {
        float dt = Time.deltaTime * transitionSpeed;

        float targetBodySpeed = _isPetting ? petBodySpeedMult : 1f;
        float targetBodyAmp = _isPetting ? petBodyAmpMult : 1f;

        float targetTailSpeed = _isPetting ? petTailSpeedMult : 1f;
        float targetTailAmp = _isPetting ? petTailAmpMult : 1f;

        _curBodySpeedM = Mathf.Lerp(_curBodySpeedM, targetBodySpeed, dt);
        _curBodyAmpM = Mathf.Lerp(_curBodyAmpM, targetBodyAmp, dt);

        _curTailSpeedM = Mathf.Lerp(_curTailSpeedM, targetTailSpeed, dt);
        _curTailAmpM = Mathf.Lerp(_curTailAmpM, targetTailAmp, dt);
    }

    private void AnimateBody()
    {
        if (bodyRoot == null) return;

        float time = Time.time + _timeOffset;

        float sineWave = Mathf.Sin(time * idleSpeed * _curBodySpeedM);
        float cosWave = Mathf.Cos(time * idleSpeed * _curBodySpeedM);

        // 1. Позиція
        Vector3 posOffset = bodyPosAxis * (sineWave * bodyPosAmount * _curBodyAmpM);
        bodyRoot.localPosition = _startLocalPos + posOffset;

        // 2. Обертання
        Quaternion rotOffset = Quaternion.AngleAxis(cosWave * bodyRotAmount * _curBodyAmpM, bodyRotAxis);
        bodyRoot.localRotation = _startLocalRot * rotOffset;

        // 3. Масштаб (Scale) - Нове
        // Використовуємо (sineWave + 1) / 2, щоб скейл був від 1.0 до 1.X, а не зменшувався
        // Або простий sineWave, якщо хочемо стиснення і розтягнення
        Vector3 scaleOffset = bodyScaleAxis * (sineWave * bodyScaleAmount * _curBodyAmpM);
        bodyRoot.localScale = _startLocalScale + scaleOffset;
    }

    // --- Фізика Хвоста ---
    private Vector3 CalculateWagForce(float normalizedLength)
    {
        if (!enableTailWag || _isSleeping) return Vector3.zero;

        float time = Time.time + _timeOffset;

        // Хвилястість
        float phaseOffset = normalizedLength * tailWaviness;

        float wave = Mathf.Sin((time * wagSpeed * _curTailSpeedM) - phaseOffset);

        // Напрямок
        Vector3 worldDirection = transform.TransformDirection(wagLocalAxis);

        // Сила
        Vector3 force = worldDirection * (wave * wagStrength * _curTailAmpM);

        // Розподіл сили
        float distributionFactor = Mathf.Pow(normalizedLength, forceDistribution);

        return force * distributionFactor;
    }

    private void OnPettingStart(PuzzlePiece piece)
    {
        if (piece.gameObject == gameObject) _isPetting = true;
    }

    private void OnPettingEnd(PuzzlePiece piece)
    {
        if (piece.gameObject == gameObject) _isPetting = false;
    }

    public void SetSleeping(bool sleeping)
    {
        _isSleeping = sleeping;
    }
}