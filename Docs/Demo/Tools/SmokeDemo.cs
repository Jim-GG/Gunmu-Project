using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.AI;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.LowLevel;
using NullBoundary.Demo;

public static class DemoSmoke
{
    [Serializable] public sealed class Report { public bool passed; public string error; public List<string> checks=new List<string>(); }
    static Report report;
    static DemoFlow flow;
    static Mouse testMouse,previousMouse;
    static InputSettings previousSettings,verificationSettings;
    static bool holdingBlock;
    static string folder="Docs/Demo/Verification";
    public static async Task<string> Run()
    {
        report=new Report();flow=UnityEngine.Object.FindFirstObjectByType<DemoFlow>();
        Directory.CreateDirectory(folder);
        try
        {
            Check(Application.isPlaying,"Smoke verification runs only in Play Mode");
            Check(flow!=null,"Demo flow exists in the actual scene");
            previousSettings=InputSystem.settings;
            verificationSettings=UnityEngine.Object.Instantiate(previousSettings);
            verificationSettings.backgroundBehavior=InputSettings.BackgroundBehavior.IgnoreFocus;
            InputSystem.settings=verificationSettings;
            previousMouse=Mouse.current;
            testMouse=InputSystem.AddDevice<Mouse>("NB_Verification_Mouse");
            var ledger=new EncounterLedger();ledger.Reset(3);ledger.Register(1);ledger.Defeat(1);
            Check(!ledger.Complete,"Pending spawns prevent early encounter completion");
            Check(!ledger.Defeat(1)&&!ledger.Defeat(999),"Duplicate and unrelated deaths do not decrement the encounter");
            ledger.Register(2);ledger.Register(3);ledger.Defeat(2);ledger.Defeat(3);
            Check(ledger.Complete,"Only all three registered enemies complete the encounter");
            Check(DemoFlow.PhaseError(359,1)==2,"Calibration handles the 0/360 wrap boundary");
            Check(DemoPlayer.FacesAttack(Vector3.forward,Vector3.forward)&&
                !DemoPlayer.FacesAttack(Vector3.forward,Vector3.back),"Blocking has a forward-facing cone");

            // Drive the real CharacterController deterministically without live desktop input.
            flow.player.enabled=false;
            flow.RestartFromBeginning();
            await Task.Delay(150);
            flow.intro.director.Pause();
            Seek(9.8);
            Check(flow.intro.ceilingLight.intensity==0,"Timeline switches the ceiling light off after two flashes");
            Seek(11.2);
            Check(flow.intro.brokenCable.activeSelf&&!flow.intro.intactCable.activeSelf&&flow.intro.cabin.position.y<0,
                "Timeline drives the shaft shot, cable rupture and falling cabin");
            await Capture("01-shaft");
            Seek(17.6);
            Check(flow.intro.Black>.99f,"Timeline holds the unconscious blackout");
            flow.intro.Complete();flow.intro.Complete();
            Check(flow.Stage==DemoStage.Exploring&&flow.player.controlEnabled&&flow.intro.Black==0,
                "Skip and repeated completion safely restore the playable wake state");
            flow.player.Teleport(new Vector3(0,.08f,6));
            await Capture("02-canteen");

            flow.player.Teleport(new Vector3(0,.08f,20.6f),-40);
            await Until(()=>flow.FridgePlayed,2);
            await Task.Delay(1600);
            Check(flow.player.controlEnabled&&flow.Stage==DemoStage.Exploring,"Fridge event preserves player control");
            Check(Mathf.Abs(Mathf.DeltaAngle(flow.fridgeHinge.localEulerAngles.y,-105))<1,"Fridge door reaches its stable open state");
            await Capture("03-kitchen");
            flow.player.Teleport(new Vector3(0,.08f,29));
            await Task.Delay(100);await Capture("04-resonating");
            flow.player.Teleport(new Vector3(0,.08f,32.5f));
            await Until(()=>flow.Stage==DemoStage.Chase,2);
            Check(flow.chaser.ActiveThreat&&flow.chaser.GetComponent<NavMeshAgent>().isOnNavMesh,
                "Approach triggers a real NavMesh chase");
            await MoveTo(new Vector3(0,.08f,68.5f),5.4f,12);
            Check(flow.player.IsAlive,"The route is escapable at the configured sprint speed");
            flow.Interact(DemoAction.CloseChaseDoor);
            await Until(()=>flow.Stage==DemoStage.SafeLab,2);
            Check(flow.chaseDoor.IsClosed&&!flow.chaser.ActiveThreat,"Closing gate A terminates the chase");
            Check(flow.chaser.transform.position.z<66.3f,"The chaser stays on the corridor side of the gate");

            flow.Interact(DemoAction.TakeGauntlet);
            Check(!flow.player.hasGauntlet,"The pickup remains locked before calibration");
            flow.Interact(DemoAction.Calibrate);
            Check(flow.Stage==DemoStage.Calibrating&&flow.InputBlocked,"Calibration owns the UI input mode");
            flow.calibrationPhase=210;await Task.Delay(950);
            await Capture("05-calibration");
            Check(flow.ConfirmCalibration(),"Aligned waveforms unlock the rack");
            Check(!flow.ConfirmCalibration()&&!flow.InputBlocked,"Calibration completion is idempotent and releases input");
            flow.Interact(DemoAction.TakeGauntlet);
            flow.player.hands.gameObject.SetActive(true);
            Check(flow.player.hasGauntlet&&flow.CheckpointIndex==2&&!flow.gauntletPickup.gameObject.activeSelf,
                "Actual pickup equips gauntlets and creates the combat checkpoint");
            flow.Interact(DemoAction.TakeGauntlet);
            await Until(()=>flow.Stage==DemoStage.Combat&&flow.Ledger.Spawned==1,5);
            flow.player.Teleport(new Vector3(3,.08f,77),90);
            SetBlock(true);
            await Until(()=>flow.player.charge>.9f,8);
            Check(flow.player.health==100,"An actual enemy attack is blocked and stored as impulse");
            await Capture("06-first-block");
            flow.player.Punch();
            await Until(()=>flow.Ledger.Defeated>=1,2);
            Check(flow.Stage==DemoStage.Combat&&!flow.Ledger.Complete&&flow.exitDoor.IsClosed,
                "Defeating the first enemy does not unlock the exit while two spawns are pending");
            await Until(()=>flow.Ledger.Spawned==3,4);
            for(int iteration=0;iteration<8 && flow.Stage==DemoStage.Combat;iteration++)
            {
                SetBlock(true);
                await Until(()=>flow.player.charge>.9f||flow.Stage==DemoStage.Cleared||!flow.player.IsAlive,8);
                if(flow.Stage==DemoStage.Cleared)break;
                Check(flow.player.IsAlive,"Combat teaching leaves a recoverable player state");
                flow.player.Punch();await Task.Delay(850);
            }
            SetBlock(false);
            Check(flow.Stage==DemoStage.Cleared&&flow.Ledger.Defeated==3,"The gauntlet defeats all three encounter enemies");
            await Task.Delay(900);
            Check(!flow.exitDoor.IsClosed&&flow.chaser&&!flow.chaser.IsDown,"Exit unlock ignores the still-living corridor chaser");
            flow.player.Teleport(new Vector3(0,.08f,79));
            await Capture("07-lab-cleared");
            await MoveTo(new Vector3(0,.08f,92.3f),5.4f,5);
            Check(flow.Stage==DemoStage.Ended,"Walking through the unlocked exit completes the Demo");
            await Capture("08-complete");

            flow.RestartCheckpoint();
            Check(flow.Stage==DemoStage.Armed&&flow.player.hasGauntlet&&flow.player.health==100&&flow.player.charge==0,
                "Combat retry restores health, gauntlet and empty storage");
            Check(flow.chaseDoor.IsClosed&&flow.exitDoor.IsClosed&&flow.Ledger.Defeated==0,
                "Combat retry restores gates and the encounter ledger");
            await Until(()=>flow.Ledger.Spawned==1,5);
            Check(flow.Ledger.Expected==3&&flow.Ledger.Spawned==1,
                "Combat retry restarts the complete 1+2 encounter rather than retaining defeated units");
            report.passed=true;
        }
        catch(Exception exception)
        {
            report.error=exception.ToString();
            if(flow)report.error+=" / health="+flow.player.health+" charge="+flow.player.charge+
                " blocked="+flow.player.Blocking+" mouse="+(Mouse.current==null?"none":Mouse.current.name);
        }
        finally
        {
            SetBlock(false);
            if(testMouse!=null){InputSystem.RemoveDevice(testMouse);testMouse=null;}
            if(previousMouse!=null)previousMouse.MakeCurrent();
            if(previousSettings!=null)InputSystem.settings=previousSettings;
            if(verificationSettings)UnityEngine.Object.Destroy(verificationSettings);
            if(flow)flow.player.enabled=true;
            File.WriteAllText(folder+"/smoke-report.json",JsonUtility.ToJson(report,true));
        }
        return JsonUtility.ToJson(report,true);
    }
    static void Seek(double time) { flow.intro.director.time=time;flow.intro.director.Evaluate(); }
    static void Check(bool success,string name)
    {
        if(!success)throw new Exception("FAILED: "+name);
        report.checks.Add(name);
    }
    static async Task Until(Func<bool> predicate,float seconds)
    {
        float deadline=Time.realtimeSinceStartup+seconds;
        while(!predicate())
        {
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Timed out at stage "+flow.Stage+
                " / spawned "+flow.Ledger.Spawned+" / defeated "+flow.Ledger.Defeated);
            await Task.Delay(20);
            if(holdingBlock)SetBlock(true);
        }
    }
    static async Task MoveTo(Vector3 target,float speed,float timeout)
    {
        float deadline=Time.realtimeSinceStartup+timeout;
        var motor=flow.player.GetComponent<CharacterController>();
        float lastMove=Time.realtimeSinceStartup;
        while(Vector2.Distance(new Vector2(flow.player.transform.position.x,flow.player.transform.position.z),
            new Vector2(target.x,target.z))>.2f)
        {
            if(Time.realtimeSinceStartup>deadline)throw new Exception("Movement blocked at "+flow.player.transform.position+" toward "+target);
            Vector3 direction=target-flow.player.transform.position;direction.y=0;
            float now=Time.realtimeSinceStartup;
            motor.Move(direction.normalized*speed*Mathf.Min(now-lastMove,.08f));
            lastMove=now;
            await Task.Delay(15);
        }
    }
    static void SetBlock(bool value)
    {
        holdingBlock=value;
        var mouse=testMouse??Mouse.current;
        if(mouse!=null)
        {
            if(testMouse!=null){testMouse.MakeCurrent();InputSystem.EnableDevice(testMouse);}
            InputSystem.QueueStateEvent(mouse,
            value?new MouseState().WithButton(MouseButton.Right):new MouseState());
        }
    }
    static async Task Capture(string name)
    {
        ScreenCapture.CaptureScreenshot(folder+"/"+name+".png");await Task.Delay(250);
    }
}

