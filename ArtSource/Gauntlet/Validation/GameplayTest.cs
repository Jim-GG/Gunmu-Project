using System;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEditor;
using FirstPerson;
public static class GameplayTest
{
 static void Check(bool condition,string message){if(!condition)throw new Exception(message);}
 public static object Assets()
 {
  var p=new ChargePolicy();
  foreach(int frame in new[]{13,31,32,61,62,63,91,92,93})Check(p.Punch(frame/30f)==(frame<62?2:frame<92?3:4),"Boundary "+frame);
  Check(p.Punch(.1f)==1,"Quick click");Check(p.Tier(32/30f)==1&&p.Tier(62/30f)==2&&p.Tier(92/30f)==3,"Tier boundaries");
  var c=AssetDatabase.LoadAllAssetsAtPath("Assets/备份拳套/Gauntlet/Animations/Gauntlet_charge_full.fbx").OfType<AnimationClip>().First(a=>a.name=="charge full");
  Check(Mathf.Abs(c.length-50/30f)<.001f&&c.isLooping,"Full clip range/loop");
  var combat=UnityEngine.Object.FindFirstObjectByType<GauntletCombat>();
  var bindings=AnimationUtility.GetCurveBindings(c);Check(bindings.All(b=>b.path==""||combat.animationPlayer.animator.transform.Find(b.path)),"Unbound full animation path");
  return new{passed=true,boundaries=9,fullSeconds=c.length,fullLoops=c.isLooping,fullCurves=bindings.Length};
 }
 public static async Task<object> Runtime()
 {
  Check(Application.isPlaying,"Run preview first");
  var combat=UnityEngine.Object.FindFirstObjectByType<GauntletCombat>();var input=combat.GetComponent<FirstPersonInput>();var motor=combat.GetComponent<FirstPersonMotor>();
  bool bg=Application.runInBackground;Application.runInBackground=true;input.enabled=false;motor.enabled=false;combat.enabled=false;
  var hits=new List<int>();Action<int> listen=n=>hits.Add(n);combat.AttackStarted+=listen;var animator=combat.animationPlayer.animator;
  try
  {
   combat.Cancel();combat.Press();combat.Release();Check(hits.Last()==1,"Quick punch");
   combat.Cancel();combat.Press();combat.Tick(.2f);combat.Release();Check(combat.State==GauntletCombat.Phase.ReleasePending,"Early release must queue");combat.Tick(.24f);Check(hits.Last()==2,"Queued punch2");
   foreach(int f in new[]{13,61,62,91,92}){combat.Cancel();combat.Press();combat.Tick(f/30f);combat.Release();Check(hits.Last()==(f<62?2:f<92?3:4),"Release "+f);}
   combat.Cancel();combat.Press();combat.Tick(92/30f);animator.Update(.1f);Check(combat.State==GauntletCombat.Phase.Full&&animator.GetCurrentAnimatorStateInfo(0).IsName("charge full"),"Full hold state");animator.Update(4);combat.Tick(4);Check(combat.State==GauntletCombat.Phase.Full,"Full must remain held");combat.Release();for(int step=0;step<40;step++){animator.Update(1/30f);combat.Tick(1/30f);}Check(combat.State==GauntletCombat.Phase.Idle,"Return idle: "+combat.State+" normalized="+animator.GetCurrentAnimatorStateInfo(0).normalizedTime);
   var t=combat.transform;Vector3 position=t.position;Quaternion rotation=t.rotation,pivot=motor.viewPivot.localRotation;
   motor.enabled=true;typeof(FirstPersonInput).GetProperty("Move").SetValue(input,Vector2.up);
   await Task.Delay(200);float moved=Vector3.Distance(position,t.position);Check(moved>.02f,"Movement did not move controller");
   typeof(FirstPersonInput).GetProperty("Move").SetValue(input,Vector2.zero);
   typeof(FirstPersonInput).GetProperty("Look").SetValue(input,new Vector2(10,0));typeof(FirstPersonMotor).GetMethod("Update",System.Reflection.BindingFlags.NonPublic|System.Reflection.BindingFlags.Instance).Invoke(motor,null);typeof(FirstPersonInput).GetProperty("Look").SetValue(input,Vector2.zero);
   Check(Quaternion.Angle(rotation,t.rotation)>.5f,"Mouse look");
   var cc=combat.GetComponent<CharacterController>();cc.enabled=false;t.SetPositionAndRotation(position,rotation);motor.viewPivot.localRotation=pivot;cc.enabled=true;
   return new{passed=true,attackSequence=hits.ToArray(),fullHeldAcrossLoops=true,walkDistance=moved};
  }
  finally{combat.AttackStarted-=listen;combat.Cancel();combat.enabled=true;motor.enabled=true;input.enabled=true;Application.runInBackground=bg;}
 }
}

