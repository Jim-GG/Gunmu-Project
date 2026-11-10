using System;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.VFX;
using UnityEngine.Rendering.Universal;
using GameActions;

namespace AssetPipeline.Editor
{
    // Disposable preview objects only: never adds preview behaviours to production prefabs.
    public sealed class CombatEffectPreviewWindow:EditorWindow
    {
        const string Menu="Tools/Combat VFX/Effect Preview";
        PreviewRenderUtility preview;GameObject source,instance,floor,backdrop;
        Material groundMaterial;Texture2D checker;Vector2 orbit=new Vector2(25,25);
        double previous;float time,speed=1,cycle=5,distance=10;bool playing=true,repeat=true;
        Vector3 focus=Vector3.up;string error;
        UnityEditor.Editor nativeGraphPreview;bool graphPreview=true;
        internal VisualEffect[] Effects=>instance?instance.GetComponentsInChildren<VisualEffect>():Array.Empty<VisualEffect>();
        [MenuItem(Menu)]public static void Open()=>GetWindow<CombatEffectPreviewWindow>("特效预览");
        [MenuItem("Assets/Preview Combat Effect",false,2100)]static void OpenAsset(){Open();}
        void OnEnable(){EditorApplication.update+=Tick;Selection.selectionChanged+=SelectionChanged;previous=EditorApplication.timeSinceStartup;SelectionChanged();}
        void OnDisable(){EditorApplication.update-=Tick;Selection.selectionChanged-=SelectionChanged;Dispose();}
        void SelectionChanged()
        {
            if(Selection.activeObject is GameObject go&&EditorUtility.IsPersistent(go)&&AssetDatabase.GetAssetPath(go).EndsWith(".prefab",StringComparison.OrdinalIgnoreCase))Load(go);
        }
        void Dispose(){if(nativeGraphPreview){DestroyImmediate(nativeGraphPreview);nativeGraphPreview=null;}if(preview!=null){preview.Cleanup();preview=null;}instance=null;if(groundMaterial)DestroyImmediate(groundMaterial);if(checker)DestroyImmediate(checker);}
        public void Load(GameObject asset)
        {
            source=asset;Dispose();error=null;
            if(!asset)return;
            // Runtime scripts on a clone must never execute against the player's live session.
            if(EditorApplication.isPlayingOrWillChangePlaymode)return;
            try
            {
                preview=new PreviewRenderUtility();preview.cameraFieldOfView=45;
                preview.camera.GetUniversalAdditionalCameraData().requiresColorTexture=true;
                preview.camera.GetUniversalAdditionalCameraData().requiresDepthTexture=true;
                preview.camera.nearClipPlane=.03f;preview.camera.farClipPlane=300;
                preview.camera.backgroundColor=new Color(.075f,.085f,.1f);preview.camera.clearFlags=CameraClearFlags.SolidColor;
                preview.lights[0].intensity=1.4f;preview.lights[0].transform.rotation=Quaternion.Euler(40,40,0);preview.lights[1].intensity=.6f;
                var stage=PrefabStageUtility.GetCurrentPrefabStage();
                var from=stage&&stage.assetPath==AssetDatabase.GetAssetPath(asset)?stage.prefabContentsRoot:asset;
                instance=Instantiate(from);instance.name=asset.name+" (editor preview only)";instance.hideFlags=HideFlags.HideAndDontSave;GraphReflect.Call(preview,"AddManagedGO",instance);
                instance.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);instance.SetActive(true);
                foreach(var audio in instance.GetComponentsInChildren<AudioSource>(true))audio.enabled=false;
                foreach(var script in instance.GetComponentsInChildren<MonoBehaviour>(true))script.enabled=false;
                foreach(var heat in instance.GetComponentsInChildren<HeatHazeController>(true)){if(heat.gpu)heat.gpu.gameObject.SetActive(heat.useGPU);if(heat.surface)heat.surface.gameObject.SetActive(!heat.useGPU);}
                foreach(var wave in instance.GetComponentsInChildren<ChromaticWaveController>(true)){if(wave.gpu)wave.gpu.gameObject.SetActive(wave.useGPU);if(wave.meshCore)wave.meshCore.gameObject.SetActive(!wave.useGPU);}
                checker=new Texture2D(64,64){hideFlags=HideFlags.HideAndDontSave,wrapMode=TextureWrapMode.Repeat};
                for(int y=0;y<64;y++)for(int x=0;x<64;x++)checker.SetPixel(x,y,((x/8+y/8)%2==0)?new Color(.28f,.31f,.35f):new Color(.42f,.44f,.47f));checker.Apply();
                groundMaterial=new Material(Shader.Find("Universal Render Pipeline/Lit")){hideFlags=HideFlags.HideAndDontSave};groundMaterial.SetTexture("_BaseMap",checker);groundMaterial.SetTextureScale("_BaseMap",new Vector2(6,6));
                floor=GameObject.CreatePrimitive(PrimitiveType.Plane);floor.hideFlags=HideFlags.HideAndDontSave;floor.name="Preview floor (not saved)";floor.transform.localScale=Vector3.one*6;floor.transform.position=Vector3.down*.05f;floor.GetComponent<Renderer>().sharedMaterial=groundMaterial;GraphReflect.Call(preview,"AddManagedGO",floor);
                backdrop=GameObject.CreatePrimitive(PrimitiveType.Cube);backdrop.hideFlags=HideFlags.HideAndDontSave;backdrop.name="Refraction background (not saved)";backdrop.transform.position=new Vector3(0,4,10);backdrop.transform.localScale=new Vector3(25,9,.1f);backdrop.GetComponent<Renderer>().sharedMaterial=groundMaterial;GraphReflect.Call(preview,"AddManagedGO",backdrop);
                focus=Vector3.up;distance=asset.name.Contains("Explosion")?18:asset.name.Contains("Crystal")?9:asset.name.Contains("Spark")?4:8;
                cycle=asset.name.Contains("Explosion")?6:asset.name.Contains("Spark")?1:asset.name.Contains("Crystal")?2.5f:5;
                Restart();
                var effect=Effects.FirstOrDefault();if(effect&&effect.visualEffectAsset)nativeGraphPreview=UnityEditor.Editor.CreateEditor(effect.visualEffectAsset);
            }
            catch(Exception e){error=e.Message;Debug.LogException(e);}
            Repaint();
        }
        public void Restart()
        {
            time=0;if(!instance)return;instance.SetActive(true);
            foreach(var ps in instance.GetComponentsInChildren<ParticleSystem>(true)){if(!ps.gameObject.activeInHierarchy)continue;ps.Simulate(0,false,true,true);ps.Pause(false);}
            foreach(var v in instance.GetComponentsInChildren<VisualEffect>(true)){if(!v.gameObject.activeInHierarchy)continue;if(v.HasVector3("GroundPoint"))v.SetVector3("GroundPoint",v.transform.InverseTransformPoint(Vector3.zero));if(v.HasVector3("GroundNormal"))v.SetVector3("GroundNormal",v.transform.InverseTransformDirection(Vector3.up));v.Reinit();v.pause=!playing;}
            Step(.016f);Repaint();
        }
        public void Step(float dt)
        {Advance(dt,true);EditorApplication.QueuePlayerLoopUpdate();}
        public void SetPlayback(bool value){playing=value;if(instance)foreach(var v in instance.GetComponentsInChildren<VisualEffect>())v.pause=!value;}
        void Advance(float dt,bool manualGPU)
        {
            if(!instance)return;time+=dt;
            foreach(var ps in instance.GetComponentsInChildren<ParticleSystem>())ps.Simulate(dt,false,false,true);
            foreach(var v in instance.GetComponentsInChildren<VisualEffect>()){v.pause=manualGPU;if(manualGPU)v.Simulate(dt,1);else v.playRate=speed;}
            foreach(var wind in instance.GetComponentsInChildren<WindConeFX.WindConeBurst>())wind.SetNormalizedTime(time/Mathf.Max(.05f,wind.duration));
            foreach(var dash in instance.GetComponentsInChildren<DashWindConeController>())if(!dash.prefabPlayback)foreach(var r in dash.shells)if(r){var b=new MaterialPropertyBlock();r.GetPropertyBlock(b);float t=time/Mathf.Max(.05f,dash.duration);b.SetFloat("_LifeAlpha",Mathf.SmoothStep(0,1,t/.2f)*(1-Mathf.SmoothStep(.35f,1,t)));r.SetPropertyBlock(b);}
            foreach(var p in instance.GetComponentsInChildren<VFXLightPulse>())if(p.point){p.point.enabled=time<p.duration;p.point.intensity=p.IntensityAt(time);}
            foreach(var heat in instance.GetComponentsInChildren<HeatHazeController>())if(heat.surface&&!heat.useGPU){var b=new MaterialPropertyBlock();heat.surface.GetPropertyBlock(b);b.SetFloat("_Mode",2);b.SetFloat("_Opacity",heat.opacity);b.SetFloat("_DistortionPixels",heat.distortionPixels);b.SetFloat("_ChromaticPixels",0);b.SetFloat("_Lifecycle",heat.loop?0:Mathf.Clamp01(time/heat.duration));heat.surface.SetPropertyBlock(b);}
            foreach(var wave in instance.GetComponentsInChildren<ChromaticWaveController>())if(wave.meshCore&&!wave.useGPU){float t=Mathf.Clamp01(time/wave.duration);wave.meshCore.transform.localScale=Vector3.one*Mathf.Lerp(.15f,wave.radius*2,1-Mathf.Pow(1-t,2));var b=new MaterialPropertyBlock();wave.meshCore.GetPropertyBlock(b);b.SetFloat("_Mode",1);b.SetFloat("_Lifecycle",t);b.SetFloat("_DistortionPixels",wave.distortionPixels);b.SetFloat("_ChromaticPixels",wave.chromaticPixels);b.SetVector("_CenterSS",new Vector4(.5f,.5f,1,0));wave.meshCore.SetPropertyBlock(b);}
            foreach(var crack in instance.GetComponentsInChildren<GroundCrackImpact>())if(crack.parallaxMesh){crack.parallaxMesh.enabled=true;if(crack.distantDecal)crack.distantDecal.enabled=false;var b=new MaterialPropertyBlock();crack.parallaxMesh.GetPropertyBlock(b);b.SetFloat("_HeightAmplitudeCm",crack.depth*100);b.SetFloat("_POMSteps",crack.nearSteps);b.SetVector("_PrimitiveSize",new Vector4(1,1,0,0));b.SetFloat("_CrackOpacity",1);crack.parallaxMesh.SetPropertyBlock(b);}
        }
        void Tick()
        {
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Clamp((float)(now-previous),0,.05f);previous=now;
            if(EditorApplication.isPlayingOrWillChangePlaymode){if(preview!=null)Dispose();return;}
            if(!instance&&source&&error==null)Load(source);
            if(playing&&instance){Advance(dt*speed,false);if(repeat&&time>=cycle)Restart();EditorApplication.QueuePlayerLoopUpdate();Repaint();}
        }
        public Texture2D CaptureFrame(int width=640,int height=480)
        {
            preview.BeginStaticPreview(new Rect(0,0,width,height));PositionCamera();preview.Render(true);return preview.EndStaticPreview();
        }
        void PositionCamera(){preview.camera.cameraType=CameraType.Game;preview.camera.transform.position=focus+Quaternion.Euler(orbit.y,orbit.x,0)*new Vector3(0,0,-distance);preview.camera.transform.LookAt(focus);preview.camera.allowHDR=true;}
        void OnGUI()
        {
            EditorGUILayout.HelpBox("GPU 特效默认使用 Unity 原生 Graph 预览，播放按钮在下方；显示 Graph 默认参数。组合预览用于 Prefab 组件和网格。修改 Prefab 后点“重新读取”。",MessageType.Info);
            var selected=(GameObject)EditorGUILayout.ObjectField("特效 Prefab",source,typeof(GameObject),false);if(selected!=source)Load(selected);
            using(new EditorGUILayout.HorizontalScope())
            {
                if(GUILayout.Button(playing?"暂停":"播放"))SetPlayback(!playing);
                if(GUILayout.Button("重播"))Restart();
                if(GUILayout.Button("前进一帧")){SetPlayback(false);Step(1/30f);Repaint();}
                if(GUILayout.Button("重新读取")&&source)Load(source);
                if(GUILayout.Button("编辑 Prefab")&&source)AssetDatabase.OpenAsset(source);
                if(GUILayout.Button("选择 Graph")){var v=Effects.FirstOrDefault();if(v&&v.visualEffectAsset)Selection.activeObject=v.visualEffectAsset;}
            }
            using(new EditorGUILayout.HorizontalScope()){repeat=EditorGUILayout.ToggleLeft("循环预览",repeat,GUILayout.Width(90));cycle=EditorGUILayout.FloatField("周期(秒)",cycle);cycle=Mathf.Max(.1f,cycle);speed=EditorGUILayout.Slider("速度",speed,.05f,2);}
            if(EditorApplication.isPlayingOrWillChangePlaymode){EditorGUILayout.HelpBox("请停止 Play 后预览资源，避免临时副本运行游戏逻辑。",MessageType.Info);return;}
            if(!string.IsNullOrEmpty(error)){EditorGUILayout.HelpBox(error,MessageType.Error);return;}
            if(nativeGraphPreview&&nativeGraphPreview.HasPreviewGUI())
            {
                graphPreview=EditorGUILayout.ToggleLeft("使用 Unity 原生 GPU Graph 预览（下方自带播放/暂停）",graphPreview);
                if(graphPreview){nativeGraphPreview.OnPreviewSettings();var nativeRect=GUILayoutUtility.GetRect(200,200,10000,10000);nativeGraphPreview.OnInteractivePreviewGUI(nativeRect,GUIStyle.none);return;}
            }
            var rect=GUILayoutUtility.GetRect(200,200,10000,10000);
            if(preview==null||!instance){GUI.Label(rect,"选择一个特效 Prefab");return;}
            var ev=Event.current;if(rect.Contains(ev.mousePosition)){if(ev.type==EventType.MouseDrag&&ev.button==0){orbit+=ev.delta*.5f;ev.Use();}if(ev.type==EventType.ScrollWheel){distance=Mathf.Clamp(distance*Mathf.Pow(1.1f,ev.delta.y),.3f,100);ev.Use();}}
            if(ev.type==EventType.Repaint)
            {
                preview.BeginPreview(rect,GUIStyle.none);PositionCamera();
                preview.Render(true);GUI.DrawTexture(rect,preview.EndPreview(),ScaleMode.StretchToFill,false);
                GUI.Label(new Rect(rect.x+8,rect.y+8,300,22),$"{time:F2}s  拖动旋转 / 滚轮缩放");
            }
        }
    }
}
