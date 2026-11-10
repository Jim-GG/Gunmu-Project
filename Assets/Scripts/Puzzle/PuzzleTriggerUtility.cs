using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 按钮 / 开关等“触发源”共用的目标解析与派发工具。
/// 目标只需实现 <see cref="IPuzzleTriggerable"/>，触发源代码不需要认识具体机关类型。
/// </summary>
public static class PuzzleTriggerUtility
{
    /// <summary>把 Inspector 里的引用过滤成可触发目标列表，非法项给出警告。</summary>
    public static void Collect(MonoBehaviour[] targets, List<IPuzzleTriggerable> result, Object context)
    {
        result.Clear();
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            MonoBehaviour target = targets[i];
            if (target == null)
                continue;

            if (target is IPuzzleTriggerable triggerable)
            {
                result.Add(triggerable);
                continue;
            }

            string owner = context != null ? context.GetType().Name : "PuzzleTrigger";
            Debug.LogWarning($"[{owner}] '{target.name}' 没有实现 IPuzzleTriggerable，不会被触发。", context);
        }
    }

    /// <summary>依次触发所有目标。</summary>
    public static void TriggerAll(List<IPuzzleTriggerable> targets)
    {
        if (targets == null)
            return;

        for (int i = 0; i < targets.Count; i++)
            targets[i].Trigger();
    }

    /// <summary>是否有任一目标处于“可触发”状态（没有目标时视为可触发）。</summary>
    public static bool AnyCanTrigger(List<IPuzzleTriggerable> targets)
    {
        if (targets == null || targets.Count == 0)
            return true;

        for (int i = 0; i < targets.Count; i++)
            if (targets[i].CanTrigger)
                return true;

        return false;
    }
}
