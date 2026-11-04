/// <summary>
/// 可被按钮触发的机关统一接口。
/// 按钮（PuzzleButton）只认这个接口，因此新增机关不需要改按钮代码。
/// </summary>
public interface IPuzzleTriggerable
{
    /// <summary>现在是否还能被触发（用于按钮外观反馈）。</summary>
    bool CanTrigger { get; }

    /// <summary>执行一次触发。</summary>
    void Trigger();
}
