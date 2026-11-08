using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using CrystalParkour;
using System.Threading.Tasks;
public static class ReviewDrone
{
 const string ScenePath="Assets/Game/Scene/MCU7A_Drone_Review.unity";
 const string Out="ArtSource/Drone/UserModel/Baked_v12/Preview/";
 public static async Task<object> Run()
 {
  if(Application.isPlaying)throw new Exception("Edit mode required");var setup=EditorSceneManager.GetSceneManagerSetup();for(int i=0;i<SceneManager.sceneCount;i++)if(SceneManager.GetSceneAt(i).isDirty)throw new Exception("Preserve dirty scenes before review capture");var s=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);SceneManager.SetActiveScene(s);
  ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(s,mask);
  try
  {
   RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.38f,.44f,.52f);RenderSettings.ambientEquatorColor=new Color(.18f,.22f,.28f);RenderSettings.ambientGroundColor=new Color(.10f,.11f,.13f);RenderSettings.reflectionIntensity=.85f;RenderSettings.fog=false;
   var cube=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/CrystalParkour/Data/CrystalParkourRoomReflection.cubemap");if(cube){RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=cube;}
   Material Mat(string name,string shader,Color colour,float smooth=0)
   {string p="Assets/Game/Environment/Drone/Materials/"+name+".mat";var mat=AssetDatabase.LoadAssetAtPath<Material>(p);if(!mat){mat=new Material(Shader.Find(shader));AssetDatabase.CreateAsset(mat,p);}mat.SetColor("_BaseColor",colour);mat.SetFloat("_Smoothness",smooth);EditorUtility.SetDirty(mat);return mat;}
   var floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.name="Review studio floor";floor.transform.localScale=Vector3.one*8;floor.GetComponent<Renderer>().sharedMaterial=Mat("Review_Floor","Universal Render Pipeline/Lit",new Color(.12f,.15f,.19f),.45f);
   var drone=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab"),s);drone.transform.position=new Vector3(0,1.1f,0);drone.GetComponent<TraversalDrone>().moving=false;
   void Lamp(string name,LightType type,Vector3 p,Vector3 target,Color color,float power)
   {var go=new GameObject(name,typeof(Light));go.transform.position=p;go.transform.LookAt(target);var l=go.GetComponent<Light>();l.type=type;l.color=color;l.intensity=power;l.range=18;l.spotAngle=90;l.shadows=LightShadows.Soft;}
   Lamp("Studio key",LightType.Directional,new Vector3(3,7,4),Vector3.zero,new Color(1,.94f,.84f),2.2f);Lamp("Studio cool fill",LightType.Point,new Vector3(-3,4,3),Vector3.zero,new Color(.72f,.84f,1),35);Lamp("Studio edge",LightType.Point,new Vector3(2,4,-4),Vector3.zero,new Color(.82f,.93f,1),40);
   var profile=ScriptableObject.CreateInstance<VolumeProfile>();var bloom=profile.Add<Bloom>(true);bloom.intensity.Override(.25f);bloom.threshold.Override(1.4f);var tone=profile.Add<Tonemapping>(true);tone.mode.Override(TonemappingMode.ACES);string pp="Assets/Game/Environment/Drone/Materials/DroneReviewPost.asset";if(AssetDatabase.LoadAssetAtPath<VolumeProfile>(pp))AssetDatabase.DeleteAsset(pp);AssetDatabase.CreateAsset(profile,pp);var vol=new GameObject("Review post effects",typeof(Volume)).GetComponent<Volume>();vol.isGlobal=true;vol.sharedProfile=profile;
   var camera=new GameObject("Drone review camera",typeof(Camera)).GetComponent<Camera>();camera.fieldOfView=42;camera.nearClipPlane=.03f;camera.farClipPlane=100;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.035f,.05f,.075f);camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.overrideSceneCullingMask=mask;
   camera.allowMSAA=true;camera.GetUniversalAdditionalCameraData().antialiasing=AntialiasingMode.SubpixelMorphologicalAntiAliasing;
   // A studio reflection is stored with the review scene. The gameplay prefab continues
   // to use its scene's normal reflection probes and environment.
   var cards=new System.Collections.Generic.List<GameObject>();
   foreach(var p in new[]{new Vector3(-3,5,2),new Vector3(3,4,1),new Vector3(0,6,-3)})
   {var card=GameObject.CreatePrimitive(PrimitiveType.Cube);card.name="Studio reflection softbox";card.transform.position=p;card.transform.LookAt(new Vector3(0,1,0));card.transform.localScale=new Vector3(2,4,.03f);card.GetComponent<Renderer>().sharedMaterial=Mat("Review_Softbox","Universal Render Pipeline/Unlit",new Color(3,3,3,1));cards.Add(card);}
   camera.transform.position=new Vector3(0,1.1f,0);camera.GetUniversalAdditionalCameraData().renderPostProcessing=false;var droneRenderers=drone.GetComponentsInChildren<Renderer>();foreach(var r in droneRenderers)r.enabled=false;await Task.Delay(350);
   var cubeRT=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf){dimension=TextureDimension.Cube};cubeRT.Create();var reflection=new Cubemap(256,TextureFormat.RGBAHalf,true);var faceImage=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);var activeRT=RenderTexture.active;
   try{camera.RenderToCubemap(cubeRT);for(int face=0;face<6;face++){Graphics.SetRenderTarget(cubeRT,0,(CubemapFace)face);faceImage.ReadPixels(new Rect(0,0,256,256),0,0);faceImage.Apply();reflection.SetPixels(faceImage.GetPixels(),(CubemapFace)face);}reflection.Apply(true,false);string reflectionPath="Assets/Game/Environment/Drone/Materials/DroneReviewReflection.cubemap";var existing=AssetDatabase.LoadAssetAtPath<Cubemap>(reflectionPath);if(existing){EditorUtility.CopySerialized(reflection,existing);UnityEngine.Object.DestroyImmediate(reflection);reflection=existing;EditorUtility.SetDirty(existing);}else AssetDatabase.CreateAsset(reflection,reflectionPath);RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.customReflectionTexture=reflection;}
   finally{RenderTexture.active=activeRT;cubeRT.Release();UnityEngine.Object.DestroyImmediate(cubeRT);UnityEngine.Object.DestroyImmediate(faceImage);foreach(var card in cards)UnityEngine.Object.DestroyImmediate(card);foreach(var r in droneRenderers)r.enabled=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;}
   DynamicGI.UpdateEnvironment();await Task.Delay(500);
   void Shot(string name,Vector3 p,Vector3 target,float fov)
   {camera.transform.position=p;camera.transform.LookAt(target);camera.fieldOfView=fov;var rt=RenderTexture.GetTemporary(1500,1000,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1500,1000,TextureFormat.RGB24,false,false);try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1500,1000),0,0);image.Apply();File.WriteAllBytes(Out+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}}
   Directory.CreateDirectory(Out);Shot("Unity_Hero",new Vector3(4.4f,3.6f,5.6f),new Vector3(0,1.15f,0),30);Shot("Unity_Rear",new Vector3(-4.5f,3.7f,-5),new Vector3(0,1.2f,0),30);Shot("Unity_Optics",new Vector3(.8f,1.2f,2.25f),new Vector3(0,.66f,.78f),37);
   camera.transform.position=new Vector3(4.4f,3.6f,5.6f);camera.transform.LookAt(new Vector3(0,1.15f,0));camera.fieldOfView=30;camera.overrideSceneCullingMask=0;EditorSceneManager.SetSceneCullingMask(s,ulong.MaxValue);EditorSceneManager.SaveScene(s,ScenePath);AssetDatabase.SaveAssets();return new {scene=ScenePath,captures=3};
  }
  finally {EditorSceneManager.RestoreSceneManagerSetup(setup);}
 }
}
