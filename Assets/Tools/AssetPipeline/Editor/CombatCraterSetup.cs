using System;using System.IO;using System.Linq;using UnityEditor;using UnityEngine;using UnityEngine.Rendering.Universal;using GameActions;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class CombatCraterSetup
 {
  const string Root="Assets/Game/VFX/Combat/",Stage="AssetStaging/VFXUpgrade/";
  static CombatCraterSetup(){EditorApplication.update+=()=>{if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"CraterFix.request"))return;File.Delete(Stage+"CraterFix.request");try{Build();File.WriteAllText(Stage+"crater_fix.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"crater_fix.json",e.ToString());Debug.LogException(e);}};}
  public static GameObject[] Build()
  {
   for(int h=1;h<=2;h++){string path=Root+"Textures/Crater/DrillCrater_"+h+"_Height.png";var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.sRGBTexture=false;importer.textureCompression=TextureImporterCompression.Uncompressed;var platform=importer.GetPlatformTextureSettings("Standalone");platform.overridden=true;platform.maxTextureSize=2048;platform.format=TextureImporterFormat.R16;importer.SetPlatformTextureSettings(platform);importer.SaveAndReimport();}
   var shader=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Graphs/DrillCraterDecal.shadergraph");if(!shader)throw new Exception("Crater decal Shader Graph not imported");var errors=ShaderUtil.GetShaderMessages(shader).Where(x=>x.severity==UnityEditor.Rendering.ShaderCompilerMessageSeverity.Error).ToArray();if(errors.Length>0)throw new Exception(string.Join(";",errors.Select(e=>e.message)));
   var renderer=AssetDatabase.LoadAssetAtPath<UniversalRendererData>("Assets/Game/Settings/PC_Renderer.asset");if(!File.Exists(Stage+"Backup/PC_Renderer.asset"))File.Copy("Assets/Game/Settings/PC_Renderer.asset",Stage+"Backup/PC_Renderer.asset");var feature=renderer.rendererFeatures.OfType<DecalRendererFeature>().FirstOrDefault();if(!feature){feature=ScriptableObject.CreateInstance<DecalRendererFeature>();feature.name="Combat normal and AO decals";AssetDatabase.AddObjectToAsset(feature,renderer);renderer.rendererFeatures.Add(feature);}var settings=new SerializedObject(feature);settings.FindProperty("m_Settings.technique").enumValueIndex=1;settings.ApplyModifiedPropertiesWithoutUndo();feature.Create();EditorUtility.SetDirty(renderer);renderer.SetDirty();
   var pom=AssetDatabase.LoadAssetAtPath<Shader>(Root+"Graphs/SG_GroundCrack_POM.shadergraph");if(!pom)throw new Exception("POM graph not imported");var result=new GameObject[2];
   for(int i=0;i<2;i++)
   {
    string prefix="Textures/Crater/DrillCrater_"+(i+1);var mats=new Material[2];
    for(int j=0;j<2;j++)
    {
     string path=Root+"Materials/DrillCrater_"+(i+1)+(j==0?"":"_POM")+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(path);if(!mat){mat=new Material(j==0?shader:pom);AssetDatabase.CreateAsset(mat,path);}mat.shader=j==0?shader:pom;mat.shaderKeywords=Array.Empty<string>();mat.renderQueue=-1;
     mat.SetTexture("_CraterNormal",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+prefix+"_Normal.png"));mat.SetTexture("_CraterAO",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+prefix+"_AO.png"));mat.SetTexture("_CraterMask",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+prefix+"_Mask.png"));mat.SetFloat("_NormalStrength",1);mat.SetFloat("_AOIntensity",1);mat.SetFloat("_CrackOpacity",1);mat.SetFloat("_EdgeFadeStart",.62f);mat.SetFloat("_EdgeFadeEnd",.95f);mat.SetFloat("_Smoothness",.15f);
     if(j==1){mat.SetTexture("_HeightMap",AssetDatabase.LoadAssetAtPath<Texture2D>(Root+prefix+"_Height.png"));mat.SetFloat("_HeightAmplitudeCm",20);mat.SetFloat("_POMSteps",24);mat.SetVector("_PrimitiveSize",new Vector4(1,1,0,0));}
     var serialized=new SerializedObject(mat);foreach(string property in new[]{"m_SavedProperties.m_TexEnvs","m_SavedProperties.m_Floats","m_SavedProperties.m_Colors"}){var array=serialized.FindProperty(property);for(int k=array.arraySize-1;k>=0;k--)if(!mat.HasProperty(array.GetArrayElementAtIndex(k).FindPropertyRelative("first").stringValue))array.DeleteArrayElementAtIndex(k);}serialized.ApplyModifiedPropertiesWithoutUndo();EditorUtility.SetDirty(mat);mats[j]=mat;
    }
    var go=new GameObject("DrillCrater_"+(i+1));var far=new GameObject("Distant normal AO projection");far.transform.SetParent(go.transform,false);var projector=far.AddComponent<DecalProjector>();projector.material=mats[0];projector.size=new Vector3(1,1,.25f);projector.pivot=Vector3.zero;projector.scaleMode=DecalScaleMode.InheritFromHierarchy;projector.drawDistance=90;
    var mesh=GameObject.CreatePrimitive(PrimitiveType.Quad);mesh.name="Near POM mesh (no color)";UnityEngine.Object.DestroyImmediate(mesh.GetComponent<Collider>());mesh.transform.SetParent(go.transform,false);var rendererMesh=mesh.GetComponent<Renderer>();rendererMesh.sharedMaterial=mats[1];rendererMesh.shadowCastingMode=UnityEngine.Rendering.ShadowCastingMode.Off;rendererMesh.receiveShadows=false;var runtime=go.AddComponent<GroundCrackImpact>();runtime.parallaxMesh=rendererMesh;runtime.distantDecal=projector;result[i]=PrefabUtility.SaveAsPrefabAsset(go,Root+"DrillCrater_"+(i+1)+".prefab");UnityEngine.Object.DestroyImmediate(go);
   }
   AssetDatabase.SaveAssets();return result;
  }
 }
}
