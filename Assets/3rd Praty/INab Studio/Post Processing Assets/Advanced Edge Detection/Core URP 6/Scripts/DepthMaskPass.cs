using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Rendering.Universal;
using static INab.AdvancedEdgeDetection.URP.AdvancedEdgeDetection;

namespace INab.AdvancedEdgeDetection.URP
{
    public class DepthMaskPass : ScriptableRenderPass
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
        private EdgeDetectionSettings m_Settings = new EdgeDetectionSettings();

        private class PassData
        {
            public RendererListHandle rendererListHandle;
            public TextureHandle destination;
        }

        public DepthMaskPass(string passName)
        {
            profilingSampler = new ProfilingSampler(passName);

        }

        public void Setup(ref Material material, ref EdgeDetectionSettings settings)
        {
            m_Material = material;
            m_Settings = settings;
        }

        private void InitRendererLists(ContextContainer frameData, ref PassData passData, RenderGraph renderGraph)
        {
            UniversalRenderingData universalRenderingData = frameData.Get<UniversalRenderingData>();
            UniversalCameraData cameraData = frameData.Get<UniversalCameraData>();
            UniversalLightData lightData = frameData.Get<UniversalLightData>();

            var sortFlags = cameraData.defaultOpaqueSortFlags;
            RenderQueueRange renderQueueRange = RenderQueueRange.all;
            FilteringSettings filterSettings = new FilteringSettings(renderQueueRange, m_Settings._DepthMaskLayerMask);

            DrawingSettings drawSettings = RenderingUtils.CreateDrawingSettings(m_ShaderTagIdList, universalRenderingData, cameraData, lightData, sortFlags);
            drawSettings.overrideMaterial = m_Material;
            drawSettings.overrideMaterialPassIndex = 0;
            //drawSettings.enableDynamicBatching = true;

            var param = new RendererListParams(universalRenderingData.cullResults, drawSettings, filterSettings);
            passData.rendererListHandle = renderGraph.CreateRendererList(param);
        }


        // Each ScriptableRenderPass can use the RenderGraph handle to add multiple render passes to the render graph
        public override void RecordRenderGraph(RenderGraph renderGraph, ContextContainer frameData)
        {
            using (var builder = renderGraph.AddRasterRenderPass<PassData>(passName, out var passData))
            {
                UniversalResourceData resourceData = frameData.Get<UniversalResourceData>();

                InitRendererLists(frameData, ref passData, renderGraph);

                if (!passData.rendererListHandle.IsValid())
                    return;

                var texRefExist = frameData.Contains<TextureRefData>();
                var texRef = frameData.GetOrCreate<TextureRefData>();


                // Setup the descriptor we use for BlitData. We should use the camera target's descriptor as a start.
                var cameraData = frameData.Get<UniversalCameraData>();
                var descriptor = cameraData.cameraTargetDescriptor;
                descriptor.msaaSamples = 1;
                descriptor.depthBufferBits = 0;
                descriptor.colorFormat = RenderTextureFormat.RFloat;

                // Create a new temporary texture to keep the blit result.
                passData.destination = UniversalRenderer.CreateRenderGraphTexture(renderGraph, descriptor, "_DepthMaskEdgeDetection", true);
                texRef.depthMaskTexture = passData.destination;

                builder.UseRendererList(passData.rendererListHandle);
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
        }

        public void Dispose()
        {
            // Nothing here
            //m_SSAOTextures[0]?.Release();
            //m_SSAOParamsPrev = default;
        }
    }
}