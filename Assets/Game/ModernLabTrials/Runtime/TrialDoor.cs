using UnityEngine;

namespace ModernLabTrials
{
    // A simple sliding door used for lab exits and blockers.
    public sealed class TrialDoor : MonoBehaviour
    {
        [SerializeField] private Vector3 openOffset = new Vector3(0f, 3.4f, 0f);
        [SerializeField, Min(.05f)] private float duration = .85f;

        private Vector3 closedPosition;
        private Vector3 targetPosition;
        private bool open;

        public bool IsOpen => open;

        private void Awake()
        {
            closedPosition = transform.position;
            targetPosition = closedPosition;
        }

        public void SetOpen(bool value)
        {
            if (open == value) return;
            open = value;
            targetPosition = closedPosition + (open ? openOffset : Vector3.zero);
        }

        public void ResetDoor()
        {
            open = false;
            targetPosition = closedPosition;
            transform.position = closedPosition;
        }

        private void Update()
        {
            if ((transform.position - targetPosition).sqrMagnitude < .0001f) return;
            float speed = openOffset.magnitude / Mathf.Max(.05f, duration);
            transform.position = Vector3.MoveTowards(transform.position, targetPosition, speed * Time.deltaTime);
        }
    }
}
