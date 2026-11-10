using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using GameActions;
using Resonator.AI;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static class SurfaceVFXSetup
    {
        const string Stage="AssetStaging/VFXPhase2/",Root="Assets/Game/VFX/Combat/",ProfilePath=Root+"Advanced/Resources/GauntletImpactVFX.asset";
        static SurfaceVFXSetup(){EditorApplication.update+=()=>{if(EditorApplication.isPlayingOrWillChangePlaymode||EditorApplication.isCompiling||EditorApplication.isUpdating)return;foreach(var task in new[]{"SurfaceVFXSetup","SurfaceVFXVerify","EffectPreviewVerify","EffectPreviewOpen"})if(File.Exists(Stage+task+".request")){File.Delete(Stage+task+".request");try{if(task=="SurfaceVFXSetup")Setup();else if(task=="SurfaceVFXVerify")Verify();else if(task=="EffectPreviewOpen"){CombatEffectPreviewWindow.Open();}else PreviewVerify();}catch(Exception e){File.WriteAllText(Stage+task+"_error.txt",e.ToString());Debug.LogException(e);}return;}};}
        static T Load<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static void CleanSteam(string path)
        {
            var stage=PrefabStageUtility.GetCurrentPrefabStage();if(stage&&stage.assetPath==path)throw new Exception("Close the open steam prefab before repairing it: "+path);
            var backup=Stage+Path.GetFileNameWithoutExtension(path)+"_BeforeCubeCleanup.prefab";if(!File.Exists(backup))File.Copy(path,backup);
            var root=PrefabUtility.LoadPrefabContents(path);
            try{foreach(var t in root.GetComponentsInChildren<Transform>(true))if(t.name=="Cube"&&t.GetComponent<BoxCollider>()&&t.GetComponent<MeshFilter>())UnityEngine.Object.DestroyImmediate(t.gameObject);PrefabUtility.SaveAsPrefabAsset(root,path);}
            finally{PrefabUtility.UnloadPrefabContents(root);}
        }
        static GameObject SmallBurst(string original,string name,float countScale)
        {
            string path=Root+"Advanced/Prefabs/"+name+".prefab";var existing=Load<GameObject>(path);if(existing)return existing;
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root+original+".prefab"),scene);go.name=name;
                foreach(var ps in go.GetComponentsInChildren<ParticleSystem>(true))
                {
                    var main=ps.main;main.maxParticles=Mathf.Max(8,Mathf.CeilToInt(main.maxParticles*countScale));
                    var emission=ps.emission;for(int i=0;i<emission.burstCount;i++){var burst=emission.GetBurst(i);burst.count=new ParticleSystem.MinMaxCurve(Mathf.Max(1,Mathf.CeilToInt(burst.count.constantMin*countScale)),Mathf.Max(1,Mathf.CeilToInt(burst.count.constantMax*countScale)));emission.SetBurst(i,burst);}
                }
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
        }
        static void Setup()
        {
            CleanSteam(Root+"TankSteam.prefab");CleanSteam(Root+"Advanced/Prefabs/TankSteamAdvanced.prefab");
            var data=Load<GauntletImpactVFXProfile>(ProfilePath);if(!data){data=ScriptableObject.CreateInstance<GauntletImpactVFXProfile>();AssetDatabase.CreateAsset(data,ProfilePath);}
            data.metalSpark=Load<GameObject>(Root+"Sparks.prefab");data.concreteDebris=Load<GameObject>(Root+"ConcreteImpactLight.prefab");
            data.concreteCracks=new[]{Load<GameObject>(Root+"DrillCrater_1.prefab"),Load<GameObject>(Root+"DrillCrater_2.prefab")};
            data.bloodMist=SmallBurst("PurpleBloodBurst","GauntletBloodHit",.25f);data.crystalFragments=SmallBurst("VioletCrystalDebris","GauntletCrystalHit",.25f);
            EditorUtility.SetDirty(data);AssetDatabase.SaveAssetIfDirty(data);
            File.WriteAllText(Stage+"surface_vfx_setup.json",JsonConvert.SerializeObject(new{passed=true,profile=ProfilePath,steamCubesRemoved=true,decalRelativeScale=data.concreteDecalScale,craterDiameter=data.referenceCraterDiameter*data.concreteDecalScale},Formatting.Indented));
        }
        [MenuItem("Tools/Combat VFX/Select fist impact settings")]static void Select()=>Selection.activeObject=Load<GauntletImpactVFXProfile>(ProfilePath);
        static void Verify()
        {
            var data=Load<GauntletImpactVFXProfile>(ProfilePath);var checks=new List<string>();
            void Check(bool ok,string name){if(!ok)throw new Exception(name);checks.Add(name);}
            Check(data&&data.metalSpark&&data.concreteDebris&&data.bloodMist&&data.crystalFragments&&data.concreteCracks.All(c=>c),"All surface variants reference valid resources");
            foreach(string path in new[]{Root+"TankSteam.prefab",Root+"Advanced/Prefabs/TankSteamAdvanced.prefab"})
            {
                var go=PrefabUtility.LoadPrefabContents(path);try{Check(!go.GetComponentsInChildren<Transform>(true).Any(t=>t.name=="Cube"&&t.GetComponent<MeshRenderer>()),"No placeholder cube: "+path);Check(go.GetComponentsInChildren<ParticleSystem>(true).Length>0,"Steam particles preserved: "+path);}finally{PrefabUtility.UnloadPrefabContents(go);}
            }
            var scene=EditorSceneManager.NewPreviewScene();
            try
            {
                var root=new GameObject("Surface test only");UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(root,scene);var effects=root.AddComponent<GauntletSurfaceVFX>();effects.profile=data;
                foreach(var kind in new[]{SoundSurface.Metal,SoundSurface.Concrete,SoundSurface.Flesh})
                {
                    var surface=GameObject.CreatePrimitive(PrimitiveType.Cube);UnityEngine.SceneManagement.SceneManager.MoveGameObjectToScene(surface,scene);surface.AddComponent<SurfaceAudio>().surface=kind;
                    effects.Contact(surface.GetComponent<Collider>(),Vector3.zero,Vector3.forward);Check(effects.LastSurface==kind,"Surface dispatch: "+kind);
                    if(kind==SoundSurface.Concrete){var crack=effects.LastDecal.GetComponent<GroundCrackImpact>();Check(Mathf.Abs(effects.LastDecal.transform.localScale.x-3.4f*.2f)<.0001f,"Crater has one fifth SlamImpact diameter");Check(Vector3.Dot(-crack.transform.forward,Vector3.forward)>.999f,"Crater follows wall contact normal");Check(Mathf.Abs(crack.depth-.04f)<.0001f,"POM depth scales down with fist crater");}
                }
                // CombatEffects owns a bounded shared pool, including these temporary instances.
                var pool=UnityEngine.Object.FindObjectsByType<CombatEffectLease>(FindObjectsInactive.Include,FindObjectsSortMode.None);var roots=pool.Where(p=>p&&p.transform.root.name=="Combat VFX Pool").Select(p=>p.transform.root.gameObject).Distinct().ToArray();foreach(var item in roots)UnityEngine.Object.DestroyImmediate(item);
            }
            finally{EditorSceneManager.ClosePreviewScene(scene);}
            VerifyContactGates(Check);
            var pulseObject=new GameObject("Temporary pulse check");pulseObject.hideFlags=HideFlags.HideAndDontSave;
            try{var pulse=pulseObject.AddComponent<VFXLightPulse>();pulse.peakIntensity=10;pulse.attackSeconds=.04f;pulse.duration=.65f;Check(pulse.IntensityAt(0)==0&&Mathf.Abs(pulse.IntensityAt(.04f)-10)<.001f&&pulse.IntensityAt(.3f)<10&&pulse.IntensityAt(.65f)==0,"Pillar light rises quickly, then fades completely");for(int i=0;i<4;i++){var p=new GameObject("Temporary silent pillar").AddComponent<UDK7M.UDK7MCrystalPillar>();p.transform.SetParent(pulseObject.transform);p.crystals=new GameObject("Crystals");p.crystals.transform.SetParent(p.transform);p.Shatter(Vector3.zero,false);Check(p.Consumed&&!p.crystals.activeSelf,"Linked pillar disappears without spawning VFX: "+i);}}
            finally{UnityEngine.Object.DestroyImmediate(pulseObject);}
            File.WriteAllText(Stage+"surface_vfx_verify.json",JsonConvert.SerializeObject(new{passed=true,checks},Formatting.Indented));
        }
        static void VerifyContactGates(Action<bool,string> check)
        {
            var owner=new GameObject("Temporary contact gate test");owner.hideFlags=HideFlags.HideAndDontSave;owner.transform.position=new Vector3(10000,10000,10000);
            var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);wall.hideFlags=HideFlags.HideAndDontSave;wall.transform.position=owner.transform.position+Vector3.forward;
            var track=ScriptableObject.CreateInstance<ResonatorMotionTrack>();var clip=new AnimationClip();clip.SetCurve("Pose",typeof(Transform),"m_LocalPosition.x",AnimationCurve.Constant(0,1,0));track.clip=clip;track.framesPerSecond=30;
            var sound=AudioClip.Create("Silent test contact",100,1,1000,false);var target=new GameObject("Temporary target");target.hideFlags=HideFlags.HideAndDontSave;target.transform.position=wall.transform.position;
            try
            {
                var pose=new GameObject("Pose");pose.transform.SetParent(owner.transform,false);var motion=owner.AddComponent<ActionMotionPlayer>();motion.animator=owner.AddComponent<Animator>();
                var hit=new HitWindow{startFrame=0,endFrame=6,position=Vector3.forward,size=Vector3.one*1.5f,contactSound=sound,damage=1};var action=new ActionDefinition{isAttack=true};action.action.hits=new[]{hit};action.action.animation.track=track;
                var boxes=new ActionHitbox(owner.transform);var contacts=new ActionContact();int count=0;contacts.SurfaceContactDelivered+=(c,p,n)=>count++;motion.Play(action.Motion,0,true);Physics.SyncTransforms();
                contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);motion.Seek(2);contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);
                check(count==1,"Environment VFX is once per strike, despite resampling");
                contacts.Reset();contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);check(count==2,"Next strike can affect the same surface again");
                contacts.Reset();motion.Seek(10);contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);check(count==2,"Windup/recovery outside hit window produces no contact VFX");
                wall.SetActive(false);var receiver=target.AddComponent<SurfaceVFXTestTarget>();target.AddComponent<BoxCollider>();var extra=new GameObject("Second hit collider");extra.transform.SetParent(target.transform,false);extra.AddComponent<BoxCollider>();Physics.SyncTransforms();contacts.Reset();motion.Seek(2);
                contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);check(receiver.hits==1&&count==3,"Enemy hit and VFX are deduplicated across multiple colliders");
                contacts.Reset();receiver.alive=false;contacts.Resolve(action,motion,boxes,owner.transform,owner.transform.position,~0,0,.02f);check(receiver.hits==1&&count==3,"Dead target does not produce a new successful-hit burst");
            }
            finally{UnityEngine.Object.DestroyImmediate(owner);UnityEngine.Object.DestroyImmediate(wall);UnityEngine.Object.DestroyImmediate(target);UnityEngine.Object.DestroyImmediate(track);UnityEngine.Object.DestroyImmediate(clip);UnityEngine.Object.DestroyImmediate(sound);}
        }
        static void PreviewVerify()
        {
            var window=ScriptableObject.CreateInstance<CombatEffectPreviewWindow>();
            // GPU simulation/event buffers need real render ticks, rather than a series
            // of Simulate calls and a single readback in the same editor callback.
            int tick=0,crystalPixels=0,maxAlive=0;double maxSimulationTime=0;float difference=0;Color32[] baseline=null;var timeline=new List<object>();double next=0;
            window.Load(Load<GameObject>(Root+"Sparks.prefab"));baseline=window.CaptureFrame().GetPixels32();
            EditorApplication.CallbackFunction advance=null;
            advance=()=>
            {
                try
                {
                    if(EditorApplication.isPlayingOrWillChangePlaymode)throw new Exception("Preview validation interrupted by Play Mode.");
                    if(EditorApplication.timeSinceStartup<next)return;next=EditorApplication.timeSinceStartup+.04;
                    EditorApplication.QueuePlayerLoopUpdate();var frame=window.CaptureFrame();tick++;
                    foreach(var v in window.Effects){var names=new List<string>();v.GetSpawnSystemNames(names);foreach(string n in names)maxSimulationTime=Math.Max(maxSimulationTime,v.GetSpawnSystemInfo(n).totalTime);maxAlive=Math.Max(maxAlive,v.aliveParticleCount);timeline.Add(new{tick,asset=v.visualEffectAsset.name,alive=v.aliveParticleCount,paused=v.pause,spawn=names.Select(n=>new{name=n,time=v.GetSpawnSystemInfo(n).totalTime}).ToArray()});}
                    if(tick==2){File.WriteAllBytes(Stage+"prefab_preview_sparks.png",frame.EncodeToPNG());var a=baseline;var b=frame.GetPixels32();for(int i=0;i<a.Length;i++)difference+=Mathf.Abs(a[i].r-b[i].r)+Mathf.Abs(a[i].g-b[i].g)+Mathf.Abs(a[i].b-b[i].b);difference/=a.Length*3f;}
                    if(tick==4)window.Load(Load<GameObject>(Root+"Advanced/Prefabs/CrystalShatterGPU.prefab"));
                    if(tick>=8){int visible=frame.GetPixels32().Count(c=>c.b>c.g*1.2f+8&&c.r>c.g*1.1f+5);crystalPixels=Math.Max(crystalPixels,visible);if(tick==18)File.WriteAllBytes(Stage+"prefab_preview_crystal.png",frame.EncodeToPNG());}
                    UnityEngine.Object.DestroyImmediate(frame);
                    if(tick<22)return;
                    File.WriteAllText(Stage+"prefab_preview_verify.json",JsonConvert.SerializeObject(new{passed=difference>.01f&&crystalPixels>20&&maxAlive>0&&maxSimulationTime>.2,sparkFrameDifference=difference,crystalVisiblePixels=crystalPixels,maxAlive,maxSimulationTime,timeline,images=new[]{"prefab_preview_sparks.png","prefab_preview_crystal.png"}},Formatting.Indented));
                }
                catch(Exception e){File.WriteAllText(Stage+"EffectPreviewVerify_error.txt",e.ToString());}
                EditorApplication.update-=advance;if(window)UnityEngine.Object.DestroyImmediate(window);
            };
            EditorApplication.update+=advance;
        }
    }
    public sealed class SurfaceVFXTestTarget:MonoBehaviour,IActionTarget
    {
        public bool alive=true;public int hits;public bool ActionTargetAlive=>alive;public Transform ActionRoot=>transform;public Vector3 ActionAimPoint=>transform.position;
        public void ReceiveActionHit(float damage,Vector3 source,float impulse){hits++;}
    }
}
