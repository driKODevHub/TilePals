using UnityEngine;
using System.Collections;
using System;

/// <summary>
/// Відповідає виключно за ФІЗИЧНЕ переміщення та обертання об'єкта.
/// Не містить ігрової логіки.
/// </summary>
public class PieceMovement : MonoBehaviour
{
    [Header("Settings")]
    [SerializeField] private float followSpeed = 25f;
    [SerializeField] private float rotationSpeed = 360f;

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

    // Специфічний метод для обертання навколо точки (півота), як це було в старій логіці
    public void RotateAroundPivot(Vector3 pivotPoint, Vector3 axis, float angle, Action onComplete)
    {
        if (IsRotating) return;
        StartCoroutine(SmoothRotationAroundPoint(pivotPoint, axis, angle, onComplete));
    }

    private IEnumerator SmoothRotationAroundPoint(Vector3 pivot, Vector3 axis, float angleBy, Action onComplete)
    {
        IsRotating = true;
        float traveled = 0f;
        float absAngle = Mathf.Abs(angleBy);
        float direction = Mathf.Sign(angleBy);

        Quaternion finalRotation = transform.rotation * Quaternion.Euler(0, angleBy, 0);

        while (traveled < absAngle)
        {
            float step = Time.deltaTime * rotationSpeed;
            step = Mathf.Min(step, absAngle - traveled);

            transform.RotateAround(pivot, axis, step * direction);

            traveled += step;
            yield return null;
        }

        // Вирівнюємо точно в кінці
        transform.rotation = finalRotation;
        _targetRotation = finalRotation;

        IsRotating = false;
        onComplete?.Invoke();
    }

    // Просте обертання на місці (якщо знадобиться)
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