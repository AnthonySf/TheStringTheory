using System.Collections.Generic;
using UnityEngine;

public sealed class TabsStarfieldBackground : ITabsBackgroundEffect
{
    private struct Star
    {
        public Transform transform;
        public Renderer renderer;
        public float twinkleSpeed;
        public float twinklePhase;
        public float baseAlpha;
        public float depth01;
    }

    private readonly List<Star> stars = new List<Star>();

    private GuitarBridgeServer owner;
    private GameObject root;
    private Material starMaterial;

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsStarfieldBackground");
        root.transform.SetParent(parent, false);

        starMaterial = owner.CreateSharedGlowMaterial(owner.tabStarColor, owner.tabStarEmission);

        CreateStars();
    }

    public void Tick(float deltaTime)
    {
        if (root == null || stars.Count == 0)
            return;

        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.5f;

        for (int i = 0; i < stars.Count; i++)
        {
            Star star = stars[i];
            if (star.transform == null)
                continue;

            Vector3 p = star.transform.localPosition;
            p.x -= owner.tabStarDriftSpeed * deltaTime * Mathf.Lerp(0.6f, 1.3f, star.depth01);

            if (p.x < -halfWidth)
                p.x += width;

            star.transform.localPosition = p;

            if (owner.tabStarTwinkleStrength > 0.0001f)
            {
                float pulse = Mathf.Sin((Time.time * star.twinkleSpeed) + star.twinklePhase) * 0.5f + 0.5f;
                float alpha = Mathf.Clamp01(star.baseAlpha + (pulse - 0.5f) * owner.tabStarTwinkleStrength);

                if (star.renderer != null)
                {
                    Color c = owner.tabStarColor;
                    c.a = alpha;
                    star.renderer.material.color = c;
                }
            }
        }

        if (owner.tabStarSubtleVerticalWave > 0.0001f)
        {
            root.transform.localPosition = new Vector3(
                0f,
                Mathf.Sin(Time.time * 0.18f) * owner.tabStarSubtleVerticalWave,
                0f);
        }

    }

    public void Dispose()
    {
        stars.Clear();

        if (root != null)
            Object.Destroy(root);

        root = null;
        starMaterial = null;
        owner = null;
    }

    private void CreateStars()
    {
        if (owner == null || root == null)
            return;

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed);

        int count = Mathf.Clamp(owner.tabStarCount, 16, 2000);
        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.5f;
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float nearZ = owner.tabStarfieldNearZ;
        float farZ = owner.tabStarfieldFarZ;
        float alphaMin = Mathf.Min(owner.tabStarAlphaMin, owner.tabStarAlphaMax);
        float alphaMax = Mathf.Max(owner.tabStarAlphaMin, owner.tabStarAlphaMax);

        for (int i = 0; i < count; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            go.name = $"Star_{i:0000}";
            go.transform.SetParent(root.transform, false);

            float depth01 = Random.value;
            float z = Mathf.Lerp(nearZ, farZ, depth01);
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(minY, maxY);

            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.identity;

            float size = Mathf.Lerp(owner.tabStarSizeMin, owner.tabStarSizeMax, Random.value);
            go.transform.localScale = new Vector3(size, size, size);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.material = starMaterial;

            Color c = owner.tabStarColor;
            c.a = Random.Range(alphaMin, alphaMax);
            renderer.material.color = c;

            Object.Destroy(go.GetComponent<Collider>());

            stars.Add(new Star
            {
                transform = go.transform,
                renderer = renderer,
                twinkleSpeed = Random.Range(0.35f, 1.25f),
                twinklePhase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = c.a,
                depth01 = depth01
            });
        }

        Random.state = oldState;
    }
}
