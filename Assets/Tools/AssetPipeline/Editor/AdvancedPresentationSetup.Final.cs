using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Newtonsoft.Json;
using UnityEditor;
using UnityEngine;
using GameActions;

namespace AssetPipeline.Editor
{
 public static partial class AdvancedPresentationSetup
 {
  struct SurfaceTriangle{public Vector3 a,b,c;}
  static bool RayTriangle(Vector3 origin,Vector3 direction,SurfaceTriangle tri,out float distance)
  {
   distance=0;var e1=tri.b-tri.a;var e2=tri.c-tri.a;var p=Vector3.Cross(direction,e2);float det=Vector3.Dot(e1,p);if(Mathf.Abs(det)<1e-8f)return false;float inv=1/det;var t=origin-tri.a;float u=Vector3.Dot(t,p)*inv;if(u<0||u>1)return false;var q=Vector3.Cross(t,e1);float v=Vector3.Dot(direction,q)*inv;if(v<0||u+v>1)return false;distance=Vector3.Dot(e2,q)*inv;return distance>0;
  }
  static void SurfaceBlasts(AdvancedCombatVFXLibrary data)
  {
   var raw=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Enemies/UDK7M/Prefabs/UDK7M.prefab");var scene=UnityEditor.SceneManagement.EditorSceneManager.NewPreviewScene();var model=(GameObject)PrefabUtility.InstantiatePrefab(raw,scene);var tris=new List<SurfaceTriangle>();
   try
   {
    void Collect(Mesh mesh,Transform space){var vertices=mesh.vertices.Select(v=>model.transform.InverseTransformPoint(space.TransformPoint(v))).ToArray();var ids=mesh.triangles;for(int i=0;i<ids.Length;i+=3)tris.Add(new SurfaceTriangle{a=vertices[ids[i]],b=vertices[ids[i+1]],c=vertices[ids[i+2]]});}
    foreach(var f in model.GetComponentsInChildren<MeshFilter>())if(f.sharedMesh)Collect(f.sharedMesh,f.transform);
    foreach(var r in model.GetComponentsInChildren<SkinnedMeshRenderer>()){var bake=new Mesh();r.BakeMesh(bake);Collect(bake,r.transform);UnityEngine.Object.DestroyImmediate(bake);}
    var directions=new[]{new Vector3(-1,.05f,.3f),new Vector3(1,.1f,-.4f),new Vector3(-.4f,.1f,-1),new Vector3(.4f,.15f,1),new Vector3(-.3f,1,.1f),new Vector3(.7f,.65f,.3f),new Vector3(-.7f,.5f,-.3f)};var offsets=new[]{.18f,.3f,.5f,.25f,.4f,.15f,.55f};var points=new List<Vector3>();var report=new List<object>();var focus=new Vector3(0,2.8f,0);
    for(int i=0;i<directions.Length;i++){var outward=directions[i].normalized;var origin=focus+outward*25;float nearest=float.MaxValue;SurfaceTriangle chosen=default;foreach(var t in tris)if(RayTriangle(origin,-outward,t,out float d)&&d<nearest){nearest=d;chosen=t;}if(nearest==float.MaxValue)throw new Exception("No mesh surface for blast "+i);var surface=origin-outward*nearest;var normal=Vector3.Cross(chosen.b-chosen.a,chosen.c-chosen.a).normalized;if(Vector3.Dot(normal,outward)<0)normal=-normal;var point=surface+normal*offsets[i];points.Add(point);report.Add(new{index=i,surface=new[]{surface.x,surface.y,surface.z},normal=new[]{normal.x,normal.y,normal.z},offsetMetres=offsets[i],point=new[]{point.x,point.y,point.z}});}
    data.smallBlastPositions=points.ToArray();data.smallBlastSpacing=.26f;data.smallBlastTail=1.4f;data.finalBlastDelay=3.35f;File.WriteAllText(Stage+"surface_explosion_mounts.json",JsonConvert.SerializeObject(report,Formatting.Indented));
   }
   finally{UnityEditor.SceneManagement.EditorSceneManager.ClosePreviewScene(scene);}
  }
  static void FinalPresentation()
  {
   var data=Library();SurfaceBlasts(data);data.windCone=ImportedWind();data.chargeWindFollowDrillTip=true;data.chargeWindTipOffset=Vector3.zero;
   data.fullChargeFlash=GPU("GauntletFullFlash","VFX_GauntletFullFlash");
   var heavy=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/VFX/Combat/ConcreteImpactHeavy.prefab");var dust=new GameObject("ExpandingGroundDust_Explosion");var inherited=(GameObject)PrefabUtility.InstantiatePrefab(heavy);inherited.transform.SetParent(dust.transform,false);foreach(var particles in inherited.GetComponentsInChildren<ParticleSystem>(true))particles.gameObject.SetActive(particles.name=="Expanding ground dust");data.groundDust=Save(dust,"ExpandingGroundDust_Explosion");data.groundDustInstances=6;data.groundDustScale=1.35f;
   EditorUtility.SetDirty(data);AssetDatabase.SaveAssets();
  }
 }
}

