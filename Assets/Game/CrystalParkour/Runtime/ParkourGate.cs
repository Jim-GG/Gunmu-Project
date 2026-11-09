using UnityEngine;
namespace CrystalParkour
{
    public sealed class ParkourGate:MonoBehaviour
    {
        public CrystalGrowthSite requiredSite;public float openHeight=4,speed=3;Vector3 closed;
        void Awake()=>closed=transform.position;
        void Update(){if(requiredSite&&requiredSite.Finished)transform.position=Vector3.MoveTowards(transform.position,closed+Vector3.up*openHeight,speed*Time.deltaTime);}
    }
}
