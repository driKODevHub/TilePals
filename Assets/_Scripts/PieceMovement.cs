using UnityEngine;
using System.Collections;
using System;
using Random = UnityEngine.Random; // Явно вказуємо, щоб не плутати з System.Random

/// <summary>
/// Відповідає виключно за ФІЗИЧНЕ переміщення та обертання об'єкта.
/// Не містить ігрової логіки.
/// </summary>
public class PieceMovement : MonoBehaviour
{
    [Header("Movement Settings")]
    [SerializeField] private float followSpeed = 25f;

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

    private Vector3 _targetPosition;
    private Quaternion _targetRotation;
    private bool _isFollowingTarget = false;

    // Для відстеження швидкості тряски
    private Vector3 _lastPosition;
    public float CurrentVelocity { get; private set; }

    private void Awake()
    {
        _targetPosition = transform.position;
        _targetRotation = transform.rotation;
        _lastPosition = transform.position;
    }

    private void Update()
    {
        if (_isFollowingTarget && !IsRotating)
        {
            transform.position = Vector3.Lerp(transform.position, _targetPosition, Time.deltaTime * followSpeed);
        }

        // Розрахунок швидкості
        CurrentVelocity = (transform.position - _lastPosition).magnitude / Time.deltaTime;
        _lastPosition = transform.position;
    }

    public void SetTargetPosition(Vector3 newPosition)
    {
        _targetPosition = newPosition;
        _isFollowingTarget = true;
    }

    public void TeleportTo(Vector3 position, Quaternion rotation)
    {
        transform.position = position;
        transform.rotation = rotation;
        _targetPosition = position;
        _targetRotation = rotation;
        _isFollowingTarget = false;
    }

    public void RotateTowards(Quaternion targetRot, Action onRotationComplete = null)
    {
        if (IsRotating) return;
        StartCoroutine(SmoothRotationCoroutine(targetRot, onRotationComplete));
    }

    public void RotateAroundPivot(Vector3 pivotPoint, Vector3 axis, float angle, Action onComplete)
    {
        if (IsRotating) return;
        StartCoroutine(SmoothRotationAroundPoint(pivotPoint, axis, angle, onComplete));
    }

    private IEnumerator SmoothRotationAroundPoint(Vector3 pivot, Vector3 axis, float angleBy, Action onComplete)
    {
        IsRotating = true;

        // --- РАНДОМІЗАЦІЯ ПАРАМЕТРІВ ЗАНОСУ ---
        // Генеруємо унікальні значення для цього конкретного повороту
        float currentOvershoot = Random.Range(minOvershootAngle, maxOvershootAngle);
        float currentReturnMult = Random.Range(minReturnSpeedMultiplier, maxReturnSpeedMultiplier);

        // --- ПІДГОТОВКА ДАНИХ ---
        float absTotalAngle = Mathf.Abs(angleBy);
        float direction = Mathf.Sign(angleBy);

        float forwardAngle = absTotalAngle;
        float backwardAngle = 0f;

        if (useInertia)
        {
            forwardAngle += currentOvershoot; // Фаза 1: Крутимось далі цілі (випадковий кут)
            backwardAngle = currentOvershoot; // Фаза 2: Повертаємось назад на той же кут
        }

        // Запам'ятовуємо ідеальний кінцевий поворот
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
            float returnSpeed = rotationSpeed * currentReturnMult; // Використовуємо випадкову швидкість повернення

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
        _targetPosition = transform.position;

        IsRotating = false;
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
        onComplete?.Invoke();
    }
}