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

    [Header("Налаштування Сортування")]
    [SerializeField] private int sortingOrderIdle = 5;
    [SerializeField] private int sortingOrderHeld = 15;

    private Dictionary<FeatureStateSO.FeatureType, SpriteRenderer> _rendererMap;
    private Vector3 _leftPupilOrigin, _rightPupilOrigin;

    private void Awake()
    {
        _rendererMap = new Dictionary<FeatureStateSO.FeatureType, SpriteRenderer>();
        foreach (var feature in otherFeatures)
        {
            if (feature.featureRenderer != null)
                _rendererMap[feature.featureType] = feature.featureRenderer;
        }

        if (eyes.leftPupil) _leftPupilOrigin = eyes.leftPupil.transform.localPosition;
        if (eyes.rightPupil) _rightPupilOrigin = eyes.rightPupil.transform.localPosition;

        UpdateSortingOrder(false);
    }

    private void Start()
    {
        StartCoroutine(BlinkRoutine());
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

        foreach (var rendererPair in _rendererMap)
        {
            rendererPair.Value.enabled = false;
        }

        ApplyEyeState(emotionProfile.eyeState);

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

            // ВИПРАВЛЕНО: Ігноруємо рух по осі Z, щоб уникнути Z-fighting
            direction.z = 0;

            eyes.leftPupil.transform.localPosition = _leftPupilOrigin + direction.normalized * pupilMovementRadius;
        }
        if (eyes.rightPupil)
        {
            Vector3 localTarget = eyes.rightPupil.transform.parent.InverseTransformPoint(worldPosition);
            Vector3 direction = localTarget - _rightPupilOrigin;

            // ВИПРАВЛЕНО: Ігноруємо рух по осі Z, щоб уникнути Z-fighting
            direction.z = 0;

            eyes.rightPupil.transform.localPosition = _rightPupilOrigin + direction.normalized * pupilMovementRadius;
        }
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
            SetEyesActive(false);
            yield return new WaitForSeconds(blinkDuration);
            SetEyesActive(true);
        }
    }

    private void SetEyesActive(bool isActive)
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
