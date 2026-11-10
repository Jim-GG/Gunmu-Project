using UnityEngine;
namespace GameActions
{
 public sealed class GroundImpactDecals:MonoBehaviour
 {
  public GameObject[] prefabs;public GameObject lightDebris,heavyDebris;public float lifetime=25;public int maximum=12;GameObject[] pool;float[] born,radii;int cursor;
  public void Stamp(Vector3 position,float radius)
  {
   RaycastHit ground=default;float nearest=float.MaxValue;foreach(var hit in Physics.RaycastAll(position+Vector3.up*3,Vector3.down,12,~0,QueryTriggerInteraction.Ignore))if(!hit.transform.IsChildOf(transform)&&hit.normal.y>.5f&&hit.collider.GetComponentInParent<IActionTarget>()==null&&hit.distance<nearest){ground=hit;nearest=hit.distance;}
   if(nearest==float.MaxValue||prefabs==null||prefabs.Length==0)return;var debris=radius>=1.5f?heavyDebris:lightDebris;if(debris)CombatEffects.Burst(debris,ground.point+ground.normal*.08f,ground.normal,1,8);else CombatEffects.Emit(radius>=1.5f?CombatEffect.ConcreteImpactHeavy:CombatEffect.ConcreteImpactLight,ground.point+ground.normal*.08f,ground.normal);if(pool==null){pool=new GameObject[maximum];born=new float[maximum];radii=new float[maximum];}
   for(int i=0;i<pool.Length;i++)if(pool[i]&&pool[i].activeSelf&&Time.time-born[i]<2.5f&&Vector3.Distance(pool[i].transform.position,ground.point)<radius*.35f){if(radius>radii[i]){pool[i].transform.localScale=Vector3.one*radius*2;radii[i]=radius;}born[i]=Time.time;return;}
   int index=cursor++%pool.Length;if(!pool[index]){pool[index]=Instantiate(prefabs[index%prefabs.Length]);pool[index].name="Pooled drill impact decal";}
   var go=pool[index];go.transform.SetPositionAndRotation(ground.point+ground.normal*.025f,Quaternion.FromToRotation(Vector3.back,ground.normal)*Quaternion.Euler(0,0,Random.Range(0,360)));go.transform.localScale=Vector3.one*radius*2;go.SetActive(true);var crack=go.GetComponent<GroundCrackImpact>();if(crack){crack.lifetime=lifetime;crack.SpawnCrack(ground.point,ground.normal,radius*2,crack.depth);}born[index]=Time.time;radii[index]=radius;
  }
  void Update(){if(pool==null)return;for(int i=0;i<pool.Length;i++)if(pool[i]&&pool[i].activeSelf&&Time.time-born[i]>lifetime)pool[i].SetActive(false);}
  public void Clear(){if(pool!=null)foreach(var go in pool)if(go)go.SetActive(false);}
  void OnDestroy(){if(pool!=null)foreach(var go in pool)if(go)Destroy(go);}
 }
}

