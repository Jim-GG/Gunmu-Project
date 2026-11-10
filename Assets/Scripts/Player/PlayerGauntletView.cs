using Gauntlet;
using FirstPerson;
using UnityEngine;

// Instantiates the level viewmodel; the authored combat graph exclusively owns poses.
[DefaultExecutionOrder(60)]
public class PlayerGauntletView : MonoBehaviour
{
    [SerializeField] private GameObject viewModelPrefab;
    [SerializeField] private Transform mount;
    [SerializeField] private float nearClipPlane=.01f;
    [SerializeField,Range(0,1)] private float cameraMotionStrength=.55f;
    [SerializeField,HideInInspector] private float animationFps=30;
    [SerializeField,HideInInspector] private int tierOneFrame=32,tierTwoFrame=62,fullFrame=92;
    GauntletAnimationPlayer animationPlayer;
    GauntletCameraMotion cameraMotion;
    PlayerGauntletEquipment equipment;
    GauntletCombat combat;
    Transform model;
    public bool Hidden {get;set;}
    public bool IsVisible=>model&&model.gameObject.activeInHierarchy;
    public GauntletAnimationPlayer AnimationPlayer=>animationPlayer;
    public bool ApplyCameraPose(Transform camera,Vector3 position,Quaternion rotation)=>isActiveAndEnabled&&IsVisible&&cameraMotion&&cameraMotion.ApplyPose(camera,position,rotation,cameraMotionStrength);
    void Awake(){equipment=GetComponent<PlayerGauntletEquipment>();}
    void Start()
    {
        var camera=GetComponentInChildren<Camera>();var parent=mount?mount:camera?camera.transform:null;
        if(!parent||!viewModelPrefab){Debug.LogError("拳套外观缺少预制体或相机。",this);enabled=false;return;}
        model=Instantiate(viewModelPrefab,parent).transform;model.name="GauntletViewModel";model.localPosition=Vector3.zero;model.localRotation=Quaternion.identity;
        animationPlayer=model.GetComponentInChildren<GauntletAnimationPlayer>(true);
        if(!animationPlayer||!animationPlayer.animator){Debug.LogError("拳套预制体缺少动画组件。",this);enabled=false;return;}
        cameraMotion=model.GetComponentInChildren<GauntletCameraMotion>(true);
        if(cameraMotion)
        {
            Transform target=null;foreach(var bone in model.GetComponentsInChildren<Transform>(true))if(bone.name=="camera_target"){target=bone;break;}
            var controller=animationPlayer.animator.runtimeAnimatorController;
            if(controller)foreach(var clip in controller.animationClips)if(clip.name=="Idle"){clip.SampleAnimation(animationPlayer.animator.gameObject,0);break;}
            cameraMotion.PrepareExternalDrive(target);
        }
        if(camera)camera.nearClipPlane=Mathf.Min(camera.nearClipPlane,nearClipPlane);
        combat=GetComponent<GauntletCombat>();combat.animationPlayer=animationPlayer;combat.attackOrigin=camera?camera.transform:parent;
        Refresh();if(IsVisible)combat.InitializePipeline();
    }
    void LateUpdate(){Refresh();}
    void Refresh(){if(model){bool visible=(!equipment||equipment.HasGauntlet)&&!Hidden;if(model.gameObject.activeSelf!=visible)model.gameObject.SetActive(visible);}}
    void OnDisable(){if(model)model.gameObject.SetActive(false);}
}
