using UnityEngine;
using UnityEngine.InputSystem;

public sealed class ElevatorFallIntro : MonoBehaviour
{
    [SerializeField, InspectorName("演出相机")] private Camera shotCamera;
    [SerializeField, InspectorName("轿厢内镜头起点")] private Transform interiorShot;
    [SerializeField, InspectorName("竖井镜头起点")] private Transform shaftShot;
    [SerializeField, InspectorName("轿厢内景组")] private GameObject cabinSet;
    [SerializeField, InspectorName("竖井外景组")] private GameObject shaftSet;
    [SerializeField, InspectorName("竖井里的电梯轿厢")] private Transform fallingCar;
    [SerializeField, InspectorName("绷紧的钢缆")] private GameObject intactCable;
    [SerializeField, InspectorName("断裂火花")] private GameObject sparks;
    [SerializeField, InspectorName("轿厢主灯")] private Light cabinLight;
    [SerializeField, InspectorName("主灯灯带外观")] private GameObject cabinLampStrip;
    [SerializeField, InspectorName("楼层显示")] private TextMesh floorDisplay;
    [SerializeField, InspectorName("演出结束后进入")] private string arrivalScene = "Soviet_1";
    [SerializeField, Range(0f, 1f), InspectorName("镜头震动强度")] private float shakeStrength = .65f;

    private const float FirstImpact = 5.9f;
    private const float SecondImpact = 6.65f;
    private const float ShaftCut = 6.85f;
    private const float InteriorCut = 10.1f;
    private const float Landing = 11.25f;
    private const float End = 13.4f;

    private Vector3 carHome;
    private Quaternion carRotation;
    private AudioSource sound;
    private AudioClip motorSound;
    private AudioClip impactSound;
    private float elapsed;
    private bool loading;
    private bool failed;

    private static bool IsSkipPressed()
    {
        foreach (var device in InputSystem.devices)
        {
            if (device is Keyboard keyboard && keyboard.spaceKey.isPressed)
                return true;
        }

        return false;
    }

    private void Start()
    {
        GameStateManager.SetState(GameState.Starting);
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = false;
        if (fallingCar != null)
        {
            carHome = fallingCar.position;
            carRotation = fallingCar.rotation;
        }

        sound = gameObject.AddComponent<AudioSource>();
        sound.spatialBlend = 0f;
        sound.loop = true;
        motorSound = MakeSound(false);
        impactSound = MakeSound(true);
        sound.clip = motorSound;
        sound.volume = .22f;
        sound.Play();
        ApplyPose(0f);
    }

    private void Update()
    {
        if (loading) return;
        if (IsSkipPressed())
        {
            Complete();
            return;
        }
        if (failed) return;

        float previous = elapsed;
        // A slow first frame should not skip the start of the cinematic.
        elapsed = Mathf.Min(End, elapsed + Mathf.Min(.1f, Time.unscaledDeltaTime));
        if (Crossed(previous, FirstImpact)) sound.PlayOneShot(impactSound, .75f);
        if (Crossed(previous, SecondImpact)) sound.PlayOneShot(impactSound, 1.4f);
        if (Crossed(previous, Landing)) sound.PlayOneShot(impactSound, 1.75f);
        sound.volume = elapsed >= Landing ? .06f : elapsed >= SecondImpact ? .43f : .22f;
        ApplyPose(elapsed);
        if (elapsed >= End) Complete();
    }

    private bool Crossed(float previous, float moment) => previous < moment && elapsed >= moment;

    private void ApplyPose(float time)
    {
        bool outside = time >= ShaftCut && time < InteriorCut;
        float fall = Mathf.Clamp01((time - ShaftCut) / (InteriorCut - ShaftCut));
        if (fallingCar != null)
        {
            fallingCar.position = carHome - Vector3.up * (fall * fall * 10f)
                + Vector3.right * (Mathf.Sin(fall * 6.5f) * fall * .34f);
            fallingCar.rotation = carRotation * Quaternion.Euler(fall * 9f, 0f, -fall * 11f);
        }
        if (intactCable != null) intactCable.SetActive(time < ShaftCut + .3f);
        if (sparks != null) sparks.SetActive(outside && (fall < .19f || (fall > .58f && fall < .72f)));
        bool lampOn = time < 5.36f || (time >= 5.48f && time < 5.59f)
            || (time >= 5.71f && time < 5.82f);
        if (cabinLight != null) cabinLight.enabled = lampOn;
        if (cabinLampStrip != null) cabinLampStrip.SetActive(lampOn);
        if (floorDisplay != null)
        {
            int floor = -2 - Mathf.Clamp(Mathf.FloorToInt(time / .82f), 0, 6);
            floorDisplay.text = time >= SecondImpact ? "ERR" : floor.ToString();
        }

        if (cabinSet != null && cabinSet.activeSelf == outside)
            cabinSet.SetActive(!outside);
        if (shaftSet != null && shaftSet.activeSelf != outside)
            shaftSet.SetActive(outside);

        Transform anchor = outside ? shaftShot : interiorShot;
        if (shotCamera == null || anchor == null) return;
        float shake = Pulse(time, FirstImpact, .44f) * .045f + Pulse(time, SecondImpact, .55f) * .12f
            + Pulse(time, Landing, .65f) * .18f;
        if (outside) shake += fall * .025f;
        shake *= shakeStrength;
        float lift = time >= InteriorCut
            ? .36f * Mathf.SmoothStep(0f, 1f, (time - InteriorCut) / .68f)
                - .78f * Mathf.SmoothStep(0f, 1f, (time - Landing) / .32f)
            : 0f;
        Vector3 offset = anchor.up * (outside ? -fall * fall * 7f : lift);
        offset += (anchor.right * Mathf.Sin(time * 88f) + anchor.up * Mathf.Cos(time * 103f)) * shake;
        Quaternion wobble = Quaternion.Euler(shake * Mathf.Sin(time * 93f) * 36f, 0f,
            shake * Mathf.Cos(time * 71f) * 42f + (outside ? 0f : lift * -12f));
        shotCamera.transform.SetPositionAndRotation(anchor.position + offset, anchor.rotation * wobble);
    }

    private static float Pulse(float time, float at, float duration)
    {
        return time < at ? 0f : 1f - Mathf.Clamp01((time - at) / duration);
    }

    private void Complete()
    {
        loading = true;
        if (GameStateManager.LoadLevel(arrivalScene)) return;
        loading = false;
        failed = true;
        sound.Stop();
    }

    private void OnGUI()
    {
        if (loading) return;
        ChineseGameUI.EnsureFont();
        float blackout = Mathf.SmoothStep(0f, 1f, (elapsed - Landing) / .32f);
        if (blackout > 0f)
        {
            Color color = GUI.color;
            GUI.color = new Color(0f, 0f, 0f, blackout);
            GUI.DrawTexture(new Rect(0f, 0f, Screen.width, Screen.height), Texture2D.whiteTexture);
            GUI.color = color;
        }
        var text = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleCenter, fontSize = 20 };
        if (failed)
            GUI.Label(new Rect(0f, Screen.height * .5f, Screen.width, 40f), "无法载入下一关｜按空格重试", text);
        else if (elapsed < FirstImpact)
            GUI.Label(new Rect(0f, Screen.height - 96f, Screen.width, 40f), "地下设施｜目的地 -17 层", text);
        else if (elapsed >= Landing + .5f)
            GUI.Label(new Rect(0f, Screen.height - 96f, Screen.width, 40f), "……通讯中断", text);

        var skip = new GUIStyle(GUI.skin.label) { alignment = TextAnchor.MiddleRight, fontSize = 16 };
        GUI.Label(new Rect(Screen.width - 220f, Screen.height - 42f, 200f, 30f), "空格 跳过演出", skip);
    }

    private static AudioClip MakeSound(bool impact)
    {
        const int rate = 22050;
        float seconds = impact ? .72f : 2f;
        float[] samples = new float[Mathf.CeilToInt(rate * seconds)];
        var random = new System.Random(17);
        float noise = 0f;
        for (int i = 0; i < samples.Length; i++)
        {
            float t = (float)i / rate;
            noise = .92f * noise + .08f * ((float)random.NextDouble() * 2f - 1f);
            if (impact)
                samples[i] = (.49f * noise + .22f * Mathf.Sin(t * 251f)) * Mathf.Exp(-t * 7f)
                    * Mathf.Clamp01(t * 120f);
            else
                samples[i] = .14f * Mathf.Sin(t * 2f * Mathf.PI * 54f)
                    + .06f * Mathf.Sin(t * 2f * Mathf.PI * 81f);
        }
        var clip = AudioClip.Create(impact ? "ElevatorImpact" : "ElevatorMotor", samples.Length, 1, rate, false);
        clip.SetData(samples, 0);
        return clip;
    }

    private void OnDestroy()
    {
        if (motorSound != null) Destroy(motorSound);
        if (impactSound != null) Destroy(impactSound);
    }
}
