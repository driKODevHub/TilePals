using System;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;

namespace INab.AdvancedEdgeDetection.URP
{
	[System.Serializable]
	public class EdgeDetectionSettings
	{
		public RenderPassEvent Event = RenderPassEvent.BeforeRenderingTransparents;
        public bool _ControlViaVolumes = false;
        public LayerMask _CustomDataLayerMask;
		public LayerMask _DepthMaskLayerMask;

		public bool _UseCustomData = false;
		public bool _UseDepthMask = false;

		// Custom Data
		public bool _UseCustomTexture;
		public Texture _CustomTexture;
		public LayerMask _CustomTextureLayerMask;

		// Edge Detection properties

		// Main
		[Range(0,5)]
		public float _Thickness = 1.0f;
		public bool _ResolutionAdjust = false;

		// Depth Fade
		public bool _UseDepthFade = false;
		public float _FadeStart = 20;
		public float _FadeEnd = 40;

		// Normals
		public bool _NormalsEdgeDetection = true;
		[Range(.01f,1.5f)]
		public float _NormalsOffset = 0.1f;
		[Range(0,.99f)]
		public float _NormalsHardness = 0;
		[Range(1, 5)]
		public float _NormalsPower = 1;

		// Depth
		public bool _DepthEdgeDetection = true;
		public bool _AcuteAngleFix = false;
		[Range(0, 1)]
		public float _ViewDirThreshold = 1;
		[Range(0, 100)]
		public float _ViewDirThresholdScale = 50;
		[Range(0, 3)]
		public float _DepthThreshold = 1;
		[Range(0, 1)]
		public float _DepthHardness = .9f;
		[Range(1, 5)]
		public float _DepthPower = 5;


		// Edge Blend properties

		// Colors
		public Color _EdgeColor = Color.black;
		
		public bool _UseEdgeBlendDepthFade = false;
		public float _EdgeBlendFadeStart = 10;
		public float _EdgeBlendFadeEnd = 20;

		// Sketch
		public bool _UseSketchEdges = false;
		[Range(0, .01f)]
		public float _Amplitude = .005f;
		[Range(0, 150)]
		public float _Frequency = 40;
		[Range(0, 10)]
		public float _ChangesPerSecond = 0;


		// Grain
		public bool _UseGrain = false;
		public Texture2D _GrainTexture;
		[Range(0, 1)]
		public float _GrainStrength = 1;
		[Range(0, 3)]
		public float _GrainScale = 1;

		// UV Offset
		public bool _UseUvOffset = false;
		public Texture2D _OffsetNoise;
		[Range(0, 4)]
		public float _OffsetNoiseScale = .4f;
		[Range(0, 10)]
		public float _OffsetChangesPerSecond = 0;
		[Range(0, .01f)]
		public float _OffsetStrength = .005f;
	}

	public class AdvancedEdgeDetection : ScriptableRendererFeature
	{
        public class TextureRefData : ContextItem
        {
            public TextureHandle depthMaskTexture = TextureHandle.nullHandle;
            public TextureHandle customDataTexture = TextureHandle.nullHandle;

            public override void Reset()
            {
                depthMaskTexture = TextureHandle.nullHandle;
                customDataTexture = TextureHandle.nullHandle;
            }
        }

        [SerializeField] private EdgeDetectionSettings m_Settings = new EdgeDetectionSettings();

        [SerializeField][HideInInspector] private Shader m_EdgeDetectionShader;
        [SerializeField][HideInInspector] private Shader m_EdgeBlendShader;

        [SerializeField][HideInInspector] private Shader m_DepthMaskShader;
        [SerializeField][HideInInspector] private Shader m_CustomDataShader;

        private Material m_EdgeDetectionMaterial;
        private Material m_EdgeBlendMaterial;

        private Material m_DepthMaskMaterial;
        private Material m_CustomDataMaterial;
        private Material m_CustomDataMaterialTexture;

        private EdgeDetectionPass m_EdgeDetectionPass = null;
        private EdgeBlendPass m_EdgeBlendPass = null;
        private BlitTemporaryCameraRenderPass m_BlitTemporaryCameraRenderPass = null;
        private CustomDataPass m_CustomDataPass = null;
        private DepthMaskPass m_DepthMaskPass = null;

		public override void Create()
		{
            m_EdgeDetectionPass = new EdgeDetectionPass();
            m_EdgeBlendPass = new EdgeBlendPass();
            m_BlitTemporaryCameraRenderPass = new BlitTemporaryCameraRenderPass();
            m_CustomDataPass = new CustomDataPass("Custom Data Pass");
            m_DepthMaskPass = new DepthMaskPass("Edge Detection Depth Mask Pass");
		}

		public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
		{
            if (m_EdgeDetectionPass == null || m_EdgeBlendPass == null)
                return;

            if (renderingData.cameraData.cameraType == CameraType.Preview || renderingData.cameraData.cameraType == CameraType.Reflection)
                return;

            // [Update 2.1] disable effect by toggling off Post Processing in camera settings
            if (renderingData.cameraData.postProcessEnabled == false)
                return;

            // [Update 2.1] fixed memory leaks 
            // Create materials only once
            if (m_EdgeDetectionMaterial == null) m_EdgeDetectionMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Shader Graphs/EdgeDetection"));
            if (m_EdgeBlendMaterial == null) m_EdgeBlendMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Shader Graphs/EdgeBlend"));
            if (m_DepthMaskMaterial == null) m_DepthMaskMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Shader Graphs/DepthMask"));
            if (m_CustomDataMaterial == null) m_CustomDataMaterial = CoreUtils.CreateEngineMaterial(Shader.Find("Shader Graphs/CustomData"));
            if (m_CustomDataMaterialTexture == null) m_CustomDataMaterialTexture = CoreUtils.CreateEngineMaterial(Shader.Find("Shader Graphs/CustomData"));

            // old
            /*
            m_EdgeDetectionShader = Shader.Find("Shader Graphs/EdgeDetection");
            m_EdgeBlendShader = Shader.Find("Shader Graphs/EdgeBlend");

            m_DepthMaskShader = Shader.Find("Shader Graphs/DepthMask");
            m_CustomDataShader = Shader.Find("Shader Graphs/CustomData");


            if (m_EdgeDetectionShader == null || m_EdgeBlendShader == null)
                return;

            m_EdgeDetectionMaterial = new Material(m_EdgeDetectionShader);
            m_EdgeBlendMaterial = new Material(m_EdgeBlendShader);
            m_DepthMaskMaterial = new Material(m_DepthMaskShader);
            m_CustomDataMaterial = new Material(m_CustomDataShader);
            m_CustomDataMaterialTexture = new Material(m_CustomDataShader);
             */

            if (m_Settings._UseDepthMask) m_DepthMaskPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            if (m_Settings._UseCustomData) m_CustomDataPass.renderPassEvent = RenderPassEvent.AfterRenderingOpaques;
            m_EdgeDetectionPass.renderPassEvent = m_Settings.Event;
            m_EdgeBlendPass.renderPassEvent = m_Settings.Event;
            m_BlitTemporaryCameraRenderPass.renderPassEvent = m_Settings.Event;

            // Required if depth priming or depth texture in urp settings is off. Normals work only with Forward and Forward+. (TODO: for now)
            m_EdgeDetectionPass.ConfigureInput(ScriptableRenderPassInput.Depth | ScriptableRenderPassInput.Normal);
            
            if (m_Settings._UseDepthMask) m_DepthMaskPass.Setup(ref m_DepthMaskMaterial,ref m_Settings);
            if (m_Settings._UseCustomData) m_CustomDataPass.Setup(ref m_CustomDataMaterial,ref m_CustomDataMaterialTexture, ref m_Settings);
            m_EdgeDetectionPass.Setup(ref m_EdgeDetectionMaterial, ref m_Settings);
            m_EdgeBlendPass.Setup(ref m_EdgeBlendMaterial, ref m_Settings);

            if (m_Settings._UseDepthMask) renderer.EnqueuePass(m_DepthMaskPass);
            if (m_Settings._UseCustomData) renderer.EnqueuePass(m_CustomDataPass);
            renderer.EnqueuePass(m_EdgeDetectionPass);
            renderer.EnqueuePass(m_BlitTemporaryCameraRenderPass);
            renderer.EnqueuePass(m_EdgeBlendPass);
        }

        protected override void Dispose(bool disposing)
        {
            if (m_CustomDataPass != null) m_CustomDataPass.Dispose();
            m_CustomDataPass = null;

            if (m_DepthMaskPass != null) m_DepthMaskPass.Dispose();
            m_DepthMaskPass = null;

            // [Update 2.1] memory leak fix
            CoreUtils.Destroy(m_EdgeDetectionMaterial);
            CoreUtils.Destroy(m_EdgeBlendMaterial);
            CoreUtils.Destroy(m_DepthMaskMaterial);
            CoreUtils.Destroy(m_CustomDataMaterial);
            CoreUtils.Destroy(m_CustomDataMaterialTexture);

            // old
            /*
#if UNITY_EDITOR
            if (EditorApplication.isPlaying)
            {
                if (m_EdgeDetectionMaterial) Destroy(m_EdgeDetectionMaterial);
                if (m_EdgeBlendMaterial) Destroy(m_EdgeBlendMaterial);
                if (m_DepthMaskMaterial) Destroy(m_DepthMaskMaterial);
                if (m_CustomDataMaterial) Destroy(m_CustomDataMaterial); 
                if (m_CustomDataMaterialTexture) Destroy(m_CustomDataMaterialTexture); 
            }
            else
            {
                if (m_EdgeDetectionMaterial) DestroyImmediate(m_EdgeDetectionMaterial);
                if (m_EdgeBlendMaterial) DestroyImmediate(m_EdgeBlendMaterial);
                if (m_DepthMaskMaterial) DestroyImmediate(m_DepthMaskMaterial);
                if (m_CustomDataMaterial) DestroyImmediate(m_CustomDataMaterial);
                if (m_CustomDataMaterialTexture) DestroyImmediate(m_CustomDataMaterialTexture);
            }
#else
                if(m_EdgeDetectionMaterial)Destroy(m_EdgeDetectionMaterial);
                if(m_EdgeBlendMaterial)Destroy(m_EdgeBlendMaterial);
                if(m_DepthMaskMaterial)Destroy(m_DepthMaskMaterial);
                if(m_CustomDataMaterial)Destroy(m_CustomDataMaterial);
                if(m_CustomDataMaterialTexture)Destroy(m_CustomDataMaterialTexture);
#endif
            */
        }


    }

    public class EdgeDetectionPassData : ContextItem, IDisposable
    {
        private static readonly int kDepthMaskTexture = Shader.PropertyToID("_DepthMaskRT");
        private static readonly int kCustomDataTexture = Shader.PropertyToID("_CustomDataRT");

        private static readonly int kEdgesTexturePropertyId = Shader.PropertyToID("_EdgesTexture");

        private static readonly int kBlitTexturePropertyId = Shader.PropertyToID("_BlitTexture");
        private static readonly int kBlitScaleBiasPropertyId = Shader.PropertyToID("_BlitScaleBias");

        private static MaterialPropertyBlock s_SharedPropertyBlock = new MaterialPropertyBlock();

        RTHandle m_EdgesTexture;
        TextureHandle m_EdgesTextureHandle;

        RTHandle m_TemporaryTexture;
        TextureHandle m_TemporaryTextureHandle;

        public void Init(RenderGraph renderGraph, RenderTextureDescriptor targetDescriptor, string textureName = null)
        {
            var texName = String.IsNullOrEmpty(textureName) ? "_EdgesTexture" : textureName;
            RenderingUtils.ReAllocateHandleIfNeeded(ref m_EdgesTexture, targetDescriptor, FilterMode.Point, TextureWrapMode.Clamp, name: texName);
            m_EdgesTextureHandle = renderGraph.ImportTexture(m_EdgesTexture);

            RenderingUtils.ReAllocateHandleIfNeeded(ref m_TemporaryTexture, targetDescriptor, FilterMode.Bilinear, TextureWrapMode.Clamp, name: "_TemporaryTexture");
            m_TemporaryTextureHandle = renderGraph.ImportTexture(m_TemporaryTexture);
        }

        public override void Reset()
        {
            m_EdgesTextureHandle = TextureHandle.nullHandle;
            m_TemporaryTextureHandle = TextureHandle.nullHandle;
        }

        class PassData
        {
            public TextureHandle edgesTexture;

            public TextureHandle source;
            public TextureHandle destination;

            public Material edgeDetectionMaterial;
            public Material edgeBlendMaterial;

            public bool UseMask;
            public TextureHandle depthMask;

            public bool UseCustomData;
            public TextureHandle customData;
        }

        public void RecordBlitColor(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>("BlitColorPass", out var passData))
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                passData.source = resourceData.activeColorTexture;
                passData.destination = m_TemporaryTextureHandle;

                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.destination, 0);

                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) => ExecutePass(passData, rgContext));
            }
        }

        static void ExecutePass(PassData data, RasterGraphContext rgContext)
        {
            Blitter.BlitTexture(rgContext.cmd, data.source, new Vector4(1f, 1f, 0f, 0f), 0, false);
        }

        public void RecordEdgeDetectionPass(RenderGraph renderGraph, ContextContainer frameData, string passName, Material material, EdgeDetectionSettings settings)
        {
            var cameraData = frameData.Get<UniversalCameraData>();
            var descriptor = cameraData.cameraTargetDescriptor;

            if (!m_EdgesTextureHandle.IsValid())
            {
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                Init(renderGraph, descriptor,"_EdgesTexture");
            }

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                passData.UseMask = settings._UseDepthMask;
                passData.UseCustomData = settings._UseCustomData;

                if (frameData.Contains<AdvancedEdgeDetection.TextureRefData>())
                {
                    var texRef = frameData.Get<AdvancedEdgeDetection.TextureRefData>();

                    if(passData.UseMask)
                    {
                        passData.depthMask = texRef.depthMaskTexture;
                        builder.UseTexture(passData.depthMask);
                    }

                    if (passData.UseCustomData)
                    {
                        passData.customData = texRef.customDataTexture;
                        builder.UseTexture(passData.customData);
                    }
                }

                material.SetMatrix("_InverseView", cameraData.camera.cameraToWorldMatrix);

                passData.edgeDetectionMaterial = material;

                passData.source = resourceData.activeColorTexture;
                passData.destination = m_EdgesTextureHandle;

                builder.UseTexture(passData.source);
                builder.SetRenderAttachment(passData.destination, 0);

                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) => ExecuteEdgeDetectionPass(passData, rgContext));
            }
        }

        private static void ExecuteEdgeDetectionPass(PassData data, RasterGraphContext context)
        {
            s_SharedPropertyBlock.Clear();
            if (data.source.IsValid()) s_SharedPropertyBlock.SetTexture(kBlitTexturePropertyId, data.source);

            if (data.UseMask)
            {
                if (data.depthMask.IsValid()) s_SharedPropertyBlock.SetTexture(kDepthMaskTexture, data.depthMask);
            }

            if (data.UseCustomData)
            {
                if (data.customData.IsValid()) s_SharedPropertyBlock.SetTexture(kCustomDataTexture, data.customData);
            }

            s_SharedPropertyBlock.SetVector(kBlitScaleBiasPropertyId, new Vector4(1, 1, 0, 0));

            context.cmd.DrawProcedural(Matrix4x4.identity, data.edgeDetectionMaterial, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
        }

        public void RecordBlendPass(RenderGraph renderGraph, ContextContainer frameData, string passName, Material material,EdgeDetectionSettings settings)
        {

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                var resourceData = frameData.Get<UniversalResourceData>();

                passData.UseMask = settings._UseDepthMask;

                if (frameData.Contains<AdvancedEdgeDetection.TextureRefData>())
                {
                    var texRef = frameData.Get<AdvancedEdgeDetection.TextureRefData>();

                    if (passData.UseMask)
                    {
                        passData.depthMask = texRef.depthMaskTexture;
                        builder.UseTexture(passData.depthMask);
                    }
                }

                passData.edgeBlendMaterial = material;

                passData.source = m_TemporaryTextureHandle;
                passData.edgesTexture = m_EdgesTextureHandle;
                passData.destination = resourceData.activeColorTexture;

                builder.UseTexture(passData.source);
                builder.UseTexture(passData.edgesTexture);

                //[Update 2.1] Adding edge RT read in shader graphs
                int globalTextureID = Shader.PropertyToID("_EdgeRT");
                builder.SetGlobalTextureAfterPass(passData.edgesTexture, globalTextureID);

                builder.SetRenderAttachment(passData.destination, 0);

                builder.SetRenderFunc((PassData passData, RasterGraphContext rgContext) => ExecuteEdgeBlendPass(passData, rgContext));
            }
        }

        private static void ExecuteEdgeBlendPass(PassData data, RasterGraphContext context)
        {
            s_SharedPropertyBlock.Clear();

            if (data.source.IsValid())
            {
                s_SharedPropertyBlock.SetTexture(kBlitTexturePropertyId, data.source);
            }

            if (data.edgesTexture.IsValid())
            {
                s_SharedPropertyBlock.SetTexture(kEdgesTexturePropertyId, data.edgesTexture);
            }

            if (data.UseMask)
            {
                if (data.depthMask.IsValid()) s_SharedPropertyBlock.SetTexture(kDepthMaskTexture, data.depthMask);
            }

            s_SharedPropertyBlock.SetVector(kBlitScaleBiasPropertyId, new Vector4(1, 1, 0, 0));

            context.cmd.DrawProcedural(Matrix4x4.identity, data.edgeBlendMaterial, 0, MeshTopology.Triangles, 3, 1, s_SharedPropertyBlock);
        }

        public void Dispose()
        {
            m_EdgesTexture?.Release();
            m_TemporaryTexture?.Release();
        }
        
    }

    class EdgeDetectionPass : ScriptableRenderPass
    {
        private Material material;
        private EdgeDetectionSettings m_Settings;

        private void SetEdgeDetectionProperties()
        {
            // Main
            material.SetFloat("_Thickness", m_Settings._Thickness);
            material.SetInt("_ResolutionAdjust", m_Settings._ResolutionAdjust ? 1 : 0);

            // Depth Fade
            if (m_Settings._UseDepthFade)
            {
                material.EnableKeyword("_USEDEPTHFADE");
                material.SetFloat("_FadeStart", m_Settings._FadeStart);
                material.SetFloat("_FadeEnd", m_Settings._FadeEnd);
            }
            else
            {
                material.DisableKeyword("_USEDEPTHFADE_ON");
            }

            // Normals
            if (m_Settings._NormalsEdgeDetection)
            {
                material.EnableKeyword("_NORMALS_EDGES");
                material.SetFloat("_NormalsOffset", m_Settings._NormalsOffset);
                material.SetFloat("_NormalsHardness", m_Settings._NormalsHardness);
                material.SetFloat("_NormalsPower", m_Settings._NormalsPower);
            }
            else
            {
                material.DisableKeyword("_NORMALS_EDGES");
            }

            // Depth
            if (m_Settings._DepthEdgeDetection)
            {
                material.EnableKeyword("_DEPTH_EDGES");
                material.SetFloat("_ViewDirThreshold", m_Settings._ViewDirThreshold);
                material.SetFloat("_ViewDirThresholdScale", m_Settings._ViewDirThresholdScale);
                material.SetFloat("_DepthThreshold", m_Settings._DepthThreshold);
                material.SetFloat("_DepthHardness", m_Settings._DepthHardness);
                material.SetFloat("_DepthPower", m_Settings._DepthPower);
            }
            else
            {
                material.DisableKeyword("_DEPTH_EDGES");
            }

            if (m_Settings._AcuteAngleFix)
            {
                material.EnableKeyword("_ACUTE_ANGLES_FIX");
            }
            else
            {
                material.DisableKeyword("_ACUTE_ANGLES_FIX");
            }

            // Custom Data
            if (m_Settings._UseCustomData)
            {
                material.EnableKeyword("_CUSTOM_DATA_EDGES");
            }
            else
            {
                material.DisableKeyword("_CUSTOM_DATA_EDGES");
            }
        }

        public void Setup(ref Material edgeDetectionMaterial, ref EdgeDetectionSettings settings)
        {
            material = edgeDetectionMaterial;
            m_Settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            SetEdgeDetectionProperties();

            var blitTextureData = frameData.Create<EdgeDetectionPassData>();
            blitTextureData.RecordEdgeDetectionPass(renderGraph, frameData, "Edge Detection Pass", material, m_Settings);
        }
    }

    class BlitTemporaryCameraRenderPass : ScriptableRenderPass
    {
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            var blitTextureData = frameData.Get<EdgeDetectionPassData>();
            blitTextureData.RecordBlitColor(renderGraph, frameData);
        }
    }

    class EdgeBlendPass : ScriptableRenderPass
    {
        private Material material;
        private EdgeDetectionSettings m_Settings;

        private void SetEdgeBlendProperties()
        {
            if (m_Settings._UseDepthMask)
            {
                material.EnableKeyword("_USEDEPTHMASK");
            }
            else
            {
                material.DisableKeyword("_USEDEPTHMASK");
            }

            // Colors
            material.SetColor("_EdgeColor", m_Settings._EdgeColor);

            // Sketch
            if (m_Settings._UseSketchEdges)
            {
                material.EnableKeyword("_USESKETCHEDGES");
                material.SetFloat("_Amplitude", m_Settings._Amplitude);
                material.SetFloat("_Frequency", m_Settings._Frequency);
                material.SetFloat("_ChangesPerSecond", m_Settings._ChangesPerSecond);
            }
            else
            {
                material.DisableKeyword("_USESKETCHEDGES");
            }

            if (m_Settings._UseEdgeBlendDepthFade)
            {
                material.EnableKeyword("_USEDEPTHFADE");
                material.SetFloat("_EdgeBlendFadeStart", m_Settings._EdgeBlendFadeStart);
                material.SetFloat("_EdgeBlendFadeEnd", m_Settings._EdgeBlendFadeEnd);
            }
            else
            {
                material.DisableKeyword("_USEDEPTHFADE");
            }

            // Grain
            if (m_Settings._UseGrain)
            {
                material.EnableKeyword("_USEGRAIN");
                material.SetTexture("_GrainTexture", m_Settings._GrainTexture);
                material.SetFloat("_GrainStrength", m_Settings._GrainStrength);
                material.SetFloat("_GrainScale", m_Settings._GrainScale);
            }
            else
            {
                material.DisableKeyword("_USEGRAIN");
            }

            // UV Offset
            if (m_Settings._UseUvOffset)
            {
                material.EnableKeyword("_USEUVOFFSET");
                material.SetTexture("_OffsetNoise", m_Settings._OffsetNoise);
                material.SetFloat("_OffsetNoiseScale", m_Settings._OffsetNoiseScale);
                material.SetFloat("_OffsetChangesPerSecond", m_Settings._OffsetChangesPerSecond);
                material.SetFloat("_OffsetStrength", m_Settings._OffsetStrength);
            }
            else
            {
                material.DisableKeyword("_USEUVOFFSET");
            }
        }

        public void Setup(ref Material edgeDetectionMaterial, ref EdgeDetectionSettings settings)
        {
            material = edgeDetectionMaterial;
            m_Settings = settings;
        }

        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            SetEdgeBlendProperties();

            var blitTextureData = frameData.Get<EdgeDetectionPassData>();
            blitTextureData.RecordBlendPass(renderGraph, frameData, "Edge Blend Pass", material, m_Settings);
        }
    }
}