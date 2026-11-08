using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
namespace Resonator.AI.Editor
{
    public sealed partial class ResonatorAnimationWorkbench
    {
        void DrawBonePicker(SerializedProperty window)
        {
            if(!model)return;
            if(!GUILayout.Button("从模型搜索 / 选择骨骼…"))return;
            var target=window.serializedObject.targetObject;string property=window.propertyPath;
            var paths=model.GetComponentsInChildren<Transform>(true).Where(t=>t!=model.transform).Select(t=>AnimationUtility.CalculateTransformPath(t,model.transform)).ToArray();
            PopupWindow.Show(GUILayoutUtility.GetLastRect(),new BoneSearchPopup(paths,path=>
            {
                Undo.RecordObject(target,"Bind action bone");var serialized=new SerializedObject(target);var p=serialized.FindProperty(property);
                p.FindPropertyRelative("anchor").enumValueIndex=(int)HitboxAnchor.CustomBone;p.FindPropertyRelative("customBone").stringValue=path;serialized.ApplyModifiedProperties();data.Update();Repaint();
            }));
        }
        sealed class BoneSearchPopup:PopupWindowContent
        {
            readonly string[] paths;readonly Action<string> select;readonly HashSet<string> expanded=new HashSet<string>(),parents=new HashSet<string>();string search="";Vector2 scroll;
            public BoneSearchPopup(string[] paths,Action<string> select)
            {
                this.paths=paths.OrderBy(p=>p,StringComparer.Ordinal).ToArray();this.select=select;
                foreach(var path in paths){int slash=path.LastIndexOf('/');if(slash>=0)parents.Add(path.Substring(0,slash));if(path.Count(c=>c=='/')<2)expanded.Add(path);}
            }
            public override Vector2 GetWindowSize()=>new Vector2(580,420);
            public override void OnGUI(Rect rect)
            {
                GUI.SetNextControlName("BoneSearch");search=EditorGUILayout.TextField("搜索骨骼",search);
                scroll=EditorGUILayout.BeginScrollView(scroll);
                foreach(var path in paths)
                {
                    string alias=path.EndsWith("DrillRoll")?"  [钻芯 / 旋转钻头]":path.EndsWith("Control_Tip")?"  [钻尖 IK 目标]":path.EndsWith("Tool")?"  [钻具]":"";
                    if((path+alias).IndexOf(search,StringComparison.OrdinalIgnoreCase)<0)continue;
                    if(string.IsNullOrEmpty(search))
                    {
                        string parent=path;bool visible=true;int slash;
                        while((slash=parent.LastIndexOf('/'))>=0){parent=parent.Substring(0,slash);if(!expanded.Contains(parent)){visible=false;break;}}
                        if(!visible)continue;
                    }
                    bool picked=false;
                    using(new EditorGUILayout.HorizontalScope())
                    {
                        if(string.IsNullOrEmpty(search))GUILayout.Space(Mathf.Min(140,path.Count(c=>c=='/')*12));
                        if(parents.Contains(path)){bool open=GUILayout.Toggle(expanded.Contains(path),GUIContent.none,EditorStyles.foldout,GUILayout.Width(14));if(open)expanded.Add(path);else expanded.Remove(path);}else GUILayout.Space(14);
                        string label=string.IsNullOrEmpty(search)?path.Substring(path.LastIndexOf('/')+1):path;
                        picked=GUILayout.Button(new GUIContent(label+alias,path),EditorStyles.miniButton);
                    }
                    if(picked){select(path);editorWindow.Close();break;}
                }
                EditorGUILayout.EndScrollView();
            }
        }
    }
}
