using System;
using UnityEngine;
namespace Resonator.AI
{
    public enum RestActivity { FeignDeath, Feeding, SlowIdle, RestlessIdle, Resonating }
    public enum EnemyState { Dormant, Waking, FeedingRise, Agonizing, Screaming, Walking, Running, Headbutt, Kicking, Attack, Punching, CombatIdle, Hit, Stumbling, Dying, Dead }
    [Serializable] public sealed class HitWindow
    {
        public string label="Hitbox";
        [Tooltip("Optional: boxes with the same non-empty group can damage the target only once per attack.")]
        public string hitGroup;
        public bool enabled=true;
        public float startFrame=35, endFrame=42, damage=15;
        [HideInInspector] public float reach=1.45f;
        [HideInInspector,Range(0,180)] public float halfAngle=65;
        public HitboxShape shape=HitboxShape.Box;
        public HitboxAnchor anchor=HitboxAnchor.Root;
        public string customBone;
        [Tooltip("Metres in the anchor's rotation frame. Independent of imported skeleton scale.")]
        public Vector3 position=new Vector3(0,1.05f,.9f), rotation;
        public Vector3 size=new Vector3(.65f,.7f,.65f);
        [Min(.005f)] public float radius=.25f;
        [Min(.01f)] public float height=.7f;
        public LayerMask targetLayers=~0;
        [Range(0,1)] public float minimumBlendWeight;
        [Header("Contact sampling / old windows remain single-hit")]
        public GameActions.HitRepeat repeat;
        [Min(1)] public int sampleEveryFrames=1;
        [Min(.01f)] public float damageCooldown=.25f,effectCooldown=.1f;
        public bool onlyWhileRotating,contactEffects;
        public GameObject contactEffect;
        public AudioClip contactSound;
        [Min(.05f)] public float contactEffectLifetime=2;
        public Vector3 rotationAxis=Vector3.up;
        [Min(0)] public float rotationThreshold=5;
        [Header("Guard and knockback")]
        public bool blockable=true;
        [Tooltip("-1 calculates distance from original damage.")] public float knockbackDistance=-1;
        [Min(0)] public float knockbackPerDamage;
        [Min(.01f)] public float knockbackDuration=.2f;
        [Range(0,1)] public float blockedKnockbackMultiplier;
    }
    [Serializable] public sealed class MotionBeat
    {
        public ResonatorMotionTrack track;
        [Min(0)] public float startFrame;
        [Tooltip("-1 uses the clip end. Frame numbers are zero based, at the track FPS.")]
        public float endFrame=-1;
        public bool loop;
        [Min(.05f)] public float speed=1;
        [Range(0,.6f)] public float blend=.15f;
        public MotionFrameEvent[] events=Array.Empty<MotionFrameEvent>();
        public float End => !track?0:endFrame<0 ? track.Duration*track.framesPerSecond : Mathf.Min(endFrame,track.Duration*track.framesPerSecond);
        public float StartSeconds => startFrame/Mathf.Max(1,track?track.framesPerSecond:30);
        public float EndSeconds => End/Mathf.Max(1,track?track.framesPerSecond:30);
    }
    [Serializable] public sealed class AttackBeat
    {
        public MotionBeat animation=new MotionBeat();
        [Tooltip("A separate hit per window, evaluated once even when a frame is skipped.")]
        public HitWindow[] hits={new HitWindow()};
        public float rootMotionScale=1;
        [Tooltip("Attacker may turn before this source frame, then aim is locked.")]
        public float lockFacingFrame=20;
        public AttackDisplacement displacement=new AttackDisplacement();
        [Tooltip("Maximum AI starting distance, root to root. 0 uses the global legacy distance. Attack remains combo-only.")]
        [Min(0)] public float startDistance;
    }
    [CreateAssetMenu(menuName="Resonator/Enemy Tuning")]
    public sealed class ResonatorTuning : ScriptableObject
    {
        [Header("Animation beats / editable ranges and blends")]
        public MotionBeat wake=new MotionBeat(), feeding=new MotionBeat(), idleSlow=new MotionBeat(), idleRestless=new MotionBeat(), resonance=new MotionBeat(), agonizing=new MotionBeat(), scream=new MotionBeat(), walk=new MotionBeat(), run=new MotionBeat(), hit=new MotionBeat(), stumble=new MotionBeat(), death=new MotionBeat();
        public AttackBeat headbutt=new AttackBeat(), kick=new AttackBeat(), attack=new AttackBeat(), punch=new AttackBeat();
        [Header("Rest activities: source frames")]
        public float wakeRevealFrame=520, feedingResumeFrame=421, feedingRevealFrame=490, ragdollFrame=50;
        [Header("Detection")]
        public float sightRange=10, sightAngle=105, hearingMultiplier=1, sensorInterval=.12f;
        public LayerMask obstructionMask=~0;
        [Header("Combat distances (centre to centre)")]
        [Min(.1f)] public float attackEnterDistance=1.1f;
        [Min(.1f)] public float minimumSeparation=.8f;
        public float walkToRunDistance=5;
        public float attackDecisionSlack=.08f;
        [Header("Attack planning")]
        [Range(0,180)] public float attackStartFacingAngle=65;
        [Range(0,1)] public float retreatPrediction=.6f;
        public float maxAttackPredictionSeconds=.4f;
        [Tooltip("Run sooner when an aware target is retreating faster than this (m/s). 0 disables.")]
        public float retreatRunThreshold=1.2f;
        [Header("Navigation / speed matched animation")]
        public float walkSpeedMultiplier=1.5f, runSpeedMultiplier=1.2f;
        public Vector2 walkSpeedLimits=new Vector2(.8f,2.1f), runSpeedLimits=new Vector2(2.8f,5.2f);
        public float acceleration=13, navigationTurnSpeed=420, attackTurnSpeed=200, screamTurnSpeed=300, disturbedTurnSpeed=20;
        public Vector2 locomotionPlaybackLimits=new Vector2(.35f,2.2f);
        [Header("Crowd navigation")]
        public bool crowdAvoidance=true;
        [Min(0)] public float crowdPadding=.1f;
        [Min(0)] public float crowdBodyGap=.025f;
        [Min(0)] public float crowdSeparationSpeed=1.2f;
        [Range(0,99)] public int avoidancePriority=50;
        [Range(0,40)] public int avoidancePrioritySpread=12;
        [Min(.1f)] public float stuckAfterSeconds=.55f;
        [Min(.1f)] public float unstuckStepDistance=.75f;
        [Min(.1f)] public float unstuckDuration=.7f;
        [Header("Combat rules")]
        public Vector2 recoverySeconds=new Vector2(.6f,1.5f);
        [Range(0,1)] public float headbuttContinueChance=.75f;
        [Tooltip("Headbutt may stop here if target has escaped, and enters recovery.")]
        public float maxComboContinueDistance=2;
        public bool blockedCountsAsHit;
        public float maxHealth=120;
        [Tooltip("Hits from the rear/side use Stumbling. Front hits use Reaction Hit.")]
        [Range(-1,1)] public float frontHitDot=.25f;
        [Header("Root motion / collision")]
        public bool allowAttackRetreat=true;
        [Tooltip("Optional constrained authored displacement during reactions and before ragdoll.")]
        public float hitRootMotionScale=0, stumbleRootMotionScale=.5f, deathRootMotionScale=.25f;
        public float maxActionSpeed=5, collisionSkin=.025f, navSampleRadius=.25f;
        public float ragdollImpulse=2.5f;
        [Header("Tier-three lethal launch")]
        [Min(0)] public float tierThreeLaunchSpeed=9,tierThreeLaunchUp=4.2f;
        [HideInInspector] public int hitboxSchemaVersion;
        [Header("Per-edge transitions and combo cancels")]
        [Tooltip("Exact From/To rules take precedence over Any Source. Entry/cancel values are SOURCE frames, not percentages.")]
        public ResonatorTransition[] transitions=DefaultTransitions();
        public ResonatorTransition Transition(EnemyState from,EnemyState to)
        {
            if(transitions==null)return null;
            foreach(var r in transitions)if(r.enabled&&!r.anySource&&r.from==from&&r.to==to)return r;
            foreach(var r in transitions)if(r.enabled&&r.anySource&&r.to==to)return r;
            return null;
        }
        static ResonatorTransition Edge(EnemyState from,EnemyState to,float blend,float entry=-1,float hit=-1,float miss=-1,bool combo=false)=>new ResonatorTransition{label=from+" -> "+to,from=from,to=to,blend=blend,entryFrame=entry,cancelFrameOnHit=hit,cancelFrameOnMiss=miss,automaticCombo=combo};
        static ResonatorTransition[] DefaultTransitions()=>new[]{
            Edge(EnemyState.Headbutt,EnemyState.Kicking,.06f,24,43,53,true),
            Edge(EnemyState.Kicking,EnemyState.Attack,.055f,28,49,58,true),
            Edge(EnemyState.Running,EnemyState.Kicking,.08f,24),
            Edge(EnemyState.Waking,EnemyState.Screaming,.18f,0),
            Edge(EnemyState.FeedingRise,EnemyState.Screaming,.18f,0),
            Edge(EnemyState.Screaming,EnemyState.Running,.10f,0),
            Edge(EnemyState.Screaming,EnemyState.Walking,.14f,0),
            Edge(EnemyState.Screaming,EnemyState.Headbutt,.10f,0),
            Edge(EnemyState.Screaming,EnemyState.Punching,.10f,0),
            Edge(EnemyState.Walking,EnemyState.Headbutt,.10f,0),
            Edge(EnemyState.Walking,EnemyState.Kicking,.10f,0),
            Edge(EnemyState.Walking,EnemyState.Punching,.10f,0),
            Edge(EnemyState.Attack,EnemyState.Screaming,.15f),
            new ResonatorTransition{label="Any -> Combat Idle",anySource=true,to=EnemyState.CombatIdle,blend=.15f},
            new ResonatorTransition{label="Any -> Hit",anySource=true,to=EnemyState.Hit,blend=.04f,entryFrame=0},
            new ResonatorTransition{label="Any -> Stumbling",anySource=true,to=EnemyState.Stumbling,blend=.05f,entryFrame=0},
            new ResonatorTransition{label="Any -> Dying",anySource=true,to=EnemyState.Dying,blend=.03f,entryFrame=0}
        };
        public MotionBeat RestBeat(RestActivity rest) => rest==RestActivity.FeignDeath?wake:rest==RestActivity.Feeding?feeding:rest==RestActivity.SlowIdle?idleSlow:rest==RestActivity.RestlessIdle?idleRestless:resonance;
        public AttackBeat AttackFor(EnemyState state) => state==EnemyState.Headbutt?headbutt:state==EnemyState.Kicking?kick:state==EnemyState.Punching?punch:attack;
        public MotionBeat BeatFor(EnemyState s,RestActivity activity=RestActivity.RestlessIdle)
        {
            if(ResonatorEnemy.IsAttack(s))return AttackFor(s).animation;
            switch(s)
            {
                case EnemyState.Dormant:return RestBeat(activity);
                case EnemyState.Waking:return wake;
                case EnemyState.FeedingRise:return new MotionBeat{track=feeding.track,startFrame=feedingResumeFrame,endFrame=feedingRevealFrame,speed=feeding.speed,blend=feeding.blend,events=feeding.events};
                case EnemyState.Agonizing:return agonizing;
                case EnemyState.Screaming:return scream;
                case EnemyState.Walking:return walk;
                case EnemyState.Running:return run;
                case EnemyState.CombatIdle:return idleRestless;
                case EnemyState.Hit:return hit;
                case EnemyState.Stumbling:return stumble;
                case EnemyState.Dying:return death;
                default:return null;
            }
        }
    }
}

