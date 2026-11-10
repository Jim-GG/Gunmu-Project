using System;
using System.Collections.Generic;
using FirstPerson;
using GameActions;
using UnityEngine;
using UnityEngine.InputSystem;

// Compatibility for authored levels. GauntletCombat owns all attack clocks and hitboxes.
public class PlayerChargedPunch : MonoBehaviour
{
    [SerializeField] private float tierOneLungeDistance=2.5f,tierTwoLungeDistance=4.5f,tierThreeLungeDistance=6.5f;
    [SerializeField] private float tierOneImpulse=11,tierTwoImpulse=16,tierThreeImpulse=22;
    [SerializeField] private int basicDamage=1,tierOneDamage=2,tierTwoDamage=4,tierThreeDamage=6;
    [SerializeField] private bool liftOnEnemyHit=true;
    [SerializeField] private Vector3 combatLiftSpeeds=new Vector3(2.2f,3,3.8f);
    [SerializeField,Range(0,3)] private int initialEnergy;
    // Retained for old scene serialization; authored ChargePolicy now owns timing.
    [SerializeField,HideInInspector] private float minimumWindup=.15f,tierOneTime=.5f,tierTwoTime=1.25f,tierThreeTime=2.5f,reach=1,recovery=.3f,guardAngle=120;
    [SerializeField,HideInInspector] private int storedEnergy;
    GauntletCombat shared;
    PlayerMovement movement;
    PlayerCombatController health;
    PlayerGauntletEquipment equipment;
    PunchPresentation presentation;
    bool wasCharging,wasGuarding;
    readonly HashSet<int> puzzleHits=new HashSet<int>();
    public GauntletCombat SharedCombat=>shared;
    public bool CanUseSharedCombat=>isActiveAndEnabled&&!GameplayGate.IsBlocked&&(!equipment||equipment.HasGauntlet)&&(!health||!health.IsDefeated);
    public int StoredEnergy=>shared?shared.BlockEnergy:0;
    public bool IsCharging=>shared&&(shared.State==GauntletCombat.Phase.Charging||shared.State==GauntletCombat.Phase.ReleasePending||shared.State==GauntletCombat.Phase.Full);
    public bool IsAttacking=>shared&&shared.State==GauntletCombat.Phase.Punching;
    public float ChargeTime=>shared?shared.ChargeSeconds:0;
    public int CurrentTier=>shared?shared.ChargeTier:0;
    public float TierOneTime=>shared?shared.policy.tierOne/shared.policy.framesPerSecond:32/30f;
    public float TierTwoTime=>shared?shared.policy.tierTwo/shared.policy.framesPerSecond:62/30f;
    public float TierThreeTime=>shared?shared.policy.full/shared.policy.framesPerSecond:92/30f;
    public float ChargeCap=>TierThreeTime;
    public bool IsChargeCapped=>shared&&shared.State==GauntletCombat.Phase.Full;
    public float VisualCharge=>IsCharging?Mathf.Clamp01(ChargeTime/TierThreeTime):0;
    public bool CanGuard=>CanUseSharedCombat&&shared;
    public bool IsGuarding=>CanGuard&&shared.IsBlocking;
    public event Action ChargeStarted,ChargeCanceled,GuardStarted,GuardEnded,GuardSucceeded;
    public event Action<float> ChargeChanged;
    public event Action<int> PunchReleased;
    void Awake()
    {
        movement=GetComponent<PlayerMovement>();health=GetComponent<PlayerCombatController>();equipment=GetComponent<PlayerGauntletEquipment>();
        presentation=GetComponent<PunchPresentation>()??gameObject.AddComponent<PunchPresentation>();
        shared=GetComponent<GauntletCombat>()??gameObject.AddComponent<GauntletCombat>();
        shared.useTargetAssist=false;shared.externalInputEnabled=isActiveAndEnabled;
        shared.AttackStarted+=Attack;shared.AttackFinished+=Finished;shared.ContactResolved+=Contact;shared.AttackBlocked+=Blocked;
        if(initialEnergy>0)shared.AddEnergy(initialEnergy);
        LevelPlayerPresentation.Ensure(gameObject);
    }
    void OnEnable(){if(shared)shared.externalInputEnabled=true;}
    void Update()
    {
        if(!shared||!shared.InitializePipeline())return;
        if(!CanUseSharedCombat||!Application.isFocused||Cursor.lockState!=CursorLockMode.Locked)
        {if(shared.State!=GauntletCombat.Phase.Idle||shared.HasBufferedInput)CancelCharge();return;}
        var mouse=Mouse.current;if(mouse==null)return;
        if(mouse.rightButton.wasPressedThisFrame)shared.BeginBlock();
        if(mouse.rightButton.wasReleasedThisFrame)shared.EndBlock();
        if(mouse.leftButton.wasPressedThisFrame)shared.Press();
        if(mouse.leftButton.wasReleasedThisFrame)shared.Release();
    }
    void LateUpdate()
    {
        bool charging=IsCharging,guarding=IsGuarding;
        if(charging&&!wasCharging)ChargeStarted?.Invoke();
        if(charging)ChargeChanged?.Invoke(ChargeTime);
        if(guarding!=wasGuarding){if(guarding)GuardStarted?.Invoke();else GuardEnded?.Invoke();}
        wasCharging=charging;wasGuarding=guarding;
    }
    void Attack(int punch)
    {
        puzzleHits.Clear();int tier=Mathf.Clamp(punch-1,0,3);PunchReleased?.Invoke(tier);presentation.Swing(tier);
        float distance=tier==3?tierThreeLungeDistance:tier==2?tierTwoLungeDistance:tier==1?tierOneLungeDistance:0;
        if(movement&&distance>0)movement.BeginPunchLunge(distance);
    }
    void Finished(int punch){if(movement)movement.StopPunchLunge();}
    void Blocked(){GuardSucceeded?.Invoke();presentation.GuardFeedback(true);}
    void Contact(Collider collider,Vector3 point,Vector3 normal)
    {
        if(!collider)return;
        int tier=Mathf.Clamp(shared.ActivePunch-1,0,3);
        var actionTarget=collider.GetComponentInParent<IActionTarget>();
        bool flesh=SurfaceAudio.Kind(collider)==SoundSurface.Flesh;
        // ActionContact already emits surface VFX/audio and damages IActionTarget.
        presentation.Impact(point,normal,tier,flesh);
        var box=collider.GetComponentInParent<KineticBox>();
        int identity=box?box.GetInstanceID():collider.attachedRigidbody?collider.attachedRigidbody.GetInstanceID():collider.GetInstanceID();
        if(!puzzleHits.Add(identity))return;
        var direction=shared.attackOrigin?shared.attackOrigin.forward:transform.forward;
        float impulse=tier==3?tierThreeImpulse:tier==2?tierTwoImpulse:tier==1?tierOneImpulse:0;
        if(box&&tier>0)
        {
            Vector3 horizontal=Vector3.ProjectOnPlane(direction,Vector3.up);
            var controller=GetComponent<CharacterController>();
            bool sideLid=normal.y>.65f&&controller&&controller.bounds.min.y<point.y-.15f&&horizontal.sqrMagnitude>.01f;
            var applied=box.ApplyCardinalImpulse(sideLid?-horizontal.normalized:normal,direction,impulse);
            if(movement)movement.ApplyBoxHitRecoil(applied);
        }
        if(actionTarget==null)
        {
            var receiver=collider.GetComponentInParent<IPunchReceiver>();receiver?.OnPunched(point,normal,direction,tier,impulse);
            var targetHealth=collider.GetComponentInParent<PunchTargetHealth>();
            if(targetHealth)targetHealth.TakeDamage(tier==3?tierThreeDamage:tier==2?tierTwoDamage:tier==1?tierOneDamage:basicDamage);
            var body=collider.attachedRigidbody;
            if(!box&&tier>0&&body&&!body.isKinematic)body.AddForce(direction*impulse,ForceMode.VelocityChange);
        }
        if(flesh&&tier>0&&liftOnEnemyHit&&movement)movement.LiftAfterCombatPunch(tier==3?combatLiftSpeeds.z:tier==2?combatLiftSpeeds.y:combatLiftSpeeds.x);
    }
    public bool AddEnergy(int amount)
    {
        if(!shared||amount<=0||StoredEnergy>=3||(equipment&&!equipment.HasGauntlet))return false;
        shared.AddEnergy(amount);return true;
    }
    public bool IsGuardingTowardsPoint(Vector3 point)=>IsGuarding&&WithinGuard(point-transform.position);
    public bool IsGuardingSurface(Vector3 normal)=>IsGuarding&&WithinGuard(-normal);
    bool WithinGuard(Vector3 direction)=>Vector3.Angle(transform.forward,Vector3.ProjectOnPlane(direction,Vector3.up))<=shared.blockAngle*.5f;
    public void RefillEnergyByGuard(Vector3 point,Vector3 normal){if(shared&&CanUseSharedCombat)shared.ResolveGuard(point);}
    public void NotifySuccessfulBlock()=>AddEnergy(1);
    public void NotifyPlasticCollision()=>presentation.GuardFeedback(false);
    // Body contact stops movement only. Authored hitboxes exclusively decide damage.
    public void ResolveLungeContact(Collider collider,Vector3 point,Vector3 normal){if(normal.y>.65f&&shared&&shared.attackOrigin&&shared.attackOrigin.forward.y>-.4f)return;if(movement)movement.StopPunchLunge();}
    public void CancelCharge(){bool charging=IsCharging;if(shared)shared.Cancel();if(movement)movement.StopPunchLunge();if(charging){ChargeChanged?.Invoke(0);ChargeCanceled?.Invoke();}}
    public void ResetPunchState(){CancelCharge();if(shared){shared.ResetResonance();shared.AddEnergy(initialEnergy);}}
    void OnDisable(){CancelCharge();if(shared)shared.externalInputEnabled=false;}
    void OnApplicationFocus(bool focused){if(!focused)CancelCharge();}
    void OnDestroy(){if(shared){shared.AttackStarted-=Attack;shared.AttackFinished-=Finished;shared.ContactResolved-=Contact;shared.AttackBlocked-=Blocked;}}
    void OnGUI()
    {
        if(GameplayGate.IsBlocked||(equipment&&!equipment.HasGauntlet))return;
        ChineseGameUI.EnsureFont();GUI.Label(new Rect(15,Screen.height-95,500,30),$"充能：{StoredEnergy}/3 格｜蓄力：{CurrentTier} 档");
    }
}
