Shader "Custom/PickupSphere"
{
    Properties
    {
        _Color ("Glow Color", Color) = (0.2, 0.5, 1, 1)
        _FresnelPower ("Fresnel Power", Range(0.5, 8)) = 3
        _GlowIntensity ("Glow Intensity", Range(0, 5)) = 2
        _PulseSpeed ("Pulse Speed", Range(0, 5)) = 2
        _PulseMin ("Pulse Min", Range(0, 1)) = 0.5
        _PulseMax ("Pulse Max", Range(0, 1)) = 1
        _CenterAlpha ("Center Alpha", Range(0, 0.5)) = 0.05
    }
    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "RenderType"="Transparent"
            "RenderPipeline"="UniversalPipeline"
        }
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Back
        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
            };
            struct v2f
            {
                float4 pos : SV_POSITION;
                float3 worldNormal : TEXCOORD0;
                float3 worldPos : TEXCOORD1;
            };
            float4 _Color;
            float _FresnelPower;
            float _GlowIntensity;
            float _PulseSpeed;
            float _PulseMin;
            float _PulseMax;
            float _CenterAlpha;
            v2f vert(appdata v)
            {
                v2f o;
                o.pos = TransformObjectToHClip(v.vertex.xyz);
                o.worldNormal = TransformObjectToWorldNormal(v.normal);
                o.worldPos = TransformObjectToWorld(v.vertex.xyz);
                return o;
            }
            half4 frag(v2f i) : SV_Target
            {
                float3 viewDir = normalize(_WorldSpaceCameraPos - i.worldPos);
                float3 normal = normalize(i.worldNormal);
                float fresnel = pow(1.0 - saturate(dot(normal, viewDir)), _FresnelPower);
                float pulse = lerp(_PulseMin, _PulseMax, sin(_Time.y * _PulseSpeed) * 0.5 + 0.5);
                float3 col = _Color.rgb * _GlowIntensity * pulse;
                float alpha = lerp(_CenterAlpha, _Color.a, fresnel) * pulse;
                return half4(col * fresnel + col * _CenterAlpha * 0.3, alpha);
            }
            ENDHLSL
        }
    }
}
