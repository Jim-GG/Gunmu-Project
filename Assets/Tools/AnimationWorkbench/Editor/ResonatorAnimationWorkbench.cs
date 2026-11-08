using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
using GameActions;

namespace Resonator.AI.Editor
{
    public sealed partial class ResonatorAnimationWorkbench : EditorWindow
    {
        static readonly string[] Keys={"headbutt","kick","attack","punch","wake","feeding","idleSlow","idleRestless","resonance","agonizing","scream","walk","run","hit","stumble","death"};
        static readonly string[] Names={"Headbutt / 头槌","Kicking / 踢击","Attack / 双手砸击","Punching / 挥击","Wake / 装死起身","Feeding / 进食","Idle Slow","Idle Restless","Resonance / 共鸣","Agonizing","Scream","Walking","Running","Reaction Hit","Stumbling","Death"};
        [SerializeField] ResonatorTuning tuning;
        [SerializeField] ActionSet actionSet;
        ScriptableObject Profile=>actionSet?(ScriptableObject)actionSet:tuning;
        bool IsAttackAction=>ActionAttack()!=null;
        string[] ActionNames=>actionSet?actionSet.actions.Select(a=>a.id).ToArray():Names;
        float TransitionBlend=>actionSet?(SharedRule()?.blend??0):(Rule()?.blend??0);
        [SerializeField] int mode,actionIndex,transitionIndex,selectedHit,selectedEvent;
        [SerializeField] float frame,transitionTime,sourceExitFrame=40,rate=1;
        [SerializeField] bool hitBranch=true,showInactive=true,showTarget=true,loop=true,previewRootMotion;
        [SerializeField] Vector2 orbit=new Vector2(140,12);
        [SerializeField] float distance=4.5f;
        [SerializeField] EnemyState anySource=EnemyState.Headbutt;
        Vector2 scroll;
        SerializedObject data;
        ReorderableList hits,events;
        PreviewRenderUtility preview;
        GameObject model,previewRoot;
        ActionMotionPlayer player;
        ResonatorHitbox geometry;
        bool playing,previewSuspended;
        double lastTime;
        float shownFrame;
        MotionBeat shownBeat;
        AttackBeat shownAttack;
        Rect viewRect;
        Vector3 dragPoint,dragOffset;
        int dragKind=-1;

        [MenuItem("Tools/Resonator/Animation Workbench 动画与命中盒")]
        public static void OpenMenu(){if(Selection.activeObject is ActionSet set)OpenShared(set);else Open(Selection.activeObject as ResonatorTuning);}
        [MenuItem("Tools/Game Actions/Animation Workbench 动画工作台")]
        public static void OpenUnified()=>OpenMenu();
        public static ResonatorAnimationWorkbench Open(ResonatorTuning profile)
        {
            var w=GetWindow<ResonatorAnimationWorkbench>();w.titleContent=new GUIContent("动作工作台");w.minSize=new Vector2(930,680);
            w.SetProfile(profile?profile:AssetDatabase.LoadAssetAtPath<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset"));w.Show();return w;
        }
        void OnEnable(){EditorApplication.update+=Tick;EditorApplication.playModeStateChanged+=PlayModeChanged;Undo.undoRedoPerformed+=Repaint;lastTime=EditorApplication.timeSinceStartup;}
        void OnDisable(){EditorApplication.update-=Tick;EditorApplication.playModeStateChanged-=PlayModeChanged;Undo.undoRedoPerformed-=Repaint;Cleanup();}
        void PlayModeChanged(PlayModeStateChange change)
        {
            previewSuspended=change==PlayModeStateChange.ExitingEditMode||change==PlayModeStateChange.ExitingPlayMode;
            if(previewSuspended)Cleanup();else Repaint();
        }
        void Cleanup(){if(player)player.StopForRagdoll();if(previewRoot)DestroyImmediate(previewRoot);else if(model)DestroyImmediate(model);previewRoot=null;model=null;player=null;geometry=null;if(preview!=null)preview.Cleanup();preview=null;}
        void SetProfile(ResonatorTuning profile){Cleanup();actionSet=null;tuning=profile;data=profile?new SerializedObject(profile):null;hits=events=moves=null;playing=false;Repaint();}
        public void ShowAction(int index,float atFrame){mode=0;actionIndex=Mathf.Clamp(index,0,ActionNames.Length-1);frame=atFrame;hits=events=moves=null;Repaint();}
        public void ShowTransition(int index,float atSeconds){mode=1;transitionIndex=index;transitionTime=atSeconds;Repaint();}
        void Tick()
        {
            double now=EditorApplication.timeSinceStartup;float dt=Mathf.Min(.05f,(float)(now-lastTime));lastTime=now;
            if(!playing||!Profile)return;
            if(mode==0)
            {
                var beat=ActionBeat();if(beat==null||!beat.track)return;
                frame+=dt*rate*beat.speed*beat.track.framesPerSecond;
                if(frame>beat.End){frame=loop?beat.startFrame:beat.End;if(!loop)playing=false;}
            }
            else
            {
                if(actionSet?SharedRule()==null:Rule()==null)return;transitionTime+=dt*rate;
                if(transitionTime>Mathf.Max(.5f,TransitionBlend)+.65f){transitionTime=loop?-.5f:Mathf.Max(.5f,TransitionBlend)+.65f;if(!loop)playing=false;}
            }
            Repaint();
        }
        MotionBeat ActionBeat()
        {
            if(actionSet)return SharedAction()?.Motion;
            if(!tuning)return null;
            switch(actionIndex)
            {
                case 0:return tuning.headbutt.animation;case 1:return tuning.kick.animation;case 2:return tuning.attack.animation;case 3:return tuning.punch.animation;
                case 4:return tuning.wake;case 5:return tuning.feeding;case 6:return tuning.idleSlow;case 7:return tuning.idleRestless;
                case 8:return tuning.resonance;case 9:return tuning.agonizing;case 10:return tuning.scream;case 11:return tuning.walk;case 12:return tuning.run;
                case 13:return tuning.hit;case 14:return tuning.stumble;default:return tuning.death;
            }
        }
        AttackBeat ActionAttack()=>actionSet?(SharedAction()?.isAttack==true?SharedAction().action:null):actionIndex==0?tuning.headbutt:actionIndex==1?tuning.kick:actionIndex==2?tuning.attack:actionIndex==3?tuning.punch:null;
        ResonatorTransition Rule()=>tuning&&tuning.transitions!=null&&tuning.transitions.Length>0?tuning.transitions[Mathf.Clamp(transitionIndex,0,tuning.transitions.Length-1)]:null;
        SerializedProperty BeatProperty()
        {if(actionSet)return SharedActionProperty().FindPropertyRelative("action").FindPropertyRelative("animation");var p=data.FindProperty(Keys[actionIndex]);return actionIndex<4?p.FindPropertyRelative("animation"):p;}
        SerializedProperty AttackProperty()=>actionSet?SharedActionProperty().FindPropertyRelative("action"):data.FindProperty(Keys[actionIndex]);
        void OnGUI()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            var profile=EditorGUILayout.ObjectField(Profile,typeof(ScriptableObject),false,GUILayout.MinWidth(180));if(profile!=Profile){if(profile is ActionSet set)SetSharedProfile(set);else if(profile is ResonatorTuning legacy)SetProfile(legacy);}
            if(GUILayout.Button("拳套",EditorStyles.toolbarButton,GUILayout.Width(45)))SetSharedProfile(AssetDatabase.LoadAssetAtPath<ActionSet>("Assets/Game/Shared/Actions/Resources/GauntletActions.asset"));
            if(GUILayout.Button("共鸣者",EditorStyles.toolbarButton,GUILayout.Width(55)))SetProfile(AssetDatabase.LoadAssetAtPath<ResonatorTuning>("Assets/Game/Enemies/Resonator/AI/Data/Resonator_Default.asset"));
            if(GUILayout.Button("保存资产",EditorStyles.toolbarButton,GUILayout.Width(70))&&Profile){EditorUtility.SetDirty(Profile);AssetDatabase.SaveAssetIfDirty(Profile);}
            if(GUILayout.Button("重置视角",EditorStyles.toolbarButton,GUILayout.Width(70))){orbit=new Vector2(140,12);distance=4.5f;}
            EditorGUILayout.EndHorizontal();
            if(!Profile){EditorGUILayout.HelpBox("指定一个 Action Set 或 Resonator Tuning 资产。",MessageType.Info);return;}
            if(data==null||data.targetObject!=Profile)data=new SerializedObject(Profile);data.Update();
            if(actionSet&&(actionSet.actions==null||actionSet.actions.Length==0))
            {
                EditorGUILayout.PropertyField(data.FindProperty("previewModel"));EditorGUILayout.PropertyField(data.FindProperty("firstPersonPreview"));EditorGUILayout.PropertyField(data.FindProperty("sockets"),true);
                if(GUILayout.Button("创建第一个动作")){Undo.RecordObject(Profile,"Add first action");actionSet.actions=new[]{new ActionDefinition{id="Idle"}};EditorUtility.SetDirty(Profile);data.Update();}
                data.ApplyModifiedProperties();return;
            }
            int newMode=GUILayout.Toolbar(mode,new[]{"动作 · Hitbox · 逐帧事件","两段动画混合预览"});if(newMode!=mode){mode=newMode;playing=false;hits=events=moves=null;}
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.BeginVertical(GUILayout.Width(355));scroll=EditorGUILayout.BeginScrollView(scroll);
            if(mode==0)DrawActionControls();else DrawTransitionControls();
            EditorGUILayout.EndScrollView();EditorGUILayout.EndVertical();
            EditorGUILayout.BeginVertical();
            DrawPlayback();
            viewRect=GUILayoutUtility.GetRect(250,250,GUILayout.ExpandWidth(true),GUILayout.ExpandHeight(true));
            try{if(!previewSuspended){EnsurePreview();Sample();DrawPreview(viewRect);}}catch(Exception ex){GUI.Label(viewRect,"预览不可用："+ex.Message);}
            DrawTimeline();
            EditorGUILayout.EndVertical();EditorGUILayout.EndHorizontal();
            if(data.ApplyModifiedProperties())Repaint();
        }
        void DrawActionControls()
        {
            EditorGUI.BeginChangeCheck();int next=EditorGUILayout.Popup("动作",actionIndex,ActionNames);
            if(EditorGUI.EndChangeCheck()){actionIndex=next;frame=ActionBeat().startFrame;selectedHit=selectedEvent=0;hits=events=moves=null;playing=false;}
            if(actionSet)DrawSharedActionOptions();
            var beat=BeatProperty();EditorGUILayout.PropertyField(beat.FindPropertyRelative("track"));
            foreach(var field in new[]{"startFrame","endFrame","speed","loop","blend"})EditorGUILayout.PropertyField(beat.FindPropertyRelative(field));
            if(IsAttackAction)DrawMovementControls();
            EditorGUILayout.Space();EditorGUILayout.LabelField("命中盒 / 真实物理碰撞",EditorStyles.boldLabel);
            showInactive=EditorGUILayout.Toggle("显示未激活的盒体",showInactive);showTarget=EditorGUILayout.Toggle("显示玩家参考体",showTarget);
            previewTargetDistance=EditorGUILayout.Slider("玩家初始距离（米）",previewTargetDistance,.6f,5);
            previewRootMotion=EditorGUILayout.Toggle("预览完整位移（无碰撞）",previewRootMotion);
            if(IsAttackAction)
            {
                var array=AttackProperty().FindPropertyRelative("hits");
                if(hits==null||hits.serializedProperty.propertyPath!=array.propertyPath)hits=MakeList(array,true);
                hits.DoLayoutList();selectedHit=hits.index;
                if(selectedHit>=0&&selectedHit<array.arraySize)
                {
                    var p=array.GetArrayElementAtIndex(selectedHit);
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("label"),new GUIContent("名称"));EditorGUILayout.PropertyField(p.FindPropertyRelative("enabled"),new GUIContent("启用"));
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("hitGroup"),new GUIContent("命中组（可留空）","Once 模式同组只结算一次；Cooldown 模式同组共用目标冷却。"));
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("startFrame"),new GUIContent("开始帧"));EditorGUILayout.PropertyField(p.FindPropertyRelative("endFrame"),new GUIContent("结束帧（包含）"));
                    EditorGUILayout.BeginHorizontal();
                    if(GUILayout.Button("当前帧 → 开始"))p.FindPropertyRelative("startFrame").floatValue=frame;
                    if(GUILayout.Button("当前帧 → 结束"))p.FindPropertyRelative("endFrame").floatValue=frame;
                    if(GUILayout.Button("定位开始"))frame=p.FindPropertyRelative("startFrame").floatValue;
                    EditorGUILayout.EndHorizontal();
                    foreach(var item in new[]{("damage","伤害"),("shape","形状"),("anchor","跟随部位"),("customBone","自定义骨骼名称"),("position","位置偏移（米）"),("rotation","局部旋转（度）")})EditorGUILayout.PropertyField(p.FindPropertyRelative(item.Item1),new GUIContent(item.Item2));
                    DrawBonePicker(p);
                    foreach(var field in new[]{"repeat","sampleEveryFrames","damageCooldown","contactEffects","contactEffect","contactSound","contactEffectLifetime","effectCooldown","onlyWhileRotating","rotationAxis","rotationThreshold","blockable","knockbackDistance","knockbackPerDamage","knockbackDuration","blockedKnockbackMultiplier"})EditorGUILayout.PropertyField(p.FindPropertyRelative(field));
                    var shape=(HitboxShape)p.FindPropertyRelative("shape").enumValueIndex;
                    if(shape==HitboxShape.Box)EditorGUILayout.PropertyField(p.FindPropertyRelative("size"),new GUIContent("完整尺寸（米）"));
                    else{EditorGUILayout.PropertyField(p.FindPropertyRelative("radius"),new GUIContent("半径（米）"));if(shape==HitboxShape.Capsule)EditorGUILayout.PropertyField(p.FindPropertyRelative("height"),new GUIContent("完整高度（米）"));}
                    EditorGUILayout.PropertyField(p.FindPropertyRelative("targetLayers"),new GUIContent("目标碰撞层"));EditorGUILayout.PropertyField(p.FindPropertyRelative("minimumBlendWeight"),new GUIContent("目标动作最小混合权重"));
                    if(geometry!=null&&!geometry.HasAnchor((HitboxAnchor)p.FindPropertyRelative("anchor").enumValueIndex,p.FindPropertyRelative("customBone").stringValue))EditorGUILayout.HelpBox("找不到绑定骨骼，请检查 Custom Bone 名称。此盒体不会造成伤害。",MessageType.Warning);
                    EditorGUILayout.HelpBox("黄色中心点拖动位置；彩色点拖动尺寸，按住 Shift 拖动彩色点可绕对应轴旋转。尺寸以米计，Repeat 决定单次命中或重复接触规则。",MessageType.None);
                    if(p.FindPropertyRelative("endFrame").floatValue<p.FindPropertyRelative("startFrame").floatValue)EditorGUILayout.HelpBox("结束帧小于开始帧，这个盒体不会生效。",MessageType.Warning);
                }
            }
            else EditorGUILayout.HelpBox(actionSet?"将动作设为攻击后，可以编辑有效帧和命中盒。":"只有四个攻击动作会执行伤害检测；共鸣时的 Headbutt 不会伤害玩家。",MessageType.Info);
            EditorGUILayout.Space();EditorGUILayout.LabelField("逐帧事件",EditorStyles.boldLabel);
            var markers=beat.FindPropertyRelative("events");if(events==null||events.serializedProperty.propertyPath!=markers.propertyPath)events=MakeList(markers,false);
            events.DoLayoutList();selectedEvent=events.index;
            if(selectedEvent>=0&&selectedEvent<markers.arraySize)
            {
                var p=markers.GetArrayElementAtIndex(selectedEvent);
                foreach(var field in new[]{"name","frame","kind","payload","value","anchor","customBone","position","rotation","followAnchor"})EditorGUILayout.PropertyField(p.FindPropertyRelative(field));
                DrawBonePicker(p);
                var kind=(MotionEventKind)p.FindPropertyRelative("kind").enumValueIndex;
                if(kind!=MotionEventKind.Signal&&geometry!=null&&!geometry.HasAnchor((HitboxAnchor)p.FindPropertyRelative("anchor").enumValueIndex,p.FindPropertyRelative("customBone").stringValue))EditorGUILayout.HelpBox("找不到事件绑定的骨骼；声音或特效不会播放，请重新选择。",MessageType.Warning);
                if(kind==MotionEventKind.Sound)EditorGUILayout.PropertyField(p.FindPropertyRelative("sound"));
                if(kind==MotionEventKind.Effect){EditorGUILayout.PropertyField(p.FindPropertyRelative("effect"));EditorGUILayout.PropertyField(p.FindPropertyRelative("effectLifetime"));}
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("事件放在当前帧"))p.FindPropertyRelative("frame").floatValue=frame;
                if(GUILayout.Button("定位事件"))frame=p.FindPropertyRelative("frame").floatValue;EditorGUILayout.EndHorizontal();
            }
            EditorGUILayout.HelpBox("Signal：运行时触发敌人的 On Animation Event 和 C# FrameEventReceived。Sound / Effect：播放音效或生成特效。编辑预览只显示标记，不执行事件或造成伤害。",MessageType.Info);
        }
        ReorderableList MakeList(SerializedProperty array,bool isHit)
        {
            var list=new ReorderableList(data,array,true,true,true,true);list.index=0;
            list.drawHeaderCallback=r=>EditorGUI.LabelField(r,isHit?"Hitbox 轨道（可多段）":"事件轨道（+ 添加在当前帧）");
            list.drawElementCallback=(r,i,a,f)=>
            {
                if(i>=array.arraySize)return;var p=array.GetArrayElementAtIndex(i);
                string label=isHit?p.FindPropertyRelative("label").stringValue:p.FindPropertyRelative("name").stringValue;
                string timing=isHit?$"{p.FindPropertyRelative("startFrame").floatValue:0.#}–{p.FindPropertyRelative("endFrame").floatValue:0.#}f":$"{p.FindPropertyRelative("frame").floatValue:0.#}f";
                EditorGUI.LabelField(r,label+"   ["+timing+"]");
            };
            list.onAddCallback=l=>
            {
                data.ApplyModifiedProperties();Undo.RecordObject(Profile,isHit?"Add hitbox":"Add frame event");
                if(isHit){var attack=ActionAttack();var hit=new HitWindow{label="Hitbox "+(attack.hits.Length+1),startFrame=frame,endFrame=Mathf.Min(ActionBeat().End,frame+3),anchor=actionSet?HitboxAnchor.Root:actionIndex==0?HitboxAnchor.Head:actionIndex==1?HitboxAnchor.RightFoot:HitboxAnchor.RightHand,position=Vector3.zero};attack.hits=attack.hits.Concat(new[]{hit}).ToArray();l.index=attack.hits.Length-1;}
                else{var b=ActionBeat();b.events=(b.events??Array.Empty<MotionFrameEvent>()).Concat(new[]{new MotionFrameEvent{frame=frame}}).ToArray();l.index=b.events.Length-1;}
                EditorUtility.SetDirty(Profile);data.Update();Repaint();
            };
            return list;
        }
        void DrawTransitionControls()
        {
            if(actionSet){DrawSharedTransitionControls();return;}
            var rules=tuning.transitions;if(rules==null||rules.Length==0){EditorGUILayout.HelpBox("先在 Tuning 中添加衔接规则。",MessageType.Info);return;}
            transitionIndex=EditorGUILayout.Popup("衔接",Mathf.Clamp(transitionIndex,0,rules.Length-1),rules.Select(r=>r.label).ToArray());var rule=Rule();
            if(rule.anySource)anySource=(EnemyState)EditorGUILayout.EnumPopup("预览来源动作",anySource);
            var p=data.FindProperty("transitions").GetArrayElementAtIndex(transitionIndex);EditorGUILayout.PropertyField(p,true);
            bool completion=IsCompletion(rule);
            if(!completion)hitBranch=EditorGUILayout.Toggle("预览命中分支",hitBranch);
            if(!rule.automaticCombo&&!completion)sourceExitFrame=EditorGUILayout.FloatField("来源切出帧（仅预览）",sourceExitFrame);
            EditorGUILayout.HelpBox(completion?
                "这条规则已接入攻击收尾：Screaming 只在当前攻击命中后使用；Combat Idle 只在当前攻击打空后使用。收尾衔接帧 -1 为自动，非负数会至少等到指定帧。自动预览按首个命中窗口开始估算命中时机，实际游戏需真正发生碰撞；打空会等最后一个窗口关闭。Any Combat Idle 不会打断受击或死亡。":
                "蓝色为来源动作，绿色为目标动作。拖动底部时间轴可查看过渡前、混合中和过渡后。混合时长和目标起始帧修改会直接写入这条运行时衔接；普通状态的来源切出帧仅控制预览。",MessageType.Info);
            var from=tuning.BeatFor(rule.anySource?anySource:rule.from);var to=tuning.BeatFor(rule.to);
            if(from==null||to==null)EditorGUILayout.HelpBox("这条规则的状态没有可预览的动画。",MessageType.Warning);
            else EditorGUILayout.LabelField($"目标混合权重：{(transitionTime<0?0:rule.blend<=0?1:Mathf.Clamp01(transitionTime/rule.blend)):P0}");
        }
        bool IsCompletion(ResonatorTransition rule)=>ResonatorEnemy.IsAttack(rule.anySource?anySource:rule.from)&&(rule.to==EnemyState.Screaming||rule.to==EnemyState.CombatIdle);
        float PreviewExitFrame(ResonatorTransition rule,MotionBeat from)
        {
            if(IsCompletion(rule))
            {
                if(rule.exitFrame>=0)return rule.exitFrame;
                var attack=tuning.AttackFor(rule.anySource?anySource:rule.from);
                var windows=attack.hits?.Where(h=>h!=null&&h.enabled&&h.startFrame<=h.endFrame).ToArray();
                if(windows==null||windows.Length==0)return from.End;
                return rule.to==EnemyState.Screaming?windows.Min(h=>h.startFrame):windows.Max(h=>h.endFrame);
            }
            float exit=rule.automaticCombo?rule.CancelFrame(hitBranch):sourceExitFrame;return exit<0?from.End:exit;
        }
        void DrawPlayback()
        {
            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            if(GUILayout.Button(playing?"暂停":"播放",EditorStyles.toolbarButton,GUILayout.Width(48)))playing=!playing;
            if(GUILayout.Button("◀ 帧",EditorStyles.toolbarButton,GUILayout.Width(45)))Step(-1);
            if(GUILayout.Button("帧 ▶",EditorStyles.toolbarButton,GUILayout.Width(45)))Step(1);
            if(GUILayout.Button("重头",EditorStyles.toolbarButton,GUILayout.Width(40))){playing=false;if(mode==0)frame=ActionBeat().startFrame;else transitionTime=-.5f;}
            loop=GUILayout.Toggle(loop,"循环",EditorStyles.toolbarButton,GUILayout.Width(42));
            rate=EditorGUILayout.Slider(rate,.1f,2,GUILayout.MinWidth(80));GUILayout.Label(rate.ToString("0.00")+"x",GUILayout.Width(40));
            EditorGUILayout.EndHorizontal();
        }
        void Step(int direction){playing=false;if(mode==0)frame=Mathf.Clamp(frame+direction,0,ActionBeat().End);else transitionTime+=direction/30f;}
        void EnsurePreview()
        {
            if(preview!=null&&model)return;
            Cleanup();preview=new PreviewRenderUtility();preview.cameraFieldOfView=32;preview.ambientColor=new Color(.38f,.4f,.46f);
            preview.lights[0].intensity=1.6f;preview.lights[0].transform.rotation=Quaternion.Euler(35,25,0);preview.lights[1].intensity=1.4f;preview.lights[1].transform.rotation=Quaternion.Euler(315,215,0);
            var prefab=actionSet?actionSet.previewModel:AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/Prefabs/ResonatorScientist.prefab");
            var agentPrefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab");
            var agentVisual=agentPrefab?agentPrefab.transform.Find("Visual"):null;
            if(!actionSet&&agentVisual)prefab=agentVisual.gameObject;
            if(!prefab)throw new Exception("Missing Resonator visual prefab");
            model=Instantiate(prefab);model.name="Animation workbench isolated preview";model.hideFlags=HideFlags.HideAndDontSave;
            foreach(var b in model.GetComponentsInChildren<MonoBehaviour>(true))DestroyImmediate(b);
            foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;
            var animator=model.GetComponentInChildren<Animator>();animator.runtimeAnimatorController=null;animator.applyRootMotion=false;
            foreach(var renderer in model.GetComponentsInChildren<SkinnedMeshRenderer>())renderer.updateWhenOffscreen=true;
            previewRoot=new GameObject("Preview actor root"){hideFlags=HideFlags.HideAndDontSave};model.transform.SetParent(previewRoot.transform,false);
            preview.AddSingleGO(previewRoot);player=actionSet?(ActionMotionPlayer)animator.gameObject.AddComponent<ActionMotionPlayer>():model.AddComponent<ResonatorMotionPlayer>();player.animator=animator;player.hips=actionSet?null:model.GetComponentsInChildren<Transform>().First(t=>t.name=="ValveBiped.Bip01_Pelvis");
            geometry=new ResonatorHitbox(previewRoot.transform);
            if(actionSet)geometry.Bind(HitboxAnchor.Root,preview.camera.transform);
        }
        void Sample()
        {
            shownAttack=null;shownBeat=null;
            previewRoot.transform.position=Vector3.zero;
            if(mode==0)
            {
                shownBeat=ActionBeat();if(shownBeat==null||!shownBeat.track)return;frame=Mathf.Clamp(frame,0,shownBeat.End);shownFrame=frame;player.Play(shownBeat,frame,true);shownAttack=ActionAttack();
                ApplyMovementPreview();
            }
            else
            {
                if(actionSet){SampleSharedTransition();return;}
                var rule=Rule();if(rule==null)return;var from=tuning.BeatFor(rule.anySource?anySource:rule.from);var to=tuning.BeatFor(rule.to);if(from==null||to==null||!from.track||!to.track)return;
                float exit=PreviewExitFrame(rule,from);
                float entry=rule.entryFrame<0?to.startFrame:rule.entryFrame;
                if(transitionTime<0){shownBeat=from;shownFrame=Mathf.Clamp(exit+transitionTime*from.track.framesPerSecond*from.speed,0,from.End);player.Play(from,shownFrame,true);var source=rule.anySource?anySource:rule.from;if(ResonatorEnemy.IsAttack(source))shownAttack=tuning.AttackFor(source);}
                else{shownBeat=to;shownFrame=Mathf.Clamp(entry+transitionTime*to.track.framesPerSecond*to.speed,0,to.End);player.SampleBlend(from,Mathf.Clamp(exit+transitionTime*from.track.framesPerSecond*from.speed,0,from.End),to,shownFrame,rule.blend,transitionTime);if(ResonatorEnemy.IsAttack(rule.to))shownAttack=tuning.AttackFor(rule.to);}
            }
        }
        void CameraSetup(float aspect)
        {
            var camera=preview.camera;camera.aspect=aspect;
            if(actionSet){UpdateSharedPreviewBindings();if(firstPersonView){SetupFirstPersonCamera(camera);return;}}camera.fieldOfView=32;camera.nearClipPlane=.02f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.12f,.15f);
            var focus=new Vector3(0,.95f*(model?Mathf.Abs(model.transform.localScale.y):1),0);
            float cameraDistance=distance*(model?Mathf.Max(1,model.transform.localScale.magnitude/Mathf.Sqrt(3)):1);
            if(actionSet&&actionSet.previewDistance>0){focus=actionSet.previewFocus;cameraDistance=actionSet.previewDistance*distance/4.5f;}
            if(mode==0&&previewRootMotion&&previewRoot)
            {
                focus.z=showTarget?(previewRoot.transform.position.z+previewTargetDistance)*.5f:previewRoot.transform.position.z;
                if(showTarget)cameraDistance=Mathf.Max(distance,Mathf.Abs(previewTargetDistance-previewRoot.transform.position.z)*1.1f+3);
            }
            camera.transform.position=focus+Quaternion.Euler(orbit.y,orbit.x,0)*Vector3.back*cameraDistance;camera.transform.LookAt(focus);
        }
        void DrawPreview(Rect r)
        {
            CameraSetup(r.width/Mathf.Max(1,r.height));
            if(Event.current.type==EventType.Repaint)
            {
                preview.BeginPreview(r,GUIStyle.none);preview.Render(true);GUI.DrawTexture(r,preview.EndPreview(),ScaleMode.StretchToFill,false);
            }
            DrawOverlay(r);DrawManipulators(r);
            GUI.Label(new Rect(r.x+8,r.y+8,r.width-16,35),$"{(shownBeat?.track?shownBeat.track.clip.name:"")}   帧 {shownFrame:0.0}\n左键旋转视角 · 滚轮缩放 · 黄色点移动 · 彩色点改尺寸 / Shift+拖动改旋转",EditorStyles.whiteMiniLabel);
            var ev=Event.current;
            if(r.Contains(ev.mousePosition))
            {
                if(ev.type==EventType.ScrollWheel){distance=Mathf.Clamp(distance+ev.delta.y*.15f,1.3f,12);ev.Use();Repaint();}
                if(ev.type==EventType.MouseDrag&&ev.button==0&&GUIUtility.hotControl==0){orbit+=new Vector2(ev.delta.x,-ev.delta.y)*.5f;orbit.y=Mathf.Clamp(orbit.y,-75,75);ev.Use();Repaint();}
            }
        }
        Vector3 Project(Rect r,Vector3 point)
        {var p=preview.camera.WorldToViewportPoint(point);return new Vector3(r.x+p.x*r.width,r.y+(1-p.y)*r.height,p.z);}
        void Segment(Rect r,Vector3 a,Vector3 b,Color color)
        {
            var p=Project(r,a);var q=Project(r,b);if(p.z<=0||q.z<=0)return;Handles.color=color;Handles.DrawAAPolyLine(2,new Vector3(p.x,p.y,0),new Vector3(q.x,q.y,0));
        }
        void Ring(Rect r,Vector3 center,Vector3 a,Vector3 b,float radius,Color color)
        {for(int i=0;i<32;i++){float x=i*Mathf.PI*2/32,y=(i+1)*Mathf.PI*2/32;Segment(r,center+(a*Mathf.Cos(x)+b*Mathf.Sin(x))*radius,center+(a*Mathf.Cos(y)+b*Mathf.Sin(y))*radius,color);}}
        void Volume(Rect r,HitWindow h,HitboxPose p,Color color)
        {
            var right=p.rotation*Vector3.right;var up=p.rotation*Vector3.up;var forward=p.rotation*Vector3.forward;
            if(h.shape==HitboxShape.Box)
            {
                var v=new Vector3[8];for(int i=0;i<8;i++)v[i]=p.centre+p.rotation*Vector3.Scale(p.size*.5f,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));
                for(int i=0;i<8;i++)foreach(int bit in new[]{1,2,4})if((i&bit)==0)Segment(r,v[i],v[i|bit],color);
            }
            else
            {
                var d=h.shape==HitboxShape.Capsule?p.CapsuleOffset:Vector3.zero;
                foreach(var c in new[]{p.centre+d,p.centre-d}){Ring(r,c,right,up,p.radius,color);Ring(r,c,right,forward,p.radius,color);Ring(r,c,up,forward,p.radius,color);}
                if(h.shape==HitboxShape.Capsule)foreach(var side in new[]{right,-right,forward,-forward})Segment(r,p.centre+d+side*p.radius,p.centre-d+side*p.radius,color);
            }
        }
        void DrawOverlay(Rect r)
        {
            Handles.BeginGUI();var old=Handles.color;
            float floorY=actionSet&&actionSet.firstPersonPreview?-1.65f:0;
            for(int i=-3;i<=3;i++){Segment(r,new Vector3(i,floorY,-3),new Vector3(i,floorY,3),new Color(.3f,.35f,.4f,.45f));Segment(r,new Vector3(-3,floorY,i),new Vector3(3,floorY,i),new Color(.3f,.35f,.4f,.45f));}
            DrawMovementOverlay(r);
            if(showTarget)Volume(r,new HitWindow{shape=HitboxShape.Capsule},new HitboxPose(new Vector3(0,.9f+floorY,previewTargetDistance),Quaternion.identity,Vector3.one,.3f,1.8f),new Color(.3f,.8f,1,.7f));
            if(shownAttack!=null)for(int i=0;i<shownAttack.hits.Length;i++)
            {
                var h=shownAttack.hits[i];if(h==null||!h.enabled)continue;bool active=shownFrame>=h.startFrame&&shownFrame<=h.endFrame&&player.DestinationWeight>=h.minimumBlendWeight;
                if(!active&&!showInactive)continue;var p=geometry.Pose(h);Volume(r,h,p,active?new Color(1,.2f,.15f):new Color(.65f,.68f,.72f,.65f));
                var point=Project(r,p.centre);if(point.z>0)GUI.Label(new Rect(point.x+9,point.y,200,20),h.label+(active?" [生效]":" [关闭]"),EditorStyles.whiteMiniLabel);
            }
            if(shownBeat?.events!=null)foreach(var marker in shownBeat.events)if(Mathf.Abs(shownFrame-marker.frame)<1){var p=Project(r,geometry.Point(marker.anchor,marker.customBone,marker.position));GUI.Label(new Rect(p.x,p.y,220,22),"◆ "+marker.name,EditorStyles.whiteBoldLabel);}
            Handles.color=old;Handles.EndGUI();
        }
        void DrawManipulators(Rect r)
        {
            if(mode!=0||!IsAttackAction||shownAttack==null||selectedHit<0||selectedHit>=shownAttack.hits.Length)return;
            var h=shownAttack.hits[selectedHit];if(h==null)return;var pose=geometry.Pose(h);var p=Project(r,pose.centre);
            var anchor=geometry.Anchor(h.anchor,h.customBone);if(!anchor)return;var ev=Event.current;
            int id=GUIUtility.GetControlID(FocusType.Passive);var centerRect=new Rect(p.x-6,p.y-6,12,12);
            if(p.z>0)EditorGUI.DrawRect(centerRect,Color.yellow);
            var axes=new[]{pose.rotation*Vector3.right,pose.rotation*Vector3.up,pose.rotation*Vector3.forward};
            var ends=new Vector3[3];
            for(int i=0;i<3;i++)
            {
                float extent=h.shape==HitboxShape.Box?pose.size[i]*.5f:h.shape==HitboxShape.Capsule&&i==1?pose.height*.5f:pose.radius;
                ends[i]=Project(r,pose.centre+axes[i]*extent);if(ends[i].z>0)EditorGUI.DrawRect(new Rect(ends[i].x-4,ends[i].y-4,8,8),i==0?Color.red:i==1?Color.green:Color.cyan);
            }
            if(ev.type==EventType.MouseDown&&ev.button==0&&r.Contains(ev.mousePosition))
            {
                int kind=centerRect.Contains(ev.mousePosition)?0:-1;
                if(kind<0)for(int i=0;i<3;i++)if(new Rect(ends[i].x-7,ends[i].y-7,14,14).Contains(ev.mousePosition)){kind=i+1;break;}
                if(kind>=0){Undo.RecordObject(Profile,"Move / resize / rotate hitbox");GUIUtility.hotControl=id;dragKind=kind>0&&ev.shift?kind+3:kind;dragPoint=pose.centre;dragOffset=h.position;playing=false;ev.Use();}
            }
            if(GUIUtility.hotControl==id)
            {
                if(ev.type==EventType.MouseDrag)
                {
                    if(dragKind==0)
                    {
                        var plane=new Plane(preview.camera.transform.forward,dragPoint);var ray=preview.camera.ViewportPointToRay(new Vector3((ev.mousePosition.x-r.x)/r.width,1-(ev.mousePosition.y-r.y)/r.height,0));
                        if(plane.Raycast(ray,out float enter))h.position=dragOffset+Quaternion.Inverse(anchor.rotation)*(ray.GetPoint(enter)-dragPoint);
                    }
                    else if(dragKind>=4)
                    {
                        var localAxis=dragKind==4?Vector3.right:dragKind==5?Vector3.up:Vector3.forward;
                        h.rotation=(Quaternion.Euler(h.rotation)*Quaternion.AngleAxis((ev.delta.x-ev.delta.y)*.7f,localAxis)).eulerAngles;
                    }
                    else
                    {
                        int axis=dragKind-1;var screen=Project(r,pose.centre+axes[axis])-Project(r,pose.centre);var direction=new Vector2(screen.x,screen.y);
                        float delta=Vector2.Dot(ev.delta,direction.normalized)/Mathf.Max(1,direction.magnitude);
                        if(h.shape==HitboxShape.Box){var size=h.size;size[axis]=Mathf.Max(.01f,size[axis]+delta*2);h.size=size;}
                        else if(h.shape==HitboxShape.Capsule&&axis==1)h.height=Mathf.Max(h.radius*2,h.height+delta*2);
                        else h.radius=Mathf.Max(.005f,h.radius+delta);
                    }
                    EditorUtility.SetDirty(Profile);data.Update();ev.Use();Repaint();
                }
                if(ev.type==EventType.MouseUp){GUIUtility.hotControl=0;dragKind=-1;ev.Use();}
            }
        }
        void DrawTimeline()
        {
            if(mode==1)
            {
                if(actionSet?SharedRule()==null:Rule()==null)return;transitionTime=EditorGUILayout.Slider("过渡时间（秒）",transitionTime,-.5f,Mathf.Max(.5f,TransitionBlend)+.65f);
                var r=GUILayoutUtility.GetRect(200,55,GUILayout.ExpandWidth(true));EditorGUI.DrawRect(r,new Color(.09f,.1f,.13f));
                float span=Mathf.Max(.5f,TransitionBlend)+1.15f;float zero=r.x+.5f/span*r.width;float end=zero+TransitionBlend/span*r.width;
                EditorGUI.DrawRect(new Rect(r.x,r.y+6,Mathf.Max(0,end-r.x),17),new Color(.2f,.4f,.65f));EditorGUI.DrawRect(new Rect(zero,r.y+28,r.xMax-zero,17),new Color(.2f,.6f,.4f));
                GUI.Label(new Rect(r.x+5,r.y+6,300,18),"来源 → 淡出",EditorStyles.whiteMiniLabel);GUI.Label(new Rect(zero+5,r.y+28,300,18),"目标 → 淡入",EditorStyles.whiteMiniLabel);
                float x=r.x+(transitionTime+.5f)/span*r.width;EditorGUI.DrawRect(new Rect(x,r.y,2,r.height),Color.white);return;
            }
            var beat=ActionBeat();if(beat?.track==null)return;frame=EditorGUILayout.Slider("源动画帧",frame,0,beat.End);
            int count=ActionAttack()?.hits?.Length??0;var rect=GUILayoutUtility.GetRect(200,Mathf.Max(100,52+(count+MovementRows())*21),GUILayout.ExpandWidth(true));EditorGUI.DrawRect(rect,new Color(.08f,.09f,.11f));
            float left=rect.x+7,width=rect.width-14;Func<float,float> at=f=>left+Mathf.Clamp01(f/Mathf.Max(1,beat.End))*width;
            int increment=beat.End>250?50:10;for(int f=0;f<=beat.End;f+=increment){float x=at(f);EditorGUI.DrawRect(new Rect(x,rect.y,1,rect.height),new Color(.22f,.24f,.28f));GUI.Label(new Rect(x+2,rect.y,50,18),f.ToString(),EditorStyles.whiteMiniLabel);}
            var attack=ActionAttack();if(attack!=null)for(int i=0;i<count;i++){var h=attack.hits[i];var r=new Rect(at(h.startFrame),rect.y+23+i*21,Mathf.Max(4,at(h.endFrame)-at(h.startFrame)),17);EditorGUI.DrawRect(r,i==selectedHit?new Color(.9f,.25f,.15f):new Color(.55f,.2f,.18f));GUI.Label(r,h.label,EditorStyles.whiteMiniLabel);}
            DrawMovementTimeline(rect,count,at);
            if(actionSet)DrawSharedCancelMarkers(rect,at);
            if(beat.events!=null)foreach(var e in beat.events){float x=at(e.frame);EditorGUI.DrawRect(new Rect(x,rect.yMax-22,2,18),Color.yellow);GUI.Label(new Rect(x+3,rect.yMax-22,150,18),e.name,EditorStyles.whiteMiniLabel);}
            EditorGUI.DrawRect(new Rect(at(frame),rect.y,2,rect.height),Color.white);
            var ev=Event.current;int id=GUIUtility.GetControlID(FocusType.Passive);
            if(ev.type==EventType.MouseDown&&rect.Contains(ev.mousePosition)&&ev.button==0){GUIUtility.hotControl=id;playing=false;}
            if(GUIUtility.hotControl==id&&(ev.type==EventType.MouseDown||ev.type==EventType.MouseDrag)){frame=Mathf.Clamp01((ev.mousePosition.x-left)/width)*beat.End;ev.Use();Repaint();}
            if(GUIUtility.hotControl==id&&ev.type==EventType.MouseUp){GUIUtility.hotControl=0;ev.Use();}
        }
        public void SaveModelPreview(string path)
        {
            EnsurePreview();Sample();CameraSetup(1.25f);preview.BeginStaticPreview(new Rect(0,0,1000,800));preview.Render(true);var texture=preview.EndStaticPreview();System.IO.File.WriteAllBytes(path,texture.EncodeToPNG());DestroyImmediate(texture);
        }
    }
}
