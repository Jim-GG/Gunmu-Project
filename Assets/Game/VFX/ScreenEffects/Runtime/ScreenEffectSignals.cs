using System;
using UnityEngine;

namespace GameActions
{
    public readonly struct ScreenEffectSignal
    {
        public readonly ScreenCue cue;
        public readonly Vector3 position;
        public readonly float scale, radius;
        public readonly GameObject receiver;
        public ScreenEffectSignal(ScreenCue cue, Vector3 position, float scale, float radius, GameObject receiver)
        { this.cue = cue; this.position = position; this.scale = scale; this.radius = radius; this.receiver = receiver; }
    }

    /// <summary>有接收者的个人事件，或带距离衰减的世界事件。没有全局 Shader 属性。</summary>
    public static class ScreenEffectSignals
    {
        public static event Action<ScreenEffectSignal> Raised;
        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)] static void Reset() => Raised = null;
        public static void Emit(ScreenCue cue, Vector3 position, float scale = 1, float radius = 0, GameObject receiver = null)
            => Raised?.Invoke(new ScreenEffectSignal(cue, position, Mathf.Max(0, scale), Mathf.Max(0, radius), receiver));
    }

}
