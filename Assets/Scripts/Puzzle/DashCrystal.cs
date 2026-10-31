using UnityEngine;

/// <summary>
/// Energy crystal that refills the player's Kinetic Dash charge upon contact and respawns after a short cooldown.
/// </summary>
[RequireComponent(typeof(Collider))]
public class DashCrystal : MonoBehaviour
{
    [SerializeField, Min(0.1f)] private float respawnCooldown = 1.5f;
    [SerializeField] private float rotateSpeed = 90f;

    private Collider crystalCollider;
    private Renderer[] renderers;
    private float respawnTimer;

    private void Awake()
    {
        crystalCollider = GetComponent<Collider>();
        crystalCollider.isTrigger = true;
        renderers = GetComponentsInChildren<Renderer>();
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
    }

    private void SetCrystalActive(bool active)
    {
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
