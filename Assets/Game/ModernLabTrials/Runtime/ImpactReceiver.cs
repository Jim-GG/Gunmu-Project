using System;
using UnityEngine;

namespace ModernLabTrials
{
    // A wall-mounted impact receiver. It only accepts a fast-moving ImpactCrate,
    // so the player has to solve it with the collision / punch mechanic.
    public sealed class ImpactReceiver : MonoBehaviour
    {
        [SerializeField, Min(0f)] private float minimumImpactSpeed = 4.5f;
        [SerializeField] private Color inactiveColor = new Color(1f, .38f, .12f);
        [SerializeField] private Color activeColor = new Color(.18f, 1f, .55f);
        [SerializeField] private Light indicator;
        [SerializeField] private Renderer[] panels;

        private bool active;
        private MaterialPropertyBlock block;

        public event Action<ImpactReceiver> Activated;
        public bool IsActive => active;

        public void Configure(Light light, Renderer[] visualPanels)
        {
            indicator = light;
            panels = visualPanels;
            ApplyVisuals();
        }

        private void Awake()
        {
            block = new MaterialPropertyBlock();
            if (panels == null || panels.Length == 0) panels = GetComponentsInChildren<Renderer>(true);
            ApplyVisuals();
        }

        private void OnCollisionEnter(Collision collision)
        {
            if (active) return;
            ImpactCrate crate = collision.collider.GetComponentInParent<ImpactCrate>();
            if (crate == null) return;
            if (collision.relativeVelocity.magnitude < minimumImpactSpeed) return;
            Activate();
        }

        public void Activate()
        {
            if (active) return;
            active = true;
            ApplyVisuals();
            Activated?.Invoke(this);
        }

        public void ResetReceiver()
        {
            active = false;
            ApplyVisuals();
        }

        private void ApplyVisuals()
        {
            if (block == null) block = new MaterialPropertyBlock();
            Color color = active ? activeColor : inactiveColor;
            if (panels != null)
            {
                for (int i = 0; i < panels.Length; i++)
                {
                    if (panels[i] == null) continue;
                    panels[i].GetPropertyBlock(block);
                    block.SetColor("_BaseColor", color);
                    block.SetColor("_Color", color);
                    block.SetColor("_EmissionColor", color * (active ? 2.8f : 1.1f));
                    panels[i].SetPropertyBlock(block);
                }
            }
            if (indicator != null)
            {
                indicator.color = color;
                indicator.intensity = active ? 4.2f : 1.4f;
            }
        }
    }
}
