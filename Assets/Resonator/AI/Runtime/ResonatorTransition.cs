using System;
using UnityEngine;
namespace Resonator.AI
{
    [Serializable] public sealed class ResonatorTransition
    {
        public string label="New transition";
        public bool enabled=true;
        public bool anySource;
        public EnemyState from, to;
        [Tooltip("Seconds, specific to this From -> To edge. Overrides the destination beat's default blend.")]
        [Range(0,.8f)] public float blend=.12f;
        [Tooltip("-1 uses destination beat start; otherwise skip directly to this source animation frame.")]
        public float entryFrame=-1;
        [Tooltip("Attack -> Screaming/CombatIdle: -1 exits as soon as the outcome is known; otherwise wait at least until this source frame. A miss cannot exit before all active hit windows close.")]
        public float exitFrame=-1;
        [Tooltip("An attack-to-attack edge may cancel before the source clip ends. Disable to use the normal combo ending.")]
        public bool automaticCombo;
        [Tooltip("Source frame at which a connected attack may cancel. -1 waits for clip end.")]
        public float cancelFrameOnHit=-1;
        [Tooltip("Source frame at which a missed attack may cancel. -1 waits for clip end.")]
        public float cancelFrameOnMiss=-1;
        public float CancelFrame(bool connected)=>connected?cancelFrameOnHit:cancelFrameOnMiss;
    }
}
