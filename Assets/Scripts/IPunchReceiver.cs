using UnityEngine;

/// <summary>
/// 可被机械拳套命中的目标（敌人 / 可破坏物）统一接口。
/// 玩家侧只认这个接口，所以新增敌人或可击打物体时不需要修改玩家战斗代码。
/// </summary>
public interface IPunchReceiver
{
    /// <summary>是否按“肉体”处理表现（影响打击特效与顿帧时长）。</summary>
    bool IsFlesh { get; }

    /// <summary>
    /// 被冲拳命中。<paramref name="tier"/> 0 表示没有蓄力的接触（例如冲撞擦到），
    /// 1~3 对应一至三段蓄力。
    /// </summary>
    void OnPunched(Vector3 point, Vector3 normal, Vector3 direction, int tier, float strength);
}
