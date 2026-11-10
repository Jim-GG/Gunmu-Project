using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using GameActions;
using Resonator.AI;
using FirstPerson;
using UDK7M;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static partial class UDK7MBossValidation
    {
        const string Stage="AssetStaging/UDK7M/Boss/";const string Key="UDK7MBossValidation";
        static UDK7MArena arena;static int phase;static float at;static Vector3 start,flightStart;static ResonatorEnemy flying,ordinary;static readonly List<object> checks=new List<object>();static bool started,loadingIsolated;
        static UDK7MBossValidation(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=ModeChanged;}
        static void ModeChanged(PlayModeStateChange mode)
        {
            if(mode==PlayModeStateChange.EnteredEditMode&&SessionState.GetBool(Key+"Restore",false))
            {EditorSceneManager.playModeStartScene=AssetDatabase.LoadAssetAtPath<SceneAsset>(SessionState.GetString(Key+"Previous",""));SessionState.SetBool(Key+"Restore",false);}
        }
        static void Assert(bool condition,string message){if(!condition)throw new Exception(message);}
        static string ReportPrefix=>SessionState.GetBool(Key+"ThreePhase",false)?"three_phase":SessionState.GetBool(Key+"Correction",false)?"correction":SessionState.GetBool(Key+"Environment",false)?"environment":"play";
        static void Pass(string name,object details=null){checks.Add(new{name,passed=true,details});File.WriteAllText(Stage+ReportPrefix+"_progress.json",JsonConvert.SerializeObject(checks,Formatting.Indented));}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(Application.isPlaying&&SessionState.GetBool(Key,false)){Application.runInBackground=true;Time.timeScale=1;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();}
            if(!Application.isPlaying&&File.Exists(Stage+"ThreePhaseCheck.request")){File.Delete(Stage+"ThreePhaseCheck.request");SessionState.SetBool(Key+"ThreePhase",true);File.WriteAllText(Stage+"PlayCheck.request","three complete cycles");}
            if(!Application.isPlaying&&File.Exists(Stage+"CorrectionCheck.request")){File.Delete(Stage+"CorrectionCheck.request");SessionState.SetBool(Key+"Correction",true);File.WriteAllText(Stage+"PlayCheck.request","live fist kill, tracks and unified turning");}
            if(!Application.isPlaying&&File.Exists(Stage+"EnvironmentCheck.request")){File.Delete(Stage+"EnvironmentCheck.request");SessionState.SetBool(Key+"Environment",true);File.WriteAllText(Stage+"PlayCheck.request","environment mechanics");}
            if(!Application.isPlaying&&File.Exists(Stage+"EditorCheck.request")){File.Delete(Stage+"EditorCheck.request");EditorChecks();return;}
            if(!Application.isPlaying&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Stage+"PlayCheck.request"))
            {
                File.Delete(Stage+"PlayCheck.request");loadingIsolated=false;for(int i=0;i<SceneManager.sceneCount;i++){var scene=SceneManager.GetSceneAt(i);if(scene.isDirty)EditorSceneManager.SaveScene(scene,Stage+"UserBeforeRegression_"+i+".unity",true);}started=false;phase=0;checks.Clear();SessionState.SetBool(Key,true);SessionState.SetBool(Key+"Restore",true);SessionState.SetString(Key+"Previous",AssetDatabase.GetAssetPath(EditorSceneManager.playModeStartScene));EditorSceneManager.playModeStartScene=null;EditorApplication.EnterPlaymode();return;
            }
            if(!Application.isPlaying||!SessionState.GetBool(Key,false)||Time.timeSinceLevelLoad<.5f)return;
            try{if(!started){string target=SessionState.GetBool(Key+"ThreePhase",false)?UDK7MThreePhaseBuilder.ScenePath:UDK7MBossBuilder.ScenePath;if(!loadingIsolated){foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).ToArray())foreach(var go in scene.GetRootGameObjects())go.SetActive(false);EditorSceneManager.LoadSceneInPlayMode(target,new LoadSceneParameters(LoadSceneMode.Additive));loadingIsolated=true;return;}var loaded=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).LastOrDefault(s=>s.path==target&&s.isLoaded);if(!loaded.isLoaded)return;SceneManager.SetActiveScene(loaded);typeof(CombatEffects).GetMethod("Reset",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,null);started=true;phase=0;checks.Clear();arena=loaded.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).FirstOrDefault();Assert(arena,"Arena not loaded");}if(SessionState.GetBool(Key+"ThreePhase",false))RunThreePhase();else if(SessionState.GetBool(Key+"Correction",false))RunCorrections();else if(SessionState.GetBool(Key+"Environment",false))RunEnvironment();else RunPlay();}
            catch(Exception e){Finish(false,e.ToString());}
        }
        static void Finish(bool success,string error=null)
        {
            File.WriteAllText(Stage+ReportPrefix+"_validation.json",JsonConvert.SerializeObject(new{passed=success,error,checks},Formatting.Indented));SessionState.SetBool(Key+"Environment",false);SessionState.SetBool(Key+"Correction",false);SessionState.SetBool(Key+"ThreePhase",false);SessionState.SetBool(Key,false);Time.timeScale=1;EditorApplication.ExitPlaymode();
        }
        static void Player(Vector3 position)
        {
            var motor=arena.player.GetComponent<FirstPersonMotor>();motor.Body.enabled=false;arena.player.transform.SetPositionAndRotation(position,Quaternion.identity);motor.Body.enabled=true;motor.StopMomentum();motor.SetLookRotation(Quaternion.identity);Physics.SyncTransforms();
        }
        static void Boss(Vector3 position)
        {
            arena.boss.motor.agent.Warp(position);arena.boss.transform.SetPositionAndRotation(position,Quaternion.identity);arena.boss.motor.Stop();Physics.SyncTransforms();
        }
        static void RunPlay()
        {
            var boss=arena.boss;var combat=arena.player.gauntlet;var vitals=arena.player.vitals;var motor=arena.player.GetComponent<FirstPersonMotor>();
            if(phase==0)
            {
                boss.enabled=false;motor.enabled=false;arena.player.GetComponent<FirstPersonInput>().enabled=false;arena.player.GetComponent<CrystalParkour.CrystalTraversal>().enabled=false;Cursor.lockState=CursorLockMode.None;combat.ResetResonance();combat.Cancel();vitals.Restore();Player(new Vector3(0,0,-18));
                combat.BeginBlock();var outcome=arena.player.ReceiveStrike(new ActionDamage(30,arena.player.transform.position+Vector3.forward*2,3,.2f,true,.6f));Assert(Mathf.Abs(vitals.Health-100)<.01f&&outcome==StrikeResult.Blocked&&combat.BlockEnergy==1,"Perfect guard failed");
                combat.Cancel();combat.BeginBlock();combat.actions.motion.Seek(combat.actions.motion.Beat.End);combat.Tick(.001f);Assert(combat.State==GauntletCombat.Phase.ParryKeep,"Could not enter imperfect guard");arena.player.ReceiveStrike(new ActionDamage(30,arena.player.transform.position+Vector3.forward*2));Assert(Mathf.Abs(vitals.Health-89.8f)<.02f&&combat.BlockEnergy==2,"Imperfect guard damage/energy failed");Pass("Perfect/imperfect guard",new{perfectDamage=0,imperfectDamage=10.2f,energy=combat.BlockEnergy});
                combat.Cancel();combat.GrantResonance(15);combat.Press();combat.Tick(.7f);Assert(combat.State==GauntletCombat.Phase.Full&&combat.BlockEnergy==3,"Resonance charge skip failed");combat.Cancel();Pass("15 second shared energy charge skip");vitals.Restore();arena.player.invulnerableForTesting=true;
                Player(new Vector3(0,0,-16));var normal=AssetDatabase.LoadAssetAtPath<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset");var attackHit=normal.attack.hits.OrderByDescending(h=>h.damage).First();start=arena.player.transform.position;arena.player.ReceiveStrike(ActionDamage.From(attackHit,start+Vector3.forward*2));for(int i=0;i<12;i++)motor.Tick(.02f);float push=Vector3.ProjectOnPlane(arena.player.transform.position-start,Vector3.up).magnitude;Assert(push>.7f&&push<.9f,"Resonator Attack knockback "+push);Pass("Resonator Attack knockback",new{metres=push});
                Player(new Vector3(0,0,-16));var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.name="Temporary knockback test wall";wall.transform.position=new Vector3(0,1,-16.6f);wall.transform.localScale=new Vector3(5,2,.2f);Physics.SyncTransforms();start=arena.player.transform.position;arena.player.ReceiveStrike(ActionDamage.From(attackHit,start+Vector3.forward*2));for(int i=0;i<12;i++)motor.Tick(.02f);push=Vector3.ProjectOnPlane(arena.player.transform.position-start,Vector3.up).magnitude;Assert(push<.5f,"Knockback crossed a wall");wall.SetActive(false);UnityEngine.Object.Destroy(wall);Pass("Knockback stops at solid wall");Player(new Vector3(0,0,-18));
                arena.BeginFight();Assert(boss.Engaged,"Entry fight did not engage");
                Boss(new Vector3(0,0,3));start=boss.transform.position;boss.ForceState(BossState.SlamRecover);boss.Tick(1);float distance=Vector3.Distance(start,boss.transform.position);Assert(distance>3.8f&&distance<4.2f,"Slam braking travel "+distance);Pass("Slam braking",new{metres=distance});
                Boss(new Vector3(0,0,-10));Player(new Vector3(0,0,21));boss.ForceState(BossState.SlamHold);boss.Tick(2.05f);Assert(boss.State==BossState.SlamRecover,"Slam timeout failed: "+boss.State);Pass("Slam two-second timeout");
                Boss(Vector3.zero);Player(new Vector3(0,0,6));boss.ForceState(BossState.SlamHold);boss.Tick(.25f);Assert(boss.State==BossState.SlamRecover,"Slam front distance gate failed");Pass("Slam front band crossing");
                Boss(Vector3.zero);Player(new Vector3(0,0,-15));start=boss.transform.position;boss.ForceState(BossState.SlamHold,true);boss.Tick(.5f);Assert(Vector3.Distance(start,boss.transform.position)<.01f&&boss.HoldElapsed<.01f,"Rear aiming moved or counted time early");Pass("Rear aiming remains stationary");
                Boss(new Vector3(0,0,-12));Player(new Vector3(0,0,22));boss.ForceState(BossState.ChargeHold);boss.Tick(2.55f);Assert(boss.State==BossState.ChargeRecover,"Charge duration failed: "+boss.State);Pass("Charge 2.5 second timeout");
                boss.ResetBoss();boss.Engage();Boss(Vector3.zero);boss.ReceiveActionDamage(new ActionDamage(160,arena.player.transform.position));Assert(Mathf.Abs(boss.Health-992)<.01f,"Boss punch resistance failed");Pass("Boss full punch damage",new{damage=1000-boss.Health});boss.ResetBoss();boss.Engage();Boss(Vector3.zero);Player(new Vector3(0,0,-19));
                Assert(arena.pillars[0].Consume(boss)&&!arena.pillars[0].Consume(boss),"Pillar was reusable");Assert(arena.minions.Running&&combat.ResonanceRemaining>14,"Pillar did not activate buff and population");Pass("One-use crystal pillar");at=Time.time;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time-at<4.3f)return;Assert(arena.minions.Living==3,"Population did not reach three");Assert(arena.minions.Actors.Where(e=>e.gameObject.activeSelf&&e.ActionTargetAlive).All(e=>e.State!=EnemyState.Waking),"Wakeup did not finish in four seconds");Pass("Hidden population and four-second wake",new{living=arena.minions.Living});
                Boss(new Vector3(0,0,18));flying=arena.minions.Actors.First(e=>e.ActionTargetAlive);flying.motor.agent.Warp(new Vector3(0,0,-7));flying.transform.position=new Vector3(0,0,-7);flying.motion.Play(flying.tuning.idleRestless,immediate:true);flying.ReceiveActionDamage(new ActionDamage(160,new Vector3(0,0,-9),chargeLevel:3));Assert(flying.ragdoll.Active&&flying.State==EnemyState.Dead,"Tier-three lethal ragdoll was delayed");flightStart=flying.ragdoll.Centre;Assert(arena.minions.LastRespawnDelay>=3&&arena.minions.LastRespawnDelay<=5,"Ordinary respawn delay wrong");at=Time.time;phase=2;return;
            }
            if(phase==2)
            {
                if(Time.time-at<1.2f)return;float distance=Vector3.ProjectOnPlane(flying.ragdoll.Centre-flightStart,Vector3.up).magnitude;Assert(distance>=7&&distance<=13,"Launch distance outside tolerance: "+distance);Pass("Tier-three physical launch",new{metres=distance});
                ordinary=arena.minions.Actors.First(e=>e.ActionTargetAlive);ordinary.ReceiveActionDamage(new ActionDamage(160,ordinary.transform.position-Vector3.forward,chargeLevel:1));Assert(ordinary.State==EnemyState.Dying&&!ordinary.ragdoll.Active,"Light kill skipped death animation");at=Time.time;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.time-at<1.8f)return;Assert(ordinary.ragdoll.Active,"Normal death did not reach ragdoll frame 50");Pass("Normal frame-50 death ragdoll");
                var victim=arena.minions.Actors.FirstOrDefault(e=>e.ActionTargetAlive);Assert(victim,"Missing resonance damage victim");victim.ReceiveActionDamage(new ActionDamage(130,victim.transform.position,cause:DamageCause.Resonance));Assert(arena.minions.LastDeathCause==DamageCause.Resonance&&arena.minions.LastRespawnDelay>=15&&arena.minions.LastRespawnDelay<=20,"Resonance respawn delay wrong");Pass("Separate resonance respawn cooldown",new{seconds=arena.minions.LastRespawnDelay});
                Boss(Vector3.zero);boss.ForceState(BossState.Idle);boss.Tick(.1f);Assert(boss.DrillSpinning,"Idle drill did not report local-axis rotation");
                var offset=boss.DrillTip+Vector3.forward*3+Vector3.up*.2f-flying.ragdoll.Centre;foreach(var body in flying.ragdoll.bodies)body.position+=offset;flying.ragdoll.Launch(Vector3.zero);Physics.SyncTransforms();at=Time.time;phase=4;return;
            }
            if(phase==4)
            {
                boss.runner.motion.Advance(Time.deltaTime);boss.SendMessage("FixedUpdate");
                if(boss.BodiesConsumed==0){if(Time.time-at>6)throw new Exception("Corpse vacuum failed; distance="+Vector3.Distance(flying.ragdoll.Centre,boss.DrillTip));return;}
                Assert(Mathf.Abs(boss.Health-860)<.01f,"First corpse resonance damage wrong");float before=boss.Health;boss.Consume(flying);Assert(boss.Health==before,"Corpse consumed twice");Pass("Physical corpse vacuum and single consumption");
                Time.timeScale=3;at=Time.time;phase=7;return;
            }
            if(phase==7)
            {
                if(!arena.Won)
                {
                    boss.runner.motion.Advance(Time.deltaTime);boss.SendMessage("FixedUpdate");
                    if(flying&&flying.gameObject.activeSelf&&!flying.ActionTargetAlive&&!flying.Absorbed){Assert(Time.time-at<12,"Repeated physical vacuum stalled");return;}
                    var candidate=arena.minions.Actors.FirstOrDefault(e=>e.gameObject.activeSelf&&e.ActionTargetAlive);
                    if(!candidate){Assert(Time.time-at<30,"No replenishment during full corpse loop");return;}
                    flying=candidate;flying.motion.Play(flying.tuning.idleRestless,immediate:true);flying.ReceiveActionDamage(new ActionDamage(160,flying.transform.position-Vector3.forward*2,chargeLevel:3));
                    Assert(flying.ragdoll.Active,"Reused minion did not become an immediate tier-three corpse");var offset=boss.DrillTip+Vector3.forward*3+Vector3.up*.2f-flying.ragdoll.Centre;
                    foreach(var body in flying.ragdoll.bodies)body.position+=offset;flying.ragdoll.Launch(Vector3.zero);Physics.SyncTransforms();at=Time.time;return;
                }
                Time.timeScale=1;Assert(boss.BodiesConsumed==8,"Victory did not use eight physically consumed corpses");
                Assert(arena.Won&&boss.Health==0&&boss.gameObject.activeSelf&&!arena.minions.Running&&!boss.DrillSpinning,"Eight resonance victory failed");Pass("Eight resonances defeat boss without destroying root");
                Pass("Eight physical corpse ingestions across repeated population reuse");
                arena.ResetEncounter();boss.enabled=false;Assert(!arena.Started&&!arena.Won&&boss.Health==1000&&arena.pillars.All(p=>!p.Consumed)&&!arena.minions.Running,"Encounter reset failed");arena.BeginFight();arena.pillars[0].Consume(boss);at=Time.time;phase=5;return;
            }
            if(phase==5)
            {
                if(Time.time-at<.7f)return;Assert(arena.minions.Living==3,"Population did not recover after reset");foreach(var e in arena.minions.Actors.Where(e=>e.gameObject.activeSelf)){Assert(e.Health==120&&!e.ragdoll.Active&&e.GetComponentsInChildren<Rigidbody>().Length==0&&e.GetComponentsInChildren<CharacterJoint>().Length==0&&e.motor.agent.isOnNavMesh,"Pooled actor retained ragdoll/health/navigation state");}
                Pass("Pool reuse resets physics, health and navigation");arena.ResetEncounter();boss.enabled=true;Player(new Vector3(0,0,-18));arena.player.invulnerableForTesting=false;motor.enabled=true;arena.player.GetComponent<FirstPersonInput>().enabled=true;arena.player.GetComponent<CrystalParkour.CrystalTraversal>().enabled=true;arena.BeginFight();at=Time.time;phase=6;return;
            }
            if(phase==6)
            {
                if(Time.time-at<1)return;
                var camera=arena.player.eyes.GetComponentInChildren<Camera>();if(!camera)camera=Camera.main;Assert(camera,"No gameplay camera");AssetReviewUtility.Capture(arena.gameObject.scene,camera,Stage+"Boss_Gameplay.png");Pass("Live encounter render");Finish(true);
            }
        }
        static void EditorChecks()
        {
            var report=new List<object>();var prior=SceneManager.GetActiveScene();var preview=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);GameObject actor=null,target=null;
            try
            {
                var set=AssetDatabase.LoadAssetAtPath<ActionSet>("Assets/Game/Enemies/UDK7M/Actions/UDK7MActions.asset");Assert(set&&set.previewModel,"Missing workbench profile");actor=(GameObject)PrefabUtility.InstantiatePrefab(set.previewModel,preview);actor.transform.position=Vector3.one*1000;var motion=actor.GetComponentInChildren<ActionMotionPlayer>();var resolver=new ActionHitbox(actor.transform);var hit=set.Find("SlamRecover").action.hits.First();Assert(resolver.HasAnchor(hit.anchor,hit.customBone),"Full bone path did not resolve");
                foreach(var action in set.actions.Where(a=>a.id!="OriginalDemo")){motion.Play(action.Motion,0,true);foreach(float f in new[]{action.Motion.startFrame,(action.Motion.End+action.Motion.startFrame)/2,action.Motion.End}){motion.Seek(f);var pose=resolver.Pose(hit);Assert(Vector3.Distance(pose.centre,resolver.Anchor(hit.anchor,hit.customBone).position+resolver.Anchor(hit.anchor,hit.customBone).rotation*hit.position)<.0001f,"Bone hitbox drift");}}
                motion.SampleBlend(set.Find("SlamPrepare").Motion,15,set.Find("SlamHold").Motion,0,.08f,.04f);Assert(Mathf.Abs(motion.DestinationWeight-.5f)<.001f,"Blend preview failed");report.Add(new{test="All action bone paths, poses and blend",passed=true});
                motion.Play(set.Find("Idle").Motion,0,true);var tread=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Tread_L_00");var reference=tread.localPosition;var tracks=new UDK7MTracks(actor.transform);
                motion.Seek(20);tracks.Apply();Assert(Vector3.Distance(tread.localPosition,reference)<.0001f,"Stationary track advanced with arm clock");actor.transform.position+=actor.transform.forward;tracks.Apply();Assert(Vector3.Distance(tread.localPosition,reference)>.05f,"Track did not follow actual displacement");var stopped=tread.localPosition;motion.Seek(25);tracks.Apply();Assert(Vector3.Distance(tread.localPosition,stopped)<.0001f,"Blocked track kept rolling");actor.transform.Rotate(0,10,0);tracks.Apply();Assert(Vector3.Distance(tread.localPosition,stopped)>.01f,"Track did not counter-roll on turning");report.Add(new{test="Tracks follow physical travel and turning, stop when blocked",passed=true});
                Assert(!resolver.HasAnchor(HitboxAnchor.CustomBone,"Missing/Bone"),"Invalid bone silently fell back");
                target=new GameObject("Contact test target");SceneManager.MoveGameObjectToScene(target,preview);target.transform.position=actor.transform.position;var vitals=target.AddComponent<FirstPersonVitals>();vitals.Restore();var receiver=target.AddComponent<ResonatorTarget>();receiver.vitals=vitals;target.AddComponent<CapsuleCollider>().center=Vector3.up;var extra=new GameObject("Second collider");extra.transform.SetParent(target.transform,false);extra.AddComponent<BoxCollider>().center=Vector3.up;
                foreach(var mode in new[]{HitRepeat.Once,HitRepeat.EffectsOnly,HitRepeat.Cooldown,HitRepeat.EverySample})
                {
                    vitals.Restore();var h=new HitWindow{startFrame=0,endFrame=30,position=Vector3.up,radius=1,shape=HitboxShape.Sphere,damage=3,repeat=mode,sampleEveryFrames=2};var definition=new ActionDefinition{id="Test",isAttack=true,action=new AttackBeat{animation=set.Find("Idle").Motion,hits=new[]{h}}};var contact=new ActionContact();motion.Play(definition.Motion,0,true);
                    for(int i=0;i<10;i++){motion.Seek(i);Physics.SyncTransforms();contact.Resolve(definition,motion,resolver,actor.transform,actor.transform.position+Vector3.up,~0,0,1/30f);}
                    float expected=mode==HitRepeat.Once?97:mode==HitRepeat.EffectsOnly?100:mode==HitRepeat.Cooldown?94:85;Assert(Mathf.Abs(vitals.Health-expected)<.001f,"Contact cadence "+mode+": "+vitals.Health);report.Add(new{test="Contact "+mode,health=vitals.Health,passed=true});
                }
                vitals.Restore();var spin=new GameObject("Spin test");spin.transform.SetParent(actor.transform,false);spin.transform.localPosition=Vector3.up;resolver=new ActionHitbox(actor.transform);var rotatingHit=new HitWindow{startFrame=0,endFrame=30,anchor=HitboxAnchor.CustomBone,customBone="Spin test",position=Vector3.zero,radius=1,shape=HitboxShape.Sphere,damage=3,repeat=HitRepeat.EverySample,sampleEveryFrames=2,onlyWhileRotating=true};var rotatingAction=new ActionDefinition{id="Spin",isAttack=true,action=new AttackBeat{animation=set.Find("Idle").Motion,hits=new[]{rotatingHit}}};var spinContact=new ActionContact();motion.Play(rotatingAction.Motion,0,true);motion.Seek(0);Physics.SyncTransforms();spinContact.Resolve(rotatingAction,motion,resolver,actor.transform,actor.transform.position+Vector3.up,~0,0,.033f);actor.transform.Rotate(0,90,0);motion.Seek(2);Physics.SyncTransforms();spinContact.Resolve(rotatingAction,motion,resolver,actor.transform,actor.transform.position+Vector3.up,~0,0,.067f);Assert(vitals.Health==100,"Parent turn incorrectly activated drill damage");spin.transform.localRotation=Quaternion.Euler(0,30,0);motion.Seek(4);Physics.SyncTransforms();spinContact.Resolve(rotatingAction,motion,resolver,actor.transform,actor.transform.position+Vector3.up,~0,0,.067f);Assert(vitals.Health==97,"Local drill roll did not activate contact");report.Add(new{test="Local rotation excludes parent turning",passed=true});
                File.WriteAllText(Stage+"editor_validation.json",JsonConvert.SerializeObject(new{passed=true,checks=report},Formatting.Indented));
            }
            catch(Exception e){File.WriteAllText(Stage+"editor_validation.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString(),checks=report},Formatting.Indented));Debug.LogException(e);}
            finally{if(actor)UnityEngine.Object.DestroyImmediate(actor);if(target)UnityEngine.Object.DestroyImmediate(target);SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(preview,true);}
        }
    }
}

