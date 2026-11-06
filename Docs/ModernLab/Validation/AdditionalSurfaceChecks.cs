using System;using System.Linq;using System.Collections.Generic;using System.IO;using System.Threading.Tasks;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
public static class AdditionalSurfaceChecks
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static async Task<object> Run()
 {
  var errors=new List<string>();Application.LogCallback record=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=record;
  var prior=SceneManager.GetActiveScene();var probes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in probes)p.enabled=false;
  var scene=EditorSceneManager.OpenScene("Assets/Game/Environment/ModernLab/Scenes/ModernLabAdditionalSurfaces.unity",OpenSceneMode.Additive);
  try
  {
   SceneManager.SetActiveScene(scene);await Task.Delay(500);var roots=scene.GetRootGameObjects();
   foreach(var pair in new[]{("吊顶_6m",6f),("吊顶_3m",3f)})
   {
    var go=roots.Single(r=>r.name==pair.Item1);var bounds=go.GetComponentInChildren<Renderer>().bounds;Check(Mathf.Abs(bounds.size.x-pair.Item2)<.01f&&Mathf.Abs(bounds.size.z-pair.Item2)<.01f,"Ceiling module scale is wrong: "+pair.Item1);
   }
   foreach(var colour in new[]{"Lime","Red","Yellow"})
   {
    var go=roots.Single(r=>r.name=="分区墙_"+colour);var r=go.GetComponentInChildren<Renderer>();Check(r.sharedMaterials.Length==2&&r.sharedMaterials.Any(m=>m.name=="Lab_Paint_"+colour),"Colour band material slots missing: "+colour);Check(go.GetComponentInChildren<MeshFilter>().sharedMesh.subMeshCount==2,"Colour band is not separately editable");
   }
   var floor=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Floor_Checker.mat");Check(roots.Where(r=>r.name=="平面.018"||r.name=="平面.019").All(g=>g.GetComponentInChildren<Renderer>().sharedMaterials.Contains(floor)),"Grey/white floor is not on the actual meshes");
   var ribbed=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Wall_Ribbed.mat");Check(roots.Single(r=>r.name=="平面.014").GetComponentInChildren<Renderer>().sharedMaterials.Contains(ribbed),"Ribbed wall preview missing");
   var camera=roots.Select(g=>g.GetComponent<Camera>()).First(c=>c);var rt=RenderTexture.GetTemporary(1600,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;Texture2D image=null;
   try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1600,1000,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1600,1000),0,0);image.Apply();File.WriteAllBytes("ArtSource/ModernLab/Preview/ModernLab_UnityAdditionalSurfaces.png",image.EncodeToPNG());}
   finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
  }
  finally
  {
   SceneManager.SetActiveScene(prior);foreach(var g in scene.GetRootGameObjects()){if(g.TryGetComponent<ReflectionProbe>(out var p))p.enabled=false;if(g.TryGetComponent<Camera>(out var c))c.enabled=false;}await Task.Delay(500);EditorSceneManager.CloseScene(scene,true);foreach(var p in probes)if(p)p.enabled=true;await Task.Delay(250);Application.logMessageReceived-=record;
  }
  Check(errors.Count==0,string.Join(";",errors.Distinct()));return new{passed=true,ceilingMetres=new[]{6,3},separateColourBands=new[]{"Lime","Red","Yellow"},greyWhiteChecker=true,ribbedNormal=true,renderErrors=0};
 }
}
