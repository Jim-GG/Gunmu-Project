using UnityEngine;

// Health for the combat prototype. Enemy attacks call TakeDamage.
public class PlayerHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 5;
    [SerializeField] private int currentHealth;
    [SerializeField, Min(0f)] private float invulnerabilityDuration = 0.8f;

    private float nextDamageTime;
    private float damageFeedbackUntil;
    private GUIStyle healthStyle;

    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    // Returns true only when this hit actually reduces health.
    public bool TakeDamage(int damage)
    {
        if (damage <= 0 || IsDefeated || Time.time < nextDamageTime)
            return false;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        nextDamageTime = Time.time + invulnerabilityDuration;
        damageFeedbackUntil = Time.time + 0.4f;

        if (IsDefeated)
        {
            // Stop player control when defeated; gravity/collisions need no further updates.
            PlayerMovement movement = GetComponent<PlayerMovement>();
            PlayerLook look = GetComponent<PlayerLook>();
            PlayerGauntlet gauntlet = GetComponent<PlayerGauntlet>();
            if (movement != null) movement.enabled = false;
            if (look != null) look.enabled = false;
            if (gauntlet != null) gauntlet.enabled = false;
            Cursor.lockState = CursorLockMode.None;
            Cursor.visible = true;
        }
        return true;
    }

    private void OnGUI()
    {
        if (healthStyle == null)
        {
            healthStyle = new GUIStyle(GUI.skin.label) { fontSize = 22 };
        }

        Color previousColor = GUI.color;
        GUI.color = Time.time < damageFeedbackUntil ? Color.red : Color.white;
        GUI.Label(new Rect(15f, Screen.height - 50f, 350f, 40f),
            $"Player HP: {currentHealth}/{maxHealth}", healthStyle);
        GUI.color = previousColor;

    }
}
