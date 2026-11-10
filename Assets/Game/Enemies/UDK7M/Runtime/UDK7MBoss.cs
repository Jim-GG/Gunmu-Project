using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using GameActions;
using Resonator.AI;
namespace UDK7M
{
    public enum MachinePhase { Stable,Overloaded,Paralyzed }
    public enum BossState { Idle,TripleSlam,SlamPrepare,SlamHold,SlamRecover,ChargePrepare,ChargeHold,ChargeRecover,Defeated,Turning,OverloadCharge,DrillCapture,Paralyzed,KnockedBack }
    public sealed class UDK7MBoss:MonoBehaviour,IActionTarget,IActionDamageReceiver
    {
        public UDK7MTuning tuning;public ActionSet actions;public ActionRunner runner;public UDK7MMotor motor;public UDK7MArena arena;public Transform drill;
        public BossState State {get;private set;}public MachinePhase Phase {get;private set;}
        public float Health {get;private set;}public bool Engaged {get;private set;}public bool DrillSpinning {get;private set;}
        public float HoldElapsed=>holdElapsed;public float AddedTravel {get;private set;}public int BodiesConsumed {get;private set;}public int CompletedCycles {get;private set;}
        public int CrushHits=>crush?.Hits??0;public ResonatorEnemy Captured=>captured;
        public UnityEvent onDefeated=new UnityEvent();public UnityEvent<string> onStateChanged=new UnityEvent<string>();
        public bool ActionTargetAlive=>Health>0;public Transform ActionRoot=>transform;public Vector3 ActionAimPoint=>transform.position+Vector3.up*2;
        public Vector3 DrillTip=>drill?drill.TransformPoint(tuning.drillLocalTip):ActionAimPoint;
        public event Action Defeated;
        readonly ActionContact contacts=new ActionContact();readonly ActionEventDispatcher events=new ActionEventDispatcher();
        ActionHitbox boxes;UDK7MTracks tracks;UDK7MCrush crush;
        Vector3 spawn,pushVelocity,staggerVelocity;Quaternion spawnRotation,previousDrill,chargeHeading;
        float stateTime,holdElapsed,nextDecision,previousDistance;bool ready;BossState afterTurn=BossState.SlamPrepare;
        ResonatorEnemy captured;
        void Start()=>Initialize();
        public void Initialize()
        {
            if(ready)return;ready=true;spawn=transform.position;spawnRotation=transform.rotation;
            if(!motor)motor=GetComponent<UDK7MMotor>();motor.tuning=tuning;motor.Initialize();
            if(!runner)runner=GetComponentInChildren<ActionRunner>();runner.profile=actions;boxes=new ActionHitbox(transform);boxes.BindProfile(actions);
            contacts.TargetFilter=t=>t is ResonatorTarget||t is ResonatorEnemy;runner.motion.FrameEventRaised+=FrameEvent;
            UDK7MPresentation.Ensure(this);ResetBoss();tracks=new UDK7MTracks(transform);crush=new UDK7MCrush(transform,tuning);
        }
        public void ResetBoss()
        {
            if(!ready){Initialize();return;}ReleaseCapture();motor.Stop();transform.SetPositionAndRotation(spawn,spawnRotation);motor.Initialize();
            GetComponent<UDK7MPresentation>()?.ResetVisuals();GetComponent<GroundImpactDecals>()?.Clear();Health=tuning.maxHealth;Engaged=false;Phase=MachinePhase.Stable;CompletedCycles=BodiesConsumed=0;DrillSpinning=false;
            previousDrill=drill?drill.localRotation:Quaternion.identity;Enter(BossState.Idle,true);nextDecision=UnityEngine.Random.Range(tuning.ambientInterval.x,tuning.ambientInterval.y);tracks?.Reset();crush?.Reset();
        }
        public void Engage(){Initialize();Engaged=true;nextDecision=0;}
        public void ReceiveActionHit(float damage,Vector3 source,float impulse)=>ReceiveActionDamage(new ActionDamage(damage,source));
        public void ReceiveActionDamage(ActionDamage hit){if(Engaged)Damage(hit.damage*tuning.directDamageMultiplier);}
        public void Damage(float amount)
        {
            if(!ActionTargetAlive||amount<=0)return;
            float floor=CompletedCycles<tuning.cyclesToDefeat?tuning.maxHealth*(1-(CompletedCycles+.95f)/Mathf.Max(1,tuning.cyclesToDefeat)):0;
            Health=Mathf.Max(Mathf.Max(0,floor),Health-amount);if(Health<=0)Defeat();
        }
        void Defeat()
        {
            Health=0;ReleaseCapture();Enter(BossState.Defeated);DrillSpinning=false;runner.motion.StopForRagdoll();
            UDK7MPresentation.Ensure(this).PlayDefeat();
            arena?.BossDefeated();Defeated?.Invoke();onDefeated.Invoke();
        }
        void FrameEvent(MotionFrameEvent e)
        {
            events.Dispatch(e,boxes);if(State!=BossState.ChargeRecover&&(e.name=="SlamLight1"||e.name=="SlamLight2"||e.name=="SlamImpact"||e.name=="ThirdSlam"))GetComponent<GroundImpactDecals>()?.Stamp(DrillTip,e.name=="ThirdSlam"?2.2f:e.name=="SlamImpact"?1.7f:1.1f);if(e.name=="SlamLight1"||e.name=="SlamLight2"){arena?.feedback.Shake(DrillTip,tuning.lightSlamShake);arena?.feedback.Sound(UDK7MSound.SlamLight,DrillTip,drill);}
            if(e.name=="SlamImpact"){arena?.feedback.Shake(DrillTip,tuning.heavySlamShake);arena?.feedback.Sound(State==BossState.ChargeRecover?UDK7MSound.ChargeImpact:UDK7MSound.SlamHeavy,DrillTip,drill);}
            if(e.name=="ThirdSlam"&&State==BossState.TripleSlam){GetComponent<SurfaceContactAudio>()?.MuteFor(tuning.thirdSlamContactMuteSeconds);arena?.feedback.Shake(DrillTip,tuning.heavySlamShake,.5f);arena?.feedback.Sound(UDK7MSound.TripleFinalImpact,DrillTip,drill);if(Phase==MachinePhase.Paralyzed)arena.DropCentralCrystal();else arena.DropRocks();}
        }
        void Update(){if(ready&&ActionTargetAlive)Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(!ready||!ActionTargetAlive)return;float remaining=Mathf.Max(0,dt);
            while(remaining>.000001f){float frameStep=runner.Current?.Motion.track?1/(runner.Current.Motion.track.framesPerSecond*Mathf.Max(1,runner.Current.Motion.speed)):1/60f;float step=Mathf.Min(remaining,Mathf.Min(1/60f,frameStep));Step(step);remaining-=step;if(!ActionTargetAlive)break;}
        }
        void Step(float dt)
        {
            var before=transform.position;var rotation=transform.rotation;stateTime+=dt;var player=arena?arena.player:null;var target=player?player.transform.position:transform.position;
            float distance=Vector3.ProjectOnPlane(target-transform.position,Vector3.up).magnitude;
            if(Phase==MachinePhase.Paralyzed&&State==BossState.TripleSlam&&stateTime-dt<tuning.paralyzedStaggerSeconds)
                motor.Displace(staggerVelocity*Mathf.Min(dt,Mathf.Max(0,tuning.paralyzedStaggerSeconds-(stateTime-dt))),out _);
            if(State==BossState.DrillCapture)
            {
                motor.Stop();if(!captured||captured.Absorbed||!captured.gameObject.activeInHierarchy){ReleaseCapture();Enter(BossState.ChargeRecover);return;}
                if(stateTime>=tuning.captureHoldSeconds&&Vector3.Distance(captured.ragdoll.Centre,DrillTip)<tuning.consumeRadius){Consume(captured);return;}
                if(stateTime>tuning.captureTimeout){ReleaseCapture();Enter(BossState.ChargeRecover);}return;
            }
            if(State==BossState.Paralyzed){motor.Stop();return;}
            if(State==BossState.KnockedBack)
            {
                motor.Displace(pushVelocity*dt,out var normal);if(normal.sqrMagnitude>.01f){pushVelocity=Vector3.Reflect(pushVelocity,normal)*.9f;arena.feedback.Shake(ActionAimPoint,tuning.chargeImpactShake);arena.feedback.Sound(UDK7MSound.ChargeImpact,ActionAimPoint,transform);}
                float gap=Vector3.ProjectOnPlane(transform.position-target,Vector3.up).magnitude;
                if(gap>=tuning.groundPoundMinBossDistance&&stateTime>.45f){CompleteCycle();return;}
                if(stateTime>1.3f){var route=motor.Direction(target+(transform.position-target).normalized*(tuning.groundPoundMinBossDistance+5),dt);pushVelocity=Vector3.MoveTowards(pushVelocity,route*14,dt*25);}
                if(tuning.tracksFollowMovement)tracks?.Apply(tuning.trackDistanceMultiplier);return;
            }
            if(State==BossState.OverloadCharge)
            {
                motor.Stop();if(stateTime>=tuning.overloadChargeSeconds){Phase=MachinePhase.Overloaded;arena.minions.Activate();Enter(BossState.Idle);nextDecision=.2f;}
                else if(Mathf.FloorToInt((stateTime-dt)*4)!=Mathf.FloorToInt(stateTime*4))CombatEffects.Emit(CombatEffect.Resonance,ActionAimPoint,Vector3.up,.7f);
            }
            else if(State==BossState.Turning)
            {
                motor.Stop();motor.Face(target-transform.position,tuning.rearTurnSpeed,dt);if(motor.Angle(target)<=tuning.aimTolerance)Enter(afterTurn);
            }
            else if(State==BossState.Idle)
            {
                nextDecision-=dt;if(nextDecision<=0){if(Engaged)Decide();else Enter(BossState.TripleSlam);}
            }
            else if(State==BossState.SlamPrepare||State==BossState.ChargePrepare)
            {
                if(motor.Angle(target)>tuning.turnBeforeChargeAngle){afterTurn=State;Enter(BossState.Turning);}else motor.Face(target-transform.position,tuning.turnSpeed,dt);
            }
            else if(State==BossState.SlamHold||State==BossState.ChargeHold)
            {
                bool slam=State==BossState.SlamHold;
                if(slam&&InSlamRange(distance,previousDistance,motor.Angle(target))){Enter(BossState.SlamRecover);return;}
                holdElapsed+=dt;var route=motor.Direction(target,dt);var desired=route.sqrMagnitude>.001f?Quaternion.LookRotation(route):chargeHeading;
                desired=Quaternion.RotateTowards(chargeHeading,desired,tuning.chargeTrackingAngle);motor.Face(desired*Vector3.forward,tuning.holdTurnSpeed,dt);
                float moved=motor.Move(tuning.chargeSpeed,dt);AddedTravel+=moved;
                if(Phase==MachinePhase.Stable)
                {
                    var pillar=arena.pillars.FirstOrDefault(p=>p&&!p.Consumed&&Vector3.Distance(p.ContactPoint(DrillTip),DrillTip)<1.4f);
                    if(pillar&&pillar.Consume(this))return;
                }
                if(holdElapsed>=(slam?tuning.slamSeconds:tuning.chargeSeconds)||holdElapsed>.15f&&moved<tuning.chargeSpeed*dt*.08f)
                {if(moved<tuning.chargeSpeed*dt*.08f){arena.feedback.Shake(ActionAimPoint,tuning.chargeImpactShake);arena.feedback.Sound(UDK7MSound.ChargeImpact,ActionAimPoint,transform);}Enter(slam?BossState.SlamRecover:BossState.ChargeRecover);return;}
            }
            else if(State==BossState.SlamRecover||State==BossState.ChargeRecover)
            {
                float duration=State==BossState.SlamRecover?tuning.slamBrakeSeconds:tuning.chargeBrakeSeconds;
                float speed=tuning.chargeSpeed*(tuning.braking.Evaluate(Mathf.Clamp01((stateTime-dt)/duration))+tuning.braking.Evaluate(Mathf.Clamp01(stateTime/duration)))*.5f;AddedTravel+=motor.Move(speed,dt);
            }
            previousDistance=distance;runner.motion.Advance(dt);
            if(Phase==MachinePhase.Paralyzed&&State==BossState.TripleSlam&&runner.motion.SourceFrame>=tuning.paralyzedFreezeFrame){runner.motion.Seek(tuning.paralyzedFreezeFrame);State=BossState.Paralyzed;stateTime=0;DrillSpinning=false;motor.Stop();arena.feedback.Sound(UDK7MSound.Paralyzed,DrillTip,drill);onStateChanged.Invoke(State.ToString());return;}
            if(tuning.tracksFollowMovement)tracks?.Apply(tuning.trackDistanceMultiplier);
            if(drill){var current=drill.localRotation;var delta=Quaternion.Inverse(previousDrill)*current;delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;DrillSpinning=Mathf.Abs(angle*Vector3.Dot(axis,Vector3.up))/Mathf.Max(.001f,dt)>tuning.rotationThreshold;previousDrill=current;}
            Physics.SyncTransforms();if(State!=BossState.OverloadCharge){contacts.Resolve(runner.Current,runner.motion,boxes,transform,DrillTip,~0,0,dt);crush?.Tick(dt,before,rotation);}
            if(runner.motion.Finished)
            {
                if(State==BossState.SlamPrepare)Enter(BossState.SlamHold);
                else if(State==BossState.ChargePrepare)Enter(BossState.ChargeHold);
                else if(State==BossState.TripleSlam||State==BossState.SlamRecover||State==BossState.ChargeRecover){Enter(BossState.Idle);nextDecision=UnityEngine.Random.Range(Engaged?tuning.decisionInterval.x:tuning.ambientInterval.x,Engaged?tuning.decisionInterval.y:tuning.ambientInterval.y);}
            }
        }
        public bool InSlamRange(float distance,float previous,float angle)=>angle<=tuning.slamFrontAngle&&(distance<=tuning.slamRange||previous>tuning.slamRange&&distance<tuning.slamMinimumRange);
        public BossState SelectAttack(float unitRoll)
        {
            var w=CurrentAttackWeights;float choice=Mathf.Clamp01(unitRoll)*(w.x+w.y+w.z);
            return choice<w.x?BossState.SlamPrepare:choice<w.x+w.y?BossState.TripleSlam:BossState.ChargePrepare;
        }
        public bool NearUnbrokenPillar=>arena&&arena.pillars!=null&&arena.pillars.Any(p=>p&&!p.Consumed&&Vector3.ProjectOnPlane(p.transform.position-transform.position,Vector3.up).sqrMagnitude<=tuning.nearPillarDistance*tuning.nearPillarDistance);
        public Vector3 CurrentAttackWeights
        {
            get
            {
                var w=Phase==MachinePhase.Stable?tuning.stableWeights:tuning.overloadedWeights;
                if(Phase==MachinePhase.Stable&&NearUnbrokenPillar)
                {float removed=Mathf.Max(0,w.y-(w.x+w.y+w.z)*tuning.nearPillarTripleChance);w.x+=removed;w.y-=removed;}
                return w;
            }
        }
        void Decide(){if(!arena.player||!arena.player.Alive)return;afterTurn=SelectAttack(UnityEngine.Random.value);Enter(motor.Angle(arena.player.transform.position)>tuning.turnBeforeChargeAngle?BossState.Turning:afterTurn);}
        public void ForceState(BossState next,bool align=false){afterTurn=next;if(align)Enter(BossState.Turning);else Enter(next);}
        void Enter(BossState next,bool immediate=false)
        {
            State=next;stateTime=holdElapsed=0;motor.Stop();contacts.Reset(true);previousDistance=float.PositiveInfinity;
            if(next==BossState.DrillCapture)arena.feedback.Sound(UDK7MSound.DrillCapture,DrillTip,drill);
            if(next==BossState.SlamPrepare)arena?.feedback.Sound(UDK7MSound.SlamPrepare,ActionAimPoint,transform);
            if(next==BossState.ChargePrepare)arena?.feedback.Sound(UDK7MSound.ChargePrepare,ActionAimPoint,transform);
            if(next==BossState.SlamHold||next==BossState.ChargeHold)arena?.feedback.Sound(UDK7MSound.ChargeStart,ActionAimPoint,transform);
            if(next==BossState.Defeated||next==BossState.DrillCapture||next==BossState.Paralyzed||next==BossState.KnockedBack)return;
            string clip=next==BossState.Turning?"SlamHold":next==BossState.OverloadCharge?"Idle":next.ToString();runner.Play(clip,immediate);
            if(next==BossState.SlamHold||next==BossState.ChargeHold)chargeHeading=transform.rotation;onStateChanged.Invoke(next.ToString());
        }
        public void PillarOverload(){if(Phase!=MachinePhase.Stable)return;Enter(BossState.OverloadCharge);arena.feedback.Accumulate(ActionAimPoint,tuning.overloadChargeSeconds);arena.feedback.Shake(ActionAimPoint,tuning.resonanceShake,.7f);arena.feedback.Sound(UDK7MSound.OverloadCharge,ActionAimPoint,transform);}
        void FixedUpdate()
        {
            if(!ActionTargetAlive||!Engaged||!arena||Phase!=MachinePhase.Overloaded)return;
            if(captured)
            {
                if(!ActionVisibility.Clear(captured.ragdoll.Centre,DrillTip,captured.transform,transform,~0)){ReleaseCapture();Enter(BossState.ChargeRecover);return;}
                arena.minions.Reserve(captured);captured.ragdoll.CaptureTowards(DrillTip,tuning.vacuumSpeed,Time.fixedDeltaTime);return;
            }
            if(!DrillSpinning||State==BossState.OverloadCharge)return;
            foreach(var enemy in arena.minions.Actors)
            {
                if(!enemy||!enemy.gameObject.activeInHierarchy||enemy.ActionTargetAlive||enemy.Absorbed||!enemy.ragdoll||!enemy.ragdoll.Active)continue;
                if(tuning.captureFlyingOnly&&(!enemy.PlayerLaunched||Time.time-enemy.PlayerLaunchTime>tuning.captureFlightAge||enemy.ragdoll.Speed<tuning.captureMinimumSpeed))continue;
                var delta=enemy.ragdoll.Centre-DrillTip;if(delta.magnitude>tuning.captureRadius||Vector3.Angle(drill?drill.up:transform.forward,delta)>tuning.captureFrontAngle)continue;
                if(!ActionVisibility.Clear(enemy.ragdoll.Centre,DrillTip,enemy.transform,transform,~0))continue;
                captured=enemy;arena.minions.Reserve(enemy);Enter(BossState.DrillCapture);break;
            }
        }
        void ReleaseCapture(){if(captured&&captured.ragdoll)captured.ragdoll.ReleaseCapture();captured=null;}
        public void Consume(ResonatorEnemy corpse)
        {
            if(!corpse||!corpse.gameObject.activeInHierarchy||corpse.ActionTargetAlive||corpse.Absorbed||!ActionTargetAlive||Phase!=MachinePhase.Overloaded)return;
            corpse.Absorbed=true;BodiesConsumed++;var point=DrillTip;ReleaseCapture();
            CombatEffects.Emit(CombatEffect.PurpleBlood,point,Vector3.up,2);CombatEffects.Emit(CombatEffect.CrystalShards,point,Vector3.up);arena.feedback.Wave(point,tuning.resonanceRadius);arena.feedback.Shake(point,tuning.resonanceShake,.65f);arena.feedback.Sound(UDK7MSound.ResonanceBurst,point,drill);
            arena.minions.Recycle(corpse);foreach(var enemy in arena.minions.Actors.ToArray())if(enemy&&enemy.ActionTargetAlive){if(Vector3.Distance(enemy.ActionAimPoint,point)<=tuning.resonanceRadius)enemy.ReceiveActionDamage(new ActionDamage(tuning.resonanceDamage,point,cause:DamageCause.Resonance));if(enemy.ActionTargetAlive)enemy.Agonize();}
            Damage(tuning.corpseDamage);Phase=MachinePhase.Paralyzed;
            var centreOffset=Vector3.ProjectOnPlane(transform.position,Vector3.up);var away=centreOffset.sqrMagnitude>.01f?centreOffset.normalized:-transform.forward;
            staggerVelocity=away*Mathf.Max(0,tuning.paralyzedCenterClearance-centreOffset.magnitude)/Mathf.Max(.1f,tuning.paralyzedStaggerSeconds);Enter(BossState.TripleSlam);
        }
        public bool GroundPound(Vector3 playerPoint)
        {
            if(Phase!=MachinePhase.Paralyzed||State!=BossState.Paralyzed)return false;
            var away=Vector3.ProjectOnPlane(transform.position-playerPoint,Vector3.up).normalized;if(away.sqrMagnitude<.01f)away=transform.forward;
            pushVelocity=away*tuning.groundPoundPushSpeed;Enter(BossState.KnockedBack);Damage(tuning.groundPoundBossDamage);return true;
        }
        void CompleteCycle()
        {
            CompletedCycles++;Health=Mathf.Min(Health,tuning.maxHealth*(1-CompletedCycles/(float)Mathf.Max(1,tuning.cyclesToDefeat)));
            if(CompletedCycles>=tuning.cyclesToDefeat){Defeat();return;}Phase=MachinePhase.Stable;arena.ResetPopulationForCycle();Enter(BossState.Idle);nextDecision=.5f;arena.feedback.Sound(UDK7MSound.CycleRestart,ActionAimPoint,transform);
        }
        void OnDestroy(){if(runner&&runner.motion)runner.motion.FrameEventRaised-=FrameEvent;}
        void OnDrawGizmosSelected(){if(tuning&&tuning.crushEnabled)crush?.DrawGizmos();}
    }
}

