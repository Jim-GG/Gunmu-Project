using System.Collections;
using UnityEngine;

namespace Gauntlet
{
    /// <summary>Animation playback only. Combat, damage and hit windows remain owned by gameplay.</summary>
    public sealed class GauntletAnimationPlayer : MonoBehaviour
    {
        public Animator animator;
        public SkinnedMeshRenderer meshRenderer;
        [Range(0f, 0.15f)] public float blendSeconds = 0.04f;
        private Coroutine returnToIdle;
        private MaterialPropertyBlock properties;
        private static readonly int ChargeId = Shader.PropertyToID("_Charge");

        public void PlayClip(string state)
        {
            if (!animator || !animator.HasState(0, Animator.StringToHash(state)))
            {
                Debug.LogWarning("Gauntlet animation state not found: " + state, this);
                return;
            }
            if (returnToIdle != null) StopCoroutine(returnToIdle);
            animator.CrossFadeInFixedTime(state, blendSeconds, 0, 0f);
            SetCharge(state == "charge" ? 1f : 0f);
            if (state != "Idle" && state != "Parry-Keep" && state != "charge full")
                returnToIdle = StartCoroutine(ReturnAfter(state));
        }

        private IEnumerator ReturnAfter(string state)
        {
            // Wait for the new state to become current before reading its duration.
            yield return null;
            yield return new WaitForSeconds(blendSeconds + 0.01f);
            AnimatorStateInfo info = animator.GetCurrentAnimatorStateInfo(0);
            if (!info.IsName(state)) yield break;
            yield return new WaitForSeconds(Mathf.Max(0f, info.length - info.normalizedTime * info.length));
            animator.CrossFadeInFixedTime("Idle", blendSeconds, 0, 0f);
            SetCharge(0f);
            returnToIdle = null;
        }

        public void SetCharge(float amount)
        {
            if (!meshRenderer) return;
            if (properties == null) properties = new MaterialPropertyBlock();
            meshRenderer.GetPropertyBlock(properties, 0);
            properties.SetFloat(ChargeId, Mathf.Clamp01(amount));
            meshRenderer.SetPropertyBlock(properties, 0);
        }

        public void SuspendAutomaticPlayback()
        {if(returnToIdle!=null)StopCoroutine(returnToIdle);returnToIdle=null;}

        public void PlayControlled(string state, float blend = .025f)
        {
            if (returnToIdle != null) StopCoroutine(returnToIdle);
            returnToIdle = null;
            if (!animator || !animator.HasState(0, Animator.StringToHash(state))) { Debug.LogError("Missing gauntlet state: " + state, this); return; }
            animator.CrossFadeInFixedTime(state, blend, 0, 0f);
        }

        public void SeekControlled(string state, float seconds)
        {
            if (!animator || !animator.runtimeAnimatorController) return;
            foreach (var clip in animator.runtimeAnimatorController.animationClips)
            {
                if (clip.name != state) continue;
                if (returnToIdle != null) StopCoroutine(returnToIdle);
                returnToIdle = null;
                animator.Play(state, 0, seconds / Mathf.Max(.001f, clip.length));
                return;
            }
            Debug.LogError("Missing gauntlet clip: " + state, this);
        }

        private void OnDisable()
        {
            if (returnToIdle != null) StopCoroutine(returnToIdle);
            returnToIdle = null;
        }
    }
}
