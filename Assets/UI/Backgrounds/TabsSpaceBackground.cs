using UnityEngine;
using UnityEngine.Rendering;

public sealed class TabsSpaceBackground : ITabsBackgroundEffect
{
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int GlowColorShaderId = Shader.PropertyToID("_GlowColor");
    private static readonly int AccentColorShaderId = Shader.PropertyToID("_AccentColor");
    private static readonly int FlowSpeedShaderId = Shader.PropertyToID("_FlowSpeed");
    private static readonly int LineIntensityShaderId = Shader.PropertyToID("_LineIntensity");
    private static readonly int SparkIntensityShaderId = Shader.PropertyToID("_SparkIntensity");
    private static readonly int BackdropModeShaderId = Shader.PropertyToID("_BackdropMode");

    private const float HighwayFloorY = -5.6f;
    private const float HighwayFloorDistance = 170f;
    private const float HighwayFloorWidth = 300f;
    private const float HighwayFloorDepth = 430f;
    private const float HighwayBackdropDistance = 720f;
    private const float MenuBackdropDistance = 16f;

    private readonly bool applyHighwayOverrides;

    private GuitarBridgeServer owner;
    private GameObject root;
    private GameObject floorQuad;
    private Renderer floorRenderer;
    private Material floorMaterial;
    private GameObject backdropQuad;
    private Renderer backdropRenderer;
    private Material backdropMaterial;

    public TabsSpaceBackground(bool applyHighwayOverrides = false)
    {
        this.applyHighwayOverrides = applyHighwayOverrides;
    }

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsSpaceBackground");
        root.transform.SetParent(parent, false);

        backdropQuad = CreateQuad("SpaceBackdrop", root.transform, out backdropRenderer);
        backdropMaterial = CreateMaterial(backdrop: true);
        if (backdropRenderer != null)
            backdropRenderer.sharedMaterial = backdropMaterial;

        if (applyHighwayOverrides)
        {
            floorQuad = CreateQuad("SpaceFloor", root.transform, out floorRenderer);
            floorMaterial = CreateMaterial(backdrop: false);
            if (floorRenderer != null)
                floorRenderer.sharedMaterial = floorMaterial;
        }

        ApplyVisualState();
        UpdatePlacement();
    }

    public void Tick(float deltaTime)
    {
        if (root == null || owner == null)
            return;

        ApplyVisualState();
        UpdatePlacement();
    }

    public void Dispose()
    {
        if (floorMaterial != null)
            Object.Destroy(floorMaterial);
        if (backdropMaterial != null)
            Object.Destroy(backdropMaterial);
        if (root != null)
            Object.Destroy(root);

        floorMaterial = null;
        backdropMaterial = null;
        floorRenderer = null;
        backdropRenderer = null;
        floorQuad = null;
        backdropQuad = null;
        root = null;
        owner = null;
    }

    private void ApplyVisualState()
    {
        ApplyMaterialState(floorMaterial, backdrop: false);
        ApplyMaterialState(backdropMaterial, backdrop: true);
    }

    private void ApplyMaterialState(Material material, bool backdrop)
    {
        if (material == null || owner == null)
            return;

        Color baseColor = owner.tabSpaceBackgroundColor;
        Color glowColor = owner.tabSpaceGlowColor;
        Color accentColor = owner.tabSpaceAccentColor;

        if (backdrop)
        {
            baseColor = Color.Lerp(baseColor, glowColor, 0.08f);
            glowColor = Color.Lerp(glowColor, accentColor, 0.18f);
            accentColor = Color.Lerp(accentColor, Color.white, 0.10f);
        }

        if (material.HasProperty(BaseColorShaderId))
            material.SetColor(BaseColorShaderId, baseColor);
        if (material.HasProperty(GlowColorShaderId))
            material.SetColor(GlowColorShaderId, glowColor);
        if (material.HasProperty(AccentColorShaderId))
            material.SetColor(AccentColorShaderId, accentColor);
        if (material.HasProperty(FlowSpeedShaderId))
            material.SetFloat(FlowSpeedShaderId, Mathf.Max(0.01f, owner.tabSpaceFlowSpeed));
        if (material.HasProperty(LineIntensityShaderId))
            material.SetFloat(LineIntensityShaderId, Mathf.Max(0.1f, owner.tabSpaceLineIntensity));
        if (material.HasProperty(SparkIntensityShaderId))
            material.SetFloat(SparkIntensityShaderId, Mathf.Max(0.1f, owner.tabSpaceSparkIntensity));
        if (material.HasProperty(BackdropModeShaderId))
            material.SetFloat(BackdropModeShaderId, backdrop ? 1f : 0f);
    }

    private void UpdatePlacement()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        UpdateBackdropPlacement(camera);

        if (applyHighwayOverrides)
            UpdateFloorPlacement(camera);
    }

    private void UpdateBackdropPlacement(Camera camera)
    {
        if (backdropQuad == null)
            return;

        float distance = applyHighwayOverrides ? HighwayBackdropDistance : MenuBackdropDistance;
        Vector3 lowerLeft = camera.ViewportToWorldPoint(new Vector3(-0.15f, -0.12f, distance));
        Vector3 lowerRight = camera.ViewportToWorldPoint(new Vector3(1.15f, -0.12f, distance));
        Vector3 upperLeft = camera.ViewportToWorldPoint(new Vector3(-0.15f, 1.15f, distance));
        Vector3 upperRight = camera.ViewportToWorldPoint(new Vector3(1.15f, 1.15f, distance));
        Vector3 center = (lowerLeft + lowerRight + upperLeft + upperRight) * 0.25f;

        backdropQuad.transform.position = center;
        backdropQuad.transform.rotation = camera.transform.rotation;
        backdropQuad.transform.localScale = new Vector3(
            Vector3.Distance(lowerLeft, lowerRight),
            Vector3.Distance(lowerLeft, upperLeft),
            1f);
    }

    private void UpdateFloorPlacement(Camera camera)
    {
        if (floorQuad == null)
            return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;
        Vector3 center = camera.transform.position + (forwardFlat * HighwayFloorDistance);
        center.x = Mathf.Lerp(center.x, boardMidX, 0.35f);
        center.y = HighwayFloorY;

        floorQuad.transform.position = center;
        floorQuad.transform.rotation = Quaternion.LookRotation(Vector3.up, forwardFlat);
        floorQuad.transform.localScale = new Vector3(HighwayFloorWidth, HighwayFloorDepth, 1f);
    }

    private Material CreateMaterial(bool backdrop)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsSpaceWarp");
        if (shader == null)
            shader = Shader.Find("Custom/TabsSpaceWarp");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(owner != null ? owner.tabSpaceBackgroundColor : Color.black, 0.2f);

        material.renderQueue = (int)RenderQueue.Transparent - 15;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.LessEqual);

        if (material.HasProperty(BackdropModeShaderId))
            material.SetFloat(BackdropModeShaderId, backdrop ? 1f : 0f);

        return material;
    }

    private static GameObject CreateQuad(string name, Transform parent, out Renderer renderer)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        Object.Destroy(quad.GetComponent<Collider>());

        renderer = quad.GetComponent<Renderer>();
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        return quad;
    }
}
