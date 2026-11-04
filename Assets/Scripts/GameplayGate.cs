/// <summary>
/// 中立的“玩法是否被拦截”开关：
/// 由 <see cref="GameStateManager"/>（暂停 / 死亡 / 关卡完成）写入，玩法道具只读。
/// 这样可复用的道具代码不需要引用具体的流程/菜单管理类。
/// </summary>
public static class GameplayGate
{
    public static bool IsBlocked { get; set; }
}
