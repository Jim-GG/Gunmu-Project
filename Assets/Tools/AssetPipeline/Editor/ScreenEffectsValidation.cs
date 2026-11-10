using System;using System.IO;using System.Linq;using System.Collections.Generic;
using UnityEngine;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine.SceneManagement;using UnityEngine.Rendering.Universal;
using Newtonsoft.Json;using GameActions;using FirstPerson;using Resonator.AI;using UDK7M;
namespace AssetPipeline.Editor
{
 [InitializeOnLoad]public static class ScreenEffectsValidation
 {
  const string SourceScene="Assets/Game/Scene/UDK7MBossArena_ThreePhase.unity";
  const string Key="ScreenEffectsValidation",Stage="AssetStaging/ScreenEffectsIntegration/",ScenePath="Assets/Tools/AssetPipeline/Editor/__ScreenEffectsValidation.unity";
  static int phase;static double deadline;static readonly List<string> checks=new List<string>();
  static ScreenEffectsValidation(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(SessionState.GetBool(Key+"Scene",false)){SessionState.SetBool(Key+"Scene",false);AssetDatabase.DeleteAsset(ScenePath);}}};}
  static void Check(bool ok,string text){if(!ok)throw new Exception(text);checks.Add(text);}
  static void Finish(Exception e=null){EditorApplication.isPaused=false;File.WriteAllText("Docs/Integration/screen-effects-validation.json",JsonConvert.SerializeObject(new{passed=e==null,scene=SourceScene,time=DateTimeOffset.Now,checks,error=e?.ToString()},Formatting.Indented));SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();}
  static Color32[] Capture(Camera camera,string name)
  {
   var prior=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(960,540,24);var image=new Texture2D(960,540,TextureFormat.RGB24,false);
   try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,960,540),0,0);image.Apply();File.WriteAllBytes(Stage+name+".png",image.EncodeToPNG());return image.GetPixels32();}
   finally{camera.targetTexture=prior;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
  }
  static int Changed(Color32[] a,Color32[] b){int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>6)n++;return n;}
  static void Poll()
  {
   if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
   if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Stage+"Validate.request"))
   {
    File.Delete(Stage+"Validate.request");foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s.isDirty))EditorSceneManager.SaveScene(scene,Stage+"UserBefore_"+scene.handle+".unity",true);
    foreach(var key in new[]{"CrystalReleaseSmoke","UDK7MBossValidation","CombatVFXValidation","AdvancedVFXValidation","LevelFeedbackValidation"})SessionState.SetBool(key,false);
    if(File.Exists(ScenePath))throw new Exception("Temporary validation scene already exists");File.Copy(SourceScene,ScenePath);SessionState.SetBool(Key+"Scene",true);AssetDatabase.ImportAsset(ScenePath,ImportAssetOptions.ForceSynchronousImport);
    phase=0;SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();return;
   }
   if(Application.isPlaying&&SessionState.GetBool(Key,false)){Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();if(phase==0)Time.timeScale=1;}
   if(!Application.isPlaying||!SessionState.GetBool(Key,false)||Time.timeSinceLevelLoad<.5f)return;
   try
   {
    if(phase==0){checks.Clear();deadline=EditorApplication.timeSinceStartup+90;EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));phase=1;return;}
    if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Screen effects timeout");if(!SceneManager.GetSceneByPath(ScenePath).isLoaded)return;
    var arena=UnityEngine.Object.FindFirstObjectByType<UDK7MArena>();if(!arena)return;var p=arena.player;
    if(phase==1){arena.boss.enabled=false;p.GetComponent<FirstPersonInput>().enabled=false;p.GetComponent<CrystalParkour.CrystalTraversal>().enabled=false;GameStateManager.RequestResume();phase=2;return;}
    if(phase==2)
    {
     var core=p.gauntlet;Check(core.InitializePipeline(),"原版拳套初始化成功");var fx=p.GetComponent<GauntletAdvancedPresentation>();var camera=p.GetComponentInChildren<Camera>();var config=fx.profile;
     Check(fx&&config&&config.advancedScreenEffects,"UDK7M 场内原版玩家自动接入色散/热浪模块和启用配置");
     Check(camera.GetUniversalAdditionalCameraData().requiresColorTexture,"实际游戏相机开启 Scene Color 所需颜色纹理");
     EditorApplication.isPaused=true;p.GetComponent<FirstPersonMotor>().enabled=false;core.Cancel();core.ResetResonance();core.Press();core.Tick(.2f);fx.SendMessage("LateUpdate");Check(!fx.ChargingHeatActive,"低段蓄力不显示持续热浪");
     core.Cancel();core.AddEnergy(3);core.Press();core.Tick(.7f);fx.SendMessage("LateUpdate");
     Check(core.State==GauntletCombat.Phase.Full&&fx.ChargingHeatActive,"原版满蓄力真实触发拳套热浪循环");
     var heat=p.GetComponentsInChildren<HeatHazeController>(true).First(h=>h.loop&&h.gameObject.activeInHierarchy);heat.SendMessage("LateUpdate");
     Check(Mathf.Approximately(heat.distortionPixels,config.chargingHeatDistortion)&&Mathf.Approximately(heat.opacity,config.chargingHeatOpacity),"蓄力热浪读取可调强度与透明度");
     Check(!heat.GetComponentInChildren<Collider>(),"热浪为纯视觉，不引入物理碰撞体");
     Capture(camera,"warmup");heat.surface.enabled=false;var baseHeat=Capture(camera,"charge-baseline");heat.surface.enabled=true;heat.SendMessage("LateUpdate");var heatImage=Capture(camera,"charge-heat");int heatPixels=Changed(baseHeat,heatImage);
     Check(heatPixels>100,"实际相机热浪产生可见像素变化："+heatPixels);
     GameStateManager.RequestPause();fx.SendMessage("LateUpdate");Check(!fx.ChargingHeatActive,"暂停隐藏持续热浪");GameStateManager.RequestResume();fx.SendMessage("LateUpdate");Check(fx.ChargingHeatActive,"恢复后热浪能重新显示");
     core.Cancel();fx.SendMessage("LateUpdate");Check(!fx.ChargingHeatActive,"取消蓄力停止持续热浪");
     int waves=fx.WavesPlayed;core.BeginBlock();core.ResolveGuard(p.transform.position+p.transform.forward*2);Check(core.LastGuard==GuardKind.Perfect&&fx.WavesPlayed==waves+1,"完美格挡真实触发短促色散波");
     var wave=fx.LastWave;wave.SetNormalizedTime(.4f);Check(wave.chromaticPixels>0&&wave.distortionPixels>0,"格挡波同时设置色散与扭曲参数");
     wave.meshCore.enabled=false;var baseWave=Capture(camera,"guard-baseline");wave.meshCore.enabled=true;wave.SetNormalizedTime(.4f);var waveImage=Capture(camera,"guard-chromatic");int wavePixels=Changed(baseWave,waveImage);
     Check(wavePixels>100,"实际相机色散波产生可见像素变化："+wavePixels);wave.gameObject.SetActive(false);
     waves=fx.WavesPlayed;core.Cancel();core.BeginBlock();core.Tick(1);core.ResolveGuard(p.transform.position+p.transform.forward*2);Check(core.LastGuard==GuardKind.Imperfect&&fx.WavesPlayed==waves,"非完美格挡不冒充完美格挡色散反馈");
     core.Cancel();core.ResetResonance();core.AddEnergy(3);core.Press();core.Tick(.7f);waves=fx.WavesPlayed;core.Release();Check(fx.WavesPlayed==waves,"释放重拳不提前播放命中色散");
     var h=core.profile.Find("punch4").action.hits.First(x=>x.enabled);core.actions.motion.Seek(h.startFrame);var boxes=new ActionHitbox(p.transform);boxes.Bind(HitboxAnchor.Root,core.attackOrigin);
     foreach(var t in core.animationPlayer.animator.GetComponentsInChildren<Transform>(true)){if(t.name=="hand_L.R")boxes.Bind(HitboxAnchor.RightHand,t);if(t.name=="hand_L.L")boxes.Bind(HitboxAnchor.LeftHand,t);}boxes.BindProfile(core.profile);
     var go=UnityEngine.Object.Instantiate(arena.minions.prefab);var enemy=go.GetComponent<ResonatorEnemy>();enemy.tuning=arena.minions.profile;enemy.target=p;enemy.Initialize(RestActivity.RestlessIdle);enemy.enabled=false;enemy.motor.enabled=false;enemy.motor.agent.enabled=false;go.transform.position=boxes.Pose(h).centre-Vector3.up;float beforeHealth=enemy.Health;Physics.SyncTransforms();core.Tick(1/30f);
     Check(enemy.Health<beforeHealth,"UDK7M 共鸣者实际受到原版重拳 Hitbox 伤害");
     Check(fx.WavesPlayed==waves+1&&fx.ImpactHeatPlayed==1,"原版三级 Hitbox 命中同时触发色散和冲击热浪");core.Tick(1/30f);Check(fx.WavesPlayed==waves+1&&fx.ImpactHeatPlayed==1,"同一重拳不会每帧重复生成屏幕特效");
     Check(fx.LastImpactHeat&&!fx.LastImpactHeat.loop&&Mathf.Approximately(fx.LastImpactHeat.distortionPixels,config.impactHeatDistortion),"冲击热浪为有限时长并读取配置");
     fx.LastWave.SetNormalizedTime(.4f);fx.LastImpactHeat.SendMessage("LateUpdate");fx.LastWave.meshCore.enabled=false;fx.LastImpactHeat.surface.enabled=false;var heavyBase=Capture(camera,"udk7m-heavy-baseline");fx.LastWave.meshCore.enabled=true;fx.LastImpactHeat.surface.enabled=true;int heavyPixels=Changed(heavyBase,Capture(camera,"udk7m-heavy-contact"));Check(heavyPixels>100,"UDK7M 共鸣者重拳命中画面产生可见扭曲与色散："+heavyPixels);
     File.WriteAllText(Stage+"pixel-differences.json",JsonConvert.SerializeObject(new{heatPixels,wavePixels,heavyPixels},Formatting.Indented));Finish();
    }
   }
   catch(Exception e){Finish(e);}
  }
 }
}
