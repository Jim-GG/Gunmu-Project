using UnityEngine;
using CrystalParkour;
using FirstPerson;
using UDK7M;
using Resonator.AI;
namespace GameActions
{
    public sealed class PlayerObjectAudio:MonoBehaviour
    {
        [Min(.1f)]public float stepDistance=1.5f;
        [Range(0,1)]public float stepVolume=.3f;
        [Tooltip("Use workbench Footstep / FootLeft / FootRight events instead of distance-based foot plants.")]
        public bool useAnimationEvents;
        public AudioSource stepSource;
        public int FootstepsPlayed{get;private set;}
        public SoundSurface LastGroundSurface{get;private set;}
        CrystalTraversal traversal;CharacterController body;ObjectAudio events;
        GauntletCombat combat;ResonatorEnemy enemy;Vector3 previous;float distance,fallSpeed;bool grounded;
        public void EnsureSource()
        {
            if(stepSource)return;var child=transform.Find("Footstep AudioSource");
            var go=child?child.gameObject:new GameObject("Footstep AudioSource");go.transform.SetParent(transform,false);
            stepSource=go.GetComponent<AudioSource>();if(!stepSource)stepSource=go.AddComponent<AudioSource>();
            stepSource.playOnAwake=false;stepSource.spatialBlend=.35f;stepSource.minDistance=1;stepSource.maxDistance=40;
        }
        void Start()
        {
            EnsureSource();body=GetComponent<CharacterController>();enemy=GetComponent<ResonatorEnemy>();
            traversal=GetComponent<CrystalTraversal>();events=GetComponent<ObjectAudio>();combat=GetComponent<GauntletCombat>();
            if(traversal){traversal.TetherStarted+=Tether;traversal.AttachedToSurface+=Attach;}
            if(combat){combat.AttackStarted+=Swing;combat.AttackBlocked+=Parry;combat.FrameEventReceived+=Frame;}
            if(enemy)enemy.FrameEventReceived+=Frame;
        }
        void OnEnable(){previous=transform.position;distance=fallSpeed=0;grounded=false;}
        void Tether(CrystalSurface s){if(events)events.Play(UDK7MSound.TetherStart,transform.position);}
        void Attach(CrystalSurface s){if(events)events.Play(UDK7MSound.TetherAttach,transform.position);}
        void Swing(int punch){if(events)events.Play(UDK7MSound.GauntletSwing,transform.position);}
        void Parry(){if(events)events.Play(UDK7MSound.ParryImpact,transform.position);}
        void Frame(MotionFrameEvent e){if(useAnimationEvents&&(e.name=="Footstep"||e.name=="FootLeft"||e.name=="FootRight"))Footstep();}
        bool Ground(out RaycastHit ground)
        {
            ground=default;float nearest=float.MaxValue;
            foreach(var hit in Physics.RaycastAll(transform.position+Vector3.up*.35f,Vector3.down,.85f,~0,QueryTriggerInteraction.Ignore))
                if(!hit.transform.IsChildOf(transform)&&hit.distance<nearest){nearest=hit.distance;ground=hit;}
            return nearest<float.MaxValue;
        }
        public void Footstep()
        {
            if(!Ground(out var hit))return;EnsureSource();LastGroundSurface=SurfaceAudio.Kind(hit.collider);
            var clip=SurfaceAudio.Pick(SurfaceAudio.Preset(hit.collider)?.steps);if(!clip)return;
            stepSource.transform.position=hit.point+Vector3.up*.04f;stepSource.PlayOneShot(clip,stepVolume);FootstepsPlayed++;
        }
        void Landing(float speed)
        {
            if(speed<2||!Ground(out var hit))return;EnsureSource();var preset=SurfaceAudio.Preset(hit.collider);
            var clip=SurfaceAudio.Pick(preset?.landings)??SurfaceAudio.Pick(preset?.steps);if(!clip)return;
            stepSource.transform.position=hit.point+Vector3.up*.04f;stepSource.PlayOneShot(clip,stepVolume*Mathf.Clamp(speed/7,.6f,1.5f));
        }
        void Update()
        {
            var delta=transform.position-previous;previous=transform.position;
            bool onGround=body?body.isGrounded:enemy&&enemy.ActionTargetAlive&&enemy.State!=EnemyState.Waking;
            TickStride(delta,onGround,Time.deltaTime);
        }
        public void TickStride(Vector3 delta,bool onGround,float dt)
        {
            if(!onGround){fallSpeed=Mathf.Max(fallSpeed,-delta.y/Mathf.Max(.001f,dt));distance=0;grounded=false;return;}
            if(!grounded){Landing(fallSpeed);fallSpeed=0;}grounded=true;
            if(useAnimationEvents)return;
            float travel=Vector3.ProjectOnPlane(delta,Vector3.up).magnitude;
            if(travel>3){distance=0;return;} // Warps/spawn placement are not footsteps.
            distance+=travel;
            if(distance>=Mathf.Max(.1f,stepDistance)){distance%=Mathf.Max(.1f,stepDistance);Footstep();}
        }
        void OnDisable(){if(stepSource)stepSource.Stop();distance=fallSpeed=0;}
        void OnDestroy()
        {
            if(traversal){traversal.TetherStarted-=Tether;traversal.AttachedToSurface-=Attach;}
            if(combat){combat.AttackStarted-=Swing;combat.AttackBlocked-=Parry;combat.FrameEventReceived-=Frame;}
            if(enemy)enemy.FrameEventReceived-=Frame;
        }
    }
}
