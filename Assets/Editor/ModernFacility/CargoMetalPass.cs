using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
public static class CargoMetalPass {
 const string Dir="Assets/Scenes/现代设施/卸货大厅材质/";
 public static void Run(){
 var scene=EditorSceneManager.OpenScene(CargoHallBuilder.ScenePath);
 Set("珍珠白墙面",new Color(.76f,.79f,.84f),.92f,.94f);Set("冷灰抛光地坪",new Color(.56f,.60f,.66f),.96f,.98f);
 RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;
 RenderSettings.customReflection=AssetDatabase.LoadAssetAtPath<Cubemap>("Assets/Game/Environment/ModernLab/Scenes/ModernLabRoomReflection.cubemap");
 var player=Object.FindFirstObjectByType<PlayerMovement>();if(player)player.gameObject.SetActive(false);
 int index=0;foreach(var probe in Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None)){
  probe.enabled=false;var go=new GameObject("capture");go.transform.position=probe.transform.position;var c=go.AddComponent<Camera>();c.enabled=false;c.nearClipPlane=.1f;c.farClipPlane=100;c.allowHDR=true;c.clearFlags=CameraClearFlags.SolidColor;c.backgroundColor=new Color(.4f,.45f,.5f);
  var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf,RenderTextureReadWrite.Linear){dimension=TextureDimension.Cube};rt.Create();
  var read=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);var old=RenderTexture.active;
  string path=Dir+"大厅金属反射_"+(index++)+".cubemap";var cube=AssetDatabase.LoadAssetAtPath<Cubemap>(path);bool created=!cube;if(created)cube=new Cubemap(256,TextureFormat.RGBAHalf,true);
  try{if(!c.RenderToCubemap(rt))throw new System.Exception("reflection capture failed");for(int f=0;f<6;f++){Graphics.SetRenderTarget(rt,0,(CubemapFace)f);read.ReadPixels(new Rect(0,0,256,256),0,0);read.Apply();cube.SetPixels(read.GetPixels(),(CubemapFace)f);}cube.Apply(true,false);if(created)AssetDatabase.CreateAsset(cube,path);else EditorUtility.SetDirty(cube);}
  finally{RenderTexture.active=old;rt.Release();Object.DestroyImmediate(rt);Object.DestroyImmediate(read);Object.DestroyImmediate(go);}
  probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;probe.intensity=1.35f;probe.enabled=true;
 }
 if(player)player.gameObject.SetActive(true);AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Debug.Log("METAL REFLECTION DONE");
 }
 static void Set(string name,Color color,float metal,float smooth){var m=AssetDatabase.LoadAssetAtPath<Material>(Dir+name+".mat");m.SetColor("_BaseColor",color);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);m.SetFloat("_EnvironmentReflections",1);m.SetFloat("_SpecularHighlights",1);m.DisableKeyword("_ENVIRONMENTREFLECTIONS_OFF");EditorUtility.SetDirty(m);}
}
