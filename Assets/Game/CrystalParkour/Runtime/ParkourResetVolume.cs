using UnityEngine;
namespace CrystalParkour
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ParkourResetVolume:MonoBehaviour
    {
        void Awake()=>GetComponent<BoxCollider>().isTrigger=true;
        void OnTriggerEnter(Collider other){var player=other.GetComponentInParent<CrystalTraversal>();if(player)player.Respawn();}
    }
}
