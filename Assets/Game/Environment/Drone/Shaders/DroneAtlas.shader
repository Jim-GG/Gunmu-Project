Shader "Null Boundary/Drone/Baked Enamel"
{
    Properties
    {
        [MainTexture] _BaseMap("Atlas · base colour", 2D) = "white" {}
        [MainColor] _BaseColor("Tint", Color) = (1,1,1,1)
        [Normal] _BumpMap("Atlas · tangent normal", 2D) = "bump" {}
        _BumpScale("Normal strength", Range(0,2)) = 1
        _MetallicGlossMap("Atlas · metal R / AO G / smooth A", 2D) = "white" {}
        _Metallic("Metallic multiplier", Range(0,1)) = 1
        _Smoothness("Smoothness multiplier", Range(0,1)) = 1
        _OcclusionStrength("Occlusion strength", Range(0,1)) = 1
        _EmissionMap("Atlas · emission RGB / clear coat A", 2D) = "black" {}
        [HDR] _EmissionColor("Emission multiplier", Color) = (8,8,8,1)
        _ClearCoatMask("Glazed enamel coat multiplier", Range(0,1)) = 1
        _ClearCoatSmoothness("Coat smoothness", Range(0,1)) = 0.9
        [Normal] _DetailNormalMap("Shared lab micro normal", 2D) = "bump" {}
        _DetailNormalMapScale("Micro normal strength", Range(0,1)) = 0.06
        _DetailAlbedoMapScale("Micro tiling (atlas UV)", Range(1,100)) = 28
        [HideInInspector] _SpecColor("Specular", Color) = (.2,.2,.2,1)
        [HideInInspector] _Cutoff("Cutoff", Float) = .5
        [HideInInspector] _Surface("Surface", Float) = 0
        [HideInInspector] _Cull("Cull", Float) = 2
        [HideInInspector] _Parallax("Parallax", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "UniversalMaterialType"="Lit" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForwardOnly" }
            Cull Back ZWrite On
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex LitPassVertex
            #pragma fragment LitPassFragment
            #pragma multi_compile_instancing
            #pragma multi_compile_fog
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BLENDING
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_BOX_PROJECTION
            #pragma multi_compile_fragment _ _REFLECTION_PROBE_ATLAS
            #pragma multi_compile_fragment _ _SCREEN_SPACE_OCCLUSION
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile _ LIGHTMAP_ON
            #pragma multi_compile _ LIGHTMAP_SHADOW_MIXING
            #pragma multi_compile _ SHADOWS_SHADOWMASK
            #pragma multi_compile _ DIRLIGHTMAP_COMBINED
            #pragma multi_compile _ EVALUATE_SH_MIXED EVALUATE_SH_VERTEX
            #define _NORMALMAP 1
            #define _CLEARCOAT 1
            #define _EMISSION 1
            #define _METALLICSPECGLOSSMAP 1
            #define InitializeStandardLitSurfaceData InitializeUnusedLitSurfaceData
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitInput.hlsl"
            #undef InitializeStandardLitSurfaceData
            void InitializeStandardLitSurfaceData(float2 uv, out SurfaceData data)
            {
                data = (SurfaceData)0;
                half4 mask = SAMPLE_TEXTURE2D(_MetallicGlossMap, sampler_MetallicGlossMap, uv);
                half4 glow = SAMPLE_TEXTURE2D(_EmissionMap, sampler_EmissionMap, uv);
                data.albedo = SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
                data.alpha = 1;
                data.metallic = saturate(mask.r * _Metallic);
                data.specular = _SpecColor.rgb;
                data.smoothness = saturate(mask.a * _Smoothness);
                data.occlusion = lerp(1, mask.g, _OcclusionStrength);
                data.emission = glow.rgb * _EmissionColor.rgb;
                data.clearCoatMask = saturate(glow.a * _ClearCoatMask);
                data.clearCoatSmoothness = _ClearCoatSmoothness;
                half3 main = UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap, sampler_BumpMap, uv), _BumpScale);
                half3 micro = UnpackNormalScale(SAMPLE_TEXTURE2D(_DetailNormalMap, sampler_DetailNormalMap, uv * _DetailAlbedoMapScale), _DetailNormalMapScale);
                // Same reoriented normal blend as the existing ModernLab microsurface shader.
                half3 a = main + half3(0,0,1);
                half3 b = micro * half3(-1,-1,1);
                data.normalTS = normalize(a * dot(a,b) / max(a.z, .001) - b);
            }
            #include "Packages/com.unity.render-pipelines.universal/Shaders/LitForwardPass.hlsl"
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
    Fallback "Hidden/Universal Render Pipeline/FallbackError"
}
