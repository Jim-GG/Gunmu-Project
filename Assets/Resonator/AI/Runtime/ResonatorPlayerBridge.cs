using UnityEngine;
namespace Resonator.AI
{
    [RequireComponent(typeof(ResonatorTarget))]
    public sealed class ResonatorPlayerBridge : MonoBehaviour
    {
        public ResonatorTarget actor;
        public FirstPerson.FirstPersonMotor motor;
        public FirstPerson.GauntletCombat combat;
        public Transform attackOrigin;
        [Header("Gameplay noise radii")]
        public float walkNoise=7, runNoise=13, attackNoise=16, chargeNoise=9;
        public float walkStepDistance=1.3f, runStepDistance=1.6f;
        [Header("Fist impact timing / damage")]
        public float reach=2.2f, radius=.24f;
        public Vector4 impactSeconds=new Vector4(.18f,.15f,.18f,.20f);
        public Vector4 punchDamage=new Vector4(24,40,65,110);
        public LayerMask hitMask=~0;
        Vector3 previous;float stepDistance,impactAt=-1;int pendingPunch;
        void Awake(){if(!actor)actor=GetComponent<ResonatorTarget>();if(!motor)motor=GetComponent<FirstPerson.FirstPersonMotor>();if(!combat)combat=GetComponent<FirstPerson.GauntletCombat>();previous=transform.position;}
        void OnEnable(){if(!combat)combat=GetComponent<FirstPerson.GauntletCombat>();if(combat){combat.AttackStarted+=Attack;combat.TierChanged+=Charge;}}
        void OnDisable(){if(combat){combat.AttackStarted-=Attack;combat.TierChanged-=Charge;}impactAt=-1;}
        void Attack(int punch)
        {
            ResonatorNoise.Emit(transform.position+Vector3.up,attackNoise,actor);
            pendingPunch=punch;impactAt=Time.time+impactSeconds[Mathf.Clamp(punch-1,0,3)];
        }
        void Charge(int tier){if(tier>0)ResonatorNoise.Emit(transform.position+Vector3.up,chargeNoise,actor);}
        void Update()
        {
            Vector3 change=transform.position-previous;previous=transform.position;change.y=0;
            var controller=GetComponent<CharacterController>();
            if(actor.Alive&&(!controller||controller.isGrounded)&&change.magnitude<2)
            {
                stepDistance+=change.magnitude;bool running=motor&&Vector3.ProjectOnPlane(motor.Velocity,Vector3.up).magnitude>motor.walkSpeed+.25f;
                if(stepDistance>=(running?runStepDistance:walkStepDistance)){stepDistance=0;ResonatorNoise.Emit(transform.position+Vector3.up,running?runNoise:walkNoise,actor);}
            }
            if(impactAt<0||Time.time<impactAt)return;
            impactAt=-1;if(!actor.Alive||!combat||combat.State!=FirstPerson.GauntletCombat.Phase.Punching)return;
            var origin=attackOrigin?attackOrigin:actor.eyes;
            if(!origin)return;
            var hits=Physics.SphereCastAll(origin.position,radius,origin.forward,reach,hitMask,QueryTriggerInteraction.Ignore);
            System.Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            foreach(var hit in hits)
            {
                if(hit.transform.IsChildOf(transform))continue;
                var enemy=hit.collider.GetComponentInParent<ResonatorEnemy>();
                if(enemy)enemy.TakeDamage(punchDamage[Mathf.Clamp(pendingPunch-1,0,3)],transform.position,pendingPunch*1.5f);
                break;
            }
        }
        public void EmitTestNoise(float radius=20)=>ResonatorNoise.Emit(transform.position+Vector3.up,radius,actor);
    }
}
