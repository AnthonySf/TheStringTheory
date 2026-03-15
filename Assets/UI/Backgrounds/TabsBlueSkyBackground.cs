using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class TabsBlueSkyBackground : ITabsBackgroundEffect
{
    private enum SkyCloudLayer
    {
        Near,
        Mid,
        Far
    }

    private struct SkyCloud
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public float speed;
        public float bobAmplitude;
        public float bobFrequency;
        public float bobPhase;
    }

    private readonly List<SkyCloud> clouds = new List<SkyCloud>();
    private readonly List<Sprite> cloudSprites = new List<Sprite>();

    private GuitarBridgeServer owner;
    private GameObject root;
    private Transform skyGradient;

    private const float SkyWidthOverscan = 1.45f;
    private const float SkyHeightOverscan = 1.60f;
    private const float CloudScaleBoost = 1.75f;

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsBlueSkyBackground");
        root.transform.SetParent(parent, false);

        CreateGradientSky();
        LoadCloudSprites();
        CreateCloudLayer(SkyCloudLayer.Far, owner.tabSkyCloudCountFar, owner.tabSkyCloudSpeedFar, owner.tabSkyCloudAlphaFar, owner.tabSkyCloudScaleMinFar, owner.tabSkyCloudScaleMaxFar, 0.65f, 1f);
        CreateCloudLayer(SkyCloudLayer.Mid, owner.tabSkyCloudCountMid, owner.tabSkyCloudSpeedMid, owner.tabSkyCloudAlphaMid, owner.tabSkyCloudScaleMinMid, owner.tabSkyCloudScaleMaxMid, 0.32f, 0.70f);
        CreateCloudLayer(SkyCloudLayer.Near, owner.tabSkyCloudCountNear, owner.tabSkyCloudSpeedNear, owner.tabSkyCloudAlphaNear, owner.tabSkyCloudScaleMinNear, owner.tabSkyCloudScaleMaxNear, 0f, 0.38f);
    }

    public void Tick(float deltaTime)
    {
        if (root == null || owner == null || clouds.Count == 0)
            return;

        GetSkyCoverage(out float width, out _, out _);
        float halfWidth = width * 0.5f;

        for (int i = 0; i < clouds.Count; i++)
        {
            SkyCloud cloud = clouds[i];
            if (cloud.transform == null)
                continue;

            Vector3 p = cloud.transform.localPosition;
            p.x -= cloud.speed * deltaTime;
            if (p.x < -halfWidth)
                p.x += width;

            p.y += Mathf.Sin((Time.time * cloud.bobFrequency) + cloud.bobPhase) * cloud.bobAmplitude * deltaTime;
            cloud.transform.localPosition = p;
        }
    }

    public void Dispose()
    {
        clouds.Clear();

        for (int i = 0; i < cloudSprites.Count; i++)
        {
            Sprite sprite = cloudSprites[i];
            if (sprite == null)
                continue;

            Texture2D texture = sprite.texture;
            Object.Destroy(sprite);

            if (texture != null)
                Object.Destroy(texture);
        }

        cloudSprites.Clear();

        if (root != null)
            Object.Destroy(root);

        owner = null;
        root = null;
        skyGradient = null;
    }

    private void GetSkyDepthRange(out float nearZ, out float farZ)
    {
        float userNear = Mathf.Min(owner.tabSkyNearZ, owner.tabSkyFarZ);
        float userFar = Mathf.Max(owner.tabSkyNearZ, owner.tabSkyFarZ);

        float minNear = owner.tabZDepth + 2.6f;
        nearZ = Mathf.Max(userNear, minNear);
        farZ = Mathf.Max(userFar, nearZ + 4.2f);
    }

    private void GetSkyCoverage(out float width, out float minY, out float maxY)
    {
        float baseWidth = Mathf.Max(0.01f, owner.tabSkyWidth);
        float baseMinY = Mathf.Min(owner.tabSkyMinY, owner.tabSkyMaxY);
        float baseMaxY = Mathf.Max(owner.tabSkyMinY, owner.tabSkyMaxY);

        float cameraHalfHeight = Mathf.Max(owner.tabCameraSize, (baseMaxY - baseMinY) * 0.5f);
        float cameraHalfWidth = cameraHalfHeight * Mathf.Max(1f, Camera.main != null ? Camera.main.aspect : 16f / 9f);

        width = Mathf.Max(baseWidth, cameraHalfWidth * 2f) * SkyWidthOverscan;

        float centerY = (baseMinY + baseMaxY) * 0.5f;
        float halfHeight = Mathf.Max((baseMaxY - baseMinY) * 0.5f, cameraHalfHeight) * SkyHeightOverscan;
        minY = centerY - halfHeight;
        maxY = centerY + halfHeight;
    }

    private void CreateGradientSky()
    {
        GetSkyCoverage(out float width, out float minY, out float maxY);
        GetSkyDepthRange(out _, out float farZ);

        GameObject gradientRoot = new GameObject("SkyGradient");
        gradientRoot.transform.SetParent(root.transform, false);
        skyGradient = gradientRoot.transform;

        CreateGradientBand("SkyBandTop", owner.tabSkyTopColor, owner.tabSkyMidColor, (minY + maxY) * 0.5f, maxY, farZ - 0.03f, width);
        CreateGradientBand("SkyBandBottom", owner.tabSkyMidColor, owner.tabSkyBottomColor, minY, (minY + maxY) * 0.5f, farZ - 0.02f, width);

        GameObject haze = GameObject.CreatePrimitive(PrimitiveType.Quad);
        haze.name = "SkyHaze";
        haze.transform.SetParent(skyGradient, false);
        haze.transform.localPosition = new Vector3(0f, minY + (maxY - minY) * 0.26f, farZ - 0.01f);
        haze.transform.localScale = new Vector3(width * 1.06f, (maxY - minY) * 0.45f, 1f);

        Renderer hazeRenderer = haze.GetComponent<Renderer>();
        hazeRenderer.shadowCastingMode = ShadowCastingMode.Off;
        hazeRenderer.receiveShadows = false;
        hazeRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        hazeRenderer.material = CreateUnlitTransparentMaterial(new Color(0.95f, 0.98f, 1f, 0.14f), BuildVerticalAlphaTexture(1f, 0f));
        Object.Destroy(haze.GetComponent<Collider>());
    }

    private void CreateGradientBand(string name, Color topColor, Color bottomColor, float minY, float maxY, float z, float width)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Quad);
        band.name = name;
        band.transform.SetParent(skyGradient, false);

        float centerY = (minY + maxY) * 0.5f;
        float height = Mathf.Max(0.01f, maxY - minY);
        band.transform.localPosition = new Vector3(0f, centerY, z);
        band.transform.localScale = new Vector3(width * 1.06f, height, 1f);

        Renderer renderer = band.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.material = CreateUnlitOpaqueMaterial(Color.white, BuildVerticalGradientTexture(topColor, bottomColor));

        Object.Destroy(band.GetComponent<Collider>());
    }

    private void LoadCloudSprites()
    {
        cloudSprites.Clear();

        string cloudDirectory = Path.Combine(Application.dataPath, "Art", "Cloud Pack");

        for (int i = 1; i <= 20; i++)
        {
            string filePath = Path.Combine(cloudDirectory, $"Cloud {i}.png");
            if (!File.Exists(filePath))
                continue;

            byte[] bytes = File.ReadAllBytes(filePath);
            if (bytes == null || bytes.Length == 0)
                continue;

            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false, true);
            if (!texture.LoadImage(bytes, false))
            {
                Object.Destroy(texture);
                continue;
            }

            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            if (sprite != null)
                cloudSprites.Add(sprite);
            else
                Object.Destroy(texture);
        }

        if (cloudSprites.Count == 0)
            cloudSprites.Add(CreateProceduralCloudSprite());
    }

    private void CreateCloudLayer(SkyCloudLayer layer, int count, float baseSpeed, float alpha, float scaleMin, float scaleMax, float nearBand, float farBand)
    {
        if (cloudSprites.Count == 0)
            return;

        GetSkyCoverage(out float width, out float minY, out float maxY);
        float halfWidth = width * 0.5f;
        GetSkyDepthRange(out float nearZ, out float farZ);

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed ^ (int)layer * 7919);

        int safeCount = Mathf.Clamp(count, 8, 220);
        for (int i = 0; i < safeCount; i++)
        {
            float depth = Random.Range(nearBand, farBand);
            float z = Mathf.Lerp(nearZ, farZ, depth);
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(minY + 0.6f, maxY - 0.6f);

            GameObject cloudGo = new GameObject($"{layer}Cloud_{i:000}");
            cloudGo.transform.SetParent(root.transform, false);
            cloudGo.transform.localPosition = new Vector3(x, y, z);
            cloudGo.transform.localRotation = Quaternion.identity;

            SpriteRenderer spriteRenderer = cloudGo.AddComponent<SpriteRenderer>();
            spriteRenderer.sprite = cloudSprites[Random.Range(0, cloudSprites.Count)];
            float layerBrightness = layer == SkyCloudLayer.Far ? 0.94f : 0.98f;
            float alphaBoost = layer == SkyCloudLayer.Near ? 1f : 0.95f;
            spriteRenderer.color = new Color(layerBrightness, layerBrightness, 1f, Mathf.Clamp01(alpha * alphaBoost * Random.Range(0.88f, 1f)));
            spriteRenderer.sortingOrder = -200;

            float scale = Random.Range(Mathf.Min(scaleMin, scaleMax), Mathf.Max(scaleMin, scaleMax));
            float stretchX = Random.Range(0.92f, 1.22f);
            float stretchY = Random.Range(0.85f, 1.15f);
            cloudGo.transform.localScale = new Vector3(scale * stretchX * CloudScaleBoost, scale * stretchY * CloudScaleBoost, 1f);

            clouds.Add(new SkyCloud
            {
                transform = cloudGo.transform,
                renderer = spriteRenderer,
                speed = baseSpeed * Random.Range(0.85f, 1.2f) * Mathf.Lerp(0.82f, 1.2f, 1f - depth),
                bobAmplitude = owner.tabSkyCloudVerticalBob * Random.Range(0.3f, 1f),
                bobFrequency = Random.Range(0.06f, 0.18f),
                bobPhase = Random.Range(0f, Mathf.PI * 2f)
            });
        }

        Random.state = oldState;
    }

    private static Sprite CreateProceduralCloudSprite()
    {
        const int width = 256;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        Vector2[] centers =
        {
            new Vector2(72f, 56f),
            new Vector2(122f, 66f),
            new Vector2(174f, 54f),
            new Vector2(112f, 42f)
        };

        float[] radii = { 40f, 46f, 38f, 30f };

        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width; x++)
            {
                float alpha = 0f;
                for (int i = 0; i < centers.Length; i++)
                {
                    float d = Vector2.Distance(new Vector2(x, y), centers[i]);
                    float circle = Mathf.Clamp01(1f - (d / radii[i]));
                    alpha = Mathf.Max(alpha, circle * circle);
                }

                alpha *= Mathf.SmoothStep(0f, 1f, y / (float)height);
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), 100f);
    }

    private static Material CreateUnlitOpaqueMaterial(Color tint, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.mainTexture = texture;
        material.color = tint;
        material.renderQueue = (int)RenderQueue.Geometry - 10;
        material.SetInt("_ZWrite", 1);
        material.SetInt("_Cull", (int)CullMode.Off);
        return material;
    }

    private static Material CreateUnlitTransparentMaterial(Color tint, Texture2D texture)
    {
        Shader shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.mainTexture = texture;
        material.color = tint;
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.EnableKeyword("_ALPHABLEND_ON");
        return material;
    }

    private static Texture2D BuildVerticalGradientTexture(Color top, Color bottom)
    {
        Texture2D texture = new Texture2D(2, 128, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 128; y++)
        {
            float t = y / 127f;
            Color c = Color.Lerp(bottom, top, t);
            texture.SetPixel(0, y, c);
            texture.SetPixel(1, y, c);
        }

        texture.Apply(false, false);
        return texture;
    }

    private static Texture2D BuildVerticalAlphaTexture(float topAlpha, float bottomAlpha)
    {
        Texture2D texture = new Texture2D(2, 64, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 64; y++)
        {
            float t = y / 63f;
            float alpha = Mathf.Lerp(bottomAlpha, topAlpha, t);
            Color c = new Color(1f, 1f, 1f, alpha);
            texture.SetPixel(0, y, c);
            texture.SetPixel(1, y, c);
        }

        texture.Apply(false, false);
        return texture;
    }
}
