using UnityEngine;
using GameActions;
namespace Resonator.AI
{
    /// <summary>每个骨骼只上报碰撞，整具尸体共用限流和一个声音源。</summary>
    public sealed class CorpseImpactAudio : MonoBehaviour
    {
        public ResonatorRagdoll owner;
        void OnCollisionEnter(Collision collision)
        {
            if(owner)owner.PlayCollision(collision.collider,collision.relativeVelocity.magnitude,collision.contactCount>0?collision.GetContact(0).point:transform.position);
        }
    }
}
