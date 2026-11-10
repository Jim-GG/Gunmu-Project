using UnityEngine;
namespace GameActions
{
    /// <summary>可直接挂给机关、UnityEvent 或动画事件的信号发送器。</summary>
    public sealed class ScreenEffectEmitter : MonoBehaviour
    {
        public ScreenCue cue = ScreenCue.Impact;
        [Min(0)] public float radius = 20, strength = 1;
        public GameObject receiver;
        public void Emit() => ScreenEffectSignals.Emit(cue, transform.position, strength, radius, receiver);
        public void EmitAt(Vector3 point) => ScreenEffectSignals.Emit(cue, point, strength, radius, receiver);
    }
}
