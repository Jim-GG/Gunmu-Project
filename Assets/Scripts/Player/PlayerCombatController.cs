using UnityEngine;
using UnityEngine.InputSystem;

public class PlayerCombatController : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 6;
    [SerializeField, Range(1f, 180f)] private float guardAngle = 120f;
    [SerializeField, Min(0f)] private float hurtProtection = 0.6f;
    private int health;
    private float nextHurt;
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
            return true;
        }
        if (Time.time < nextHurt) return false;
        health = Mathf.Max(0, health - Mathf.Max(0, damage));
        GetComponent<PunchPresentation>()?.GuardFeedback(false);
        nextHurt = Time.time + hurtProtection;
        if (IsDefeated) GameStateManager.NotifyPlayerDied();
        return false;
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        Color old = GUI.color;
        Rect track = new Rect(20f, Screen.height - 33f, 110f, 7f);
        GUI.color = new Color(.19f, .14f, .18f);
        GUI.DrawTexture(track, Texture2D.whiteTexture);
        GUI.color = new Color(.8f, .24f, .29f);
        GUI.DrawTexture(new Rect(track.x, track.y, track.width * health / maxHealth, track.height), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
