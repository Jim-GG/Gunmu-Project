using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using UnityEditor.SceneManagement;
using Object=UnityEngine.Object;

public static class CargoMinimalPass {
 const string Materials="Assets/Scenes/现代设施/卸货大厅材质";
 [MenuItem("零界/现代设施/卸货大厅改为简约光滑风格")]
 public static void Run(){
  if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
  var scene=EditorSceneManager.OpenScene(CargoHallBuilder.ScenePath);
  if(!AssetDatabase.IsValidFolder(Materials))AssetDatabase.CreateFolder("Assets/Scenes/现代设施","卸货大厅材质");
  var white=Mat("珍珠白墙面",new Color(.9f,.92f,.95f),.62f,.08f);
  var floor=Mat("冷灰抛光地坪",new Color(.42f,.47f,.54f),.88f,.22f);
  var graphite=Mat("石墨灰门板",new Color(.10f,.12f,.16f),.76f,.25f);
  var cover=Mat("银灰货箱",new Color(.46f,.51f,.57f),.68f,.2f);
  var purple=Mat("紫色功能标记",new Color(.29f,.12f,.48f),.72f,.12f);
  var led=Mat("柔白灯带",new Color(.75f,.84f,1f),.55f,0);led.EnableKeyword("_EMISSION");led.SetColor("_EmissionColor",new Color(1.3f,1.6f,2));
  string[] remove={"维护配电柜","柜门检修缝","散热栅格","柜门状态指示","法兰工业管线","法兰接头","泵组地台","电机接线盒","维修台背板","维修台工具槽","货架侧框","货架层板","货架封装设备","高架线槽","墙面下护板","模块化设备壁","墙面维护模块","货运路线边标"};
  foreach(var g in scene.GetRootGameObjects().SelectMany(g=>g.GetComponentsInChildren<Transform>(true)).Select(t=>t.gameObject).ToArray())
   if(g&&(remove.Contains(g.name)||g.name.StartsWith("PWR /")))Object.DestroyImmediate(g);
  foreach(var root in scene.GetRootGameObjects().Where(g=>g.name.StartsWith("01 ")||g.name.StartsWith("02 ")))
   foreach(var r in root.GetComponentsInChildren<Renderer>(true)){
    if(r is not MeshRenderer || r.GetComponent<TextMesh>())continue;
    Material m=white;
    if(r.name.Contains("地基")||r.name.Contains("平台"))m=floor;
    else if(r.name.Contains("闸门")||r.name.Contains("隔断门")||r.name.Contains("出口门"))m=graphite;
    else if(r.name.Contains("装甲")||r.name.Contains("检修缝"))m=purple;
    else if(r.name.Contains("灯槽")||r.name.Contains("屏幕"))m=led;
    else if(r.transform.GetComponentsInParent<Transform>().Any(t=>t.name.Contains("货箱")||t.name.Contains("木箱")))m=cover;
    // Preserve textured exit signs, which carry navigation information.
    if(r.transform.GetComponentsInParent<Transform>().Any(t=>t.name=="出口标识"))continue;
    r.sharedMaterials=Enumerable.Repeat(m,r.sharedMaterials.Length).ToArray();r.reflectionProbeUsage=ReflectionProbeUsage.BlendProbes;
   }
  var old=scene.GetRootGameObjects().FirstOrDefault(g=>g.name=="简约风格反射探针");if(old)Object.DestroyImmediate(old);
  var probes=new GameObject("简约风格反射探针");
  foreach(float z in new[]{-18f,18f}){
   var p=new GameObject("大厅一次性环境反射").AddComponent<ReflectionProbe>();p.transform.SetParent(probes.transform);p.transform.position=new Vector3(0,5,z);p.mode=ReflectionProbeMode.Realtime;p.refreshMode=ReflectionProbeRefreshMode.OnAwake;p.timeSlicingMode=ReflectionProbeTimeSlicingMode.NoTimeSlicing;p.resolution=256;p.size=new Vector3(42,14,40);p.boxProjection=true;p.blendDistance=3;p.nearClipPlane=.1f;p.farClipPlane=95;p.intensity=1;
  }
  foreach(var d in Object.FindObjectsByType<CargoAttackDrone>(FindObjectsInactive.Include,FindObjectsSortMode.None)){
   d.headLocalDirection=Vector3.forward;d.turnSpeed=240;
   if(d.player)d.transform.rotation=Quaternion.LookRotation(d.player.transform.position+Vector3.up-d.transform.position);
   foreach(var r in d.GetComponentsInChildren<Renderer>())Debug.Log("DRONE PART "+r.name+" local "+d.transform.InverseTransformPoint(r.bounds.center));
  }
  RenderSettings.ambientSkyColor=new Color(.65f,.69f,.76f);RenderSettings.ambientEquatorColor=new Color(.48f,.52f,.6f);RenderSettings.ambientGroundColor=new Color(.3f,.33f,.4f);
  foreach(var l in Object.FindObjectsByType<Light>(FindObjectsInactive.Include,FindObjectsSortMode.None)){if(l.name=="顶灯柔光")l.intensity=.85f;if(l.type==LightType.Directional)l.intensity=1.1f;}
  AssetDatabase.SaveAssets();EditorSceneManager.SaveScene(scene);Debug.Log("CARGO MINIMAL PASS DONE");
 }
 static Material Mat(string name,Color color,float smooth,float metallic){string p=Materials+"/"+name+".mat";var m=AssetDatabase.LoadAssetAtPath<Material>(p);if(!m){m=new Material(Shader.Find("Universal Render Pipeline/Lit"));AssetDatabase.CreateAsset(m,p);}m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",smooth);m.SetFloat("_Metallic",metallic);m.enableInstancing=true;EditorUtility.SetDirty(m);return m;}
}

