using System;using System.Linq;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class BoneStability
{
 public static async Task<object> Run()
 {
  if(!Application.isPlaying)throw new Exception("Play first");bool bg=Application.runInBackground;Application.runInBackground=true;
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var x in originals){x.enabled=false;x.motor.Stop();}
  var actorGo=new GameObject("Far stability target");actorGo.transform.position=new Vector3(0,0,-50);var target=actorGo.AddComponent<ResonatorTarget>();target.invulnerableForTesting=true;
  var focus=new GameObject("Rotating crystal focus");focus.transform.position=new Vector3(7,1,10);
  var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/Resonator/AI/Prefabs/ResonatorAgent.prefab"),new Vector3(7,0,6),Quaternion.Euler(0,180,0));var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;e.target=target;e.crystalFocus=focus.transform;e.Initialize(RestActivity.Resonating);
  var bones=e.ragdoll.definitions.Where(b=>!b.bone.name.EndsWith("Pelvis")).Select(b=>b.bone).ToArray();var reference=bones.Select(b=>b.localPosition).ToArray();float worst=0;
  try{e.enabled=true;for(int i=0;i<20;i++){focus.transform.position=go.transform.position+Quaternion.Euler(0,i*35,0)*Vector3.forward*3;await Task.Delay(50);for(int j=0;j<bones.Length;j++)worst=Mathf.Max(worst,(bones[j].localPosition-reference[j]).magnitude*130.65f);}return new{maxJointTranslationErrorMetres=worst,poses=e.CurrentFrame,livingRigidbodies=e.GetComponentsInChildren<Rigidbody>().Length};}
  finally{UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(actorGo);UnityEngine.Object.DestroyImmediate(focus);foreach(var x in originals)if(x)x.enabled=true;Application.runInBackground=bg;}
 }
}

