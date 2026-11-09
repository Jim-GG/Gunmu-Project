using System.Linq;
using UnityEngine;
using FirstPerson;
using Resonator.AI;
using UDK7M;
namespace AssetPipeline.Editor
{
    public static partial class UDK7MBossValidation
    {
        static ResonatorEnemy fistVictim;static Vector3 lethalOrigin;static int killedSignals;static float flightDistance;
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
                Assert(!fistVictim.ActionTargetAlive&&fistVictim.ragdoll.Active&&fistVictim.State==EnemyState.Dead,"Standing full fist did not kill; health="+fistVictim.Health+", hits="+combat.ContactsThisAttack);Assert(killedSignals==1,"Lethal strike callback repeated");
                lethalOrigin=fistVictim.ragdoll.Centre;flightDistance=0;at=Time.time;phase=1;Pass("Actual level-view punch4 lethal strike immediately activates standing ragdoll");return;
            }
            if(phase==1)
            {
                flightDistance=Mathf.Max(flightDistance,Vector3.ProjectOnPlane(fistVictim.ragdoll.Centre-lethalOrigin,Vector3.up).magnitude);if(Time.time-at<1.5f)return;
                Assert(flightDistance>=8&&flightDistance<=14,"Standing lethal fist launch distance: "+flightDistance);Assert(killedSignals==1,"Corpse generated another death");Pass("Physical standing lethal launch without attacking corpse",new{metres=flightDistance});UnityEngine.Object.Destroy(fistVictim.gameObject);Finish(true);
            }
        }
    }
}
