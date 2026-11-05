using UnityEngine;

// Temporary world-space impact ring, with no collider or physical force.
public class ChargedPunchPulse : MonoBehaviour
{
    private LineRenderer line;
    private Material material;
    private float born;
    private int tier;
    private Color color;

    public static void Spawn(Vector3 point, Vector3 normal, int level)
    {
        GameObject effect = new GameObject("PunchImpact_" + level);
        effect.layer = 2;
        effect.transform.SetPositionAndRotation(point,
            Quaternion.LookRotation(normal.sqrMagnitude > 0.001f ? normal : Vector3.forward));
        ChargedPunchPulse pulse = effect.AddComponent<ChargedPunchPulse>();
        pulse.tier = level;
        pulse.born = Time.unscaledTime;
        pulse.color = level == 0 ? Color.white : level == 1 ? Color.cyan
            : level == 2 ? new Color(1f, 0.7f, 0.1f) : new Color(1f, 0.25f, 0.05f);
        pulse.line = effect.AddComponent<LineRenderer>();
        pulse.line.useWorldSpace = false;
        pulse.line.loop = true;
        pulse.line.positionCount = 40;
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit") ?? Shader.Find("Unlit/Color");
        if (shader != null)
        {
            pulse.material = new Material(shader);
            pulse.line.sharedMaterial = pulse.material;
            if (pulse.material.HasProperty("_BaseColor")) pulse.material.SetColor("_BaseColor", pulse.color);
            if (pulse.material.HasProperty("_Color")) pulse.material.SetColor("_Color", pulse.color);
        }
    }

    private void Update()
    {
        float t = (Time.unscaledTime - born) / 0.22f;
        if (t >= 1f) { Destroy(gameObject); return; }
        float radius = Mathf.Lerp(0.04f, 0.15f + tier * 0.12f, t);
        line.widthMultiplier = (0.02f + tier * 0.012f) * (1f - t);
        for (int i = 0; i < line.positionCount; i++)
        {
            float angle = i * Mathf.PI * 2f / line.positionCount;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * radius);
        }
    }

    private void OnDestroy() { if (material != null) Destroy(material); }
}
