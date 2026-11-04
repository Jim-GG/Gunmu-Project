using UnityEditor;
using UnityEngine;
namespace Resonator.AI.Editor
{
    [CustomPropertyDrawer(typeof(ResonatorTransition))]
    public sealed class ResonatorTransitionDrawer:PropertyDrawer
    {
        static readonly string[] Fields={"label","enabled","anySource","from","to","blend","entryFrame","exitFrame","automaticCombo","cancelFrameOnHit","cancelFrameOnMiss"};
        static readonly GUIContent[] Labels={
            new GUIContent("备注"),new GUIContent("启用"),new GUIContent("任意来源状态"),new GUIContent("来源状态"),new GUIContent("目标状态"),
            new GUIContent("混合时长（秒）","仅覆盖这条衔接；0 为直接切换。"),
            new GUIContent("目标起始帧","目标动画的帧号，-1 使用动作默认起始帧。"),
            new GUIContent("收尾衔接帧（-1 自动）","攻击→Screaming/CombatIdle 的实际切出帧。-1：命中后立即衔接；打空则等最后一个命中窗口关闭。非负数：至少等到指定源动画帧，再按命中结果衔接。"),
            new GUIContent("自动连招取消","开启后到达取消帧便自动衔接；关闭后可用 TryCancelAttack 手动请求。原有踢击收尾必接 Attack 规则仍保留。"),
            new GUIContent("命中后取消帧","来源动作的帧号；-1 等待动作结束。"),
            new GUIContent("未命中取消帧","来源动作的帧号；-1 等待动作结束。")};
        public override float GetPropertyHeight(SerializedProperty p,GUIContent label)=>(EditorGUIUtility.singleLineHeight+2)*(p.isExpanded?Fields.Length+1:1);
        public override void OnGUI(Rect rect,SerializedProperty p,GUIContent label)
        {
            EditorGUI.BeginProperty(rect,label,p);float h=EditorGUIUtility.singleLineHeight;rect.height=h;
            var title=p.FindPropertyRelative("label").stringValue;
            p.isExpanded=EditorGUI.Foldout(rect,p.isExpanded,string.IsNullOrEmpty(title)?label.text:title,true);
            if(p.isExpanded){EditorGUI.indentLevel++;for(int i=0;i<Fields.Length;i++){rect.y+=h+2;EditorGUI.PropertyField(rect,p.FindPropertyRelative(Fields[i]),Labels[i]);}EditorGUI.indentLevel--;}
            EditorGUI.EndProperty();
        }
    }
    [CustomEditor(typeof(ResonatorTuning))]
    public sealed class ResonatorTuningInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if(GUILayout.Button("打开动画工作台：混合预览 / Hitbox / 逐帧事件"))ResonatorAnimationWorkbench.Open((ResonatorTuning)target);
            EditorGUILayout.HelpBox("动作本身：修改各 Animation Beat 的 Start / End Frame、Speed、Blend。\n单独衔接：修改下方 Transitions 的混合秒数、目标起始帧、命中/未命中取消帧。精确 From→To 优先于 Any Source。帧号按 Track 的 FPS，通常为 30。\n运行时可以即时调试；退出播放前记下需要保留的值。建议先复制 Tuning 资产作为变体。",MessageType.Info);
            DrawDefaultInspector();
        }
    }
}
