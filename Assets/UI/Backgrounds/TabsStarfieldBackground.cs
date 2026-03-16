using System.Collections.Generic;
using UnityEngine;

public sealed class TabsStarfieldBackground : ITabsBackgroundEffect
{
    private enum StarLayerType
    {
        Near,
        Mid,
        Far
    }

    private struct Star
    {
        public Transform transform;
        public Renderer renderer;
        public float twinkleSpeed;
        public float twinklePhase;
        public float baseAlpha;
        public float depth01;
        public float speedMultiplier;
        public StarLayerType layer;
    }

    private sealed class ShootingStar
    {
        public Transform transform;
        public Renderer renderer;
        public Vector3 velocity;
        public float life;
        public float maxLife;
        public bool active;
    }

    private readonly List<Star> stars = new List<Star>();
    private readonly List<ShootingStar> shootingStars = new List<ShootingStar>();

    private GuitarBridgeServer owner;
    private GameObject root;
    private Material starMaterial;
    private Material shootingStarMaterial;
    private float shootingStarSpawnTimer;

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsStarfieldBackground");
        root.transform.SetParent(parent, false);

        starMaterial = owner.CreateSharedGlowMaterial(owner.tabMidStarColor, owner.tabStarEmission);
        shootingStarMaterial = owner.CreateSharedGlowMaterial(owner.tabShootingStarColor, owner.tabStarEmission * 1.35f);

        CreateStars();
        CreateShootingStarPool();
        ResetShootingStarTimer();
    }

    public void Tick(float deltaTime)
    {
        if (root == null)
            return;

        UpdateStars(deltaTime);
        UpdateShootingStars(deltaTime);

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
        shootingStars.Clear();

        if (root != null)
            Object.Destroy(root);

        root = null;
        starMaterial = null;
        shootingStarMaterial = null;
        owner = null;
    }

    private void UpdateStars(float deltaTime)
    {
        if (stars.Count == 0)
            return;

        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.5f;

        for (int i = 0; i < stars.Count; i++)
        {
            Star star = stars[i];
            if (star.transform == null)
                continue;

            Vector3 p = star.transform.localPosition;
            p.x -= owner.tabStarDriftSpeed * deltaTime * star.speedMultiplier * Mathf.Lerp(0.85f, 1.15f, star.depth01);

            if (p.x < -halfWidth)
                p.x += width;

            star.transform.localPosition = p;

            if (owner.tabStarTwinkleStrength <= 0.0001f || star.renderer == null)
                continue;

            float pulse = Mathf.Sin((Time.time * star.twinkleSpeed) + star.twinklePhase) * 0.5f + 0.5f;
            float alpha = Mathf.Clamp01(star.baseAlpha + (pulse - 0.5f) * owner.tabStarTwinkleStrength);
            Color c = GetLayerColor(star.layer);
            c.a = alpha;
            star.renderer.material.color = c;
        }
    }

    private void UpdateShootingStars(float deltaTime)
    {
        if (!owner.tabShootingStarsEnabled || shootingStars.Count == 0)
            return;

        shootingStarSpawnTimer -= deltaTime;
        if (shootingStarSpawnTimer <= 0f)
        {
            TrySpawnShootingStar();
            ResetShootingStarTimer();
        }

        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.6f;
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY) - 0.7f;
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY) + 0.7f;

        for (int i = 0; i < shootingStars.Count; i++)
        {
            ShootingStar streak = shootingStars[i];
            if (!streak.active || streak.transform == null)
                continue;

            streak.life += deltaTime;
            Vector3 p = streak.transform.localPosition + streak.velocity * deltaTime;
            streak.transform.localPosition = p;

            float lifeT = Mathf.Clamp01(streak.life / Mathf.Max(0.01f, streak.maxLife));
            if (streak.renderer != null)
            {
                Color c = owner.tabShootingStarColor;
                c.a = Mathf.Lerp(owner.tabShootingStarAlpha, 0f, lifeT);
                streak.renderer.material.color = c;
            }

            if (streak.life >= streak.maxLife || p.x < -halfWidth || p.y < minY || p.y > maxY)
            {
                streak.active = false;
                if (streak.renderer != null)
                    streak.renderer.enabled = false;
            }
        }
    }

    private void TrySpawnShootingStar()
    {
        int activeCount = 0;
        for (int i = 0; i < shootingStars.Count; i++)
        {
            if (shootingStars[i].active)
                activeCount++;
        }

        if (activeCount >= Mathf.Max(1, owner.tabShootingStarMaxConcurrent))
            return;

        ShootingStar available = null;
        for (int i = 0; i < shootingStars.Count; i++)
        {
            if (!shootingStars[i].active)
            {
                available = shootingStars[i];
                break;
            }
        }

        if (available == null || available.transform == null)
            return;

        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.5f;
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);

        float y = Random.Range(minY, maxY);
        float z = Random.Range(owner.tabStarfieldFarZ, owner.tabStarfieldNearZ);
        available.transform.localPosition = new Vector3(halfWidth + 0.8f, y, z);

        Vector3 direction = new Vector3(-1f, Random.Range(-0.22f, 0.22f), 0f).normalized;
        available.velocity = direction * owner.tabShootingStarSpeed;
        available.life = 0f;
        available.maxLife = Random.Range(0.7f, 1.35f);
        available.active = true;

        available.transform.localRotation = Quaternion.Euler(0f, 0f, Mathf.Atan2(direction.y, direction.x) * Mathf.Rad2Deg);
        if (available.renderer != null)
        {
            available.renderer.enabled = true;
            Color c = owner.tabShootingStarColor;
            c.a = owner.tabShootingStarAlpha;
            available.renderer.material.color = c;
        }
    }

    private void CreateShootingStarPool()
    {
        int poolSize = Mathf.Clamp(owner.tabShootingStarMaxConcurrent + 2, 2, 12);

        for (int i = 0; i < poolSize; i++)
        {
            GameObject go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = $"ShootingStar_{i:00}";
            go.transform.SetParent(root.transform, false);
            go.transform.localScale = new Vector3(owner.tabShootingStarLength, 0.04f, 0.04f);
            go.transform.localPosition = new Vector3(1000f, 1000f, 1000f);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.material = shootingStarMaterial;
            renderer.enabled = false;

            Object.Destroy(go.GetComponent<Collider>());

            shootingStars.Add(new ShootingStar
            {
                transform = go.transform,
                renderer = renderer,
                active = false,
                life = 0f,
                maxLife = 1f,
                velocity = Vector3.zero
            });
        }
    }

    private void ResetShootingStarTimer()
    {
        float min = Mathf.Max(0.1f, owner.tabShootingStarIntervalMin);
        float max = Mathf.Max(min, owner.tabShootingStarIntervalMax);
        shootingStarSpawnTimer = Random.Range(min, max);
    }

    private void CreateStars()
    {
        if (owner == null || root == null)
            return;

        Random.State oldState = Random.state;
        Random.InitState(owner.tabStarSeed);

        SpawnLayer(
            StarLayerType.Far,
            Mathf.Clamp(owner.tabFarStarCount, 8, 1200),
            owner.tabFarStarSizeMin,
            owner.tabFarStarSizeMax,
            owner.tabFarStarAlphaMin,
            owner.tabFarStarAlphaMax,
            owner.tabFarLayerSpeedMultiplier,
            0.65f,
            1.0f);

        SpawnLayer(
            StarLayerType.Mid,
            Mathf.Clamp(owner.tabMidStarCount, 8, 1200),
            owner.tabMidStarSizeMin,
            owner.tabMidStarSizeMax,
            owner.tabMidStarAlphaMin,
            owner.tabMidStarAlphaMax,
            owner.tabMidLayerSpeedMultiplier,
            0.35f,
            0.8f);

        SpawnLayer(
            StarLayerType.Near,
            Mathf.Clamp(owner.tabNearStarCount, 8, 1200),
            owner.tabNearStarSizeMin,
            owner.tabNearStarSizeMax,
            owner.tabNearStarAlphaMin,
            owner.tabNearStarAlphaMax,
            owner.tabNearLayerSpeedMultiplier,
            0.0f,
            0.4f);

        Random.state = oldState;
    }

    private void SpawnLayer(
        StarLayerType layerType,
        int count,
        float sizeMin,
        float sizeMax,
        float alphaMin,
        float alphaMax,
        float speedMultiplier,
        float nearBand,
        float farBand)
    {
        float width = owner.tabStarfieldWidth;
        float halfWidth = width * 0.5f;
        float minY = Mathf.Min(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float maxY = Mathf.Max(owner.tabStarfieldMinY, owner.tabStarfieldMaxY);
        float nearZ = owner.tabStarfieldNearZ;
        float farZ = owner.tabStarfieldFarZ;

        float safeSizeMin = Mathf.Max(0.001f, Mathf.Min(sizeMin, sizeMax));
        float safeSizeMax = Mathf.Max(safeSizeMin, Mathf.Max(sizeMin, sizeMax));
        float safeAlphaMin = Mathf.Clamp01(Mathf.Min(alphaMin, alphaMax));
        float safeAlphaMax = Mathf.Clamp01(Mathf.Max(alphaMin, alphaMax));

        for (int i = 0; i < count; i++)
        {
            GameObject go = GameObject.CreatePrimitive(GetPrimitiveForStyle());
            go.name = $"{layerType}Star_{i:0000}";
            go.transform.SetParent(root.transform, false);

            float layerDepth = Random.Range(nearBand, farBand);
            float z = Mathf.Lerp(nearZ, farZ, layerDepth);
            float x = Random.Range(-halfWidth, halfWidth);
            float y = Random.Range(minY, maxY);

            go.transform.localPosition = new Vector3(x, y, z);
            go.transform.localRotation = Quaternion.identity;

            float size = Mathf.Lerp(safeSizeMin, safeSizeMax, Random.value);
            go.transform.localScale = GetScaleForStyle(size);

            Renderer renderer = go.GetComponent<Renderer>();
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.material = starMaterial;

            Color c = GetLayerColor(layerType);
            c.a = Random.Range(safeAlphaMin, safeAlphaMax);
            renderer.material.color = c;

            Object.Destroy(go.GetComponent<Collider>());

            stars.Add(new Star
            {
                transform = go.transform,
                renderer = renderer,
                twinkleSpeed = Random.Range(0.35f, 1.55f),
                twinklePhase = Random.Range(0f, Mathf.PI * 2f),
                baseAlpha = c.a,
                depth01 = layerDepth,
                speedMultiplier = speedMultiplier,
                layer = layerType
            });
        }
    }

    private PrimitiveType GetPrimitiveForStyle()
    {
        switch (owner.tabStarStyle)
        {
            case GuitarBridgeServer.TabsStarStyle.Crystal:
                return PrimitiveType.Cube;
            case GuitarBridgeServer.TabsStarStyle.Neon:
                return PrimitiveType.Capsule;
            case GuitarBridgeServer.TabsStarStyle.SoftDots:
            default:
                return PrimitiveType.Sphere;
        }
    }

    private Vector3 GetScaleForStyle(float baseSize)
    {
        switch (owner.tabStarStyle)
        {
            case GuitarBridgeServer.TabsStarStyle.Crystal:
                return new Vector3(baseSize * 0.9f, baseSize * 0.9f, baseSize * 0.9f);
            case GuitarBridgeServer.TabsStarStyle.Neon:
                return new Vector3(baseSize * 0.5f, baseSize * 1.35f, baseSize * 0.5f);
            case GuitarBridgeServer.TabsStarStyle.SoftDots:
            default:
                return new Vector3(baseSize, baseSize, baseSize);
        }
    }

    private Color GetLayerColor(StarLayerType layerType)
    {
        switch (layerType)
        {
            case StarLayerType.Near:
                return owner.tabNearStarColor;
            case StarLayerType.Far:
                return owner.tabFarStarColor;
            case StarLayerType.Mid:
            default:
                return owner.tabMidStarColor;
        }
    }
}
