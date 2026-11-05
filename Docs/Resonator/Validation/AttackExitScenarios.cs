using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class AttackExitScenarios
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 public static object Run()
 {
  Check(Application.isPlaying,"Start play mode");
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var e in originals){e.enabled=false;e.motor.Stop();}
  var actorGo=new GameObject("Attack exit target");var actor=actorGo.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;
  var collider=actorGo.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.3f;collider.center=Vector3.up*.9f;
  var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonator/AI/Prefabs/ResonatorAgent.prefab"));var enemy=go.GetComponent<ResonatorEnemy>();enemy.enabled=false;enemy.target=actor;
  var profile=UnityEngine.Object.Instantiate(enemy.tuning);enemy.tuning=profile;profile.recoverySeconds=Vector2.one*.6f;
  foreach(var a in new[]{profile.attack,profile.punch,profile.headbutt,profile.kick}){a.animation.speed=1;a.rootMotionScale=0;a.hits=new[]{new HitWindow{startFrame=20,endFrame=23,anchor=HitboxAnchor.Root,position=new Vector3(0,1,1),size=Vector3.one*.6f}};}
  var start=typeof(ResonatorEnemy).GetMethod("BeginAttack",BindingFlags.Instance|BindingFlags.NonPublic);
  Action<EnemyState,bool> enter=(state,near)=>{go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);actorGo.transform.position=new Vector3(0,0,near?1.05f:9);enemy.Initialize(RestActivity.FeignDeath);start.Invoke(enemy,new object[]{state,true});Physics.SyncTransforms();};
  var cases=new List<string>();
  try
  {
   var scream=profile.Transition(EnemyState.Attack,EnemyState.Screaming);var idle=profile.Transition(EnemyState.Punching,EnemyState.CombatIdle);
   Check(scream!=null&&!scream.anySource&&idle!=null&&idle.anySource,"Missing exact Attack -> Screaming or shared Any -> Combat Idle");
   enter(EnemyState.Attack,true);enemy.Tick(19/30f);Check(enemy.State==EnemyState.Attack,"Wind-up exited before hit");enemy.Tick(1.01f/30);
   Check(enemy.State==EnemyState.Screaming,"Attack hit must immediately scream");Check(Mathf.Abs(enemy.motion.BlendSeconds-scream.blend)<.0001f&&Mathf.Abs(enemy.CurrentFrame-scream.entryFrame)<.001f,"Authored scream blend/entry lost");cases.Add("Attack hit exits immediately using authored Screaming blend and entry");
   enter(EnemyState.Attack,false);enemy.Tick(22/30f);Check(enemy.State==EnemyState.Attack,"Miss exit before final active window closes");enemy.Tick(1.01f/30);Check(enemy.State==EnemyState.CombatIdle,"Attack miss must enter recovery without full clip tail");Check(Mathf.Abs(enemy.motion.BlendSeconds-idle.blend)<.0001f,"Any idle blend ignored");enemy.Tick(.59f);Check(enemy.State==EnemyState.CombatIdle,"Recovery timer skipped");enemy.Tick(.02f);Check(enemy.State==EnemyState.Running,"Recovery failed to resume combat");cases.Add("Attack miss waits for active windows, uses Any idle, and keeps recovery timer");
   enter(EnemyState.Punching,false);enemy.Tick(24/30f);Check(enemy.State==EnemyState.CombatIdle&&Mathf.Abs(enemy.motion.BlendSeconds-idle.blend)<.0001f,"Punch miss does not share Any idle");cases.Add("Punching miss shares Any -> Combat Idle");
   var exact=new ResonatorTransition{label="Test exact idle",from=EnemyState.Attack,to=EnemyState.CombatIdle,blend=.123f,entryFrame=7,exitFrame=25};profile.transitions=profile.transitions.Concat(new[]{exact}).ToArray();
   enter(EnemyState.Attack,false);enemy.Tick(24/30f);Check(enemy.State==EnemyState.Attack,"Explicit exit frame ignored");enemy.Tick(1.01f/30);Check(enemy.State==EnemyState.CombatIdle&&Mathf.Abs(enemy.CurrentFrame-7)<.001f&&Mathf.Abs(enemy.motion.BlendSeconds-.123f)<.001f,"Exact idle override did not take precedence");cases.Add("Exact Attack idle overrides Any and respects authored exit frame");exact.enabled=false;
   scream.exitFrame=30;enter(EnemyState.Attack,true);enemy.Tick(25/30f);Check(enemy.State==EnemyState.Attack&&enemy.CurrentAttackConnected,"Configured tail was not preserved");enemy.Tick(5.01f/30);Check(enemy.State==EnemyState.Screaming,"Configured source exit frame not connected");scream.exitFrame=-1;cases.Add("Optional source exit frame can preserve a chosen hit follow-through");
   enter(EnemyState.Kicking,true);enemy.Tick(24/30f);Check(enemy.State==EnemyState.Kicking,"Any idle/scream stole kick -> Attack continuation");enemy.Tick(6/30f);Check(enemy.State==EnemyState.Attack,"Kick no longer chains Attack");actorGo.transform.position=new Vector3(0,0,9);Physics.SyncTransforms();enemy.Tick(1);Check(enemy.State==EnemyState.CombatIdle,"Earlier kick hit incorrectly makes a missed Attack scream");cases.Add("Kick still chains Attack; previous kick hit does not hide a missed final Attack");
   profile.headbuttContinueChance=1;enter(EnemyState.Headbutt,true);enemy.Tick(24/30f);Check(enemy.State==EnemyState.Headbutt,"Any idle stole pending headbutt combo");enemy.Tick(20/30f);Check(enemy.State==EnemyState.Kicking,"Headbutt continuation lost");cases.Add("Headbutt pending combo keeps priority over shared recovery");
   enter(EnemyState.Attack,false);enemy.TakeDamage(1,Vector3.forward*2);enemy.Tick(.01f);Check(enemy.State==EnemyState.Hit,"Any idle interrupted hit");enemy.TakeDamage(10000,Vector3.forward*2);enemy.Tick(.01f);Check(enemy.State==EnemyState.Dying,"Any idle interrupted death");cases.Add("Any recovery never interrupts reaction or death");
   return new{passed=true,cases=cases.ToArray(),authoredScreamBlend=scream.blend,authoredScreamEntry=scream.entryFrame,sharedIdleBlend=idle.blend};
  }
  finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(profile);UnityEngine.Object.DestroyImmediate(actorGo);foreach(var e in originals)if(e)e.enabled=true;}
 }
}
