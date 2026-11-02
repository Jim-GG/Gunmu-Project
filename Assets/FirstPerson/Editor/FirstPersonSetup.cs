using System;
using System.Linq;
using UnityEditor;
using UnityEditor.Animations;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;
using Unity.Cinemachine;
namespace FirstPerson.Editor
{
 public static class FirstPersonSetup
 {
  [MenuItem("Tools/Gauntlet/Enable First Person Gameplay")]
  public static void Build()
  {
   if(EditorApplication.isPlaying)throw new InvalidOperationException("Exit Play Mode first");
   const string root="Assets/备份拳套/Gauntlet/";
   AssetDatabase.Refresh(ImportAssetOptions.ForceSynchronousImport);
   var modelAvatar=AssetDatabase.LoadAllAssetsAtPath(root+"Models/Gauntlet_Model.fbx").OfType<Avatar>().First();
   var importer=(ModelImporter)AssetImporter.GetAtPath(root+"Animations/Gauntlet_charge_full.fbx");
   importer.animationType=ModelImporterAnimationType.Generic;importer.avatarSetup=ModelImporterAvatarSetup.CopyFromOther;importer.sourceAvatar=modelAvatar;
   importer.materialImportMode=ModelImporterMaterialImportMode.None;importer.preserveHierarchy=true;importer.importAnimation=true;importer.animationCompression=ModelImporterAnimationCompression.Off;
   var clips=importer.defaultClipAnimations;clips[0].name="charge full";clips[0].loopTime=true;clips[0].loopPose=false;importer.clipAnimations=clips;importer.SaveAndReimport();
   var controller=AssetDatabase.LoadAssetAtPath<AnimatorController>(root+"Animations/Gauntlet.controller");
   var machine=controller.layers[0].stateMachine;
   var state=machine.states.FirstOrDefault(s=>s.state.name=="charge full").state;
   if(!state)state=machine.AddState("charge full");
   state.motion=AssetDatabase.LoadAllAssetsAtPath(root+"Animations/Gauntlet_charge_full.fbx").OfType<AnimationClip>().First(c=>c.name=="charge full");state.writeDefaultValues=false;
   EditorUtility.SetDirty(controller);AssetDatabase.SaveAssets();
   var camera=GameObject.Find("Gauntlet Preview Camera").GetComponent<Camera>();
   var player=UnityEngine.Object.FindFirstObjectByType<Gauntlet.GauntletAnimationPlayer>();
   var cm=GameObject.Find("Gauntlet Animation Camera").GetComponent<CinemachineCamera>();
   var rootGo=GameObject.Find("First Person Player");
   if(!rootGo){rootGo=new GameObject("First Person Player");Undo.RegisterCreatedObjectUndo(rootGo,"Create first person controller");rootGo.transform.position=camera.transform.position-Vector3.up*1.65f;}
   var pivot=rootGo.transform.Find("View Pivot");if(!pivot){var go=new GameObject("View Pivot");go.transform.SetParent(rootGo.transform,false);go.transform.localPosition=Vector3.up*1.65f;pivot=go.transform;}
   Undo.SetTransformParent(camera.transform,pivot,"Parent output camera to look pivot");
   Undo.SetTransformParent(player.transform,pivot,"Parent independent viewmodel to look pivot");
   Undo.SetTransformParent(cm.transform,pivot,"Parent Cinemachine to look pivot");
   var motor=rootGo.GetComponent<CharacterController>();if(!motor)motor=Undo.AddComponent<CharacterController>(rootGo);motor.height=1.8f;motor.radius=.3f;motor.center=Vector3.up*.9f;
   var combat=rootGo.GetComponent<GauntletCombat>();if(!combat)combat=Undo.AddComponent<GauntletCombat>(rootGo);combat.animationPlayer=player;
   var input=rootGo.GetComponent<FirstPersonInput>();if(!input)input=Undo.AddComponent<FirstPersonInput>(rootGo);input.combat=combat;
   var movement=rootGo.GetComponent<FirstPersonMotor>();if(!movement)movement=Undo.AddComponent<FirstPersonMotor>(rootGo);movement.input=input;movement.viewPivot=pivot;
   foreach(var ui in UnityEngine.Object.FindObjectsByType<Gauntlet.GauntletPreviewControls>(FindObjectsSortMode.None)){Undo.RecordObject(ui,"Disable animation debug input");ui.enabled=false;}
   if(!GameObject.Find("Gameplay Test Floor")){var floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Gameplay Test Floor";floor.transform.position=new Vector3(0,-1.9f,0);floor.transform.localScale=new Vector3(30,.5f,30);Undo.RegisterCreatedObjectUndo(floor,"Create preview floor");}
   PrefabUtility.RecordPrefabInstancePropertyModifications(player.transform);
   ParryTrainingSetup.Configure(rootGo);
   if(!AssetDatabase.IsValidFolder("Assets/FirstPerson/Prefabs"))AssetDatabase.CreateFolder("Assets/FirstPerson","Prefabs");
   var spawn=rootGo.transform.position;var yaw=rootGo.transform.rotation;
   try{rootGo.transform.SetPositionAndRotation(Vector3.zero,Quaternion.identity);PrefabUtility.SaveAsPrefabAsset(rootGo,"Assets/FirstPerson/Prefabs/FirstPersonPlayer.prefab");}
   finally{rootGo.transform.SetPositionAndRotation(spawn,yaw);}
   EditorSceneManager.MarkSceneDirty(camera.gameObject.scene);EditorSceneManager.SaveScene(camera.gameObject.scene);
  }
 }
}

