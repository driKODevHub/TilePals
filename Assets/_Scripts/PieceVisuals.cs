using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Відповідає виключно за ВІЗУАЛ: аутлайн, та запуск фідбеків (звуки/ефекти).
/// </summary>
public class PieceVisuals : MonoBehaviour
{
    [System.Serializable]
    public class FeedbackCollection
    {
        [Header("Interaction (Mouse)")]
        public UnityEvent OnPickup;
        public UnityEvent OnDrop;      // Кидок в "молоко" (OffGrid) або просто відпускання
        public UnityEvent OnHoverStart;
        public UnityEvent OnHoverEnd;

        [Header("Action (Gameplay)")]
        public UnityEvent OnRotate;
        public UnityEvent OnPlaceSuccess; // Успішно поставили на сітку
        public UnityEvent OnPlaceFailed;  // Спроба поставити на зайняте місце (Error)

        [Header("Movement (Passive)")]
        public UnityEvent OnGridSnap;     // Фігура "знайшла" валідне місце під час перетягування
    }

    [Header("Outline Settings (Hover)")]
    [Tooltip("Виберіть шар, на якому працює ефект аутлайну (Hover).")]
    [SerializeField] private LayerMask outlineLayerMask;
    [SerializeField] private List<MeshRenderer> outlineMeshRenderers;
    [SerializeField] private List<SkinnedMeshRenderer> outlineSkinnedMeshRenderers;

    [Header("Feedbacks")]
    [Tooltip("Список всіх подій для Feel. Можна згортати.")]
    public FeedbackCollection feedbacks;

    // Внутрішні змінні
    private int _outlineLayerIndex;
    private Dictionary<Renderer, int> _originalLayers = new Dictionary<Renderer, int>();
    private bool _isOutlineLocked = false;
    private bool _isCurrentStateInvalid = false;

    private void Awake()
    {
        InitializeOutlineData();
    }

    private void OnDestroy()
    {
        if (_isCurrentStateInvalid && VisualFeedbackManager.Instance != null)
        {
            VisualFeedbackManager.Instance.SetInvalidState(false);
        }
    }

    #region Trigger Methods (Викликаються з PuzzleManager)

    public void PlayPickup() => feedbacks.OnPickup?.Invoke();
    public void PlayDrop() => feedbacks.OnDrop?.Invoke();
    public void PlayPlaceSuccess() => feedbacks.OnPlaceSuccess?.Invoke();

    // Новий метод для "помилки"
    public void PlayPlaceFailed()
    {
        feedbacks.OnPlaceFailed?.Invoke();
        // Можна тут додатково форсувати червоний аутлайн на долю секунди, якщо треба
    }

    public void PlayRotate() => feedbacks.OnRotate?.Invoke();
    public void PlayGridSnap() => feedbacks.OnGridSnap?.Invoke();

    #endregion

    #region Visual State Management

    /// <summary>
    /// Оновлює тільки ГРАФІЧНИЙ стан (червоний/синій аутлайн).
    /// Більше не викликає звукові фідбеки сам по собі.
    /// </summary>
    public void SetInvalidPlacementVisual(bool isInvalid)
    {
        // Оновлюємо стан, але не граємо фідбек (фідбек тепер в PlayPlaceFailed / PlayGridSnap)
        _isCurrentStateInvalid = isInvalid;

        if (VisualFeedbackManager.Instance != null)
        {
            VisualFeedbackManager.Instance.SetInvalidState(isInvalid);
        }
    }

    // Заглушка сумісності
    public void SetTemperamentMaterial(Material mat) { }

    #endregion

    #region Outline Management (Hover)
    private void InitializeOutlineData()
    {
        int maskValue = outlineLayerMask.value;
        if (maskValue > 0)
        {
            _outlineLayerIndex = 0;
            while ((maskValue & 1) == 0) { maskValue >>= 1; _outlineLayerIndex++; }
        }
        else
        {
            _outlineLayerIndex = 0;
        }

        CacheRendererLayers(outlineMeshRenderers);
        CacheRendererLayers(outlineSkinnedMeshRenderers);
    }

    private void CacheRendererLayers<T>(List<T> renderers) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r != null && !_originalLayers.ContainsKey(r))
                _originalLayers[r] = r.gameObject.layer;
        }
    }

    public void SetOutline(bool isActive)
    {
        if (_isOutlineLocked && !isActive) return;

        if (isActive) feedbacks.OnHoverStart?.Invoke();
        else feedbacks.OnHoverEnd?.Invoke();

        SetRenderersLayer(outlineMeshRenderers, isActive);
        SetRenderersLayer(outlineSkinnedMeshRenderers, isActive);
    }

    public void SetOutlineLocked(bool isLocked)
    {
        _isOutlineLocked = isLocked;
        SetOutline(isLocked);
    }

    private void SetRenderersLayer<T>(List<T> renderers, bool active) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            r.gameObject.layer = active ? _outlineLayerIndex : _originalLayers.GetValueOrDefault(r, 0);
        }
    }
    #endregion

    public void SetMeshesToColorFromEditor(List<MeshRenderer> newMeshes)
    {
        outlineMeshRenderers = newMeshes;
    }
}