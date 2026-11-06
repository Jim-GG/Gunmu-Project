using System;using System.Linq;using UnityEngine;using UnityEditor;
public static class InspectGauntlet
{
 public static object Run()
 {
  var prefab=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Gauntlet/Prefabs/GauntletViewModel.prefab");
  var go=UnityEngine.Object.Instantiate(prefab);go.hideFlags=HideFlags.HideAndDontSave;
  try
  {
   foreach(var b in go.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;
   var animator=go.GetComponentInChildren<Animator>();var hand=go.GetComponentsInChildren<Transform>().First(t=>t.name=="hand_L.R");
   var cam=go.GetComponentsInChildren<Transform>().First(t=>t.name=="camera_target");
   return Newtonsoft.Json.JsonConvert.SerializeObject(animator.runtimeAnimatorController.animationClips.Where(c=>c.name.StartsWith("punch")).Distinct().Select(c=>{
    var samples=Enumerable.Range(0,Mathf.RoundToInt(c.length*c.frameRate)+1).Select(f=>{c.SampleAnimation(animator.gameObject,f/c.frameRate);return new{frame=f,hand=animator.transform.InverseTransformPoint(hand.position).z,camera=animator.transform.InverseTransformPoint(cam.position).z};}).ToArray();
    return new{name=c.name,frames=c.length*c.frameRate,samples};
   }).ToArray());
  }finally{UnityEngine.Object.DestroyImmediate(go);}
 }
}
