using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Newtonsoft.Json.Linq;
using FirstPerson;

namespace CrystalParkour.Editor
{
    // A one-time assembly recipe. Saved scenes and module prefabs remain independently editable.
    public static class CrystalTraversalLabBuilder
    {
        public const string ScenePath="Assets/Game/Scene/CrystalTraversalLab_V2.unity";
        const string Root="Assets/Game/CrystalParkour/", Kit="Assets/Game/Environment/crystal/";
        const string Modules=Root+"Prefabs/LevelModules/", Data=Root+"Data/V2/";
        static Scene scene;
        static readonly Dictionary<string,Material> mats=new Dictionary<string,Material>();
        static readonly Dictionary<int,GameObject> kit=new Dictionary<int,GameObject>();
        static T Load<T>(string path) where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        [MenuItem("Tools/Crystal Parkour/Build V2 Traversal Lab (new scene only)")]
        public static void Menu(){try{Debug.Log(Build());}catch(Exception e){Debug.LogException(e);}}
        public static object Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first.");
            if(File.Exists(ScenePath))throw new InvalidOperationException("V2 already exists; open and edit it. This recipe never overwrites a designed scene.");
            Directory.CreateDirectory(Data);Directory.CreateDirectory(Modules);AssetDatabase.Refresh();PrepareKit();
            var previous=SceneManager.GetActiveScene();scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=new Color(.33f,.39f,.5f);RenderSettings.ambientEquatorColor=new Color(.2f,.23f,.29f);RenderSettings.ambientGroundColor=new Color(.12f,.13f,.17f);
                RenderSettings.fog=true;RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.004f;RenderSettings.fogColor=new Color(.1f,.13f,.18f);
                var a=Silo("01 Pulse Silo A",Vector3.zero,8,-420,60,0);SaveModule(a,"PulseSilo_A");
                var b=Silo("02 Pulse Silo B",new Vector3(18,12.8f,0),6,216,28.8f,2);SaveModule(b,"PulseSilo_B");
                var join=Group("Silo connecting bridge",null,new Vector3(9,12.8f,0));Floor(join.transform,"Bridge",Vector3.zero,new Vector3(7,.4f,3.4f));Rail(join.transform,new Vector3(0,0,1.65f),6.8f);Rail(join.transform,new Vector3(0,0,-1.65f),6.8f);
                var duct=FanDuct();SaveModule(duct,"FanDuct_ThreeDirections");
                var bay=CrystalBay();SaveModule(bay,"CrystalBay_NoWind");
                var drones=DroneSection();SaveModule(drones,"DescendingDrone_ThreeStream");
                var player=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root+"Prefabs/CrystalParkourPlayer.prefab"),scene);
                player.name="Traversal Player V2";player.transform.position=new Vector3(0,.12f,-3.7f);player.transform.rotation=Quaternion.Euler(0,90,0);
                var traversal=player.GetComponent<CrystalTraversal>();traversal.fallResetHeight=-6;
                player.GetComponent<FirstPersonMotor>().airSpeed=5.2f;player.GetComponent<FirstPersonMotor>().airAcceleration=18;
                foreach(var c in player.GetComponentsInChildren<Camera>(true)){c.farClipPlane=200;c.GetUniversalAdditionalCameraData().renderPostProcessing=true;}
                foreach(var c in player.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true)){var lens=c.Lens;lens.FarClipPlane=200;c.Lens=lens;}
                var post=Group("Traversal colour grading");var volume=post.AddComponent<Volume>();volume.isGlobal=true;volume.sharedProfile=Load<VolumeProfile>(Root+"Data/ParkourVolume.asset");
                var sun=Group("Facility skylight");sun.transform.rotation=Quaternion.Euler(48,-30,0);var light=sun.AddComponent<Light>();light.type=LightType.Directional;light.color=new Color(.75f,.85f,1);light.intensity=.8f;light.shadows=LightShadows.Soft;
                RenderSettings.customReflectionTexture=Load<Cubemap>(Root+"Data/CrystalParkourRoomReflection.cubemap");RenderSettings.defaultReflectionMode=DefaultReflectionMode.Custom;RenderSettings.reflectionIntensity=.6f;
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();
                return new{scene=ScenePath,crystalVariants=kit.Count,silos=2,horizontalFans=3,descendingDrones=3,moduleFolder=Modules};
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        static void PrepareKit()
        {
            mats.Clear();kit.Clear();string lab="Assets/Game/Environment/ModernLab/Materials/";
            foreach(var p in new[]{("white","Lab_White_Matte"),("dark","Lab_Dark_Panel"),("floor","Lab_Floor_Resin"),("metal","Lab_Rail_BrushedSteel"),("yellow","Lab_Paint_Yellow"),("lime","Lab_Paint_Lime"),("red","Lab_Paint_Red"),("ribbed","Lab_Wall_Ribbed")})
                mats[p.Item1]=Load<Material>(lab+p.Item2+".mat")??throw new Exception("Missing material "+p.Item2);
            mats["light"]=Load<Material>(Root+"Data/light.mat");mats["marker"]=Load<Material>(Root+"Data/growth.mat");mats["drone"]=Load<Material>(Root+"Data/drone.mat");
            mats["text"]=Load<Material>(Data+"DepthTestedLabel.mat");if(!mats["text"]){mats["text"]=new Material(Shader.Find("NullBoundary/World Label"));AssetDatabase.CreateAsset(mats["text"],Data+"DepthTestedLabel.mat");}mats["text"].shader=Shader.Find("NullBoundary/World Label");EditorUtility.SetDirty(mats["text"]);
            var manifest=JObject.Parse(File.ReadAllText("ArtSource/Crystal/V2/environment_manifest.json"));
            foreach(var entry in manifest["environment_variants"])
            {
                string name=(string)entry["name"],path=Kit+"Models/V2/"+(string)entry["fbx"];
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);if(!importer)throw new Exception("Import FBX first: "+path);
                importer.importAnimation=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;importer.materialLocation=ModelImporterMaterialLocation.InPrefab;importer.SaveAndReimport();
                var model=UnityEngine.Object.Instantiate(Load<GameObject>(path));var copy=new GameObject(name);model.transform.SetParent(copy.transform,false);model.name="Authored FBX axes (keep local rotation)";
                Material Facet(string n)=>Load<Material>(Kit+"Materials/Crystal_"+(n.ToLowerInvariant().Contains("pale")?"PaleFacets":n.ToLowerInvariant().Contains("deep")?"DeepFacets":"SmokyViolet")+".mat");
                foreach(var r in copy.GetComponentsInChildren<Renderer>())r.sharedMaterials=r.sharedMaterials.Select(m=>Facet(m?m.name:"deep")).ToArray();
                int number=int.Parse(name.Substring(11,2));string prefab=Kit+"Prefabs/V2/"+name+".prefab";
                kit[number]=PrefabUtility.SaveAsPrefabAsset(copy,prefab);UnityEngine.Object.DestroyImmediate(copy);
            }
        }
        static GameObject Group(string name,Transform parent=null,Vector3? position=null)
        {var go=new GameObject(name);SceneManager.MoveGameObjectToScene(go,scene);if(parent)go.transform.SetParent(parent,false);go.transform.localPosition=position??Vector3.zero;return go;}
        static GameObject Box(Transform parent,string name,Vector3 local,Vector3 size,string mat="dark",bool collide=true,PrimitiveType shape=PrimitiveType.Cube)
        {var go=GameObject.CreatePrimitive(shape);SceneManager.MoveGameObjectToScene(go,scene);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localScale=size;go.GetComponent<Renderer>().sharedMaterial=mats[mat];if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
        static void Floor(Transform parent,string name,Vector3 top,Vector3 size)
        {Box(parent,name,top-Vector3.up*size.y*.5f,size,"floor");Box(parent,name+" yellow edge",top+new Vector3(0,.015f,-size.z*.5f+.1f),new Vector3(size.x,.025f,.15f),"yellow",false);}
        static void Rail(Transform parent,Vector3 bottom,float length)
        {Box(parent,"Hand rail",bottom+Vector3.up*1.05f,new Vector3(length,.065f,.065f),"metal");foreach(float t in new[]{-.5f,0,.5f})Box(parent,"Rail post",bottom+new Vector3(t*length,.52f,0),new Vector3(.065f,1.04f,.065f),"metal");}
        static TextMesh Label(Transform parent,string message,Vector3 local,float size=.1f,Quaternion? rotation=null)
        {var go=Group(message,parent,local);go.transform.localRotation=rotation??Quaternion.identity;var text=go.AddComponent<TextMesh>();text.text=message;text.fontSize=64;text.characterSize=size;text.anchor=TextAnchor.MiddleCenter;text.color=new Color(.7f,.9f,1);text.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<Renderer>().sharedMaterial=mats["text"];go.AddComponent<WorldSpaceLabel>();return text;}
        static void Lamp(Transform parent,Vector3 local,Color colour,float intensity=10,float range=12)
        {var go=Group("Local fixture light",parent,local);var l=go.AddComponent<Light>();l.type=LightType.Point;l.color=colour;l.intensity=intensity;l.range=range;l.shadows=LightShadows.None;}
        static GameObject Visual(int number,Transform parent,Vector3 local,Quaternion? rotation=null,float scale=1)
        {var go=(GameObject)PrefabUtility.InstantiatePrefab(kit[number],scene);go.transform.SetParent(parent,false);go.transform.localPosition=local;go.transform.localRotation=rotation??Quaternion.identity;go.transform.localScale=Vector3.one*scale;return go;}
        static void SaveModule(GameObject go,string name)
        {if(!File.Exists(Modules+name+".prefab"))PrefabUtility.SaveAsPrefabAsset(go,Modules+name+".prefab");}
        static void Checkpoint(Transform parent,string name,Vector3 feet,Vector3 size,bool finish=false,float yaw=0)
        {var root=Group(name+" checkpoint",parent,feet+Vector3.up);var box=root.AddComponent<BoxCollider>();box.isTrigger=true;box.size=size;var cp=root.AddComponent<ParkourCheckpoint>();cp.label=name;cp.finish=finish;cp.respawn=Group("Respawn feet",root.transform,-Vector3.up).transform;cp.respawn.localRotation=Quaternion.Euler(0,yaw,0);}
        static void Hazard(Transform parent,string name,Vector3 centre,Vector3 size)
        {var go=Group(name,parent,centre);go.AddComponent<BoxCollider>().size=size;go.GetComponent<BoxCollider>().isTrigger=true;go.AddComponent<ParkourResetVolume>();}
        static WindVolume Wind(Transform parent,string name,Vector3 centre,Vector3 size,WindKind kind,Vector3 direction,bool pulse=false,float phase=0)
        {
            var go=Group(name,parent,centre);var box=go.AddComponent<BoxCollider>();box.size=size;box.isTrigger=true;
            var w=go.AddComponent<WindVolume>();w.kind=kind;w.direction=direction;w.directionIsLocal=true;w.intermittent=pulse;w.phaseOffset=phase;w.gravityScale=.42f;w.onSeconds=5;w.offSeconds=2.5f;
            var particles=Group("Air streaks",go.transform).AddComponent<ParticleSystem>();var main=particles.main;main.playOnAwake=true;main.startLifetime=1.5f;main.startSpeed=0;main.startSize=.022f;main.startColor=new Color(.4f,.75f,1,.4f);main.maxParticles=150;main.simulationSpace=ParticleSystemSimulationSpace.Local;
            var emission=particles.emission;emission.rateOverTime=25;var shape=particles.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=size*.9f;
            var velocity=particles.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.Local;var dir=kind==WindKind.GravityRelief?Vector3.up:direction;velocity.x=dir.x*3;velocity.y=dir.y*3;velocity.z=dir.z*3;
            particles.GetComponent<ParticleSystemRenderer>().sharedMaterial=mats["light"];return w;
        }
        static GameObject Silo(string name,Vector3 origin,int steps,float angleStart,float angleStep,float phase)
        {
            var root=Group(name,null,origin);float exit=steps*1.6f,height=exit+3.5f;
            // Independent panels give the inward-facing shell real door openings and metre-scaled UVs.
            int segments=32;var verts=new List<Vector3>();var uv=new List<Vector2>();var tris=new List<int>();
            for(float y=0;y<height;y+=1.6f)for(int i=0;i<segments;i++)
            {
                float mid=(i+.5f)*360/segments;bool right=Mathf.Abs(Mathf.DeltaAngle(mid,0))<22.6f&&y>=exit-1.61f&&y<exit+2.4f;
                bool left=steps==6&&Mathf.Abs(Mathf.DeltaAngle(mid,180))<22.6f&&y<3.2f;if(right||left)continue;
                float a=i*Mathf.PI*2/segments,b=(i+1)*Mathf.PI*2/segments,top=Mathf.Min(height,y+1.6f);int n=verts.Count;
                verts.AddRange(new[]{new Vector3(Mathf.Cos(a)*6,y,Mathf.Sin(a)*6),new Vector3(Mathf.Cos(b)*6,y,Mathf.Sin(b)*6),new Vector3(Mathf.Cos(b)*6,top,Mathf.Sin(b)*6),new Vector3(Mathf.Cos(a)*6,top,Mathf.Sin(a)*6)});
                uv.AddRange(new[]{new Vector2(a*3,y*.5f),new Vector2(b*3,y*.5f),new Vector2(b*3,top*.5f),new Vector2(a*3,top*.5f)});tris.AddRange(new[]{n,n+1,n+2,n,n+2,n+3});
            }
            var mesh=new Mesh{name=name+" inward wall"};mesh.SetVertices(verts);mesh.SetUVs(0,uv);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();AssetDatabase.CreateAsset(mesh,Data+name.Replace(" ","_")+"_Shell.asset");
            var shell=Group("Panelled cylindrical shell",root.transform);shell.AddComponent<MeshFilter>().sharedMesh=mesh;shell.AddComponent<MeshRenderer>().sharedMaterial=mats["white"];shell.AddComponent<MeshCollider>().sharedMesh=mesh;
            Box(root.transform,"Base slab",new Vector3(0,-.3f,0),new Vector3(12,.6f,12),"floor");
            for(int i=0;i<steps;i++)
            {
                float angle=(angleStart+i*angleStep)*Mathf.Deg2Rad;var ledge=Group("Safe ledge "+(i+1),root.transform,new Vector3(Mathf.Cos(angle)*4.45f,(i+1)*1.6f,Mathf.Sin(angle)*4.45f));ledge.transform.localRotation=Quaternion.Euler(0,-angle*Mathf.Rad2Deg,0);
                Floor(ledge.transform,"Waiting platform",Vector3.zero,new Vector3(2.65f,.28f,2.75f));
                var back=Group("Outer handrail",ledge.transform,new Vector3(1.27f,0,0));back.transform.localRotation=Quaternion.Euler(0,90,0);Rail(back.transform,Vector3.zero,2.7f);
                Box(ledge.transform,"Step number plate",new Vector3(1.1f,.35f,0),new Vector3(.1f,.55f,.55f),"yellow",false);
                Lamp(root.transform,ledge.transform.localPosition+Vector3.up*2,new Color(.7f,.83f,1),7,9);
            }
            for(int i=0;i<16;i++){float a=i*Mathf.PI/8;Box(root.transform,"Vertical service pipe",new Vector3(Mathf.Cos(a)*5.9f,height*.5f,Mathf.Sin(a)*5.9f),new Vector3(.09f,height,.09f),i%4==0?"yellow":"metal",false);}
            var wind=Wind(root.transform,"Pulse updraft (remaining gravity 42%)",new Vector3(0,height*.5f,0),new Vector3(11.7f,height,11.7f),WindKind.GravityRelief,Vector3.up,true,phase);wind.shape=WindShape.Cylinder;
            var text=Label(root.transform,"AIR ON / JUMP",new Vector3(0,2.7f,4.9f),.075f);var lamp=Box(root.transform,"Valve status lamp",new Vector3(0,3.4f,4.9f),new Vector3(2,.12f,.1f),"light",false).GetComponent<Renderer>();var signal=root.AddComponent<WindCycleSignal>();signal.wind=wind;signal.lamp=lamp;signal.text=text;
            Label(root.transform,name+"\nGREEN: JUMP / WASD IN AIR",new Vector3(0,4.2f,5),.055f);
            Checkpoint(root.transform,name+" entrance",new Vector3(steps==6?-4.7f:0,.12f,steps==6?0:-3.7f),new Vector3(2,2.5f,2),yaw:steps==6?90:90);
            Checkpoint(root.transform,name+" exit",new Vector3(4.6f,exit+.1f,0),new Vector3(2.5f,2.5f,2.5f),yaw:90);
            return root;
        }
        static GameObject Fan(Transform parent,string name,Vector3 local,Vector3 normal)
        {
            var root=Group(name,parent,local);root.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,normal);
            for(int i=0;i<16;i++){float a=i*360f/16;var part=Box(root.transform,"Fan casing",Quaternion.Euler(0,0,a)*new Vector3(0,2.45f,0),new Vector3(1.02f,.19f,.5f),"metal");part.transform.localRotation=Quaternion.Euler(0,0,a);}
            var rotor=Group("Rotor / four dark panel blades",root.transform);rotor.AddComponent<WindFanRotor>();
            for(int i=0;i<4;i++){var blade=Box(rotor.transform,"Dark panel blade "+(i+1),Quaternion.Euler(0,0,i*90)*new Vector3(0,1.15f,0),new Vector3(.9f,2.1f,.11f),"dark",false);blade.transform.localRotation=Quaternion.Euler(0,0,i*90-13);}
            Box(rotor.transform,"Rotor hub",Vector3.zero,Vector3.one*.6f,"metal",false,PrimitiveType.Sphere);
            var back=Box(root.transform,"Orange circular back panel",new Vector3(0,0,-.3f),new Vector3(4.2f,.04f,4.2f),"yellow",false,PrimitiveType.Cylinder);back.transform.localRotation=Quaternion.Euler(90,0,0);
            for(int i=-2;i<=2;i++)Box(root.transform,"Safety grille",new Vector3(i*.75f,0,.3f),new Vector3(.065f,4,.065f),"metal");
            Hazard(root.transform,"Fan intake hazard",Vector3.zero,new Vector3(4.4f,4.4f,.7f));Lamp(root.transform,new Vector3(0,0,.7f),new Color(1,.45f,.1f),20,9);return root;
        }
        static GameObject FanDuct()
        {
            var root=Group("03 Horizontal fan ducts",null,new Vector3(24,22.4f,0));
            Floor(root.transform,"Entry rest",new Vector3(1,0,0),new Vector3(4,.4f,5));
            Floor(root.transform,"Crosswind bridge",new Vector3(7,0,0),new Vector3(9,.35f,2.8f));
            Floor(root.transform,"Middle rest",new Vector3(12,0,0),new Vector3(2,.4f,5));
            Floor(root.transform,"Tail / headwind crossing",new Vector3(17.5f,0,0),new Vector3(10,.4f,2.8f));
            Floor(root.transform,"Duct exit turn",new Vector3(20,0,3),new Vector3(5,.4f,7));
            Box(root.transform,"Duct back wall",new Vector3(10,3,5.8f),new Vector3(26,8,.35f),"ribbed");
            Box(root.transform,"Duct lower service floor",new Vector3(10,-6,0),new Vector3(27,.5f,15),"dark");
            for(int i=0;i<5;i++){Box(root.transform,"Overhead truss",new Vector3(i*5,6.5f,0),new Vector3(.18f,.25f,12),"metal",false);Lamp(root.transform,new Vector3(i*5,4.8f,1),Color.white,12,12);}
            Fan(root.transform,"Suction fan",new Vector3(7,2.5f,-6),Vector3.forward);var cross=Wind(root.transform,"Crosswind suction",new Vector3(7,2,-1),new Vector3(7,5,9),WindKind.HorizontalAirflow,Vector3.back);cross.driftSpeed=6;cross.responseAcceleration=3.5f;
            Fan(root.transform,"Tailwind fan",new Vector3(12,2.5f,-3.6f),Vector3.right);var tail=Wind(root.transform,"Tailwind",new Vector3(13.7f,2,0),new Vector3(3.3f,5,4),WindKind.HorizontalAirflow,Vector3.right);tail.driftSpeed=3.6f;tail.responseAcceleration=3;
            Fan(root.transform,"Opposing fan",new Vector3(24,2.5f,0),Vector3.left);var head=Wind(root.transform,"Pulsed headwind",new Vector3(18.4f,2,0),new Vector3(5.4f,5,4),WindKind.HorizontalAirflow,Vector3.left,true);head.driftSpeed=6.2f;head.onSeconds=4;head.offSeconds=3;head.responseAcceleration=3;
            Label(root.transform,"SUCTION <  CROSS BEFORE DRIFT",new Vector3(7,2.2f,4.8f),.055f);
            Label(root.transform,"TAILWIND >   < HEADWIND\nWAIT FOR THE VALVE / EXIT LEFT",new Vector3(18,3,4.8f),.055f);
            var signal=root.AddComponent<WindCycleSignal>();signal.wind=head;signal.text=Label(root.transform,"AIR OFF / WAIT",new Vector3(20,1.7f,4.8f),.045f);
            Checkpoint(root.transform,"Fan crossing",new Vector3(1,.12f,0),new Vector3(2,3,3),yaw:90);
            Hazard(root.transform,"Duct fall recovery",new Vector3(11,-3.5f,0),new Vector3(30,1,18));return root;
        }
        static CrystalSurface Anchor(Transform parent,string name,Vector3 local,Vector3 normal,int variant=10)
        {
            var root=Group(name,parent,local);root.transform.localRotation=Quaternion.FromToRotation(Vector3.forward,normal);var model=Visual(variant,root.transform,Vector3.zero);
            // Static contact follows the exported crystal geometry, including the long visible tips.
            var mesh=model.GetComponentInChildren<MeshFilter>();var collider=mesh.gameObject.AddComponent<MeshCollider>();collider.sharedMesh=mesh.sharedMesh;
            var s=root.AddComponent<CrystalSurface>();s.contact=collider;s.displayName=name;s.normalLaunchSpeed=7.2f;s.upwardLaunchSpeed=8.2f;s.useLaunchNormal=true;
            Lamp(root.transform,new Vector3(0,.3f,1.8f),new Color(.6f,.35f,1),7,5);return s;
        }
        static CrystalGrowthSite Growth(Transform parent,string name,Vector3 marker,Vector3 normal,GameObject grown,bool wall)
        {
            var root=Group(name,parent,marker);root.transform.localRotation=Quaternion.FromToRotation(Vector3.up,normal);
            var box=root.AddComponent<BoxCollider>();box.size=new Vector3(1.8f,.1f,1.8f);
            Box(root.transform,"Strike this fracture",Vector3.zero,new Vector3(1.8f,.07f,1.8f),"marker",false);
            for(int i=-1;i<=1;i++)Box(root.transform,"Luminous crack",new Vector3(i*.3f,.055f,0),new Vector3(.035f,.025f,1.1f),"light",false);
            grown.transform.SetParent(root.transform,true);var site=root.AddComponent<CrystalGrowthSite>();site.growthRoot=grown;site.strikeCollider=box;site.wallGrowth=wall;site.growthDirection=grown.transform.parent.InverseTransformDirection(root.transform.parent.TransformDirection(normal));site.duration=.8f;
            return site;
        }
        static GameObject CrystalBay()
        {
            var root=Group("04 Crystal parkour / no wind",null,new Vector3(44,22.4f,9));
            Floor(root.transform,"Arrival safe floor",new Vector3(0,0,1),new Vector3(12,.5f,8));
            Floor(root.transform,"Rebound exit ledge",new Vector3(0,4.1f,21),new Vector3(8,.5f,4));
            Box(root.transform,"Left mineral wall",new Vector3(-7,5.5f,11),new Vector3(.5f,13,28),"white");Box(root.transform,"Right mineral wall",new Vector3(7,5.5f,11),new Vector3(.5f,13,28),"white");
            for(int i=0;i<5;i++){Box(root.transform,"Ceiling beam",new Vector3(0,10,1+i*5),new Vector3(14,.3f,.22f),"metal",false);Lamp(root.transform,new Vector3(0,8,1+i*5),Color.white,16,15);}
            var platform=Group("Grown ground shelf",root.transform,new Vector3(0,0,2));Visual(12,platform.transform,Vector3.zero);var floorBox=platform.AddComponent<BoxCollider>();floorBox.center=new Vector3(0,.18f,0);floorBox.size=new Vector3(2.8f,.3f,1.6f);var floorSurface=platform.AddComponent<CrystalSurface>();floorSurface.contact=floorBox;
            var ground=Growth(root.transform,"Strike floor / raise shutter",new Vector3(0,.04f,2),Vector3.up,platform,false);
            var gate=Box(root.transform,"Crystal interlock shutter",new Vector3(0,2,4.7f),new Vector3(13.5f,4,.25f),"dark").AddComponent<ParkourGate>();gate.requiredSite=ground;gate.openHeight=5;
            // A separate reachable spur lets the wall trigger be punched before the first remote tether.
            Floor(root.transform,"Wall trigger spur",new Vector3(4.8f,0,5.5f),new Vector3(3,.4f,4));
            var remote=Anchor(root.transform,"New anchor / unavailable before wall strike",new Vector3(-6.65f,4,8),Vector3.right);
            Growth(root.transform,"Strike wall / grow opposite side anchor",new Vector3(6.62f,1.4f,5.6f),Vector3.left,remote.gameObject,true);
            var anchor2=Anchor(root.transform,"Anchor 02 / right wall",new Vector3(6.65f,5,12.3f),Vector3.left);
            Anchor(root.transform,"Anchor 03 / left high",new Vector3(-6.65f,6.3f,16.5f),Vector3.right);
            // A forgiving centre shelf splits the diagonal reach; it also provides a stable aiming rest.
            var centre=Group("Central crystal rest",root.transform,new Vector3(0,3.1f,10.5f));Visual(12,centre.transform,Vector3.zero);var centreBox=centre.AddComponent<BoxCollider>();centreBox.center=new Vector3(0,.18f,0);centreBox.size=new Vector3(2.8f,.3f,1.6f);centre.AddComponent<CrystalSurface>().contact=centreBox;
            // Decorative context variants deliberately have no CrystalSurface or blocking contact volume.
            Visual(1,root.transform,new Vector3(-6.7f,0,-1),Quaternion.Euler(0,90,0));Visual(2,root.transform,new Vector3(5.7f,0,0),Quaternion.Euler(0,-90,0));
            Visual(3,root.transform,new Vector3(-4,9.8f,12));Visual(11,root.transform,new Vector3(3,9.8f,19));Visual(8,root.transform,new Vector3(-5,-2,18));Visual(9,root.transform,new Vector3(6.6f,3,18),Quaternion.FromToRotation(Vector3.forward,Vector3.left));Visual(6,root.transform,new Vector3(-3,.02f,-1));Visual(7,root.transform,new Vector3(3,.02f,20));
            Label(root.transform,"CRYSTAL BAY / NO WIND\nSTRIKE FLOOR, THEN WALL SEAM",new Vector3(0,3.5f,3.5f),.065f);
            Label(root.transform,"HOLD LMB: TETHER\nRELEASE WITHIN 2 SEC AT CONTACT",new Vector3(0,7,15),.06f);
            Checkpoint(root.transform,"Crystal bay",new Vector3(0,.12f,-.7f),new Vector3(8,3,2));Checkpoint(root.transform,"Drone approach",new Vector3(0,4.25f,21),new Vector3(7,3,2),yaw:90);
            Hazard(root.transform,"Crystal trench recovery",new Vector3(0,-2,13),new Vector3(14,1,30));return root;
        }
        static GameObject DroneSection()
        {
            var root=Group("05 Descending disaster response stream",null,new Vector3(54,26.5f,30));
            Floor(root.transform,"Boarding walkway",new Vector3(-3,0,0),new Vector3(10,.5f,4));Floor(root.transform,"Opposite exit",new Vector3(12,-1.7f,0),new Vector3(6,.5f,6));
            for(int i=0;i<3;i++)
            {
                var routeRoot=Group("Drone route "+(i+1),root.transform);var start=Group("Top / spawn",routeRoot.transform,new Vector3(5.5f,5.5f,0));var end=Group("Bottom / repair destination",routeRoot.transform,new Vector3(5.5f,-8.5f,0));
                var drone=(GameObject)PrefabUtility.InstantiatePrefab(Load<GameObject>(Root+"Prefabs/TraversalDronePlaceholder.prefab"),scene);drone.transform.SetParent(routeRoot.transform,false);var d=drone.GetComponent<TraversalDrone>();d.route=DroneRoute.DescendingStream;d.startAnchor=start.transform;d.endAnchor=end.transform;d.speed=1.4f;d.phaseOffset=i*4;d.respawnSeconds=2;d.hoverAmplitude=0;drone.transform.position=d.PositionAt(0,out _);
                if(i==0)SaveModule(routeRoot,"DescendingDrone_SingleRoute");
            }
            Label(root.transform,"DISASTER RESPONSE / DOWNBOUND\nBOARD > CROSS > EXIT",new Vector3(5.5f,4.5f,4.5f),.065f);
            Box(root.transform,"Distant service shaft",new Vector3(5.5f,-2,5.5f),new Vector3(14,21,.4f),"ribbed");Lamp(root.transform,new Vector3(5.5f,4,2),Color.white,20,18);
            Checkpoint(root.transform,"Drone boarding",new Vector3(0,.12f,0),new Vector3(3,3,3),yaw:90);Checkpoint(root.transform,"Demo complete",new Vector3(12,-1.55f,0),new Vector3(5,3,5),true);
            Label(root.transform,"DEMO COMPLETE / R TO RETRY",new Vector3(14,1,3),.065f);Hazard(root.transform,"Drone shaft recovery",new Vector3(5,-10,0),new Vector3(28,1,18));return root;
        }
    }
}
