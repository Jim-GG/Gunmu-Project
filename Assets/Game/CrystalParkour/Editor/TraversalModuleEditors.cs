using System.IO;
using UnityEditor;
using UnityEngine;
namespace CrystalParkour.Editor
{
    public static class TraversalModuleMenu
    {
        const string Folder="Assets/Game/CrystalParkour/Prefabs/LevelModules/";
        static void Add(string name)
        {
            var prefab=AssetDatabase.LoadAssetAtPath<GameObject>(Folder+name+".prefab");if(!prefab){Debug.LogError("Missing module: "+name);return;}
            var go=(GameObject)PrefabUtility.InstantiatePrefab(prefab);Undo.RegisterCreatedObjectUndo(go,"Add traversal module");
            go.transform.position=Selection.activeTransform?Selection.activeTransform.position:SceneView.lastActiveSceneView?SceneView.lastActiveSceneView.pivot:Vector3.zero;
            Selection.activeGameObject=go;
        }
        [MenuItem("GameObject/Crystal Parkour/Pulse silo",false,20)]static void Silo()=>Add("PulseSilo_A");
        [MenuItem("GameObject/Crystal Parkour/Horizontal fan crossing",false,21)]static void Fan()=>Add("FanDuct_ThreeDirections");
        [MenuItem("GameObject/Crystal Parkour/Crystal growth and rebound bay",false,22)]static void Bay()=>Add("CrystalBay_NoWind");
        [MenuItem("GameObject/Crystal Parkour/One descending drone route",false,23)]static void Drone()=>Add("DescendingDrone_SingleRoute");
        [MenuItem("GameObject/Crystal Parkour/Three descending drones",false,24)]static void Stream()=>Add("DescendingDrone_ThreeStream");
        [MenuItem("Tools/Crystal Parkour/Open V2 level design guide")]
        static void Guide()=>EditorUtility.OpenWithDefaultApp(Path.GetFullPath("Docs/CrystalParkour/V2/关卡搭建与参数指南.md"));
    }
    [CustomEditor(typeof(WindVolume))]
    public sealed class WindVolumeEditor:UnityEditor.Editor
    {
        double previewTime=1;
        public override void OnInspectorGUI()
        {
            serializedObject.Update();var w=(WindVolume)target;
            void Field(string name,string label)=>EditorGUILayout.PropertyField(serializedObject.FindProperty(name),new GUIContent(label));
            Field("windEnabled","启用气流");Field("kind","气流类型");Field("shape","作用范围形状");
            EditorGUILayout.HelpBox("范围由同物体 BoxCollider 的 Center / Size 决定。圆柱使用 X/Z 半径和 Y 高度。",MessageType.Info);
            if(w.kind==WindKind.GravityRelief){Field("gravityScale","剩余重力比例");EditorGUILayout.HelpBox("0.42 表示保留 42% 重力；不提供永续升力。需要先跳跃，WASD 保留空中控制。",MessageType.None);}
            else{Field("direction","气流方向");Field("directionIsLocal","方向跟随模块旋转");Field("driftSpeed","最大水平漂移 m/s");Field("responseAcceleration","漂移加速度 m/s²");}
            Field("intermittent","间歇开关");if(w.intermittent){Field("onSeconds","开启秒数");Field("offSeconds","关闭秒数");Field("phaseOffset","相位偏移秒");Field("rampSeconds","开关渐变秒");}
            Field("enterBlend","进入混合秒");Field("exitBlend","离开混合秒");serializedObject.ApplyModifiedProperties();
            if(w.intermittent){EditorGUILayout.Space();previewTime=EditorGUILayout.DoubleField("预览关卡时间（秒）",previewTime);EditorGUILayout.LabelField("此时风量",w.StrengthAt(previewTime).ToString("P0"));}
        }
    }
    [CustomEditor(typeof(CrystalGrowthSite))]
    public sealed class CrystalGrowthEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var site=(CrystalGrowthSite)target;
            EditorGUILayout.HelpBox("本物体是敲击点，Growth Root 可以放在远处。运行时先隐藏，实际命中帧才生长；生长完毕才启用碰撞与牵引。编辑模式显示完整目标便于摆放。",MessageType.Info);
            if(site.growthRoot&&GUILayout.Button("选择生长目标（移动它可做远程生晶）"))Selection.activeGameObject=site.growthRoot;
            if(site.strikeCollider&&GUILayout.Button("定位敲击范围")){Selection.activeGameObject=site.strikeCollider.gameObject;SceneView.lastActiveSceneView?.FrameSelected();}
        }
    }
    [CustomEditor(typeof(TraversalDrone))]
    public sealed class TraversalDroneEditor:UnityEditor.Editor
    {
        public override void OnInspectorGUI()
        {
            DrawDefaultInspector();var drone=(TraversalDrone)target;
            EditorGUILayout.HelpBox("下降队列：将 Start/End 锚点放在同一个路线容器下，不能放在正在移动的无人机本体下。三个实例相位错开周期的 1/3。隐藏回收时不会把玩家传回顶部。",MessageType.Info);
            if(drone.startAnchor&&GUILayout.Button("选择顶部锚点"))Selection.activeTransform=drone.startAnchor;
            if(drone.endAnchor&&GUILayout.Button("选择底部锚点"))Selection.activeTransform=drone.endAnchor;
        }
        void OnSceneGUI()
        {
            var d=(TraversalDrone)target;if(!d.startAnchor||!d.endAnchor)return;
            Handles.color=Color.cyan;Handles.DrawDottedLine(d.startAnchor.position,d.endAnchor.position,5);
            foreach(var t in new[]{d.startAnchor,d.endAnchor}){EditorGUI.BeginChangeCheck();var p=Handles.PositionHandle(t.position,Quaternion.identity);if(EditorGUI.EndChangeCheck()){Undo.RecordObject(t,"Move drone route anchor");t.position=p;}}
        }
    }
}
