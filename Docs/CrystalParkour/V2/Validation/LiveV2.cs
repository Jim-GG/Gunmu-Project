using System;using System.IO;using System.Linq;using System.Reflection;using System.Threading.Tasks;using System.Collections.Generic;using UnityEngine;using FirstPerson;using CrystalParkour;
public static class TraversalV2Live
{
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play Mode required");var p=UnityEngine.Object.FindFirstObjectByType<CrystalTraversal>();var input=p.motor.input;var combat=p.combat;var motor=p.motor;bool enabled=input.enabled,background=Application.runInBackground;Application.runInBackground=true;var errors=new List<string>();Application.LogCallback log=(m,s,t)=>{if(t==LogType.Error||t==LogType.Exception)errors.Add(m);};Application.logMessageReceived+=log;
  void Move(Vector2 value)=>typeof(FirstPersonInput).GetProperty("Move").SetValue(input,value);
  void Place(Vector3 pos){p.Drop("Live validation");motor.Body.enabled=false;p.transform.position=pos;motor.Body.enabled=true;motor.StopMomentum();motor.SetLookRotation(Quaternion.identity);Physics.SyncTransforms();}
  try
  {
   input.enabled=false;Move(Vector2.zero);combat.Cancel();combat.InitializePipeline();await Task.Delay(100);
   var bone=combat.animationPlayer.animator.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");var initial=bone.localRotation;combat.Press();await Task.Delay(600);float frame=combat.CurrentFrame;float angle=Quaternion.Angle(initial,bone.localRotation);if(combat.State!=GauntletCombat.Phase.Charging||frame<13||angle<.1f)throw new Exception("Gauntlet animation failed");combat.Release();await Task.Delay(1500);if(combat.State!=GauntletCombat.Phase.Idle)throw new Exception("Gauntlet did not return idle");
   Place(new Vector3(0,10,0));Move(Vector2.right);float startX=p.transform.position.x;await Task.Delay(450);float steeringDistance=p.transform.position.x-startX;if(steeringDistance<.3f||motor.Velocity.y>=0)throw new Exception("Live airflow steering failed");Move(Vector2.zero);
   var rotor=UnityEngine.Object.FindFirstObjectByType<WindFanRotor>();var rotation=rotor.transform.localRotation;await Task.Delay(120);float rotorAngle=Quaternion.Angle(rotation,rotor.transform.localRotation);if(rotorAngle<1)throw new Exception("Fan blades not rotating");
   Directory.CreateDirectory("Docs/CrystalParkour/V2/Preview");p.Respawn();motor.SetLookRotation(Quaternion.Euler(-18,40,0));await Task.Delay(180);ScreenCapture.CaptureScreenshot("Docs/CrystalParkour/V2/Preview/05_LiveGame.png");await Task.Delay(250);
   var result=new{passed=errors.Count==0,chargeFrame=frame,gauntletBoneRotation=angle,liveAirSteeringMetres=steeringDistance,fanRotationDegrees=rotorAngle,droneCount=UnityEngine.Object.FindObjectsByType<TraversalDrone>(FindObjectsSortMode.None).Length,errors=errors.ToArray()};File.WriteAllText("Docs/CrystalParkour/V2/Validation/LiveReport.json",Newtonsoft.Json.JsonConvert.SerializeObject(result,Newtonsoft.Json.Formatting.Indented));if(errors.Count>0)throw new Exception(string.Join(";",errors));return result;
  }
  finally{Move(Vector2.zero);combat.Cancel();p.Respawn();input.enabled=enabled;Application.runInBackground=background;Application.logMessageReceived-=log;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;}
 }
}
