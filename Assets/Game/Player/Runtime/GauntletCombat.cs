using System;
using UnityEngine;

namespace FirstPerson
{
    [Serializable]
    public sealed class ChargePolicy
    {
        public float framesPerSecond = 30f, quickClickSeconds = .12f;
        public int releaseGate = 13, tierOne = 32, tierTwo = 62, full = 92;
        public int Frame(float seconds) => Mathf.FloorToInt(seconds * framesPerSecond + .0001f);
        public int Punch(float seconds) => seconds <= quickClickSeconds ? 1 : Frame(seconds) < tierTwo ? 2 : Frame(seconds) < full ? 3 : 4;
        public int Tier(float seconds) => Frame(seconds) >= full ? 3 : Frame(seconds) >= tierTwo ? 2 : Frame(seconds) >= tierOne ? 1 : 0;

        // Add the skipped duration to preserve the remainder of this tick, even at low FPS.
        // A visited interval cannot consume another stack during the same charge.
        public float Advance(float seconds, float dt, ref int energy, ref int visited, out int skipped)
        {
            float next = seconds + Mathf.Max(0, dt);
            skipped = 0;
            for (int stage = 0; stage < 3; stage++)
            {
                int start = stage == 0 ? 13 : stage == 1 ? 33 : 63;
                int destination = stage == 0 ? 31 : stage == 1 ? 61 : 91;
                if ((visited & (1 << stage)) != 0 || Frame(next) < start) continue;
                visited |= 1 << stage;
                if (energy <= 0 || Frame(seconds) >= destination) continue;
                float remaining = Mathf.Max(0, destination / framesPerSecond - Mathf.Max(seconds, start / framesPerSecond));
                next += remaining;
                energy--; skipped++;
            }
            return next;
        }
    }

    [DefaultExecutionOrder(20)]
    public sealed class GauntletCombat : MonoBehaviour
    {
        public enum Phase { Idle, Charging, ReleasePending, Full, Punching, ParryStart, ParryKeep, ParryHit, ParryHolster }
        public Gauntlet.GauntletAnimationPlayer animationPlayer;
        public GameActions.ActionSet profile;
        public GameActions.ActionRunner actions;
        public Transform attackOrigin;
        public LayerMask obstructionMask=~0;
        public ChargePolicy policy = new ChargePolicy();
        [Range(0,360)] public float blockAngle=120;
        public string fullState="charge full";
        public bool externalInputEnabled=true, drawHitboxes=true;
        public bool useTargetAssist=true;
        public event Action<Collider,Vector3,Vector3> ContactResolved;
        public Phase State {get;private set;}
        public float ChargeSeconds {get;private set;}
        public int ChargeTier=>policy.Tier(ChargeSeconds);
        public int BlockEnergy {get;private set;}
        public float ResonanceRemaining=>Mathf.Max(0,resonanceUntil-Time.time);
        float resonanceUntil;
        public void GrantResonance(float seconds){resonanceUntil=Mathf.Max(resonanceUntil,Time.time+seconds);SetEnergy(3);}
        public void AddEnergy(int amount)=>SetEnergy(BlockEnergy+amount);
        public void ResetResonance(){resonanceUntil=0;SetEnergy(0);}
        public int ActivePunch=>State==Phase.Punching?activePunch:0;
        public float CurrentFrame=>actions&&actions.motion?actions.motion.SourceFrame:0;
        public bool HasBufferedInput=>buffer.Valid(clock);
        public bool IsBlocking=>State==Phase.ParryStart||State==Phase.ParryKeep||State==Phase.ParryHit;
        public float AssistTravel=>assist.Travel;
        public Transform AssistTarget=>assist.Target;
        public int ContactsThisAttack=>contacts.HitCount;
        public GameActions.GuardKind LastGuard {get;private set;}
        public event Action<int> TierChanged,AttackStarted,AttackFinished,AttackCancelled,EnergyChanged,ChargeSkipped;
        public event Action AttackBlocked;
        public event Action<Resonator.AI.MotionFrameEvent> FrameEventReceived;
        public UnityEngine.Events.UnityEvent<string> onAnimationEvent=new UnityEngine.Events.UnityEvent<string>();
        readonly GameActions.ActionInputBuffer buffer=new GameActions.ActionInputBuffer();
        readonly GameActions.ActionContact contacts=new GameActions.ActionContact();
        readonly GameActions.ActionTargetAssist assist=new GameActions.ActionTargetAssist();
        readonly GameActions.ActionEventDispatcher eventDispatcher=new GameActions.ActionEventDispatcher();
        GameActions.ActionHitbox boxes;
        GameActions.GauntletSurfaceVFX surfaceVFX;
        CharacterController body;
        FirstPersonVitals vitals;
        bool held,blockHeld,connected,ready,attackOverridesGuard,stopping;
        float heldSeconds;
        double clock;
        int lastTier,activePunch,visitedSkips;
        bool CanAct=>externalInputEnabled&&!GameplayGate.IsBlocked&&(!vitals||vitals.Health>0)&&(!GetComponent<PlayerChargedPunch>()||GetComponent<PlayerChargedPunch>().CanUseSharedCombat);
        void Awake()=>InitializePipeline();
        public bool InitializePipeline()
        {
            if(ready&&actions&&actions.motion&&boxes!=null&&actions.profile)return true;
            if(!animationPlayer)animationPlayer=GetComponentInChildren<Gauntlet.GauntletAnimationPlayer>();
            if(!profile)profile=Resources.Load<GameActions.ActionSet>("GauntletActions");
            if(!animationPlayer||!animationPlayer.gameObject.activeInHierarchy||!animationPlayer.animator||!profile)return false;
            animationPlayer.SuspendAutomaticPlayback();
            var go=animationPlayer.animator.gameObject;
            var motion=go.GetComponent<GameActions.ActionMotionPlayer>();if(!motion)motion=go.AddComponent<GameActions.ActionMotionPlayer>();
            motion.animator=animationPlayer.animator;motion.hips=null;
            if(!actions)actions=go.GetComponent<GameActions.ActionRunner>();if(!actions)actions=go.AddComponent<GameActions.ActionRunner>();
            actions.motion=motion;actions.profile=profile;
            body=GetComponent<CharacterController>();vitals=GetComponent<FirstPersonVitals>();
            if(!attackOrigin){var motor=GetComponent<FirstPersonMotor>();attackOrigin=motor&&motor.viewPivot?motor.viewPivot:transform.Find("View Pivot");}
            if(!attackOrigin)attackOrigin=transform;
            boxes=new GameActions.ActionHitbox(transform);boxes.Bind(Resonator.AI.HitboxAnchor.Root,attackOrigin);
            // Bind semantic names once; future rigs may use their own transforms.
            foreach(var t in go.GetComponentsInChildren<Transform>(true))
            {
                if(t.name=="hand_L.R")boxes.Bind(Resonator.AI.HitboxAnchor.RightHand,t);
                if(t.name=="hand_L.L")boxes.Bind(Resonator.AI.HitboxAnchor.LeftHand,t);
            }
            boxes.BindProfile(profile);
            surfaceVFX=GetComponent<GameActions.GauntletSurfaceVFX>();if(!surfaceVFX)surfaceVFX=gameObject.AddComponent<GameActions.GauntletSurfaceVFX>();
            contacts.SurfaceContactDelivered-=SurfaceContact;contacts.SurfaceContactDelivered+=SurfaceContact;
            motion.FrameEventRaised-=ReceiveFrameEvent;motion.FrameEventRaised+=ReceiveFrameEvent;ready=true;GameActions.WeaponEnergyVFXController.Ensure(this);GameActions.GauntletAdvancedPresentation.Ensure(this);GameActions.GauntletCombatFeedback.Ensure(this);ReturnIdle(true);return true;
        }
        void OnEnable(){stopping=false;ready=false;if(Application.isPlaying)InitializePipeline();}
        void SurfaceContact(Collider hit,Vector3 point,Vector3 normal){if(surfaceVFX)surfaceVFX.Contact(hit,point,normal);ContactResolved?.Invoke(hit,point,normal); }
        void ReceiveFrameEvent(Resonator.AI.MotionFrameEvent marker)
        {eventDispatcher.Dispatch(marker,boxes);FrameEventReceived?.Invoke(marker);onAnimationEvent?.Invoke(marker.name);}
        public void Press()
        {
            if(!CanAct||!InitializePipeline()||State==Phase.Charging||State==Phase.ReleasePending||State==Phase.Full)return;
            if(!actions.CanBuffer("charge")&&State!=Phase.Idle)return;
            // The most recent explicit command wins. A fresh left press can leave
            // held guard; held right resumes guard after the next punch's impact.
            attackOverridesGuard=true;
            buffer.Press(clock,profile.inputBufferSeconds);TryBuffered();
        }
        public void Release()
        {
            buffer.Release(clock);held=false;
            if(State!=Phase.Charging&&State!=Phase.Full)return;
            if(heldSeconds<=policy.quickClickSeconds){Attack(1);return;}
            if(policy.Frame(ChargeSeconds)<policy.releaseGate)State=Phase.ReleasePending;
            else Attack(policy.Punch(ChargeSeconds));
        }
        bool TryBuffered()
        {
            if(blockHeld&&!attackOverridesGuard)
            {
                if(State==Phase.Punching&&actions.CanCancel("Parry-Start"))
                {FinishAttack(true);EnterParry(Phase.ParryStart,"Parry-Start");return true;}
                return false;
            }
            if(!buffer.Valid(clock)||State!=Phase.Idle&&!actions.CanCancel("charge"))return false;
            bool keepHolding=buffer.Held;float duration=buffer.HeldSeconds;buffer.Clear();
            if(State==Phase.Punching)FinishAttack(true);
            held=keepHolding;heldSeconds=ChargeSeconds=0;lastTier=visitedSkips=0;
            State=Phase.Charging;actions.Play("charge");animationPlayer.SetCharge(0);
            if(!keepHolding){heldSeconds=duration;Release();}
            return true;
        }
        public void BeginBlock()
        {
            if(!CanAct||!InitializePipeline())return;
            blockHeld=true;attackOverridesGuard=false;buffer.Clear();
            if(IsBlocking)return;
            if(State==Phase.Punching&&!actions.CanCancel("Parry-Start"))return;
            if(State==Phase.Punching)FinishAttack(true);
            held=false;ChargeSeconds=0;animationPlayer.SetCharge(0);EnterParry(Phase.ParryStart,"Parry-Start");
        }
        public void EndBlock()
        {blockHeld=false;if(IsBlocking)EnterParry(Phase.ParryHolster,"Parry-Holster");}
        public bool TryBlock(Vector3 attackerPosition)
        {return ResolveGuard(attackerPosition)!=GameActions.GuardKind.None;}
        public GameActions.GuardKind ResolveGuard(Vector3 attackerPosition)
        {
            if(!CanAct||!IsBlocking)return GameActions.GuardKind.None;
            var direction=Vector3.ProjectOnPlane(attackerPosition-transform.position,Vector3.up);
            if(direction.sqrMagnitude>.0001f&&Vector3.Angle(transform.forward,direction)>blockAngle*.5f)return GameActions.GuardKind.None;
            var result=State==Phase.ParryStart?GameActions.GuardKind.Perfect:GameActions.GuardKind.Imperfect;
            LastGuard=result;SetEnergy(BlockEnergy+1);EnterParry(Phase.ParryHit,"Parry-Hit");AttackBlocked?.Invoke();return result;
        }
        void EnterParry(Phase phase,string clip)
        {assist.Clear();State=phase;actions.Play(clip);animationPlayer.SetCharge(0);}
        void SetEnergy(int value)
        {value=Mathf.Clamp(value,0,3);if(value==BlockEnergy)return;BlockEnergy=value;EnergyChanged?.Invoke(value);}
        void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if(!InitializePipeline())return;
            if(!CanAct){if(State!=Phase.Idle||HasBufferedInput)Cancel();return;}
            if(GameActions.GauntletCombatFeedback.Stopped(gameObject))return;
            float remaining=Mathf.Max(0,dt);
            while(remaining>.000001f)
            {
                var motion=actions.motion;var definition=actions.Current;if(definition==null)break;
                float fps=Mathf.Max(1,definition.Motion.track.framesPerSecond),speed=Mathf.Max(.05f,definition.Motion.speed);
                float step=Mathf.Min(remaining,1/(fps*speed));
                float previous=motion.SourceFrame,next=previous+step*fps*speed;
                // Evaluate hitboxes and displacement at their actual poses, even at low FPS.
                if(State==Phase.Punching&&definition.action.hits!=null)foreach(var h in definition.action.hits)if(h!=null&&h.enabled)
                {if(h.startFrame>previous+.0001f)next=Mathf.Min(next,h.startFrame);if(h.endFrame>previous+.0001f)next=Mathf.Min(next,h.endFrame);}
                if(profile.transitions!=null)foreach(var rule in profile.transitions)if(rule!=null&&rule.enabled&&(rule.anySource||rule.from==actions.CurrentId)&&rule.cancelFrame>previous+.0001f)next=Mathf.Min(next,rule.cancelFrame);
                step=Mathf.Min(step,Mathf.Max(.000001f,(next-previous)/(fps*speed)));clock+=step;
                var beforeState=State;motion.Advance(step);
                if(State!=beforeState||motion.Beat!=definition.Motion){remaining-=step;continue;}
                if(State==Phase.Punching)
                {
                    if(useTargetAssist)assist.Step(definition,previous,motion.SourceFrame,step,transform,attackOrigin,body,obstructionMask,profile.minimumSeparation,connected);
                    Physics.SyncTransforms();
                    connected|=contacts.Resolve(definition,motion,boxes,transform,attackOrigin.position,obstructionMask,activePunch*1.5f,step,Mathf.Clamp(activePunch-1,0,3),attackOrigin.forward);
                    if(!TryBuffered()&&State==Phase.Punching&&motion.Finished)
                    {
                        FinishAttack(false);ReturnIdle();
                        if(blockHeld)EnterParry(Phase.ParryStart,"Parry-Start");else TryBuffered();
                    }
                }
                else if(IsBlocking||State==Phase.ParryHolster)
                {
                    if(!TryBuffered())
                    {
                        if((State==Phase.ParryStart||State==Phase.ParryHit)&&motion.Finished)EnterParry(blockHeld?Phase.ParryKeep:Phase.ParryHolster,blockHeld?"Parry-Keep":"Parry-Holster");
                        else if(State==Phase.ParryHolster&&motion.Finished){ReturnIdle();TryBuffered();}
                    }
                }
                else if(State==Phase.Charging||State==Phase.ReleasePending)
                {
                    heldSeconds+=step;
                    if(State==Phase.Charging)
                    {
                        int energy=ResonanceRemaining>0?3:BlockEnergy;float seconds=policy.Advance(ChargeSeconds,step*speed,ref energy,ref visitedSkips,out int skipped);
                        ChargeSeconds=seconds;
                        if(skipped>0){SetEnergy(ResonanceRemaining>0?3:energy);motion.Seek(seconds*fps);ChargeSkipped?.Invoke(skipped);}
                    }
                    else ChargeSeconds=motion.SourceSeconds;
                    animationPlayer.SetCharge(Mathf.Clamp01(ChargeSeconds*policy.framesPerSecond/policy.full));
                    int tier=ChargeTier;if(tier!=lastTier){lastTier=tier;TierChanged?.Invoke(tier);}
                    if(State==Phase.ReleasePending&&policy.Frame(ChargeSeconds)>=policy.releaseGate)Attack(2);
                    else if(State==Phase.Charging&&held&&policy.Frame(ChargeSeconds)>=policy.full){State=Phase.Full;actions.Play(fullState);}
                }
                else if(State==Phase.Idle)TryBuffered();
                remaining-=step;
                if(GameActions.GauntletCombatFeedback.Stopped(gameObject))break;
            }
        }
        void Attack(int punch)
        {
            activePunch=punch;held=false;attackOverridesGuard=false;State=Phase.Punching;connected=false;contacts.Reset();buffer.Clear();
            actions.Play("punch"+punch);assist.Reset(actions.Current,CurrentFrame,transform,attackOrigin,obstructionMask);
            animationPlayer.SetCharge(punch==4?1:Mathf.Clamp01((punch-1)/3f));AttackStarted?.Invoke(punch);
        }
        void FinishAttack(bool cancelled)
        {int completed=activePunch;assist.Clear();if(cancelled)AttackCancelled?.Invoke(completed);AttackFinished?.Invoke(completed);}
        void ReturnIdle(bool immediate=false)
        {State=Phase.Idle;ChargeSeconds=0;actions.Play("Idle",immediate);animationPlayer.SetCharge(0);}
        public void Cancel()
        {
            if(State==Phase.Punching)FinishAttack(true);
            held=blockHeld=attackOverridesGuard=false;buffer.Clear();assist.Clear();contacts.Reset();State=Phase.Idle;ChargeSeconds=heldSeconds=0;
            if(Application.isPlaying&&!stopping&&ready&&actions&&actions.motion&&animationPlayer&&animationPlayer.animator&&animationPlayer.animator.isActiveAndEnabled)ReturnIdle(true);
        }
        void OnApplicationQuit()=>stopping=true;
        void OnDisable()=>Cancel();
        void OnDestroy(){if(actions&&actions.motion)actions.motion.FrameEventRaised-=ReceiveFrameEvent;}
        void OnDrawGizmosSelected()
        {
            if(!drawHitboxes||!Application.isPlaying||State!=Phase.Punching||boxes==null||actions?.Current==null)return;
            foreach(var h in actions.Current.action.hits)if(GameActions.ActionContact.Active(h,CurrentFrame))GameActions.ActionHitbox.DrawGizmo(h,boxes.Pose(h),Color.red);
            if(assist.Target){Gizmos.color=Color.cyan;Gizmos.DrawLine(transform.position+Vector3.up,assist.Target.position+Vector3.up);}
        }
    }
}
