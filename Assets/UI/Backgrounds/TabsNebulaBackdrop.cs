using System.Collections.Generic;
using UnityEngine;

public sealed class TabsNebulaBackdrop
{
    private readonly List<Transform> layers = new List<Transform>();

    private GuitarBridgeServer owner;
    private Transform root;

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;
        root = parent;

        if (!owner.tabNebulaEnabled || root == null)
            return;

        int layerCount = Mathf.Clamp(owner.tabNebulaLayerCount, 1, 4);
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float centerY = (minY + maxY) * 0.5f;
        float height = Mathf.Max(2f, maxY - minY);

        for (int i = 0; i < layerCount; i++)
        {
            GameObject layer = GameObject.CreatePrimitive(PrimitiveType.Quad);
            layer.name = $"NebulaLayer_{i}";
            layer.transform.SetParent(root, false);

            float z = Mathf.Lerp(owner.tabStarfieldFarZ - 0.7f, owner.tabStarfieldNearZ - 0.1f, i / Mathf.Max(1f, layerCount - 1f));
            layer.transform.localPosition = new Vector3(0f, centerY + (i - (layerCount * 0.5f)) * 0.45f, z);
            layer.transform.localScale = new Vector3(owner.tabNebulaScale, Mathf.Max(8f, height * 1.65f), 1f);

            Renderer renderer = layer.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

            float t = i / Mathf.Max(1f, layerCount - 1f);
            Color c = Color.Lerp(owner.tabNebulaColorA, owner.tabNebulaColorB, t);
            c.a = Mathf.Clamp01(owner.tabNebulaOpacity * Mathf.Lerp(1f, 0.6f, t));

            renderer.material = owner.CreateSharedGlowMaterial(c, 0f);
            renderer.material.color = c;

            Object.Destroy(layer.GetComponent<Collider>());
            layers.Add(layer.transform);
        }
    }

    public void Tick(float deltaTime)
    {
        if (owner == null || !owner.tabNebulaEnabled || layers.Count == 0)
            return;

        float width = owner.tabStarfieldWidth;
        float wrapMin = -width * 0.35f;
        float wrapMax = width * 0.35f;

        for (int i = 0; i < layers.Count; i++)
        {
            Transform t = layers[i];
            if (t == null)
                continue;

            Vector3 p = t.localPosition;
            p.x -= owner.tabNebulaScrollSpeed * deltaTime * (0.6f + (i * 0.22f));
            if (p.x < wrapMin)
                p.x = wrapMax;

            p.y += Mathf.Sin((Time.time * 0.08f) + i) * deltaTime * 0.05f;
            t.localPosition = p;
        }
    }

    public void Dispose()
    {
        layers.Clear();
        owner = null;
        root = null;
    }
}
