using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Керує візуальним відображенням емоцій, включаючи моргання та рух зіниць.
/// </summary>
public class FacialExpressionController : MonoBehaviour
{
    [System.Serializable]
    public struct FeatureRenderer
    {
        public FacialFeatureSetSO.FeatureType featureType;
        public MeshRenderer featureRenderer;
    }

    // --- НОВА СТРУКТУРА ДЛЯ ОЧЕЙ ---
    [System.Serializable]
    public struct EyeRenderers
    {
        [Tooltip("MeshRenderer для форми лівого ока (білок).")]
        public MeshRenderer leftEyeShape;
        [Tooltip("Transform зіниці лівого ока для руху.")]
        public Transform leftPupil;
        [Space]
        [Tooltip("MeshRenderer для форми правого ока (білок).")]
        public MeshRenderer rightEyeShape;
        [Tooltip("Transform зіниці правого ока для руху.")]
        public Transform rightPupil;
    }

    [Header("Налаштування Рис Обличчя")]
    [SerializeField] private EyeRenderers eyes;
    [SerializeField] private List<FeatureRenderer> otherFeatures; // Рот, брови і т.д.

    [Header("Налаштування Поведінки Очей")]
    [SerializeField] private float blinkIntervalMin = 3f;
    [SerializeField] private float blinkIntervalMax = 7f;
    [SerializeField] private float blinkDuration = 0.1f;
    [SerializeField] private float pupilMovementRadius = 0.15f;

    private Dictionary<FacialFeatureSetSO.FeatureType, MeshRenderer> _rendererMap;
    private Vector3 _leftPupilOrigin;
    private Vector3 _rightPupilOrigin;

    private void Awake()
    {
        _rendererMap = new Dictionary<FacialFeatureSetSO.FeatureType, MeshRenderer>();
        foreach (var feature in otherFeatures)
        {
            if (feature.featureRenderer != null)
                _rendererMap[feature.featureType] = feature.featureRenderer;
        }

        if (eyes.leftPupil) _leftPupilOrigin = eyes.leftPupil.localPosition;
        if (eyes.rightPupil) _rightPupilOrigin = eyes.rightPupil.localPosition;
    }

    private void Start()
    {
        StartCoroutine(BlinkRoutine());
    }

    /// <summary>
    /// Застосовує повний профіль емоції до обличчя.
    /// </summary>
    public void ApplyEmotion(EmotionProfileSO emotionProfile)
    {
        if (emotionProfile == null)
        {
            SetAllFeaturesActive(false);
            return;
        }

        SetAllFeaturesActive(true);

        // Застосовуємо вираз очей
        ApplyEyeState(emotionProfile.eyeState);

        // Застосовуємо інші риси (рот, брови)
        foreach (var expression in emotionProfile.otherExpressions)
        {
            if (expression.featureSet == null) continue;
            if (_rendererMap.TryGetValue(expression.featureSet.feature, out MeshRenderer renderer))
            {
                ApplyTexture(renderer, expression.featureSet, expression.textureIndex);
            }
        }
    }

    /// <summary>
    /// Змушує зіниці дивитися на певну точку у світових координатах.
    /// </summary>
    public void LookAt(Vector3 worldPosition)
    {
        if (eyes.leftPupil)
        {
            Vector3 localTarget = eyes.leftPupil.parent.InverseTransformPoint(worldPosition);
            Vector3 direction = (localTarget - _leftPupilOrigin).normalized;
            eyes.leftPupil.localPosition = _leftPupilOrigin + direction * pupilMovementRadius;
        }
        if (eyes.rightPupil)
        {
            Vector3 localTarget = eyes.rightPupil.parent.InverseTransformPoint(worldPosition);
            Vector3 direction = (localTarget - _rightPupilOrigin).normalized;
            eyes.rightPupil.localPosition = _rightPupilOrigin + direction * pupilMovementRadius;
        }
    }

    // --- ПРИВАТНІ МЕТОДИ ---

    private void ApplyEyeState(EyeStateSO eyeState)
    {
        if (eyeState == null)
        {
            SetEyesActive(false);
            return;
        }

        SetEyesActive(true);
        if (eyes.leftEyeShape) eyes.leftEyeShape.material.mainTexture = eyeState.eyeShapeTexture;
        if (eyes.rightEyeShape) eyes.rightEyeShape.material.mainTexture = eyeState.eyeShapeTexture;

        if (eyes.leftPupil) eyes.leftPupil.GetComponent<MeshRenderer>().material.mainTexture = eyeState.pupilTexture;
        if (eyes.rightPupil) eyes.rightPupil.GetComponent<MeshRenderer>().material.mainTexture = eyeState.pupilTexture;
    }

    private void ApplyTexture(MeshRenderer renderer, FacialFeatureSetSO featureSet, int index)
    {
        if (index >= 0 && index < featureSet.textures.Count)
        {
            renderer.material.mainTexture = featureSet.textures[index];
            renderer.enabled = true;
        }
        else
        {
            renderer.enabled = false;
        }
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
        if (eyes.leftPupil) eyes.leftPupil.GetComponent<MeshRenderer>().enabled = isActive;
        if (eyes.rightPupil) eyes.rightPupil.GetComponent<MeshRenderer>().enabled = isActive;
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
