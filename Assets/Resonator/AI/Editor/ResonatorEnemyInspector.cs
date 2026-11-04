using UnityEditor;using UnityEngine;
namespace Resonator.AI.Editor
{
    [CustomEditor(typeof(ResonatorEnemy))]
    public sealed class ResonatorEnemyInspector:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var enemy=(ResonatorEnemy)target;
            if(enemy.tuning&&GUILayout.Button("打开动画工作台：混合预览 / Hitbox / 事件"))ResonatorAnimationWorkbench.Open(enemy.tuning);
            if(enemy.tuning&&GUILayout.Button("编辑动画衔接 / 连招取消参数")){Selection.activeObject=enemy.tuning;EditorGUIUtility.PingObject(enemy.tuning);}
            EditorGUILayout.Space();EditorGUILayout.LabelField("Runtime",EditorStyles.boldLabel);
            EditorGUILayout.LabelField("State",enemy.State.ToString());EditorGUILayout.LabelField("Frame",enemy.CurrentFrame.ToString("0.0"));EditorGUILayout.LabelField("Health",enemy.Health.ToString("0.0"));
            if(Application.isPlaying)
            {
                EditorGUILayout.LabelField("计划起手动作",enemy.PlannedAttack.ToString());
                EditorGUILayout.LabelField("当前起手距离",enemy.PlannedStartDistance.ToString("0.00")+" m");
                EditorGUILayout.LabelField("侧移脱困次数",enemy.motor?enemy.motor.UnstuckAttempts.ToString():"0");
                if(GUILayout.Button("Emit player noise here (test)"))if(enemy.target)ResonatorNoise.Emit(enemy.transform.position,30,enemy.target);
                if(GUILayout.Button("Front hit: 10 damage"))enemy.TakeDamage(10,enemy.transform.position+enemy.transform.forward*2);
                if(GUILayout.Button("Back hit: 10 damage"))enemy.TakeDamage(10,enemy.transform.position-enemy.transform.forward*2);
                if(GUILayout.Button("Lethal hit (ragdoll at frame 50)"))enemy.TakeDamage(10000,enemy.transform.position+enemy.transform.forward*2,3);
                if(ResonatorEnemy.IsAttack(enemy.State))
                {
                    EditorGUILayout.LabelField("当前动作已命中",enemy.CurrentAttackConnected.ToString());
                    EditorGUILayout.LabelField("本次额外追击位移",enemy.AttackAddedTravel.ToString("0.00")+" m");
                    EditorGUILayout.LabelField("攻击方向已锁定",enemy.AttackAimLocked.ToString());
                    foreach(var edge in enemy.tuning.transitions)
                        if(edge.enabled&&!edge.anySource&&edge.from==enemy.State&&ResonatorEnemy.IsAttack(edge.to))
                            if(GUILayout.Button("测试取消 → "+edge.to+" （仍遵循取消帧）"))enemy.TryCancelAttack(edge.to);
                }
                Repaint();
            }
            EditorGUILayout.HelpBox("Edit the Tuning asset for frame gates, fades, hit windows, spacing and movement speed. Duplicate that asset for enemy variants.",MessageType.Info);
        }
    }
}
