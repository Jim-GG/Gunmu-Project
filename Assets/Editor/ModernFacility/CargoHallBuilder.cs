using System;
using System.Linq;
using UnityEngine;
using UnityEditor;
using UnityEditor.SceneManagement;
using Resonator.AI;
using Object=UnityEngine.Object;

public static class CargoHallBuilder
{
    const string Lab="Assets/Game/Environment/ModernLab/";
    public const string ScenePath="Assets/Scenes/现代设施/Modern_01_UnloadingHall.unity";
    static Transform shell,decor,logic;
    static Material floor,wall,dark,metal,mark,light;
    [MenuItem("零界/现代设施/生成卸货大厅（独立新场景）")]
    public static void Build()
    {
        if(!Application.isBatchMode&&!EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())return;
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene);
        shell=new GameObject("01 建筑结构 · 40×72米").transform;
        decor=new GameObject("02 工业陈设 · 可独立编辑").transform;
        logic=new GameObject("03 无人机与流程").transform;
        floor=Mat("Lab_Floor_Resin");wall=Mat("Lab_Wall_Ribbed");dark=Mat("Lab_Dark_Panel");metal=Mat("Lab_Rail_BrushedSteel");mark=Mat("Preview_Safety_Marking");light=Mat("Preview_Light");
        DressEquipment(); Block("整体地基",new Vector3(0,-.35f,0),new Vector3(40,.7f,72),floor);
        Block("顶板",new Vector3(0,12,0),new Vector3(40,.4f,72),dark);
        foreach(int side in new[]{-1,1}){
            Block("实体外墙",new Vector3(side*20,6,0),new Vector3(.5f,12,72),wall);
            for(int z=-32;z<=32;z+=8){
                Block("结构立柱",new Vector3(side*19.3f,5.8f,z),new Vector3(.9f,11.6f,1),metal);
                Block("墙面下护板",new Vector3(side*19.65f,1.5f,z+3),new Vector3(.2f,3,5.6f),dark);
                Block("高架线槽",new Vector3(side*17.8f,9.4f,z),new Vector3(.55f,.4f,8),metal);
                Prop("Lab_WallWhite_14","模块化设备壁",new Vector3(side*19.4f,4.8f,z+3),new Vector3(4,3,.3f),side==1?-90:90);
                Prop("Lab_AccentWall_31","墙面维护模块",new Vector3(side*18.9f,1.5f,z+2),new Vector3(2.8f,2.6f,.5f),side==1?-90:90);
            }
        }
        Block("入口端墙",new Vector3(0,6,-36),new Vector3(40,12,.5f),wall);
        Block("出口端墙",new Vector3(0,6,36),new Vector3(40,12,.5f),wall);
        for(int z=-30;z<=30;z+=10){
            Block("横梁",new Vector3(0,10.8f,z),new Vector3(39,.6f,.6f),metal);
            for(int x=-12;x<=12;x+=12){
                Block("顶灯灯槽",new Vector3(x,11.5f,z),new Vector3(5,.12f,.5f),light);
                var l=new GameObject("顶灯柔光").AddComponent<Light>();l.transform.SetParent(decor);l.transform.position=new Vector3(x,10.8f,z);l.type=LightType.Point;l.range=16;l.intensity=2.5f;l.color=new Color(.78f,.86f,1);l.shadows=LightShadows.None;
            }
        }
        for(int z=-30;z<=30;z+=4) foreach(int x in new[]{-5,5}) Block("货运路线边标",new Vector3(x,.012f,z),new Vector3(.12f,.015f,2),mark);
        // Broad central combat lanes; cargo creates safe flanking routes without a maze.
        foreach(var p in new[]{new Vector3(-10,0,-20),new Vector3(10,0,-12),new Vector3(-12,0,-6),new Vector3(11,0,12),new Vector3(-11,0,22)}){
            Prop("Props/ML_WoodCrate_1m","运输木箱",p+Vector3.up*.8f,new Vector3(3.8f,1.6f,2.5f),0,true);
            Prop("Props/ML_Crate_1m","封存货箱",p+new Vector3(2.8f,.7f,1),new Vector3(1.3f,1.4f,1.3f),15,true);
            Block("托盘底座",p+new Vector3(0,.1f,0),new Vector3(4.1f,.2f,2.8f),metal);
        }
        foreach(int side in new[]{-1,1})for(int z=8;z<=28;z+=10){
            Block("货架侧框",new Vector3(side*17.5f,2.2f,z),new Vector3(.14f,4.4f,4),metal);
            for(int y=1;y<=3;y+=2){
                Block("货架层板",new Vector3(side*16.5f,y,z),new Vector3(2,.15f,4),metal);
                Prop("Props/ML_Crate_1m","货架封装设备",new Vector3(side*16.5f,y+.7f,z),new Vector3(1.5f,1.2f,2.8f));
            }
        }
        // Impact walls have a clean, reachable collision face.
        foreach(int side in new[]{-1,1}){
            Block("撞击维护装甲墙",new Vector3(side*18.7f,2,-15),new Vector3(.7f,4,8),mark);
            Label("冲击维护区",new Vector3(side*18.2f,4.5f,-15),side==1?-90:90,1);
        }
        Block("隔断左翼",new Vector3(-11.5f,3,0),new Vector3(17,6,.7f),wall);
        Block("隔断右翼",new Vector3(11.5f,3,0),new Vector3(17,6,.7f),wall);
        Block("隔断门楣",new Vector3(0,5.2f,0),new Vector3(6,1.6f,.7f),metal);
        var divider=Block("安保隔断门",new Vector3(0,2.2f,0),new Vector3(6,4.4f,.4f),dark);
        // Upper room is inaccessible on foot but has a generous drone entrance.
        Block("上层控制室平台",new Vector3(-14,5.5f,-4),new Vector3(9,.4f,7),floor);
        Block("控制室后壁",new Vector3(-14,7.3f,-.7f),new Vector3(9,3.4f,.25f),dark);
        foreach(int side in new[]{-1,1})Block("控制室侧壁",new Vector3(-14+side*4.5f,7.3f,-4),new Vector3(.2f,3.4f,7),wall);
        Block("控制室顶盖",new Vector3(-14,9.1f,-4),new Vector3(9,.2f,7),metal);
        Block("控制台基座",new Vector3(-14,6.2f,-2),new Vector3(2.4f,1.2f,.7f),dark);
        Block("接入屏幕",new Vector3(-14,7,-2.35f),new Vector3(2.2f,1.1f,.08f),light);
        Label("01 远程接入",new Vector3(-14,7.9f,-2.5f),0,.7f);
        Label("无人机检修入口  ↑",new Vector3(-14,9.6f,-7.6f),0,.8f);
        Prop("Props/ML_Exit_Door","出口标识",new Vector3(0,4.8f,31),new Vector3(3,.6f,.12f),180);
        Block("出口门框左",new Vector3(-11.5f,3,32),new Vector3(17,6,.6f),wall);
        Block("出口门框右",new Vector3(11.5f,3,32),new Vector3(17,6,.6f),wall);
        var exit=Block("清场出口门",new Vector3(0,2.2f,32),new Vector3(6,4.4f,.35f),dark);
        Label("卸货大厅 / 安保检修",new Vector3(0,6,-35.5f),180,1.1f);
        var pg=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/Player.prefab"));pg.name="玩家 · 现有3C与拳套";pg.transform.position=new Vector3(0,.1f,-29);
        Set(pg.GetComponent<PlayerGauntletEquipment>(),"startsEquipped",true);Set(pg.GetComponent<PlayerChargedPunch>(),"initialEnergy",3);Set(pg.GetComponent<PlayerMovement>(),"startsWithMomentumRemote",false);
        if(!pg.GetComponent<RoomRestartController>())pg.AddComponent<RoomRestartController>();
        var lesson=new GameObject("卸货大厅 · 分阶段教学").AddComponent<CargoDroneLesson>();lesson.transform.SetParent(logic);lesson.player=pg.GetComponent<PlayerCombatController>();lesson.dividerDoor=divider;lesson.exitDoor=exit;
        lesson.serviceDock=Anchor("备用维修座",new Vector3(-5,0,-26));
        Block("维修座台面",new Vector3(-5,.35f,-26),new Vector3(2.2f,.7f,2.2f),metal);
        Label("无人机回收 / 按 E 重试",new Vector3(-5,2.6f,-25.8f),0,.6f);
        lesson.remoteSocket=Anchor("上层接入范围",new Vector3(-14,7,-3.5f));
        lesson.secondWaveTrigger=Anchor("后厅遭遇入口",new Vector3(0,1,5));lesson.exitPoint=Anchor("本关完成点",new Vector3(0,1,34));
        lesson.firstDrone=Drone("教学无人机",new Vector3(0,6,-13),lesson);
        lesson.secondWave=new[]{Drone("后厅无人机A",new Vector3(-7,6,18),lesson),Drone("后厅无人机B",new Vector3(7,7,22),lesson)};
        lesson.secondWave[1].attackInterval=5.5f;foreach(var d in lesson.secondWave)d.gameObject.SetActive(false);
        var cam=new GameObject("无人机遥控镜头").AddComponent<Camera>();cam.fieldOfView=80;cam.nearClipPlane=.08f;cam.gameObject.AddComponent<AudioListener>();cam.gameObject.SetActive(false);lesson.remoteCamera=cam;
        foreach(var p in new[]{new Vector3(5,1,-26),new Vector3(-6,1,6),new Vector3(12,1,26)}){
            var g=new GameObject("补给紫晶");g.transform.position=p;g.AddComponent<SphereCollider>().radius=.65f;g.AddComponent<DashCrystal>();
            var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/crystal/Prefabs/Crystal_07_Fractured.prefab"));model.transform.SetParent(g.transform,false);model.transform.localScale=Vector3.one*.65f;foreach(var c in model.GetComponentsInChildren<Collider>())c.enabled=false;
        }
        RenderSettings.ambientMode=UnityEngine.Rendering.AmbientMode.Trilight;RenderSettings.ambientSkyColor=new Color(.32f,.38f,.46f);RenderSettings.ambientEquatorColor=new Color(.24f,.28f,.34f);RenderSettings.ambientGroundColor=new Color(.13f,.15f,.2f);RenderSettings.fog=false;
        var sun=new GameObject("设施漫反射补光").AddComponent<Light>();sun.type=LightType.Directional;sun.intensity=.65f;sun.transform.rotation=Quaternion.Euler(65,-25,0);sun.shadows=LightShadows.Soft;
        EditorSceneManager.SaveScene(scene,ScenePath);
        var scenes=EditorBuildSettings.scenes.ToList();if(!scenes.Any(s=>s.path==ScenePath))scenes.Add(new EditorBuildSettingsScene(ScenePath,true));EditorBuildSettings.scenes=scenes.ToArray();AssetDatabase.SaveAssets();CargoMinimalPass.Run();CargoMetalPass.Run();Debug.Log("CARGO HALL BUILD OK");
    }
    static CargoAttackDrone Drone(string name,Vector3 pos,CargoDroneLesson lesson){
        var g=new GameObject(name);g.transform.SetParent(logic);g.transform.position=pos;
        var d=g.AddComponent<CargoAttackDrone>();d.player=lesson.player;d.lesson=lesson;d.healthSource=AssetDatabase.LoadAssetAtPath<ResonatorTuning>("Assets/Resonator/AI/Data/Resonator_Default.asset");
        var model=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Environment/Drone/Prefabs/Drone_MCU7A.prefab"));
        foreach(var b in model.GetComponentsInChildren<MonoBehaviour>())b.enabled=false;foreach(var c in model.GetComponentsInChildren<Collider>())Object.DestroyImmediate(c);foreach(var b in model.GetComponentsInChildren<Rigidbody>())Object.DestroyImmediate(b);
        var rs=model.GetComponentsInChildren<Renderer>();var bounds=rs[0].bounds;foreach(var r in rs.Skip(1))bounds.Encapsulate(r.bounds);float scale=2.1f/bounds.size.x;model.transform.SetParent(g.transform,false);model.transform.localScale=Vector3.one*scale;model.transform.localPosition=-bounds.center*scale;
        var l=new GameObject("攻击预告灯").AddComponent<Light>();l.transform.SetParent(g.transform,false);l.transform.localPosition=new Vector3(0,0,.7f);l.range=3;l.intensity=2;d.warning=l;
        d.rotors=model.GetComponentsInChildren<Transform>().Where(t=>t.name.Contains("upright pivot")).ToArray();d.sparkMaterial=light;var beam=g.AddComponent<LineRenderer>();beam.sharedMaterial=light;beam.startWidth=.035f;beam.endWidth=.015f;beam.enabled=false;return d;
    }
    static void DressEquipment(){
        var ivory=AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/FacilityDressing/Materials/Paint_Ivory_1.00.mat");if(!ivory)ivory=wall;
        for(int side=-1;side<=1;side+=2)for(int z=-30;z<=30;z+=12){
            float x=side*18;
            Block("维护配电柜",new Vector3(x,1.5f,z),new Vector3(1.2f,3,2.4f),ivory);
            Block("柜门检修缝",new Vector3(x-side*.62f,1.5f,z),new Vector3(.03f,2.7f,.035f),dark);
            for(int k=0;k<6;k++)Block("散热栅格",new Vector3(x-side*.63f,.5f+k*.12f,z),new Vector3(.035f,.04f,1.8f),dark);
            for(int k=0;k<3;k++)Block("柜门状态指示",new Vector3(x-side*.65f,2.4f,z-.5f+k*.35f),new Vector3(.04f,.08f,.12f),light);
            Pipe(new Vector3(side*18.7f,3.1f,z),new Vector3(side*18.7f,8.5f,z),.2f);
            Label("PWR / "+(z+31).ToString("00"),new Vector3(x-side*.7f,2,z),side==1?-90:90,.45f);
        }
        foreach(int side in new[]{-1,1}){
            Pipe(new Vector3(side*18.6f,7.7f,-34),new Vector3(side*18.6f,7.7f,34),.35f);
            Pipe(new Vector3(side*18.6f,8.4f,-34),new Vector3(side*18.6f,8.4f,34),.18f);
            for(int z=12;z<=24;z+=6){
                Block("泵组地台",new Vector3(side*14,.2f,z),new Vector3(2.6f,.4f,2),metal);
                Pipe(new Vector3(side*14,.8f,z-.7f),new Vector3(side*14,.8f,z+.7f),1);
                Pipe(new Vector3(side*14,.8f,z+.7f),new Vector3(side*14,3.7f,z+.7f),.18f);
                Pipe(new Vector3(side*14,3.7f,z+.7f),new Vector3(side*18.5f,3.7f,z+.7f),.18f);
                Block("电机接线盒",new Vector3(side*14,1.5f,z),new Vector3(.65f,.4f,.6f),ivory);
            }
        }
        Block("维修台背板",new Vector3(-5,1.7f,-28),new Vector3(4,3.4f,.2f),ivory);
        for(int i=0;i<4;i++)Block("维修台工具槽",new Vector3(-6.2f+i*.8f,1.7f,-27.8f),new Vector3(.5f,1,.12f),metal);
    }
    static void Pipe(Vector3 a,Vector3 b,float diameter){var g=GameObject.CreatePrimitive(PrimitiveType.Cylinder);g.name="法兰工业管线";g.transform.SetParent(decor);g.transform.position=(a+b)*.5f;g.transform.up=(b-a).normalized;g.transform.localScale=new Vector3(diameter,(b-a).magnitude*.5f,diameter);g.GetComponent<Renderer>().sharedMaterial=metal;Object.DestroyImmediate(g.GetComponent<Collider>());
        for(int i=0;i<2;i++){var f=GameObject.CreatePrimitive(PrimitiveType.Cylinder);f.name="法兰接头";f.transform.SetParent(decor);f.transform.position=i==0?a:b;f.transform.up=g.transform.up;f.transform.localScale=new Vector3(diameter*1.45f,.06f,diameter*1.45f);f.GetComponent<Renderer>().sharedMaterial=dark;Object.DestroyImmediate(f.GetComponent<Collider>());}}
    static Transform Anchor(string n,Vector3 p){var t=new GameObject(n).transform;t.SetParent(logic);t.position=p;return t;}
    static Material Mat(string n)=>AssetDatabase.LoadAssetAtPath<Material>(Lab+"Materials/"+n+".mat");
    static GameObject Block(string n,Vector3 p,Vector3 s,Material m){var g=GameObject.CreatePrimitive(PrimitiveType.Cube);g.name=n;g.transform.SetParent(shell);g.transform.position=p;g.transform.localScale=s;g.GetComponent<Renderer>().sharedMaterial=m;return g;}
    static void Set(Object o,string key,object val){var s=new SerializedObject(o);var p=s.FindProperty(key);if(val is bool b)p.boolValue=b;else p.intValue=(int)val;s.ApplyModifiedPropertiesWithoutUndo();}
    static GameObject Prop(string path,string n,Vector3 p,Vector3 size,float yaw=0,bool solid=false){
        var g=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>(Lab+"Prefabs/"+path+".prefab"));g.name=n;g.transform.SetParent(decor);
        var rs=g.GetComponentsInChildren<Renderer>();var b=rs[0].bounds;foreach(var r in rs.Skip(1))b.Encapsulate(r.bounds);
        var scale=new Vector3(size.x/Mathf.Max(.01f,b.size.x),size.y/Mathf.Max(.01f,b.size.y),size.z/Mathf.Max(.01f,b.size.z));var q=Quaternion.Euler(0,yaw,0);g.transform.localScale=scale;g.transform.SetPositionAndRotation(p-q*Vector3.Scale(b.center,scale),q);
        foreach(var c in g.GetComponentsInChildren<Collider>())c.enabled=false;if(solid){var c=g.AddComponent<BoxCollider>();c.center=b.center;c.size=b.size;}return g;
    }
    static void Label(string text,Vector3 p,float yaw,float size){var t=new GameObject(text).AddComponent<TextMesh>();t.transform.SetParent(decor);t.transform.SetPositionAndRotation(p,Quaternion.Euler(0,yaw,0));t.text=text;t.font=Font.CreateDynamicFontFromOSFont("Microsoft YaHei",48);t.GetComponent<Renderer>().sharedMaterial=t.font.material;t.fontSize=48;t.characterSize=.09f*size;t.anchor=TextAnchor.MiddleCenter;t.color=new Color(.65f,1, .86f);}
}



