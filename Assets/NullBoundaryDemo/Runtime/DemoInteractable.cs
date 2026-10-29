using UnityEngine;
namespace NullBoundary.Demo
{
    public enum DemoAction { CloseChaseDoor, Calibrate, TakeGauntlet }
    public sealed class DemoInteractable : MonoBehaviour
    {
        public DemoAction action;
        public string prompt;
        public void Use()
        {
            var flow=DemoFlow.Instance;
            if (flow) flow.Interact(action);
        }
    }
}

