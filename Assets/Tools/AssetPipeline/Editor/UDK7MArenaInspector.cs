using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using Unity.AI.Navigation;
using UDK7M;
namespace AssetPipeline.Editor
{
    [CustomEditor(typeof(UDK7MArena))]public sealed class UDK7MArenaInspector:UnityEditor.Editor
    {
        [MenuItem("Tools/UDK7M/Open Boss Animation Workbench")]
        public static void OpenWorkbench()
        {
            var actions=AssetDatabase.LoadAssetAtPath<GameActions.ActionSet>("Assets/Game/Enemies/UDK7M/Actions/UDK7MActions.asset");
            if(!actions)return;var window=Resonator.AI.Editor.ResonatorAnimationWorkbench.OpenShared(actions);window.ShowAction(System.Array.FindIndex(actions.actions,a=>a.id=="SlamRecover"),5);
        }
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var arena=(UDK7MArena)target;
            if(arena.boss&&GUILayout.Button("打开 UDK7M 共用动画工作台"))Resonator.AI.Editor.ResonatorAnimationWorkbench.OpenShared(arena.boss.actions);
            if(!Application.isPlaying&&GUILayout.Button("重新烘焙本场景导航"))Rebake(arena);
            if(Application.isPlaying){EditorGUILayout.LabelField("Boss",arena.boss.State+" · "+arena.boss.Health.ToString("0")+" HP");if(GUILayout.Button("重置本场战斗"))arena.ResetEncounter();}
            EditorGUILayout.HelpBox("移动晶柱、地板或改变缺口后，重新烘焙导航并保存场景。AI 数值在 Tuning，命中盒、事件和混合在 Actions 中调整。",MessageType.Info);
        }
        public static void Rebake(UDK7MArena arena)
        {
            foreach(var surface in arena.gameObject.scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<NavMeshSurface>(true)))
            {
                var old=surface.navMeshData;surface.BuildNavMesh();var fresh=surface.navMeshData;
                if(old&&AssetDatabase.Contains(old)){EditorUtility.CopySerialized(fresh,old);surface.RemoveData();surface.navMeshData=old;surface.AddData();Object.DestroyImmediate(fresh);EditorUtility.SetDirty(old);}EditorUtility.SetDirty(surface);
            }
            AssetDatabase.SaveAssets();EditorSceneManager.MarkSceneDirty(arena.gameObject.scene);
        }
    }
}
