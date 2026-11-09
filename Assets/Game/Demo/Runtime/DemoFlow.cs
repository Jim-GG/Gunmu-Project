using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;
using Unity.AI.Navigation;

namespace NullBoundary.Demo
{
    public enum DemoStage { Intro, Exploring, Chase, SafeLab, Calibrating, Armed, Combat, Cleared, Dead, Ended }

    public sealed class EncounterLedger
    {
        public int Expected { get; private set; }
        readonly HashSet<int> registered=new HashSet<int>(), down=new HashSet<int>();
        public int Spawned => registered.Count;
        public int Defeated => down.Count;
        public bool Complete => Expected>0 && Spawned==Expected && Defeated==Expected;
        public void Reset(int expected) { Expected=expected;registered.Clear();down.Clear(); }
        public void Register(int id) { registered.Add(id); }
        public bool Defeat(int id) { return registered.Contains(id)&&down.Add(id); }
    }

    public sealed class DemoFlow : MonoBehaviour
    {
        public static DemoFlow Instance { get; private set; }
        public DemoPlayer player;
        public DemoIntro intro;
        public DemoSound sound;
        public DemoDoor chaseDoor, entryDoor, exitDoor;
        public DemoEnemy chaser, enemyPrefab;
        public Transform[] spawnPoints;
        public Transform fridgeHinge, fridgeBody, gauntletPickup;
        public Light[] labLights;
        public NavMeshSurface navigation;
        public DemoStage Stage { get; private set; }
        public int CheckpointIndex { get; private set; }
        public bool FridgePlayed { get; private set; }
        public bool CalibrationSolved { get; private set; }
        public EncounterLedger Ledger { get; } = new EncounterLedger();
        public bool paused, reduceMotion;
        public bool InputBlocked => paused || Stage==DemoStage.Intro || Stage==DemoStage.Calibrating ||
                                    Stage==DemoStage.Dead || Stage==DemoStage.Ended;
        public float calibrationPhase=30;
        public float alignedTime;
        const float TargetPhase=210;
        readonly List<DemoEnemy> enemies=new List<DemoEnemy>();
        Vector3 fridgeStart;
        Quaternion fridgeRotation;
        Transform fridgeChest;
        Vector3 fridgeChestScale;
        string message;float messageUntil;
        GUIStyle titleStyle,bodyStyle,smallStyle;
        Font font;
        bool alertPending;
        float impactUntil;
        void Awake() { Instance=this; }
        void Start()
        {
            Application.runInBackground=true;
            fridgeStart=fridgeBody.position;fridgeRotation=fridgeBody.rotation;
            fridgeChest=fridgeBody.Find("Workcoat");
            if(fridgeChest)fridgeChestScale=fridgeChest.localScale;
            if(navigation && !navigation.navMeshData)navigation.BuildNavMesh();
            Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;
            Stage=DemoStage.Intro;player.SetControl(false);intro.Begin();
        }
        void OnDestroy()
        {
            if(Instance==this)Instance=null;
            Time.timeScale=1;Cursor.lockState=CursorLockMode.None;Cursor.visible=true;
        }
        void Update()
        {
            var key=Keyboard.current;
            if(key!=null && key.escapeKey.wasPressedThisFrame)
            {
                if(Stage==DemoStage.Calibrating) CloseCalibration();
                else if(Stage!=DemoStage.Dead&&Stage!=DemoStage.Ended)SetPaused(!paused);
            }
            if(Stage==DemoStage.Intro)
            {
                if(!paused&&key!=null&&key.spaceKey.wasPressedThisFrame)intro.Complete();
                return;
            }
            if(Stage==DemoStage.Dead)
            {
                if(key!=null&&key.rKey.wasPressedThisFrame)RestartCheckpoint();
                return;
            }
            if(paused||Stage==DemoStage.Ended)return;
            if(FridgePlayed&&fridgeChest)
                fridgeChest.localScale=Vector3.Scale(fridgeChestScale,new Vector3(1,1+.025f*Mathf.Sin(Time.time*2.3f),1));
            if(Stage==DemoStage.Calibrating)
                alignedTime=PhaseError(calibrationPhase,TargetPhase)<=15 ? alignedTime+Time.deltaTime:0;
            if(Stage==DemoStage.Exploring)
            {
                if(player.transform.position.z>20&&!FridgePlayed)StartCoroutine(FridgeEvent());
                if(player.transform.position.z>28)CheckpointIndex=1;
                if(player.transform.position.z>32&&!alertPending)StartCoroutine(AlertChaser());
            }
            if(Stage==DemoStage.Cleared&&player.transform.position.z>92)EndDemo();
            if(player.hasGauntlet && Stage!=DemoStage.Cleared)
                foreach(var lamp in labLights)
                {
                    lamp.color=Color.Lerp(new Color(.45f,.68f,1),new Color(1,.08f,.035f),.92f);
                    lamp.intensity=1.8f+Mathf.Sin(Time.time*3)*.55f;
                }
        }
        public static float PhaseError(float phase,float target) => Mathf.Abs(Mathf.DeltaAngle(phase,target));
        public void IntroFinished()
        {
            if(Stage!=DemoStage.Intro)return;
            player.Teleport(new Vector3(0,.08f,3),0);player.SetControl(true);
            Stage=DemoStage.Exploring;CheckpointIndex=0;
            Message("通讯中断。离开电梯，寻找通往旧设施的道路。",5);
        }
        IEnumerator FridgeEvent()
        {
            FridgePlayed=true;
            sound.Play(DemoSound.Cue.Door,fridgeHinge.position);
            for(float t=0;t<1.4f;t+=Time.deltaTime)
            {
                float k=Mathf.SmoothStep(0,1,t/1.4f);
                fridgeHinge.localRotation=Quaternion.Euler(0,-105*k,0);
                fridgeBody.position=Vector3.Lerp(fridgeStart,fridgeStart+new Vector3(1.0f,.25f,-1.1f),k);
                fridgeBody.rotation=Quaternion.Slerp(fridgeRotation,Quaternion.Euler(82,10,0),k);
                yield return null;
            }
            ApplyFridgeEnd();
            sound.Play(DemoSound.Cue.Hit,fridgeBody.position);
        }
        void ApplyFridgeEnd()
        {
            fridgeHinge.localRotation=Quaternion.Euler(0,-105,0);
            fridgeBody.position=fridgeStart+new Vector3(1,.25f,-1.1f);
            fridgeBody.rotation=Quaternion.Euler(82,10,0);
        }
        IEnumerator AlertChaser()
        {
            alertPending=true;
            Message("它停止了敲击……",1.2f);
            yield return new WaitForSeconds(.8f);
            Stage=DemoStage.Chase;
            chaser.BeginThreat();
            Message("奔跑！进入前方实验室，关上入口门。",4);
        }
        public void Interact(DemoAction action)
        {
            if(InputBlocked)return;
            switch(action)
            {
                case DemoAction.CloseChaseDoor:
                    if(Stage==DemoStage.Chase && player.transform.position.z>66.4f)
                    {
                        chaseDoor.SetOpen(false);
                        Message("液压门正在关闭。",1);
                    }
                    else Message("控制器位于门内，先进入实验室。",2);
                    break;
                case DemoAction.Calibrate:
                    if(Stage==DemoStage.SafeLab)
                    {
                        if(CalibrationSolved)Message("支架已解锁，取下拳套。",2);
                        else { Stage=DemoStage.Calibrating;alignedTime=0;UnlockCursor(); }
                    }
                    else if(Stage==DemoStage.Chase)Message("先关闭入口门。",2);
                    break;
                case DemoAction.TakeGauntlet:
                    if(Stage!=DemoStage.SafeLab)return;
                    if(!CalibrationSolved){Message("支架锁定：使用旁边终端完成校准。",2.5f);return;}
                    EquipGauntlet();break;
            }
        }
        public void ChaseDoorClosed()
        {
            if(Stage!=DemoStage.Chase)return;
            chaser.StopThreat();Stage=DemoStage.SafeLab;
            player.health=100;
            Message("安全了。检查左侧实验终端。",4);
        }
        public bool ConfirmCalibration()
        {
            if(Stage!=DemoStage.Calibrating||alignedTime<.8f||PhaseError(calibrationPhase,TargetPhase)>15)return false;
            CalibrationSolved=true;CloseCalibration();sound.Play(DemoSound.Cue.Crystal,player.transform.position);
            Message("校准完成 · 拳套支架已解锁",3);return true;
        }
        public void CloseCalibration()
        {
            if(Stage!=DemoStage.Calibrating)return;
            Stage=DemoStage.SafeLab;LockCursor();
        }
        void EquipGauntlet()
        {
            if(player.hasGauntlet)return;
            player.hasGauntlet=true;gauntletPickup.gameObject.SetActive(false);
            Stage=DemoStage.Armed;CheckpointIndex=2;player.charge=0;player.health=100;
            sound.Alarm(true);Message("右键格挡来袭冲击 → 左键压缩并释放",6);
            StartCoroutine(Encounter());
        }
        IEnumerator Encounter()
        {
            Ledger.Reset(3);
            yield return new WaitForSeconds(2);
            entryDoor.SetOpen(true);Stage=DemoStage.Combat;
            yield return new WaitForSeconds(1);
            var first=Spawn(0);
            while(first&&!first.IsDown)yield return null;
            yield return new WaitForSeconds(1.0f);
            Spawn(1);yield return new WaitForSeconds(.65f);Spawn(2);
        }
        DemoEnemy Spawn(int index)
        {
            var enemy=Instantiate(enemyPrefab,spawnPoints[index].position,Quaternion.Euler(0,-90,0));
            enemy.name="Encounter_Resonator_"+index;enemy.gameObject.SetActive(true);
            enemies.Add(enemy);Ledger.Register(enemy.GetInstanceID());enemy.BeginThreat();return enemy;
        }
        public void EnemyDown(DemoEnemy enemy)
        {
            if(Stage!=DemoStage.Combat || !Ledger.Defeat(enemy.GetInstanceID()))return;
            if(!Ledger.Complete)return;
            Stage=DemoStage.Cleared;sound.Alarm(false);exitDoor.SetOpen(true);
            foreach(var lamp in labLights){lamp.color=new Color(.45f,.7f,1);lamp.intensity=2;}
            Message("危险解除 · 离开实验室",4);
        }
        public void Fail()
        {
            if(Stage==DemoStage.Dead)return;
            Stage=DemoStage.Dead;UnlockCursor();
        }
        void EndDemo() { Stage=DemoStage.Ended;sound.Alarm(false);player.SetControl(false);UnlockCursor(); }
        public void Message(string text,float seconds=3) { message=text;messageUntil=Time.unscaledTime+seconds; }
        public void ShowImpact(Vector3 at) { impactUntil=Time.time+.14f; }
        public void SetPaused(bool value)
        {
            paused=value;Time.timeScale=value?0:1;
            if(value)UnlockCursor();else LockCursor();
        }
        void LockCursor() { Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false; }
        void UnlockCursor() { Cursor.lockState=CursorLockMode.None;Cursor.visible=true; }
        public void RestartCheckpoint()
        {
            StopAllCoroutines();paused=false;Time.timeScale=1;
            foreach(var e in enemies)if(e){e.gameObject.SetActive(false);Destroy(e.gameObject);}
            enemies.Clear();Ledger.Reset(3);alertPending=false;CalibrationSolved=CheckpointIndex==2;
            player.health=100;player.charge=0;player.hasGauntlet=CheckpointIndex==2;
            player.SetControl(true);chaser.Restore(new Vector3(-.8f,0,34.5f),true);
            chaseDoor.ResetDoor(CheckpointIndex<2);entryDoor.ResetDoor(false);exitDoor.ResetDoor(false);
            gauntletPickup.gameObject.SetActive(CheckpointIndex<2);
            FridgePlayed=CheckpointIndex>0;
            if(FridgePlayed)ApplyFridgeEnd();
            else { fridgeHinge.localRotation=Quaternion.identity;fridgeBody.SetPositionAndRotation(fridgeStart,fridgeRotation); }
            sound.Alarm(CheckpointIndex==2);LockCursor();
            foreach(var lamp in labLights){lamp.color=new Color(.45f,.7f,1);lamp.intensity=2;}
            if(CheckpointIndex==2)
            {
                player.Teleport(new Vector3(0,.08f,71));Stage=DemoStage.Armed;StartCoroutine(Encounter());
            }
            else
            {
                player.Teleport(new Vector3(0,.08f,CheckpointIndex==1?29:3));
                Stage=DemoStage.Exploring;
            }
            Message("检查点已恢复",2);
        }
        public void RestartFromBeginning()
        {
            CheckpointIndex=0;RestartCheckpoint();Stage=DemoStage.Intro;player.SetControl(false);intro.Begin();
        }
        string Objective()
        {
            switch(Stage)
            {
                case DemoStage.Exploring:return "寻找通往旧实验室的路线";
                case DemoStage.Chase:return "奔跑进入实验室 · 使用门内控制器";
                case DemoStage.SafeLab:return CalibrationSolved?"取下试验台上的拳套":"校准左侧实验终端";
                case DemoStage.Armed:return "实验兵装已启动";
                case DemoStage.Combat:return "格挡储存冲量，再释放反击";
                case DemoStage.Cleared:return "离开实验室，继续前进";
                default:return "";
            }
        }
        void OnGUI()
        {
            if(!player)return;
            if(titleStyle==null)
            {
                font=Font.CreateDynamicFontFromOSFont(new[]{"Microsoft YaHei","Arial"},22);
                titleStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=27,fontStyle=FontStyle.Bold};
                bodyStyle=new GUIStyle(GUI.skin.label){font=font,fontSize=19,wordWrap=true};
                smallStyle=new GUIStyle(bodyStyle){fontSize=15};
                titleStyle.normal.textColor=bodyStyle.normal.textColor=smallStyle.normal.textColor=new Color(.88f,.94f,.94f);
            }
            float scale=Mathf.Min(Screen.width/1280f,Screen.height/720f);
            GUI.matrix=Matrix4x4.TRS(new Vector3((Screen.width-1280*scale)/2,(Screen.height-720*scale)/2),Quaternion.identity,Vector3.one*scale);
            if(Stage!=DemoStage.Intro)
            {
                GUI.Label(new Rect(30,22,700,40),"零界  /  NULL BOUNDARY",titleStyle);
                GUI.Label(new Rect(32,65,880,40),Objective(),bodyStyle);
                if(Time.unscaledTime<messageUntil)GUI.Label(new Rect(300,585,760,70),message,bodyStyle);
                GUI.Label(new Rect(30,674,1150,30),"WASD 移动   Shift 奔跑   E 交互   空格 跳跃   Esc 暂停",smallStyle);
                Rect bar=new Rect(32,633,160,7);Fill(bar,new Color(.12f,.18f,.2f));bar.width*=player.health/100;
                Fill(bar,new Color(.65f,.84f,.76f));
                if(player.hasGauntlet)
                {
                    GUI.Label(new Rect(965,607,280,35),"冲量储存",smallStyle);
                    Fill(new Rect(965,643,260,10),new Color(.1f,.2f,.24f));
                    Fill(new Rect(965,643,260*player.charge,10),new Color(.3f,.8f,1));
                }
                if(!InputBlocked)
                {
                    Fill(new Rect(638,358,4,4),Color.white);
                    if(player.Focus)GUI.Label(new Rect(455,420,600,45),"[ E ] "+player.Focus.prompt,bodyStyle);
                }
            }
            if(Stage==DemoStage.Calibrating)DrawCalibration();
            if(Stage==DemoStage.Intro)
            {
                Fill(new Rect(0,0,1280,720),new Color(0,0,0,intro.Black));
                GUI.Label(new Rect(30,670,500,30),"空格 跳过开场  ·  Esc 暂停",smallStyle);
            }
            if(player.DamageFlash>0)Fill(new Rect(0,0,1280,720),new Color(.7f,0,0,player.DamageFlash*.4f));
            if(Time.time<impactUntil)Fill(new Rect(0,0,1280,720),new Color(.2f,.7f,1,.08f));
            if(paused||Stage==DemoStage.Dead||Stage==DemoStage.Ended)
            {
                Fill(new Rect(0,0,1280,720),new Color(.02f,.04f,.06f,.92f));
                GUI.Label(new Rect(435,180,700,70),Stage==DemoStage.Dead?"行动中断":Stage==DemoStage.Ended?"零界 · Demo 结束":"已暂停",titleStyle);
                if(Stage==DemoStage.Ended)GUI.Label(new Rect(435,245,640,60),"你已穿过事故现场，并完成首次冲量反击。",bodyStyle);
                if(paused&&GUI.Button(new Rect(435,290,360,48),"继续"))SetPaused(false);
                if(GUI.Button(new Rect(435,350,360,48),"重试当前检查点"))RestartCheckpoint();
                if(GUI.Button(new Rect(435,410,360,48),"从电梯开场重新开始"))RestartFromBeginning();
                reduceMotion=GUI.Toggle(new Rect(435,480,400,35),reduceMotion," 减弱镜头震动");
            }
        }
        void DrawCalibration()
        {
            Fill(new Rect(0,0,1280,720),new Color(.02f,.05f,.07f,.94f));
            GUI.Label(new Rect(230,130,900,55),"冲量储存装置 / 共振校准",titleStyle);
            GUI.Label(new Rect(230,195,900,65),"移动旋钮，使青色波形与灰白参照重合，稳定后确认。",bodyStyle);
            var box=new Rect(230,285,820,140);Fill(box,new Color(.07f,.13f,.16f));
            Wave(box,TargetPhase,new Color(.65f,.7f,.72f));
            Wave(box,calibrationPhase,new Color(.16f,.8f,.9f));
            calibrationPhase=GUI.HorizontalSlider(new Rect(250,465,780,35),calibrationPhase,0,360);
            GUI.Label(new Rect(250,505,700,35),alignedTime>=.8f?"同调稳定":"调整并保持波形重合",bodyStyle);
            GUI.enabled=alignedTime>=.8f;
            if(GUI.Button(new Rect(700,562,330,50),"确认校准"))ConfirmCalibration();
            GUI.enabled=true;
            if(GUI.Button(new Rect(250,562,260,50),"返回实验室"))CloseCalibration();
        }
        static void Wave(Rect rect,float phase,Color color)
        {
            for(float x=0;x<rect.width;x+=3)
            {
                float y=Mathf.Sin(x/rect.width*Mathf.PI*4+phase*Mathf.Deg2Rad)*rect.height*.34f;
                Fill(new Rect(rect.x+x,rect.center.y+y,3,3),color);
            }
        }
        static void Fill(Rect rect,Color color)
        {
            Color old=GUI.color;GUI.color=color;GUI.DrawTexture(rect,Texture2D.whiteTexture);GUI.color=old;
        }
    }
}

