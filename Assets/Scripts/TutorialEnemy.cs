using UnityEngine;

[RequireComponent(typeof(CharacterController), typeof(PunchTargetHealth))]
public class TutorialEnemy : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 6;
    [SerializeField, Min(0f)] private float moveSpeed = 2.2f;
    [SerializeField, Min(0.1f)] private float attackRange = 2.1f;
    [SerializeField, Min(0.1f)] private float attackWindup = 1.1f;
    [SerializeField, Min(0.1f)] private float recoveryTime = 1.5f;
    [SerializeField, Min(1)] private int attackDamage = 1;
    [SerializeField, InspectorName("一至三级击飞向上速度")] private Vector3 launchUpSpeeds = new Vector3(3.2f, 5f, 6.5f);
    private enum State { Chase, Windup, Recovery }
    private State state;
    private float stateEnd;
    private float hideAfter;
    private float verticalSpeed;
    private Vector3 knockback;
    private CharacterController controller;
    private PunchTargetHealth health;
    private PlayerCombatController player;
    private Camera view;
    public bool IsWindingUp => state == State.Windup;
    private TutorialEnemyVisual presentation;
    public bool IsDefeated => health != null && health.IsDefeated;

    public void Initialize(PlayerCombatController target)
    {
        player = target;
        view = target.GetComponentInChildren<Camera>();
    }

    private void Awake()
    {
        presentation = GetComponent<TutorialEnemyVisual>();
        if (presentation == null) presentation = gameObject.AddComponent<TutorialEnemyVisual>();
        controller = GetComponent<CharacterController>();
        controller.center = Vector3.zero;
        controller.height = 2f;
        controller.radius = 0.45f;
        controller.skinWidth = 0.05f;
        CapsuleCollider original = GetComponent<CapsuleCollider>();
        if (original != null) original.enabled = false;
        health = GetComponent<PunchTargetHealth>();
        health.ResetHealth(maxHealth);
    }

    public void OnPunched(int tier, Vector3 direction, float strength)
    {
        if (presentation != null) presentation.Hit(tier);
        if (tier > 0)
        {
            Vector3 horizontal = Vector3.ProjectOnPlane(direction, Vector3.up).normalized;
            knockback = horizontal * strength;
            verticalSpeed = tier == 3 ? launchUpSpeeds.z : tier == 2 ? launchUpSpeeds.y : launchUpSpeeds.x;
            state = State.Recovery;
            stateEnd = Time.time + 0.25f + tier * 0.2f;
        }
        if (IsDefeated) hideAfter = Time.time + 1.2f;
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked) return;
        if (player == null || !controller.enabled || (presentation != null && presentation.IsHitStopped)) return;
        Vector3 walking = Vector3.zero;
        if (IsDefeated)
        {
            if (Time.time >= hideAfter) { gameObject.SetActive(false); return; }
        }
        else if (!player.IsDefeated)
        {
            Vector3 delta = Vector3.ProjectOnPlane(player.transform.position - transform.position, Vector3.up);
            if (state == State.Chase)
            {
                if (InAttackRange()) { state = State.Windup; stateEnd = Time.time + attackWindup; }
                else if (delta.magnitude > 1.6f)
                {
                    walking = delta.normalized * moveSpeed;
                    transform.rotation = Quaternion.LookRotation(delta.normalized, Vector3.up);
                }
            }
            else if (state == State.Windup && Time.time >= stateEnd)
            {
                if (InAttackRange()) player.ReceiveAttack(transform.position, attackDamage);
                state = State.Recovery;
                stateEnd = Time.time + recoveryTime;
            }
            else if (state == State.Recovery && Time.time >= stateEnd) state = State.Chase;
        }
        if (controller.isGrounded && verticalSpeed < 0f) verticalSpeed = -2f;
        verticalSpeed -= 20f * Time.deltaTime;
        controller.Move((walking + knockback + Vector3.up * verticalSpeed) * Time.deltaTime);
        knockback = Vector3.MoveTowards(knockback, Vector3.zero, 16f * Time.deltaTime);
    }

    private bool InAttackRange()
    {
        Vector3 center = player.transform.position + Vector3.up;
        Vector3 delta = center - transform.position;
        if (delta.magnitude > attackRange) return false;
        foreach (RaycastHit hit in Physics.RaycastAll(transform.position, delta.normalized, delta.magnitude,
            Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            if (!hit.collider.transform.IsChildOf(transform)
                && !hit.collider.transform.IsChildOf(player.transform)) return false;
        }
        return true;
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        if (view == null || IsDefeated) return;
        Vector3 p = view.WorldToScreenPoint(transform.position + Vector3.up * 1.25f);
        if (p.z <= 0f) return;
        GUIStyle text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 18 };
        Color old = GUI.color;
        GUI.color = state == State.Windup ? Color.yellow : Color.white;
        GUI.Label(new Rect(p.x - 150f, Screen.height - p.y - 30f, 300f, 50f),
            $"生命：{health.CurrentHealth}/{maxHealth}\n" + (state == State.Windup ? "即将攻击！按住右键防御" : (state == State.Chase ? "接近中" : "攻击恢复中")), text);
        GUI.color = old;
    }
}
