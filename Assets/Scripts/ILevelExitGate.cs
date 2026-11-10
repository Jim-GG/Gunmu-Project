/// <summary>
/// 关卡出口的放行条件：任何组件实现它，就能作为 <see cref="LevelExit"/> 的前置门禁。
/// 例如 <see cref="PuzzleSwitch"/>（解谜完成）与 <see cref="ResonatorEncounter"/>（战斗清场）。
/// 出口只读这个接口，所以新增"解锁条件"不需要改出口代码。
/// </summary>
public interface ILevelExitGate
{
    bool IsSatisfied { get; }
}
