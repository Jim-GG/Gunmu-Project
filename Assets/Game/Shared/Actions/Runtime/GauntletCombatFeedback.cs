using System.Collections.Generic;
using FirstPerson;
using Resonator.AI;
using UnityEngine;

namespace GameActions
{
    /// <summary>原版拳套的个人反馈。只冻结挥拳与活体受击动作，不冻结全局时间或布娃娃。</summary>
    [DisallowMultipleComponent]
    public sealed class GauntletCombatFeedback : MonoBehaviour
    {
        public CombatFeedbackProfile profile;
        GauntletCombat combat; AudioSource voice; bool bound;
        readonly HashSet<int> hitActors = new HashSet<int>();
        float freezeUntil,nextHurt;FirstPersonVitals vitals;
        public float LastHitStop { get; private set; }
        public int HitsPlayed { get; private set; }
        public int SwingsPlayed { get; private set; }
        public int GuardsPlayed { get; private set; }
        public AudioClip LastClip { get; private set; }
        public AudioClip LastHurtClip {get;private set;}
        public int HurtsPlayed {get;private set;}
        public bool IsStopped => Time.unscaledTime < freezeUntil;
        public static bool Stopped(GameObject actor) => actor && actor.TryGetComponent<GauntletCombatFeedback>(out var feedback) && feedback.IsStopped;
        public static GauntletCombatFeedback Ensure(GauntletCombat owner)
        {
            var c=owner.GetComponent<GauntletCombatFeedback>()??owner.gameObject.AddComponent<GauntletCombatFeedback>();c.Bind(owner);ScreenEffectsController.Ensure(owner.gameObject);return c;
        }
        void Awake()
        {
            profile=CombatFeedbackProfile.Load();var go=new GameObject("Gauntlet impact cues");go.transform.SetParent(transform,false);
            voice=go.AddComponent<AudioSource>();voice.playOnAwake=false;voice.spatialBlend=0;
        }
        void Bind(GauntletCombat owner)
        {
            if(combat==owner&&bound)return;Unbind();combat=owner;if(!combat||!isActiveAndEnabled)return;
            combat.AttackStarted+=Swing;combat.AttackBlocked+=Guard;combat.ContactResolved+=Contact;bound=true;
            vitals=owner.GetComponent<FirstPersonVitals>();if(vitals)vitals.Damaged+=Hurt;
        }
        void Swing(int punch)
        {
            hitActors.Clear();freezeUntil=0;
            if(punch==4&&profile&&profile.tierThreeSwing){LastClip=profile.tierThreeSwing;voice.PlayOneShot(LastClip,profile.swingVolume);SwingsPlayed++;}
        }
        void Guard()
        {
            if(profile&&profile.guardImpact){LastClip=profile.guardImpact;voice.PlayOneShot(LastClip,profile.guardVolume);GuardsPlayed++;}
            if(combat.LastGuard==GuardKind.Perfect)ScreenEffectSignals.Emit(ScreenCue.PerfectGuard,transform.position+transform.forward,receiver:gameObject);
        }
        void Hurt(float amount){if(amount>0)PlayHurt();}
        public void PlayHurt()
        {
            if(!profile||Time.unscaledTime<nextHurt)return;
            var clip=SurfaceAudio.Pick(profile.playerHurts);if(!clip)return;
            LastHurtClip=clip;voice.PlayOneShot(clip,profile.playerHurtVolume);nextHurt=Time.unscaledTime+profile.hurtSoundInterval;HurtsPlayed++;
        }
        void Contact(Collider collider,Vector3 point,Vector3 normal)
        {
            var enemy=collider?collider.GetComponentInParent<ResonatorEnemy>():null;
            if(!enemy||!combat||!hitActors.Add(enemy.GetInstanceID()))return;
            int tier=Mathf.Clamp(combat.ActivePunch-1,0,3);HitsPlayed++;
            LastHitStop=profile?profile.HitStop(tier):tier<2?.033f:tier==2?.05f:.078f;
            freezeUntil=Mathf.Max(freezeUntil,Time.unscaledTime+LastHitStop);enemy.FreezeHitReaction(LastHitStop);
            ScreenEffectSignals.Emit((ScreenCue)((int)ScreenCue.LightHit+tier),point,receiver:gameObject);
        }
        void OnEnable(){if(combat)Bind(combat);}
        void Unbind(){if(combat&&bound){combat.AttackStarted-=Swing;combat.AttackBlocked-=Guard;combat.ContactResolved-=Contact;}if(vitals)vitals.Damaged-=Hurt;vitals=null;bound=false;}
        void OnDisable(){Unbind();freezeUntil=0;if(voice)voice.Stop();}
        void OnDestroy()=>Unbind();
    }
}
