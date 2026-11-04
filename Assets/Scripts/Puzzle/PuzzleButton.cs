using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.InputSystem;

/// <summary>
/// 通用机关按钮：
/// - 靠近按 F，或由玩家踩上去 → 触发所有实现了 <see cref="IPuzzleTriggerable"/> 的机关，并抛出 onPressed 事件。
/// - 不再区分摆锤 / 起重机等具体机关，也不再按物体名字自动绑定目标。
/// - 被流程层拦截时（<see cref="GameplayGate"/>）不响应输入。
/// </summary>
[RequireComponent(typeof(Collider))]
public class PuzzleButton : MonoBehaviour
{
    [Header("目标")]
    [SerializeField, InspectorName("要触发的机关（需实现 IPuzzleTriggerable）")] private MonoBehaviour[] targets;
    [SerializeField, InspectorName("按下时触发（附加事件）")] private UnityEvent onPressed = new UnityEvent();

    [Header("交互")]
    [SerializeField, InspectorName("按 F 可交互距离（米）"), Min(0f)] private float interactDistance = 4.5f;
    [SerializeField, InspectorName("允许按 F 触发")] private bool triggerOnInteract = true;
    [SerializeField, InspectorName("允许踩踏触发")] private bool triggerOnStep = true;

    [Header("外观")]
    [SerializeField, InspectorName("可触发颜色")] private Color readyColor = new Color(1f, 0.55f, 0.1f, 1f);
    [SerializeField, InspectorName("已触发/冷却颜色")] private Color activeColor = new Color(0.2f, 0.95f, 0.3f, 1f);

    private readonly List<IPuzzleTriggerable> resolvedTargets = new List<IPuzzleTriggerable>();
    private Renderer buttonRenderer;
    private MaterialPropertyBlock propBlock;
    private Transform playerTransform;
    private Color lastAppliedColor;
    private bool hasAppliedColor;

    /// <summary>只要有任一目标可以被触发就算就绪（没有目标时视为就绪）。</summary>
    public bool IsReady
    {
        get
        {
            for (int i = 0; i < resolvedTargets.Count; i++)
                if (resolvedTargets[i].CanTrigger)
                    return true;
            return resolvedTargets.Count == 0;
        }
    }

    private void Awake()
    {
        buttonRenderer = GetComponent<Renderer>();
        propBlock = new MaterialPropertyBlock();
        ResolveTargets();
    }

    private void Start()
    {
        PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
        if (player != null)
            playerTransform = player.transform;

        UpdateVisuals(true);
    }

    private void Update()
    {
        if (playerTransform == null)
        {
            PlayerMovement player = FindFirstObjectByType<PlayerMovement>();
            if (player != null)
                playerTransform = player.transform;
        }

        if (!GameplayGate.IsBlocked && triggerOnInteract && playerTransform != null)
        {
            Keyboard keyboard = Keyboard.current;
            if (keyboard != null && keyboard.fKey.wasPressedThisFrame &&
                Vector3.Distance(playerTransform.position, transform.position) <= interactDistance)
            {
                Press();
            }
        }

        UpdateVisuals(false);
    }

    /// <summary>按下按钮：触发全部目标机关并抛出 onPressed。</summary>
    public void Press()
    {
        if (GameplayGate.IsBlocked)
            return;

        for (int i = 0; i < resolvedTargets.Count; i++)
            resolvedTargets[i].Trigger();

        onPressed.Invoke();
    }

    private void OnTriggerEnter(Collider other)
    {
        if (triggerOnStep)
            TryStepPress(other);
    }

    private void OnCollisionEnter(Collision collision)
    {
        if (triggerOnStep)
            TryStepPress(collision.collider);
    }

    private void TryStepPress(Collider other)
    {
        if (other == null)
            return;

        if (other.GetComponentInParent<PlayerMovement>() == null)
            return;

        Press();
    }

    private void ResolveTargets()
    {
        resolvedTargets.Clear();
        if (targets == null)
            return;

        for (int i = 0; i < targets.Length; i++)
        {
            MonoBehaviour target = targets[i];
            if (target == null)
                continue;

            if (target is IPuzzleTriggerable triggerable)
            {
                resolvedTargets.Add(triggerable);
                continue;
            }

            Debug.LogWarning($"[PuzzleButton] '{target.name}' 没有实现 IPuzzleTriggerable，不会被这个按钮触发。", this);
        }
    }

    private void UpdateVisuals(bool force)
    {
        if (buttonRenderer == null)
            return;

        Color targetColor = IsReady ? readyColor : activeColor;
        if (!force && hasAppliedColor && targetColor == lastAppliedColor)
            return;

        lastAppliedColor = targetColor;
        hasAppliedColor = true;

        propBlock.Clear();
        propBlock.SetColor("_BaseColor", targetColor);
        propBlock.SetColor("_Color", targetColor);
        buttonRenderer.SetPropertyBlock(propBlock);
    }
}
