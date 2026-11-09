using UnityEngine;
using FirstPerson;
using CrystalParkour;
namespace UDK7M
{
    [DefaultExecutionOrder(30)]public sealed class UDK7MGroundPound:MonoBehaviour
    {
        public UDK7MArena arena;public bool Diving {get;private set;}
        FirstPersonMotor motor;GauntletCombat combat;CrystalTraversal traversal;float time;
        void Awake(){motor=GetComponent<FirstPersonMotor>();combat=GetComponent<GauntletCombat>();traversal=GetComponent<CrystalTraversal>();}
        void OnEnable(){if(!combat)combat=GetComponent<GauntletCombat>();combat.AttackStarted+=Attack;}
        void OnDisable(){if(combat)combat.AttackStarted-=Attack;ResetPound();}
        void Attack(int punch){if(punch==4)TryBegin();}
        public bool TryBegin()
        {
            if(Diving||!arena||arena.boss.State!=BossState.Paralyzed||combat.ResonanceRemaining<=0||motor.Body.isGrounded||transform.position.y<arena.tuning.groundPoundMinimumHeight||traversal&&traversal.State!=CrystalTraversal.Mode.Free)return false;
            Diving=true;time=0;motor.ForcedLookPitch=85;motor.Launch(Vector3.down*arena.tuning.groundPoundFallSpeed,.3f);arena.feedback.Sound(UDK7MSound.GroundPoundStart,transform.position,transform);return true;
        }
        void LateUpdate()=>Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if(!Diving)return;time+=dt;if(arena.Won||time>6){ResetPound();return;}
            motor.ForcedLookPitch=85;
            if(time>.06f&&motor.Body.isGrounded){if(transform.position.y<1.5f)arena.GroundImpact(transform.position);ResetPound();}
        }
        public void ResetPound(){Diving=false;time=0;if(motor)motor.ForcedLookPitch=null;}
    }
}
