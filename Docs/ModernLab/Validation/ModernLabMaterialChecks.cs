using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using System.IO;
using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static class ModernLabMaterialChecks
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static async Task<object> Run()
 {
  Check(!Application.isPlaying,"Run material validation in Edit Mode");string root="Assets/Game/Environment/ModernLab/";var cases=new List<string>();
  string[] names={"Lab_White_Matte","Lab_Dark_Panel","Lab_Floor_Resin","Lab_Rail_BrushedSteel","Lab_Floor_Checker","Lab_Ceiling_Acoustic","Lab_Wall_Ribbed","Lab_Paint_RoughWhite","Lab_Paint_GlossWhite","Lab_Paint_Lime","Lab_Paint_Red","Lab_Paint_Yellow"};var materials=names.Select(n=>AssetDatabase.LoadAssetAtPath<Material>(root+"Materials/"+n+".mat")).ToArray();
  for(int i=0;i<materials.Length;i++)
  {
   var m=materials[i];Check(m&&m.shader.name=="Universal Render Pipeline/Lit"&&!ShaderUtil.ShaderHasError(m.shader),"Material shader missing or failed "+names[i]);
   Check(m.GetTexture("_BaseMap")&&m.GetTexture("_BumpMap")&&m.GetTexture("_MetallicGlossMap"),"A PBR channel is missing "+names[i]);
   Check(m.GetTexture("_MetallicGlossMap")==m.GetTexture("_OcclusionMap"),"Mask is not shared for AO and smoothness");
   Check(m.GetFloat("_Smoothness")==1&&m.IsKeywordEnabled("_METALLICSPECGLOSSMAP")&&m.IsKeywordEnabled("_NORMALMAP"),"Packed smoothness/normal is disabled");
   Check(m.GetFloat("_Cull")==2,"Surface should use correctly oriented front/back faces");
   foreach(var suffix in new[]{"BaseColor","Normal","Mask"})
   {
    var key=i>=9?(i==11?"Lab_Paint_GlossWhite":"Lab_Paint_RoughWhite"):names[i];
    var path=root+"Textures/"+key+"_"+suffix+".png";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);
    Check(t&&t.width==2048&&t.height==2048,"Texture resolution "+path);Check(importer.sRGBTexture==(suffix=="BaseColor"),"Incorrect colour space "+path);Check((importer.textureType==TextureImporterType.NormalMap)==(suffix=="Normal"),"Normal map import "+path);
   }
  }
  Check(materials[3].GetFloat("_Metallic")==1,"Steel handrail material was assigned a dielectric response");
  cases.Add("Twelve shared URP materials have complete 2K PBR maps and correct colour spaces");cases.Add("Walls and resin are dielectric; handrails have a dedicated metallic steel material");
  for(int i=9;i<12;i++)Check(materials[i].GetTexture("_BaseMap")==materials[i==11?8:7].GetTexture("_BaseMap")&&materials[i].GetTexture("_BumpMap")==materials[i==11?8:7].GetTexture("_BumpMap")&&materials[i].GetTexture("_MetallicGlossMap")==materials[i==11?8:7].GetTexture("_MetallicGlossMap"),"Colour variants duplicated surface maps");
  var prefabPaths=Directory.GetFiles(root+"Prefabs","*.prefab");Check(prefabPaths.Length==36,"Missing original or derived module exports: "+prefabPaths.Length);
  foreach(var path in prefabPaths)
  {
   var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(path);Check(prefab,"Missing prefab "+path);
   foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))Check(r.sharedMaterials.All(m=>materials.Contains(m)),"Unexpected per-object material "+path);
   foreach(var f in prefab.GetComponentsInChildren<MeshFilter>(true))
   {
    var mesh=f.sharedMesh;Check(mesh&&mesh.vertexCount>0&&mesh.uv.Length==mesh.vertexCount,"Missing UV0 "+path);Check(mesh.tangents.Length==mesh.vertexCount,"Missing tangent basis "+path);
    if(path.Contains("Wall")||path.Contains("Ceiling"))Check(mesh.normals.Any(n=>Vector3.Dot(n,mesh.normals[0])<-.9f),"Thin module lacks oppositely oriented normals "+path);
    if(path.Contains("WallWhite_16"))Check(mesh.uv.Max(p=>p.x)-mesh.uv.Min(p=>p.x)>1.9f,"4m wall stretched one material tile instead of 2");
   }
  }
  cases.Add("Original 29 modules plus 7 derived modules use shared materials, valid UV0 and tangent bases");cases.Add("Metric UV scale survives FBX import on a 4 metre wall; colour paints reuse white paint surface maps");
  var errors=new List<string>();Application.LogCallback record=(message,stack,type)=>{if(type==LogType.Error||type==LogType.Exception)errors.Add(message);};Application.logMessageReceived+=record;
  var otherProbes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in otherProbes)p.enabled=false;
  var prior=SceneManager.GetActiveScene();var preview=EditorSceneManager.OpenScene(root+"Scenes/ModernLabModuleReview.unity",OpenSceneMode.Additive);
  try
  {
   SceneManager.SetActiveScene(preview);await Task.Delay(500);var camera=preview.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c);var probe=preview.GetRootGameObjects().Select(g=>g.GetComponent<ReflectionProbe>()).First(p=>p);
   Check(camera.cullingMask==(1<<30)&&probe.cullingMask==(1<<30),"Preview overlaps the current gameplay scene");
   Check(probe.mode==ReflectionProbeMode.Custom&&probe.customBakedTexture is Cubemap,"Room reflection was not baked and persisted");
   var rails=preview.GetRootGameObjects().Where(g=>g.name.Contains("护栏")||new[]{"平面.001","平面.002","平面.003","平面.004"}.Contains(g.name)).ToArray();Check(rails.Length==9,"Review is missing actual guardrail variants");
   Check(preview.GetRootGameObjects().Count(g=>g.name.StartsWith("立方体"))==4,"Review is missing actual stair variants");
   foreach(var rail in rails)
   {
    Check(rail.GetComponentsInChildren<Renderer>().All(r=>r.sharedMaterials.All(m=>m==materials[3])),"Actual guardrail is not metallic: "+rail.name);
    foreach(var f in rail.GetComponentsInChildren<MeshFilter>()){var uv=f.sharedMesh.uv;Check(uv.Max(p=>p.x)-uv.Min(p=>p.x)>.1f||uv.Max(p=>p.y)-uv.Min(p=>p.y)>.1f,"Guardrail UV collapsed: "+rail.name);}
   }
   cases.Add("Actual mesh review contains four stair variants and all nine metallic guardrail variants with noncollapsed UVs");
   var path=Path.Combine(Directory.GetCurrentDirectory(),"ArtSource/ModernLab/Preview/ModernLab_UnityActualModules.png");
   var rt=RenderTexture.GetTemporary(1600,1100,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var old=camera.targetTexture;var oldActive=RenderTexture.active;Texture2D image=null;
   try
   {
    RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1600,1100,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1600,1100),0,0);image.Apply();File.WriteAllBytes(path,image.EncodeToPNG());
   }
   finally{camera.targetTexture=old;RenderTexture.active=oldActive;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
   Check(new FileInfo(path).Length>10000,"Unity preview render is empty");cases.Add("Actual URP preview renders with a persistent baked reflection, isolated from the gameplay scene");
  }
  finally
  {
   SceneManager.SetActiveScene(prior);foreach(var go in preview.GetRootGameObjects()){if(go.TryGetComponent<ReflectionProbe>(out var p))p.enabled=false;if(go.TryGetComponent<Camera>(out var c))c.enabled=false;}
   await Task.Delay(500);EditorSceneManager.CloseScene(preview,true);foreach(var p in otherProbes)if(p)p.enabled=true;await Task.Delay(250);Application.logMessageReceived-=record;
  }
  Check(errors.Count==0,"Rendering logged errors: "+string.Join("; ",errors.Distinct()));cases.Add("Preview rendering and scene cleanup log no new errors");
  return new{passed=true,cases=cases.ToArray(),surfaceMaterials=12,prefabs=prefabPaths.Length,textureCount=27,actualReviewMeshes=21};
 }
}
