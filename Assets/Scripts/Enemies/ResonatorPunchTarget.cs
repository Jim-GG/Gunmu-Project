using UnityEngine;
using Resonator.AI;

/// <summary>
/// 让共鸣者接入原有玩家框架的打击接口 <see cref="IPunchReceiver"/>：
/// 冲拳命中转成 <see cref="ResonatorEnemy.TakeDamage"/>。
/// 不需要再挂 <see cref="PunchTargetHealth"/>，敌人血量由 ResonatorTuning 的 maxHealth 管理。
/// </summary>
[RequireComponent(typeof(ResonatorEnemy))]
public class ResonatorPunchTarget : MonoBehaviour, IPunchReceiver
{
    [SerializeField, InspectorName("基础 / 一 / 二 / 三级伤害")]
    private Vector4 damagePerTier = new Vector4(24f, 40f, 65f, 110f);
    [SerializeField, Min(0f), InspectorName("推离力度倍率")] private float forceMultiplier = 1.5f;
    [SerializeField, InspectorName("教学：蓄力拳直接击飞")] private bool tutorialLaunch;

    private ResonatorEnemy enemy;
    private ResonatorEnemy Enemy => enemy != null ? enemy : enemy = GetComponent<ResonatorEnemy>();

    /// <summary>共鸣者按肉体处理：命中顿帧、血液特效、打击音更重。</summary>
    public bool IsFlesh => true;
    public bool OverridesAuthoredDamage => tutorialLaunch;

    public void OnPunched(Vector3 point, Vector3 normal, Vector3 direction, int tier, float strength)
    {
        ResonatorEnemy target = Enemy;
        if (target == null) return;
        if (tutorialLaunch)
        {
            if (tier > 0)
            {
                Vector3 forward = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
                if (forward == Vector3.zero) forward = -target.transform.forward;
                target.DefeatWithLaunch(forward * (10f + tier * 3f) + Vector3.up * (3f + tier * 0.5f));
            }
            return;
        }
        float damage = damagePerTier[Mathf.Clamp(tier, 0, 3)];
        if (damage <= 0f) return;
        Vector3 flat = Vector3.ProjectOnPlane(direction, Vector3.up);
        Vector3 source = flat.sqrMagnitude > 0.0001f ? point - flat.normalized * 0.5f : point - direction * 0.5f;
        target.TakeDamage(damage, source, Mathf.Max(2.5f, strength * forceMultiplier));
    }
}
