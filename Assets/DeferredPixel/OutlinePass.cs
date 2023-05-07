using System;
using UnityEngine;
using UnityEngine.Experimental.Rendering;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using System.Reflection;

namespace DeferredPixel
{
    class OutlinePass : ScriptableRenderPass
    {
        
        private struct Textures
        {
            public RTHandle OutlineTexture;
        }
        
        private struct Resources
        {
            public Material OutlinePassMaterial;
        }


        private Textures _textures;
        private Resources _resources;

        public OutlinePass()
        {
            _textures = new Textures();

            _resources = new Resources()
            {
                OutlinePassMaterial = new Material(Shader.Find("DeferredPixel/Outline")),
            };
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
            var outlineDescriptor = cameraTextureDescriptor;
            outlineDescriptor.graphicsFormat = GraphicsFormat.R32_SFloat;
            outlineDescriptor.depthBufferBits = 0;
            outlineDescriptor.stencilFormat = GraphicsFormat.None;
            outlineDescriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _textures.OutlineTexture, outlineDescriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_OutlineTexture");

        }

        // Here you can implement the rendering logic.
        // Use <c>ScriptableRenderContext</c> to issue drawing commands or execute command buffers
        // https://docs.unity3d.com/ScriptReference/Rendering.ScriptableRenderContext.html
        // You don't have to call ScriptableRenderContext.submit, the render pipeline will call it at specific points in the pipeline.
        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                var cmd = CommandBufferPool.Get("OutlinePass");
                cmd.SetGlobalTexture(_textures.OutlineTexture.name, _textures.OutlineTexture.nameID);
                cmd.SetRenderTarget(_textures.OutlineTexture);
                cmd.ClearRenderTarget(true, true, Color.clear);
                cmd.SetGlobalFloat("_Depth_Test_Threshold", 0.1f);
                Blitter.BlitTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, _textures.OutlineTexture, _resources.OutlinePassMaterial, 0);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }


        public void Dispose()
        {
            _textures.OutlineTexture?.Release();
        }
    }

    class ApplyOutlinePass : ScriptableRenderPass
    {
        private struct Textures
        {
            public RTHandle TargetCopyTexture;
        }
        
        private struct Resources
        {
            public Material OutlinePassMaterial;
        }

        private Textures _textures;
        private Resources _resources;

        public ApplyOutlinePass()
        {
            _textures = new Textures();
            _resources = new Resources()
            {
                OutlinePassMaterial = new Material(Shader.Find("DeferredPixel/Outline")),
            };
        }

        public override void Configure(CommandBuffer cmd, RenderTextureDescriptor cameraTextureDescriptor)
        {

            var descriptor = cameraTextureDescriptor;
            descriptor.depthBufferBits = 0;
            descriptor.enableRandomWrite = true;
            RenderingUtils.ReAllocateIfNeeded(ref _textures.TargetCopyTexture,  descriptor, FilterMode.Point, TextureWrapMode.Repeat, false, 1, 0, "_CopyTargetTexture");

        }

        public override void Execute(ScriptableRenderContext context, ref RenderingData renderingData)
        {
            if (renderingData.cameraData.renderType == CameraRenderType.Base)
            {
                var cmd = CommandBufferPool.Get("ApplyOutlinePass");
                cmd.SetGlobalTexture(_textures.TargetCopyTexture.name, _textures.TargetCopyTexture.nameID);
                cmd.Blit(_textures.TargetCopyTexture.name, _textures.TargetCopyTexture);
                Blitter.BlitTexture(cmd, renderingData.cameraData.renderer.cameraColorTargetHandle, renderingData.cameraData.renderer.cameraColorTargetHandle, _resources.OutlinePassMaterial, 1);
                context.ExecuteCommandBuffer(cmd);
                CommandBufferPool.Release(cmd);
            }
        }
        
        public void Dispose()
        {
            _textures.TargetCopyTexture?.Release();
        }
    }
}