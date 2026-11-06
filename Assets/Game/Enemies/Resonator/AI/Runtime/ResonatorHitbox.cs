using UnityEngine;
namespace Resonator.AI
{
    public enum HitboxShape { Box, Sphere, Capsule }
    public enum HitboxAnchor { Root, Head, Chest, LeftHand, RightHand, LeftFoot, RightFoot, CustomBone }
    public readonly struct HitboxPose
    {
        public readonly Vector3 centre,size;
        public readonly Quaternion rotation;
        public readonly float radius,height;
        public HitboxPose(Vector3 centre,Quaternion rotation,Vector3 size,float radius,float height)
        {this.centre=centre;this.rotation=rotation;this.size=size;this.radius=radius;this.height=height;}
        public Vector3 CapsuleOffset=>rotation*Vector3.up*Mathf.Max(0,height*.5f-radius);
    }
    public sealed class ResonatorHitbox : GameActions.ActionHitbox
    {public ResonatorHitbox(Transform root):base(root){} }
}
