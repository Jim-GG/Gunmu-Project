using UnityEngine;
namespace FirstPerson
{
    [RequireComponent(typeof(CharacterController))]
    [DefaultExecutionOrder(10)]
    public sealed class FirstPersonMotor : MonoBehaviour
    {
        public FirstPersonInput input;
        public Transform viewPivot;
        public float walkSpeed = 3f, sprintSpeed = 5.5f, acceleration = 20f, jumpHeight = 1.1f, gravity = -20f, sensitivity = .1f;
        private CharacterController motor;
        private Vector3 horizontal;
        private float vertical, pitch;
        private Vector3 carried;
        private float momentumProtection;
        private Vector3 windDrift;
        Vector3 knockDirection;
        float knockDistance,knockDuration,knockElapsed;
        public void Knockback(Vector3 source,float distance,float duration)
        {
            if(distance<=0)return;
            var traversal=GetComponent<CrystalParkour.CrystalTraversal>();if(traversal&&traversal.State!=CrystalParkour.CrystalTraversal.Mode.Free)traversal.Drop("受到冲击，牵引中断");
            knockDirection=Vector3.ProjectOnPlane(transform.position-source,Vector3.up).normalized;if(knockDirection.sqrMagnitude<.001f)knockDirection=-transform.forward;
            knockDistance=distance;knockDuration=Mathf.Max(.02f,duration);knockElapsed=0;
        }
        [Header("Traversal air control")]
        public float airSpeed=5.2f,airAcceleration=18f;
        public bool TraversalAirControl {get;set;}
        public float GravityScale {get;set;}=1;
        public Vector3 WindTargetVelocity {get;set;}
        public float WindResponse {get;set;}=5;
        public Vector3 ControlVelocity=>horizontal;
        public Vector3 WindVelocity=>windDrift;
        // Optional traversal inputs. With their defaults the original controller is unchanged.
        public bool MovementSuspended {get;set;}
        public float? ForcedLookPitch {get;set;}
        public Vector3 ExternalAcceleration {get;set;}
        public float ExternalRiseSpeedLimit {get;set;}=float.PositiveInfinity;
        public float carriedAirDrag=.45f, carriedGroundDrag=18f;
        public Vector3 Velocity => horizontal + carried + windDrift + Vector3.up * vertical;
        public CharacterController Body=>motor?motor:GetComponent<CharacterController>();
        public void Launch(Vector3 velocity,float controlProtection=.1f)
        {MovementSuspended=false;horizontal=Vector3.zero;carried=Vector3.ProjectOnPlane(velocity,Vector3.up);vertical=velocity.y;momentumProtection=Mathf.Max(0,controlProtection);}
        public void StopMomentum(){horizontal=carried=windDrift=Vector3.zero;vertical=0;momentumProtection=0;knockDistance=0;}
        public void SetLookRotation(Quaternion rotation)
        {transform.rotation=Quaternion.Euler(0,rotation.eulerAngles.y,0);pitch=Mathf.DeltaAngle(0,rotation.eulerAngles.x);if(viewPivot)viewPivot.localRotation=Quaternion.Euler(pitch,0,0);}
        private void Awake() => motor = GetComponent<CharacterController>();
        private void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if(!input || !viewPivot)return;
            transform.Rotate(0,input.Look.x*sensitivity,0);
            pitch = ForcedLookPitch.HasValue?Mathf.Clamp(ForcedLookPitch.Value,-85,85):Mathf.Clamp(pitch-input.Look.y*sensitivity,-85,85);viewPivot.localRotation=Quaternion.Euler(pitch,0,0);
            if(MovementSuspended)return;
            bool controlledAir=TraversalAirControl&&!motor.isGrounded;
            float speed=controlledAir?airSpeed:input.Sprint?sprintSpeed:walkSpeed;
            Vector3 desired=(transform.right*input.Move.x+transform.forward*input.Move.y)*speed;
            horizontal=Vector3.MoveTowards(horizontal,desired,(controlledAir?airAcceleration:acceleration)*dt);
            // Airflow drift and WASD are independent budgets: fans cannot erase lateral steering.
            windDrift=Vector3.MoveTowards(windDrift,Vector3.ProjectOnPlane(WindTargetVelocity,Vector3.up),Mathf.Max(.1f,WindResponse)*dt);
            momentumProtection=Mathf.Max(0,momentumProtection-dt);
            if(momentumProtection<=0&&motor.isGrounded && vertical<0)vertical=-2;
            if(motor.isGrounded && input.JumpPressed)vertical=Mathf.Sqrt(jumpHeight*-2*gravity);
            vertical+=gravity*Mathf.Clamp(GravityScale,.2f,1f)*dt;
            carried+=Vector3.ProjectOnPlane(ExternalAcceleration,Vector3.up)*dt;vertical+=ExternalAcceleration.y*dt;
            if(ExternalAcceleration.y>0)vertical=Mathf.Min(vertical,ExternalRiseSpeedLimit);
            carried=Vector3.MoveTowards(carried,Vector3.zero,(motor.isGrounded&&momentumProtection<=0?carriedGroundDrag:carriedAirDrag)*dt);
            Vector3 forced=Vector3.zero;
            if(knockDistance>0&&knockElapsed<knockDuration){float a=knockElapsed/knockDuration;knockElapsed=Mathf.Min(knockDuration,knockElapsed+dt);float b=knockElapsed/knockDuration;forced=knockDirection*knockDistance*((2*b-b*b)-(2*a-a*a));}
            var flags=motor.Move(Velocity*dt+forced);
            if((flags&CollisionFlags.Above)!=0 && vertical>0)vertical=0;
        }
    }
}
