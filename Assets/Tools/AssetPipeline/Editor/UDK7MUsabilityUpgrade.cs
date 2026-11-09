using System;
using System.IO;
using System.Linq;
using Newtonsoft.Json;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UDK7M;
namespace AssetPipeline.Editor
{
    [InitializeOnLoad]public static class UDK7MUsabilityUpgrade
    {
        const string Stage="AssetStaging/UDK7M/Boss/",AudioPath="Assets/Game/Enemies/UDK7M/AI/UDK7M_Audio.asset",Rails="Upper platform guardrails";
        static double ready;static bool busy;
        static UDK7MUsabilityUpgrade(){ready=EditorApplication.timeSinceStartup+3;EditorApplication.update+=Poll;EditorApplication.playModeStateChanged+=s=>{if(s==PlayModeStateChange.EnteredEditMode)ready=EditorApplication.timeSinceStartup+3;};}
        static void Poll(){if(busy||EditorApplication.timeSinceStartup<ready||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||SessionState.GetBool("UDK7MBossValidation",false)||!File.Exists(Stage+"UsabilityUpgrade.request"))return;File.Delete(Stage+"UsabilityUpgrade.request");Run();}
        static void Run()
        {
            busy=true;var prior=SceneManager.GetActiveScene();var scene=SceneManager.GetSceneByPath(UDK7MThreePhaseBuilder.ScenePath);bool opened=false;
            try
            {
                if(!scene.IsValid()||!scene.isLoaded){scene=EditorSceneManager.OpenScene(UDK7MThreePhaseBuilder.ScenePath,OpenSceneMode.Additive);opened=true;}
                SceneManager.SetActiveScene(scene);Directory.CreateDirectory(Stage+"BeforeUsability");if(!File.Exists(Stage+"BeforeUsability/Arena.unity"))EditorSceneManager.SaveScene(scene,Stage+"BeforeUsability/Arena.unity",true);
                var arena=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<UDK7MArena>()).Single();arena.tuning.holdTurnSpeed=18;arena.tuning.chargeTrackingAngle=30;EditorUtility.SetDirty(arena.tuning);
                var audio=AssetDatabase.LoadAssetAtPath<UDK7MAudioProfile>(AudioPath);if(!audio){audio=ScriptableObject.CreateInstance<UDK7MAudioProfile>();AssetDatabase.CreateAsset(audio,AudioPath);}
                var controller=arena.GetComponent<UDK7MAudio>();if(!controller)controller=arena.gameObject.AddComponent<UDK7MAudio>();controller.profile=audio;controller.arena=arena;arena.feedback.audio=controller;
                int layer=LayerMask.NameToLayer("ArenaLowerFloor");var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/SovietFacility/Prefabs/R_Straight_2m_Yellow.prefab");
                foreach(var pillar in arena.pillars)
                {
                    if(pillar.transform.Find(Rails))continue;var group=new GameObject(Rails);group.transform.SetParent(pillar.transform,false);group.layer=layer;
                    // Full side rails, with 1.7 m gates aligned to the pure crystal
                    // and opposite edge. Landing and the downward attack stay usable.
                    for(int side=-1;side<=1;side+=2)
                    {
                        Rail(prefab,scene,group.transform,new Vector3(side*2.9f,8.98f,-2.9f),new Vector3(side*2.9f,8.98f,0),layer);
                        Rail(prefab,scene,group.transform,new Vector3(side*2.9f,8.98f,0),new Vector3(side*2.9f,8.98f,2.9f),layer);
                        Rail(prefab,scene,group.transform,new Vector3(-2.9f,8.98f,side*2.9f),new Vector3(-.85f,8.98f,side*2.9f),layer);
                        Rail(prefab,scene,group.transform,new Vector3(.85f,8.98f,side*2.9f),new Vector3(2.9f,8.98f,side*2.9f),layer);
                    }
                }
                EditorUtility.SetDirty(controller);EditorUtility.SetDirty(arena.feedback);AssetDatabase.SaveAssetIfDirty(arena.tuning);AssetDatabase.SaveAssetIfDirty(audio);EditorSceneManager.SaveScene(scene);
                var camera=scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>(true)).First(c=>c.name=="Disabled overview camera");var position=camera.transform.position;var rotation=camera.transform.rotation;float fov=camera.fieldOfView;
                var column=arena.pillars[0].transform.position;camera.transform.SetPositionAndRotation(column+new Vector3(9,14,-12),Quaternion.LookRotation(column+Vector3.up*9.5f-(column+new Vector3(9,14,-12))));camera.fieldOfView=52;AssetReviewUtility.Capture(scene,camera,Stage+"UpperPlatform_Rails.png");camera.transform.SetPositionAndRotation(position,rotation);camera.fieldOfView=fov;camera.enabled=false;EditorSceneManager.SaveScene(scene);
                File.WriteAllText(Stage+"usability_upgrade.json",JsonConvert.SerializeObject(new{passed=true,turn=18,maximumCorrection=30,rails=arena.pillars.Sum(p=>p.transform.Find(Rails).childCount),audio=AudioPath,cues=audio.sounds.Length,clipsAssigned=audio.sounds.Sum(s=>s.clips.Count(c=>c))},Formatting.Indented));
            }
            catch(Exception e){File.WriteAllText(Stage+"usability_upgrade.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{if(prior.IsValid())SceneManager.SetActiveScene(prior);if(opened&&scene.IsValid())EditorSceneManager.CloseScene(scene,true);busy=false;}
        }
        static Bounds Bounds(GameObject go){var renderers=go.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var r in renderers)bounds.Encapsulate(r.bounds);return bounds;}
        static void Rail(GameObject prefab,Scene scene,Transform parent,Vector3 a,Vector3 b,int layer)
        {
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);PrefabUtility.UnpackPrefabInstance(go,PrefabUnpackMode.OutermostRoot,InteractionMode.AutomatedAction);go.name="Yellow safety railing";go.transform.SetParent(parent,false);go.transform.localPosition=Vector3.zero;var bounds=Bounds(go);bool x=bounds.size.x>=bounds.size.z;var scale=go.transform.localScale;scale[x?0:2]*=Vector3.Distance(a,b)/(x?bounds.size.x:bounds.size.z);go.transform.localScale=scale;
            go.transform.rotation=Quaternion.FromToRotation(x?Vector3.right:Vector3.forward,parent.TransformDirection(b-a).normalized);bounds=Bounds(go);var target=parent.TransformPoint((a+b)*.5f);go.transform.position+=target-new Vector3(bounds.center.x,bounds.min.y,bounds.center.z);bounds=Bounds(go);
            foreach(var t in go.GetComponentsInChildren<Transform>())t.gameObject.layer=layer;foreach(var collider in go.GetComponentsInChildren<Collider>())UnityEngine.Object.DestroyImmediate(collider);
            var body=go.AddComponent<BoxCollider>();body.center=go.transform.InverseTransformPoint(bounds.center);var size=go.transform.InverseTransformVector(bounds.size);body.size=new Vector3(Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
        }
    }
}
