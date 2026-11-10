using System;using System.IO;using System.Linq;using System.Collections.Generic;using System.Reflection;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;
using Newtonsoft.Json;using GameActions;using FirstPerson;using Resonator.AI;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad] public static class LevelFeedbackValidation
 {
  const string Key="LevelFeedbackValidation",Stage="AssetStaging/LevelFeedbackIntegration/",TestScene="Assets/Tools/AssetPipeline/Editor/__LevelFeedbackValidation.unity";
  static int phase;static double deadline,captureAt;static readonly List<string> checks=new List<string>();
  static LevelFeedbackValidation(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(SessionState.GetBool(Key+"OwnScene",false)){SessionState.SetBool(Key+"OwnScene",false);AssetDatabase.DeleteAsset(TestScene);}}};}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
  static void Invoke(object o,string name,params object[] args)=>o.GetType().GetMethod(name,BindingFlags.NonPublic|BindingFlags.Instance).Invoke(o,args);
  static void Finish(Exception e=null){File.WriteAllText("Docs/Integration/level-feedback-validation.json",JsonConvert.SerializeObject(new{passed=e==null,time=DateTimeOffset.Now,checks,error=e?.ToString()},Formatting.Indented));SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();}
  static void Capture(Camera camera)
  {
   var prior=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1280,720,24);var image=new Texture2D(1280,720,TextureFormat.RGB24,false);
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,1280,720),0,0);image.Apply();File.WriteAllBytes(Stage+"level-authored-charge.png",image.EncodeToPNG());}
   finally{camera.targetTexture=prior;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
  }
  static void Poll()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Stage+"Validate.request"))
   {
    File.Delete(Stage+"Validate.request");foreach(var s in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s.isDirty))EditorSceneManager.SaveScene(s,Stage+"UserBefore_"+s.handle+".unity",true);
    foreach(var k in new[]{"CrystalReleaseSmoke","UDK7MBossValidation","CombatVFXValidation","AdvancedVFXValidation"})SessionState.SetBool(k,false);
    if(File.Exists(TestScene))throw new Exception("Temporary scene already exists");File.Copy("Assets/Scenes/Soviet_1.unity",TestScene);SessionState.SetBool(Key+"OwnScene",true);AssetDatabase.ImportAsset(TestScene,ImportAssetOptions.ForceSynchronousImport);
    phase=0;SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();return;
   }
   if(Application.isPlaying&&SessionState.GetBool(Key,false)){Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();if(phase==0)Time.timeScale=1;}
   if(!Application.isPlaying||!SessionState.GetBool(Key,false)||Time.timeSinceLevelLoad<.5f)return;
   try
   {
    if(phase==0){checks.Clear();deadline=EditorApplication.timeSinceStartup+120;EditorSceneManager.LoadSceneInPlayMode(TestScene,new LoadSceneParameters(LoadSceneMode.Single));phase=1;return;}
    if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Validation timeout");if(!SceneManager.GetSceneByPath(TestScene).isLoaded)return;
    var p=UnityEngine.Object.FindFirstObjectByType<PlayerChargedPunch>();if(!p)return;
    if(phase==1){p.GetComponent<PlayerGauntletEquipment>()?.Equip();GameStateManager.RequestResume();phase=2;return;}
    var core=p.SharedCombat;var visual=p.GetComponent<LevelPlayerPresentation>();
    if(phase==2)
    {
     Check(core&&core.InitializePipeline()&&core.profile==Resources.Load<ActionSet>("GauntletActions"),"关卡直接使用原版 GauntletCombat 和动作/Hitbox 资产");
     Check(p.GetComponents<GauntletCombat>().Length==1&&!p.GetComponent<FirstPersonMotor>(),"只有一套拳套控制器，关卡移动仍使用 PlayerMovement");
     Check(Mathf.Approximately(p.TierOneTime,32/30f)&&Mathf.Approximately(p.TierTwoTime,62/30f)&&Mathf.Approximately(p.TierThreeTime,92/30f),"蓄力按原版 32/62/92 帧时序，音效共用同一时钟");
     Check(core.animationPlayer.animator.runtimeAnimatorController==null,"只有原版 Playable 动画图驱动骨骼，旧 Animator 状态机不抢姿态");
     var movement=p.GetComponent<PlayerMovement>();movement.enabled=false;var cc=p.GetComponent<CharacterController>();var spawn=p.transform.position;cc.enabled=false;p.transform.position+=Vector3.up*100;cc.enabled=true;Physics.SyncTransforms();
     core.Cancel();core.ResetResonance();
     var targetGo=GameObject.CreatePrimitive(PrimitiveType.Cube);targetGo.transform.localScale=Vector3.one*.15f;targetGo.transform.position=p.transform.position+Vector3.up;
     var vitals=targetGo.AddComponent<FirstPersonVitals>();var target=targetGo.AddComponent<ResonatorTarget>();target.vitals=vitals;
     core.Press();Check(vitals.Health==100,"按下左键只进入蓄力，不会立即伤害目标");core.Tick(.05f);core.Release();
     Check(core.ActivePunch==1&&!movement.IsPunchLunging,"短按走原版轻攻击，零档不冲刺");core.Tick(.1f);Check(vitals.Health==100,"动画尚未到 Hitbox 有效帧时目标不受伤");
     var hit=core.profile.Find("punch1").action.hits.First(h=>h.enabled);core.actions.motion.Seek(hit.startFrame);
     var boxes=new ActionHitbox(p.transform);boxes.Bind(HitboxAnchor.Root,core.attackOrigin);
     foreach(var t in core.animationPlayer.animator.GetComponentsInChildren<Transform>(true)){if(t.name=="hand_L.R")boxes.Bind(HitboxAnchor.RightHand,t);if(t.name=="hand_L.L")boxes.Bind(HitboxAnchor.LeftHand,t);}boxes.BindProfile(core.profile);
     targetGo.transform.position=boxes.Pose(hit).centre;Physics.SyncTransforms();core.Tick(1/30f);
     Check(vitals.Health<100&&core.ContactsThisAttack>0,"原版手部 Hitbox 的有效帧实际结算伤害");UnityEngine.Object.Destroy(targetGo);
     core.Cancel();core.ResetResonance();core.Press();core.Tick(.8f);visual.chargeAudio.Sync();
     Check(visual.chargeAudio.stages.isPlaying&&Mathf.Abs(visual.chargeAudio.stages.time/visual.chargeAudio.stages.clip.length-core.ChargeSeconds/(92/30f))<.08f,"三段录音进度与原版蓄力帧同步");core.Cancel();
     p.AddEnergy(3);Check(core.BlockEnergy==3&&p.StoredEnergy==3,"关卡晶体与原版格挡共享同一个三格能量池");
     core.Press();core.Tick(.7f);Check(core.State==GauntletCombat.Phase.Full&&core.BlockEnergy==0,"三格能量按原版规则跳过三个蓄力区间并消耗三格");visual.chargeAudio.Sync();visual.energy.SendMessage("LateUpdate");
     Check(visual.chargeAudio.fullLoop.isPlaying&&visual.energy.DisplayedCharge>.99f,"原版满蓄力状态同步循环音效与金色能量");
     core.Release();Check(core.ActivePunch==4&&movement.IsPunchLunging,"原版三级出拳保留用户认可的长距离关卡冲刺");
     var so=new SerializedObject(p);Check(Mathf.Approximately(so.FindProperty("tierThreeLungeDistance").floatValue,6.5f),"保留原关卡三级六点五米冲刺参数");core.Cancel();
     var hp=p.GetComponent<PlayerCombatController>();hp.RestoreHealth();core.ResetResonance();core.BeginBlock();float before=hp.ExactHealth;
     hp.ReceiveAttack(p.transform.position+p.transform.forward*2,30f,true);Check(hp.LastGuard==GuardKind.Perfect&&Mathf.Approximately(hp.ExactHealth,before)&&core.BlockEnergy==1,"完美格挡无伤并向同一能量池增加一格");
     core.Cancel();hp.RestoreHealth();core.BeginBlock();core.Tick(1);before=hp.ExactHealth;hp.ReceiveAttack(p.transform.position+p.transform.forward*2,30f,true);
     Check(hp.LastGuard==GuardKind.Imperfect&&Mathf.Abs(before-hp.ExactHealth-10.2f)<.001f,"非完美格挡精确减伤百分之六十六，三十伤害只承受十点二");
     core.Cancel();hp.RestoreHealth();core.BeginBlock();before=hp.ExactHealth;hp.ReceiveAttack(p.transform.position-p.transform.forward*2,30f,true);Check(Mathf.Approximately(before-hp.ExactHealth,30),"背后攻击不能被正面格挡");
     core.Cancel();hp.RestoreHealth();core.BeginBlock();before=hp.ExactHealth;hp.ReceiveAttack(p.transform.position+p.transform.forward*2,30f,false);Check(Mathf.Approximately(before-hp.ExactHealth,30),"不可格挡攻击保持原版规则");core.Cancel();hp.RestoreHealth();
     core.ResetResonance();var crystalGo=GameObject.CreatePrimitive(PrimitiveType.Sphere);crystalGo.transform.position=p.transform.position+Vector3.right*10;var crystal=crystalGo.AddComponent<DashCrystal>();Invoke(crystal,"OnTriggerEnter",cc);
     Check(core.BlockEnergy==3&&p.StoredEnergy==3,"实际接触关卡晶体可补满原版拳套三格能量");Invoke(crystal,"OnTriggerStay",cc);Check(crystalGo.GetComponent<PuzzlePresentation>().SignalsPlayed==1,"晶体冷却不会重复补能或重放反馈");
     var boxGo=GameObject.CreatePrimitive(PrimitiveType.Cube);boxGo.transform.position=p.transform.position+Vector3.right*20;var rb=boxGo.AddComponent<Rigidbody>();rb.useGravity=false;var box=boxGo.AddComponent<KineticBox>();rb.linearVelocity=Vector3.right*5;box.LockMomentum();box.ApplyCardinalImpulse(Vector3.left,Vector3.right,11);box.ReleaseMomentum();
     Check(Mathf.Abs(rb.linearVelocity.x-16)<.01f,"解谜方块仍可锁定、蓄能并按十六米每秒释放");
     var library=AdvancedCombatVFXLibrary.Load();var wind=UnityEngine.Object.Instantiate(library.windCone).GetComponentInChildren<WindConeFX.WindConeBurst>(true);Check(wind,"沿用冲刺风锥资源");wind.overrideMaterialAppearance=false;wind.SetNormalizedTime(.2f);
     var block=new MaterialPropertyBlock();foreach(var shell in wind.shells){shell.GetPropertyBlock(block);Check(Mathf.Approximately(block.GetFloat("_Intensity"),shell.sharedMaterial.GetFloat("_Intensity")),"风锥运行时尊重材质亮度："+shell.sharedMaterial.name);}
     core.Cancel();visual.chargeAudio.Sync();Check(!visual.chargeAudio.stages.isPlaying&&!visual.chargeAudio.fullLoop.isPlaying,"取消出拳/蓄力后停止录音与满充循环");
     cc.enabled=false;p.transform.position=spawn;cc.enabled=true;visual.energy.preview=true;visual.energy.previewCharge=1;captureAt=EditorApplication.timeSinceStartup;phase=3;return;
    }
    if(phase==3){if(EditorApplication.timeSinceStartup-captureAt<.5)return;Capture(p.GetComponentInChildren<Camera>());Finish();}
   }
   catch(Exception e){Finish(e);}
  }
 }
}
