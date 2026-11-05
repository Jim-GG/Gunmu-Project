using System;
using UnityEngine;
namespace Resonator.AI
{
    // Signal / Effect 会被执行；Sound 仅保留 enum 值与字段以兼容已序列化的资产，运行时不再播放任何声音。
    public enum MotionEventKind { Signal, Sound, Effect }
    [Serializable] public sealed class MotionFrameEvent
    {
        public string name="AnimationEvent";
        [Min(0)] public float frame;
        public MotionEventKind kind;
        public string payload;
        public float value=1;
        public AudioClip sound;
        public GameObject effect;
        [Min(.01f)] public float effectLifetime=3;
        public HitboxAnchor anchor=HitboxAnchor.Root;
        public string customBone;
        public Vector3 position;
    }
}
