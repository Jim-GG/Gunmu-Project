using System.Linq;
using UnityEngine;
using GameActions;
using Resonator.AI;
using FirstPerson;
using CrystalParkour;
using UDK7M;
namespace AssetPipeline.Editor
{
    public static partial class UDK7MBossValidation
    {
        static Transform[] warnings;static Vector3[] warningPoints;static UDK7MDebris pickup;static float envStart,healthBefore,highest;
        static void RunEnvironment()
        {
            var boss=arena.boss;var combat=arena.player.gauntlet;var vitals=arena.player.vitals;var motor=arena.player.GetComponent<FirstPersonMotor>();var traversal=arena.player.GetComponent<CrystalTraversal>();
            if(phase==0)
            {
                Random.InitState(470);boss.enabled=false;motor.enabled=false;arena.player.GetComponent<FirstPersonInput>().enabled=false;traversal.enabled=false;Cursor.lockState=CursorLockMode.None;arena.player.invulnerableForTesting=true;Player(new Vector3(0,0,-19));arena.BeginFight();boss.ForceState(BossState.TripleSlam);boss.Tick(2.1f);
                warnings=arena.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).Where(t=>t.name.StartsWith("Locked falling debris warning")).ToArray();Assert(warnings.Length==5,"Third slam did not create five warnings");warningPoints=warnings.Select(w=>w.position).ToArray();Assert(warningPoints.Count(p=>Vector3.ProjectOnPlane(p-arena.player.transform.position,Vector3.up).magnitude<2.5f)>=2,"Two warnings did not target the original player position");Player(new Vector3(0,0,-23));at=envStart=Time.time;phase=1;return;
            }
            if(phase==1)
            {
                if(Time.time-at<.35f)return;for(int i=0;i<warnings.Length;i++)Assert(warnings[i]&&Vector3.Distance(warnings[i].position,warningPoints[i])<.001f,"Warning followed the moving player");Pass("Third-slam five locked warnings, including two near player");phase=2;return;
            }
            if(phase==2)
            {
                if(Time.time-envStart<3.5f)return;var debris=UnityEngine.Object.FindObjectsByType<UDK7MDebris>(FindObjectsSortMode.None);var crystals=debris.Where(d=>d.IsCrystal).ToArray();
                if((debris.Length<5||crystals.Count(d=>d.Landed)<2)&&Time.time-envStart<6)return;
                Assert(debris.Length==5&&crystals.Length==2&&crystals.All(d=>d.Landed),"Falling debris did not produce three rocks and two landed crystals");Pass("Physical falling rocks and two landed crystal pickups");pickup=crystals[0];combat.ResetResonance();Player(pickup.transform.position-Vector3.up*.6f);at=Time.time;phase=3;return;
            }
            if(phase==3)
            {
                if(Time.time-at<.2f)return;Assert(!pickup.gameObject.activeSelf&&combat.BlockEnergy>=1,"Crystal pickup did not grant energy");Pass("Landed crystal grants shared gauntlet energy");arena.player.invulnerableForTesting=false;vitals.Restore();Player(new Vector3(-10,-4.45f,-6));healthBefore=vitals.Health;at=Time.time;phase=4;return;
            }
            if(phase==4)
            {
                motor.Tick(Mathf.Min(.05f,Time.deltaTime));if(Time.time-at<1.1f)return;float loss=healthBefore-vitals.Health;Assert(loss>5&&loss<25,"Liquid damage rate incorrect: "+loss);Pass("Liquid applies continuous damage",new{loss});vitals.Restore();arena.player.invulnerableForTesting=true;
                var surface=arena.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CrystalSurface>()).Where(s=>s.name=="Lower level escape crystal").OrderBy(s=>Vector3.Distance(s.transform.position,new Vector3(-10,-1.3f,-4.9f))).First();
                Assert(Vector3.Dot(surface.WorldLaunchNormal,Vector3.up)>.99f,"Escape launch axis is not world-up");var point=surface.contact.ClosestPoint(new Vector3(-10,-2,-5));Player(new Vector3(-10,Mathf.Max(-4.4f,point.y-2.5f),-5));traversal.enabled=true;
                traversal.Tick(.6f,false,true);Assert(traversal.BeginTether(surface,point,Vector3.up),"Could not acquire lower crystal");for(int i=0;i<15&&traversal.State==CrystalTraversal.Mode.Pulling;i++)traversal.Tick(.02f,true,true);Assert(traversal.State==CrystalTraversal.Mode.Attached,"Escape crystal unreachable through grating");traversal.SendMessage("AttackBegan",4);Assert(traversal.Strike(surface),"Crystal rebound failed");highest=motor.transform.position.y;at=Time.time;phase=5;return;
            }
            if(phase==5)
            {
                motor.Tick(Mathf.Min(.05f,Time.deltaTime));highest=Mathf.Max(highest,motor.transform.position.y);if(Time.time-at<1.35f)return;Assert(highest>.1f,"Rebound did not reach upper floor: "+highest);Pass("Lower liquid basin escapes through crystal rebound",new{peakFeetHeight=highest});traversal.enabled=false;Player(new Vector3(0,0,-19));arena.pillars[0].Consume(boss);at=Time.time;phase=6;return;
            }
            if(phase==6)
            {
                if(Time.time-at<.5f)return;Assert(arena.minions.Living==3,"Population activation failed");var e=arena.minions.Actors.First(a=>a.ActionTargetAlive);e.ReceiveActionDamage(new ActionDamage(130,e.transform.position,cause:DamageCause.Resonance));Assert(arena.minions.Living==2,"Killed actor still counted as living");at=Time.time;Time.timeScale=4;phase=7;return;
            }
            if(phase==7)
            {
                if(Time.time-at<1)return;Assert(arena.minions.Living==2,"Resonance death replenished too early");Pass("Resonance slot remains vacant before cooldown");phase=8;return;
            }
            if(phase==8)
            {
                if(Time.time-at<21)return;Assert(arena.minions.Living==3,"Resonance slot did not replenish after 15–20 seconds");Pass("Actual resonance cooldown replenishes the slot");var e=arena.minions.Actors.First(a=>a.ActionTargetAlive);e.ReceiveActionDamage(new ActionDamage(160,e.transform.position,chargeLevel:3));at=Time.time;phase=9;return;
            }
            if(phase==9)
            {
                if(Time.time-at<5.5f)return;Assert(arena.minions.Living==3,"Ordinary slot did not replenish after 3–5 seconds");Pass("Actual ordinary cooldown replenishes the slot");Time.timeScale=1;arena.ResetEncounter();Pass("Environmental hazards and population reset");Finish(true);
            }
        }
    }
}
