using UnityEngine;
using UnityEngine.InputSystem;

/// <summary>
/// 望远镜视野：按 C 在“正常视野 / 放大视野”之间切换，单纯放大画面（不做遮罩、不改渲染）。
/// 放大时会把鼠标灵敏度按倍率下调，避免高倍率下瞄不准；把倍率设成 1 就是纯放大。
/// 暂停 / 死亡（<see cref="GameplayGate.IsBlocked"/>）时自动退出放大。
/// </summary>
public sealed class PlayerZoom : MonoBehaviour
{
    [Header("引用")]
    [SerializeField, InspectorName("观察相机（留空自动找子物体）")] private Camera viewCamera;
    [SerializeField, InspectorName("鼠标观察（留空自动找，用于缩放灵敏度）")] private PlayerLook playerLook;
    [SerializeField, InspectorName("拳套外观（留空自动找，放大时隐藏以免被拉大）")] private PlayerGauntletView gauntletView;

    [Header("视野")]
    [SerializeField, Min(5f), InspectorName("放大后视野（越小放得越大）")] private float zoomFieldOfView = 20f;
    [SerializeField, Min(0f), InspectorName("视野过渡时间（秒，0 = 瞬切）")] private float transitionTime = 0.12f;

    [Header("操作")]
    [SerializeField, InspectorName("切换按键")] private Key toggleKey = Key.C;
    [SerializeField, Range(0.05f, 1f), InspectorName("放大时鼠标灵敏度倍率")] private float zoomSensitivityScale = 0.35f;
    [SerializeField, InspectorName("放大时隐藏拳套外观")] private bool hideGauntletWhileZoomed = true;

    private float defaultFieldOfView = 60f;
    private float targetFieldOfView = 60f;
    private float appliedSensitivity = 1f;

    /// <summary>当前是否处于放大状态。</summary>
    public bool IsZoomed { get; private set; }

    private void Awake()
    {
        if (viewCamera == null) viewCamera = GetComponentInChildren<Camera>(true);
        if (playerLook == null) playerLook = GetComponent<PlayerLook>();
        if (gauntletView == null) gauntletView = GetComponent<PlayerGauntletView>();

        if (viewCamera != null && viewCamera.fieldOfView > 0f)
            defaultFieldOfView = viewCamera.fieldOfView;

        targetFieldOfView = defaultFieldOfView;
    }

    private void OnDisable()
    {
        SetZoom(false, true);
    }

    private void Update()
    {
        if (viewCamera == null) return;

        if (GameplayGate.IsBlocked)
        {
            if (IsZoomed) SetZoom(false, true);
            return;
        }

        Keyboard keyboard = Keyboard.current;
        bool canToggle = keyboard != null
                         && keyboard[toggleKey] != null
                         && Cursor.lockState == CursorLockMode.Locked;

        if (canToggle && keyboard[toggleKey].wasPressedThisFrame)
            Toggle();

        float speed = transitionTime <= 0f
            ? float.MaxValue
            : Mathf.Abs(defaultFieldOfView - zoomFieldOfView) / transitionTime;
        viewCamera.fieldOfView = Mathf.MoveTowards(viewCamera.fieldOfView, targetFieldOfView, speed * Time.unscaledDeltaTime);
    }

    /// <summary>切换放大 / 正常视野。</summary>
    public void Toggle()
    {
        SetZoom(!IsZoomed, false);
    }

    /// <summary>设置放大状态；<paramref name="immediate"/> = true 时不走过渡。</summary>
    public void SetZoom(bool zoomed, bool immediate)
    {
        IsZoomed = zoomed && viewCamera != null;
        targetFieldOfView = IsZoomed
            ? Mathf.Min(zoomFieldOfView, defaultFieldOfView)
            : defaultFieldOfView;

        if (immediate && viewCamera != null)
            viewCamera.fieldOfView = targetFieldOfView;

        float scale = IsZoomed ? zoomSensitivityScale : 1f;
        if (playerLook != null && !Mathf.Approximately(appliedSensitivity, scale))
        {
            playerLook.SensitivityScale = scale;
            appliedSensitivity = scale;
        }

        // 放大时把拳套外观藏起来（它是挂在主相机下的 viewmodel，会被视野一并拉大）。
        if (gauntletView != null)
            gauntletView.Hidden = IsZoomed && hideGauntletWhileZoomed;
    }
}
