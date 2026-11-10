using UnityEngine;
using Resonator.AI;

[RequireComponent(typeof(Rigidbody), typeof(SphereCollider))]
public sealed class CargoAttackDrone : MonoBehaviour, IPunchReceiver
{
    public enum Mode { Hunting, Launched, Wreck, Remote }
    public ResonatorTuning healthSource;
    public PlayerCombatController player;
    public CargoDroneLesson lesson;
    public Transform eye; public Transform[] rotors; public Material sparkMaterial; AudioSource audioSource; AudioClip warningTone, impactTone;
    public Light warning;
    [Min(1)] public float detectionRange = 24, approachSpeed = 2.3f, attackInterval = 4, attackWindup = 1.1f;
    public float flightRadius = .65f;
    [Header("朝向")]
    [Tooltip("模型头部的本地朝向；替换模型时可调整")]
    public Vector3 headLocalDirection = Vector3.forward;
    [Min(1)] public float turnSpeed = 240f;
    public Vector3 HeadForward => transform.rotation * headLocalDirection.normalized;
    public Transform patrolCenter;
    [Min(0)] public float patrolRadius = 1.2f;
    public bool CombatEnabled { get; set; } = true;
    public Mode State { get; private set; }
    public float Health { get; private set; }
    public bool IsFlesh => false;
    public bool Defeated => State == Mode.Wreck || State == Mode.Remote;
    Rigidbody body;
    float attackAt, fireAt, launchedUntil;
    Vector3 shotTarget;
    LineRenderer beam;
    float beamUntil;
    void Awake()
    {
        body=GetComponent<Rigidbody>(); body.isKinematic=true; body.useGravity=false;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousSpeculative;
        GetComponent<SphereCollider>().radius=flightRadius;
        Health=healthSource ? healthSource.maxHealth : 120;
        attackAt=Time.time+attackInterval;
        beam=GetComponent<LineRenderer>(); audioSource=gameObject.AddComponent<AudioSource>();audioSource.spatialBlend=1;audioSource.minDistance=3;audioSource.maxDistance=30;warningTone=Tone(880,.23f);impactTone=Tone(110,.4f);
    }
    public bool SeesPlayer()
    {
        if(!player) return false;
        Vector3 target=player.transform.position+Vector3.up;
        Vector3 delta=target-transform.position;
        return delta.magnitude<detectionRange && Physics.Raycast(transform.position,delta.normalized,out var hit,delta.magnitude+.2f,~0,QueryTriggerInteraction.Ignore)
            && hit.collider.GetComponentInParent<PlayerCombatController>()==player;
    }
    void Update()
    {
        if(beam) beam.enabled=Time.time<beamUntil; if(!GameplayGate.IsBlocked && State!=Mode.Wreck && rotors!=null) for(int i=0;i<rotors.Length;i++) if(rotors[i]) rotors[i].Rotate(Vector3.up,(i%2==0?1:-1)*1100*Time.deltaTime,Space.Self);
        if(GameplayGate.IsBlocked) return;
        if(State==Mode.Launched && Time.time>launchedUntil) { body.isKinematic=true;body.useGravity=false; State=Mode.Hunting; }
        if(State!=Mode.Hunting || !player || player.IsDefeated || (lesson && lesson.IsRemote)) return;
        bool visible=CombatEnabled && SeesPlayer();
        if(!visible){ fireAt=0; if(warning)warning.color=Color.yellow;return; }
        if(fireAt>0 && Time.time>=fireAt)
        {
            if(Physics.Raycast(transform.position,(shotTarget-transform.position).normalized,out var hit,detectionRange,~0,QueryTriggerInteraction.Ignore)
                && hit.collider.GetComponentInParent<PlayerCombatController>()==player) player.ReceiveAttack(transform.position,1);
            DrawBeam(shotTarget); fireAt=0; attackAt=Time.time+attackInterval;
        }
        if(fireAt==0 && Time.time>=attackAt){ fireAt=Time.time+attackWindup;shotTarget=player.transform.position+Vector3.up;audioSource.PlayOneShot(warningTone,.6f); }
        if(warning) warning.color=fireAt>0?Color.red:new Color(.8f,.3f,1);
    }
    void FixedUpdate()
    {
        if(GameplayGate.IsBlocked || State!=Mode.Hunting || !player || (lesson&&lesson.IsRemote)) return;
        if (!CombatEnabled || !SeesPlayer())
        {
            if (patrolCenter)
            {
                float angle = Time.time * .8f;
                Vector3 offset = new Vector3(Mathf.Cos(angle), 0, Mathf.Sin(angle)) * patrolRadius;
                Vector3 targetPosition = patrolCenter.position + offset;
                MoveSafe(Vector3.MoveTowards(body.position, targetPosition, approachSpeed * Time.fixedDeltaTime) - body.position);
            }
            return;
        }
        var delta=player.transform.position-transform.position;
        Vector3 flat=Vector3.ProjectOnPlane(delta,Vector3.up).normalized;
                Vector3 aim=player.transform.position+Vector3.up-transform.position;
        if(aim.sqrMagnitude>.001f && headLocalDirection.sqrMagnitude>.001f)
        {
            Quaternion targetRotation=Quaternion.LookRotation(aim.normalized,Vector3.up)*Quaternion.FromToRotation(headLocalDirection.normalized,Vector3.forward);
            body.MoveRotation(Quaternion.RotateTowards(body.rotation,targetRotation,turnSpeed*Time.fixedDeltaTime));
        }
        Vector3 target=player.transform.position-flat*2.3f+Vector3.up*1.65f;
        MoveSafe(Vector3.MoveTowards(body.position,target,approachSpeed*Time.fixedDeltaTime)-body.position);
    }
    public void MoveSafe(Vector3 delta)
    {
        if(delta.sqrMagnitude<.00001f)return;
        if(Physics.SphereCast(body.position,flightRadius,delta.normalized,out var hit,delta.magnitude+.08f,~0,QueryTriggerInteraction.Ignore))
            delta=delta.normalized*Mathf.Max(0,hit.distance-.08f);
        body.MovePosition(body.position+delta);
    }
    public void OnPunched(Vector3 point, Vector3 normal, Vector3 direction,int tier,float strength)
    {
        if(Defeated || GameplayGate.IsBlocked || !CombatEnabled)return;
        Health-=new Vector4(24,40,65,110)[Mathf.Clamp(tier,0,3)];
        fireAt=0;
        if(Health<=0){Crash();return;}
        if(tier<=0)return;
        State=Mode.Launched;launchedUntil=Time.time+2.5f;
        body.isKinematic=false;body.useGravity=false;
        body.collisionDetectionMode=CollisionDetectionMode.ContinuousDynamic;
        body.linearVelocity=direction.normalized*(11+6*tier);body.angularVelocity=Vector3.zero;
    }
    void OnCollisionEnter(Collision c)
    {
        if(State!=Mode.Launched || c.collider.isTrigger || c.collider.GetComponentInParent<PlayerMovement>() || c.collider.GetComponentInParent<CargoAttackDrone>())return;
        for(int i=0;i<c.contactCount;i++) if(Mathf.Abs(c.GetContact(i).normal.y)<.7f){Crash();break;}
    }
    public void Crash()
    {
        if(Defeated)return;
        Health=0;if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        body.isKinematic=false;body.useGravity=true;State=Mode.Wreck;
        if(warning)warning.color=Color.cyan;
        audioSource.PlayOneShot(impactTone,.8f);
        var fx=new GameObject("撞毁电火花").AddComponent<ParticleSystem>();fx.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);fx.transform.position=transform.position;
        var main=fx.main;main.loop=false;main.duration=.4f;main.startLifetime=.4f;main.startSpeed=6;main.startSize=.07f;main.startColor=new Color(.8f,.5f,1);main.gravityModifier=1;
        var emission=fx.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,35)});
        if(sparkMaterial)fx.GetComponent<ParticleSystemRenderer>().sharedMaterial=sparkMaterial;fx.Play();Destroy(fx.gameObject,1.5f);
        if(lesson)lesson.OnDroneDown(this);
    }
    public void SetRemote(bool active)
    {
        State=active?Mode.Remote:Mode.Wreck;
        if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        body.isKinematic=active;body.useGravity=!active;
        body.collisionDetectionMode=active?CollisionDetectionMode.ContinuousSpeculative:CollisionDetectionMode.ContinuousDynamic;
        transform.rotation=Quaternion.identity;
    }
    public void Dock(Vector3 point)
    {
        if(!body.isKinematic){body.linearVelocity=Vector3.zero;body.angularVelocity=Vector3.zero;}
        body.position=point; transform.position=point;
    }
    static AudioClip Tone(float hz,float seconds){int n=(int)(22050*seconds);var samples=new float[n];for(int i=0;i<n;i++){float t=i/22050f;samples[i]=Mathf.Sin(t*hz*6.28318f)*Mathf.Pow(1-i/(float)n,2)*.3f;}var clip=AudioClip.Create("无人机机械反馈",n,1,22050,false);clip.SetData(samples,0);return clip;}
    void OnDestroy(){if(warningTone)Destroy(warningTone);if(impactTone)Destroy(impactTone);}
    void DrawBeam(Vector3 end)
    {
        if(!beam)return;beam.positionCount=2;beam.SetPosition(0,transform.position);beam.SetPosition(1,end);beamUntil=Time.time+.12f;
    }
}



