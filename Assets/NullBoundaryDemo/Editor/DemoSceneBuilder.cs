using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.Playables;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.Timeline;
using Unity.AI.Navigation;

namespace NullBoundary.Demo.Editor
{
    public static class DemoSceneBuilder
    {
        public const string Root="Assets/NullBoundaryDemo";
        public const string ScenePath=Root+"/Scenes/NullBoundary_FirstDescent.unity";
        static Transform world;
        static Material concrete,steel,green,cream,blue,amber,red,crystal,dark;
        static Mesh crystalMesh;
        [MenuItem("Null Boundary/Build Greybox Demo")]
        public static void BuildMenu() { Debug.Log(Build()); }
        public static string Build()
        {
            if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play Mode before building.");
            for(int i=0;i<SceneManager.sceneCount;i++)
            {
                var s=SceneManager.GetSceneAt(i);
                if(s.path==ScenePath)throw new InvalidOperationException("Close the Demo scene before rebuilding; preserve edits first.");
            }
            var previous=SceneManager.GetActiveScene();
            var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);
            try
            {
                SceneManager.SetActiveScene(scene);
                PrepareAssets();
                RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Flat;
                RenderSettings.ambientLight=new Color(.27f,.31f,.34f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Exponential;
                RenderSettings.fogColor=new Color(.045f,.075f,.09f);RenderSettings.fogDensity=.009f;
                world=new GameObject("World_Greybox").transform;
                BuildRooms();
                BuildCanteen();
                BuildKitchen(out var hinge,out var body);
                var system=new GameObject("Demo Systems");
                var flow=system.AddComponent<DemoFlow>();
                flow.sound=system.AddComponent<DemoSound>();
                var playerGO=new GameObject("Player");
                var motor=playerGO.AddComponent<CharacterController>();
                motor.height=1.8f;motor.center=new Vector3(0,.9f,0);motor.radius=.32f;
                motor.stepOffset=.28f;motor.skinWidth=.04f;
                var player=playerGO.AddComponent<DemoPlayer>();playerGO.transform.position=new Vector3(0,.08f,3);
                var cameraGO=new GameObject("Main Camera");cameraGO.tag="MainCamera";
                cameraGO.transform.SetParent(playerGO.transform,false);cameraGO.transform.localPosition=new Vector3(0,1.65f,0);
                var camera=cameraGO.AddComponent<Camera>();camera.nearClipPlane=.06f;camera.farClipPlane=150;camera.fieldOfView=76;
                cameraGO.AddComponent<AudioListener>();camera.clearFlags=CameraClearFlags.SolidColor;
                camera.backgroundColor=new Color(.025f,.045f,.06f);player.view=camera;
                var hands=new GameObject("Gauntlet_ViewModel").transform;hands.SetParent(camera.transform,false);
                hands.localPosition=new Vector3(0,-.3f,.5f);player.hands=hands;
                LocalCube("Left forearm",new Vector3(-.23f,0,.1f),new Vector3(.19f,.19f,.42f),steel,hands);
                LocalCube("Right forearm",new Vector3(.23f,0,.1f),new Vector3(.19f,.19f,.42f),steel,hands);
                LocalCube("Left impulse core",new Vector3(-.23f,.11f,.25f),new Vector3(.12f,.06f,.15f),blue,hands);
                LocalCube("Right impulse core",new Vector3(.23f,.11f,.25f),new Vector3(.12f,.06f,.15f),blue,hands);
                flow.player=player;flow.fridgeHinge=hinge;flow.fridgeBody=body;
                flow.chaseDoor=Door("A_ChaseGate",new Vector3(0,0,66),true,0);
                flow.entryDoor=Door("B_EnemyEntry",new Vector3(8,0,77),false,90);
                flow.exitDoor=Door("C_ExitGate",new Vector3(0,0,84),false,0);
                Button("Close gate control",new Vector3(-1.65f,1.2f,68),DemoAction.CloseChaseDoor,"关闭入口液压门",red);
                Label("GATE A",new Vector3(-1.5f,2.2f,68.05f),.045f,Color.white);
                Button("Calibration terminal",new Vector3(-4.5f,1.5f,74.8f),DemoAction.Calibrate,"校准冲量装置",blue);
                Cube("Console support",new Vector3(-4.5f,.6f,75),new Vector3(1.8f,1.2f,.8f),green);
                Cube("Gauntlet bench",new Vector3(-4.5f,.8f,78),new Vector3(2,.25f,1.2f),steel);
                var pickup=Button("Impulse Gauntlet pickup",new Vector3(-4.5f,1.2f,77.7f),DemoAction.TakeGauntlet,"取下冲量拳套",amber);
                pickup.transform.localScale=new Vector3(.65f,.45f,.6f);flow.gauntletPickup=pickup.transform;
                Crystals(new Vector3(-4.1f,1.4f,77.8f),.25f,pickup.transform);
                Label("01 / IMPULSE STORAGE",new Vector3(-3.4f,3.5f,83.7f),.055f,new Color(.65f,.8f,1));
                Label("EXPERIMENTAL EQUIPMENT",new Vector3(-6.5f,2.8f,79),.04f,Color.white);
                flow.labLights=new[]{Lamp("Lab Blue 1",new Vector3(-4,4,71),new Color(.45f,.7f,1),3,11),
                    Lamp("Lab Blue 2",new Vector3(4,4,79),new Color(.45f,.7f,1),3,11)};
                for(int i=0;i<3;i++)
                    Cube("Impact test block",new Vector3(3.3f+i*1.25f,.6f,73.5f),new Vector3(.9f,1.2f,1.1f),steel);

                var prefab=EnemyPrefab();
                flow.enemyPrefab=prefab;
                var chaserGO=(GameObject)PrefabUtility.InstantiatePrefab(prefab.gameObject,scene);
                chaserGO.name="Resonator_Knocking";chaserGO.transform.position=new Vector3(-.8f,0,34.5f);
                flow.chaser=chaserGO.GetComponent<DemoEnemy>();flow.chaser.isChaser=true;
                var severe=Human("Severely crystallized subject",new Vector3(-.8f,.25f,35.8f),out _,out _);
                severe.rotation=Quaternion.Euler(83,0,10);
                Crystals(new Vector3(-.8f,.8f,35.8f),.7f,severe);
                flow.spawnPoints=new Transform[3];
                for(int i=0;i<3;i++)
                {
                    var point=new GameObject("Encounter Spawn "+i).transform;
                    point.position=new Vector3(12+i,0,76+i*.6f);flow.spawnPoints[i]=point;
                }
                BuildIntro(system,flow);
                var nav=world.gameObject.AddComponent<NavMeshSurface>();nav.collectObjects=CollectObjects.Children;
                nav.useGeometry=NavMeshCollectGeometry.PhysicsColliders;nav.layerMask=~(1<<2);
                nav.overrideVoxelSize=true;nav.voxelSize=.13f;nav.BuildNavMesh();
                if(!nav.navMeshData)throw new InvalidOperationException("NavMesh build produced no data.");
                string navPath=Root+"/Art/DemoNavigation.asset";
                if(AssetDatabase.LoadAssetAtPath<NavMeshData>(navPath))AssetDatabase.DeleteAsset(navPath);
                AssetDatabase.CreateAsset(nav.navMeshData,navPath);flow.navigation=nav;
                AssetDatabase.SaveAssets();
                EditorSceneManager.SaveScene(scene,ScenePath);
                int count=UnityEngine.Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None).Length;
                File.WriteAllText("Temp/NullBoundaryDemo/build-result.json",
                    JsonUtility.ToJson(new BuildResult{scene=ScenePath,renderers=count,navmesh=true,timeline=true},true));
                return "Demo saved: "+ScenePath+" / renderers "+count;
            }
            finally
            {
                if(previous.IsValid()&&previous.isLoaded)SceneManager.SetActiveScene(previous);
                if(scene.IsValid())EditorSceneManager.CloseScene(scene,true);
            }
        }
        [Serializable]sealed class BuildResult{public string scene;public int renderers;public bool navmesh,timeline;}
        static void PrepareAssets()
        {
            concrete=Mat("Concrete",new Color(.24f,.27f,.26f));
            steel=Mat("Steel",new Color(.21f,.26f,.28f),.65f);
            green=Mat("Soviet Green",new Color(.16f,.27f,.23f));
            cream=Mat("Old Porcelain",new Color(.66f,.64f,.52f));
            blue=Mat("Control Cyan",new Color(.12f,.6f,.77f),.2f,true);
            amber=Mat("Sodium Amber",new Color(.9f,.43f,.12f),.1f,true);
            red=Mat("Safety Red",new Color(.7f,.09f,.055f),.1f,true);
            crystal=Mat("Abyssonance Study",new Color(.32f,.53f,.62f),.45f,true);
            dark=Mat("Rubber",new Color(.055f,.07f,.075f));
            string meshPath=Root+"/Art/CrystalStudy.asset";
            crystalMesh=AssetDatabase.LoadAssetAtPath<Mesh>(meshPath);
            if(!crystalMesh)
            {
                crystalMesh=new Mesh{name="Crystal octahedron placeholder"};
                crystalMesh.vertices=new[]{new Vector3(0,1,0),new Vector3(-.4f,0,0),new Vector3(0,0,.4f),
                    new Vector3(.4f,0,0),new Vector3(0,0,-.4f),new Vector3(0,-.4f,0)};
                crystalMesh.triangles=new[]{0,2,1,0,3,2,0,4,3,0,1,4,5,1,2,5,2,3,5,3,4,5,4,1};
                crystalMesh.RecalculateNormals();AssetDatabase.CreateAsset(crystalMesh,meshPath);
            }
        }
        static Material Mat(string name,Color color,float metal=0,bool glow=false)
        {
            string path=Root+"/Art/"+name.Replace(" ","")+".mat";
            var mat=AssetDatabase.LoadAssetAtPath<Material>(path);
            if(!mat){mat=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(mat,path);}
            mat.color=color;mat.SetFloat("_Metallic",metal);mat.SetFloat("_Smoothness",.28f);
            if(glow){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",color*.7f);}
            EditorUtility.SetDirty(mat);return mat;
        }
        static GameObject Cube(string name,Vector3 pos,Vector3 scale,Material mat,bool solid=true,Transform parent=null)
        {
            var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent?parent:world,true);
            go.transform.position=pos;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=mat;
            if(!solid)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());
            return go;
        }
        static GameObject LocalCube(string name,Vector3 pos,Vector3 scale,Material mat,Transform parent)
        {
            var go=Cube(name,Vector3.zero,scale,mat,false,parent);go.transform.localPosition=pos;return go;
        }
        static void WallZ(string name,float z,float halfWidth,float height,float gap)
        {
            if(gap<=0){Cube(name,new Vector3(0,height/2,z),new Vector3(halfWidth*2,height,.35f),green);return;}
            float width=halfWidth-gap/2;
            Cube(name+" left",new Vector3(-gap/2-width/2,height/2,z),new Vector3(width,height,.35f),green);
            Cube(name+" right",new Vector3(gap/2+width/2,height/2,z),new Vector3(width,height,.35f),green);
            Cube(name+" header",new Vector3(0,(height+3.3f)/2,z),new Vector3(gap,height-3.3f,.35f),steel);
        }
        static void Room(string name,float width,float from,float to,float southGap,float northGap,bool roof=true)
        {
            Cube(name+" floor",new Vector3(0,-.15f,(from+to)/2),new Vector3(width,.3f,to-from),concrete);
            WallZ(name+" south",from,width/2,4.8f,southGap);WallZ(name+" north",to,width/2,4.8f,northGap);
            Cube(name+" west",new Vector3(-width/2,2.4f,(from+to)/2),new Vector3(.35f,4.8f,to-from),green);
            if(name!="Lab")
                Cube(name+" east",new Vector3(width/2,2.4f,(from+to)/2),new Vector3(.35f,4.8f,to-from),green);
            if(roof)Cube(name+" ceiling",new Vector3(0,4.9f,(from+to)/2),new Vector3(width,.25f,to-from),concrete);
        }
        static void BuildRooms()
        {
            Room("Canteen",18,0,16,0,4,false);
            Room("Kitchen",10,16,28,4,4);
            Room("Corridor",5,28,66,4,4);
            Room("Lab",16,66,84,4,4);
            Room("Exit corridor",5,84,96,4,0);
            Cube("Lab east south",new Vector3(8,2.4f,70.5f),new Vector3(.35f,4.8f,9),green);
            Cube("Lab east north",new Vector3(8,2.4f,81.5f),new Vector3(.35f,4.8f,5),green);
            Cube("Lab east header",new Vector3(8,4.1f,77),new Vector3(.35f,1.4f,4),steel);
            Cube("Enemy holding floor",new Vector3(12,-.15f,77),new Vector3(8,.3f,8),concrete);
            Cube("Enemy holding east",new Vector3(16,2.4f,77),new Vector3(.35f,4.8f,8),green);
            Cube("Enemy holding south",new Vector3(12,2.4f,73),new Vector3(8,4.8f,.35f),green);
            Cube("Enemy holding north",new Vector3(12,2.4f,81),new Vector3(8,4.8f,.35f),green);
            Cube("Enemy holding roof",new Vector3(12,4.9f,77),new Vector3(8,.25f,8),concrete);
            // Four slabs leave an actual opening over the crashed elevator.
            Cube("Broken roof west",new Vector3(-5.5f,4.9f,8),new Vector3(7,.25f,16),concrete);
            Cube("Broken roof east",new Vector3(5.5f,4.9f,8),new Vector3(7,.25f,16),concrete);
            Cube("Broken roof north",new Vector3(0,4.9f,10.5f),new Vector3(4,.25f,11),concrete);
            Cube("Broken roof south",new Vector3(0,4.9f,.5f),new Vector3(4,.25f,1),concrete);
            for(int i=0;i<4;i++)
            {
                Cube("Exposed rebar",new Vector3(-1.6f+i*1.1f,5.1f,4.5f),new Vector3(.06f,.08f,2),steel);
                var rubble=Cube("Crash rubble",new Vector3(-2.4f+i*1.6f,.2f,4.4f),new Vector3(.6f,.4f,.7f),concrete);
                rubble.transform.rotation=Quaternion.Euler(12,i*35,23);
            }
            for(float z=31;z<66;z+=8)
            {
                Lamp("Corridor lamp",new Vector3(0,3.9f,z),new Color(1,.65f,.3f),2,9);
                Cube("Ceiling duct",new Vector3(-1.4f,4.1f,z),new Vector3(.5f,.45f,7.5f),steel);
                Cube("Floor guide",new Vector3(1.8f,.015f,z),new Vector3(.10f,.02f,5),amber,false);
            }
            Lamp("Kitchen lamp",new Vector3(0,4,22),new Color(.8f,.85f,.72f),2.4f,12);
            Lamp("Canteen lamp",new Vector3(0,4,10),new Color(1,.72f,.4f),3.5f,15);
            Lamp("Crash shaft spill",new Vector3(0,6,3),new Color(.45f,.65f,1),3,12);
            Lamp("Holding lamp",new Vector3(12,4,77),new Color(1,.45f,.2f),2,10);
            Lamp("Exit lamp",new Vector3(0,3.5f,90),new Color(.5f,.7f,.8f),2,9);
            Label("SECTOR 7 / CANTEEN",new Vector3(-4,3.4f,15.7f),.065f,new Color(.85f,.8f,.6f));
            Label("LAB 01  >>>",new Vector3(-1.6f,3.2f,63),.055f,new Color(.55f,.8f,1));
            CrashCabin();
        }
        static void CrashCabin()
        {
            Cube("Crashed cabin floor",new Vector3(0,.05f,2.8f),new Vector3(3.3f,.15f,3.6f),steel);
            Cube("Crashed cabin back",new Vector3(0,1.65f,1),new Vector3(3.3f,3.3f,.15f),steel);
            Cube("Crashed cabin left",new Vector3(-1.65f,1.65f,2.8f),new Vector3(.15f,3.3f,3.6f),steel);
            Cube("Crashed cabin right",new Vector3(1.65f,1.65f,2.8f),new Vector3(.15f,3.3f,3.6f),steel);
            var left=Cube("Bent door left",new Vector3(-1.5f,1.4f,4.6f),new Vector3(.65f,2.8f,.18f),steel);
            left.transform.rotation=Quaternion.Euler(0,-18,-9);
            var right=Cube("Bent door right",new Vector3(1.5f,1.4f,4.6f),new Vector3(.65f,2.8f,.18f),steel);
            right.transform.rotation=Quaternion.Euler(0,20,12);
        }
        static void BuildCanteen()
        {
            for(int x=-5;x<=5;x+=10)for(int z=8;z<=12;z+=4)
            {
                Cube("Dining table",new Vector3(x,.85f,z),new Vector3(3,.2f,1.4f),cream);
                Cube("Table pedestal",new Vector3(x,.4f,z),new Vector3(.5f,.8f,.5f),steel);
                for(int side=-1;side<=1;side+=2)
                {
                    Cube("Chair seat",new Vector3(x,.45f,z+side*1.1f),new Vector3(.8f,.15f,.65f),green);
                    Cube("Chair support",new Vector3(x,.2f,z+side*1.1f),new Vector3(.1f,.4f,.1f),steel);
                    Cube("Meal tray",new Vector3(x+.7f,.98f,z),new Vector3(.55f,.04f,.4f),steel,false);
                }
            }
            for(int i=0;i<3;i++)
            {
                Vector3 p=new Vector3(i==0?-3.8f:i==1?4:1,.28f,7+i*2);
                var victim=Human("Zero disaster victim",p,out _,out _);victim.rotation=Quaternion.Euler(83,i*70,0);
                Crystals(p+new Vector3(.15f,.5f,.5f),.48f,victim);
            }
        }
        static void BuildKitchen(out Transform hinge,out Transform body)
        {
            Cube("Kitchen preparation counter",new Vector3(3,.6f,22),new Vector3(1.3f,1.2f,7),steel);
            Cube("Fridge back",new Vector3(-3.5f,1.5f,23.7f),new Vector3(1.8f,3,.16f),cream);
            Cube("Fridge left",new Vector3(-4.4f,1.5f,23.1f),new Vector3(.15f,3,1.2f),cream);
            Cube("Fridge right",new Vector3(-2.6f,1.5f,23.1f),new Vector3(.15f,3,1.2f),cream);
            Cube("Fridge top",new Vector3(-3.5f,3,23.1f),new Vector3(1.8f,.15f,1.3f),cream);
            hinge=new GameObject("Fridge door hinge").transform;hinge.SetParent(world);
            hinge.position=new Vector3(-4.35f,0,22.45f);
            var panel=Cube("Fridge door",new Vector3(-3.5f,1.5f,22.45f),new Vector3(1.7f,2.9f,.16f),cream,true,hinge);
            panel.layer=2;
            body=Human("Fridge living resonator",new Vector3(-3.4f,0,23),out _,out _);
            Crystals(new Vector3(-3.15f,1.5f,23),.32f,body);
        }
        static DemoDoor Door(string name,Vector3 pos,bool open,float yaw)
        {
            var root=new GameObject(name);root.transform.SetParent(world);root.transform.SetPositionAndRotation(pos,Quaternion.Euler(0,yaw,0));
            var slab=LocalCube("Hydraulic slab",new Vector3(0,1.65f,0),new Vector3(3.9f,3.3f,.3f),steel,root.transform);
            slab.AddComponent<BoxCollider>();slab.layer=2;
            LocalCube("Stripe",new Vector3(0,1.65f,-.17f),new Vector3(3.4f,.25f,.04f),amber,root.transform);
            // Stripe belongs to the moving slab.
            var stripe=root.transform.Find("Stripe");stripe.SetParent(slab.transform,true);
            var door=root.AddComponent<DemoDoor>();door.slab=slab.transform;door.startsOpen=open;
            return door;
        }
        static GameObject Button(string name,Vector3 position,DemoAction action,string prompt,Material mat)
        {
            var go=Cube(name,position,new Vector3(.65f,.65f,.3f),mat);
            var interact=go.AddComponent<DemoInteractable>();interact.action=action;interact.prompt=prompt;return go;
        }
        static Light Lamp(string name,Vector3 position,Color color,float intensity,float range)
        {
            var go=new GameObject(name);go.transform.SetParent(world);go.transform.position=position;
            var light=go.AddComponent<Light>();light.type=LightType.Point;light.color=color;light.intensity=intensity;light.range=range;
            Cube(name+" housing",position,new Vector3(.8f,.15f,.5f),amber,false);
            return light;
        }
        static TextMesh Label(string text,Vector3 position,float size,Color color,Transform parent=null)
        {
            var go=new GameObject(text);go.transform.SetParent(parent?parent:world,true);go.transform.position=position;
            var mesh=go.AddComponent<TextMesh>();mesh.text=text;mesh.fontSize=64;mesh.characterSize=size;mesh.color=color;
            mesh.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            go.GetComponent<MeshRenderer>().sharedMaterial=mesh.font.material;return mesh;
        }
        static Transform Human(string name,Vector3 pos,out Transform left,out Transform right)
        {
            var root=new GameObject(name).transform;root.SetParent(world);root.position=pos;
            LocalCube("Workcoat",new Vector3(0,1.15f,0),new Vector3(.65f,.75f,.35f),green,root);
            var head=GameObject.CreatePrimitive(PrimitiveType.Sphere);head.name="Head placeholder";
            UnityEngine.Object.DestroyImmediate(head.GetComponent<Collider>());
            head.transform.SetParent(root,false);head.transform.localPosition=new Vector3(0,1.72f,0);
            head.transform.localScale=new Vector3(.36f,.42f,.36f);head.GetComponent<Renderer>().sharedMaterial=cream;
            LocalCube("Left leg",new Vector3(-.18f,.43f,0),new Vector3(.23f,.85f,.27f),dark,root);
            LocalCube("Right leg",new Vector3(.18f,.43f,0),new Vector3(.23f,.85f,.27f),dark,root);
            left=new GameObject("Left arm pivot").transform;left.SetParent(root,false);left.localPosition=new Vector3(-.43f,1.45f,0);
            right=new GameObject("Right arm pivot").transform;right.SetParent(root,false);right.localPosition=new Vector3(.43f,1.45f,0);
            LocalCube("Left arm",new Vector3(0,-.3f,0),new Vector3(.22f,.7f,.23f),green,left);
            LocalCube("Right arm",new Vector3(0,-.3f,0),new Vector3(.22f,.7f,.23f),green,right);
            return root;
        }
        static void Crystals(Vector3 pos,float size,Transform parent)
        {
            for(int i=0;i<4;i++)
            {
                var go=new GameObject("Abyssonance placeholder");
                go.transform.SetParent(parent,true);go.transform.position=pos+new Vector3(i*.09f,0,i*.05f);
                go.transform.rotation=Quaternion.Euler(i*17,20+i*37,-22+i*16);go.transform.localScale=Vector3.one*size*(1-i*.13f);
                go.AddComponent<MeshFilter>().sharedMesh=crystalMesh;go.AddComponent<MeshRenderer>().sharedMaterial=crystal;
            }
        }
        static DemoEnemy EnemyPrefab()
        {
            var root=new GameObject("Resonator_Ordinary");
            var visual=Human("Visual",Vector3.zero,out var left,out var right);visual.SetParent(root.transform,false);
            var collider=root.AddComponent<CapsuleCollider>();collider.center=new Vector3(0,.95f,0);collider.height=1.9f;collider.radius=.36f;
            var agent=root.AddComponent<NavMeshAgent>();agent.height=1.9f;agent.radius=.36f;agent.acceleration=16;agent.angularSpeed=540;agent.enabled=false;
            var enemy=root.AddComponent<DemoEnemy>();enemy.visual=visual;enemy.leftArm=left;enemy.rightArm=right;
            Crystals(new Vector3(.35f,1.4f,0),.35f,visual);
            var prefab=PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/OrdinaryResonator.prefab");
            UnityEngine.Object.DestroyImmediate(root);return prefab.GetComponent<DemoEnemy>();
        }
        static void BuildIntro(GameObject system,DemoFlow flow)
        {
            var intro=system.AddComponent<DemoIntro>();flow.intro=intro;
            var director=system.AddComponent<PlayableDirector>();intro.director=director;
            var cabin=new GameObject("Intro animated elevator").transform;cabin.position=new Vector3(80,0,0);
            intro.cabin=cabin;
            LocalCube("Floor",new Vector3(0,-.1f,0),new Vector3(3.5f,.2f,4),steel,cabin);
            LocalCube("Ceiling",new Vector3(0,3.5f,0),new Vector3(3.5f,.2f,4),steel,cabin);
            LocalCube("Left wall",new Vector3(-1.75f,1.7f,0),new Vector3(.15f,3.5f,4),steel,cabin);
            LocalCube("Right wall",new Vector3(1.75f,1.7f,0),new Vector3(.15f,3.5f,4),steel,cabin);
            LocalCube("Front doors",new Vector3(0,1.7f,2),new Vector3(3.5f,3.5f,.15f),cream,cabin);
            LocalCube("Back wall",new Vector3(0,1.7f,-2),new Vector3(3.5f,3.5f,.15f),steel,cabin);
            LocalCube("Handrail",new Vector3(-1.6f,1.05f,0),new Vector3(.08f,.08f,3.4f),amber,cabin);
            intro.floorDisplay=Label("LEVEL -2\nDEST -17",new Vector3(79.9f,2.5f,1.85f),.035f,new Color(.3f,.8f,1),cabin);
            var lamp=new GameObject("Elevator ceiling light");lamp.transform.SetParent(cabin,false);lamp.transform.localPosition=new Vector3(0,3.2f,0);
            intro.ceilingLight=lamp.AddComponent<Light>();intro.ceilingLight.type=LightType.Point;intro.ceilingLight.range=8;intro.ceilingLight.intensity=2.8f;
            var emergency=new GameObject("Panel emergency glow");emergency.transform.SetParent(cabin,false);emergency.transform.localPosition=new Vector3(0,2,1.5f);
            var e=emergency.AddComponent<Light>();e.type=LightType.Point;e.color=new Color(.15f,.5f,1);e.range=4;e.intensity=.45f;
            // Unparented shafts stay in place while the cabin moves.
            var shaft=new GameObject("Intro shaft").transform;
            Cube("Shaft west",new Vector3(77.6f,-10,0),new Vector3(.4f,55,5),concrete,true,shaft);
            Cube("Shaft east",new Vector3(82.4f,-10,0),new Vector3(.4f,55,5),concrete,true,shaft);
            Cube("Shaft far",new Vector3(80,-10,-2.8f),new Vector3(5,55,.4f),concrete,true,shaft);
            for(int i=0;i<8;i++)Cube("Shaft beam",new Vector3(80,10-i*5,-2.4f),new Vector3(5,.18f,.3f),amber,false,shaft);
            intro.intactCable=Cube("Intact cable",new Vector3(80,13.5f,0),new Vector3(.10f,20,.10f),steel,false,shaft);
            intro.brokenCable=Cube("Broken cable",new Vector3(80,8,0),new Vector3(.10f,8,.10f),steel,false,shaft);
            intro.brokenCable.transform.rotation=Quaternion.Euler(0,0,20);
            intro.brokenCable.SetActive(false);
            intro.sparks=new[]{Sparks("Cable sparks",new Vector3(80,4,0),shaft),Sparks("Wall scrape",new Vector3(81.8f,1,0),cabin)};
            string timelinePath=Root+"/Timelines/ElevatorCrash.playable";
            var timeline=AssetDatabase.LoadAssetAtPath<TimelineAsset>(timelinePath);
            if(timeline)AssetDatabase.DeleteAsset(timelinePath);
            timeline=ScriptableObject.CreateInstance<TimelineAsset>();timeline.name="Elevator crash and wake";
            AssetDatabase.CreateAsset(timeline,timelinePath);
            var track=timeline.CreateTrack<DemoIntroTrack>(null,"Elevator / Camera / Light / Crash cues");
            double[] starts={0,8,10,13.5,17,19};double[] ends={8,10,13.5,17,19,26};
            string[] names={"01 Descent -2 to -8","02 Blackout and dual shock","03 Cable rupture / shaft shot","04 Float / tilt / impact","05 Unconscious / set switch","06 Wake inside crashed elevator"};
            for(int i=0;i<starts.Length;i++)
            {
                var clip=track.CreateClip<DemoIntroClip>();clip.start=starts[i];clip.duration=ends[i]-starts[i];clip.displayName=names[i];
                ((DemoIntroClip)clip.asset).sequenceStart=starts[i];
            }
            timeline.durationMode=TimelineAsset.DurationMode.FixedLength;timeline.fixedDuration=26;
            director.playableAsset=timeline;director.playOnAwake=false;director.extrapolationMode=DirectorWrapMode.Hold;
            director.SetGenericBinding(track,intro);EditorUtility.SetDirty(timeline);
        }
        static ParticleSystem Sparks(string name,Vector3 at,Transform parent)
        {
            var go=new GameObject(name);go.transform.SetParent(parent,true);go.transform.position=at;
            var p=go.AddComponent<ParticleSystem>();p.Stop();
            var main=p.main;main.duration=.5f;main.loop=false;main.playOnAwake=false;
            main.startLifetime=.35f;main.startSpeed=5;main.startSize=.06f;main.startColor=new Color(1,.6f,.15f);main.simulationSpace=ParticleSystemSimulationSpace.World;
            var emission=p.emission;emission.rateOverTime=0;emission.SetBursts(new[]{new ParticleSystem.Burst(0,35)});
            var shape=p.shape;shape.shapeType=ParticleSystemShapeType.Sphere;shape.radius=.12f;
            var renderer=go.GetComponent<ParticleSystemRenderer>();renderer.sharedMaterial=amber;
            return p;
        }
    }
}

