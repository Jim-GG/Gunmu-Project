using UnityEngine;
using GameActions;
namespace CrystalParkour
{
    public sealed class CrystalGrowthSite:MonoBehaviour,IActionTarget
    {
        public bool wallGrowth;
        public GameObject growthRoot;
        public Collider strikeCollider;
        public Vector3 growthDirection=Vector3.up;
        public float duration=.65f,emergenceDistance=.6f,groundLift=.78f;
        public bool HasGrown{get;private set;}
        public bool Finished{get;private set;}
        public bool ActionTargetAlive=>isActiveAndEnabled&&!HasGrown;
        public Transform ActionRoot=>transform;
        public Vector3 ActionAimPoint=>strikeCollider?strikeCollider.bounds.center:transform.position;
        float time,lifted,liftAmount;Vector3 restPosition,restScale;Collider[] colliders;CrystalTraversal striker;Bounds finalPlatform;
        void Awake()
        {
            if(!strikeCollider)strikeCollider=GetComponent<Collider>();if(!growthRoot)return;
            restPosition=growthRoot.transform.localPosition;restScale=growthRoot.transform.localScale;colliders=growthRoot.GetComponentsInChildren<Collider>(true);
            bool first=true;foreach(var c in colliders)if(c is BoxCollider box)for(int i=0;i<8;i++)
            {var corner=box.center+Vector3.Scale(box.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));var world=box.transform.TransformPoint(corner);if(first){finalPlatform=new Bounds(world,Vector3.zero);first=false;}else finalPlatform.Encapsulate(world);}
            growthRoot.SetActive(false);
        }
        public void ReceiveActionHit(float damage,Vector3 source,float impulse)
        {
            if(HasGrown||!growthRoot)return;striker=CrystalTraversal.Find(source);if(!striker)return;
            HasGrown=true;time=lifted=0;if(wallGrowth)striker.GrewWall=true;else striker.GrewFloor=true;
            var foot=striker.transform.position;bool under=foot.x>=finalPlatform.min.x-.2f&&foot.x<=finalPlatform.max.x+.2f&&foot.z>=finalPlatform.min.z-.2f&&foot.z<=finalPlatform.max.z+.2f;
            liftAmount=under?Mathf.Max(wallGrowth?0:groundLift,finalPlatform.max.y+striker.motor.Body.skinWidth-foot.y):0;
            foreach(var c in colliders)c.enabled=false;growthRoot.SetActive(true);if(strikeCollider)strikeCollider.enabled=false;
            ApplyGrowth(0);
        }
        void Update()=>Tick(Time.deltaTime);
        public void Tick(float dt){if(!HasGrown||Finished)return;time+=dt;ApplyGrowth(Mathf.Clamp01(time/Mathf.Max(.05f,duration)));}
        void ApplyGrowth(float t)
        {
            float eased=1-Mathf.Pow(1-t,3);growthRoot.transform.localScale=restScale*Mathf.Lerp(.04f,1,eased);growthRoot.transform.localPosition=restPosition-growthDirection.normalized*emergenceDistance*(1-eased);
            if(striker&&liftAmount>0&&(striker.transform.position-transform.position).sqrMagnitude<16)
            {float rise=liftAmount*eased;striker.motor.Body.Move(Vector3.up*Mathf.Max(0,rise-lifted));lifted=rise;}
            if(t>=1){Finished=true;foreach(var c in colliders)c.enabled=true;}
        }
    }
}
