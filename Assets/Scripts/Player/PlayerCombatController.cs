using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 6;
    [SerializeField, Range(1f, 180f)] private float guardAngle = 120f;
    [SerializeField, Min(0f)] private float hurtProtection = 0.6f;
    private int health;
    private float nextHurt;
    private float feedbackUntil;
    private string feedback;
    private PlayerChargedPunch punch;
    public bool IsDefeated => health <= 0;
    public int Health => health;
    public bool IsGuarding => !IsDefeated && punch != null && punch.CanGuard
        && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null
        && Mouse.current.rightButton.isPressed;

    private void Awake() { health = maxHealth; }
    private void Start() { punch = GetComponent<PlayerChargedPunch>(); }

    /// <summary>调试 / 检查点用：直接回满生命（不重置其他状态）。</summary>
    public void RestoreHealth()
    {
        health = maxHealth;
        nextHurt = 0f;
    }

    /// <summary>
    /// 敌人命中结算入口。返回 true 表示本次攻击被格挡（已回充能、未掉血）。
    /// </summary>
    public bool ReceiveAttack(Vector3 source, int damage)
    {
        if (IsDefeated || GameplayGate.IsBlocked) return false;
        Vector3 direction = Vector3.ProjectOnPlane(source - transform.position, Vector3.up);
        if (IsGuarding && Vector3.Angle(transform.forward, direction) <= guardAngle * 0.5f)
        {
            punch.RefillEnergyByGuard(transform.position + Vector3.up * 1.0f + transform.forward * 0.6f, -transform.forward);
            feedback = "格挡成功！充能 +1";
            feedbackUntil = Time.unscaledTime + 1.2f;
            return true;
        }
        if (Time.time < nextHurt) return false;
        health = Mathf.Max(0, health - Mathf.Max(0, damage));
        GetComponent<PunchPresentation>()?.GuardFeedback(false);
        nextHurt = Time.time + hurtProtection;
        feedback = "受到攻击！面向敌人并按住右键防御";
        feedbackUntil = Time.unscaledTime + 1.2f;
        if (IsDefeated) GameStateManager.NotifyPlayerDied();
        return false;
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        ChineseGameUI.EnsureFont();
        GUI.Label(new Rect(15f, Screen.height - 65f, 240f, 30f),
            $"生命：{health}/{maxHealth} | {(IsGuarding ? "防御中" : "按住右键防御")}");
    }
}
