using UnityEngine.Timeline;
namespace NullBoundary.Demo
{
    [TrackColor(.25f,.57f,.62f)]
    [TrackBindingType(typeof(DemoIntro))]
    [TrackClipType(typeof(DemoIntroClip))]
    public sealed class DemoIntroTrack : TrackAsset { }
}

