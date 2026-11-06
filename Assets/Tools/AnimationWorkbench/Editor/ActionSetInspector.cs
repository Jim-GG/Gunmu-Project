using System.Linq;
using UnityEditor;
using UnityEngine;
namespace GameActions.Editor
{
    [CustomEditor(typeof(ActionSet))]
    public sealed class ActionSetInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            if(GUILayout.Button("打开共用动作工作台"))Resonator.AI.Editor.ResonatorAnimationWorkbench.OpenShared((ActionSet)target);
            DrawDefaultInspector();var profile=(ActionSet)target;
            if(profile.actions!=null&&profile.actions.Where(a=>a!=null).GroupBy(a=>a.id).Any(g=>string.IsNullOrEmpty(g.Key)||g.Count()>1))EditorGUILayout.HelpBox("动作 ID 需要唯一且非空，运行时按 ID 查找。",MessageType.Warning);
        }
    }
    [CustomEditor(typeof(FirstPerson.GauntletCombat))]
    public sealed class GauntletCombatInspector : UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            var combat=(FirstPerson.GauntletCombat)target;
            if(GUILayout.Button("在共用工作台调整拳套"))Resonator.AI.Editor.ResonatorAnimationWorkbench.OpenShared(combat.profile?combat.profile:AssetDatabase.LoadAssetAtPath<ActionSet>(GauntletActionsSetup.ProfilePath));
            DrawDefaultInspector();
            if(Application.isPlaying)
            {
                EditorGUILayout.Space();EditorGUILayout.LabelField("当前动作",combat.actions?combat.actions.CurrentId:combat.State.ToString());
                EditorGUILayout.LabelField("源动画帧",combat.CurrentFrame.ToString("0.00"));EditorGUILayout.LabelField("预输入",combat.HasBufferedInput?"等待窗口":"无");
                EditorGUILayout.LabelField("吸附位移",combat.AssistTravel.ToString("0.00")+" m");EditorGUILayout.ObjectField("吸附目标",combat.AssistTarget,typeof(Transform),true);Repaint();
            }
        }
    }
}
