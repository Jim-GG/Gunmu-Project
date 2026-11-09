Shader "NullBoundary/Charge Crystal"
{
    Properties
    {
        _BaseColor("矿晶底色", Color) = (.15,.045,.26,1)
        _InnerColor("透光矿脉", Color) = (.65,.32,.94,1)
        _Smoothness("晶面光泽", Range(0,1)) = .87
        _EmissionStrength("晶隙自发光", Range(0,3)) = .55
        _VeinScale("矿脉密度", Float) = 6
        _PickupPulse("微光变化", Float) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor, _InnerColor;
                half _Smoothness, _EmissionStrength, _VeinScale, _PickupPulse;
            CBUFFER_END

            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; };
            struct Varyings
            {
                float4 positionCS:SV_POSITION;
                float3 positionWS:TEXCOORD0;
                half3 normalWS:TEXCOORD1;
                float3 positionOS:TEXCOORD2;
                half fog:TEXCOORD3;
            };
            float Hash(float3 p)
            {
                p = frac(p * .1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }
            float MineralNoise(float3 p)
            {
                float3 cell = floor(p), f = frac(p);
                f = f*f*(3-2*f);
                return lerp(lerp(lerp(Hash(cell), Hash(cell+float3(1,0,0)), f.x),
                    lerp(Hash(cell+float3(0,1,0)), Hash(cell+float3(1,1,0)), f.x), f.y),
                    lerp(lerp(Hash(cell+float3(0,0,1)), Hash(cell+float3(1,0,1)), f.x),
                    lerp(Hash(cell+float3(0,1,1)), Hash(cell+float3(1,1,1)), f.x), f.y), f.z);
            }
            Varyings vert(Attributes input)
            {
                Varyings output;
                VertexPositionInputs p = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = p.positionCS;
                output.positionWS = p.positionWS;
                output.positionOS = input.positionOS.xyz;
                output.normalWS = TransformObjectToWorldNormal(input.normalOS);
                output.fog = ComputeFogFactor(p.positionCS.z);
                return output;
            }
            half4 frag(Varyings input):SV_Target
            {
                float3 p = input.positionOS * _VeinScale;
                float inclusion = MineralNoise(p*1.2) * .7 + MineralNoise(p*3.7+17.2) * .3;
                float band = abs(sin(p.x*3.7 + p.y*2.1 - p.z*4.8 + inclusion*5.2));
                float vein = 1-smoothstep(.025,.085,band);
                float fine = 1-smoothstep(.015,.05,abs(sin(p.x*11.3-p.y*7.9+inclusion*13.0)));
                half3 normal = normalize(input.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(input.positionWS);
                half rim = pow(1-saturate(dot(normal,view)), 5);
                float fractured = saturate(vein*.7 + fine*.14);
                SurfaceData surface = (SurfaceData)0;
                surface.albedo = lerp(_BaseColor.rgb * lerp(.5,1.15,inclusion), _InnerColor.rgb, fractured*.50 + rim*.20);
                surface.alpha = 1;
                surface.metallic = .06;
                surface.specular = half3(.065,.065,.065);
                surface.smoothness = _Smoothness - inclusion*.12;
                surface.normalTS = half3(0,0,1);
                surface.occlusion = .92;
                // Light comes from narrow mineral fractures, preserving dark, reflective facets.
                surface.emission = _InnerColor.rgb * ((_EmissionStrength+_PickupPulse) * (fractured + rim*.12) + .035);
                InputData data = (InputData)0;
                data.positionWS = input.positionWS;
                data.normalWS = normal;
                data.viewDirectionWS = view;
                data.shadowCoord = TransformWorldToShadowCoord(input.positionWS);
                data.bakedGI = SampleSH(normal);
                data.normalizedScreenSpaceUV = GetNormalizedScreenSpaceUV(input.positionCS);
                data.shadowMask = half4(1,1,1,1);
                half4 color = UniversalFragmentPBR(data,surface);
                color.rgb = MixFog(color.rgb,input.fog);
                return color;
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
