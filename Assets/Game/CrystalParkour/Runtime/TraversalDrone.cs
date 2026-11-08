using UnityEngine;
namespace CrystalParkour
{
    public enum DroneRoute { Shuttle, DescendingStream }
    [DefaultExecutionOrder(-20)]
    public sealed class TraversalDrone:MonoBehaviour
    {
        public Transform rotorLeft,rotorRight;public BoxCollider deck;
        public Vector3 startPoint,endPoint;
        public Transform startAnchor,endAnchor;
        public DroneRoute route;
        public float phaseOffset,respawnSeconds=2;
        public float speed=2.4f,dwellSeconds=2,hoverAmplitude=.07f;
        public bool moving=true;float phase;Vector3 previous;bool wasVisible=true;Renderer[] visuals;Light[] lamps;bool[] lampEnabled;
        void Start(){Physics.SyncTransforms();previous=transform.position;}
        void Update()=>Tick(Time.deltaTime,Time.timeSinceLevelLoad);
        public Vector3 PositionAt(double time,out bool visible)
        {
            var start=startAnchor?startAnchor.position:startPoint;var end=endAnchor?endAnchor.position:endPoint;
            float travel=Mathf.Max(.1f,Vector3.Distance(start,end)/Mathf.Max(.1f,speed));
            double period=travel+Mathf.Max(.1f,respawnSeconds);float part=(float)(((time+phaseOffset)%period+period)%period);visible=part<travel;
            return Vector3.Lerp(start,end,Mathf.Clamp01(part/travel));
        }
        void SetVisible(bool value)
        {
            if(visuals==null)visuals=GetComponentsInChildren<Renderer>(true);foreach(var r in visuals)r.enabled=value;
            if(lamps==null){lamps=GetComponentsInChildren<Light>(true);lampEnabled=new bool[lamps.Length];for(int i=0;i<lamps.Length;i++)lampEnabled[i]=lamps[i].enabled;}
            for(int i=0;i<lamps.Length;i++)if(lamps[i])lamps[i].enabled=value&&lampEnabled[i];
            if(deck)deck.enabled=value;
        }
        public void Tick(float dt,double time)
        {
            if(rotorLeft)rotorLeft.Rotate(Vector3.up,950*dt,Space.Self);if(rotorRight)rotorRight.Rotate(Vector3.up,-950*dt,Space.Self);
            bool visible=true;
            if(moving)
            {
                if(route==DroneRoute.DescendingStream)transform.position=PositionAt(time,out visible);
                else
                {var start=startAnchor?startAnchor.position:startPoint;var end=endAnchor?endAnchor.position:endPoint;float travel=Mathf.Max(.1f,Vector3.Distance(start,end)/Mathf.Max(.1f,speed));float half=travel+dwellSeconds;phase+=dt;float cycle=phase%(half*2);bool returning=cycle>=half;float part=returning?cycle-half:cycle;float t=Mathf.Clamp01((part-dwellSeconds)/travel);if(returning)t=1-t;transform.position=Vector3.Lerp(start,end,t)+Vector3.up*Mathf.Sin((float)time*1.4f)*hoverAmplitude;}
            }
            bool resetJump=route==DroneRoute.DescendingStream&&(!visible||!wasVisible||Vector3.Distance(previous,transform.position)>Mathf.Max(2,speed*dt*3));
            if(route==DroneRoute.DescendingStream)SetVisible(visible);wasVisible=visible;
            var delta=transform.position-previous;previous=transform.position;
            if(delta.sqrMagnitude>.0000001f)Physics.SyncTransforms();
            if(!deck||resetJump)return;var bounds=deck.bounds;
            foreach(var p in FindObjectsByType<CrystalTraversal>(FindObjectsSortMode.None))
            {
                if(p.State!=CrystalTraversal.Mode.Free||p.motor.Velocity.y>.5f)continue;var foot=p.transform.position;
                if(Mathf.Abs(foot.y-(bounds.max.y-delta.y))>.24f||foot.x<bounds.min.x-.1f||foot.x>bounds.max.x+.1f||foot.z<bounds.min.z-.1f||foot.z>bounds.max.z+.1f)continue;
                p.motor.Body.Move(delta);p.RodeDrone=true;
            }
        }
        public bool Supports(Vector3 foot){if(!deck)return false;var b=deck.bounds;return Mathf.Abs(foot.y-b.max.y)<.25f&&foot.x>=b.min.x&&foot.x<=b.max.x&&foot.z>=b.min.z&&foot.z<=b.max.z;}
    }
}
