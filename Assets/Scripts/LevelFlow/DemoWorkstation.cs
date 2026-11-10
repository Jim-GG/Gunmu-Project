using Resonator.AI;
using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public sealed class DemoWorkstation : MonoBehaviour, ILevelExitGate
{
    [SerializeField, InspectorName("本站敌人预制体")] private ResonatorEnemy enemyPrefab;
    [SerializeField, InspectorName("敌人起点")] private Transform enemyStart;
    [SerializeField, InspectorName("玩家")] private ResonatorTarget player;
    [SerializeField, InspectorName("机关指示灯")] private Renderer statusLight;
    [SerializeField, Min(1f), InspectorName("击飞失败后重试时间")] private float retrySeconds = 6f;

    private ResonatorEnemy enemy;
    private float retryAt = float.PositiveInfinity;
    public bool IsSatisfied { get; private set; }

    private void Start()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        SpawnEnemy();
    }

    private void SpawnEnemy()
    {
        if (enemyPrefab == null || enemyStart == null) return;
        enemy = Instantiate(enemyPrefab, enemyStart.position, enemyStart.rotation);
        enemy.target = player;
        enemy.Died += ScheduleRetry;
        retryAt = float.PositiveInfinity;
    }

    private void ScheduleRetry() => retryAt = Time.time + retrySeconds;

    private void Update()
    {
        if (IsSatisfied || GameplayGate.IsBlocked || Time.time < retryAt) return;
        if (enemy != null)
        {
            enemy.gameObject.SetActive(false);
            Destroy(enemy.gameObject);
        }
        SpawnEnemy();
    }

    private void OnTriggerEnter(Collider other) => CheckCorpse(other);
    private void OnTriggerStay(Collider other) => CheckCorpse(other);

    private void CheckCorpse(Collider other)
    {
        if (IsSatisfied || enemy == null || enemy.State != EnemyState.Dead || other.attachedRigidbody == null
            || !other.transform.IsChildOf(enemy.transform)) return;

        IsSatisfied = true;
        if (statusLight != null)
        {
            var block = new MaterialPropertyBlock();
            statusLight.GetPropertyBlock(block);
            Color unlocked = new Color(.24f, 1f, .45f);
            block.SetColor("_BaseColor", unlocked);
            block.SetColor("_Color", unlocked);
            block.SetColor("_EmissionColor", unlocked * .5f);
            statusLight.SetPropertyBlock(block);
        }
    }
}
