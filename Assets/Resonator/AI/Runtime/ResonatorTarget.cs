using System;
using UnityEngine;

namespace Resonator.AI
{
    public readonly struct ResonatorNoiseEvent
    {
        public readonly Vector3 position; public readonly float radius; public readonly ResonatorTarget source;
        public ResonatorNoiseEvent(Vector3 position, float radius, ResonatorTarget source) { this.position = position; this.radius = radius; this.source = source; }
    }
    public static class ResonatorNoise
    {
        public static event Action<ResonatorNoiseEvent> Emitted;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Clear() => Emitted = null;
        public static void Emit(Vector3 position, float radius, ResonatorTarget source) { if (radius > 0 && source) Emitted?.Invoke(new ResonatorNoiseEvent(position, radius, source)); }
    }
    public enum StrikeResult { Miss, Blocked, Damaged }

    /// <summary>
    /// 共鸣者的“玩家目标”适配器：把敌人攻击交给原有框架的 <see cref="PlayerCombatController"/> 结算，
    /// 格挡、充能回满、死亡与 <see cref="GameStateManager"/> 都由原有流程负责。
    /// </summary>
    public sealed class ResonatorTarget : MonoBehaviour
    {
        [SerializeField, InspectorName("观察点（留空 = 相机）")] public Transform eyes;
        [SerializeField, InspectorName("玩家战斗组件")] public PlayerCombatController combat;
        [SerializeField, InspectorName("测试用无敌")] public bool invulnerableForTesting;

        public bool Alive => invulnerableForTesting || combat == null || !combat.IsDefeated;
        public Vector3 EyePosition => eyes ? eyes.position : transform.position + Vector3.up * 1.6f;
        public Vector3 ChestPosition => transform.position + Vector3.up * 1.05f;

        private void Awake()
        {
            if (!combat) combat = GetComponent<PlayerCombatController>();
            if (!eyes)
            {
                Camera view = GetComponentInChildren<Camera>();
                if (view) eyes = view.transform;
            }
        }

        public StrikeResult ReceiveStrike(float damage, Vector3 source)
        {
            if (!Alive) return StrikeResult.Miss;
            if (invulnerableForTesting) return StrikeResult.Damaged;
            if (combat == null) return StrikeResult.Miss;
            bool blocked = combat.ReceiveAttack(source, Mathf.Max(1, Mathf.RoundToInt(damage)));
            return blocked ? StrikeResult.Blocked : StrikeResult.Damaged;
        }
    }
}
