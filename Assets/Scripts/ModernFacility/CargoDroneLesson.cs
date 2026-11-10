using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

public sealed class CargoDroneLesson : MonoBehaviour, ILevelExitGate
{
    public PlayerCombatController player;
    public CargoAttackDrone firstDrone;
    public CargoAttackDrone[] secondWave;
    public Transform serviceDock, remoteSocket, secondWaveTrigger, exitPoint;
    public GameObject dividerDoor, exitDoor;
    public string nextSceneName;
    public Camera remoteCamera;
    [Min(1)] public float controlSeconds=20, flySpeed=6;
    public float mouseSensitivity=.12f, socketRange=2;
    public bool IsSatisfied => terminalDone && waveStarted && AllDown(secondWave);
    public bool IsRemote => controlled;
    public bool TerminalDone => terminalDone;
    public float Remaining => remaining;
    CargoAttackDrone controlled;
    Camera playerCamera;
    List<Behaviour> suspended=new List<Behaviour>();
    float remaining,yaw,pitch;
    bool terminalDone,waveStarted,finished;
    string hint;
    void Start(){playerCamera=player.GetComponentInChildren<Camera>();remoteCamera.gameObject.SetActive(false);}
    bool AllDown(CargoAttackDrone[] ds){foreach(var d in ds)if(d&&!d.Defeated)return false;return true;}
    public void OnDroneDown(CargoAttackDrone drone){}
    void Update()
    {
        if(player.IsDefeated){EndRemote();return;}
        if(GameplayGate.IsBlocked)return;
        var k=Keyboard.current;var m=Mouse.current;
        if(controlled)
        {
            remaining-=Time.deltaTime;
            if(remaining<=0){EndRemote();return;}
            if(Cursor.lockState==CursorLockMode.Locked&&m!=null){var d=m.delta.ReadValue()*mouseSensitivity;yaw+=d.x;pitch=Mathf.Clamp(pitch-d.y,-80,80);}
            remoteCamera.transform.rotation=Quaternion.Euler(pitch,yaw,0);
            bool atSocket=Vector3.Distance(controlled.transform.position,remoteSocket.position)<socketRange;
            hint=atSocket?"按 E 接入控制器，解除隔断":"按住右键飞行 · 松开悬停 · E 返回身体";
            if(k!=null&&k.eKey.wasPressedThisFrame){if(atSocket)ActivateTerminal();else EndRemote();}
            return;
        }
        if(terminalDone&&!waveStarted&&Vector3.Distance(player.transform.position,secondWaveTrigger.position)<7)
        { waveStarted=true;foreach(var d in secondWave)d.gameObject.SetActive(true); }
        if(IsSatisfied&&exitDoor)exitDoor.SetActive(false);
        if(IsSatisfied&&Vector3.Distance(player.transform.position,exitPoint.position)<2.5f){if (!string.IsNullOrEmpty(nextSceneName)) { GameStateManager.LoadLevel(nextSceneName); return; } finished=true;GameStateManager.NotifyLevelCompleted();}
        hint=!firstDrone.Defeated?"格挡积蓄冲量，将无人机击向墙面":!terminalDone?"对损坏的无人机按 E 接管；沿绿色指示飞往上层控制器":!IsSatisfied?"穿过隔断，清除后厅两架无人机":"出口已开启，前往前方通道";
        if(k==null||!k.eKey.wasPressedThisFrame)return;
        if(!terminalDone&&firstDrone.Defeated&&Vector3.Distance(player.transform.position,serviceDock.position)<3)
        {firstDrone.Dock(serviceDock.position+Vector3.up*1.1f);BeginRemote(firstDrone);return;}
        if(Physics.Raycast(playerCamera.transform.position,playerCamera.transform.forward,out var hit,5,~0,QueryTriggerInteraction.Ignore))
        {var drone=hit.collider.GetComponentInParent<CargoAttackDrone>();if(drone&&drone.Defeated&&(!waveStarted||AllDown(secondWave)))BeginRemote(drone);}
    }
    void FixedUpdate()
    {
        if(!controlled||GameplayGate.IsBlocked)return;
        if(Mouse.current!=null&&Mouse.current.rightButton.isPressed&&Cursor.lockState==CursorLockMode.Locked)
            controlled.MoveSafe(remoteCamera.transform.forward*flySpeed*Time.fixedDeltaTime);
    }
    void LateUpdate(){if(controlled)remoteCamera.transform.position=controlled.transform.position+Vector3.up*.15f+remoteCamera.transform.forward*.8f;}
    public bool BeginRemote(CargoAttackDrone drone)
    {
        if(controlled||!drone.Defeated||GameplayGate.IsBlocked)return false;
        controlled=drone;remaining=controlSeconds;yaw=0;pitch=0;
        player.GetComponent<PlayerMovement>()?.StopPunchLunge();
        // Only suspend player input/presentation. Health, restart and global pause remain active.
        foreach(var b in player.GetComponents<MonoBehaviour>())
            if(b.enabled&&(b is PlayerMovement||b is PlayerLook||b is PlayerChargedPunch||b is PlayerGauntletView||b.GetType().Name=="PlayerInteractor")) {suspended.Add(b);b.enabled=false;}
        playerCamera.gameObject.SetActive(false);remoteCamera.gameObject.SetActive(true);
        drone.SetRemote(true);Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;return true;
    }
    public void ActivateTerminal()
    {
        if(!controlled||Vector3.Distance(controlled.transform.position,remoteSocket.position)>=socketRange)return;
        terminalDone=true;if(dividerDoor)dividerDoor.SetActive(false);EndRemote();
    }
    public void EndRemote()
    {
        if(!controlled)return;
        var d=controlled;controlled=null;d.SetRemote(false);
        if(!terminalDone&&serviceDock)d.Dock(serviceDock.position+Vector3.up*1.1f);
        if(remoteCamera)remoteCamera.gameObject.SetActive(false);
        if(playerCamera)playerCamera.gameObject.SetActive(true);
        foreach(var b in suspended)if(b)b.enabled=true;suspended.Clear();
        if(!GameplayGate.IsBlocked){Cursor.lockState=CursorLockMode.Locked;Cursor.visible=false;}
    }
    void OnDisable(){EndRemote();}
    void OnGUI()
    {
        ChineseGameUI.EnsureFont();
        if(GameplayGate.IsBlocked&&!finished)return;
        var style=new GUIStyle(GUI.skin.box){fontSize=18,alignment=TextAnchor.MiddleCenter,wordWrap=true};
        if(finished){GUI.Box(new Rect(Screen.width/2-230,Screen.height/2-60,460,120),"卸货大厅完成\n本次试玩到此结束，下一关等待后续制作",style);if(GUI.Button(new Rect(Screen.width/2-100,Screen.height/2+75,200,44),"重新试玩本关"))GameStateManager.RestartCurrentRoom();return;}
        GUI.Box(new Rect(Screen.width/2-320,20,640,64),(controlled?$"应急推进剩余 {remaining:0.0} 秒\n":"卸货大厅\n")+hint,style);
        if(controlled)GUI.Label(new Rect(Screen.width/2-5,Screen.height/2-10,20,20),"＋");
    }
}


