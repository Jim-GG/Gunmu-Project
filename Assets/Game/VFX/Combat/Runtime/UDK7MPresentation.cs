using System.Collections;using System.Collections.Generic;using UnityEngine;using UnityEngine.Events;using UnityEngine.VFX;using UDK7M;
namespace GameActions
{
 public sealed class UDK7MPresentation:MonoBehaviour
 {
  public UDK7MBoss boss;public AdvancedCombatVFXLibrary library;public UnityEvent onFinalExplosion=new UnityEvent();
  public int ExplosionsPlayed{get;private set;}public float DustRate{get;private set;}
  public Vector3 LastFinalBlastPosition{get;private set;}
  VisualEffect[] tracks;DashWindConeController wind;Vector3 previous;float nextWind;readonly List<GameObject> owned=new List<GameObject>();Coroutine defeat;
  public static UDK7MPresentation Ensure(UDK7MBoss boss){var c=boss.GetComponent<UDK7MPresentation>();if(!c)c=boss.gameObject.AddComponent<UDK7MPresentation>();c.boss=boss;if(!c.library)c.library=AdvancedCombatVFXLibrary.Load();return c;}
  void Start(){if(!boss)boss=GetComponent<UDK7MBoss>();if(!library)library=AdvancedCombatVFXLibrary.Load();CreateTrackDust();previous=transform.position;}
  void CreateTrackDust(){if(!library||!library.trackDustGraph||tracks!=null)return;tracks=new VisualEffect[library.trackDustPositions.Length];for(int i=0;i<tracks.Length;i++){var go=new GameObject("Track rear GPU dust "+(i+1));go.transform.SetParent(transform,false);go.transform.localPosition=library.trackDustPositions[i];tracks[i]=go.AddComponent<VisualEffect>();tracks[i].visualEffectAsset=library.trackDustGraph;}}
  void LateUpdate()
  {
   if(!boss||!library)return;CreateTrackDust();float speed=Vector3.ProjectOnPlane(transform.position-previous,Vector3.up).magnitude/Mathf.Max(Time.deltaTime,.001f);previous=transform.position;if(speed>20)speed=0;DustRate=boss.ActionTargetAlive?Mathf.Clamp(speed*library.dustParticlesPerMetre,0,library.maximumTrackDustRate):0;
   if(tracks!=null)foreach(var v in tracks)if(v){if(v.HasFloat("SpawnRate"))v.SetFloat("SpawnRate",DustRate);if(v.HasVector3("SpawnPosition"))v.SetVector3("SpawnPosition",v.transform.position);if(v.HasVector3("EmissionVelocity"))v.SetVector3("EmissionVelocity",-transform.forward*(1+speed*.2f)+Vector3.up*.2f);}
   if(boss.ActionTargetAlive&&boss.State==BossState.ChargeHold&&library.windCone)
   {
    if(!wind){var go=Instantiate(library.windCone,transform);wind=go.GetComponent<DashWindConeController>();}
    if(library.chargeWindFollowDrillTip)wind.AlignTip(boss.DrillTip+transform.TransformVector(library.chargeWindTipOffset),transform.forward,library.chargeWindScale);
    else wind.transform.SetPositionAndRotation(transform.TransformPoint(library.chargeWindOffset),transform.rotation);
    if(Time.time>=nextWind){nextWind=Time.time+library.chargeWindRepeat;wind.PlayDash(wind.transform.position,transform.forward,library.chargeWindScale);}
   }
   else if(wind)wind.gameObject.SetActive(false);
  }
  GameObject Burst(GameObject prefab,Vector3 point,float scale=1,float life=8){if(!prefab)return null;var go=CombatEffects.Burst(prefab,point,Vector3.up,scale,life);owned.RemoveAll(x=>!x);if(go&&!owned.Contains(go))owned.Add(go);return go;}
  public GameObject EnergyRelease(Vector3 point,float radius,float distortion,float chromatic){if(!library)return null;var go=Burst(library.chromaticSphere,point,1,2);if(go)go.GetComponent<ChromaticWaveController>()?.PlayAt(point,radius,.65f,distortion,chromatic);return go;}
  public void PlayPillarShatter(Vector3 point){if(!library)library=AdvancedCombatVFXLibrary.Load();if(!library)return;var effect=Burst(library.crystalShatter,point,1,4);if(effect)foreach(var light in effect.GetComponentsInChildren<Light>(true)){var pulse=light.GetComponent<VFXLightPulse>();float peak=pulse?pulse.peakIntensity:light.intensity;if(!pulse)pulse=light.gameObject.AddComponent<VFXLightPulse>();pulse.point=light;pulse.peakIntensity=peak;pulse.attackSeconds=library.pillarFlashAttack;pulse.duration=library.pillarFlashDuration;light.shadows=LightShadows.None;light.intensity=0;}EnergyRelease(point,library.crystalRadius,library.crystalDistortionPixels,library.crystalChromaticPixels);}
  public void PlayDefeat(){if(!library)library=AdvancedCombatVFXLibrary.Load();if(defeat!=null)StopCoroutine(defeat);ExplosionsPlayed=0;defeat=StartCoroutine(DefeatSequence());}
  IEnumerator DefeatSequence()
  {
   if(!library)yield break;
   int count=library.smallBlastPositions.Length;
   for(int i=0;i<count;i++){var point=transform.TransformPoint(library.smallBlastPositions[i]);Burst(library.smallExplosion,point,1,5);ExplosionsPlayed++;boss.arena?.feedback.Shake(point,.35f,.2f);if(library.smallExplosionAudio)CombatEffects.Contact(null,library.smallExplosionAudio,point,3);yield return new WaitForSeconds(library.smallBlastSpacing);}
   float finalAt=Mathf.Max(library.finalBlastDelay,(count-1)*library.smallBlastSpacing+library.smallBlastTail);
   yield return new WaitForSeconds(Mathf.Max(0,finalAt-library.smallBlastSpacing*count));var centre=FinalBlastPoint();LastFinalBlastPosition=centre;Burst(library.finalExplosion,centre,library.finalBlastScale,8);
   for(int i=0;i<library.groundDustInstances;i++){float angle=i*Mathf.PI*2/library.groundDustInstances;var offset=new Vector3(Mathf.Cos(angle),0,Mathf.Sin(angle))*1.8f;Burst(library.groundDust,transform.position+offset+Vector3.up*.15f,library.groundDustScale,8);}
   Burst(library.heatBurst,centre,1,4);EnergyRelease(boss.ActionAimPoint,library.finalRadius,library.explosionDistortionPixels,library.explosionChromaticPixels);ExplosionsPlayed++;boss.arena?.feedback.Shake(centre,boss.tuning.groundPoundShake,1);boss.arena?.feedback.Sound(UDK7MSound.Defeat,centre,transform);onFinalExplosion.Invoke();defeat=null;
  }
  Vector3 FinalBlastPoint()
  {
   if(!library.finalExplosionOnSurface||library.blastSurfacePoints==null||library.blastSurfaceNormals==null||library.blastSurfacePoints.Length==0||library.blastSurfaceNormals.Length==0)return transform.TransformPoint(library.finalBlastOffset);
   var camera=Camera.main;Vector3 viewer=boss.arena&&boss.arena.player?boss.arena.player.EyePosition:camera?camera.transform.position:transform.position-transform.forward*20;float best=float.NegativeInfinity;Vector3 chosen=transform.TransformPoint(library.finalBlastOffset);
   for(int i=0;i<Mathf.Min(library.blastSurfacePoints.Length,library.blastSurfaceNormals.Length);i++){var surface=transform.TransformPoint(library.blastSurfacePoints[i]);var normal=transform.TransformDirection(library.blastSurfaceNormals[i]).normalized;float score=Vector3.Dot(normal,(viewer-surface).normalized);if(score>best){best=score;chosen=surface+normal*library.finalSurfaceOutset;}}
   return chosen;
  }
  public void ResetVisuals(){if(defeat!=null)StopCoroutine(defeat);defeat=null;ExplosionsPlayed=0;previous=transform.position;DustRate=0;nextWind=0;if(wind)wind.gameObject.SetActive(false);if(tracks!=null)foreach(var v in tracks)if(v){if(v.HasFloat("SpawnRate"))v.SetFloat("SpawnRate",0);v.Reinit();}foreach(var go in owned)if(go)go.SetActive(false);owned.Clear();}
  void OnDisable(){ResetVisuals();}
 }
}
