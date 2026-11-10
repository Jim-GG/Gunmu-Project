using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 拳套装备状态。挂在 Player 上时：
/// - <see cref="startsEquipped"/> 关闭（默认）：拳套未装备，需要 <see cref="GauntletPickup"/> 拾取；
/// - <see cref="startsEquipped"/> 开启：开局即可出拳、格挡，拳套外观直接显示。
/// 解谜/战斗关卡直接勾上，教学关用它做“先捡拳套”。
/// </summary>
[DefaultExecutionOrder(-200)]
public class PlayerGauntletEquipment : MonoBehaviour
{
    [SerializeField, InspectorName("开局已装备拳套")] private bool startsEquipped;
    [SerializeField, InspectorName("装备时触发")] private UnityEvent onEquipped = new UnityEvent();

    private PlayerChargedPunch punch;
    public bool HasGauntlet { get; private set; }

    private void Awake()
    {
        HasGauntlet = startsEquipped;
        punch = GetComponent<PlayerChargedPunch>();
        if (punch == null) punch = gameObject.AddComponent<PlayerChargedPunch>();
        punch.enabled = HasGauntlet;
    }

    public bool Equip()
    {
        if (HasGauntlet) return false;
        HasGauntlet = true;
        punch.enabled = true;
        onEquipped.Invoke();
        return true;
    }
}
