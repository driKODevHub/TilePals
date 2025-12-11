using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using static INab.AdvancedEdgeDetection.URP.AdvancedEdgeDetection;

namespace INab.AdvancedEdgeDetection.URP
{
    public class CustomDataPass : ScriptableRenderPass
    {
        static List<ShaderTagId> m_ShaderTagIdList = new List<ShaderTagId>
           {
                new ShaderTagId("UniversalForward"),
                new ShaderTagId("UniversalForwardOnly"),
                new ShaderTagId("LightweightForward"),
                new ShaderTagId("SRPDefaultUnlit"),
                new ShaderTagId("DepthOnly"),
                new ShaderTagId("UniversalGBuffer"),
                new ShaderTagId("DepthNormalsOnly"),
                new ShaderTagId("Universal2D"),
           };

        private Material m_Material;
        private Material m_MaterialCustomTexture;
        private EdgeDetectionSettings m_Settings = new EdgeDetectionSettings();

        private class PassData
        {
            public RendererListHandle rendererListHandle;
            public RendererListHandle textureRendererListHandle;
            public TextureHandle destination;
        }

        public CustomDataPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);

        }

        public void Setup(ref Material material,ref Material material2, ref EdgeDetectionSettings settings)
        {
            m_Material = material;
            m_MaterialCustomTexture = material2;
            m_Settings = settings;
        }

        private void InitRendererLists(ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
        {
            UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            var sortFlags = cameraData.defaultOpaqueSortFlags;
            RenderQueueRange renderQueueRange = RenderQueueRange.opaque;

            FilteringSettings filterSettings1 = new FilteringSettings(renderQueueRange, m_Settings._CustomDataLayerMask);

            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, universalRenderingData, cameraData, lightData, sortFlags);
            drawSettings.overrideMaterial = m_Material;
            drawSettings.overrideMaterialPassIndex = 0;

            var param1 = new RendererListParams(universalRenderingData.cullResults, drawSettings, filterSettings1);
            passData.rendererListHandle = renderGraph.CreateRendererList(param1);

            FilteringSettings filterSettings2 = new FilteringSettings(renderQueueRange, m_Settings._CustomTextureLayerMask);

            DrawingSettings drawSettings2 = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, universalRenderingData, cameraData, lightData, sortFlags);
            drawSettings2.overrideMaterial = m_MaterialCustomTexture;
            drawSettings2.overrideMaterialPassIndex = 0;

            var param2 = new RendererListParams(universalRenderingData.cullResults, drawSettings2, filterSettings2);
            passData.textureRendererListHandle = renderGraph.CreateRendererList(param2);
        }

        private void UpdateMaterialProperties()
        {
            if(m_Settings._UseCustomData)
            {
                m_MaterialCustomTexture.SetTexture("_CustomTexture", m_Settings._CustomTexture);
                m_MaterialCustomTexture.SetInt("_UseCustomTexture",1);
            }
            else
            {
                m_MaterialCustomTexture.SetInt("_UseCustomTexture",0);
            }

            m_Material.SetInt("_UseCustomTexture",0);
            
        }

        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            UpdateMaterialProperties();

            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                InitRendererLists(frameData, ref passData, renderGraph);

                //if (!passData.rendererListHandle.IsValid())
                //    return;

                var texRefExist = frameData.Contains<TextureRefData>();
                var texRef = frameData.GetOrCreate<TextureRefData>();

                // Setup the descriptor we use for BlitData. We should use the camera target's descriptor as a start.
                var cameraData = frameData.Get<UniversalCameraData>();
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                descriptor.colorFormat = RenderTextureFormat.ARGBFloat;

                // Create a new temporary texture to keep the blit result.
                passData.destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_CustomDataEdgeDetection", false);
                texRef.customDataTexture = passData.destination;

                builder.UseRendererList(passData.rendererListHandle);
                builder.UseRendererList(passData.textureRendererListHandle);
                builder.SetRenderAttachment(passData.destination, 0);

                // Enable to test things out if the texture output is not used anywhere
                //builder.AllowPassCulling(false);

                builder.SetRenderFunc((PassData data, RasterGraphContext context) => ExecutePass(data, context));

            }
        }

        static void ExecutePass(PassData data, RasterGraphContext context)
        {
            //context.cmd.ClearRenderTarget(RTClearFlags.Color, Color.green, 1, 0);
            context.cmd.DrawRendererList(data.rendererListHandle);
            context.cmd.DrawRendererList(data.textureRendererListHandle);
        }

        public void Dispose()
        {
            // Nothing here
            //m_SSAOTextures[0]?.Release();
            //m_SSAOParamsPrev = default;
        }
    }
}