using UnityEngine;
using UnityEngine.Events;
using Resonator.AI;

/// <summary>
/// 共鸣者战斗遭遇：逐个敌人出场，全部清场后开门。
/// 敌人预制体、出生点、数量、节奏全部在 Inspector 配置，脚本不含写死的坐标。
/// 玩家死亡与重生沿用 <see cref="GameStateManager"/> + <see cref="RoomRestartController"/>。
/// </summary>
public class ResonatorEncounter : MonoBehaviour, ILevelExitGate
{
    [Header("引用")]
    [SerializeField, InspectorName("玩家战斗组件")] private PlayerCombatController player;
    [SerializeField, InspectorName("玩家拳套装备组件（可留空）")] private PlayerGauntletEquipment equipment;
    [SerializeField, InspectorName("敌人预制体（ResonatorAgent）")] private GameObject enemyPrefab;
    [SerializeField, InspectorName("出生点（按顺序循环使用）")] private Transform[] spawnPoints = new Transform[0];
    [SerializeField, InspectorName("清场后消失的门（可留空）")] private GameObject exitDoor;

    [Header("节奏")]
    [SerializeField, Min(1), InspectorName("敌人数量")] private int enemyCount = 3;
    [SerializeField, Min(0f), InspectorName("开场等待（秒）")] private float startDelay = 1f;
    [SerializeField, Min(0f), InspectorName("逐个出场间隔（秒）")] private float spawnInterval = 1.5f;

    [Header("事件")]
    [SerializeField, InspectorName("清场时")] private UnityEvent onCleared = new UnityEvent();

    private ResonatorEnemy current;
    private int spawned;
    private float nextSpawn;
    private bool ready;

    public bool IsCleared { get; private set; }
    public int Spawned => spawned;

    /// <summary>ILevelExitGate：全部敌人清场后才放行。</summary>
    public bool IsSatisfied => IsCleared;

    private void Start()
    {
        var missing = new System.Collections.Generic.List<string>();
        if (player == null) missing.Add("Player（玩家战斗组件）");
        if (enemyPrefab == null) missing.Add("Enemy Prefab（敌人预制体）");
        else if (!enemyPrefab.GetComponent<ResonatorEnemy>()) missing.Add("敌人预制体上的 ResonatorEnemy 组件");
        if (spawnPoints == null || spawnPoints.Length == 0) missing.Add("Spawn Points（至少一个出生点）");
        else if (System.Array.Exists(spawnPoints, point => point == null)) missing.Add("出生点中存在空引用");
        if (missing.Count > 0)
        {
            Debug.LogError("战斗遭遇缺少配置：" + string.Join("、", missing) + "。请停止运行后拖入引用并保存场景。", this);
            return;
        }
        if (exitDoor != null) exitDoor.SetActive(true);
        nextSpawn = Time.time + startDelay;
        ready = true;
    }

    private void Update()
    {
        if (!ready || IsCleared || GameplayGate.IsBlocked) return;
        if (player != null && player.IsDefeated) return;
        if (equipment != null && !equipment.HasGauntlet) return;

        if (current != null && IsDead(current))
        {
            current = null;
            nextSpawn = Time.time + spawnInterval;
            if (spawned >= enemyCount) Clear();
            return;
        }

        if (current == null && spawned < enemyCount && Time.time >= nextSpawn) SpawnNext();
    }

    private static bool IsDead(ResonatorEnemy enemy) =>
        enemy == null || !enemy.isActiveAndEnabled || enemy.State == EnemyState.Dead;

    private void SpawnNext()
    {
        Transform point = spawnPoints[spawned % spawnPoints.Length];
        GameObject instance = Instantiate(enemyPrefab, point.position, point.rotation);
        instance.name = $"{enemyPrefab.name}_{spawned + 1}";
        current = instance.GetComponent<ResonatorEnemy>();
        if (current == null)
        {
            Debug.LogError("敌人预制体上没有 ResonatorEnemy 组件。", this);
            Destroy(instance);
            ready = false;
            return;
        }
        if (!instance.GetComponent<ResonatorPunchTarget>()) instance.AddComponent<ResonatorPunchTarget>();
        spawned++;
    }

    private void Clear()
    {
        IsCleared = true;
        if (exitDoor != null) exitDoor.SetActive(false);
        onCleared.Invoke();
    }
}
