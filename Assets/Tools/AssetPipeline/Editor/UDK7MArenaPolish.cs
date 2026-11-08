using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using CrystalParkour;
using UDK7M;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static class UDK7MArenaPolish
    {
        const string Stage="AssetStaging/UDK7M/Boss/";static bool busy;
        static UDK7MArenaPolish(){EditorApplication.update+=Poll;}
        static void Poll(){if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||Application.isPlaying||!File.Exists(Stage+"Polish.request"))return;File.Delete(Stage+"Polish.request");Run();}
        static void Run()
        {
            busy=true;var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(UDK7MBossBuilder.ScenePath);bool opened=!scene.IsValid();
            try
            {
                if(opened)scene=EditorSceneManager.OpenScene(UDK7MBossBuilder.ScenePath,OpenSceneMode.Additive);SceneManager.SetActiveScene(scene);var arena=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).First();
                foreach(var crystal in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<CrystalSurface>(true)).Where(s=>s.name=="Lower level escape crystal"))crystal.launchNormal=crystal.transform.InverseTransformDirection(Vector3.up);
                foreach(var bar in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Where(t=>t.name=="Open steel grating bar"&&Mathf.Abs(Mathf.Abs(t.position.x)-10)<.01f).ToArray())UnityEngine.Object.DestroyImmediate(bar.gameObject);
                var entry=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArenaEntry>()).First();entry.GetComponent<BoxCollider>().size=new Vector3(44,4,2);
                var fill=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Facility fill");if(fill){fill.transform.rotation=Quaternion.Euler(45,-30,0);fill.GetComponent<Light>().intensity=1.1f;}
                var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.35f,.38f,.4f));RenderSettings.ambientProbe=sh;
                if(!scene.GetRootGameObjects().Any(g=>g.name=="Industrial reflected fill"))AssetReviewUtility.Light(scene,"Industrial reflected fill",new Vector3(-15,10,4),new Vector3(0,3,0),LightType.Directional,.45f,new Color(.65f,.78f,1));
                var world=scene.GetRootGameObjects().First(g=>g.name=="Facility Geometry").transform.Find("Main arena modules");
                if(!world.Find("Perimeter service props"))
                {
                    var props=new GameObject("Perimeter service props");props.transform.SetParent(world,false);
                    for(int i=0;i<8;i++)
                    {
                        float a=(i*45+22.5f)*Mathf.Deg2Rad;string name=i%2==0?"SV_Crate_1m":"SV_WoodCrate_Old";var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/SovietFacility/Prefabs/Props/"+name+".prefab");var p=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);p.transform.SetParent(props.transform,false);p.transform.position=new Vector3(Mathf.Sin(a)*22,0,Mathf.Cos(a)*22);p.transform.localRotation=Quaternion.Euler(0,-i*45,0);
                    }
                }
                var lights=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Light>(true)).ToArray();foreach(var l in lights)l.cullingMask=~0;
                var renderers=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Renderer>(true)).ToArray();var layers=renderers.ToDictionary(r=>r,r=>r.gameObject.layer);
                Cubemap cube;
                try{foreach(var r in renderers)r.gameObject.layer=30;cube=AssetReviewUtility.RoomCube(scene,new Vector3(0,3,0),"Assets/Game/Enemies/UDK7M/AI/Materials/ArenaReflection.cubemap");}
                finally{foreach(var pair in layers)if(pair.Key)pair.Key.gameObject.layer=pair.Value;}
                var probeRoot=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="Arena reflections");if(!probeRoot){probeRoot=new GameObject("Arena reflections",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(probeRoot,scene);}var probe=probeRoot.GetComponent<ReflectionProbe>();probe.mode=ReflectionProbeMode.Custom;probe.customBakedTexture=cube;probe.boxProjection=true;probe.size=new Vector3(50,28,50);probe.center=Vector3.up*10;probe.intensity=.7f;
                UDK7MArenaInspector.Rebake(arena);
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.gameObject.name=="Disabled overview camera");camera.transform.SetPositionAndRotation(new Vector3(0,6,-21),Quaternion.LookRotation(new Vector3(0,4,1)-new Vector3(0,6,-21)));camera.fieldOfView=70;camera.cullingMask=~0;AssetReviewUtility.Capture(scene,camera,Stage+"Arena_Interior.png");camera.enabled=false;
                EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();File.WriteAllText(Stage+"polish_status.json",JsonConvert.SerializeObject(new{passed=true,reflections=true,lowerEscapeAnchors=true},Formatting.Indented));
            }
            catch(Exception e){File.WriteAllText(Stage+"polish_status.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);if(opened&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);busy=false;}
        }
    }
}
