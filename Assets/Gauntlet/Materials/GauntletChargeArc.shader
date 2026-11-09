Shader "Gauntlet/Charge Arc URP"
{
    Properties { _Intensity("亮度", Range(.1,4))=1.4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Name "ChargeArc"
            Blend SrcAlpha One
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half _Intensity;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; float2 uv:TEXCOORD0; half4 color:COLOR; };
            Varyings Vert(Attributes input)
            {
                Varyings o;
                o.positionCS=TransformObjectToHClip(input.positionOS.xyz);
                o.uv=input.uv; o.color=input.color;
                return o;
            }
            half4 Frag(Varyings input):SV_Target
            {
                half distance=abs(input.uv.y-.5)*2;
                half halo=pow(saturate(1-distance),2);
                half core=pow(saturate(1-distance),7);
                half tip=saturate(input.uv.x*12)*saturate((1-input.uv.x)*12);
                return half4((input.color.rgb+core*.6)*_Intensity, input.color.a*halo*tip);
            }
            ENDHLSL
        }
    }
}
