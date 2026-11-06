using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
namespace ModernLab.Editor
{
 public static class ModernLabSurfaceInspection
 {
  [Serializable]class Export{public string source,fbx;}
  [Serializable]class Build{public Export[] exports;}
  [Serializable]class Lamp{public string name;public float[] position,target,size;public float energy;}
  [Serializable]class Shot{public string name;public float[] position,target;public float fov;}
  [Serializable]class Rig{public Lamp[] lights;public Shot[] shots;}
  static Vector3 P(float[] v)=>new Vector3(-v[0],v[2],-v[1]);
  public static async Task<object> Capture()
  {
   var rig=JsonUtility.FromJson<Rig>(File.ReadAllText("ArtSource/ModernLab/surface_inspection_layout.json"));var exports=JsonUtility.FromJson<Build>(File.ReadAllText("ArtSource/ModernLab/build_report.json")).exports.ToDictionary(x=>x.source,x=>x.fbx);
   const string path="Assets/Game/Environment/ModernLab/Scenes/ModernLabSurfaceInspection.unity";var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(path);bool created=!scene.isLoaded;
   var existing=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled&&p.gameObject.scene.path!=path).ToArray();foreach(var p in existing)p.enabled=false;
   var errors=new List<string>();Application.LogCallback record=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=record;
   Camera camera=null;ulong previousMask=0,previousOverride=0;bool maskChanged=false;
   try
   {
    if(created)
    {
     scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
     void Place(string source,string label,float[] position,Material mat=null)
     {
      var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/ModernLab/Prefabs/"+Path.GetFileNameWithoutExtension(exports[source])+".prefab");var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.name=label;go.transform.position=P(position);foreach(var t in go.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;if(mat)foreach(var r in go.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>mat).ToArray();
     }
     Place("分区墙_Red","Rough red and white wall",new[]{-3.1f,0,0});Place("分区墙_Yellow","Gloss white and yellow wall",new[]{0f,0,0});Place("6*1.2护栏","Painted metal actual guardrail",new[]{4.5f,3,0},AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Paint_WhiteSteel.mat"));Place("平面","Actual resin floor",new[]{-.5f,-1,-.05f});
     var glow=AssetDatabase.LoadAssetAtPath<Material>("Assets/Game/Environment/ModernLab/Materials/Preview_Light.mat");
     foreach(var lamp in rig.lights)
     {
      var go=new GameObject(lamp.name,typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.layer=30;go.transform.position=P(lamp.position);go.transform.LookAt(P(lamp.target));var light=go.GetComponent<Light>();light.type=LightType.Spot;light.intensity=lamp.energy*.08f;light.range=12;light.spotAngle=110;light.innerSpotAngle=90;light.shadows=LightShadows.Soft;light.cullingMask=1<<30;
      var emitter=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(emitter,scene);emitter.name=lamp.name+" reflection shape";emitter.layer=30;emitter.transform.SetPositionAndRotation(go.transform.position,go.transform.rotation);emitter.transform.localScale=new Vector3(lamp.size[0],lamp.size[1],.008f);emitter.GetComponent<Renderer>().sharedMaterial=glow;UnityEngine.Object.DestroyImmediate(emitter.GetComponent<Collider>());
     }
     RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.22f,.24f,.26f);RenderSettings.reflectionIntensity=1;
     var cameraGo=new GameObject("Surface inspection camera",typeof(Camera));SceneManager.MoveGameObjectToScene(cameraGo,scene);cameraGo.layer=30;camera=cameraGo.GetComponent<Camera>();camera.nearClipPlane=.03f;camera.farClipPlane=60;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.08f,.09f,.10f);camera.allowHDR=true;camera.cullingMask=1<<30;var extra=camera.GetUniversalAdditionalCameraData();extra.renderPostProcessing=true;extra.volumeLayerMask=1<<30;extra.antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
     var colour=new GameObject("Inspection colour",typeof(Volume));SceneManager.MoveGameObjectToScene(colour,scene);colour.layer=30;var volume=colour.GetComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=AssetDatabase.LoadAssetAtPath<VolumeProfile>("Assets/Game/Environment/ModernLab/Scenes/ModernLabPreviewVolume.asset");
     var pg=new GameObject("Inspection room reflection",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(pg,scene);pg.layer=30;pg.transform.position=new Vector3(-.2f,1.6f,.5f);var probe=pg.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.size=new Vector3(22,12,14);probe.boxProjection=true;probe.cullingMask=1<<30;probe.intensity=.35f;probe.customBakedTexture=ModernLabMaterialSetup.CaptureRoom(scene,pg.transform.position,"SurfaceV2InspectionReflection");probe.enabled=true;
    }
    else{SceneManager.SetActiveScene(scene);camera=scene.GetRootGameObjects().Select(g=>g.GetComponent<Camera>()).First(c=>c);}
    previousMask=EditorSceneManager.GetSceneCullingMask(scene);previousOverride=camera.overrideSceneCullingMask;ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);camera.overrideSceneCullingMask=mask;maskChanged=true;
    await Task.Delay(500);
    var originalPosition=camera.transform.position;var originalRotation=camera.transform.rotation;float originalFov=camera.fieldOfView;
    foreach(var shot in rig.shots)
    {
     camera.transform.position=P(shot.position);camera.transform.LookAt(P(shot.target));camera.fieldOfView=Camera.HorizontalToVerticalFieldOfView(shot.fov,1280f/960);
     var rt=RenderTexture.GetTemporary(1280,960,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;Texture2D image=null;
     try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1280,960,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1280,960),0,0);image.Apply();File.WriteAllBytes("ArtSource/ModernLab/Preview/Surface_Unity_V2_"+shot.name+".png",image.EncodeToPNG());}
     finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}
    }
    if(created){var first=rig.shots[0];camera.transform.position=P(first.position);camera.transform.LookAt(P(first.target));camera.fieldOfView=Camera.HorizontalToVerticalFieldOfView(first.fov,1280f/960);camera.overrideSceneCullingMask=0;EditorSceneManager.SaveScene(scene,path);}
    else{camera.transform.SetPositionAndRotation(originalPosition,originalRotation);camera.fieldOfView=originalFov;}
   }
   finally
   {
    if(maskChanged){EditorSceneManager.SetSceneCullingMask(scene,previousMask);if(camera)camera.overrideSceneCullingMask=previousOverride;}SceneManager.SetActiveScene(prior);if(created){foreach(var g in scene.GetRootGameObjects()){if(g.TryGetComponent<Camera>(out var c))c.enabled=false;if(g.TryGetComponent<ReflectionProbe>(out var p))p.enabled=false;}await Task.Delay(500);EditorSceneManager.CloseScene(scene,true);}foreach(var p in existing)if(p)p.enabled=true;await Task.Delay(250);Application.logMessageReceived-=record;
   }
   if(errors.Count>0)throw new Exception(string.Join("; ",errors.Distinct()));return new{passed=true,path,shots=rig.shots.Select(s=>s.name).ToArray(),errors=0};
  }
 }
}
