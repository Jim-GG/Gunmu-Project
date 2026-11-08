using UnityEngine;

namespace FirstPerson
{
    // A stationary, reusable training attacker. One impact per telegraphed swing.
    public sealed class ParryTrainingEnemy : MonoBehaviour
    {
        public enum AttackPhase { Waiting, Windup, Recovery }
        public GauntletCombat target;
        public FirstPersonVitals vitals;
        public Renderer body;
        public Transform strikingArm;
        public float range = 3.2f, damage = 10, windupSeconds = .9f, recoverySeconds = 1.2f;
        public AttackPhase State { get; private set; }
        public int AttacksLanded { get; private set; }
        public int AttacksBlocked { get; private set; }
        public string LastResult { get; private set; } = "Approach and hold RMB";
        private float timer;
        private MaterialPropertyBlock properties;
        private static readonly int ColorId = Shader.PropertyToID("_BaseColor");
        private void Update() => Tick(Time.deltaTime);
        public void Tick(float dt)
        {
            if (!target || !vitals || vitals.Health <= 0) return;
            float distance = Vector3.Distance(target.transform.position, transform.position);
            if (distance > range) { State = AttackPhase.Waiting; timer = 0; Paint(Color.gray); return; }
            var facing = target.transform.position - transform.position; facing.y = 0;
            if (facing.sqrMagnitude > .001f) transform.rotation = Quaternion.LookRotation(facing);
            timer += Mathf.Max(0, dt);
            if (State == AttackPhase.Waiting) { State = AttackPhase.Windup; timer = 0; }
            if (State == AttackPhase.Windup)
            {
                Paint(Color.Lerp(new Color(.45f,.15f,.05f), Color.yellow, timer / windupSeconds));
                if (strikingArm) strikingArm.localRotation = Quaternion.Euler(-70 * Mathf.Clamp01(timer / windupSeconds), 0, 0);
                if (timer >= windupSeconds) { Strike(); State = AttackPhase.Recovery; timer = 0; }
            }
            else if (State == AttackPhase.Recovery)
            {
                if (strikingArm) strikingArm.localRotation = Quaternion.Euler(45 * (1 - Mathf.Clamp01(timer / recoverySeconds)), 0, 0);
                if (timer >= recoverySeconds) { State = AttackPhase.Windup; timer = 0; }
            }
        }
        public bool Strike()
        {
            if (!target || !vitals || vitals.Health <= 0 || Vector3.Distance(target.transform.position, transform.position) > range) return false;
            Vector3 from = transform.position + Vector3.up * 1.2f, to = target.transform.position + Vector3.up * 1.2f;
            if (Physics.Linecast(from, to, out var obstruction, ~0, QueryTriggerInteraction.Ignore) && !obstruction.transform.IsChildOf(target.transform))
                return false;
            AttacksLanded++;
            var guard=target.ResolveGuard(transform.position);bool blocked=guard!=GameActions.GuardKind.None;
            float applied=damage*(guard==GameActions.GuardKind.Perfect?0:guard==GameActions.GuardKind.Imperfect?.34f:1);if(applied>0)vitals.ReceiveDamage(applied);
            if (blocked) { AttacksBlocked++; LastResult = guard==GameActions.GuardKind.Perfect?"PERFECT +1 energy":"GUARD -"+applied.ToString("0.0")+" HP +1 energy"; Paint(Color.cyan); }
            else { LastResult = "HIT -" + damage + " HP"; Paint(Color.red); }
            return blocked;
        }
        private void Paint(Color color)
        {
            if (!body) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            body.GetPropertyBlock(properties); properties.SetColor(ColorId, color); body.SetPropertyBlock(properties);
        }
    }
}
