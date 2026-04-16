using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TabsStageBackground : ITabsBackgroundEffect
{
    private readonly bool applyHighwayOverrides;
    private readonly List<Material> ownedMaterials = new List<Material>();
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();

    private GuitarBridgeServer owner;
    private GameObject root;
    private Renderer backdropRenderer;
    private Renderer floorRenderer;
    private Renderer floorGlowRenderer;
    private Renderer spotlightLeftRenderer;
    private Renderer spotlightRightRenderer;
    private Renderer centerAuraRenderer;
    private Renderer dividerShadowRenderer;
    private int cachedScreenWidth = -1;
    private int cachedScreenHeight = -1;

    public TabsStageBackground(bool applyHighwayOverrides = false)
    {
        this.applyHighwayOverrides = applyHighwayOverrides;
    }

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsStageBackground");
        root.transform.SetParent(parent, false);

        CreateVisuals();
        RebuildLayout();
        UpdateColors(0f);
    }

    public void Tick(float deltaTime)
    {
        if (root == null || owner == null)
            return;

        if (cachedScreenWidth != Screen.width || cachedScreenHeight != Screen.height)
            RebuildLayout();

        float pulse = 0.5f + (0.5f * Mathf.Sin(Time.time * 0.72f));
        UpdateColors(pulse);
    }

    public void Dispose()
    {
        foreach (Material material in ownedMaterials)
        {
            if (material != null)
                Object.Destroy(material);
        }

        foreach (Texture2D texture in ownedTextures)
        {
            if (texture != null)
                Object.Destroy(texture);
        }

        ownedMaterials.Clear();
        ownedTextures.Clear();

        if (root != null)
            Object.Destroy(root);

        owner = null;
        root = null;
        backdropRenderer = null;
        floorRenderer = null;
        floorGlowRenderer = null;
        spotlightLeftRenderer = null;
        spotlightRightRenderer = null;
        centerAuraRenderer = null;
        dividerShadowRenderer = null;
        cachedScreenWidth = -1;
        cachedScreenHeight = -1;
    }

    private void CreateVisuals()
    {
        backdropRenderer = CreateQuad("StageBackdrop", root.transform);
        backdropRenderer.material = CreateUnlitOpaqueMaterial(new Color(0.01f, 0.01f, 0.02f, 1f), BuildSolidTexture(Color.white));

        floorRenderer = CreateQuad("StageFloor", root.transform);
        floorRenderer.material = CreateUnlitOpaqueMaterial(new Color(0.97f, 0.78f, 0.11f, 1f), BuildSolidTexture(Color.white));

        floorGlowRenderer = CreateQuad("StageFloorGlow", root.transform);
        floorGlowRenderer.material = CreateUnlitTransparentMaterial(new Color(0.72f, 0.43f, 0.52f, 0.34f), BuildEllipseTexture(512, 320, 0.92f, 0.80f));

        dividerShadowRenderer = CreateQuad("StageDividerShadow", root.transform);
        dividerShadowRenderer.material = CreateUnlitTransparentMaterial(new Color(0.12f, 0.08f, 0.10f, 0.18f), BuildVerticalAlphaTexture(0f, 1f));

        spotlightLeftRenderer = CreateQuad("StageSpotlightLeft", root.transform);
        spotlightLeftRenderer.material = CreateUnlitTransparentMaterial(new Color(0.44f, 0.63f, 0.68f, 0.12f), BuildVerticalSpotlightTexture(128, 256));

        spotlightRightRenderer = CreateQuad("StageSpotlightRight", root.transform);
        spotlightRightRenderer.material = CreateUnlitTransparentMaterial(new Color(0.67f, 0.45f, 0.58f, 0.10f), BuildVerticalSpotlightTexture(128, 256));

        centerAuraRenderer = CreateQuad("StageCenterAura", root.transform);
        centerAuraRenderer.material = CreateUnlitTransparentMaterial(new Color(0.90f, 0.72f, 0.78f, 0.12f), BuildRadialTexture(256, 256, 0.92f));
    }

    private void RebuildLayout()
    {
        cachedScreenWidth = Screen.width;
        cachedScreenHeight = Screen.height;

        GetCoverage(out float width, out float minY, out float maxY);
        GetDepthRange(out _, out float farZ);

        float height = Mathf.Max(0.01f, maxY - minY);
        float centerY = (minY + maxY) * 0.5f;

        Place(backdropRenderer, new Vector3(0f, centerY, farZ - 0.06f), new Vector3(width * 1.08f, height * 1.08f, 1f), Quaternion.identity);

        float dividerY = minY + (height * 0.29f);
        Place(floorRenderer, new Vector3(0f, dividerY - (height * 0.22f), farZ - 0.035f), new Vector3(width * 1.75f, height * 0.92f, 1f), Quaternion.Euler(0f, 0f, -9f));
        Place(floorGlowRenderer, new Vector3(width * 0.14f, dividerY - (height * 0.03f), farZ - 0.03f), new Vector3(width * 0.84f, height * 0.20f, 1f), Quaternion.Euler(0f, 0f, -9f));
        Place(dividerShadowRenderer, new Vector3(0f, dividerY + (height * 0.01f), farZ - 0.028f), new Vector3(width * 1.70f, height * 0.06f, 1f), Quaternion.Euler(0f, 0f, -9f));

        Place(spotlightLeftRenderer, new Vector3(-width * 0.18f, minY + (height * 0.34f), farZ - 0.045f), new Vector3(width * 0.38f, height * 0.92f, 1f), Quaternion.Euler(0f, 0f, 11f));
        Place(spotlightRightRenderer, new Vector3(width * 0.22f, minY + (height * 0.30f), farZ - 0.045f), new Vector3(width * 0.34f, height * 0.84f, 1f), Quaternion.Euler(0f, 0f, -13f));
        Place(centerAuraRenderer, new Vector3(width * 0.18f, minY + (height * 0.26f), farZ - 0.025f), new Vector3(width * 0.42f, height * 0.18f, 1f), Quaternion.identity);
    }

    private void UpdateColors(float pulse)
    {
        Color backdropTop = new Color(0.18f, 0.29f, 0.35f, 1f);
        Color floorBase = new Color(0.97f, 0.78f, 0.11f, 1f);
        Color floorGlow = Color.Lerp(new Color(1.00f, 0.84f, 0.24f, 0.18f), new Color(1.00f, 0.90f, 0.42f, 0.30f), pulse);
        Color dividerShadow = Color.Lerp(new Color(0.21f, 0.13f, 0.11f, 0.12f), new Color(0.30f, 0.18f, 0.16f, 0.22f), pulse);
        Color tealLight = Color.Lerp(new Color(0.45f, 0.72f, 0.78f, 0.10f), new Color(0.62f, 0.88f, 0.92f, 0.18f), pulse);
        Color roseLight = Color.Lerp(new Color(0.91f, 0.43f, 0.61f, 0.08f), new Color(0.98f, 0.60f, 0.74f, 0.16f), pulse);
        Color centerAura = Color.Lerp(new Color(1.00f, 0.73f, 0.78f, 0.08f), new Color(1.00f, 0.84f, 0.86f, 0.16f), pulse);

        ApplyColor(backdropRenderer, backdropTop);
        ApplyColor(floorRenderer, floorBase);
        ApplyColor(floorGlowRenderer, floorGlow);
        ApplyColor(dividerShadowRenderer, dividerShadow);
        ApplyColor(spotlightLeftRenderer, tealLight);
        ApplyColor(spotlightRightRenderer, roseLight);
        ApplyColor(centerAuraRenderer, centerAura);
    }

    private void GetCoverage(out float width, out float minY, out float maxY)
    {
        float baseWidth = Mathf.Max(0.01f, owner.tabSkyWidth);
        float baseMinY = Mathf.Min(owner.tabSkyMinY, owner.tabSkyMaxY);
        float baseMaxY = Mathf.Max(owner.tabSkyMinY, owner.tabSkyMaxY);

        float cameraHalfHeight = Mathf.Max(owner.tabCameraSize, (baseMaxY - baseMinY) * 0.5f);
        float cameraHalfWidth = cameraHalfHeight * Mathf.Max(1f, Camera.main != null ? Camera.main.aspect : 16f / 9f);

        width = Mathf.Max(baseWidth, cameraHalfWidth * 2f) * 1.30f;

        float centerY = (baseMinY + baseMaxY) * 0.5f;
        float halfHeight = Mathf.Max((baseMaxY - baseMinY) * 0.5f, cameraHalfHeight) * 1.18f;
        minY = centerY - halfHeight;
        maxY = centerY + halfHeight;
    }

    private void GetDepthRange(out float nearZ, out float farZ)
    {
        float userNear = Mathf.Min(owner.tabSkyNearZ, owner.tabSkyFarZ);
        float userFar = Mathf.Max(owner.tabSkyNearZ, owner.tabSkyFarZ);

        float minNear = owner.tabZDepth + 2.6f;
        nearZ = Mathf.Max(userNear, minNear);
        farZ = Mathf.Max(userFar, nearZ + 4.2f);

        if (applyHighwayOverrides)
        {
            nearZ *= 0.9f;
            farZ *= 1.1f;
        }
    }

    private static void Place(Renderer renderer, Vector3 position, Vector3 scale, Quaternion rotation)
    {
        if (renderer == null)
            return;

        Transform transform = renderer.transform;
        transform.localPosition = position;
        transform.localRotation = rotation;
        transform.localScale = scale;
    }

    private static void ApplyColor(Renderer renderer, Color color)
    {
        if (renderer == null || renderer.material == null)
            return;

        renderer.material.color = color;
        renderer.material.SetColor("_Color", color);
        renderer.material.SetColor("_BaseColor", color);
    }

    private Renderer CreateQuad(string name, Transform parent)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);

        Renderer renderer = quad.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;

        Object.Destroy(quad.GetComponent<Collider>());
        return renderer;
    }

    private Material CreateUnlitOpaqueMaterial(Color color, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Texture") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.mainTexture = texture;
        material.color = color;
        material.renderQueue = (int)RenderQueue.Geometry - 10;
        material.SetInt("_ZWrite", 1);
        material.SetInt("_Cull", (int)CullMode.Off);
        ownedMaterials.Add(material);
        return material;
    }

    private Material CreateUnlitTransparentMaterial(Color color, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
        Material material = new Material(shader);
        material.mainTexture = texture;
        material.color = color;
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_ALPHABLEND_ON");
        ownedMaterials.Add(material);
        return material;
    }

    private Texture2D BuildSolidTexture(Color color)
    {
        Texture2D texture = new Texture2D(1, 1, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        texture.SetPixel(0, 0, color);
        texture.Apply();
        ownedTextures.Add(texture);
        return texture;
    }

    private Texture2D BuildEllipseTexture(int width, int height, float radiusX, float radiusY)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float pxRadiusX = Mathf.Max(1f, center.x * Mathf.Clamp01(radiusX));
        float pxRadiusY = Mathf.Max(1f, center.y * Mathf.Clamp01(radiusY));

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float dx = (x - center.x) / pxRadiusX;
                float dy = (y - center.y) / pxRadiusY;
                float distance = Mathf.Sqrt((dx * dx) + (dy * dy));
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.88f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        ownedTextures.Add(texture);
        return texture;
    }

    private Texture2D BuildVerticalSpotlightTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        float centerX = (width - 1) * 0.5f;
        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            for (int x = 0; x < width; x++)
            {
                float dx = Mathf.Abs(x - centerX) / Mathf.Max(1f, centerX);
                float horizontal = 1f - Mathf.SmoothStep(0.08f, 1f, dx);
                float vertical = 1f - Mathf.SmoothStep(0.35f, 1f, v);
                float alpha = horizontal * vertical;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        ownedTextures.Add(texture);
        return texture;
    }

    private Texture2D BuildRadialTexture(int width, int height, float radius)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((width - 1) * 0.5f, (height - 1) * 0.5f);
        float maxRadius = Mathf.Min(center.x, center.y) * Mathf.Clamp01(radius);

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float distance = Vector2.Distance(new Vector2(x, y), center) / Mathf.Max(1f, maxRadius);
                float alpha = Mathf.Clamp01(1f - Mathf.SmoothStep(0.1f, 1f, distance));
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply();
        ownedTextures.Add(texture);
        return texture;
    }

    private Texture2D BuildVerticalAlphaTexture(float topAlpha, float bottomAlpha)
    {
        Texture2D texture = new Texture2D(2, 128, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 128; y++)
        {
            float t = y / 127f;
            float alpha = Mathf.Lerp(bottomAlpha, topAlpha, t);
            Color color = new Color(1f, 1f, 1f, alpha);
            texture.SetPixel(0, y, color);
            texture.SetPixel(1, y, color);
        }

        texture.Apply();
        ownedTextures.Add(texture);
        return texture;
    }
}
