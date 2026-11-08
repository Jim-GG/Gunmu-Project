Shader "NullBoundary/Crystal Facet"
{
 Properties
 {
  _BaseColor("Smoky violet",Color)=(.25,.15,.36,1)
  _FacetLight("Internal fracture tint",Color)=(.68,.56,.83,1)
  _BaseMap("Base",2D)="white"{}
  _Smoothness("Polished facets",Range(0,1))=.86
  _Metallic("Metallic",Range(0,1))=.08
  _EmissionStrength("Inner glow",Range(0,2))=.08
  _FractureScale("Fracture scale",Float)=7
  _ImpactPulse("Impact flash",Float)=0
 }
 SubShader
 {
  Tags{"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
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
   half4 _BaseColor,_FacetLight;float4 _BaseMap_ST;half _Smoothness,_Metallic,_EmissionStrength,_FractureScale,_ImpactPulse;
   CBUFFER_END
   struct Attributes{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct Varyings{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float3 local:TEXCOORD2;half fog:TEXCOORD3;};
   Varyings vert(Attributes a){Varyings o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.positionWS=p.positionWS;o.normalWS=TransformObjectToWorldNormal(a.normalOS);o.local=a.positionOS.xyz;o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 frag(Varyings i):SV_Target
   {
    float3 p=i.local*_FractureScale;
    float bands=sin(p.x*2.8+p.y*3.7+sin(p.z*1.6)*.35);float seam=pow(saturate(1-abs(bands)*16),3)*.16;
    half3 normal=normalize(i.normalWS);half3 view=GetWorldSpaceNormalizeViewDir(i.positionWS);half rim=pow(1-saturate(dot(normal,view)),4);
    SurfaceData s=(SurfaceData)0;s.albedo=lerp(_BaseColor.rgb,_FacetLight.rgb,saturate(seam+rim*.3));s.alpha=1;s.metallic=_Metallic;s.specular=half3(.04,.04,.04);s.smoothness=_Smoothness;s.normalTS=half3(0,0,1);s.occlusion=1;s.emission=_FacetLight.rgb*(_EmissionStrength*(.16+seam+rim*.35)+_ImpactPulse*1.8);
    InputData d=(InputData)0;d.positionWS=i.positionWS;d.normalWS=normal;d.viewDirectionWS=view;d.shadowCoord=TransformWorldToShadowCoord(i.positionWS);d.bakedGI=SampleSH(normal);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
    half4 c=UniversalFragmentPBR(d,s);c.rgb=MixFog(c.rgb,i.fog);return c;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
