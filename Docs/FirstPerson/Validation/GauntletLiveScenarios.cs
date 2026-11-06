using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using FirstPerson;using GameActions;using Resonator.AI;
public static class GauntletLiveScenarios
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 public static async Task<object> Run()
 {
  Check(Application.isPlaying,"Enter Play Mode first");bool background=Application.runInBackground;Application.runInBackground=true;
  var enemies=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);var statuses=enemies.Select(e=>e.enabled).ToArray();foreach(var e in enemies){e.enabled=false;e.motor.Stop();}
  GameObject go=null,expected=null;ResonatorEnemy enemy=null;var cases=new List<string>();
  try
  {
   go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab"),new Vector3(60,0,60),Quaternion.identity);
   go.GetComponent<FirstPersonInput>().enabled=false;go.GetComponent<FirstPersonMotor>().enabled=false;
   foreach(var camera in go.GetComponentsInChildren<Camera>())camera.enabled=false;foreach(var a in go.GetComponentsInChildren<AudioListener>())a.enabled=false;
   var c=go.GetComponent<GauntletCombat>();c.InitializePipeline();c.Cancel();var animator=c.animationPlayer.animator;
   Check(animator.runtimeAnimatorController==null,"Legacy Animator Controller still owns the native update");cases.Add("Native Animator Controller is suspended while the shared graph owns animation");
   var hand=animator.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");var cameraBone=animator.GetComponentsInChildren<Transform>().First(t=>t.name=="camera_target");
   Quaternion idleHand=hand.localRotation,idleCamera=cameraBone.localRotation;
   c.Press();await Task.Delay(300);Check(c.State==GauntletCombat.Phase.Charging&&c.CurrentFrame>2,"Charge did not advance in actual Update frames");
   float chargeAngle=Quaternion.Angle(idleHand,hand.localRotation);Check(chargeAngle>.5f,"Native updates overwrote charge pose with Idle");cases.Add("Held charge visibly changes bones across actual engine updates");
   c.Release();await Task.Delay(95);Check(c.State==GauntletCombat.Phase.Punching,"Punch did not start from charge release");float frame=c.CurrentFrame;
   expected=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Gauntlet/Prefabs/GauntletViewModel.prefab"));
   foreach(var b in expected.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;var ea=expected.GetComponentInChildren<Animator>();ea.runtimeAnimatorController=null;
   c.actions.Current.Motion.track.clip.SampleAnimation(ea.gameObject,frame/c.actions.Current.Motion.track.framesPerSecond);
   var handExpected=expected.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");float poseError=Quaternion.Angle(hand.localRotation,handExpected.localRotation);
   Check(poseError<.1f,"Real rendered bone was overwritten or misbound: "+poseError);cases.Add("Punch pose survives native animation evaluation and matches the original FBX at its source frame");
   await Task.Delay(850);Check(c.State==GauntletCombat.Phase.Idle,"Live punch did not return to Idle");
   c.BeginBlock();await Task.Delay(400);Check(c.State==GauntletCombat.Phase.ParryKeep,"Live guard did not loop");c.EndBlock();await Task.Delay(350);Check(c.State==GauntletCombat.Phase.Idle,"Live holster did not finish");cases.Add("Guard enter/loop/exit advance in the real player loop");
   c.Cancel();c.actions.motion.ReleaseGraph();c.Press();c.Release();await Task.Delay(80);Check(c.State==GauntletCombat.Phase.Punching&&c.CurrentFrame>10,"Graph recreation lost the live action");cases.Add("Destroyed graph can be recreated without invalid Playable handles");
   // Actual enemy adapter, original shared shape queries, actual player Update.
   go.transform.position=new Vector3(0,0,0);go.transform.rotation=Quaternion.identity;Physics.SyncTransforms();
   var eg=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"));enemy=eg.GetComponent<ResonatorEnemy>();enemy.enabled=false;enemy.Initialize(RestActivity.FeignDeath);enemy.motor.agent.Warp(new Vector3(0,0,.9f));enemy.transform.rotation=Quaternion.Euler(0,180,0);Physics.SyncTransforms();float hp=enemy.Health;
   c.Cancel();c.Press();c.Release();await Task.Delay(300);Check(enemy.Health<hp,"Live actual enemy was not damaged; state="+c.State+" frame="+c.CurrentFrame+" playerHP="+c.GetComponent<FirstPersonVitals>().Health+" enemy="+enemy.Health);cases.Add("Real Update punches damage the actual Resonator adapter");
   return new{passed=true,cases=cases.ToArray(),nativePoseAngleError=poseError,chargeBoneRotation=chargeAngle,punchFrame=frame};
  }
  finally
  {
   if(enemy){enemy.motion.StopForRagdoll();UnityEngine.Object.DestroyImmediate(enemy.gameObject);}if(expected)UnityEngine.Object.DestroyImmediate(expected);
   if(go){var c=go.GetComponent<GauntletCombat>();c.Cancel();if(c.actions)c.actions.motion.ReleaseGraph();UnityEngine.Object.DestroyImmediate(go);}
   for(int i=0;i<enemies.Length;i++)if(enemies[i])enemies[i].enabled=statuses[i];Application.runInBackground=background;
  }
 }
}
