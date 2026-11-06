using System.Collections.Generic;
using UnityEngine;
namespace Resonator.AI
{
    public static class ResonatorCrowd
    {
        static readonly HashSet<ResonatorMotor> motors=new HashSet<ResonatorMotor>();
        static readonly HashSet<ResonatorRagdoll> corpses=new HashSet<ResonatorRagdoll>();
        public static IEnumerable<ResonatorMotor> Motors=>motors;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]static void Clear(){motors.Clear();corpses.Clear();}
        public static void Register(ResonatorMotor motor)
        {
            if(!motor||!motors.Add(motor))return;
            foreach(var other in motors)if(other&&other!=motor)Ignore(motor.BodyCollider,other.BodyCollider);
            foreach(var corpse in corpses)if(corpse&&corpse.Active)foreach(var bone in corpse.boneColliders)Ignore(motor.BodyCollider,bone);
        }
        public static void Unregister(ResonatorMotor motor)=>motors.Remove(motor);
        public static void RegisterCorpse(ResonatorRagdoll corpse)
        {
            corpses.Add(corpse);
            foreach(var motor in motors)if(motor&&motor.IsCrowdBody)foreach(var bone in corpse.boneColliders)Ignore(motor.BodyCollider,bone);
        }
        public static void UnregisterCorpse(ResonatorRagdoll corpse)=>corpses.Remove(corpse);
        static void Ignore(Collider a,Collider b){if(a&&b&&a!=b&&a.enabled&&b.enabled&&a.gameObject.activeInHierarchy&&b.gameObject.activeInHierarchy)Physics.IgnoreCollision(a,b,true);}
        public static Vector3 Separation(ResonatorMotor motor,float dt)
        {
            if(!motor.tuning.crowdAvoidance)return Vector3.zero;
            Vector3 correction=Vector3.zero;
            foreach(var other in motors)
            {
                if(!other||other==motor||!other.IsCrowdBody||Mathf.Abs(other.transform.position.y-motor.transform.position.y)>.8f)continue;
                Vector3 away=Vector3.ProjectOnPlane(motor.transform.position-other.transform.position,Vector3.up);float length=away.magnitude;
                float minimum=motor.radius+other.radius+motor.tuning.crowdBodyGap;
                if(length>=minimum)continue;
                var direction=length>.001f?away/length:(motor.GetInstanceID()<other.GetInstanceID()?Vector3.left:Vector3.right);
                correction+=direction*(minimum-length);
            }
            return Vector3.ClampMagnitude(correction,motor.tuning.crowdSeparationSpeed*Mathf.Max(0,dt));
        }
        public static Vector3 Limit(ResonatorMotor motor,Vector3 delta)
        {
            if(!motor.tuning.crowdAvoidance)return delta;
            foreach(var other in motors)
            {
                if(!other||other==motor||!other.IsCrowdBody||Mathf.Abs(other.transform.position.y-motor.transform.position.y)>.8f)continue;
                var away=Vector3.ProjectOnPlane(motor.transform.position-other.transform.position,Vector3.up);float minimum=motor.radius+other.radius+motor.tuning.crowdBodyGap;
                if(away.magnitude<minimum)
                {
                    if(away.sqrMagnitude<.000001f)away=motor.GetInstanceID()<other.GetInstanceID()?Vector3.left:Vector3.right;
                    float inward=Vector3.Dot(delta,away.normalized);if(inward<0)delta-=away.normalized*inward;
                }
                else
                {
                    float a=delta.sqrMagnitude;if(a<.000001f)continue;
                    float b=2*Vector3.Dot(away,delta),c=away.sqrMagnitude-minimum*minimum,d=b*b-4*a*c;
                    if(b<0&&d>=0){float t=(-b-Mathf.Sqrt(d))/(2*a);if(t>=0&&t<=1)delta*=Mathf.Max(0,t-.001f);}
                }
            }
            return delta;
        }
    }
}
