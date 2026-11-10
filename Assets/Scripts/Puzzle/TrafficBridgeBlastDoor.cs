using UnityEngine;

/// <summary>Soviet_Traffic 结尾防爆门：Switch_Bridge 激活后，门扇绕 Y 轴转 -100°。</summary>
public sealed class TrafficBridgeBlastDoor : MonoBehaviour
{
    [SerializeField, InspectorName("桥开关")] private PuzzleSwitch switchBridge;

    private Quaternion closedRotation;

    private void Awake()
    {
        closedRotation = transform.rotation;
    }

    private void Update()
    {
        bool open = switchBridge != null && switchBridge.IsActivated;
        transform.rotation = closedRotation * Quaternion.Euler(0f, open ? -100f : 0f, 0f);
    }
}
