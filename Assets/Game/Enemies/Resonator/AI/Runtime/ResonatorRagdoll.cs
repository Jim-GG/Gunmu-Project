using System;
using UnityEngine;
namespace Resonator.AI
{
    [Serializable] public sealed class RagdollBone
    {
        public Transform bone;
        public int parentIndex=-1;
        public float mass=4;
    }
    public sealed class ResonatorRagdoll : MonoBehaviour
    {
        public RagdollBone[] definitions=Array.Empty<RagdollBone>();
        [NonSerialized] public Rigidbody[] bodies=Array.Empty<Rigidbody>();
        public Collider[] boneColliders=Array.Empty<Collider>();
        public Collider livingCollider;
        public bool Active {get;private set;}
        void Awake()=>SetDormant();
        void OnEnable(){if(Active)ResonatorCrowd.RegisterCorpse(this);}
        void OnDisable()=>ResonatorCrowd.UnregisterCorpse(this);
        public void SetDormant()
        {
            ResonatorCrowd.UnregisterCorpse(this);
            Active=false;
            foreach(var c in boneColliders)if(c)c.enabled=false;
            foreach(var b in bodies)if(b){b.isKinematic=true;b.useGravity=false;b.interpolation=RigidbodyInterpolation.None;}
        }
        public void Activate(Vector3 impulse)
        {
            if(Active)return;Active=true;
            if(livingCollider)livingCollider.enabled=false;
            // Living bones have NO Rigidbody or joint. Physics interpolation on a
            // moving/scaled animation hierarchy otherwise fights the pose graph.
            bodies=new Rigidbody[definitions.Length];
            for(int i=0;i<definitions.Length;i++)
            {
                var d=definitions[i];var b=d.bone.gameObject.AddComponent<Rigidbody>();
                b.isKinematic=true;b.detectCollisions=false;b.mass=d.mass;b.useGravity=false;
                b.interpolation=RigidbodyInterpolation.None;b.solverIterations=10;b.solverVelocityIterations=8;bodies[i]=b;
            }
            for(int i=0;i<definitions.Length;i++)
            {
                int parent=definitions[i].parentIndex;if(parent<0)continue;
                var joint=definitions[i].bone.gameObject.AddComponent<CharacterJoint>();joint.connectedBody=bodies[parent];
                joint.enablePreprocessing=false;joint.enableProjection=true;joint.enableCollision=false;
                joint.lowTwistLimit=new SoftJointLimit{limit=-30};joint.highTwistLimit=new SoftJointLimit{limit=30};joint.swing1Limit=new SoftJointLimit{limit=50};joint.swing2Limit=new SoftJointLimit{limit=30};
            }
            foreach(var c in boneColliders)if(c)c.enabled=true;
            ResonatorCrowd.RegisterCorpse(this);
            foreach(var b in bodies)if(b){b.detectCollisions=true;b.isKinematic=false;b.useGravity=true;b.linearVelocity=Vector3.zero;b.interpolation=RigidbodyInterpolation.Interpolate;}
            if(bodies.Length>0&&bodies[0])bodies[0].AddForce(impulse,ForceMode.Impulse);
        }
        void OnDestroy()=>ResonatorCrowd.UnregisterCorpse(this);
    }
}
