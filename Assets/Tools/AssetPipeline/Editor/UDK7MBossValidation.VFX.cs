using System.Linq;
using System.Reflection;
using UnityEngine;
using GameActions;
using Resonator.AI;
using UDK7M;
namespace AssetPipeline.Editor
{
 public static partial class UDK7MBossValidation
 {
  static void CheckAdvancedPresentation()
  {
   var boss=arena.boss;Boss(Vector3.zero);Player(new Vector3(0,0,30));var show=boss.GetComponent<UDK7MPresentation>();Assert(show,"Missing presentation controller");show.SendMessage("LateUpdate");boss.ForceState(BossState.ChargeHold);show.SendMessage("LateUpdate");var wind=boss.GetComponentInChildren<DashWindConeController>(true);Assert(wind&&wind.prefabPlayback&&wind.gameObject.activeSelf&&Vector3.Distance(wind.TipPosition,boss.DrillTip)<.001f,"User wind cone is not attached at drill tip");
   boss.transform.position+=Vector3.forward*5*Time.deltaTime;show.SendMessage("LateUpdate");Assert(show.DustRate>0&&Vector3.Distance(wind.TipPosition,boss.DrillTip)<.001f,"Moving boss dust/tip follow failed");show.SendMessage("LateUpdate");Assert(show.DustRate==0,"Stationary machine kept producing track dust");boss.ForceState(BossState.Idle);show.SendMessage("LateUpdate");Assert(!wind.gameObject.activeSelf,"Wind leaked outside ChargeHold");Pass("User wind follows animated drill tip; track dust follows actual travel and stops with machine");
   var decals=boss.GetComponent<GroundImpactDecals>();Assert(decals,"Missing ground crack component");decals.Clear();boss.ForceState(BossState.ChargeRecover);boss.SendMessage("FrameEvent",new MotionFrameEvent{name="SlamImpact"});var field=typeof(GroundImpactDecals).GetField("pool",BindingFlags.NonPublic|BindingFlags.Instance);int Count()=>((GameObject[])field.GetValue(decals))?.Count(g=>g&&g.activeSelf)??0;Assert(Count()==0,"ChargeRecover stamped a crater");boss.ForceState(BossState.SlamRecover);boss.SendMessage("FrameEvent",new MotionFrameEvent{name="SlamImpact"});Assert(Count()>0,"SlamRecover stopped stamping craters");decals.Clear();boss.ForceState(BossState.Idle);Pass("ChargeRecover has no crater; SlamRecover retains impact decals");
  }
 }
}
