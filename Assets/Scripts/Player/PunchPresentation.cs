using UnityEngine;

/// <summary>
/// 拳套的打击反馈：镜头震动、命中顿帧、准星反馈及共享音效/特效。
/// 只做表现，不改变伤害 / 蓄力 / 格挡规则；拳套外观与动画由 <see cref="PlayerGauntletView"/> 负责。
/// </summary>
public class PunchPresentation : MonoBehaviour
{
    [Header("反馈强度")]
    [SerializeField, Range(0f, 2f), InspectorName("镜头震动强度")] private float shakeStrength = 1f;
    [SerializeField, Range(0f, 2f), InspectorName("命中停顿强度")] private float hitStopStrength = 1f;

    private GameActions.LevelPlayerPresentation polished;
    private GameActions.LevelPlayerPresentation Polished => polished ? polished : polished = GameActions.LevelPlayerPresentation.Ensure(gameObject);
    private Camera view;
    private PlayerChargedPunch punch;
    private PlayerGauntletEquipment equipment;
    private PlayerGauntletView gauntletView;
    private Vector3 cameraHome;
    private Quaternion cameraRotation;
    private float freezeUntil, shakeUntil, shakeDuration, shakeAmplitude;
    private float hitAt = -10f;

    public bool IsHitStopped => Time.unscaledTime < freezeUntil;
    public static bool IsStopped(GameObject actor) => GameActions.GauntletCombatFeedback.Stopped(actor) || actor != null && actor.TryGetComponent(out PunchPresentation p) && p.IsHitStopped;

    private void Start()
    {
        view = GetComponentInChildren<Camera>();
        punch = GetComponent<PlayerChargedPunch>();
        equipment = GetComponent<PlayerGauntletEquipment>();
        gauntletView = GetComponent<PlayerGauntletView>();
        if (view == null) { enabled = false; return; }
        cameraHome = view.transform.localPosition;
        cameraRotation = view.transform.localRotation;
        view.nearClipPlane = Mathf.Min(view.nearClipPlane, .05f);
    }

    public void Swing(int tier)
    {
        Shake(.12f, .009f + tier * .004f);
        Polished.Swing(tier);
    }

    public void Impact(Vector3 point, Vector3 normal, int tier, bool flesh, Collider target = null)
    {
        hitAt = Time.unscaledTime;
        // Freeze only the player's translation and the struck enemy, never global timeScale.
        freezeUntil = flesh ? 0 : Time.unscaledTime + (.018f + tier * .009f) * hitStopStrength;
        if(!flesh) Shake(.16f + tier * .055f, .022f + tier * .016f);
        Polished.Contact(target, point, normal);
    }

    public void GuardFeedback(bool success, Vector3? hitPoint = null, Vector3? hitNormal = null)
    {
        Shake(success ? .17f : .25f, success ? .025f : .045f);
        if (success && view != null)
        {
            Vector3 spawnPos = hitPoint ?? (view.transform.position + view.transform.forward * .85f);
            Vector3 spawnNormal = hitNormal ?? (-view.transform.forward);
            Polished.Guard(true, spawnPos, spawnNormal);
        }
        else if (!success) Polished.Guard(false, transform.position, Vector3.up);
    }

    private void Shake(float duration, float amount)
    {
        shakeDuration = duration;
        shakeUntil = Time.unscaledTime + duration;
        shakeAmplitude = amount;
    }

    private void LateUpdate()
    {
        if (view == null) return;
        bool usable = punch != null && punch.isActiveAndEnabled && (equipment == null || equipment.HasGauntlet);
        bool allowFeedback = usable && Cursor.lockState == CursorLockMode.Locked;
        float envelope = allowFeedback ? Mathf.Clamp01((shakeUntil - Time.unscaledTime) / Mathf.Max(.01f, shakeDuration)) : 0f;
        float t = Time.unscaledTime * 95f;
        Vector3 offset = new Vector3(Mathf.Sin(t * 1.13f), Mathf.Sin(t * 1.71f), Mathf.Cos(t * 1.37f)) * shakeAmplitude * envelope * shakeStrength;
        Vector3 position = cameraHome + offset;
        Quaternion rotation = cameraRotation * Quaternion.Euler(offset.y * 26f, offset.x * 20f, offset.x * 35f);
        if (gauntletView == null || !gauntletView.ApplyCameraPose(view.transform, position, rotation))
            view.transform.SetLocalPositionAndRotation(position, rotation);
    }

    private void OnDisable()
    {
        if (view != null) { view.transform.localPosition = cameraHome; view.transform.localRotation = cameraRotation; }
        freezeUntil = 0f;
    }

    private void OnGUI()
    {
        if (GameplayGate.IsBlocked) return;
        float age = Time.unscaledTime - hitAt;
        if (age > .18f) return;
        Color old = GUI.color;
        GUI.color = new Color(1f, .78f, .38f, 1f - age / .18f);
        float x = Screen.width * .5f, y = Screen.height * .5f;
        GUI.DrawTexture(new Rect(x - 13f, y - 2f, 8f, 4f), Texture2D.whiteTexture);
        GUI.DrawTexture(new Rect(x + 5f, y - 2f, 8f, 4f), Texture2D.whiteTexture);
        GUI.color = old;
    }
}
