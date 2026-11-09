using UnityEngine;
using UnityEngine.AI;
namespace NullBoundary.Demo
{
    [RequireComponent(typeof(NavMeshAgent))]
    public sealed class DemoEnemy : MonoBehaviour
    {
        public Transform visual, leftArm, rightArm;
        public bool isChaser;
        public float health=100;
        public bool IsDown { get; private set; }
        public bool ActiveThreat { get; private set; }
        NavMeshAgent agent;
        Collider body;
        float attackClock=-1, recovery, hurtUntil, lastResonance;
        Vector3 knock;
        void Awake()
        {
            agent=GetComponent<NavMeshAgent>();body=GetComponent<Collider>();
            agent.enabled=false;
        }
        public void Restore(Vector3 pos, bool chaser)
        {
            if(!agent)agent=GetComponent<NavMeshAgent>();
            if(!body)body=GetComponent<Collider>();
            agent.enabled=false;transform.position=pos;health=100;IsDown=false;ActiveThreat=false;
            isChaser=chaser;body.enabled=true;attackClock=-1;knock=Vector3.zero;
            recovery=0;visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;
        }
        public void BeginThreat()
        {
            if (IsDown) return;
            visual.localPosition=Vector3.zero;visual.localRotation=Quaternion.identity;
            agent.enabled=true;
            if(NavMesh.SamplePosition(transform.position,out var sample,2,NavMesh.AllAreas))agent.Warp(sample.position);
            ActiveThreat=true;agent.speed=isChaser?3.7f:2.3f;
            agent.stoppingDistance=1.55f;
        }
        public void StopThreat()
        {
            ActiveThreat=false;attackClock=-1;
            if(agent.enabled&&agent.isOnNavMesh) { agent.isStopped=true;agent.ResetPath(); }
        }
        void Update()
        {
            var flow=DemoFlow.Instance;
            if(!flow||IsDown) return;
            if (!ActiveThreat)
            {
                if(isChaser && flow.Stage==DemoStage.Exploring)
                {
                    visual.localPosition=new Vector3(0,-.2f,0);
                    visual.localRotation=Quaternion.Euler(50,0,0);
                    if(rightArm)rightArm.localRotation=Quaternion.Euler(-50+Mathf.Sin(Time.time*7)*27,0,0);
                    if(Time.time-lastResonance>.85f && Vector3.Distance(transform.position,flow.player.transform.position)<20)
                    { lastResonance=Time.time;flow.sound.Play(DemoSound.Cue.Crystal,transform.position); }
                }
                return;
            }
            if(flow.InputBlocked||!flow.player.IsAlive)
            { if(agent.enabled&&agent.isOnNavMesh)agent.isStopped=true;return; }
            if(!agent.enabled||!agent.isOnNavMesh) return;
            agent.isStopped=false;
            if(knock.magnitude>.05f)
            { agent.Move(knock*Time.deltaTime);knock=Vector3.MoveTowards(knock,Vector3.zero,14*Time.deltaTime); }
            Vector3 player=flow.player.transform.position;
            // The door approach is a visible reach/attack position, not an invisible wall.
            Vector3 target=isChaser&&player.z>66.4f?new Vector3(0,0,64.8f):player;
            agent.SetDestination(target);
            float distance=Vector3.Distance(transform.position,player);
            if(Time.time<hurtUntil) { agent.isStopped=true;return; }
            recovery-=Time.deltaTime;
            if(attackClock>=0)
            {
                agent.isStopped=true;attackClock+=Time.deltaTime;
                if(rightArm)rightArm.localRotation=Quaternion.Euler(-130+attackClock*170,0,0);
                if(attackClock>=.85f)
                {
                    if(distance<2.3f && CanReach(flow.player) && !(isChaser&&flow.chaseDoor.IsClosed))
                        flow.player.TakeDamage(isChaser?34:18,transform.position);
                    attackClock=-1;recovery=isChaser?.85f:1.1f;
                }
            }
            else if(distance<2.1f&&recovery<=0&&CanReach(flow.player))attackClock=0;
            else
            {
                visual.localRotation=Quaternion.identity;
                if(leftArm)leftArm.localRotation=Quaternion.Euler(Mathf.Sin(Time.time*6)*20,0,0);
                if(rightArm)rightArm.localRotation=Quaternion.Euler(-Mathf.Sin(Time.time*6)*20,0,0);
            }
        }
        bool CanReach(DemoPlayer player)
        {
            Vector3 start=transform.position+Vector3.up*1.1f;
            Vector3 end=player.transform.position+Vector3.up*1.1f;
            return !Physics.Raycast(start,(end-start).normalized,out var hit,(end-start).magnitude,~0,QueryTriggerInteraction.Ignore)
                || hit.collider.GetComponentInParent<DemoPlayer>()!=null;
        }
        public void Hit(float damage, Vector3 direction, float force)
        {
            if(IsDown) return;
            health-=damage;hurtUntil=Time.time+.4f;attackClock=-1;
            knock=direction.normalized*force;
            if(health>0)return;
            IsDown=true;ActiveThreat=false;
            if(agent.enabled)agent.enabled=false;
            body.enabled=false;
            visual.localPosition=new Vector3(0,.36f,0);visual.localRotation=Quaternion.Euler(83,0,0);
            if(DemoFlow.Instance)DemoFlow.Instance.EnemyDown(this);
        }
    }
}

