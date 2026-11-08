using System.Linq;
using UnityEngine;
using GameActions;
using FirstPerson;
using Resonator.AI;
using UDK7M;
namespace AssetPipeline.Editor
{
    public static partial class UDK7MBossValidation
    {
        static ResonatorEnemy fistVictim;static Vector3 lethalOrigin;static int killedSignals;static float flightDistance;static UDK7MCrush wheelTest;
        static ResonatorEnemy CorrectionEnemy(Vector3 position)
        {
            var go=UnityEngine.Object.Instantiate(arena.minions.prefab);go.name="Standing correction target";go.transform.position=position;var enemy=go.GetComponent<ResonatorEnemy>();enemy.tuning=arena.minions.profile;enemy.target=arena.player;enemy.Initialize(RestActivity.RestlessIdle);enemy.motion.Play(enemy.tuning.idleRestless,immediate:true);enemy.enabled=false;return enemy;
        }
        static void RunCorrections()
        {
            var boss=arena.boss;var combat=arena.player.gauntlet;var motor=arena.player.GetComponent<FirstPersonMotor>();
            if(phase==0)
            {
                boss.enabled=false;motor.enabled=false;arena.player.GetComponent<FirstPersonInput>().enabled=false;arena.player.GetComponent<CrystalParkour.CrystalTraversal>().enabled=false;combat.enabled=false;combat.Cancel();Cursor.lockState=CursorLockMode.None;arena.player.invulnerableForTesting=true;
                Boss(new Vector3(0,0,18));Player(new Vector3(0,0,-18));fistVictim=CorrectionEnemy(new Vector3(0,0,-16.5f));killedSignals=0;fistVictim.Killed+=_=>killedSignals++;
                combat.GrantResonance(15);combat.Press();combat.Tick(.7f);Assert(combat.State==GauntletCombat.Phase.Full,"Could not charge actual fist");combat.Release();Assert(combat.ActivePunch==4,"Full charge did not select punch4");
                for(int i=0;i<45&&fistVictim.ActionTargetAlive;i++)combat.Tick(1/120f);
                var probe=new ActionHitbox(arena.player.transform);probe.Bind(HitboxAnchor.Root,combat.attackOrigin);var hitPose=probe.Pose(combat.profile.Find("punch4").action.hits[0]);
                Assert(!fistVictim.ActionTargetAlive&&fistVictim.ragdoll.Active&&fistVictim.State==EnemyState.Dead,"Level forward-facing fist did not kill and launch a standing enemy; health="+fistVictim.Health+", contacts="+combat.ContactsThisAttack+", eye="+combat.attackOrigin.position+", forward="+combat.attackOrigin.forward+", box="+hitPose.centre+", enemy="+fistVictim.GetComponent<Collider>().bounds+", player="+arena.player.transform.position);Assert(killedSignals==1,"Lethal strike callback repeated");
                lethalOrigin=fistVictim.ragdoll.Centre;flightDistance=0;at=Time.time;phase=1;Pass("Actual level-view punch4 lethal strike immediately launches standing ragdoll");return;
            }
            if(phase==1)
            {
                flightDistance=Mathf.Max(flightDistance,Vector3.ProjectOnPlane(fistVictim.ragdoll.Centre-lethalOrigin,Vector3.up).magnitude);if(Time.time-at<1.5f)return;
                Assert(flightDistance>=8&&flightDistance<=14,"Standing lethal fist launch distance: "+flightDistance);Assert(killedSignals==1,"Corpse generated another death");Pass("Physical standing lethal launch without attacking corpse",new{metres=flightDistance});UnityEngine.Object.Destroy(fistVictim.gameObject);Player(new Vector3(0,0,-22));arena.BeginFight();
                foreach(var angle in new[]{-170f,-90f,61f,90f,170f})
                {
                    Boss(Vector3.zero);Player(Quaternion.Euler(0,angle,0)*Vector3.forward*17);boss.ForceState(BossState.ChargeHold);start=boss.transform.position;boss.Tick(.2f);
                    Assert(boss.State==BossState.Turning&&Vector3.Distance(start,boss.transform.position)<.001f&&boss.HoldElapsed==0,"Charge moved before unified alignment at "+angle);
                    Assert(boss.runner.Current.id=="SlamHold","Turning did not use common SlamHold pose");
                }
                Boss(Vector3.zero);Player(Quaternion.Euler(0,90,0)*Vector3.forward*18);boss.ForceState(BossState.SlamHold);boss.Tick(.5f);Assert(boss.State==BossState.Turning,"Slam did not enter turn state");float angleNow=boss.motor.Angle(arena.player.transform.position);Assert(angleNow>64&&angleNow<66,"Unified turning is not 50 degrees/sec: "+angleNow);start=boss.transform.position;
                for(int i=0;i<180&&boss.State==BossState.Turning;i++)boss.Tick(1/120f);Assert(boss.State==BossState.SlamHold&&boss.HoldElapsed==0&&boss.motor.Angle(arena.player.transform.position)<=5&&Vector3.Distance(start,boss.transform.position)<.001f,"Turning did not finish stationary and reset hold timer");boss.Tick(.1f);Assert(boss.HoldElapsed>.09f&&Vector3.Distance(start,boss.transform.position)>.7f,"Aligned boss did not launch SlamHold charge");Pass("Both charge types use stationary 60-degree gate, 50-degree turn and fresh SlamHold timer");
                Boss(Vector3.zero);Player(new Vector3(0,0,-22));boss.ForceState(BossState.Idle);wheelTest=new UDK7MCrush(boss.transform,boss.tuning);for(int side=0;side<2;side++)Assert(wheelTest.LocalBounds(side).size.x<2&&wheelTest.LocalBounds(side).size.x>.4f,"Track bounds crossed both sides or missed shoe width");
                fistVictim=CorrectionEnemy(boss.transform.TransformPoint(wheelTest.LocalBounds(0).center)+Vector3.back*.5f);fistVictim.motor.agent.Warp(new Vector3(fistVictim.transform.position.x,0,fistVictim.transform.position.z));fistVictim.transform.position=new Vector3(fistVictim.transform.position.x,0,fistVictim.transform.position.z);Physics.SyncTransforms();float health=fistVictim.Health;wheelTest.Tick(.02f,boss.transform.position,boss.transform.rotation);Assert(fistVictim.Health==health,"Stationary tracks damaged enemy");
                var before=boss.transform.position;boss.transform.position+=Vector3.forward*.15f;Physics.SyncTransforms();wheelTest.Tick(.02f,before,boss.transform.rotation);Assert(fistVictim.Health<health&&wheelTest.Hits==1,"Moving tracks did not damage a living Resonator");health=fistVictim.Health;before=boss.transform.position;boss.transform.position+=Vector3.forward*.05f;Physics.SyncTransforms();wheelTest.Tick(.02f,before,boss.transform.rotation);Assert(fistVictim.Health==health,"Track cooldown or collider deduplication failed");start=fistVictim.transform.position;for(int i=0;i<15;i++)fistVictim.motor.AdvanceKnockback(.02f);Assert(Vector3.Distance(start,fistVictim.transform.position)>.2f,"Track damage did not push enemy");Pass("Moving track bounds damage and push Resonator, with cooldown and stationary safety");
                UnityEngine.Object.Destroy(fistVictim.gameObject);at=Time.time;phase=2;return;
            }
            if(phase==2)
            {
                if(Time.time-at<.15f)return;Boss(Vector3.zero);boss.ForceState(BossState.Idle);var contact=boss.actions.Find("Idle").action.hits.First(h=>h.onlyWhileRotating);var boxes=new ActionHitbox(boss.transform);boxes.BindProfile(boss.actions);var pose=boxes.Pose(contact);fistVictim=CorrectionEnemy(new Vector3(pose.centre.x,0,pose.centre.z));fistVictim.transform.position=new Vector3(pose.centre.x,Mathf.Max(0,pose.centre.y-1),pose.centre.z);Physics.SyncTransforms();float health=fistVictim.Health;boss.Tick(.2f);Assert(fistVictim.Health<health,"Boss drill hitbox still filtered out Resonators");Pass("Shared rotating drill hitbox damages Resonator");UnityEngine.Object.Destroy(fistVictim.gameObject);arena.ResetEncounter();Finish(true);
            }
        }
    }
}
