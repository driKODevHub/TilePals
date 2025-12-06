using UnityEngine;
using System.Collections;
#if UNITY_EDITOR
using UnityEditor;
#endif

public class FacialExpressionController : MonoBehaviour
{
    // --- НОВИЙ ENUM ДЛЯ ВИБОРУ ОСЕЙ ---
    public enum FacingDirection
    {
        Z_Forward_Y_Up, // Стандарт Unity (Z - вперед, Y - вгору)
        Y_Forward_Z_Up  // Твій варіант (Y - вперед, Z - вгору)
    }

    [System.Serializable]
    public class EyeRig
    {
        [Header("References")]
        [Tooltip("Півот ока.")]
        public Transform referencePivot;

        [Tooltip("Кістка зіниці.")]
        public Transform eyeBone;

        [Tooltip("Об'єкт повіки.")]
        public GameObject blinkObject;

        [Header("Limits & Settings")]
        [Tooltip("Множник руху.")]
        public float scaleMultiplier = 0.1f;

        public bool useEllipticalClamp = true;

        [Tooltip("Межі (0..1).")]
        public float limitLeft = 0.5f;
        public float limitRight = 0.5f;
        public float limitUp = 0.5f;
        public float limitDown = 0.5f;

        [Header("Resting State")]
        [Tooltip("Позиція зіниці в спокої.")]
        public Vector2 restPosition = Vector2.zero;

        // Внутрішній стан
        [HideInInspector] public Quaternion currentWorldRotation;
    }

    [Header("Axis Configuration")]
    [Tooltip("ВИБЕРИ ЦЕ: Куди дивиться око в локальних координатах півота?")]
    [SerializeField] private FacingDirection facingDirection = FacingDirection.Z_Forward_Y_Up;

    [Header("3D Eye Configuration")]
    [SerializeField] private float lookSensitivity = 1.0f;
    [SerializeField] private EyeRig leftEye;
    [SerializeField] private EyeRig rightEye;

    [Header("Smoothing")]
    [SerializeField] private float rotationSpeed = 30f;
    [Range(0f, 1f)][SerializeField] private float damping = 0.1f;

    [Header("Blinking")]
    [SerializeField] private float blinkIntervalMin = 3f;
    [SerializeField] private float blinkIntervalMax = 7f;
    [SerializeField] private float blinkDuration = 0.15f;
    [SerializeField] private bool hideObjectOnBlink = true;

    [Header("Gizmo Settings")]
    [Tooltip("Розмір Зеленої сфери (Rest Position).")]
    [Range(0.00001f, 2f)][SerializeField] private float gizmoScaleRest = 0.1f;
    [Tooltip("Розмір Червоної сфери (Current Position).")]
    [Range(0.00001f, 2f)][SerializeField] private float gizmoScaleCurrent = 0.1f;
    [SerializeField] private bool showGizmos = true;

    private Coroutine _blinkingCoroutine;
    private Vector3 _currentWorldLookTarget;
    private bool _isLookingAtSomething = false;

    public bool IsLookingAtSomething => _isLookingAtSomething;

    private void Start()
    {
        InitializeEyeRotation(leftEye);
        InitializeEyeRotation(rightEye);

        if (_blinkingCoroutine != null) StopCoroutine(_blinkingCoroutine);
        _blinkingCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            ForceUpdateEyeInEditor(leftEye);
            ForceUpdateEyeInEditor(rightEye);
        }
    }

    private void ForceUpdateEyeInEditor(EyeRig rig)
    {
        if (rig.referencePivot == null || rig.eyeBone == null) return;
        rig.currentWorldRotation = GetRestingRotation(rig);
        ApplyEyePosition(rig);
    }

    private void InitializeEyeRotation(EyeRig rig)
    {
        if (rig.referencePivot != null)
        {
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

    // --- ВИПРАВЛЕНА ЛОГІКА РОЗРАХУНКУ REST POSITION ---
    private Quaternion GetRestingRotation(EyeRig rig)
    {
        if (rig.referencePivot == null) return Quaternion.identity;

        float yaw = rig.restPosition.x * 45f;
        float pitch = -rig.restPosition.y * 45f;

        if (facingDirection == FacingDirection.Z_Forward_Y_Up)
        {
            // Стандарт: Pitch (X), Yaw (Y)
            return rig.referencePivot.rotation * Quaternion.Euler(pitch, yaw, 0);
        }
        else
        {
            // Твій варіант: Y=Forward, Z=Up.
            // Pitch (вгору/вниз) - це все ще вісь X (правило правої руки).
            // Yaw (вліво/вправо) - це тепер вісь Z (бо Z дивиться вгору).
            // Обертання навколо Y було б Roll (нахил голови), що нам не треба.
            return rig.referencePivot.rotation * Quaternion.Euler(pitch, 0, yaw);
        }
    }

    private void UpdateEye(EyeRig rig)
    {
        if (rig.eyeBone == null || rig.referencePivot == null) return;

        Quaternion targetRotation;

        if (_isLookingAtSomething)
        {
            Vector3 directionToTarget = _currentWorldLookTarget - rig.referencePivot.position;
            if (directionToTarget.sqrMagnitude < 0.001f)
            {
                targetRotation = GetRestingRotation(rig);
            }
            else
            {
                if (facingDirection == FacingDirection.Z_Forward_Y_Up)
                {
                    targetRotation = Quaternion.LookRotation(directionToTarget, rig.referencePivot.up);
                }
                else
                {
                    // Для Y-Forward:
                    // LookRotation вирівнює Z на ціль. Нам треба вирівняти Y на ціль.
                    // Поворот на 90 градусів по X перетворює Y на Z.
                    // Тому ми беремо стандартний LookRotation і "доворочуємо" його.
                    // Важливо: Vector3.up (світовий) або rig.referencePivot.forward (локальний Z, який є Up) як hint.
                    // Оскільки у тебе Z - це Up, то rig.referencePivot.forward дивиться вгору.
                    targetRotation = Quaternion.LookRotation(directionToTarget, rig.referencePivot.forward) * Quaternion.Euler(90, 0, 0);
                }
            }
        }
        else
        {
            targetRotation = GetRestingRotation(rig);
        }

        float step = rotationSpeed * Time.deltaTime * (1f - damping);
        rig.currentWorldRotation = Quaternion.Slerp(rig.currentWorldRotation, targetRotation, step);

        ApplyEyePosition(rig);
    }

    private void ApplyEyePosition(EyeRig rig)
    {
        Vector3 stabilizedLookDir = rig.currentWorldRotation * ((facingDirection == FacingDirection.Z_Forward_Y_Up) ? Vector3.forward : Vector3.up);
        Vector3 localDir = rig.referencePivot.InverseTransformDirection(stabilizedLookDir);

        float x, y;
        if (facingDirection == FacingDirection.Z_Forward_Y_Up)
        {
            x = localDir.x;
            y = localDir.y;
        }
        else
        {
            // Для Y-Forward: X - це ліво/право, Z - це вгору/вниз (локальний Y - це глибина).
            x = localDir.x;
            y = localDir.z;
        }

        x *= lookSensitivity;
        y *= lookSensitivity;

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
        if (facingDirection == FacingDirection.Z_Forward_Y_Up)
            finalLocalPos = new Vector3(x, y, 0);
        else
            finalLocalPos = new Vector3(x, 0, y); // Y - це глибина (0), Z - це висота (y)

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

    private void OnDrawGizmos()
    {
        if (!showGizmos) return;
        DrawEyeGizmos(leftEye);
        DrawEyeGizmos(rightEye);

        if (_isLookingAtSomething)
        {
            Gizmos.color = Color.magenta;
            if (leftEye.referencePivot) Gizmos.DrawLine(leftEye.referencePivot.position, _currentWorldLookTarget);
            if (rightEye.referencePivot) Gizmos.DrawLine(rightEye.referencePivot.position, _currentWorldLookTarget);
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

        // 1. Frame
        Gizmos.color = new Color(0, 1, 1, 0.3f);
        Vector3 tl, tr, br, bl;

        if (facingDirection == FacingDirection.Z_Forward_Y_Up)
        {
            tl = new Vector3(-lLeft, lUp, 0); tr = new Vector3(lRight, lUp, 0);
            br = new Vector3(lRight, -lDown, 0); bl = new Vector3(-lLeft, -lDown, 0);
        }
        else
        {
            // Для Y-Forward: Z це верх, X це право.
            tl = new Vector3(-lLeft, 0, lUp); tr = new Vector3(lRight, 0, lUp);
            br = new Vector3(lRight, 0, -lDown); bl = new Vector3(-lLeft, 0, -lDown);
        }

        Gizmos.DrawLine(tl, tr); Gizmos.DrawLine(tr, br);
        Gizmos.DrawLine(br, bl); Gizmos.DrawLine(bl, tl);

        // 2. Forward Direction
        Gizmos.color = Color.blue;
        Vector3 fwdDir = (facingDirection == FacingDirection.Z_Forward_Y_Up) ? Vector3.forward : Vector3.up;
        Gizmos.DrawLine(Vector3.zero, fwdDir * (Mathf.Max(lUp, lRight) * 2f));

        // 3. Rest Position (GREEN)
        float rX = rig.restPosition.x * lookSensitivity * rig.scaleMultiplier;
        float rY = rig.restPosition.y * lookSensitivity * rig.scaleMultiplier;
        Vector3 restPosLocal;

        if (facingDirection == FacingDirection.Z_Forward_Y_Up)
            restPosLocal = new Vector3(rX, rY, 0);
        else
            restPosLocal = new Vector3(rX, 0, rY);

        Gizmos.color = Color.green;
        // Використовуємо окремий розмір для Rest
        Gizmos.DrawWireSphere(restPosLocal, gizmoScaleRest);

        Gizmos.matrix = Matrix4x4.identity;

        // 4. Current Position (RED)
        if (rig.eyeBone != null)
        {
            Gizmos.color = Color.red;
            // Використовуємо окремий розмір для Current
            Gizmos.DrawWireSphere(rig.eyeBone.position, gizmoScaleCurrent);
        }
    }
}