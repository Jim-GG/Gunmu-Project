using System;using System.IO;using System.Linq;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using FirstPerson;using CrystalParkour;
public static class CrystalLiveSmoke
{
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Enter the demo Play Mode first");var p=UnityEngine.Object.FindFirstObjectByType<CrystalTraversal>();if(!p)throw new Exception("Missing live player");var input=p.GetComponent<FirstPersonInput>();var c=p.combat;bool enabled=input.enabled,background=Application.runInBackground;Application.runInBackground=true;var errors=new System.Collections.Generic.List<string>();Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=log;
  try
  {
   input.enabled=false;c.Cancel();c.InitializePipeline();await Task.Delay(100);var bone=c.animationPlayer.animator.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");var start=bone.localRotation;c.Press();await Task.Delay(650);float frame=c.CurrentFrame;float rotation=Quaternion.Angle(start,bone.localRotation);if(c.State!=GauntletCombat.Phase.Charging||frame<13||rotation<.1f)throw new Exception("Live charge animation failed: "+c.State+"/"+frame+"/"+rotation);c.Release();await Task.Delay(90);if(c.State!=GauntletCombat.Phase.Punching)throw new Exception("Live release did not punch");await Task.Delay(1200);if(c.State!=GauntletCombat.Phase.Idle)throw new Exception("Live punch did not return idle");
   var camera=Camera.main;if(!camera)throw new Exception("Missing live main camera");camera.farClipPlane=180;Directory.CreateDirectory("Docs/CrystalParkour/Validation/Preview");ScreenCapture.CaptureScreenshot("Docs/CrystalParkour/Validation/Preview/04_LiveGame.png");await Task.Delay(500);
   var result=new{passed=errors.Count==0,sourceFrame=frame,chargeBoneRotation=rotation,playerHeight=p.transform.position.y,drone=UnityEngine.Object.FindFirstObjectByType<TraversalDrone>().transform.position.ToString(),errors=errors.ToArray()};File.WriteAllText("Docs/CrystalParkour/Validation/LiveReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));if(errors.Count>0)throw new Exception(string.Join(";",errors));return result;
  }
  finally{c.Cancel();input.enabled=enabled;Application.runInBackground=background;Application.logMessageReceived-=log;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
