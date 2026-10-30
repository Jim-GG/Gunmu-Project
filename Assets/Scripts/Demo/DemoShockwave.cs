using UnityEngine;

/// <summary>View-only expanding shockwave used by the impulse release.</summary>
public class DemoShockwave : MonoBehaviour
{
    private const float Duration = 0.55f;

    private LineRenderer ring;
    private Light flashLight;
    private float targetRadius;
    private float elapsed;
    private Color color;

    public static void Spawn(Vector3 position, float radius, Color color)
    {
        GameObject go = new GameObject("ImpulseShockwave");
        go.transform.position = new Vector3(position.x, 0.055f, position.z);
        DemoShockwave shockwave = go.AddComponent<DemoShockwave>();
        shockwave.Initialize(radius, color);
    }

    private void Initialize(float radius, Color tint)
    {
        targetRadius = radius;
        color = tint;

        ring = DemoVisualFactory.CreateLineCircle(
            "ShockRing",
            transform,
            Vector3.zero,
            1f,
            72,
            0.12f,
            new Color(color.r, color.g, color.b, 1f));
        SetRadius(0.35f);
        SetAlpha(1f);

        GameObject lightObject = new GameObject("ShockFlash");
        lightObject.transform.SetParent(transform, false);
        lightObject.transform.localPosition = new Vector3(0f, 1.1f, 0f);
        flashLight = lightObject.AddComponent<Light>();
        flashLight.type = LightType.Point;
        flashLight.color = color;
        flashLight.range = radius * 1.4f;
        flashLight.intensity = 8f;
        flashLight.shadows = LightShadows.None;
    }

    private void Update()
    {
        elapsed += Time.deltaTime;
        float t = Mathf.Clamp01(elapsed / Duration);
        float eased = 1f - Mathf.Pow(1f - t, 3f);

        SetRadius(Mathf.Lerp(0.35f, targetRadius, eased));
        SetAlpha(1f - t);

        if (ring != null)
        {
            ring.startWidth = Mathf.Lerp(0.14f, 0.02f, t);
            ring.endWidth = ring.startWidth;
        }

        if (flashLight != null)
        {
            flashLight.intensity = Mathf.Lerp(8f, 0f, t);
        }

        if (t >= 1f)
        {
            Destroy(gameObject);
        }
    }

    private void SetRadius(float radius)
    {
        if (ring == null)
        {
            return;
        }

        int count = ring.positionCount;
        for (int i = 0; i < count; i++)
        {
            float angle = Mathf.PI * 2f * i / count;
            ring.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }
    }

    private void SetAlpha(float alpha)
    {
        if (ring == null)
        {
            return;
        }

        Color c = new Color(color.r, color.g, color.b, Mathf.Clamp01(alpha));
        ring.startColor = c;
        ring.endColor = c;
    }
}
