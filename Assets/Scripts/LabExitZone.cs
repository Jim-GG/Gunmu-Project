using UnityEngine;

public class LabExitZone : MonoBehaviour
{
    [SerializeField] private LabDemoFlow flow;
    public void Configure(LabDemoFlow demoFlow) => flow = demoFlow;

    private void OnTriggerEnter(Collider other)
    {
        if (flow != null) flow.TryFinish(other.GetComponentInParent<PlayerHealth>());
    }
}
