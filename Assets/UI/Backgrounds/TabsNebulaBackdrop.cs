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
    private Material sharedNebulaMaterial;
    private Texture2D falloffTexture;

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;
        root = parent;

        if (!owner.tabNebulaEnabled || root == null)
            return;

        int layerCount = Mathf.Clamp(owner.tabNebulaLayerCount, 1, 4);
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float safeOpacity = Mathf.Clamp(owner.tabNebulaOpacity, 0f, 0.35f);

        falloffTexture = CreateRadialFalloffTexture(96);
        sharedNebulaMaterial = CreateTransparentNebulaMaterial(safeOpacity);

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed ^ 0x45A1B7);

        for (int layer = 0; layer < layerCount; layer++)
        {
            int blobsPerLayer = 9;
            float layerT = layer / Mathf.Max(1f, layerCount - 1f);
            float z = Mathf.Lerp(owner.tabStarfieldFarZ - 0.8f, owner.tabStarfieldNearZ - 0.4f, layerT);

            for (int i = 0; i < blobsPerLayer; i++)
            {
                GameObject blob = GameObject.CreatePrimitive(PrimitiveType.Quad);
                blob.name = $"NebulaBlob_{layer}_{i}";
                blob.transform.SetParent(root, false);

                float x = Random.Range(-owner.tabStarfieldWidth * 0.55f, owner.tabStarfieldWidth * 0.55f);
                float y = Random.Range(minY, maxY);
                blob.transform.localPosition = new Vector3(x, y, z);

                float baseScale = owner.tabNebulaScale * Mathf.Lerp(0.10f, 0.22f, Random.value);
                float stretchX = Mathf.Lerp(0.75f, 1.4f, Random.value);
                float stretchY = Mathf.Lerp(0.55f, 1.15f, Random.value);
                blob.transform.localScale = new Vector3(baseScale * stretchX, baseScale * stretchY, 1f);
                blob.transform.localRotation = Quaternion.Euler(0f, 0f, Random.Range(0f, 360f));

                Renderer renderer = blob.GetComponent<Renderer>();
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
                renderer.receiveShadows = false;
                renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

                Material blobMaterial = new Material(sharedNebulaMaterial);
                Color c = Color.Lerp(owner.tabNebulaColorA, owner.tabNebulaColorB, Random.value);
                c.a = safeOpacity * Mathf.Lerp(0.20f, 0.50f, 1f - layerT);
                blobMaterial.color = c;
                renderer.material = blobMaterial;

                Object.Destroy(blob.GetComponent<Collider>());

                blobs.Add(new NebulaBlob
                {
                    transform = blob.transform,
                    driftSpeed = owner.tabNebulaScrollSpeed * Mathf.Lerp(0.25f, 0.65f, 1f - layerT),
                    bobAmplitude = Mathf.Lerp(0.02f, 0.08f, Random.value),
                    bobFrequency = Mathf.Lerp(0.08f, 0.20f, Random.value),
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

        float wrapMin = -owner.tabStarfieldWidth * 0.70f;
        float wrapMax = owner.tabStarfieldWidth * 0.70f;

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

        if (sharedNebulaMaterial != null)
            Object.Destroy(sharedNebulaMaterial);
        if (falloffTexture != null)
            Object.Destroy(falloffTexture);

        sharedNebulaMaterial = null;
        falloffTexture = null;
        owner = null;
        root = null;
    }

    private Material CreateTransparentNebulaMaterial(float opacity)
    {
        Shader shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.color = new Color(1f, 1f, 1f, opacity);
        material.mainTexture = falloffTexture;
        material.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Transparent;

        material.SetInt("_ZWrite", 0);
        material.SetInt("_SrcBlend", (int)UnityEngine.Rendering.BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)UnityEngine.Rendering.BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)UnityEngine.Rendering.CullMode.Off);

        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");

        return material;
    }

    private static Texture2D CreateRadialFalloffTexture(int size)
    {
        int safeSize = Mathf.Clamp(size, 32, 256);
        Texture2D texture = new Texture2D(safeSize, safeSize, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float center = (safeSize - 1) * 0.5f;
        float radius = center;

        for (int y = 0; y < safeSize; y++)
        {
            for (int x = 0; x < safeSize; x++)
            {
                float dx = (x - center) / radius;
                float dy = (y - center) / radius;
                float dist = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = Mathf.Clamp01(1f - dist);
                alpha = alpha * alpha * (3f - (2f * alpha));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return texture;
    }
}
