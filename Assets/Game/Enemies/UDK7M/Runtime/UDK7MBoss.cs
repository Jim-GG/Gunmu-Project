using System;
using System.Linq;
using UnityEngine;
using UnityEngine.Events;
using GameActions;
using Resonator.AI;
namespace UDK7M
{
    public enum BossState { Idle,TripleSlam,SlamPrepare,SlamHold,SlamRecover,ChargePrepare,ChargeHold,ChargeRecover,Defeated,Turning }
    public sealed class UDK7MBoss:MonoBehaviour,IActionTarget,IActionDamageReceiver
    {
        public UDK7MTuning tuning;public ActionSet actions;public ActionRunner runner;public UDK7MMotor motor;public UDK7MArena arena;public Transform drill;
        public BossState State {get;private set;}public float Health {get;private set;}public bool Engaged {get;private set;}public bool DrillSpinning {get;private set;}
        public float HoldElapsed=>holdElapsed;public float AddedTravel {get;private set;}public int BodiesConsumed {get;private set;}
        public int CrushHits=>crush?.Hits??0;
        public UnityEvent onDefeated=new UnityEvent();public UnityEvent<string> onStateChanged=new UnityEvent<string>();
        public bool ActionTargetAlive=>Health>0;public Transform ActionRoot=>transform;public Vector3 ActionAimPoint=>transform.position+Vector3.up*2;
        public Vector3 DrillTip=>drill?drill.TransformPoint(tuning.drillLocalTip):ActionAimPoint;
        readonly ActionContact contacts=new ActionContact();readonly ActionEventDispatcher events=new ActionEventDispatcher();ActionHitbox boxes;UDK7MTracks tracks;UDK7MCrush crush;
        Vector3 spawn;Quaternion spawnRotation,previousDrill;float stateTime,holdElapsed,nextDecision,previousDistance;bool aligning;UDK7MCrystalPillar pillar;bool ready;
        public event Action Defeated;
        void Start(){Initialize();}
        public void Initialize()
        {
            if(ready)return;ready=true;spawn=transform.position;spawnRotation=transform.rotation;
            if(!motor)motor=GetComponent<UDK7MMotor>();motor.tuning=tuning;motor.Initialize();
            if(!runner)runner=GetComponentInChildren<ActionRunner>();runner.profile=actions;boxes=new ActionHitbox(transform);boxes.BindProfile(actions);contacts.TargetFilter=t=>t is ResonatorTarget||t is ResonatorEnemy;
            runner.motion.FrameEventRaised+=FrameEvent;ResetBoss();tracks=new UDK7MTracks(transform);crush=new UDK7MCrush(transform,tuning);
        }
        public void ResetBoss()
        {
            if(!ready){Initialize();return;}motor.Stop();transform.SetPositionAndRotation(spawn,spawnRotation);motor.Initialize();Health=tuning.maxHealth;Engaged=false;pillar=null;BodiesConsumed=0;DrillSpinning=false;previousDrill=drill?drill.localRotation:Quaternion.identity;Enter(BossState.Idle,true);nextDecision=UnityEngine.Random.Range(tuning.ambientInterval.x,tuning.ambientInterval.y);
            tracks?.Reset();crush?.Reset();
        }
        public void Engage(){Initialize();Engaged=true;nextDecision=0;}
        public void ReceiveActionHit(float damage,Vector3 source,float impulse)=>ReceiveActionDamage(new ActionDamage(damage,source));
        public void ReceiveActionDamage(ActionDamage hit){if(!Engaged)return;Damage(hit.damage*tuning.directDamageMultiplier);}
        public void Damage(float amount)
        {
            if(!ActionTargetAlive||amount<=0)return;Health=Mathf.Max(0,Health-amount);
            if(Health<=0){Enter(BossState.Defeated);DrillSpinning=false;motor.Stop();runner.motion.ReleaseGraph();runner.motion.animator.runtimeAnimatorController=null;runner.motion.animator.enabled=false;CombatEffects.Emit(CombatEffect.Explosion,ActionAimPoint,Vector3.up,2);CombatEffects.Emit(CombatEffect.Dust,ActionAimPoint,Vector3.up,2);CombatEffects.Emit(CombatEffect.MetalDebris,ActionAimPoint,Vector3.up,2);arena?.BossDefeated();Defeated?.Invoke();onDefeated.Invoke();}
        }
        void FrameEvent(MotionFrameEvent e){events.Dispatch(e,boxes);if(e.name=="ThirdSlam"&&State==BossState.TripleSlam)arena?.DropRocks();}
        void Update(){if(!ready||!ActionTargetAlive)return;Tick(Time.deltaTime);}
        public void Tick(float dt)
        {
            if(!ready||!ActionTargetAlive)return;float remaining=Mathf.Max(0,dt);
            while(remaining>.000001f){float frameStep=runner.Current?.Motion.track?1/(runner.Current.Motion.track.framesPerSecond*Mathf.Max(1,runner.Current.Motion.speed)):1/60f;float step=Mathf.Min(remaining,Mathf.Min(1/60f,frameStep));Step(step);remaining-=step;if(!ActionTargetAlive)break;}
        }
        void Step(float dt)
        {
            var before=transform.position;var beforeRotation=transform.rotation;
            stateTime+=dt;var player=arena?arena.player:null;var target=pillar&&!pillar.Consumed?pillar.transform.position:player?player.transform.position:transform.position;
            float distance=Vector3.ProjectOnPlane(target-transform.position,Vector3.up).magnitude;
            if(Engaged&&player&&player.Alive&&CanStartTurn(State)&&motor.Angle(player.transform.position)>tuning.turnBeforeChargeAngle){pillar=null;Enter(BossState.Turning);}
            if(State==BossState.Turning)
            {
                motor.Stop();if(player){motor.Face(player.transform.position-transform.position,tuning.rearTurnSpeed,dt);if(motor.Angle(player.transform.position)<=tuning.aimTolerance){aligning=false;Enter(BossState.SlamHold);}}
            }
            else if(State==BossState.Idle)
            {
                if(Engaged&&player)motor.Face(target-transform.position,tuning.turnSpeed,dt);
                nextDecision-=dt;if(nextDecision<=0){if(Engaged)Decide();else Enter(BossState.TripleSlam);}
            }
            else if(State==BossState.SlamPrepare||State==BossState.ChargePrepare)motor.Face(target-transform.position,tuning.turnSpeed,dt);
            else if(State==BossState.SlamHold||State==BossState.ChargeHold)
            {
                bool slam=State==BossState.SlamHold;
                if(slam&&InSlamRange(distance,previousDistance,motor.Angle(target))){Enter(BossState.SlamRecover);return;}
                if(aligning){motor.Face(target-transform.position,tuning.rearTurnSpeed,dt);if(motor.Angle(target)<=tuning.aimTolerance)aligning=false;}
                if(!aligning)
                {
                    holdElapsed+=dt;motor.Face(motor.Direction(target,dt),tuning.holdTurnSpeed,dt);float moved=motor.Move(tuning.chargeSpeed,dt);AddedTravel+=moved;
                    var struckPillar=arena?arena.pillars.FirstOrDefault(p=>p&&!p.Consumed&&Vector3.Distance(p.ContactPoint(DrillTip),DrillTip)<1.3f):null;
                    if(struckPillar){struckPillar.Consume(this);pillar=null;Enter(BossState.ChargeRecover);return;}
                    if(holdElapsed>=(slam?tuning.slamSeconds:tuning.chargeSeconds)||holdElapsed>.15f&&moved<tuning.chargeSpeed*dt*.08f){Enter(slam?BossState.SlamRecover:BossState.ChargeRecover);return;}
                }
            }
            else if(State==BossState.SlamRecover||State==BossState.ChargeRecover)
            {
                float duration=State==BossState.SlamRecover?tuning.slamBrakeSeconds:tuning.chargeBrakeSeconds;
                float speed=tuning.chargeSpeed*(tuning.braking.Evaluate(Mathf.Clamp01((stateTime-dt)/duration))+tuning.braking.Evaluate(Mathf.Clamp01(stateTime/duration)))*.5f;AddedTravel+=motor.Move(speed,dt);
            }
            previousDistance=distance;runner.motion.Advance(dt);
            if(tuning.tracksFollowMovement)tracks?.Apply(tuning.trackDistanceMultiplier);
            if(drill){var rotation=drill.localRotation;var delta=Quaternion.Inverse(previousDrill)*rotation;delta.ToAngleAxis(out float angle,out var axis);if(angle>180)angle-=360;DrillSpinning=Mathf.Abs(angle*Vector3.Dot(axis,Vector3.up))/Mathf.Max(.001f,dt)>tuning.rotationThreshold;previousDrill=rotation;}
            Physics.SyncTransforms();contacts.Resolve(runner.Current,runner.motion,boxes,transform,DrillTip,~0,0,dt);
            crush?.Tick(dt,before,beforeRotation);
            if(runner.motion.Finished)
            {
                if(State==BossState.SlamPrepare)Enter(BossState.SlamHold);
                else if(State==BossState.ChargePrepare)Enter(BossState.ChargeHold);
                else if(State!=BossState.Idle&&State!=BossState.Turning&&State!=BossState.SlamHold&&State!=BossState.ChargeHold){pillar=null;Enter(BossState.Idle);nextDecision=UnityEngine.Random.Range(Engaged?tuning.decisionInterval.x:tuning.ambientInterval.x,Engaged?tuning.decisionInterval.y:tuning.ambientInterval.y);}
            }
        }
        static bool CanStartTurn(BossState state)=>state==BossState.Idle||state==BossState.SlamPrepare||state==BossState.ChargePrepare||state==BossState.SlamHold||state==BossState.ChargeHold;
        public bool InSlamRange(float distance,float previous,float angle)=>angle<=tuning.slamFrontAngle&&(distance<=tuning.slamRange||previous>tuning.slamRange&&distance<tuning.slamMinimumRange);
        void Decide()
        {
            var player=arena?arena.player:null;if(!player||!player.Alive)return;var target=player.transform.position;float distance=Vector3.ProjectOnPlane(target-transform.position,Vector3.up).magnitude;
            pillar=null;aligning=false;
            if(motor.Angle(target)>tuning.turnBeforeChargeAngle){Enter(BossState.Turning);return;}
            if(distance<=tuning.nearDistance){Enter(BossState.TripleSlam);return;}
            Enter(UnityEngine.Random.value*(tuning.farWeights.x+tuning.farWeights.y)<tuning.farWeights.x?BossState.SlamPrepare:BossState.ChargePrepare);
        }
        public void ForceState(BossState next,bool align=false){aligning=align;Enter(next);}
        void Enter(BossState next,bool immediate=false)
        {
            State=next;stateTime=holdElapsed=0;motor.Stop();contacts.Reset(true);previousDistance=float.PositiveInfinity;
            if(next==BossState.Defeated)return;
            runner.Play(next==BossState.Turning?"SlamHold":next.ToString(),immediate);onStateChanged.Invoke(next.ToString());
        }
        void FixedUpdate()
        {
            if(!ActionTargetAlive||!Engaged||!DrillSpinning||!arena||!arena.minions)return;
            foreach(var enemy in arena.minions.Actors.ToArray())
            {
                if(!enemy||!enemy.gameObject.activeInHierarchy||enemy.ActionTargetAlive||enemy.Absorbed||!enemy.ragdoll||!enemy.ragdoll.Active)continue;
                Vector3 centre=enemy.ragdoll.Centre;float distance=Vector3.Distance(centre,DrillTip);if(distance>tuning.vacuumRadius)continue;
                if(!ActionVisibility.Clear(centre,DrillTip,enemy.transform,transform,~0))continue;
                arena.minions.Reserve(enemy);enemy.ragdoll.Attract(DrillTip,tuning.vacuumSpeed,Time.fixedDeltaTime);
                if(distance<tuning.consumeRadius)Consume(enemy);
            }
        }
        public void Consume(ResonatorEnemy corpse)
        {
            if(!corpse||!corpse.gameObject.activeInHierarchy||corpse.ActionTargetAlive||corpse.Absorbed||!ActionTargetAlive)return;corpse.Absorbed=true;BodiesConsumed++;var point=DrillTip;
            CombatEffects.Emit(CombatEffect.PurpleBlood,point,Vector3.up);CombatEffects.Emit(CombatEffect.CrystalShards,point,Vector3.up);CombatEffects.Emit(CombatEffect.Resonance,point,Vector3.up,2);
            arena.minions.Recycle(corpse);
            foreach(var enemy in arena.minions.Actors.ToArray())if(enemy&&enemy.ActionTargetAlive)
            {if(Vector3.Distance(enemy.transform.position+Vector3.up,point)<=tuning.resonanceRadius)enemy.ReceiveActionDamage(new ActionDamage(tuning.resonanceDamage,point,cause:DamageCause.Resonance));if(enemy.ActionTargetAlive)enemy.Agonize();}
            Damage(tuning.corpseDamage);
        }
        void OnDestroy(){if(runner&&runner.motion)runner.motion.FrameEventRaised-=FrameEvent;}
        void OnDrawGizmosSelected(){if(tuning&&tuning.crushEnabled)crush?.DrawGizmos();}
    }
}
