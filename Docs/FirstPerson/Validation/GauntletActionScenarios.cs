using System;using System.Linq;using System.Collections.Generic;using System.Threading.Tasks;
using UnityEngine;using UnityEditor;using GameActions;using FirstPerson;using Resonator.AI;
public sealed class GauntletTestTarget : MonoBehaviour,IActionTarget
{
 public bool alive=true;public int hits;public float damage;
 public bool ActionTargetAlive=>alive;
 public Transform ActionRoot=>transform;
 public Vector3 ActionAimPoint=>transform.position+Vector3.up*1.05f;
 public void ReceiveActionHit(float amount,Vector3 source,float impulse){hits++;damage+=amount;}
}
public static class GauntletActionScenarios
{
 static void Check(bool value,string message){if(!value)throw new Exception(message);}
 static GameObject root,floor;static GauntletCombat c;static ActionSet profile;static GauntletTestTarget target;static readonly List<GameObject> garbage=new List<GameObject>();static Vector3 home=new Vector3(60,0,60);
 static GauntletTestTarget Dummy(Vector3 position)
 {
  var go=new GameObject("Action Validation Target");garbage.Add(go);go.transform.position=home+position;
  var collider=go.AddComponent<CapsuleCollider>();collider.height=1.8f;collider.radius=.28f;collider.center=Vector3.up*.9f;
  return go.AddComponent<GauntletTestTarget>();
 }
 static void Step(float time)=>c.Tick(time);
 static void To(float frame){float dt=Mathf.Max(0,frame-c.CurrentFrame)/c.actions.Current.Motion.track.framesPerSecond/c.actions.Current.Motion.speed;Step(dt+.00001f);}
 static void Reset()
 {
  c.Cancel();root.GetComponent<CharacterController>().enabled=false;root.transform.SetPositionAndRotation(home,Quaternion.identity);root.GetComponent<CharacterController>().enabled=true;
  var pivot=root.transform.Find("View Pivot");pivot.localRotation=Quaternion.identity;
  target.transform.position=home+new Vector3(0,0,1.3f);target.alive=true;target.hits=0;target.damage=0;Physics.SyncTransforms();
 }
 static void Punch(int n)
 {
  c.Press();if(n>1)Step(n==2?.5f:n==3?2.15f:3.2f);c.Release();Check(c.ActivePunch==n,"Expected punch"+n+", state="+c.State+", actual="+c.ActivePunch);
 }
 public static async Task<object> Run()
 {
  await Task.CompletedTask;
  Check(Application.isPlaying,"Enter Play Mode first");var cases=new List<string>();
  floor=GameObject.CreatePrimitive(PrimitiveType.Cube);floor.name="Action Validation Floor";floor.transform.position=home+Vector3.down*.3f;floor.transform.localScale=new Vector3(16,.6f,16);
  root=UnityEngine.Object.Instantiate(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Game/Player/Prefabs/FirstPersonPlayer.prefab"),home,Quaternion.identity);root.name="Gauntlet Validation Player";
  root.GetComponent<FirstPersonInput>().enabled=false;root.GetComponent<FirstPersonMotor>().enabled=false;
  foreach(var cam in root.GetComponentsInChildren<Camera>())cam.enabled=false;
  foreach(var listener in root.GetComponentsInChildren<AudioListener>())listener.enabled=false;
  c=root.GetComponent<GauntletCombat>();c.enabled=false;c.InitializePipeline();
  profile=UnityEngine.Object.Instantiate(c.profile);c.profile=c.actions.profile=profile;
  target=Dummy(new Vector3(0,0,1.3f));int attacks=0;Action<int> onAttack=n=>attacks++;c.AttackStarted+=onAttack;
  try
  {
   foreach(var a in profile.actions)if(a.isAttack)a.action.displacement.enabled=false;
   Reset();Punch(1);Check(Mathf.Abs(c.CurrentFrame-10)<.01f,"Quick punch should skip redundant windup using configured start frame");To(13.9f);Check(target.hits==0,"Damage during windup");To(14.1f);Check(target.hits==1,"No hit at active source frame");To(29);Check(target.hits==1,"Repeated damage during one impact group");cases.Add("Quick punch: no windup damage, hit at source frame 14, one contact per target");
   Reset();var quick=profile.Find("punch1");float speed=quick.Motion.speed;quick.Motion.speed=2;Punch(1);Step(.05f);Check(target.hits==0,"Fast clip hit before frame 14");Step(.02f);Check(target.hits==1&&c.CurrentFrame>=14,"Hit timing did not follow clip speed");quick.Motion.speed=speed;cases.Add("Animation speed changes effective hit time without changing authored hit frames");
   Reset();Punch(1);To(16);int before=attacks;c.Press();c.Release();Check(c.HasBufferedInput&&c.ActivePunch==1,"Click not buffered during recovery");To(17.8f);Check(attacks==before,"Cancelled before legal gate");To(18.1f);Check(attacks==before+1&&c.ActivePunch==1&&!c.HasBufferedInput,"Buffered short click not executed exactly once at cancel gate");Step(1);Check(attacks==before+1,"One buffered input expanded into multiple attacks");cases.Add("Buffered short click cancels recovery at frame 18, keeps release, executes once");
   Reset();Punch(1);before=attacks;c.Press();c.Release();Check(!c.HasBufferedInput,"Too-early mash should be outside the authored buffer window");Step(1);Check(attacks==before,"Rejected early input replayed later");cases.Add("Input outside pre-input window is ignored instead of scheduling future attacks");
   Reset();var edge=profile.Transition("punch1","charge");float oldGate=edge.cancelFrame,oldBuffer=edge.bufferStartFrame;edge.cancelFrame=29;edge.bufferStartFrame=0;Punch(1);before=attacks;c.Press();c.Release();Step(.25f);Check(!c.HasBufferedInput,"Stale input did not expire");Step(1);Check(attacks==before,"Expired input executed after recovery");edge.cancelFrame=oldGate;edge.bufferStartFrame=oldBuffer;cases.Add("Pre-input expires after configured 0.22 seconds");
   Reset();Punch(1);c.BeginBlock();Check(!c.IsBlocking,"Guard bypassed protected startup/impact");To(17.9f);Check(!c.IsBlocking,"Guard before cancel gate");To(18.1f);Check(c.IsBlocking&&c.State==GauntletCombat.Phase.ParryStart,"Held guard did not cancel recovery");c.EndBlock();Check(!c.IsBlocking&&c.State==GauntletCombat.Phase.ParryHolster,"Release guard not immediate");cases.Add("Held block cancels at legal gate; releasing block immediately removes protection");
   Reset();c.Press();Step(.3f);c.BeginBlock();Check(c.IsBlocking,"Guard cannot cancel charge");Check(c.TryBlock(root.transform.position+Vector3.forward*2),"Valid front block failed");Check(c.State==GauntletCombat.Phase.ParryHit,"Parry hit not interruptible");Step(.4f);Check(c.State==GauntletCombat.Phase.ParryKeep,"Parry hit did not return to keep");c.EndBlock();Step(.4f);Check(c.State==GauntletCombat.Phase.Idle,"Parry holster did not finish");cases.Add("Charge-to-guard, hit interrupt, keep and holster use shared playback");
   Reset();c.BeginBlock();Step(.4f);c.Press();Check(c.State==GauntletCombat.Phase.Charging,"Fresh attack input did not leave held guard");c.Release();Check(c.ActivePunch==1,"Guard-to-attack click lost");To(18.1f);Check(c.IsBlocking,"Held right button did not resume guard after the punch impact");cases.Add("Fresh attack cancels held guard, while held right resumes guard after the punch");
   // Consume any energy, then seed a known amount through the actual parry interface.
   Reset();c.Press();Step(4);c.Cancel();c.BeginBlock();for(int i=0;i<3;i++)c.TryBlock(root.transform.position+Vector3.forward*2);c.EndBlock();Step(.4f);Check(c.BlockEnergy==3,"Block energy seed");c.Press();Step(13/30f);Check(c.policy.Frame(c.ChargeSeconds)==31&&c.BlockEnergy==2,"First energy skip");Step(2/30f);Check(c.policy.Frame(c.ChargeSeconds)==61&&c.BlockEnergy==1,"Second energy skip");Step(2/30f);Check(c.policy.Frame(c.ChargeSeconds)==91&&c.BlockEnergy==0,"Third energy skip");Step(1.1f/30);Check(c.State==GauntletCombat.Phase.Full,"Full charge transition");Step(2.3f);Check(c.CurrentFrame<=50.001f,"Charge-full sampled beyond the valid first 50 frames");c.Release();Check(c.ActivePunch==4,"Full release did not punch4");cases.Add("Three block-energy skips and looping charge-full frames 0-50 remain intact");
   Reset();c.Press();Step(.2f);c.Release();Check(c.State==GauntletCombat.Phase.ReleasePending,"Early non-click release must wait");Step(.24f);Check(c.ActivePunch==2,"Early release did not wait for charge frame 13");cases.Add("Non-click release before charge frame 13 retains its release gate");
   Reset();Punch(1);To(16);c.Press();To(18.1f);Check(c.State==GauntletCombat.Phase.Charging&&c.ChargeSeconds<.03f,"Buffered hold must start a new charge, without retroactive charging");Step(.2f);c.Release();Check(c.State==GauntletCombat.Phase.ReleasePending,"Buffered hold lost its intended charge");cases.Add("Buffered held input starts charging from acceptance, not from the preceding attack");
   Reset();var off=target.transform.position;target.transform.position+=Vector3.right*2;Physics.SyncTransforms();Punch(1);Step(1);Check(target.hits==0,"Off-axis target took damage");cases.Add("Spatial miss does not turn into guaranteed damage");
   Reset();var wall=GameObject.CreatePrimitive(PrimitiveType.Cube);garbage.Add(wall);wall.transform.position=home+new Vector3(0,1,.65f);wall.transform.localScale=new Vector3(2,2,.12f);Physics.SyncTransforms();Punch(1);Step(.5f);Check(target.hits==0,"Hitbox dealt damage through a wall");UnityEngine.Object.DestroyImmediate(wall);cases.Add("World geometry blocks damage even when overlap volume reaches the target");
   var travel=new List<float>();
   for(int i=1;i<=4;i++)
   {
    Reset();var a=profile.Find("punch"+i);a.action.displacement.enabled=true;a.groundedAssistanceOnly=false;
    var h=a.action.hits[0];var offset=h.position;h.position=new Vector3(5,0,1);target.transform.position=home+Vector3.forward*2.9f;Physics.SyncTransforms();Punch(i);
    Check(c.AssistTarget,"No assist target punch"+i+" aim="+c.attackOrigin.position+" forward="+c.attackOrigin.forward+" target="+target.ActionAimPoint+" cone="+Vector3.Angle(c.attackOrigin.forward,target.ActionAimPoint-c.attackOrigin.position)+" visible="+ActionVisibility.Clear(c.attackOrigin.position,target.ActionAimPoint,root.transform,target.transform,~0));
    float start=h.startFrame;To(start-.05f);Check(Vector3.Distance(root.transform.position,home)<.002f,"Absorption during startup, punch"+i);To(h.endFrame+.1f);
    float moved=(root.transform.position-home).magnitude,max=a.action.displacement.segments[0].maxDistance;
    Check(moved>max-.06f&&moved<=max+.01f,"Assist budget incorrect punch"+i+": "+moved+" / "+max);
    travel.Add(moved);Vector3 stopped=root.transform.position;Step(.4f);Check(Vector3.Distance(stopped,root.transform.position)<.002f,"Assistance continued after hit window");h.position=offset;
   }
   cases.Add("Each punch absorbs only in active frames; distinct 0.65/0.85/1.00/1.15 metre budgets");
   Reset();target.transform.position=home+new Vector3(0,0,2.05f);Physics.SyncTransforms();Punch(1);Step(.4f);Check(target.hits==1&&c.AssistTravel>.2f,"Assist failed to turn a short spatial miss into a real contact");cases.Add("Assistance improves reach through actual player movement and collision hitboxes");
   Reset();target.transform.position=home+new Vector3(0,0,-2);Physics.SyncTransforms();Punch(1);Step(.3f);Check(c.AssistTravel<.001f,"Pulled toward target behind player");cases.Add("No assistance to targets behind the player");
   Reset();target.transform.position=home+new Vector3(0,0,2);Physics.SyncTransforms();Punch(1);target.alive=false;Step(.4f);Check(c.AssistTravel<.001f,"Pulled toward dead target");cases.Add("Dead or lost target ends assistance");
   Reset();target.transform.position=home+Vector3.forward*2;Physics.SyncTransforms();Punch(1);wall=GameObject.CreatePrimitive(PrimitiveType.Cube);garbage.Add(wall);wall.transform.position=home+new Vector3(0,1,.85f);wall.transform.localScale=new Vector3(2,2,.1f);Physics.SyncTransforms();Step(.5f);Check(c.AssistTravel<.001f&&target.hits==0,"New obstruction did not stop assistance and damage");UnityEngine.Object.DestroyImmediate(wall);cases.Add("Obstruction appearing after target acquisition stops assistance and damage");
   Reset();Punch(1);To(13);c.Cancel();Step(.5f);Check(target.hits==0&&!c.HasBufferedInput&&c.AssistTravel<.001f,"Cancelled attack kept active hit or movement");cases.Add("Cancel/Escape removes pending inputs, active hits and target movement");
   Reset();Punch(1);To(16);c.Press();c.Release();c.externalInputEnabled=false;Step(.1f);Check(c.State==GauntletCombat.Phase.Idle&&!c.HasBufferedInput,"Cutscene/input lock did not clear action");c.externalInputEnabled=true;cases.Add("External input lock clears buffered commands and current attack");
   return new{passed=true,cases=cases.ToArray(),assistanceTravel=travel.ToArray(),profile="Shared Action Set",core=typeof(ActionMotionPlayer).FullName};
  }
  finally
  {
   c.AttackStarted-=onAttack;c.Cancel();if(c.actions&&c.actions.motion)c.actions.motion.ReleaseGraph();
   UnityEngine.Object.DestroyImmediate(root);UnityEngine.Object.DestroyImmediate(floor);UnityEngine.Object.DestroyImmediate(profile);foreach(var go in garbage)if(go)UnityEngine.Object.DestroyImmediate(go);garbage.Clear();
  }
 }
}
