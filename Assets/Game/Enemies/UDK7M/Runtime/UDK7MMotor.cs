using UnityEngine;
using UnityEngine.AI;
using GameActions;
namespace UDK7M
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class UDK7MMotor:MonoBehaviour
    {
        public UDK7MTuning tuning;public NavMeshAgent agent;
        public Vector3 ActualVelocity {get;private set;}
        NavMeshPath path;Vector3[] corners=new Vector3[128];int count,index;float repath;
        NavMeshQueryFilter Filter=>new NavMeshQueryFilter{agentTypeID=agent.agentTypeID,areaMask=agent.areaMask};
        public void Initialize(){if(!agent)agent=GetComponent<NavMeshAgent>();agent.updatePosition=false;agent.updateRotation=false;agent.autoTraverseOffMeshLink=false;agent.radius=tuning.navigationRadius;agent.height=tuning.bodyHeight;agent.enabled=true;if(NavMesh.SamplePosition(transform.position,out var sample,3,Filter))agent.Warp(sample.position);path=new NavMeshPath();}
        public Vector3 Direction(Vector3 target,float dt)
        {
            repath-=dt;if(agent&&agent.isOnNavMesh&&(repath<=0||count==0))
            {repath=.2f;if(NavMesh.SamplePosition(target,out var sample,3,Filter)&&agent.CalculatePath(sample.position,path)){count=path.GetCornersNonAlloc(corners);index=Mathf.Min(1,count-1);}}
            if(count>1){while(index<count-1&&Vector3.ProjectOnPlane(corners[index]-transform.position,Vector3.up).magnitude<.6f)index++;return Vector3.ProjectOnPlane(corners[index]-transform.position,Vector3.up).normalized;}
            return Vector3.ProjectOnPlane(target-transform.position,Vector3.up).normalized;
        }
        public float Angle(Vector3 target)=>Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(target-transform.position,Vector3.up));
        public void Face(Vector3 direction,float degrees,float dt){direction.y=0;if(direction.sqrMagnitude>.001f)transform.rotation=Quaternion.RotateTowards(transform.rotation,Quaternion.LookRotation(direction),degrees*dt);}
        public float Move(float speed,float dt)
        {return Displace(transform.forward*Mathf.Max(0,speed)*dt,out _);}
        public float Displace(Vector3 delta,out Vector3 collisionNormal)
        {
            collisionNormal=Vector3.zero;var before=transform.position;float dt=Mathf.Max(.0001f,Time.deltaTime);
            if(!agent||!agent.enabled||!agent.isOnNavMesh){ActualVelocity=Vector3.zero;return 0;}
            var destination=before+delta;if(NavMesh.Raycast(before,destination,out var nav,Filter)){destination=nav.position;collisionNormal=Vector3.ProjectOnPlane(nav.normal,Vector3.up).normalized;}
            var travel=destination-before;travel.y=0;float allowed=travel.magnitude;
            // Inset the sweep so a sphere resting exactly on a floor tile does
            // not report a synthetic, horizontal zero-distance blocking normal.
            float sweepRadius=Mathf.Max(.05f,tuning.bodyRadius-Mathf.Max(.03f,tuning.collisionSkin));
            if(allowed>.0001f)foreach(var hit in Physics.CapsuleCastAll(before+Vector3.up*tuning.bodyRadius,before+Vector3.up*Mathf.Max(tuning.bodyRadius,tuning.bodyHeight-tuning.bodyRadius),sweepRadius,travel.normalized,allowed+tuning.collisionSkin,~0,QueryTriggerInteraction.Ignore))
            {if(hit.transform.IsChildOf(transform)||hit.normal.y>.65f||hit.collider.GetComponentInParent<IActionTarget>()!=null||hit.collider.GetComponentInParent<UDK7MDebris>())continue;float next=Mathf.Max(0,hit.distance-tuning.collisionSkin);if(next<allowed){allowed=next;collisionNormal=Vector3.ProjectOnPlane(hit.normal,Vector3.up).normalized;}}
            transform.position=before+(travel.sqrMagnitude>.0001f?travel.normalized*allowed:Vector3.zero);agent.nextPosition=transform.position;ActualVelocity=(transform.position-before)/Mathf.Max(.0001f,dt);return allowed;
        }
        public void Stop(){ActualVelocity=Vector3.zero;if(agent&&agent.enabled&&agent.isOnNavMesh){agent.isStopped=true;agent.ResetPath();}count=0;}
    }
}
