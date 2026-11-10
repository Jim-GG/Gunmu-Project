Shader "Game/Combat/Transient Electric Surface"
{
    Properties { [HDR] _Color("电流颜色",Color)=(.28,.35,1.3,1) _Envelope("包络",Range(0,1))=0 _LineScale("电弧密度",Float)=4 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent+10" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha One
            ZWrite Off ZTest LEqual Cull Back
            Offset -1,-1
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;float _Envelope,_LineScale;
            CBUFFER_END
            struct Attributes { float4 positionOS:POSITION;float3 normalOS:NORMAL; };
            struct Varyings { float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;float3 normal:TEXCOORD1; };
            Varyings vert(Attributes i){Varyings o;float3 p=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(p+TransformObjectToWorldNormal(i.normalOS)*.003);o.world=p;o.normal=TransformObjectToWorldNormal(i.normalOS);return o;}
            half4 frag(Varyings i):SV_Target
            {
                float3 p=i.world*_LineScale;float tick=floor(_Time.y*24);
                float zig=sin(p.y*4+tick*1.7)*.19+sin(p.y*11-tick)*.07;
                float arc=1-smoothstep(.012,.055,abs(frac(p.x*.7+p.z*.45+zig+tick*.13)-.5));
                float branches=1-smoothstep(.008,.027,abs(frac(p.y*.43+sin(p.x*8+tick)*.12+tick*.21)-.5));
                float rim=pow(1-saturate(abs(dot(normalize(i.normal),GetWorldSpaceNormalizeViewDir(i.world)))),3)*.1;
                return half4(_Color.rgb,saturate(max(arc,branches*.6)+rim)*_Envelope*.85);
            }
            ENDHLSL
        }
    }
}
