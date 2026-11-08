using UnityEngine;
using UnityEngine.AI;
namespace Resonator.AI
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class ResonatorMotor : MonoBehaviour
    {
        public NavMeshAgent agent;
        public ResonatorTuning tuning;
        public Vector3 ActualVelocity {get;private set;}
        public bool Ready => agent&&agent.enabled&&agent.isOnNavMesh;
        public bool PathPending => Ready&&agent.pathPending;
        public float radius=.28f, height=1.8f;
        readonly RaycastHit[] hits=new RaycastHit[32];
        NavMeshPath attackPath;
        readonly Vector3[] attackCorners=new Vector3[128];
        int attackCorner,attackCornerCount;
        Vector3 attackDestination;
        float attackRepath;
        Collider bodyCollider;
        Vector3 pushDirection;float pushDistance,pushDuration,pushElapsed;
        public void Push(Vector3 source,float distance,float duration){pushDirection=Vector3.ProjectOnPlane(transform.position-source,Vector3.up).normalized;pushDistance=distance;pushDuration=Mathf.Max(.02f,duration);pushElapsed=0;}
        public void AdvanceKnockback(float dt)
        {
            if(pushElapsed>=pushDuration||pushDistance<=0)return;float a=pushElapsed/pushDuration;pushElapsed=Mathf.Min(pushDuration,pushElapsed+dt);float b=pushElapsed/pushDuration;Commit(pushDirection*pushDistance*((2*b-b*b)-(2*a-a*a)),null,0,dt);
        }
        public Collider BodyCollider=>bodyCollider?bodyCollider:bodyCollider=GetComponent<Collider>();
        public bool IsCrowdBody=>isActiveAndEnabled&&agent&&agent.enabled;
        Vector3 navigationGoal,unstuckGoal;
        float blockedSeconds,unstuckSeconds;
        bool bypassing;
        NavMeshPath flankPath;
        Vector3 flankGoal,flankTarget;
        float flankClock;
        bool flankValid;
        public int UnstuckAttempts {get;private set;}
        void Awake()
        {
            if(!agent)agent=GetComponent<NavMeshAgent>();
            agent.updatePosition=false;agent.updateRotation=false;agent.autoTraverseOffMeshLink=false;
            ConfigureAvoidance();
        }
        void OnEnable()=>ResonatorCrowd.Register(this);
        void OnDisable()=>ResonatorCrowd.Unregister(this);
        void ConfigureAvoidance()
        {
            if(!agent||!tuning)return;
            agent.autoRepath=true;
            agent.radius=radius+(tuning.crowdAvoidance?tuning.crowdPadding:0);
            agent.obstacleAvoidanceType=tuning.crowdAvoidance?ObstacleAvoidanceType.HighQualityObstacleAvoidance:ObstacleAvoidanceType.NoObstacleAvoidance;
            int span=tuning.avoidancePrioritySpread;
            int jitter=span==0?0:(GetInstanceID()&int.MaxValue)%(span*2+1)-span;
            agent.avoidancePriority=Mathf.Clamp(tuning.avoidancePriority+jitter,0,99);
        }
        public bool PlaceOnMesh()
        {
            if(!agent)agent=GetComponent<NavMeshAgent>();
            agent.updatePosition=false;agent.updateRotation=false;agent.autoTraverseOffMeshLink=false;
            ConfigureAvoidance();ResonatorCrowd.Register(this);
            if(NavMesh.SamplePosition(transform.position,out var h,1,agent.areaMask))return agent.Warp(h.position);
            return false;
        }
        public void Navigate(Vector3 destination,float speed,float stopDistance)
        {
            if(!Ready)return;
            agent.isStopped=false;agent.speed=speed;agent.acceleration=tuning.acceleration;agent.stoppingDistance=stopDistance;
            navigationGoal=destination;
            if(bypassing)
            {
                if(Vector3.ProjectOnPlane(unstuckGoal-transform.position,Vector3.up).magnitude<.15f||unstuckSeconds<=0)bypassing=false;
                else{destination=unstuckGoal;agent.stoppingDistance=.05f;}
            }
            // Avoid rebuilding the path every tick when the destination has not moved.
            if(!agent.hasPath||Vector3.SqrMagnitude(agent.destination-destination)>.04f)agent.SetDestination(destination);
        }
        public void Stop()
        {
            if(Ready){agent.isStopped=true;agent.nextPosition=transform.position;}
            ActualVelocity=Vector3.zero;
            attackRepath=0;attackCornerCount=0;
            bypassing=false;blockedSeconds=unstuckSeconds=0;
            flankValid=false;flankClock=0;
        }
        public void Face(Vector3 point,float degreesPerSecond,float dt)
        {
            var d=point-transform.position;d.y=0;if(d.sqrMagnitude<.0001f)return;
            transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(d),degreesPerSecond*Mathf.Max(0,dt));
        }
        public void NavigationStep(ResonatorTarget target,float separation,float dt)
        {
            if(!Ready||agent.isStopped){ActualVelocity=Vector3.zero;return;}
            Vector3 before=transform.position;Vector3 desired=agent.nextPosition-before;
            // Rewind the simulation until the unified collision owner commits this step.
            agent.nextPosition=before;Commit(desired+ResonatorCrowd.Separation(this,dt),target,separation,dt);
            if(agent.desiredVelocity.sqrMagnitude>.03f)Face(transform.position+agent.desiredVelocity,tuning.navigationTurnSpeed,dt);
            if(bypassing)unstuckSeconds-=dt;
            bool needsProgress=!agent.pathPending&&Vector3.ProjectOnPlane(navigationGoal-transform.position,Vector3.up).magnitude>agent.stoppingDistance+.25f;
            blockedSeconds=needsProgress&&ActualVelocity.magnitude<.12f?blockedSeconds+dt:0;
            if(tuning.crowdAvoidance&&!bypassing&&blockedSeconds>=tuning.stuckAfterSeconds)TryUnstuck();
        }
        void TryUnstuck()
        {
            blockedSeconds=0;UnstuckAttempts++;
            Vector3 forward=Vector3.ProjectOnPlane(navigationGoal-transform.position,Vector3.up).normalized;
            Vector3 side=Vector3.Cross(Vector3.up,forward);int hand=((GetInstanceID()+UnstuckAttempts)&1)==0?1:-1;
            foreach(int sign in new[]{hand,-hand})
            {
                Vector3 desired=transform.position+side*(sign*tuning.unstuckStepDistance);
                if(!NavMesh.SamplePosition(desired,out var point,.35f,agent.areaMask)||NavMesh.Raycast(transform.position,point.position,out _,agent.areaMask))continue;
                Vector3 delta=point.position-transform.position;float length=delta.magnitude;bool clear=true;
                int count=Physics.CapsuleCastNonAlloc(transform.position+Vector3.up*radius,transform.position+Vector3.up*(height-radius),radius,delta.normalized,hits,length,tuning.obstructionMask,QueryTriggerInteraction.Ignore);
                for(int i=0;i<count;i++)if(hits[i].collider&&!hits[i].collider.GetComponentInParent<ResonatorMotor>()&&hits[i].normal.y<.65f){clear=false;break;}
                if(!clear)continue;
                unstuckGoal=point.position;unstuckSeconds=tuning.unstuckDuration;bypassing=true;agent.SetDestination(unstuckGoal);return;
            }
            agent.ResetPath();
        }
        bool PeerBlocks(Vector3 from,Vector3 to)
        {
            var line=Vector3.ProjectOnPlane(to-from,Vector3.up);float length=line.sqrMagnitude;if(length<.001f)return false;
            foreach(var other in ResonatorCrowd.Motors)
            {
                if(!other||other==this||!other.IsCrowdBody||Mathf.Abs(other.transform.position.y-from.y)>.8f)continue;
                var point=Vector3.ProjectOnPlane(other.transform.position-from,Vector3.up);float t=Vector3.Dot(point,line)/length;
                if(t>0&&t<1&&(point-line*t).magnitude<other.radius+.08f)return true;
            }
            return false;
        }
        public bool TryCrowdApproach(Vector3 target,float range,float dt,out Vector3 destination)
        {
            destination=target;if(!tuning.crowdAvoidance||!Ready||!PeerBlocks(transform.position,target)){flankValid=false;return false;}
            flankClock-=dt;
            if(flankValid&&flankClock>0&&(target-flankTarget).sqrMagnitude<.0625f){destination=flankGoal;return true;}
            flankClock=.3f;flankTarget=target;flankValid=false;if(flankPath==null)flankPath=new NavMeshPath();
            var radial=Vector3.ProjectOnPlane(transform.position-target,Vector3.up).normalized;
            int hand=(GetInstanceID()&1)==0?1:-1;float best=float.PositiveInfinity;
            foreach(float angle in new[]{35f,-35f,60f,-60f,85f,-85f})
            {
                var wanted=target+Quaternion.Euler(0,angle*hand,0)*radial*Mathf.Max(tuning.minimumSeparation+.1f,range-.1f);
                if(!NavMesh.SamplePosition(wanted,out var sample,.4f,agent.areaMask)||PeerBlocks(sample.position,target))continue;
                bool occupied=false;
                foreach(var other in ResonatorCrowd.Motors)if(other&&other!=this&&other.IsCrowdBody&&Vector3.ProjectOnPlane(sample.position-other.transform.position,Vector3.up).magnitude<radius+other.radius+.08f){occupied=true;break;}
                if(occupied||!agent.CalculatePath(sample.position,flankPath)||flankPath.status!=NavMeshPathStatus.PathComplete)continue;
                float cost=(sample.position-transform.position).sqrMagnitude;
                if(cost>=best)continue;best=cost;flankGoal=sample.position;flankValid=true;
            }
            if(flankValid)destination=flankGoal;return flankValid;
        }
        public void AnimationStep(Vector3 authoredDelta,ResonatorTarget target,float separation,float scale,float dt)
        {
            Vector3 delta=transform.rotation*authoredDelta*scale;delta.y=0;
            if(!tuning.allowAttackRetreat&&Vector3.Dot(delta,transform.forward)<0)delta-=transform.forward*Vector3.Dot(delta,transform.forward);
            delta=Vector3.ClampMagnitude(delta,tuning.maxActionSpeed*Mathf.Max(0,dt));
            Commit(delta,target,separation,dt);
        }
        public bool AttackDirection(Vector3 destination,float dt,out Vector3 direction,out float cornerDistance)
        {
            direction=Vector3.zero;cornerDistance=0;if(!Ready)return false;attackRepath-=dt;
            if(attackPath==null)attackPath=new NavMeshPath();
            if(attackRepath<=0||attackCornerCount==0||(destination-attackDestination).sqrMagnitude>.0225f)
            {
                attackRepath=.12f;attackDestination=destination;
                if(!NavMesh.SamplePosition(destination,out var goal,1,agent.areaMask)||!agent.CalculatePath(goal.position,attackPath)||attackPath.status==NavMeshPathStatus.PathInvalid){attackCornerCount=0;return false;}
                attackCornerCount=attackPath.GetCornersNonAlloc(attackCorners);attackCorner=Mathf.Min(1,attackCornerCount-1);
            }
            if(attackCornerCount<1)return false;
            while(attackCorner<attackCornerCount-1&&Vector3.ProjectOnPlane(attackCorners[attackCorner]-transform.position,Vector3.up).magnitude<.12f)attackCorner++;
            var delta=Vector3.ProjectOnPlane(attackCorners[attackCorner]-transform.position,Vector3.up);cornerDistance=delta.magnitude;
            if(cornerDistance<.001f)return false;direction=delta/cornerDistance;return true;
        }
        public Vector3 AttackStep(Vector3 authoredDelta,float scale,Vector3 added,float maxSpeed,ResonatorTarget target,float separation,float dt)
        {
            Vector3 root=transform.rotation*authoredDelta*scale;root.y=0;
            if(!tuning.allowAttackRetreat&&Vector3.Dot(root,transform.forward)<0)root-=transform.forward*Vector3.Dot(root,transform.forward);
            Vector3 before=transform.position;
            Commit(Vector3.ClampMagnitude(root+added,Mathf.Max(tuning.maxActionSpeed,maxSpeed)*Mathf.Max(0,dt)),target,separation,dt);
            return Vector3.ProjectOnPlane(transform.position-before,Vector3.up);
        }
        // One owner writes Transform and mirrors its final position to the agent.
        void Commit(Vector3 delta,ResonatorTarget target,float separation,float dt)
        {
            if(!Ready||dt<=0){ActualVelocity=Vector3.zero;return;}
            Vector3 before=transform.position;delta.y=0;
            delta=ResonatorCrowd.Limit(this,delta);
            if(target)
            {
                Vector3 away=before-target.transform.position;away.y=0;
                if(away.magnitude<separation)
                {
                    if(away.sqrMagnitude<.0001f)away=-transform.forward;
                    // No teleporting out of an overlap: remove inward movement only.
                    float inward=Vector3.Dot(delta,away.normalized);
                    if(inward<0)delta-=away.normalized*inward;
                }
                else if(delta.sqrMagnitude>.000001f)
                {
                    float a=delta.sqrMagnitude,b=2*Vector3.Dot(away,delta),c=away.sqrMagnitude-separation*separation;
                    float discriminant=b*b-4*a*c;
                    if(b<0&&discriminant>=0)
                    {
                        float contact=(-b-Mathf.Sqrt(discriminant))/(2*a);
                        if(contact>=0&&contact<=1)delta*=Mathf.Max(0,contact-.001f);
                    }
                }
            }
            Vector3 destination=before+delta;
            if(NavMesh.Raycast(before,destination,out var navHit,agent.areaMask))destination=navHit.position;
            Vector3 travel=destination-before;travel.y=0;float length=travel.magnitude;
            if(length>.0001f)
            {
                int count=Physics.CapsuleCastNonAlloc(before+Vector3.up*radius,before+Vector3.up*(height-radius),radius,travel.normalized,hits,length+tuning.collisionSkin,tuning.obstructionMask,QueryTriggerInteraction.Ignore);
                float allowed=length;
                for(int i=0;i<count;i++)
                {
                    var c=hits[i].collider;if(!c||c.transform.IsChildOf(transform))continue;
                    // Living agents are avoided by RVO + body separation. Corpse
                    // bones are traversable, not an invisible stationary wall.
                    if(c.GetComponentInParent<ResonatorMotor>())continue;
                    // Floor contacts have an upward normal and must not stop horizontal travel.
                    if(hits[i].normal.y>.65f)continue;
                    allowed=Mathf.Min(allowed,Mathf.Max(0,hits[i].distance-tuning.collisionSkin));
                }
                destination=before+travel.normalized*allowed;
            }
            if(NavMesh.SamplePosition(destination,out var floor,tuning.navSampleRadius,agent.areaMask))destination.y=floor.position.y;
            transform.position=destination;agent.nextPosition=destination;
            ActualVelocity=(destination-before)/dt;
        }
        public void Disable(){Stop();ResonatorCrowd.Unregister(this);if(agent)agent.enabled=false;}
    }
}
