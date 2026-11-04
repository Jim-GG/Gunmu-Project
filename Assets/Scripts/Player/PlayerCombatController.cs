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

    public void ReceiveAttack(Vector3 source, int damage)
    {
        if (IsDefeated || DemoSession.IsGameplayBlocked) return;
        Vector3 direction = Vector3.ProjectOnPlane(source - transform.position, Vector3.up);
        if (IsGuarding && Vector3.Angle(transform.forward, direction) <= guardAngle * 0.5f)
        {
            punch.RefillEnergyByGuard(transform.position + Vector3.up * 1.0f + transform.forward * 0.6f, -transform.forward);
            feedback = "格挡成功！蓄力已回满";
            feedbackUntil = Time.unscaledTime + 1.2f;
            return;
        }
        if (Time.time < nextHurt) return;
        health = Mathf.Max(0, health - Mathf.Max(0, damage));
        GetComponent<PunchPresentation>()?.GuardFeedback(false);
        nextHurt = Time.time + hurtProtection;
        feedback = "受到攻击！面向敌人并按住右键防御";
        feedbackUntil = Time.unscaledTime + 1.2f;
        if (IsDefeated && DemoSession.Instance != null) DemoSession.Instance.ShowDeath();
    }

    private void OnGUI()
    {
        if (DemoSession.IsGameplayBlocked) return;
        ChineseGameUI.EnsureFont();
        GUI.Label(new Rect(15f, Screen.height - 65f, 240f, 30f),
            $"生命：{health}/{maxHealth} | {(IsGuarding ? "防御中" : "按住右键防御")}");
        if (Time.unscaledTime < feedbackUntil)
            GUI.Label(new Rect(15f, Screen.height - 195f, Screen.width - 30f, 30f), feedback);
    }
}
