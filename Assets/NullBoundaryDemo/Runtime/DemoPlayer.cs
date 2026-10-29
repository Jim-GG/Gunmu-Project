using System;
using UnityEngine;
using UnityEngine.InputSystem;

namespace NullBoundary.Demo
{
    [RequireComponent(typeof(CharacterController))]
    public sealed class DemoPlayer : MonoBehaviour
    {
        public Camera view;
        public Transform hands;
        public float walkSpeed = 3f, runSpeed = 5.4f, lookSensitivity = 0.10f;
        public float health = 100f, charge;
        public bool hasGauntlet, controlEnabled;
        public DemoInteractable Focus { get; private set; }
        public bool Blocking => controlEnabled && hasGauntlet && Mouse.current != null &&
                                Mouse.current.rightButton.isPressed && !DemoFlow.Instance.InputBlocked;
        CharacterController motor;
        float pitch, fallVelocity, nextPunch, flash, handKick, nextStep;
        Vector3 handRest;
        public bool IsAlive => health > 0;
        public static bool FacesAttack(Vector3 forward, Vector3 toAttacker)
        {
            forward.y = toAttacker.y = 0;
            return toAttacker.sqrMagnitude < 0.01f ||
                   Vector3.Dot(forward.normalized, toAttacker.normalized) >= 0.25f;
        }
        void Awake()
        {
            motor = GetComponent<CharacterController>();
            if (hands) handRest = hands.localPosition;
        }
        public void SetControl(bool value)
        {
            controlEnabled = value;
            if (!value) Focus = null;
        }
        public void Teleport(Vector3 position, float yaw = 0)
        {
            if(!motor)motor=GetComponent<CharacterController>();
            motor.enabled = false;
            transform.SetPositionAndRotation(position, Quaternion.Euler(0,yaw,0));
            fallVelocity = 0; pitch = 0;
            view.transform.localPosition = new Vector3(0, 1.65f, 0);
            view.transform.localRotation = Quaternion.identity;
            motor.enabled = true;
        }
        void Update()
        {
            var flow = DemoFlow.Instance;
            if (!flow) return;
            flash = Mathf.MoveTowards(flash,0,Time.unscaledDeltaTime*1.8f);
            handKick = Mathf.MoveTowards(handKick,0,Time.deltaTime*4);
            if (hands)
            {
                hands.gameObject.SetActive(hasGauntlet && controlEnabled);
                hands.localPosition = handRest + new Vector3(0, Blocking ? .13f : 0, handKick*.32f);
                hands.localRotation = Quaternion.Euler(Blocking ? -18 : handKick*-24,0,0);
            }
            if (!controlEnabled || flow.InputBlocked || !IsAlive) { Focus = null; return; }
            var keyboard = Keyboard.current; var mouse = Mouse.current;
            if (keyboard == null || mouse == null) return;
            Vector2 delta = mouse.delta.ReadValue() * lookSensitivity;
            transform.Rotate(0,delta.x,0);
            pitch = Mathf.Clamp(pitch-delta.y,-78,78);
            view.transform.localRotation = Quaternion.Euler(pitch,0,0);
            Vector2 move = new Vector2((keyboard.dKey.isPressed?1:0)-(keyboard.aKey.isPressed?1:0),
                (keyboard.wKey.isPressed?1:0)-(keyboard.sKey.isPressed?1:0));
            move = Vector2.ClampMagnitude(move,1);
            bool sprint = keyboard.leftShiftKey.isPressed && move.y > 0 && !Blocking;
            Vector3 velocity = (transform.right*move.x+transform.forward*move.y)*(sprint?runSpeed:walkSpeed);
            if (motor.isGrounded && fallVelocity < 0) fallVelocity=-2;
            if (motor.isGrounded && keyboard.spaceKey.wasPressedThisFrame) fallVelocity=4.5f;
            fallVelocity += Physics.gravity.y*Time.deltaTime;
            motor.Move((velocity+Vector3.up*fallVelocity)*Time.deltaTime);
            if (move.sqrMagnitude>.1f && motor.isGrounded && Time.time>nextStep)
            {
                nextStep=Time.time+(sprint?.30f:.47f); flow.sound.Step(transform.position);
            }
            Focus = null;
            if (Physics.Raycast(view.transform.position,view.transform.forward,out var hit,3.2f,~0,QueryTriggerInteraction.Ignore))
                Focus=hit.collider.GetComponentInParent<DemoInteractable>();
            if (Focus && keyboard.eKey.wasPressedThisFrame) Focus.Use();
            if (hasGauntlet && mouse.leftButton.wasPressedThisFrame && Time.time>=nextPunch) Punch();
            if (transform.position.y < -8) TakeDamage(200, transform.position+transform.forward);
        }
        public bool TakeDamage(float amount, Vector3 source)
        {
            var flow = DemoFlow.Instance;
            if (!IsAlive || !controlEnabled || !flow || flow.InputBlocked) return false;
            if (Blocking && FacesAttack(view.transform.forward,source-transform.position))
            {
                charge=Mathf.Clamp01(charge+1); handKick=.45f;
                flow.sound.Play(DemoSound.Cue.Block,transform.position);
                flow.Message("冲量已储存 · 左键压缩释放",2.2f);
                return true;
            }
            health=Mathf.Max(0,health-amount); flash=.6f;
            flow.sound.Play(DemoSound.Cue.Hit,transform.position);
            if (health<=0) flow.Fail();
            return false;
        }
        public int Punch()
        {
            if (!hasGauntlet || !IsAlive) return 0;
            bool charged=charge>.1f;
            nextPunch=Time.time+(charged?.75f:.42f);
            handKick=1;
            if (charged) charge=0;
            var flow=DemoFlow.Instance;
            flow.sound.Play(charged?DemoSound.Cue.Release:DemoSound.Cue.Swing,transform.position);
            RaycastHit[] hits=Physics.SphereCastAll(view.transform.position,.48f,view.transform.forward,3.1f,~0,QueryTriggerInteraction.Ignore);
            Array.Sort(hits,(a,b)=>a.distance.CompareTo(b.distance));
            int count=0;
            foreach(var hit in hits)
            {
                if (hit.collider.GetComponentInParent<DemoPlayer>()) continue;
                var enemy=hit.collider.GetComponentInParent<DemoEnemy>();
                if (!enemy) break; // A solid wall occludes the attack.
                if (enemy.IsDown) continue;
                enemy.Hit(charged?110:25,view.transform.forward,charged?3.8f:.45f);
                count++;
                if (!charged) break;
            }
            if (charged) flow.ShowImpact(view.transform.position+view.transform.forward*2);
            return count;
        }
        public float DamageFlash => flash;
    }
}

