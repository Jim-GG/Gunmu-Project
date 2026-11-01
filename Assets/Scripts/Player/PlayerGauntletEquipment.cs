using UnityEngine;
using UnityEngine.Events;

// Add to the tutorial player only. Puzzle-only scenes keep their existing starting ability.
[DefaultExecutionOrder(-200)]
public class PlayerGauntletEquipment : MonoBehaviour
{
    [SerializeField] private bool hasGauntlet;
    [SerializeField] private UnityEvent onEquipped = new UnityEvent();
    private PlayerChargedPunch punch;
    public bool HasGauntlet => hasGauntlet;

    private void Awake()
    {
        hasGauntlet = false;
        punch = GetComponent<PlayerChargedPunch>();
        if (punch == null) punch = gameObject.AddComponent<PlayerChargedPunch>();
        punch.enabled = false;
    }

    public bool Equip()
    {
        if (hasGauntlet) return false;
        hasGauntlet = true;
        punch.enabled = true;
        onEquipped.Invoke();
        return true;
    }
}
