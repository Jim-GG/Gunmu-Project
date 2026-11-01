using UnityEngine;
using UnityEngine.Events;

// Optional receiver for damage tests. Puzzle boxes do not need a health component.
public class PunchTargetHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 20;
    [SerializeField] private int currentHealth;
    [SerializeField] private UnityEvent onDefeated = new UnityEvent();
    public int CurrentHealth => currentHealth;
    public bool IsDefeated => currentHealth <= 0;
    public void ResetHealth(int value) { maxHealth = Mathf.Max(1, value); currentHealth = maxHealth; }
    private void Awake() => currentHealth = maxHealth;
    public void TakeDamage(int amount)
    {
        if (currentHealth <= 0 || amount <= 0) return;
        currentHealth = Mathf.Max(0, currentHealth - amount);
        Debug.Log($"{name}: HP {currentHealth}/{maxHealth}", this);
        if (currentHealth == 0) onDefeated.Invoke();
    }
}
