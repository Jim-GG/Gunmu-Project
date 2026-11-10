using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using Unity.Cinemachine;
using GameActions;
using FirstPerson;
using Resonator.AI;
using CrystalParkour;
using UDK7M;
namespace AssetPipeline.Editor
{
    public static partial class UDK7MBossValidation
    {
        static int cycle;static float freezeFrame;static Vector3 capturePosition;static UDK7MDebris central;static CrystalSurface jumpCrystal;static double warningProbeDSP;
        static void WeightCheck(int slam,int triple,int charge)
        {
            var counts=new int[3];for(int i=0;i<100;i++){var s=arena.boss.SelectAttack((i+.5f)/100);counts[s==BossState.SlamPrepare?0:s==BossState.TripleSlam?1:2]++;}Assert(counts[0]==slam&&counts[1]==triple&&counts[2]==charge,"Phase attack weights mismatch");
        }
        static void RunThreePhase()
        {
            var boss=arena.boss;var motor=arena.player.GetComponent<FirstPersonMotor>();var combat=arena.player.gauntlet;var traversal=arena.player.GetComponent<CrystalTraversal>();var pound=arena.player.GetComponent<UDK7MGroundPound>();
            if(phase==0)
            {
                Application.runInBackground=true;
                boss.enabled=false;motor.enabled=false;combat.enabled=false;traversal.enabled=false;arena.player.GetComponent<FirstPersonInput>().enabled=false;Cursor.lockState=CursorLockMode.None;arena.player.invulnerableForTesting=true;arena.BeginFight();cycle=0;
                Assert(arena.tuning.arenaRadius==36&&arena.tuning.navigationRadius>=3.7f,"Arena/navigation dimensions wrong");for(int x=-28;x<=28;x+=7)for(int z=-28;z<=28;z+=7)if(new Vector2(x,z).magnitude<32)Assert(Physics.Raycast(new Vector3(x,1,z),Vector3.down,out var floor,2,~0,QueryTriggerInteraction.Ignore)&&floor.point.y>=-.4f,"Unsealed floor at "+x+","+z);
                Assert(arena.player.GetComponentsInChildren<CinemachineImpulseListener>(true).Any(l=>(l.ChannelMask&arena.tuning.impulseChannel)!=0),"Missing Cinemachine impulse listener");Pass("72m sealed arena, wide navigation and Cinemachine listener");
                CheckAudioAndRails();CheckAdvancedPresentation();
                var filter=new NavMeshQueryFilter{agentTypeID=boss.motor.agent.agentTypeID,areaMask=NavMesh.AllAreas};foreach(var pillar in arena.pillars){var path=new NavMeshPath();Assert(NavMesh.CalculatePath(pillar.transform.position+new Vector3(-8,0,-8),pillar.transform.position+new Vector3(8,0,8),filter,path)&&path.status==NavMeshPathStatus.PathComplete,"No wide route around pillar");}Pass("Wide boss navigation routes around all four columns");
                Boss(Vector3.zero);Player(new Vector3(0,0,-32));boss.ForceState(BossState.Idle);var wheels=new UDK7MCrush(boss.transform,boss.tuning);var bounds=wheels.LocalBounds(0);var victim=CorrectionEnemy(new Vector3(bounds.center.x,0,bounds.center.z));Physics.SyncTransforms();wheels.Tick(.02f,boss.transform.position,boss.transform.rotation);Assert(victim.ActionTargetAlive,"Stationary wheel damaged minion");var before=boss.transform.position;boss.transform.position+=Vector3.forward*.1f;Physics.SyncTransforms();wheels.Tick(.02f,before,boss.transform.rotation);Assert(!victim.ActionTargetAlive&&victim.ragdoll.Active&&victim.State==EnemyState.Dead,"Moving tracks did not instantly ragdoll minion");victim.gameObject.SetActive(false);UnityEngine.Object.Destroy(victim.gameObject);Pass("Moving tracks instantly ragdoll Resonators; stationary tracks remain safe");
                Boss(Vector3.zero);Player(new Vector3(20,0,20));boss.ForceState(BossState.ChargeHold);var initial=boss.transform.rotation;boss.Tick(.75f);Assert(Quaternion.Angle(initial,boss.transform.rotation)>8&&Quaternion.Angle(initial,boss.transform.rotation)<=13.6f,"Moderate charge steering rate mismatch; angle="+Quaternion.Angle(initial,boss.transform.rotation)+", rate="+boss.tuning.holdTurnSpeed+", cap="+boss.tuning.chargeTrackingAngle+", state="+boss.State);boss.Tick(1.25f);Assert(Quaternion.Angle(initial,boss.transform.rotation)<=30.1f,"Charge exceeded total correction budget");
                Boss(Vector3.zero);Player(new Vector3(20,0,25));boss.ForceState(BossState.SlamHold);initial=boss.transform.rotation;boss.Tick(.75f);Assert(Quaternion.Angle(initial,boss.transform.rotation)>8&&Quaternion.Angle(initial,boss.transform.rotation)<=13.6f,"Moderate Slam steering rate mismatch");Pass("Both charge forms steer at 18 degrees/sec with 30-degree total budget");
                Boss(new Vector3(29,0,0));var boundary=boss.transform.position;boss.motor.Displace(Vector3.right*8,out var normal);Assert(normal.sqrMagnitude>.1f&&boss.transform.position.x<33,"Boundary did not return collision normal");var impact=boss.transform.position;boss.motor.Displace(Vector3.Reflect(Vector3.right*3,normal),out _);Assert(boss.transform.position.x<impact.x-2,"Reflected movement did not bounce back from arena boundary");Pass("Collision-constrained boss push reflects at arena wall");
                Boss(Vector3.zero);Player(new Vector3(0,0,-32));boss.ForceState(BossState.Idle);WeightCheck(50,30,20);
                var nearPillar=arena.pillars[0];Boss(nearPillar.transform.position-nearPillar.transform.position.normalized*8);Assert(boss.NearUnbrokenPillar,"Near-pillar gate failed");WeightCheck(70,10,20);Boss(Vector3.zero);WeightCheck(50,30,20);Pass("Stable phase near pillar uses 70/10/20; far from pillars retains 50/30/20");
                var vfx=Resources.Load<CombatEffectLibrary>("CombatVFX");Assert(vfx&&vfx.Find(CombatEffect.Sparks)?.prefab&&vfx.Find(CombatEffect.Explosion)?.prefab&&arena.feedback.shockwavePrefab,"Editable combat effect prefabs missing");CombatEffects.Emit(CombatEffect.Sparks,new Vector3(0,3,0),Vector3.up);Assert(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Any(p=>p.GetComponentInParent<VFXGroundBinder>()&&p.emission.burstCount>0),"Sparks did not use editable prefab");Assert(arena.feedback.shockwavePrefab.GetComponent<ICombatWave>()!=null&&arena.feedback.shockwavePrefab.GetComponentsInChildren<Renderer>().Any(r=>r.sharedMaterial),"Shockwave prefab is not editable");Pass("Editable Sparks/Explosion library and Shockwave prefab connected");
                boss.ForceState(BossState.SlamPrepare);arena.boss.GetComponentInChildren<MachinePrepareAudio>().Sync();boss.ForceState(BossState.SlamHold);warningProbeDSP=AudioSettings.dspTime;at=Time.time;phase=10;return;
            }
            if(phase==10)
            {
                var warning=boss.GetComponentInChildren<MachinePrepareAudio>();warning.Sync();double elapsed=AudioSettings.dspTime-warningProbeDSP;float length=warning.source.clip.length/Mathf.Max(.1f,warning.source.pitch);if(elapsed<length+.3f||warning.CompletedLoops<1&&elapsed<length*3)return;
                Assert(warning.source.isPlaying&&warning.CompletedLoops>=1,"Warning did not actually wrap/repeat during SlamHold; state="+boss.State+", playing="+warning.source.isPlaying+", time="+warning.source.time+", loops="+warning.CompletedLoops+", nativeLoop="+warning.source.loop+", dspElapsed="+elapsed);boss.ForceState(BossState.SlamRecover);warning.Sync();Assert(!warning.source.isPlaying,"Warning leaked after recover");Pass("Prepare warning continues through SlamHold, audibly loops past clip duration, and stops at recover",new{loops=warning.CompletedLoops,dspSeconds=elapsed});
                boss.ForceState(BossState.TripleSlam);boss.Tick(2.1f);at=Time.time;phase=1;return;
            }            if(phase==1)
            {
                if(Time.time-at<3.5f)return;Assert(UnityEngine.Object.FindObjectsByType<UDK7MDebris>(FindObjectsSortMode.None).All(d=>!d.IsCrystal),"Stable TripleSlam dropped purple crystals");Assert(arena.feedback.SignalsSent>=3,"Slam impulses did not emit");Pass("Phase one 50/30/20 weights, two light/one heavy impulse, rocks without purple crystals");BeginCycle();return;
            }
            if(phase==2)
            {
                if(Time.time-at<.5f)return;Assert(arena.minions.Living==3,"Overloaded phase did not maintain population");WeightCheck(10,40,50);Pass("Cycle "+(cycle+1)+": overloaded 10/40/50 weights and population");
                Boss(Vector3.zero);boss.ForceState(BossState.Idle);boss.Tick(.12f);flying=arena.minions.Actors.First(e=>e.ActionTargetAlive);flying.motion.Play(flying.tuning.idleRestless,immediate:true);flying.ReceiveActionDamage(new ActionDamage(160,flying.transform.position-Vector3.forward,chargeLevel:3,direction:-boss.drill.up));
                var forward=Vector3.ProjectOnPlane(boss.drill.up,Vector3.up).normalized;var destination=boss.DrillTip+forward*2+Vector3.up*.15f;destination.y=Mathf.Max(.8f,destination.y);var offset=destination-flying.ragdoll.Centre;foreach(var b in flying.ragdoll.bodies)b.position+=offset;flying.ragdoll.Launch(-forward*5);Physics.SyncTransforms();boss.SendMessage("FixedUpdate");Assert(boss.Captured==flying&&boss.State==BossState.DrillCapture,"Flying corpse did not enter assisted capture");capturePosition=boss.transform.position;freezeFrame=boss.runner.motion.SourceFrame;at=Time.time;phase=3;return;
            }
            if(phase==3)
            {
                boss.SendMessage("FixedUpdate");boss.Tick(Time.deltaTime);
                if(boss.Phase!=MachinePhase.Paralyzed){Assert(Vector3.Distance(capturePosition,boss.transform.position)<.001f&&Mathf.Abs(freezeFrame-boss.runner.motion.SourceFrame)<.001f,"Boss moved or animated while capturing");Assert(Time.time-at<3.5f,"Capture failed to finish; state="+boss.State);return;}
                Assert(boss.BodiesConsumed==cycle+1,"Capture double-counted corpse");Pass("Cycle "+(cycle+1)+": flying capture freezes machine and triggers phase three once");at=Time.time;phase=4;return;
            }
            if(phase==4)
            {
                boss.Tick(Time.deltaTime);if(boss.State!=BossState.Paralyzed){Assert(Time.time-at<5,"Third form did not freeze");return;}
                Assert(Mathf.Abs(boss.runner.motion.SourceFrame-64)<.01f,"Not frozen at source frame 64");boss.Tick(.5f);Assert(boss.runner.motion.SourceFrame==64,"Paralyzed pose advanced");at=Time.time;phase=5;Pass("Cycle "+(cycle+1)+": TripleSlam freezes precisely at 64");return;
            }
            if(phase==5)
            {
                var crystals=UnityEngine.Object.FindObjectsByType<UDK7MDebris>(FindObjectsSortMode.None).Where(d=>d.IsCrystal).ToArray();central=crystals.FirstOrDefault(d=>d.Landed);if(!central){Assert(Time.time-at<5,"Central rebound crystal did not land");return;}
                Assert(crystals.Length==1&&Vector3.ProjectOnPlane(central.transform.position,Vector3.up).magnitude<.5f,"Phase three did not drop exactly one central crystal");jumpCrystal=central.GetComponent<CrystalSurface>();Assert(jumpCrystal&&jumpCrystal.canTether,"Landed crystal is not tetherable");Pass("Cycle "+(cycle+1)+": one large central tether/rebound crystal");
                if(cycle==0)
                {
                    Player(new Vector3(0,0,-3));traversal.enabled=true;traversal.Tick(.6f,false,true);var point=jumpCrystal.contact.ClosestPoint(arena.player.eyes.position);Assert(traversal.BeginTether(jumpCrystal,point,Vector3.up),"Cannot tether central crystal");for(int i=0;i<100&&traversal.State==CrystalTraversal.Mode.Pulling;i++)traversal.Tick(.02f,true,true);Assert(traversal.State==CrystalTraversal.Mode.Attached,"Central pull cannot enter striking distance: "+traversal.Hint+"; boss="+boss.transform.position);traversal.SendMessage("AttackBegan",4);jumpCrystal.ReceiveActionHit(160,arena.player.transform.position,0);Assert(traversal.State==CrystalTraversal.Mode.Free&&motor.Velocity.y>20,"Central crystal strike did not launch player");for(int i=0;i<70;i++)motor.Tick(.02f);Assert(arena.player.transform.position.y>10,"Central crystal cannot launch above upper platform");Pass("Central crystal physically pulls and punches player above pillar platform height");
                }
                var pure=arena.pillars[0].GetComponentInChildren<UDK7MPureCrystal>();var surface=pure.GetComponent<CrystalSurface>();var p=surface.contact.bounds.center;Player(new Vector3(p.x,9.1f,p.z-1.7f));traversal.enabled=true;traversal.Tick(.6f,false,true);var contact=surface.contact.ClosestPoint(arena.player.eyes.position);Assert(traversal.BeginTether(surface,contact,Vector3.up),"Cannot tether pure upper crystal");for(int i=0;i<100&&traversal.State==CrystalTraversal.Mode.Pulling;i++)traversal.Tick(.02f,true,true);Assert(traversal.State==CrystalTraversal.Mode.Attached,"Upper crystal cannot be reached from platform");traversal.SendMessage("AttackBegan",4);surface.ReceiveActionHit(160,arena.player.transform.position,0);Assert(combat.ResonanceRemaining>14&&pure.Used,"Pure crystal did not grant resonance");Pass("Cycle "+(cycle+1)+": upper pure crystal grants shared 15-second resonance mode");
                var column=arena.pillars[0].transform.position;Player(column+new Vector3(0,10,-4.2f));motor.Launch(Vector3.up,.1f);motor.Tick(.02f);combat.Cancel();combat.Press();combat.Tick(.7f);combat.Release();Assert(pound.Diving,"Air punch4 did not start downward impact");at=Time.time;phase=6;return;
            }
            if(phase==6)
            {
                motor.Tick(Mathf.Min(.03f,Time.deltaTime));pound.Tick(Time.deltaTime);
                if(boss.State!=BossState.KnockedBack){Assert(Time.time-at<3,"Ground pound did not land; y="+arena.player.transform.position.y+", diving="+pound.Diving);return;}
                Assert(arena.pillars.All(p=>!p.Consumed&&p.crystals.activeSelf),"Ground shockwave did not regrow all pillar crystals");Assert(motor.ForcedLookPitch==null,"Impact left camera forcibly pointing down");Pass("Cycle "+(cycle+1)+": real downward Punch4 landing sends shockwave and regrows crystals");at=Time.time;phase=7;return;
            }
            if(phase==7)
            {
                boss.Tick(Time.deltaTime);if(boss.State==BossState.KnockedBack){Assert(Time.time-at<7,"Knockback failed to reach safe distance");return;}
                cycle++;Assert(boss.CompletedCycles==cycle,"Cycle count incorrect");Assert(Vector3.ProjectOnPlane(boss.transform.position-arena.player.transform.position,Vector3.up).magnitude>=10,"Boss did not end beyond 10m");
                if(cycle==3){Assert(arena.Won&&boss.Health==0&&!arena.minions.Running,"Third full cycle did not defeat boss");Pass("Three complete physical capture / slam / crystal / ground-impact cycles defeat boss");at=Time.time;phase=9;return;}
                Assert(boss.Phase==MachinePhase.Stable&&!arena.minions.Running,"Ground impact did not return to phase one");Pass("Cycle "+cycle+": boss pushed beyond 10m and returns to stable form");BeginCycle();
            }
            if(phase==9){if(Time.time-at<4.2f)return;Assert(boss.GetComponent<UDK7MPresentation>().ExplosionsPlayed==AdvancedCombatVFXLibrary.Load().smallBlastPositions.Length+1,"Final defeat did not complete three plus one explosion sequence");Pass("Final defeat finishes all seven small blasts and the final stage");Assert(Vector3.Distance(boss.GetComponent<UDK7MPresentation>().LastFinalBlastPosition,boss.ActionAimPoint)>1,"Big explosion remained at machine centre");Pass("Big explosion is placed outside player-facing hull surface");arena.ResetEncounter();Assert(boss.Phase==MachinePhase.Stable&&boss.CompletedCycles==0&&arena.pillars.All(p=>!p.Consumed),"Three-phase reset incomplete");Pass("Three-phase encounter resets");arena.feedback.Shake(arena.player.EyePosition,.25f,.5f);at=Time.time;phase=8;return;
            }
            if(phase==8)
            {
                if(Time.time-at<.08f)return;Assert(CinemachineImpulseManager.Instance.GetImpulseAt(arena.player.EyePosition,false,arena.tuning.impulseChannel,out var offset,out _)&&offset.magnitude>.001f,"Cinemachine did not receive the published impulse");Pass("Cinemachine listener position receives nonzero shake signal",new{amplitude=offset.magnitude});Finish(true);
            }
        }
        static void BeginCycle()
        {
            var boss=arena.boss;Boss(Vector3.zero);Player(new Vector3(0,0,-32));Assert(arena.pillars[0].Consume(boss),"Stable pillar did not trigger next cycle");Assert(arena.pillars.All(p=>p.Consumed)&&boss.State==BossState.OverloadCharge,"Pillars did not shatter together into idle charge");Assert(boss.runner.Current.id=="Idle","Overload charge did not use Idle");boss.Tick(arena.tuning.overloadChargeSeconds+.05f);Assert(boss.Phase==MachinePhase.Overloaded,"Overload charge did not enter form two");at=Time.time;phase=2;
        }
    }
}



