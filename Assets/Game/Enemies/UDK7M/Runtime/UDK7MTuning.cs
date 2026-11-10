using UnityEngine;
namespace UDK7M
{
    [CreateAssetMenu(menuName="UDK7M/Boss Tuning")]
    public sealed class UDK7MTuning:ScriptableObject
    {
        public float maxHealth=1000,directDamageMultiplier=.05f,corpseDamage=140;
        public float turnSpeed=25,holdTurnSpeed=18,rearTurnSpeed=50,aimTolerance=5;
        public float chargeTrackingAngle=30,navigationRadius=3.8f;
        [HideInInspector]public float rearAngle=110;
        [Range(0,180)]public float turnBeforeChargeAngle=60;
        public float nearDistance=6,chargeSpeed=8,slamSeconds=2,chargeSeconds=2.5f,slamRange=5,slamMinimumRange=3,slamFrontAngle=35;
        public float slamBrakeSeconds=1,chargeBrakeSeconds=.6f;
        public AnimationCurve braking=AnimationCurve.Linear(0,1,1,0);
        public Vector2 ambientInterval=new Vector2(4,7),decisionInterval=new Vector2(.3f,.55f);
        [HideInInspector]public Vector2 farWeights=new Vector2(55,45);[HideInInspector]public Vector3 rearWeights=new Vector3(40,30,30);
        [Header("Three phases / Slam, TripleSlam, Charge")]
        public Vector3 stableWeights=new Vector3(50,30,20),overloadedWeights=new Vector3(10,40,50);
        [Tooltip("Horizontal distance from boss root to an unbroken pillar centre.")]
        [Min(0)]public float nearPillarDistance=10;
        [Range(0,1)]public float nearPillarTripleChance=.1f;
        public float overloadChargeSeconds=3,paralyzedFreezeFrame=64;
        public float paralyzedCenterClearance=8,paralyzedStaggerSeconds=.6f;
        public int cyclesToDefeat=3;
        [Header("Drill capture")]
        public bool captureFlyingOnly=true;
        [HideInInspector]public bool freezeDuringCapture=true;
        public float captureRadius=2.5f,captureFrontAngle=85,captureFlightAge=3.5f,captureMinimumSpeed=1.5f,captureHoldSeconds=.45f,captureTimeout=2;
        public float bodyRadius=2.1f,bodyHeight=4,collisionSkin=.06f;
        public bool tracksFollowMovement=true;public float trackDistanceMultiplier=1;
        [Header("Moving tracks / 履带碾压")]
        public bool crushEnabled=true,crushUseTrackBounds=true,crushKillsMinions=true;
        public Vector3 crushLeftCenter=new Vector3(-1.8f,.9f,0),crushRightCenter=new Vector3(1.8f,.9f,0),crushBoxSize=new Vector3(1.3f,1.8f,6);
        public float crushBoundsPadding=.16f,crushMinimumSpeed=.2f,crushDamage=20,crushCooldown=.5f,crushKnockback=3,crushPushSeconds=.25f,crushBlockedKnockback=.7f;
        public float vacuumRadius=5,vacuumSpeed=18,consumeRadius=.75f,rotationThreshold=5;
        public Vector3 drillLocalTip=new Vector3(0,1.57f,0);
        public float resonanceRadius=6,resonanceDamage=130,chargedModeSeconds=15;
        public int livingMinions=3,maximumCorpses=6;
        public Vector2 ordinaryRespawn=new Vector2(3,5),resonanceRespawn=new Vector2(15,20);
        public float corpseLifetime=30,spawnMinimumDistance=9;
        public float rockWarningSeconds=1,rockDamage=20,liquidDps=12;
        [Header("Falling debris landing VFX / defaults to gauntlet heavy ground impact")]
        public GameObject fallingImpactEffect;
        [Min(.01f)]public float fallingRockImpactScale=.65f,fallingCrystalImpactScale=.85f,centralCrystalImpactScale=1.67f;
        [Header("Arena, rebound and ground impact")]
        public float arenaRadius=36,centralCrystalScale=2.2f,centralLaunchSpeed=23,pillarReboundSpeed=19,pureCrystalHeight=9;
        public float groundPoundMinimumHeight=3,groundPoundFallSpeed=35,groundPoundBossDamage=0,groundPoundRadius=40,groundPoundMinBossDistance=10,groundPoundPushSpeed=28;
        [Header("Cinemachine impulse")]
        public int impulseChannel=4;
        public float lightSlamShake=.12f,heavySlamShake=.38f,chargeImpactShake=.23f,resonanceShake=.5f,groundPoundShake=.8f;
        [Min(0)]public float thirdSlamContactMuteSeconds=.35f;
    }
}

