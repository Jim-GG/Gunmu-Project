using System.Collections.Generic;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    // Playback is independent of the decision maker (player input or enemy AI).
    public sealed class ActionEventDispatcher
    {
        readonly Dictionary<Transform,AudioSource> sources=new Dictionary<Transform,AudioSource>();
        public void Dispatch(MotionFrameEvent marker,ActionHitbox sockets)
        {
            if(marker==null||sockets==null||!sockets.HasAnchor(marker.anchor,marker.customBone))return;
            var anchor=sockets.Anchor(marker.anchor,marker.customBone);
            Vector3 point=sockets.Point(marker.anchor,marker.customBone,marker.position);
            if(marker.kind==MotionEventKind.Sound&&marker.sound)
            {
                if(!sources.TryGetValue(anchor,out var source)||!source)
                {
                    var go=new GameObject("Action Audio");go.transform.SetParent(anchor,false);
                    source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=.5f;source.minDistance=5;source.maxDistance=80;sources[anchor]=source;
                }
                source.transform.localPosition=marker.position;source.PlayOneShot(marker.sound,Mathf.Clamp01(marker.value));
            }
            else if(marker.kind==MotionEventKind.Effect&&marker.effect)
            {
                var fx=Object.Instantiate(marker.effect,point,anchor.rotation*Quaternion.Euler(marker.rotation));
                if(marker.followAnchor)fx.transform.SetParent(anchor,true);
                Object.Destroy(fx,Mathf.Max(.01f,marker.effectLifetime));
            }
        }
    }
}
