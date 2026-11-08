using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Unity.AI.Navigation;
using GameActions;
using Resonator.AI;
using FirstPerson;
using CrystalParkour;
using UDK7M;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static class UDK7MBossBuilder
    {
        public const string ScenePath="Assets/Game/Scene/UDK7MBossArena.unity";
        const string Root="Assets/Game/Enemies/UDK7M/",Stage="AssetStaging/UDK7M/Boss/",SV="Assets/Game/Environment/SovietFacility/";
        static bool busy;static Scene scene;static int lowerLayer;static Material concrete,metal,yellow,green,crystal;
        static UDK7MBossBuilder(){EditorApplication.update+=Poll;}
        static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"Build.request"))return;File.Delete(Stage+"Build.request");Build();}
        static T A<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        [MenuItem("Tools/UDK7M/Build Boss Arena")]
        public static void Build()
        {
            if(busy)return;busy=true;Directory.CreateDirectory(Stage);var previous=SceneManager.GetActiveScene();bool created=false;
            try
            {
                if(File.Exists(ScenePath))throw new Exception("Boss arena already exists. Rebuild will not overwrite your edited scene.");
                foreach(var part in new[]{"AI","AI/Navigation","AI/Materials","AI/Prefabs"})Directory.CreateDirectory(Root+part);AssetDatabase.Refresh();
                var actions=A<ActionSet>(Root+"Actions/UDK7MActions.asset");if(!actions)throw new Exception("Missing UDK7M action set");
                string backup=Root+"Actions/UDK7M_BeforeBoss.asset";if(!A<ActionSet>(backup)){var copy=UnityEngine.Object.Instantiate(actions);copy.name="UDK7M_BeforeBoss";AssetDatabase.CreateAsset(copy,backup);}
                ConfigureActions(actions);
                var tune=A<UDK7MTuning>(Root+"AI/UDK7M_Default.asset");if(!tune){tune=ScriptableObject.CreateInstance<UDK7MTuning>();AssetDatabase.CreateAsset(tune,Root+"AI/UDK7M_Default.asset");}
                ConfigureGauntletAndKnockback();
                var normal=A<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset");var minion=A<ResonatorTuning>(Root+"AI/UDK7M_Resonator.asset");if(!minion){minion=UnityEngine.Object.Instantiate(normal);minion.name="UDK7M_Resonator";AssetDatabase.CreateAsset(minion,Root+"AI/UDK7M_Resonator.asset");}
                minion.wake.startFrame=400;minion.wake.endFrame=520;minion.wake.speed=1;minion.wakeRevealFrame=520;EditorUtility.SetDirty(minion);
                lowerLayer=EnsureLayer("ArenaLowerFloor");int bossAgent=EnsureAgent(tune);
                scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);created=true;SceneManager.SetActiveScene(scene);
                concrete=A<Material>(SV+"Materials/SV_Concrete_Damp.mat");metal=A<Material>(SV+"Materials/SV_Metal_Diamond.mat");yellow=A<Material>(SV+"Materials/SV_Paint_Yellow.mat");green=A<Material>(SV+"Materials/SV_Paint_Green.mat");
                var cluster=A<GameObject>("Assets/Game/Environment/crystal/Prefabs/Crystal_Cluster_Wall.prefab");crystal=cluster.GetComponentInChildren<Renderer>(true).sharedMaterial;
                var root=Group("UDK7M Boss Encounter");var arena=root.AddComponent<UDK7MArena>();arena.tuning=tune;arena.rockMaterial=A<Material>(SV+"Materials/SV_Concrete_Fracture.mat");arena.crystalMaterial=crystal;arena.warningMaterial=Colour("Rock_Warning",new Color(1,.25f,.025f),1.5f);
                var outer=Group("Facility Geometry");var world=Group("Main arena modules",outer.transform);BuildGeometry(world.transform,arena);
                var pool=Group("Resonator population",root.transform);arena.minions=pool.AddComponent<UDK7MMinions>();arena.minions.arena=arena;arena.minions.prefab=A<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");arena.minions.profile=minion;
                var spawnRoot=Group("Hidden spawn anchors",root.transform);var spawns=new List<Transform>();for(int i=0;i<20;i++){float angle=i*360f/20*Mathf.Deg2Rad;spawns.Add(Group("Spawn "+(i+1),spawnRoot.transform,new Vector3(Mathf.Sin(angle)*20,0,Mathf.Cos(angle)*20)).transform);}arena.minions.spawnPoints=spawns.ToArray();
                arena.checkpoint=Group("Entrance checkpoint",root.transform,new Vector3(0,.1f,-23)).transform;
                var player=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>("Assets/Game/CrystalParkour/Prefabs/CrystalParkourPlayer.prefab"),scene);player.name="Boss arena player";player.transform.position=arena.checkpoint.position;
                arena.player=player.GetComponent<ResonatorTarget>();if(!arena.player)arena.player=player.AddComponent<ResonatorTarget>();arena.player.vitals=player.GetComponent<FirstPersonVitals>();arena.player.gauntlet=player.GetComponent<GauntletCombat>();arena.player.eyes=player.GetComponent<FirstPersonMotor>().viewPivot;
                foreach(var b in player.GetComponentsInChildren<MonoBehaviour>(true))if(b is CrystalParkourHUD||b is GauntletTrainingHUD)b.enabled=false;
                var entry=Group("Arena entry trigger",root.transform,new Vector3(0,1,-20.5f));var trigger=entry.AddComponent<BoxCollider>();trigger.isTrigger=true;trigger.size=new Vector3(8,4,2);entry.AddComponent<UDK7MArenaEntry>().arena=arena;
                var humanSurface=world.AddComponent<NavMeshSurface>();humanSurface.collectObjects=CollectObjects.Children;humanSurface.layerMask=1;humanSurface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;humanSurface.overrideVoxelSize=true;humanSurface.voxelSize=.12f;humanSurface.BuildNavMesh();AssetDatabase.CreateAsset(humanSurface.navMeshData,Root+"AI/Navigation/Arena_Humanoid.asset");
                var bossSurface=outer.AddComponent<NavMeshSurface>();bossSurface.agentTypeID=bossAgent;bossSurface.collectObjects=CollectObjects.Children;bossSurface.layerMask=1;bossSurface.useGeometry=NavMeshCollectGeometry.PhysicsColliders;bossSurface.overrideVoxelSize=true;bossSurface.voxelSize=.15f;bossSurface.BuildNavMesh();AssetDatabase.CreateAsset(bossSurface.navMeshData,Root+"AI/Navigation/Arena_Boss.asset");
                var boss=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>(Root+"Prefabs/UDK7M.prefab"),scene);boss.name="UDK7M Boss";PrefabUtility.UnpackPrefabInstance(boss,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);
                var agent=boss.AddComponent<NavMeshAgent>();agent.agentTypeID=bossAgent;agent.updatePosition=false;agent.updateRotation=false;agent.radius=tune.bodyRadius;agent.height=tune.bodyHeight;
                var body=boss.AddComponent<BoxCollider>();body.center=new Vector3(0,1.15f,0);body.size=new Vector3(4.7f,2.3f,5.9f);
                var motor=boss.AddComponent<UDK7MMotor>();motor.agent=agent;motor.tuning=tune;var brain=boss.AddComponent<UDK7MBoss>();brain.actions=actions;brain.tuning=tune;brain.runner=boss.GetComponentInChildren<ActionRunner>();brain.motor=motor;brain.drill=boss.GetComponentsInChildren<Transform>().First(t=>t.name=="DrillRoll");
                PrefabUtility.SaveAsPrefabAsset(boss,Root+"AI/Prefabs/UDK7MBoss.prefab");brain.arena=arena;arena.boss=brain;boss.transform.SetPositionAndRotation(new Vector3(0,0,3),Quaternion.Euler(0,180,0));
                Lighting();AssetDatabase.SaveAssets();
                var camera=AssetReviewUtility.Camera(scene,new Vector3(31,22,-31),new Vector3(0,6,0),59);camera.cullingMask=~0;
                foreach(var lamp in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)))lamp.cullingMask=~0;
                AssetReviewUtility.Capture(scene,camera,Stage+"Arena_Overview.png");camera.enabled=false;camera.gameObject.name="Disabled overview camera";
                foreach(var lamp in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)))lamp.cullingMask=~0;
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                File.WriteAllText(Stage+"build_status.json",JsonConvert.SerializeObject(new{passed=true,scene=ScenePath,bossPrefab=Root+"AI/Prefabs/UDK7MBoss.prefab",pillars=arena.pillars.Length,spawns=spawns.Count,bossAgent,bone=actions.Find("SlamRecover").action.hits[0].customBone},Formatting.Indented));Debug.Log("UDK7M_BOSS_BUILD_PASSED");
            }
            catch(Exception e){File.WriteAllText(Stage+"build_status.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{if(previous.IsValid())SceneManager.SetActiveScene(previous);if(created)EditorSceneManager.CloseScene(scene,true);busy=false;}
        }
        static void ConfigureActions(ActionSet set)
        {
            var prefab=set.previewModel;string path=AnimationUtility.CalculateTransformPath(prefab.GetComponentsInChildren<Transform>(true).First(t=>t.name=="DrillRoll"),prefab.transform);
            HitWindow Contact()=>new HitWindow{label="Drill Contact",hitGroup="drill-contact",startFrame=0,endFrame=100000,anchor=HitboxAnchor.CustomBone,customBone=path,shape=HitboxShape.Capsule,position=Vector3.up*.77f,radius=.28f,height=1.7f,damage=3,repeat=HitRepeat.Cooldown,sampleEveryFrames=2,damageCooldown=.25f,contactEffects=true,onlyWhileRotating=true,knockbackDistance=.15f};
            HitWindow Heavy(string name,string group,float start,float end,float damage,float knock)=>new HitWindow{label=name,hitGroup=group,startFrame=start,endFrame=end,anchor=HitboxAnchor.CustomBone,customBone=path,shape=HitboxShape.Capsule,position=Vector3.up*.77f,radius=.38f,height=1.9f,damage=damage,knockbackDistance=knock,blockedKnockbackMultiplier=.6f,contactEffects=true};
            foreach(var a in set.actions)
            {
                if(a.id=="OriginalDemo")continue;a.isAttack=true;
                var hits=(a.action.hits??Array.Empty<HitWindow>()).Where(h=>h.label!="Drill Contact").ToList();
                if(a.id=="SlamRecover"){hits.Clear();hits.Add(Heavy("下砸有效帧","slam-main",3,8,40,3));}
                if(a.id=="TripleSlam")
                {hits.Clear();hits.Add(Heavy("第一砸","triple-1",12,19,35,2));hits.Add(Heavy("第二砸","triple-2",30,37,35,2));hits.Add(Heavy("第三砸","triple-3",54,63,45,3));a.Motion.events=a.Motion.events.Where(e=>e.name!="ThirdSlam").Concat(new[]{new MotionFrameEvent{name="ThirdSlam",frame=59,kind=MotionEventKind.Signal}}).ToArray();}
                if(a.id=="ChargeHold"||a.id=="SlamHold")hits.Add(new HitWindow{label="Tracked chassis impact",hitGroup="chassis",startFrame=0,endFrame=100000,anchor=HitboxAnchor.Root,position=Vector3.up*1.1f,size=new Vector3(4.8f,2.3f,6),damage=20,repeat=HitRepeat.Cooldown,damageCooldown=.7f,knockbackDistance=2,blockedKnockbackMultiplier=.7f});
                hits.Add(Contact());a.action.hits=hits.ToArray();
            }
            EditorUtility.SetDirty(set);
        }
        static void ConfigureGauntletAndKnockback()
        {
            var gloves=A<ActionSet>("Assets/Game/Shared/Actions/Resources/GauntletActions.asset");for(int i=1;i<=4;i++){var a=gloves.Find("punch"+i);if(a==null)continue;a.chargeLevel=i-1;foreach(var h in a.action.hits){if(i==4)h.damage=160;h.knockbackPerDamage=.012f;}}EditorUtility.SetDirty(gloves);
            var enemy=A<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset");float damage=enemy.attack.hits.Where(h=>h.enabled).Select(h=>h.damage).DefaultIfEmpty(20).Max();
            foreach(var attack in new[]{enemy.headbutt,enemy.kick,enemy.attack,enemy.punch})foreach(var h in attack.hits){h.knockbackPerDamage=.8f/Mathf.Max(1,damage);h.blockedKnockbackMultiplier=attack==enemy.attack?.5f:0;}EditorUtility.SetDirty(enemy);
        }
        static int EnsureLayer(string name)
        {
            int exists=LayerMask.NameToLayer(name);if(exists>=0)return exists;var data=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);var layers=data.FindProperty("layers");for(int i=24;i<32;i++)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue)){layers.GetArrayElementAtIndex(i).stringValue=name;data.ApplyModifiedProperties();return i;}throw new Exception("No free layer for lower arena");
        }
        static int EnsureAgent(UDK7MTuning tune)
        {
            int id=-1;for(int i=0;i<NavMesh.GetSettingsCount();i++){var s=NavMesh.GetSettingsByIndex(i);if(NavMesh.GetSettingsNameFromID(s.agentTypeID)=="UDK7M Boss")id=s.agentTypeID;}
            if(id==-1)id=NavMesh.CreateSettings().agentTypeID;
            var serialized=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);var settings=serialized.FindProperty("m_Settings");var names=serialized.FindProperty("m_SettingNames");
            for(int i=0;i<settings.arraySize;i++){var s=settings.GetArrayElementAtIndex(i);if(s.FindPropertyRelative("agentTypeID").intValue!=id)continue;s.FindPropertyRelative("agentRadius").floatValue=tune.bodyRadius;s.FindPropertyRelative("agentHeight").floatValue=tune.bodyHeight;s.FindPropertyRelative("agentClimb").floatValue=.3f;s.FindPropertyRelative("agentSlope").floatValue=35;if(names.arraySize<=i)names.arraySize=i+1;names.GetArrayElementAtIndex(i).stringValue="UDK7M Boss";}
            serialized.ApplyModifiedProperties();return id;
        }
        static GameObject Group(string name,Transform parent=null,Vector3 position=default){var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);if(parent)go.transform.SetParent(parent,false);go.transform.localPosition=position;return go;}
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 size,Material material,int layer=0){var go=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(go,scene);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=size;go.layer=layer;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static Material Colour(string name,Color colour,float glow=0)
        {
            string path=Root+"AI/Materials/"+name+".mat";var m=A<Material>(path);if(m)return m;m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=name};m.SetColor("_BaseColor",colour);m.SetFloat("_Smoothness",.65f);if(glow>0){m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",colour*glow);}AssetDatabase.CreateAsset(m,path);return m;
        }
        static void BuildGeometry(Transform world,UDK7MArena arena)
        {
            var lower=Group("Liquid basins and return crystals",world).transform;Vector3[] holes={new Vector3(-10,0,-6),new Vector3(10,0,-6),new Vector3(-10,0,6),new Vector3(10,0,6)};
            for(int x=-22;x<=22;x+=4)for(int z=-22;z<=22;z+=4)
            {var p=new Vector3(x,0,z);if(p.magnitude>23.5f)continue;if(holes.Any(h=>(h-p).sqrMagnitude<.5f))continue;Cube("Concrete floor 4m",world,p-Vector3.up*.18f,new Vector3(3.98f,.36f,3.98f),concrete);}
            foreach(var hole in holes)
            {
                for(int i=-2;i<=2;i++)Cube("Open steel grating bar",lower,hole+new Vector3(i*.8f,.025f,0),new Vector3(.14f,.08f,4),metal,lowerLayer);
                Cube("Lower basin floor",lower,hole+Vector3.down*4.7f,new Vector3(5,.4f,5),concrete,lowerLayer);
                var water=Cube("Corrosive experimental liquid",lower,hole+Vector3.down*4.05f,new Vector3(4.5f,1,4.5f),Colour("Experimental_Liquid",new Color(.045f,.18f,.13f),.35f),lowerLayer);water.GetComponent<Collider>().isTrigger=true;water.AddComponent<UDK7MLiquid>().arena=arena;
                var visual=Cube("Liquid surface",lower,hole+Vector3.down*3.6f,new Vector3(4.5f,.02f,4.5f),water.GetComponent<Renderer>().sharedMaterial,lowerLayer);UnityEngine.Object.DestroyImmediate(water.GetComponent<MeshRenderer>());UnityEngine.Object.DestroyImmediate(visual.GetComponent<Collider>());
                for(int k=0;k<3;k++)
                {
                    var anchor=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>("Assets/Game/Environment/crystal/Prefabs/CrystalAnchor_Wall.prefab"),scene);anchor.name="Lower level escape crystal";anchor.transform.SetParent(lower,false);anchor.transform.position=hole+new Vector3((k-1)*1.1f,-1.3f,1.1f);anchor.transform.localScale=Vector3.one*.7f;
                    var surface=anchor.GetComponent<CrystalSurface>();if(!surface)surface=anchor.AddComponent<CrystalSurface>();surface.contact=anchor.GetComponentInChildren<Collider>();surface.useLaunchNormal=true;surface.launchNormal=Vector3.up;surface.normalLaunchSpeed=5;surface.upwardLaunchSpeed=10;
                    foreach(var t in anchor.GetComponentsInChildren<Transform>())t.gameObject.layer=lowerLayer;
                }
                for(int s=-1;s<=1;s+=2)Cube("Yellow hole edge",world,hole+new Vector3(s*2.05f,.03f,0),new Vector3(.1f,.09f,4.2f),yellow);
            }
            for(int i=0;i<32;i++)
            {
                float angle=(i+.5f)*360/32,rad=angle*Mathf.Deg2Rad;var wall=Cube("Bunker perimeter panel",world,new Vector3(Mathf.Sin(rad)*24,4,Mathf.Cos(rad)*24),new Vector3(4.75f,8,.5f),concrete);wall.transform.localRotation=Quaternion.Euler(0,angle,0);
                if(i%4==0){var stripe=Cube("Facility safety band",world,new Vector3(Mathf.Sin(rad)*23.7f,2.8f,Mathf.Cos(rad)*23.7f),new Vector3(4.5f,.3f,.03f),yellow);stripe.transform.localRotation=wall.transform.localRotation;}
            }
            Dome(world);var pillars=new List<UDK7MCrystalPillar>();
            for(int i=0;i<4;i++)
            {
                float angle=(45+i*90)*Mathf.Deg2Rad;var p=new Vector3(Mathf.Sin(angle)*14,0,Mathf.Cos(angle)*14);var root=Group("One-use crystal column "+(i+1),world,p);var core=GameObject.CreatePrimitive(PrimitiveType.Cylinder);SceneManager.MoveGameObjectToScene(core,scene);core.name="Reinforced concrete column";core.transform.SetParent(root.transform,false);core.transform.localPosition=Vector3.up*8;core.transform.localScale=new Vector3(2.4f,8,2.4f);core.GetComponent<Renderer>().sharedMaterial=concrete;
                var pillar=root.AddComponent<UDK7MCrystalPillar>();var crystals=Group("Consumable crystal growth",root.transform);pillar.crystals=crystals;var bounds=crystals.AddComponent<BoxCollider>();bounds.center=Vector3.up*4;bounds.size=new Vector3(5,8,5);bounds.isTrigger=true;pillar.crystalBounds=bounds;
                for(int n=0;n<12;n++){var cluster=(GameObject)PrefabUtility.InstantiatePrefab(A<GameObject>("Assets/Game/Environment/crystal/Prefabs/Crystal_Cluster_Wall.prefab"),scene);cluster.transform.SetParent(crystals.transform,false);float a=n*137.5f*Mathf.Deg2Rad;cluster.transform.localPosition=new Vector3(Mathf.Sin(a)*1.35f,1+(n%6)*1.1f,Mathf.Cos(a)*1.35f);cluster.transform.localRotation=Quaternion.Euler(0,n*137.5f,0);cluster.transform.localScale=Vector3.one*1.2f;foreach(var c in cluster.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(c);}
                pillars.Add(pillar);
                for(int j=0;j<3;j++)Cube("Column yellow steel collar",root.transform,new Vector3(0,1+j*4,0),new Vector3(2.7f,.12f,2.7f),yellow);
            }
            arena.pillars=pillars.ToArray();
            Cube("Entrance safe landing",world,new Vector3(0,-.18f,-22),new Vector3(7,.36f,5),concrete);
        }
        static void Dome(Transform parent)
        {
            var vertices=new List<Vector3>();var indices=new List<int>();int sides=48,rings=10;
            for(int r=0;r<=rings;r++){float phi=r*Mathf.PI*.5f/rings;for(int i=0;i<=sides;i++){float theta=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Sin(theta)*24*Mathf.Cos(phi),8+16*Mathf.Sin(phi),Mathf.Cos(theta)*24*Mathf.Cos(phi)));}}
            for(int r=0;r<rings;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i,b=a+sides+1;indices.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{name="Bunker dome inward shell"};mesh.SetVertices(vertices);mesh.SetTriangles(indices,0);mesh.RecalculateNormals();mesh.uv=vertices.Select(v=>new Vector2(Mathf.Atan2(v.x,v.z)*4,v.y*.2f)).ToArray();AssetDatabase.CreateAsset(mesh,Root+"AI/BunkerDome.asset");var go=Group("Segmented concrete dome",parent);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=A<Material>(SV+"Materials/SV_Ceiling_Aged.mat");go.AddComponent<MeshCollider>().sharedMesh=mesh;
            for(int ring=0;ring<3;ring++){float phi=(ring+1)*Mathf.PI/8;float radius=24*Mathf.Cos(phi),y=8+16*Mathf.Sin(phi);for(int s=0;s<32;s++){float angle=(s+.5f)*360/32,rad=angle*Mathf.Deg2Rad;var beam=Cube("Dome structural rib",parent,new Vector3(Mathf.Sin(rad)*radius,y,Mathf.Cos(rad)*radius),new Vector3(radius*.2f,.18f,.18f),metal);beam.transform.localRotation=Quaternion.Euler(0,angle,0);}}
        }
        static void Lighting()
        {
            RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.35f,.4f,.44f);RenderSettings.ambientEquatorColor=new Color(.24f,.27f,.29f);RenderSettings.ambientGroundColor=new Color(.12f,.13f,.14f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.09f,.12f,.13f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.006f;
            AssetReviewUtility.Light(scene,"Facility fill",new Vector3(0,20,0),Vector3.zero,LightType.Directional,1.2f,new Color(.85f,.91f,1));
            for(int i=0;i<8;i++){float a=i*Mathf.PI/4;var pos=new Vector3(Mathf.Sin(a)*16,11,Mathf.Cos(a)*16);var light=Group("Overhead sodium floodlight",null,pos);var l=light.AddComponent<Light>();l.type=LightType.Spot;l.range=30;l.spotAngle=100;l.intensity=140;l.color=new Color(1,.82f,.57f);light.transform.LookAt(new Vector3(pos.x*.35f,0,pos.z*.35f));l.shadows=LightShadows.Soft;}
        }
    }
}
