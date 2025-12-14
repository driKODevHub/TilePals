using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random;

/// <summary>
/// Відповідає виключно за ФІЗИЧНЕ переміщення та обертання об'єкта.
/// ОПТИМІЗОВАНО: Скрипт вимикається (enabled = false), коли об'єкт не рухається.
/// </summary>
public class PieceMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float followSpeed = 25f;
    [Tooltip("Дистанція, при якій об'єкт 'прилипає' до цілі і скрипт вимикається.")]
    [SerializeField] private float snapDistance = 0.01f;

    [Header("Rotation Settings")]
    [SerializeField] private float rotationSpeed = 400f;

    [Header("Inertia / Drift Settings")]
    [Tooltip("Чи використовувати ефект заносу (пролітання повз ціль) при повороті.")]
    [SerializeField] private bool useInertia = true;

    [Header("Randomization")]
    [Tooltip("Мінімальний кут 'пролітання' повз ціль.")]
    [SerializeField] private float minOvershootAngle = 10f;
    [Tooltip("Максимальний кут 'пролітання' повз ціль.")]
    [SerializeField] private float maxOvershootAngle = 20f;

    [Space(5)]
    [Tooltip("Мінімальний множник швидкості повернення (менше 1 = повільніше).")]
    [SerializeField] private float minReturnSpeedMultiplier = 0.5f;
    [Tooltip("Максимальний множник швидкості повернення.")]
    [SerializeField] private float maxReturnSpeedMultiplier = 0.8f;

    public bool IsRotating { get; private set; } = false;
    public float CurrentVelocity { get; private set; }

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _isFollowingTarget = false;
    private Vector3 _lastPosition;

    private void Awake()
    {
        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
        _lastPosition = transform.position;

        // Оптимізація: на старті вимикаємо скрипт, бо об'єкт стоїть
        enabled = false;
    }

    private void OnEnable()
    {
        // Скидаємо позицію для розрахунку швидкості, щоб не було ривка при ввімкненні
        _lastPosition = transform.position;
        CurrentVelocity = 0f;
    }

    private void Update()
    {
        // 1. Логіка руху
        if (_isFollowingTarget && !IsRotating)
        {
            float dist = Vector3.Distance(transform.position, _targetPosition);

            if (dist > snapDistance)
            {
                // Рухаємось плавно
                transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * followSpeed);
            }
            else
            {
                // Прибули: ставимо точно в ціль і "засинаємо"
                transform.position = _targetPosition;
                _isFollowingTarget = false;

                // Якщо ми не крутимось, можна вимикати скрипт для економії ресурсів
                enabled = false;
            }
        }

        // 2. Розрахунок швидкості (тільки коли скрипт увімкнено)
        float distMoved = (transform.position - _lastPosition).magnitude;
        if (distMoved > 0)
        {
            CurrentVelocity = distMoved / Time.deltaTime;
        }
        else
        {
            CurrentVelocity = 0f;
        }

        _lastPosition = transform.position;
    }

    public void SetTargetPosition(Vector3 newPosition)
    {
        if (Vector3.Distance(_targetPosition, newPosition) > 0.001f)
        {
            _targetPosition = newPosition;
            _isFollowingTarget = true;

            // Вмикаємо скрипт, бо треба рухатись
            enabled = true;
        }
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        _targetPosition = position;
        _targetRotation = rotation;
        _isFollowingTarget = false;

        // Після телепортації ми стоїмо, тому можна вимкнути, 
        // АЛЕ краще залишити один кадр апдейту, щоб скинути Velocity
        _lastPosition = position;
        CurrentVelocity = 0f;
        enabled = false;
    }

    public void RotateTowards(Quaternion targetRot, Action onRotationComplete = null)
    {
        if (IsRotating) return;

        // Вмикаємо скрипт для корутини
        enabled = true;
        StartCoroutine(SmoothRotationCoroutine(targetRot, onRotationComplete));
    }

    public void RotateAroundPivot(Vector3 pivotPoint, Vector3 axis, float angle, Action onComplete)
    {
        if (IsRotating) return;

        // Вмикаємо скрипт для корутини
        enabled = true;
        StartCoroutine(SmoothRotationAroundPoint(pivotPoint, axis, angle, onComplete));
    }

    private IEnumerator SmoothRotationAroundPoint(Vector3 pivot, Vector3 axis, float angleBy, Action onComplete)
    {
        IsRotating = true;

        // Генеруємо унікальні значення для цього повороту
        float currentOvershoot = Random.Range(minOvershootAngle, maxOvershootAngle);
        float currentReturnMult = Random.Range(minReturnSpeedMultiplier, maxReturnSpeedMultiplier);

        float absTotalAngle = Mathf.Abs(angleBy);
        float direction = Mathf.Sign(angleBy);

        float forwardAngle = absTotalAngle;
        float backwardAngle = 0f;

        if (useInertia)
        {
            forwardAngle += currentOvershoot;
            backwardAngle = currentOvershoot;
        }

        Quaternion finalRotation = transform.rotation * Quaternion.Euler(axis * angleBy);

        // --- ФАЗА 1: Основний поворот + Занос ---
        float traveled = 0f;
        while (traveled < forwardAngle)
        {
            float step = Time.deltaTime * rotationSpeed;
            step = Mathf.Min(step, forwardAngle - traveled);

            transform.RotateAround(pivot, axis, step * direction);

            traveled += step;
            yield return null;
        }

        // --- ФАЗА 2: Повернення (Відкат) ---
        if (useInertia && backwardAngle > 0)
        {
            traveled = 0f;
            float returnSpeed = rotationSpeed * currentReturnMult;

            while (traveled < backwardAngle)
            {
                float step = Time.deltaTime * returnSpeed;
                step = Mathf.Min(step, backwardAngle - traveled);

                transform.RotateAround(pivot, axis, step * -direction);

                traveled += step;
                yield return null;
            }
        }

        // --- ФІНАЛІЗАЦІЯ ---
        transform.rotation = finalRotation;
        _targetRotation = finalRotation;

        // Оновлюємо targetPosition, бо при RotateAround ми змістилися
        _targetPosition = transform.position;

        IsRotating = false;

        // Якщо ми більше не рухаємось до цілі - вимикаємось
        if (!_isFollowingTarget) enabled = false;

        onComplete?.Invoke();
    }

    private IEnumerator SmoothRotationCoroutine(Quaternion targetRot, Action onComplete)
    {
        IsRotating = true;
        while (Quaternion.Angle(transform.rotation, targetRot) > 0.1f)
        {
            transform.rotation = Quaternion.RotateTowards(transform.rotation, targetRot, rotationSpeed * Time.deltaTime);
            yield return null;
        }
        transform.rotation = targetRot;
        IsRotating = false;

        if (!_isFollowingTarget) enabled = false;

        onComplete?.Invoke();
    }
}