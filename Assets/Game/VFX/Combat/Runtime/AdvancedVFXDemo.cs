using UnityEngine;using UnityEngine.InputSystem;
namespace GameActions
{
 public sealed class AdvancedVFXDemo:MonoBehaviour
 {
  public AdvancedCombatVFXLibrary library;public UDK7MPresentation machine;public WeaponEnergyVFXController weapon;public bool automatic=true;[Range(0,4)]public int mode;public float interval=8;float next;
  public void Trigger()
  {
   if(!library)return;
   if(mode==0)machine.PlayDefeat();
   else if(mode==1)machine.PlayPillarShatter(new Vector3(0,2,1));
   else if(mode==2){if(!machine.boss.drill)foreach(var t in machine.GetComponentsInChildren<Transform>())if(t.name=="DrillRoll"){machine.boss.drill=t;break;}var go=Instantiate(library.windCone);var c=go.GetComponent<DashWindConeController>();c.PlayDash(machine.boss.DrillTip,machine.transform.forward,library.chargeWindScale);c.AlignTip(machine.boss.DrillTip,machine.transform.forward,library.chargeWindScale);Destroy(go,1);}
   else if(mode==3)CombatEffects.Burst(library.heatBurst,new Vector3(3,0,4),Vector3.up,1,4);
   else if(weapon){weapon.preview=true;weapon.previewCharge=weapon.previewCharge>.9f?.33f:Mathf.Min(1,weapon.previewCharge+.33f);}
  }
  void Update(){var k=Keyboard.current;if(k!=null){if(k.digit1Key.wasPressedThisFrame)mode=0;if(k.digit2Key.wasPressedThisFrame)mode=1;if(k.digit3Key.wasPressedThisFrame)mode=2;if(k.digit4Key.wasPressedThisFrame)mode=3;if(k.digit5Key.wasPressedThisFrame)mode=4;if(k.spaceKey.wasPressedThisFrame)Trigger();if(k.pKey.wasPressedThisFrame)Time.timeScale=Time.timeScale==0?1:0;}if(automatic&&Time.time>=next){next=Time.time+interval;Trigger();}}
  void OnGUI(){GUI.Label(new Rect(15,15,900,25),"1: 7+1 explosions  2: crystal trails  3: drill-tip wind cone  4: heat haze  5: weapon energy | Space: trigger | P: pause");}
  void OnDisable(){Time.timeScale=1;}
 }
}
