using UnityEngine;
namespace CrystalParkour
{
    [RequireComponent(typeof(BoxCollider))]
    public sealed class ParkourCheckpoint:MonoBehaviour
    {
        public Transform respawn;public string label="安全平台";public bool finish;bool reached;
        void Awake()=>GetComponent<BoxCollider>().isTrigger=true;
        void OnTriggerEnter(Collider other)
        {
            var p=other.GetComponentInParent<CrystalTraversal>();if(!p)return;if(finish){p.TryComplete();return;}if(reached)return;reached=true;p.SetCheckpoint(respawn?respawn.position:transform.position,respawn?respawn.rotation:Quaternion.identity,label);
        }
    }
}
