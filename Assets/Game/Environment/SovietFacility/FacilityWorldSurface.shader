Shader "NullBoundary/Facility World Surface"
{
 Properties
 {
  _BaseMap("旧设施表面贴图",2D)="white"{}
  _BumpMap("表面法线",2D)="bump"{}
  _BaseColor("表面色调",Color)=(1,1,1,1)
  _WorldScale("每米纹理重复",Float)=.45
  _NormalStrength("法线强度",Range(0,2))=.7
  _Metallic("金属",Range(0,1))=.1
  _Smoothness("光滑度",Range(0,1))=.25
 }
 SubShader
 {
  Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
  Pass
  {
   Name "ForwardLit" Tags{"LightMode"="UniversalForward"}
   HLSLPROGRAM
   #pragma target 3.5
   #pragma vertex vert
   #pragma fragment frag
   #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
   #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
   #pragma multi_compile _ _CLUSTER_LIGHT_LOOP
   #pragma multi_compile_fragment _ _SHADOWS_SOFT
   #pragma multi_compile_fog
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
   TEXTURE2D(_BaseMap);
   TEXTURE2D(_BumpMap);
   CBUFFER_START(UnityPerMaterial)
   float4 _BaseColor; float _WorldScale,_NormalStrength,_Metallic,_Smoothness;
   CBUFFER_END
   struct A{float4 positionOS:POSITION;float3 normalOS:NORMAL;};
   struct V{float4 positionCS:SV_POSITION;float3 world:TEXCOORD0;half3 normal:TEXCOORD1;half fog:TEXCOORD2;};
   V vert(A a){V o;VertexPositionInputs p=GetVertexPositionInputs(a.positionOS.xyz);o.positionCS=p.positionCS;o.world=p.positionWS;o.normal=TransformObjectToWorldNormal(a.normalOS);o.fog=ComputeFogFactor(p.positionCS.z);return o;}
   half4 frag(V i):SV_Target
   {
    float3 p=i.world*_WorldScale;half3 n=normalize(i.normal);half3 w=pow(abs(n),8);w/=max(dot(w,1),.001);
    half3 base=SAMPLE_TEXTURE2D(_BaseMap,sampler_LinearRepeat,p.zy).rgb*w.x+SAMPLE_TEXTURE2D(_BaseMap,sampler_LinearRepeat,p.xz).rgb*w.y+SAMPLE_TEXTURE2D(_BaseMap,sampler_LinearRepeat,p.xy).rgb*w.z;
    half3 nx=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_LinearRepeat,p.zy),_NormalStrength);
    half3 ny=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_LinearRepeat,p.xz),_NormalStrength);
    half3 nz=UnpackNormalScale(SAMPLE_TEXTURE2D(_BumpMap,sampler_LinearRepeat,p.xy),_NormalStrength);
    half3 surf=normalize(half3(n.x,nx.y,nx.x)*w.x+half3(ny.x,n.y,ny.y)*w.y+half3(nz.x,nz.y,n.z)*w.z);
    // Large, slow variation breaks up modular repeats without hiding the worn source texture.
    half variation=.94+.06*sin(i.world.x*.23+i.world.z*.39)*sin(i.world.y*.43+i.world.z*.11);
    SurfaceData s=(SurfaceData)0;s.albedo=base*_BaseColor.rgb*variation;s.alpha=1;s.metallic=_Metallic;s.smoothness=_Smoothness;s.specular=half3(.04,.04,.04);s.occlusion=1;s.normalTS=half3(0,0,1);
    InputData d=(InputData)0;d.positionWS=i.world;d.normalWS=surf;d.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.world);d.shadowCoord=TransformWorldToShadowCoord(i.world);d.bakedGI=SampleSH(surf);d.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);d.shadowMask=half4(1,1,1,1);
    half4 c=UniversalFragmentPBR(d,s);c.rgb=MixFog(c.rgb,i.fog);return c;
   }
   ENDHLSL
  }
  UsePass "Universal Render Pipeline/Lit/ShadowCaster"
  UsePass "Universal Render Pipeline/Lit/DepthOnly"
 }
}
