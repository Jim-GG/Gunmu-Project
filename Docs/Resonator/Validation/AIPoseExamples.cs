using System;using System.Linq;using System.IO;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class AIPoseExamples
{
 static void Capture(Camera camera,string filename,Vector3 focus)
 {
  camera.transform.position=focus+new Vector3(2.8f,1.6f,3.3f);camera.transform.LookAt(focus);var rt=new RenderTexture(1000,800,24);camera.targetTexture=rt;camera.Render();var old=RenderTexture.active;RenderTexture.active=rt;var image=new Texture2D(1000,800,TextureFormat.RGB24,false);image.ReadPixels(new Rect(0,0,1000,800),0,0);image.Apply();File.WriteAllBytes("Assets/Game/Enemies/Resonator/AI/Validation/"+filename+".png",image.EncodeToPNG());RenderTexture.active=old;camera.targetTexture=null;UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(rt);
 }
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Start arena");bool bg=Application.runInBackground;Application.runInBackground=true;
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var e in originals){e.enabled=false;e.motor.Stop();}
  var originalRenderers=originals.SelectMany(e=>e.GetComponentsInChildren<Renderer>()).Where(r=>r.enabled).ToArray();foreach(var r in originalRenderers)r.enabled=false;
  var actorGo=new GameObject("Pose target");actorGo.transform.position=new Vector3(0,0,15);var actor=actorGo.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;
  var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"),Vector3.zero,Quaternion.identity);var enemy=go.GetComponent<ResonatorEnemy>();enemy.enabled=false;enemy.target=actor;
  var cameraGo=new GameObject("AI Pose Camera");var camera=cameraGo.AddComponent<Camera>();camera.enabled=false;camera.orthographic=true;camera.orthographicSize=1.35f;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.07f,.08f,.10f);camera.nearClipPlane=.02f;
  try
  {
   enemy.Initialize(RestActivity.FeignDeath);await Task.Delay(70);Capture(camera,"Rest_FeignDeath",new Vector3(0,.5f,0));
   enemy.Initialize(RestActivity.Feeding);await Task.Delay(70);Capture(camera,"Rest_Feeding",new Vector3(0,.7f,0));
   enemy.Initialize(RestActivity.Resonating);enemy.Tick(37/30f);await Task.Delay(70);Capture(camera,"Rest_Resonating",new Vector3(0,.95f,0));
   enemy.Initialize(RestActivity.SlowIdle);enemy.motion.Play(enemy.tuning.run,10,true);await Task.Delay(70);Capture(camera,"Combat_Running",new Vector3(0,.95f,0));
   enemy.motion.Play(enemy.tuning.kick.animation,45,true);await Task.Delay(70);Capture(camera,"Combat_Kicking",new Vector3(0,.95f,0));
   enemy.TakeDamage(10000,Vector3.forward*2,2);enemy.Tick(50.1f/30);await Task.Delay(800);Vector3 centre=enemy.ragdoll.bodies.Select(b=>b.position).Aggregate(Vector3.zero,(a,b)=>a+b)/enemy.ragdoll.bodies.Length;await Task.Delay(70);Capture(camera,"Death_Ragdoll",centre);
   return new{saved=6,ragdoll=enemy.ragdoll.Active};
  }
  finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(cameraGo);UnityEngine.Object.DestroyImmediate(actorGo);foreach(var r in originalRenderers)if(r)r.enabled=true;foreach(var e in originals)if(e)e.enabled=true;Application.runInBackground=bg;}
 }
}

