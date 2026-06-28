using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using Object = UnityEngine.Object;

public sealed class MiniGameFightStage3DRenderer
{
    private const string RootName = "MiniGameFightStage3D";
    private const string StageCameraName = "MiniGameFightStage3DCamera";
    private const string RuntimeRevisionMarkerName = "FightClubStageRuntimeRevision_28";
    private const string LeftSpotlightName = "FightClubLeftSpotlight";
    private const string RightSpotlightName = "FightClubRightSpotlight";
    private const string FloorName = "FightClubFlatFloor";
    private const string StageEdgeFixturesRootName = "FightClubStageEdgeFixtures";
    private const string FightClubAssetDirectory = "MiniGames/Assets_FightClub";
    private const string FightClubIdleSheetFileName = "Elize_Idle_Spritesheet.png";
    private const string FightClubActionJumpFileName = "NEW_Elize_Jumping.png";
    private const string FightClubActionHeadbangFileName = "NEW_Elize_Headbanging.png";
    private const string FightClubIdlePoseFileName = "NEW_Elize_Idle.png";
    private const int IdleFrameCount = 6;
    private const float IdleFps = 7.5f;
    private const float ActionHoldSeconds = 0.58f;
    private const float MissHoldSeconds = 0.82f;
    private const float StageSpotIntensity = 2500.0f;
    private const float StageSpotRange = 19.0f;
    private const float StageSpotInnerAngle = 28f;
    private const float StageSpotAngle = 31f;
    private const float StageEdgeFixtureLightIntensity = 5.0f;
    private const float StageDistance = 11.45f;
    private const float StageYOffset = -1.18f;
    private const float FloorY = -3.05f;
    private const float CharacterHeight = 6.55f;
    private const float CharacterWidth = 4.55f;
    private const float IdleFramePixelWidth = 120f;
    private const float IdleFramePixelHeight = 160f;
    private const float ReferenceVisiblePixelHeight = 124f;
    private const float CharacterFootSink = -0.02f;
    private const float LeftCharacterBaseX = -5.05f;
    private const float RightCharacterBaseX = 5.05f;
    public const int StageUnityLayer = 29;
    public const int StageUnityLayerMask = 1 << StageUnityLayer;

    private const string FloorAlbedoTextureName = "FightClubFloorAlbedo_16";
    private static readonly Color FloorColor = new Color(0.96f, 0.93f, 0.88f, 1f);
    private static readonly Color FloorEmissionColor = new Color(0.0030f, 0.0034f, 0.0045f, 1f);
    private static readonly Color FloorSpecularColor = new Color(0.58f, 0.55f, 0.68f, 1f);
    private static readonly Color FloorBackColor = new Color(0.026f, 0.027f, 0.040f, 1f);
    private static readonly Color FloorMidColor = new Color(0.060f, 0.054f, 0.068f, 1f);
    private static readonly Color FloorFrontColor = new Color(0.115f, 0.105f, 0.096f, 1f);
    private static readonly Color FloorVioletBalanceColor = new Color(0.072f, 0.048f, 0.080f, 1f);
    private static readonly Color StageMonitorColor = new Color(0.018f, 0.022f, 0.030f, 1f);
    private static readonly Color StageEdgeFixtureColor = new Color(0.010f, 0.013f, 0.018f, 1f);
    private static readonly Color StageEdgeBulbColor = new Color(0.92f, 0.97f, 1.00f, 1f);
    private static readonly Color StageEdgeBulbEmissionColor = new Color(1.15f, 1.35f, 1.55f, 1f);
    private static readonly Color HitTint = new Color(0.94f, 0.95f, 1f, 1f);
    private static readonly Color MissTint = new Color(0.88f, 0.42f, 0.56f, 1f);
    private static readonly Color MissPulseTint = new Color(1.0f, 0.58f, 0.72f, 1f);
    private static readonly Color LeftSpotColor = new Color(0.30f, 0.70f, 1.00f, 1f);
    private static readonly Color RightSpotColor = new Color(0.95f, 0.34f, 1.00f, 1f);
    private const float CharacterAlphaCutoff = 0.08f;

    private readonly GuitarBridgeServer owner;
    private readonly int[] lastChordStatuses = { -1, -1, -1, -1 };
    private GameObject root;
    private Transform leftCharacter;
    private Transform rightCharacter;
    private Transform leftShadowCaster;
    private Transform rightShadowCaster;
    private Transform edgeFixturesRoot;
    private Renderer floorRenderer;
    private Renderer leftCharacterRenderer;
    private Renderer rightCharacterRenderer;
    private Renderer leftShadowCasterRenderer;
    private Renderer rightShadowCasterRenderer;
    private Material leftCharacterMaterial;
    private Material rightCharacterMaterial;
    private Material leftShadowCasterMaterial;
    private Material rightShadowCasterMaterial;
    private Material floorMaterial;
    private bool stageEdgeFixturesVisible = true;
    private readonly Dictionary<Light, int> externalLightCullingMasks = new Dictionary<Light, int>();
    private Camera stageLayerCamera;
    private bool renderStateCaptured;
    private bool stageRuntimeStateRefreshed;
    private bool externalLightsIsolated;
    private bool staleStageCameraCleanupDone;
    private bool originalFogEnabled;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogDensity;
    private float originalFogStartDistance;
    private float originalFogEndDistance;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientLight;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private float originalAmbientIntensity;
    private Color originalSubtractiveShadowColor;
    private float originalReflectionIntensity;
    private Texture2D idleSheetTexture;
    private Texture2D actionJumpTexture;
    private Texture2D actionHeadbangTexture;
    private Texture2D idlePoseTexture;
    private static Texture2D floorAlbedoTexture;
    private bool staleRootScanDone;
    private int lastRound = -1;
    private int lastActiveChordIndex = -2;
    private int lastOpponentActiveChordIndex = -2;
    private int lastActionChordIndex;
    private int lastOpponentActionChordIndex;
    private float actionStartedAt = -999f;
    private float actionUntil = -999f;
    private float opponentActionStartedAt = -999f;
    private float opponentActionUntil = -999f;
    private float missStartedAt = -999f;
    private float missUntil = -999f;

    public MiniGameFightStage3DRenderer(GuitarBridgeServer owner)
    {
        this.owner = owner;
    }

    public void Update(FightClubMiniGameSnapshot snapshot, bool visible)
    {
        bool shouldShow = visible && snapshot != null && snapshot.active && !snapshot.ended;
        if (!shouldShow)
        {
            Hide();
            return;
        }

        DestroyExistingStageCameraOnce();
        EnsureMainCameraIncludesStageLayer();
        ApplyFightClubRenderState();
        EnsureRoot();
        EnsureCurrentRuntimeRevision();
        EnsureTextures();
        EnsureStageRuntimeState();
        EnsureStageLightingIsolated();
        HandleEdgeFixtureVisibilityShortcuts();
        PositionRoot();
        SetVisible(true);
        UpdateTriggers(snapshot);
        UpdateCharacterPose(snapshot);
    }

    public void Hide()
    {
        DestroyExistingStageCamera();
        EnsureMainCameraIncludesStageLayer();
        RestoreFightClubRenderState();
        RestoreExternalLightCullingMasks();

        if (root == null)
        {
            DestroyExistingRootOnce();
            return;
        }

        SetVisible(false);
        ResetState();
    }

    private void EnsureRoot()
    {
        if (root != null)
            return;

        DestroyExistingRoot();
        DestroyExistingStageCamera();
        staleRootScanDone = true;

        root = new GameObject(RootName);
        root.hideFlags = HideFlags.DontSave;
        root.layer = StageUnityLayer;
        if (owner != null)
            root.transform.SetParent(owner.transform, false);

        CreateRuntimeRevisionMarker(root.transform);
        CreateStageLighting(root.transform);
        CreateFloor(root.transform);
        CreateStageDesign(root.transform);
        CreateCharacters(root.transform);
        root.SetActive(false);
    }

    private void EnsureCurrentRuntimeRevision()
    {
        if (root == null || root.transform.Find(RuntimeRevisionMarkerName) != null)
            return;

        RestoreExternalLightCullingMasks();
        root.name = $"{RootName}_Stale";
        Object.Destroy(root);
        ClearStageReferences();
        EnsureRoot();
    }

    private static void CreateRuntimeRevisionMarker(Transform parent)
    {
        GameObject marker = new GameObject(RuntimeRevisionMarkerName);
        marker.hideFlags = HideFlags.DontSave;
        marker.layer = StageUnityLayer;
        marker.transform.SetParent(parent, false);
    }

    private void ClearStageReferences()
    {
        root = null;
        leftCharacter = null;
        rightCharacter = null;
        leftShadowCaster = null;
        rightShadowCaster = null;
        edgeFixturesRoot = null;
        floorRenderer = null;
        leftCharacterRenderer = null;
        rightCharacterRenderer = null;
        leftShadowCasterRenderer = null;
        rightShadowCasterRenderer = null;
        leftCharacterMaterial = null;
        rightCharacterMaterial = null;
        leftShadowCasterMaterial = null;
        rightShadowCasterMaterial = null;
        floorMaterial = null;
        stageRuntimeStateRefreshed = false;
        externalLightsIsolated = false;
    }

    private void EnsureMainCameraIncludesStageLayer()
    {
        Camera camera = Camera.main;
        if (camera == null)
            return;

        if (stageLayerCamera == camera && (camera.cullingMask & StageUnityLayerMask) != 0)
            return;

        camera.cullingMask |= StageUnityLayerMask;
        stageLayerCamera = camera;
    }

    private void ApplyFightClubRenderState()
    {
        if (renderStateCaptured)
            return;

        originalFogEnabled = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStartDistance = RenderSettings.fogStartDistance;
        originalFogEndDistance = RenderSettings.fogEndDistance;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientLight = RenderSettings.ambientLight;
        originalAmbientSkyColor = RenderSettings.ambientSkyColor;
        originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        originalAmbientGroundColor = RenderSettings.ambientGroundColor;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalSubtractiveShadowColor = RenderSettings.subtractiveShadowColor;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        renderStateCaptured = true;

        RenderSettings.fog = false;
        RenderSettings.ambientMode = AmbientMode.Flat;
        RenderSettings.ambientLight = Color.black;
        RenderSettings.ambientSkyColor = Color.black;
        RenderSettings.ambientEquatorColor = Color.black;
        RenderSettings.ambientGroundColor = Color.black;
        RenderSettings.ambientIntensity = 0f;
        RenderSettings.subtractiveShadowColor = Color.black;
        RenderSettings.reflectionIntensity = 0f;
    }

    private void RestoreFightClubRenderState()
    {
        if (!renderStateCaptured)
            return;

        RenderSettings.fog = originalFogEnabled;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStartDistance;
        RenderSettings.fogEndDistance = originalFogEndDistance;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientLight = originalAmbientLight;
        RenderSettings.ambientSkyColor = originalAmbientSkyColor;
        RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
        RenderSettings.ambientGroundColor = originalAmbientGroundColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.subtractiveShadowColor = originalSubtractiveShadowColor;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;
        renderStateCaptured = false;
    }

    private void RefreshStageRuntimeState()
    {
        if (root == null)
            return;

        if (floorRenderer == null)
        {
            Transform floor = root.transform.Find(FloorName);
            if (floor != null)
                floorRenderer = floor.GetComponent<Renderer>();
        }

        if (floorRenderer != null)
        {
            if (floorMaterial == null || floorRenderer.sharedMaterial != floorMaterial)
                floorMaterial = floorRenderer.sharedMaterial != null ? floorRenderer.sharedMaterial : CreateFloorMaterial();
            floorRenderer.sharedMaterial = floorMaterial;
            ConfigureFloorMaterial(floorMaterial);
            ConfigureRenderer(floorRenderer, ShadowCastingMode.Off, receiveShadows: true);
        }

        RefreshStageSpotLight(LeftSpotlightName, new Vector3(-8.15f, 3.95f, -6.10f), new Vector3(LeftCharacterBaseX + 0.20f, FloorY + 0.03f, 0.82f), LeftSpotColor);
        RefreshStageSpotLight(RightSpotlightName, new Vector3(8.15f, 3.95f, -6.10f), new Vector3(RightCharacterBaseX - 0.20f, FloorY + 0.03f, 0.82f), RightSpotColor);
    }

    private void EnsureStageRuntimeState()
    {
        if (stageRuntimeStateRefreshed)
            return;

        RefreshStageRuntimeState();
        stageRuntimeStateRefreshed = true;
    }

    private void HandleEdgeFixtureVisibilityShortcuts()
    {
        if (IsEdgeFixturesHideShortcutPressed())
        {
            SetStageEdgeFixturesVisible(false);
            return;
        }

        if (IsEdgeFixturesShowShortcutPressed())
            SetStageEdgeFixturesVisible(true);
    }

    private void SetStageEdgeFixturesVisible(bool visible)
    {
        stageEdgeFixturesVisible = visible;

        Transform fixtures = GetStageEdgeFixturesRoot();
        if (fixtures == null)
            return;

        bool changed = fixtures.gameObject.activeSelf != visible;
        if (!changed)
            return;

        fixtures.gameObject.SetActive(visible);
        Debug.Log($"[FightClubStageDebug] Edge fixture lights {(visible ? "shown" : "hidden")}.");
    }

    private Transform GetStageEdgeFixturesRoot()
    {
        if (edgeFixturesRoot != null)
            return edgeFixturesRoot;

        if (root == null)
            return null;

        edgeFixturesRoot = root.transform.Find(StageEdgeFixturesRootName);
        return edgeFixturesRoot;
    }

    private static bool IsEdgeFixturesHideShortcutPressed()
    {
        bool legacyPressed = Input.GetKeyDown(KeyCode.Alpha1) || Input.GetKeyDown(KeyCode.Keypad1);
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool inputSystemPressed = keyboard != null &&
            ((keyboard.digit1Key != null && keyboard.digit1Key.wasPressedThisFrame) ||
             (keyboard.numpad1Key != null && keyboard.numpad1Key.wasPressedThisFrame));
        return legacyPressed || inputSystemPressed;
    }

    private static bool IsEdgeFixturesShowShortcutPressed()
    {
        bool legacyPressed = Input.GetKeyDown(KeyCode.Alpha2) || Input.GetKeyDown(KeyCode.Keypad2);
        UnityEngine.InputSystem.Keyboard keyboard = UnityEngine.InputSystem.Keyboard.current;
        bool inputSystemPressed = keyboard != null &&
            ((keyboard.digit2Key != null && keyboard.digit2Key.wasPressedThisFrame) ||
             (keyboard.numpad2Key != null && keyboard.numpad2Key.wasPressedThisFrame));
        return legacyPressed || inputSystemPressed;
    }

    private void EnsureStageLightingIsolated()
    {
        if (externalLightsIsolated)
            return;

        IsolateStageLighting();
        externalLightsIsolated = true;
    }

    private void RefreshStageSpotLight(string name, Vector3 localPosition, Vector3 target, Color color)
    {
        if (root == null)
            return;

        Transform lightTransform = root.transform.Find(name);
        if (lightTransform == null)
            return;

        lightTransform.localPosition = localPosition;
        Vector3 direction = target - localPosition;
        if (direction.sqrMagnitude > 0.0001f)
            lightTransform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        Light light = lightTransform.GetComponent<Light>();
        if (light == null)
            return;

        ConfigureStageSpotLight(light, color, StageSpotIntensity, StageSpotRange, StageSpotAngle, castShadows: true);
    }

    private void IsolateStageLighting()
    {
        if (root == null)
            return;

        Light[] lights = Resources.FindObjectsOfTypeAll<Light>();
        for (int i = 0; i < lights.Length; i++)
        {
            Light light = lights[i];
            if (light == null || IsStageLight(light))
                continue;

            GameObject lightObject = light.gameObject;
            if (lightObject == null || !lightObject.scene.IsValid() || !lightObject.scene.isLoaded)
                continue;

            int mask = light.cullingMask;
            if ((mask & StageUnityLayerMask) == 0)
                continue;

            if (!externalLightCullingMasks.ContainsKey(light))
                externalLightCullingMasks.Add(light, mask);

            light.cullingMask = mask & ~StageUnityLayerMask;
        }
    }

    private void RestoreExternalLightCullingMasks()
    {
        externalLightsIsolated = false;

        if (externalLightCullingMasks.Count == 0)
            return;

        foreach (KeyValuePair<Light, int> entry in externalLightCullingMasks)
        {
            if (entry.Key != null)
                entry.Key.cullingMask = entry.Value;
        }

        externalLightCullingMasks.Clear();
    }

    private bool IsStageLight(Light light)
    {
        return light != null && root != null && light.transform != null && light.transform.IsChildOf(root.transform);
    }

    private void CreateFloor(Transform parent)
    {
        GameObject floor = GameObject.CreatePrimitive(PrimitiveType.Plane);
        floor.name = FloorName;
        floor.transform.SetParent(parent, false);
        floor.transform.localPosition = new Vector3(0f, FloorY, 0.15f);
        floor.transform.localRotation = Quaternion.identity;
        floor.transform.localScale = new Vector3(1.82f, 1f, 1.92f);
        floorRenderer = floor.GetComponent<Renderer>();
        floorMaterial = CreateFloorMaterial();
        floorRenderer.sharedMaterial = floorMaterial;
        ConfigureRenderer(floorRenderer, ShadowCastingMode.Off, receiveShadows: true);
        Object.Destroy(floor.GetComponent<Collider>());
    }

    private void CreateStageDesign(Transform parent)
    {
        Material monitorMaterial = CreateStageColorMaterial(StageMonitorColor, (int)RenderQueue.Geometry + 25);
        Material platformMaterial = floorMaterial != null ? floorMaterial : CreateFloorMaterial();
        Material edgeFixtureMaterial = CreateStageEdgeFixtureMaterial();
        Material edgeBulbMaterial = CreateStageEdgeBulbMaterial();

        CreateStagePlatformEdges(parent, platformMaterial);
        edgeFixturesRoot = CreateStageEdgeFixtures(parent, edgeFixtureMaterial, edgeBulbMaterial);
        SetStageEdgeFixturesVisible(stageEdgeFixturesVisible);

        CreateStageMonitor(parent, "FightClubLeftFloorMonitor", new Vector3(-2.35f, FloorY + 0.025f, -3.42f), 1.20f, 0.58f, 0.34f, Quaternion.Euler(0f, 7f, 0f), monitorMaterial);
        CreateStageMonitor(parent, "FightClubRightFloorMonitor", new Vector3(2.35f, FloorY + 0.025f, -3.42f), 1.20f, 0.58f, 0.34f, Quaternion.Euler(0f, -7f, 0f), monitorMaterial);
    }

    private void CreateCharacters(Transform parent)
    {
        leftCharacter = CreateCharacterQuad(parent, "FightClubLeftCharacter", LeftCharacterBaseX, out leftCharacterRenderer, out leftCharacterMaterial);
        rightCharacter = CreateCharacterQuad(parent, "FightClubRightCharacter", RightCharacterBaseX, out rightCharacterRenderer, out rightCharacterMaterial);
        leftShadowCaster = CreateCharacterShadowCaster(parent, "FightClubLeftShadowCaster", LeftCharacterBaseX, out leftShadowCasterRenderer, out leftShadowCasterMaterial);
        rightShadowCaster = CreateCharacterShadowCaster(parent, "FightClubRightShadowCaster", RightCharacterBaseX, out rightShadowCasterRenderer, out rightShadowCasterMaterial);
        leftCharacter.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        rightCharacter.localScale = new Vector3(-CharacterWidth, CharacterHeight, 1f);
        leftShadowCaster.localScale = leftCharacter.localScale;
        rightShadowCaster.localScale = rightCharacter.localScale;
    }

    private void CreateStageLighting(Transform parent)
    {
        CreateStageSpotLight(parent, LeftSpotlightName, new Vector3(-8.15f, 3.95f, -6.10f), new Vector3(LeftCharacterBaseX + 0.20f, FloorY + 0.03f, 0.82f), LeftSpotColor, StageSpotIntensity, StageSpotRange, StageSpotAngle, castShadows: true);
        CreateStageSpotLight(parent, RightSpotlightName, new Vector3(8.15f, 3.95f, -6.10f), new Vector3(RightCharacterBaseX - 0.20f, FloorY + 0.03f, 0.82f), RightSpotColor, StageSpotIntensity, StageSpotRange, StageSpotAngle, castShadows: true);
    }

    private static void CreateStageSpotLight(Transform parent, string name, Vector3 localPosition, Vector3 target, Color color, float intensity, float range, float spotAngle, bool castShadows)
    {
        GameObject lightObject = new GameObject(name);
        lightObject.hideFlags = HideFlags.DontSave;
        lightObject.layer = StageUnityLayer;
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition;
        Vector3 direction = target - localPosition;
        if (direction.sqrMagnitude > 0.0001f)
            lightObject.transform.localRotation = Quaternion.LookRotation(direction.normalized, Vector3.up);

        Light light = lightObject.AddComponent<Light>();
        ConfigureStageSpotLight(light, color, intensity, range, spotAngle, castShadows);
    }

    private static void ConfigureStageSpotLight(Light light, Color color, float intensity, float range, float spotAngle, bool castShadows)
    {
        if (light == null)
            return;

        light.type = LightType.Spot;
        light.color = color;
        light.intensity = intensity;
        light.range = range;
        light.spotAngle = spotAngle;
        light.innerSpotAngle = Mathf.Min(StageSpotInnerAngle, spotAngle);
        light.shadows = castShadows ? LightShadows.Soft : LightShadows.None;
        light.shadowStrength = castShadows ? 1.0f : 0f;
        light.shadowBias = 0.0012f;
        light.shadowNormalBias = 0.0028f;
        light.renderMode = LightRenderMode.ForcePixel;
        light.cullingMask = StageUnityLayerMask;
        ConfigureStageLightRenderLayers(light);
    }

    private static Renderer CreateStageMonitor(Transform parent, string name, Vector3 localPosition, float width, float depth, float height, Quaternion localRotation, Material material)
    {
        GameObject monitor = new GameObject(name);
        monitor.hideFlags = HideFlags.DontSave;
        monitor.layer = StageUnityLayer;
        monitor.transform.SetParent(parent, false);
        monitor.transform.localPosition = localPosition;
        monitor.transform.localRotation = localRotation;

        float halfWidth = width * 0.5f;
        float halfDepth = depth * 0.5f;
        float lowFront = height * 0.20f;
        var mesh = new Mesh
        {
            name = $"{name}Mesh",
            hideFlags = HideFlags.DontSave
        };
        mesh.vertices = new[]
        {
            new Vector3(-halfWidth, 0f, -halfDepth),
            new Vector3(halfWidth, 0f, -halfDepth),
            new Vector3(-halfWidth, 0f, halfDepth),
            new Vector3(halfWidth, 0f, halfDepth),
            new Vector3(-halfWidth, lowFront, -halfDepth),
            new Vector3(halfWidth, lowFront, -halfDepth),
            new Vector3(-halfWidth, height, halfDepth),
            new Vector3(halfWidth, height, halfDepth)
        };
        mesh.triangles = new[]
        {
            0, 2, 1, 1, 2, 3,
            4, 5, 6, 5, 7, 6,
            0, 1, 4, 1, 5, 4,
            2, 6, 3, 3, 6, 7,
            0, 4, 2, 2, 4, 6,
            1, 3, 5, 3, 7, 5
        };
        mesh.RecalculateNormals();
        monitor.AddComponent<MeshFilter>().sharedMesh = mesh;
        Renderer renderer = monitor.AddComponent<MeshRenderer>();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.Off, receiveShadows: true);
        return renderer;
    }

    private static void CreateStagePlatformEdges(Transform parent, Material platformMaterial)
    {
        const float floorWidth = 18.20f;
        const float floorDepth = 19.20f;
        const float floorCenterZ = 0.15f;
        const float halfFloorWidth = floorWidth * 0.5f;
        const float halfFloorDepth = floorDepth * 0.5f;
        const float wallHeight = 0.36f;
        const float wallThickness = 0.24f;
        const float capHeight = 0.055f;

        float leftX = -halfFloorWidth - (wallThickness * 0.5f);
        float rightX = halfFloorWidth + (wallThickness * 0.5f);
        float frontZ = floorCenterZ - halfFloorDepth - (wallThickness * 0.5f);
        float backZ = floorCenterZ + halfFloorDepth + (wallThickness * 0.5f);
        float sideLength = floorDepth + (wallThickness * 2f);
        float frontBackWidth = floorWidth + (wallThickness * 2f);
        float wallY = FloorY + (wallHeight * 0.5f);
        float capY = FloorY + wallHeight + (capHeight * 0.5f);

        CreateStageBox(parent, "FightClubPlatformLeftWall", new Vector3(leftX, wallY, floorCenterZ), new Vector3(wallThickness, wallHeight, sideLength), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformRightWall", new Vector3(rightX, wallY, floorCenterZ), new Vector3(wallThickness, wallHeight, sideLength), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformFrontWall", new Vector3(0f, wallY, frontZ), new Vector3(frontBackWidth, wallHeight, wallThickness), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformBackWall", new Vector3(0f, wallY, backZ), new Vector3(frontBackWidth, wallHeight, wallThickness), Quaternion.identity, platformMaterial, receiveShadows: true);

        CreateStageBox(parent, "FightClubPlatformLeftCap", new Vector3(leftX, capY, floorCenterZ), new Vector3(wallThickness * 1.12f, capHeight, sideLength), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformRightCap", new Vector3(rightX, capY, floorCenterZ), new Vector3(wallThickness * 1.12f, capHeight, sideLength), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformFrontCap", new Vector3(0f, capY, frontZ), new Vector3(frontBackWidth, capHeight, wallThickness * 1.12f), Quaternion.identity, platformMaterial, receiveShadows: true);
        CreateStageBox(parent, "FightClubPlatformBackCap", new Vector3(0f, capY, backZ), new Vector3(frontBackWidth, capHeight, wallThickness * 1.12f), Quaternion.identity, platformMaterial, receiveShadows: true);
    }

    private static Transform CreateStageEdgeFixtures(Transform parent, Material fixtureMaterial, Material bulbMaterial)
    {
        GameObject group = new GameObject(StageEdgeFixturesRootName);
        group.hideFlags = HideFlags.DontSave;
        group.layer = StageUnityLayer;
        group.transform.SetParent(parent, false);

        const float floorWidth = 18.20f;
        const float floorDepth = 19.20f;
        const float floorCenterZ = 0.15f;
        const float halfFloorWidth = floorWidth * 0.5f;
        const float halfFloorDepth = floorDepth * 0.5f;
        const float wallHeight = 0.36f;
        const float wallThickness = 0.24f;
        const float capHeight = 0.055f;
        const float fixtureY = FloorY + wallHeight + capHeight + 0.010f;

        float leftX = -halfFloorWidth - (wallThickness * 0.5f);
        float rightX = halfFloorWidth + (wallThickness * 0.5f);
        float backZ = floorCenterZ + halfFloorDepth + (wallThickness * 0.5f);
        float insetX = wallThickness * 0.10f;
        float insetZ = wallThickness * 0.10f;
        float[] backX = { -5.9f, 0f, 5.9f };
        float[] sideZ = { -6.4f, 0f, 6.4f };
        Vector3 horizontalFixtureScale = new Vector3(0.34f, 0.014f, 0.145f);
        Vector3 horizontalLensScale = new Vector3(0.205f, 0.010f, 0.075f);
        Vector3 verticalFixtureScale = new Vector3(0.145f, 0.014f, 0.34f);
        Vector3 verticalLensScale = new Vector3(0.075f, 0.010f, 0.205f);

        for (int i = 0; i < backX.Length; i++)
        {
            float x = backX[i];
            CreateStageEdgeFixture(group.transform, $"FightClubBackEdgeFixture{i + 1}", new Vector3(x, fixtureY, backZ - insetZ), horizontalFixtureScale, horizontalLensScale, fixtureMaterial, bulbMaterial);
        }

        for (int i = 0; i < sideZ.Length; i++)
        {
            float z = floorCenterZ + sideZ[i];
            CreateStageEdgeFixture(group.transform, $"FightClubLeftEdgeFixture{i + 1}", new Vector3(leftX + insetX, fixtureY, z), verticalFixtureScale, verticalLensScale, fixtureMaterial, bulbMaterial);
            CreateStageEdgeFixture(group.transform, $"FightClubRightEdgeFixture{i + 1}", new Vector3(rightX - insetX, fixtureY, z), verticalFixtureScale, verticalLensScale, fixtureMaterial, bulbMaterial);
        }

        return group.transform;
    }

    private static void CreateStageEdgeFixture(Transform parent, string name, Vector3 localPosition, Vector3 fixtureScale, Vector3 lensScale, Material fixtureMaterial, Material bulbMaterial)
    {
        CreateStageCylinder(parent, $"{name}Bezel", localPosition, fixtureScale, fixtureMaterial, receiveShadows: true);
        CreateStageCylinder(parent, $"{name}Lens", localPosition + new Vector3(0f, 0.012f, 0f), lensScale, bulbMaterial, receiveShadows: false);

        GameObject lightObject = new GameObject($"{name}Light");
        lightObject.hideFlags = HideFlags.DontSave;
        lightObject.layer = StageUnityLayer;
        lightObject.transform.SetParent(parent, false);
        lightObject.transform.localPosition = localPosition + new Vector3(0f, 0.075f, 0f);

        Light light = lightObject.AddComponent<Light>();
        light.type = LightType.Point;
        light.color = StageEdgeBulbColor;
        light.intensity = StageEdgeFixtureLightIntensity;
        light.range = 0.95f;
        light.shadows = LightShadows.None;
        light.renderMode = LightRenderMode.Auto;
        light.cullingMask = StageUnityLayerMask;
        ConfigureStageLightRenderLayers(light);
    }

    private static Renderer CreateStageCylinder(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Material material, bool receiveShadows)
    {
        GameObject cylinder = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
        cylinder.name = name;
        cylinder.hideFlags = HideFlags.DontSave;
        cylinder.layer = StageUnityLayer;
        cylinder.transform.SetParent(parent, false);
        cylinder.transform.localPosition = localPosition;
        cylinder.transform.localRotation = Quaternion.identity;
        cylinder.transform.localScale = localScale;

        Renderer renderer = cylinder.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.Off, receiveShadows);
        Object.Destroy(cylinder.GetComponent<Collider>());
        return renderer;
    }

    private static Renderer CreateStageBox(Transform parent, string name, Vector3 localPosition, Vector3 localScale, Quaternion localRotation, Material material, bool receiveShadows)
    {
        GameObject box = GameObject.CreatePrimitive(PrimitiveType.Cube);
        box.name = name;
        box.hideFlags = HideFlags.DontSave;
        box.layer = StageUnityLayer;
        box.transform.SetParent(parent, false);
        box.transform.localPosition = localPosition;
        box.transform.localRotation = localRotation;
        box.transform.localScale = localScale;

        Renderer renderer = box.GetComponent<Renderer>();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.Off, receiveShadows);
        Object.Destroy(box.GetComponent<Collider>());
        return renderer;
    }

    private Transform CreateCharacterQuad(Transform parent, string name, float localX, out Renderer renderer, out Material material)
    {
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Quad);
        character.name = name;
        character.transform.SetParent(parent, false);
        character.transform.localPosition = new Vector3(localX, FloorY + (CharacterHeight * 0.50f) - 0.14f, -0.03f);
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        renderer = character.GetComponent<Renderer>();
        material = CreateCharacterMaterial();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.Off, receiveShadows: false);
        Object.Destroy(character.GetComponent<Collider>());
        return character.transform;
    }

    private Transform CreateCharacterShadowCaster(Transform parent, string name, float localX, out Renderer renderer, out Material material)
    {
        GameObject character = GameObject.CreatePrimitive(PrimitiveType.Quad);
        character.name = name;
        character.transform.SetParent(parent, false);
        character.transform.localPosition = new Vector3(localX, FloorY + (CharacterHeight * 0.50f) - 0.14f, -0.032f);
        character.transform.localRotation = Quaternion.identity;
        character.transform.localScale = new Vector3(CharacterWidth, CharacterHeight, 1f);
        renderer = character.GetComponent<Renderer>();
        material = CreateCharacterShadowCasterMaterial();
        renderer.sharedMaterial = material;
        ConfigureRenderer(renderer, ShadowCastingMode.ShadowsOnly, receiveShadows: false);
        Object.Destroy(character.GetComponent<Collider>());
        return character.transform;
    }

    private void PositionRoot()
    {
        Camera camera = Camera.main;
        if (camera == null || root == null)
            return;

        Transform cameraTransform = camera.transform;
        root.transform.position = cameraTransform.position + (cameraTransform.forward * StageDistance) + (cameraTransform.up * StageYOffset);
        root.transform.rotation = cameraTransform.rotation;
        root.transform.localScale = Vector3.one;
    }

    private void UpdateTriggers(FightClubMiniGameSnapshot snapshot)
    {
        if (snapshot.round != lastRound)
        {
            lastRound = snapshot.round;
            for (int i = 0; i < lastChordStatuses.Length; i++)
                lastChordStatuses[i] = 0;
            lastActiveChordIndex = -2;
            lastOpponentActiveChordIndex = -2;
        }

        float now = Time.unscaledTime;
        lastActiveChordIndex = snapshot.activeChordIndex;

        if (snapshot.opponentActiveChordIndex >= 0 && snapshot.opponentActiveChordIndex != lastOpponentActiveChordIndex)
            TriggerOpponentAction(snapshot.opponentActiveChordIndex, now);
        lastOpponentActiveChordIndex = snapshot.opponentActiveChordIndex;

        if (snapshot.opponentPreviewActive)
            return;

        List<FightClubChordSnapshot> chords = snapshot.chords ?? new List<FightClubChordSnapshot>();
        for (int i = 0; i < lastChordStatuses.Length; i++)
        {
            int status = i < chords.Count ? Mathf.Clamp(chords[i]?.status ?? 0, 0, 2) : 0;
            if (status == lastChordStatuses[i])
                continue;

            if (status == 1)
                TriggerAction(i, now);
            else if (status == 2)
                TriggerMiss(i, now);
            lastChordStatuses[i] = status;
        }
    }

    private void UpdateCharacterPose(FightClubMiniGameSnapshot snapshot)
    {
        float now = Time.unscaledTime;
        bool missActive = now < missUntil;
        bool actionActive = !missActive && now < actionUntil;
        bool opponentActionActive = now < opponentActionUntil;
        float idleBreath = (Mathf.Sin(now * 2.0f) + 1f) * 0.5f;
        float idleSway = Mathf.Sin(now * 1.35f);
        Texture2D leftTexture = idleSheetTexture;
        Texture2D rightTexture = idleSheetTexture;
        Vector2 leftScale = GetIdleUvScale();
        Vector2 rightScale = leftScale;
        Vector2 leftOffset = GetIdleUvOffset(now, 0f);
        Vector2 rightOffset = GetIdleUvOffset(now, 0.18f);
        Color leftTint = HitTint;
        Color rightTint = HitTint;
        float poseHeight = GetPoseHeight(leftTexture);
        float poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
        float scaledPoseHeight = poseHeight * (1f + (idleBreath * 0.018f));
        float leftX = LeftCharacterBaseX;
        float rightX = RightCharacterBaseX;
        float leftY = GetPoseCenterY(leftTexture, scaledPoseHeight);
        float rightY = leftY;
        float leftRot = idleSway * 1.1f;
        float rightRot = -leftRot;
        float leftScaleX = poseWidth * (1f - (idleBreath * 0.012f));
        float rightScaleX = -leftScaleX;
        float leftScaleY = scaledPoseHeight;
        float rightScaleY = scaledPoseHeight;

        if (missActive)
        {
            float t = Mathf.Clamp01((now - missStartedAt) / MissHoldSeconds);
            float hitPulse = Mathf.Sin(Mathf.Clamp01(t / 0.38f) * Mathf.PI);
            float settle = Mathf.SmoothStep(1f, 0f, Mathf.Clamp01(t / 0.58f));
            float recoil = -0.24f * hitPulse;
            float shake = Mathf.Sin(t * 28f) * 0.035f * settle;
            leftTexture = idlePoseTexture != null ? idlePoseTexture : idleSheetTexture;
            bool useIdlePose = idlePoseTexture != null;
            leftScale = useIdlePose ? Vector2.one : GetIdleUvScale();
            leftOffset = useIdlePose ? Vector2.zero : GetIdleUvOffset(now, 0f);
            leftTint = Color.Lerp(MissTint, MissPulseTint, hitPulse * 0.28f);
            rightTint = HitTint;
            poseHeight = GetPoseHeight(leftTexture);
            poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
            leftScaleX = poseWidth * (1f + (hitPulse * 0.075f));
            leftScaleY = poseHeight * (1f - (hitPulse * 0.028f));
            leftX = LeftCharacterBaseX + recoil + shake;
            leftY = GetPoseCenterY(leftTexture, leftScaleY);
            leftRot = 0f;
        }
        else if (actionActive)
        {
            float t = Mathf.Clamp01((now - actionStartedAt) / ActionHoldSeconds);
            float power = Mathf.Sin(t * Mathf.PI);
            bool useJump = (lastActionChordIndex & 1) == 0;
            Texture2D actionTexture = useJump ? actionJumpTexture : actionHeadbangTexture;
            if (actionTexture != null)
            {
                leftTexture = actionTexture;
                leftScale = Vector2.one;
                leftOffset = Vector2.zero;
            }

            poseHeight = GetPoseHeight(leftTexture);
            poseWidth = GetPoseWidth(leftTexture, leftScale, poseHeight);
            leftScaleY = poseHeight * (1f + (power * 0.055f));
            leftX = LeftCharacterBaseX + (0.64f * power);
            leftY = GetPoseCenterY(leftTexture, leftScaleY) + (0.34f * power);
            leftRot = useJump ? -5.0f * power : 4.0f * power;
            leftScaleX = poseWidth * (1f + (power * 0.10f));
        }

        if (opponentActionActive)
        {
            float t = Mathf.Clamp01((now - opponentActionStartedAt) / ActionHoldSeconds);
            float power = Mathf.Sin(t * Mathf.PI);
            bool useJump = (lastOpponentActionChordIndex & 1) == 0;
            Texture2D actionTexture = useJump ? actionJumpTexture : actionHeadbangTexture;
            if (actionTexture != null)
            {
                rightTexture = actionTexture;
                rightScale = Vector2.one;
                rightOffset = Vector2.zero;
            }

            float rightPoseHeight = GetPoseHeight(rightTexture);
            float rightPoseWidth = GetPoseWidth(rightTexture, rightScale, rightPoseHeight);
            rightScaleY = rightPoseHeight * (1f + (power * 0.055f));
            rightX = RightCharacterBaseX - (0.64f * power);
            rightY = GetPoseCenterY(rightTexture, rightScaleY) + (0.34f * power);
            rightRot = useJump ? 5.0f * power : -4.0f * power;
            rightScaleX = -rightPoseWidth * (1f + (power * 0.10f));
        }

        ApplyCharacter(leftCharacter, leftCharacterMaterial, leftTexture, leftScale, leftOffset, leftTint, leftX, leftY, leftScaleX, leftScaleY, leftRot);
        ApplyCharacter(rightCharacter, rightCharacterMaterial, rightTexture, rightScale, rightOffset, rightTint, rightX, rightY, rightScaleX, rightScaleY, rightRot);
        ApplyCharacter(leftShadowCaster, leftShadowCasterMaterial, leftTexture, leftScale, leftOffset, Color.white, leftX, leftY, leftScaleX, leftScaleY, leftRot);
        ApplyCharacter(rightShadowCaster, rightShadowCasterMaterial, rightTexture, rightScale, rightOffset, Color.white, rightX, rightY, rightScaleX, rightScaleY, rightRot);
    }

    private void ApplyCharacter(
        Transform character,
        Material material,
        Texture2D texture,
        Vector2 textureScale,
        Vector2 textureOffset,
        Color tint,
        float x,
        float y,
        float scaleX,
        float scaleY,
        float rotationZ)
    {
        if (character == null || material == null)
            return;

        if (texture != null)
        {
            SetMaterialTexture(material, texture);
            SetMaterialTextureScale(material, textureScale);
            SetMaterialTextureOffset(material, textureOffset);
        }

        SetMaterialColor(material, tint);
        character.localPosition = new Vector3(x, y, -0.03f);
        character.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        character.localScale = new Vector3(scaleX, scaleY, 1f);
    }

    private void TriggerAction(int chordIndex, float now)
    {
        lastActionChordIndex = Mathf.Max(0, chordIndex);
        actionStartedAt = now;
        actionUntil = now + ActionHoldSeconds;
    }

    private void TriggerOpponentAction(int chordIndex, float now)
    {
        lastOpponentActionChordIndex = Mathf.Max(0, chordIndex);
        opponentActionStartedAt = now;
        opponentActionUntil = now + ActionHoldSeconds;
    }

    private void TriggerMiss(int chordIndex, float now)
    {
        lastActionChordIndex = Mathf.Max(0, chordIndex);
        missStartedAt = now;
        missUntil = now + MissHoldSeconds;
    }

    private void ResetState()
    {
        lastRound = -1;
        lastActiveChordIndex = -2;
        lastOpponentActiveChordIndex = -2;
        for (int i = 0; i < lastChordStatuses.Length; i++)
            lastChordStatuses[i] = -1;
        actionUntil = -999f;
        opponentActionUntil = -999f;
        missUntil = -999f;
    }

    private void EnsureTextures()
    {
        if (idleSheetTexture != null)
            return;

        idleSheetTexture = LoadFightClubTexture(FightClubIdleSheetFileName);
        actionJumpTexture = LoadFightClubTexture(FightClubActionJumpFileName);
        actionHeadbangTexture = LoadFightClubTexture(FightClubActionHeadbangFileName);
        idlePoseTexture = LoadFightClubTexture(FightClubIdlePoseFileName);
    }

    private static Vector2 GetIdleUvScale()
    {
        return new Vector2(1f / IdleFrameCount, 1f);
    }

    private static Vector2 GetIdleUvOffset(float now, float phaseOffset)
    {
        int frame = Mathf.FloorToInt((now + phaseOffset) * IdleFps) % IdleFrameCount;
        if (frame < 0)
            frame = 0;
        return new Vector2(frame / (float)IdleFrameCount, 0f);
    }

    private static float GetPoseHeight(Texture2D texture)
    {
        float referenceVisibleHeight = CharacterHeight * (ReferenceVisiblePixelHeight / IdleFramePixelHeight);
        float frameHeight = GetFramePixelHeight(texture);
        float visibleHeight = Mathf.Max(1f, GetVisiblePixelHeight(texture));
        return referenceVisibleHeight * (frameHeight / visibleHeight);
    }

    private static float GetPoseWidth(Texture2D texture, Vector2 textureScale, float poseHeight)
    {
        if (texture == null || texture.height <= 0)
            return CharacterWidth;

        float frameWidth = texture.width * Mathf.Max(0.0001f, Mathf.Abs(textureScale.x));
        float frameHeight = texture.height * Mathf.Max(0.0001f, Mathf.Abs(textureScale.y));
        return poseHeight * (frameWidth / frameHeight);
    }

    private static float GetPoseCenterY(Texture2D texture, float poseHeight)
    {
        float frameHeight = Mathf.Max(1f, GetFramePixelHeight(texture));
        float bottomPadding = GetBottomPaddingPixels(texture);
        return FloorY + (poseHeight * 0.5f) - ((bottomPadding / frameHeight) * poseHeight) + CharacterFootSink;
    }

    private static float GetFramePixelHeight(Texture2D texture)
    {
        if (texture == null)
            return IdleFramePixelHeight;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("NEW_Elize_Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            return 128f;
        if (name.IndexOf("Jumping", StringComparison.OrdinalIgnoreCase) >= 0)
            return 144f;
        return IdleFramePixelHeight;
    }

    private static float GetVisiblePixelHeight(Texture2D texture)
    {
        if (texture == null)
            return ReferenceVisiblePixelHeight;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0)
            return 126f;
        return ReferenceVisiblePixelHeight;
    }

    private static float GetBottomPaddingPixels(Texture2D texture)
    {
        if (texture == null)
            return 16f;

        string name = texture.name ?? string.Empty;
        if (name.IndexOf("Headbanging", StringComparison.OrdinalIgnoreCase) >= 0 ||
            name.IndexOf("NEW_Elize_Idle", StringComparison.OrdinalIgnoreCase) >= 0)
            return 0f;
        return 16f;
    }

    private static Texture2D LoadFightClubTexture(string fileName)
    {
        string resourcePath = $"{FightClubAssetDirectory}/{System.IO.Path.GetFileNameWithoutExtension(fileName)}";
        Texture2D resourceTexture = Resources.Load<Texture2D>(resourcePath);
        if (resourceTexture != null)
        {
            HighwayCharacterVisualUtility.ApplyRuntimeTextureSettings(resourceTexture);
            return resourceTexture;
        }

        string path = ResolveFightClubTextureFilePath(fileName);
        if (string.IsNullOrEmpty(path))
            return null;

        try
        {
            byte[] bytes = System.IO.File.ReadAllBytes(path);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = System.IO.Path.GetFileNameWithoutExtension(fileName),
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Clamp,
                anisoLevel = 0
            };
            if (!texture.LoadImage(bytes, false))
                return null;
            HighwayCharacterVisualUtility.ApplyRuntimeTextureSettings(texture);
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[MiniGameFightStage3DRenderer] Failed to load Fight Club texture '{path}': {ex.Message}");
            return null;
        }
    }

    private static string ResolveFightClubTextureFilePath(string fileName)
    {
        string[] paths =
        {
            System.IO.Path.Combine(Application.dataPath, "MiniGames", "Resources", FightClubAssetDirectory, fileName),
            System.IO.Path.Combine(Application.dataPath, FightClubAssetDirectory, fileName)
        };

        for (int i = 0; i < paths.Length; i++)
        {
            if (System.IO.File.Exists(paths[i]))
                return paths[i];
        }

        return null;
    }

    private static Texture2D GetFloorAlbedoTexture()
    {
        if (floorAlbedoTexture != null && floorAlbedoTexture.name == FloorAlbedoTextureName)
            return floorAlbedoTexture;

        const int width = 96;
        const int height = 96;
        var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = FloorAlbedoTextureName,
            hideFlags = HideFlags.DontSave,
            filterMode = FilterMode.Bilinear,
            wrapMode = TextureWrapMode.Clamp,
            anisoLevel = 2
        };

        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / (float)(height - 1);
            float depth = Mathf.SmoothStep(0f, 1f, v);
            float midWeight = Mathf.Sin(v * Mathf.PI) * 0.30f;

            for (int x = 0; x < width; x++)
            {
                float u = x / (float)(width - 1);
                float center = 1f - Mathf.Abs((u * 2f) - 1f);
                float sideVignette = Mathf.Pow(1f - center, 1.65f) * 0.13f;
                float violetBalance = Mathf.SmoothStep(0.16f, 0.88f, v) * Mathf.SmoothStep(0.04f, 1f, center) * 0.12f;
                float fineGrain = (Hash01(x, y) - 0.5f) * 0.012f;
                float satinStreak = Mathf.Sin((x * 0.24f) + (y * 0.075f)) * 0.004f;

                Color color = Color.Lerp(FloorFrontColor, FloorBackColor, depth);
                color = Color.Lerp(color, FloorMidColor, midWeight);
                color = Color.Lerp(color, FloorVioletBalanceColor, violetBalance);

                float value = 1f + fineGrain + satinStreak - sideVignette;
                color.r = Mathf.Clamp01(color.r * value);
                color.g = Mathf.Clamp01(color.g * value);
                color.b = Mathf.Clamp01(color.b * value);
                color.a = 1f;
                pixels[(y * width) + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
        floorAlbedoTexture = texture;
        return floorAlbedoTexture;
    }

    private static float Hash01(int x, int y)
    {
        uint hash = ((uint)x * 374761393u) + ((uint)y * 668265263u);
        hash = (hash ^ (hash >> 13)) * 1274126177u;
        return (hash & 0x00FFFFFFu) / 16777215f;
    }

    private static Material CreateFloorMaterial()
    {
        Material material = CreateLitMaterial();
        ConfigureFloorMaterial(material);
        return material;
    }

    private static void ConfigureFloorMaterial(Material material)
    {
        if (material == null)
            return;

        SetMaterialTexture(material, GetFloorAlbedoTexture());
        SetMaterialTextureScale(material, Vector2.one);
        SetMaterialTextureOffset(material, Vector2.zero);
        SetMaterialColor(material, FloorColor);
        SetMaterialEmission(material, FloorEmissionColor);
        SetMaterialSmoothness(material, 0.88f);
        SetMaterialSpecular(material, FloorSpecularColor);
        SetMaterialClearCoat(material, 0.32f, 0.92f);
        ConfigureOpaqueMaterial(material, (int)RenderQueue.Geometry + 10);
    }

    private static Material CreateStageColorMaterial(Color color, int renderQueue)
    {
        Material material = CreateLitMaterial();
        ConfigureOpaqueMaterial(material, renderQueue);
        SetMaterialColor(material, color);
        SetMaterialSmoothness(material, 0.58f);
        SetMaterialSpecular(material, new Color(0.16f, 0.20f, 0.28f, 1f));
        return material;
    }

    private static Material CreateStageEdgeFixtureMaterial()
    {
        Material material = CreateStageColorMaterial(StageEdgeFixtureColor, (int)RenderQueue.Geometry + 34);
        SetMaterialSmoothness(material, 0.66f);
        SetMaterialSpecular(material, new Color(0.26f, 0.30f, 0.36f, 1f));
        return material;
    }

    private static Material CreateStageEdgeBulbMaterial()
    {
        Material material = CreateStageColorMaterial(StageEdgeBulbColor, (int)RenderQueue.Geometry + 35);
        SetMaterialEmission(material, StageEdgeBulbEmissionColor);
        SetMaterialSmoothness(material, 0.72f);
        SetMaterialSpecular(material, new Color(0.72f, 0.82f, 0.92f, 1f));
        return material;
    }

    private static Material CreateCharacterMaterial()
    {
        Material material = CreateSpriteTransparentMaterial((int)RenderQueue.Transparent + 64);
        SetMaterialColor(material, HitTint);
        return material;
    }

    private static Material CreateCharacterShadowCasterMaterial()
    {
        Material material = CreateCharacterCutoutLitMaterial();
        if (material == null)
            material = CreateCharacterAlphaShadowCasterMaterial();
        SetMaterialColor(material, Color.white);
        SetMaterialSmoothness(material, 0.12f);
        DisableExtraLitResponse(material);
        return material;
    }

    private static Material CreateMaterial(bool transparent)
    {
        Shader shader = transparent ? Shader.Find("Unlit/Transparent") : Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = transparent ? Shader.Find("Sprites/Default") : Shader.Find("Unlit/Color");
        if (shader == null)
            shader = transparent ? Shader.Find("Universal Render Pipeline/Unlit") : Shader.Find("Standard");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        if (transparent)
        {
            material.SetOverrideTag("RenderType", "Transparent");
            if (material.HasProperty("_Surface"))
                material.SetFloat("_Surface", 1f);
            if (material.HasProperty("_Blend"))
                material.SetFloat("_Blend", 0f);
            if (material.HasProperty("_AlphaClip"))
                material.SetFloat("_AlphaClip", 0f);
            if (material.HasProperty("_SrcBlend"))
                material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend"))
                material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            material.EnableKeyword("_ALPHABLEND_ON");
            material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            material.DisableKeyword("_ALPHATEST_ON");
        }

        return material;
    }

    private static Material CreateSpriteTransparentMaterial(int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTexturedTransparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.SetOverrideTag("RenderType", "Transparent");
        ConfigureMaterial(material, renderQueue);
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 1f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.DisableKeyword("_ALPHATEST_ON");
        return material;
    }

    private static Material CreateCharacterAlphaShadowCasterMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/FightClubSpriteShadowCaster");
        if (shader == null)
            return null;

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.renderQueue = (int)RenderQueue.AlphaTest + 20;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        return material;
    }

    private static Material CreateCharacterCutoutLitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return CreateSpriteTransparentMaterial((int)RenderQueue.AlphaTest + 20);

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        material.renderQueue = (int)RenderQueue.AlphaTest + 20;
        material.SetOverrideTag("RenderType", "TransparentCutout");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_Blend"))
            material.SetFloat("_Blend", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 1f);
        if (material.HasProperty("_Cutoff"))
            material.SetFloat("_Cutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_AlphaCutoff"))
            material.SetFloat("_AlphaCutoff", CharacterAlphaCutoff);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        material.DisableKeyword("_ALPHABLEND_ON");
        material.DisableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
        material.EnableKeyword("_ALPHATEST_ON");
        return material;
    }

    private static Material CreateLitMaterial()
    {
        Shader shader = Shader.Find("Universal Render Pipeline/Lit");
        if (shader == null)
            shader = Shader.Find("Standard");
        if (shader == null)
            return CreateMaterial(transparent: false);

        Material material = new Material(shader)
        {
            hideFlags = HideFlags.DontSave
        };

        return material;
    }

    private static void ConfigureMaterial(Material material, int renderQueue)
    {
        if (material == null)
            return;

        material.renderQueue = renderQueue;
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 0f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
    }

    private static void ConfigureOpaqueMaterial(Material material, int renderQueue)
    {
        if (material == null)
            return;

        material.renderQueue = renderQueue;
        material.SetOverrideTag("RenderType", "Opaque");
        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
        if (material.HasProperty("_ZWrite"))
            material.SetFloat("_ZWrite", 1f);
        if (material.HasProperty("_Cull"))
            material.SetFloat("_Cull", (float)CullMode.Off);
        if (material.HasProperty("_CullMode"))
            material.SetFloat("_CullMode", (float)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetFloat("_ZTest", (float)CompareFunction.LessEqual);
        if (material.HasProperty("_ZTestMode"))
            material.SetFloat("_ZTestMode", (float)CompareFunction.LessEqual);
    }

    private static void ConfigureRenderer(Renderer renderer, ShadowCastingMode shadowCastingMode = ShadowCastingMode.Off, bool receiveShadows = false)
    {
        if (renderer == null)
            return;

        renderer.gameObject.layer = StageUnityLayer;
        renderer.shadowCastingMode = shadowCastingMode;
        renderer.receiveShadows = receiveShadows;
        renderer.renderingLayerMask = uint.MaxValue;
        renderer.lightProbeUsage = LightProbeUsage.Off;
        renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
    }

    private static void ConfigureStageLightRenderLayers(Light light)
    {
        if (light == null)
            return;

        UniversalAdditionalLightData lightData = light.GetUniversalAdditionalLightData();
        lightData.customShadowLayers = false;
        lightData.renderingLayers = uint.MaxValue;
        light.renderingLayerMask = -1;
    }

    private void SetVisible(bool visible)
    {
        if (root == null)
            return;

        if (root.activeSelf != visible)
            root.SetActive(visible);
    }

    private void DestroyExistingRootOnce()
    {
        if (staleRootScanDone)
            return;

        DestroyExistingRoot();
        staleRootScanDone = true;
    }

    private static void DestroyExistingRoot()
    {
        GameObject existing = GameObject.Find(RootName);
        if (existing != null)
            Object.Destroy(existing);
    }

    private static void DestroyExistingStageCamera()
    {
        GameObject existing = GameObject.Find(StageCameraName);
        if (existing != null)
            Object.Destroy(existing);
    }

    private void DestroyExistingStageCameraOnce()
    {
        if (staleStageCameraCleanupDone)
            return;

        DestroyExistingStageCamera();
        staleStageCameraCleanupDone = true;
    }

    private static void SetMaterialTexture(Material material, Texture texture)
    {
        if (material == null || texture == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTexture("_BaseMap", texture);
        if (material.HasProperty("_MainTex"))
            material.SetTexture("_MainTex", texture);
        material.mainTexture = texture;
    }

    private static void SetMaterialTextureScale(Material material, Vector2 scale)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureScale("_BaseMap", scale);
        if (material.HasProperty("_MainTex"))
            material.SetTextureScale("_MainTex", scale);
        material.mainTextureScale = scale;
    }

    private static void SetMaterialTextureOffset(Material material, Vector2 offset)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseMap"))
            material.SetTextureOffset("_BaseMap", offset);
        if (material.HasProperty("_MainTex"))
            material.SetTextureOffset("_MainTex", offset);
        material.mainTextureOffset = offset;
    }

    private static void SetMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_TintColor"))
            material.SetColor("_TintColor", color);
        material.color = color;
    }

    private static void SetMaterialSmoothness(Material material, float smoothness)
    {
        if (material == null)
            return;

        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", smoothness);
        if (material.HasProperty("_Glossiness"))
            material.SetFloat("_Glossiness", smoothness);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

    private static void SetMaterialEmission(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", color);
        if (color.maxColorComponent > 0.0001f)
            material.EnableKeyword("_EMISSION");
        else
            material.DisableKeyword("_EMISSION");
    }

    private static void DisableExtraLitResponse(Material material)
    {
        if (material == null)
            return;

        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 0f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);
        if (material.HasProperty("_SpecColor"))
            material.SetColor("_SpecColor", Color.black);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
    }

    private static void SetMaterialSpecular(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_WorkflowMode"))
            material.SetFloat("_WorkflowMode", 0f);
        if (material.HasProperty("_SpecularHighlights"))
            material.SetFloat("_SpecularHighlights", 1f);
        if (material.HasProperty("_EnvironmentReflections"))
            material.SetFloat("_EnvironmentReflections", 0f);
        if (material.HasProperty("_SpecColor"))
            material.SetColor("_SpecColor", color);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);
        material.EnableKeyword("_SPECULAR_SETUP");
    }

    private static void SetMaterialClearCoat(Material material, float mask, float smoothness)
    {
        if (material == null)
            return;

        if (material.HasProperty("_ClearCoatMask"))
            material.SetFloat("_ClearCoatMask", mask);
        if (material.HasProperty("_ClearCoatSmoothness"))
            material.SetFloat("_ClearCoatSmoothness", smoothness);
        material.EnableKeyword("_CLEARCOAT");
    }

}
