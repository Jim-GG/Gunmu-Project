using System;
using System.Linq;
using UnityEngine;

namespace UDK7M
{
    // Exported links are rigid bones on a closed loop. Advance their cached loop
    // by actual chassis travel, independently of the arm's animation clock.
    public sealed class UDK7MTracks
    {
        sealed class Belt
        {
            public Transform[] bones;
            public Vector3[] positions, scales;
            public Quaternion[] rotations;
            public float pitch, direction, lateral;
            public double phase;
        }
        readonly Transform owner;
        readonly Belt[] belts;
        Vector3 previousPosition;
        Quaternion previousRotation;
        public UDK7MTracks(Transform owner)
        {
            this.owner=owner;
            belts=new[]{Build("Tread_L_"),Build("Tread_R_")};
            Reset();
        }
        Belt Build(string prefix)
        {
            var bones=owner.GetComponentsInChildren<Transform>(true).Where(t=>t.name.StartsWith(prefix,StringComparison.Ordinal)).OrderBy(t=>t.name,StringComparer.Ordinal).ToArray();
            var belt=new Belt{bones=bones,positions=bones.Select(t=>t.localPosition).ToArray(),rotations=bones.Select(t=>t.localRotation).ToArray(),scales=bones.Select(t=>t.localScale).ToArray()};
            if(bones.Length<3)return belt;
            var points=bones.Select(t=>owner.InverseTransformPoint(t.position)).ToArray();
            float bottom=points.Min(p=>p.y),top=points.Max(p=>p.y),tangent=0,total=0;
            for(int i=0;i<bones.Length;i++)
            {
                var next=points[(i+1)%bones.Length];total+=Vector3.Distance(bones[i].position,bones[(i+1)%bones.Length].position);
                if(points[i].y<Mathf.Lerp(bottom,top,.2f)&&next.y<Mathf.Lerp(bottom,top,.2f))tangent+=next.z-points[i].z;
            }
            belt.pitch=Mathf.Max(.001f,total/bones.Length);belt.direction=tangent<0?1:-1;
            belt.lateral=points.Average(p=>p.x)*owner.lossyScale.x;
            return belt;
        }
        public void Reset(){previousPosition=owner.position;previousRotation=owner.rotation;foreach(var b in belts)b.phase=0;}
        public void Apply(float distanceMultiplier=1)
        {
            float distance=Vector3.Dot(owner.position-previousPosition,owner.forward);
            float yaw=Mathf.DeltaAngle(previousRotation.eulerAngles.y,owner.eulerAngles.y)*Mathf.Deg2Rad;
            foreach(var b in belts)
            {
                if(b.bones.Length<3)continue;
                b.phase+=(distance-yaw*b.lateral)*distanceMultiplier*b.direction/b.pitch;
                for(int i=0;i<b.bones.Length;i++)
                {
                    double coordinate=i+b.phase;int whole=(int)Math.Floor(coordinate);float blend=(float)(coordinate-Math.Floor(coordinate));
                    int a=((whole%b.bones.Length)+b.bones.Length)%b.bones.Length,c=(a+1)%b.bones.Length;
                    b.bones[i].SetLocalPositionAndRotation(Vector3.Lerp(b.positions[a],b.positions[c],blend),Quaternion.Slerp(b.rotations[a],b.rotations[c],blend));
                    b.bones[i].localScale=Vector3.Lerp(b.scales[a],b.scales[c],blend);
                }
            }
            previousPosition=owner.position;previousRotation=owner.rotation;
        }
    }
}
