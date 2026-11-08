using System;using System.IO;using System.Linq;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using CrystalParkour;
public static class CaptureUV13
{
 public static Task<object> Before()=>Capture("Before");public static Task<object> After()=>Capture("After");
 static async Task<object> Capture(string label)
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");var old=SceneManager.GetActiveScene();string path="Assets/Game/Scene/MCU7A_Drone_Review.unity";var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);
  var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First();var drone=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TraversalDrone>(true)).First();var visual=drone.GetComponentsInChildren<Transform>(true).First(t=>t.name=="MCU7A game visual");var pos=camera.transform.position;var rot=camera.transform.rotation;float fov=camera.fieldOfView;ulong oldMask=EditorSceneManager.GetSceneCullingMask(scene),oldCameraMask=camera.overrideSceneCullingMask,mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);camera.overrideSceneCullingMask=mask;string output="ArtSource/Drone/UserModel/Baked_v13/Preview/";
  Vector3 World(Vector3 source)=>visual.TransformPoint(new Vector3(source.x/3,(source.z+.75f)/3,source.y/3));
  void Shot(string name,Vector3 p,Vector3 target,float lens)
  {
   camera.transform.position=World(p);camera.transform.LookAt(World(target));camera.fieldOfView=2*Mathf.Atan(36/(lens*1.4f*2))*Mathf.Rad2Deg;
   var rt=RenderTexture.GetTemporary(1400,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var oldRT=RenderTexture.active;var image=new Texture2D(1400,1000,TextureFormat.RGB24,false,false);
   try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1400,1000),0,0);image.Apply();File.WriteAllBytes(output+"Unity_"+label+"_"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=oldRT;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}
  }
  try{await Task.Delay(300);Shot("SideText",new Vector3(-3.6f,-1.3f,-.92f),new Vector3(-1.02f,-.5f,-1.3f),68);Shot("RoofText",new Vector3(1,-2.4f,3.6f),new Vector3(0,-.2f,.35f),70);return new{label,captures=2,output};}
  finally{camera.transform.SetPositionAndRotation(pos,rot);camera.fieldOfView=fov;camera.overrideSceneCullingMask=oldCameraMask;EditorSceneManager.SetSceneCullingMask(scene,oldMask);SceneManager.SetActiveScene(old);if(opened)EditorSceneManager.CloseScene(scene,true);}
 }
 public static object Reimport()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");string root="Assets/Game/Environment/Drone/";
  foreach(string map in new[]{"BaseColor","Normal","Mask","Emission"})AssetDatabase.ImportAsset(root+"Textures/Drone_"+map+".png",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
  AssetDatabase.ImportAsset(root+"Models/MCU7A_Drone.fbx",ImportAssetOptions.ForceUpdate|ImportAssetOptions.ForceSynchronousImport);
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(root+"Prefabs/Drone_MCU7A.prefab");var renderers=prefab.GetComponentsInChildren<MeshRenderer>(true);int triangles=renderers.Sum(r=>(int)(Enumerable.Range(0,r.GetComponent<MeshFilter>().sharedMesh.subMeshCount).Sum(i=>(long)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(i))/3));
  if(triangles!=12407||renderers.Length!=3)throw new Exception("Unexpected geometry change");
  var maps=new[]{"BaseColor","Normal","Mask","Emission"}.Select(n=>{string path=root+"Textures/Drone_"+n+".png";var t=AssetDatabase.LoadAssetAtPath<Texture2D>(path);if(t.width!=4096||t.height!=4096)throw new Exception("Resolution changed: "+n);return new{name=n,width=t.width,height=t.height,guid=AssetDatabase.AssetPathToGUID(path),format=t.format.ToString()};}).ToArray();
  var shader=Shader.Find("Null Boundary/Drone/Baked Enamel");var messages=ShaderUtil.GetShaderMessages(shader);if(messages.Any(m=>m.severity.ToString()=="Error"))throw new Exception("Shader error");
  var report=new{passed=true,triangles,renderers=renderers.Length,materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count(),maps,shaderMessages=messages.Select(m=>m.message).ToArray(),prefabRebuilt=false};File.WriteAllText("ArtSource/Drone/UserModel/Baked_v13/unity_import_report.json",Newtonsoft.Json.JsonConvert.SerializeObject(report,Newtonsoft.Json.Formatting.Indented));return report;
 }
}
