Shader "Game/Combat/Transparent Falling Warning"
{
    Properties { _Color("警示颜色与透明度",Color)=(1,.19,.025,.52) }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off ZTest LEqual
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
            float4 _Color;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float2 uv:TEXCOORD0;};
            Varyings vert(Attributes i){Varyings o;o.positionCS=TransformObjectToHClip(i.positionOS.xyz);o.uv=i.uv;return o;}
            half4 frag(Varyings i):SV_Target
            {
                float r=length(i.uv*2-1);float ring=1-smoothstep(.016,.055,abs(r-.91));
                float fill=(1-smoothstep(.85,.98,r))*.13;float pulse=.82+.18*sin(_Time.y*11);
                return half4(_Color.rgb,_Color.a*saturate(ring+fill)*pulse);
            }
            ENDHLSL
        }
    }
}
