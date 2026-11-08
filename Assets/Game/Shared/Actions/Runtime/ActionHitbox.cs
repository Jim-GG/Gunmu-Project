using System.Collections.Generic;
using UnityEngine;
using Resonator.AI;
namespace GameActions
{
    // Shared by the runtime physics queries, preview and scene handles.
    // Bone scale in the imported FBX is deliberately excluded: sizes are metres.
    public class ActionHitbox
    {
        readonly Transform root;
        readonly Dictionary<HitboxAnchor,Transform> bindings=new Dictionary<HitboxAnchor,Transform>();
        public void Bind(HitboxAnchor anchor,Transform value){if(value)bindings[anchor]=value;else bindings.Remove(anchor);}
        readonly Dictionary<string,Transform> bones=new Dictionary<string,Transform>();
        readonly HashSet<string> ambiguous=new HashSet<string>();
        readonly Collider[] overlaps=new Collider[128];
        public ActionHitbox(Transform root)
        {this.root=root;foreach(var t in root.GetComponentsInChildren<Transform>(true)){if(bones.ContainsKey(t.name))ambiguous.Add(t.name);else bones.Add(t.name,t);}}
        public Transform FindBone(string path)
        {
            if(string.IsNullOrEmpty(path))return null;
            var exact=root.Find(path);if(exact)return exact;
            if(path.Contains("/")){Transform found=null;foreach(var t in root.GetComponentsInChildren<Transform>(true)){string relative="";var p=t;while(p&&p!=root){relative=p.name+(relative.Length>0?"/"+relative:"");p=p.parent;}if(!relative.EndsWith("/"+path))continue;if(found)return null;found=t;}return found;}
            return !ambiguous.Contains(path)&&bones.TryGetValue(path,out var bone)?bone:null;
        }
        public void BindProfile(ActionSet profile)
        {
            if(!profile||profile.sockets==null)return;
            foreach(var socket in profile.sockets)if(socket!=null&&!string.IsNullOrEmpty(socket.transformName))
            {var t=FindBone(socket.transformName);if(t)Bind(socket.role,t);}
        }
        public Transform Anchor(HitboxAnchor anchor,string custom=null)
        {
            if(anchor==HitboxAnchor.CustomBone&&!string.IsNullOrEmpty(custom))return FindBone(custom);
            if(bindings.TryGetValue(anchor,out var binding)&&binding)return binding;
            string name=null;
            switch(anchor)
            {
                case HitboxAnchor.Head:name="ValveBiped.Bip01_Head1";break;
                case HitboxAnchor.Chest:name="ValveBiped.Bip01_Spine2";break;
                case HitboxAnchor.LeftHand:name="ValveBiped.Bip01_L_Hand";break;
                case HitboxAnchor.RightHand:name="ValveBiped.Bip01_R_Hand";break;
                case HitboxAnchor.LeftFoot:name="ValveBiped.Bip01_L_Foot";break;
                case HitboxAnchor.RightFoot:name="ValveBiped.Bip01_R_Foot";break;
                case HitboxAnchor.CustomBone:name=custom;break;
            }
            return anchor==HitboxAnchor.Root?root:FindBone(name);
        }
        public bool HasAnchor(HitboxAnchor anchor,string custom)=>Anchor(anchor,custom)!=null;
        public Vector3 Point(HitboxAnchor anchor,string custom,Vector3 offset)
        {var a=Anchor(anchor,custom);return a?a.position+a.rotation*offset:root.position;}
        public HitboxPose Pose(HitWindow hit)
        {
            var a=Anchor(hit.anchor,hit.customBone);
            if(!a)return new HitboxPose(root.position,Quaternion.identity,Vector3.zero,0,0);
            return new HitboxPose(a.position+a.rotation*hit.position,a.rotation*Quaternion.Euler(hit.rotation),
                new Vector3(Mathf.Max(.005f,Mathf.Abs(hit.size.x)),Mathf.Max(.005f,Mathf.Abs(hit.size.y)),Mathf.Max(.005f,Mathf.Abs(hit.size.z))),
                Mathf.Max(.005f,hit.radius),Mathf.Max(hit.radius*2,hit.height));
        }
        public int Query(HitWindow hit,out Collider[] results)
        {
            results=overlaps;if(!HasAnchor(hit.anchor,hit.customBone))return 0;
            var p=Pose(hit);int count;
            switch(hit.shape)
            {
                case HitboxShape.Sphere:count=Physics.OverlapSphereNonAlloc(p.centre,p.radius,overlaps,hit.targetLayers,QueryTriggerInteraction.Collide);break;
                case HitboxShape.Capsule:var d=p.CapsuleOffset;count=Physics.OverlapCapsuleNonAlloc(p.centre-d,p.centre+d,p.radius,overlaps,hit.targetLayers,QueryTriggerInteraction.Collide);break;
                default:count=Physics.OverlapBoxNonAlloc(p.centre,p.size*.5f,overlaps,p.rotation,hit.targetLayers,QueryTriggerInteraction.Collide);break;
            }
            if(count==overlaps.Length)
            {
                // Extremely crowded scenes must not silently lose a target at the buffer limit.
                Collider[] all;
                switch(hit.shape)
                {
                    case HitboxShape.Sphere:all=Physics.OverlapSphere(p.centre,p.radius,hit.targetLayers,QueryTriggerInteraction.Collide);break;
                    case HitboxShape.Capsule:var d=p.CapsuleOffset;all=Physics.OverlapCapsule(p.centre-d,p.centre+d,p.radius,hit.targetLayers,QueryTriggerInteraction.Collide);break;
                    default:all=Physics.OverlapBox(p.centre,p.size*.5f,p.rotation,hit.targetLayers,QueryTriggerInteraction.Collide);break;
                }
                results=all;return all.Length;
            }
            return count;
        }
        public bool Contains(HitWindow hit,ResonatorTarget target)
        {
            if(!target)return false;int count=Query(hit,out var results);
            for(int i=0;i<count;i++)if(results[i].GetComponentInParent<ResonatorTarget>()==target)return true;
            return false;
        }
        public static void DrawGizmo(HitWindow h,HitboxPose p,Color colour)
        {
            var matrix=Gizmos.matrix;var color=Gizmos.color;Gizmos.color=colour;
            Gizmos.matrix=Matrix4x4.TRS(p.centre,p.rotation,Vector3.one);
            switch(h.shape)
            {
                case HitboxShape.Box:Gizmos.DrawWireCube(Vector3.zero,p.size);break;
                case HitboxShape.Sphere:Gizmos.DrawWireSphere(Vector3.zero,p.radius);break;
                default:
                    var d=Vector3.up*Mathf.Max(0,p.height*.5f-p.radius);Gizmos.DrawWireSphere(d,p.radius);Gizmos.DrawWireSphere(-d,p.radius);
                    foreach(var side in new[]{Vector3.right,Vector3.left,Vector3.forward,Vector3.back})Gizmos.DrawLine(d+side*p.radius,-d+side*p.radius);break;
            }
            Gizmos.matrix=matrix;Gizmos.color=color;
        }
    }
}
