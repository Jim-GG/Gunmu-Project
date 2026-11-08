using System;using System.IO;using System.Linq;using System.Threading.Tasks;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using UnityEngine.Rendering;using UnityEngine.Rendering.Universal;
public static class CaptureTraversalV2
{
 public static async Task<object> Run()
 {
  if(Application.isPlaying)throw new Exception("Edit Mode required");string path=CrystalParkour.Editor.CrystalTraversalLabBuilder.ScenePath;var s=SceneManager.GetSceneByPath(path);bool opened=!s.isLoaded;if(opened)s=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
  var oldScene=SceneManager.GetActiveScene();SceneManager.SetActiveScene(s);var player=s.GetRootGameObjects().First(g=>g.GetComponent<CrystalParkour.CrystalTraversal>());player.SetActive(false);
  ulong old=EditorSceneManager.GetSceneCullingMask(s),mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(s,mask);
  var go=new GameObject("V2 capture",typeof(Camera));SceneManager.MoveGameObjectToScene(go,s);var c=go.GetComponent<Camera>();c.enabled=false;c.overrideSceneCullingMask=mask;c.farClipPlane=220;c.nearClipPlane=.06f;c.allowHDR=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.1f,.13f,.18f);c.GetUniversalAdditionalCameraData().renderPostProcessing=true;
  string dir="Docs/CrystalParkour/V2/Preview";Directory.CreateDirectory(dir);
  void Shot(string name,Vector3 p,Vector3 target,float fov=75)
  {go.transform.position=p;go.transform.LookAt(target);c.fieldOfView=fov;var rt=RenderTexture.GetTemporary(1440,900,24,RenderTextureFormat.ARGB32,RenderTextureReadWrite.sRGB);var previous=RenderTexture.active;var image=new Texture2D(1440,900,TextureFormat.RGB24,false,false);try{RenderPipeline.SubmitRenderRequest(c,new UniversalRenderPipeline.SingleCameraRequest{destination=rt});RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1440,900),0,0);image.Apply();File.WriteAllBytes(dir+"/"+name+".png",image.EncodeToPNG());}finally{RenderTexture.active=previous;RenderTexture.ReleaseTemporary(rt);UnityEngine.Object.DestroyImmediate(image);}}
  try{await Task.Delay(250);Shot("01_Silo",new Vector3(-2,2,-3.3f),new Vector3(1,10,0),85);Shot("02_FanDuct",new Vector3(26,24,-.3f),new Vector3(35,24,-2),83);Shot("03_CrystalBay",new Vector3(46,25,14.6f),new Vector3(43,28,28),80);Shot("04_Drones",new Vector3(53,29,27),new Vector3(60,26,30),76);Shot("00_Overview",new Vector3(70,65,-45),new Vector3(24,15,9),62);return new{captured=5,directory=dir};}
  finally{UnityEngine.Object.DestroyImmediate(go);player.SetActive(true);EditorSceneManager.SetSceneCullingMask(s,old);SceneManager.SetActiveScene(oldScene);if(opened)EditorSceneManager.CloseScene(s,true);}
 }
}
