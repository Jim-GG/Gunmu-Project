Shader "WindConeFX/URP/WindCone"
{
    Properties
    {
        [MainTexture] _MainTex ("Packed smoke mask (G by default)", 2D) = "white" {}
        _NoiseTex ("Distortion / breakup noise", 2D) = "gray" {}
        [HDR][MainColor] _TintColor ("Wind emission tint", Color) = (0.48,0.88,1,1)
        _Intensity ("Emission intensity", Range(0,12)) = 2.5
        _Opacity ("Opacity", Range(0,1)) = 0.72
        _LifeAlpha ("Legacy controller life alpha", Range(0,1)) = 1
        _Progress ("Optional end erosion (0 = visible)", Range(0,1)) = 0
        _PhaseOffset ("Animation phase offset (seconds)", Float) = 0
        _Additive ("Additive amount", Range(0,1)) = 0.7
        _MaskChannel ("Smoke mask channel RGBA", Vector) = (0,1,0,0)
        _ScrollSpeed ("Smoke scroll XY", Vector) = (0,-2.8,0,0)
        _NoiseScrollSpeed ("Noise scroll XY", Vector) = (0.32,-0.65,0,0)
        _DistortionStrength ("UV distortion", Range(0,0.5)) = 0.065
        _Breakup ("Noise breakup strength", Range(0,1)) = 0.2
        _Threshold ("Wisp threshold", Range(0,1)) = 0.12
        _Softness ("Wisp softness", Range(0.01,1)) = 0.45
        _AxialUV ("Axial coordinate from UV dot XY", Vector) = (0,1,0,0)
        _AxialFlip ("Reverse axial coordinate", Range(0,1)) = 0
        _BaseFade ("Axial base fade width", Range(0.001,0.5)) = 0.08
        _TipFade ("Axial tip fade width", Range(0.001,0.5)) = 0.16
        _FresnelStrength ("Soft grazing glow", Range(0,1)) = 0.28
        _FresnelPower ("Grazing power", Range(0.1,8)) = 1.6
        _UseVertexColor ("Multiply source vertex colors", Range(0,1)) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Pass
        {
            Name "WindConeUnlit"
            Tags { "LightMode"="SRPDefaultUnlit" }
            Blend One OneMinusSrcAlpha
            Cull Off
            ZWrite Off
            ZTest LEqual
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_MainTex); SAMPLER(sampler_MainTex);
            TEXTURE2D(_NoiseTex); SAMPLER(sampler_NoiseTex);
            CBUFFER_START(UnityPerMaterial)
                float4 _MainTex_ST;
                float4 _NoiseTex_ST;
                float4 _TintColor;
                float4 _MaskChannel;
                float4 _ScrollSpeed;
                float4 _NoiseScrollSpeed;
                float4 _AxialUV;
                float _Intensity;
                float _Opacity;
                float _LifeAlpha;
                float _Progress;
                float _PhaseOffset;
                float _Additive;
                float _DistortionStrength;
                float _Breakup;
                float _Threshold;
                float _Softness;
                float _AxialFlip;
                float _BaseFade;
                float _TipFade;
                float _FresnelStrength;
                float _FresnelPower;
                float _UseVertexColor;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS : NORMAL;
                float2 uv : TEXCOORD0;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };
            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3 normalWS : TEXCOORD1;
                float2 uv : TEXCOORD2;
                half4 color : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };
            Varyings Vert(Attributes input)
            {
                Varyings output = (Varyings)0;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.uv = input.uv;
                output.color = input.color;
                return output;
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float t = _Time.y + _PhaseOffset;
                float2 noiseUV = input.uv * _NoiseTex_ST.xy + _NoiseTex_ST.zw + t * _NoiseScrollSpeed.xy;
                float2 noise = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV).rg;
                // Source image can have correlated R/G; two offset samples avoid
                // turning its distortion into a single diagonal displacement.
                noise.y = SAMPLE_TEXTURE2D(_NoiseTex, sampler_NoiseTex, noiseUV + float2(0.37,0.19)).r;
                float2 smokeUV = input.uv * _MainTex_ST.xy + _MainTex_ST.zw
                    + t * _ScrollSpeed.xy + (noise * 2.0 - 1.0) * _DistortionStrength;
                float4 packed = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, smokeUV);
                float smoke = saturate(dot(packed, _MaskChannel));
                float erosion = saturate(_Progress) * saturate(_Progress) * 0.65;
                float wisp = smoothstep(_Threshold + erosion,
                    _Threshold + erosion + max(_Softness,0.01),
                    smoke - noise.x * _Breakup);

                // Fade uses ORIGINAL UVs, never scrolling/distorted UVs. The
                // importer chooses the UV axis; this does not depend on world Z.
                float axial = saturate(dot(input.uv, _AxialUV.xy));
                axial = lerp(axial,1.0-axial,saturate(_AxialFlip));
                float envelope = smoothstep(0.0,max(_BaseFade,0.001),axial)
                    * smoothstep(0.0,max(_TipFade,0.001),1.0-axial);
                float3 viewDir = GetWorldSpaceNormalizeViewDir(input.positionWS);
                float fresnel = pow(saturate(1.0-abs(dot(normalize(input.normalWS),viewDir))),max(_FresnelPower,0.1));
                float facing = lerp(1.0,0.65+fresnel*0.75,saturate(_FresnelStrength));
                half4 vertex = lerp(half4(1,1,1,1),input.color,saturate(_UseVertexColor));
                float alpha = saturate(wisp * envelope * facing * _Opacity
                    * _LifeAlpha * _TintColor.a * vertex.a);
                half3 emission = _TintColor.rgb * vertex.rgb * max(_Intensity,0.0) * alpha;
                // RGB is premultiplied. At Additive=1, destination is retained
                // while emission remains; both modes obey _LifeAlpha/_Opacity.
                return half4(emission,alpha*(1.0-saturate(_Additive)));
            }
            ENDHLSL
        }
    }
    Fallback Off
}
