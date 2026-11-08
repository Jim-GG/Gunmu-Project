using System;
using System.Linq;
using UnityEditor;
using UnityEngine;
using GameActions;
namespace Resonator.AI.Editor
{
    // Extends the existing workbench; hitbox handles, timeline, events, curves and
    // pose mixing are shared with the legacy enemy adapter, not a second tool.
    public sealed partial class ResonatorAnimationWorkbench
    {
        [SerializeField] int sharedSourceIndex;
        [SerializeField] bool firstPersonView=true;
        Transform previewAim;
        public static ResonatorAnimationWorkbench OpenShared(ActionSet set)
        {
            var w=GetWindow<ResonatorAnimationWorkbench>();w.titleContent=new GUIContent("动作工作台");w.minSize=new Vector2(930,680);w.SetSharedProfile(set);w.Show();return w;
        }
        void SetSharedProfile(ActionSet set)
        {
            Cleanup();tuning=null;actionSet=set;data=set?new SerializedObject(set):null;
            actionIndex=transitionIndex=sharedSourceIndex=0;mode=0;frame=0;hits=events=moves=null;playing=false;firstPersonView=set&&set.firstPersonPreview;Repaint();
        }
        ActionDefinition SharedAction()=>actionSet&&actionSet.actions!=null&&actionSet.actions.Length>0?actionSet.actions[Mathf.Clamp(actionIndex,0,actionSet.actions.Length-1)]:null;
        SerializedProperty SharedActionProperty()=>data.FindProperty("actions").GetArrayElementAtIndex(Mathf.Clamp(actionIndex,0,actionSet.actions.Length-1));
        ActionTransition SharedRule()=>actionSet&&actionSet.transitions!=null&&actionSet.transitions.Length>0?actionSet.transitions[Mathf.Clamp(transitionIndex,0,actionSet.transitions.Length-1)]:null;
        void DrawSharedActionOptions()
        {
            var p=SharedActionProperty();EditorGUILayout.PropertyField(p.FindPropertyRelative("id"),new GUIContent("动作 ID"));EditorGUILayout.PropertyField(p.FindPropertyRelative("isAttack"),new GUIContent("攻击动作"));
            firstPersonView=EditorGUILayout.Toggle("第一人称预览",firstPersonView);
            EditorGUILayout.PropertyField(data.FindProperty("inputBufferSeconds"),new GUIContent("预输入保留时间（秒）"));
            if(SharedAction()?.isAttack==true)
            {
                foreach(var field in new[]{("acquisitionRange","吸附选敌范围（米）"),("acquisitionHalfAngle","吸附前方半角（度）"),("maxHeightDifference","最大目标高差（米）"),("groundedAssistanceOnly","仅落地时吸附")})EditorGUILayout.PropertyField(p.FindPropertyRelative(field.Item1),new GUIContent(field.Item2));
                EditorGUILayout.PropertyField(data.FindProperty("minimumSeparation"),new GUIContent("最小中心间距（米）"));
                EditorGUILayout.HelpBox("玩家只在命中盒有效帧内执行位移轨道；需有前方可见目标，受每段距离、速度、接触间距和场景碰撞限制。不会转动镜头。预览为直线预算，游戏内还会检查目标和碰撞。",MessageType.None);
            }
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("添加动作"))
            {
                data.ApplyModifiedProperties();Undo.RecordObject(Profile,"Add action");
                actionSet.actions=actionSet.actions.Concat(new[]{new ActionDefinition{id="Action "+actionSet.actions.Length}}).ToArray();actionIndex=actionSet.actions.Length-1;hits=events=moves=null;data.Update();EditorUtility.SetDirty(Profile);
            }
            if(GUILayout.Button("Inspector 全部配置"))Selection.activeObject=Profile;
            EditorGUILayout.EndHorizontal();
        }
        void DrawSharedTransitionControls()
        {
            var rules=actionSet.transitions;
            EditorGUILayout.BeginHorizontal();
            if(GUILayout.Button("添加衔接"))
            {
                data.ApplyModifiedProperties();Undo.RecordObject(Profile,"Add transition");
                actionSet.transitions=(rules??Array.Empty<ActionTransition>()).Concat(new[]{new ActionTransition{from=SharedAction()?.id,to="Idle",cancelFrame=0}}).ToArray();transitionIndex=actionSet.transitions.Length-1;data.Update();EditorUtility.SetDirty(Profile);rules=actionSet.transitions;
            }
            if(rules!=null&&rules.Length>0&&GUILayout.Button("删除衔接"))
            {
                data.ApplyModifiedProperties();Undo.RecordObject(Profile,"Delete transition");actionSet.transitions=rules.Where((r,i)=>i!=transitionIndex).ToArray();transitionIndex=Mathf.Max(0,transitionIndex-1);data.Update();EditorUtility.SetDirty(Profile);rules=actionSet.transitions;
            }
            EditorGUILayout.EndHorizontal();
            if(rules==null||rules.Length==0)return;
            transitionIndex=EditorGUILayout.Popup("衔接",Mathf.Clamp(transitionIndex,0,rules.Length-1),rules.Select(r=>string.IsNullOrEmpty(r.label)?r.from+" → "+r.to:r.label).ToArray());
            var rule=SharedRule();var p=data.FindProperty("transitions").GetArrayElementAtIndex(transitionIndex);
            EditorGUILayout.PropertyField(p.FindPropertyRelative("label"),new GUIContent("名称"));EditorGUILayout.PropertyField(p.FindPropertyRelative("enabled"),new GUIContent("启用"));EditorGUILayout.PropertyField(p.FindPropertyRelative("anySource"),new GUIContent("任意来源"));
            var names=ActionNames;
            if(rule.anySource)sharedSourceIndex=EditorGUILayout.Popup("预览来源",Mathf.Clamp(sharedSourceIndex,0,names.Length-1),names);
            else DrawActionId(p.FindPropertyRelative("from"),"来源动作",names);
            DrawActionId(p.FindPropertyRelative("to"),"目标动作",names);
            foreach(var field in new[]{("blend","混合时长（秒）"),("entryFrame","目标切入帧（-1 默认）"),("cancelFrame","可取消开始帧（-1 动作结束）"),("cancelEndFrame","可取消结束帧（-1 动作结束）"),("bufferStartFrame","可预输入开始帧（-1 不限制）")})EditorGUILayout.PropertyField(p.FindPropertyRelative(field.Item1),new GUIContent(field.Item2));
            EditorGUILayout.HelpBox("帧数属于来源动画。预输入只有在保留时间内遇到合法取消窗口才执行；短按松开也会被记住。相同来源/目标的精确规则优先于 Any。格挡松开和受击属于强制切换，使用混合配置但不等待输入取消帧。",MessageType.Info);
            firstPersonView=EditorGUILayout.Toggle("第一人称预览",firstPersonView);
        }
        static void DrawActionId(SerializedProperty p,string label,string[] names)
        {
            int index=Array.IndexOf(names,p.stringValue);int next=EditorGUILayout.Popup(label,index,names);if(next>=0&&next<names.Length)p.stringValue=names[next];
        }
        void SampleSharedTransition()
        {
            var rule=SharedRule();if(rule==null)return;
            var from=actionSet.Find(rule.anySource?ActionNames[Mathf.Clamp(sharedSourceIndex,0,ActionNames.Length-1)]:rule.from);var to=actionSet.Find(rule.to);
            if(from?.Motion.track==null||to?.Motion.track==null)return;
            float exit=rule.cancelFrame<0?from.Motion.End:rule.cancelFrame,entry=rule.entryFrame<0?to.Motion.startFrame:rule.entryFrame;
            if(transitionTime<0)
            {
                shownBeat=from.Motion;shownFrame=Mathf.Clamp(exit+transitionTime*shownBeat.track.framesPerSecond*shownBeat.speed,shownBeat.startFrame,shownBeat.End);
                player.Play(shownBeat,shownFrame,true);shownAttack=from.isAttack?from.action:null;
            }
            else
            {
                shownBeat=to.Motion;shownFrame=Mathf.Clamp(entry+transitionTime*shownBeat.track.framesPerSecond*shownBeat.speed,shownBeat.startFrame,shownBeat.End);
                player.SampleBlend(from.Motion,Mathf.Clamp(exit+transitionTime*from.Motion.track.framesPerSecond*from.Motion.speed,from.Motion.startFrame,from.Motion.End),to.Motion,shownFrame,rule.blend,transitionTime);shownAttack=to.isAttack?to.action:null;
            }
        }
        void UpdateSharedPreviewBindings()
        {
            if(!previewAim){var go=new GameObject("Preview aim origin");go.transform.SetParent(previewRoot.transform,false);previewAim=go.transform;}
            var anchor=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name=="ViewCameraAnchor");
            if(anchor)previewAim.SetPositionAndRotation(anchor.position,Quaternion.LookRotation(-anchor.forward,anchor.up));
            else previewAim.SetPositionAndRotation(previewRoot.transform.position+Vector3.up*1.65f,previewRoot.transform.rotation);
            geometry.Bind(HitboxAnchor.Root,actionSet.firstPersonPreview?previewAim:model.transform);
            foreach(var t in model.GetComponentsInChildren<Transform>(true))
            {if(t.name=="hand_L.R")geometry.Bind(HitboxAnchor.RightHand,t);if(t.name=="hand_L.L")geometry.Bind(HitboxAnchor.LeftHand,t);}
            geometry.BindProfile(actionSet);
        }
        void SetupFirstPersonCamera(Camera camera)
        {
            var bone=model.GetComponentsInChildren<Transform>(true).FirstOrDefault(t=>t.name==actionSet.previewCameraBone);
            if(bone)camera.transform.SetPositionAndRotation(bone.position,Quaternion.LookRotation(-bone.forward,bone.up));
            else camera.transform.SetPositionAndRotation(previewAim.position,previewAim.rotation);
            camera.fieldOfView=actionSet.previewFieldOfView;camera.nearClipPlane=.01f;camera.farClipPlane=100;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=new Color(.10f,.12f,.15f);
        }
        void DrawSharedCancelMarkers(Rect rect,Func<float,float> at)
        {
            if(actionSet.transitions==null||SharedAction()==null)return;
            foreach(var r in actionSet.transitions)if(r.enabled&&!r.anySource&&r.from==SharedAction().id)
            {
                float x=at(r.cancelFrame<0?ActionBeat().End:r.cancelFrame);
                EditorGUI.DrawRect(new Rect(x,rect.y+20,2,rect.height-40),Color.green);
                GUI.Label(new Rect(x+3,rect.y+21,200,18),"可接 "+r.to,EditorStyles.whiteMiniLabel);
            }
        }
    }
}
