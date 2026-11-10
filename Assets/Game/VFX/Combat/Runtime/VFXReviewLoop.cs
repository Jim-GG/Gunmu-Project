using UnityEngine;
namespace GameActions
{
 public sealed class VFXReviewLoop:MonoBehaviour
 {
  public GameObject shockwavePrefab;public float interval=3.5f;float next;
  void Update(){if(Time.time<next)return;next=Time.time+interval;CombatEffects.Emit(CombatEffect.Sparks,new Vector3(-4,.25f,0),Vector3.up);CombatEffects.Emit(CombatEffect.PurpleBlood,new Vector3(0,1.5f,0),Vector3.up);CombatEffects.Emit(CombatEffect.CrystalShards,new Vector3(0,1.5f,0),Vector3.up);if(shockwavePrefab){var go=Instantiate(shockwavePrefab);go.transform.position=new Vector3(0,.06f,0);go.GetComponent<ICombatWave>()?.Begin(5,1.5f);}}
 }
}
