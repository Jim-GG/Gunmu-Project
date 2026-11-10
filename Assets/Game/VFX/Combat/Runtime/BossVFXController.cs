using UnityEngine;using UDK7M;
namespace GameActions
{
 public sealed class BossVFXController:MonoBehaviour
 {
  public bool useAdvancedSteam=true;public UDK7MBoss boss;public Transform[] gasSockets,surfaceSockets;public GameObject steamPrefab,lightningPrefab;GameObject[] steam;GameObject lightning;bool holding,stunned;
  void Start(){var advanced=AdvancedCombatVFXLibrary.Load();if(useAdvancedSteam&&advanced&&advanced.tankSteam)steamPrefab=advanced.tankSteam;if(!boss)boss=GetComponent<UDK7MBoss>();if(gasSockets!=null&&steamPrefab){steam=new GameObject[gasSockets.Length];for(int i=0;i<steam.Length;i++){steam[i]=Instantiate(steamPrefab,gasSockets[i],false);steam[i].SetActive(false);}}if(lightningPrefab){lightning=Instantiate(lightningPrefab,transform,false);var arcs=lightning.GetComponent<SurfaceLightning>();if(arcs){arcs.boss=boss;arcs.surfaceAnchors=surfaceSockets;}lightning.SetActive(false);}}
  void Update(){if(!boss)return;bool h=boss.ActionTargetAlive&&(boss.State==BossState.SlamHold||boss.State==BossState.ChargeHold);bool s=boss.ActionTargetAlive&&boss.Phase==MachinePhase.Paralyzed;if(h!=holding){holding=h;if(steam!=null)foreach(var go in steam)if(go)go.SetActive(h);}if(s!=stunned){stunned=s;if(lightning)lightning.SetActive(s);}}
  void OnDisable(){holding=stunned=false;if(steam!=null)foreach(var go in steam)if(go)go.SetActive(false);if(lightning)lightning.SetActive(false);}
 }
}

