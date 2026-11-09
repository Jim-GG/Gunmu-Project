using UnityEngine;

/// <summary>
/// Energy crystal that refills the player's Kinetic Dash charge upon contact and respawns after a short cooldown.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DashCrystal : MonoBehaviour, IPuzzleResettable
{
    [SerializeField, Min(0.1f)] private float respawnCooldown = 1.5f;
    [SerializeField] private float rotateSpeed = 90f;
    [Tooltip("指定充能水晶外观后，只隐藏/恢复这个模型，不会重新显示旧占位块。")]
    [SerializeField] private FloatingChargeCrystal visual;

    private Collider crystalCollider;
    private Renderer[] renderers;
    private float respawnTimer;

    private void Awake()
    {
        crystalCollider = GetComponent<Collider>();
        crystalCollider.isTrigger = true;
        // Legacy scenes can keep their original renderer. Furnished scenes use an explicit visual.
        renderers = visual != null ? null : GetComponentsInChildren<Renderer>();
    }

    private void Update()
    {
        if (respawnTimer > 0f)
        {
            respawnTimer -= Time.deltaTime;
            if (respawnTimer <= 0f)
                SetCrystalActive(true);
            return;
        }

        if (visual == null)
            transform.Rotate(Vector3.up, rotateSpeed * Time.deltaTime, Space.World);
    }

    private void OnTriggerEnter(Collider other)
    {
        if (respawnTimer > 0f)
            return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.TryRechargeDash("Crystal"))
        {
            respawnTimer = respawnCooldown;
            SetCrystalActive(false);
        }
    }

    private void OnTriggerStay(Collider other)
    {
        if (respawnTimer > 0f)
            return;

        PlayerMovement player = other.GetComponent<PlayerMovement>();
        if (player != null && player.TryRechargeDash("Crystal"))
        {
            respawnTimer = respawnCooldown;
            SetCrystalActive(false);
        }
    }

    public void ResetCrystal()
    {
        respawnTimer = 0f;
        SetCrystalActive(true);
        if (visual != null) visual.ResetPose();
    }

    /// <summary>统一重置接口（由 PuzzleResetUtility 调用，等价于 ResetCrystal）。</summary>
    public void ResetPuzzle()
    {
        ResetCrystal();
    }

    private void SetCrystalActive(bool active)
    {
        if (visual != null)
        {
            visual.SetAvailable(active);
            return;
        }
        if ( renderers != null )
        {
            foreach (Renderer r in renderers)
            {
                if (r != null)
                    r.enabled = active;
            }
        }
    }
}
