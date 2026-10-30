using UnityEngine;
using UnityEngine.Rendering;

/// <summary>Small runtime-only visual helpers for the graybox combat demo.</summary>
public static class DemoVisualFactory
{
    public static Material CreateLitMaterial(
        string name,
        Color baseColor,
        float metallic = 0f,
        float smoothness = 0.25f,
        Color emission = default,
        float emissionIntensity = 0f)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
        {
            shader = Shader.Find("Standard");
        }

        Material material = new Material(shader)
        {
            name = name,
            enableInstancing = true
        };

        if (material.HasProperty("_BaseColor"))
        {
            material.SetColor("_BaseColor", baseColor);
        }
        if (material.HasProperty("_Color"))
        {
            material.SetColor("_Color", baseColor);
        }
        if (material.HasProperty("_Metallic"))
        {
            material.SetFloat("_Metallic", metallic);
        }
        if (material.HasProperty("_Smoothness"))
        {
            material.SetFloat("_Smoothness", smoothness);
        }
        if (emissionIntensity > 0f)
        {
            material.EnableKeyword("_EMISSION");
            material.SetColor("_EmissionColor", emission * emissionIntensity);
            material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
        }

        return material;
    }

    public static Material CreateVertexColorMaterial(string name)
    {
        Shader shader = Shader.Find("Sprites/Default");
        if (shader == null)
        {
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        }

        return new Material(shader)
        {
            name = name
        };
    }

    public static GameObject CreatePrimitive(
        PrimitiveType type,
        string name,
        Transform parent,
        Vector3 localPosition,
        Vector3 localScale,
        Quaternion localRotation,
        Material material,
        bool keepCollider = false,
        bool castShadows = false)
    {
        GameObject go = GameObject.CreatePrimitive(type);
        go.name = name;
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        go.transform.localScale = localScale;
        go.transform.localRotation = localRotation;

        Collider collider = go.GetComponent<Collider>();
        if (collider != null && !keepCollider)
        {
            Object.Destroy(collider);
        }

        MeshRenderer renderer = go.GetComponent<MeshRenderer>();
        if (renderer != null)
        {
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = castShadows ? ShadowCastingMode.On : ShadowCastingMode.Off;
            renderer.receiveShadows = castShadows;
        }

        return go;
    }

    public static GameObject CreateEmpty(string name, Transform parent, Vector3 localPosition)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;
        return go;
    }

    public static LineRenderer CreateLineCircle(
        string name,
        Transform parent,
        Vector3 localPosition,
        float radius,
        int segments,
        float width,
        Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        go.transform.localPosition = localPosition;

        LineRenderer line = go.AddComponent<LineRenderer>();
        line.useWorldSpace = false;
        line.loop = true;
        line.positionCount = segments;
        line.startWidth = width;
        line.endWidth = width;
        line.startColor = color;
        line.endColor = color;
        line.material = CreateVertexColorMaterial(name + "_Material");
        line.shadowCastingMode = ShadowCastingMode.Off;
        line.receiveShadows = false;

        for (int i = 0; i < segments; i++)
        {
            float angle = (Mathf.PI * 2f * i) / segments;
            line.SetPosition(i, new Vector3(Mathf.Cos(angle) * radius, 0f, Mathf.Sin(angle) * radius));
        }

        return line;
    }
}
