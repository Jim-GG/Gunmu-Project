using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

/// <summary>One crystal host: telegraphed charge attack, block reaction and death shatter.</summary>
[RequireComponent(typeof(CharacterController))]
public class CrystalEnemy : MonoBehaviour
{
    public enum EnemyState
    {
        Dormant,
        Chase,
        Windup,
        Strike,
        Recover,
        Stunned,
        Dead
    }

    [Header("Stats")]
    [SerializeField, Min(1f)] private float maxHealth = 100f;
    [SerializeField, Min(0f)] private float attackDamage = 20f;
    [SerializeField, Min(0.4f)] private float attackRange = 2.9f;
    [SerializeField, Min(0.4f)] private float stopDistance = 2.15f;

    [Header("Movement")]
    [SerializeField, Min(0f)] private float moveSpeed = 3.4f;
    [SerializeField, Min(1f)] private float turnSpeed = 8f;
    [SerializeField, Min(0f)] private float lungeSpeed = 8f;
    [SerializeField, Min(0.1f)] private float attackHitRange = 3.25f;

    [Header("Timing")]
    [SerializeField, Min(0.2f)] private float wakeDelay = 1.2f;
    [SerializeField, Min(0.4f)] private float windupDuration = 1.15f;
    [SerializeField, Min(0.08f)] private float strikeDuration = 0.2f;
    [SerializeField, Min(0.3f)] private float recoverDuration = 1.3f;
    [SerializeField, Min(0.3f)] private float stunDuration = 1.45f;

    [Header("Visual")]
    [SerializeField] private string displayName = "结晶异形体";

    public string DisplayName => displayName;
    public float Health { get; private set; }
    public float MaxHealth => maxHealth;
    public bool IsAlive => state != EnemyState.Dead;
    public bool IsTelegraphing =>
        (state == EnemyState.Windup || state == EnemyState.Strike)
        && combatActive
        && (DemoManager.Instance == null || DemoManager.Instance.IsPlaying);
    public EnemyState State => state;

    private EnemyState state = EnemyState.Dormant;
    private PlayerCombat player;
    private CharacterController controller;
    private DemoAudio audio;

    private GameObject visualsRoot;
    private Transform shardRing;
    private Transform bodyTransform;
    private Transform headTransform;
    private Transform coreTransform;
    private Light coreLight;
    private LineRenderer telegraphRing;
    private Material crystalMaterial;
    private Material coreMaterial;
    private float hitFlash;
    private float verticalVelocity;
    private float stateTimer;
    private float nextAttackTime;
    private Vector3 strikeDirection;
    private Vector3 knockbackVelocity;
    private bool combatActive = true;
    private float shardSpin;
    private readonly List<Transform> shards = new List<Transform>();

    private void Awake()
    {
        Health = maxHealth;
        controller = GetComponent<CharacterController>();
    }

    private void Start()
    {
        player = FindFirstObjectByType<PlayerCombat>();
        audio = DemoManager.Instance != null ? DemoManager.Instance.Audio : null;
        BuildVisuals();
        EnemyWorldHealthBar.Attach(this, transform);

        Vector3 toPlayer = player != null ? player.transform.position - transform.position : transform.forward;
        toPlayer.y = 0f;
        if (toPlayer.sqrMagnitude > 0.001f)
        {
            transform.rotation = Quaternion.LookRotation(toPlayer.normalized, Vector3.up);
        }

        state = EnemyState.Dormant;
        stateTimer = wakeDelay;
    }

    private void Update()
    {
        float dt = Time.deltaTime;
        UpdateVisuals(dt);

        if (!combatActive || !IsAlive)
        {
            return;
        }

        if (DemoManager.Instance != null && !DemoManager.Instance.IsPlaying)
        {
            SetTelegraph(false);
            return;
        }

        if (player == null)
        {
            player = FindFirstObjectByType<PlayerCombat>();
            if (player == null)
            {
                return;
            }
        }

        Vector3 toPlayer = player.transform.position - transform.position;
        toPlayer.y = 0f;
        float distance = toPlayer.magnitude;
        Vector3 direction = distance > 0.001f ? toPlayer / distance : transform.forward;

        Vector3 planarMove = Vector3.zero;

        switch (state)
        {
            case EnemyState.Dormant:
                stateTimer -= dt;
                Face(direction, dt);
                if (stateTimer <= 0f)
                {
                    state = EnemyState.Chase;
                }
                break;

            case EnemyState.Chase:
                Face(direction, dt);
                if (distance > stopDistance)
                {
                    planarMove = direction * moveSpeed;
                }

                if (distance <= attackRange
                    && Time.time >= nextAttackTime
                    && player.IsAlive)
                {
                    EnterWindup();
                }
                break;

            case EnemyState.Windup:
                Face(direction, dt * 0.35f);
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = EnemyState.Strike;
                    stateTimer = strikeDuration;
                    strikeDirection = direction;
                }
                break;

            case EnemyState.Strike:
                stateTimer -= dt;
                planarMove = strikeDirection * lungeSpeed;
                if (stateTimer <= 0f)
                {
                    ResolveStrike();
                }
                break;

            case EnemyState.Recover:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = EnemyState.Chase;
                }
                break;

            case EnemyState.Stunned:
                stateTimer -= dt;
                if (stateTimer <= 0f)
                {
                    state = EnemyState.Chase;
                }
                break;
        }

        Move(planarMove, dt);
    }

    private void EnterWindup()
    {
        state = EnemyState.Windup;
        stateTimer = windupDuration;
        SetTelegraph(true);
        audio?.PlayEnemyWindup();
    }

    private void ResolveStrike()
    {
        SetTelegraph(false);

        float distance = player != null
            ? Vector3.Distance(
                new Vector3(transform.position.x, 0f, transform.position.z),
                new Vector3(player.transform.position.x, 0f, player.transform.position.z))
            : float.MaxValue;

        if (player != null && player.IsAlive && distance <= attackHitRange)
        {
            bool blocked = player.ReceiveAttack(transform.position, attackDamage);
            if (blocked)
            {
                state = EnemyState.Stunned;
                stateTimer = stunDuration;
                knockbackVelocity = -strikeDirection * 7.5f;
                hitFlash = 0.22f;
                nextAttackTime = Time.time + 0.7f;
                return;
            }
        }

        state = EnemyState.Recover;
        stateTimer = recoverDuration;
        nextAttackTime = Time.time + recoverDuration + 0.2f;
    }

    private void Face(Vector3 direction, float dt)
    {
        if (direction.sqrMagnitude < 0.001f)
        {
            return;
        }

        Quaternion target = Quaternion.LookRotation(direction, Vector3.up);
        transform.rotation = Quaternion.Slerp(transform.rotation, target, 1f - Mathf.Exp(-turnSpeed * dt));
    }

    private void Move(Vector3 planarVelocity, float dt)
    {
        if (controller == null || !controller.enabled)
        {
            return;
        }

        if (controller.isGrounded && verticalVelocity < 0f)
        {
            verticalVelocity = -2f;
        }
        else
        {
            verticalVelocity -= 22f * dt;
        }

        Vector3 motion = planarVelocity * dt;
        motion += Vector3.up * verticalVelocity * dt;
        motion += knockbackVelocity * dt;

        controller.Move(motion);
        knockbackVelocity = Vector3.MoveTowards(knockbackVelocity, Vector3.zero, 15f * dt);
    }

    public void TakeDamage(float amount, Vector3 sourcePosition, float knockbackStrength, bool stagger)
    {
        if (!IsAlive)
        {
            return;
        }

        Health = Mathf.Max(0f, Health - amount);
        hitFlash = 0.16f;
        audio?.PlayEnemyHit();

        Vector3 away = transform.position - sourcePosition;
        away.y = 0f;
        if (away.sqrMagnitude > 0.001f)
        {
            knockbackVelocity += away.normalized * knockbackStrength;
        }

        if (Health <= 0f)
        {
            Die();
            return;
        }

        if (stagger)
        {
            state = EnemyState.Stunned;
            stateTimer = Mathf.Max(stateTimer, 0.9f);
            SetTelegraph(false);
            nextAttackTime = Time.time + 0.45f;
        }
    }

    public void SetCombatActive(bool active)
    {
        combatActive = active;
        if (!active)
        {
            SetTelegraph(false);
        }
    }

    private void Die()
    {
        state = EnemyState.Dead;
        SetTelegraph(false);
        if (controller != null && controller.enabled)
        {
            controller.enabled = false;
        }

        audio?.PlayEnemyDeath();
        StartCoroutine(DeathRoutine());
    }

    private IEnumerator DeathRoutine()
    {
        if (coreLight != null)
        {
            coreLight.intensity = 8f;
        }

        if (bodyTransform != null)
        {
            bodyTransform.gameObject.SetActive(false);
        }
        if (headTransform != null)
        {
            headTransform.gameObject.SetActive(false);
        }
        if (coreTransform != null)
        {
            coreTransform.gameObject.SetActive(false);
        }

        for (int i = 0; i < shards.Count; i++)
        {
            Transform shard = shards[i];
            if (shard == null)
            {
                continue;
            }

            shard.SetParent(null, true);
            shard.gameObject.SetActive(true);
            Rigidbody body = shard.gameObject.AddComponent<Rigidbody>();
            body.mass = 0.35f;
            body.AddForce(
                (Vector3.up * Random.Range(1.5f, 4.5f)
                 + Random.insideUnitSphere * 4f)
                * 2.2f,
                ForceMode.VelocityChange);
            body.AddTorque(Random.insideUnitSphere * 20f, ForceMode.VelocityChange);
            Destroy(shard.gameObject, 2.5f);
        }

        if (coreLight != null)
        {
            coreLight.intensity = 0f;
        }

        yield return new WaitForSeconds(0.75f);
        DemoManager.Instance?.NotifyEnemyDefeated(this);
        gameObject.SetActive(false);
    }

    private void SetTelegraph(bool active)
    {
        if (telegraphRing != null)
        {
            telegraphRing.enabled = active;
        }
    }

    private void UpdateVisuals(float dt)
    {
        if (visualsRoot == null)
        {
            return;
        }

        if (hitFlash > 0f)
        {
            hitFlash = Mathf.Max(0f, hitFlash - dt);
        }

        float telegraph = state == EnemyState.Windup ? 1f : (state == EnemyState.Strike ? 0.7f : 0f);
        float bob = Mathf.Sin(Time.time * 2.2f) * 0.04f;
        visualsRoot.transform.localPosition = new Vector3(0f, bob, 0f);

        shardSpin += dt * (24f + telegraph * 150f);
        if (shardRing != null)
        {
            shardRing.localRotation = Quaternion.Euler(0f, shardSpin, 0f);
            float spread = 1f + telegraph * 0.11f;
            shardRing.localScale = new Vector3(spread, 1f, spread);
        }

        if (visualsRoot != null)
        {
            visualsRoot.transform.localRotation = Quaternion.Euler(telegraph * 6f, 0f, 0f);
        }

        Color hot = Color.Lerp(new Color(1f, 0.18f, 0.16f), new Color(1f, 0.82f, 0.55f), telegraph);
        if (hitFlash > 0f)
        {
            hot = Color.Lerp(hot, Color.white, Mathf.Clamp01(hitFlash * 6f));
        }

        if (crystalMaterial != null)
        {
            float emission = 0.8f + telegraph * 2.2f + Mathf.Sin(Time.time * 3.5f) * 0.15f + hitFlash * 5f;
            crystalMaterial.EnableKeyword("_EMISSION");
            crystalMaterial.SetColor("_EmissionColor", hot * emission);
        }

        if (coreMaterial != null)
        {
            float corePulse = 1.2f + telegraph * 3.2f + Mathf.Sin(Time.time * 8f) * 0.25f + hitFlash * 6f;
            coreMaterial.EnableKeyword("_EMISSION");
            coreMaterial.SetColor("_EmissionColor", hot * corePulse);
        }

        if (coreLight != null)
        {
            coreLight.color = hot;
            coreLight.intensity = (IsAlive ? 0.7f : 0f) + telegraph * 2.2f + hitFlash * 3f;
        }

        if (telegraphRing != null && telegraphRing.enabled)
        {
            float pulse = 0.65f + 0.35f * Mathf.Abs(Mathf.Sin(Time.time * 8f));
            Color ringColor = new Color(1f, 0.16f, 0.15f, 0.45f + 0.45f * pulse);
            telegraphRing.startColor = ringColor;
            telegraphRing.endColor = ringColor;
            telegraphRing.startWidth = 0.055f + 0.025f * pulse;
            telegraphRing.endWidth = telegraphRing.startWidth;
        }
    }

    private void BuildVisuals()
    {
        Material bodyMaterial = DemoVisualFactory.CreateLitMaterial(
            "CrystalHost_Body",
            new Color(0.055f, 0.065f, 0.075f),
            metallic: 0.55f,
            smoothness: 0.28f);

        crystalMaterial = DemoVisualFactory.CreateLitMaterial(
            "CrystalHost_Shards",
            new Color(0.35f, 0.045f, 0.08f),
            metallic: 0.25f,
            smoothness: 0.62f,
            emission: new Color(1f, 0.18f, 0.16f),
            emissionIntensity: 1.4f);

        coreMaterial = DemoVisualFactory.CreateLitMaterial(
            "CrystalHost_Core",
            new Color(0.8f, 0.15f, 0.1f),
            metallic: 0.1f,
            smoothness: 0.8f,
            emission: new Color(1f, 0.32f, 0.2f),
            emissionIntensity: 3f);

        visualsRoot = new GameObject("Visuals");
        visualsRoot.transform.SetParent(transform, false);

        bodyTransform = DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Capsule, "Body", visualsRoot.transform,
            new Vector3(0f, 1.08f, 0f), new Vector3(0.84f, 0.92f, 0.84f),
            Quaternion.identity, bodyMaterial, castShadows: true).transform;

        headTransform = DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "Head", visualsRoot.transform,
            new Vector3(0f, 1.82f, 0.02f), new Vector3(0.46f, 0.34f, 0.44f),
            Quaternion.Euler(0f, 10f, 0f), bodyMaterial, castShadows: true).transform;

        coreTransform = DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Sphere, "Core", visualsRoot.transform,
            new Vector3(0f, 1.38f, 0.37f), Vector3.one * 0.27f,
            Quaternion.identity, coreMaterial, castShadows: false).transform;

        GameObject shardRoot = new GameObject("ShardRing");
        shardRoot.transform.SetParent(visualsRoot.transform, false);
        shardRoot.transform.localPosition = new Vector3(0f, 1.08f, 0f);
        shardRing = shardRoot.transform;

        for (int i = 0; i < 7; i++)
        {
            float angle = i * Mathf.PI * 2f / 7f;
            Vector3 localPosition = new Vector3(Mathf.Cos(angle) * 0.58f, 0.1f + (i % 3) * 0.17f, Mathf.Sin(angle) * 0.58f);
            GameObject shard = DemoVisualFactory.CreatePrimitive(
                PrimitiveType.Cube, "Shard_" + i, shardRing,
                localPosition,
                new Vector3(0.11f, 0.38f + (i % 2) * 0.16f, 0.11f),
                Quaternion.Euler(20f + i * 13f, -angle * Mathf.Rad2Deg, 28f),
                crystalMaterial,
                castShadows: true);
            shards.Add(shard.transform);
        }

        for (int side = -1; side <= 1; side += 2)
        {
            GameObject arm = DemoVisualFactory.CreatePrimitive(
                PrimitiveType.Cube, "Arm_" + side, visualsRoot.transform,
                new Vector3(side * 0.56f, 1.12f, 0.08f),
                new Vector3(0.14f, 0.58f, 0.14f),
                Quaternion.Euler(0f, 0f, side * 28f),
                crystalMaterial,
                castShadows: true);
            shards.Add(arm.transform);
        }

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cylinder, "Base", visualsRoot.transform,
            new Vector3(0f, 0.025f, 0f), new Vector3(0.85f, 0.02f, 0.85f),
            Quaternion.identity, bodyMaterial, castShadows: false);

        telegraphRing = DemoVisualFactory.CreateLineCircle(
            "AttackTelegraph",
            transform,
            new Vector3(0f, 0.045f, 0f),
            2.7f,
            64,
            0.065f,
            new Color(1f, 0.16f, 0.15f, 0.8f));
        telegraphRing.enabled = false;

        GameObject lightObject = new GameObject("CoreLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.4f, 0.3f);
        coreLight = lightObject.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.color = new Color(1f, 0.2f, 0.15f);
        coreLight.range = 4.5f;
        coreLight.intensity = 1.4f;
        coreLight.shadows = LightShadows.None;
    }
}
