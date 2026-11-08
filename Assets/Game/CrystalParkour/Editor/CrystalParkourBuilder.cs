using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using GameActions;
using FirstPerson;
namespace CrystalParkour.Editor
{
    public static class CrystalParkourBuilder
    {
        public const string ScenePath="Assets/Game/Scene/CrystalImpactParkour.unity";
        const string Root="Assets/Game/CrystalParkour/",Kit="Assets/Game/Environment/crystal/";
        static readonly Dictionary<string,Material> materials=new Dictionary<string,Material>();
        static readonly Dictionary<string,GameObject> crystals=new Dictionary<string,GameObject>();
        static Scene scene;
        static T Asset<T>(string p)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(p);
        [MenuItem("Tools/Crystal Parkour/Build New Demo")]
        public static async void Menu(){try{Debug.Log(await Build());}catch(Exception e){Debug.LogException(e);}}
        public static async Task<object> Build()
        {
            if(Application.isPlaying)throw new InvalidOperationException("Stop Play Mode first");
            if(File.Exists(ScenePath))throw new InvalidOperationException("Demo already exists. Open it directly; builder will not overwrite edited scenes.");
            AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);PrepareKit();var profile=PrepareProfile();
            var previous=SceneManager.GetActiveScene();scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            try
            {
                RenderSettings.skybox=null;RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.37f,.46f);RenderSettings.ambientEquatorColor=new Color(.18f,.20f,.26f);RenderSettings.ambientGroundColor=new Color(.08f,.09f,.12f);RenderSettings.fog=true;RenderSettings.fogColor=new Color(.065f,.08f,.12f);RenderSettings.fogMode=FogMode.ExponentialSquared;RenderSettings.fogDensity=.007f;
                Floor("Start laboratory",new Vector3(0,0,4),new Vector3(12,.6f,12));Floor("Entry ledge",new Vector3(0,1.1f,12),new Vector3(6,.5f,4));
                Floor("Wind exit",new Vector3(0,3,35),new Vector3(10,.6f,6));Floor("Growth gallery",new Vector3(0,3,40),new Vector3(9,.6f,6));Floor("Drone boarding",new Vector3(0,4.1f,44.5f),new Vector3(8,.6f,4));Floor("Exit laboratory",new Vector3(2,5.4f,65),new Vector3(10,.6f,8));
                Cube("Left facility wall",new Vector3(-6.4f,3.5f,32),new Vector3(.7f,19,78),materials["wall"]);Cube("Right facility wall",new Vector3(6.4f,3.5f,32),new Vector3(.7f,19,78),materials["wall"]);
                Cube("Start bulkhead",new Vector3(0,4,-2.4f),new Vector3(13,9,.5f),materials["dark"]);Cube("Finish bulkhead",new Vector3(0,5,70),new Vector3(13,15,.5f),materials["dark"]);
                Cube("Deep service trench",new Vector3(0,-13.4f,36),new Vector3(13,.6f,74),materials["dark"]);
                for(int z=0;z<70;z+=6)
                {
                    Cube("Left colour course",new Vector3(-5.99f,1.6f+z*.04f,z),new Vector3(.06f,.7f,5.7f),z<36?materials["yellow"]:materials["lime"]);
                    Cube("Right colour course",new Vector3(5.99f,1.6f+z*.04f,z),new Vector3(.06f,.7f,5.7f),materials["yellow"]);
                    Cube("Overhead beam",new Vector3(0,11,z),new Vector3(12,.25f,.22f),materials["steel"]);Cube("Ceiling light",new Vector3(0,10.8f,z),new Vector3(3,.07f,.18f),materials["light"]);
                    LightAt("Lab strip lighting",new Vector3(0,9,z),Color.white,950,17);
                    for(int side=-1;side<=1;side+=2)Cube("Structural column",new Vector3(side*6,3.8f,z),new Vector3(.25f,16,.32f),materials["dark"]);
                }
                var floorSite=GrowthSite("01 Ground crystal trigger",new Vector3(0,.035f,7.7f),false,Vector3.up);
                var gate=Cube("Growth interlock door",new Vector3(0,2,10.4f),new Vector3(4,4,.35f),materials["dark"]).AddComponent<ParkourGate>();gate.requiredSite=floorSite;
                Label("01 / IMPACT TO GROW",new Vector3(0,2.4f,8.6f),.065f);
                Label("LOOK DOWN + RELEASE LMB",new Vector3(0,1.8f,8.6f),.04f);
                Anchor("02 Warm-up crystal",new Vector3(-5.65f,2,6),Vector3.right,2.2f);
                var wind=Wind(new Vector3(0,1.5f,25),new Vector3(12,21,23),19.2f);
                Anchor("03 Left wall anchor",new Vector3(-5.7f,2.5f,17),Vector3.right,2.2f);
                Anchor("04 Right wall anchor",new Vector3(5.7f,3.4f,23),Vector3.left,2.4f);
                Anchor("05 Left high anchor",new Vector3(-5.7f,4.4f,29),Vector3.right,2.4f);
                Label("02 / WIND SHAFT",new Vector3(0,7.7f,15),.09f);Label("HOLD LMB > ATTACH > RELEASE WITHIN 2 SEC",new Vector3(0,7.0f,15),.036f);
                var wallSite=GrowthSite("06 Wall crystal trigger",new Vector3(5.95f,4.4f,39.5f),true,Vector3.left);
                Label("03 / STRIKE WALL SEAM",new Vector3(1.7f,5.3f,40.7f),.06f);
                var wallGate=Cube("Wall growth interlock",new Vector3(0,5,42.7f),new Vector3(5,3.8f,.3f),materials["dark"]).AddComponent<ParkourGate>();wallGate.requiredSite=wallSite;
                Wind(new Vector3(0,1,54),new Vector3(12,20,23),18.6f);
                var drone=MakeDrone(new Vector3(-.8f,4.6f,47.4f));drone.startPoint=drone.transform.position;drone.endPoint=new Vector3(2.1f,5.7f,60.9f);
                SavePrefab(drone.gameObject,Root+"Prefabs/TraversalDronePlaceholder.prefab");
                Label("04 / RIDE THE DRONE",new Vector3(0,7.0f,46.5f),.075f);Label("LAND ON THE METAL DECK",new Vector3(0,6.35f,46.5f),.045f);
                for(int i=0;i<12;i++)
                {
                    var key=i%3==0?"Crystal_Cluster_Burst":"Crystal_Cluster_Wall";var go=Visual(key,new Vector3(i%2==0?-6:6,-1+(i%4),13+i*4));go.transform.rotation=Quaternion.FromToRotation(Vector3.up,i%2==0?Vector3.right:Vector3.left);go.transform.localScale=Vector3.one*(.8f+(i%3)*.25f);
                }
                // Use the authored modular rails on safe pads; the crystals themselves are exported from Blender.
                foreach(var z in new[]{2f,35f,44.5f,65f}){ActualRail(new Vector3(-3.9f,z<10?0:z<40?3:z<50?4.1f:5.4f,z));ActualRail(new Vector3(3.9f,z<10?0:z<40?3:z<50?4.1f:5.4f,z));}
                Checkpoint("Start",new Vector3(0,.1f,1),new Vector3(8,3,2));Checkpoint("Wind exit",new Vector3(0,3.1f,34),new Vector3(8,3,2));Checkpoint("Drone boarding",new Vector3(0,4.2f,44.5f),new Vector3(6,3,2));Checkpoint("Complete",new Vector3(2,5.5f,67.3f),new Vector3(8,3,2),true);
                Label("05 / ESCAPE COMPLETE",new Vector3(2,8,69),.085f);
                var player=(GameObject)PrefabUtility.InstantiatePrefab(Asset<GameObject>("Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab"),scene);player.name="Crystal Parkour Player";player.transform.SetPositionAndRotation(new Vector3(0,.1f,.7f),Quaternion.identity);
                foreach(var hud in player.GetComponentsInChildren<GauntletTrainingHUD>(true))UnityEngine.Object.DestroyImmediate(hud);
                player.GetComponent<GauntletCombat>().profile=profile;var traversal=player.AddComponent<CrystalTraversal>();traversal.motor=player.GetComponent<FirstPersonMotor>();traversal.combat=player.GetComponent<GauntletCombat>();traversal.aim=traversal.motor.viewPivot;
                var gui=player.AddComponent<CrystalParkourHUD>();gui.player=traversal;
                foreach(var c in player.GetComponentsInChildren<Camera>(true)){c.farClipPlane=180;c.GetUniversalAdditionalCameraData().renderPostProcessing=true;}
                foreach(var c in player.GetComponentsInChildren<Unity.Cinemachine.CinemachineCamera>(true)){var lens=c.Lens;lens.FarClipPlane=180;c.Lens=lens;}
                var volumeGo=new GameObject("Traversal post processing",typeof(Volume));SceneManager.MoveGameObjectToScene(volumeGo,scene);var volume=volumeGo.GetComponent<Volume>();volume.isGlobal=true;
                var volumeProfile=ScriptableObject.CreateInstance<VolumeProfile>();volumeProfile.Add<Tonemapping>(true).mode.Override(TonemappingMode.ACES);var bloom=volumeProfile.Add<Bloom>(true);bloom.intensity.Override(.3f);bloom.threshold.Override(1.1f);AssetDatabase.CreateAsset(volumeProfile,Root+"Data/ParkourVolume.asset");volume.sharedProfile=volumeProfile;
                // Bake the surrounding geometry into a custom reflection, with no unrelated additive scene content.
                var probeGo=new GameObject("Crystal environment reflection",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(probeGo,scene);probeGo.transform.position=new Vector3(0,4,24);var probe=probeGo.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.size=new Vector3(30,40,120);probe.intensity=.75f;probe.customBakedTexture=CaptureReflection();probe.enabled=true;
                SavePrefab(player,Root+"Prefabs/CrystalParkourPlayer.prefab");
                EditorSceneManager.SaveScene(scene,ScenePath);AssetDatabase.SaveAssets();await Task.Delay(250);
                return new{scene=ScenePath,crystalModels=crystals.Count,anchors=scene.GetRootGameObjects().Sum(g=>g.GetComponentsInChildren<CrystalSurface>(true).Length),growthSites=2,windVolumes=2,drone=true,player=Root+"Prefabs/CrystalParkourPlayer.prefab"};
            }
            finally{SceneManager.SetActiveScene(previous);EditorSceneManager.CloseScene(scene,true);}
        }
        static void PrepareKit()
        {
            materials.Clear();crystals.Clear();
            Material Lit(string key,Color colour,float metal=0,float smooth=.4f)
            {string path=Root+"Data/"+key+".mat";var m=Asset<Material>(path);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit")){name=key};m.SetColor("_BaseColor",colour);m.SetFloat("_Metallic",metal);m.SetFloat("_Smoothness",smooth);AssetDatabase.CreateAsset(m,path);}materials[key]=m;return m;}
            materials["wall"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Paint_RoughWhite.mat");materials["dark"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Dark_Panel.mat");materials["floor"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Floor_Resin.mat");materials["steel"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Rail_BrushedSteel.mat");materials["yellow"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Paint_Yellow.mat");materials["lime"]=Asset<Material>("Assets/Game/Environment/ModernLab/Materials/Lab_Paint_Lime.mat");
            var glow=Lit("light",new Color(.5f,.8f,.95f));glow.EnableKeyword("_EMISSION");glow.SetColor("_EmissionColor",new Color(1.5f,2,2.4f));var marker=Lit("growth",new Color(.065f,.12f,.16f),.3f,.6f);marker.EnableKeyword("_EMISSION");marker.SetColor("_EmissionColor",new Color(.08f,.6f,.8f));Lit("drone",new Color(.74f,.76f,.77f),.12f,.55f);
            var shader=Asset<Shader>(Kit+"Shaders/CrystalFacet.shader");if(!shader||ShaderUtil.ShaderHasError(shader))throw new Exception("Crystal facet shader failed to compile");
            var facets=new Material[3];var colors=new[]{new Color(.29f,.20f,.39f),new Color(.53f,.43f,.64f),new Color(.12f,.085f,.19f)};string[] names={"SmokyViolet","PaleFacets","DeepFacets"};
            for(int i=0;i<3;i++){string path=Kit+"Materials/Crystal_"+names[i]+".mat";facets[i]=Asset<Material>(path);if(!facets[i]){facets[i]=new Material(shader){name=names[i]};facets[i].SetColor("_BaseColor",colors[i]);facets[i].SetColor("_FacetLight",new Color(.62f,.52f,.76f));AssetDatabase.CreateAsset(facets[i],path);}}
            foreach(string path in Directory.GetFiles(Kit+"Models","*.fbx"))
            {
                var importer=(ModelImporter)AssetImporter.GetAtPath(path);importer.importAnimation=false;importer.importNormals=ModelImporterNormals.Import;importer.importTangents=ModelImporterTangents.CalculateMikk;importer.isReadable=false;importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.SaveAndReimport();
                var model=Asset<GameObject>(path);var copy=UnityEngine.Object.Instantiate(model);copy.name=Path.GetFileNameWithoutExtension(path);
                foreach(var renderer in copy.GetComponentsInChildren<Renderer>())
                {int count=renderer is SkinnedMeshRenderer sk?sk.sharedMesh.subMeshCount:renderer.GetComponent<MeshFilter>().sharedMesh.subMeshCount;renderer.sharedMaterials=Enumerable.Range(0,count).Select(i=>facets[Mathf.Min(i,2)]).ToArray();}
                string prefabPath=Kit+"Prefabs/"+copy.name+".prefab";crystals[copy.name]=PrefabUtility.SaveAsPrefabAsset(copy,prefabPath);UnityEngine.Object.DestroyImmediate(copy);
            }
        }
        static ActionSet PrepareProfile()
        {
            string path=Root+"Data/ParkourGauntletActions.asset";var existing=Asset<ActionSet>(path);if(existing)return existing;
            var p=UnityEngine.Object.Instantiate(Asset<ActionSet>("Assets/Game/Shared/Actions/Resources/GauntletActions.asset"));p.name="ParkourGauntletActions";
            foreach(var a in p.actions){a.acquisitionRange=0;a.action.displacement.enabled=false;if(!a.isAttack)continue;foreach(var h in a.action.hits){h.position.z=1.35f;h.size.z=Mathf.Max(h.size.z,1.05f);}}
            AssetDatabase.CreateAsset(p,path);return p;
        }
        static GameObject Cube(string name,Vector3 position,Vector3 scale,Material material,PrimitiveType type=PrimitiveType.Cube)
        {var go=GameObject.CreatePrimitive(type);SceneManager.MoveGameObjectToScene(go,scene);go.name=name;go.transform.position=position;go.transform.localScale=scale;go.GetComponent<Renderer>().sharedMaterial=material;return go;}
        static GameObject Visual(string key,Vector3 position)
        {var go=(GameObject)PrefabUtility.InstantiatePrefab(crystals[key],scene);go.transform.position=position;return go;}
        static void Floor(string name,Vector3 top,Vector3 size)
        {
            Cube(name,top-Vector3.up*size.y*.5f,size,materials["floor"]);
            var tile=Asset<GameObject>("Assets/Game/Environment/ModernLab/Prefabs/Lab_Floor_00.prefab");
            if(tile&&name=="Start laboratory")
            {var go=(GameObject)PrefabUtility.InstantiatePrefab(tile,scene);go.name="Authored modular floor";var bounds=BoundsOf(go);go.transform.localScale=new Vector3(size.x/Mathf.Max(.01f,bounds.size.x),1,size.z/Mathf.Max(.01f,bounds.size.z));bounds=BoundsOf(go);go.transform.position+=top+Vector3.up*.015f-new Vector3(bounds.center.x,bounds.max.y,bounds.center.z);}
            Cube(name+" landing edge",top+new Vector3(0,.015f,-size.z*.5f+.13f),new Vector3(size.x,.025f,.2f),materials["yellow"]);
        }
        static Bounds BoundsOf(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();var b=renderers[0].bounds;foreach(var r in renderers)b.Encapsulate(r.bounds);return b;}
        static void ActualRail(Vector3 position)
        {var prefab=Asset<GameObject>("Assets/Game/Environment/ModernLab/Prefabs/Lab_Rail_01.prefab");if(!prefab)return;var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);go.name="Authored lab safety rail";go.transform.rotation=Quaternion.Euler(0,90,0);var b=BoundsOf(go);go.transform.position+=position-new Vector3(b.center.x,b.min.y,b.center.z);}
        static void LightAt(string name,Vector3 position,Color colour,float intensity,float range)
        {var go=new GameObject(name,typeof(Light));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;var l=go.GetComponent<Light>();l.type=LightType.Point;l.color=colour;l.intensity=intensity*.015f;l.range=range;l.shadows=LightShadows.None;}
        static void Label(string text,Vector3 position,float size)
        {var go=new GameObject(text,typeof(TextMesh));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=position;go.transform.rotation=Quaternion.identity;var t=go.GetComponent<TextMesh>();t.text=text;t.anchor=TextAnchor.MiddleCenter;t.fontSize=64;t.characterSize=size*.55f;t.color=new Color(.7f,.85f,1);t.font=Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");go.GetComponent<MeshRenderer>().sharedMaterial=t.font.material;}
        static CrystalSurface Anchor(string name,Vector3 point,Vector3 normal,float scale=1)
        {
            var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);root.transform.SetPositionAndRotation(point,Quaternion.FromToRotation(Vector3.up,normal));
            var visual=Visual("Crystal_Cluster_Wall",point);visual.transform.SetParent(root.transform,false);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;visual.transform.localScale=Vector3.one*scale*.55f;
            var contact=root.AddComponent<BoxCollider>();contact.center=new Vector3(0,.48f,0);contact.size=new Vector3(2.4f,.28f,2.5f);var surface=root.AddComponent<CrystalSurface>();surface.contact=contact;surface.displayName=name;
            LightAt("Crystal contact light",point+normal, new Color(.65f,.4f,1),55,4);
            return surface;
        }
        static GameObject Platform(string name,Vector3 position)
        {
            var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=position;var visual=Visual("Crystal_Cluster_Platform",position);visual.transform.SetParent(root.transform,false);visual.transform.localPosition=Vector3.zero;visual.transform.localRotation=Quaternion.identity;
            var box=root.AddComponent<BoxCollider>();box.center=new Vector3(0,.60f,0);box.size=new Vector3(2.8f,.3f,1.8f);var surface=root.AddComponent<CrystalSurface>();surface.contact=box;surface.normalLaunchSpeed=6;surface.upwardLaunchSpeed=5;return root;
        }
        static CrystalGrowthSite GrowthSite(string name,Vector3 point,bool wall,Vector3 normal)
        {
            var root=new GameObject(name);SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=point;
            var marker=Cube("Fracture seam",point,new Vector3(2.6f,.07f,wall?1.8f:2.6f),materials["growth"]);marker.transform.rotation=Quaternion.FromToRotation(Vector3.up,normal);marker.transform.SetParent(root.transform,true);
            // Collider lives on the site root so the actual gauntlet damage query reaches IActionTarget.
            UnityEngine.Object.DestroyImmediate(marker.GetComponent<Collider>());var box=root.AddComponent<BoxCollider>();box.size=wall?new Vector3(.08f,1.8f,2.6f):new Vector3(2.6f,.08f,2.6f);
            for(int i=-1;i<=1;i++){var crack=Cube("Fracture indicator",point+normal*.045f+new Vector3(0,wall?i*.3f:0,wall?0:i*.5f),wall?new Vector3(.04f,.035f,1.4f):new Vector3(1.4f,.025f,.035f),materials["light"]);crack.transform.SetParent(root.transform,true);UnityEngine.Object.DestroyImmediate(crack.GetComponent<Collider>());}
            var grown=Platform("Emerging crystal platform",point+(wall?normal*1.25f+Vector3.down*1.2f:Vector3.zero));grown.transform.SetParent(root.transform,true);
            var site=root.AddComponent<CrystalGrowthSite>();site.wallGrowth=wall;site.strikeCollider=box;site.growthRoot=grown;site.growthDirection=normal;site.groundLift=.8f;grown.SetActive(false);SavePrefab(root,Root+"Prefabs/"+(wall?"CrystalGrowth_Wall":"CrystalGrowth_Ground")+".prefab");return site;
        }
        static WindVolume Wind(Vector3 position,Vector3 size,float acceleration)
        {
            var root=new GameObject("Updraft wind tunnel",typeof(BoxCollider),typeof(WindVolume));SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=position;root.GetComponent<BoxCollider>().size=size;root.GetComponent<BoxCollider>().isTrigger=true;var wind=root.GetComponent<WindVolume>();wind.acceleration=acceleration;
            var stream=new GameObject("Visible rising air",typeof(ParticleSystem));stream.transform.SetParent(root.transform,false);var ps=stream.GetComponent<ParticleSystem>();var main=ps.main;main.startLifetime=4;main.startSpeed=3;main.startSize=.025f;main.startColor=new Color(.4f,.8f,1,.35f);main.maxParticles=260;main.simulationSpace=ParticleSystemSimulationSpace.World;var emission=ps.emission;emission.rateOverTime=35;var shape=ps.shape;shape.shapeType=ParticleSystemShapeType.Box;shape.scale=new Vector3(size.x,.1f,size.z);shape.position=new Vector3(0,-size.y*.45f,0);var velocity=ps.velocityOverLifetime;velocity.enabled=true;velocity.space=ParticleSystemSimulationSpace.World;velocity.y=3.5f;ps.GetComponent<ParticleSystemRenderer>().sharedMaterial=materials["light"];return wind;
        }
        static TraversalDrone MakeDrone(Vector3 position)
        {
            var finished=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab");
            if(finished){var instance=(GameObject)PrefabUtility.InstantiatePrefab(finished,scene);instance.transform.position=position;var ready=instance.GetComponent<TraversalDrone>();ready.startPoint=position;return ready;}
            var root=new GameObject("Disaster response drone / placeholder");SceneManager.MoveGameObjectToScene(root,scene);root.transform.position=position;
            GameObject Part(string name,Vector3 local,Vector3 scale,Material mat,PrimitiveType type=PrimitiveType.Cube,bool collide=false)
            {var go=Cube(name,position+local,scale,mat,type);go.transform.SetParent(root.transform,true);if(!collide)UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());return go;}
            Part("Load-bearing chassis",Vector3.zero,new Vector3(2.8f,.3f,2.4f),materials["dark"]);
            var deck=Part("Metal standing deck",new Vector3(0,.22f,0),new Vector3(2.8f,.18f,2.4f),materials["drone"],collide:true).GetComponent<BoxCollider>();
            Part("Energy vessel",new Vector3(0,.62f,0),new Vector3(.55f,.35f,.55f),materials["light"],PrimitiveType.Cylinder);
            Part("Vessel protective frame",new Vector3(0,1.05f,0),new Vector3(.9f,.1f,.9f),materials["steel"]);
            var rotors=new Transform[2];int index=0;
            foreach(int side in new[]{-1,1})
            {Part("Ducted thruster",new Vector3(side*1.85f,0,0),new Vector3(1.1f,.22f,1.1f),materials["dark"],PrimitiveType.Cylinder);var hub=Part("Rotor",new Vector3(side*1.85f,.25f,0),new Vector3(.85f,.05f,.12f),materials["steel"]);rotors[index++]=hub.transform;Part("Safety foot pad",new Vector3(side*.9f,.33f,0),new Vector3(.65f,.04f,1.8f),materials["yellow"]);}
            Part("Forward impact plate",new Vector3(0,.05f,1.3f),new Vector3(1,.6f,.2f),materials["steel"]);
            var drone=root.AddComponent<TraversalDrone>();drone.deck=deck;drone.rotorLeft=rotors[0];drone.rotorRight=rotors[1];return drone;
        }
        static void Checkpoint(string label,Vector3 feet,Vector3 size,bool finish=false)
        {var go=new GameObject(label+" checkpoint",typeof(BoxCollider),typeof(ParkourCheckpoint));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=feet+Vector3.up;go.GetComponent<BoxCollider>().isTrigger=true;go.GetComponent<BoxCollider>().size=size;var marker=go.GetComponent<ParkourCheckpoint>();marker.label=label;marker.finish=finish;var spawn=new GameObject("Respawn feet");spawn.transform.SetParent(go.transform);spawn.transform.position=feet;marker.respawn=spawn.transform;}
        static void SavePrefab(GameObject go,string path){if(!File.Exists(path))PrefabUtility.SaveAsPrefabAsset(go,path);}
        static Cubemap CaptureReflection()
        {
            ulong old=EditorSceneManager.GetSceneCullingMask(scene),mask=EditorSceneManager.CalculateAvailableSceneCullingMask();EditorSceneManager.SetSceneCullingMask(scene,mask);var go=new GameObject("Temporary reflection camera",typeof(Camera));SceneManager.MoveGameObjectToScene(go,scene);go.transform.position=new Vector3(0,4,24);var camera=go.GetComponent<Camera>();camera.enabled=false;camera.overrideSceneCullingMask=mask;camera.nearClipPlane=.1f;camera.farClipPlane=130;camera.allowHDR=true;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.12f,.15f,.2f);
            var rt=new RenderTexture(256,256,24,RenderTextureFormat.ARGBHalf){dimension=TextureDimension.Cube};rt.Create();var cube=new Cubemap(256,TextureFormat.RGBAHalf,true);var image=new Texture2D(256,256,TextureFormat.RGBAHalf,false,true);var previous=RenderTexture.active;
            try{camera.RenderToCubemap(rt);for(int face=0;face<6;face++){Graphics.SetRenderTarget(rt,0,(CubemapFace)face);image.ReadPixels(new Rect(0,0,256,256),0,0);image.Apply();cube.SetPixels(image.GetPixels(),(CubemapFace)face);}cube.Apply(true,false);AssetDatabase.CreateAsset(cube,Root+"Data/CrystalParkourRoomReflection.cubemap");return cube;}
            finally{RenderTexture.active=previous;rt.Release();UnityEngine.Object.DestroyImmediate(rt);UnityEngine.Object.DestroyImmediate(image);UnityEngine.Object.DestroyImmediate(go);EditorSceneManager.SetSceneCullingMask(scene,old);}
        }
    }
}
