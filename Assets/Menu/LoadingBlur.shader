Shader "CustomEffects/LoadingBlur"
{
    HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"

        float _BlurAmount;
        float _PixelSize;

        float4 BlurVertical (Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            if (_PixelSize > 1.0)
            {
                float2 pixelCount = _ScreenParams.xy / _PixelSize;
                uv = floor(uv * pixelCount) / pixelCount;
            }

            const float SAMPLES = 16;
            const float HALF = SAMPLES / 2;
            float3 color = 0;
            float blurPixels = _BlurAmount * _ScreenParams.y;

            for (float i = -HALF; i <= HALF; i++)
            {
                float2 offset = float2(0, (blurPixels / _BlitTexture_TexelSize.w) * (i / HALF));
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset).rgb;
            }

            return float4(color / (SAMPLES + 1), 1);
        }

        float4 BlurHorizontal (Varyings input) : SV_Target
        {
            float2 uv = input.texcoord;

            const float SAMPLES = 16;
            const float HALF = SAMPLES / 2;
            float3 color = 0;
            float blurPixels = _BlurAmount * _ScreenParams.x;

            for (float i = -HALF; i <= HALF; i++)
            {
                float2 offset = float2((blurPixels / _BlitTexture_TexelSize.z) * (i / HALF), 0);
                color += SAMPLE_TEXTURE2D(_BlitTexture, sampler_LinearClamp, uv + offset).rgb;
            }

            return float4(color / (SAMPLES + 1), 1);
        }

    ENDHLSL

    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline" = "UniversalPipeline" }
        LOD 100
        ZWrite Off Cull Off

        Pass
        {
            Name "BlurPassVertical"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurVertical
            ENDHLSL
        }

        Pass
        {
            Name "BlurPassHorizontal"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment BlurHorizontal
            ENDHLSL
        }
    }
}
