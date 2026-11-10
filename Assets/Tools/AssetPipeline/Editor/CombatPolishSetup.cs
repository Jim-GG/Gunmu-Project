using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GameActions;

namespace AssetPipeline.Editor
{
    [InitializeOnLoad]
    public static class CombatPolishSetup
    {
        public const string Stage="AssetStaging/CombatPolish/";
        public const string ScreenRoot="Assets/Game/VFX/ScreenEffects/";
        static CombatPolishSetup(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"Setup.request"))return;
            File.Delete(Stage+"Setup.request");try{Setup();File.WriteAllText(Stage+"setup-result.json","{\"passed\":true}");}catch(Exception e){File.WriteAllText(Stage+"setup-result.json",Newtonsoft.Json.JsonConvert.SerializeObject(new{passed=false,error=e.ToString()}));Debug.LogException(e);}
        }
        static T Load<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static T Asset<T>(string path)where T:ScriptableObject
        {var asset=Load<T>(path);if(asset)return asset;asset=ScriptableObject.CreateInstance<T>();AssetDatabase.CreateAsset(asset,path);return asset;}
        static Material Material(string path,string shader)
        {var m=Load<Material>(path);if(m)return m;var s=Shader.Find(shader);if(!s)throw new Exception("Missing shader "+shader);m=new Material(s);AssetDatabase.CreateAsset(m,path);return m;}
        static ScreenEffectPreset Signal(ScreenCue cue,float radial,float strength,float release,float chromatic=0,float noise=0,float zoom=.009f)
            =>new ScreenEffectPreset{cue=cue,radial=radial,strength=strength,attack=.025f,release=release,chromaticPixels=chromatic,purpleNoise=noise,zoomMetres=zoom,zoomFrequency=24};
        [MenuItem("Tools/晶振/接入战斗音画与屏幕效果")]
        public static void Setup()
        {
            Directory.CreateDirectory(Stage);Directory.CreateDirectory(ScreenRoot+"Resources");Directory.CreateDirectory(ScreenRoot+"Materials");AssetDatabase.Refresh();
            var profile=Asset<CombatFeedbackProfile>("Assets/Game/Shared/Actions/Resources/CombatFeedback.asset");
            profile.tierThreeSwing=Load<AudioClip>("Assets/Game/Audio/SFX/拳套/三级蓄力出拳.mp3");
            profile.guardImpact=null; // 恢复各玩家 ObjectAudio 原有的 ParryImpact。
            profile.concreteImpact=Load<AudioClip>("Assets/Game/Audio/SFX/Impact_Blunt_Large_V5_Stone_02.mp3");
            profile.fleshHits=Enumerable.Range(1,10).Select(i=>Load<AudioClip>($"Assets/Game/Audio/SFX/拳套/打击音效-{i:00}.mp3")).ToArray();
            profile.bodyFalls=Enumerable.Range(1,5).Select(i=>Load<AudioClip>($"Assets/Game/Audio/SFX/Impacts/BodyFall/Enemy_bodyfall{i}.ogg")).ToArray();
            profile.resonatorRoar=Load<AudioClip>("Assets/Game/Audio/SFX/共鸣者吼叫.mp3");profile.playerHurts=Enumerable.Range(0,7).Select(i=>Load<AudioClip>($"Assets/Game/Audio/SFX/Impacts/character_gets_wound_{i:00}.ogg")).ToArray();
            if(!profile.tierThreeSwing||!profile.resonatorRoar||!profile.concreteImpact||profile.fleshHits.Any(c=>!c)||profile.bodyFalls.Any(c=>!c))throw new Exception("Requested audio clip is missing");
            profile.fracturedCrystal=Load<GameObject>("Assets/Game/Environment/crystal/Prefabs/Crystal_07_Fractured.prefab");profile.squatCrystal=Load<GameObject>("Assets/Game/Environment/crystal/Prefabs/Crystal_04_Squat.prefab");
            profile.rockMeshes=Enumerable.Range(1,4).Select(i=>AssetDatabase.LoadAllAssetsAtPath($"Assets/Game/Environment/掉落石块/石块{i}.fbx").OfType<Mesh>().FirstOrDefault()).ToArray();
            if(!profile.fracturedCrystal||!profile.squatCrystal||profile.rockMeshes.Any(m=>!m))throw new Exception("Requested falling mesh is missing");
            profile.rockFaceDown=profile.rockMeshes.Select(RockGeometryUtility.LargestFaceDown).ToArray();
            profile.electricity=Material("Assets/Game/VFX/Combat/Materials/TransientElectricSurface.mat","Game/Combat/Transient Electric Surface");profile.fallingWarning=Material("Assets/Game/VFX/Combat/Materials/FallingWarning_Transparent.mat","Game/Combat/Transparent Falling Warning");EditorUtility.SetDirty(profile);

            var surfaces=Load<SurfaceSoundLibrary>("Assets/Game/Shared/Actions/Resources/SurfaceSounds.asset");
            var concrete=Array.Find(surfaces.surfaces,s=>s.surface==SoundSurface.Concrete);concrete.fist=profile.concreteImpact;concrete.fists=Array.Empty<AudioClip>();
            var flesh=Array.Find(surfaces.surfaces,s=>s.surface==SoundSurface.Flesh);flesh.fists=profile.fleshHits;EditorUtility.SetDirty(surfaces);
            var actions=Resources.Load<ActionSet>("GauntletActions");
            var parkourActions=Load<ActionSet>("Assets/Game/CrystalParkour/Data/ParkourGauntletActions.asset");
            foreach(var set in new[]{actions,parkourActions})if(set)
            {
                for(int i=0;i<4;i++)foreach(var hit in set.Find("punch"+(i+1)).action.hits)
                {if(i<3){hit.knockbackDistance=profile.knockbackMetres[i];hit.knockbackDuration=profile.knockbackSeconds;}hit.contactSound=null;}
                EditorUtility.SetDirty(set);
            }
            var blood=Blood();var impacts=Resources.Load<GauntletImpactVFXProfile>("GauntletImpactVFX");impacts.bloodMist=blood;impacts.bloodScale=1;EditorUtility.SetDirty(impacts);

            var screen=Asset<ScreenEffectsProfile>(ScreenRoot+"Resources/CombatScreenEffects.asset");screen.material=Material(ScreenRoot+"Materials/M_CombatScreenEffects.mat","Game/Screen Effects/Combat Radial Blur");
            screen.material.SetFloat("_Enabled",0);EditorUtility.SetDirty(screen.material);
            if(screen.signals.Length==0)screen.signals=new[]{
                Signal(ScreenCue.LightHit,.38f,.075f,.11f,zoom:.006f),Signal(ScreenCue.ChargeOneHit,.5f,.1f,.14f,zoom:.009f),
                Signal(ScreenCue.ChargeTwoHit,.72f,.14f,.19f,1.2f,zoom:.013f),Signal(ScreenCue.ChargeThreeHit,.9f,.18f,.24f,2.4f,zoom:.019f),
                Signal(ScreenCue.Impact,.6f,.13f,.21f,zoom:.009f),Signal(ScreenCue.Explosion,.9f,.19f,.28f,3.5f,zoom:.015f),
                Signal(ScreenCue.Resonance,.95f,.18f,.42f,4.5f,.5f,.014f),Signal(ScreenCue.Hurt,.22f,.06f,.2f,zoom:0),
                Signal(ScreenCue.PerfectGuard,.4f,.085f,.16f,2.5f,zoom:.006f),Signal(ScreenCue.PillarShock,.8f,.16f,.32f,4.5f,.12f,.01f),Signal(ScreenCue.Custom,.8f,.13f,.22f,zoom:0)};
            if(screen.Find(ScreenCue.ResonatorRoar)==null){var list=screen.signals.ToList();list.Add(Signal(ScreenCue.ResonatorRoar,.55f,.12f,.24f,3,zoom:.004f));screen.signals=list.ToArray();}
            var features=new List<FullScreenPassRendererFeature>();
            foreach(string path in new[]{"Assets/Game/Settings/PC_Renderer.asset","Assets/Game/Settings/Mobile_Renderer.asset"})
            {
                var data=Load<UniversalRendererData>(path);if(!data)continue;
                var feature=data.rendererFeatures.OfType<FullScreenPassRendererFeature>().FirstOrDefault(f=>f.name=="Combat screen effects (camera owned)");
                if(!feature){feature=ScriptableObject.CreateInstance<FullScreenPassRendererFeature>();feature.name="Combat screen effects (camera owned)";AssetDatabase.AddObjectToAsset(feature,data);data.rendererFeatures.Add(feature);}
                feature.injectionPoint=FullScreenPassRendererFeature.InjectionPoint.AfterRenderingPostProcessing;feature.fetchColorBuffer=true;feature.requirements=ScriptableRenderPassInput.None;feature.bindDepthStencilAttachment=false;feature.passMaterial=null;feature.passIndex=0;feature.SetActive(true);feature.Create();
                features.Add(feature);EditorUtility.SetDirty(feature);data.SetDirty();EditorUtility.SetDirty(data);
            }
            screen.rendererFeatures=features.ToArray();EditorUtility.SetDirty(screen);
            foreach(var asset in new UnityEngine.Object[]{profile,profile.electricity,profile.fallingWarning,surfaces,actions,parkourActions,impacts,screen,screen.material})if(asset)AssetDatabase.SaveAssetIfDirty(asset);
            foreach(var feature in features)AssetDatabase.SaveAssetIfDirty(feature);
        }
        static GameObject Blood()
        {
            const string path="Assets/Game/VFX/Combat/ResonatorBloodMist.prefab";var existing=Load<GameObject>(path);
            if(existing)
            {
                var content=PrefabUtility.LoadPrefabContents(path);try{BloodSheet(content.GetComponent<ParticleSystem>());return PrefabUtility.SaveAsPrefabAsset(content,path);}finally{PrefabUtility.UnloadPrefabContents(content);}
            }
            var go=new GameObject("Resonator stationary purple blood mist");
            try
            {
                var particles=go.AddComponent<ParticleSystem>();particles.Stop(true,ParticleSystemStopBehavior.StopEmittingAndClear);
                var main=particles.main;main.duration=.6f;main.loop=false;main.playOnAwake=false;main.maxParticles=5;main.simulationSpace=ParticleSystemSimulationSpace.World;
                main.startLifetime=new ParticleSystem.MinMaxCurve(.35f,.55f);main.startSpeed=0;main.startSize=new ParticleSystem.MinMaxCurve(.35f,.55f);main.gravityModifier=0;
                main.startRotation=new ParticleSystem.MinMaxCurve(-Mathf.PI,Mathf.PI);main.startColor=new ParticleSystem.MinMaxGradient(new Color(.42f,.035f,.6f,.7f),new Color(.65f,.075f,.78f,.8f));
                var emission=particles.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,4,5,1,.01f)});
                var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.07f;
                var color=particles.colorOverLifetime;color.enabled=true;var gradient=new Gradient();gradient.SetKeys(new[]{new GradientColorKey(Color.white,0),new GradientColorKey(Color.white,1)},new[]{new GradientAlphaKey(0,0),new GradientAlphaKey(1,.08f),new GradientAlphaKey(0,1)});color.color=gradient;
                var size=particles.sizeOverLifetime;size.enabled=true;size.size=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,.65f,1,1.2f));
                var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=Load<Material>("Assets/Game/VFX/Combat/Materials/PurpleBloodMist.mat");renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
                BloodSheet(particles);
                return PrefabUtility.SaveAsPrefabAsset(go,path);
            }
            finally{UnityEngine.Object.DestroyImmediate(go);}
        }
        static void BloodSheet(ParticleSystem particles)
        {
            var sheet=particles.textureSheetAnimation;sheet.enabled=true;sheet.numTilesX=8;sheet.numTilesY=8;sheet.animation=ParticleSystemAnimationType.WholeSheet;sheet.cycleCount=1;sheet.startFrame=0;sheet.frameOverTime=new ParticleSystem.MinMaxCurve(1,AnimationCurve.Linear(0,0,1,1));
        }
    }
}
