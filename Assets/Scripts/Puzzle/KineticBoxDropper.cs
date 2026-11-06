using System.Collections.Generic;
using UnityEngine;

/// <summary>
/// 动能方块运输管道断口：按固定间隔从管口“冒”出带有初速度的动能方块。
/// - 只负责生成 / 回收，不含任何关卡坐标：预制体摆到哪、朝哪，方块就从哪、朝哪冒出来。
/// - 掉到“回收高度”以下、超过最长存活时间、或超过同时存在上限时会自动清掉。
/// - 实现 <see cref="IPuzzleResettable"/>：按 R 复位时清空已经冒出来的方块。
///
/// 常见用法：把 <c>Prefabs/KineticBoxDropper.prefab</c>（末端管段 + 本脚本）拖进场景，
/// 让管口朝向下方，再把 <c>Prefabs/KineticBox.prefab</c> 拖到“动能方块预制体”上即可。
/// </summary>
[DisallowMultipleComponent]
public class KineticBoxDropper : MonoBehaviour, IPuzzleResettable
{
    [Header("生成")]
    [SerializeField, InspectorName("动能方块预制体")] private GameObject boxPrefab;
    [SerializeField, Min(0.2f), InspectorName("生成间隔（秒）")] private float interval = 4.5f;
    [SerializeField, Min(0f), InspectorName("首次生成延迟（秒）")] private float startDelay = 1.2f;
    [SerializeField, InspectorName("初速度（相对管口朝向，m/s）")] private Vector3 initialVelocity = new Vector3(0f, -12f, 0f);
    [SerializeField, InspectorName("出生点相对管口的偏移（本地）")] private Vector3 spawnOffset = new Vector3(0f, 0.4f, 0f);
    [SerializeField, Min(1), InspectorName("同时存在的最大数量")] private int maxAlive = 2;

    [Header("回收")]
    [SerializeField, InspectorName("低于该世界高度自动销毁")] private float despawnBelowY = -25f;
    [SerializeField, Min(0f), InspectorName("单个方块最长存活时间（秒，0 = 不限）")] private float maxLifetime = 30f;

    private struct Entry
    {
        public GameObject box;
        public float bornAt;
    }

    private readonly List<Entry> alive = new List<Entry>();
    private float timer;

    /// <summary>当前还活着的方块数量（调试 / 表现层可读）。</summary>
    public int AliveCount => alive.Count;

    private void OnEnable()
    {
        timer = Mathf.Max(0f, interval - startDelay);
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked)
            return;

        Cleanup();

        timer += Time.deltaTime;
        if (timer < interval)
            return;

        timer = 0f;
        if (alive.Count >= maxAlive)
            return;

        Spawn();
    }

    private void Spawn()
    {
        if (boxPrefab == null)
        {
            Debug.LogWarning("[KineticBoxDropper] 未指定“动能方块预制体”，不会冒出方块。", this);
            return;
        }

        Vector3 position = transform.TransformPoint(spawnOffset);
        GameObject instance = Instantiate(boxPrefab, position, Quaternion.identity);
        instance.name = boxPrefab.name + "_Dropped";

        Rigidbody body = instance.GetComponent<Rigidbody>();
        if (body != null)
        {
            body.linearVelocity = transform.TransformDirection(initialVelocity);
            body.angularVelocity = Vector3.zero;
        }

        alive.Add(new Entry { box = instance, bornAt = Time.time });
    }

    private void Cleanup()
    {
        for (int i = alive.Count - 1; i >= 0; i--)
        {
            GameObject box = alive[i].box;
            bool gone = box == null;
            if (!gone && box.transform.position.y < despawnBelowY)
                gone = true;
            if (!gone && maxLifetime > 0f && Time.time - alive[i].bornAt > maxLifetime)
                gone = true;

            if (!gone)
                continue;

            if (box != null)
                Destroy(box);
            alive.RemoveAt(i);
        }
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用）：清空已经冒出来的方块。</summary>
    public void ResetPuzzle()
    {
        for (int i = 0; i < alive.Count; i++)
            if (alive[i].box != null)
                Destroy(alive[i].box);

        alive.Clear();
        timer = 0f;
    }
}
