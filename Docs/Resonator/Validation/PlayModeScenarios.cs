using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;
using UnityEngine;using UnityEngine.AI;using UnityEditor;
using Resonator.AI;
public static class ResonatorScenarios
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static ResonatorTarget actor;static List<ResonatorEnemy> created;
 static ResonatorEnemy Make(RestActivity activity)
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
  var go=UnityEngine.Object.Instantiate(prefab,Vector3.zero,Quaternion.identity);go.name="Scenario Enemy";
  var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;e.target=actor;
  e.tuning=UnityEngine.Object.Instantiate(e.tuning);e.tuning.headbuttContinueChance=1;e.tuning.recoverySeconds=new Vector2(.6f,.6f);
  // These are state-machine/integration scenarios: use a controlled contact volume
  // to guarantee hit/miss branches independently of the artist's current offsets.
  // Real shapes, bone attachment and exact frame activation have separate tests
  // in AnimationAuthoringScenarios, including the actual runtime physics queries.
  foreach(var attack in new[]{e.tuning.headbutt,e.tuning.kick,e.tuning.attack,e.tuning.punch})foreach(var h in attack.hits)
  {h.shape=HitboxShape.Box;h.anchor=HitboxAnchor.Root;h.position=new Vector3(0,1,.95f);h.size=new Vector3(.7f,.7f,.8f);h.minimumBlendWeight=0;}
  e.Initialize(activity);created.Add(e);return e;
 }
 static void At(Vector3 point){actor.transform.position=point;Physics.SyncTransforms();}
 static void Hear(ResonatorEnemy e)=>e.Hear(new ResonatorNoiseEvent(e.transform.position+Vector3.up,30,actor));
 static float End(MotionBeat beat)=>(beat.EndSeconds-beat.StartSeconds)/beat.speed+.03f;
 static void Finish(ResonatorEnemy e)=>e.Tick(Mathf.Max(0,e.motion.Beat.EndSeconds-e.motion.SourceSeconds)/e.motion.Beat.speed+.03f);
 static void Drop(ResonatorEnemy e){created.Remove(e);var data=e.tuning;UnityEngine.Object.DestroyImmediate(e.gameObject);UnityEngine.Object.DestroyImmediate(data);}
 public static async Task<object> Run()
 {
  Check(Application.isPlaying,"Start Arena Play Mode first");bool background=Application.runInBackground;Application.runInBackground=true;
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var e in originals){e.enabled=false;e.motor.Stop();}
  var inputs=UnityEngine.Object.FindObjectsByType<FirstPerson.FirstPersonInput>(FindObjectsSortMode.None);foreach(var i in inputs)i.enabled=false;
  var playerMotors=UnityEngine.Object.FindObjectsByType<FirstPerson.FirstPersonMotor>(FindObjectsSortMode.None);foreach(var m in playerMotors)m.enabled=false;
  var testActor=new GameObject("Scenario Target");actor=testActor.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;var capsule=testActor.AddComponent<CapsuleCollider>();capsule.height=1.8f;capsule.radius=.3f;capsule.center=Vector3.up*.9f;
  created=new List<ResonatorEnemy>();var cases=new List<string>();
  try
  {
   At(new Vector3(0,0,9));var e=Make(RestActivity.FeignDeath);
   Check(e.motor.Ready,"Test enemy not on NavMesh");e.Tick(4);Check(e.State==EnemyState.Dormant&&e.CurrentFrame==0,"Feign death must remain at first frame");
   var head=e.GetComponentsInChildren<Transform>().First(t=>t.name=="ValveBiped.Bip01_Head1");Check(head.position.y-e.transform.position.y<.6f,"Feign-death opening must be prone, not the FBX bind pose");Check(e.GetComponentsInChildren<Rigidbody>().Length==0,"Living skeleton must not have physics ownership");
   e.Hear(new ResonatorNoiseEvent(new Vector3(100,1,100),1,actor));Check(e.State==EnemyState.Dormant,"Out of range noise");
   Hear(e);Check(e.State==EnemyState.Waking&&!e.Aware,"Wake should not yet reveal precise target");e.Tick(519/30f);Check(e.State==EnemyState.Waking,"Wake gate too early");e.Tick(1.1f/30);Check(e.State==EnemyState.Screaming&&e.Aware,"Wake 520 -> scream");Finish(e);Check(e.State==EnemyState.Running,"Distant scream -> run");
   cases.Add("Feign death freezes; hearing only; reveal at frame 520; far scream -> run");Drop(e);
   At(new Vector3(0,0,9));e=Make(RestActivity.Feeding);e.Tick(20);Check(e.CurrentFrame>=150&&e.CurrentFrame<=420&&e.State==EnemyState.Dormant,"Feeding rest range");Hear(e);Check(e.State==EnemyState.FeedingRise&&Mathf.Abs(e.CurrentFrame-421)<.01f,"Feeding jumps to 421");e.Tick(68/30f);Check(e.State==EnemyState.FeedingRise,"Feeding reveal too early");e.Tick(1.1f/30);Check(e.State==EnemyState.Screaming,"Feeding 490 -> scream");cases.Add("Feeding loops 150-420, resumes 421, reveals 490");Drop(e);
   foreach(var item in new[]{(RestActivity.SlowIdle,700),(RestActivity.RestlessIdle,425)})
   {
    At(new Vector3(0,0,25));e=Make(item.Item1);e.Tick((item.Item2+2)/30f);Check(e.State==EnemyState.Dormant&&e.CurrentFrame<3,"Idle range "+item.Item1);At(new Vector3(0,0,5));e.Tick(.2f);Check(e.State==EnemyState.Screaming,"Idle vision "+item.Item1);cases.Add(item.Item1+" loop and sight detection");Drop(e);
   }
   At(new Vector3(5,0,0));e=Make(RestActivity.Resonating);e.Tick(.5f);Check(e.State==EnemyState.Dormant,"Resonance must ignore sight");Hear(e);Check(e.State==EnemyState.Agonizing,"Resonance hearing -> agonizing");Quaternion before=e.transform.rotation;e.Tick(1);float angle=Quaternion.Angle(before,e.transform.rotation);Check(Mathf.Abs(angle-20)<.2f,"Agonizing turn rate "+angle);Finish(e);Check(e.State!=EnemyState.Agonizing,"Agonizing ends into combat");cases.Add("Resonance hearing only; agonizing turns 20 degrees per second");Drop(e);
   At(new Vector3(0,0,9));e=Make(RestActivity.FeignDeath);Hear(e);Finish(e);Finish(e);Check(e.State==EnemyState.Running,"Run setup");At(new Vector3(0,0,1.05f));e.Tick(.02f);Check(e.State==EnemyState.Kicking,"Running must only enter kicking");Finish(e);Check(e.State==EnemyState.Attack,"Kick must continue attack");Check(e.Distance>=e.tuning.minimumSeparation-.01f,"Kick penetrated target");Finish(e);Check(e.State==EnemyState.Screaming,"Successful combo -> scream");Check(e.DamageWindowsResolved==e.tuning.kick.hits.Length+e.tuning.attack.hits.Length,"One damage evaluation per authored window");cases.Add("Run -> kick -> attack -> scream; separation; single-use spatial impact windows");Drop(e);
   At(new Vector3(0,0,1.05f));e=Make(RestActivity.FeignDeath);e.TakeDamage(1,e.transform.position+e.transform.forward*2);Check(e.State==EnemyState.Hit,"Front hit direction");Finish(e);Check(e.State==EnemyState.Headbutt,"Close hit recovery directly headbutt");Finish(e);Check(e.State==EnemyState.Kicking,"Headbutt continuation");Finish(e);Check(e.State==EnemyState.Attack,"Headbutt -> kick -> attack");e.TakeDamage(1,e.transform.position-e.transform.forward*2);Check(e.State==EnemyState.Stumbling,"Back hit interrupts attack");At(new Vector3(0,0,9));Finish(e);Check(e.State==EnemyState.Running,"Hit recovery far -> running, no idle");cases.Add("Headbutt combo; front/back reactions; hit interrupts attack; direct combat return");Drop(e);
   At(new Vector3(0,0,9));e=Make(RestActivity.FeignDeath);Hear(e);Finish(e);Finish(e);At(new Vector3(0,0,1.05f));e.Tick(.02f);At(new Vector3(0,0,9));Finish(e);Check(e.State==EnemyState.Attack,"Missed kick still chains attack");Finish(e);Check(e.State==EnemyState.CombatIdle,"Missed sequence -> combat idle");e.Tick(.59f);Check(e.State==EnemyState.CombatIdle,"Recovery minimum");e.Tick(.02f);Check(e.State==EnemyState.Running,"Recovery restarts pursuit");cases.Add("Missed combo -> timed recovery -> pursuit");Drop(e);
   At(new Vector3(0,0,.8f));e=Make(RestActivity.FeignDeath);e.TakeDamage(1,e.transform.position+Vector3.forward*2);Finish(e);
   var hk=e.tuning.Transition(EnemyState.Headbutt,EnemyState.Kicking);var ka=e.tuning.Transition(EnemyState.Kicking,EnemyState.Attack);
   e.Tick((hk.cancelFrameOnHit-1)/30f);Check(e.State==EnemyState.Headbutt,"Headbutt cancelled before the configured frame");e.Tick(1.1f/30);Check(e.State==EnemyState.Kicking&&Mathf.Abs(e.CurrentFrame-hk.entryFrame)<.01f&&Mathf.Abs(e.motion.BlendSeconds-hk.blend)<.001f,"Connected headbutt must use configured entry and blend: "+e.State+" frame="+e.CurrentFrame+" connected="+e.CurrentAttackConnected+" entry="+hk.entryFrame+" blend="+e.motion.BlendSeconds);
   e.Tick((ka.cancelFrameOnHit-hk.entryFrame-1)/30f);Check(e.State==EnemyState.Kicking,"Kick cancelled early");e.Tick(1.1f/30);Check(e.State==EnemyState.Attack&&Mathf.Abs(e.CurrentFrame-ka.entryFrame)<.01f,"Kick must use configured cancel and entry frames");cases.Add("Fast connected combo uses configured hit cancel frames, entries and blends");Drop(e);
   e=Make(RestActivity.FeignDeath);e.TakeDamage(1,e.transform.position+Vector3.forward*2);Finish(e);e.tuning.headbutt.hits=Array.Empty<HitWindow>();e.Tick(52/30f);Check(e.State==EnemyState.Headbutt,"Miss cancel must use separate frame 53");e.Tick(1.1f/30);Check(e.State==EnemyState.Kicking,"Miss cancel at 53");cases.Add("Missed attacks use independently editable cancellation timing");Drop(e);
   e=Make(RestActivity.FeignDeath);e.motion.Play(e.tuning.headbutt.animation,37,true);e.motion.Play(e.tuning.kick.animation,45,false,.5f);e.motion.Advance(.2f);
   var poseBones=e.ragdoll.definitions.Select(d=>d.bone).ToArray();var pose=poseBones.Select(b=>b.localRotation).ToArray();e.motion.Play(e.tuning.attack.animation,28,false,.25f);
   Check(poseBones.Select((b,i)=>Quaternion.Angle(b.localRotation,pose[i])).Max()<.1f,"Interrupting a blend must preserve the current mixed pose");cases.Add("Interrupting an unfinished blend preserves the full mixed skeletal pose");Drop(e);
   bool punching=false;
   for(int seed=0;seed<20&&!punching;seed++)
   {
    At(new Vector3(0,0,1.05f));e=Make(RestActivity.SlowIdle);UnityEngine.Random.InitState(seed);Hear(e);Finish(e);
    if(e.State==EnemyState.Punching){punching=true;At(new Vector3(0,0,8));Finish(e);Check(e.State==EnemyState.CombatIdle,"Punch must not branch into kick/attack");}Drop(e);
   }
   Check(punching,"Punch selection never reached");cases.Add("Close scream selects headbutt/punch; punching has no combo branch");
   At(new Vector3(0,0,1.05f));e=Make(RestActivity.FeignDeath);e.TakeDamage(1,e.transform.position+e.transform.forward*2);Finish(e);
   var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Scenario Attack Wall";wall.transform.position=new Vector3(0,1,.53f);wall.transform.localScale=new Vector3(2,2,.12f);Physics.SyncTransforms();int contacts=0;Action<StrikeResult> listener=r=>{if(r!=StrikeResult.Miss)contacts++;};e.StrikeResolved+=listener;Finish(e);Check(contacts==0,"Damage passed through wall");Check(e.transform.position.z<.3f,"Root motion crossed wall");UnityEngine.Object.DestroyImmediate(wall);cases.Add("Physics obstruction blocks attack motion and damage");Drop(e);
   At(new Vector3(0,0,1));e=Make(RestActivity.FeignDeath);e.TakeDamage(10000,e.transform.position+Vector3.forward*2,3);Check(e.State==EnemyState.Dying,"Death priority");e.TakeDamage(2,e.transform.position-Vector3.forward);Check(e.State==EnemyState.Dying,"Hit must not interrupt death");e.Tick(49/30f);Check(!e.ragdoll.Active&&e.State==EnemyState.Dying,"Ragdoll before 50");e.Tick(1.1f/30);Check(e.State==EnemyState.Dead&&e.ragdoll.Active&&!e.motion.animator.enabled,"Frame 50 ragdoll");Check(e.ragdoll.bodies.All(b=>!b.isKinematic)&&e.ragdoll.boneColliders.All(c=>c.enabled),"Ragdoll not dynamic");   await Task.Delay(700);Check(e.ragdoll.bodies.All(b=>Vector3.Distance(b.position,e.transform.position)<4),"Ragdoll exploded or fell through ground");cases.Add("Death dominates hit; ragdoll at frame 50; physical settling");Drop(e);
   At(new Vector3(0,0,4));e=Make(RestActivity.FeignDeath);e.enabled=true;ResonatorNoise.Emit(actor.transform.position+Vector3.up,20,actor);Check(e.State==EnemyState.Waking,"Live hearing event bus failed");e.enabled=false;cases.Add("Live gameplay noise event wakes a hearing-only unit");Drop(e);
   At(new Vector3(0,0,1.05f));e=Make(RestActivity.FeignDeath);e.TakeDamage(1,e.transform.position+Vector3.forward*2);Finish(e);At(new Vector3(0,0,3));e.Tick(37/30f);Check(e.DamageWindowsResolved==0,"Miss should remain active until hit-window end");At(e.transform.position+Vector3.forward*1.05f);e.Tick(1/30f);Check(e.DamageWindowsResolved==1,"Target entering active hit window was not hit");e.Tick(2/30f);Check(e.DamageWindowsResolved==1,"Same impact window hit twice");cases.Add("Active impact window can catch an entering target, only once");Drop(e);
   var real=UnityEngine.Object.FindObjectsByType<ResonatorTarget>(FindObjectsSortMode.None).First(t=>t!=actor&&t.gauntlet&&t.vitals);real.vitals.Restore();
   At(new Vector3(0,0,20));e=Make(RestActivity.FeignDeath);e.target=real;e.motor.agent.Warp(real.transform.position+real.transform.forward*.8f);e.transform.rotation=Quaternion.LookRotation(real.transform.position-e.transform.position);Physics.SyncTransforms();
   real.gauntlet.Cancel();real.gauntlet.BeginBlock();float hp=real.vitals.Health;int energy=real.gauntlet.BlockEnergy;int blocked=0;e.StrikeResolved+=r=>{if(r==StrikeResult.Blocked)blocked++;};e.TakeDamage(1,e.transform.position+e.transform.forward*2);Finish(e);Finish(e);Check(blocked==1&&real.vitals.Health==hp&&real.gauntlet.BlockEnergy==Mathf.Min(3,energy+1),"Live player block did not prevent damage / grant energy");real.gauntlet.EndBlock();real.gauntlet.Cancel();
   e.Initialize(RestActivity.FeignDeath);e.target=real;e.TakeDamage(1,e.transform.position+e.transform.forward*2);Finish(e);Finish(e);Check(real.vitals.Health<hp,"Unblocked enemy hit did not damage live player");real.vitals.Restore();cases.Add("Actual gauntlet parry blocks enemy and gains charge energy; unblocked hit damages player");Drop(e);
   e=Make(RestActivity.FeignDeath);e.target=real;e.motor.agent.Warp(real.transform.position+real.transform.forward*.8f);e.transform.rotation=Quaternion.LookRotation(real.transform.position-e.transform.position);Physics.SyncTransforms();hp=e.Health;real.gauntlet.Cancel();real.gauntlet.Press();real.gauntlet.Release();await Task.Delay(350);Check(e.Health<hp,"Actual gauntlet impact did not damage enemy");real.gauntlet.Cancel();cases.Add("Actual player punch emits noise and damages/staggers the enemy");Drop(e);
   // Let the native NavMesh simulation and the real Update loop advance together.
   At(new Vector3(0,0,3));e=Make(RestActivity.SlowIdle);Hear(e);Finish(e);Check(e.State==EnemyState.Walking,"Walking setup");Vector3 initial=e.transform.position;e.enabled=true;
   await Task.Delay(500);float moved=Vector3.Distance(initial,e.transform.position);Check(moved>.03f,"Native navigation did not advance");
   // Async continuations can resume between the native animation update and our
   // manual pose/root compensation. Sample both at one controlled clock position.
   e.motion.Advance(0);
   Vector3 hip=e.transform.InverseTransformPoint(e.motion.hips.position);Vector3 reference=e.motion.ReferenceHipInParent;float drift=new Vector2(hip.x-reference.x,hip.z-reference.z).magnitude;
   Check(drift<.025f,"Authored root movement double applied: "+drift);Check(e.CurrentFrame>0,"Locomotion pose did not advance");cases.Add("Real navigation moves; animation follows speed; no double root translation");Drop(e);
   At(new Vector3(-6,0,2));e=Make(RestActivity.SlowIdle);Hear(e);Finish(e);e.enabled=true;await Task.Delay(350);Check(e.motor.agent.pathStatus==NavMeshPathStatus.PathComplete&&e.motor.agent.path.corners.Length>=3,"Navigation failed to route around the wall");cases.Add("Native NavMesh path routes around a static wall");Drop(e);
   return new{passed=true,cases=cases.ToArray(),nativeWalkDistance=moved,visualRootDrift=drift};
  }
  finally
  {
   foreach(var e in created.ToArray())if(e)Drop(e);UnityEngine.Object.DestroyImmediate(testActor);
   foreach(var e in originals)if(e)e.enabled=true;foreach(var i in inputs)if(i)i.enabled=true;foreach(var m in playerMotors)if(m)m.enabled=true;Application.runInBackground=background;
  }
 }
}


