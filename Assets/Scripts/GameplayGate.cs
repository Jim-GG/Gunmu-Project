/// <summary>
/// 中立的"玩法是否被拦截"开关：
/// 由流程/菜单层（开始界面、暂停、死亡、通关、切场景）写入，玩法道具只读。
/// 这样可复用的道具代码不需要引用具体的流程管理类。
/// </summary>
public static class GameplayGate
{
    public static bool IsBlocked { get; set; }
}
