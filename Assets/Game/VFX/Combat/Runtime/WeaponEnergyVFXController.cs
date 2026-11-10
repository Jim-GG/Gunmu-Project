using UnityEngine;using UnityEngine.Rendering;using FirstPerson;
namespace GameActions
{
 public sealed class WeaponEnergyVFXController:MonoBehaviour
 {
  public GauntletCombat combat;public AdvancedCombatVFXLibrary library;public bool effectEnabled=true,preview;[Range(0,1)]public float previewCharge;
  GauntletPresentationSource state;
  SkinnedMeshRenderer[] overlays;MaterialPropertyBlock block;float charge,lastCharge,releaseAge=10;bool wasCharging;
  [ColorUsage(true,true)]public Color chargingColor=new Color(.15f,.8f,1),fullChargeColor=new Color(1,.64f,.06f);
  public bool overrideLibraryColors;
  public Transform flashAnchor;GameObject fullFlash;bool fullLatched;
  public static WeaponEnergyVFXController Ensure(GauntletCombat c){var v=c.GetComponent<WeaponEnergyVFXController>();if(!v)v=c.gameObject.AddComponent<WeaponEnergyVFXController>();v.combat=c;if(!v.library)v.library=AdvancedCombatVFXLibrary.Load();return v;}
  public static WeaponEnergyVFXController Ensure(PlayerChargedPunch c){var v=c.GetComponent<WeaponEnergyVFXController>();if(!v)v=c.gameObject.AddComponent<WeaponEnergyVFXController>();v.state=GauntletPresentationSource.Ensure(c.gameObject);if(!v.library)v.library=AdvancedCombatVFXLibrary.Load();return v;}
  public int OverlayCount=>overlays==null?0:overlays.Length;
  public float DisplayedCharge=>charge;
  void Create()
  {
   if(!state)state=GauntletPresentationSource.Ensure(gameObject);if(overlays!=null||!library||!library.energyMaterial||!library.gauntletOverlay||!state.Animation)return;var source=state.Animation.GetComponentsInChildren<SkinnedMeshRenderer>(true);var list=new System.Collections.Generic.List<SkinnedMeshRenderer>();
   foreach(var s in source){if(s.name=="EnergySurfaceOverlay"||s.sharedMesh!=library.gauntletSource)continue;var go=new GameObject("EnergySurfaceOverlay");go.layer=s.gameObject.layer;go.transform.SetParent(s.transform,false);var r=go.AddComponent<SkinnedMeshRenderer>();r.sharedMesh=library.gauntletOverlay;r.sharedMaterial=library.energyMaterial;r.bones=s.bones;r.rootBone=s.rootBone;r.localBounds=s.localBounds;r.updateWhenOffscreen=s.updateWhenOffscreen;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;list.Add(r);}if(list.Count>0){overlays=list.ToArray();block=new MaterialPropertyBlock();}
  }
  public void SetCharge01(float value){charge=Mathf.Clamp01(value);}
  public void SetChargeLevel(int level){charge=Mathf.Clamp(level,0,3)/3f;}
  public void TriggerDischarge(){lastCharge=charge;releaseAge=0;}
  public void SetEffectEnabled(bool value){effectEnabled=value;if(!value&&overlays!=null)foreach(var r in overlays)if(r)r.enabled=false;}
  void LateUpdate()
  {
   if(!library)library=AdvancedCombatVFXLibrary.Load();if(!state)state=GauntletPresentationSource.Ensure(gameObject);Create();if(overlays==null)return;bool charging=state.IsCharging&&!GameplayGate.IsBlocked;
   if(GameplayGate.IsBlocked)return;
   if(preview)charge=previewCharge;else if(charging){charge=Mathf.Clamp01(state.Seconds/Mathf.Max(.1f,state.FullDuration));releaseAge=10;}else{if(wasCharging)TriggerDischarge();releaseAge+=state.UsesPuzzleRules?Time.unscaledDeltaTime:Time.deltaTime;charge=Mathf.Lerp(lastCharge,0,Mathf.Clamp01(releaseAge/.14f));}wasCharging=charging;
   if(!overrideLibraryColors){chargingColor=library.chargingEnergyColor;fullChargeColor=library.fullEnergyColor;}bool full=charge>=.999f;if(full&&!fullLatched&&effectEnabled)PlayFullFlash();fullLatched=full;
   if(fullFlash&&fullFlash.activeSelf&&flashAnchor)PlaceFullFlash();
   foreach(var r in overlays)if(r){r.enabled=effectEnabled&&charge>.001f;if(!r.enabled)continue;r.GetPropertyBlock(block);block.SetFloat("_Charge01",charge);block.SetColor("_EnergyColor",full?fullChargeColor:chargingColor);block.SetFloat("_FlowSpeed",Mathf.Lerp(.5f,2,charge));block.SetFloat("_NoiseThreshold",Mathf.Lerp(.8f,.55f,charge));block.SetFloat("_EmissionIntensity",Mathf.Lerp(1,5,charge));block.SetFloat("_OverlayOpacity",Mathf.Lerp(.15f,.45f,charge));r.SetPropertyBlock(block);}
  }
  void PlayFullFlash()
  {
   if(!library.fullChargeFlash)return;var animation=state.Animation;if(!animation||!animation.meshRenderer)return;var source=animation.meshRenderer;if(!flashAnchor)flashAnchor=state.WeaponAnchor;if(!flashAnchor)return;
   if(!fullFlash){fullFlash=Instantiate(library.fullChargeFlash,flashAnchor,false);foreach(var t in fullFlash.GetComponentsInChildren<Transform>(true))t.gameObject.layer=source.gameObject.layer;}
   fullFlash.SetActive(false);PlaceFullFlash();var lease=fullFlash.GetComponent<CombatEffectLease>();if(!lease)lease=fullFlash.AddComponent<CombatEffectLease>();lease.Until=Time.time+.2f;fullFlash.SetActive(true);foreach(var v in fullFlash.GetComponentsInChildren<UnityEngine.VFX.VisualEffect>())v.Reinit();
  }
  void PlaceFullFlash()
  {
   if(fullFlash.transform.parent!=flashAnchor)fullFlash.transform.SetParent(flashAnchor,false);
   fullFlash.transform.localPosition=flashAnchor.InverseTransformVector(flashAnchor.rotation*library.fullChargeFlashOffset);fullFlash.transform.localRotation=Quaternion.identity;
   var scale=flashAnchor.lossyScale;fullFlash.transform.localScale=new Vector3(1/Mathf.Max(.0001f,Mathf.Abs(scale.x)),1/Mathf.Max(.0001f,Mathf.Abs(scale.y)),1/Mathf.Max(.0001f,Mathf.Abs(scale.z)));
  }
  void OnDisable(){SetEffectEnabled(false);wasCharging=false;fullLatched=false;charge=0;if(fullFlash)fullFlash.SetActive(false);}
  void OnEnable(){effectEnabled=true;releaseAge=10;}
 }
}
