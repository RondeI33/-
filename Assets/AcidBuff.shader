Shader "Hidden/AcidBuff"
{
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off
        Pass
        {
            Name "AcidBuffPass"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            float _AcidIntensity;
            float3 RGBtoHSV(float3 c)
            {
                float4 K = float4(0.0, -1.0/3.0, 2.0/3.0, -1.0);
                float4 p = lerp(float4(c.bg, K.wz), float4(c.gb, K.xy), step(c.b, c.g));
                float4 q = lerp(float4(p.xyw, c.r), float4(c.r, p.yzx), step(p.x, c.r));
                float d = q.x - min(q.w, q.y);
                float e = 1.0e-10;
                return float3(abs(q.z + (q.w - q.y) / (6.0 * d + e)), d / (q.x + e), q.x);
            }
            float3 HSVtoRGB(float3 c)
            {
                float4 K = float4(1.0, 2.0/3.0, 1.0/3.0, 3.0);
                float3 p = abs(frac(c.xxx + K.xyz) * 6.0 - K.www);
                return c.z * lerp(K.xxx, saturate(p - K.xxx), c.y);
            }
            float2 SafeUV(float2 uv)
            {
                return clamp(uv, 0.001, 0.999);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv = input.texcoord;
                if (_AcidIntensity < 0.001)
                {
                    return SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, uv, _BlitMipLevel);
                }
                float time = _Time.y;
                float intensity = _AcidIntensity;
                float2 centeredUV = uv - 0.5;
                centeredUV *= 1.0 + 0.015 * intensity;
                float2 scaledUV = centeredUV + 0.5;
                float caOffset = 0.006 * intensity;
                float2 dir = scaledUV - 0.5;
                float dist = length(dir);
                float2 caDir = normalize(dir + 0.0001) * caOffset * dist;
                float r = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, SafeUV(scaledUV + caDir), _BlitMipLevel).r;
                float g = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, SafeUV(scaledUV),         _BlitMipLevel).g;
                float b = SAMPLE_TEXTURE2D_X_LOD(_BlitTexture, sampler_LinearClamp, SafeUV(scaledUV - caDir), _BlitMipLevel).b;
                float3 color = float3(r, g, b);
                float3 hsv = RGBtoHSV(color);
                hsv.x = frac(hsv.x + 0.33 * intensity);
                hsv.y = saturate(hsv.y + 0.01 * intensity);
                color = HSVtoRGB(hsv);
                return half4(color, 1.0);
            }
            ENDHLSL
        }
    }
}