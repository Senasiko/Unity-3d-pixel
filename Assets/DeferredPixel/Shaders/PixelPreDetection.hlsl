#include "Packages/com.unity.render-pipelines.universal/Shaders/SimpleLitInput.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

struct Attributes
{
    float4 objectPositionCS : TEXCOORD1;
    float4 position                         : POSITION;
    float2 texcoord                         : TEXCOORD0;
    UNITY_VERTEX_INPUT_INSTANCE_ID
};

struct Varyings
{
    nointerpolation float4 objectPositionCS : TEXCOORD1;
    float2 uv                               : TEXCOORD0;
    float4 positionCS                       : SV_POSITION;
    UNITY_VERTEX_INPUT_INSTANCE_ID
    UNITY_VERTEX_OUTPUT_STEREO
};

struct PixelPreFragmentOutput
{
    uint pixelMask : SV_Target;
};

uint _OutlinePixelSize;
uint _ID;

TEXTURE2D_X(_CameraDepthTexture);
SamplerState my_point_clamp_sampler;

Varyings PixelPreVertex(Attributes input)
{
    Varyings output = (Varyings)0;
    UNITY_SETUP_INSTANCE_ID(input);
    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

    float4x4 objectToWorldMatrix = GetObjectToWorldMatrix();
    float3 objectPositionWS = objectToWorldMatrix._m03_m13_m23;
    float4 objectPositionCS = TransformWorldToHClip(objectPositionWS);

    output.uv = TRANSFORM_TEX(input.texcoord, _BaseMap);
    output.positionCS = TransformObjectToHClip(input.position.xyz);
    output.objectPositionCS = objectPositionCS;
    return output;
}


float SampleDepth(float2 uv)
{
    return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, uv, 0);
}

float PixelPreFrag(Varyings input) : SV_Target
{
    // float2 perPixelSize = _ScreenSize.zw;
    // int2 startCS = input.objectPositionCS + int2((input.positionCS - input.objectPositionCS).xy / _PixelSize) * _PixelSize;
    // float2 startUV = startCS / _ScreenSize.xy;
    //
    //
    // for (int i = 0; i < _PixelSize; ++i)
    // {
    //     for (int j = 0; j < _PixelSize; ++j)
    //     {
    //         float2 uv = float2(startUV.x + perPixelSize.x * i, startUV.y + perPixelSize.y * j);
    //         float depth = SampleDepth(uv);
    //         if (depth != input.positionCS.z)
    //         {
    //             return 1;
    //         }
    //     }
    // }
    return float(_ID) / 1000;
}