Shader "Custom/BloodDecal"
{
    Properties
    {
        _MainTex        ("Albedo (RGBA)", 2D)       = "white" {}
        _NormalTex      ("Normal Map", 2D)          = "bump"  {}
        _Color          ("Tint", Color)             = (1,1,1,1)
        _NormalCutoff   ("Normal Cutoff", Range(0,1)) = 0.3
        _AlphaEdge      ("Alpha Edge Softness", Range(0.01,0.5)) = 0.15
    }

    SubShader
    {
        // Render after opaque geometry, before transparent
        Tags
        {
            "RenderType"  = "Transparent"
            "Queue"       = "Geometry+100"
            "RenderPipeline" = "UniversalPipeline"
        }

        Pass
        {
            Name "BloodDecalPass"

            // Don't write depth, blend over the surface
            ZWrite Off
            ZTest LEqual
            Cull Back

            // Standard alpha blend
            Blend SrcAlpha OneMinusSrcAlpha
            BlendOp Add

            HLSLPROGRAM
            #pragma vertex   vert
            #pragma fragment frag

            // GPU instancing
            #pragma multi_compile_instancing
            #pragma instancing_options assumeuniformscaling

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareNormalsTexture.hlsl"

            TEXTURE2D(_MainTex);   SAMPLER(sampler_MainTex);
            TEXTURE2D(_NormalTex); SAMPLER(sampler_NormalTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _Color;
                float  _NormalCutoff;
                float  _AlphaEdge;
            CBUFFER_END

            // Per-instance data injected by DecalManager via MaterialPropertyBlock
            UNITY_INSTANCING_BUFFER_START(Props)
                UNITY_DEFINE_INSTANCED_PROP(float4, _DecalColor)
                // packed: xyz = world anchor normal, w = unused
                UNITY_DEFINE_INSTANCED_PROP(float4, _AnchorNormal)
            UNITY_INSTANCING_BUFFER_END(Props)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionHCS  : SV_POSITION;
                float2 uv           : TEXCOORD0;
                // Screen-space position for depth sampling
                float4 screenPos    : TEXCOORD1;
                // View-space ray for world-pos reconstruction
                float3 viewRay      : TEXCOORD2;
                // Decal box space: local position of the vertex
                float3 decalPosOS   : TEXCOORD3;
                // World normal of anchor surface (passed through)
                float3 anchorNormal : TEXCOORD4;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            Varyings vert(Attributes IN)
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                Varyings OUT;
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);

                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.screenPos   = ComputeScreenPos(OUT.positionHCS);
                OUT.decalPosOS  = IN.positionOS.xyz;

                // View-space ray used for depth-to-world reconstruction
                float3 posVS    = TransformWorldToView(TransformObjectToWorld(IN.positionOS.xyz));
                OUT.viewRay     = posVS;

                OUT.anchorNormal = UNITY_ACCESS_INSTANCED_PROP(Props, _AnchorNormal).xyz;

                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);

                // ── 1. Reconstruct world position from scene depth ──────────────
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;

                float rawDepth = SampleSceneDepth(screenUV);

                // Reconstruct view-space position
                // linearEyeDepth gives us distance along -Z view axis
                float eyeDepth = LinearEyeDepth(rawDepth, _ZBufferParams);

                // Perspective-correct: scale the view ray to the sampled depth
                float3 viewPos = IN.viewRay * (eyeDepth / (-IN.viewRay.z));

                // World position of the scene surface under this pixel
                float3 worldPos = mul(unity_CameraToWorld, float4(viewPos, 1.0)).xyz;

                // ── 2. Transform world pos into decal object space ─────────────
                // unity_WorldToObject is per-instance thanks to GPU instancing
                float3 decalOS = mul(unity_WorldToObject, float4(worldPos, 1.0)).xyz;

                // Decal box is a unit cube [-0.5, 0.5] in object space
                // Discard pixels that fall outside the box
                float3 boxTest = abs(decalOS);
                clip(0.5 - boxTest);                      // kills anything outside

                // ── 3. Normal clipping ─────────────────────────────────────────
                float3 sceneNormalWS = SampleSceneNormals(screenUV);
                float  normalDot     = dot(sceneNormalWS, normalize(IN.anchorNormal));
                // Pixels whose surface faces away from our anchor normal get cut
                clip(normalDot - _NormalCutoff);

                // ── 4. Build UVs from decal box XZ ────────────────────────────
                // decalOS.xz spans [-0.5, 0.5] → remap to [0,1]
                float2 decalUV = decalOS.xz + 0.5;

                // Soft alpha falloff at box edges to avoid hard cut
                float2 edgeDist  = 0.5 - abs(decalOS.xz);
                float  edgeAlpha = saturate(min(edgeDist.x, edgeDist.y) / _AlphaEdge);

                // ── 5. Sample textures ─────────────────────────────────────────
                half4 albedo = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, decalUV);
                albedo      *= _Color * UNITY_ACCESS_INSTANCED_PROP(Props, _DecalColor);
                albedo.a    *= edgeAlpha;

                return albedo;
            }
            ENDHLSL
        }
    }

    FallBack Off
}
