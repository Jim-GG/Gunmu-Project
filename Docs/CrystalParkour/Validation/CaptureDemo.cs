using System;using System.IO;using System.Linq;using System.Threading.Tasks;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
public static class CaptureCrystalDemo
{
 public static async Task<object> Run()
 {
  if(Application.isPlaying)throw new Exception("Capture in Edit Mode");string path=CrystalParkour.Editor.CrystalParkourBuilder.ScenePath;var scene=SceneManager.GetSceneByPath(path);if(!scene.isLoaded)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
  var player=scene.GetRootGameObjects().FirstOrDefault(g=>g.GetComponent<CrystalParkour.CrystalTraversal>());bool playerActive=player&&player.activeSelf;if(player)player.SetActive(false);
  ulong oldMask=EditorSceneManager.GetSceneCullingMask(scene);ulong mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);var go=new GameObject("Temporary demo capture",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);var camera=go.GetComponent<Camera>();camera.overrideSceneCullingMask=mask;camera.nearClipPlane=.05f;camera.farClipPlane=180;camera.allowHDR=true;camera.GetUniversalAdditionalCameraData().renderPostProcessing=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.065f,.08f,.12f);var dir="Docs/CrystalParkour/Validation/Preview";Directory.CreateDirectory(dir);
  void Shot(string name,Vector3 position,Vector3 target,float fov)
  {go.transform.position=position;go.transform.LookAt(target);camera.fieldOfView=fov;var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;Texture2D image=null;try{RenderPipeline.SubmitRenderRequest(camera,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image=new Texture2D(1440,900,TextureFormat.RGB24,false,false);image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(dir+"/"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);if(image)UnityEngine.Object.DestroyImmediate(image);}}
  try{await Task.Delay(500);Shot("01_Entry",new Vector3(0,1.8f,.7f),new Vector3(0,1.8f,9),70);Shot("02_WindShaft",new Vector3(-3,6,12),new Vector3(1,4.5f,29),75);Shot("03_Drone",new Vector3(4,8,43),new Vector3(0,5,53),65);return new{captured=3,path};}
  finally{UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.SetSceneCullingMask(scene,oldMask);if(player)player.SetActive(playerActive);}
 }
}
