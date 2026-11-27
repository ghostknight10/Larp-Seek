Shader "Hidden/LarpSeek/RetroScreen"
{
    SubShader
    {
        Tags { "RenderType" = "Opaque" "RenderPipeline" = "UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always

        Pass
        {
            Name "RetroDither"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float4 _ColorLevels;      // per-channel levels, e.g. (32, 64, 32) = R5G6B5
            float _DitherStrength;
            float _ScanlineIntensity;

            static const float kBayer4[16] =
            {
                 0.0,  8.0,  2.0, 10.0,
                12.0,  4.0, 14.0,  6.0,
                 3.0, 11.0,  1.0,  9.0,
                15.0,  7.0, 13.0,  5.0
            };

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                half4 col = SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_PointClamp, input.texcoord);

                uint2 p = (uint2)input.positionCS.xy;
                float threshold = (kBayer4[(p.y & 3u) * 4u + (p.x & 3u)] + 0.5) / 16.0 - 0.5;

                float3 rgb = saturate(col.rgb);
                #if !UNITY_COLORSPACE_GAMMA
                rgb = LinearToSRGB(rgb);
                #endif

                float3 steps = max(_ColorLevels.rgb - 1.0, 1.0);
                rgb = floor(rgb * steps + 0.5 + threshold * _DitherStrength) / steps;

                if ((p.y & 1u) == 1u)
                    rgb *= 1.0 - _ScanlineIntensity;

                rgb = saturate(rgb);
                #if !UNITY_COLORSPACE_GAMMA
                rgb = SRGBToLinear(rgb);
                #endif

                return half4(rgb, col.a);
            }
            ENDHLSL
        }
    }
    Fallback Off
}
