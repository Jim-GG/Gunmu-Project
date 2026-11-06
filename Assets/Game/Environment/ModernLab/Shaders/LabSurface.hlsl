#ifndef MODERN_LAB_SURFACE_INCLUDED
#define MODERN_LAB_SURFACE_INCLUDED
float3 LabNormal(float3 n,float strength)
{
 n.xy*=strength;return normalize(float3(n.xy,max(n.z,0.001)));
}
void LabSurface_float(float2 UV,UnityTexture2D BaseMap,UnityTexture2D NormalMap,UnityTexture2D MaskMap,UnityTexture2D MicroNormal,
 float4 Tint,float4 DustColour,float DustAmount,float DustColourStrength,float DustRoughness,float SpotAmount,
 float NormalStrength,float MicroStrength,float MicroTiling,float RoughnessBias,float SurfaceTiling,float DustTiling,
 out float3 Albedo,out float3 Normal,out float Metallic,out float Smoothness,out float Occlusion)
{
 float2 microUV=UV*MicroTiling;UV*=SurfaceTiling;
 float4 base=SAMPLE_TEXTURE2D(BaseMap.tex,BaseMap.samplerstate,UV);
 float4 mask=SAMPLE_TEXTURE2D(MaskMap.tex,MaskMap.samplerstate,UV);
 float dirt=saturate(base.a*SpotAmount);
 float dust=saturate(SAMPLE_TEXTURE2D(MaskMap.tex,MaskMap.samplerstate,UV*DustTiling).b*DustAmount);
 float3 clean=base.rgb*lerp(Tint.rgb,float3(1,1,1),saturate(mask.r));
 Albedo=lerp(clean,float3(.018,.021,.023),dirt);
 Albedo=lerp(Albedo,DustColour.rgb,saturate(dust*DustColourStrength));
 float rough=saturate(1-mask.a+RoughnessBias);
 rough=lerp(rough,.83,dirt);rough=lerp(rough,DustRoughness,dust);
 Smoothness=1-saturate(rough);Metallic=saturate(mask.r*(1-dirt)*(1-dust));Occlusion=mask.g;
 float3 main=LabNormal(UnpackNormal(SAMPLE_TEXTURE2D(NormalMap.tex,NormalMap.samplerstate,UV)),NormalStrength);
 float3 detail=LabNormal(UnpackNormal(SAMPLE_TEXTURE2D(MicroNormal.tex,MicroNormal.samplerstate,microUV)),MicroStrength);
 float3 a=main+float3(0,0,1);float3 b=detail*float3(-1,-1,1);
 Normal=normalize(a*dot(a,b)/max(a.z,0.001)-b);
}
#endif
