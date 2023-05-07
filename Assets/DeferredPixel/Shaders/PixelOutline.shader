Shader "DeferredPixel/Outline"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        // No culling or depth
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "./GBuffer.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }


            TEXTURE2D(_MainTex);
            SAMPLER(my_point_clamp_sampler);

            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer1);
            TEXTURE2D_X_HALF(_GBuffer2);
            TEXTURE2D_X_HALF(_GBuffer3);

            float _Depth_Test_Threshold;
            uint _OutlinePixelSize;

            float SampleDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, uv, 0);
            }

            float GetOutlineFactor(float depth, float id, float2 uv, float2 dither, float2 pixelUVSize)
            {
                float2 neighbourUV = uv + dither * pixelUVSize;
                float4 gbuffer2 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, neighbourUV, 0);
                float neighbourDepth = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, neighbourUV, 0);
                float neighbourId = gbuffer2.a;

                return neighbourId == id ? 1 : 0;
            }

            
            float Frag(Varyings input) : SV_Target
            {
                
                float2 screen_uv = input.texcoord;

                float d = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, screen_uv, 0).x;

                float4 originGbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, screen_uv, 0);
                float4 gbuffer2 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, screen_uv, 0);

                uint originMaterialFlags = UnpackMaterialFlags(originGbuffer0.a);
                if ((originMaterialFlags & kMaterialFlagPixel) == 0)
                {
                    return 0;
                }
                int2 startCS = input.positionCS / _OutlinePixelSize;
                float2 startUV = startCS * _OutlinePixelSize / _ScreenSize.xy;
                float2 pixelUVSize = _OutlinePixelSize * _ScreenSize.zw;
                
                int outCount = 0;
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(0, 1), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(0, -1), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(1, 0), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(1, 1), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(1, -1), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(-1, 0), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(-1, 1), pixelUVSize);
                outCount += GetOutlineFactor(d, gbuffer2.a, startUV, float2(-1, -1), pixelUVSize);

                uint isOutline = outCount > 0 && outCount < 8 ? 1 : 0;
                
                // just invert the colors
                return isOutline;
            }
            ENDHLSL
        }
        
                Pass
        {
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma enable_d3d11_debug_symbols
            #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Common.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/UnityGBuffer.hlsl"
            #include "./GBuffer.hlsl"

            struct Attributes
            {
                uint vertexID : SV_VertexID;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 texcoord : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                float4 pos = GetFullScreenTriangleVertexPosition(input.vertexID);
                float2 uv = GetFullScreenTriangleTexCoord(input.vertexID);
                output.positionCS = pos;
                output.texcoord = uv;
                return output;
            }


            SAMPLER(my_point_clamp_sampler);

            TEXTURE2D_X(_OutlineTexture);
            TEXTURE2D_X(_CopyTargetTexture);

            float4 Frag(Varyings input) : SV_Target
            {
                int isOutline = SAMPLE_TEXTURE2D_X(_OutlineTexture, my_point_clamp_sampler, input.texcoord);
                float4 originColor = SAMPLE_TEXTURE2D_X(_CopyTargetTexture, my_point_clamp_sampler, input.texcoord);
                return isOutline > 0 ? float4(0, 0, 0, 1) : originColor;
            }
            ENDHLSL
        }
    }
}