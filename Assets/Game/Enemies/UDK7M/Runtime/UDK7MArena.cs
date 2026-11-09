using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using Resonator.AI;
using FirstPerson;
namespace UDK7M
{
    [DefaultExecutionOrder(100)]public sealed class UDK7MArena:MonoBehaviour
    {
        public UDK7MTuning tuning;public UDK7MBoss boss;public UDK7MMinions minions;public ResonatorTarget player;public Transform checkpoint;public UDK7MCrystalPillar[] pillars;
        public UDK7MFeedback feedback;
        public GameObject localDebrisPrefab;
        public AudioClip[] warningSounds;
        public Material rockMaterial,crystalMaterial,warningMaterial;public float ceiling=22;public bool Started {get;private set;}public bool Won {get;private set;}
        readonly List<UDK7MDebris> debris=new List<UDK7MDebris>();readonly List<GameObject> warningObjects=new List<GameObject>();int epoch;float retryAt;GUIStyle text;
        void Start()
        {
            if(!feedback)feedback=GetComponent<UDK7MFeedback>();if(!feedback)feedback=gameObject.AddComponent<UDK7MFeedback>();feedback.tuning=tuning;
            if(!feedback.audio)feedback.audio=GetComponent<UDK7MAudio>();if(feedback.audio)feedback.audio.arena=this;
            boss.arena=this;minions.arena=this;boss.Initialize();if(player.vitals)player.vitals.Died+=PlayerDied;
            var traversal=player.GetComponent<CrystalParkour.CrystalTraversal>();if(traversal)traversal.SetCheckpoint(checkpoint.position,checkpoint.rotation,"碰撞实验室入口");
            // Raycasts still see the anchor, but its decorative geometry must not
            // trap the player's capsule during an upward escape rebound.
            foreach(var surface in gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CrystalParkour.CrystalSurface>(true)).Where(s=>s.gameObject.layer==LayerMask.NameToLayer("ArenaLowerFloor")))foreach(var c in surface.GetComponentsInChildren<Collider>())Physics.IgnoreCollision(player.GetComponent<CharacterController>(),c,true);
        }
        void OnDestroy(){if(player&&player.vitals)player.vitals.Died-=PlayerDied;}
        void PlayerDied(){retryAt=Time.time+1.3f;}
        void Update(){if(Keyboard.current!=null&&Keyboard.current.rKey.wasPressedThisFrame||retryAt>0&&Time.time>=retryAt)ResetEncounter();}
        public void BeginFight(){if(Started||Won)return;Started=true;boss.Engage();}
        public void PillarResonated(){if(boss.Phase!=MachinePhase.Stable||boss.State==BossState.OverloadCharge)return;foreach(var p in pillars){p.Shatter(boss.DrillTip);feedback.Sound(UDK7MSound.PillarShatter,p.ContactPoint(boss.DrillTip),p.transform);}boss.PillarOverload();}
        public void ResetPopulationForCycle(){minions.StopAndClear();ClearDebris();foreach(var p in gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MPureCrystal>(true)))p.ResetCrystal();}
        void ClearDebris(){epoch++;StopAllCoroutines();ClearWarnings();foreach(var d in debris)if(d)d.gameObject.SetActive(false);}
        public bool GroundImpact(Vector3 point)
        {
            if(!boss.GroundPound(point))return false;feedback.Wave(point,tuning.groundPoundRadius,1.25f);feedback.Shake(point,tuning.groundPoundShake,.8f);feedback.Sound(UDK7MSound.GroundPoundImpact,point,player.transform);foreach(var p in pillars){p.Regrow();feedback.Sound(UDK7MSound.PillarRegrow,p.transform.position,p.transform);}
            foreach(var e in minions.Actors.Where(e=>e&&e.ActionTargetAlive).ToArray())e.ReceiveActionDamage(new GameActions.ActionDamage(130,point,4,.3f,cause:GameActions.DamageCause.Resonance));return true;
        }
        public UDK7MCrystalPillar AvailablePillar(Vector3 origin)=>pillars.Where(p=>p&&!p.Consumed).OrderBy(p=>(p.transform.position-origin).sqrMagnitude).FirstOrDefault();
        void ClearWarnings(){foreach(var w in warningObjects)if(w)Destroy(w);warningObjects.Clear();}
        public void BossDefeated(){Won=true;epoch++;StopAllCoroutines();ClearWarnings();minions.StopAndClear();if(feedback.audio)feedback.audio.StopLoops();foreach(var d in debris)if(d)d.gameObject.SetActive(false);}
        public void ResetEncounter()
        {
            if(feedback)feedback.ClearWaves();
            if(feedback&&feedback.audio)feedback.audio.StopAll();
            retryAt=0;epoch++;StopAllCoroutines();ClearWarnings();Started=Won=false;minions.StopAndClear();foreach(var d in debris)if(d)d.gameObject.SetActive(false);foreach(var p in pillars)p.ResetPillar();
            player.gauntlet.Cancel();player.gauntlet.ResetResonance();var traversal=player.GetComponent<CrystalParkour.CrystalTraversal>();if(traversal)traversal.Drop("返回实验室入口");var motor=player.GetComponent<FirstPersonMotor>();var body=motor.Body;body.enabled=false;player.transform.SetPositionAndRotation(checkpoint.position,checkpoint.rotation);body.enabled=true;motor.StopMomentum();motor.SetLookRotation(checkpoint.rotation);player.vitals.Restore();boss.runner.motion.animator.enabled=true;boss.ResetBoss();
            var pound=player.GetComponent<UDK7MGroundPound>();if(pound)pound.ResetPound();foreach(var p in gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MPureCrystal>(true)))p.ResetCrystal();
        }
        public void DropRocks(){if(Won)return;StartCoroutine(Rocks(epoch,false));}
        public void DropCentralCrystal(){if(Won)return;StartCoroutine(Rocks(epoch,true));}
        IEnumerator Rocks(int generation,bool central)
        {
            int count=central?1:5;var points=new Vector3[count];var warnings=new GameObject[count];
            for(int i=0;i<count;i++)
            {
                var p=central?Vector3.zero:i<2?player.transform.position+Quaternion.Euler(0,i*180+Random.Range(-45,45),0)*Vector3.forward*1.2f:new Vector3(Random.Range(-tuning.arenaRadius*.8f,tuning.arenaRadius*.8f),0,Random.Range(-tuning.arenaRadius*.8f,tuning.arenaRadius*.8f));p.y=0;
                if(NavMesh.SamplePosition(p,out var nav,5,NavMesh.AllAreas))p=nav.position;
                if(Vector3.ProjectOnPlane(p,Vector3.up).magnitude>tuning.arenaRadius-3)p=Vector3.ProjectOnPlane(p,Vector3.up).normalized*(tuning.arenaRadius-4);
                points[i]=p;var warn=GameObject.CreatePrimitive(PrimitiveType.Cylinder);Destroy(warn.GetComponent<Collider>());warn.name="Locked falling debris warning";warn.transform.position=p+Vector3.up*.03f;warn.transform.localScale=new Vector3(2,.015f,2);warn.GetComponent<Renderer>().sharedMaterial=warningMaterial;warn.transform.SetParent(transform);warnings[i]=warn;warningObjects.Add(warn);
                var warningAudio=warn.AddComponent<GameActions.ObjectAudio>();warningAudio.sounds=new[]{new GameActions.ObjectSound{cue=UDK7MSound.RockWarning,clips=warningSounds,volume=.25f}};feedback.Sound(UDK7MSound.RockWarning,p,warn.transform);
            }
            yield return new WaitForSeconds(tuning.rockWarningSeconds);
            for(int i=0;i<count;i++)
            {
                if(warnings[i]){warningObjects.Remove(warnings[i]);Destroy(warnings[i]);}if(generation!=epoch||Won)continue;
                float height=ceiling;foreach(var hit in Physics.RaycastAll(points[i]+Vector3.up*2,Vector3.up,ceiling+8,~0,QueryTriggerInteraction.Ignore))if(hit.normal.y<-.1f&&hit.collider.gameObject.scene==gameObject.scene&&!hit.collider.GetComponentInParent<UDK7MBoss>())height=Mathf.Min(height,hit.point.y-.8f);
                var d=GetDebris();d.Begin(this,new Vector3(points[i].x,height,points[i].z),central,central);
            }
        }
        UDK7MDebris GetDebris()
        {
            var d=debris.FirstOrDefault(x=>x&&!x.gameObject.activeSelf);
            if(!d)
            {
                if(debris.Count>=30){d=debris.OrderBy(x=>x.SpawnTime).First();d.gameObject.SetActive(false);}
                else{var go=localDebrisPrefab?Instantiate(localDebrisPrefab,transform):GameObject.CreatePrimitive(PrimitiveType.Cube);go.name="Pooled falling debris";go.transform.SetParent(transform);if(!go.GetComponent<Rigidbody>())go.AddComponent<Rigidbody>();d=go.GetComponent<UDK7MDebris>();if(!d)d=go.AddComponent<UDK7MDebris>();debris.Add(d);go.SetActive(false);}
            }
            return d;
        }
        void OnGUI()
        {
            if(!player||!boss)return;if(text==null){text=new GUIStyle(GUI.skin.label){fontSize=17,wordWrap=true};text.normal.textColor=Color.white;}
            float width=Mathf.Min(700,Screen.width-40);GUI.Box(new Rect((Screen.width-width)/2,16,width,62),GUIContent.none);GUI.Label(new Rect((Screen.width-width)/2+12,20,width-24,26),"УДК-7М · 共振碰撞实验装置",text);
            var rect=new Rect((Screen.width-width)/2+12,51,width-24,12);GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.18f,.15f,.13f),0,0);rect.width*=boss.Health/tuning.maxHealth;GUI.DrawTexture(rect,Texture2D.whiteTexture,ScaleMode.StretchToFill,false,0,new Color(.85f,.36f,.08f),0,0);
            string hint=Won?"装置失效 · 实验区已解除封锁":!Started?"进入实验区。诱导装置撞击晶柱。":boss.Phase==MachinePhase.Stable?"一形态：引导冲锋撞晶柱；侧面晶体可牵引反弹逃离。":boss.Phase==MachinePhase.Overloaded?"二形态：三级蓄力击杀共鸣者，把飞行中的尸体送向钻头。":"三形态：中央晶体反弹 → 柱顶纯晶体 → 空中松开左键，落地共振冲击。";
            GUI.Box(new Rect(18,Screen.height-112,Mathf.Min(980,Screen.width-36),94),GUIContent.none);GUI.Label(new Rect(30,Screen.height-106,960,88),hint+"\n循环 "+boss.CompletedCycles+"/"+tuning.cyclesToDefeat+"  共鸣者 "+minions.Living+"/"+tuning.livingMinions+"  共振剩余 "+player.gauntlet.ResonanceRemaining.ToString("0.0")+" 秒  [R] 重试",text);
        }
    }
}
