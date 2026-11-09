Shader "Gauntlet/Atlas URP"
{
 Properties
 {
  _BaseMap("Base Color",2D)="white"{}
  [Normal] _BumpMap("Normal",2D)="bump"{}
  _MetallicGlossMap("Metallic (R), Smoothness (A)",2D)="white"{}
  _EmissionMap("Emission (RGB), Effect Region (A)",2D)="black"{}
  _EmissionStrength("Emission Strength",Range(0,12))=2
  _PulseAmount("Lamp Pulse",Range(0,1))=.15
  _Charge("Charge",Range(0,1))=0
  [HideInInspector] _DiegeticCharge("Diegetic Charge",Float)=0
  [HideInInspector] _StoredEnergy("Stored Energy",Range(0,1))=0
  [HideInInspector] _ChargeProgress("Charge Progress",Range(0,1))=0
  [HideInInspector] _ChargeTier("Charge Tier",Range(0,1))=0
  [HideInInspector] _ChargeActive("Charge Active",Float)=0
  [HideInInspector] _ChargeFlash("Charge Flash",Float)=0
  [HideInInspector] _ChargeFxIntensity("Charge FX Intensity",Float)=1
  [HideInInspector] _ChargeFxTime("Charge FX Time",Float)=0
  [HDR] _StoredColor("Stored Violet",Color)=(.32,.09,.7,1)
  [HDR] _ChargedColor("Charged Violet",Color)=(.83,.46,1,1)
  [Enum(UnityEngine.Rendering.CullMode)] _Cull("Cull",Float)=2
 }
 SubShader
 {
  Tags{"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit"
   Tags{"LightMode"="UniversalForwardOnly"}
   Cull[_Cull]
   HLSLPROGRAM
   #pragma vertex Vert
   #pragma fragment Frag
   #pragma target 3.5
   #pragma multi_compile_fog
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #define _RECEIVE_SHADOWS_OFF
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   #include "Packages/com.unity.render-pipelines.core/ShaderLibrary/Packing.hlsl"
   TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
   TEXTURE2D(_BumpMap);SAMPLER(sampler_BumpMap);
   TEXTURE2D(_MetallicGlossMap);SAMPLER(sampler_MetallicGlossMap);
   TEXTURE2D(_EmissionMap);SAMPLER(sampler_EmissionMap);
   CBUFFER_START(UnityPerMaterial)
    float4 _BaseMap_ST;
    half _EmissionStrength,_PulseAmount,_Charge;
    half _DiegeticCharge,_StoredEnergy,_ChargeProgress,_ChargeTier;
    half _ChargeActive,_ChargeFlash,_ChargeFxIntensity;
    float _ChargeFxTime;
    half4 _StoredColor,_ChargedColor;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;float4 tangentOS:TANGENT;float2 uv:TEXCOORD0;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;half4 tangentWS:TEXCOORD2;float2 uv:TEXCOORD3;half fog:TEXCOORD4;};
   Varyings Vert(Attributes v)
   {
    Varyings o;VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);VertexNormalInputs n=GetVertexNormalInputs(v.normalOS,v.tangentOS);
    o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=n.normalWS;o.tangentWS=half4(n.tangentWS,v.tangentOS.w*GetOddNegativeScale());o.uv=TRANSFORM_TEX(v.uv,_BaseMap);o.fog=ComputeFogFactor(p.positionCS.z);return o;
   }
   half4 Frag(Varyings v):SV_Target
   {
    half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,v.uv);
    half4 mask=SAMPLE_TEXTURE2D(_MetallicGlossMap,sampler_MetallicGlossMap,v.uv);
    half4 emit=SAMPLE_TEXTURE2D(_EmissionMap,sampler_EmissionMap,v.uv);
    half3 normalTS=UnpackNormal(SAMPLE_TEXTURE2D(_BumpMap,sampler_BumpMap,v.uv));
    half3 n=normalize(v.normalWS);half3 t=normalize(v.tangentWS.xyz);half3 b=cross(n,t)*v.tangentWS.w;
    InputData input=(InputData)0;input.positionWS=v.positionWS;input.normalWS=NormalizeNormalPerPixel(mul(normalTS,half3x3(t,b,n)));input.viewDirectionWS=GetWorldSpaceNormalizeViewDir(v.positionWS);input.shadowCoord=TransformWorldToShadowCoord(v.positionWS);input.fogCoord=v.fog;input.vertexLighting=VertexLighting(v.positionWS,input.normalWS);input.bakedGI=SampleSH(input.normalWS);input.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(v.positionCS);input.shadowMask=half4(1,1,1,1);
    half energy=1-smoothstep(.05,.13,abs(emit.a-.33));half lamp=1-smoothstep(.05,.13,abs(emit.a-.66));
    half pulse=1+energy*(sin(_Time.y*7+v.uv.y*80)*_PulseAmount+_Charge*2)+lamp*sin(_Time.y*1.7)*_PulseAmount;
    SurfaceData s=(SurfaceData)0;s.albedo=albedo.rgb;s.metallic=mask.r;s.smoothness=mask.a;s.normalTS=normalTS;s.occlusion=1;s.alpha=1;half energyIntensity=max(emit.r,max(emit.g,emit.b));
    half3 energyTint=lerp(half3(.48,.24,.76),half3(.81,.55,1),_Charge);
    s.emission=lerp(emit.rgb,energyIntensity*energyTint,energy)*_EmissionStrength*pulse;
    // The existing atlas already contains authored filaments. Animate only its
    // energy-tube mask, preserving painted metal, scratches, glass and hands.
    if (_DiegeticCharge > .5)
    {
     half charged=saturate(_ChargeProgress);
     half available=saturate(_StoredEnergy);
     float time=_ChargeFxTime;
     float2 flowUV=v.uv*float2(310,245);
     half drift=sin(flowUV.y*2.1-time*(1.2+charged*4.5)+sin(flowUV.x*1.8+time*.7)*1.5);
     half filaments=pow(saturate(energyIntensity),2);
     half travel=pow(saturate(.5+.5*drift),5);
     half shimmer=.8+.2*sin(time*(2+_ChargeTier*8)+flowUV.x*2.7);
     half ignition=_ChargeActive*(.12+charged*1.4);
     half3 tint=lerp(_StoredColor.rgb,_ChargedColor.rgb,charged);
     half reservoir=(.035+available*.8)*(.5+filaments*.7);
     half chargeLight=ignition*(filaments*.6+travel*(.3+filaments*1.2));
     half3 tube=tint*(reservoir+chargeLight)*shimmer;
     tube+=_ChargedColor.rgb*_ChargeFlash*(.2+filaments*1.4+travel*.7);
     // Empty tubes stay dark while the original fine surface texture remains.
     s.albedo=lerp(s.albedo,albedo.rgb*half3(.24,.13,.4),energy);
     s.emission=lerp(s.emission,tube*_EmissionStrength*_ChargeFxIntensity,energy);
     // The top indicator stays in the player's field of view while the reservoir
     // moves with the charging animation. Preserve its authored glass pattern.
     half3 lampTint=lerp(_StoredColor.rgb,_ChargedColor.rgb,charged);
     half lampLevel=.045+available*.38+ignition*(.65+travel*.7)+_ChargeFlash*.6;
     s.albedo=lerp(s.albedo,albedo.rgb*half3(.31,.17,.46),lamp*.85);
     s.emission=lerp(s.emission,lampTint*lampLevel*(.4+energyIntensity*.7)*_EmissionStrength*_ChargeFxIntensity,lamp);
    }
    half4 color=UniversalFragmentPBR(input,s);color.rgb=MixFog(color.rgb,input.fogCoord);return color;
   }
   ENDHLSL
  }
 }
 Fallback "Universal Render Pipeline/Lit"
}
