using System.Collections.Generic;
using System.Linq;
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
        public SpriteRenderer glowRenderer;
        public float baseY;
        public float speed;
        public float bobAmplitude;
        public float bobFrequency;
        public float bobPhase;
        public float baseScaleX;
        public float baseScaleY;
        public float baseAlpha;
        public float spriteUnitHalfWidth;
    }

    private struct SkyStar
    {
        public Transform transform;
        public SpriteRenderer renderer;
        public Color baseColor;
        public float baseAlpha;
        public bool twinkles;
        public float twinkleSpeed;
        public float twinklePhase;
    }

    private readonly List<SkyCloud> clouds = new List<SkyCloud>();
    private readonly List<SkyStar> stars = new List<SkyStar>();
    private readonly List<Sprite> cloudSprites = new List<Sprite>();
    private readonly List<Sprite> starSprites = new List<Sprite>();
    private readonly List<Renderer> skyMainRenderers = new List<Renderer>();
    private readonly List<Renderer> skyTopRenderers = new List<Renderer>();
    private readonly List<Renderer> skyBottomRenderers = new List<Renderer>();
    private readonly List<Renderer> skyLowerFillRenderers = new List<Renderer>();
    private readonly List<Renderer> hazeRenderers = new List<Renderer>();
    private readonly List<Renderer> warmGlowRenderers = new List<Renderer>();
    private readonly List<Transform> cameraFacingBands = new List<Transform>();
    private readonly HashSet<Sprite> ownedCloudSprites = new HashSet<Sprite>();
    private readonly HashSet<Texture2D> ownedCloudTextures = new HashSet<Texture2D>();
    private readonly HashSet<Texture2D> ownedStarTextures = new HashSet<Texture2D>();
    private readonly bool applyHighwayOverrides;
    private const int StarSpriteRevision = 2;
    private int appliedStarSpriteRevision = -1;

    private GuitarBridgeServer owner;
    private Material cloudEdgeGlowMaterial;
    private int loadedCloudSpriteCount;
    private GameObject root;
    private Transform skyGradient;

    private const float SkyWidthOverscan = 1.45f;
    private const float SkyHeightOverscan = 1.60f;
    private const float CurvedSkyWidthOverscan = 3.25f;
    private const float CurvedSkyLowerCoverageFactor = 4.80f;
    private const float CurvedSkyUpperCoverageFactor = 2.25f;
    private const float CurvedSkyLowerFillMultiplier = 6.50f;
    private const int CurvedSkySegments = 56;
    private const float CurvedSkyArcDegrees = 72f;
    private const float HighwayCloudWidthCoverageFactor = 0.88f;
    private const float HighwayCloudLowerCoverageFactor = 1.22f;
    private const float HighwayCloudUpperCoverageFactor = 0.78f;
    private const float HighwayCloudSpawnWidthMultiplier = 1.72f;
    private const float HighwayCloudScaleCompensation = 0.86f;
    private const float HighwayCloudWrapPadding = 0.35f;
    private const float HighwayCloudBodyWarmTintStrength = 0.045f;
    private const float HighwayCloudGlowScaleX = 1.0f;
    private const float HighwayCloudGlowScaleY = 1.0f;
    private const float HighwayCloudGlowOffsetX = 0f;
    private const float HighwayCloudGlowOffsetY = 0f;
    private GuitarBridgeServer.TabsSkyMood appliedMood = (GuitarBridgeServer.TabsSkyMood)(-1);

    public TabsBlueSkyBackground(bool applyHighwayOverrides = false)
    {
        this.applyHighwayOverrides = applyHighwayOverrides;
    }

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsBlueSkyBackground");
        root.transform.SetParent(parent, false);

        CreateGradientSky();
        ApplyMoodToSkyIfNeeded();
        CreateStaticStars();
        LoadCloudSprites();
        LogCloudDiagnostics();
        CreateCloudLayer(SkyCloudLayer.Far, owner.tabSkyCloudCountFar, owner.tabSkyCloudSpeedFar, owner.tabSkyCloudAlphaFar, owner.tabSkyCloudScaleMinFar, owner.tabSkyCloudScaleMaxFar, 0.65f, 1f);
        CreateCloudLayer(SkyCloudLayer.Mid, owner.tabSkyCloudCountMid, owner.tabSkyCloudSpeedMid, owner.tabSkyCloudAlphaMid, owner.tabSkyCloudScaleMinMid, owner.tabSkyCloudScaleMaxMid, 0.32f, 0.70f);
        CreateCloudLayer(SkyCloudLayer.Near, owner.tabSkyCloudCountNear, owner.tabSkyCloudSpeedNear, owner.tabSkyCloudAlphaNear, owner.tabSkyCloudScaleMinNear, owner.tabSkyCloudScaleMaxNear, 0f, 0.38f);
    }

    public void Tick(float deltaTime)
    {
        if (root == null || owner == null)
            return;

        ApplyMoodToSkyIfNeeded();
        SyncStaticStarsState();
        UpdateStarTint();
        UpdateCameraFacingSprites();

        if (clouds.Count == 0)
            return;

        GetAtmosphereCoverage(false, out float width, out float minY, out float maxY);
        float visibleHalfWidth = width * 0.5f;
        float spawnHalfWidth = GetCloudSpawnHalfWidth(visibleHalfWidth);
        float safeGlobalScale = Mathf.Max(0.2f, owner.tabSkyCloudGlobalScale) * GetCloudScaleMultiplier();
        float cloudYOffset = GetCloudVerticalOffset();

        for (int i = 0; i < clouds.Count; i++)
        {
            SkyCloud cloud = clouds[i];
            if (cloud.transform == null)
                continue;

            Vector3 p = cloud.transform.localPosition;
            p.x -= cloud.speed * deltaTime;
            float cloudHalfWidth = Mathf.Max(0.1f, cloud.spriteUnitHalfWidth * cloud.baseScaleX * safeGlobalScale);
            float wrapThreshold = -spawnHalfWidth - cloudHalfWidth - HighwayCloudWrapPadding;
            float respawnX = spawnHalfWidth + cloudHalfWidth + HighwayCloudWrapPadding;
            if (p.x < wrapThreshold)
                p.x = respawnX;

            p.y = cloud.baseY + cloudYOffset + (Mathf.Sin((Time.time * cloud.bobFrequency) + cloud.bobPhase) * cloud.bobAmplitude);
            cloud.transform.localPosition = p;

            cloud.transform.localScale = new Vector3(cloud.baseScaleX * safeGlobalScale, cloud.baseScaleY * safeGlobalScale, 1f);

            if (cloud.renderer != null)
            {
                float yT = Mathf.InverseLerp(minY, maxY, p.y);
                float xT = Mathf.InverseLerp(-visibleHalfWidth, visibleHalfWidth, p.x);
                Color cloudTint = GetCloudTint(yT, xT);
                cloudTint.a = cloud.baseAlpha;
                cloud.renderer.color = cloudTint;

                if (cloud.glowRenderer != null)
                {
                    Color glowColor = GetCloudGlowColor(yT, xT);
                    glowColor.a *= cloud.baseAlpha;
                    cloud.glowRenderer.color = glowColor;
                    cloud.glowRenderer.enabled = glowColor.a > 0.004f;
                }
            }
        }
    }

    public void Dispose()
    {
        clouds.Clear();
        stars.Clear();

        foreach (Sprite sprite in starSprites.Where(sprite => sprite != null))
            Object.Destroy(sprite);

        foreach (Texture2D texture in ownedStarTextures.Where(texture => texture != null))
            Object.Destroy(texture);

        starSprites.Clear();
        ownedStarTextures.Clear();

        foreach (Sprite sprite in ownedCloudSprites.Where(sprite => sprite != null))
            Object.Destroy(sprite);

        foreach (Texture2D texture in ownedCloudTextures.Where(texture => texture != null))
            Object.Destroy(texture);

        cloudSprites.Clear();
        ownedCloudSprites.Clear();
        ownedCloudTextures.Clear();

        if (cloudEdgeGlowMaterial != null)
            Object.Destroy(cloudEdgeGlowMaterial);
        cloudEdgeGlowMaterial = null;

        if (root != null)
            Object.Destroy(root);

        owner = null;
        root = null;
        skyGradient = null;
        skyMainRenderers.Clear();
        skyTopRenderers.Clear();
        skyBottomRenderers.Clear();
        skyLowerFillRenderers.Clear();
        hazeRenderers.Clear();
        warmGlowRenderers.Clear();
        cameraFacingBands.Clear();
        appliedMood = (GuitarBridgeServer.TabsSkyMood)(-1);
        appliedStarSpriteRevision = -1;
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

        float widthOverscan = applyHighwayOverrides ? CurvedSkyWidthOverscan : SkyWidthOverscan;
        width = Mathf.Max(baseWidth, cameraHalfWidth * 2f) * widthOverscan;

        float centerY = (baseMinY + baseMaxY) * 0.5f;
        float baseHalfHeight = Mathf.Max((baseMaxY - baseMinY) * 0.5f, cameraHalfHeight);
        if (applyHighwayOverrides)
        {
            minY = centerY - (baseHalfHeight * CurvedSkyLowerCoverageFactor);
            maxY = centerY + (baseHalfHeight * CurvedSkyUpperCoverageFactor);
            return;
        }

        float halfHeight = baseHalfHeight * SkyHeightOverscan;
        minY = centerY - halfHeight;
        maxY = centerY + halfHeight;
    }

    private void GetAtmosphereCoverage(bool stars, out float width, out float minY, out float maxY)
    {
        float baseWidth = Mathf.Max(0.01f, owner.tabSkyWidth);
        float baseMinY = Mathf.Min(owner.tabSkyMinY, owner.tabSkyMaxY);
        float baseMaxY = Mathf.Max(owner.tabSkyMinY, owner.tabSkyMaxY);

        float cameraHalfHeight = Mathf.Max(owner.tabCameraSize, (baseMaxY - baseMinY) * 0.5f);
        float cameraHalfWidth = cameraHalfHeight * Mathf.Max(1f, Camera.main != null ? Camera.main.aspect : 16f / 9f);
        float centerY = (baseMinY + baseMaxY) * 0.5f;
        float baseHalfHeight = Mathf.Max((baseMaxY - baseMinY) * 0.5f, cameraHalfHeight);

        float horizontalSpread = stars ? GetStarSpreadMultiplier() : GetCloudSpreadMultiplier();
        if (applyHighwayOverrides && !stars)
        {
            width = Mathf.Max(baseWidth, cameraHalfWidth * 2f) * HighwayCloudWidthCoverageFactor * horizontalSpread;
            float highwayHalfHeight = baseHalfHeight * SkyHeightOverscan;
            minY = centerY - (highwayHalfHeight * HighwayCloudLowerCoverageFactor);
            maxY = centerY + (highwayHalfHeight * HighwayCloudUpperCoverageFactor);
            return;
        }

        width = Mathf.Max(baseWidth, cameraHalfWidth * 2f) * SkyWidthOverscan * horizontalSpread;
        float halfHeight = baseHalfHeight * SkyHeightOverscan;
        minY = centerY - halfHeight;
        maxY = centerY + halfHeight;
    }

    private void CreateGradientSky()
    {
        GetAtmosphereCoverage(false, out float atmosphereWidth, out float atmosphereMinY, out float atmosphereMaxY);
        GetSkyCoverage(out float shellWidth, out float shellMinY, out float shellMaxY);
        GetSkyDepthRange(out _, out float farZ);

        GameObject gradientRoot = new GameObject("SkyGradient");
        gradientRoot.transform.SetParent(root.transform, false);
        skyGradient = gradientRoot.transform;

        skyMainRenderers.Clear();
        skyTopRenderers.Clear();
        skyBottomRenderers.Clear();
        skyLowerFillRenderers.Clear();
        hazeRenderers.Clear();

        float extraLowerFillHeight = (shellMaxY - shellMinY) * CurvedSkyLowerFillMultiplier;
        CreateMainGradientBand(shellMinY, shellMaxY, farZ - 0.03f, shellWidth);
        if (applyHighwayOverrides)
            CreateLowerFillBand(shellMinY - extraLowerFillHeight, shellMinY + 0.02f, farZ - 0.025f, shellWidth * 1.12f);
    }

    private void CreateMainGradientBand(float minY, float maxY, float z, float width)
    {
        float centerY = (minY + maxY) * 0.5f;
        float height = Mathf.Max(0.01f, maxY - minY);
        Texture2D gradientTexture = BuildThreeStopGradientTexture(owner.tabSkyTopColor, owner.tabSkyMidColor, owner.tabSkyBottomColor);

        if (applyHighwayOverrides)
        {
            Renderer curvedRenderer = CreateCurvedBandMesh("SkyBandMain", centerY, height, z, width, Color.white, gradientTexture, false);
            if (curvedRenderer != null)
                skyMainRenderers.Add(curvedRenderer);
            return;
        }

        Renderer renderer = CreateBandQuad(
            "SkyBandMain",
            new Vector3(0f, centerY, z),
            Quaternion.identity,
            new Vector3(width * 1.06f, height, 1f),
            CreateUnlitOpaqueMaterial(Color.white, gradientTexture));
        if (renderer != null)
            skyMainRenderers.Add(renderer);
    }

    private void CreateGradientBand(string name, Color topColor, Color bottomColor, float minY, float maxY, float z, float width, List<Renderer> targetRenderers)
    {
        float centerY = (minY + maxY) * 0.5f;
        float height = Mathf.Max(0.01f, maxY - minY);
        Texture2D gradientTexture = BuildVerticalGradientTexture(topColor, bottomColor);

        if (applyHighwayOverrides)
        {
            Renderer curvedRenderer = CreateCurvedBandMesh(name, centerY, height, z, width, Color.white, gradientTexture, false);
            if (curvedRenderer != null)
                targetRenderers.Add(curvedRenderer);
            return; 
        }

        Renderer renderer = CreateBandQuad(name, new Vector3(0f, centerY, z), Quaternion.identity, new Vector3(width * 1.06f, height, 1f), CreateUnlitOpaqueMaterial(Color.white, gradientTexture));
        if (renderer != null)
            targetRenderers.Add(renderer);
    }

    private void CreateHazeBand(float centerY, float height, float z, float width)
    {
        Texture2D hazeTexture = BuildVerticalAlphaTexture(1f, 0f);
        Material hazeMaterial = CreateUnlitTransparentMaterial(new Color(0.95f, 0.98f, 1f, 0.14f), hazeTexture);

        if (applyHighwayOverrides)
        {
            Renderer billboardRenderer = CreateBandQuad("SkyHaze", new Vector3(0f, centerY, z), Quaternion.identity, new Vector3(width * 1.06f, height, 1f), hazeMaterial);
            if (billboardRenderer != null)
            {
                hazeRenderers.Add(billboardRenderer);
                cameraFacingBands.Add(billboardRenderer.transform);
            }
            return;
        }

        Renderer renderer = CreateBandQuad("SkyHaze", new Vector3(0f, centerY, z), Quaternion.identity, new Vector3(width * 1.06f, height, 1f), hazeMaterial);
        if (renderer != null)
            hazeRenderers.Add(renderer);
    }

    private void CreateWarmGlowBand(float centerY, float height, float z, float width)
    {
        Texture2D glowTexture = BuildLeftWeightedGlowTexture();
        Material glowMaterial = CreateUnlitTransparentMaterial(new Color(1f, 0.45f, 0.22f, 0.12f), glowTexture);

        if (applyHighwayOverrides)
        {
            Renderer billboardRenderer = CreateBandQuad(
                "SkyWarmGlow",
                new Vector3(-width * 0.22f, centerY, z),
                Quaternion.identity,
                new Vector3(width * 0.52f, height, 1f),
                glowMaterial);
            if (billboardRenderer != null)
            {
                warmGlowRenderers.Add(billboardRenderer);
                cameraFacingBands.Add(billboardRenderer.transform);
            }
            return;
        }

        Renderer renderer = CreateBandQuad(
            "SkyWarmGlow",
            new Vector3(-width * 0.22f, centerY, z),
            Quaternion.identity,
            new Vector3(width * 0.52f, height, 1f),
            glowMaterial);
        if (renderer != null)
            warmGlowRenderers.Add(renderer);
    }

    private Renderer CreateCurvedBandMesh(string name, float centerY, float height, float z, float width, Color tint, Texture2D texture, bool transparent)
    {
        int segments = Mathf.Max(12, CurvedSkySegments);
        float arcRadians = CurvedSkyArcDegrees * Mathf.Deg2Rad;
        float spanWidth = width * 1.08f;
        float radius = spanWidth / Mathf.Max(0.01f, arcRadians);
        float halfHeight = height * 0.5f;

        Vector3[] vertices = new Vector3[(segments + 1) * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[segments * 6];

        for (int i = 0; i <= segments; i++)
        {
            float t = i / (float)segments;
            float angle = Mathf.Lerp(-arcRadians * 0.5f, arcRadians * 0.5f, t);

            float x = Mathf.Sin(angle) * radius;
            float pointZ = z - ((1f - Mathf.Cos(angle)) * radius);
            int vertexIndex = i * 2;
            vertices[vertexIndex] = new Vector3(x, centerY - halfHeight, pointZ);
            vertices[vertexIndex + 1] = new Vector3(x, centerY + halfHeight, pointZ);
            uvs[vertexIndex] = new Vector2(t, 0f);
            uvs[vertexIndex + 1] = new Vector2(t, 1f);
        }

        for (int i = 0; i < segments; i++)
        {
            int triangleIndex = i * 6;
            int vertexIndex = i * 2;
            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex + 2;
            triangles[triangleIndex + 4] = vertexIndex + 1;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"{name}_CurvedMesh";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();

        GameObject curvedBand = new GameObject(name);
        curvedBand.transform.SetParent(skyGradient, false);

        MeshFilter meshFilter = curvedBand.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;

        MeshRenderer meshRenderer = curvedBand.AddComponent<MeshRenderer>();
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        meshRenderer.material = transparent
            ? CreateUnlitTransparentMaterial(tint, texture)
            : CreateUnlitOpaqueMaterial(tint, texture);

        return meshRenderer;
    }

    private void UpdateCameraFacingSprites()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        // Keep billboarded elements parallel to the camera plane instead of
        // aiming them at the camera position. That avoids visible rotation as
        // they move across the sky volume.
        Quaternion planeRotation = camera.transform.rotation;

        for (int i = 0; i < clouds.Count; i++)
        {
            Transform cloudTransform = clouds[i].transform;
            if (cloudTransform != null)
                cloudTransform.rotation = planeRotation;
        }

        for (int i = 0; i < stars.Count; i++)
        {
            Transform starTransform = stars[i].transform;
            if (starTransform != null)
                starTransform.rotation = planeRotation;
        }

        for (int i = 0; i < cameraFacingBands.Count; i++)
        {
            Transform bandTransform = cameraFacingBands[i];
            if (bandTransform != null)
                bandTransform.rotation = planeRotation;
        }
    }

    private void CreateLowerFillBand(float minY, float maxY, float z, float width)
    {
        float centerY = (minY + maxY) * 0.5f;
        float height = Mathf.Max(0.01f, maxY - minY);
        Texture2D fadeTexture = BuildVerticalAlphaTexture(0f, 1f);
        Material fillMaterial = CreateUnlitTransparentMaterial(owner.tabSkyBottomColor, fadeTexture);

        if (applyHighwayOverrides)
        {
            Renderer billboardRenderer = CreateBandQuad(
                "SkyBandLowerFill",
                new Vector3(0f, centerY, z),
                Quaternion.identity,
                new Vector3(width * 1.04f, height, 1f),
                fillMaterial);
            if (billboardRenderer != null)
            {
                skyLowerFillRenderers.Add(billboardRenderer);
                cameraFacingBands.Add(billboardRenderer.transform);
            }
            return;
        }

        Renderer renderer = CreateBandQuad(
            "SkyBandLowerFill",
            new Vector3(0f, centerY, z),
            Quaternion.identity,
            new Vector3(width * 1.04f, height, 1f),
            fillMaterial);
        if (renderer != null)
            skyLowerFillRenderers.Add(renderer);
    }

    private Renderer CreateBandQuad(string name, Vector3 localPosition, Quaternion localRotation, Vector3 localScale, Material material)
    {
        GameObject band = GameObject.CreatePrimitive(PrimitiveType.Quad);
        band.name = name;
        band.transform.SetParent(skyGradient, false);
        band.transform.localPosition = localPosition;
        band.transform.localRotation = localRotation;
        band.transform.localScale = localScale;

        Renderer renderer = band.GetComponent<Renderer>();
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        renderer.material = material;
        Object.Destroy(band.GetComponent<Collider>());
        return renderer;
    }


    private void LogCloudDiagnostics()
    {
        if (owner == null)
            return;

        GetSkyCoverage(out float width, out float minY, out float maxY);
        GetSkyDepthRange(out float nearZ, out float farZ);
        Debug.Log(
            $"[BlueSkyBackground] init highwayOverrides={applyHighwayOverrides} bgMode={owner.tabBackgroundMode} loadedCloudSprites={loadedCloudSpriteCount} " +
            $"cloudCounts=(near:{owner.tabSkyCloudCountNear}, mid:{owner.tabSkyCloudCountMid}, far:{owner.tabSkyCloudCountFar}) " +
            $"cloudScaleGlobal={owner.tabSkyCloudGlobalScale:F2} cloudScaleOverride={GetCloudScaleMultiplier():F2} cloudSpread={GetCloudSpreadMultiplier():F2} cloudYOffset={GetCloudVerticalOffset():F2} " +
            $"skyCoverage=width:{width:F2} minY:{minY:F2} maxY:{maxY:F2} depthRange=near:{nearZ:F2} far:{farZ:F2}");
    }

    private void LoadCloudSprites()
    {
        cloudSprites.Clear();

        LoadCloudSpritesFromResources("Cloud Pack");
        LoadCloudTexturesFromResources("Cloud Pack");
        LoadCloudSpritesFromResources("Clouds");
        LoadCloudTexturesFromResources("Clouds");

#if UNITY_EDITOR
        if (cloudSprites.Count == 0)
            LoadCloudSpritesFromProjectFiles();
#endif

        if (cloudSprites.Count == 0)
        {
            Sprite fallbackSprite = CreateProceduralCloudSprite();
            cloudSprites.Add(fallbackSprite);
            ownedCloudSprites.Add(fallbackSprite);
            if (fallbackSprite != null && fallbackSprite.texture != null)
                ownedCloudTextures.Add(fallbackSprite.texture);
        }

        loadedCloudSpriteCount = cloudSprites.Count;
    }

    private void LoadCloudSpritesFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return;

        Sprite[] loadedSprites = Resources.LoadAll<Sprite>(resourcesPath);
        if (loadedSprites == null || loadedSprites.Length == 0)
        {
            Debug.LogWarning($"[BlueSkyBackground] Resources.LoadAll<Sprite>(\"{resourcesPath}\") returned 0 sprites.");
            return;
        }

        Debug.Log($"[BlueSkyBackground] Resources.LoadAll<Sprite>(\"{resourcesPath}\") loaded {loadedSprites.Length} sprites.");

        for (int i = 0; i < loadedSprites.Length; i++)
        {
            Sprite sprite = loadedSprites[i];
            if (sprite != null)
                cloudSprites.Add(sprite);
        }
    }


    private void LoadCloudTexturesFromResources(string resourcesPath)
    {
        if (string.IsNullOrWhiteSpace(resourcesPath))
            return;

        Texture2D[] loadedTextures = Resources.LoadAll<Texture2D>(resourcesPath);
        if (loadedTextures == null || loadedTextures.Length == 0)
        {
            Debug.LogWarning($"[BlueSkyBackground] Resources.LoadAll<Texture2D>(\"{resourcesPath}\") returned 0 textures.");
            return;
        }

        int createdCount = 0;
        for (int i = 0; i < loadedTextures.Length; i++)
        {
            Texture2D texture = loadedTextures[i];
            if (texture == null)
                continue;

            bool alreadyPresent = false;
            for (int spriteIndex = 0; spriteIndex < cloudSprites.Count; spriteIndex++)
            {
                Sprite existing = cloudSprites[spriteIndex];
                if (existing != null && existing.texture == texture)
                {
                    alreadyPresent = true;
                    break;
                }
            }

            if (alreadyPresent)
                continue;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);
            if (sprite == null)
                continue;

            cloudSprites.Add(sprite);
            ownedCloudSprites.Add(sprite);
            createdCount++;
        }

        Debug.Log($"[BlueSkyBackground] Resources.LoadAll<Texture2D>(\"{resourcesPath}\") loaded {loadedTextures.Length} textures and created {createdCount} sprites.");
    }

#if UNITY_EDITOR
    private void LoadCloudSpritesFromProjectFiles()
    {

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
            texture.filterMode = FilterMode.Point;

            Sprite sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                100f,
                0,
                SpriteMeshType.FullRect);

            if (sprite != null)
            {
                cloudSprites.Add(sprite);
                ownedCloudSprites.Add(sprite);
                ownedCloudTextures.Add(texture);
            }
            else
                Object.Destroy(texture);
        }

    }

    #endif

    private void CreateCloudLayer(SkyCloudLayer layer, int count, float baseSpeed, float alpha, float scaleMin, float scaleMax, float nearBand, float farBand)
    {
        if (cloudSprites.Count == 0)
            return;

        GetAtmosphereCoverage(false, out float width, out float minY, out float maxY);
        float visibleHalfWidth = width * 0.5f;
        float spawnHalfWidth = GetCloudSpawnHalfWidth(visibleHalfWidth);
        GetSkyDepthRange(out float nearZ, out float farZ);

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed ^ (int)layer * 7919);

        int safeCount = Mathf.Clamp(count, 8, 220);
        GetCloudDepthBand(layer, nearBand, farBand, out float actualNearBand, out float actualFarBand);
        Debug.Log($"[BlueSkyBackground] spawning {layer} clouds requested={count} actual={safeCount} xVisible=[{-visibleHalfWidth:F2},{visibleHalfWidth:F2}] xSpawn=[{-spawnHalfWidth:F2},{spawnHalfWidth:F2}] yRange=[{minY + 0.6f:F2},{maxY - 0.6f:F2}] zRange=[{Mathf.Lerp(nearZ, farZ, actualNearBand):F2},{Mathf.Lerp(nearZ, farZ, actualFarBand):F2}] offsetY={GetCloudVerticalOffset():F2}");
        for (int i = 0; i < safeCount; i++)
        {
            float depth = Random.Range(actualNearBand, actualFarBand);
            float z = Mathf.Lerp(nearZ, farZ, depth);
            float x = SampleCloudX(spawnHalfWidth);
            float y = SampleCloudY(minY + 0.6f, maxY - 0.6f, layer);

            GameObject cloudGo = new GameObject($"{layer}Cloud_{i:000}");
            cloudGo.transform.SetParent(root.transform, false);
            cloudGo.transform.localPosition = new Vector3(x, y + GetCloudVerticalOffset(), z);
            cloudGo.transform.localRotation = Quaternion.identity;

            SpriteRenderer spriteRenderer = cloudGo.AddComponent<SpriteRenderer>();
            Sprite selectedSprite = cloudSprites[Random.Range(0, cloudSprites.Count)];
            spriteRenderer.sprite = selectedSprite;
            float alphaBoost = layer == SkyCloudLayer.Near ? 1f : 0.95f;
            float cloudAlpha = Mathf.Clamp01(alpha * alphaBoost * Random.Range(0.88f, 1f));
            spriteRenderer.sortingOrder = -200;

            SpriteRenderer glowRenderer = null;
            if (applyHighwayOverrides)
            {
                GameObject glowGo = new GameObject($"{layer}CloudGlow_{i:000}");
                glowGo.transform.SetParent(cloudGo.transform, false);
                glowGo.transform.localPosition = new Vector3(HighwayCloudGlowOffsetX, HighwayCloudGlowOffsetY, 0f);
                glowGo.transform.localRotation = Quaternion.identity;
                glowGo.transform.localScale = new Vector3(HighwayCloudGlowScaleX, HighwayCloudGlowScaleY, 1f);

                glowRenderer = glowGo.AddComponent<SpriteRenderer>();
                glowRenderer.sprite = selectedSprite;
                glowRenderer.sortingOrder = spriteRenderer.sortingOrder - 1;
                glowRenderer.sharedMaterial = GetCloudEdgeGlowMaterial();
                glowRenderer.color = Color.clear;
                glowRenderer.enabled = false;
            }

            float scale = Random.Range(Mathf.Min(scaleMin, scaleMax), Mathf.Max(scaleMin, scaleMax));
            float stretchX = Random.Range(0.92f, 1.22f);
            float stretchY = Random.Range(0.85f, 1.15f);
            float baseScaleX = scale * stretchX;
            float baseScaleY = scale * stretchY;
            float cloudScaleMultiplier = Mathf.Max(0.2f, owner.tabSkyCloudGlobalScale) * GetCloudScaleMultiplier();
            cloudGo.transform.localScale = new Vector3(baseScaleX * cloudScaleMultiplier, baseScaleY * cloudScaleMultiplier, 1f);

            clouds.Add(new SkyCloud
            {
                transform = cloudGo.transform,
                renderer = spriteRenderer,
                glowRenderer = glowRenderer,
                baseY = y,
                speed = baseSpeed * Random.Range(0.85f, 1.2f) * Mathf.Lerp(0.82f, 1.2f, 1f - depth),
                bobAmplitude = owner.tabSkyCloudVerticalBob * Random.Range(0.3f, 1f),
                bobFrequency = Random.Range(0.06f, 0.18f),
                bobPhase = Random.Range(0f, Mathf.PI * 2f),
                baseScaleX = baseScaleX,
                baseScaleY = baseScaleY,
                baseAlpha = cloudAlpha,
                spriteUnitHalfWidth = selectedSprite != null ? selectedSprite.bounds.extents.x : 0.5f
            });
        }

        Random.state = oldState;
    }

    private float GetCloudVerticalOffset()
    {
        return owner != null && applyHighwayOverrides
            ? owner.highwayBackgroundCloudYOffset
            : 0f;
    }

    private float GetCloudScaleMultiplier()
    {
        return owner != null && applyHighwayOverrides
            ? Mathf.Max(0.05f, owner.highwayBackgroundCloudScale) * HighwayCloudScaleCompensation
            : 1f;
    }

    private float GetCloudSpawnHalfWidth(float visibleHalfWidth)
    {
        return applyHighwayOverrides
            ? visibleHalfWidth * HighwayCloudSpawnWidthMultiplier
            : visibleHalfWidth;
    }

    private void GetCloudDepthBand(SkyCloudLayer layer, float nearBand, float farBand, out float actualNearBand, out float actualFarBand)
    {
        actualNearBand = nearBand;
        actualFarBand = farBand;

        if (!applyHighwayOverrides)
            return;

        switch (layer)
        {
            case SkyCloudLayer.Near:
                actualNearBand = Mathf.Max(actualNearBand, 0.20f);
                actualFarBand = Mathf.Max(actualFarBand, 0.52f);
                break;
            case SkyCloudLayer.Mid:
                actualNearBand = Mathf.Max(actualNearBand, 0.42f);
                actualFarBand = Mathf.Max(actualFarBand, 0.76f);
                break;
            default:
                actualNearBand = Mathf.Max(actualNearBand, 0.68f);
                actualFarBand = Mathf.Max(actualFarBand, 0.96f);
                break;
        }

        if (actualFarBand <= actualNearBand)
            actualFarBand = Mathf.Min(1f, actualNearBand + 0.05f);
    }

    private float GetCloudSpreadMultiplier()
    {
        return owner != null && applyHighwayOverrides
            ? Mathf.Max(0.05f, owner.highwayBackgroundCloudSpread)
            : 1f;
    }

    private float GetStarSpreadMultiplier()
    {
        return owner != null && applyHighwayOverrides
            ? Mathf.Max(0.05f, owner.highwayBackgroundStarSpread)
            : 1f;
    }

    private float GetStarScaleMultiplier()
    {
        return applyHighwayOverrides
            ? 0.52f * Mathf.Max(0.05f, owner.highwayBackgroundStarScale)
            : 1f;
    }

    private void ApplyMoodToSkyIfNeeded()
    {
        if (owner == null || (appliedMood == owner.tabSkyMood && skyMainRenderers.Count > 0 && (!applyHighwayOverrides || skyLowerFillRenderers.Count > 0)))
            return;

        GetSkyColors(out Color top, out Color mid, out Color bottom);

        ReplaceMaterialTexture(skyMainRenderers, BuildThreeStopGradientTexture(top, mid, bottom));

        if (skyLowerFillRenderers.Count > 0)
        {
            ReplaceMaterialTexture(skyLowerFillRenderers, BuildVerticalAlphaTexture(0f, 1f));
            for (int i = 0; i < skyLowerFillRenderers.Count; i++)
            {
                Renderer renderer = skyLowerFillRenderers[i];
                if (renderer != null && renderer.material != null)
                    renderer.material.color = bottom;
            }
        }

        if (hazeRenderers.Count > 0)
        {
            Color hazeColor;
            if (applyHighwayOverrides && owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
            {
                hazeColor = new Color(0.18f, 0.24f, 0.46f, 0.13f);
            }
            else if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Sunset)
            {
                hazeColor = new Color(1f, 0.74f, 0.50f, 0.18f);
            }
            else if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
            {
                hazeColor = new Color(0.20f, 0.36f, 0.72f, 0.16f);
            }
            else
            {
                hazeColor = new Color(0.95f, 0.98f, 1f, 0.14f);
            }
            for (int i = 0; i < hazeRenderers.Count; i++)
            {
                Renderer renderer = hazeRenderers[i];
                if (renderer != null && renderer.material != null)
                    renderer.material.color = hazeColor;
            }
        }

        if (warmGlowRenderers.Count > 0)
        {
            Color glowColor = owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight
                ? new Color(1f, 0.43f, 0.22f, 0.14f)
                : new Color(1f, 0.58f, 0.30f, 0.10f);

            for (int i = 0; i < warmGlowRenderers.Count; i++)
            {
                Renderer renderer = warmGlowRenderers[i];
                if (renderer != null && renderer.material != null)
                    renderer.material.color = glowColor;
            }
        }

        appliedMood = owner.tabSkyMood;
    }

    private static void ReplaceMaterialTexture(List<Renderer> renderers, Texture2D newTexture)
    {
        if (renderers == null || renderers.Count == 0)
            return;

        Texture oldTexture = null;
        for (int i = 0; i < renderers.Count; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || renderer.material == null)
                continue;

            if (oldTexture == null)
                oldTexture = renderer.material.mainTexture;

            renderer.material.mainTexture = newTexture;
        }

        if (oldTexture != null && oldTexture != newTexture)
            Object.Destroy(oldTexture);
    }

    private void GetSkyColors(out Color top, out Color mid, out Color bottom)
    {
        if (applyHighwayOverrides)
        {
            GetHighwaySkyColors(out top, out mid, out bottom);
            return;
        }

        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Sunset)
        {
            top = owner.tabSkySunsetTopColor;
            mid = owner.tabSkySunsetMidColor;
            bottom = owner.tabSkySunsetBottomColor;
            return;
        }

        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
        {
            top = owner.tabSkyMidnightTopColor;
            mid = owner.tabSkyMidnightMidColor;
            bottom = owner.tabSkyMidnightBottomColor;
            return;
        }

        top = owner.tabSkyTopColor;
        mid = owner.tabSkyMidColor;
        bottom = owner.tabSkyBottomColor;
    }

    private void GetHighwaySkyColors(out Color top, out Color mid, out Color bottom)
    {
        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Sunset)
        {
            top = new Color(0.11f, 0.06f, 0.09f, 1f);
            mid = new Color(0.17f, 0.10f, 0.14f, 1f);
            bottom = new Color(0.19f, 0.13f, 0.19f, 1f);
            return;
        }

        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
        {
            top = new Color(0.006f, 0.008f, 0.024f, 1f);
            mid = new Color(0.018f, 0.026f, 0.072f, 1f);
            bottom = new Color(0.045f, 0.065f, 0.145f, 1f);
            return;
        }

        top = new Color(0.03f, 0.05f, 0.10f, 1f);
        mid = new Color(0.06f, 0.10f, 0.18f, 1f);
        bottom = new Color(0.12f, 0.16f, 0.28f, 1f);
    }

    private Color GetCloudTint(float y01, float x01)
    {
        y01 = Mathf.Clamp01(y01);
        x01 = Mathf.Clamp01(x01);

        if (applyHighwayOverrides)
        {
            Color topTint;
            Color bottomTint;

            if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Sunset)
            {
                topTint = new Color(0.40f, 0.29f, 0.30f, 1f);
                bottomTint = new Color(0.11f, 0.08f, 0.11f, 1f);
            }
            else if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
            {
                topTint = new Color(0.22f, 0.24f, 0.34f, 1f);
                bottomTint = new Color(0.028f, 0.032f, 0.070f, 1f);
            }
            else
            {
                topTint = new Color(0.46f, 0.50f, 0.58f, 1f);
                bottomTint = new Color(0.14f, 0.16f, 0.20f, 1f);
            }

            Color baseTint = Color.Lerp(bottomTint, topTint, Mathf.Pow(y01, 0.88f));
            float leftWarm = Mathf.Clamp01((0.62f - x01) / 0.62f);
            float warmBand = Mathf.Clamp01(1f - Mathf.Abs(y01 - 0.34f) / 0.52f);
            float warmAmount = leftWarm * warmBand * HighwayCloudBodyWarmTintStrength;
            Color warmTint = new Color(0.72f, 0.38f, 0.22f, 1f);
            return Color.Lerp(baseTint, warmTint, warmAmount);
        }

        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Sunset)
            return Color.Lerp(owner.tabSkySunsetCloudBottomTint, owner.tabSkySunsetCloudTopTint, y01);

        if (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight)
            return Color.Lerp(owner.tabSkyMidnightCloudBottomTint, owner.tabSkyMidnightCloudTopTint, y01);

        return Color.Lerp(owner.tabSkyDayCloudBottomTint, owner.tabSkyDayCloudTopTint, y01);
    }

    private Color GetCloudGlowColor(float y01, float x01)
    {
        if (!applyHighwayOverrides)
            return Color.clear;

        y01 = Mathf.Clamp01(y01);
        x01 = Mathf.Clamp01(x01);

        float leftWarm = Mathf.Clamp01((0.58f - x01) / 0.58f);
        leftWarm = Mathf.SmoothStep(0f, 1f, leftWarm);
        float verticalBand = Mathf.Clamp01(1f - Mathf.Abs(y01 - 0.33f) / 0.44f);
        verticalBand = Mathf.Pow(verticalBand, 1.1f);
        float intensity = leftWarm * verticalBand * (owner.tabSkyMood == GuitarBridgeServer.TabsSkyMood.Midnight ? 0.34f : 0.26f);

        return new Color(1.18f, 0.56f, 0.24f, intensity);
    }

    private void CreateStaticStars()
    {
        if (owner == null || !owner.tabSkyStarsEnabled)
            return;

        GetAtmosphereCoverage(true, out float width, out float minY, out float maxY);
        GetSkyDepthRange(out float nearZ, out float farZ);

        float halfWidth = width * 0.5f;
        int starCount = Mathf.Clamp(owner.tabSkyStarCount, 8, 1200);
        float sizeMin = Mathf.Max(0.001f, Mathf.Min(owner.tabSkyStarSizeMin, owner.tabSkyStarSizeMax));
        float sizeMax = Mathf.Max(sizeMin, Mathf.Max(owner.tabSkyStarSizeMin, owner.tabSkyStarSizeMax));

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed ^ unchecked((int)0x7A11C0DEu));

        EnsureStarSprites();

        for (int i = 0; i < starCount; i++)
        {
            GameObject star = new GameObject($"SkyStaticStar_{i:000}");
            star.transform.SetParent(root.transform, false);

            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(minY + 0.5f, maxY - 0.5f);
            float z = Mathf.Clamp(farZ - Random.Range(0.45f, 1.15f), nearZ + 0.05f, farZ - 0.06f);
            star.transform.localPosition = new Vector3(x, y, z);

            Sprite starSprite = ChooseStarSprite();
            float sizeMultiplier = starSprite == starSprites[0] ? 1.45f : 1.75f;
            float size = Random.Range(sizeMin, sizeMax) * sizeMultiplier * GetStarScaleMultiplier();
            star.transform.localScale = new Vector3(size, size, 1f);

            SpriteRenderer renderer = star.AddComponent<SpriteRenderer>();
            renderer.sprite = starSprite;
            renderer.sortingOrder = -500;

            float depthT = Mathf.InverseLerp(nearZ, farZ, z);
            float distanceFade = Mathf.Lerp(1f, 0.62f, depthT);
            float alpha = Mathf.Clamp01(owner.tabSkyStarAlpha * Random.Range(0.88f, 1f) * distanceFade);
            Color baseColor = ChooseStarTint();
            renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);

            bool twinkles = Random.value < Mathf.Clamp01(owner.tabSkyStarTwinkleFraction);
            float speedMin = Mathf.Max(0.05f, owner.tabSkyStarTwinkleSpeedMin);
            float speedMax = Mathf.Max(speedMin, owner.tabSkyStarTwinkleSpeedMax);

            stars.Add(new SkyStar
            {
                transform = star.transform,
                renderer = renderer,
                baseColor = baseColor,
                baseAlpha = alpha,
                twinkles = twinkles,
                twinkleSpeed = twinkles ? Random.Range(speedMin, speedMax) : 0f,
                twinklePhase = twinkles ? Random.Range(0f, Mathf.PI * 2f) : 0f
            });
        }

        appliedStarSpriteRevision = StarSpriteRevision;
        Random.state = oldState;
    }

    private float SampleCloudX(float halfWidth)
    {
        if (!applyHighwayOverrides)
            return Random.Range(-halfWidth, halfWidth);

        float centered = Random.Range(-1f, 1f);
        centered = Mathf.Sign(centered) * Mathf.Pow(Mathf.Abs(centered), 1.85f);
        float x = centered * halfWidth * 0.82f;

        if (Random.value < 0.22f)
            x = Random.Range(-halfWidth * 0.92f, halfWidth * 0.92f);

        return x;
    }

    private float SampleCloudY(float minY, float maxY, SkyCloudLayer layer)
    {
        if (!applyHighwayOverrides)
            return Random.Range(minY, maxY);

        float t = Random.value;
        switch (layer)
        {
            case SkyCloudLayer.Near:
                t = Mathf.Pow(t, 2.45f);
                break;
            case SkyCloudLayer.Mid:
                t = Mathf.Pow(t, 1.85f);
                break;
            default:
                t = Mathf.Lerp(Mathf.Pow(t, 1.35f), 0.26f + (t * 0.74f), 0.45f);
                break;
        }

        return Mathf.Lerp(minY, maxY, t);
    }

    private void ClearStaticStars()
    {
        for (int i = 0; i < stars.Count; i++)
        {
            if (stars[i].transform != null)
                Object.Destroy(stars[i].transform.gameObject);
        }

        stars.Clear();
    }

    private void SyncStaticStarsState()
    {
        if (!owner.tabSkyStarsEnabled)
        {
            ClearStaticStars();
            return;
        }

        EnsureStarSprites();
        int targetCount = Mathf.Clamp(owner.tabSkyStarCount, 8, 1200);
        bool starSetOutdated = appliedStarSpriteRevision != StarSpriteRevision;
        if (!starSetOutdated)
        {
            for (int i = 0; i < stars.Count; i++)
            {
                Sprite sprite = stars[i].renderer != null ? stars[i].renderer.sprite : null;
                if (sprite == null || !starSprites.Contains(sprite))
                {
                    starSetOutdated = true;
                    break;
                }
            }
        }

        if (stars.Count != targetCount || starSetOutdated)
        {
            ClearStaticStars();
            CreateStaticStars();
            return;
        }

        if (stars.Count == 0)
            CreateStaticStars();
    }

    private void UpdateStarTint()
    {
        if (stars.Count == 0)
            return;

        for (int i = 0; i < stars.Count; i++)
        {
            SkyStar star = stars[i];
            if (star.renderer == null)
                continue;

            float alpha = star.baseAlpha;
            if (star.twinkles && owner.tabSkyStarTwinkleStrength > 0.0001f)
            {
                float pulse = Mathf.Sin((Time.time * star.twinkleSpeed) + star.twinklePhase) * 0.5f + 0.5f;
                float twinkle = (pulse - 0.5f) * 2f * owner.tabSkyStarTwinkleStrength;
                alpha = Mathf.Clamp01(star.baseAlpha * (1f + twinkle));
            }

            Color baseColor = star.baseColor;
            star.renderer.color = new Color(baseColor.r, baseColor.g, baseColor.b, alpha);
        }
    }

    private void EnsureStarSprites()
    {
        if (starSprites.Count > 0)
            return;

        starSprites.Add(CreateSoftStarSprite(48, 0.16f, 0.44f, 1f));
        starSprites.Add(CreateSparkleStarSprite(48, 0.08f, 0.16f, 0.95f));
        starSprites.Add(CreateSparkleStarSprite(64, 0.06f, 0.11f, 0.78f));
    }

    private Sprite ChooseStarSprite()
    {
        if (starSprites.Count == 0)
            EnsureStarSprites();

        float pick = Random.value;
        if (pick < 0.72f)
            return starSprites[0];
        if (pick < 0.92f)
            return starSprites[1];
        return starSprites[2];
    }

    private static Color ChooseStarTint()
    {
        float pick = Random.value;
        if (pick < 0.55f)
            return new Color(1f, 1f, 1f, 1f);
        if (pick < 0.82f)
            return new Color(0.82f, 0.90f, 1f, 1f);
        if (pick < 0.95f)
            return new Color(0.68f, 0.83f, 1f, 1f);
        return new Color(1f, 0.93f, 0.82f, 1f);
    }

    private Sprite CreateSoftStarSprite(int size, float coreRadius01, float glowRadius01, float glowStrength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float invHalf = 1f / Mathf.Max(1f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                Vector2 p = new Vector2(x, y);
                float dist01 = Vector2.Distance(p, center) * invHalf;
                float core = 1f - Mathf.SmoothStep(coreRadius01, coreRadius01 + 0.16f, dist01);
                float glow = 1f - Mathf.SmoothStep(coreRadius01, glowRadius01, dist01);
                float alpha = Mathf.Clamp01((core * 0.95f) + (glow * glowStrength * 0.55f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, false);
        ownedStarTextures.Add(tex);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return sprite;
    }

    private Sprite CreateSparkleStarSprite(int size, float armHalfWidth01, float diagonalHalfWidth01, float glowStrength)
    {
        Texture2D tex = new Texture2D(size, size, TextureFormat.RGBA32, false, true);
        tex.wrapMode = TextureWrapMode.Clamp;
        tex.filterMode = FilterMode.Bilinear;

        Vector2 center = new Vector2((size - 1) * 0.5f, (size - 1) * 0.5f);
        float invHalf = 1f / Mathf.Max(1f, size * 0.5f);

        for (int y = 0; y < size; y++)
        {
            for (int x = 0; x < size; x++)
            {
                float px = (x - center.x) * invHalf;
                float py = (y - center.y) * invHalf;
                float radial = Mathf.Sqrt((px * px) + (py * py));

                float vertical = 1f - Mathf.SmoothStep(armHalfWidth01, armHalfWidth01 + 0.09f, Mathf.Abs(px));
                float horizontal = 1f - Mathf.SmoothStep(armHalfWidth01, armHalfWidth01 + 0.09f, Mathf.Abs(py));
                float diagA = 1f - Mathf.SmoothStep(diagonalHalfWidth01, diagonalHalfWidth01 + 0.08f, Mathf.Abs(px - py));
                float diagB = 1f - Mathf.SmoothStep(diagonalHalfWidth01, diagonalHalfWidth01 + 0.08f, Mathf.Abs(px + py));
                float sparkle = Mathf.Max(Mathf.Max(vertical, horizontal), Mathf.Max(diagA * 0.72f, diagB * 0.72f));
                float core = 1f - Mathf.SmoothStep(0.04f, 0.22f, radial);
                float halo = 1f - Mathf.SmoothStep(0.10f, 0.62f, radial);
                float alpha = Mathf.Clamp01((sparkle * 0.74f) + (core * 0.92f) + (halo * glowStrength * 0.28f));
                tex.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        tex.Apply(false, false);
        ownedStarTextures.Add(tex);
        Sprite sprite = Sprite.Create(tex, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
        return sprite;
    }

    private static Sprite CreateProceduralCloudSprite()
    {
        const int width = 256;
        const int height = 128;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Point;

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

    private Material GetCloudEdgeGlowMaterial()
    {
        if (cloudEdgeGlowMaterial == null)
            cloudEdgeGlowMaterial = CreateSpriteAdditiveMaterial();

        return cloudEdgeGlowMaterial;
    }

    private static Material CreateSpriteAdditiveMaterial()
    {
        Shader shader = Shader.Find("Custom/HighwayCloudEdgeGlow");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader);
        material.renderQueue = (int)RenderQueue.Transparent + 5;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        material.SetInt("_DstBlend", (int)BlendMode.One);
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

    private static Texture2D BuildThreeStopGradientTexture(Color top, Color mid, Color bottom)
    {
        Texture2D texture = new Texture2D(2, 192, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < 192; y++)
        {
            float t = y / 191f;
            Color c;
            if (t < 0.5f)
            {
                float bottomToMid = t / 0.5f;
                c = Color.Lerp(bottom, mid, bottomToMid);
            }
            else
            {
                float midToTop = (t - 0.5f) / 0.5f;
                c = Color.Lerp(mid, top, midToTop);
            }

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

    private static Texture2D BuildLeftWeightedGlowTexture()
    {
        const int width = 128;
        const int height = 96;

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false, true);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;

        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float vertical = Mathf.Pow(Mathf.Sin(v * Mathf.PI), 0.85f);

            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float leftBias = Mathf.Pow(1f - u, 2.2f);
                float alpha = vertical * leftBias;
                texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
            }
        }

        texture.Apply(false, false);
        return texture;
    }
}
