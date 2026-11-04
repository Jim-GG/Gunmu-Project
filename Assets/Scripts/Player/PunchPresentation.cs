using UnityEngine;

// Presentation only: replace the visual prefab without changing damage/charge rules.
public class PunchPresentation : MonoBehaviour
{
    [Header("可替换外观")]
    [SerializeField, InspectorName("拳套外观预制体")] private GameObject gauntletPrefab;
    [SerializeField, InspectorName("待机位置")] private Vector3 restingPosition = new Vector3(.36f, -.31f, .66f);
    [Header("反馈强度")]
    [SerializeField, Range(0f, 2f), InspectorName("镜头震动强度")] private float shakeStrength = 1f;
    [SerializeField, Range(0f, 2f), InspectorName("命中停顿强度")] private float hitStopStrength = 1f;
    private Transform model;
    private Camera view;
    private PlayerChargedPunch punch;
    private PlayerGauntletEquipment equipment;
    private PlayerCombatController combat;
    private Vector3 cameraHome;
    private Quaternion cameraRotation;
    private float freezeUntil, shakeUntil, shakeDuration, shakeAmplitude, attackAt = -10f;
    private float hitAt = -10f;
    private int lastTier;
    private Vector3 swingFrom;
    private Quaternion swingRotation;
    private AudioSource sound;
    private AudioClip swingSound, impactSound;
    [SerializeField, InspectorName("格挡成功音效")] private AudioClip guardSound;
    public bool IsHitStopped => Time.unscaledTime < freezeUntil;
    public static bool IsStopped(GameObject actor) => actor != null && actor.TryGetComponent(out PunchPresentation p) && p.IsHitStopped;

    private void Start()
    {
        view = GetComponentInChildren<Camera>();
        punch = GetComponent<PlayerChargedPunch>();
        equipment = GetComponent<PlayerGauntletEquipment>();
        combat = GetComponent<PlayerCombatController>();
        if (view == null) { enabled = false; return; }
        cameraHome = view.transform.localPosition;
        cameraRotation = view.transform.localRotation;
        view.nearClipPlane = Mathf.Min(view.nearClipPlane, .05f);
        sound = view.gameObject.AddComponent<AudioSource>();
        sound.spatialBlend = 0f; sound.playOnAwake = false;
        swingSound = Resources.Load<AudioClip>("DemoPresentation/MechanicalSwing");
        impactSound = Resources.Load<AudioClip>("DemoPresentation/HeavyImpact");
        if (guardSound == null) guardSound = Resources.Load<AudioClip>("DemoPresentation/GuardClang");
        if (gauntletPrefab == null) gauntletPrefab = Resources.Load<GameObject>("DemoPresentation/GauntletVisual");
        if (gauntletPrefab != null)
        {
            model = Instantiate(gauntletPrefab, view.transform).transform;
            model.localPosition = restingPosition;
        }
    }

    public void Swing(int tier)
    {
        lastTier = tier;
        swingFrom = model != null ? model.localPosition : restingPosition;
        swingRotation = model != null ? model.localRotation : Quaternion.identity;
        attackAt = Time.unscaledTime;
        if (sound != null && swingSound != null) { sound.pitch = 1.05f - tier * .07f; sound.PlayOneShot(swingSound, .35f + tier * .1f); }
        Shake(.12f, .009f + tier * .004f);
    }

    public void Impact(Vector3 point, Vector3 normal, int tier, bool flesh)
    {
        hitAt = Time.unscaledTime;
        attackAt = Time.unscaledTime - .07f;
        if (sound != null && impactSound != null) { sound.pitch = 1.1f - tier * .1f; sound.PlayOneShot(impactSound, .45f + tier * .13f); }
        // Freeze only the player's translation and the struck enemy, never global timeScale.
        freezeUntil = Time.unscaledTime + (flesh ? .045f + tier * .025f : .018f + tier * .009f) * hitStopStrength;
        Shake(.16f + tier * .055f, .022f + tier * .016f);
        DemoImpactBurst.Spawn(point, normal, tier, flesh);
    }

    public void GuardFeedback(bool success, Vector3? hitPoint = null, Vector3? hitNormal = null)
    {
        if (success && sound != null && guardSound != null)
        {
            sound.pitch = 1f;
            sound.PlayOneShot(guardSound, .85f);
        }
        else if (!success && sound != null && impactSound != null)
        {
            sound.pitch = 0.85f;
            sound.PlayOneShot(impactSound, .4f);
        }

        Shake(success ? .17f : .25f, success ? .025f : .045f);
        if (success && view != null)
        {
            Vector3 spawnPos = hitPoint ?? (view.transform.position + view.transform.forward * .85f);
            Vector3 spawnNormal = hitNormal ?? (-view.transform.forward);
            DemoImpactBurst.Spawn(spawnPos, spawnNormal, 2, false);
        }
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
        if (model != null)
        {
            model.gameObject.SetActive(usable);
            float charge = punch != null ? punch.VisualCharge : 0f;
            Vector3 pos = restingPosition + new Vector3(.03f * charge, -.035f * charge, -.23f * charge);
            Quaternion rot = Quaternion.Euler(-8f * charge, -10f * charge, -14f * charge);
            bool isGuarding = (punch != null && punch.IsGuarding) || (combat != null && combat.IsGuarding);
            if (isGuarding)
            {
                pos = new Vector3(.06f, -.17f, .51f);
                rot = Quaternion.Euler(-10f, -25f, -30f);
            }
            float age = Time.unscaledTime - attackAt;
            if (age < .32f)
            {
                // Fast extension, short impact hold, then slower recovery.
                float phase = IsHitStopped ? .08f : age;
                float extension = phase < .065f ? Mathf.SmoothStep(0f, 1f, phase / .065f)
                    : phase < .13f ? 1f : 1f - Mathf.SmoothStep(0f, 1f, (phase - .13f) / .19f);
                if (phase < .13f) { pos = swingFrom; rot = swingRotation; }
                pos = Vector3.Lerp(pos, new Vector3(.12f, -.12f, .99f + lastTier * .035f), extension);
                rot = Quaternion.Slerp(rot, Quaternion.Euler(0f, 8f, -5f), extension);
            }
            model.localPosition = pos;
            model.localRotation = rot;
        }
        float envelope = allowFeedback ? Mathf.Clamp01((shakeUntil - Time.unscaledTime) / Mathf.Max(.01f, shakeDuration)) : 0f;
        float t = Time.unscaledTime * 95f;
        Vector3 offset = new Vector3(Mathf.Sin(t * 1.13f), Mathf.Sin(t * 1.71f), Mathf.Cos(t * 1.37f)) * shakeAmplitude * envelope * shakeStrength;
        view.transform.localPosition = cameraHome + offset;
        view.transform.localRotation = cameraRotation * Quaternion.Euler(offset.y * 26f, offset.x * 20f, offset.x * 35f);
    }

    private void OnDisable()
    {
        if (view != null) { view.transform.localPosition = cameraHome; view.transform.localRotation = cameraRotation; }
        if (model != null) model.gameObject.SetActive(false);
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
