using System;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace DeferredPixel
{
    
public class PixelDetectFeature : ScriptableRendererFeature
{
    class PixelDetectPass : ScriptableRenderPass
    {
        public int pixelSize = 10;
        private RTHandle _pixelTexture;
        private static readonly ProfilingSampler PixelDetectInfo = new ProfilingSampler("PixelDetect");

        private struct DeferredTextures
        {
            public static int BaseColor = Shader.PropertyToID("_GBuffer0");
            public static int Depth = Shader.PropertyToID("_CameraDepthAttachment");
        }
        
        private struct Resources
        {
            public static Shader PixelDetectionShader;
            public static Material PixelDetectionMaterial;
        }

        public PixelDetectPass()
        {
            Resources.PixelDetectionShader = Shader.Find("DeferredPixel/PixelDetection");
            if (Resources.PixelDetectionShader == null)
            {
                Debug.Log("PixelDetection error");
                return;
            }
            Resources.PixelDetectionMaterial = new Material(Resources.PixelDetectionShader);
            
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
            var pixelTextureDescriptor = cameraTextureDescriptor;
            pixelTextureDescriptor.colorFormat = RenderTextureFormat.ARGB32;
            pixelTextureDescriptor.graphicsFormat = UnityEngine.Experimental.Rendering.GraphicsFormat.R32G32B32A32_SFloat;
            pixelTextureDescriptor.depthBufferBits = 0;
            RenderingUtils.ReAllocateIfNeeded(ref _pixelTexture, pixelTextureDescriptor, FilterMode.Point, TextureWrapMode.Repeat, false,1, 0, "_PixelTexture");
        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            var cmd = CommandBufferPool.Get();
            using (new ProfilingScope(cmd, PixelDetectInfo))
            {
                cmd.SetRenderTarget(_pixelTexture);
                cmd.ClearRenderTarget(true, true, Color.clear);
                cmd.SetGlobalInt("_PixelSize", pixelSize);
                cmd.Blit(DeferredTextures.BaseColor, _pixelTexture, Resources.PixelDetectionMaterial);
                cmd.SetGlobalTexture(_pixelTexture.name, _pixelTexture.nameID);
            }   
            context.ExecuteCommandBuffer(cmd);
            CommandBufferPool.Release(cmd);
        }

        
        public void Dispose()
        {
            _pixelTexture?.Release();
        }
    }

    private PreDetectionPass _preDetectPass;
    private PixelDetectPass _detectPass;
    private ApplyPixelDetectPass _applyPass;
    private OutlinePass _outlinePass;
    private ApplyOutlinePass _applyOutlinePass;
    
    /// <inheritdoc/>
    public override void Create()
    {
        _preDetectPass = new PreDetectionPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingGbuffer,
            PixelSize = pixelSize,
            OutlinePixelSize = outlinePixelSize,
        };
        _detectPass = new PixelDetectPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights,
            pixelSize = pixelSize
        };

        _applyPass = new ApplyPixelDetectPass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights,
        };
        _applyPass._resources.ApplyShader = applyPassResources.ApplyShader;

        _outlinePass = new OutlinePass()
        {
            renderPassEvent = RenderPassEvent.BeforeRenderingDeferredLights,
        };

        _applyOutlinePass = new ApplyOutlinePass()
        {
            renderPassEvent = RenderPassEvent.AfterRenderingDeferredLights,
        };
    }

    // Here you can inject one or multiple render passes in the renderer.
    // This method is called when setting up the renderer once per-camera.
    public override void AddRenderPasses(ScriptableRenderer renderer, ref RenderingData renderingData)
    {
        // _applyPass.Setup(renderer);
        renderer.EnqueuePass(_preDetectPass);
        renderer.EnqueuePass(_detectPass);
        // renderer.EnqueuePass(_applyPass);
        renderer.EnqueuePass(_outlinePass);
        renderer.EnqueuePass(_applyOutlinePass);
    }

    protected override void Dispose(bool disposing)
    {
        _preDetectPass.Dispose();
        _detectPass.Dispose();
        _applyPass.Dispose();
        _outlinePass.Dispose();
        _applyOutlinePass.Dispose();
    }
    
    [Serializable]
    public struct ApplyPassResources
    {
        [SerializeField]
        public ComputeShader ApplyShader;
    }
    
    public ApplyPassResources applyPassResources = new ApplyPassResources();
    public int pixelSize = 10;
    public int outlinePixelSize = 10;

}


}
