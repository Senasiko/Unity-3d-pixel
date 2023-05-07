Shader "DeferredPixel/PixelDetection"
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
            SamplerState my_point_clamp_sampler;

            TEXTURE2D_X(_CameraDepthTexture);
            TEXTURE2D_X_HALF(_GBuffer0);
            TEXTURE2D_X_HALF(_GBuffer1);
            TEXTURE2D_X_HALF(_GBuffer2);
            TEXTURE2D_X_HALF(_GBuffer3);


            float SampleDepth(float2 uv)
            {
                return SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, uv, 0);
            }

            uint _PixelSize;
            
            float4 Frag(Varyings input) : SV_Target
            {
                float2 perPixelSize = _ScreenSize.zw;
                float4 col = SAMPLE_TEXTURE2D(_MainTex, my_point_clamp_sampler,
                                              float2(input.texcoord.x - perPixelSize.x, input.texcoord.y));

                float2 screen_uv = input.texcoord;

                float d = SAMPLE_TEXTURE2D_X_LOD(_CameraDepthTexture, my_point_clamp_sampler, screen_uv, 0).x;
                // raw depth value has UNITY_REVERSED_Z applied on most platforms.
                float4 originGbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, screen_uv, 0);
                float4 gbuffer1 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer1, my_point_clamp_sampler, screen_uv, 0);
                float4 gbuffer2 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer2, my_point_clamp_sampler, screen_uv, 0);
                float4 gbuffer3 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer3, my_point_clamp_sampler, screen_uv, 0);

                int2 objectPositionCS = gbuffer3.xyz / gbuffer3.w * _ScreenSize.xy;

                // float2 startUV = floor(i.positionCS / _ScreenSize.xy / _PixelSize * _ScreenSize.xy) * _PixelSize / _ScreenSize.xy;
                int2 startCS = objectPositionCS + int2((input.positionCS - objectPositionCS) / _PixelSize) * _PixelSize;
                float2 startUV = startCS / _ScreenSize.xy;
                float nearstDepth = SampleDepth(startUV);
                float2 nearstUV = startUV;

                uint isPixel = 0;

                for (int i = 0; i < _PixelSize; ++i)
                {
                    for (int j = 0; j < _PixelSize; ++j)
                    {
                        float2 uv = float2(startUV.x + perPixelSize.x * i, startUV.y + perPixelSize.y * j);
                        float4 gbuffer0 = SAMPLE_TEXTURE2D_X_LOD(_GBuffer0, my_point_clamp_sampler, uv, 0);

                        uint materialFlags = UnpackMaterialFlags(gbuffer0.a);

                        if ((materialFlags & kMaterialFlagPixel) != 0)
                        {
                            float depth = SampleDepth(uv);
                            nearstUV = depth < nearstDepth ? uv : nearstUV;
                            nearstDepth = depth < nearstDepth ? depth : nearstDepth;
                        } else
                        {
                            return float4(screen_uv, 0, 1);
                        }
                    }
                }

                // just invert the colors
                return float4(nearstUV, 0, 1);
            }
            ENDHLSL
        }
    }
}
