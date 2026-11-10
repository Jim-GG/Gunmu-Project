using UnityEngine;
using GameActions;
namespace Resonator.AI
{
    [DisallowMultipleComponent]
    public sealed class ResonatorVoice : MonoBehaviour
    {
        public AudioSource source;
        ResonatorEnemy enemy;CombatFeedbackProfile profile;float next;bool bound;
        public AudioClip LastClip {get;private set;}
        public int RoarsPlayed {get;private set;}
        public static ResonatorVoice Ensure(ResonatorEnemy actor)
        {var c=actor.GetComponent<ResonatorVoice>()??actor.gameObject.AddComponent<ResonatorVoice>();c.Bind(actor);return c;}
        void Awake()
        {
            profile=CombatFeedbackProfile.Load();var go=new GameObject("Resonator roar voice");go.transform.SetParent(transform,false);go.transform.localPosition=Vector3.up*1.3f;
            source=go.AddComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=1;source.minDistance=4;source.maxDistance=50;source.priority=70;
        }
        void Bind(ResonatorEnemy actor){if(enemy==actor&&bound)return;Unbind();enemy=actor;if(enemy&&isActiveAndEnabled){enemy.StateChanged+=State;bound=true;}}
        void State(EnemyState state)
        {
            if(state==EnemyState.Dying||state==EnemyState.Dead){source.Stop();return;}
            if(state!=EnemyState.Screaming||!profile||!profile.resonatorRoar||Time.time<next||source.isPlaying)return;
            LastClip=profile.resonatorRoar;source.PlayOneShot(LastClip,profile.roarVolume);next=Time.time+profile.roarInterval;RoarsPlayed++;
            ScreenEffectSignals.Emit(ScreenCue.ResonatorRoar,source.transform.position,1,profile.roarScreenRadius);
        }
        void Unbind(){if(enemy&&bound)enemy.StateChanged-=State;bound=false;}
        void OnEnable(){if(enemy)Bind(enemy);}
        void OnDisable(){Unbind();next=0;if(source)source.Stop();}
        void OnDestroy()=>Unbind();
    }
}
