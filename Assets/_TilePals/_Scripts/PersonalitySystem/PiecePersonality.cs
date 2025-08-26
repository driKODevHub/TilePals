using UnityEngine;
using System.Collections;

/// <summary>
/// "Мозок" фігури. Керує її станом, емоціями та поведінкою на основі темпераменту.
/// </summary>
[RequireComponent(typeof(PuzzlePiece))]
public class PiecePersonality : MonoBehaviour
{
    [Header("Налаштування Особистості")]
    [SerializeField] private TemperamentSO temperament;

    [Header("Профілі Емоцій")]
    [SerializeField] private EmotionProfileSO neutralEmotion;
    [SerializeField] private EmotionProfileSO sleepingEmotion;
    [SerializeField] private EmotionProfileSO pickedUpEmotion;
    [SerializeField] private EmotionProfileSO droppedEmotion;
    [Tooltip("Емоція, коли фігуру різко рухають ('мотиляють').")]
    [SerializeField] private EmotionProfileSO shakenEmotion;

    [Header("Налаштування Поведінки")]
    [Tooltip("Час бездіяльності (в секундах), після якого фігура засинає.")]
    [SerializeField] private float timeToSleep = 10f;
    [Tooltip("Час (в секундах), протягом якого тримається емоція після різкого руху.")]
    [SerializeField] private float shakenEmotionDuration = 1.0f;

    [Header("Посилання на Компоненти")]
    [SerializeField] private FacialExpressionController facialController;

    // Внутрішні параметри стану
    private float _currentFatigue;
    private float _currentIrritation;
    private float _currentTrust;
    private bool _isHeld = false;
    private bool _isSleeping = false;

    private Coroutine _sleepCoroutine;
    private Coroutine _shakenCoroutine;
    private PuzzlePiece _puzzlePiece;

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
        // НОВА ПІДПИСКА
        PersonalityEventManager.OnPieceShaken += HandlePieceShaken;
    }

    private void OnDisable()
    {
        PersonalityEventManager.OnPiecePickedUp -= HandlePiecePickedUp;
        PersonalityEventManager.OnPieceDropped -= HandlePieceDropped;
        PersonalityEventManager.OnPieceShaken -= HandlePieceShaken;
    }

    private void Start()
    {
        InitializePersonality();
    }

    private void Update()
    {
        if (!_isHeld && !_isSleeping && facialController != null)
        {
            LookAtCursor();
        }
    }

    public void InitializePersonality()
    {
        if (temperament == null)
        {
            Debug.LogError("Для фігури не призначено темперамент!", this);
            this.enabled = false;
            return;
        }

        _currentFatigue = temperament.initialFatigue;
        _currentIrritation = temperament.initialIrritation;
        _currentTrust = temperament.initialTrust;

        ReturnToNeutralState();
    }

    public void SetEmotion(EmotionProfileSO emotion)
    {
        if (facialController != null)
        {
            facialController.ApplyEmotion(emotion);
        }
    }

    // --- Обробники Подій ---

    private void HandlePiecePickedUp(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        _isHeld = true;
        _isSleeping = false;
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(pickedUpEmotion);
    }

    private void HandlePieceDropped(PuzzlePiece piece)
    {
        if (piece != _puzzlePiece) return;

        _isHeld = false;
        SetEmotion(droppedEmotion);
        StartCoroutine(ReturnToNeutralAfterDelay(0.5f));
    }

    // НОВИЙ МЕТОД
    private void HandlePieceShaken(PuzzlePiece piece, float velocity)
    {
        if (piece != _puzzlePiece || _isSleeping) return;

        // Збільшуємо роздратування
        float irritationGain = 0.05f * temperament.irritationModifier;
        _currentIrritation = Mathf.Clamp01(_currentIrritation + irritationGain);
        Debug.Log($"{temperament.name} роздратований від тряски! Нове роздратування: {_currentIrritation:F2}");

        // Показуємо емоцію "укачало/роздратування"
        if (_shakenCoroutine != null) StopCoroutine(_shakenCoroutine);
        _shakenCoroutine = StartCoroutine(ShowShakenEmotion());
    }

    // --- Логіка Станів ---

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
        _isSleeping = true;
        SetEmotion(sleepingEmotion);
    }

    private IEnumerator ShowShakenEmotion()
    {
        // Зупиняємо таймер сну, поки фігура роздратована
        if (_sleepCoroutine != null) StopCoroutine(_sleepCoroutine);

        SetEmotion(shakenEmotion);
        yield return new WaitForSeconds(shakenEmotionDuration);

        // Повертаємось до емоції "в руках", якщо її все ще тримають
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
