using UnityEngine;
using UnityEngine.Events;

/// <summary>
/// 升降廊桥：被机关触发后，桥面从下方升到指定位置。
/// - <see cref="jammed"/> 打勾时表示“卡死”：只会抖一下、升不起来，用来做关卡剧本里的坏桥。
/// - 复位时桥面回到初始（收起）位置。
/// </summary>
public class RisingBridge : MonoBehaviour, IPuzzleTriggerable, IPuzzleResettable
{
    [Header("目标")]
    [SerializeField, InspectorName("要升起的桥面")] private Transform deck;
    [SerializeField, InspectorName("升起位移（世界方向）")] private Vector3 raisedOffset = new Vector3(0f, 20f, 0f);
    [SerializeField, Min(0.1f), InspectorName("升起用时（秒）")] private float riseDuration = 2.6f;

    [Header("卡死")]
    [SerializeField, InspectorName("卡死（永远升不起来）")] private bool jammed;
    [SerializeField, Min(0f), InspectorName("卡死时上抬高度（米）")] private float jamLift = 0.35f;
    [SerializeField, Min(0.1f), InspectorName("卡死抖动时长（秒）")] private float jamDuration = 0.8f;

    [Header("事件")]
    [SerializeField, InspectorName("升起完成")] private UnityEvent onRaised = new UnityEvent();

    private Vector3 startPosition;
    private bool isRising;
    private bool isRaised;
    private float timer;
    private float jamTimer;

    public bool IsRaised => isRaised;
    public bool CanTrigger => deck != null && !isRaised && !isRising;

    private void Awake()
    {
        if (deck == null)
            deck = transform;

        startPosition = deck.position;
    }

    private void Update()
    {
        if (deck == null)
            return;

        if (isRising)
        {
            timer += Time.deltaTime;
            float t = Mathf.Clamp01(timer / Mathf.Max(riseDuration, 0.01f));
            deck.position = startPosition + raisedOffset * Mathf.SmoothStep(0f, 1f, t);

            if (t >= 1f)
            {
                isRising = false;
                isRaised = true;
                deck.position = startPosition + raisedOffset;
                onRaised.Invoke();
            }
            return;
        }

        if (jamTimer > 0f)
        {
            jamTimer -= Time.deltaTime;
            float t = 1f - Mathf.Clamp01(jamTimer / Mathf.Max(jamDuration, 0.01f));
            float lift = Mathf.Sin(t * Mathf.PI) * jamLift;
            deck.position = startPosition + Vector3.up * lift;

            if (jamTimer <= 0f)
                deck.position = startPosition;
        }
    }

    /// <summary>尝试升起；卡死时只抖一下。</summary>
    public void Trigger()
    {
        if (deck == null || isRising || isRaised)
            return;

        if (jammed)
        {
            jamTimer = jamDuration;
            Debug.LogWarning($"[RisingBridge] {name} 卡住了，升不起来。", this);
            return;
        }

        isRising = true;
        timer = 0f;
        Debug.Log($"[RisingBridge] {name} 开始升起。", this);
    }

    /// <summary>复位到收起状态。</summary>
    public void ResetPuzzle()
    {
        isRising = false;
        isRaised = false;
        timer = 0f;
        jamTimer = 0f;

        if (deck != null)
            deck.position = startPosition;
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (deck == null)
            deck = transform;
    }
#endif
}
