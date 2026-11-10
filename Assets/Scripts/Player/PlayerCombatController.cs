using UnityEngine;
using GameActions;
using FirstPerson;
public class PlayerCombatController:MonoBehaviour
{
    [SerializeField,Min(1)]private int maxHealth=6;
    [SerializeField,Range(1,180)]private float guardAngle=120;
    [SerializeField,Min(0)]private float hurtProtection=.6f;
    float health,nextHurt;
    public bool IsDefeated=>health<=0;
    public int Health=>Mathf.CeilToInt(health);
    public float ExactHealth=>health;
    public float HealthFraction=>health/Mathf.Max(1,maxHealth);
    public GuardKind LastGuard {get;private set;}
    public bool IsGuarding=>!IsDefeated&&GetComponent<GauntletCombat>()&&GetComponent<GauntletCombat>().IsBlocking;
    void Awake(){health=maxHealth;}
    public void RestoreHealth(){health=maxHealth;nextHurt=0;}
    public bool ReceiveAttack(Vector3 source,int damage)=>ReceiveAttack(source,(float)damage,true);
    public bool ReceiveAttack(Vector3 source,float damage,bool blockable)
    {
        LastGuard=GuardKind.None;
        if(IsDefeated||GameplayGate.IsBlocked)return false;
        var gauntlet=GetComponent<GauntletCombat>();
        if(blockable&&gauntlet)LastGuard=gauntlet.ResolveGuard(source);
        float received=Mathf.Max(0,damage)*(LastGuard==GuardKind.Perfect?0:LastGuard==GuardKind.Imperfect?.34f:1);
        if(received<=0)return LastGuard!=GuardKind.None;
        if(Time.time<nextHurt)return LastGuard!=GuardKind.None;
        health=Mathf.Max(0,health-received);nextHurt=Time.time+hurtProtection;
        ScreenEffectSignals.Emit(ScreenCue.Hurt,source,receiver:gameObject);
        GetComponent<GauntletCombatFeedback>()?.PlayHurt();
        if(IsDefeated)GameStateManager.NotifyPlayerDied();
        return LastGuard!=GuardKind.None;
    }
    void OnGUI()
    {
        if(GameplayGate.IsBlocked)return;
        ChineseGameUI.EnsureFont();GUI.Label(new Rect(15,Screen.height-65,400,30),$"生命：{health:0.#}/{maxHealth}｜"+(IsGuarding?"防御中":"右键防御"));
    }
}
