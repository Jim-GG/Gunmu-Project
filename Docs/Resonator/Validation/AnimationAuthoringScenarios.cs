using System;using System.Linq;using System.Collections.Generic;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class AnimationAuthoringScenarios
{
 static void Check(bool ok,string message){if(!ok)throw new Exception(message);}
 public static object Run()
 {
  Check(Application.isPlaying,"Start arena play mode first");
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var x in originals){x.enabled=false;x.motor.Stop();}
  var actorGo=new GameObject("Hitbox validation actor");actorGo.transform.position=new Vector3(0,0,1.05f);var actor=actorGo.AddComponent<ResonatorTarget>();actor.invulnerableForTesting=true;
  var collider=actorGo.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.3f;collider.center=Vector3.up*.9f;
  var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"));var enemy=go.GetComponent<ResonatorEnemy>();enemy.enabled=false;
  var profile=UnityEngine.Object.Instantiate(enemy.tuning);enemy.tuning=profile;enemy.target=actor;var cases=new List<string>();int damageCount=0;enemy.StrikeResolved+=r=>{if(r!=StrikeResult.Miss)damageCount++;};
  Action reset=()=>{go.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);enemy.Initialize(RestActivity.FeignDeath);profile.headbuttContinueChance=0;profile.headbutt.rootMotionScale=0;enemy.TakeDamage(1,Vector3.forward*2);enemy.Tick(profile.hit.EndSeconds+.1f);Physics.SyncTransforms();damageCount=0;};
  Func<HitWindow> box=()=>new HitWindow{startFrame=20,endFrame=23,shape=HitboxShape.Box,anchor=HitboxAnchor.Root,position=new Vector3(0,1,1.05f),size=Vector3.one*.4f,damage=7};
  try
  {
   profile.headbutt.hits=new[]{box()};reset();enemy.Tick(19/30f);Check(damageCount==0,"Wind-up caused early damage");enemy.Tick(1/30f);Check(damageCount==1,"Exact opening frame did not hit");enemy.Tick(4/30f);Check(damageCount==1,"Same hitbox damaged twice");cases.Add("No wind-up damage; exact frame activation; one hit per window");
   var query=new ResonatorHitbox(go.transform);
   foreach(HitboxShape shape in Enum.GetValues(typeof(HitboxShape))){var h=box();h.shape=shape;h.radius=.3f;h.height=.8f;Check(query.Contains(h,actor),shape+" did not overlap target");h.position.x=4;Check(!query.Contains(h,actor),shape+" ignored spatial offset");}
   cases.Add("Box, sphere and capsule respect position and volume");
   var small=box();small.position=new Vector3(0,3,1.05f);small.size=Vector3.one*.1f;profile.headbutt.hits=new[]{small};reset();enemy.Tick(25/30f);Check(damageCount==0,"Range/angle fallback bypassed the real hitbox");cases.Add("Nearby target outside the volume cannot be damaged");
   var narrow=box();narrow.startFrame=narrow.endFrame=20.25f;profile.headbutt.hits=new[]{narrow};reset();enemy.Tick(21/30f);Check(damageCount==1,"Low FPS skipped a sub-frame impact window");cases.Add("Large time steps still evaluate a fractional one-frame hitbox");
   profile.headbutt.hits=new[]{box()};reset();enemy.motion.Play(profile.headbutt.animation,24,true);enemy.Tick(.1f);Check(damageCount==0,"Entry-frame skip retroactively hit an old window");cases.Add("Skipped windows never hit retroactively using a recovery pose");
   var h1=box();h1.hitGroup="one blow";var h2=box();h2.hitGroup="one blow";profile.headbutt.hits=new[]{h1,h2};reset();actorGo.AddComponent<BoxCollider>();Physics.SyncTransforms();enemy.Tick(21/30f);Check(damageCount==1,"Shared hit group / duplicate colliders damaged twice");cases.Add("Multiple volumes in one hit group and multiple hurt colliders hit only once");
   var disabled=box();disabled.enabled=false;profile.headbutt.hits=new[]{disabled};reset();enemy.Tick(25/30f);Check(damageCount==0,"Disabled hitbox still damaged");
   var masked=box();masked.targetLayers=0;profile.headbutt.hits=new[]{masked};reset();enemy.Tick(25/30f);Check(damageCount==0,"Layer mask ignored");cases.Add("Disabled volumes and target layers suppress hits");
   var names=new List<string>();Action<MotionFrameEvent> listener=e=>names.Add(e.name);enemy.FrameEventReceived+=listener;
   var beat=new MotionBeat{track=profile.headbutt.animation.track,endFrame=60,events=new[]{new MotionFrameEvent{name="start",frame=0},new MotionFrameEvent{name="swing",frame=10},new MotionFrameEvent{name="impact",frame=20}}};
   enemy.motion.Play(beat,0,true);enemy.motion.Advance(12/30f);enemy.motion.Advance(12/30f);enemy.motion.Advance(1/30f);Check(string.Join(",",names)=="start,swing,impact","Frame events skipped or duplicated: "+string.Join(",",names));
   names.Clear();beat.events=beat.events.Reverse().ToArray();enemy.motion.Play(beat,0,true);enemy.motion.Advance(25/30f);Check(string.Join(",",names)=="start,swing,impact","Events must execute chronologically, independent of list order");
   names.Clear();enemy.motion.Play(beat,15,true);enemy.motion.Advance(8/30f);Check(string.Join(",",names)=="impact","Entry-frame skip replayed old events");
   names.Clear();beat.startFrame=5;beat.endFrame=15;beat.loop=true;beat.events=new[]{new MotionFrameEvent{name="begin",frame=5},new MotionFrameEvent{name="middle",frame=10},new MotionFrameEvent{name="end",frame=15}};enemy.motion.Play(beat,5,true);enemy.motion.Advance(11/30f);Check(string.Join(",",names)=="begin,middle,end,begin","Loop markers failed: "+string.Join(",",names));cases.Add("Frame events fire once on crossing, skip past entry, and repeat on loops");
   names.Clear();enemy.motion.SampleBlend(profile.headbutt.animation,37.9f,profile.kick.animation,12.9f,.06f,.03f);Check(names.Count==0,"Scrubbing dispatched gameplay events");
   var bones=enemy.ragdoll.definitions.Select(d=>d.bone).ToArray();var sampled=bones.Select(b=>b.localRotation).ToArray();enemy.motion.Play(profile.headbutt.animation,37,true);enemy.motion.Play(profile.kick.animation,12,false,.06f);enemy.motion.Advance(.03f);Check(bones.Select((b,i)=>Quaternion.Angle(b.localRotation,sampled[i])).Max()<.1f,"Preview and runtime blending do not match");cases.Add("Preview uses the exact runtime blend and never dispatches gameplay events");
   var anchored=box();anchored.anchor=HitboxAnchor.RightFoot;anchored.position=Vector3.zero;enemy.motion.Play(profile.kick.animation,24,true);var p=query.Pose(anchored);var bone=query.Anchor(HitboxAnchor.RightFoot);Check(Vector3.Distance(p.centre,bone.position)<.0001f,"Bone-attached hitbox does not follow the foot");anchored.position=Vector3.right*.2f;p=query.Pose(anchored);Check(Mathf.Abs(Vector3.Distance(p.centre,bone.position)-.2f)<.0001f,"Imported 130x bone scale distorted metre-sized offsets");cases.Add("Bone attachment follows the pose; FBX scale never inflates offsets");
   return new{passed=true,cases=cases.ToArray()};
  }
  finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(profile);UnityEngine.Object.DestroyImmediate(actorGo);foreach(var x in originals)if(x)x.enabled=true;}
 }
}
