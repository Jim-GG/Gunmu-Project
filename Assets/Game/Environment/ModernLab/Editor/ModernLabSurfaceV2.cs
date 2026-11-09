using System;using System.IO;using System.Linq;using UnityEngine;using UnityEditor;
namespace ModernLab.Editor
{
 public static class ModernLabSurfaceV2
 {
  [Serializable]class Surface{public string name,source,detail,kind;public float[] tint;public float dust_amount,dust_colour_strength,dust_roughness,dust_tiling,spot_amount,normal_strength,micro_strength,micro_tiling,roughness_bias;}
  [Serializable]class Config{public Surface[] materials;}
  static Config config;
  public static void Initialize()
  {
   config=JsonUtility.FromJson<Config>(File.ReadAllText("ArtSource/ModernLab/surface_v2_unity.json"));
   foreach(var key in new[]{"Lab_Detail_Mineral_Normal","Lab_Detail_Coating_Normal"})
   {
    var importer=(TextureImporter)AssetImporter.GetAtPath("Assets/Game/Environment/ModernLab/Textures/"+key+".png");if(!importer)throw new Exception("Missing shared micro normal "+key);
    string before=EditorJsonUtility.ToJson(importer);importer.textureType=TextureImporterType.NormalMap;importer.sRGBTexture=false;importer.wrapMode=TextureWrapMode.Repeat;importer.filterMode=FilterMode.Trilinear;importer.anisoLevel=4;importer.mipmapEnabled=true;importer.maxTextureSize=1024;importer.textureCompression=TextureImporterCompression.CompressedHQ;
    if(EditorJsonUtility.ToJson(importer)!=before)importer.SaveAndReimport();
   }
  }
  public static void Configure(Material m,string key)
  {
   var p=config.materials.Single(s=>s.name==key);var source=string.IsNullOrEmpty(p.source)?key:p.source;string root="Assets/Game/Environment/ModernLab/Textures/";
   m.shaderKeywords=Array.Empty<string>();m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+source+"_BaseColor.png"));m.SetTexture("_BumpMap",AssetDatabase.LoadAssetAtPath<Texture2D>(root+source+"_Normal.png"));m.SetTexture("_SurfaceMask",AssetDatabase.LoadAssetAtPath<Texture2D>(root+source+"_Mask.png"));m.SetTexture("_MicroNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(root+p.detail+".png"));
   m.SetColor("_BaseColor",new Color(p.tint[0],p.tint[1],p.tint[2]));m.SetColor("_DustColour",new Color(.56f,.57f,.56f));
   foreach(var pair in new[]{("_DustAmount",p.dust_amount),("_DustColourStrength",p.dust_colour_strength),("_DustRoughness",p.dust_roughness),("_DustTiling",p.dust_tiling),("_SpotAmount",p.spot_amount),("_NormalStrength",p.normal_strength),("_MicroStrength",p.micro_strength),("_MicroTiling",p.micro_tiling),("_RoughnessBias",p.roughness_bias),("_SurfaceTiling",1f)})m.SetFloat(pair.Item1,pair.Item2);
   m.renderQueue=-1;m.enableInstancing=true;
  }
 }
}
