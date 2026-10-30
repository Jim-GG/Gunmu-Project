using System;
using UnityEngine;

// Minimal health for the single-room combat prototype.
public class DemoEnemyHealth : MonoBehaviour
{
    [SerializeField, Min(1)] private int maxHealth = 3;
    [SerializeField] private int currentHealth;

    public int CurrentHealth => currentHealth;
    public int MaxHealth => maxHealth;
    public bool IsDefeated => currentHealth <= 0;
    public event Action<DemoEnemyHealth> Defeated;

    private void Awake()
    {
        currentHealth = Mathf.Max(1, maxHealth);
    }

    public void TakeDamage(int damage)
    {
        // Repeated hits after defeat must not trigger completion again.
        if (damage <= 0 || IsDefeated)
            return;

        currentHealth = Mathf.Max(0, currentHealth - damage);
        Debug.Log($"{name}: HP {currentHealth}/{maxHealth}", this);

        if (IsDefeated)
        {
            Debug.Log($"{name}: defeated.", this);
            Defeated?.Invoke(this);
            gameObject.SetActive(false);
        }
    }
}
