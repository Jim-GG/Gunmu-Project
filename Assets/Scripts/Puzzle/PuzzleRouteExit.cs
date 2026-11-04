using UnityEngine;

[RequireComponent(typeof(BoxCollider))]
public class PuzzleRouteExit : MonoBehaviour
{
    [SerializeField, InspectorName("所属关卡出口管理器")] private PuzzleRouteProgress route;
    private void Reset() { GetComponent<BoxCollider>().isTrigger = true; }
    private void Awake()
    {
        GetComponent<BoxCollider>().isTrigger = true;
        if (route == null) { Debug.LogError("出口触发区没有设置所属关卡出口管理器。", this); enabled = false; }
    }
    private void OnTriggerEnter(Collider other) => Check(other);
    private void OnTriggerStay(Collider other) => Check(other);
    private void Check(Collider other)
    {
        if (!GameplayGate.IsBlocked && enabled && route != null && other.GetComponentInParent<PlayerMovement>() is PlayerMovement player)
            route.ReachExit(player);
    }
}
