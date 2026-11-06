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
        [Header("Legacy impact values (migrated to shared Action Set)")]
        public float reach=2.2f, radius=.24f;
        public Vector4 impactSeconds=new Vector4(.18f,.15f,.18f,.20f);
        public Vector4 punchDamage=new Vector4(24,40,65,110);
        public LayerMask hitMask=~0;
        Vector3 previous;float stepDistance;
        void Awake(){if(!actor)actor=GetComponent<ResonatorTarget>();if(!motor)motor=GetComponent<FirstPerson.FirstPersonMotor>();if(!combat)combat=GetComponent<FirstPerson.GauntletCombat>();previous=transform.position;}
        void OnEnable(){if(!combat)combat=GetComponent<FirstPerson.GauntletCombat>();if(combat){combat.AttackStarted+=Attack;combat.TierChanged+=Charge;}}
        void OnDisable(){if(combat){combat.AttackStarted-=Attack;combat.TierChanged-=Charge;}}
        void Attack(int punch)
        {
            ResonatorNoise.Emit(transform.position+Vector3.up,attackNoise,actor);
            // Damage is exclusively owned by the shared action hit windows. No delayed timer here.
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
        }
        public void EmitTestNoise(float radius=20)=>ResonatorNoise.Emit(transform.position+Vector3.up,radius,actor);
    }
}
