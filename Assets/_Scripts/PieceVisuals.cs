using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events;

/// <summary>
/// Відповідає виключно за ВІЗУАЛ: матеріали, аутлайн, партикли.
/// ОНОВЛЕНО: Тепер використовує VisualFeedbackManager для зміни глобальних ефектів замість зміни матеріалів.
/// </summary>
public class PieceVisuals : MonoBehaviour
{
    [Header("Visual Components (Legacy / Optional)")]
    [Tooltip("Список мешів. У новій системі використовується для аутлайну, але не для зміни кольору валідації.")]
    [SerializeField] private List<MeshRenderer> meshesToColor;

    [Header("Outline Settings (Hover)")]
    [Tooltip("Виберіть шар, на якому працює ефект аутлайну (Hover).")]
    [SerializeField] private LayerMask outlineLayerMask;
    [SerializeField] private List<MeshRenderer> outlineMeshRenderers;
    [SerializeField] private List<SkinnedMeshRenderer> outlineSkinnedMeshRenderers;

    [Header("Feedback Events (Connect Feel Here)")]
    public UnityEvent OnPickupFeedback;
    public UnityEvent OnDropFeedback;
    public UnityEvent OnPlaceValidFeedback;
    public UnityEvent OnPlaceInvalidFeedback;
    public UnityEvent OnHoverStartFeedback;
    public UnityEvent OnHoverEndFeedback;

    // Внутрішні змінні для аутлайну
    private int _outlineLayerIndex;
    private Dictionary<Renderer, int> _originalLayers = new Dictionary<Renderer, int>();
    private bool _isOutlineLocked = false;

    // Стан валідності
    private bool _isCurrentStateInvalid = false;

    private void Awake()
    {
        InitializeOutlineData();
    }

    private void OnDestroy()
    {
        // При знищенні переконуємось, що ми не залишили гру в стані "помилки"
        if (_isCurrentStateInvalid && VisualFeedbackManager.Instance != null)
        {
            VisualFeedbackManager.Instance.SetInvalidState(false);
        }
    }

    #region Visual State Management (New Logic)

    /// <summary>
    /// Основний метод, який викликається з PuzzleManager/PuzzlePiece для відображення валідності.
    /// </summary>
    public void SetInvalidPlacementVisual(bool isInvalid, Material invalidMat_Ignored = null)
    {
        // Викликаємо події для MMFeedbacks (Feel)
        if (isInvalid && !_isCurrentStateInvalid)
        {
            OnPlaceInvalidFeedback?.Invoke();
        }
        else if (!isInvalid && _isCurrentStateInvalid)
        {
            OnPlaceValidFeedback?.Invoke();
        }

        _isCurrentStateInvalid = isInvalid;

        // --- НОВА ЛОГІКА: Делегуємо зміну візуалу глобальному менеджеру ---
        if (VisualFeedbackManager.Instance != null)
        {
            VisualFeedbackManager.Instance.SetInvalidState(isInvalid);
        }

        // Стара логіка зміни матеріалів видалена за запитом.
        // Якщо потрібно буде повернути - додай сюди код зміни матеріалів з meshesToColor.
    }

    // Заглушка для сумісності з іншими скриптами (наприклад, Personality), якщо вони викликають цей метод
    public void SetTemperamentMaterial(Material mat)
    {
        // У новій системі з Render Features темперамент, можливо, не впливає на матеріал так прямо,
        // або ти захочеш змінювати Base Map.
        // Поки що залишаємо стандартну зміну матеріалу, якщо вона не конфліктує з фічами.
        if (mat == null || meshesToColor == null) return;
        foreach (var r in meshesToColor)
        {
            if (r != null)
            {
                // Просто замінюємо матеріал, як і раніше. 
                // Це не впливає на "червоний" режим, бо він тепер через Overlay/RenderFeature.
                var mats = new Material[r.materials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.materials = mats;
            }
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
            _outlineLayerIndex = 0; // Default
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

        if (isActive) OnHoverStartFeedback?.Invoke();
        else OnHoverEndFeedback?.Invoke();

        SetRenderersLayer(outlineMeshRenderers, isActive);
        SetRenderersLayer(outlineSkinnedMeshRenderers, isActive);
    }

    public void SetOutlineLocked(bool isLocked)
    {
        _isOutlineLocked = isLocked;
        // Якщо ми заблокували аутлайн (взяли фігуру), ми його вмикаємо
        SetOutline(isLocked);
    }

    private void SetRenderersLayer<T>(List<T> renderers, bool active) where T : Renderer
    {
        if (renderers == null) return;
        foreach (var r in renderers)
        {
            if (r == null) continue;
            // Перемикаємо шар об'єкта на шар аутлайну або повертаємо назад
            r.gameObject.layer = active ? _outlineLayerIndex : _originalLayers.GetValueOrDefault(r, 0);
        }
    }
    #endregion

    // --- ДЛЯ EDITOR SCRIPT ---
    public void SetMeshesToColorFromEditor(List<MeshRenderer> newMeshes)
    {
        meshesToColor = newMeshes;
    }
}