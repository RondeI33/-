Shader "Custom/VHSEffect"
{
    Properties
    {
        _Intensity ("Intensity", Range(0, 1)) = 1
        _ScanlineIntensity ("Scanline Intensity", Range(0, 1)) = 0.3
        _NoiseIntensity ("Noise Intensity", Range(0, 1)) = 0.15
        _ColorBleed ("Color Bleed", Range(0, 0.01)) = 0.003
        _Distortion ("Distortion", Range(0, 0.05)) = 0.01
        _WobbleSpeed ("Wobble Speed", Range(0, 10)) = 3
        _ScanlineCount ("Scanline Count", Float) = 400
        _VignetteIntensity ("Vignette Intensity", Range(0, 1)) = 0.4
        _Saturation ("Saturation", Range(0, 2)) = 0.8
    }

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off

        Pass
        {
            Name "VHSPass"

            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

            float _Intensity;
            float _ScanlineIntensity;
            float _NoiseIntensity;
            float _ColorBleed;
            float _Distortion;
            float _WobbleSpeed;
            float _ScanlineCount;
            float _VignetteIntensity;
            float _Saturation;
            float _UnscaledTime;
            float Hash(float2 p)
            {
                float3 p3 = frac(float3(p.xyx) * 0.1031);
                p3 += dot(p3, p3.yzx + 33.33);
                return frac((p3.x + p3.y) * p3.z);
            }

            float4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord.xy;

                float time = _UnscaledTime;

                float wobble = sin(uv.y * 50.0 + time * _WobbleSpeed) * _Distortion;
                wobble += sin(uv.y * 130.0 + time * 1.7) * _Distortion * 0.3;
                float2 distortedUV = float2(uv.x + wobble, uv.y);

                float r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, distortedUV + float2(_ColorBleed, 0), _BlitMipLevel).r;
                float g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, distortedUV, _BlitMipLevel).g;
                float b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, distortedUV - float2(_ColorBleed, 0), _BlitMipLevel).b;
                float4 color = float4(r, g, b, 1.0);

                float scanline = sin(uv.y * _ScanlineCount * 3.14159) * 0.5 + 0.5;
                scanline = lerp(1.0, scanline, _ScanlineIntensity);
                color.rgb *= scanline;

                float noise = Hash(uv * _ScreenParams.xy + time * 1000.0);
                color.rgb += (noise - 0.5) * _NoiseIntensity;

                float lum = dot(color.rgb, float3(0.299, 0.587, 0.114));
                color.rgb = lerp(float3(lum, lum, lum), color.rgb, _Saturation);

                float2 vig = uv - 0.5;
                float vignette = 1.0 - dot(vig, vig) * _VignetteIntensity * 2.0;
                color.rgb *= saturate(vignette);

                float4 original = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                color = lerp(original, color, _Intensity);

                return color;
            }
            ENDHLSL
        }
    }
}
