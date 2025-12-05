using UnityEngine;
using System.Collections.Generic;
using UnityEngine.Events; // Для UnityEvents

/// <summary>
/// Відповідає виключно за ВІЗУАЛ: матеріали, аутлайн, партикли.
/// Це місце, де ти будеш підключати Feel (MMFeedbacks).
/// </summary>
public class PieceVisuals : MonoBehaviour
{
    [Header("Visual Components")]
    [SerializeField] private List<MeshRenderer> meshesToColor;

    [Header("Outline Settings")]
    [Tooltip("Виберіть шар, на якому працює ефект аутлайну.")]
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
    private Dictionary<MeshRenderer, Material[]> _originalMaterials;
    private bool _isInvalidMaterialApplied = false;

    private void Awake()
    {
        CacheOriginalMaterials();
        InitializeOutlineData();
    }

    #region Material Management
    private void CacheOriginalMaterials()
    {
        _originalMaterials = new Dictionary<MeshRenderer, Material[]>();
        if (meshesToColor == null) return;

        foreach (var meshRenderer in meshesToColor)
        {
            if (meshRenderer != null)
                _originalMaterials[meshRenderer] = meshRenderer.materials;
        }
    }

    public void SetTemperamentMaterial(Material mat)
    {
        if (mat == null || meshesToColor == null) return;
        foreach (var r in meshesToColor)
        {
            if (r != null)
            {
                var mats = new Material[r.materials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.materials = mats;
            }
        }
        CacheOriginalMaterials(); // Оновлюємо кеш, бо це тепер "базовий" матеріал
    }

    public void SetInvalidPlacementVisual(bool isInvalid, Material invalidMat)
    {
        if (isInvalid && !_isInvalidMaterialApplied)
        {
            ApplyMaterialOverride(invalidMat);
            _isInvalidMaterialApplied = true;
            OnPlaceInvalidFeedback?.Invoke();
        }
        else if (!isInvalid && _isInvalidMaterialApplied)
        {
            RestoreOriginalMaterials();
            _isInvalidMaterialApplied = false;
            OnPlaceValidFeedback?.Invoke();
        }
    }

    private void ApplyMaterialOverride(Material mat)
    {
        if (meshesToColor == null) return;
        foreach (var r in meshesToColor)
        {
            if (r != null)
            {
                var mats = new Material[r.materials.Length];
                for (int i = 0; i < mats.Length; i++) mats[i] = mat;
                r.materials = mats;
            }
        }
    }

    private void RestoreOriginalMaterials()
    {
        if (meshesToColor == null) return;
        foreach (var r in meshesToColor)
        {
            if (r != null && _originalMaterials.ContainsKey(r))
            {
                r.materials = _originalMaterials[r];
            }
        }
    }
    #endregion

    #region Outline Management
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
        if (isLocked) SetOutline(true);
        else SetOutline(false);
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

    // --- ДЛЯ EDITOR SCRIPT (Щоб не ламався кастомний едітор) ---
    public void SetMeshesToColorFromEditor(List<MeshRenderer> newMeshes)
    {
        meshesToColor = newMeshes;
    }
}