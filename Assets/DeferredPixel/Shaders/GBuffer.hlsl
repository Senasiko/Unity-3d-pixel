#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"

#define kMaterialFlagPixel 16

    // This will encode SurfaceData into GBuffer
FragmentOutput PixelSurfaceDataToGbuffer(SurfaceData surfaceData, InputData inputData, half3 globalIllumination, half id)
{
    half3 packedNormalWS = PackNormal(inputData.normalWS);

    uint materialFlags = 0;

    // SimpleLit does not use _SPECULARHIGHLIGHTS_OFF to disable specular highlights.

    #ifdef _RECEIVE_SHADOWS_OFF
    materialFlags |= kMaterialFlagReceiveShadowsOff;
    #endif

    #if defined(LIGHTMAP_ON) && defined(_MIXED_LIGHTING_SUBTRACTIVE)
    materialFlags |= kMaterialFlagSubtractiveMixedLighting;
    #endif

    materialFlags |= kMaterialFlagPixel;

    FragmentOutput output;
    output.GBuffer0 = half4(surfaceData.albedo.rgb, PackMaterialFlags(materialFlags));   // albedo          albedo          albedo          materialFlags   (sRGB rendertarget)
    output.GBuffer1 = half4(surfaceData.specular.rgb, surfaceData.occlusion);            // specular        specular        specular        occlusion
    output.GBuffer2 = half4(packedNormalWS, id / 100);                     // encoded-normal  encoded-normal  encoded-normal  smoothness
    output.GBuffer3 = half4(globalIllumination, 0);                                      
    #if _RENDER_PASS_ENABLED
    output.GBuffer4 = inputData.positionCS.z;
    #endif
    #if OUTPUT_SHADOWMASK
    output.GBUFFER_SHADOWMASK = inputData.shadowMask; // will have unity_ProbesOcclusion value if subtractive lighting is used (baked)
    #endif
    #ifdef _WRITE_RENDERING_LAYERS
    uint renderingLayers = GetMeshRenderingLayer();
    output.GBUFFER_LIGHT_LAYERS = float4(EncodeMeshRenderingLayer(renderingLayers), 0.0, 0.0, 0.0);
    #endif

    return output;
}
