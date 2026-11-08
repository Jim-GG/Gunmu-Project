using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Threading.Tasks;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.Rendering;
using GameActions;
using Resonator.AI;
using Resonator.AI.Editor;

namespace AssetPipeline.Editor
{
    [InitializeOnLoad]
    public static class UDK7MImport
    {
        const string Stage="AssetStaging/UDK7M/", Root="Assets/Game/Enemies/UDK7M/";
        static bool busy;
        static UDK7MImport(){EditorApplication.update+=Poll;}
        static void Poll()
        {
            if(!busy&&!EditorApplication.isCompiling&&!EditorApplication.isUpdating&&!EditorApplication.isPlayingOrWillChangePlaymode&&File.Exists(Stage+"Review.request")){File.Delete(Stage+"Review.request");FinishExisting();return;}
            if(busy||EditorApplication.isCompiling||EditorApplication.isUpdating||EditorApplication.isPlayingOrWillChangePlaymode||!File.Exists(Stage+"UnityImport.request"))return;
            File.Delete(Stage+"UnityImport.request");Run();
        }
        static void FinishExisting()
        {
            busy=true;
            try
            {
                var validation=JObject.Parse(File.ReadAllText(Stage+"unity_pose_validation.json"));if(!(bool)validation["passed"])throw new Exception("Pose validation must pass first");
                var result=Finish(A<ActionSet>(Root+"Actions/UDK7MActions.asset"),validation["clips"]);File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(result,Formatting.Indented));Debug.Log("UDK7M_IMPORT_PASSED");
            }
            catch(Exception e){File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{busy=false;}
        }
        [MenuItem("Tools/Asset Pipeline/Import UDK7M Animations")]
        public static async void Run()
        {
            if(busy)return;busy=true;File.WriteAllText(Stage+"unity_import_status.json","{\"status\":\"running\"}");
            try{var result=await Build();File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(result,Formatting.Indented));Debug.Log("UDK7M_IMPORT_PASSED");}
            catch(Exception e){File.WriteAllText(Stage+"unity_import_status.json",JsonConvert.SerializeObject(new{passed=false,error=e.ToString()},Formatting.Indented));Debug.LogException(e);}
            finally{busy=false;}
        }
        static T A<T>(string path)where T:UnityEngine.Object=>AssetDatabase.LoadAssetAtPath<T>(path);
        static Vector3 V(JToken t)=>new Vector3((float)t[0],(float)t[1],(float)t[2]);
        static Vector3 FromBlender(JToken t){var v=V(t);return new Vector3(-v.x,v.z,-v.y);}
        static T Save<T>(T value,string path)where T:UnityEngine.Object
        {
            var existing=A<T>(path);if(!existing){AssetDatabase.CreateAsset(value,path);return value;}
            EditorUtility.CopySerialized(value,existing);UnityEngine.Object.DestroyImmediate(value);EditorUtility.SetDirty(existing);return existing;
        }
        static ModelImporter Model(string path,bool animation)
        {
            var i=(ModelImporter)AssetImporter.GetAtPath(path);if(!i)throw new Exception("Missing FBX "+path);
            i.importAnimation=animation;i.animationType=ModelImporterAnimationType.Generic;i.preserveHierarchy=true;i.optimizeGameObjects=false;i.importCameras=false;i.importLights=false;i.globalScale=1;i.useFileScale=true;i.bakeAxisConversion=false;
            i.importNormals=ModelImporterNormals.Import;i.importTangents=ModelImporterTangents.CalculateMikk;i.animationCompression=ModelImporterAnimationCompression.Off;i.materialImportMode=ModelImporterMaterialImportMode.ImportStandard;return i;
        }
        static Task<object> Build()
        {
            var cfg=JObject.Parse(File.ReadAllText(Stage+"export_manifest.json"));AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
            var shader=Shader.Find("Null Boundary/Drone/Baked Enamel");if(!shader)throw new Exception("Missing shared atlas shader");
            var mats=new Dictionary<string,Material>();
            foreach(var desc in cfg["atlases"])
            {
                foreach(string channel in new[]{"base","mask","normal","emission"})
                {
                    string path=Root+"Textures/"+(string)desc[channel];var ti=(TextureImporter)AssetImporter.GetAtPath(path);
                    ti.textureType=channel=="normal"?TextureImporterType.NormalMap:TextureImporterType.Default;ti.sRGBTexture=channel=="base"||channel=="emission";ti.alphaSource=TextureImporterAlphaSource.FromInput;ti.alphaIsTransparency=false;
                    ti.maxTextureSize=(int)desc["size"];ti.mipmapEnabled=true;ti.wrapMode=TextureWrapMode.Clamp;ti.filterMode=FilterMode.Trilinear;ti.anisoLevel=8;ti.flipGreenChannel=false;ti.textureCompression=TextureImporterCompression.CompressedHQ;
                    var platform=ti.GetPlatformTextureSettings("Standalone");platform.name="Standalone";platform.overridden=true;platform.maxTextureSize=ti.maxTextureSize;platform.format=channel=="normal"?TextureImporterFormat.BC5:TextureImporterFormat.BC7;ti.SetPlatformTextureSettings(platform);ti.SaveAndReimport();
                }
                string name=(string)desc["material"],materialPath=Root+"Materials/"+name+".mat";var m=A<Material>(materialPath);if(!m){m=new Material(shader){name=name};AssetDatabase.CreateAsset(m,materialPath);}m.shader=shader;
                foreach(var pair in new[]{("_BaseMap","base"),("_BumpMap","normal"),("_MetallicGlossMap","mask"),("_EmissionMap","emission")})m.SetTexture(pair.Item1,A<Texture2D>(Root+"Textures/"+(string)desc[pair.Item2]));
                m.SetColor("_BaseColor",Color.white);m.SetColor("_EmissionColor",Color.white);m.SetFloat("_Metallic",1);m.SetFloat("_Smoothness",1);m.SetFloat("_BumpScale",1);m.SetFloat("_OcclusionStrength",1);m.SetFloat("_ClearCoatMask",0);
                m.SetTexture("_DetailNormalMap",A<Texture2D>("Assets/Game/Environment/ModernLab/Textures/Lab_Detail_Coating_Normal.png"));m.SetFloat("_DetailNormalMapScale",.06f);m.SetFloat("_DetailAlbedoMapScale",28);m.enableInstancing=true;EditorUtility.SetDirty(m);mats[name]=m;
            }
            string modelPath=Root+"Models/UDK7M_Model.fbx";var mi=Model(modelPath,false);mi.avatarSetup=ModelImporterAvatarSetup.CreateFromThisModel;
            foreach(var pair in mats)mi.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material),pair.Key),pair.Value);mi.SaveAndReimport();
            var source=A<GameObject>(modelPath);var avatar=AssetDatabase.LoadAllAssetsAtPath(modelPath).OfType<Avatar>().FirstOrDefault();
            var clips=new Dictionary<string,AnimationClip>();var checks=new List<object>();
            var scene=EditorSceneManager.NewPreviewScene();var instance=(GameObject)PrefabUtility.InstantiatePrefab(source,scene);
            try
            {
                var renderers=instance.GetComponentsInChildren<SkinnedMeshRenderer>(true);int triangles=renderers.Sum(r=>r.sharedMesh.triangles.Length/3);
                if(triangles>50000||triangles<37500)throw new Exception("Unexpected triangle count "+triangles);
                foreach(var r in renderers)
                {
                    if(r.sharedMesh.uv.Length!=r.sharedMesh.vertexCount||r.bones.Any(b=>!b)||r.sharedMaterials.Any(m=>!m||m.shader!=shader))throw new Exception("Invalid UV, skin or material on "+r.name);
                    r.updateWhenOffscreen=true;
                }
                foreach(var desc in cfg["clips"])
                {
                    string id=(string)desc["id"],fbx=Root+"Animations/Source/UDK7M_"+id+".fbx";var ai=Model(fbx,true);ai.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;ai.sourceAvatar=avatar;ai.SaveAndReimport();
                    var takes=ai.defaultClipAnimations;if(takes.Length!=1)throw new Exception("Expected one animation take "+id);
                    takes[0].name=id;takes[0].firstFrame=0;takes[0].lastFrame=(int)desc["frames"]-1;takes[0].loopTime=(bool)desc["loop"];takes[0].loopPose=false;ai.clipAnimations=takes;ai.SaveAndReimport();
                    var imported=AssetDatabase.LoadAllAssetsAtPath(fbx).OfType<AnimationClip>().First(c=>!c.name.StartsWith("__preview__"));var clip=UnityEngine.Object.Instantiate(imported);clip.name=id;
                    // Resolve exporter-root prefixes once on the editable clip; preserve the actual bone paths.
                    var paths=instance.GetComponentsInChildren<Transform>(true).Select(t=>AnimationUtility.CalculateTransformPath(t,instance.transform)).ToArray();
                    foreach(var binding in AnimationUtility.GetCurveBindings(clip))
                    {
                        if(binding.type!=typeof(Transform)||binding.path.Length==0||paths.Contains(binding.path))continue;
                        var candidates=paths.Where(p=>p.EndsWith("/"+binding.path)||binding.path.EndsWith("/"+p)).ToArray();if(candidates.Length!=1)throw new Exception("Unresolved clip binding "+id+" "+binding.path);
                        var curve=AnimationUtility.GetEditorCurve(clip,binding);var renamed=binding;renamed.path=candidates[0];AnimationUtility.SetEditorCurve(clip,binding,null);AnimationUtility.SetEditorCurve(clip,renamed,curve);
                    }
                    clip=Save(clip,Root+"Animations/Clips/"+id+".anim");clips[id]=clip;
                    float error=0,skinError=0;
                    foreach(var sample in desc["benchmarks"])
                    {
                        clip.SampleAnimation(instance,(float)sample["frame"]/(float)cfg["fps"]);
                        foreach(var marker in sample["positions"])
                        {
                            var t=instance.GetComponentsInChildren<Transform>(true).First(x=>x.name==(string)marker["bone"]);
                            error=Mathf.Max(error,Vector3.Distance(t.position,FromBlender(marker["position"])));
                        }
                        foreach(var skin in ((JObject)sample["skin_positions"]).Properties())
                        {
                            var renderer=renderers.First(r=>r.name==skin.Name);var baked=new Mesh();
                            try
                            {
                                renderer.BakeMesh(baked);var verts=baked.vertices.Select(v=>renderer.transform.TransformPoint(v)).ToArray();
                                foreach(var point in skin.Value){var expected=V(point);float nearest=float.PositiveInfinity;foreach(var v in verts)nearest=Mathf.Min(nearest,(v-expected).sqrMagnitude);skinError=Mathf.Max(skinError,Mathf.Sqrt(nearest));}
                            }
                            finally{UnityEngine.Object.DestroyImmediate(baked);}
                        }
                    }
                    if(error>.015f||skinError>.015f)throw new Exception("Blender/Unity pose mismatch "+id+" bones="+error+" skin="+skinError+"m");checks.Add(new{id,seconds=clip.length,max_position_error_metres=error,max_skin_vertex_error_metres=skinError,curves=AnimationUtility.GetCurveBindings(clip).Length});
                }
            }
            finally{UnityEngine.Object.DestroyImmediate(instance);EditorSceneManager.ClosePreviewScene(scene);}
            var set=A<ActionSet>(Root+"Actions/UDK7MActions.asset");bool newSet=!set;
            if(newSet){set=ScriptableObject.CreateInstance<ActionSet>();set.name="UDK7MActions";AssetDatabase.CreateAsset(set,Root+"Actions/UDK7MActions.asset");}
            var definitions=new List<ActionDefinition>();
            foreach(var desc in cfg["clips"])
            {
                string id=(string)desc["id"],path=Root+"Actions/Tracks/"+id+".asset";var track=A<ResonatorMotionTrack>(path);bool newTrack=!track;
                if(newTrack){track=ScriptableObject.CreateInstance<ResonatorMotionTrack>();track.name=id;AssetDatabase.CreateAsset(track,path);}
                track.clip=clips[id];track.framesPerSecond=(float)cfg["fps"];track.sampleRate=track.framesPerSecond;track.trajectory=desc["trajectory"].Select(V).ToArray();track.referenceHip=track.trajectory[0];float travelled=0;for(int i=1;i<track.trajectory.Length;i++){var d=track.trajectory[i]-track.trajectory[i-1];d.y=0;travelled+=d.magnitude;}track.averageSpeed=travelled/Mathf.Max(.01f,track.Duration);EditorUtility.SetDirty(track);
                var definition=set.Find(id)??new ActionDefinition{id=id,isAttack=id=="TripleSlam"||id=="SlamHold"||id=="ChargeHold",action=new AttackBeat{hits=Array.Empty<HitWindow>(),animation=new MotionBeat{loop=(bool)desc["loop"],blend=.08f}}};definition.Motion.track=track;definitions.Add(definition);
            }
            set.actions=definitions.ToArray();set.firstPersonPreview=false;set.previewFocus=new Vector3(0,2.5f,0);set.previewDistance=19;
            if(set.sockets.Length==1&&set.sockets[0].role==HitboxAnchor.CustomBone&&set.sockets[0].transformName=="DrillRoll")set.sockets=Array.Empty<ActionSocketBinding>();
            if(newSet)
            {
                set.sockets=Array.Empty<ActionSocketBinding>();
                var transitions=new List<ActionTransition>();
                void Link(string from,string to,float cancel=-1,float blend=.08f)=>transitions.Add(new ActionTransition{label=from+" → "+to,from=from,to=to,cancelFrame=cancel,blend=blend});
                Link("Idle","SlamPrepare",0);Link("SlamPrepare","SlamHold");Link("SlamHold","SlamRecover",0);Link("SlamRecover","Idle");Link("Idle","TripleSlam",0);Link("TripleSlam","Idle");Link("Idle","ChargePrepare",0);Link("ChargePrepare","ChargeHold");Link("ChargeHold","ChargeRecover",0);Link("ChargeRecover","Idle");
                set.transitions=transitions.ToArray();
            }
            string controllerPath=Root+"Animations/UDK7M.controller";var controller=A<AnimatorController>(controllerPath);
            if(!controller)
            {
                controller=AnimatorController.CreateAnimatorControllerAtPath(controllerPath);var machine=controller.layers[0].stateMachine;
                foreach(var pair in clips){var state=machine.AddState(pair.Key);state.motion=pair.Value;state.writeDefaultValues=false;if(pair.Key=="Idle")machine.defaultState=state;controller.AddParameter("Play_"+pair.Key,AnimatorControllerParameterType.Trigger);var edge=machine.AddAnyStateTransition(state);edge.hasExitTime=false;edge.duration=.08f;edge.canTransitionToSelf=false;edge.AddCondition(AnimatorConditionMode.If,0,"Play_"+pair.Key);}
            }
            var prefabScene=EditorSceneManager.NewPreviewScene();string prefabPath=Root+"Prefabs/UDK7M.prefab";
            try
            {
                var wrapper=new GameObject("UDK7M");SceneManager.MoveGameObjectToScene(wrapper,prefabScene);var visual=(GameObject)PrefabUtility.InstantiatePrefab(source,prefabScene);visual.name="Visual";visual.transform.SetParent(wrapper.transform,false);
                var animator=visual.GetComponent<Animator>();if(!animator)animator=visual.AddComponent<Animator>();animator.runtimeAnimatorController=controller;animator.applyRootMotion=false;animator.cullingMode=AnimatorCullingMode.AlwaysAnimate;
                foreach(var r in visual.GetComponentsInChildren<SkinnedMeshRenderer>()){r.updateWhenOffscreen=true;var b=r.localBounds;b.Expand(10);r.localBounds=b;}
                var player=visual.AddComponent<ActionMotionPlayer>();player.animator=animator;var runner=visual.AddComponent<ActionRunner>();runner.profile=set;runner.motion=player;
                PrefabUtility.SaveAsPrefabAsset(wrapper,prefabPath);UnityEngine.Object.DestroyImmediate(wrapper);
            }
            finally{EditorSceneManager.ClosePreviewScene(prefabScene);}
            set.previewModel=A<GameObject>(prefabPath);EditorUtility.SetDirty(set);AssetDatabase.SaveAssets();
            File.WriteAllText(Stage+"unity_pose_validation.json",JsonConvert.SerializeObject(new{passed=true,clips=checks},Formatting.Indented));
            return Task.FromResult(Finish(set,checks));
        }
        static object Finish(ActionSet set,object checks)
        {
            Review(set);
            // Exercise the same graph used by the workbench, including a half-completed transition.
            var ps=EditorSceneManager.NewPreviewScene();var actor=(GameObject)PrefabUtility.InstantiatePrefab(set.previewModel,ps);
            try
            {
                var player=actor.GetComponentInChildren<ActionMotionPlayer>();player.Play(set.Find("TripleSlam").Motion,0,true);player.Seek(45);
                var cfg=JObject.Parse(File.ReadAllText(Stage+"export_manifest.json"));var marker=cfg["clips"].First(c=>(string)c["id"]=="TripleSlam")["benchmarks"].First(b=>(int)b["frame"]==45)["positions"].First(p=>(string)p["bone"]=="Tool");var tool=actor.GetComponentsInChildren<Transform>().First(t=>t.name=="Tool");if(Vector3.Distance(tool.position,FromBlender(marker["position"]))>.015f)throw new Exception("Runtime workbench graph pose mismatch");
                player.SampleBlend(set.Find("SlamPrepare").Motion,15,set.Find("SlamHold").Motion,0,.08f,.04f);if(Mathf.Abs(player.DestinationWeight-.5f)>.001f)throw new Exception("Workbench blend weight mismatch");player.ReleaseGraph();
            }
            finally{UnityEngine.Object.DestroyImmediate(actor);EditorSceneManager.ClosePreviewScene(ps);}
            var workbench=ResonatorAnimationWorkbench.OpenShared(set);workbench.SaveModelPreview(Stage+"Preview/Unity_Workbench.png");Selection.activeObject=set;
            return new{passed=true,prefab=Root+"Prefabs/UDK7M.prefab",actionSet=AssetDatabase.GetAssetPath(set),clips=checks,triangles=38289,skinGroups=3,bones=276,workbenchPreview=true};
        }
        static void Review(ActionSet set)
        {
            // Recover only this importer's generated review scene if a script reload
            // interrupted the previous run. Never close the user's unsaved scenes.
            var generatedNames=new HashSet<string>{"UDK7M","Review floor","Soft key","Warm edge","Fill","Review camera","Review colour","Review reflections"};
            for(int i=SceneManager.sceneCount-1;i>=0;i--)
            {
                var leftover=SceneManager.GetSceneAt(i);var roots=leftover.GetRootGameObjects();
                if(leftover.path.Length!=0||roots.Length!=8||!roots.All(g=>generatedNames.Contains(g.name)))continue;
                var original=Enumerable.Range(0,SceneManager.sceneCount).Select(SceneManager.GetSceneAt).FirstOrDefault(s=>s.path=="Assets/Game/Environment/SovietFacility/Scenes/ConcreteBunkerReview.unity");
                if(original.IsValid())
                {
                    SceneManager.SetActiveScene(original);
                    foreach(var p in original.GetRootGameObjects().Where(g=>g.name=="Room reflection").SelectMany(g=>g.GetComponents<ReflectionProbe>()))p.enabled=true;
                }
                EditorSceneManager.CloseScene(leftover,true);
            }
            var prior=SceneManager.GetActiveScene();var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Additive);SceneManager.SetActiveScene(scene);
            var probes=UnityEngine.Object.FindObjectsByType<ReflectionProbe>(FindObjectsSortMode.None).Where(p=>p.enabled).ToArray();foreach(var p in probes)p.enabled=false;
            try
            {
                var actor=(GameObject)PrefabUtility.InstantiatePrefab(set.previewModel,scene);foreach(var t in actor.GetComponentsInChildren<Transform>(true))t.gameObject.layer=30;
                var animator=actor.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;var player=actor.GetComponentInChildren<ActionMotionPlayer>();
                var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);SceneManager.MoveGameObjectToScene(floor,scene);floor.name="Review floor";floor.layer=30;floor.transform.position=new Vector3(0,-.1f,0);floor.transform.localScale=new Vector3(40,.15f,40);floor.GetComponent<Renderer>().sharedMaterial=A<Material>("Assets/Game/Environment/ModernLab/Materials/Props/FP_Review_Ground.mat");
                AssetReviewUtility.Light(scene,"Soft key",new Vector3(-6,10,6),new Vector3(0,2,0),LightType.Spot,180,new Color(.9f,.95f,1));AssetReviewUtility.Light(scene,"Warm edge",new Vector3(8,7,-5),new Vector3(0,2,0),LightType.Spot,130,new Color(1,.78f,.53f));AssetReviewUtility.Light(scene,"Fill",new Vector3(0,8,5),Vector3.zero,LightType.Directional,1.5f,Color.white);
                RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=new Color(.25f,.28f,.32f);var sh=new SphericalHarmonicsL2();sh.AddAmbientLight(new Color(.3f,.32f,.35f));RenderSettings.ambientProbe=sh;
                var camera=AssetReviewUtility.Camera(scene,new Vector3(-12,8,-15),new Vector3(0,2.4f,0),36);
                var po=new GameObject("Review reflections",typeof(ReflectionProbe));SceneManager.MoveGameObjectToScene(po,scene);po.layer=30;po.transform.position=new Vector3(0,3,0);var probe=po.GetComponent<ReflectionProbe>();probe.enabled=false;probe.mode=ReflectionProbeMode.Custom;probe.size=new Vector3(30,20,30);probe.customBakedTexture=AssetReviewUtility.RoomCube(scene,po.transform.position,"Assets/Game/Scene/UDK7MReviewReflection.cubemap");probe.intensity=.65f;probe.enabled=true;
                foreach(var pose in new[]{("Idle",0f),("TripleSlam",45f),("ChargeHold",7f)})
                {
                    player.Play(set.Find(pose.Item1).Motion,pose.Item2,true);
                    // Multiple renders in one editor frame reuse GPU skin buffers. Freeze
                    // the verified CPU pose for each independent review image instead.
                    var frozen=new List<GameObject>();var meshes=new List<Mesh>();var skins=actor.GetComponentsInChildren<SkinnedMeshRenderer>();
                    try
                    {
                        foreach(var skin in skins)
                        {
                            var mesh=new Mesh();skin.BakeMesh(mesh);meshes.Add(mesh);var still=new GameObject("Review pose "+skin.name,typeof(MeshFilter),typeof(MeshRenderer));SceneManager.MoveGameObjectToScene(still,scene);still.layer=30;still.transform.SetPositionAndRotation(skin.transform.position,skin.transform.rotation);still.transform.localScale=skin.transform.lossyScale;still.GetComponent<MeshFilter>().sharedMesh=mesh;still.GetComponent<MeshRenderer>().sharedMaterials=skin.sharedMaterials;frozen.Add(still);skin.enabled=false;
                        }
                        AssetReviewUtility.Capture(scene,camera,Stage+"Preview/Unity_"+pose.Item1+".png");
                    }
                    finally{foreach(var go in frozen)UnityEngine.Object.DestroyImmediate(go);foreach(var mesh in meshes)UnityEngine.Object.DestroyImmediate(mesh);foreach(var skin in skins)skin.enabled=true;}
                }
                player.ReleaseGraph();animator.runtimeAnimatorController=A<AnimatorController>(Root+"Animations/UDK7M.controller");EditorSceneManager.SaveScene(scene,"Assets/Game/Scene/UDK7MAnimationReview.unity");
            }
            finally{foreach(var c in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Camera>()))c.enabled=false;SceneManager.SetActiveScene(prior);EditorSceneManager.CloseScene(scene,true);foreach(var p in probes)if(p)p.enabled=true;}
        }
    }
}
