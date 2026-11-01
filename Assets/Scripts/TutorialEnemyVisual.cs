using UnityEngine;

public class TutorialEnemyVisual : MonoBehaviour
{
    [SerializeField, InspectorName("敌人外观预制体")] private GameObject visualPrefab;
    private Transform visual, armLeft, armRight;
    private TutorialEnemy enemy;
    private float hitUntil;
    private float freezeUntil;
    public bool IsHitStopped => Time.unscaledTime < freezeUntil;
    private void Start()
    {
        enemy = GetComponent<TutorialEnemy>();
        if (visualPrefab == null) visualPrefab = Resources.Load<GameObject>("DemoPresentation/ResonatorVisual");
        if (visualPrefab == null) return;
        visual = Instantiate(visualPrefab, transform).transform;
        armLeft = visual.Find("左臂轴"); armRight = visual.Find("右臂轴");
        Renderer original = GetComponent<Renderer>(); if (original != null) original.enabled = false;
    }
    public void Hit(int tier) { hitUntil = Time.unscaledTime + .22f; freezeUntil = Time.unscaledTime + .045f + tier * .025f; }
    private void LateUpdate()
    {
        if (visual == null || enemy == null || IsHitStopped) return;
        float wobble = Mathf.Sin(Time.time * 7f);
        float flinch = Mathf.Clamp01((hitUntil - Time.unscaledTime) / .22f);
        visual.localRotation = Quaternion.Euler(enemy.IsDefeated ? -68f : -flinch * 18f, 0f, enemy.IsDefeated ? 25f : wobble * 2f);
        if (armLeft != null) armLeft.localRotation = Quaternion.Euler(enemy.IsWindingUp ? -100f : wobble * 15f - flinch * 30f, 0f, -8f);
        if (armRight != null) armRight.localRotation = Quaternion.Euler(enemy.IsWindingUp ? -115f : -wobble * 15f - flinch * 30f, 0f, 8f);
    }
}
