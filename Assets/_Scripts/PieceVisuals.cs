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

    // --- FIX BUG: Tracking actual hover state to prevent double triggers ---
    private bool _isHovered = false;

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

    public void PlayPlaceFailed()
    {
        feedbacks.OnPlaceFailed?.Invoke();
    }

    public void PlayRotate() => feedbacks.OnRotate?.Invoke();
    public void PlayGridSnap() => feedbacks.OnGridSnap?.Invoke();

    #endregion

    #region Visual State Management

    public void SetInvalidPlacementVisual(bool isInvalid)
    {
        _isCurrentStateInvalid = isInvalid;

        if (VisualFeedbackManager.Instance != null)
        {
            VisualFeedbackManager.Instance.SetInvalidState(isInvalid);
        }
    }

    

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

        // --- FIX BUG: Logic to prevent double triggering of Hover Sounds ---
        // Ми перевіряємо, чи змінився стан, перш ніж викликати івент.
        // Якщо ми вже Hovered і нам знову кажуть SetOutline(true) (наприклад при пікапі), ми ігноруємо івент.

        if (isActive && !_isHovered)
        {
            feedbacks.OnHoverStart?.Invoke();
            _isHovered = true;
        }
        else if (!isActive && _isHovered)
        {
            feedbacks.OnHoverEnd?.Invoke();
            _isHovered = false;
        }
        // ---------------------------------------------------------------

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