using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Reflection;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using FirstPerson;
using GameActions;
using Resonator.AI;
using UDK7M;

namespace AssetPipeline.Editor
{
    [InitializeOnLoad]
    public static class CombatPolishValidation
    {
        const string Key="CombatPolishValidation",Stage="AssetStaging/CombatPolish/",Source="Assets/Game/Scene/UDK7MBossArena_ThreePhase.unity",ScenePath="Assets/Tools/AssetPipeline/Editor/__CombatPolishValidation.unity";
        static readonly List<object> checks=new List<object>();static int phase;static double deadline;
        static CombatPolishValidation(){EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode){SessionState.SetBool(Key,false);if(SessionState.GetBool(Key+"Scene",false)){SessionState.SetBool(Key+"Scene",false);AssetDatabase.DeleteAsset(ScenePath);}}};}
        static void Check(bool passed,string name,object details=null){if(!passed)throw new Exception(name+" "+JsonConvert.SerializeObject(details));checks.Add(new{name,passed,details});}
        static void Finish(Exception error=null)
        {
            EditorApplication.isPaused=false;File.WriteAllText("Docs/Integration/combat-polish-validation.json",JsonConvert.SerializeObject(new{passed=error==null,scene=Source,time=DateTimeOffset.Now,checks,error=error?.ToString()},Formatting.Indented));SessionState.SetBool(Key,false);EditorApplication.ExitPlaymode();
        }
        static Color32[] Capture(Camera view,string name,int width=960,int height=540)
        {
            var before=view.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(width,height,24);var image=new Texture2D(width,height,TextureFormat.RGB24,false);
            try{view.targetTexture=rt;view.Render();RenderTexture.active=rt;image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply();File.WriteAllBytes(Stage+name+".png",image.EncodeToPNG());return image.GetPixels32();}
            finally{view.targetTexture=before;RenderTexture.active=active;UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);}
        }
        static int Difference(Color32[] a,Color32[] b,int threshold=6)
        {int n=0;for(int i=0;i<a.Length;i++)if(Math.Abs(a[i].r-b[i].r)+Math.Abs(a[i].g-b[i].g)+Math.Abs(a[i].b-b[i].b)>threshold)n++;return n;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating)return;
            if(!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Stage+"Validate.request"))
            {
                File.Delete(Stage+"Validate.request");File.Delete("AssetStaging/VFXUpgrade/StopForVFXSetup.request");File.WriteAllText(Stage+"phase.json","{\"phase\":\"entering\"}");foreach(var scene in Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Where(s=>s.isDirty))EditorSceneManager.SaveScene(scene,Stage+"UserBefore_"+scene.handle+".unity",true);
                foreach(var key in new[]{"ScreenEffectsValidation","LevelFeedbackValidation","CrystalReleaseSmoke","UDK7MBossValidation","CombatVFXValidation","AdvancedVFXValidation"})SessionState.SetBool(key,false);
                if(File.Exists(ScenePath))throw new Exception("Temporary test scene already exists");File.Copy(Source,ScenePath);SessionState.SetBool(Key+"Scene",true);AssetDatabase.ImportAsset(ScenePath,ImportAssetOptions.ForceSynchronousImport);phase=0;SessionState.SetBool(Key,true);EditorApplication.EnterPlaymode();return;
            }
            if(Application.isPlaying&&SessionState.GetBool(Key,false)){Application.runInBackground=true;EditorApplication.isPaused=false;EditorApplication.QueuePlayerLoopUpdate();if(phase==0)Time.timeScale=1;}
            if(!Application.isPlaying||!SessionState.GetBool(Key,false)||Time.timeSinceLevelLoad<.5f)return;
            try
            {
                if(phase==0){checks.Clear();deadline=EditorApplication.timeSinceStartup+150;File.WriteAllText(Stage+"phase.json","{\"phase\":\"loading arena\"}");EditorSceneManager.LoadSceneInPlayMode(ScenePath,new LoadSceneParameters(LoadSceneMode.Single));phase=1;return;}
                if(EditorApplication.timeSinceStartup>deadline)throw new Exception("Combat polish validation timeout");
                var arena=UnityEngine.Object.FindFirstObjectByType<UDK7MArena>();if(!arena||!SceneManager.GetSceneByPath(ScenePath).isLoaded)return;
                if(phase==1){GameStateManager.RequestResume();arena.boss.enabled=false;arena.player.GetComponent<FirstPersonInput>().enabled=false;arena.player.GetComponent<CrystalParkour.CrystalTraversal>().enabled=false;arena.player.GetComponent<FirstPersonMotor>().enabled=false;phase=2;return;}
                if(phase==2){File.WriteAllText(Stage+"phase.json","{\"phase\":\"running\"}");EditorApplication.isPaused=true;Run(arena);Finish();}
            }
            catch(Exception e){Finish(e);}
        }
        static ResonatorEnemy Spawn(UDK7MArena arena,Vector3 position)
        {
            var go=UnityEngine.Object.Instantiate(arena.minions.prefab);go.transform.position=position;var enemy=go.GetComponent<ResonatorEnemy>();enemy.tuning=arena.minions.profile;enemy.target=arena.player;enemy.Initialize(RestActivity.RestlessIdle);enemy.enabled=false;enemy.motor.agent.Warp(position);enemy.transform.position=position;return enemy;
        }
        static void Run(UDK7MArena arena)
        {
            var placement=arena.boss.transform.position+Vector3.ProjectOnPlane(arena.player.transform.position-arena.boss.transform.position,Vector3.up).normalized*14;
            Check(UnityEngine.AI.NavMesh.SamplePosition(placement,out var nav,4,UnityEngine.AI.NavMesh.AllAreas),"测试位置位于场内有效导航地面");
            var playerMotor=arena.player.GetComponent<FirstPersonMotor>();playerMotor.Body.enabled=false;arena.player.transform.position=nav.position;playerMotor.Body.enabled=true;playerMotor.StopMomentum();playerMotor.SetLookRotation(Quaternion.LookRotation(Vector3.ProjectOnPlane(arena.boss.transform.position-nav.position,Vector3.up)));
            var player=arena.player;var core=player.gauntlet;Check(core.InitializePipeline(),"UDK7M 原版玩家初始化");core.useTargetAssist=false;
            var feedback=player.GetComponent<GauntletCombatFeedback>();var profile=feedback.profile;var screen=player.GetComponentInChildren<ScreenEffectsController>();var camera=screen.GetComponent<Camera>();var settings=screen.profile;
            Check(profile&&profile.tierThreeSwing&&!profile.guardImpact&&profile.fleshHits.Length==10&&profile.bodyFalls.Length==5&&profile.resonatorRoar&&profile.playerHurts.Length==7,"三级出拳、旧格挡、十个打击、吼叫、受击与尸体音效已绑定");
            Check(screen&&settings&&screen.RuntimeMaterial&&screen.RuntimeMaterial!=settings.material,"每个游戏相机持有独立材质，资源材质保持零强度");
            Check(settings.rendererFeatures.Length==2&&settings.rendererFeatures.All(f=>f&&f.fetchColorBuffer&&f.injectionPoint==FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing),"PC/Mobile 仅追加原生 Full Screen Pass，抓取包含透明物体的当前颜色");
            Check(!ShaderUtil.ShaderHasError(settings.material.shader)&&!ShaderUtil.ShaderHasError(profile.electricity.shader),"全屏与电流覆盖 Shader 无编译错误");
            var boxes=new ActionHitbox(player.transform);boxes.Bind(HitboxAnchor.Root,core.attackOrigin);
            foreach(var t in core.animationPlayer.animator.GetComponentsInChildren<Transform>(true)){if(t.name=="hand_L.R")boxes.Bind(HitboxAnchor.RightHand,t);if(t.name=="hand_L.L")boxes.Bind(HitboxAnchor.LeftHand,t);}boxes.BindProfile(core.profile);
            ResonatorEnemy launched=null;
            for(int tier=0;tier<4;tier++)
            {
                core.Cancel();core.ResetResonance();typeof(GauntletCombat).GetMethod("Attack",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(core,new object[]{tier+1});
                var hit=core.profile.Find("punch"+(tier+1)).action.hits.First(h=>h.enabled);core.actions.motion.Seek(hit.startFrame);var centre=boxes.Pose(hit).centre;
                var enemy=Spawn(arena,new Vector3(centre.x,player.transform.position.y,centre.z));float hp=enemy.Health;Physics.SyncTransforms();core.Tick(1/30f);
                Check(enemy.Health<hp&&feedback.IsStopped&&Mathf.Abs(feedback.LastHitStop-profile.HitStop(tier))<.0001f,"原版拳套真实命中与局部卡肉 "+tier,new{seconds=feedback.LastHitStop,health=enemy.Health});
                float frame=core.CurrentFrame;core.Tick(.02f);Check(core.CurrentFrame==frame,"卡肉时挥拳动画保持 "+tier);
                var audio=player.GetComponent<SurfaceContactAudio>();Check(profile.fleshHits.Contains(audio.LastClip),"共鸣者命中使用新增打击变体 "+tier);
                if(tier<3)
                {
                    float enemyFrame=enemy.CurrentFrame;enemy.Tick(.02f);Check(enemy.CurrentFrame==enemyFrame,"活体受击动作在卡肉窗口保持 "+tier);
                    var start=enemy.transform.position;for(int i=0;i<10;i++)enemy.motor.AdvanceKnockback(.02f);float distance=Vector3.ProjectOnPlane(enemy.transform.position-start,Vector3.up).magnitude;
                    Check(Mathf.Abs(distance-profile.knockbackMetres[tier])<.035f,"实际击退距离 "+tier,new{metres=distance,ready=enemy.motor.Ready,hit.knockbackDistance});enemy.gameObject.SetActive(false);
                }
                else
                {
                    launched=enemy;Check(!enemy.ActionTargetAlive&&enemy.ragdoll.Active&&enemy.PlayerLaunched,"三级拳立即进入击飞布娃娃");
                    Check(feedback.LastClip==profile.tierThreeSwing,"三级出拳使用专用音效");
                    var simulation=Physics.simulationMode;var start=enemy.ragdoll.Centre;
                    try{Physics.simulationMode=SimulationMode.Script;Physics.Simulate(.02f);Physics.Simulate(.02f);}finally{Physics.simulationMode=simulation;}
                    Check(Time.timeScale==1&&Vector3.Distance(start,enemy.ragdoll.Centre)>.08f&&enemy.ragdoll.Speed>1,"卡肉期间尸体继续实际物理飞行，不改变全局时间",new{travel=Vector3.Distance(start,enemy.ragdoll.Centre),speed=enemy.ragdoll.Speed});
                }
            }
            Check(launched.ragdoll.bodies.All(b=>b.GetComponent<CorpseImpactAudio>()),"每个布娃娃骨骼上报碰撞，共用尸体限流");
            int soundCount=launched.ragdoll.CollisionSoundsPlayed;
            var floor=Physics.RaycastAll(player.transform.position+Vector3.up*2,Vector3.down,8).First(h=>!h.transform.IsChildOf(player.transform)).collider;
            bool played=launched.ragdoll.PlayCollision(floor,8,launched.ragdoll.Centre);
            if(soundCount==0)Check(played,"有效尸体碰撞播放 body fall");
            Check(profile.bodyFalls.Contains(launched.ragdoll.LastCollisionClip)&&!launched.ragdoll.PlayCollision(floor,8,launched.ragdoll.Centre),"尸体音效使用 body fall 且多骨骼接触不会叠播");
            Check(!launched.ragdoll.PlayCollision(floor,.1f,launched.ragdoll.Centre),"静止微碰撞不触发尸体音效");launched.gameObject.SetActive(false);
            core.Cancel();core.BeginBlock();core.ResolveGuard(player.transform.position+player.transform.forward*2);var objectAudio=player.GetComponent<ObjectAudio>();Check(Array.Find(objectAudio.sounds,s=>s.cue==UDK7MSound.ParryImpact).clips.Contains(objectAudio.LastClip),"成功格挡恢复旧版 ParryImpact 音效");core.Cancel();
            screen.Stop();var roaring=Spawn(arena,player.transform.position+player.transform.right*4);int beforeRoar=screen.SignalsReceived;typeof(ResonatorEnemy).GetMethod("Enter",BindingFlags.NonPublic|BindingFlags.Instance).Invoke(roaring,new object[]{EnemyState.Screaming,false});var roar=roaring.GetComponent<ResonatorVoice>();Check(roar&&roar.LastClip==profile.resonatorRoar&&roar.RoarsPlayed==1,"共鸣者真实 Screaming 状态播放新增吼叫");screen.Tick(.03f,0,0,1);Check(screen.SignalsReceived==beforeRoar+1&&screen.RadialIntensity>0&&screen.ChromaticPixels>0,"附近共鸣者真实吼叫同时触发径向模糊和色散");Capture(camera,"udk7m-roar-screen");roaring.DefeatWithLaunch(Vector3.zero);Check(!roar.source.isPlaying,"共鸣者死亡立即停止吼叫");roaring.gameObject.SetActive(false);
            var concrete=GameObject.CreatePrimitive(PrimitiveType.Cube);concrete.transform.position=player.transform.position+Vector3.right*15;
            Check(SurfaceAudio.ContactClip(concrete.GetComponent<Collider>(),true)==profile.concreteImpact,"拳套命中混凝土使用 Impact Blunt Large Stone");concrete.SetActive(false);
            var mist=Resources.Load<GauntletImpactVFXProfile>("GauntletImpactVFX").bloodMist.GetComponent<ParticleSystem>();var burst=mist.emission.GetBurst(0);
            Check(mist.emission.burstCount==1&&burst.minCount==4&&burst.maxCount==5&&burst.cycleCount==1&&mist.emission.rateOverTime.constant==0&&!mist.main.loop&&mist.main.startSpeed.constant==0,"命中血雾为原地单次 4–5 粒，不连续喷射");
            var blood=UnityEngine.Object.Instantiate(mist);blood.transform.position=camera.transform.position+camera.transform.forward;blood.Play();blood.Simulate(.1f,true,false);Check(blood.particleCount>=4&&blood.particleCount<=5,"真实粒子模拟只生成四到五粒紫色血雾",new{count=blood.particleCount});Capture(camera,"udk7m-purple-blood");blood.gameObject.SetActive(false);
            for(int i=0;i<3;i++)
            {
                var go=UnityEngine.Object.Instantiate(arena.localDebrisPrefab);var debris=go.GetComponent<UDK7MDebris>()??go.AddComponent<UDK7MDebris>();debris.Begin(arena,player.transform.position+Vector3.up*8,i>0,i==2);
                var size=go.GetComponent<BoxCollider>().size;float diameter=Mathf.Max(size.x,Mathf.Max(size.y,size.z));float expected=i==0?profile.rockDiameter:i==1?profile.crystalDiameter:profile.centralCrystalDiameter;
                Check(debris.VisualMesh&&Mathf.Abs(diameter-expected)<.01f,"坠落外形与碰撞尺寸已归一化 "+i,new{mesh=debris.VisualMesh.name,diameter});
                var hull=debris.PhysicalCollider as MeshCollider;Physics.SyncTransforms();Check(hull&&hull.convex&&hull.sharedMesh==debris.VisualMesh&&hull.transform==debris.VisualRenderer.transform&&!go.GetComponent<BoxCollider>().enabled&&hull.bounds.size.sqrMagnitude>.1f,"落物凸包与显示 mesh 共用几何、缩放和旋转 "+i);
                var vertexBounds=RockGeometryUtility.WorldVertexBounds(debris.VisualMesh,debris.VisualRenderer.transform);
                Check(Vector3.Distance(hull.bounds.center,vertexBounds.center)<.04f&&Vector3.Distance(hull.bounds.size,vertexBounds.size)<.1f,"实际物理包围范围贴合真实 mesh 顶点 "+i,new{physical=hull.bounds.size.ToString("F3"),vertices=vertexBounds.size.ToString("F3")});
                if(i==0){Check(profile.rockMeshes.Contains(debris.VisualMesh)&&debris.VisualRenderer.sharedMaterial==arena.rockMaterial,"落石使用新增 mesh 并保留原有石材");int index=Array.IndexOf(profile.rockMeshes,debris.VisualMesh);var faceNormal=Quaternion.Inverse(profile.rockFaceDown[index])*Vector3.down;Check(Vector3.Angle(debris.VisualRenderer.transform.rotation*faceNormal,Vector3.down)<=profile.rockTiltDegrees+1,"落石最大面朝下，随机变体保留小幅倾斜");}go.SetActive(false);
            }
            var warningPoint=player.transform.position+player.transform.forward*5;
            var warning=arena.CreateFallingWarning(warningPoint,false);var warningRenderer=warning.GetComponent<Renderer>();
            Check(profile.rockDiameter>=2&&warning.transform.localScale.x>profile.rockDiameter&&warningRenderer.sharedMaterial==profile.fallingWarning&&profile.fallingWarning.GetColor("_Color").a>0&&profile.fallingWarning.GetColor("_Color").a<1&&!warning.GetComponent<Collider>(),"放大落石同步扩大半透明警示圈，圈本身无碰撞",new{rockDiameter=profile.rockDiameter,warningDiameter=warning.transform.localScale.x});
            var previewRock=UnityEngine.Object.Instantiate(arena.localDebrisPrefab);var preview=previewRock.GetComponent<UDK7MDebris>()??previewRock.AddComponent<UDK7MDebris>();preview.Begin(arena,warningPoint+Vector3.up*1.4f,false);Capture(camera,"udk7m-large-rock-warning");previewRock.SetActive(false);warning.SetActive(false);
            var original=arena.boss.GetComponentsInChildren<Renderer>().Where(r=>r is MeshRenderer||r is SkinnedMeshRenderer).ToDictionary(r=>r,r=>r.sharedMaterials);
            arena.BeginFight();Check(arena.pillars.First(p=>!p.Consumed).Consume(arena.boss),"真实撞柱入口进入过载蓄能");
            var electric=arena.boss.GetComponent<ElectricSurge>();Check(electric&&electric.Playing&&electric.OverlayCount>0,"撞晶柱触发短促模型电流覆盖");
            Check(original.All(pair=>pair.Key.sharedMaterials.SequenceEqual(pair.Value)),"电流覆层保留原模型材质");Capture(camera,"udk7m-pillar-surge");electric.Tick(profile.electricitySeconds+.01f);Check(!electric.Playing,"电流覆盖在配置时间后结束");
            var presentation=arena.boss.GetComponent<UDK7MPresentation>();int releases=presentation.OverloadReleasesPlayed;
            int particles=UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(p=>p.particleCount);
            for(int i=0;i<10;i++)arena.boss.Tick(arena.tuning.overloadChargeSeconds/10f);
            arena.boss.Tick(.02f);
            Check(arena.boss.Phase==MachinePhase.Overloaded&&presentation.OverloadReleasesPlayed==releases+1,"真实完整过载等待结束，以一次色散冲击进入二阶段");
            Check(UnityEngine.Object.FindObjectsByType<ParticleSystem>(FindObjectsSortMode.None).Sum(p=>p.particleCount)==particles,"整个过载阶段不再每四分之一秒生成青蓝方块粒子");
            var releaseWave=presentation.LastOverloadWave.GetComponent<ChromaticWaveController>();Check(releaseWave&&releaseWave.chromaticPixels>0&&releaseWave.distortionPixels>0,"二阶段释放使用实际色散/扭曲波控制器");
            releaseWave.SetNormalizedTime(.4f);screen.Tick(.03f,0,0,1);Capture(camera,"udk7m-phase-two-chromatic-release");
            foreach(var lease in UnityEngine.Object.FindObjectsByType<CombatEffectLease>(FindObjectsSortMode.None))lease.gameObject.SetActive(false);
            ScreenTests(arena,screen,camera);
        }
        static void ScreenTests(UDK7MArena arena,ScreenEffectsController screen,Camera camera)
        {
            var settings=screen.profile;var material=screen.RuntimeMaterial;screen.automaticInputs=false;screen.Stop();screen.Tick(1,0,0,1);
            foreach(var f in settings.rendererFeatures)f.SetActive(false);Capture(camera,"warmup");var baseline=Capture(camera,"screen-baseline");foreach(var f in settings.rendererFeatures)f.SetActive(true);
            screen.forcePassForPreview=true;material.SetFloat("_Enabled",0);Check(Difference(baseline,Capture(camera,"disabled-pass"))<80,"完整 Pass 禁用时透传原图");
            screen.Tick(0,0,0,1);Check(Difference(baseline,Capture(camera,"zero-intensity"))<80,"零强度不黑屏、不偏色");
            screen.PlayRadial(new Vector2(.5f,.5f));screen.Tick(.04f,0,0,1);material.SetFloat("_Strength",0);Check(Difference(baseline,Capture(camera,"zero-strength"))<80,"径向范围为零时保持原图");
            screen.Stop();screen.PlayRadial(new Vector2(.5f,.5f));screen.Tick(.04f,0,0,1);int radialPixels=Difference(baseline,Capture(camera,"radial-centre"));Check(radialPixels>2000,"真实 UDK7M 画面产生径向拉伸",new{radialPixels});
            screen.Stop();screen.PlayRadial(new Vector2(.25f,.5f));screen.Tick(.04f,0,0,1);Capture(camera,"radial-left");Check(Mathf.Abs(screen.Centre.x-.25f)<.001f,"偏左径向中心正确");
            screen.Stop();screen.PlayRadial(new Vector2(.75f,.5f));screen.Tick(.04f,0,0,1);Capture(camera,"radial-right");Check(Mathf.Abs(screen.Centre.x-.75f)<.001f,"偏右径向中心正确");
            var quality=settings.quality;
            try{foreach(var q in new[]{ScreenEffectsProfile.SampleQuality.Eight,ScreenEffectsProfile.SampleQuality.Twelve,ScreenEffectsProfile.SampleQuality.Sixteen}){settings.quality=q;screen.Tick(0,0,0,1);int n=Difference(baseline,Capture(camera,"radial-"+(int)q));Check(n>2000,"采样档位实际渲染 "+(int)q);}}
            finally{settings.quality=quality;}
            screen.Tick(.5f,0,0,1);Check(screen.RadialIntensity==0,"冲击脉冲自动衰减为零");screen.Tick(.5f,35,0,1);Check(screen.RadialIntensity>.4f,"高速运动增强径向模糊");Capture(camera,"speed-blur");screen.Tick(.5f,0,0,1);Check(screen.RadialIntensity==0,"减速后恢复清晰");
            int received=screen.SignalsReceived;ScreenEffectSignals.Emit(ScreenCue.Resonance,camera.transform.position+Vector3.forward*1000,1,20);Check(screen.SignalsReceived==received,"范围外共鸣爆炸不影响相机");
            ScreenEffectSignals.Emit(ScreenCue.Resonance,camera.transform.position+camera.transform.forward*3,1,28);screen.Tick(.03f,0,0,1);Check(screen.PurpleNoise>0&&screen.ChromaticPixels>0&&screen.RadialIntensity>0,"附近共鸣爆炸同时触发径向模糊、紫色边缘噪波与色散");Capture(camera,"resonance-screen");
            screen.Stop();screen.Tick(.5f,0,0,1);material.SetFloat("_PurpleNoise",.4f);material.SetFloat("_EffectTime",0);var noise=Capture(camera,"noise-uv-zero");material.SetFloat("_EffectTime",2);Check(Difference(noise,Capture(camera,"noise-uv-moved"))>300,"噪波随时间平移 UV");
            screen.Stop();screen.Tick(1,0,.33f,1);float v1=screen.Vignette;screen.Tick(1,0,.66f,1);float v2=screen.Vignette;screen.Tick(1,0,1,1);Check(v1>0&&v2>v1&&screen.Vignette>v2&&screen.Vignette<=.15f,"蓄力三级渐增轻量暗角");Capture(camera,"charge-vignette");
            screen.Tick(1,0,0,.1f);Check(screen.RedEdge>0&&screen.RedEdge<.5f,"低血量启用透明不规则红边");Capture(camera,"low-health-edge");screen.Tick(1,0,0,1);Check(screen.RedEdge==0,"恢复生命后红边消失");
            foreach(var pair in new[]{new{label="front",direction=camera.transform.forward,yaw=0},new{label="back",direction=-camera.transform.forward,yaw=0},new{label="left",direction=-camera.transform.right,yaw=1},new{label="right",direction=camera.transform.right,yaw=-1}})
            {
                screen.Stop();arena.player.gauntlet.Cancel();arena.player.ReceiveStrike(new ActionDamage(1,camera.transform.position+pair.direction*3));screen.Tick(.08f,0,0,1);Capture(camera,"hurt-"+pair.label);
                Check(screen.LastCameraAngles.x<0&&(pair.yaw==0?Mathf.Abs(screen.LastCameraAngles.y)<.001f:screen.LastCameraAngles.y*pair.yaw>0),"实际受击方向镜头反馈 "+pair.label,new{angles=screen.LastCameraAngles.ToString("F4")});
            }
            var hurt=arena.player.GetComponent<GauntletCombatFeedback>();Check(hurt.HurtsPlayed>0&&hurt.profile.playerHurts.Contains(hurt.LastHurtClip),"原版主角真实扣血后播放受击音效");
            screen.Stop();screen.PlayRadial(new Vector2(.5f,.5f));screen.Tick(.04f,0,0,1);Capture(camera,"radial-wide",1600,700);Capture(camera,"radial-720p",1280,720);
            Check(settings.rendererFeatures.All(f=>f.passMaterial==null)&&settings.material.GetFloat("_Intensity")==0,"渲染结束解除材质绑定，不污染其他相机或共享材质");
            Check(!ShaderUtil.ShaderHasError(settings.material.shader),"三个采样变体编译后仍无 Shader 错误");
            File.WriteAllText(Stage+"render-notes.json",JsonConvert.SerializeObject(new{resolutions=new[]{"960x540","1280x720","1600x700"},tapVariants=new[]{8,12,16},radialPixels,gpuTiming="GPU timestamp measurement not available in this automated camera-render test; no GPU milliseconds claimed.",limits="Single PC gameplay camera verified. Mobile hardware, XR and dynamic resolution unverified."},Formatting.Indented));
        }
    }
}
