using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

namespace DeferredPixel
{
    class ApplyPixelDetectPass : ScriptableRenderPass
    {
        private static readonly ProfilingSampler ApplyPixelDetectInfo = new ProfilingSampler("ApplyPixelDetect");

        private class CopyTextures
        {
            public RTHandle CopyDepthTexture;
            public RTHandle CopyGBuffer0;
            public RTHandle CopyGBuffer1;
            public RTHandle CopyGBuffer2;
            public RTHandle CopyGBuffer3;

            public void Dispose()
            {
                CopyDepthTexture?.Release();
                CopyGBuffer0?.Release();
                CopyGBuffer1?.Release();
                CopyGBuffer2?.Release();
                CopyGBuffer3?.Release();
            }
        }

        [Serializable]
        public class Resources
        {
            public ComputeShader ApplyShader;
        }

        private CopyTextures _copyTextures;

        public Resources _resources;


        public ApplyPixelDetectPass()
        {
            _copyTextures = new CopyTextures();
            _resources = new Resources();
        }
        
        private struct DeferredTextures
        {
            public RTHandle Depth;
            public RTHandle GBuffer0;
            public RTHandle GBuffer1;
            public RTHandle GBuffer2;
        }

        private DeferredTextures? _deferredTextures = null;

        private DeferredTextures? GetGBufferDeferredTextures(ScriptableRenderer renderer)
        {
            FieldInfo lightsInfo = typeof(UniversalRenderer).GetField("m_DeferredLights", BindingFlags.NonPublic | BindingFlags.Instance);
            var lights = lightsInfo?.GetValue((UniversalRenderer)renderer);
            Type lightType = lights?.GetType();
            PropertyInfo attachmentInfo = lightType?.GetProperty("GbufferAttachments", BindingFlags.NonPublic | BindingFlags.Instance);
            var attachments = (RTHandle[])attachmentInfo?.GetValue(lights);
            PropertyInfo depthInfo = lightType?.GetProperty("DepthCopyTexture", BindingFlags.NonPublic | BindingFlags.Instance);
            var depth = (RTHandle)depthInfo?.GetValue(lights);
            if (attachments != null && depth != null && depth.rt != null && attachments[0].rt != null)
            {
                return new DeferredTextures()
                {
                    Depth = depth,
                    GBuffer0 = attachments[0],
                    GBuffer1 = attachments[1],
                    GBuffer2 = attachments[2],
                };   
            }
            return null;
        }

        private bool IsDeferredTexturesValid()
        {
            return _deferredTextures != null && _deferredTextures.Value.Depth.rt != null && _deferredTextures.Value.GBuffer0.rt != null;
        }

        public void Setup(ScriptableRenderer renderer)
        {
            _deferredTextures = GetGBufferDeferredTextures(renderer);
        }

        // This method is called before executing the render pass.
        // It can be used to configure render targets and their clear state. Also to create temporary render target textures.
        // When empty this render pass will render to the active camera render target.
        // You should never call CommandBuffer.SetRenderTarget. Instead call <c>ConfigureTarget</c> and <c>ConfigureClear</c>.
        // The render pipeline will ensure target setup and clearing happens in a performant manner.
        public override void OnCameraSetup(CommandBuffer cmd, ref RenderingData renderingData)
        {
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {
            var depthDescriptor = cameraTextureDescriptor;
            depthDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32_SFloat;
            depthDescriptor.depthBufferBits = 0;
            depthDescriptor.stencilFormat = GraphicsFormat.None;
            depthDescriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _copyTextures.CopyDepthTexture, depthDescriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyDepthTexture");

            var gBuffer0Descriptor = cameraTextureDescriptor;
            gBuffer0Descriptor.graphicsFormat = GraphicsFormat.R8G8B8A8_SRGB;
            gBuffer0Descriptor.depthBufferBits = 0;
            gBuffer0Descriptor.stencilFormat = GraphicsFormat.None;
            gBuffer0Descriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _copyTextures.CopyGBuffer0,  gBuffer0Descriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyGBuffer0");

            var gBuffer1Descriptor = cameraTextureDescriptor;
            gBuffer1Descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_UNorm;
            gBuffer1Descriptor.depthBufferBits = 0;
            gBuffer1Descriptor.stencilFormat = GraphicsFormat.None;
            gBuffer1Descriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _copyTextures.CopyGBuffer1,  gBuffer1Descriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyGBuffer1");

            var gBuffer2Descriptor = cameraTextureDescriptor;
            gBuffer2Descriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R8G8B8A8_SNorm;
            gBuffer2Descriptor.depthBufferBits = 0;
            gBuffer2Descriptor.stencilFormat = GraphicsFormat.None;
            gBuffer2Descriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _copyTextures.CopyGBuffer2,  gBuffer2Descriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyGBuffer2");
            
            var gBuffer3Descriptor = cameraTextureDescriptor;
            gBuffer3Descriptor.depthBufferBits = 0;
            gBuffer3Descriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _copyTextures.CopyGBuffer3,  gBuffer3Descriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyGBuffer3");
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            _deferredTextures = GetGBufferDeferredTextures(renderingData.cameraData.renderer);
            if (IsDeferredTexturesValid() && renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                var cmd = CommandBufferPool.Get();
                using (new ProfilingScope(cmd, ApplyPixelDetectInfo))
                {
                    cmd.SetGlobalTexture(_copyTextures.CopyDepthTexture.name, _copyTextures.CopyDepthTexture.nameID);
                    cmd.SetGlobalTexture(_copyTextures.CopyGBuffer0.name, _copyTextures.CopyGBuffer0.nameID);
                    cmd.SetGlobalTexture(_copyTextures.CopyGBuffer1.name, _copyTextures.CopyGBuffer1.nameID);
                    cmd.SetGlobalTexture(_copyTextures.CopyGBuffer2.name, _copyTextures.CopyGBuffer2.nameID);
                    cmd.SetGlobalTexture(_copyTextures.CopyGBuffer3.name, _copyTextures.CopyGBuffer3.nameID);

                    if (_resources.ApplyShader != null)
                    {
                        uint threadSizeX = 0;
                        uint threadSizeY = 0;
                        uint threadSizeZ = 0;
                        int kernelIndex = _resources.ApplyShader.FindKernel("ApplyMain");
                        _resources.ApplyShader.GetKernelThreadGroupSizes(kernelIndex, out threadSizeX, out threadSizeY, out threadSizeZ);


                        int groupSizeX = (int)Mathf.Ceil((float)renderingData.cameraData.cameraTargetDescriptor.width / threadSizeX);
                        int groupSizeY = (int)Mathf.Ceil((float)renderingData.cameraData.cameraTargetDescriptor.height / threadSizeY);
                        cmd.DispatchCompute(_resources.ApplyShader, kernelIndex, groupSizeX, groupSizeY, 1);
                        // cmd.SetRenderTarget(DeferredTextures.Depth);
                        // cmd.SetGlobalTexture(Shader.PropertyToID("_BlitTexture"), _copyTextures.CopyDepthTexture);
                        // CoreUtils.DrawFullScreen(cmd, copyMaterial);
                        cmd.Blit(_copyTextures.CopyDepthTexture, _deferredTextures.Value.Depth);
                        cmd.CopyTexture(_copyTextures.CopyGBuffer0, _deferredTextures.Value.GBuffer0);
                        cmd.CopyTexture(_copyTextures.CopyGBuffer1, _deferredTextures.Value.GBuffer1);
                        cmd.CopyTexture(_copyTextures.CopyGBuffer2, _deferredTextures.Value.GBuffer2);
                        cmd.CopyTexture(_copyTextures.CopyGBuffer3, renderingData.cameraData.renderer.cameraColorTargetHandle);
                    }
                }

                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }


        public void Dispose()
        {
            _copyTextures?.Dispose();
        }
    }
}