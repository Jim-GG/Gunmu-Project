using System;
using UnityEngine;

namespace FirstPerson
{
    public sealed class FirstPersonVitals : MonoBehaviour
    {
        public float maxHealth = 100;
        public float Health { get; private set; }
        public event Action<float> Damaged;
        public event Action Died;
        private void Awake() => Restore();
        public void Restore() => Health = maxHealth;
        public void ReceiveDamage(float damage)
        {
            if (Health <= 0 || damage <= 0) return;
            Health = Mathf.Max(0, Health - damage);
            Damaged?.Invoke(damage);
            if (Health <= 0) Died?.Invoke();
        }
    }
}
