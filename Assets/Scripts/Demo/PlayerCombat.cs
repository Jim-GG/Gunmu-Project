using System;
using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>Player health, impulse gauntlet block/charge and shockwave combat.</summary>
public class PlayerCombat : MonoBehaviour
{
    [Header("Health")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(1f)] private float maxEnergy = 100f;

    [Header("Gauntlet")]
    [SerializeField, Min(1f)] private float blockEnergyGain = 50f;
    [SerializeField, Min(0f)] private float shockMinEnergy = 25f;
    [SerializeField, Min(0f)] private float shockRadius = 7f;
    [SerializeField, Min(0f)] private float shockBaseDamage = 20f;
    [SerializeField, Min(0f)] private float shockMaxBonus = 90f;
    [SerializeField, Min(0.05f)] private float blockFacingDot = 0.1f;

    [Header("Punch")]
    [SerializeField, Min(0f)] private float punchDamage = 10f;
    [SerializeField, Min(0.5f)] private float punchRange = 2.7f;
    [SerializeField, Min(0.1f)] private float punchCooldown = 0.45f;
    [SerializeField, Min(0.1f)] private float attackCooldown = 0.35f;

    [Header("Input")]
    [Tooltip("Used by the automated diagnostic. Normal gameplay should leave this enabled.")]
    public bool usePlayerInput = true;

    public event Action<float> BlockSucceeded;
    public event Action<float> ShockReleased;
    public event Action<float> Damaged;
    public event Action PunchPerformed;

    public float Health { get; private set; }
    public float Energy { get; private set; }
    public float MaxHealth => maxHealth;
    public float MaxEnergy => maxEnergy;
    public bool IsAlive => !isDead;
    public bool IsBlocking => blockHeld && IsAlive && IsGamePlaying;
    public int BlockCount { get; private set; }
    public int ShockCount { get; private set; }
    public bool IsAtFullCharge => Energy >= maxEnergy - 0.01f;

    private bool isDead;
    private bool blockHeld;
    private float nextAttackTime;
    private CharacterController controller;
    private Camera playerCamera;
    private GauntletView gauntlet;
    private DemoAudio audio;
    private Vector3 cameraBasePosition;
    private float cameraShake;

    private static bool IsGamePlaying =>
        DemoManager.Instance == null || DemoManager.Instance.IsPlaying;

    private void Awake()
    {
        controller = GetComponent<CharacterController>();
        playerCamera = GetComponentInChildren<Camera>();
        Health = maxHealth;
        Energy = 0f;

        if (playerCamera != null)
        {
            cameraBasePosition = playerCamera.transform.localPosition;
        }
    }

    private void Start()
    {
        audio = DemoManager.Instance != null ? DemoManager.Instance.Audio : GetComponent<DemoAudio>();

        if (playerCamera != null)
        {
            gauntlet = GauntletView.Create(playerCamera.transform);
        }
    }

    private void Update()
    {
        if (!IsGamePlaying || isDead)
        {
            blockHeld = false;
            if (gauntlet != null)
            {
                gauntlet.SetBlocking(false);
                gauntlet.SetVisible(false);
            }
            return;
        }

        if (usePlayerInput && Cursor.lockState == CursorLockMode.Locked && Mouse.current != null)
        {
            blockHeld = Mouse.current.rightButton.isPressed;

            if (Mouse.current.leftButton.wasPressedThisFrame)
            {
                RequestAttack();
            }
        }
        else if (usePlayerInput)
        {
            blockHeld = false;
        }

        if (gauntlet != null)
        {
            gauntlet.SetVisible(true);
            gauntlet.SetBlocking(IsBlocking);
            gauntlet.SetEnergyRatio(Energy / maxEnergy);
        }
    }

    private void LateUpdate()
    {
        if (playerCamera == null)
        {
            return;
        }

        cameraShake = Mathf.MoveTowards(cameraShake, 0f, Time.deltaTime * 0.35f);
        Vector3 offset = cameraShake > 0f
            ? UnityEngine.Random.insideUnitSphere * cameraShake
            : Vector3.zero;
        playerCamera.transform.localPosition = cameraBasePosition + offset;
    }

    /// <summary>Force the block state for automated validation.</summary>
    public void SetBlockHeld(bool held)
    {
        if (!IsAlive || !IsGamePlaying)
        {
            held = false;
        }

        blockHeld = held;
    }

    /// <summary>Trigger the current equipped attack. Used by input and by diagnostics.</summary>
    public void RequestAttack()
    {
        if (!IsAlive || !IsGamePlaying || Time.time < nextAttackTime)
        {
            return;
        }

        if (Energy >= shockMinEnergy)
        {
            ReleaseShock();
        }
        else
        {
            PerformPunch();
        }
    }

    /// <summary>Called by the enemy's impact. Returns true when the hit was blocked.</summary>
    public bool ReceiveAttack(Vector3 attackerPosition, float damage)
    {
        if (!IsAlive || !IsGamePlaying)
        {
            return false;
        }

        Vector3 toAttacker = attackerPosition - transform.position;
        toAttacker.y = 0f;
        Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
        forward.y = 0f;

        bool facingAttacker = Vector3.Dot(forward.normalized, toAttacker.normalized) >= blockFacingDot;

        if (IsBlocking && facingAttacker)
        {
            float before = Energy;
            Energy = Mathf.Min(maxEnergy, Energy + blockEnergyGain);
            float gained = Energy - before;
            BlockCount++;

            gauntlet?.PlayBlock();
            audio?.PlayBlock();
            AddCameraShake(0.022f);
            BlockSucceeded?.Invoke(gained);
            return true;
        }

        Health = Mathf.Max(0f, Health - damage);
        gauntlet?.SetBlocking(false);
        AddCameraShake(0.055f);
        audio?.PlayPlayerHit();
        Damaged?.Invoke(damage);

        if (Health <= 0f)
        {
            isDead = true;
            blockHeld = false;
            DemoManager.Instance?.NotifyPlayerDied();
        }

        return false;
    }

    private void PerformPunch()
    {
        nextAttackTime = Time.time + punchCooldown;
        gauntlet?.PlayPunch();
        PunchPerformed?.Invoke();

        CrystalEnemy enemy = DemoManager.Instance != null
            ? DemoManager.Instance.Enemy
            : FindFirstObjectByType<CrystalEnemy>();

        bool hit = false;
        if (enemy != null && enemy.IsAlive)
        {
            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            Vector3 forward = playerCamera != null ? playerCamera.transform.forward : transform.forward;
            forward.y = 0f;

            if (toEnemy.magnitude <= punchRange
                && Vector3.Dot(forward.normalized, toEnemy.normalized) >= 0.25f)
            {
                enemy.TakeDamage(punchDamage, transform.position, 1.2f, false);
                hit = true;
                AddCameraShake(0.018f);
            }
        }

        audio?.PlayPunch(hit);
    }

    private void ReleaseShock()
    {
        float spent = Energy;
        float ratio = Mathf.Clamp01(spent / maxEnergy);
        Energy = 0f;
        ShockCount++;
        nextAttackTime = Time.time + attackCooldown;

        gauntlet?.PlayShock();
        audio?.PlayShock();
        AddCameraShake(0.085f);
        ShockReleased?.Invoke(spent);

        float radius = shockRadius;
        DemoShockwave.Spawn(transform.position, radius, new Color(0.2f, 0.9f, 1f));

        CrystalEnemy[] enemies = FindObjectsByType<CrystalEnemy>(FindObjectsSortMode.None);
        float damage = shockBaseDamage + shockMaxBonus * ratio;
        float knockback = 9f + ratio * 9f;

        for (int i = 0; i < enemies.Length; i++)
        {
            CrystalEnemy enemy = enemies[i];
            if (enemy == null || !enemy.IsAlive)
            {
                continue;
            }

            Vector3 toEnemy = enemy.transform.position - transform.position;
            toEnemy.y = 0f;
            if (toEnemy.magnitude <= radius)
            {
                enemy.TakeDamage(damage, transform.position, knockback, true);
            }
        }

        if (gauntlet != null)
        {
            gauntlet.SetEnergyRatio(0f);
        }
    }

    private void AddCameraShake(float amount)
    {
        cameraShake = Mathf.Max(cameraShake, amount);
    }
}
