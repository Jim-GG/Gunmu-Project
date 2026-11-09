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
using Unity.AI.Navigation;
using Unity.Cinemachine;
using CrystalParkour;
using FirstPerson;
using GameActions;
using Resonator.AI;
using UDK7M;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static class UDK7MThreePhaseBuilder
    {
        public const string ScenePath="Assets/Game/Scene/UDK7MBossArena_ThreePhase.unity";
        const string Root="Assets/Game/Enemies/UDK7M/AI/",Stage="AssetStaging/UDK7M/Boss/",SV="Assets/Game/Environment/SovietFacility/Materials/";
        static Scene scene;static bool busy;static Material concrete,metal,yellow;static int climbLayer;static double settled;
        static UDK7MThreePhaseBuilder(){settled=EditorApplication.timeSinceStartup+5;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=state=>{if(state==PlayModeStateChange.EnteredEditMode)settled=EditorApplication.timeSinceStartup+3;};}
        static void Poll()
        {
            if(busy||EditorApplication.timeSinceStartup<settled||EditorApplication.isCompiling||EditorApplication.isUpdating||Application.isPlaying||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("UDK7MBossValidation",false))return;
            if(File.Exists(Stage+"ThreePhaseReview.request")){File.Delete(Stage+"ThreePhaseReview.request");OpenArena();return;}
            if(File.Exists(Stage+"ThreePhaseBuild.request")){File.Delete(Stage+"ThreePhaseBuild.request");Build();}
        }
        static T Load<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        [MenuItem("Tools/UDK7M/Open Three Phase Arena")]
        public static void OpenArena()
        {
            var prior=SceneManager.GetActiveScene();var asset=Load<SceneAsset>(ScenePath);if(!asset)return;
            if(Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).Any(s=>s.isDirty)){Selection.activeObject=asset;EditorGUIUtility.PingObject(asset);}
            else EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Single);
            File.WriteAllText(Stage+"three_phase_review.json",JsonConvert.SerializeObject(new{active=SceneManager.GetActiveScene().path,preservedDirty=prior.IsValid()&&prior.isDirty},Formatting.Indented));
        }
        [MenuItem("Tools/UDK7M/Build Three Phase Arena Copy")]
        public static void Build()
        {
            busy=true;var prior=SceneManager.GetActiveScene();bool opened=false;
            try
            {
                if(File.Exists(ScenePath))throw new Exception("Three-phase arena already exists; preserving your edits.");
                var source=SceneManager.GetSceneByPath(UDK7MBossBuilder.ScenePath);
                if(source.IsValid()&&source.isLoaded)EditorSceneManager.SaveScene(source,ScenePath,true);else if(!AssetDatabase.CopyAsset(UDK7MBossBuilder.ScenePath,ScenePath))throw new Exception("Cannot copy original arena");
                scene=EditorSceneManager.OpenScene(ScenePath,OpenSceneMode.Additive);opened=true;SceneManager.SetActiveScene(scene);
                var arena=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).Single();
                var tuning=UnityEngine.Object.Instantiate(arena.tuning);tuning.name="UDK7M_ThreePhase";tuning.holdTurnSpeed=18;tuning.chargeTrackingAngle=30;tuning.navigationRadius=3.8f;tuning.arenaRadius=36;tuning.decisionInterval=new Vector2(.3f,.55f);tuning.centralLaunchSpeed=25;tuning.captureFlyingOnly=true;
                AssetDatabase.CreateAsset(tuning,Root+"UDK7M_ThreePhase.asset");arena.tuning=arena.boss.tuning=arena.boss.motor.tuning=tuning;
                var profile=UnityEngine.Object.Instantiate(arena.boss.actions);profile.name="UDK7M_ThreePhaseActions";
                void Marker(string id,string name,float frame){var a=profile.Find(id);a.Motion.events=(a.Motion.events??Array.Empty<MotionFrameEvent>()).Where(e=>e.name!=name).Append(new MotionFrameEvent{name=name,frame=frame,kind=MotionEventKind.Signal}).ToArray();}
                Marker("TripleSlam","SlamLight1",16);Marker("TripleSlam","SlamLight2",34);Marker("TripleSlam","ThirdSlam",59);Marker("SlamRecover","SlamImpact",6);Marker("ChargeRecover","SlamImpact",3);
                AssetDatabase.CreateAsset(profile,"Assets/Game/Enemies/UDK7M/Actions/UDK7M_ThreePhaseActions.asset");arena.boss.actions=profile;
                concrete=Load<Material>(SV+"SV_Concrete_Damp.mat");metal=Load<Material>(SV+"SV_Metal_Diamond.mat");yellow=Load<Material>(SV+"SV_Paint_Yellow.mat");climbLayer=LayerMask.NameToLayer("ArenaLowerFloor");
                var world=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>()).First(t=>t.name=="Main arena modules");
                var keep=new HashSet<Transform>(arena.pillars.Select(p=>p.transform));foreach(Transform child in world.Cast<Transform>().ToArray())if(!keep.Contains(child))UnityEngine.Object.DestroyImmediate(child.gameObject);
                Cube("Sealed continuous safety floor",world,new Vector3(0,-.36f,0),new Vector3(76,.4f,76),concrete);
                for(int x=-34;x<=34;x+=4)for(int z=-34;z<=34;z+=4)if(new Vector2(x,z).magnitude<36)Cube("Concrete floor 4m",world,new Vector3(x,-.18f,z),new Vector3(3.99f,.36f,3.99f),concrete);
                for(int i=0;i<48;i++)
                {
                    float a=(i+.5f)*Mathf.PI*2/48;var wall=Cube("72m bunker perimeter",world,new Vector3(Mathf.Sin(a)*36,5,Mathf.Cos(a)*36),new Vector3(4.9f,10,.6f),concrete);wall.transform.localRotation=Quaternion.Euler(0,a*Mathf.Rad2Deg,0);
                    var band=Cube("Perimeter yellow band",world,new Vector3(Mathf.Sin(a)*35.65f,3,Mathf.Cos(a)*35.65f),new Vector3(4.8f,.3f,.04f),yellow);band.transform.localRotation=wall.transform.localRotation;UnityEngine.Object.DestroyImmediate(band.GetComponent<Collider>());
                }
                BuildDome(world);
                for(int i=0;i<arena.pillars.Length;i++)
                {
                    var pillar=arena.pillars[i];float a=(45+i*90)*Mathf.Deg2Rad;pillar.transform.position=new Vector3(Mathf.Sin(a)*21,0,Mathf.Cos(a)*21);
                    foreach(var c in pillar.GetComponentsInChildren<Collider>())if(c.name.Contains("collar"))UnityEngine.Object.DestroyImmediate(c);
                    Cube("Upper maintenance landing",pillar.transform,new Vector3(0,8.8f,0),new Vector3(6,.35f,6),metal,climbLayer);
                    for(int side=0;side<4;side++)
                    {
                        float theta=side*Mathf.PI*.5f;var anchor=Anchor("Column rebound crystal",pillar.transform,new Vector3(Mathf.Sin(theta)*2.2f,1.5f,Mathf.Cos(theta)*2.2f),1.1f,19);anchor.transform.localRotation=Quaternion.Euler(0,theta*Mathf.Rad2Deg,0);anchor.launchNormal=anchor.transform.InverseTransformDirection(Vector3.up);
                        var upper=Anchor("Upper pillar rebound relay",pillar.transform,new Vector3(Mathf.Sin(theta)*2.3f,6,Mathf.Cos(theta)*2.3f),.75f,14);upper.launchNormal=upper.transform.InverseTransformDirection(Vector3.up);
                    }
                    var pure=Anchor("Pure resonance crystal · pillar "+(i+1),pillar.transform,new Vector3(0,9.65f,-2.4f),.8f,6);var grant=pure.gameObject.AddComponent<UDK7MPureCrystal>();grant.arena=arena;
                    var material=UnityEngine.Object.Instantiate(pure.GetComponentInChildren<Renderer>().sharedMaterial);material.name="Pure cyan resonance";if(material.HasProperty("_BaseColor"))material.SetColor("_BaseColor",new Color(.4f,.9f,1));AssetDatabase.CreateAsset(material,Root+"Materials/PureCrystal_"+i+".mat");foreach(var r in pure.GetComponentsInChildren<Renderer>())r.sharedMaterial=material;
                }
                var motor=arena.player.GetComponent<FirstPersonMotor>();var traversal=arena.player.GetComponent<CrystalTraversal>();traversal.maximumPullDistance=29;traversal.pullSpeed=20;traversal.pullTimeout=2;motor.airSpeed=8;motor.airAcceleration=22;
                arena.checkpoint.position=new Vector3(0,.1f,-34);arena.player.transform.position=arena.checkpoint.position;arena.ceiling=29;
                var pound=arena.player.GetComponent<UDK7MGroundPound>();if(!pound)pound=arena.player.gameObject.AddComponent<UDK7MGroundPound>();pound.arena=arena;
                foreach(var camera in arena.player.GetComponentsInChildren<CinemachineCamera>(true)){var listener=camera.GetComponent<CinemachineImpulseListener>();if(!listener)listener=camera.gameObject.AddComponent<CinemachineImpulseListener>();listener.ChannelMask|=tuning.impulseChannel;listener.Gain=.7f;listener.UseCameraSpace=true;}
                arena.feedback=arena.GetComponent<UDK7MFeedback>();if(!arena.feedback)arena.feedback=arena.gameObject.AddComponent<UDK7MFeedback>();arena.feedback.tuning=tuning;
                var entry=arena.GetComponentsInChildren<UDK7MArenaEntry>().First();entry.transform.position=new Vector3(0,1,-30);entry.GetComponent<BoxCollider>().size=new Vector3(64,4,2);
                for(int i=0;i<arena.minions.spawnPoints.Length;i++){float a=i*Mathf.PI*2/arena.minions.spawnPoints.Length;arena.minions.spawnPoints[i].position=new Vector3(Mathf.Sin(a)*30,0,Mathf.Cos(a)*30);}
                int type=EnsureAgent();arena.boss.motor.agent.agentTypeID=type;arena.boss.motor.agent.radius=tuning.navigationRadius;
                foreach(var surface in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NavMeshSurface>()))
                {
                    bool boss=surface.agentTypeID!=0;surface.RemoveData();surface.navMeshData=null;if(boss)surface.agentTypeID=type;surface.BuildNavMesh();AssetDatabase.CreateAsset(surface.navMeshData,Root+"Navigation/ThreePhase_"+(boss?"Boss":"Human")+".asset");
                }
                arena.boss.transform.SetPositionAndRotation(new Vector3(0,0,4),Quaternion.Euler(0,180,0));
                foreach(var p in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<ReflectionProbe>()))p.size=new Vector3(74,40,74);
                foreach(var light in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>()))if(light.type==LightType.Spot){var position=light.transform.position;position.x*=1.35f;position.z*=1.35f;position.y=16;light.transform.position=position;light.range=48;light.transform.LookAt(new Vector3(position.x*.3f,0,position.z*.3f));}
                AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);
                var review=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.gameObject.name=="Disabled overview camera");review.transform.SetPositionAndRotation(new Vector3(0,8,-33),Quaternion.LookRotation(new Vector3(0,5,0)-new Vector3(0,8,-33)));review.fieldOfView=75;AssetReviewUtility.Capture(scene,review,Stage+"ThreePhase_Arena.png");review.enabled=false;EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Stage+"three_phase_build.json",JsonConvert.SerializeObject(new{passed=true,scene=ScenePath,radius=36,pillars=4,holes=0,agent=type,sourceDirtyPreserved=source.IsValid()&&source.isDirty},Formatting.Indented));
            }
            catch(Exception e){File.WriteAllText(Stage+"three_phase_build.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);if(opened)EditorSceneManager.CloseScene(scene,true);busy=false;}
        }
        static GameObject Cube(string name,Transform parent,Vector3 position,Vector3 scale,Material material,int layer=0)
        {var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=scale;go.layer=layer;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static CrystalSurface Anchor(string name,Transform parent,Vector3 position,float scale,float lift)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>("Assets/Game/Environment/crystal/Prefabs/CrystalAnchor_Wall.prefab"),scene);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=position;go.transform.localScale=Vector3.one*scale;foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=climbLayer;
            var surface=go.GetComponent<CrystalSurface>();if(!surface)surface=go.AddComponent<CrystalSurface>();surface.contact=go.GetComponentInChildren<Collider>();surface.useLaunchNormal=true;surface.launchNormal=go.transform.InverseTransformDirection(Vector3.up);surface.normalLaunchSpeed=0;surface.upwardLaunchSpeed=lift;return surface;
        }
        static int EnsureAgent()
        {
            int id=-1;for(int i=0;i<NavMesh.GetSettingsCount();i++){int candidate=NavMesh.GetSettingsByIndex(i).agentTypeID;if(NavMesh.GetSettingsNameFromID(candidate)=="UDK7M WideArena")id=candidate;}
            if(id==-1)id=NavMesh.CreateSettings().agentTypeID;var data=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/NavMeshAreas.asset")[0]);var settings=data.FindProperty("m_Settings");var names=data.FindProperty("m_SettingNames");
            for(int i=0;i<settings.arraySize;i++){var p=settings.GetArrayElementAtIndex(i);if(p.FindPropertyRelative("agentTypeID").intValue!=id)continue;p.FindPropertyRelative("agentRadius").floatValue=3.8f;p.FindPropertyRelative("agentHeight").floatValue=4;p.FindPropertyRelative("agentClimb").floatValue=.3f;if(names.arraySize<=i)names.arraySize=i+1;names.GetArrayElementAtIndex(i).stringValue="UDK7M WideArena";}data.ApplyModifiedProperties();return id;
        }
        static void BuildDome(Transform parent)
        {
            var vertices=new List<Vector3>();var triangles=new List<int>();int sides=64,rings=12;
            for(int r=0;r<=rings;r++){float phi=r*Mathf.PI*.5f/rings;for(int i=0;i<=sides;i++){float a=i*Mathf.PI*2/sides;vertices.Add(new Vector3(Mathf.Sin(a)*36*Mathf.Cos(phi),10+22*Mathf.Sin(phi),Mathf.Cos(a)*36*Mathf.Cos(phi)));}}
            for(int r=0;r<rings;r++)for(int i=0;i<sides;i++){int a=r*(sides+1)+i,b=a+sides+1;triangles.AddRange(new[]{a,b,a+1,a+1,b,b+1});}
            var mesh=new Mesh{name="72m inward concrete dome"};mesh.SetVertices(vertices);mesh.SetTriangles(triangles,0);mesh.RecalculateNormals();mesh.uv=vertices.Select(v=>new Vector2(Mathf.Atan2(v.x,v.z)*6,v.y*.2f)).ToArray();AssetDatabase.CreateAsset(mesh,Root+"ThreePhaseDome.asset");
            var go=new GameObject("72m concrete dome",typeof(MeshFilter),typeof(MeshRenderer),typeof(MeshCollider));go.transform.SetParent(parent,false);go.GetComponent<MeshFilter>().sharedMesh=mesh;go.GetComponent<MeshCollider>().sharedMesh=mesh;go.GetComponent<Renderer>().sharedMaterial=Load<Material>(SV+"SV_Ceiling_Aged.mat");
        }
    }
}

