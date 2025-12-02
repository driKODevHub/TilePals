using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FacialExpressionController : MonoBehaviour
{
    [System.Serializable]
    public class EyeRig
    {
        [Header("References")]
        [Tooltip("Важливо: Z - вперед, Y - вгору.")]
        public Transform referencePivot;

        [Tooltip("Кістка зіниці.")]
        public Transform eyeBone;

        [Tooltip("Об'єкт повіки.")]
        public GameObject blinkObject;

        [Header("Limits & Settings")]
        [Tooltip("Зменш це значення, щоб обмежити фізичний радіус руху кістки.")]
        public float scaleMultiplier = 0.1f;

        public bool useEllipticalClamp = true;

        [Tooltip("Межі (0..1), де 1 = 90 градусів повороту.")]
        public float limitLeft = 0.5f;
        public float limitRight = 0.5f;
        public float limitUp = 0.5f;
        public float limitDown = 0.5f;

        [Header("Resting State")]
        [Tooltip("Позиція зіниці, коли кіт нікуди не дивиться (Reset). X=Hor, Y=Vert. Спробуй Y=-0.2 щоб трохи опустити.")]
        public Vector2 restPosition = Vector2.zero;

        // Внутрішній стан: поточний "віртуальний" поворот ока у світовому просторі
        [HideInInspector] public Quaternion currentWorldRotation;
    }

    [Header("3D Eye Configuration")]
    [Tooltip("Чутливість. 1.0 = 1:1 слідування. Більше = око рухається швидше за ціль.")]
    [SerializeField] private float lookSensitivity = 1.0f;
    [SerializeField] private EyeRig leftEye;
    [SerializeField] private EyeRig rightEye;

    [Header("Smoothing (FEEL Style)")]
    [Tooltip("Швидкість повороту (градуси/сек). Як в FEEL.")]
    [SerializeField] private float rotationSpeed = 30f;
    [Tooltip("Додаткова інерція (0 = немає, 1 = дуже повільно).")]
    [Range(0f, 1f)][SerializeField] private float damping = 0.1f;

    [Header("Blinking")]
    [SerializeField] private float blinkIntervalMin = 3f;
    [SerializeField] private float blinkIntervalMax = 7f;
    [SerializeField] private float blinkDuration = 0.15f;
    [SerializeField] private bool hideObjectOnBlink = true;

    [Header("Axis Configuration")]
    [SerializeField] private bool yAxisIsUp = true;

    [Header("Debug")]
    [SerializeField] private bool showGizmos = true;
    [SerializeField] private float gizmoSphereSize = 0.005f;

    private Coroutine _blinkingCoroutine;
    private Vector3 _currentWorldLookTarget;
    private bool _isLookingAtSomething = false;

    private void Start()
    {
        InitializeEyeRotation(leftEye);
        InitializeEyeRotation(rightEye);

        if (_blinkingCoroutine != null) StopCoroutine(_blinkingCoroutine);
        _blinkingCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void InitializeEyeRotation(EyeRig rig)
    {
        if (rig.referencePivot != null)
        {
            // На старті око дивиться туди ж, куди й півот (плюс офсет спокою)
            rig.currentWorldRotation = GetRestingRotation(rig);
        }
    }

    private void LateUpdate()
    {
        UpdateEye(leftEye);
        UpdateEye(rightEye);
    }

    public void LookAt(Vector3 worldPosition)
    {
        _currentWorldLookTarget = worldPosition;
        _isLookingAtSomething = true;
    }

    public void ResetPupilPosition()
    {
        _isLookingAtSomething = false;
    }

    private Quaternion GetRestingRotation(EyeRig rig)
    {
        // Конвертуємо RestPosition (2D зміщення) у Поворот відносно півота
        // Якщо Y is Up: X - Yaw, Y - Pitch (навпаки до координат миші)
        // rest.x -> поворот навколо Y (вправо/вліво)
        // rest.y -> поворот навколо X (вверх/вниз, мінус бо X inverted для очей)

        float yaw = rig.restPosition.x * 45f; // Приблизне маппінг: 1.0 = 45 градусів
        float pitch = -rig.restPosition.y * 45f;

        return rig.referencePivot.rotation * Quaternion.Euler(pitch, yaw, 0);
    }

    private void UpdateEye(EyeRig rig)
    {
        if (rig.eyeBone == null || rig.referencePivot == null) return;

        Quaternion targetRotation;

        if (_isLookingAtSomething)
        {
            Vector3 directionToTarget = _currentWorldLookTarget - rig.referencePivot.position;
            if (directionToTarget != Vector3.zero)
            {
                targetRotation = Quaternion.LookRotation(directionToTarget, rig.referencePivot.up);
            }
            else
            {
                targetRotation = GetRestingRotation(rig);
            }
        }
        else
        {
            // Якщо нікуди не дивимось - повертаємось в позицію спокою
            targetRotation = GetRestingRotation(rig);
        }

        // 2. Плавно обертаємо "віртуальне око" (Slerp)
        float step = rotationSpeed * Time.deltaTime * (1f - damping);
        rig.currentWorldRotation = Quaternion.Slerp(rig.currentWorldRotation, targetRotation, step);

        // 3. Конвертація "Світовий Поворот" -> "Локальне Зміщення 2D"
        Vector3 stabilizedLookDir = rig.currentWorldRotation * Vector3.forward;
        Vector3 localDir = rig.referencePivot.InverseTransformDirection(stabilizedLookDir);

        // 4. Проекція на площину (X/Y)
        float x, y;
        if (yAxisIsUp) { x = localDir.x; y = localDir.y; }
        else { x = localDir.x; y = localDir.z; }

        x *= lookSensitivity;
        y *= lookSensitivity;

        // 5. Обмеження (Clamping)
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

        // 6. Застосування до кістки
        Vector3 finalLocalPos;
        if (yAxisIsUp) finalLocalPos = new Vector3(x, y, 0);
        else finalLocalPos = new Vector3(x, 0, y);

        rig.eyeBone.position = rig.referencePivot.TransformPoint(finalLocalPos);
        rig.eyeBone.rotation = rig.referencePivot.rotation;
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkIntervalMin, blinkIntervalMax));
            SetBlinkState(true);
            yield return new WaitForSeconds(blinkDuration);
            SetBlinkState(false);
        }
    }

    private void SetBlinkState(bool isBlinking)
    {
        bool shouldBeActive = hideObjectOnBlink ? !isBlinking : isBlinking;
        if (leftEye.blinkObject) leftEye.blinkObject.SetActive(shouldBeActive);
        if (rightEye.blinkObject) rightEye.blinkObject.SetActive(shouldBeActive);
    }

    public void ApplyEmotion(EmotionProfileSO emotionProfile) { }
    public void UpdateSortingOrder(bool isHeld) { }

    // --- GIZMOS ---
    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawEyeGizmos(leftEye);
        DrawEyeGizmos(rightEye);

        if (_isLookingAtSomething)
        {
            Gizmos.color = Color.magenta;
            if (leftEye.referencePivot) Gizmos.DrawLine(leftEye.referencePivot.position, _currentWorldLookTarget);
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

        Gizmos.color = new Color(0, 1, 1, 0.3f);

        Vector3 tl, tr, br, bl;
        if (yAxisIsUp)
        {
            tl = new Vector3(-lLeft, lUp, 0); tr = new Vector3(lRight, lUp, 0);
            br = new Vector3(lRight, -lDown, 0); bl = new Vector3(-lLeft, -lDown, 0);
        }
        else
        {
            tl = new Vector3(-lLeft, 0, lUp); tr = new Vector3(lRight, 0, lUp);
            br = new Vector3(lRight, 0, -lDown); bl = new Vector3(-lLeft, 0, -lDown);
        }

        Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);

        if (rig.eyeBone != null)
        {
            Gizmos.matrix = Matrix4x4.identity;
            Gizmos.color = Color.red;
            Gizmos.DrawWireSphere(rig.eyeBone.position, gizmoSphereSize);
        }
    }
}