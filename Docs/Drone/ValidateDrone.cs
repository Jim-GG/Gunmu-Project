using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
using CrystalParkour;
using FirstPerson;
public static class ValidateDrone
{
 static void Check(bool ok,string why){if(!ok)throw new Exception(why);}
 public static object Audit()
 {
  Check(!Application.isPlaying,"Edit mode required");var result=new System.Collections.Generic.List<object>();
  foreach(string path in new[]{"Assets/Game/Scene/CrystalImpactParkour.unity","Assets/Game/Scene/CrystalTraversalLab_V2.unity","Assets/Game/Scene/MCU7A_Drone_Review.unity"})
  {
   var scene=SceneManager.GetSceneByPath(path);bool opened=!scene.isLoaded;if(opened)scene=EditorSceneManager.OpenScene(path,OpenSceneMode.Additive);
   try{foreach(var d in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<TraversalDrone>(true)))
   {
    var renderers=d.GetComponentsInChildren<MeshRenderer>(true);Check(renderers.Length==3,"Unexpected renderer count in "+path);Check(d.deck&&d.rotorLeft&&d.rotorRight,"Missing collision deck or rotor in "+path);Check(renderers.SelectMany(r=>r.sharedMaterials).All(m=>m&&m.shader&&m.shader.isSupported),"Invalid material in "+path);
    Check(Vector3.Dot(d.rotorLeft.up,d.transform.up)>.999f&&Vector3.Dot(d.rotorRight.up,d.transform.up)>.999f,"Rotor axes are tilted");
    result.Add(new {scene=path,drone=d.name,route=d.route.ToString(),renderers=renderers.Length,materials=renderers.SelectMany(r=>r.sharedMaterials).Distinct().Count(),triangles=renderers.Sum(r=>(int)(Enumerable.Range(0,r.GetComponent<MeshFilter>().sharedMesh.subMeshCount).Sum(i=>(long)r.GetComponent<MeshFilter>().sharedMesh.GetIndexCount(i))/3)),deckTop=d.deck.bounds.max.y,startAnchor=d.startAnchor?d.startAnchor.name:null,endAnchor=d.endAnchor?d.endAnchor.name:null});
   }}finally{if(opened)EditorSceneManager.CloseScene(scene,true);}
  }
  var shader=Shader.Find("Null Boundary/Drone/Baked Enamel");var messages=ShaderUtil.GetShaderMessages(shader);Check(!messages.Any(m=>m.severity.ToString()=="Error"),"Shader compilation failed");
  var output=new{passed=true,drones=result,shaderMessages=messages.Select(m=>m.message).ToArray()};File.WriteAllText("Docs/Drone/asset_audit.json",Newtonsoft.Json.JsonConvert.SerializeObject(output,Newtonsoft.Json.Formatting.Indented));return output;
 }
 public static async Task<object> Live()
 {
  Check(Application.isPlaying,"Play mode required");var d=UnityEngine.Object.FindFirstObjectByType<TraversalDrone>();var p=UnityEngine.Object.FindFirstObjectByType<CrystalTraversal>();Check(d&&p,"Missing live drone/player");var input=p.GetComponent<FirstPersonInput>();bool inputEnabled=input.enabled,background=Application.runInBackground;var logs=new System.Collections.Generic.List<string>();Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)logs.Add(m);};Application.logMessageReceived+=log;
  try
  {
   Application.runInBackground=true;input.enabled=false;p.combat.Cancel();p.Drop("Drone asset validation");p.motor.StopMomentum();d.moving=false;d.route=DroneRoute.Shuttle;d.transform.position=new Vector3(180,30,180);d.Tick(0,0);d.startPoint=d.transform.position;d.endPoint=d.startPoint+Vector3.right*4;d.startAnchor=d.endAnchor=null;d.speed=1;d.dwellSeconds=0;d.hoverAmplitude=0;d.moving=true;
   typeof(TraversalDrone).GetField("phase",System.Reflection.BindingFlags.Instance|System.Reflection.BindingFlags.NonPublic).SetValue(d,0f);d.Tick(0,0);
   Physics.SyncTransforms();var b=d.deck.bounds;p.motor.Body.enabled=false;p.transform.position=new Vector3(b.center.x,b.max.y+.025f,b.center.z);p.motor.Body.enabled=true;Physics.SyncTransforms();await Task.Delay(300);
   var droneStart=d.transform.position;var playerStart=p.transform.position;var left=d.rotorLeft.localRotation;await Task.Delay(1200);
   var droneMove=d.transform.position-droneStart;var playerMove=p.transform.position-playerStart;float error=Vector3.ProjectOnPlane(droneMove-playerMove,Vector3.up).magnitude;float turn=Quaternion.Angle(left,d.rotorLeft.localRotation);
   Check(droneMove.magnitude>.3f,"Live drone did not move");Check(p.RodeDrone&&error<.16f,"Player did not ride the deck; error="+error+" foot="+p.transform.position+" deck="+d.deck.bounds+" velocity="+p.motor.Velocity+" state="+p.State);Check(turn>1,"Rotor did not rotate");
   // Verify stream hide/respawn without allowing a teleport to carry the player.
   d.enabled=false;p.motor.Body.enabled=false;p.transform.position+=Vector3.up*8;p.motor.Body.enabled=true;d.route=DroneRoute.DescendingStream;d.startPoint=d.transform.position;d.endPoint=d.startPoint+Vector3.down*4;d.speed=2;d.respawnSeconds=2;d.phaseOffset=0;
   d.Tick(.016f,0);bool visible=d.GetComponentsInChildren<Renderer>().All(r=>r.enabled)&&d.GetComponentsInChildren<Light>().All(l=>l.enabled);d.Tick(.016f,2.1);bool hidden=d.GetComponentsInChildren<Renderer>().All(r=>!r.enabled)&&d.GetComponentsInChildren<Light>().All(l=>!l.enabled)&&!d.deck.enabled;d.Tick(.016f,4.1);bool respawned=d.GetComponentsInChildren<Renderer>().All(r=>r.enabled)&&d.GetComponentsInChildren<Light>().All(l=>l.enabled)&&d.deck.enabled;
   Check(visible&&hidden&&respawned,"Stream visibility/light lifecycle failed");Check(logs.Count==0,"Runtime errors: "+string.Join(";",logs));
   var output=new{passed=true,liveSeconds=1.2f,droneTravel=droneMove.magnitude,playerTravel=playerMove.magnitude,carryHorizontalError=error,rotorRotationDegrees=turn,streamVisible=visible,streamHidden=hidden,streamRespawned=respawned,errors=logs.ToArray()};File.WriteAllText("Docs/Drone/live_validation.json",Newtonsoft.Json.JsonConvert.SerializeObject(output,Newtonsoft.Json.Formatting.Indented));return output;
  }
  finally{input.enabled=inputEnabled;Application.runInBackground=background;Application.logMessageReceived-=log;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
