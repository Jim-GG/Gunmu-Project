using UnityEngine;

// Short, pooled-by-Unity particle bursts. Never adds force or damage.
public class DemoImpactBurst : MonoBehaviour
{
    public static void Spawn(Vector3 point, Vector3 normal, int tier, bool flesh)
    {
        GameObject root = new GameObject("命中特效_" + tier);
        root.transform.SetPositionAndRotation(point + normal * .025f,
            Quaternion.LookRotation(normal.sqrMagnitude > .001f ? normal : Vector3.forward));
        Material sparks = Resources.Load<Material>("DemoPresentation/Spark");
        Material mist = Resources.Load<Material>("DemoPresentation/Mist");
        Emit(root.transform, sparks, tier >= 2 ? 32 + tier * 12 : 8,
            new Color(1f, .65f, .12f), .035f, .22f, 2.5f + tier * 1.6f, false);
        if (tier >= 2)
        {
            Emit(root.transform, mist, 12 + tier * 5, new Color(.6f, .53f, .35f, .45f), .34f, .5f, 1.6f, true);
            if (flesh) Emit(root.transform, mist, 16 + tier * 6, new Color(.34f, .035f, .025f, .65f), .18f, .55f, 2.2f + tier * .4f, true);
        }
        Destroy(root, 1.6f);
    }

    private static void Emit(Transform parent, Material material, int count, Color color, float size, float lifetime, float speed, bool cloud)
    {
        if (material == null) return;
        GameObject go = new GameObject(cloud ? "雾团" : "冲量火星");
        go.transform.SetParent(parent, false);
        ParticleSystem ps = go.AddComponent<ParticleSystem>();
        ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        var main = ps.main;
        main.loop = false; main.playOnAwake = false; main.duration = .1f;
        main.startLifetime = new ParticleSystem.MinMaxCurve(lifetime * .6f, lifetime);
        main.startSpeed = new ParticleSystem.MinMaxCurve(speed * .35f, speed);
        main.startSize = new ParticleSystem.MinMaxCurve(size * .5f, size);
        main.startColor = color; main.gravityModifier = cloud ? .08f : .55f;
        main.simulationSpace = ParticleSystemSimulationSpace.World;
        main.useUnscaledTime = true; main.maxParticles = 128;
        var emission = ps.emission; emission.enabled = false;
        var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Cone; shape.angle = 65f; shape.radius = .05f;
        var colors = ps.colorOverLifetime; colors.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
            new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0f, 1f) });
        colors.color = gradient;
        var sizes = ps.sizeOverLifetime; sizes.enabled = true;
        sizes.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, cloud ? .5f : 1f, 1f, cloud ? 2f : 0f));
        var renderer = go.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material;
        renderer.renderMode = ParticleSystemRenderMode.Billboard;
        ps.Play(); ps.Emit(count);
    }
}
