using UnityEngine;

/// <summary>First-person gauntlet built from primitives and animated procedurally.</summary>
public class GauntletView : MonoBehaviour
{
    private static readonly Vector3 BasePosition = new Vector3(0.37f, -0.34f, 0.68f);
    private static readonly Vector3 BaseEuler = new Vector3(2f, -14f, 0f);
    private static readonly Vector3 BlockPosition = new Vector3(0.16f, -0.22f, 0.56f);
    private static readonly Vector3 BlockEuler = new Vector3(-4f, -5f, -12f);

    private Material metalMaterial;
    private Material plateMaterial;
    private Material glowMaterial;
    private Material coreMaterial;

    private Transform coreTransform;
    private Light coreLight;
    private LineRenderer knuckleRing;
    private LineRenderer wristRing;

    private float energyRatio;
    private float displayedEnergy;
    private bool blocking;
    private float blockBlend;
    private float punchTimer;
    private float shockTimer;
    private float flashTimer;
    private float blockRecoilTimer;
    private bool visible = true;

    private const float PunchDuration = 0.24f;
    private const float ShockDuration = 0.5f;

    public static GauntletView Create(Transform cameraTransform)
    {
        GameObject root = new GameObject("ImpulseGauntlet");
        root.transform.SetParent(cameraTransform, false);
        GauntletView view = root.AddComponent<GauntletView>();
        view.Build();
        return view;
    }

    private void Build()
    {
        metalMaterial = DemoVisualFactory.CreateLitMaterial(
            "Gauntlet_Metal",
            new Color(0.035f, 0.048f, 0.06f),
            metallic: 0.82f,
            smoothness: 0.48f);

        plateMaterial = DemoVisualFactory.CreateLitMaterial(
            "Gauntlet_Plate",
            new Color(0.12f, 0.15f, 0.17f),
            metallic: 0.55f,
            smoothness: 0.35f);

        Color energyColor = new Color(0.2f, 0.9f, 1f);
        glowMaterial = DemoVisualFactory.CreateLitMaterial(
            "Gauntlet_Energy",
            new Color(0.05f, 0.25f, 0.3f),
            metallic: 0.1f,
            smoothness: 0.7f,
            emission: energyColor,
            emissionIntensity: 2f);

        coreMaterial = DemoVisualFactory.CreateLitMaterial(
            "Gauntlet_Core",
            new Color(0.3f, 0.9f, 1f),
            metallic: 0.1f,
            smoothness: 0.8f,
            emission: energyColor,
            emissionIntensity: 3.5f);

        BuildGeometry();

        transform.localScale = Vector3.one * 0.66f;
        transform.localPosition = BasePosition;
        transform.localRotation = Quaternion.Euler(BaseEuler);
    }

    private void BuildGeometry()
    {
        // Forearm and wrist cage.
        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "Forearm", transform,
            new Vector3(0f, 0f, -0.03f), new Vector3(0.12f, 0.10f, 0.31f),
            Quaternion.Euler(4f, 0f, 0f), metalMaterial);

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "ForearmPlate", transform,
            new Vector3(0f, 0.055f, -0.03f), new Vector3(0.10f, 0.022f, 0.27f),
            Quaternion.Euler(4f, 0f, 0f), plateMaterial);

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "WristCageL", transform,
            new Vector3(-0.075f, 0.02f, 0.08f), new Vector3(0.025f, 0.08f, 0.20f),
            Quaternion.Euler(4f, 0f, -8f), plateMaterial);

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "WristCageR", transform,
            new Vector3(0.075f, 0.02f, 0.08f), new Vector3(0.025f, 0.08f, 0.20f),
            Quaternion.Euler(4f, 0f, 8f), plateMaterial);

        // Fist block and knuckles.
        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "Fist", transform,
            new Vector3(0f, 0.015f, 0.22f), new Vector3(0.155f, 0.135f, 0.17f),
            Quaternion.Euler(-2f, 0f, 0f), metalMaterial);

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "FistPlate", transform,
            new Vector3(0f, 0.092f, 0.22f), new Vector3(0.12f, 0.03f, 0.13f),
            Quaternion.identity, plateMaterial);

        for (int i = 0; i < 3; i++)
        {
            DemoVisualFactory.CreatePrimitive(
                PrimitiveType.Cube, "Knuckle_" + i, transform,
                new Vector3(-0.052f + i * 0.052f, 0.055f, 0.31f),
                new Vector3(0.042f, 0.048f, 0.055f),
                Quaternion.Euler(-8f, 0f, 0f), plateMaterial);
        }

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "Thumb", transform,
            new Vector3(-0.087f, 0.0f, 0.17f), new Vector3(0.04f, 0.05f, 0.12f),
            Quaternion.Euler(0f, 0f, 24f), metalMaterial);

        // Energy vents on the forearm.
        for (int i = 0; i < 3; i++)
        {
            GameObject vent = DemoVisualFactory.CreatePrimitive(
                PrimitiveType.Cube, "Vent_" + i, transform,
                new Vector3(0f, 0.069f, -0.12f + i * 0.09f),
                new Vector3(0.025f, 0.012f, 0.055f),
                Quaternion.Euler(4f, 0f, 0f), glowMaterial);
            vent.transform.localRotation = Quaternion.Euler(4f, 0f, 0f);
        }

        // Glowing core set into the back of the hand.
        GameObject core = DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Sphere, "ImpulseCore", transform,
            new Vector3(0f, 0.075f, 0.15f), Vector3.one * 0.065f,
            Quaternion.identity, coreMaterial);
        coreTransform = core.transform;

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "CoreClampL", transform,
            new Vector3(-0.045f, 0.09f, 0.15f), new Vector3(0.016f, 0.05f, 0.08f),
            Quaternion.Euler(0f, 0f, 12f), plateMaterial);

        DemoVisualFactory.CreatePrimitive(
            PrimitiveType.Cube, "CoreClampR", transform,
            new Vector3(0.045f, 0.09f, 0.15f), new Vector3(0.016f, 0.05f, 0.08f),
            Quaternion.Euler(0f, 0f, -12f), plateMaterial);

        knuckleRing = CreateFacingRing("KnuckleEnergyRing", 0.115f, 0.30f, 40, 0.007f);
        wristRing = CreateFacingRing("WristEnergyRing", 0.095f, 0.10f, 40, 0.006f);

        GameObject lightObject = new GameObject("GauntletLight");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 0.11f, 0.19f);
        coreLight = lightObject.AddComponent<Light>();
        coreLight.type = LightType.Point;
        coreLight.color = new Color(0.2f, 0.9f, 1f);
        coreLight.range = 1.1f;
        coreLight.intensity = 0.12f;
        coreLight.shadows = LightShadows.None;
    }

    private LineRenderer CreateFacingRing(string name, float radius, float z, int segments, float width)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(transform, false);
        go.transform.localPosition = new Vector3(0f, 0.015f, z);

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = width;
        line.endWidth = width;
        line.material = DemoVisualFactory.CreateVertexColorMaterial(name + "_Material");
        line.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        line.receiveShadows = false;

        for (int i = 0; i < segments; i++)
        {
            float angle = Mathf.PI * 2f * i / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, Mathf.Sin(angle) * radius, 0f));
        }

        return line;
    }

    public void SetEnergyRatio(float ratio)
    {
        energyRatio = Mathf.Clamp01(ratio);
    }

    public void SetBlocking(bool value)
    {
        blocking = value;
    }

    public void PlayBlock()
    {
        flashTimer = 0.3f;
        blockRecoilTimer = 0.22f;
    }

    public void PlayPunch()
    {
        punchTimer = PunchDuration;
    }

    public void PlayShock()
    {
        shockTimer = ShockDuration;
        flashTimer = 0.45f;
        punchTimer = 0f;
    }

    public void SetVisible(bool value)
    {
        if (visible == value)
        {
            return;
        }

        visible = value;
        gameObject.SetActive(value);
    }

    private void Update()
    {
        float dt = Mathf.Min(Time.deltaTime, 0.05f);

        float targetBlock = blocking ? 1f : 0f;
        blockBlend = Mathf.MoveTowards(blockBlend, targetBlock, dt * 9f);
        displayedEnergy = Mathf.MoveTowards(displayedEnergy, energyRatio, dt * 2.4f);

        float punchArc = SampleArc(ref punchTimer, PunchDuration, 1f);
        float shockArc = SampleArc(ref shockTimer, ShockDuration, 1f);
        if (blockRecoilTimer > 0f)
        {
            blockRecoilTimer = Mathf.Max(0f, blockRecoilTimer - dt);
        }

        Vector3 position = Vector3.Lerp(BasePosition, BlockPosition, blockBlend);
        Quaternion rotation = Quaternion.Slerp(
            Quaternion.Euler(BaseEuler),
            Quaternion.Euler(BlockEuler),
            blockBlend);

        position += new Vector3(-0.05f, 0.015f, 0.18f) * punchArc;
        position += new Vector3(0.02f, -0.04f, 0.12f) * shockArc;
        position += new Vector3(-0.01f, 0.02f, -0.04f) * Mathf.Sin(blockRecoilTimer / 0.22f * Mathf.PI) * 0.6f;
        rotation *= Quaternion.Euler(-12f * punchArc, 8f * shockArc, 8f * punchArc);
        rotation *= Quaternion.Euler(-4f * blockBlend, 0f, 0f);

        transform.localPosition = Vector3.Lerp(
            transform.localPosition,
            position,
            1f - Mathf.Exp(-dt * 22f));
        transform.localRotation = Quaternion.Slerp(
            transform.localRotation,
            rotation,
            1f - Mathf.Exp(-dt * 20f));

        UpdateGlow(dt);
    }

    private float SampleArc(ref float timer, float duration, float amplitude)
    {
        if (timer <= 0f)
        {
            return 0f;
        }

        timer = Mathf.Max(0f, timer - Time.deltaTime);
        float progress = 1f - timer / duration;
        return Mathf.Sin(progress * Mathf.PI) * amplitude;
    }

    private void UpdateGlow(float dt)
    {
        if (flashTimer > 0f)
        {
            flashTimer = Mathf.Max(0f, flashTimer - dt);
        }

        float pulse = 0.5f + 0.5f * Mathf.Sin(Time.time * (3.5f + displayedEnergy * 4f));
        float fullPulse = displayedEnergy > 0.98f ? 0.35f + 0.65f * Mathf.Abs(Mathf.Sin(Time.time * 6f)) : 0f;
        float intensity = 0.3f + displayedEnergy * 2.1f + pulse * (0.12f + displayedEnergy * 0.45f) + fullPulse * 1.1f + flashTimer * 4.5f;

        Color color = Color.Lerp(
            new Color(0.18f, 0.88f, 1f),
            new Color(1f, 0.58f, 0.18f),
            Mathf.SmoothStep(0f, 1f, displayedEnergy));

        if (flashTimer > 0f)
        {
            color = Color.Lerp(color, Color.white, Mathf.Clamp01(flashTimer * 4f));
        }

        glowMaterial.EnableKeyword("_EMISSION");
        glowMaterial.SetColor("_EmissionColor", color * intensity);
        glowMaterial.SetColor("_BaseColor", Color.Lerp(new Color(0.03f, 0.06f, 0.07f), color, 0.35f));

        coreMaterial.EnableKeyword("_EMISSION");
        coreMaterial.SetColor("_EmissionColor", color * (intensity + 1.2f));
        coreMaterial.SetColor("_BaseColor", color);

        if (coreTransform != null)
        {
            float coreScale = 0.065f * (1f + displayedEnergy * 0.35f + Mathf.Sin(Time.time * 7f) * 0.04f);
            coreTransform.localScale = Vector3.one * coreScale;
        }

        if (coreLight != null)
        {
            coreLight.color = color;
            coreLight.intensity = 0.08f + displayedEnergy * 0.5f + flashTimer * 2f;
        }

        float ringAlpha = 0.18f + displayedEnergy * 0.3f + flashTimer * 0.8f;
        Color ringColor = new Color(color.r, color.g, color.b, Mathf.Clamp01(ringAlpha));
        if (knuckleRing != null)
        {
            knuckleRing.startColor = ringColor;
            knuckleRing.endColor = ringColor;
        }
        if (wristRing != null)
        {
            wristRing.startColor = ringColor;
            wristRing.endColor = ringColor;
        }
    }
}
