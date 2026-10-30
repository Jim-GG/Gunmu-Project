using UnityEngine;

// Attack prototype: telegraph, check range at impact, then recover.
[RequireComponent(typeof(DemoEnemyHealth))]
public class DemoEnemyAttack : MonoBehaviour
{
    [SerializeField] private PlayerHealth playerTarget;
    [SerializeField, Min(0.1f)] private float attackRange = 2.2f;
    [SerializeField, Min(0.1f)] private float windupDuration = 1f;
    [SerializeField, Min(0.1f)] private float recoveryDuration = 1.5f;
    [SerializeField, Min(1)] private int attackDamage = 1;

    private enum AttackState { Idle, Windup, Recovery }
    private AttackState state;
    private float stateEndTime;
    private DemoEnemyHealth health;
    private Camera playerCamera;
    private GUIStyle warningStyle;

    public PlayerHealth PlayerTarget => playerTarget;
    public bool IsBusy => state != AttackState.Idle;

    private void Start()
    {
        health = GetComponent<DemoEnemyHealth>();
        if (playerTarget == null)
        {
            Debug.LogError("DemoEnemyAttack: assign Player Target in the Inspector.", this);
            enabled = false;
            return;
        }
        playerCamera = playerTarget.GetComponentInChildren<Camera>();
    }

    private void Update()
    {
        if (health.IsDefeated || playerTarget.IsDefeated)
            return;

        switch (state)
        {
            case AttackState.Idle:
                if (CanHitPlayer())
                {
                    state = AttackState.Windup;
                    stateEndTime = Time.time + windupDuration;
                }
                break;

            case AttackState.Windup:
                if (Time.time >= stateEndTime)
                {
                    // Recheck at the hit moment so backing away can avoid the attack.
                    if (CanHitPlayer())
                    {
                        PlayerGauntlet gauntlet = playerTarget.GetComponent<PlayerGauntlet>();
                        bool blocked = gauntlet != null && gauntlet.TryBlockAttack(transform.position);
                        if (!blocked)
                            playerTarget.TakeDamage(attackDamage);
                    }
                    state = AttackState.Recovery;
                    stateEndTime = Time.time + recoveryDuration;
                }
                break;

            case AttackState.Recovery:
                if (Time.time >= stateEndTime)
                    state = AttackState.Idle;
                break;
        }
    }

    // A stored-impact hit cancels any pending attack and gives a recovery window.
    public void InterruptAttack()
    {
        state = AttackState.Recovery;
        stateEndTime = Time.time + recoveryDuration;
    }

    private bool CanHitPlayer()
    {
        Vector3 enemyCenter = transform.position;
        Vector3 playerCenter = playerTarget.transform.position + Vector3.up;
        if (Vector3.Distance(enemyCenter, playerCenter) > attackRange)
            return false;

        // A solid object between the enemy and player must block the hit.
        Vector3 direction = playerCenter - enemyCenter;
        if (direction.sqrMagnitude < 0.0001f)
            return true;
        RaycastHit[] hits = Physics.RaycastAll(enemyCenter, direction.normalized,
            direction.magnitude, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore);
        foreach (RaycastHit hit in hits)
        {
            if (hit.collider.transform.IsChildOf(transform)
                || hit.collider.transform.IsChildOf(playerTarget.transform))
                continue;
            return false;
        }
        return true;
    }

    private void OnGUI()
    {
        if (playerTarget == null || playerCamera == null || playerTarget.IsDefeated)
            return;
        Vector3 labelPosition = playerCamera.WorldToScreenPoint(transform.position + Vector3.up * 1.3f);
        if (labelPosition.z <= 0f)
            return;
        if (warningStyle == null)
            warningStyle = new GUIStyle(GUI.skin.label)
                { fontSize = 22, alignment = TextAnchor.MiddleCenter };

        string label = state == AttackState.Windup ? "ATTACK INCOMING!" :
            state == AttackState.Recovery ? "Recovering..." : "Approaching...";
        Color previousColor = GUI.color;
        GUI.color = state == AttackState.Windup ? Color.yellow : Color.white;
        GUI.Label(new Rect(labelPosition.x - 180f, Screen.height - labelPosition.y - 35f,
            360f, 40f), label, warningStyle);
        GUI.color = previousColor;
    }
}
