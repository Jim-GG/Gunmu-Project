using UnityEngine;using UnityEngine.VFX;
namespace GameActions
{
 [CreateAssetMenu(menuName="Game Actions/Advanced Combat VFX Library")]
 public sealed class AdvancedCombatVFXLibrary:ScriptableObject
 {
  public GameObject smallExplosion,finalExplosion,blackSmoke,groundDust,rubble,crystalShatter,chromaticSphere,heatBurst,heatLoop,windCone,tankSteam;
  public VisualEffectAsset trackDustGraph;public Material energyMaterial;public Mesh gauntletSource,gauntletOverlay;
  public float smallBlastSpacing=.28f,finalBlastDelay=1.15f,finalRadius=4.5f,crystalRadius=2,explosionDistortionPixels=12,explosionChromaticPixels=4,crystalDistortionPixels=8,crystalChromaticPixels=3;
  public Vector3[] smallBlastPositions={new Vector3(-1,2,.5f),new Vector3(.8f,3,-.4f),new Vector3(-.4f,2.6f,-1)};
  public float dustParticlesPerMetre=16,maximumTrackDustRate=120;public Vector3[] trackDustPositions={new Vector3(-2.2f,.15f,-2.5f),new Vector3(2.2f,.15f,-2.5f)};
  public AudioClip smallExplosionAudio;
  public Vector3 chargeWindOffset=new Vector3(0,2.57f,-.5f);[Min(.05f)]public float chargeWindRepeat=.32f;[Min(.1f)]public float chargeWindScale=1;
  public Vector3 finalBlastOffset=new Vector3(0,4.8f,0);[Min(.1f)]public float finalBlastScale=1,blackSmokeScale=1.4f,groundDustScale=1.8f;
  public bool chargeWindFollowDrillTip=true;public Vector3 chargeWindTipOffset;
  public GameObject fullChargeFlash;[Min(0)]public float smallBlastTail=1.4f;[Range(1,10)]public int groundDustInstances=6;
  [ColorUsage(true,true)]public Color chargingEnergyColor=new Color(.15f,.8f,1),fullEnergyColor=new Color(1,.64f,.06f);
  public bool finalExplosionOnSurface=true;[Range(.1f,1)]public float finalSurfaceOutset=.55f;public Vector3[] blastSurfacePoints,blastSurfaceNormals;
  public Vector3 fullChargeFlashOffset;
  [Min(0)]public float pillarFlashAttack=.04f;[Min(.05f)]public float pillarFlashDuration=.65f;
  public static AdvancedCombatVFXLibrary Load()=>Resources.Load<AdvancedCombatVFXLibrary>("AdvancedCombatVFX");
 }
}
