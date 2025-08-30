using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Керує візуальним відображенням емоцій за допомогою SpriteRenderer,
/// включаючи моргання, рух зіниць, порядок сортування та маскування.
/// </summary>
public class FacialExpressionController : MonoBehaviour
{
    [System.Serializable]
    public struct FeatureRenderer
    {
        public FeatureStateSO.FeatureType featureType;
        public SpriteRenderer featureRenderer;
    }

    [System.Serializable]
    public struct EyeRenderers
    {
        public SpriteRenderer leftEyeShape;
        public SpriteRenderer leftPupil;
        public SpriteMask leftEyeMask;
        [Space]
        public SpriteRenderer rightEyeShape;
        public SpriteRenderer rightPupil;
        public SpriteMask rightEyeMask;
    }

    [Header("Налаштування Рис Обличчя")]
    [SerializeField] private EyeRenderers eyes;
    [SerializeField] private List<FeatureRenderer> otherFeatures;

    [Header("Налаштування Поведінки Очей")]
    [SerializeField] private float blinkIntervalMin = 3f;
    [SerializeField] private float blinkIntervalMax = 7f;
    [SerializeField] private float blinkDuration = 0.1f;
    [SerializeField] private float pupilMovementRadius = 0.15f;
    [Tooltip("Швидкість, з якою зіниці плавно рухаються до цілі.")]
    [SerializeField] private float pupilLookSpeed = 8f;


    [Header("Налаштування Сортування")]
    [SerializeField] private int sortingOrderIdle = 5;
    [SerializeField] private int sortingOrderHeld = 15;

    private Dictionary<FeatureStateSO.FeatureType, SpriteRenderer> _rendererMap;
    private Vector3 _leftPupilOrigin, _rightPupilOrigin;

    private Vector3 _leftPupilTargetLocalPos;
    private Vector3 _rightPupilTargetLocalPos;
    private Coroutine _blinkingCoroutine;
    private bool _areEyesVisible = true;


    private void Awake()
    {
        _rendererMap = new Dictionary<FeatureStateSO.FeatureType, SpriteRenderer>();
        foreach (var feature in otherFeatures)
        {
            if (feature.featureRenderer != null)
                _rendererMap[feature.featureType] = feature.featureRenderer;
        }

        if (eyes.leftPupil)
        {
            _leftPupilOrigin = eyes.leftPupil.transform.localPosition;
            _leftPupilTargetLocalPos = _leftPupilOrigin;
        }
        if (eyes.rightPupil)
        {
            _rightPupilOrigin = eyes.rightPupil.transform.localPosition;
            _rightPupilTargetLocalPos = _rightPupilOrigin;
        }

        UpdateSortingOrder(false);
    }

    private void Start()
    {
        if (_blinkingCoroutine != null) StopCoroutine(_blinkingCoroutine);
        _blinkingCoroutine = StartCoroutine(BlinkRoutine());
    }

    private void Update()
    {
        if (eyes.leftPupil)
        {
            eyes.leftPupil.transform.localPosition = Vector3.Lerp(eyes.leftPupil.transform.localPosition, _leftPupilTargetLocalPos, Time.deltaTime * pupilLookSpeed);
        }
        if (eyes.rightPupil)
        {
            eyes.rightPupil.transform.localPosition = Vector3.Lerp(eyes.rightPupil.transform.localPosition, _rightPupilTargetLocalPos, Time.deltaTime * pupilLookSpeed);
        }
    }

    public void UpdateSortingOrder(bool isHeld)
    {
        int baseOrder = isHeld ? sortingOrderHeld : sortingOrderIdle;

        if (eyes.leftEyeShape) eyes.leftEyeShape.sortingOrder = baseOrder;
        if (eyes.rightEyeShape) eyes.rightEyeShape.sortingOrder = baseOrder;

        if (eyes.leftPupil) eyes.leftPupil.sortingOrder = baseOrder + 1;
        if (eyes.rightPupil) eyes.rightPupil.sortingOrder = baseOrder + 1;

        foreach (var rendererPair in _rendererMap)
        {
            rendererPair.Value.sortingOrder = baseOrder;
        }
    }

    public void ApplyEmotion(EmotionProfileSO emotionProfile)
    {
        if (emotionProfile == null)
        {
            SetAllFeaturesActive(false);
            return;
        }

        // --- ВИПРАВЛЕННЯ: Спочатку вмикаємо очі, якщо вони мають бути в емоції ---
        ApplyEyeState(emotionProfile.eyeState);

        foreach (var rendererPair in _rendererMap)
        {
            rendererPair.Value.enabled = false;
        }

        foreach (var featureState in emotionProfile.featureStates)
        {
            if (featureState == null) continue;

            if (_rendererMap.TryGetValue(featureState.feature, out SpriteRenderer renderer))
            {
                renderer.sprite = featureState.expressionSprite;
                renderer.enabled = featureState.expressionSprite != null;
            }
        }
    }

    public void LookAt(Vector3 worldPosition)
    {
        if (eyes.leftPupil)
        {
            Vector3 localTarget = eyes.leftPupil.transform.parent.InverseTransformPoint(worldPosition);
            Vector3 direction = localTarget - _leftPupilOrigin;
            direction.z = 0;
            _leftPupilTargetLocalPos = _leftPupilOrigin + Vector3.ClampMagnitude(direction, pupilMovementRadius);
        }
        if (eyes.rightPupil)
        {
            Vector3 localTarget = eyes.rightPupil.transform.parent.InverseTransformPoint(worldPosition);
            Vector3 direction = localTarget - _rightPupilOrigin;
            direction.z = 0;
            _rightPupilTargetLocalPos = _rightPupilOrigin + Vector3.ClampMagnitude(direction, pupilMovementRadius);
        }
    }

    public void ResetPupilPosition()
    {
        _leftPupilTargetLocalPos = _leftPupilOrigin;
        _rightPupilTargetLocalPos = _rightPupilOrigin;
    }


    private void ApplyEyeState(EyeStateSO eyeState)
    {
        if (eyeState == null)
        {
            SetEyesActive(false);
            return;
        }

        SetEyesActive(true);
        if (eyes.leftEyeShape) eyes.leftEyeShape.sprite = eyeState.eyeShapeSprite;
        if (eyes.rightEyeShape) eyes.rightEyeShape.sprite = eyeState.eyeShapeSprite;
        if (eyes.leftPupil) eyes.leftPupil.sprite = eyeState.pupilSprite;
        if (eyes.rightPupil) eyes.rightPupil.sprite = eyeState.pupilSprite;

        if (eyes.leftEyeMask) eyes.leftEyeMask.sprite = eyeState.eyeMaskSprite;
        if (eyes.rightEyeMask) eyes.rightEyeMask.sprite = eyeState.eyeMaskSprite;
    }

    private IEnumerator BlinkRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(Random.Range(blinkIntervalMin, blinkIntervalMax));

            // --- ВИПРАВЛЕННЯ: Кліпаємо, тільки якщо очі зараз видимі ---
            if (_areEyesVisible)
            {
                SetEyesRenderersActive(false);
                yield return new WaitForSeconds(blinkDuration);
                SetEyesRenderersActive(true);
            }
        }
    }

    private void SetEyesActive(bool isActive)
    {
        _areEyesVisible = isActive;
        SetEyesRenderersActive(isActive);
    }

    // --- НОВИЙ ДОПОМІЖНИЙ МЕТОД ---
    // Цей метод просто вмикає/вимикає рендери, не змінюючи стан _areEyesVisible
    private void SetEyesRenderersActive(bool isActive)
    {
        if (eyes.leftEyeShape) eyes.leftEyeShape.enabled = isActive;
        if (eyes.rightEyeShape) eyes.rightEyeShape.enabled = isActive;
        if (eyes.leftPupil) eyes.leftPupil.enabled = isActive;
        if (eyes.rightPupil) eyes.rightPupil.enabled = isActive;
        if (eyes.leftEyeMask) eyes.leftEyeMask.enabled = isActive;
        if (eyes.rightEyeMask) eyes.rightEyeMask.enabled = isActive;
    }

    private void SetAllFeaturesActive(bool isActive)
    {
        SetEyesActive(isActive);
        foreach (var rendererPair in _rendererMap)
        {
            rendererPair.Value.enabled = isActive;
        }
    }
}

