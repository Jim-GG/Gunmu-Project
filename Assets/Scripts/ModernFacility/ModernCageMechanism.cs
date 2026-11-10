using UnityEngine;

/// <summary>共鸣者尸体进入右侧压力机关后打开无人机笼门。</summary>
[RequireComponent(typeof(Collider))]
public sealed class ModernCageMechanism : MonoBehaviour
{
    [SerializeField] private GameObject cageDoor;
    [SerializeField] private GameObject cagedDrone;
    [SerializeField] private Renderer indicator;
    [SerializeField] private Color lockedColor = new Color(0.35f, 0.08f, 0.55f);
    [SerializeField] private Color unlockedColor = new Color(0.75f, 0.2f, 1f);

    public bool IsUnlocked { get; private set; }

    private void Reset()
    {
        Collider trigger = GetComponent<Collider>();
        trigger.isTrigger = true;
    }

    private void Start()
    {
        if (cagedDrone) cagedDrone.SetActive(false);
        SetIndicator(lockedColor);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (IsUnlocked) return;
        Resonator.AI.ResonatorEnemy enemy = other.GetComponentInParent<Resonator.AI.ResonatorEnemy>();
        if (!enemy || enemy.State != Resonator.AI.EnemyState.Dead) return;

        IsUnlocked = true;
        if (cageDoor) cageDoor.SetActive(false);
        if (cagedDrone) cagedDrone.SetActive(true);
        SetIndicator(unlockedColor);
        Debug.Log("右侧压力机关已触发：无人机笼门开启。", this);
    }

    private void SetIndicator(Color color)
    {
        if (!indicator) return;
        indicator.material.color = color;
        if (indicator.material.HasProperty("_EmissionColor"))
        {
            indicator.material.EnableKeyword("_EMISSION");
            indicator.material.SetColor("_EmissionColor", color * 1.8f);
        }
    }
}
