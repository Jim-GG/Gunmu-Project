using UnityEngine;
using UnityEngine.Playables;
namespace NullBoundary.Demo
{
    public sealed class DemoIntroClip : PlayableAsset
    {
        public double sequenceStart;
        public override Playable CreatePlayable(PlayableGraph graph,GameObject owner)
        {
            var playable=ScriptPlayable<DemoIntroBehaviour>.Create(graph);
            playable.GetBehaviour().start=sequenceStart;return playable;
        }
    }
    public sealed class DemoIntroBehaviour : PlayableBehaviour
    {
        public double start;
        public override void ProcessFrame(Playable playable,FrameData info,object playerData)
        {
            var intro=playerData as DemoIntro;
            if(intro && Application.isPlaying)intro.Evaluate((float)(start+playable.GetTime()));
        }
    }
}

