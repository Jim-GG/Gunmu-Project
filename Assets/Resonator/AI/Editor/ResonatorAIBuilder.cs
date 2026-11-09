using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.AI;
using Unity.AI.Navigation;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine.SceneManagement;
namespace Resonator.AI.Editor
{
    public static class ResonatorAIBuilder
    {
        const string Root="Assets/Game/Enemies/Resonator/AI/";
        static T Asset<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static Transform Bone(GameObject go,string suffix)=>go.GetComponentsInChildren<Transform>(true).First(t=>t.name=="ValveBiped.Bip01_"+suffix);
        static T Component<T>(GameObject go) where T:Component {var c=go.GetComponent<T>();if(!c)c=go.AddComponent<T>();return c;}
        static ResonatorMotionTrack Extract(GameObject probe,AnimationClip clip,Vector3 reference)
        {
            string path=Root+"Data/"+clip.name+"_Motion.asset";var track=Asset<ResonatorMotionTrack>(path);
            if(!track){track=ScriptableObject.CreateInstance<ResonatorMotionTrack>();AssetDatabase.CreateAsset(track,path);}
            var hips=Bone(probe,"Pelvis");int count=Mathf.CeilToInt(clip.length*30)+1;var points=new Vector3[count];
            for(int i=0;i<count;i++){clip.SampleAnimation(probe,Mathf.Min(i/30f,Mathf.Max(0,clip.length-.00001f)));points[i]=probe.transform.InverseTransformPoint(hips.position);points[i].y=0;}
            float distance=0;for(int i=1;i<points.Length;i++)distance+=(points[i]-points[i-1]).magnitude;
            track.clip=clip;track.sampleRate=track.framesPerSecond=30;track.referenceHip=reference;track.trajectory=points;track.averageSpeed=distance/Mathf.Max(.001f,clip.length);
            EditorUtility.SetDirty(track);return track;
        }
        static MotionBeat Beat(Dictionary<string,ResonatorMotionTrack> tracks,string name,bool loop=false,float end=-1,float start=0)=>new MotionBeat{track=tracks[name],loop=loop,endFrame=end,startFrame=start};
        static HitWindow Window(float frame,float damage,float end=-1,float reach=1.5f)=>new HitWindow{startFrame=frame,endFrame=end<0?frame+5:end,damage=damage,reach=reach};
        [MenuItem("Tools/Resonator/AI/1 Extract Motion And Build Enemy")]
        public static void BuildEnemy()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
            Directory.CreateDirectory(Root+"Data");Directory.CreateDirectory(Root+"Prefabs");AssetDatabase.Refresh();
            // These older converted FBXs include a bind-pose key before the motion.
            // Keep it out of loops and out of the feign-death opening pose.
            foreach(var path in Directory.GetFiles("Assets/Game/Enemies/Resonator/Animations","Zombie_*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);var clips=importer.clipAnimations;bool changed=false;
                foreach(var clip in clips)if(clip.firstFrame<2){clip.firstFrame=2;changed=true;}
                if(changed){importer.clipAnimations=clips;importer.SaveAndReimport();}
            }
            var model=Asset<GameObject>("Assets/Game/Enemies/Resonator/Models/Resonator.fbx");var probe=(GameObject)PrefabUtility.InstantiatePrefab(model);probe.name="Motion extraction temporary";
            Dictionary<string,ResonatorMotionTrack> tracks;
            try
            {
                Vector3 reference=probe.transform.InverseTransformPoint(Bone(probe,"Pelvis").position);
                var paths=Directory.GetFiles("Assets/Game/Enemies/Resonator/Animations","*.fbx");
                tracks=paths.Select(p=>AssetDatabase.LoadAllAssetsAtPath(p).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__"))).ToDictionary(c=>c.name,c=>Extract(probe,c,reference));
            }
            finally{UnityEngine.Object.DestroyImmediate(probe);}
            string profilePath=Root+"Data/Resonator_Default.asset";var tuning=Asset<ResonatorTuning>(profilePath);bool newProfile=!tuning;
            if(!tuning)
            {
                tuning=ScriptableObject.CreateInstance<ResonatorTuning>();
                tuning.wake=Beat(tracks,"Zombie_06_WakeUp_Transform",false,520);
                tuning.feeding=Beat(tracks,"Zombie_03_Feeding_Ground",true,420,150);
                tuning.idleSlow=Beat(tracks,"Zombie_05_Idle_Slow",true,700);
                tuning.idleRestless=Beat(tracks,"Zombie_04_Idle_Restless",true,425);
                tuning.resonance=Beat(tracks,"Resonator_Zombie_Headbutt",true);
                tuning.agonizing=Beat(tracks,"Resonator_Zombie_Agonizing");tuning.scream=Beat(tracks,"Resonator_Zombie_Scream");
                tuning.walk=Beat(tracks,"Resonator_Walking",true);tuning.run=Beat(tracks,"Resonator_Zombie_Running",true);
                tuning.hit=Beat(tracks,"Resonator_Zombie_Reaction_Hit");tuning.stumble=Beat(tracks,"Resonator_Zombie_Stumbling");tuning.death=Beat(tracks,"Resonator_Zombie_Death");
                tuning.headbutt.animation=Beat(tracks,"Resonator_Zombie_Headbutt");tuning.headbutt.hits=new[]{Window(37,17)};
                tuning.kick.animation=Beat(tracks,"Resonator_Zombie_Kicking");tuning.kick.hits=new[]{Window(43,20)};
                tuning.attack.animation=Beat(tracks,"Resonator_Zombie_Attack");tuning.attack.hits=new[]{Window(43,12),Window(82,12),Window(115,16)};
                tuning.punch.animation=Beat(tracks,"Resonator_Zombie_Punching");tuning.punch.hits=new[]{Window(37,19)};
                AssetDatabase.CreateAsset(tuning,profilePath);
            }
            ResonatorHitboxDefaults.Upgrade(tuning,false);
            if(newProfile){ResonatorAttackMotionSetup.ApplyPresets(tuning,false);ResonatorNavigationSetup.Configure(tuning,false);}
            var root=new GameObject("Resonator Agent");
            try
            {
                var visual=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>("Assets/Game/Enemies/Resonator/Prefabs/ResonatorScientist.prefab"));visual.name="Visual";visual.transform.SetParent(root.transform,false);
                // Revert transient editor pose: the FBX bind transforms are the reference.
                using(var bind=new BindPose(model))
                {
                    var source=bind.go.GetComponentsInChildren<Transform>().ToDictionary(t=>PathOf(t,bind.go.transform));
                    foreach(var t in visual.GetComponentsInChildren<Transform>())
                    {
                        if(source.TryGetValue(PathOf(t,visual.transform),out var s)){t.localPosition=s.localPosition;t.localRotation=s.localRotation;t.localScale=s.localScale;}
                    }
                }
                var animator=visual.GetComponent<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;
                var oldPreview=visual.GetComponent<ResonatorAnimationPreview>();if(oldPreview)UnityEngine.Object.DestroyImmediate(oldPreview);
                var motion=visual.AddComponent<ResonatorMotionPlayer>();motion.animator=animator;motion.hips=Bone(visual,"Pelvis");
                var agent=root.AddComponent<NavMeshAgent>();agent.radius=.28f;agent.height=1.8f;agent.baseOffset=0;agent.angularSpeed=420;agent.acceleration=13;agent.obstacleAvoidanceType=ObstacleAvoidanceType.HighQualityObstacleAvoidance;
                var capsule=root.AddComponent<CapsuleCollider>();capsule.radius=.28f;capsule.height=1.8f;capsule.center=Vector3.up*.9f;
                var motor=root.AddComponent<ResonatorMotor>();motor.agent=agent;motor.tuning=tuning;
                var ragdoll=root.AddComponent<ResonatorRagdoll>();ragdoll.livingCollider=capsule;BuildRagdoll(visual,ragdoll);
                var enemy=root.AddComponent<ResonatorEnemy>();enemy.tuning=tuning;enemy.motion=motion;enemy.motor=motor;enemy.ragdoll=ragdoll;
                root.AddComponent<ResonatorPunchTarget>();
                PrefabUtility.SaveAsPrefabAsset(root,Root+"Prefabs/ResonatorAgent.prefab");
            }
            finally{UnityEngine.Object.DestroyImmediate(root);}
            AssetDatabase.SaveAssets();
        }
        static string PathOf(Transform t,Transform root){if(t==root)return "";return AnimationUtility.CalculateTransformPath(t,root);}
        sealed class BindPose:IDisposable{public GameObject go;public BindPose(GameObject a){go=(GameObject)PrefabUtility.InstantiatePrefab(a);}public void Dispose(){UnityEngine.Object.DestroyImmediate(go);}}
        static void BuildRagdoll(GameObject visual,ResonatorRagdoll output)
        {
            var specs=new[]{
                ("Pelvis","Spine1",.14f,12f),("Spine1","Neck1",.15f,14f),("Head1","Head1",.115f,5f),
                ("L_UpperArm","L_Forearm",.065f,3f),("L_Forearm","L_Hand",.055f,2f),("R_UpperArm","R_Forearm",.065f,3f),("R_Forearm","R_Hand",.055f,2f),
                ("L_Thigh","L_Calf",.085f,7f),("L_Calf","L_Foot",.065f,4f),("R_Thigh","R_Calf",.085f,7f),("R_Calf","R_Foot",.065f,4f)};
            var definitions=new List<RagdollBone>();var colliders=new List<Collider>();
            foreach(var item in specs)
            {
                var bone=Bone(visual,item.Item1);var end=Bone(visual,item.Item2);
                definitions.Add(new RagdollBone{bone=bone,mass=item.Item4});
                var shape=new GameObject("Ragdoll Shape");shape.transform.SetParent(bone,false);
                Vector3 from=bone.position,to=end.position;if(item.Item1=="Head1")to=from+Vector3.up*.19f;
                shape.transform.position=(from+to)*.5f;shape.transform.rotation=Quaternion.FromToRotation(Vector3.up,to-from);
                Vector3 scale=bone.lossyScale;shape.transform.localScale=new Vector3(1/Mathf.Abs(scale.x),1/Mathf.Abs(scale.y),1/Mathf.Abs(scale.z));
                var collider=shape.AddComponent<CapsuleCollider>();collider.direction=1;collider.radius=item.Item3;collider.height=Mathf.Max(item.Item3*2,Vector3.Distance(from,to));collider.enabled=false;colliders.Add(collider);
            }
            for(int i=0;i<definitions.Count;i++)
            {
                Transform parent=definitions[i].bone.parent;
                while(parent&&!definitions.Any(d=>d.bone==parent))parent=parent.parent;
                definitions[i].parentIndex=parent?definitions.FindIndex(d=>d.bone==parent):-1;
            }
            output.definitions=definitions.ToArray();output.boneColliders=colliders.ToArray();
        }
        [MenuItem("Tools/Resonator/AI/2 Build Test Arena")]
        public static void BuildArena()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
            if(!Asset<GameObject>(Root+"Prefabs/ResonatorAgent.prefab"))BuildEnemy();
            Directory.CreateDirectory(Root+"Scenes");AssetDatabase.Refresh();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var world=new GameObject("Navigation World");
            var ground=Box("Floor",new Vector3(0,-.2f,2),new Vector3(34,.4f,34),world.transform);
            var material=Asset<Material>("Assets/Game/Enemies/Resonator/Materials/Ground.mat");if(material)ground.GetComponent<Renderer>().sharedMaterial=material;
            Box("Path obstacle",new Vector3(-3,1.25f,2),new Vector3(1,2.5f,4),world.transform);
            Box("Crystal wall",new Vector3(10,1.5f,9),new Vector3(.6f,3,5),world.transform);
            foreach(var p in new[]{new Vector3(-16,1.5f,2),new Vector3(16,1.5f,2)})Box("Boundary",p,new Vector3(.5f,3,33),world.transform);
            foreach(var p in new[]{new Vector3(0,1.5f,-14),new Vector3(0,1.5f,18)})Box("Boundary",p,new Vector3(33,3,.5f),world.transform);
            var surface=world.AddComponent<NavMeshSurface>();surface.collectObjects=CollectObjects.Children;surface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;surface.BuildNavMesh();
            string navPath=Root+"Scenes/ResonatorArenaNav.asset";
            // A new asset path is generated on rebuild so a loaded NavMeshData is never destroyed.
            navPath=AssetDatabase.GenerateUniqueAssetPath(navPath);AssetDatabase.CreateAsset(surface.navMeshData,navPath);
            var player=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>("Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab"),scene);player.name="Arena Player";player.transform.position=new Vector3(0,0,-7);
            var target=Component<ResonatorTarget>(player);target.vitals=Component<FirstPerson.FirstPersonVitals>(player);target.gauntlet=player.GetComponent<FirstPerson.GauntletCombat>();target.eyes=player.transform.Find("View Pivot");
            var bridge=Component<ResonatorPlayerBridge>(player);bridge.actor=target;bridge.motor=player.GetComponent<FirstPerson.FirstPersonMotor>();bridge.combat=target.gauntlet;bridge.attackOrigin=target.eyes;
            var positions=new[]{new Vector3(-9,0,4),new Vector3(-5,0,10),new Vector3(0,0,9),new Vector3(5,0,5),new Vector3(8.7f,0,9)};
            var enemies=new List<ResonatorEnemy>();
            for(int i=0;i<5;i++)
            {
                var go=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>(Root+"Prefabs/ResonatorAgent.prefab"),scene);go.name="Resonator "+(RestActivity)i;go.transform.position=positions[i];go.transform.rotation=Quaternion.Euler(0,180,0);
                var enemy=go.GetComponent<ResonatorEnemy>();enemy.initialActivity=(RestActivity)i;enemy.target=target;enemies.Add(enemy);
                if(i==4)
                {
                    var crystal=GameObject.CreatePrimitive(PrimitiveType.Cube);crystal.name="Resonance Crystal";crystal.transform.SetParent(world.transform);crystal.transform.position=new Vector3(9.7f,1.5f,9);crystal.transform.localScale=new Vector3(.35f,.8f,.45f);crystal.transform.rotation=Quaternion.Euler(10,20,30);
                    var cm=Asset<Material>("Assets/Game/Enemies/Resonator/Materials/Crystal_Clothing_Unity.mat");if(cm)crystal.GetComponent<Renderer>().sharedMaterial=cm;
                    enemy.crystalFocus=crystal.transform;go.transform.LookAt(new Vector3(crystal.transform.position.x,0,crystal.transform.position.z));
                }
            }
            // Re-bake after the crystal obstacle is added.
            surface.BuildNavMesh();var finalData=surface.navMeshData;AssetDatabase.CreateAsset(finalData,AssetDatabase.GenerateUniqueAssetPath(Root+"Scenes/ResonatorArenaNavFinal.asset"));
            var hud=new GameObject("AI Arena HUD").AddComponent<ResonatorArenaHUD>();hud.player=bridge;hud.enemies=enemies.ToArray();
            foreach(var d in new[]{new Vector3(-3,5,-5),new Vector3(4,4,5)}){var l=new GameObject("Arena Light",typeof(Light));l.transform.rotation=Quaternion.LookRotation(-d);l.GetComponent<Light>().type=LightType.Directional;l.GetComponent<Light>().intensity=.85f;}
            RenderSettings.ambientLight=new Color(.35f,.37f,.43f);RenderSettings.skybox=Asset<Material>("Assets/Game/Enemies/Resonator/Materials/StudioSky.mat");
            var probe=new GameObject("Arena Reflections").AddComponent<ReflectionProbe>();probe.transform.position=Vector3.up*2;probe.size=Vector3.one*40;probe.mode=UnityEngine.Rendering.ReflectionProbeMode.Realtime;probe.refreshMode=UnityEngine.Rendering.ReflectionProbeRefreshMode.OnAwake;probe.resolution=128;
            EditorSceneManager.SaveScene(scene,Root+"Scenes/ResonatorCombatArena.unity");AssetDatabase.SaveAssets();Selection.activeGameObject=enemies[0].gameObject;
        }
        static GameObject Box(string name,Vector3 position,Vector3 scale,Transform parent){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent);go.transform.position=position;go.transform.localScale=scale;return go;}
    }
}

