using UnityEngine;
using Gauntlet;

/// <summary>
/// 机械拳套的正式外观与动画（GauntletViewModel + Gauntlet.controller）。
/// 纯表现层：只读取 <see cref="PlayerChargedPunch"/> 的蓄力 / 出拳 / 格挡状态，
/// 不改动伤害、冲撞距离、充能与物理反射规则。
/// 动画源的 30 FPS 帧号：1 档 32、2 档 62、满蓄力 92，可用 Inspector 调整。
/// </summary>
[DefaultExecutionOrder(60)]
public class PlayerGauntletView : MonoBehaviour
{
    [Header("外观")]
    [SerializeField, InspectorName("拳套外观预制体（GauntletViewModel）")] private GameObject viewModelPrefab;
    [SerializeField, InspectorName("挂载点（留空 = 玩家相机）")] private Transform mount;
    [SerializeField, Min(0.001f), InspectorName("相机近裁剪面")] private float nearClipPlane = 0.01f;
    [SerializeField, Range(0f, 1f), InspectorName("拳套镜头动作强度")] private float cameraMotionStrength = .55f;

    [Header("蓄力动画帧映射（源动画 30 FPS）")]
    [SerializeField, Min(1f), InspectorName("源动画帧率")] private float animationFps = 30f;
    [SerializeField, Min(1), InspectorName("一档对应帧")] private int tierOneFrame = 32;
    [SerializeField, Min(1), InspectorName("二档对应帧")] private int tierTwoFrame = 62;
    [SerializeField, Min(1), InspectorName("满蓄力对应帧")] private int fullFrame = 92;

    private enum Phase { Idle, Charging, Full, Punching, ParryStart, ParryKeep, ParryHit, ParryHolster }

    private GauntletAnimationPlayer animationPlayer;
    private GauntletCameraMotion cameraMotion;
    private Animator animator;
    private PlayerChargedPunch punch;
    private PlayerGauntletEquipment equipment;
    private Transform model;
    private Phase phase = Phase.Idle;
    private bool guardHeld;
    private int punchIndex;
    private string currentClip = "Idle";
    private bool subscribed;

    public bool IsVisible => model != null && model.gameObject.activeSelf;

    /// <summary>临时隐藏外观（望远镜等放大视野时用，不影响装备/玩法状态）。</summary>
    public bool Hidden { get; set; }

    public bool ApplyCameraPose(Transform cameraTransform, Vector3 position, Quaternion rotation)
    {
        return isActiveAndEnabled && IsVisible && cameraMotion != null
            && cameraMotion.ApplyPose(cameraTransform, position, rotation, cameraMotionStrength);
    }

    private void Awake()
    {
        punch = GetComponent<PlayerChargedPunch>();
        equipment = GetComponent<PlayerGauntletEquipment>();
    }

    private void Start()
    {
        if (viewModelPrefab == null)
        {
            Debug.LogWarning("PlayerGauntletView：未指定拳套外观预制体，拳套不可见。", this);
            enabled = false;
            return;
        }
        Transform parent = mount != null ? mount : ResolveCamera();
        if (parent == null)
        {
            Debug.LogWarning("PlayerGauntletView：找不到玩家相机，无法挂载拳套外观。", this);
            enabled = false;
            return;
        }

        GameObject instance = Instantiate(viewModelPrefab, parent);
        instance.name = "GauntletViewModel";
        model = instance.transform;
        model.localPosition = Vector3.zero;
        model.localRotation = Quaternion.identity;

        animationPlayer = instance.GetComponentInChildren<GauntletAnimationPlayer>(true);
        animator = animationPlayer != null ? animationPlayer.animator : instance.GetComponentInChildren<Animator>(true);
        if (animationPlayer == null || animator == null || animator.runtimeAnimatorController == null)
        {
            Debug.LogError("PlayerGauntletView：拳套外观预制体缺少 Animator / GauntletAnimationPlayer。", this);
            enabled = false;
            return;
        }

        // PunchPresentation 在同一次镜头更新里合成骨骼动作和打击震屏。
        cameraMotion = instance.GetComponentInChildren<GauntletCameraMotion>(true);
        if (cameraMotion != null)
        {
            Transform cameraTarget = null;
            foreach (Transform bone in animator.GetComponentsInChildren<Transform>(true))
            {
                if (bone.name != "camera_target") continue;
                cameraTarget = bone;
                break;
            }
            foreach (AnimationClip clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != "Idle") continue;
                clip.SampleAnimation(animator.gameObject, 0f);
                break;
            }
            cameraMotion.PrepareExternalDrive(cameraTarget);
        }
        animator.updateMode = AnimatorUpdateMode.UnscaledTime;

        Camera view = parent.GetComponent<Camera>();
        if (view != null) view.nearClipPlane = Mathf.Min(view.nearClipPlane, nearClipPlane);

        PlayClip("Idle", 0f);
        Subscribe(true);
        RefreshVisibility();
    }

    private Transform ResolveCamera()
    {
        Camera camera = GetComponentInChildren<Camera>();
        return camera != null ? camera.transform : null;
    }

    private void OnEnable()
    {
        if (model != null) Subscribe(true);
    }

    private void OnDisable()
    {
        Subscribe(false);
        if (model != null) model.gameObject.SetActive(false);
    }

    private void Subscribe(bool on)
    {
        if (punch == null) return;
        if (on == subscribed) return;
        subscribed = on;
        if (on)
        {
            punch.ChargeStarted += HandleChargeStarted;
            punch.ChargeChanged += HandleChargeChanged;
            punch.ChargeCanceled += HandleChargeCanceled;
            punch.PunchReleased += HandlePunchReleased;
            punch.GuardStarted += HandleGuardStarted;
            punch.GuardEnded += HandleGuardEnded;
            punch.GuardSucceeded += HandleGuardSucceeded;
        }
        else
        {
            punch.ChargeStarted -= HandleChargeStarted;
            punch.ChargeChanged -= HandleChargeChanged;
            punch.ChargeCanceled -= HandleChargeCanceled;
            punch.PunchReleased -= HandlePunchReleased;
            punch.GuardStarted -= HandleGuardStarted;
            punch.GuardEnded -= HandleGuardEnded;
            punch.GuardSucceeded -= HandleGuardSucceeded;
        }
    }

    private void LateUpdate()
    {
        if (model == null) return;
        RefreshVisibility();
    }

    private void RefreshVisibility()
    {
        if (model == null) return;
        bool equipped = equipment == null || equipment.HasGauntlet;
        bool visible = equipped && !Hidden;
        if (model.gameObject.activeSelf != visible) model.gameObject.SetActive(visible);
    }

    // ---- 状态机 ----

    private void HandleChargeStarted()
    {
        phase = Phase.Charging;
        PlayClip("charge", .06f);
    }

    private void HandleChargeChanged(float seconds)
    {
        if (phase != Phase.Charging && phase != Phase.Full) return;
        if (punch == null || !punch.IsCharging) return;
        animationPlayer.SetCharge(ChargeFrame(seconds) / Mathf.Max(1f, fullFrame));
        if (punch.IsChargeCapped && punch.CurrentTier == 3)
        {
            if (phase != Phase.Full)
            {
                phase = Phase.Full;
                PlayClip("charge full", 0.04f);
            }
            return;
        }
        // Allow the blend from idle or guard to finish before seeking the authored pose.
        if (seconds >= .06f && !animator.IsInTransition(0)) SeekCharge(seconds);
    }

    private void HandleChargeCanceled()
    {
        if (phase != Phase.Charging && phase != Phase.Full) return;
        if (guardHeld) EnterParry(Phase.ParryStart, "Parry-Start");
        else PlayIdle();
    }

    private void HandlePunchReleased(int tier)
    {
        // 档位 0（无蓄力快拳）用 punch1，一至三档依次用 punch2~punch4。
        punchIndex = Mathf.Clamp(tier == 0 ? 1 : tier + 1, 1, 4);
        phase = Phase.Punching;
        PlayClip("punch" + punchIndex, 0.025f);
    }

    private void HandleGuardStarted()
    {
        guardHeld = true;
        if (phase == Phase.Punching) return; // 出拳结束后再进入格挡
        EnterParry(Phase.ParryStart, "Parry-Start");
    }

    private void HandleGuardEnded()
    {
        guardHeld = false;
        if (IsGuardingPhase()) EnterParry(Phase.ParryHolster, "Parry-Holster");
    }

    private void HandleGuardSucceeded()
    {
        if (!IsGuardingPhase()) return;
        EnterParry(Phase.ParryHit, "Parry-Hit");
    }

    private void Update()
    {
        if (model == null || animationPlayer == null || animator == null) return;
        animator.speed = GameplayGate.IsBlocked ? 0f : 1f;
        if (GameplayGate.IsBlocked) return;
        switch (phase)
        {
            case Phase.Punching:
                if (Finished(currentClip)) RecoverAfterAttack();
                break;
            case Phase.ParryStart:
                if (Finished("Parry-Start")) EnterParry(guardHeld ? Phase.ParryKeep : Phase.ParryHolster,
                    guardHeld ? "Parry-Keep" : "Parry-Holster");
                break;
            case Phase.ParryHit:
                if (Finished("Parry-Hit")) EnterParry(guardHeld ? Phase.ParryKeep : Phase.ParryHolster,
                    guardHeld ? "Parry-Keep" : "Parry-Holster");
                break;
            case Phase.ParryHolster:
                if (Finished("Parry-Holster")) PlayIdle();
                break;
        }
    }

    private void RecoverAfterAttack()
    {
        if (guardHeld) EnterParry(Phase.ParryStart, "Parry-Start");
        else PlayIdle();
    }

    private bool IsGuardingPhase() => phase == Phase.ParryStart || phase == Phase.ParryKeep
        || phase == Phase.ParryHit || phase == Phase.ParryHolster;

    private void EnterParry(Phase next, string clip)
    {
        phase = next;
        animationPlayer.SetCharge(0f);
        PlayClip(clip, 0.02f);
    }

    private void PlayIdle()
    {
        phase = Phase.Idle;
        animationPlayer.SetCharge(0f);
        PlayClip("Idle", 0.06f);
    }

    private void PlayClip(string clip, float blend)
    {
        currentClip = clip;
        animationPlayer.PlayControlled(clip, blend);
    }

    private void SeekCharge(float seconds)
    {
        if (punch == null) return;
        float frame = ChargeFrame(seconds);
        currentClip = "charge";
        animationPlayer.SeekControlled("charge", frame / Mathf.Max(1f, animationFps));
    }

    /// <summary>把玩法侧的蓄力秒数映射到动画侧 0..92 帧：三档阈值分别对齐 32 / 62 / 92。</summary>
    private float ChargeFrame(float seconds)
    {
        float t1 = Mathf.Max(0.0001f, punch.TierOneTime);
        float t2 = Mathf.Max(t1 + 0.0001f, punch.TierTwoTime);
        float t3 = Mathf.Max(t2 + 0.0001f, punch.TierThreeTime);
        if (seconds <= t1) return tierOneFrame * (seconds / t1);
        if (seconds <= t2) return Mathf.Lerp(tierOneFrame, tierTwoFrame, (seconds - t1) / (t2 - t1));
        return Mathf.Lerp(tierTwoFrame, fullFrame, Mathf.Clamp01((seconds - t2) / (t3 - t2)));
    }

    private bool Finished(string clip)
    {
        AnimatorStateInfo state = animator.GetCurrentAnimatorStateInfo(0);
        return state.IsName(clip) && state.normalizedTime >= 1f && !animator.IsInTransition(0);
    }
}
