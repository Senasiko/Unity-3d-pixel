using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeferredPixel
{
    public class PreDetectionPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler PixelPreDetectInfo = new ProfilingSampler("PixelPreDetection");
        private static readonly ShaderTagId PixelPreDetectionShaderTagID = new ShaderTagId("PixelPreDetection");

        private RTHandle _pixelMaskTexture;
        public int PixelSize = 10; 
        public int OutlinePixelSize = 10; 

        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                var depthDescriptor = renderingData.cameraData.cameraTargetDescriptor;
                depthDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R16_UNorm;
                depthDescriptor.depthBufferBits = 0;
                depthDescriptor.stencilFormat = GraphicsFormat.None;
                depthDescriptor.enableRandomWrite = true;
                RenderingUtils.ReAllocateIfNeeded(ref _pixelMaskTexture, depthDescriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_PixelMaskTexture");

                ConfigureTarget(_pixelMaskTexture);
                ConfigureClear(ClearFlag.All, Color.clear);
            }
        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get("PixelPreDetection");

            using (new ProfilingScope(cmd, PixelPreDetectInfo))
            {
                cmd.SetGlobalFloat("_PixelSize", PixelSize);
                cmd.SetGlobalFloat("_OutlinePixelSize", OutlinePixelSize);
                cmd.SetGlobalTexture("_PixelMaskTexture", _pixelMaskTexture);
                
                var sort = new SortingSettings(renderingData.cameraData.camera);
                var drawingSettings = new DrawingSettings(PixelPreDetectionShaderTagID, sort);
                var filteringSettings = new FilteringSettings(RenderQueueRange.all);
                context.DrawRenderers(renderingData.cullResults, ref drawingSettings, ref filteringSettings);

            }
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        public void Dispose()
        {
            _pixelMaskTexture?.Release();
        }
    }
}