using UnityEngine;
namespace CrystalParkour
{
    public sealed class WindFanRotor:MonoBehaviour
    {
        public Vector3 localAxis=Vector3.forward;public float degreesPerSecond=240;
        void Update()=>transform.Rotate(localAxis, degreesPerSecond*Time.deltaTime,Space.Self);
    }
}
