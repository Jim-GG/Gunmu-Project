using System;
using System.Linq;
using UnityEditor;
using UnityEditorInternal;
using UnityEngine;
namespace Resonator.AI.Editor
{
    public sealed partial class ResonatorAnimationWorkbench
    {
        [SerializeField] float previewTargetDistance=1.05f;
        [SerializeField] bool movementExpanded=true;
        ReorderableList moves;
        static EnemyState ActionState(int index)=>index==0?EnemyState.Headbutt:index==1?EnemyState.Kicking:index==2?EnemyState.Attack:EnemyState.Punching;
        void DrawMovementControls()
        {
            var attackProperty=data.FindProperty(Keys[actionIndex]);
            EditorGUILayout.PropertyField(attackProperty.FindPropertyRelative("startDistance"),new GUIContent("AI 最大起手距离（米）","0 使用全局 Attack Enter Distance。AI 会结合玩家后退速度和前摇时间决定起手时机。"));
            if(actionIndex==2)EditorGUILayout.HelpBox("Attack 继续遵循 Kicking 后派生的规则，不会从普通状态单独起手。",MessageType.None);
            EditorGUILayout.Space();movementExpanded=EditorGUILayout.Foldout(movementExpanded,"攻击位移与追踪",true);
            if(!movementExpanded)return;
            var property=data.FindProperty(Keys[actionIndex]).FindPropertyRelative("displacement");
            EditorGUILayout.PropertyField(property.FindPropertyRelative("enabled"),new GUIContent("启用距离适配位移"));
            if(GUILayout.Button("填入当前动作的位移预设"))
            {
                data.ApplyModifiedProperties();Undo.RecordObject(tuning,"Apply attack displacement preset");
                var attack=ActionAttack();attack.displacement=AttackDisplacement.Preset(ActionState(actionIndex),attack);
                previewRootMotion=true;EditorUtility.SetDirty(tuning);data.Update();moves=null;
            }
            if(!property.FindPropertyRelative("enabled").boolValue){EditorGUILayout.HelpBox("添加位移段或应用预设，按目标距离补足踏步、前滑、逼近或前冲。关闭时恢复原动画根位移。",MessageType.None);return;}
            EditorGUILayout.PropertyField(property.FindPropertyRelative("includeAuthoredRootMotion"),new GUIContent("叠加原动画根位移","默认关闭，避免原动作后撤抵消新加的前进。不会修改源 FBX。"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("stopOnHit"),new GUIContent("命中后停止额外前推"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("trackTarget"),new GUIContent("前摇追踪玩家"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("trackingStartFrame"),new GUIContent("追踪开始帧"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("trackingEndFrame"),new GUIContent("锁定方向帧"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("turnSpeed"),new GUIContent("转向速度（度/秒）"));
            EditorGUILayout.PropertyField(property.FindPropertyRelative("maxTrackingAngle"),new GUIContent("允许追踪的前方角度"));
            var array=property.FindPropertyRelative("segments");
            if(moves==null||moves.serializedProperty.propertyPath!=array.propertyPath)
            {
                moves=new ReorderableList(data,array,true,true,true,true);moves.index=0;
                moves.drawHeaderCallback=r=>EditorGUI.LabelField(r,"位移轨道（+ 添加一段）");
                moves.drawElementCallback=(r,i,a,f)=>{if(i>=array.arraySize)return;var p=array.GetArrayElementAtIndex(i);EditorGUI.LabelField(r,$"{p.FindPropertyRelative("label").stringValue}  [{p.FindPropertyRelative("startFrame").floatValue:0.#}–{p.FindPropertyRelative("endFrame").floatValue:0.#}f]");};
                moves.onAddCallback=l=>
                {
                    data.ApplyModifiedProperties();Undo.RecordObject(tuning,"Add displacement segment");var d=ActionAttack().displacement;
                    d.segments=(d.segments??Array.Empty<AttackMoveSegment>()).Concat(new[]{new AttackMoveSegment{startFrame=frame,endFrame=frame+10}}).ToArray();
                    l.index=d.segments.Length-1;previewRootMotion=true;EditorUtility.SetDirty(tuning);data.Update();Repaint();
                };
            }
            moves.DoLayoutList();
            if(moves.index>=0&&moves.index<array.arraySize)
            {
                var p=array.GetArrayElementAtIndex(moves.index);
                foreach(var item in new[]{("label","位移段名称"),("enabled","启用"),("startFrame","开始帧"),("endFrame","结束帧"),("maxDistance","最大补足距离（米）"),("maxSpeed","最大速度（米/秒）"),("contactDistance","期望接触间距（米）"),("progress","累计位移进度（0–1）")})EditorGUILayout.PropertyField(p.FindPropertyRelative(item.Item1),new GUIContent(item.Item2));
                EditorGUILayout.BeginHorizontal();if(GUILayout.Button("当前帧 → 开始"))p.FindPropertyRelative("startFrame").floatValue=frame;if(GUILayout.Button("当前帧 → 结束"))p.FindPropertyRelative("endFrame").floatValue=frame;EditorGUILayout.EndHorizontal();
                if(p.FindPropertyRelative("endFrame").floatValue<=p.FindPropertyRelative("startFrame").floatValue)EditorGUILayout.HelpBox("结束帧需要大于开始帧，否则此段不执行。",MessageType.Warning);
            }
            EditorGUILayout.HelpBox("曲线表示累计前进进度：斜率越陡，移动越快。已经到达接触间距就不再前推；最大距离和速度是上限。追踪窗口结束后朝原先锁定的位置移动，玩家仍能闪开。重叠位移段按列表顺序，第一条生效。启用此轨道后使用这里的转向窗口。",MessageType.Info);
        }
        void ApplyMovementPreview()
        {
            if(!previewRootMotion||shownBeat?.track==null)return;
            Vector3 authored=shownBeat.track.Sample(shownFrame/shownBeat.track.framesPerSecond)-shownBeat.track.Sample(shownBeat.StartSeconds);authored.y=0;
            if(shownAttack==null){previewRoot.transform.position=authored;return;}
            var d=shownAttack.displacement;
            if(d==null||!d.enabled){previewRoot.transform.position=authored*shownAttack.rootMotionScale;return;}
            float extra=AttackMoveBudget.Preview(shownAttack,shownBeat.startFrame,shownFrame,previewTargetDistance,tuning.minimumSeparation);
            previewRoot.transform.position=(d.includeAuthoredRootMotion?authored*shownAttack.rootMotionScale:Vector3.zero)+Vector3.forward*extra;
        }
        int MovementRows()=>actionIndex<4&&ActionAttack()?.displacement?.enabled==true?1+(ActionAttack().displacement.segments?.Length??0):0;
        void DrawMovementTimeline(Rect rect,int hitRows,Func<float,float> at)
        {
            if(MovementRows()==0)return;var d=ActionAttack().displacement;float y=rect.y+23+hitRows*21;
            var tracking=new Rect(at(d.trackingStartFrame),y,Mathf.Max(3,at(d.trackingEndFrame)-at(d.trackingStartFrame)),17);
            EditorGUI.DrawRect(tracking,new Color(.15f,.45f,.55f));GUI.Label(tracking,"追踪 → 锁定",EditorStyles.whiteMiniLabel);y+=21;
            if(d.segments!=null)for(int i=0;i<d.segments.Length;i++)
            {
                var s=d.segments[i];if(s==null)continue;var r=new Rect(at(s.startFrame),y+i*21,Mathf.Max(3,at(s.endFrame)-at(s.startFrame)),17);
                EditorGUI.DrawRect(r,s.enabled?new Color(.5f,.28f,.7f):Color.gray);GUI.Label(r,s.label,EditorStyles.whiteMiniLabel);
            }
        }
        void DrawMovementOverlay(Rect r)
        {
            if(mode!=0||shownAttack?.displacement?.enabled!=true)return;
            float entry=shownBeat.startFrame,end=shownBeat.End;
            var orange=new Color(1,.65f,.18f,.8f);float previous=0;
            for(int i=1;i<=12;i++)
            {
                float at=Mathf.Lerp(entry,end,i/12f);float next=AttackMoveBudget.Preview(shownAttack,entry,at,previewTargetDistance,tuning.minimumSeparation);
                Segment(r,new Vector3(0,.04f,previous),new Vector3(0,.04f,next),orange);previous=next;
            }
            float progress=AttackMoveBudget.Preview(shownAttack,entry,shownFrame,previewTargetDistance,tuning.minimumSeparation);
            Ring(r,new Vector3(0,.04f,progress),Vector3.right,Vector3.forward,.12f,orange);
            GUI.Label(new Rect(r.x+8,r.y+46,r.width-16,22),$"补足位移 {progress:0.00}m  ·  目标初始距离 {previewTargetDistance:0.00}m  ·  {(shownFrame>=shownAttack.displacement.trackingEndFrame?"方向已锁定":"前摇追踪中")}",EditorStyles.whiteMiniLabel);
        }
    }
}
