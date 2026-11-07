using UnityEngine;

public sealed class DemoTutorialGate : MonoBehaviour, ILevelExitGate
{
    [SerializeField, InspectorName("必须完成的机关")] private MonoBehaviour[] objectives;
    [SerializeField, InspectorName("完成后打开的门")] private GameObject exitDoor;
    [SerializeField, InspectorName("开门位移（为零时隐藏门）")] private Vector3 openOffset;
    [SerializeField, Min(.1f), InspectorName("开门速度")] private float openingSpeed = 3.5f;
    private ILevelExitGate[] gates;
    private Vector3 closedPosition;

    public int TotalCount => gates?.Length ?? 0;
    public int CompletedCount
    {
        get
        {
            int count = 0;
            if (gates != null)
                foreach (ILevelExitGate gate in gates)
                    if (gate != null && gate.IsSatisfied) count++;
            return count;
        }
    }

    public bool IsSatisfied => TotalCount > 0 && CompletedCount == TotalCount;

    private void Awake()
    {
        if (exitDoor != null) closedPosition = exitDoor.transform.position;
        gates = new ILevelExitGate[objectives?.Length ?? 0];
        for (int i = 0; i < gates.Length; i++)
            gates[i] = objectives[i] as ILevelExitGate;
    }

    private void Update()
    {
        if (exitDoor == null || !exitDoor.activeSelf || !IsSatisfied) return;
        if (openOffset == Vector3.zero)
            exitDoor.SetActive(false);
        else
            exitDoor.transform.position = Vector3.MoveTowards(exitDoor.transform.position,
                closedPosition + openOffset, openingSpeed * Time.deltaTime);
    }
}
