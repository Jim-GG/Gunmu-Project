using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using FirstPerson;
namespace CrystalParkour
{
    [DefaultExecutionOrder(5),RequireComponent(typeof(FirstPersonMotor))]
    public sealed class CrystalTraversal:MonoBehaviour
    {
        public enum Mode{Free,Pulling,Attached,AwaitingImpact}
        static readonly List<CrystalTraversal> players=new List<CrystalTraversal>();
        public FirstPersonMotor motor;public GauntletCombat combat;public Transform aim;
        public LayerMask surfaceMask=~0;
        public float maximumPullDistance=12,pullSpeed=14,attachmentReach=1.1f,attachmentSeconds=2,pullTimeout=1.8f,reattachCooldown=.55f,impactGrace=1.4f;
        public float surfaceDetectionRadius=.12f,launchSteering=1.5f,launchControlProtection=.1f;
        public float fallResetHeight=-12;
        public bool respawnKeyEnabled=true;
        public Mode State{get;private set;}
        public CrystalSurface Target{get;private set;}
        public Vector3 SurfaceNormal{get;private set;}
        public Vector3 ContactPoint=>Target?Target.transform.TransformPoint(localPoint):Vector3.zero;
        public float AttachedElapsed{get;private set;}
        public float Remaining=>Mathf.Max(0,attachmentSeconds-AttachedElapsed);
        public int AttackSerial{get;private set;}
        public int Rebounds{get;private set;}
        public bool UsedWind{get;private set;}
        public bool GrewFloor{get;set;}public bool GrewWall{get;set;}public bool RodeDrone{get;set;}public bool Completed{get;private set;}
        public string Hint{get;private set;}="按住左键对准晶体牵引；进入击打距离后 2 秒内松开";
        public Vector3 LastLaunchVelocity{get;private set;}
        Vector3 localPoint,incoming,checkpoint;Quaternion checkpointRotation;
        CrystalSurface lastSurface;float cooldown,stageTime,stalled,windExitBlend=.3f;bool reboundSpent,requireFreshCharge;
        public static CrystalTraversal Find(Vector3 source){CrystalTraversal closest=null;float distance=.25f;foreach(var p in players)if(p){float d=(p.transform.position-source).sqrMagnitude;if(d<distance){distance=d;closest=p;}}return closest;}
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void ResetRegistry()=>players.Clear();
        void Awake(){if(!motor)motor=GetComponent<FirstPersonMotor>();if(!combat)combat=GetComponent<GauntletCombat>();if(!aim)aim=motor.viewPivot;checkpoint=transform.position;checkpointRotation=transform.rotation;}
        void OnEnable(){if(!players.Contains(this))players.Add(this);if(combat)combat.AttackStarted+=AttackBegan;if(motor)motor.TraversalAirControl=true;}
        void OnDisable(){players.Remove(this);if(combat)combat.AttackStarted-=AttackBegan;Drop("牵引已结束");if(motor){motor.ExternalAcceleration=Vector3.zero;motor.ExternalRiseSpeedLimit=float.PositiveInfinity;motor.GravityScale=1;motor.WindTargetVelocity=Vector3.zero;motor.TraversalAirControl=false;}}
        public bool TryFindSurface(out RaycastHit chosen,out CrystalSurface surface)
        {
            chosen=default;surface=null;if(!aim)return false;
            var hits=Physics.SphereCastAll(aim.position,surfaceDetectionRadius,aim.forward,maximumPullDistance,surfaceMask,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var h in hits)
            {
                if(h.transform.IsChildOf(transform))continue;
                var s=h.collider.GetComponentInParent<CrystalSurface>();
                if(!s||!s.canTether||!s.ActionTargetAlive)return false;
                if(s==lastSurface)return false;chosen=h;surface=s;return true;
            }
            return false;
        }
        bool ChargeHeld=>Mouse.current!=null&&Mouse.current.leftButton.isPressed&&combat&&(combat.State==GauntletCombat.Phase.Charging||combat.State==GauntletCombat.Phase.Full);
        void Update()
        {
            if(respawnKeyEnabled&&Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame){Respawn();return;}
            Tick(Time.deltaTime,ChargeHeld,Cursor.lockState==CursorLockMode.Locked);
        }
        public void Tick(float dt,bool chargeHeld,bool controlsActive=true)
        {
            if(!motor||!aim||!combat)return;cooldown=Mathf.Max(0,cooldown-dt);
            var wind=WindVolume.Evaluate(transform.position+Vector3.up,Time.timeSinceLevelLoad);
            if(wind.volumes>0){UsedWind=true;windExitBlend=wind.exitBlend;}
            motor.GravityScale=Mathf.MoveTowards(motor.GravityScale,wind.gravityScale,dt/Mathf.Max(.02f,wind.volumes>0?wind.enterBlend:windExitBlend));
            motor.WindTargetVelocity=wind.drift;motor.WindResponse=wind.volumes>0?wind.response:Mathf.Max(5,8/Mathf.Max(.02f,windExitBlend));
            motor.ExternalAcceleration=Vector3.zero;motor.ExternalRiseSpeedLimit=float.PositiveInfinity;
            if(transform.position.y<fallResetHeight){Respawn();return;}
            if(motor.Body.isGrounded&&State==Mode.Free&&cooldown<=0)lastSurface=null;
            if(!controlsActive||!combat.externalInputEnabled||GetComponent<FirstPersonVitals>()?.Health<=0){if(State!=Mode.Free)Drop("牵引中断");return;}
            if(State==Mode.Free)
            {
                if(requireFreshCharge){if(!chargeHeld)requireFreshCharge=false;return;}
                if(cooldown<=0&&chargeHeld&&TryFindSurface(out var h,out var s))BeginTether(s,h.point,h.normal);
                return;
            }
            if(!Target||!Target.ActionTargetAlive){Drop("晶体已失去接触");return;}
            stageTime+=dt;
            if(State==Mode.Pulling)
            {
                if(!chargeHeld){Drop("牵引取消");return;}
                var delta=ContactPoint-aim.position;float remaining=delta.magnitude-attachmentReach;
                if(remaining<=.04f){Attach();return;}
                var before=transform.position;motor.Body.Move(delta.normalized*Mathf.Min(remaining,pullSpeed*dt));
                stalled=(transform.position-before).sqrMagnitude<.000001f?stalled+dt:0;
                if(stalled>.22f||stageTime>pullTimeout)Drop("路径被阻挡；重新瞄准晶体");
            }
            else if(State==Mode.Attached)
            {
                if(!chargeHeld&&combat.State!=GauntletCombat.Phase.ReleasePending&&combat.State!=GauntletCombat.Phase.Punching){Drop("攀附已取消");return;}
                AttachedElapsed+=dt;
                if(AttachedElapsed>attachmentSeconds){Drop("超过 2 秒，攀附失效");return;}
                if(Vector3.Distance(aim.position,ContactPoint)>attachmentReach+1.2f){Drop("离开攀附范围");return;}
            }
            else if(State==Mode.AwaitingImpact&&(stageTime>impactGrace||combat.State!=GauntletCombat.Phase.Punching))Drop("挥击未击中晶体");
        }
        // Public entry point also permits deterministic mechanic validation without synthetic mouse input.
        public bool BeginTether(CrystalSurface surface,Vector3 point,Vector3 normal)
        {
            if(!surface||!surface.ActionTargetAlive||surface==lastSurface||cooldown>0||requireFreshCharge)return false;
            Target=surface;localPoint=surface.transform.InverseTransformPoint(point);SurfaceNormal=surface.useLaunchNormal?surface.WorldLaunchNormal:normal.normalized;incoming=motor.Velocity;AttachedElapsed=stageTime=stalled=0;reboundSpent=false;
            motor.StopMomentum();motor.MovementSuspended=true;State=Mode.Pulling;Hint="牵引中——计时尚未开始";
            if(Vector3.Distance(aim.position,point)<=attachmentReach+.04f)Attach();return true;
        }
        void Attach(){State=Mode.Attached;stageTime=AttachedElapsed=0;Hint="已攀附：2 秒内松开左键，命中时反弹";}
        void AttackBegan(int punch)
        {
            AttackSerial++;
            if(State==Mode.Attached&&AttachedElapsed<=attachmentSeconds){State=Mode.AwaitingImpact;stageTime=0;Hint="等待拳套实际命中";}
            else if(State==Mode.Pulling)Drop("未进入击打距离，停止牵引");
        }
        public bool Strike(CrystalSurface surface)
        {
            if(State!=Mode.AwaitingImpact||Target!=surface||reboundSpent)return false;
            reboundSpent=true;
            var n=SurfaceNormal.normalized;var tangent=Vector3.ProjectOnPlane(incoming,n)*surface.tangentialRetention;
            var steering=Vector3.ProjectOnPlane(aim.forward,n);steering.y=0;if(steering.sqrMagnitude>1)steering.Normalize();
            LastLaunchVelocity=n*surface.normalLaunchSpeed+Vector3.up*surface.upwardLaunchSpeed+tangent+steering*launchSteering;
            lastSurface=surface;Target=null;State=Mode.Free;cooldown=reattachCooldown;motor.Launch(LastLaunchVelocity,launchControlProtection);Rebounds++;Hint="冲击反弹！空中用 WASD 调整，寻找下一块晶体";return true;
        }
        public void Drop(string reason){if(State!=Mode.Free)requireFreshCharge=true;if(motor)motor.MovementSuspended=false;Target=null;State=Mode.Free;cooldown=reattachCooldown;Hint=reason;}
        public void SetCheckpoint(Vector3 position,Quaternion rotation,string label){checkpoint=position;checkpointRotation=rotation;Hint="检查点："+label;}
        public void Respawn()
        {
            Drop("已返回最近检查点");Completed=false;lastSurface=null;requireFreshCharge=false;cooldown=.3f;combat.Cancel();var body=motor.Body;body.enabled=false;transform.SetPositionAndRotation(checkpoint,checkpointRotation);body.enabled=true;motor.StopMomentum();motor.SetLookRotation(checkpointRotation);GetComponent<FirstPersonVitals>()?.Restore();
        }
        public bool TryComplete(){if(Rebounds<=0||!UsedWind||!GrewFloor||!GrewWall||!RodeDrone){Hint="还需完成：反弹、风洞、地面生晶、墙面生晶、无人机借力";return false;}Completed=true;Hint="试玩完成！R 可回到最近检查点继续练习";return true;}
        void OnDrawGizmosSelected(){if(!aim)return;Gizmos.color=Color.cyan;Gizmos.DrawRay(aim.position,aim.forward*maximumPullDistance);if(Target){Gizmos.color=Color.magenta;Gizmos.DrawWireSphere(ContactPoint,.16f);Gizmos.DrawRay(ContactPoint,SurfaceNormal);}}
    }
}
