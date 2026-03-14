using System.Collections.Generic;
using UnityEngine;

public sealed class TabsNebulaBackdrop
{
    private struct NebulaBlob
    {
        public Transform transform;
        public float driftSpeed;
        public float bobAmplitude;
        public float bobFrequency;
        public float bobPhase;
    }

    private readonly List<NebulaBlob> blobs = new List<NebulaBlob>();

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
        float height = Mathf.Max(2f, maxY - minY);
        float safeOpacity = Mathf.Clamp01(owner.tabNebulaOpacity);

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed ^ 0x45A1B7);

        for (int layer = 0; layer < layerCount; layer++)
        {
            int blobsPerLayer = 5;
            float layerT = layer / Mathf.Max(1f, layerCount - 1f);
            float z = Mathf.Lerp(owner.tabStarfieldFarZ - 1.0f, owner.tabStarfieldNearZ - 0.35f, layerT);

            for (int i = 0; i < blobsPerLayer; i++)
            {
                GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                blob.name = $"NebulaBlob_{layer}_{i}";
                blob.transform.SetParent(root, false);

                float x = Random.Range(-owner.tabStarfieldWidth * 0.35f, owner.tabStarfieldWidth * 0.35f);
                float y = Random.Range(minY, maxY);
                blob.transform.localPosition = new Vector3(x, y, z);

                float baseScale = owner.tabNebulaScale * Mathf.Lerp(0.24f, 0.42f, Random.value);
                float stretchY = Mathf.Lerp(0.5f, 0.95f, Random.value);
                float stretchZ = Mathf.Lerp(0.22f, 0.45f, Random.value);
                blob.transform.localScale = new Vector3(baseScale, baseScale * stretchY, baseScale * stretchZ);

                Renderer renderer = blob.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                Color c = Color.Lerp(owner.tabNebulaColorA, owner.tabNebulaColorB, Random.value);
                c.a = safeOpacity * Mathf.Lerp(0.22f, 0.55f, 1f - layerT);

                renderer.material = CreateTransparentNebulaMaterial(c);
                renderer.material.color = c;

                Object.Destroy(blob.GetComponent<Collider>());

                blobs.Add(new NebulaBlob
                {
                    transform = blob.transform,
                    driftSpeed = owner.tabNebulaScrollSpeed * Mathf.Lerp(0.35f, 0.9f, 1f - layerT),
                    bobAmplitude = Mathf.Lerp(0.015f, 0.065f, Random.value),
                    bobFrequency = Mathf.Lerp(0.07f, 0.19f, Random.value),
                    bobPhase = Random.Range(0f, Mathf.PI * 2f)
                });
            }
        }

        Random.state = oldState;
    }

    public void Tick(float deltaTime)
    {
        if (owner == null || !owner.tabNebulaEnabled || blobs.Count == 0)
            return;

        float wrapMin = -owner.tabStarfieldWidth * 0.45f;
        float wrapMax = owner.tabStarfieldWidth * 0.45f;

        for (int i = 0; i < blobs.Count; i++)
        {
            NebulaBlob blob = blobs[i];
            if (blob.transform == null)
                continue;

            Vector3 p = blob.transform.localPosition;
            p.x -= blob.driftSpeed * deltaTime;

            if (p.x < wrapMin)
                p.x = wrapMax;

            p.y += Mathf.Sin((Time.time * blob.bobFrequency) + blob.bobPhase) * blob.bobAmplitude * deltaTime;
            blob.transform.localPosition = p;
        }
    }

    public void Dispose()
    {
        blobs.Clear();
        owner = null;
        root = null;
    }

    private static Material CreateTransparentNebulaMaterial(Color color)
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = color;

        material.SetInt("_ZWrite", 0);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.EnableKeyword("_ALPHABLEND_ON");

        return material;
    }
}
