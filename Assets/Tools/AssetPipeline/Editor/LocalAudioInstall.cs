using System;using System.IO;using System.Linq;using Newtonsoft.Json;using UnityEditor;using UnityEditor.SceneManagement;using UnityEngine;using UnityEngine.SceneManagement;using GameActions;using UDK7M;
namespace AssetPipeline.Editor {
[InitializeOnLoad] public static class LocalAudioInstall {
 const string Stage="AssetStaging/UDK7M/Boss/";static AudioClip[] clips;static SurfaceSoundLibrary library;static bool busy;
 static LocalAudioInstall(){EditorApplication.update+=Poll;}
 static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"LocalAudio.request"))return;File.Delete(Stage+"LocalAudio.request");Run();}
 static AudioClip[] Match(params string[] names)=>clips.Where(c=>names.Any(n=>string.Equals(c.name,n,StringComparison.OrdinalIgnoreCase))).GroupBy(c=>c.name,StringComparer.OrdinalIgnoreCase).Select(g=>g.First()).ToArray();
 static AudioClip[] Prefix(string prefix)=>clips.Where(c=>c.name.StartsWith(prefix,StringComparison.OrdinalIgnoreCase)).ToArray();
 static void Bind(GameObject go,params ObjectSound[] sounds){var a=go.GetComponent<ObjectAudio>();if(!a)a=go.AddComponent<ObjectAudio>();a.sounds=sounds;var source=go.GetComponent<AudioSource>();source.playOnAwake=false;source.spatialBlend=.4f;source.maxDistance=100;if(!a.source){var child=new GameObject("Event AudioSource");child.transform.SetParent(go.transform,false);a.source=child.AddComponent<AudioSource>();a.source.playOnAwake=false;a.source.spatialBlend=.4f;}a.source.minDistance=10;a.source.maxDistance=110;}
 static ObjectSound Cue(UDK7MSound cue,params string[] names)
 {
  var s=new ObjectSound{cue=cue,clips=Match(names),spatialBlend=.4f,minDistance=10,maxDistance=110};
  switch(cue)
  {
   case UDK7MSound.SlamLight:s.spatialBlend=.15f;s.minDistance=24;s.maxDistance=140;s.volume=.8f;s.priority=32;break;
   case UDK7MSound.SlamHeavy:s.spatialBlend=.1f;s.minDistance=30;s.maxDistance=160;s.volume=.95f;s.priority=24;break;
   case UDK7MSound.TripleFinalImpact:s.spatialBlend=.05f;s.minDistance=30;s.maxDistance=160;s.volume=1;s.priority=24;break;
   case UDK7MSound.ResonanceBurst:case UDK7MSound.GroundPoundImpact:case UDK7MSound.OverloadCharge:s.spatialBlend=0;s.volume=.95f;s.priority=16;break;
   case UDK7MSound.Defeat:s.spatialBlend=0;s.volume=.9f;s.priority=16;break;
   case UDK7MSound.ChargeImpact:s.spatialBlend=.2f;s.minDistance=20;s.maxDistance=140;s.volume=.8f;s.priority=32;break;
   case UDK7MSound.PillarShatter:s.spatialBlend=.55f;s.volume=.2f;s.priority=120;break;
   case UDK7MSound.PillarRegrow:s.spatialBlend=.55f;s.volume=.25f;break;
   case UDK7MSound.TetherStart:case UDK7MSound.TetherAttach:case UDK7MSound.GauntletSwing:s.spatialBlend=.2f;s.volume=.5f;break;
   case UDK7MSound.ParryImpact:s.spatialBlend=.15f;s.volume=.7f;break;
   case UDK7MSound.PureCrystalCharge:case UDK7MSound.GroundPoundStart:s.spatialBlend=.1f;break;
   case UDK7MSound.RockLanding:case UDK7MSound.CrystalLanding:s.spatialBlend=.35f;s.minDistance=14;s.maxDistance=120;break;
   case UDK7MSound.Crush:s.spatialBlend=.6f;s.volume=.35f;break;
  }
  return s;
 }
 static void Surface(Collider c){var s=c.GetComponent<SurfaceAudio>();if(!s)s=c.gameObject.AddComponent<SurfaceAudio>();s.library=library;s.surface=SurfaceAudio.Classify(c);}
 static void Prepare(GameObject root)
 {
  const string name="Prepare Warning Loop AudioSource";var t=root.transform.Find(name);var go=t?t.gameObject:new GameObject(name);go.transform.SetParent(root.transform,false);go.transform.localPosition=Vector3.up*2;
  var source=go.GetComponent<AudioSource>();if(!source)source=go.AddComponent<AudioSource>();source.clip=Match("metal_scrape_loop").FirstOrDefault();source.loop=true;source.playOnAwake=false;source.volume=.6f;source.spatialBlend=.25f;source.minDistance=20;source.maxDistance=120;source.rolloffMode=AudioRolloffMode.Linear;source.priority=40;
  var control=go.GetComponent<MachinePrepareAudio>();if(!control)control=go.AddComponent<MachinePrepareAudio>();control.source=source;
 }
 static void Charge(GameObject root)
 {
  var charge=root.GetComponent<GauntletChargeAudio>();if(!charge)charge=root.AddComponent<GauntletChargeAudio>();charge.EnsureSources();charge.stages.clip=Match("三段蓄力").FirstOrDefault();charge.fullLoop.clip=Match("蓄力满循环音效").FirstOrDefault();
  charge.stages.spatialBlend=charge.fullLoop.spatialBlend=0;charge.stages.loop=false;charge.fullLoop.loop=true;
 }
 static void Clean(GameObject root){foreach(var t in root.GetComponentsInChildren<Transform>(true).Where(t=>t.name=="Contact AudioSource").ToArray())UnityEngine.Object.DestroyImmediate(t.gameObject);foreach(var tag in root.GetComponentsInChildren<SurfaceAudio>(true)){var c=tag.GetComponent<Collider>()??tag.GetComponentInChildren<Collider>();if(!tag.GetComponent<UDK7MDebris>()&&(tag.overrides==null||tag.overrides.Length==0)&&tag.surface==SurfaceAudio.Classify(c))UnityEngine.Object.DestroyImmediate(tag);}}
 static void Contacts(GameObject root,Transform anchor){var c=root.GetComponent<SurfaceContactAudio>();if(!c)c=root.AddComponent<SurfaceContactAudio>();c.attachment=anchor;c.EnsureSources();foreach(var voice in c.sources){voice.spatialBlend=.6f;voice.minDistance=3;voice.maxDistance=80;}}
 static void Loop(GameObject parent,string name,AudioClip clip,bool drill,Vector3 local){var t=parent.transform.Find(name);var go=t?t.gameObject:new GameObject(name);go.transform.SetParent(parent.transform,false);go.transform.localPosition=local;var source=go.GetComponent<AudioSource>();if(!source)source=go.AddComponent<AudioSource>();source.clip=clip;source.loop=true;source.playOnAwake=false;source.spatialBlend=drill?.85f:.95f;source.rolloffMode=AudioRolloffMode.Logarithmic;source.volume=drill?.35f:.45f;source.minDistance=3;source.maxDistance=55;var loop=go.GetComponent<MachineLoopAudio>();if(!loop)loop=go.AddComponent<MachineLoopAudio>();loop.drill=drill;loop.source=source;}
 static void Boss(GameObject go){var drill=go.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="DrillRoll");Loop(go,"Engine AudioSource",Match("机器轰鸣").FirstOrDefault(),false,new Vector3(0,2,-1.5f));if(drill){Loop(drill.gameObject,"Drill Loop AudioSource",Match("电钻循环").FirstOrDefault(),true,Vector3.up*.8f);Bind(drill.gameObject,Cue(UDK7MSound.SlamLight,"BigMetalImpact"),Cue(UDK7MSound.SlamHeavy,"BigMetalImpact"),Cue(UDK7MSound.ChargeImpact,"BigMetalImpact"),Cue(UDK7MSound.TripleFinalImpact,"BigMetalImpact4"),Cue(UDK7MSound.DrillCapture,"钻头进入物体"),Cue(UDK7MSound.ResonanceBurst,"大共振音效"));}
 Bind(go,Cue(UDK7MSound.ChargeImpact,"BigMetalImpact"),Cue(UDK7MSound.OverloadCharge,"大共振音效"),Cue(UDK7MSound.Defeat,"大爆炸"),Cue(UDK7MSound.Crush,"BigImpact"));Contacts(go,drill?drill:go.transform);Prepare(go);foreach(var feet in go.GetComponentsInChildren<PlayerObjectAudio>(true)){var footSource=feet.stepSource;if(footSource)UnityEngine.Object.DestroyImmediate(footSource.gameObject);UnityEngine.Object.DestroyImmediate(feet);}}
 static void Player(GameObject go){Bind(go,Cue(UDK7MSound.GauntletSwing,"kick_swish"),Cue(UDK7MSound.ParryImpact,"handguard_impact_enemy"),Cue(UDK7MSound.TetherStart,"CrystalGenericCombined_02"),Cue(UDK7MSound.TetherAttach,"CrystalGenericCombined_03"),Cue(UDK7MSound.PureCrystalCharge,"CrystalHealingMiningC_3"),Cue(UDK7MSound.GroundPoundStart,"kick_swish"),Cue(UDK7MSound.GroundPoundImpact,"大共振音效"));var steps=go.GetComponent<PlayerObjectAudio>();if(!steps)steps=go.AddComponent<PlayerObjectAudio>();steps.EnsureSource();steps.stepSource.spatialBlend=.3f;Charge(go);Contacts(go,go.transform);}
 static void Run(){busy=true;var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(UDK7MThreePhaseBuilder.ScenePath);bool opened=false;try{
 clips=AssetDatabase.FindAssets("t:AudioClip",new[]{"Assets/Game"}).Select(AssetDatabase.GUIDToAssetPath).Select(AssetDatabase.LoadAssetAtPath<AudioClip>).Where(c=>c).ToArray();Directory.CreateDirectory(Stage+"BeforeLocalAudio");
 string path="Assets/Game/Shared/Actions/Resources/SurfaceSounds.asset";library=AssetDatabase.LoadAssetAtPath<SurfaceSoundLibrary>(path);if(!library){library=ScriptableObject.CreateInstance<SurfaceSoundLibrary>();Directory.CreateDirectory(Path.GetDirectoryName(path));AssetDatabase.CreateAsset(library,path);}library.surfaces=new[]{new SurfaceSounds{surface=SoundSurface.Metal,contacts=Match("metal_clang5","metalgrate_land1","metalgrate_land2","metalgrate_land3"),steps=Prefix("metalgrate_step"),landings=Match("metalgrate_land1","metalgrate_land2","metalgrate_land3"),fist=Match("BigMetalImpact3").FirstOrDefault()},new SurfaceSounds{surface=SoundSurface.Concrete,contacts=Prefix("stone_impact"),steps=Prefix("concrete_step"),landings=Prefix("concrete_land")},new SurfaceSounds{surface=SoundSurface.Wood,contacts=Prefix("wood_impact"),steps=Prefix("wood_step"),landings=Prefix("wood_land")},new SurfaceSounds{surface=SoundSurface.Crystal,contacts=Match("glass_break1","glass_break2").Concat(Prefix("CrystalGenericCombined")).ToArray(),steps=Prefix("glasssolid_step"),landings=Prefix("glasssolid_land")},new SurfaceSounds{surface=SoundSurface.Flesh,contacts=Prefix("a_bloodsplat"),steps=Prefix("concrete_step"),landings=Prefix("concrete_land")},new SurfaceSounds{surface=SoundSurface.Plastic,contacts=Prefix("bullet-impact-plastic"),steps=Prefix("concrete_step"),landings=Prefix("concrete_land")}};EditorUtility.SetDirty(library);AssetDatabase.SaveAssetIfDirty(library);
 foreach(var p in new[]{"Assets/Game/Enemies/UDK7M/Prefabs/UDK7M.prefab","Assets/Game/Enemies/UDK7M/AI/Prefabs/UDK7MBoss.prefab","Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab","Assets/Game/CrystalParkour/Prefabs/CrystalParkourPlayer.prefab","Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"}){var root=PrefabUtility.LoadPrefabContents(p);try{Clean(root);if(p.Contains("UDK7M"))Boss(root);else if(p.Contains("FirstPersonPlayer"))Charge(root);else if(p.Contains("Player"))Player(root);else{var steps=root.GetComponent<PlayerObjectAudio>();if(!steps)steps=root.AddComponent<PlayerObjectAudio>();steps.EnsureSource();steps.stepSource.spatialBlend=.6f;Contacts(root,root.transform);}PrefabUtility.SaveAsPrefabAsset(root,p);}finally{PrefabUtility.UnloadPrefabContents(root);}}
 if(!scene.IsValid()||!scene.isLoaded){scene=EditorSceneManager.OpenScene(UDK7MThreePhaseBuilder.ScenePath,OpenSceneMode.Additive);opened=true;}SceneManager.SetActiveScene(scene);if(!File.Exists(Stage+"BeforeLocalAudio/Arena.unity"))EditorSceneManager.SaveScene(scene,Stage+"BeforeLocalAudio/Arena.unity",true);var arena=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).Single();
 var old=arena.GetComponent<UDK7MAudio>();if(old){old.enabled=false;arena.feedback.audio=null;UnityEngine.Object.DestroyImmediate(old);}foreach(var root in scene.GetRootGameObjects())Clean(root);Boss(arena.boss.gameObject);Player(arena.player.gameObject);foreach(var pillar in arena.pillars)Bind(pillar.gameObject,Cue(UDK7MSound.PillarShatter,"glass_break1","glass_break2","CrystalGenericCombined_03"),Cue(UDK7MSound.PillarRegrow,"CrystalMiningCombined_04"));
 var debris=GameObject.CreatePrimitive(PrimitiveType.Cube);debris.name="Falling debris with local audio";debris.AddComponent<Rigidbody>();debris.AddComponent<UDK7MDebris>();Bind(debris,Cue(UDK7MSound.RockLanding,"BigImpact"),Cue(UDK7MSound.CrystalLanding,"BigImpact"));Surface(debris.GetComponent<Collider>());Directory.CreateDirectory("Assets/Game/Enemies/UDK7M/AI/Prefabs");arena.localDebrisPrefab=PrefabUtility.SaveAsPrefabAsset(debris,"Assets/Game/Enemies/UDK7M/AI/Prefabs/LocalAudioDebris.prefab");UnityEngine.Object.DestroyImmediate(debris);arena.warningSounds=Array.Empty<AudioClip>();EditorSceneManager.SaveScene(scene);
 File.WriteAllText(Stage+"local_audio.json",JsonConvert.SerializeObject(new{passed=true,environmentContactSources=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Count(t=>t.name=="Contact AudioSource"),playerContactVoices=arena.player.GetComponent<SurfaceContactAudio>().sources.Length,bossFootstepComponents=arena.boss.GetComponentsInChildren<PlayerObjectAudio>(true).Length,prepareLoop=arena.boss.GetComponentInChildren<MachinePrepareAudio>().source.clip.name,gauntletCharge=arena.player.GetComponent<GauntletChargeAudio>().stages.clip.name,gauntletFull=arena.player.GetComponent<GauntletChargeAudio>().fullLoop.clip.name,clips=clips.Length,surfaces=library.surfaces.Select(s=>new{type=s.surface.ToString(),contacts=s.contacts.Select(c=>c.name),steps=s.steps.Length}),loops=arena.boss.GetComponentsInChildren<MachineLoopAudio>().Select(l=>new{l.drill,clip=l.source.clip?l.source.clip.name:null,spatialBlend=l.source.spatialBlend}),decision=new{minimum=arena.tuning.decisionInterval.x,maximum=arena.tuning.decisionInterval.y}},Formatting.Indented));
 }catch(Exception e){File.WriteAllText(Stage+"local_audio.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);if(opened&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);busy=false;}}
}}













