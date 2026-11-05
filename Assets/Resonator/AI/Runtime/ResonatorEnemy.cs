using System;
using UnityEngine;
using UnityEngine.Events;
namespace Resonator.AI
{
    [RequireComponent(typeof(ResonatorMotor))]
    public sealed class ResonatorEnemy : MonoBehaviour
    {
        public ResonatorTuning tuning;
        public RestActivity initialActivity=RestActivity.RestlessIdle;
        public ResonatorTarget target;
        public Transform crystalFocus;
        public ResonatorMotionPlayer motion;
        public ResonatorMotor motor;
        public ResonatorRagdoll ragdoll;
        [SerializeField] EnemyState state;
        [SerializeField] float health;
        [SerializeField] bool aware;
        public EnemyState State=>state;
        public float Health=>health;
        public bool Aware=>aware;
        public float StateSeconds {get;private set;}
        public float CurrentFrame=>motion?motion.SourceFrame:0;
        public Vector3 LastKnownPosition {get;private set;}
        public event Action<EnemyState> StateChanged;
        public event Action<StrikeResult> StrikeResolved;
        public event Action Died;
        public event Action<MotionFrameEvent> FrameEventReceived;
        public UnityEvent<string> onAnimationEvent=new UnityEvent<string>();
        public bool drawHitboxes=true;
        public int DamageWindowsResolved {get;private set;}
        bool started, currentActionHit, headWantsChain;
        public bool CurrentAttackConnected=>currentActionHit;
        float nextSight, recoveryDuration;
        bool[] visitedWindows=Array.Empty<bool>();
        readonly System.Collections.Generic.HashSet<string> hitGroups=new System.Collections.Generic.HashSet<string>();
        ResonatorHitbox hitbox;
        ResonatorMotionPlayer subscribedMotion;
        readonly ResonatorAttackMover attackMover=new ResonatorAttackMover();
        public float AttackAddedTravel=>attackMover.AddedTravel;
        public bool AttackAimLocked=>attackMover.AimLocked;
        Vector3 lastImpulse;
        MotionBeat feedingRise;
        EnemyState plannedAttack=EnemyState.Headbutt;
        Vector3 targetVelocity,previousTargetPosition;
        ResonatorTarget velocitySource;
        public EnemyState PlannedAttack=>plannedAttack;
        public float PlannedStartDistance=>AttackStartRange(plannedAttack);
        public Vector3 EstimatedTargetVelocity=>targetVelocity;
        public static bool IsAttack(EnemyState s)=>s==EnemyState.Headbutt||s==EnemyState.Kicking||s==EnemyState.Attack||s==EnemyState.Punching;
        public static int Priority(EnemyState s)=>s==EnemyState.Dead||s==EnemyState.Dying?4:s==EnemyState.Hit||s==EnemyState.Stumbling?3:IsAttack(s)?2:1;
        void OnEnable()=>ResonatorNoise.Emitted+=Hear;
        void OnDisable()=>ResonatorNoise.Emitted-=Hear;
        void Start(){if(!started)Initialize(initialActivity);}
        public void Initialize(RestActivity activity)
        {
            if(!tuning||!motion){Debug.LogError("Resonator requires tuning and motion player",this);enabled=false;return;}
            initialActivity=activity;started=true;health=tuning.maxHealth;aware=false;
            if(!motor)motor=GetComponent<ResonatorMotor>();motor.tuning=tuning;
            if(!target)target=FindFirstObjectByType<ResonatorTarget>();
            velocitySource=null;targetVelocity=Vector3.zero;plannedAttack=EnemyState.Headbutt;
            motor.PlaceOnMesh();
            hitbox=new ResonatorHitbox(transform);
            if(subscribedMotion!=motion){if(subscribedMotion)subscribedMotion.FrameEventRaised-=ReceiveFrameEvent;motion.FrameEventRaised+=ReceiveFrameEvent;subscribedMotion=motion;}
            feedingRise=tuning.BeatFor(EnemyState.FeedingRise);
            Enter(EnemyState.Dormant,true);
        }
        void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if(!started||state==EnemyState.Dead||!motion||dt<=0)return;
            StateSeconds+=dt;
            nextSight-=dt;
            bool living=target&&target.Alive;
            UpdateTargetVelocity(dt,living);
            if(aware&&living)LastKnownPosition=target.transform.position;
            if(state==EnemyState.Dormant)
            {
                if(crystalFocus&&initialActivity==RestActivity.Resonating)motor.Face(crystalFocus.position,tuning.navigationTurnSpeed,dt);
                if((initialActivity==RestActivity.SlowIdle||initialActivity==RestActivity.RestlessIdle)&&nextSight<=0)
                {
                    nextSight=tuning.sensorInterval;
                    if(CanSeeTarget()){Detect();return;}
                }
                motion.Advance(dt,1,initialActivity==RestActivity.FeignDeath);return;
            }
            if(state==EnemyState.Dying)
            {
                motion.Advance(dt);
                motor.AnimationStep(motion.RootDelta,target,tuning.minimumSeparation,tuning.deathRootMotionScale,dt);
                if(CurrentFrame>=tuning.ragdollFrame||motion.Finished)BecomeRagdoll();
                return;
            }
            if(aware&&!living&&state!=EnemyState.Hit&&state!=EnemyState.Stumbling)
            {
                if(state!=EnemyState.CombatIdle)BeginRecovery();
                motor.Stop();motion.Advance(dt);return;
            }
            if(state==EnemyState.Walking||state==EnemyState.Running)
            {
                float speed=NavigationSpeed(state==EnemyState.Running);
                float range=AttackStartRange(plannedAttack);
                float stopping=Mathf.Max(tuning.minimumSeparation+.03f,range-.12f);
                // A nearby ally may obstruct the strike line. Keep seeking an
                // opening instead of stopping forever at the launch-radius ring.
                bool canAttack=CanStartAttack(plannedAttack);Vector3 approach=LastKnownPosition;
                if(!canAttack&&Distance<=range+.5f&&motor.TryCrowdApproach(LastKnownPosition,range,dt,out var flank)){approach=flank;stopping=.05f;}
                else if(Distance<=range+tuning.attackDecisionSlack&&!canAttack)stopping=tuning.minimumSeparation+.03f;
                motor.Navigate(approach,speed,stopping);
                motor.NavigationStep(target,tuning.minimumSeparation,dt);
                if(Distance<=range+.25f)motor.Face(LastKnownPosition,tuning.navigationTurnSpeed,dt);
                float actual=Vector3.ProjectOnPlane(motor.ActualVelocity,Vector3.up).magnitude;
                float multiplier=actual<.025f?0:Mathf.Clamp(actual/Mathf.Max(.01f,motion.Beat.track.averageSpeed*motion.Beat.speed),tuning.locomotionPlaybackLimits.x,tuning.locomotionPlaybackLimits.y);
                motion.Advance(dt,multiplier);
                if(CanStartAttack(plannedAttack))
                {
                    BeginAttack(plannedAttack,true);
                }
                else if(state==EnemyState.Walking&&ShouldRun())Enter(EnemyState.Running);
                return;
            }
            if(state==EnemyState.Screaming)motor.Face(LastKnownPosition,tuning.screamTurnSpeed,dt);
            else if(state==EnemyState.Agonizing)motor.Face(LastKnownPosition,tuning.disturbedTurnSpeed,dt);
            else if(IsAttack(state)&&!(tuning.AttackFor(state).displacement?.enabled??false)&&CurrentFrame<tuning.AttackFor(state).lockFacingFrame)motor.Face(LastKnownPosition,tuning.attackTurnSpeed,dt);
            if(IsAttack(state)){TickAttack(dt);return;}
            motion.Advance(dt);
            if(state==EnemyState.Hit||state==EnemyState.Stumbling)
                motor.AnimationStep(motion.RootDelta,target,tuning.minimumSeparation,state==EnemyState.Hit?tuning.hitRootMotionScale:tuning.stumbleRootMotionScale,dt);
            if(state==EnemyState.Waking&&CurrentFrame>=tuning.wakeRevealFrame)Enter(EnemyState.Screaming);
            else if(state==EnemyState.FeedingRise&&CurrentFrame>=tuning.feedingRevealFrame)Enter(EnemyState.Screaming);
            else if(state==EnemyState.Screaming&&motion.Finished)EvaluateCombat(true);
            else if(state==EnemyState.Agonizing&&motion.Finished)EvaluateCombat(false);
            else if((state==EnemyState.Hit||state==EnemyState.Stumbling)&&motion.Finished)
            {
                if(Distance>AttackStartRange(EnemyState.Headbutt)+tuning.attackDecisionSlack)Enter(EnemyState.Running);else BeginAttack(EnemyState.Headbutt,true);
            }
            else if(state==EnemyState.CombatIdle&&StateSeconds>=recoveryDuration)EvaluateCombat(false);
        }
        public float Distance=>target?FlatDistance(transform.position,target.transform.position):float.PositiveInfinity;
        static float FlatDistance(Vector3 a,Vector3 b){a.y=b.y=0;return Vector3.Distance(a,b);}
        void UpdateTargetVelocity(float dt,bool living)
        {
            if(!living){targetVelocity=Vector3.zero;velocitySource=null;return;}
            var point=target.transform.position;
            if(velocitySource!=target){velocitySource=target;previousTargetPosition=point;targetVelocity=Vector3.zero;return;}
            var delta=Vector3.ProjectOnPlane(point-previousTargetPosition,Vector3.up);previousTargetPosition=point;
            if(delta.magnitude>Mathf.Max(1,dt*12)){targetVelocity=Vector3.zero;return;}
            targetVelocity=Vector3.Lerp(targetVelocity,Vector3.ClampMagnitude(delta/dt,8),1-Mathf.Exp(-dt*8));
        }
        float RetreatSpeed=>target?Mathf.Max(0,Vector3.Dot(targetVelocity,Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up).normalized)):0;
        bool ShouldRun()=>Distance>tuning.walkToRunDistance||tuning.retreatRunThreshold>0&&RetreatSpeed>tuning.retreatRunThreshold&&Distance>AttackStartRange(EnemyState.Kicking)+.3f;
        public float AttackStartRange(EnemyState attackState)
        {
            if(!tuning)return 0;var attack=tuning.AttackFor(attackState);
            float maximum=attack.startDistance>0?attack.startDistance:tuning.attackEnterDistance;
            var rule=tuning.Transition(state,attackState);float entry=rule!=null&&rule.entryFrame>=0?rule.entryFrame:attack.animation.startFrame;
            float first=float.PositiveInfinity;
            if(attack.hits!=null)foreach(var h in attack.hits)if(h!=null&&h.enabled&&h.startFrame<=h.endFrame)first=Mathf.Min(first,h.startFrame);
            float lead=attack.animation.track&&float.IsFinite(first)?Mathf.Max(0,(first-entry)/(attack.animation.track.framesPerSecond*Mathf.Max(.05f,attack.animation.speed))):0;
            float correction=RetreatSpeed*Mathf.Min(tuning.maxAttackPredictionSeconds,lead)*tuning.retreatPrediction;
            return Mathf.Max(Mathf.Min(maximum,tuning.attackEnterDistance),maximum-correction);
        }
        bool CanStartAttack(EnemyState attackState)
        {
            if(!target||!target.Alive||Distance>AttackStartRange(attackState)+tuning.attackDecisionSlack)return false;
            var direction=Vector3.ProjectOnPlane(target.transform.position-transform.position,Vector3.up);
            return Vector3.Angle(transform.forward,direction)<=tuning.attackStartFacingAngle&&LineClear(transform.position+Vector3.up,target.ChestPosition);
        }
        float NavigationSpeed(bool run)
        {
            var beat=run?tuning.run:tuning.walk;var limits=run?tuning.runSpeedLimits:tuning.walkSpeedLimits;
            float multiplier=run?tuning.runSpeedMultiplier:tuning.walkSpeedMultiplier;
            return Mathf.Clamp(beat.track.averageSpeed*multiplier,limits.x,limits.y);
        }
        public void Hear(ResonatorNoiseEvent noise)
        {
            if(!started||state==EnemyState.Dying||state==EnemyState.Dead||!noise.source||!noise.source.Alive)return;
            if(Vector3.Distance(transform.position+Vector3.up,noise.position)>noise.radius*tuning.hearingMultiplier)return;
            target=noise.source;LastKnownPosition=noise.position;
            if(state==EnemyState.Dormant)Detect();
        }
        void Detect()
        {
            switch(initialActivity)
            {
                case RestActivity.FeignDeath:Enter(EnemyState.Waking);break;
                case RestActivity.Feeding:Enter(EnemyState.FeedingRise);break;
                case RestActivity.Resonating:aware=true;Enter(EnemyState.Agonizing);break;
                default:Enter(EnemyState.Screaming);break;
            }
        }
        public bool CanSeeTarget()
        {
            if(!target||!target.Alive||Distance>tuning.sightRange)return false;
            Vector3 to=target.ChestPosition-(transform.position+Vector3.up*1.5f);Vector3 flat=Vector3.ProjectOnPlane(to,Vector3.up);
            return Vector3.Angle(transform.forward,flat)<=tuning.sightAngle*.5f&&LineClear(transform.position+Vector3.up*1.5f,target.ChestPosition);
        }
        bool LineClear(Vector3 from,Vector3 to)
        {
            var hits=Physics.RaycastAll(from,(to-from).normalized,Vector3.Distance(from,to),tuning.obstructionMask,QueryTriggerInteraction.Ignore);
            float nearest=float.PositiveInfinity;Collider first=null;
            foreach(var h in hits)
            {
                if(h.collider.transform.IsChildOf(transform))continue;
                var enemy=h.collider.GetComponentInParent<ResonatorEnemy>();
                if(enemy&&(enemy.State==EnemyState.Dead||enemy.State==EnemyState.Dying))continue;
                if(h.distance<nearest){nearest=h.distance;first=h.collider;}
            }
            return !first||(target&&first.transform.IsChildOf(target.transform));
        }
        void EvaluateCombat(bool fromScream)
        {
            if(!target||!target.Alive){motor.Stop();return;}
            if(ShouldRun()){Enter(EnemyState.Running);return;}
            PlanCloseAttack(fromScream);
            if(CanStartAttack(plannedAttack))BeginAttack(plannedAttack,true);else Enter(EnemyState.Walking);
        }
        void PlanCloseAttack(bool fromScream)
        {
            int choice=UnityEngine.Random.Range(0,fromScream?2:3);
            plannedAttack=choice==0?EnemyState.Headbutt:choice==1?EnemyState.Punching:EnemyState.Kicking;
        }
        void BeginAttack(EnemyState next,bool newSequence)
        {
            Enter(next);
        }
        void ResolveWindows(AttackBeat attack)
        {
            if(!target||!target.Alive)return;
            if(attack.hits==null)return;
            if(visitedWindows.Length!=attack.hits.Length)Array.Resize(ref visitedWindows,attack.hits.Length);
            for(int i=0;i<attack.hits.Length;i++)
            {
                var window=attack.hits[i];
                if(window==null||!window.enabled||visitedWindows[i]||CurrentFrame+.0001f<window.startFrame)continue;
                if(!string.IsNullOrEmpty(window.hitGroup)&&hitGroups.Contains(window.hitGroup)){visitedWindows[i]=true;continue;}
                if(CurrentFrame>window.endFrame+.0001f){visitedWindows[i]=true;DamageWindowsResolved++;StrikeResolved?.Invoke(StrikeResult.Miss);continue;}
                StrikeResult result=StrikeResult.Miss;
                if(motion.DestinationWeight>=window.minimumBlendWeight&&hitbox.Contains(window,target)&&LineClear(transform.position+Vector3.up*1.0f,target.ChestPosition))
                    result=target.ReceiveStrike(window.damage,transform.position);
                if(result!=StrikeResult.Miss||CurrentFrame>=window.endFrame)
                {
                    visitedWindows[i]=true;DamageWindowsResolved++;
                    if(result!=StrikeResult.Miss&&!string.IsNullOrEmpty(window.hitGroup))hitGroups.Add(window.hitGroup);
                    if(result==StrikeResult.Damaged||(result==StrikeResult.Blocked&&tuning.blockedCountsAsHit))currentActionHit=true;
                    StrikeResolved?.Invoke(result);
                }
            }
        }
        void TickAttack(float dt)
        {
            var entered=state;var attack=tuning.AttackFor(state);float remaining=dt;
            // Never test an old active window against a later recovery pose. Sample
            // pose, root movement and physics at most one authored frame at a time.
            while(remaining>.000001f&&state==entered)
            {
                float step=Mathf.Min(remaining,1f/Mathf.Max(1,motion.Beat.track.framesPerSecond*motion.Beat.speed));
                // Land exactly on authored boundaries, including sub-frame windows.
                float next=CurrentFrame+step*motion.Beat.track.framesPerSecond*motion.Beat.speed;
                if(attack.hits!=null)foreach(var h in attack.hits)if(h!=null&&h.enabled)
                {
                    if(h.startFrame>CurrentFrame+.0001f)next=Mathf.Min(next,h.startFrame);
                    if(h.endFrame>CurrentFrame+.0001f)next=Mathf.Min(next,h.endFrame);
                }
                var displacement=attack.displacement;
                if(displacement!=null&&displacement.enabled)
                {
                    if(displacement.trackingEndFrame>CurrentFrame+.0001f)next=Mathf.Min(next,displacement.trackingEndFrame);
                    if(displacement.trackingStartFrame>CurrentFrame+.0001f)next=Mathf.Min(next,displacement.trackingStartFrame);
                    if(displacement.segments!=null)foreach(var segment in displacement.segments)if(segment!=null&&segment.enabled)
                    {
                        if(segment.startFrame>CurrentFrame+.0001f)next=Mathf.Min(next,segment.startFrame);
                        if(segment.endFrame>CurrentFrame+.0001f)next=Mathf.Min(next,segment.endFrame);
                    }
                }
                foreach(var destination in AttackExitStates)
                {
                    var rule=tuning.Transition(state,destination);
                    if(rule!=null&&rule.exitFrame>CurrentFrame+.0001f)next=Mathf.Min(next,rule.exitFrame);
                }
                step=Mathf.Min(step,Mathf.Max(.000001f,(next-CurrentFrame)/(motion.Beat.track.framesPerSecond*motion.Beat.speed)));
                attackMover.Aim(attack,CurrentFrame+step*motion.Beat.track.framesPerSecond*motion.Beat.speed,step,motor,target);
                motion.Advance(step);if(state!=entered)return;
                attackMover.Move(attack,motion.PreviousFrame,CurrentFrame,step,motor,target,motion.RootDelta,currentActionHit,ActiveHitboxTouches(attack));
                ResolveWindows(attack);if(state!=entered)return;
                if(TryAutomaticCombo())return;
                if(TryAttackExit())return;
                if(motion.Finished){FinishAttack();return;}
                remaining-=step;
            }
        }
        bool ActiveHitboxTouches(AttackBeat attack)
        {
            if(attack.displacement==null||!attack.displacement.enabled||!target||attack.hits==null)return false;
            foreach(var h in attack.hits)if(h!=null&&h.enabled&&CurrentFrame+.0001f>=h.startFrame&&CurrentFrame<=h.endFrame+.0001f&&motion.DestinationWeight>=h.minimumBlendWeight&&hitbox.Contains(h,target))return true;
            return false;
        }
        void ReceiveFrameEvent(MotionFrameEvent marker)
        {
            if(hitbox==null)return;
            Vector3 point=hitbox.Point(marker.anchor,marker.customBone,marker.position);
            switch(marker.kind)
            {
                case MotionEventKind.Effect:if(marker.effect){var fx=Instantiate(marker.effect,point,hitbox.Anchor(marker.anchor,marker.customBone).rotation);Destroy(fx,Mathf.Max(.01f,marker.effectLifetime));}break;
            }
            FrameEventReceived?.Invoke(marker);onAnimationEvent?.Invoke(marker.name);
        }
        void OnDestroy(){if(subscribedMotion)subscribedMotion.FrameEventRaised-=ReceiveFrameEvent;}
        void OnDrawGizmosSelected()
        {
            if(!drawHitboxes||!Application.isPlaying||!IsAttack(state)||!tuning)return;
            var resolver=hitbox??new ResonatorHitbox(transform);
            var windows=tuning.AttackFor(state).hits;if(windows==null)return;
            foreach(var h in windows)if(h!=null&&h.enabled&&CurrentFrame>=h.startFrame&&CurrentFrame<=h.endFrame)
                ResonatorHitbox.DrawGizmo(h,resolver.Pose(h),motion.DestinationWeight>=h.minimumBlendWeight?Color.red:Color.yellow);
        }
        void FinishAttack()
        {
            if(state==EnemyState.Kicking){BeginAttack(EnemyState.Attack,false);return;}
            if(state==EnemyState.Headbutt&&target&&target.Alive&&Distance<=tuning.maxComboContinueDistance&&headWantsChain)
            {BeginAttack(EnemyState.Kicking,false);return;}
            CompleteAttack();
        }
        static readonly EnemyState[] AttackExitStates={EnemyState.Screaming,EnemyState.CombatIdle};
        bool HasPendingContinuation()
        {
            if(state==EnemyState.Kicking)return true;
            if(state==EnemyState.Headbutt&&headWantsChain&&target&&target.Alive&&Distance<=tuning.maxComboContinueDistance)return true;
            if(tuning.transitions!=null)foreach(var rule in tuning.transitions)
                if(rule!=null&&rule.enabled&&!rule.anySource&&rule.automaticCombo&&rule.from==state&&rule.to!=state&&IsAttack(rule.to))
                {
                    if(state==EnemyState.Headbutt&&rule.to==EnemyState.Kicking)continue;
                    if(rule.CancelFrame(currentActionHit)>=0)return true;
                }
            return false;
        }
        bool TryAttackExit()
        {
            // Only attack completion uses these rules. An Any -> CombatIdle rule
            // must never become an unconditional interrupt of hit/death/locomotion.
            if(HasPendingContinuation())return false;
            var destination=currentActionHit?EnemyState.Screaming:EnemyState.CombatIdle;
            var rule=tuning.Transition(state,destination);if(rule==null)return false;
            if(rule.exitFrame>=0&&CurrentFrame+.0001f<rule.exitFrame)return false;
            if(!currentActionHit)
            {
                var hits=tuning.AttackFor(state).hits;bool any=false;float last=0;
                if(hits!=null)foreach(var h in hits)if(h!=null&&h.enabled&&h.startFrame<=h.endFrame){any=true;last=Mathf.Max(last,h.endFrame);}
                if(any?CurrentFrame+.0001f<last:!motion.Finished)return false;
            }
            CompleteAttack();return true;
        }
        void CompleteAttack(){if(currentActionHit)Enter(EnemyState.Screaming);else BeginRecovery();}
        bool TryAutomaticCombo()
        {
            if(tuning.transitions==null)return false;
            foreach(var rule in tuning.transitions)
            {
                if(!rule.enabled||rule.anySource||!rule.automaticCombo||rule.from!=state||!IsAttack(rule.to))continue;
                if(state==EnemyState.Headbutt&&rule.to==EnemyState.Kicking&&(!headWantsChain||Distance>tuning.maxComboContinueDistance))continue;
                if(TryCancelAttack(rule.to))return true;
            }
            return false;
        }
        public bool TryCancelAttack(EnemyState next)
        {
            if(next==state||!IsAttack(state)||!IsAttack(next)||!target||!target.Alive)return false;
            var rule=tuning.Transition(state,next);if(rule==null)return false;
            float frame=rule.CancelFrame(currentActionHit);
            if(frame<0||CurrentFrame+0.0001f<frame)return false;
            BeginAttack(next,false);return true;
        }
        void BeginRecovery(){recoveryDuration=UnityEngine.Random.Range(tuning.recoverySeconds.x,tuning.recoverySeconds.y);Enter(EnemyState.CombatIdle);}
        public void TakeDamage(float damage,Vector3 source,float force=0)
        {
            if(!started||damage<=0||state==EnemyState.Dead||state==EnemyState.Dying)return;
            health=Mathf.Max(0,health-damage);lastImpulse=Vector3.ProjectOnPlane(transform.position-source,Vector3.up).normalized*Mathf.Max(force,tuning.ragdollImpulse);
            if(health<=0){Enter(EnemyState.Dying);return;}
            aware=true;if(target)LastKnownPosition=target.transform.position;
            Vector3 to=Vector3.ProjectOnPlane(source-transform.position,Vector3.up).normalized;
            Enter(Vector3.Dot(transform.forward,to)>=tuning.frontHitDot?EnemyState.Hit:EnemyState.Stumbling);
        }
        void Enter(EnemyState next,bool immediate=false)
        {
            var previous=state;state=next;StateSeconds=0;hitGroups.Clear();visitedWindows=IsAttack(next)?new bool[tuning.AttackFor(next).hits?.Length??0]:Array.Empty<bool>();currentActionHit=false;motor.Stop();
            if(next==EnemyState.Headbutt)headWantsChain=UnityEngine.Random.value<tuning.headbuttContinueChance;
            if(next==EnemyState.Running)plannedAttack=EnemyState.Kicking;
            MotionBeat beat=null;
            switch(next)
            {
                case EnemyState.Dormant:beat=tuning.RestBeat(initialActivity);break;
                case EnemyState.Waking:beat=tuning.wake;break;
                case EnemyState.FeedingRise:beat=feedingRise;break;
                case EnemyState.Agonizing:beat=tuning.agonizing;break;
                case EnemyState.Screaming:aware=true;if(target)LastKnownPosition=target.transform.position;beat=tuning.scream;break;
                case EnemyState.Walking:beat=tuning.walk;break;
                case EnemyState.Running:beat=tuning.run;break;
                case EnemyState.CombatIdle:beat=tuning.idleRestless;break;
                case EnemyState.Hit:beat=tuning.hit;break;
                case EnemyState.Stumbling:beat=tuning.stumble;break;
                case EnemyState.Dying:beat=tuning.death;break;
                default:if(IsAttack(next))beat=tuning.AttackFor(next).animation;break;
            }
            var transition=tuning.Transition(previous,next);
            if(beat!=null)motion.Play(beat,transition!=null?transition.entryFrame:-1,immediate,transition!=null?transition.blend:-1);
            if(IsAttack(next))attackMover.Reset(tuning.AttackFor(next),CurrentFrame,transform,target);
            StateChanged?.Invoke(next);
        }
        void BecomeRagdoll()
        {
            motor.Disable();motion.StopForRagdoll();state=EnemyState.Dead;
            if(ragdoll)ragdoll.Activate(lastImpulse);
            StateChanged?.Invoke(state);Died?.Invoke();
        }
    }
}
