using UnityEngine;

/// <summary>
/// 大炮：侧面填弹仓（Trigger）吸入玩家或动能方块，稍作停顿后沿炮管方向（含仰角）以设定速度发射出去。
///
/// 摆放方式：
/// - 朝向（偏航）= 预制体实例自身的 Y 旋转；仰角 = 本组件的「仰角」字段（作用在「炮管」引用的本地 X 旋转上）。
/// - 脚本里没有任何关卡坐标，摆到哪、朝哪完全由 Transform 决定。
/// - 填弹仓是子物体 LoadingBay 上的 Trigger 碰撞体（留空会自动按名字查找）。
/// </summary>
[DisallowMultipleComponent]
public class Cannon : MonoBehaviour, IPuzzleResettable
{
    private enum State { Idle, Sucking, Charging }

    [Header("引用")]
    [SerializeField, InspectorName("炮管（用它的 forward 作为发射方向）")] private Transform barrel;
    [SerializeField, InspectorName("发射点（留空 = 炮管末端前方）")] private Transform muzzle;
    [SerializeField, InspectorName("填弹仓触发器（留空 = 自动找子物体 LoadingBay）")] private Collider loadingBay;

    [Header("朝向")]
    [SerializeField, Range(-80f, 80f), InspectorName("仰角（度，正 = 上扬）")] private float elevation = 12f;

    [Header("工作参数")]
    [SerializeField, Min(0f), InspectorName("吸入时长（秒）")] private float suckDuration = 0.25f;
    [SerializeField, Min(0f), InspectorName("蓄压间隔（秒）")] private float chargeDelay = 0.55f;
    [SerializeField, Min(1f), InspectorName("发射速度（m/s）")] private float launchSpeed = 26f;
    [SerializeField, Min(0.1f), InspectorName("方块发射速度倍率")] private float boxSpeedMultiplier = 0.7f;
    [SerializeField, Min(0f), InspectorName("装填冷却（秒）")] private float reloadTime = 1.2f;
    [SerializeField, InspectorName("发射点前方偏置（米）")] private float muzzleOffset = 0.75f;
    [SerializeField, InspectorName("是否吸入玩家")] private bool capturePlayer = true;

    [Header("外观反馈")]
    [SerializeField, InspectorName("炮口指示灯（留空 = 自动找子物体 ChargeLight）")] private Renderer chargeIndicator;
    [SerializeField, InspectorName("待机色")] private Color idleColor = new Color(0.3f, 0.85f, 1f, 1f);
    [SerializeField, InspectorName("蓄压色")] private Color chargedColor = new Color(1f, 0.5f, 0.12f, 1f);
    [SerializeField, Min(0f), InspectorName("发光强度")] private float emissionIntensity = 2.5f;

    private State state = State.Idle;
    private float timer;
    private float cooldownUntil;

    private KineticBox heldBox;
    private PlayerMovement heldPlayer;
    private CharacterController heldController;
    private Vector3 suckFrom;
    private MaterialPropertyBlock propBlock;

    /// <summary>当前是否装着东西（调试用）。</summary>
    public bool IsLoaded => state == State.Charging || state == State.Sucking;
    /// <summary>当前是否把玩家吸在炮膛里（教学提示等只读查询用）。</summary>
    public bool HasPlayer => heldPlayer != null;
    /// <summary>发射方向（含仰角）。</summary>
    public Vector3 LaunchDirection => barrel != null ? barrel.forward : transform.forward;

    private void Awake()
    {
        if (barrel == null) barrel = transform;
        if (loadingBay == null)
        {
            Transform bay = transform.Find("LoadingBay");
            if (bay != null) loadingBay = bay.GetComponent<Collider>();
        }
        if (loadingBay != null) loadingBay.isTrigger = true;
        if (chargeIndicator == null)
        {
            Transform light = transform.Find("ChargeLight");
            if (light != null) chargeIndicator = light.GetComponent<Renderer>();
        }

        propBlock = new MaterialPropertyBlock();
        ApplyElevation();
        UpdateIndicator(idleColor);
    }

    private void OnValidate()
    {
        ApplyElevation();
    }

    /// <summary>把「仰角」写入炮管的本地 X 旋转（偏航由根节点的 Y 旋转负责）。正的仰角 = 炮口上扬。</summary>
    private void ApplyElevation()
    {
        if (barrel == null || barrel == transform) return;
        Vector3 euler = barrel.localEulerAngles;
        // Unity 里绕 +X 正向旋转会让 forward 向下偏，所以这里取负号，让 Inspector 上的正值就是“上扬”。
        barrel.localEulerAngles = new Vector3(-elevation, euler.y, euler.z);
    }

    private void Update()
    {
        if (GameplayGate.IsBlocked)
        {
            if (IsLoaded) ReleaseHeld();
            return;
        }

        switch (state)
        {
            case State.Idle:
                if (Time.time >= cooldownUntil) ScanLoadingBay();
                return;

            case State.Sucking:
                timer += Time.deltaTime;
                float t = suckDuration > 0.0001f ? Mathf.Clamp01(timer / suckDuration) : 1f;
                MoveHeldTo(Vector3.Lerp(suckFrom, HoldPoint, t * t)); // 先慢后快，像被吸进去
                if (t >= 1f)
                {
                    state = State.Charging;
                    timer = 0f;
                    UpdateIndicator(chargedColor);
                }
                return;

            case State.Charging:
                timer += Time.deltaTime;
                MoveHeldTo(HoldPoint);
                if (timer >= chargeDelay)
                {
                    Fire();
                }
                return;
        }
    }

    private Vector3 HoldPoint => barrel != null ? barrel.position + barrel.forward * 0.35f : transform.position;

    private Vector3 FirePoint
    {
        get
        {
            Vector3 point = muzzle != null
                ? muzzle.position
                : (barrel != null ? barrel.position + barrel.forward * 2.4f : transform.position + transform.forward * 2.4f);
            return point + LaunchDirection * muzzleOffset;
        }
    }

    /// <summary>
    /// 轮询填弹仓体积（用 OverlapBox 而不是 Trigger 回调）。
    /// 触发器碰撞体在子物体上，而本组件在根节点：Unity 的 Trigger 消息只会发给“带碰撞体的那个物体”，
    /// 所以直接轮询更可靠，也顺便能处理 CharacterController（玩家）这种非刚体。
    /// </summary>
    private void ScanLoadingBay()
    {
        if (loadingBay == null) return;

        Bounds bounds = loadingBay.bounds;
        Collider[] hits = Physics.OverlapBox(bounds.center, bounds.extents, loadingBay.transform.rotation,
            Physics.AllLayers, QueryTriggerInteraction.Collide);

        for (int i = 0; i < hits.Length; i++)
        {
            Collider hit = hits[i];
            if (hit == null || hit.transform.IsChildOf(transform)) continue;

            KineticBox box = hit.GetComponentInParent<KineticBox>();
            if (box != null && !box.IsMomentumLocked)
            {
                CaptureBox(box);
                return;
            }

            if (!capturePlayer) continue;
            PlayerMovement player = hit.GetComponentInParent<PlayerMovement>();
            if (player != null)
            {
                CapturePlayer(player);
                return;
            }
        }
    }

    // ---------------- 捕获 ----------------

    private void CaptureBox(KineticBox box)
    {
        if (box == null || box.IsMomentumLocked) return;

        heldBox = box;
        suckFrom = box.transform.position;
        if (box.Body != null)
        {
            box.Body.linearVelocity = Vector3.zero;
            box.Body.angularVelocity = Vector3.zero;
            box.Body.isKinematic = true;
        }
        state = State.Sucking;
        timer = 0f;
        UpdateIndicator(chargedColor);
    }

    private void CapturePlayer(PlayerMovement player)
    {
        if (player == null) return;

        heldPlayer = player;
        heldController = player.GetComponent<CharacterController>();
        suckFrom = player.transform.position;
        player.enabled = false;                    // 吸住期间交出控制权
        if (heldController != null) heldController.enabled = false;
        state = State.Sucking;
        timer = 0f;
        UpdateIndicator(chargedColor);
    }

    private void MoveHeldTo(Vector3 position)
    {
        if (heldBox != null) heldBox.transform.position = position;
        else if (heldPlayer != null) heldPlayer.transform.position = position;
    }

    // ---------------- 发射 ----------------

    private void Fire()
    {
        Vector3 direction = LaunchDirection.normalized;
        Vector3 firePoint = FirePoint;

        if (heldBox != null)
        {
            KineticBox box = heldBox;
            heldBox = null;
            box.transform.position = firePoint;
            if (box.Body != null)
            {
                box.Body.isKinematic = false;
                box.Body.linearVelocity = Vector3.zero;
                box.Body.angularVelocity = Vector3.zero;
            }
            // 自由轨迹发射：不做 6 轴吸附，保留仰角。
            // 方块重力（9.81）比玩家（20）小，同样速度会飞两倍远，所以方块单独乘一个倍率（默认 0.7 ≈ sqrt(9.81/20)）。
            box.ApplyFreeImpulse(direction, launchSpeed * Mathf.Max(0.1f, boxSpeedMultiplier));
        }
        else if (heldPlayer != null)
        {
            PlayerMovement player = heldPlayer;
            CharacterController controller = heldController;
            heldPlayer = null;
            heldController = null;

            if (controller != null) controller.enabled = false;
            player.transform.position = firePoint;
            if (controller != null) controller.enabled = true;
            player.enabled = true;
            player.LaunchAsProjectile(direction * launchSpeed);
        }

        state = State.Idle;
        timer = 0f;
        cooldownUntil = Time.time + reloadTime;
        UpdateIndicator(idleColor);
    }

    /// <summary>取消装填并放掉手里的东西（不发射）。</summary>
    private void ReleaseHeld()
    {
        if (heldBox != null)
        {
            if (heldBox.Body != null) heldBox.Body.isKinematic = false;
            heldBox = null;
        }
        if (heldPlayer != null)
        {
            if (heldController != null) heldController.enabled = true;
            heldPlayer.enabled = true;
            heldPlayer = null;
            heldController = null;
        }
        state = State.Idle;
        timer = 0f;
        UpdateIndicator(idleColor);
    }

    private void OnDisable()
    {
        ReleaseHeld();
    }

    /// <summary>R 复位：清空装填状态。</summary>
    public void ResetPuzzle()
    {
        ReleaseHeld();
        cooldownUntil = 0f;
    }

    private void UpdateIndicator(Color color)
    {
        if (chargeIndicator == null) return;
        if (propBlock == null) propBlock = new MaterialPropertyBlock();
        chargeIndicator.GetPropertyBlock(propBlock);
        propBlock.SetColor("_BaseColor", color);
        propBlock.SetColor("_Color", color);
        propBlock.SetColor("_EmissionColor", new Color(color.r, color.g, color.b) * emissionIntensity);
        chargeIndicator.SetPropertyBlock(propBlock);
    }
}
