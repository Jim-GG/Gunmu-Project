using UnityEngine;

/// <summary>
/// 统一的重置接口：关卡里所有"可以按 R 复位"的机关都实现它，
/// 这样玩家脚本就不需要逐个类型写死重置逻辑，新增道具也不用改玩家。
/// </summary>
public interface IPuzzleResettable
{
    void ResetPuzzle();
}

/// <summary>
/// 遍历场景里所有实现了 <see cref="IPuzzleResettable"/> 的组件并复位。
/// </summary>
public static class PuzzleResetUtility
{
    public static void ResetAll()
    {
        MonoBehaviour[] behaviours = Object.FindObjectsByType<MonoBehaviour>(FindObjectsSortMode.None);
        for (int i = 0; i < behaviours.Length; i++)
        {
            if (behaviours[i] is IPuzzleResettable resettable)
                resettable.ResetPuzzle();
        }
    }
}
