Shader "Game/Screen Effects/Combat Radial Blur"
{
    Properties
    {
        [Toggle] _Enabled("启用", Float) = 0
        _Center("径向中心", Vector) = (.5,.5,0,0)
        _Intensity("径向混合", Range(0,1)) = 0
        _Strength("径向范围", Range(0,.25)) = .1
        _InnerRadius("清晰内圈", Float) = .06
        _OuterRadius("外围半径", Float) = .65
        _ChromaticPixels("色散像素", Float) = 0
        _Vignette("蓄力暗角", Float) = 0
        _PurpleNoise("共鸣噪波透明度", Float) = 0
        _RedEdge("低血量边缘透明度", Float) = 0
        _NoiseColor("共鸣颜色", Color) = (.4,.04,.58,1)
        _DamageColor("受伤颜色", Color) = (.6,.015,.025,1)
        _NoiseFlow("噪波UV流动", Vector) = (.11,.08,0,0)
        _NoiseScale("噪波尺寸", Float) = 15
        _EffectTime("效果时钟", Float) = 0
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" }
        ZWrite Off Cull Off ZTest Always
        Pass
        {
            Name "Combat screen effects"
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma target 3.5
            #pragma multi_compile_local_fragment _ _RADIAL_8 _RADIAL_16
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.core/Runtime/Utilities/Blit.hlsl"
            CBUFFER_START(UnityPerMaterial)
                float4 _Center, _NoiseColor, _DamageColor, _NoiseFlow;
                float _Enabled, _Intensity, _Strength, _InnerRadius, _OuterRadius, _ChromaticPixels;
                float _Vignette, _PurpleNoise, _RedEdge, _NoiseScale, _EffectTime;
            CBUFFER_END
            half4 Source(float2 uv) { return SAMPLE_TEXTURE2D_X(_BlitTexture, sampler_LinearClamp, saturate(uv)); }
            float Hash(float2 p) { return frac(sin(dot(p, float2(127.1,311.7))) * 43758.5453); }
            float Noise(float2 p)
            {
                float2 i=floor(p), f=frac(p); f=f*f*(3-2*f);
                return lerp(lerp(Hash(i),Hash(i+float2(1,0)),f.x),lerp(Hash(i+float2(0,1)),Hash(i+1),f.x),f.y);
            }
            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);
                float2 uv=input.texcoord;
                half4 original=Source(uv);
                if (_Enabled < .5) return original;
                half3 color=original.rgb;
                if (_Intensity > .00001 && _Strength > .00001)
                {
                    float2 direction=uv-_Center.xy;
                    float aspect=_ScaledScreenParams.x/max(1,_ScaledScreenParams.y);
                    float falloff=smoothstep(_InnerRadius,max(_InnerRadius+.001,_OuterRadius),length(direction*float2(aspect,1)));
                    float2 reach=direction*_Strength*falloff;
                    #if defined(_RADIAL_8)
                        #define TAPS 8
                    #elif defined(_RADIAL_16)
                        #define TAPS 16
                    #else
                        #define TAPS 12
                    #endif
                    half3 sum=0;
                    [unroll] for(int i=0;i<TAPS;i++) sum+=Source(uv-reach*((float)i/(TAPS-1))).rgb;
                    color=lerp(color,sum/TAPS,saturate(_Intensity));
                }
                float2 delta=uv-.5;
                float edge=smoothstep(.24,.71,length(delta));
                if (_ChromaticPixels > .0001)
                {
                    float2 split=normalize(delta+float2(.00001,0))*_ChromaticPixels*edge/max(_ScaledScreenParams.xy,1);
                    half3 chromatic=half3(Source(uv+split).r,original.g,Source(uv-split).b);
                    color=max(0,color+chromatic-original.rgb);
                }
                color*=1-saturate(_Vignette)*edge;
                if (_PurpleNoise > .0001 || _RedEdge > .0001)
                {
                    float2 nUV=uv*_NoiseScale+_NoiseFlow.xy*_EffectTime;
                    float noise=Noise(nUV)*.65+Noise(nUV*2.13-float2(.07,.12)*_EffectTime)*.35;
                    float irregular=smoothstep(.22+noise*.08,.7,length(delta));
                    color=lerp(color,_NoiseColor.rgb,saturate(_PurpleNoise)*irregular*(.22+noise*.5));
                    float redMask=smoothstep(.33+noise*.08,.72,length(delta));
                    color=lerp(color,_DamageColor.rgb,saturate(_RedEdge)*redMask*(.4+noise*.6));
                }
                return half4(color,original.a);
            }
            ENDHLSL
        }
    }
}
