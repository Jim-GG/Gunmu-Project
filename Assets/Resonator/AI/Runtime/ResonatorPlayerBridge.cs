using UnityEngine;
namespace Resonator.AI
{
    /// <summary>
    /// 玩家 → 共鸣者感知的适配器：只向 AI 广播噪音（脚步声、出拳、蓄力），不改动玩法。
    /// 敌人受伤由 <see cref="ResonatorPunchTarget"/>（IPunchReceiver）负责，
    /// 玩家受伤 / 格挡 / 死亡由 PlayerCombatController 与 GameStateManager 负责。
    /// </summary>
    [RequireComponent(typeof(ResonatorTarget))]
    public sealed class ResonatorPlayerBridge : MonoBehaviour
    {
        [SerializeField, InspectorName("玩家目标组件")] public ResonatorTarget actor;
        [SerializeField, InspectorName("玩家移动组件")] public PlayerMovement movement;
        [SerializeField, InspectorName("玩家拳套组件")] public PlayerChargedPunch punch;

        [Header("噪音半径（米）")]
        public float walkNoise = 7, runNoise = 13, attackNoise = 16, chargeNoise = 9;
        [Header("脚步间距（米）")]
        public float walkStepDistance = 1.3f, runStepDistance = 1.6f;
        [SerializeField, Min(0f), InspectorName("判定为奔跑的速度")] public float runSpeedThreshold = 4.5f;

        private Vector3 previous;
        private float stepDistance;
        private bool subscribed;

        private void Awake()
        {
            if (!actor) actor = GetComponent<ResonatorTarget>();
            if (!movement) movement = GetComponent<PlayerMovement>();
            if (!punch) punch = GetComponent<PlayerChargedPunch>();
            previous = transform.position;
        }

        private void OnEnable() => Subscribe(true);
        private void OnDisable() => Subscribe(false);

        private void Subscribe(bool on)
        {
            if (punch == null || on == subscribed) return;
            subscribed = on;
            if (on)
            {
                punch.PunchReleased += HandlePunch;
                punch.ChargeStarted += HandleCharge;
            }
            else
            {
                punch.PunchReleased -= HandlePunch;
                punch.ChargeStarted -= HandleCharge;
            }
        }

        private void HandlePunch(int tier)
        {
            // 无蓄力快拳噪音小一些，蓄力档位越高越吵。
            float radius = tier <= 0 ? attackNoise * 0.5f : attackNoise;
            ResonatorNoise.Emit(transform.position + Vector3.up, radius, actor);
        }

        private void HandleCharge() => ResonatorNoise.Emit(transform.position + Vector3.up, chargeNoise, actor);

        private void Update()
        {
            Vector3 change = transform.position - previous;
            previous = transform.position;
            change.y = 0f;
            if (actor == null || !actor.Alive) return;
            CharacterController controller = GetComponent<CharacterController>();
            if (controller != null && !controller.isGrounded) return;
            // 瞬移 / 重生不算脚步声。
            if (change.magnitude > 2f) { stepDistance = 0f; return; }
            stepDistance += change.magnitude;
            float speed = movement != null
                ? Vector3.ProjectOnPlane(movement.CurrentVelocity, Vector3.up).magnitude
                : change.magnitude / Mathf.Max(0.0001f, Time.deltaTime);
            bool running = speed >= runSpeedThreshold;
            if (stepDistance < (running ? runStepDistance : walkStepDistance)) return;
            stepDistance = 0f;
            ResonatorNoise.Emit(transform.position + Vector3.up, running ? runNoise : walkNoise, actor);
        }

        /// <summary>供调试 / 剧本事件使用：强制发一次噪音。</summary>
        public void EmitTestNoise(float radius = 20f) => ResonatorNoise.Emit(transform.position + Vector3.up, radius, actor);
    }
}
