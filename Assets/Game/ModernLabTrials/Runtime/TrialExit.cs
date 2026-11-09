using FirstPerson;
using UnityEngine;

namespace ModernLabTrials
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class TrialExit : MonoBehaviour
    {
        [SerializeField] private TrialLevel level;

        public void SetLevel(TrialLevel value) => level = value;

        private void Reset()
        {
            GetComponent<BoxCollider>().isTrigger = true;
        }

        private void Awake()
        {
            GetComponent<BoxCollider>().isTrigger = true;
            if (level == null) level = FindFirstObjectByType<TrialLevel>();
        }

        private void OnTriggerEnter(Collider other)
        {
            if (level == null) return;
            if (other.GetComponentInParent<FirstPersonMotor>() == null) return;
            level.TryExit();
        }
    }
}
