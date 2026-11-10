using UnityEngine;

namespace WindConeFX
{
    [DisallowMultipleComponent]
    public sealed class WindConeBurst : MonoBehaviour
    {
        public Renderer[] shells;
        [Min(0.05f)] public float duration = 0.42f;
        public bool playOnEnable;
        public bool loop;
        [Min(0)] public float loopPause = 0.55f;
        public bool deactivateOnComplete = true;
        [ColorUsage(true, true)] public Color tint = new Color(0.68f, 0.9f, 1f, 1f);
        [Range(0, 8)] public float intensity = 1.7f;
        [Tooltip("启用后由组件覆盖材质亮度/颜色；默认尊重 prefab 材质设置。") ]
        public bool overrideMaterialAppearance;
        MaterialPropertyBlock block;
        float age;
        bool playing;
        static readonly int Life = Shader.PropertyToID("_LifeAlpha");
        static readonly int Opacity = Shader.PropertyToID("_Opacity");
        static readonly int Progress = Shader.PropertyToID("_Progress");
        static readonly int Tint = Shader.PropertyToID("_TintColor");
        static readonly int Intensity = Shader.PropertyToID("_Intensity");

        void OnEnable() { if (playOnEnable) Begin(); }
        void OnDisable() { playing = false; }
        public void PlayDash(Vector3 point, Vector3 forward, float strength = 1f)
        {
            if (forward.sqrMagnitude < 0.00001f) forward = transform.forward;
            transform.SetPositionAndRotation(point, Quaternion.LookRotation(forward.normalized));
            transform.localScale = Vector3.one * Mathf.Clamp(strength, 0.25f, 3f);
            if (!gameObject.activeSelf) gameObject.SetActive(true);
            Begin();
        }
        [ContextMenu("Play wind burst")]
        public void PlayOnce() { if (!gameObject.activeSelf) gameObject.SetActive(true); Begin(); }
        void Begin() { age = 0; playing = true; SetNormalizedTime(0); }
        public void StopEffect() { playing = false; SetOpacity(0, 0); }
        void Update()
        {
            if (!playing) return;
            age += Time.deltaTime;
            float t = age / Mathf.Max(0.05f, duration);
            SetNormalizedTime(t);
            if (loop) { if (age >= duration + loopPause) age = 0; }
            else if (t >= 1f)
            {
                playing = false;
                if (deactivateOnComplete) gameObject.SetActive(false);
            }
        }
        public void SetNormalizedTime(float t)
        {
            t = Mathf.Clamp01(t);
            float envelope = Mathf.SmoothStep(0, 1, t / 0.12f) * (1 - Mathf.SmoothStep(0.48f, 1, t));
            SetOpacity(envelope, Mathf.SmoothStep(0, 0.7f, Mathf.InverseLerp(0.45f, 1, t)));
        }
        void SetOpacity(float opacity, float progress)
        {
            if (block == null) block = new MaterialPropertyBlock();
            if (shells == null) return;
            foreach (var shell in shells)
            {
                if (!shell) continue;
                shell.GetPropertyBlock(block);
                float baseOpacity = shell.sharedMaterial && shell.sharedMaterial.HasProperty(Opacity) ? shell.sharedMaterial.GetFloat(Opacity) : 1f;
                block.SetFloat(Life, 1); block.SetFloat(Opacity, opacity * baseOpacity); block.SetFloat(Progress, progress);
                var material = shell.sharedMaterial;
                block.SetColor(Tint, overrideMaterialAppearance ? tint : material && material.HasProperty(Tint) ? material.GetColor(Tint) : Color.white);
                block.SetFloat(Intensity, overrideMaterialAppearance ? intensity : material && material.HasProperty(Intensity) ? material.GetFloat(Intensity) : 1f);
                shell.SetPropertyBlock(block);
            }
        }
    }
}
