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
        public Vector3 Velocity => horizontal + Vector3.up * vertical;
        private void Awake() => motor = GetComponent<CharacterController>();
        private void Update()
        {
            if(!input || !viewPivot)return;
            transform.Rotate(0,input.Look.x*sensitivity,0);
            pitch = Mathf.Clamp(pitch-input.Look.y*sensitivity,-85,85);viewPivot.localRotation=Quaternion.Euler(pitch,0,0);
            Vector3 desired=(transform.right*input.Move.x+transform.forward*input.Move.y)*(input.Sprint?sprintSpeed:walkSpeed);
            horizontal=Vector3.MoveTowards(horizontal,desired,acceleration*Time.deltaTime);
            if(motor.isGrounded && vertical<0)vertical=-2;
            if(motor.isGrounded && input.JumpPressed)vertical=Mathf.Sqrt(jumpHeight*-2*gravity);
            vertical+=gravity*Time.deltaTime;
            var flags=motor.Move(Velocity*Time.deltaTime);
            if((flags&CollisionFlags.Above)!=0 && vertical>0)vertical=0;
        }
    }
}
