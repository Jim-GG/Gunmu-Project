using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;using UnityEngine;using UnityEditor;using Resonator.AI;
public static class InitializationStability
{
 public static async Task<object> Run()
 {
  bool bg=Application.runInBackground;Application.runInBackground=true;
  var originals=UnityEngine.Object.FindObjectsByType<ResonatorEnemy>(FindObjectsSortMode.None);foreach(var x in originals){x.enabled=false;x.motor.Stop();}
  var actor=new GameObject("Distant validation target");actor.transform.position=new Vector3(0,0,-100);var target=actor.AddComponent<ResonatorTarget>();target.invulnerableForTesting=true;
  var created=new List<GameObject>();var results=new List<object>();
  try
  {
   foreach(RestActivity activity in Enum.GetValues(typeof(RestActivity)))
   {
    var go=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Resonator/AI/Prefabs/ResonatorAgent.prefab"),new Vector3(7,0,6),Quaternion.Euler(0,123,0));created.Add(go);
    var e=go.GetComponent<ResonatorEnemy>();e.enabled=false;e.target=target;e.Initialize(activity);
    float maxExtent=0;var mesh=new Mesh();
    for(int i=0;i<12;i++)
    {
     go.transform.Rotate(0,21,0);e.Tick(.13f);await Task.Delay(25);
     foreach(var renderer in go.GetComponentsInChildren<SkinnedMeshRenderer>())
     {
      renderer.BakeMesh(mesh,true);var size=Vector3.Scale(mesh.bounds.size,renderer.transform.lossyScale);maxExtent=Mathf.Max(maxExtent,Mathf.Abs(size.x),Mathf.Abs(size.y),Mathf.Abs(size.z));
     }
    }
    UnityEngine.Object.DestroyImmediate(mesh);
    if(e.GetComponentsInChildren<Rigidbody>().Length!=0||maxExtent>3.5f||float.IsNaN(maxExtent))throw new Exception(activity+" has unstable skin extent "+maxExtent);
    results.Add(new{activity=activity.ToString(),maxSkinnedExtentMetres=maxExtent,livingRigidbodies=0});UnityEngine.Object.DestroyImmediate(go);
   }
   return new{passed=true,results};
  }
  finally{foreach(var go in created)if(go)UnityEngine.Object.DestroyImmediate(go);UnityEngine.Object.DestroyImmediate(actor);foreach(var e in originals)if(e)e.enabled=true;Application.runInBackground=bg;}
 }
}
