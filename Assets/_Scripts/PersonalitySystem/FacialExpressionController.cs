using UnityEngine;
using System.Collections;
using System.Collections.Generic;

public class FacialExpressionController : MonoBehaviour
{
    // --- PREFS ---
    public enum FacingDirection { Z_Forward_Y_Up, Y_Forward_Z_Up }

    [System.Serializable]
    public class EyeRig
    {
        public Transform referencePivot; // Корінь (Pivot), відносно якого все обертається
        public Transform eyeBone;        // Сама кістка ока
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
        
        [Header("Scaling")]
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

    [Header("--- 3D FEATURES BINDING ---")]
    [Tooltip("Прив'язка типів до конкретних об'єктів на сцені.")]
    public List<FeatureBinding> featureBindings;

    [Header("--- DEBUG / GIZMOS ---")]
    [Range(0.00001f, 2f)][SerializeField] private float gizmoScaleRest = 0.0005f;
    [Range(0.00001f, 2f)][SerializeField] private float gizmoScaleCurrent = 0.0005f;
    [SerializeField] private bool showGizmos = true;

    // --- STATE ---
    private Coroutine _blinkingCoroutine;
    private Vector3 _currentWorldLookTarget;
    private bool _isLookingAtSomething = false;

    public bool IsLookingAtSomething => _isLookingAtSomething;

    private void Start()
    {
        InitializeEyeRotation(leftEye);
        InitializeEyeRotation(rightEye);
        InitializeFacePosition(faceRig);

        if (_blinkingCoroutine != null) StopCoroutine(_blinkingCoroutine);
        _blinkingCoroutine = StartCoroutine(BlinkRoutine());
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
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
        if (rig.referencePivot == null || rig.eyeBone == null) return;
        rig.currentWorldRotation = GetRestingRotation(rig);
        ApplyEyePosition(rig);
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
        UpdateEye(leftEye);
        UpdateEye(rightEye);
        UpdateFace(faceRig);
    }

    // --- PUBLIC API ---

    public void LookAt(Vector3 worldPosition)
    {
        _currentWorldLookTarget = worldPosition;
        _isLookingAtSomething = true;
    }

    public void ResetPupilPosition()
    {
        _isLookingAtSomething = false;
    }

    /// <summary>
    /// Застосовує профіль емоції: вимикає всі відомі прив'язки, потім вмикає потрібні.
    /// </summary>
    public void ApplyEmotion(EmotionProfileSO emotionProfileSO)
    {
        if (emotionProfileSO == null) return;

        // 1. Reset: Disable ALL registered feature objects
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

        // 2. Apply: Enable objects for current emotion
        if (emotionProfileSO.featureStates != null)
        {
            foreach (var featureState in emotionProfileSO.featureStates)
            {
                if (featureState != null)
                {
                    ActivateFeature(featureState.featureType);
                }
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
        if (rig.eyeBone == null || rig.referencePivot == null) return;

        Quaternion targetRotation;

        if (_isLookingAtSomething)
        {
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
        }
        else
        {
            targetRotation = GetRestingRotation(rig);
        }

        float step = eyesRotationSpeed * Time.deltaTime * (1f - eyesDamping);
        rig.currentWorldRotation = Quaternion.Slerp(rig.currentWorldRotation, targetRotation, step);

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

        rig.eyeBone.position = rig.referencePivot.TransformPoint(finalLocalPos);
        rig.eyeBone.rotation = rig.referencePivot.rotation;
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
    

    // ===================================================================================
    // LOGIC: BLINKING
    // ===================================================================================

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
