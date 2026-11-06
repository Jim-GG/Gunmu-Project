using UnityEngine;
namespace Resonator.AI
{
    public sealed class ResonatorAttackMover
    {
        readonly AttackMoveBudget budget=new AttackMoveBudget();
        Vector3 aimPoint;
        public bool AimLocked {get;private set;}
        public float AddedTravel=>budget.TotalTravel;
        public Vector3 AimPoint=>aimPoint;
        public void Reset(AttackBeat attack,float entry,Transform actor,ResonatorTarget target)
        {
            budget.Reset(attack.displacement,entry);aimPoint=target?target.transform.position:actor.position+actor.forward;
            AimLocked=attack.displacement==null||!attack.displacement.trackTarget||entry>=attack.displacement.trackingEndFrame;
        }
        public void Aim(AttackBeat attack,float frame,float dt,ResonatorMotor motor,ResonatorTarget target)
        {
            var d=attack.displacement;if(d==null||!d.enabled||!target)return;
            if(!AimLocked&&frame>=d.trackingStartFrame)
            {
                var direction=Vector3.ProjectOnPlane(target.transform.position-motor.transform.position,Vector3.up);
                if(Vector3.Angle(motor.transform.forward,direction)<=d.maxTrackingAngle)
                {aimPoint=target.transform.position;motor.Face(aimPoint,d.turnSpeed,dt);}
                if(frame>=d.trackingEndFrame-.0001f)AimLocked=true;
            }
        }
        public void Move(AttackBeat attack,float previous,float frame,float dt,ResonatorMotor motor,ResonatorTarget target,Vector3 authored,bool connected,bool touching)
        {
            var d=attack.displacement;
            if(d==null||!d.enabled){motor.AnimationStep(authored,target,motor.tuning.minimumSeparation,attack.rootMotionScale,dt);return;}
            float gap=Vector3.ProjectOnPlane(aimPoint-motor.transform.position,Vector3.up).magnitude;
            var request=budget.Request(d,previous,frame,dt,gap,motor.tuning.minimumSeparation,connected,touching);
            Vector3 delta=Vector3.zero;
            if(request.distance>0&&motor.AttackDirection(aimPoint,dt,out var direction,out float cornerDistance)&&Vector3.Angle(motor.transform.forward,direction)<=d.maxTrackingAngle)
                delta=direction*Mathf.Min(request.distance,cornerDistance);
            float rootScale=d.includeAuthoredRootMotion?attack.rootMotionScale:0;
            var actual=motor.AttackStep(authored,rootScale,delta,request.maxSpeed,target,Mathf.Max(motor.tuning.minimumSeparation,request.contactDistance),dt);
            if(delta.sqrMagnitude>0)
            {
                float forward=Mathf.Max(0,Vector3.Dot(actual,delta.normalized));
                float authoredForward=Mathf.Max(0,Vector3.Dot(motor.transform.rotation*authored*rootScale,delta.normalized));
                budget.Consume(request,forward*delta.magnitude/Mathf.Max(.00001f,delta.magnitude+authoredForward));
            }
        }
    }
}
