using System;using System.Linq;using System.Reflection;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class AttackDisplacementScenarios
{
 static void Check(bool ok,string text){if(!ok)throw new Exception(text);}
 static readonly MethodInfo Start=typeof(ResonatorEnemy).GetMethod("BeginAttack",BindingFlags.Instance|BindingFlags.NonPublic);
 public static object Run()
 {
  Check(Application.isPlaying,"Start play mode first");
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var x in originals){x.enabled=false;x.motor.Stop();}
  var actorGo=new GameObject("Pursuit movement target");var actor=actorGo.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;var c=actorGo.AddComponent<CapsuleCollider>();c.radius=.3f;c.height=1.8f;c.center=Vector3.up*.9f;
  var created=new List<ResonatorEnemy>();var cases=new List<string>();GameObject wall=null;
  Func<Vector3,ResonatorEnemy> make=point=>
  {
   actorGo.transform.position=point;var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonator/AI/Prefabs/ResonatorAgent.prefab"));var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;e.target=actor;e.tuning=UnityEngine.Object.Instantiate(e.tuning);created.Add(e);
   var a=e.tuning.punch;a.animation.speed=1;a.rootMotionScale=0;a.hits=new[]{new HitWindow{startFrame=80,endFrame=81,anchor=HitboxAnchor.Root,position=new Vector3(0,1,1),size=Vector3.one*.5f}};
   a.displacement=new AttackDisplacement{enabled=true,trackingEndFrame=30,turnSpeed=120,segments=new[]{new AttackMoveSegment{startFrame=5,endFrame=35,maxDistance=2,maxSpeed=3,contactDistance=1,progress=AnimationCurve.Linear(0,0,1,1)}}};
   e.Initialize(RestActivity.FeignDeath);Start.Invoke(e,new object[]{EnemyState.Punching,true});Physics.SyncTransforms();return e;
  };
  Action<ResonatorEnemy> drop=e=>{created.Remove(e);var profile=e.tuning;UnityEngine.Object.DestroyImmediate(e.gameObject);UnityEngine.Object.DestroyImmediate(profile);};
  Action<ResonatorEnemy,int> frames=(e,n)=>{for(int i=0;i<n;i++)e.Tick(1/30f);};
  try
  {
   var e=make(new Vector3(0,0,.95f));frames(e,40);Check(new Vector2(e.transform.position.x,e.transform.position.z).magnitude<.001f,"In-range attack pushed forward");cases.Add("Already within planned contact distance: no extra forward travel");drop(e);
   e=make(new Vector3(0,0,3));frames(e,4);Check(e.AttackAddedTravel<.001f,"Movement started outside its source-frame window");frames(e,36);float moved=e.transform.position.z;Check(moved>1.7f&&moved<=2.001f,"Attack pursuit did not close distance: "+moved);Check(e.Distance>=.999f,"Overshot planned contact distance");Check(Vector3.Distance(e.transform.position,e.motor.agent.nextPosition)<.03f,"NavMesh agent lost position synchronization");cases.Add("Frame-windowed attack advances, stops at contact distance, and synchronizes NavMesh");drop(e);
   e=make(new Vector3(0,0,2.2f));frames(e,10);actorGo.transform.position=new Vector3(.8f,0,2.8f);Physics.SyncTransforms();frames(e,19);Check(e.transform.position.x>.03f&&Quaternion.Angle(e.transform.rotation,Quaternion.identity)>2,"Wind-up pursuit did not follow moving target");frames(e,2);Check(e.AttackAimLocked,"Tracking did not lock at configured frame");var rotation=e.transform.rotation;actorGo.transform.position=new Vector3(-3,0,3);Physics.SyncTransforms();frames(e,5);Check(Quaternion.Angle(rotation,e.transform.rotation)<.1f,"Locked attack kept homing toward a dodging player");cases.Add("Wind-up follows moving targets; lock frame preserves a chance to evade");drop(e);
   e=make(new Vector3(0,0,5));e.tuning.punch.displacement.segments[0].maxDistance=.6f;frames(e,45);Check(e.AttackAddedTravel<=.601f,"Movement exceeded authored distance budget");Check(e.transform.position.z>.4f,"Distance budget never produced movement");cases.Add("Maximum additional travel is bounded even when target escapes");drop(e);
   e=make(new Vector3(0,0,3));e.tuning.punch.displacement.segments[0].maxSpeed=.6f;frames(e,5);var before=e.transform.position;e.Tick(1/30f);Check(Vector3.Distance(before,e.transform.position)<=.021f,"Per-second movement speed cap ignored");cases.Add("Per-second speed cap prevents catch-up teleports");drop(e);
   e=make(new Vector3(0,0,3));e.tuning.punch.displacement.segments[0].maxDistance=1;Start.Invoke(e,new object[]{EnemyState.Punching,true});frames(e,35);float runtime=e.AttackAddedTravel;float preview=AttackMoveBudget.Preview(e.tuning.punch,0,35,3,e.tuning.minimumSeparation);Check(Mathf.Abs(runtime-preview)<.015f,"Workbench budget differs from free-space runtime: "+runtime+" vs "+preview);cases.Add("Workbench distance preview uses the same cumulative budget as runtime");drop(e);
   e=make(new Vector3(0,0,3));wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.transform.position=new Vector3(0,1,.65f);wall.transform.localScale=new Vector3(4,2,.15f);Physics.SyncTransforms();frames(e,40);Check(e.transform.position.z<.35f,"Attack moved through a wall");UnityEngine.Object.DestroyImmediate(wall);wall=null;cases.Add("Physics and NavMesh collision constraints prevent attack wall penetration");drop(e);
   e=make(new Vector3(0,0,3));frames(e,12);e.TakeDamage(1,e.transform.position+e.transform.forward*2);before=e.transform.position;frames(e,5);Check(e.State==EnemyState.Hit&&Vector3.Distance(before,e.transform.position)<.03f,"Pursuit continued after hit interrupt");cases.Add("Hit interrupt cancels added attack movement");drop(e);
   // Same physics hitbox, same player retreat: only the added movement differs.
   int[] hits=new int[2];
   for(int enabled=0;enabled<2;enabled++)
   {
    e=make(new Vector3(0,0,1.05f));e.tuning.punch.hits=new[]{new HitWindow{startFrame=25,endFrame=28,anchor=HitboxAnchor.Root,position=new Vector3(0,1,.85f),size=new Vector3(.5f,.6f,.4f)}};e.tuning.punch.displacement.enabled=enabled==1;
    int index=enabled;e.StrikeResolved+=result=>{if(result==StrikeResult.Damaged)hits[index]++;};
    for(int f=0;f<30;f++){actorGo.transform.position+=Vector3.forward*.03f;Physics.SyncTransforms();e.Tick(1/30f);}drop(e);
   }
   Check(hits[0]==0&&hits[1]>0,"Added movement failed to improve hit rate on retreating target: "+string.Join(",",hits));cases.Add("Retreating player: stationary attack misses, pursuit attack reaches the same hitbox");
   var beat=new AttackBeat();beat.animation.track=AssetDatabase.LoadAssetAtPath<ResonatorMotionTrack>("Assets/Resonator/AI/Data/Resonator_Zombie_Punching_Motion.asset");beat.displacement=new AttackDisplacement{enabled=true,segments=new[]{new AttackMoveSegment{startFrame=10,endFrame=20,maxDistance=1,maxSpeed=100,contactDistance=1,progress=AnimationCurve.Linear(0,0,1,1)}}};
   Check(Mathf.Abs(AttackMoveBudget.Preview(beat,15,30,4,.8f)-.5f)<.001f,"Skipped entry frames produced retroactive travel");cases.Add("Animation entry skips do not replay old displacement");
   return new{passed=true,cases=cases.ToArray(),freeSpacePursuitMetres=moved,retreatingPlayerHits=hits};
  }
  finally{if(wall)UnityEngine.Object.DestroyImmediate(wall);foreach(var e in created.ToArray())drop(e);UnityEngine.Object.DestroyImmediate(actorGo);foreach(var e in originals)if(e)e.enabled=true;}
 }
}
