using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class CrowdNavigationScenarios
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 static readonly MethodInfo Enter=typeof(ResonatorEnemy).GetMethod("Enter",BindingFlags.Instance|BindingFlags.NonPublic);
 public static async Task<object> Run()
 {
  Check(Application.isPlaying,"Start Arena Play Mode first");bool bg=Application.runInBackground;Application.runInBackground=true;
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);var active=originals.Select(x=>(ai:x.enabled,nav:x.motor.agent.enabled)).ToArray();
  foreach(var e in originals){e.enabled=false;e.motor.Stop();e.motor.agent.enabled=false;}
  var actorGo=new GameObject("Crowd navigation target");var actor=actorGo.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;
  var targetShape=actorGo.AddComponent<CapsuleCollider>();targetShape.radius=.3f;targetShape.height=1.8f;targetShape.center=Vector3.up*.9f;
  var created=new List<ResonatorEnemy>();GameObject wall=null;var cases=new List<string>();
  Func<Vector3,bool,ResonatorEnemy> make=(point,native)=>
  {
   var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonator/AI/Prefabs/ResonatorAgent.prefab"),point,Quaternion.identity);
   var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;e.target=actor;e.tuning=UnityEngine.Object.Instantiate(e.tuning);e.Initialize(RestActivity.FeignDeath);created.Add(e);
   Enter.Invoke(e,new object[]{EnemyState.Screaming,true});Enter.Invoke(e,new object[]{EnemyState.Running,true});e.enabled=native;return e;
  };
  Action<ResonatorEnemy> drop=e=>{created.Remove(e);var profile=e.tuning;UnityEngine.Object.DestroyImmediate(e.gameObject);UnityEngine.Object.DestroyImmediate(profile);};
  Action<Vector3> at=point=>{actorGo.transform.position=point;Physics.SyncTransforms();};
  try
  {
   at(new Vector3(0,0,11));var group=new[]{make(new Vector3(-.15f,0,0),true),make(new Vector3(.15f,0,0),true),make(new Vector3(0,0,-.4f),true)};
   await Task.Delay(2300);var progress=group.Select(e=>e.transform.position.z).ToArray();
   Check(progress.All(z=>z>1.5f),"Crowd deadlocked: "+string.Join(",",progress));
   Check(group.Select(e=>e.motor.agent.avoidancePriority).Distinct().Count()>1,"Crowd priorities are identical");
   float spacing=float.PositiveInfinity;for(int i=0;i<group.Length;i++)for(int j=i+1;j<group.Length;j++)spacing=Mathf.Min(spacing,Vector3.ProjectOnPlane(group[i].transform.position-group[j].transform.position,Vector3.up).magnitude);
   Check(spacing>.5f,"Crowd bodies collapsed into each other: "+spacing);cases.Add("Three initially overlapping pursuers all progress and separate with distinct avoidance priorities");foreach(var e in group)drop(e);
   at(new Vector3(0,0,11));var front=make(new Vector3(0,0,9.4f),false);var rear=make(new Vector3(0,0,8.9f),false);bool foundOpening=false;rear.StateChanged+=s=>{if(ResonatorEnemy.IsAttack(s))foundOpening=true;};rear.enabled=true;
   await Task.Delay(2000);Check(foundOpening,"Rear pursuer stopped forever at launch range behind a stationary ally");cases.Add("A pursuer behind a stationary ally finds a strike opening instead of stopping at the attack ring");drop(rear);drop(front);
   at(new Vector3(0,0,7));var existing=make(new Vector3(3,0,-3),false);var corpse=make(new Vector3(0,0,2),false);corpse.TakeDamage(10000,Vector3.back*2);corpse.Tick(50.1f/30);Check(corpse.ragdoll.Active,"Corpse did not become ragdoll");
   Check(corpse.ragdoll.boneColliders.All(c=>Physics.GetIgnoreCollision(existing.motor.BodyCollider,c)),"Existing living actor still collides with corpse");
   var pursuer=make(Vector3.zero,true);Check(corpse.ragdoll.boneColliders.All(c=>Physics.GetIgnoreCollision(pursuer.motor.BodyCollider,c)),"Newly spawned actor still collides with corpse");
   await Task.Delay(1600);float corpsePass=pursuer.transform.position.z;Check(corpsePass>2.6f,"Pursuer was trapped by fallen corpse: "+corpsePass);cases.Add("Existing and newly spawned living agents ignore corpse bones and pass a fallen body");drop(pursuer);drop(existing);drop(corpse);
   at(new Vector3(0,0,6));var blocked=make(Vector3.zero,true);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,.65f);wall.transform.localScale=new Vector3(.6f,2,.15f);Physics.SyncTransforms();
   await Task.Delay(2500);Check(blocked.motor.UnstuckAttempts>0,"Blocked navigation never attempted lateral recovery");Check(blocked.transform.position.z>.9f,"Lateral recovery failed to pass a small blocker: "+blocked.transform.position);cases.Add("A small physical blocker triggers lateral recovery instead of a permanent deadlock");drop(blocked);UnityEngine.Object.DestroyImmediate(wall);wall=null;
   at(new Vector3(0,0,6));var e2=make(Vector3.zero,false);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,.65f);wall.transform.localScale=new Vector3(4,2,.15f);Physics.SyncTransforms();e2.motor.AttackStep(Vector3.zero,0,Vector3.forward*3,5,actor,.8f,1);
   Check(e2.transform.position.z<.35f,"World collision was removed together with peer collision");cases.Add("World walls still block attacks after peer/corpse filtering");drop(e2);UnityEngine.Object.DestroyImmediate(wall);wall=null;
   at(new Vector3(0,0,1.7f));e2=make(Vector3.zero,false);e2.Tick(.02f);Check(e2.State==EnemyState.Kicking,"Running did not initiate kicking at the new longer range");Check(e2.Distance>1.5f,"Long-range attack actually started too close");cases.Add("Running initiates kicking around 1.7 metres, before old 1.1m range");drop(e2);
   at(new Vector3(0,0,1.55f));e2=make(Vector3.zero,false);e2.TakeDamage(1,Vector3.forward*2);e2.Tick((e2.motion.Beat.EndSeconds-e2.motion.SourceSeconds)/e2.motion.Beat.speed+.03f);Check(e2.State==EnemyState.Headbutt,"Headbutt cannot initiate from its extended reach");cases.Add("Close hit recovery can launch headbutt at its authored starting distance");drop(e2);
   at(new Vector3(0,0,1.8f));e2=make(Vector3.zero,false);e2.transform.rotation=Quaternion.Euler(0,180,0);e2.Tick(.01f);Check(e2.State==EnemyState.Running,"Attack began while facing away");e2.transform.rotation=Quaternion.identity;
   wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,.9f);wall.transform.localScale=new Vector3(2,2,.1f);Physics.SyncTransforms();e2.Tick(.02f);Check(e2.State==EnemyState.Running,"Attack began through a wall");cases.Add("Extended attacks still require facing and unobstructed contact line");drop(e2);UnityEngine.Object.DestroyImmediate(wall);wall=null;
   at(new Vector3(0,0,3));e2=make(Vector3.zero,false);float maximum=e2.PlannedStartDistance;e2.Tick(.1f);for(int i=0;i<4;i++){at(actorGo.transform.position+Vector3.forward*.2f);e2.Tick(.1f);}float predicted=e2.PlannedStartDistance;
   Check(predicted<maximum&&predicted>=e2.tuning.attackEnterDistance-.001f,"Retreat prediction did not constrain a premature swing");cases.Add("Attack timing accounts for outward target velocity and authored wind-up");drop(e2);
   at(new Vector3(0,0,3));e2=make(Vector3.zero,false);Enter.Invoke(e2,new object[]{EnemyState.Walking,true});e2.Tick(.1f);for(int i=0;i<4;i++){at(actorGo.transform.position+Vector3.forward*.2f);e2.Tick(.1f);}
   Check(e2.State==EnemyState.Running&&e2.Distance<e2.tuning.walkToRunDistance,"Walking did not become running sooner against retreating target");cases.Add("Retreating targets trigger running pressure sooner than the old far-distance threshold");drop(e2);
   return new{passed=true,cases=cases.ToArray(),crowdProgressMetres=progress,minimumCrowdSpacing=spacing,corpsePassedAtZ=corpsePass,predictedStartRange=predicted};
  }
  finally
  {
   if(wall)UnityEngine.Object.DestroyImmediate(wall);foreach(var e in created.ToArray())if(e)drop(e);UnityEngine.Object.DestroyImmediate(actorGo);
   for(int i=0;i<originals.Length;i++)if(originals[i]){originals[i].motor.agent.enabled=active[i].nav;if(active[i].nav)originals[i].motor.PlaceOnMesh();originals[i].enabled=active[i].ai;}Application.runInBackground=bg;
  }
 }
}
