using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

public sealed class MultiplayerRhythm3DRenderer : IGuitarGameplayRenderer
{
    private const int PlayerCount = 2;
    private const int DefaultLaneCount = 5;
    private const int MaxLaneCount = 8;
    private const float LaneBackOverhang = 8f;
    private const float LaneSurfaceY = 1.02f;
    private const float NoteY = 1.30f;
    private const float EndpointY = 1.36f;
    private const float EndpointDepth = 0.34f;
    private const float SustainActivePulseSpeed = 9.2f;
    private const float SustainActiveGlowBoost = 1.55f;
    private const float SustainActiveWidthScale = 1.12f;
    private const float SustainActiveHeightScale = 1.38f;
    private const float HitPulseDuration = 0.34f;
    private const float MissPulseDuration = 0.28f;
    private const float MaxFeedbackTriggerAge = 0.25f;
    private const float ResolvedHeadHoldSeconds = 0.24f;
    private const float CharacterDepth = 44f;
    private const float CharacterViewportMarginX = 0.015f;
    private const float CharacterViewportMarginY = 0.035f;
    private const float CharacterHeightViewportFraction = 0.365f;
    private const float CharacterViewportCenterY = 0.69f;
    private const float CharacterScaleMultiplier = 1.05f;
    private const float CharacterViewportOuterInsetX = 0.075f;
    private const float TrackHorizontalSpacingMultiplier = 0.82f;
    private const float HighwayCharacterBopGroupWindowSeconds = 0.045f;
    private const float HighwayCharacterBopMinimumSpacingSeconds = 0.16f;
    private const float HighwayCharacterBopDurationSeconds = 0.42f;
    private const float HighwayCharacterBopAttackSeconds = 0.06f;
    private const float HighwayCharacterBopLiftInCharacterHeights = 0.05f;
    private const float HighwayCharacterBopScaleYAmount = 0.045f;
    private const float HighwayCharacterBopScaleXAmount = 0.022f;
    private const float HighwayCharacterBopTiltDegrees = 4.75f;
    private const float HighwayCharacterIdleSwaySpeed = 0.82f;
    private const float HighwayCharacterIdleBreathSpeed = 1.36f;
    private const float HighwayCharacterIdleLiftInCharacterHeights = 0.012f;
    private const float HighwayCharacterIdleSwayInCharacterWidths = 0.008f;
    private const float HighwayCharacterIdleScaleYAmount = 0.016f;
    private const float HighwayCharacterIdleScaleXAmount = 0.008f;
    private const float HighwayCharacterIdleTiltDegrees = 1.65f;
    private const float HighwayCharacterMissDurationSeconds = 0.42f;
    private const float HighwayCharacterMissAttackSeconds = 0.06f;
    private const float HighwayCharacterMissDropInCharacterHeights = 0.058f;
    private const float HighwayCharacterMissSwayInCharacterWidths = 0.028f;
    private const float HighwayCharacterMissScaleXAmount = 0.06f;
    private const float HighwayCharacterMissScaleYAmount = 0.095f;
    private const float HighwayCharacterMissTiltDegrees = 8.5f;
    private const float HighwayCharacterBottomFadeStart01 = 0.22f;
    private const float HighwayCharacterBottomFadeEnd01 = 0.12f;

    private const float HighwayCharacterPortalLocalYInCharacterHeights = -0.34f;
    private const float HighwayCharacterPortalWidthInCharacterWidths = 0.96f;
    private const float HighwayCharacterPortalHeightInCharacterHeights = 0.34f;
    private const float HighwayCharacterPortalBackForwardOffset = 0.01f;
    private const float HighwayCharacterPortalFrontForwardOffset = -0.015f;
    private const float HighwayCharacterPortalSplitY01 = 0.5f;
    private const float HighwayCharacterPortalSplitSoftness01 = 0.035f;
    private const float HighwayCharacterPortalRingThickness = 0.07f;
    private const float HighwayCharacterPortalEdgeSoftness = 0.085f;
    private const float HighwayCharacterPortalRimSoftness = 0.0065f;
    private const float HighwayCharacterPortalInteriorAlphaFloor = 0.8f;
    private static readonly Color HighwayCharacterPortalBaseColor = new Color(0.07f, 0.10f, 0.19f, 1f);
    private static readonly Color HighwayCharacterPortalCoreColor = new Color(0.03f, 0.05f, 0.12f, 1f);
    private const float HighwayCharacterPortalBaseOpacity = 1f;
    private const float HighwayCharacterPortalCoreOpacity = 1f;
    private const float HighwayCharacterPortalRimOpacity = 1f;
    private const float HighwayCharacterPortalSwirlOpacity = 0.9f;
    private const float HighwayCharacterPortalGlowStrength = 1.82f;
    private const float HighwayCharacterPortalSwirlSpeed = 0.55f;
    private const float HighwayCharacterPortalSwirlSharpness = 5f;
    private const float HighwayCharacterPortalPreviewTintMix = 0.18f;
    private static readonly int CharacterPortalBaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int CharacterPortalRimColorShaderId = Shader.PropertyToID("_RimColor");
    private static readonly int CharacterPortalAccentColorShaderId = Shader.PropertyToID("_AccentColor");
    private static readonly int CharacterPortalCoreColorShaderId = Shader.PropertyToID("_CoreColor");
    private static readonly int CharacterPortalGlowStrengthShaderId = Shader.PropertyToID("_GlowStrength");
    private static readonly int CharacterPortalSwirlSpeedShaderId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int CharacterPortalSwirlSharpnessShaderId = Shader.PropertyToID("_SwirlSharpness");
    private static readonly int CharacterPortalRingThicknessShaderId = Shader.PropertyToID("_RingThickness");
    private static readonly int CharacterPortalSoftnessShaderId = Shader.PropertyToID("_Softness");
    private static readonly int CharacterPortalRimSoftnessShaderId = Shader.PropertyToID("_RimSoftness");
    private static readonly int CharacterPortalAlphaFloorShaderId = Shader.PropertyToID("_AlphaFloor");
    private static readonly int CharacterPortalHalfModeShaderId = Shader.PropertyToID("_HalfMode");
    private static readonly int CharacterPortalSplitYShaderId = Shader.PropertyToID("_SplitY");
    private static readonly int CharacterPortalSplitSoftnessShaderId = Shader.PropertyToID("_SplitSoftness");
    private static readonly int CharacterFadeStartShaderId = Shader.PropertyToID("_FadeStartY");
    private static readonly int CharacterFadeEndShaderId = Shader.PropertyToID("_FadeEndY");
    private static readonly Color BackgroundColor = new Color(0.01f, 0.015f, 0.045f, 1f);
    private static readonly Rect[] CurrentCharacterHudRects = new Rect[PlayerCount];

    private sealed class MultiplayerPlayerScene
    {
        public int playerIndex;
        public int layer;
        public GameObject root;
        public GameObject gameplayRoot;
        public Transform characterTransform;
        public Transform characterArtTransform;
        public Renderer characterRenderer;
        public Material characterMaterial;
        public Renderer characterPortalBackRenderer;
        public Renderer characterPortalFrontRenderer;
        public Material characterPortalBackMaterial;
        public Material characterPortalFrontMaterial;
        public readonly Renderer[] laneRenderers = new Renderer[MaxLaneCount];
        public readonly Material[] laneMaterials = new Material[MaxLaneCount];
        public readonly Renderer[] endpointRenderers = new Renderer[MaxLaneCount];
        public readonly Material[] endpointMaterials = new Material[MaxLaneCount];
        public readonly Dictionary<int, GameplayNoteResult> resolvedFeedbackResults = new Dictionary<int, GameplayNoteResult>();
        public readonly List<MultiplayerFeedbackEffect> feedbackEffects = new List<MultiplayerFeedbackEffect>();
        public readonly float[] lanePulseUntil = new float[MaxLaneCount];
        public readonly float[] lanePulseStrength = new float[MaxLaneCount];
        public readonly List<HighwayCharacterBopEvent> bopEvents = new List<HighwayCharacterBopEvent>();
        public int lastObservedMissCount = -1;
        public float lastMissTriggerSongTime = float.NegativeInfinity;
        public int lastBopSourceNoteCount = -1;
        public readonly Dictionary<int, MultiplayerNoteView> noteViews = new Dictionary<int, MultiplayerNoteView>();
        public readonly HashSet<int> visibleNoteIds = new HashSet<int>();
        public readonly List<int> removalBuffer = new List<int>();
    }

    private sealed class MultiplayerNoteView
    {
        public GameObject root;
        public GameObject body;
        public Renderer bodyRenderer;
        public Material bodyMaterial;
        public GameObject outline;
        public Renderer outlineRenderer;
        public Material outlineMaterial;
        public GameObject accent;
        public Renderer accentRenderer;
        public Material accentMaterial;
        public GameObject sustain;
        public Renderer sustainRenderer;
        public Material sustainMaterial;
        public Color baseColor;
    }

    private sealed class MultiplayerFeedbackEffect
    {
        public GameObject root;
        public readonly List<MultiplayerFeedbackPiece> pieces = new List<MultiplayerFeedbackPiece>();
        public float startTime;
        public float duration;
        public bool miss;
    }

    private sealed class MultiplayerFeedbackPiece
    {
        public Transform transform;
        public Material material;
        public Color color;
        public Vector3 startLocalPosition;
        public Vector3 velocity;
        public Vector3 baseScale;
        public Vector3 spin;
        public float expand;
    }

    private struct HighwayCharacterBopEvent
    {
        public float time;
        public float strength;
    }

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private CameraClearFlags originalMainCameraClearFlags;
    private Color originalMainCameraBackgroundColor;
    private int originalMainCameraCullingMask = -1;
    private float originalMainCameraDepth;
    private bool originalMainCameraOrthographic;
    private GameObject root;
    private GameObject backgroundRoot;
    private ITabsBackgroundEffect backgroundEffect;
    private MultiplayerPlayerScene[] playerScenes;
    private float currentVisualNoteSpeed = 12f;
    private TabsSongHeaderOverlay overlay;
    private Texture2D characterTexture;
    private Vector2 characterTextureScale = Vector2.one;
    private Vector2 characterTextureOffset = Vector2.zero;
    private float characterAspect = 0.79f;
    private int characterSourcePixelWidth = 1;
    private int characterSourcePixelHeight = 1;

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("MultiplayerRhythm3DRendererRoot");
        backgroundRoot = new GameObject("MultiplayerRhythmBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        overlay = new TabsSongHeaderOverlay(owner);

        if (mainCamera != null)
        {
            originalMainCameraClearFlags = mainCamera.clearFlags;
            originalMainCameraBackgroundColor = mainCamera.backgroundColor;
            originalMainCameraCullingMask = mainCamera.cullingMask;
            originalMainCameraDepth = mainCamera.depth;
            originalMainCameraOrthographic = mainCamera.orthographic;
            ConfigureMainCamera();
        }

        InitializeBackgroundEffect();
        LoadCharacterTexture();
        BuildPlayerScenes();
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        DisposeRenderer();
        Initialize(owner, chartNotes, sections);
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || playerScenes == null)
            return;

        ConfigureMainCamera();
        currentVisualNoteSpeed = GetVisualNoteSpeed(snapshot);
        bool suppressGameplay = snapshot.mainMenuFlowActive;

        UpdateBackgroundPlacement();
        backgroundEffect?.Tick(Time.deltaTime);

        for (int i = 0; i < playerScenes.Length; i++)
        {
            MultiplayerPlayerScene scene = playerScenes[i];
            if (scene == null)
                continue;

            MultiplayerRhythmPlayerSnapshot playerSnapshot = snapshot.multiplayerRhythmPlayers != null && i < snapshot.multiplayerRhythmPlayers.Count
                ? snapshot.multiplayerRhythmPlayers[i]
                : null;

            bool sceneVisible = !suppressGameplay;
            if (scene.root != null && scene.root.activeSelf != sceneVisible)
                scene.root.SetActive(sceneVisible);

            if (!sceneVisible)
            {
                CurrentCharacterHudRects[i] = GetFallbackCharacterHudScreenRect(i, Screen.width, Screen.height);
                continue;
            }

            UpdateTrackPlacement(scene);
            UpdateCharacter(scene, i == 1, playerSnapshot, snapshot);
            UpdateResolvedFeedback(scene, playerSnapshot, snapshot.songTime);
            UpdateLaneVisuals(scene, playerSnapshot, snapshot.songTime);
            UpdatePlayerNotes(scene, playerSnapshot, snapshot.songTime);
            UpdateFeedbackEffects(scene);
        }

        overlay?.UpdateFromSnapshot(snapshot);
    }

    public void DisposeRenderer()
    {
        overlay?.Dispose();
        overlay = null;

        backgroundEffect?.Dispose();
        backgroundEffect = null;

        if (mainCamera != null && originalMainCameraCullingMask >= 0)
        {
            mainCamera.clearFlags = originalMainCameraClearFlags;
            mainCamera.backgroundColor = originalMainCameraBackgroundColor;
            mainCamera.cullingMask = originalMainCameraCullingMask;
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.orthographic = originalMainCameraOrthographic;
        }

        if (root != null)
            Object.Destroy(root);

        backgroundRoot = null;
        playerScenes = null;
        characterTexture = null;
        for (int i = 0; i < CurrentCharacterHudRects.Length; i++)
            CurrentCharacterHudRects[i] = Rect.zero;
    }

    public static Rect GetPlayerCharacterHudScreenRect(int playerIndex, float screenWidth, float screenHeight)
    {
        if (playerIndex < 0 || playerIndex >= CurrentCharacterHudRects.Length)
            return Rect.zero;

        Rect current = CurrentCharacterHudRects[playerIndex];
        return current.width > 1f && current.height > 1f
            ? current
            : GetFallbackCharacterHudScreenRect(playerIndex, screenWidth, screenHeight);
    }

    private static Rect GetFallbackCharacterHudScreenRect(int playerIndex, float screenWidth, float screenHeight)
    {
        float panelWidth = Mathf.Max(1f, screenWidth * 0.5f);
        float panelStartX = playerIndex == 0 ? 0f : panelWidth;
        if (!HighwayCharacterVisualUtility.TryLoadTextureData(out HighwayCharacterTextureData data))
            return new Rect(panelStartX + panelWidth * 0.62f, screenHeight * 0.28f, panelWidth * 0.18f, screenHeight * 0.34f);

        Rect local = ComputeSymmetricCharacterViewportRect(
            panelWidth,
            Mathf.Max(1f, screenHeight),
            data.aspect,
            data.sourcePixelWidth,
            data.sourcePixelHeight,
            1f,
            0f,
            0f,
            playerIndex);

        return new Rect(
            panelStartX + local.x * panelWidth,
            (1f - (local.y + local.height)) * screenHeight,
            local.width * panelWidth,
            local.height * screenHeight);
    }

    private void ConfigureMainCamera()
    {
        if (mainCamera == null || owner == null)
            return;

        mainCamera.orthographic = false;
        mainCamera.clearFlags = CameraClearFlags.SolidColor;
        mainCamera.backgroundColor = GetCameraBackgroundColor();
        if (originalMainCameraCullingMask >= 0)
            mainCamera.cullingMask = originalMainCameraCullingMask;
        mainCamera.depth = originalMainCameraDepth;
        mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
        mainCamera.transform.position = new Vector3(
            owner.multiplayerHighwayCameraOffsetX,
            owner.highwayCameraY + owner.multiplayerHighwayCameraOffsetY,
            owner.highwayCameraZ + owner.multiplayerHighwayCameraOffsetZ);
        mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch + owner.multiplayerHighwayCameraPitchOffset, 0f, 0f);
        mainCamera.fieldOfView = Mathf.Clamp(owner.multiplayerHighwayCameraFieldOfView, 30f, 90f);
        mainCamera.orthographic = false;
    }

    private Color GetCameraBackgroundColor()
    {
        if (owner == null)
            return BackgroundColor;

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.BlueSky)
        {
            switch (owner.tabSkyMood)
            {
                case GuitarBridgeServer.TabsSkyMood.Sunset:
                    return new Color(0.05f, 0.03f, 0.05f, 1f);
                case GuitarBridgeServer.TabsSkyMood.Midnight:
                    return new Color(0.010f, 0.012f, 0.034f, 1f);
                default:
                    return new Color(0.03f, 0.05f, 0.10f, 1f);
            }
        }

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.Space)
            return owner.tabSpaceBackgroundColor;

        return owner.tabBackgroundColor;
    }

    private void InitializeBackgroundEffect()
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: true);
        if (backgroundEffect != null && backgroundRoot != null)
        {
            backgroundEffect.Initialize(backgroundRoot.transform, owner);
            SetLayerRecursively(backgroundRoot, 0);
            UpdateBackgroundPlacement();
        }
    }

    private void UpdateBackgroundPlacement()
    {
        if (backgroundRoot == null || owner == null)
            return;

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.Space)
        {
            backgroundRoot.transform.position = Vector3.zero;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;
            return;
        }

        backgroundRoot.transform.position = new Vector3(
            Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f),
            owner.highwayBackgroundCenterY,
            owner.highwayBackgroundDistance);
        backgroundRoot.transform.localRotation = Quaternion.identity;
        backgroundRoot.transform.localScale = Vector3.one * owner.highwayBackgroundScale;
    }

    private void LoadCharacterTexture()
    {
        if (!HighwayCharacterVisualUtility.TryLoadTextureData(out HighwayCharacterTextureData data) || data.texture == null)
            return;

        characterTexture = data.texture;
        characterTextureScale = data.textureScale;
        characterTextureOffset = data.textureOffset;
        characterAspect = data.aspect;
        characterSourcePixelWidth = data.sourcePixelWidth;
        characterSourcePixelHeight = data.sourcePixelHeight;
    }

    private void BuildPlayerScenes()
    {
        playerScenes = new[]
        {
            CreatePlayerScene(0),
            CreatePlayerScene(1)
        };
    }

    private MultiplayerPlayerScene CreatePlayerScene(int playerIndex)
    {
        MultiplayerPlayerScene scene = new MultiplayerPlayerScene
        {
            playerIndex = playerIndex,
            layer = 0
        };

        scene.root = new GameObject(playerIndex == 0 ? "MultiplayerRhythmLeftRoot" : "MultiplayerRhythmRightRoot");
        scene.root.transform.SetParent(root.transform, false);
        scene.root.transform.localPosition = new Vector3(GetTrackHorizontalOffset(playerIndex), 0f, 0f);
        SetLayerRecursively(scene.root, 0);

        scene.gameplayRoot = new GameObject(playerIndex == 0 ? "MultiplayerRhythmLeftGameplayRoot" : "MultiplayerRhythmRightGameplayRoot");
        scene.gameplayRoot.transform.SetParent(scene.root.transform, false);
        SetLayerRecursively(scene.gameplayRoot, 0);

        BuildTrackGeometry(scene);
        BuildCharacter(scene, mirrored: playerIndex == 1);
        return scene;
    }

    private void UpdateTrackPlacement(MultiplayerPlayerScene scene)
    {
        if (scene?.root == null)
            return;

        scene.root.transform.localPosition = new Vector3(GetTrackHorizontalOffset(scene.playerIndex), 0f, 0f);
    }

    private void BuildTrackGeometry(MultiplayerPlayerScene scene)
    {
        int laneCount = GetLaneCount();
        float trackWidth = GetTrackWidth(laneCount);
        float laneWidth = GetLaneWidth();
        float guideDepth = GetLaneGuideDepth();
        float surfaceDepth = guideDepth + LaneBackOverhang;
        float surfaceCenterZ = owner.StrikeLineZ - LaneBackOverhang + (surfaceDepth * 0.5f);
        float guideCenterZ = owner.StrikeLineZ + (guideDepth * 0.5f);

        GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = $"Player{scene.playerIndex + 1}Deck";
        deck.transform.SetParent(scene.gameplayRoot.transform, false);
        deck.transform.localPosition = new Vector3(0f, LaneSurfaceY - 0.025f, surfaceCenterZ);
        deck.transform.localScale = new Vector3(trackWidth + laneWidth * 0.30f, 0.030f, surfaceDepth);
        Object.Destroy(deck.GetComponent<Collider>());
        Material deckMaterial = owner.CreateSharedTransparentMaterial(new Color(0.010f, 0.014f, 0.024f, 0.64f), 0.04f);
        ConfigureOverlayMaterial(deckMaterial, 30, false);
        deck.GetComponent<Renderer>().material = deckMaterial;
        SetLayerRecursively(deck, 0);

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject laneSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            laneSurface.name = $"Player{scene.playerIndex + 1}Lane_{lane}";
            laneSurface.transform.SetParent(scene.gameplayRoot.transform, false);
            laneSurface.transform.localPosition = new Vector3(GetLaneX(lane), LaneSurfaceY, surfaceCenterZ);
            laneSurface.transform.localScale = new Vector3(laneWidth, 0.025f, surfaceDepth);
            Object.Destroy(laneSurface.GetComponent<Collider>());
            Material laneMaterial = CreateLaneSurfaceMaterial();
            Renderer laneRenderer = laneSurface.GetComponent<Renderer>();
            laneRenderer.material = laneMaterial;
            scene.laneRenderers[lane] = laneRenderer;
            scene.laneMaterials[lane] = laneMaterial;
            SetLayerRecursively(laneSurface, 0);
        }

        for (int boundary = 0; boundary <= laneCount; boundary++)
        {
            GameObject laneGuide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            laneGuide.name = $"Player{scene.playerIndex + 1}Guide_{boundary}";
            laneGuide.transform.SetParent(scene.gameplayRoot.transform, false);
            laneGuide.transform.localPosition = new Vector3(GetBoundaryX(boundary), LaneSurfaceY + 0.064f, guideCenterZ);
            laneGuide.transform.localScale = new Vector3(Mathf.Max(Mathf.Max(0.02f, owner.highwayLaneGuideThickness), laneWidth * 0.03f), 0.085f, guideDepth);
            Object.Destroy(laneGuide.GetComponent<Collider>());
            Material guideMaterial = CreateLaneGuideMaterial();
            laneGuide.GetComponent<Renderer>().material = guideMaterial;
            SetLayerRecursively(laneGuide, 0);
        }

        for (int lane = 0; lane < laneCount; lane++)
        {
            Color laneColor = GetLaneColor(lane);
            GameObject endpoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            endpoint.name = $"Player{scene.playerIndex + 1}Endpoint_{lane}";
            endpoint.transform.SetParent(scene.gameplayRoot.transform, false);
            endpoint.transform.localPosition = new Vector3(GetLaneX(lane), EndpointY, owner.StrikeLineZ);
            endpoint.transform.localScale = new Vector3(laneWidth * 0.56f, 0.16f, EndpointDepth);
            Object.Destroy(endpoint.GetComponent<Collider>());
            Material endpointMaterial = owner.CreateSharedGlowMaterial(laneColor, 1.15f);
            ConfigureOverlayMaterial(endpointMaterial, 100, true);
            Renderer endpointRenderer = endpoint.GetComponent<Renderer>();
            endpointRenderer.material = endpointMaterial;
            scene.endpointRenderers[lane] = endpointRenderer;
            scene.endpointMaterials[lane] = endpointMaterial;
            SetLayerRecursively(endpoint, 0);
        }

        GameObject strikeLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strikeLine.name = $"Player{scene.playerIndex + 1}StrikeLine";
        strikeLine.transform.SetParent(scene.gameplayRoot.transform, false);
        strikeLine.transform.localPosition = new Vector3(0f, EndpointY - 0.04f, owner.StrikeLineZ - 0.04f);
        strikeLine.transform.localScale = new Vector3(trackWidth + laneWidth * 0.18f, 0.055f, 0.08f);
        Object.Destroy(strikeLine.GetComponent<Collider>());
        Material strikeMaterial = owner.CreateSharedTransparentMaterial(new Color(0.92f, 0.96f, 1f, 0.74f), 0.35f);
        ConfigureOverlayMaterial(strikeMaterial, 120, true);
        strikeLine.GetComponent<Renderer>().material = strikeMaterial;
        SetLayerRecursively(strikeLine, 0);

        CreateSideRail(scene, -1, trackWidth, guideCenterZ, guideDepth);
        CreateSideRail(scene, 1, trackWidth, guideCenterZ, guideDepth);
    }

    private void CreateSideRail(MultiplayerPlayerScene scene, int side, float trackWidth, float centerZ, float guideDepth)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = side < 0 ? $"Player{scene.playerIndex + 1}LeftRail" : $"Player{scene.playerIndex + 1}RightRail";
        rail.transform.SetParent(scene.gameplayRoot.transform, false);
        float laneWidth = GetLaneWidth();
        rail.transform.localPosition = new Vector3(side * ((trackWidth * 0.5f) + laneWidth * 0.08f), LaneSurfaceY + 0.05f, centerZ);
        rail.transform.localScale = new Vector3(Mathf.Max(0.04f, laneWidth * 0.04f), 0.075f, guideDepth);
        Object.Destroy(rail.GetComponent<Collider>());
        Material railMaterial = owner.CreateSharedTransparentMaterial(new Color(0.74f, 0.86f, 1f, 0.20f), 0.12f);
        ConfigureOverlayMaterial(railMaterial, 65, true);
        rail.GetComponent<Renderer>().material = railMaterial;
        SetLayerRecursively(rail, 0);
    }

    private void BuildCharacter(MultiplayerPlayerScene scene, bool mirrored)
    {
        if (characterTexture == null)
            return;

        GameObject characterRootObject = new GameObject($"Player{scene.playerIndex + 1}CharacterRoot");
        characterRootObject.transform.SetParent(scene.root.transform, false);
        scene.characterTransform = characterRootObject.transform;
        SetLayerRecursively(characterRootObject, 0);

        GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        characterObject.name = $"Player{scene.playerIndex + 1}Character";
        characterObject.transform.SetParent(scene.characterTransform, false);
        Object.Destroy(characterObject.GetComponent<Collider>());

        scene.characterArtTransform = characterObject.transform;
        scene.characterRenderer = characterObject.GetComponent<Renderer>();
        scene.characterMaterial = GetHighwayCharacterMaterial();
        scene.characterRenderer.sharedMaterial = scene.characterMaterial;
        scene.characterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        scene.characterRenderer.receiveShadows = false;
        scene.characterPortalBackMaterial = CreateHighwayCharacterPortalMaterial(-1f, (int)RenderQueue.Transparent - 52);
        scene.characterPortalFrontMaterial = CreateHighwayCharacterPortalMaterial(1f, (int)RenderQueue.Transparent - 51);
        scene.characterPortalBackRenderer = CreateHighwayCharacterPortalRenderer(scene, $"Player{scene.playerIndex + 1}PortalBack", scene.characterPortalBackMaterial, HighwayCharacterPortalBackForwardOffset);
        scene.characterPortalFrontRenderer = CreateHighwayCharacterPortalRenderer(scene, $"Player{scene.playerIndex + 1}PortalFront", scene.characterPortalFrontMaterial, HighwayCharacterPortalFrontForwardOffset);
        SetLayerRecursively(characterObject, 0);

        UpdateCharacter(scene, mirrored, null, null);
    }

    private void UpdateCharacter(MultiplayerPlayerScene scene, bool mirrored, MultiplayerRhythmPlayerSnapshot playerSnapshot, GuitarGameplaySnapshot snapshot)
    {
        if (scene == null || mainCamera == null || scene.characterTransform == null)
            return;

        float panelWidth = Mathf.Max(1f, Screen.width * 0.5f);
        float panelHeight = Mathf.Max(1f, Screen.height);
        float mirroredCharacterHorizontal = 0f;
        float multiplayerCharacterVertical = 0f;
        if (owner != null)
        {
            mirroredCharacterHorizontal = owner.multiplayerCharacterHorizontalOffset;
            multiplayerCharacterVertical = owner.multiplayerCharacterVerticalOffset;
        }
        float rigOffsetY = owner != null ? owner.highwayCharacterRigOffsetY : 0f;
        rigOffsetY += multiplayerCharacterVertical;
        float sideCharacterOffsetX = owner != null ? owner.highwayCharacterOffsetX * (scene.playerIndex == 0 ? -1f : 1f) : 0f;
        float artLocalOffsetY = owner != null ? owner.highwayCharacterOffsetY : 0f;
        Rect localViewportRect = ComputeSymmetricCharacterViewportRect(
            panelWidth,
            panelHeight,
            characterAspect,
            characterSourcePixelWidth,
            characterSourcePixelHeight,
            owner != null ? owner.highwayCharacterScale : 1f,
            mirroredCharacterHorizontal,
            rigOffsetY,
            scene.playerIndex);

        float viewportXOffset = scene.playerIndex == 0 ? 0f : 0.5f;
        Rect viewportRect = new Rect(
            viewportXOffset + (localViewportRect.x * 0.5f),
            localViewportRect.y,
            localViewportRect.width * 0.5f,
            localViewportRect.height);

        Vector3 lowerLeft = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMin, CharacterDepth));
        Vector3 lowerRight = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMin, CharacterDepth));
        Vector3 upperRight = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMax, CharacterDepth));
        Vector3 upperLeft = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMax, CharacterDepth));
        float targetWidth = Vector3.Distance(lowerLeft, lowerRight);
        float targetHeight = Vector3.Distance(lowerLeft, upperLeft);
        Vector3 worldPosition = (lowerLeft + upperRight) * 0.5f;

        scene.characterTransform.position = worldPosition;
        scene.characterTransform.rotation = mainCamera.transform.rotation;
        scene.characterTransform.localScale = new Vector3(
            targetWidth * CharacterScaleMultiplier,
            targetHeight * CharacterScaleMultiplier,
            1f);
        float localOffsetX = HighwayCharacterVisualUtility.ComputeCharacterLocalXOffset(
            viewportRect.width,
            sideCharacterOffsetX,
            CharacterScaleMultiplier);
        float localOffsetY = HighwayCharacterVisualUtility.ComputeCharacterLocalYOffset(
            viewportRect.height,
            artLocalOffsetY,
            CharacterScaleMultiplier);
        if (scene.characterArtTransform != null)
        {
            scene.characterArtTransform.localPosition = new Vector3(localOffsetX, localOffsetY, 0f);
            scene.characterArtTransform.localRotation = Quaternion.identity;
            scene.characterArtTransform.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
        }
        ApplyCharacterFade(scene, localOffsetY);
        UpdateHighwayCharacterPortalVisuals(scene, true);
        UpdateCharacterAnimation(scene, playerSnapshot, snapshot, localOffsetX, localOffsetY, mirrored);

        float artOutwardShift = owner != null ? owner.highwayCharacterOffsetX : 0f;
        Rect hudLocalViewportRect = ComputeSymmetricCharacterViewportRect(
            panelWidth,
            panelHeight,
            characterAspect,
            characterSourcePixelWidth,
            characterSourcePixelHeight,
            owner != null ? owner.highwayCharacterScale : 1f,
            mirroredCharacterHorizontal + artOutwardShift,
            rigOffsetY + artLocalOffsetY,
            scene.playerIndex);
        Rect hudViewportRect = new Rect(
            viewportXOffset + (hudLocalViewportRect.x * 0.5f),
            hudLocalViewportRect.y,
            hudLocalViewportRect.width * 0.5f,
            hudLocalViewportRect.height);
        CurrentCharacterHudRects[scene.playerIndex] = new Rect(
            hudViewportRect.x * Screen.width,
            (1f - (hudViewportRect.y + hudViewportRect.height)) * Screen.height,
            hudViewportRect.width * Screen.width,
            hudViewportRect.height * Screen.height);
    }

    private static Rect ComputeSymmetricCharacterViewportRect(
        float screenWidth,
        float screenHeight,
        float characterAspect,
        int sourcePixelWidth,
        int sourcePixelHeight,
        float scale,
        float outwardShift,
        float verticalShift,
        int playerIndex)
    {
        float safeScreenWidth = Mathf.Max(1f, screenWidth);
        float safeScreenHeight = Mathf.Max(1f, screenHeight);
        float screenAspect = safeScreenWidth / safeScreenHeight;

        float viewportHeight = Mathf.Min(CharacterHeightViewportFraction * Mathf.Max(0.1f, scale), 1f - (2f * CharacterViewportMarginY));
        float viewportWidth = viewportHeight * characterAspect / Mathf.Max(0.1f, screenAspect);
        float maxViewportWidth = Mathf.Max(0.05f, 1f - (2f * CharacterViewportMarginX));
        if (viewportWidth > maxViewportWidth)
        {
            float shrink = maxViewportWidth / viewportWidth;
            viewportWidth = maxViewportWidth;
            viewportHeight *= shrink;
        }

        float maxLeft = 1f - CharacterViewportMarginX - viewportWidth;
        float desiredLeft = playerIndex == 0
            ? CharacterViewportMarginX + CharacterViewportOuterInsetX - outwardShift
            : maxLeft - CharacterViewportOuterInsetX + outwardShift;
        float offsetX = Mathf.Clamp(desiredLeft, CharacterViewportMarginX, maxLeft) - CharacterViewportMarginX;

        return HighwayCharacterVisualUtility.ComputeViewportRect(
            screenWidth,
            screenHeight,
            characterAspect,
            sourcePixelWidth,
            sourcePixelHeight,
            CharacterViewportMarginX,
            CharacterViewportMarginY,
            CharacterHeightViewportFraction,
            CharacterViewportCenterY,
            scale,
            offsetX,
            verticalShift);
    }

    private Material GetHighwayCharacterMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(Color.white, 0f);
        material.color = Color.white;
        material.mainTexture = characterTexture;
        material.SetTexture("_MainTex", characterTexture);
        material.mainTextureScale = characterTextureScale;
        material.mainTextureOffset = characterTextureOffset;
        material.SetTextureScale("_MainTex", characterTextureScale);
        material.SetTextureOffset("_MainTex", characterTextureOffset);
        if (material.HasProperty(CharacterFadeStartShaderId))
            material.SetFloat(CharacterFadeStartShaderId, HighwayCharacterBottomFadeStart01);
        if (material.HasProperty(CharacterFadeEndShaderId))
            material.SetFloat(CharacterFadeEndShaderId, HighwayCharacterBottomFadeEnd01);
        return material;
    }

    private void ApplyCharacterFade(MultiplayerPlayerScene scene, float localCharacterYOffset)
    {
        if (scene?.characterMaterial == null || owner == null)
            return;

        HighwayCharacterVisualUtility.ComputeVerticalCompensation(
            localCharacterYOffset,
            HighwayCharacterBottomFadeStart01,
            HighwayCharacterBottomFadeEnd01,
            owner.highwayCharacterFadeSoftness,
            HighwayCharacterPortalLocalYInCharacterHeights + owner.multiplayerPortalVerticalOffset,
            out float fadeStart,
            out float fadeEnd,
            out _);

        if (scene.characterMaterial.HasProperty(CharacterFadeStartShaderId))
            scene.characterMaterial.SetFloat(CharacterFadeStartShaderId, fadeStart);
        if (scene.characterMaterial.HasProperty(CharacterFadeEndShaderId))
            scene.characterMaterial.SetFloat(CharacterFadeEndShaderId, fadeEnd);
    }

    private void UpdateCharacterAnimation(
        MultiplayerPlayerScene scene,
        MultiplayerRhythmPlayerSnapshot playerSnapshot,
        GuitarGameplaySnapshot snapshot,
        float baseLocalX,
        float baseLocalY,
        bool mirrored)
    {
        if (scene?.characterArtTransform == null)
            return;

        if ((owner != null && !owner.highwayCharacterAnimationsEnabled) ||
            snapshot == null ||
            snapshot.isPaused)
        {
            ResetCharacterAnimation(scene, baseLocalX, baseLocalY, mirrored);
            return;
        }

        float songTime = snapshot.songTime;
        EnsureCharacterBopEvents(scene, playerSnapshot);
        UpdateCharacterMissState(scene, playerSnapshot, songTime);

        float missAge = songTime - scene.lastMissTriggerSongTime;
        float missStrength = GetCharacterMissStrength(missAge);
        float missMotionSuppression = 1f - Mathf.Clamp01(missStrength * 0.76f);
        int eventIndex = FindLastCharacterBopEventIndex(scene, songTime);
        float lift = 0f;
        float squash = 0f;
        float stretch = 0f;
        float tilt = 0f;
        float bopPresence = 0f;

        for (int i = eventIndex; i >= 0 && i >= eventIndex - 2; i--)
        {
            HighwayCharacterBopEvent bopEvent = scene.bopEvents[i];
            float age = songTime - bopEvent.time;
            if (age < 0f || age > HighwayCharacterBopDurationSeconds)
                continue;

            float attack = Mathf.Clamp01(age / Mathf.Max(0.01f, HighwayCharacterBopAttackSeconds));
            attack = attack * attack * (3f - (2f * attack));
            float normalizedAge = Mathf.Clamp01(age / HighwayCharacterBopDurationSeconds);
            float decay = 1f - normalizedAge;
            decay *= decay;
            float pulse = attack * decay;
            float motionWave = Mathf.Sin(normalizedAge * Mathf.PI * 1.18f);
            float strength = bopEvent.strength;

            lift += pulse * strength;
            stretch += pulse * strength;
            squash += motionWave * pulse * strength;
            tilt += Mathf.Sin(normalizedAge * Mathf.PI * 1.9f) * pulse * strength;
            bopPresence = Mathf.Max(bopPresence, pulse * strength);
        }

        lift *= missMotionSuppression;
        squash *= missMotionSuppression;
        stretch *= missMotionSuppression;
        tilt *= missMotionSuppression;
        bopPresence *= missMotionSuppression;

        float idleWeight = (1f - Mathf.Clamp01(bopPresence * 1.45f)) * missMotionSuppression;
        float swayWave = Mathf.Sin((songTime * HighwayCharacterIdleSwaySpeed) + 0.35f);
        float breathWave = Mathf.Sin((songTime * HighwayCharacterIdleBreathSpeed) - 0.65f);
        float breathEnvelope = (breathWave * 0.5f) + 0.5f;
        float idleLocalX = swayWave * HighwayCharacterIdleSwayInCharacterWidths * idleWeight;
        float idleLocalLift = breathEnvelope * HighwayCharacterIdleLiftInCharacterHeights * idleWeight;
        float idleScaleX = 1f - (breathEnvelope * HighwayCharacterIdleScaleXAmount * idleWeight);
        float idleScaleY = 1f + (breathEnvelope * HighwayCharacterIdleScaleYAmount * idleWeight);
        float idleRotationZ = swayWave * HighwayCharacterIdleTiltDegrees * idleWeight;

        float localLift = idleLocalLift + Mathf.Clamp(lift * HighwayCharacterBopLiftInCharacterHeights, 0f, HighwayCharacterBopLiftInCharacterHeights * 1.35f);
        float scaleX = idleScaleX - Mathf.Clamp(squash * HighwayCharacterBopScaleXAmount, 0f, HighwayCharacterBopScaleXAmount * 1.5f);
        float scaleY = idleScaleY + Mathf.Clamp(stretch * HighwayCharacterBopScaleYAmount, 0f, HighwayCharacterBopScaleYAmount * 1.5f);
        float rotationZ = idleRotationZ + Mathf.Clamp(tilt * HighwayCharacterBopTiltDegrees, -HighwayCharacterBopTiltDegrees, HighwayCharacterBopTiltDegrees);

        if (missStrength > 0.0001f)
        {
            float missShakeWave = Mathf.Sin((missAge * 26f) + 0.45f);
            float missReboundWave = Mathf.Sin((missAge * 11.5f) - 0.4f);
            float missLocalX = missShakeWave * HighwayCharacterMissSwayInCharacterWidths * missStrength;
            float missLocalLift = (-HighwayCharacterMissDropInCharacterHeights * missStrength) + (Mathf.Max(0f, missReboundWave) * HighwayCharacterMissDropInCharacterHeights * 0.18f * missStrength);

            idleLocalX += missLocalX;
            localLift += missLocalLift;
            scaleX += HighwayCharacterMissScaleXAmount * missStrength;
            scaleY -= HighwayCharacterMissScaleYAmount * missStrength;
            rotationZ += missShakeWave * HighwayCharacterMissTiltDegrees * missStrength;
        }

        float uniformScale = Mathf.Max(0.82f, (scaleX + scaleY) * 0.5f);
        scene.characterArtTransform.localPosition = new Vector3(baseLocalX + idleLocalX, baseLocalY + localLift, 0f);
        scene.characterArtTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        scene.characterArtTransform.localScale = new Vector3((mirrored ? -1f : 1f) * uniformScale, uniformScale, 1f);
    }

    private void ResetCharacterAnimation(MultiplayerPlayerScene scene, float baseLocalX, float baseLocalY, bool mirrored)
    {
        if (scene?.characterArtTransform == null)
            return;

        scene.characterArtTransform.localPosition = new Vector3(baseLocalX, baseLocalY, 0f);
        scene.characterArtTransform.localRotation = Quaternion.identity;
        scene.characterArtTransform.localScale = new Vector3(mirrored ? -1f : 1f, 1f, 1f);
    }

    private void EnsureCharacterBopEvents(MultiplayerPlayerScene scene, MultiplayerRhythmPlayerSnapshot playerSnapshot)
    {
        int noteCount = playerSnapshot?.arcadeNoteStates?.Count ?? 0;
        if (noteCount == scene.lastBopSourceNoteCount)
            return;

        scene.lastBopSourceNoteCount = noteCount;
        BuildCharacterBopEvents(scene, playerSnapshot?.arcadeNoteStates);
    }

    private void BuildCharacterBopEvents(MultiplayerPlayerScene scene, List<ArcadeNoteState> noteStates)
    {
        scene.bopEvents.Clear();
        if (noteStates == null || noteStates.Count == 0)
            return;

        List<ArcadeNoteData> orderedNotes = new List<ArcadeNoteData>(noteStates.Count);
        for (int i = 0; i < noteStates.Count; i++)
        {
            ArcadeNoteState noteState = noteStates[i];
            if (noteState != null && noteState.data.time >= 0f)
                orderedNotes.Add(noteState.data);
        }

        if (orderedNotes.Count == 0)
            return;

        orderedNotes.Sort((a, b) => a.time.CompareTo(b.time));

        float groupTime = orderedNotes[0].time;
        int noteCount = 0;
        bool hasTechnique = false;
        for (int i = 0; i < orderedNotes.Count; i++)
        {
            ArcadeNoteData note = orderedNotes[i];
            if (note.time - groupTime <= HighwayCharacterBopGroupWindowSeconds)
            {
                noteCount++;
                hasTechnique |= note.isHopo || note.isTap;
                continue;
            }

            AddCharacterBopEvent(scene, groupTime, noteCount, hasTechnique);
            groupTime = note.time;
            noteCount = 1;
            hasTechnique = note.isHopo || note.isTap;
        }

        AddCharacterBopEvent(scene, groupTime, noteCount, hasTechnique);
    }

    private void AddCharacterBopEvent(MultiplayerPlayerScene scene, float time, int noteCount, bool hasTechnique)
    {
        float strength = 0.95f;
        strength += Mathf.Min(0.24f, Mathf.Max(0, noteCount - 1) * 0.08f);
        if (hasTechnique)
            strength += 0.06f;
        strength = Mathf.Clamp(strength, 0.9f, 1.3f);

        if (scene.bopEvents.Count > 0)
        {
            int lastIndex = scene.bopEvents.Count - 1;
            HighwayCharacterBopEvent previous = scene.bopEvents[lastIndex];
            if (time - previous.time < HighwayCharacterBopMinimumSpacingSeconds)
            {
                previous.time = Mathf.Lerp(previous.time, time, 0.35f);
                previous.strength = Mathf.Max(previous.strength, strength);
                scene.bopEvents[lastIndex] = previous;
                return;
            }
        }

        scene.bopEvents.Add(new HighwayCharacterBopEvent
        {
            time = time,
            strength = strength
        });
    }

    private void UpdateCharacterMissState(MultiplayerPlayerScene scene, MultiplayerRhythmPlayerSnapshot playerSnapshot, float songTime)
    {
        int missCount = Mathf.Max(0, playerSnapshot?.missCount ?? 0);
        if (scene.lastObservedMissCount < 0 || missCount < scene.lastObservedMissCount)
        {
            scene.lastObservedMissCount = missCount;
            if (missCount == 0)
                scene.lastMissTriggerSongTime = float.NegativeInfinity;
            return;
        }

        if (missCount > scene.lastObservedMissCount)
            scene.lastMissTriggerSongTime = songTime;

        scene.lastObservedMissCount = missCount;
    }

    private float GetCharacterMissStrength(float missAge)
    {
        if (missAge < 0f || missAge > HighwayCharacterMissDurationSeconds)
            return 0f;

        float attack = Mathf.Clamp01(missAge / Mathf.Max(0.01f, HighwayCharacterMissAttackSeconds));
        attack = attack * attack * (3f - (2f * attack));
        float recoveryDuration = Mathf.Max(0.01f, HighwayCharacterMissDurationSeconds - HighwayCharacterMissAttackSeconds);
        float recovery = 1f - Mathf.Clamp01((missAge - HighwayCharacterMissAttackSeconds) / recoveryDuration);
        recovery *= recovery;
        return attack * recovery;
    }

    private int FindLastCharacterBopEventIndex(MultiplayerPlayerScene scene, float songTime)
    {
        int low = 0;
        int high = scene.bopEvents.Count - 1;
        int best = -1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            if (scene.bopEvents[mid].time <= songTime + 0.0001f)
            {
                best = mid;
                low = mid + 1;
            }
            else
                high = mid - 1;
        }

        return best;
    }

    private Renderer CreateHighwayCharacterPortalRenderer(MultiplayerPlayerScene scene, string name, Material material, float localForwardOffset)
    {
        if (scene?.characterTransform == null)
            return null;

        GameObject portalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portalObject.name = name;
        portalObject.transform.SetParent(scene.characterTransform, false);
        portalObject.transform.localPosition = new Vector3(0f, HighwayCharacterPortalLocalYInCharacterHeights, localForwardOffset);
        portalObject.transform.localScale = new Vector3(
            HighwayCharacterPortalWidthInCharacterWidths,
            HighwayCharacterPortalHeightInCharacterHeights,
            1f);
        Object.Destroy(portalObject.GetComponent<Collider>());

        Renderer portalRenderer = portalObject.GetComponent<Renderer>();
        portalRenderer.sharedMaterial = material;
        portalRenderer.shadowCastingMode = ShadowCastingMode.Off;
        portalRenderer.receiveShadows = false;
        SetLayerRecursively(portalObject, 0);
        return portalRenderer;
    }

    private Material CreateHighwayCharacterPortalMaterial(float halfMode, int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterPortal");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterPortal");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.03f, 0.12f, 0.16f, 0.72f), 0f);
        material.renderQueue = renderQueue;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        if (material.HasProperty(CharacterPortalHalfModeShaderId))
            material.SetFloat(CharacterPortalHalfModeShaderId, halfMode);
        if (material.HasProperty(CharacterPortalRingThicknessShaderId))
            material.SetFloat(CharacterPortalRingThicknessShaderId, HighwayCharacterPortalRingThickness);
        if (material.HasProperty(CharacterPortalSoftnessShaderId))
            material.SetFloat(CharacterPortalSoftnessShaderId, HighwayCharacterPortalEdgeSoftness);
        if (material.HasProperty(CharacterPortalRimSoftnessShaderId))
            material.SetFloat(CharacterPortalRimSoftnessShaderId, HighwayCharacterPortalRimSoftness);
        if (material.HasProperty(CharacterPortalAlphaFloorShaderId))
            material.SetFloat(CharacterPortalAlphaFloorShaderId, HighwayCharacterPortalInteriorAlphaFloor);
        if (material.HasProperty(CharacterPortalSwirlSpeedShaderId))
            material.SetFloat(CharacterPortalSwirlSpeedShaderId, HighwayCharacterPortalSwirlSpeed);
        if (material.HasProperty(CharacterPortalSwirlSharpnessShaderId))
            material.SetFloat(CharacterPortalSwirlSharpnessShaderId, HighwayCharacterPortalSwirlSharpness);
        if (material.HasProperty(CharacterPortalSplitYShaderId))
            material.SetFloat(CharacterPortalSplitYShaderId, HighwayCharacterPortalSplitY01);
        if (material.HasProperty(CharacterPortalSplitSoftnessShaderId))
            material.SetFloat(CharacterPortalSplitSoftnessShaderId, HighwayCharacterPortalSplitSoftness01);
        return material;
    }

    private void UpdateHighwayCharacterPortalVisuals(MultiplayerPlayerScene scene, bool shouldShowPortal)
    {
        if (scene == null)
            return;

        UpdateHighwayCharacterPortalTransform(scene.characterPortalBackRenderer, scene, HighwayCharacterPortalBackForwardOffset);
        UpdateHighwayCharacterPortalTransform(scene.characterPortalFrontRenderer, scene, HighwayCharacterPortalFrontForwardOffset);

        bool portalEnabled = shouldShowPortal && owner != null && owner.highwayCharacterPortalEnabled;
        if (scene.characterPortalBackRenderer != null)
            scene.characterPortalBackRenderer.enabled = portalEnabled;
        if (scene.characterPortalFrontRenderer != null)
            scene.characterPortalFrontRenderer.enabled = portalEnabled;

        if (!portalEnabled)
            return;

        ApplyHighwayCharacterPortalPalette(scene.characterPortalBackMaterial);
        ApplyHighwayCharacterPortalPalette(scene.characterPortalFrontMaterial);
    }

    private void UpdateHighwayCharacterPortalTransform(Renderer portalRenderer, MultiplayerPlayerScene scene, float localForwardOffset)
    {
        if (portalRenderer == null)
            return;

        float mirroredPortalHorizontal = owner != null ? owner.multiplayerPortalHorizontalOffset : 0f;
        float portalLocalX = scene.playerIndex == 0 ? -mirroredPortalHorizontal : mirroredPortalHorizontal;
        float portalLocalY = HighwayCharacterPortalLocalYInCharacterHeights + (owner != null ? owner.multiplayerPortalVerticalOffset : 0f);
        portalRenderer.transform.localPosition = new Vector3(portalLocalX, portalLocalY, localForwardOffset);
        float widthScale = owner != null ? Mathf.Max(0.05f, owner.multiplayerPortalWidthScale) : 1f;
        portalRenderer.transform.localScale = new Vector3(
            HighwayCharacterPortalWidthInCharacterWidths * widthScale,
            HighwayCharacterPortalHeightInCharacterHeights,
            1f);
    }

    private void ApplyHighwayCharacterPortalPalette(Material material)
    {
        if (material == null || owner == null)
            return;

        float bodyOpacity = Mathf.Clamp01(owner.highwayCharacterPortalBodyOpacity);
        Color baseColor = WithAlpha(HighwayCharacterPortalBaseColor, HighwayCharacterPortalBaseOpacity);
        Color coreColor = WithAlpha(HighwayCharacterPortalCoreColor, HighwayCharacterPortalCoreOpacity);
        Color rimColor = WithAlpha(
            HighwayCharacterVisualUtility.ResolvePortalEdgeColor(owner.highwayCharacterPortalEdgeColor),
            HighwayCharacterPortalRimOpacity);
        Color accentColor = owner.highwayCharacterPortalSwirlsEnabled
            ? WithAlpha(
                HighwayCharacterVisualUtility.ResolvePortalSwirlColor(owner.highwayCharacterPortalSwirlColor),
                HighwayCharacterPortalSwirlOpacity)
            : new Color(0f, 0f, 0f, 0f);

        material.color = Color.Lerp(baseColor, rimColor, HighwayCharacterPortalPreviewTintMix);
        if (material.HasProperty(CharacterPortalBaseColorShaderId))
            material.SetColor(CharacterPortalBaseColorShaderId, baseColor);
        if (material.HasProperty(CharacterPortalRimColorShaderId))
            material.SetColor(CharacterPortalRimColorShaderId, rimColor);
        if (material.HasProperty(CharacterPortalAccentColorShaderId))
            material.SetColor(CharacterPortalAccentColorShaderId, accentColor);
        if (material.HasProperty(CharacterPortalCoreColorShaderId))
            material.SetColor(CharacterPortalCoreColorShaderId, coreColor);
        if (material.HasProperty(CharacterPortalAlphaFloorShaderId))
            material.SetFloat(CharacterPortalAlphaFloorShaderId, bodyOpacity);
        if (material.HasProperty(CharacterPortalGlowStrengthShaderId))
            material.SetFloat(CharacterPortalGlowStrengthShaderId, HighwayCharacterPortalGlowStrength);
    }

    private static Color WithAlpha(Color color, float alpha)
    {
        color.a = Mathf.Clamp01(alpha);
        return color;
    }

    private void UpdatePlayerNotes(MultiplayerPlayerScene scene, MultiplayerRhythmPlayerSnapshot playerSnapshot, float songTime)
    {
        scene.visibleNoteIds.Clear();
        if (playerSnapshot == null || playerSnapshot.arcadeNoteStates == null)
        {
            ClearRemovedNotes(scene);
            return;
        }

        for (int i = 0; i < playerSnapshot.arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = playerSnapshot.arcadeNoteStates[i];
            if (state == null)
                continue;

            float headZ = owner.StrikeLineZ + ((state.data.time - songTime) * currentVisualNoteSpeed);
            float sustainEndZ = owner.StrikeLineZ + (((state.data.time + Mathf.Max(0f, state.data.duration)) - songTime) * currentVisualNoteSpeed);
            bool keepResolvedBriefly = state.IsResolved && songTime - state.resolvedAt <= ResolvedHeadHoldSeconds;
            bool showHead = headZ <= owner.ArcadeSpawnZ && headZ >= owner.StrikeLineZ && (!state.IsResolved || keepResolvedBriefly);
            bool showSustain = Mathf.Max(0f, state.data.duration) > 0.08f &&
                               sustainEndZ > owner.StrikeLineZ &&
                               headZ <= owner.ArcadeSpawnZ;

            if (!showHead && !showSustain)
                continue;

            scene.visibleNoteIds.Add(state.data.id);
            MultiplayerNoteView view = GetOrCreateNoteView(scene, state.data);
            UpdateNoteView(view, state, scene.playerIndex, headZ, sustainEndZ, showHead);
        }

        ClearRemovedNotes(scene);
    }

    private void UpdateLaneVisuals(MultiplayerPlayerScene scene, MultiplayerRhythmPlayerSnapshot playerSnapshot, float songTime)
    {
        if (scene == null)
            return;

        int laneCount = GetLaneCount();
        bool[] held = playerSnapshot?.heldLanes;
        bool[] incoming = new bool[laneCount];
        if (playerSnapshot?.arcadeNoteStates != null)
        {
            for (int i = 0; i < playerSnapshot.arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState state = playerSnapshot.arcadeNoteStates[i];
                if (state == null || state.IsResolved || state.data.isOpen)
                    continue;

                float delta = state.data.time - songTime;
                if (delta >= -0.05f && delta <= 0.75f && state.data.lane >= 0 && state.data.lane < laneCount)
                    incoming[state.data.lane] = true;
            }
        }

        for (int lane = 0; lane < laneCount; lane++)
        {
            bool laneHeld = held != null && lane < held.Length && held[lane];
            bool laneIncoming = incoming[lane];
            Color laneColor = GetLaneColor(lane);
            float pulse = GetLanePulse(scene, lane);
            bool missPulse = pulse < -0.001f;
            float pulseAmount = Mathf.Abs(pulse);
            Color laneBase = laneHeld || laneIncoming
                ? new Color(0.08f, 0.10f, 0.14f, 1f)
                : new Color(0.025f, 0.03f, 0.045f, 0.14f);
            if (pulseAmount > 0f)
            {
                laneBase = missPulse
                    ? Color.Lerp(laneBase, new Color(0.42f, 0.05f, 0.06f, 0.78f), pulseAmount)
                    : Color.Lerp(laneBase, new Color(laneColor.r, laneColor.g, laneColor.b, 0.76f), pulseAmount * 0.42f);
            }
            if (scene.laneMaterials[lane] != null)
            {
                ApplyMaterialColor(scene.laneMaterials[lane], laneBase, 0f);
                Color laneEmission = laneHeld ? new Color(0.18f, 0.32f, 0.46f, 1f) * Mathf.Pow(2f, 0.15f) : Color.black;
                if (pulseAmount > 0f)
                {
                    laneEmission = missPulse
                        ? owner.highwayMissColor * Mathf.Pow(2f, 0.7f + pulseAmount)
                        : laneColor * Mathf.Pow(2f, 0.65f + pulseAmount * 1.2f);
                }
                scene.laneMaterials[lane].SetColor("_EmissionColor", laneEmission);
                if (scene.laneMaterials[lane].HasProperty("_FrontBackFade"))
                    scene.laneMaterials[lane].SetFloat("_FrontBackFade", 0.1f);
            }

            Color endpointColor = laneHeld ? new Color(0.985f, 0.99f, 1f, 1f) : laneColor;
            float endpointEmission = laneHeld ? 3.35f : laneIncoming ? 1.55f : 0.65f;
            if (pulseAmount > 0f)
            {
                endpointEmission = Mathf.Max(endpointEmission, missPulse ? 2.2f * pulseAmount : 2.85f * pulseAmount);
                endpointColor = missPulse
                    ? Color.Lerp(endpointColor, owner.highwayMissColor, pulseAmount)
                    : Color.Lerp(endpointColor, new Color(0.95f, 0.99f, 1f, 1f), pulseAmount * 0.62f);
            }
            if (scene.endpointMaterials[lane] != null)
                ApplyMaterialColor(scene.endpointMaterials[lane], endpointColor, endpointEmission);
        }
    }

    private void UpdateResolvedFeedback(MultiplayerPlayerScene scene, MultiplayerRhythmPlayerSnapshot playerSnapshot, float songTime)
    {
        if (scene == null || playerSnapshot?.arcadeNoteStates == null || owner == null)
            return;

        for (int i = 0; i < playerSnapshot.arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = playerSnapshot.arcadeNoteStates[i];
            if (state == null)
                continue;

            if (!state.IsResolved)
            {
                scene.resolvedFeedbackResults.Remove(state.data.id);
                continue;
            }

            if (scene.resolvedFeedbackResults.TryGetValue(state.data.id, out GameplayNoteResult recorded) && recorded == state.result)
                continue;

            scene.resolvedFeedbackResults[state.data.id] = state.result;
            if (state.resolvedAt < 0f || Mathf.Abs(songTime - state.resolvedAt) > MaxFeedbackTriggerAge)
                continue;

            TriggerFeedbackForResolvedNote(scene, state);
        }
    }

    private void TriggerFeedbackForResolvedNote(MultiplayerPlayerScene scene, ArcadeNoteState state)
    {
        if (scene == null || state == null || owner == null)
            return;

        bool hit = state.IsHit;
        bool miss = state.IsMissed;
        if (!hit && !miss)
            return;

        Color color = hit ? GetNoteBaseColor(state.data) : owner.highwayMissColor;
        if (hit && state.data.noteType == ArcadeNoteType.Tap)
            color = Color.Lerp(color, GetNoteAccentColor(state.data), 0.35f);

        float timingWindow = Mathf.Max(0.001f, state.resolvedAt <= state.data.time ? owner.arcadeHitWindowEarly : owner.arcadeHitWindowLate);
        float precision = hit ? Mathf.Clamp01(1f - Mathf.Abs(state.resolvedAt - state.data.time) / timingWindow) : 0f;
        if (state.data.isOpen)
        {
            int laneCount = GetLaneCount();
            for (int lane = 0; lane < laneCount; lane++)
                SetLanePulse(scene, lane, hit ? Mathf.Lerp(0.76f, 1.15f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);

            CreateFeedbackBurst(scene, 0f, Mathf.Max(owner.FretSpacing, GetTrackWidth(laneCount) * 0.92f), color, hit, precision, openNote: true);
        }
        else
        {
            int lane = Mathf.Clamp(state.data.lane, 0, GetLaneCount() - 1);
            SetLanePulse(scene, lane, hit ? Mathf.Lerp(0.85f, 1.25f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);
            CreateFeedbackBurst(scene, GetLaneX(lane), owner.FretSpacing * 0.72f, color, hit, precision, openNote: false);
        }
    }

    private void SetLanePulse(MultiplayerPlayerScene scene, int lane, float strength, float duration)
    {
        if (scene == null || lane < 0 || lane >= scene.lanePulseUntil.Length)
            return;

        bool expired = scene.lanePulseUntil[lane] <= Time.time;
        bool changedSign = !Mathf.Approximately(Mathf.Sign(strength), Mathf.Sign(scene.lanePulseStrength[lane]));
        scene.lanePulseUntil[lane] = Mathf.Max(scene.lanePulseUntil[lane], Time.time + Mathf.Max(0.01f, duration));
        if (expired || changedSign || Mathf.Abs(strength) >= Mathf.Abs(scene.lanePulseStrength[lane]))
            scene.lanePulseStrength[lane] = strength;
    }

    private float GetLanePulse(MultiplayerPlayerScene scene, int lane)
    {
        if (scene == null || lane < 0 || lane >= scene.lanePulseUntil.Length || scene.lanePulseUntil[lane] <= Time.time)
            return 0f;

        float duration = scene.lanePulseStrength[lane] < 0f ? MissPulseDuration : HitPulseDuration;
        float remaining = Mathf.Clamp01((scene.lanePulseUntil[lane] - Time.time) / Mathf.Max(0.01f, duration));
        return scene.lanePulseStrength[lane] * EaseOutCubic(remaining);
    }

    private MultiplayerNoteView GetOrCreateNoteView(MultiplayerPlayerScene scene, ArcadeNoteData note)
    {
        if (scene.noteViews.TryGetValue(note.id, out MultiplayerNoteView existing))
            return existing;

        Color baseColor = GetNoteBaseColor(note);
        GameObject rootObject = new GameObject($"P{scene.playerIndex + 1}_Note_{note.id}");
        rootObject.transform.SetParent(scene.gameplayRoot.transform, false);
        SetLayerRecursively(rootObject, scene.layer);

        GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
        body.name = "Body";
        body.transform.SetParent(rootObject.transform, false);
        Object.Destroy(body.GetComponent<Collider>());
        SetLayerRecursively(body, scene.layer);
        Material bodyMaterial = owner.CreateSharedGlowMaterial(baseColor, 1.1f);
        ConfigureOverlayMaterial(bodyMaterial, 130, true);
        Renderer bodyRenderer = body.GetComponent<Renderer>();
        bodyRenderer.material = bodyMaterial;

        GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
        outline.name = "Outline";
        outline.transform.SetParent(rootObject.transform, false);
        Object.Destroy(outline.GetComponent<Collider>());
        SetLayerRecursively(outline, scene.layer);
        Material outlineMaterial = owner.CreateSharedTransparentMaterial(new Color(0.01f, 0.015f, 0.03f, 0.96f), 0f);
        ConfigureOverlayMaterial(outlineMaterial, 123, true);
        Renderer outlineRenderer = outline.GetComponent<Renderer>();
        outlineRenderer.material = outlineMaterial;

        GameObject accent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        accent.name = "Accent";
        accent.transform.SetParent(rootObject.transform, false);
        Object.Destroy(accent.GetComponent<Collider>());
        SetLayerRecursively(accent, scene.layer);
        Material accentMaterial = owner.CreateSharedGlowMaterial(GetNoteAccentColor(note), 1.45f);
        ConfigureOverlayMaterial(accentMaterial, 116, true);
        Renderer accentRenderer = accent.GetComponent<Renderer>();
        accentRenderer.material = accentMaterial;

        GameObject sustain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sustain.name = "Sustain";
        sustain.transform.SetParent(rootObject.transform, false);
        Object.Destroy(sustain.GetComponent<Collider>());
        SetLayerRecursively(sustain, scene.layer);
        Material sustainMaterial = CreateTransparentGlowMaterial(new Color(baseColor.r, baseColor.g, baseColor.b, 0.48f), 0.32f);
        ConfigureOverlayMaterial(sustainMaterial, 90, true);
        Renderer sustainRenderer = sustain.GetComponent<Renderer>();
        sustainRenderer.material = sustainMaterial;

        MultiplayerNoteView view = new MultiplayerNoteView
        {
            root = rootObject,
            body = body,
            bodyRenderer = bodyRenderer,
            bodyMaterial = bodyMaterial,
            outline = outline,
            outlineRenderer = outlineRenderer,
            outlineMaterial = outlineMaterial,
            accent = accent,
            accentRenderer = accentRenderer,
            accentMaterial = accentMaterial,
            sustain = sustain,
            sustainRenderer = sustainRenderer,
            sustainMaterial = sustainMaterial,
            baseColor = baseColor
        };
        scene.noteViews[note.id] = view;
        return view;
    }

    private void UpdateNoteView(MultiplayerNoteView view, ArcadeNoteState state, int playerIndex, float headZ, float sustainEndZ, bool showHead)
    {
        if (view == null || state == null)
            return;

        int laneCount = GetLaneCount();
        float x = state.data.isOpen ? 0f : GetLaneX(state.data.lane);
        Vector3 bodyScale = state.data.isOpen ? GetOpenNoteScale(laneCount) : GetFrettedNoteScale();
        float width = bodyScale.x;
        view.root.transform.localPosition = new Vector3(x, NoteY, owner.StrikeLineZ);

        float bodyWorldZ = showHead ? Mathf.Clamp(headZ, owner.StrikeLineZ, owner.ArcadeSpawnZ) : owner.StrikeLineZ;
        float bodyLocalZ = bodyWorldZ - owner.StrikeLineZ;

        if (view.body != null)
        {
            view.body.SetActive(showHead);
            if (showHead)
            {
                view.body.transform.localPosition = new Vector3(0f, 0f, bodyLocalZ);
                view.body.transform.localScale = bodyScale;
            }
        }

        UpdateAccentView(view, state.data, bodyScale, bodyLocalZ, showHead);

        bool hasMeaningfulSustain = owner != null && owner.HasArcadeVisibleSustain(state.data);
        bool sustainActivelyHeld = owner != null && owner.IsMultiplayerRhythmSustainActivelyHeld(playerIndex, state.data);
        float sustainPulse01 = sustainActivelyHeld
            ? 0.5f + (0.5f * Mathf.Sin(Time.time * SustainActivePulseSpeed + (state.data.chordId * 0.73f)))
            : 0f;
        bool showSustain = hasMeaningfulSustain;
        if (view.sustain != null)
        {
            if (showSustain)
            {
                float sustainStartZ = showHead
                    ? Mathf.Min(owner.ArcadeSpawnZ, bodyWorldZ + Mathf.Max(0.02f, bodyScale.z * 0.5f))
                    : owner.StrikeLineZ;
                float clippedEndZ = Mathf.Min(owner.ArcadeSpawnZ, sustainEndZ);
                float tailLength = Mathf.Max(0f, clippedEndZ - sustainStartZ);
                showSustain = tailLength > 0.02f;
                if (showSustain)
                {
                    float baseWidth = state.data.isOpen ? width * 0.88f : Mathf.Max(0.16f, owner.FretSpacing * 0.08f);
                    float baseHeight = Mathf.Max(0.08f, bodyScale.y * 0.42f);
                    float animatedWidth = sustainActivelyHeld ? baseWidth * Mathf.Lerp(1f, SustainActiveWidthScale, sustainPulse01) : baseWidth;
                    float animatedHeight = sustainActivelyHeld ? baseHeight * Mathf.Lerp(1f, SustainActiveHeightScale, sustainPulse01) : baseHeight;
                    view.sustain.transform.localPosition = new Vector3(0f, -0.02f, ((sustainStartZ + clippedEndZ) * 0.5f) - owner.StrikeLineZ);
                    view.sustain.transform.localScale = new Vector3(animatedWidth, animatedHeight, tailLength);
                }
            }

            view.sustain.SetActive(showSustain);
        }

        Color headColor = view.baseColor;
        float emission = 1.05f;
        if (state.IsHit)
        {
            headColor = Color.Lerp(view.baseColor, owner.highwayHitColor, 0.64f);
            emission = 2.6f;
        }
        else if (state.IsMissed)
        {
            headColor = owner.highwayMissColor;
            emission = 0f;
        }
        else if (state.isJudgeable)
        {
            headColor = Color.Lerp(view.baseColor, Color.white, 0.25f);
            emission = 2.0f;
        }

        ApplyMaterialColor(view.bodyMaterial, headColor, emission);
        if (view.outlineMaterial != null)
        {
            Color outlineColor = state.IsMissed
                ? Color.Lerp(owner.highwayMissColor, Color.black, 0.72f)
                : new Color(0.01f, 0.015f, 0.03f, 0.96f);
            ApplyMaterialColor(view.outlineMaterial, outlineColor, state.IsMissed ? 0.18f : 0f);
        }
        if (view.accentMaterial != null)
        {
            Color accentColor = state.IsMissed ? owner.highwayMissColor : GetNoteAccentColor(state.data);
            ApplyMaterialColor(view.accentMaterial, accentColor, state.IsMissed ? 0f : Mathf.Max(1.75f, emission + 0.25f));
        }
        if (view.sustainMaterial != null)
        {
            Color sustainBaseColor = state.IsMissed ? owner.highwayMissColor : view.baseColor;
            Color sustainColor = new Color(sustainBaseColor.r, sustainBaseColor.g, sustainBaseColor.b, state.IsMissed ? 0.20f : 0.46f);
            float sustainEmission = state.IsMissed ? 0f : 0.55f;
            if (sustainActivelyHeld)
            {
                sustainColor = Color.Lerp(sustainColor, new Color(1f, 1f, 1f, sustainColor.a), 0.10f + (0.08f * sustainPulse01));
                sustainColor.a = Mathf.Clamp01(sustainColor.a + (0.10f * sustainPulse01));
                sustainEmission += 0.4f + (SustainActiveGlowBoost * sustainPulse01);
            }

            ApplyMaterialColor(view.sustainMaterial, sustainColor, sustainEmission);
        }
    }

    private void ClearRemovedNotes(MultiplayerPlayerScene scene)
    {
        scene.removalBuffer.Clear();
        foreach (KeyValuePair<int, MultiplayerNoteView> pair in scene.noteViews)
        {
            if (!scene.visibleNoteIds.Contains(pair.Key))
                scene.removalBuffer.Add(pair.Key);
        }

        for (int i = 0; i < scene.removalBuffer.Count; i++)
        {
            int noteId = scene.removalBuffer[i];
            if (!scene.noteViews.TryGetValue(noteId, out MultiplayerNoteView view))
                continue;

            if (view.root != null)
                Object.Destroy(view.root);
            scene.noteViews.Remove(noteId);
        }
    }

    private void UpdateAccentView(MultiplayerNoteView view, ArcadeNoteData note, Vector3 bodyScale, float bodyLocalZ, bool showBody)
    {
        if (view?.accent == null)
            return;

        bool showAccent = showBody && note.noteType != ArcadeNoteType.Strum;
        if (view.outline != null)
            view.outline.SetActive(showAccent);
        view.accent.SetActive(showAccent);
        if (!showAccent)
            return;

        float outlineWidthScale = note.isOpen ? 1.07f : 1.08f;
        float outlineHeightScale = note.isOpen ? 1.22f : 1.16f;
        float outlineDepthScale = note.isOpen ? 1.12f : 1.10f;
        float accentWidthScale = note.isOpen ? 1.13f : 1.17f;
        float accentHeightScale = note.isOpen ? 1.44f : 1.34f;
        float accentDepthScale = note.isOpen ? 1.18f : 1.22f;
        float minimumOutlineHeightPadding = 0.035f;
        float minimumAccentHeightPadding = 0.07f;

        if (note.noteType == ArcadeNoteType.Hopo)
        {
            float gap = owner != null ? owner.GetRhythmHopoOutlineGap() : 0f;
            outlineWidthScale += gap;
            outlineHeightScale += gap * 1.35f;
            outlineDepthScale += gap * 0.85f;
            accentWidthScale += gap * 1.85f;
            accentHeightScale += gap * 2.20f;
            accentDepthScale += gap * 1.30f;
            minimumOutlineHeightPadding += gap * 0.55f;
            minimumAccentHeightPadding += gap * 0.95f;
        }

        if (view.outline != null)
        {
            view.outline.transform.localPosition = new Vector3(0f, 0f, bodyLocalZ);
            view.outline.transform.localScale = new Vector3(
                bodyScale.x * outlineWidthScale,
                Mathf.Max(bodyScale.y * outlineHeightScale, bodyScale.y + minimumOutlineHeightPadding),
                bodyScale.z * outlineDepthScale);
        }

        view.accent.transform.localPosition = new Vector3(0f, 0f, bodyLocalZ);
        view.accent.transform.localScale = new Vector3(
            bodyScale.x * accentWidthScale,
            Mathf.Max(bodyScale.y * accentHeightScale, bodyScale.y + minimumAccentHeightPadding),
            bodyScale.z * accentDepthScale);
    }

    private void CreateFeedbackBurst(MultiplayerPlayerScene scene, float x, float width, Color color, bool hit, float precision, bool openNote)
    {
        if (scene?.gameplayRoot == null || owner == null)
            return;

        MultiplayerFeedbackEffect effect = new MultiplayerFeedbackEffect
        {
            root = new GameObject(hit ? $"P{scene.playerIndex + 1}HitFeedback" : $"P{scene.playerIndex + 1}MissFeedback"),
            startTime = Time.time,
            duration = hit ? Mathf.Lerp(0.22f, 0.38f, precision) : 0.26f,
            miss = !hit
        };
        effect.root.transform.SetParent(scene.gameplayRoot.transform, false);
        effect.root.transform.localPosition = new Vector3(x, EndpointY + (hit ? 0.10f : 0.08f), owner.StrikeLineZ - 0.035f);

        Color hitHighlightColor = new Color(0.92f, 0.99f, 1f, 1f);
        Color missColor = owner.highwayMissColor;
        Color coreColor = hit ? Color.Lerp(color, hitHighlightColor, 0.68f + precision * 0.18f) : missColor;
        CreateFeedbackPiece(
            scene,
            effect,
            "CorePulse",
            new Color(coreColor.r, coreColor.g, coreColor.b, hit ? 0.74f : 0.58f),
            Vector3.zero,
            new Vector3(Mathf.Max(0.18f, width), hit ? 0.070f : 0.050f, openNote ? 0.42f : 0.28f),
            Vector3.zero,
            hit ? Mathf.Lerp(1.2f, 2.0f, precision) : 0.8f,
            Vector3.zero);

        if (hit)
        {
            CreateFeedbackPiece(
                scene,
                effect,
                "JudgeFlash",
                new Color(hitHighlightColor.r, hitHighlightColor.g, hitHighlightColor.b, 0.88f),
                new Vector3(0f, 0.01f, 0f),
                new Vector3(Mathf.Max(0.14f, width * 0.62f), 0.040f, openNote ? 0.26f : 0.16f),
                Vector3.zero,
                Mathf.Lerp(1.8f, 2.8f, precision),
                Vector3.zero);

            CreateFeedbackPiece(
                scene,
                effect,
                "LaneHalo",
                new Color(color.r, color.g, color.b, 0.44f),
                Vector3.zero,
                new Vector3(Mathf.Max(0.18f, width * 1.08f), 0.030f, openNote ? 0.52f : 0.34f),
                Vector3.zero,
                Mathf.Lerp(1.1f, 1.8f, precision),
                Vector3.zero);
        }
        else
        {
            CreateFeedbackPiece(
                scene,
                effect,
                "MissSlash",
                new Color(missColor.r, missColor.g * 0.52f, missColor.b * 0.52f, 0.84f),
                new Vector3(0f, -0.02f, 0f),
                new Vector3(Mathf.Max(0.10f, width * 0.16f), 0.22f, 0.08f),
                new Vector3(0f, -0.08f, -0.06f),
                -0.18f,
                new Vector3(0f, 0f, 220f));
        }

        int sparkCount = hit ? (openNote ? 12 : 8) : 5;
        float sparkWidth = Mathf.Max(0.12f, width * (openNote ? 0.18f : 0.12f));
        for (int i = 0; i < sparkCount; i++)
        {
            float normalized = sparkCount <= 1 ? 0f : (i / (sparkCount - 1f)) - 0.5f;
            float seed = (i + 1) * 1.618f + width * 0.37f;
            float side = openNote ? normalized * width * 0.80f : Mathf.Sin(seed) * width * 0.48f;
            float upward = hit ? Mathf.Lerp(0.34f, 0.74f, precision) + (Mathf.Sin(seed * 2.1f) * 0.08f) : 0.12f + Mathf.Abs(Mathf.Sin(seed)) * 0.18f;
            float back = hit ? 0.38f + Mathf.Cos(seed) * 0.16f : -0.18f + Mathf.Sin(seed) * 0.08f;
            Vector3 velocity = new Vector3(side * (hit ? 1.25f : 0.55f), upward, back);
            Vector3 start = new Vector3(openNote ? normalized * width * 0.36f : Mathf.Sin(seed * 0.7f) * width * 0.12f, 0.035f, 0f);
            Color sparkColor = hit ? Color.Lerp(color, hitHighlightColor, 0.45f + precision * 0.32f) : missColor;
            CreateFeedbackPiece(
                scene,
                effect,
                hit ? "Spark" : "MissShard",
                new Color(sparkColor.r, sparkColor.g, sparkColor.b, hit ? 0.82f : 0.66f),
                start,
                new Vector3(sparkWidth, hit ? 0.035f : 0.028f, hit ? 0.09f : 0.16f),
                velocity,
                hit ? -0.35f : 0.10f,
                new Vector3(0f, Mathf.Sin(seed) * 280f, Mathf.Cos(seed) * 220f));
        }

        scene.feedbackEffects.Add(effect);
    }

    private void CreateFeedbackPiece(
        MultiplayerPlayerScene scene,
        MultiplayerFeedbackEffect effect,
        string name,
        Color color,
        Vector3 localPosition,
        Vector3 scale,
        Vector3 velocity,
        float expand,
        Vector3 spin)
    {
        if (scene == null || effect == null || effect.root == null || owner == null)
            return;

        GameObject pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pieceObject.name = name;
        pieceObject.transform.SetParent(effect.root.transform, false);
        pieceObject.transform.localPosition = localPosition;
        pieceObject.transform.localScale = scale;
        Object.Destroy(pieceObject.GetComponent<Collider>());
        SetLayerRecursively(pieceObject, scene.layer);

        Material material = owner.CreateSharedTransparentMaterial(color, 0.08f);
        ConfigureOverlayMaterial(material, effect.miss ? 175 : 185, true);
        Renderer renderer = pieceObject.GetComponent<Renderer>();
        renderer.material = material;
        ApplyMaterialColor(material, color, effect.miss ? 1.0f : 2.2f);

        effect.pieces.Add(new MultiplayerFeedbackPiece
        {
            transform = pieceObject.transform,
            material = material,
            color = color,
            startLocalPosition = localPosition,
            velocity = velocity,
            baseScale = scale,
            spin = spin,
            expand = expand
        });
    }

    private void UpdateFeedbackEffects(MultiplayerPlayerScene scene)
    {
        if (scene == null)
            return;

        for (int i = scene.feedbackEffects.Count - 1; i >= 0; i--)
        {
            MultiplayerFeedbackEffect effect = scene.feedbackEffects[i];
            if (effect == null || effect.root == null)
            {
                scene.feedbackEffects.RemoveAt(i);
                continue;
            }

            float age = Time.time - effect.startTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, effect.duration));
            float fade = 1f - SmoothStep01(t);
            float moveEase = EaseOutCubic(t);

            for (int pieceIndex = 0; pieceIndex < effect.pieces.Count; pieceIndex++)
            {
                MultiplayerFeedbackPiece piece = effect.pieces[pieceIndex];
                if (piece?.transform == null)
                    continue;

                Vector3 gravity = effect.miss ? Vector3.down * 0.10f * age * age : Vector3.down * 0.24f * age * age;
                piece.transform.localPosition = piece.startLocalPosition + (piece.velocity * moveEase) + gravity;
                piece.transform.localScale = piece.baseScale * Mathf.Max(0.01f, 1f + piece.expand * moveEase);
                piece.transform.Rotate(piece.spin * Time.deltaTime, Space.Self);

                Color color = piece.color;
                color.a *= fade;
                ApplyMaterialColor(piece.material, color, effect.miss ? fade * 1.4f : fade * 2.6f);
            }

            if (t >= 1f)
            {
                Object.Destroy(effect.root);
                scene.feedbackEffects.RemoveAt(i);
            }
        }
    }

    private void ApplyMaterialColor(Material material, Color color, float emission)
    {
        if (material == null)
            return;

        material.color = color;
        material.SetColor("_Color", color);
        material.SetColor("_BaseColor", color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission > 0f ? color * Mathf.Pow(2f, emission) : Color.black);
    }

    private Material CreateTransparentGlowMaterial(Color color, float emission)
    {
        Material material = owner.CreateSharedRuntimeTransparentGlowMaterial(color, emission);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        return material;
    }

    private void ConfigureOverlayMaterial(Material material, int renderQueueOffset, bool disableCulling)
    {
        if (material == null)
            return;

        material.renderQueue = Mathf.Clamp((int)RenderQueue.Transparent + renderQueueOffset, 0, 5000);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (disableCulling && material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
    }

    private float GetVisualNoteSpeed(GuitarGameplaySnapshot snapshot)
    {
        float spacingScale = snapshot != null
            ? Mathf.Clamp(snapshot.tabSpeedOffsetPercent / 100f, 0.5f, 1.5f)
            : 1f;
        return Mathf.Max(0.01f, owner.noteSpeed * spacingScale);
    }

    private int GetLaneCount()
    {
        return Mathf.Clamp(owner != null ? owner.ArcadeLaneCount : DefaultLaneCount, 1, MaxLaneCount);
    }

    private float GetLaneGuideDepth()
    {
        if (owner == null)
            return 120f;

        return Mathf.Max(60f, owner.ArcadeSpawnZ - owner.StrikeLineZ + 12f);
    }

    private float GetLaneWidth()
    {
        return Mathf.Max(0.01f, owner != null ? owner.FretSpacing : 1f);
    }

    private float GetTrackWidth(int laneCount)
    {
        return Mathf.Max(GetLaneWidth(), laneCount * GetLaneWidth());
    }

    private float GetTrackHorizontalOffset(int playerIndex)
    {
        float fallbackTrackWidth = GetTrackWidth(GetLaneCount());
        float fallbackSpacing = fallbackTrackWidth * TrackHorizontalSpacingMultiplier;
        float fallback = playerIndex <= 0 ? -fallbackSpacing : fallbackSpacing;
        if (mainCamera == null || owner == null)
            return fallback;

        Vector3 strikeViewport = mainCamera.WorldToViewportPoint(new Vector3(0f, EndpointY, owner.StrikeLineZ));
        float halfSpread = owner != null ? Mathf.Clamp(owner.multiplayerHighwayHalfSpread, 0.08f, 0.38f) : 0.23f;
        float targetViewportX = playerIndex <= 0 ? 0.5f - halfSpread : 0.5f + halfSpread;
        float targetViewportY = Mathf.Clamp01(strikeViewport.y);
        Ray ray = mainCamera.ViewportPointToRay(new Vector3(targetViewportX, targetViewportY, 0f));
        if (Mathf.Abs(ray.direction.z) < 0.0001f)
            return fallback;

        float distance = (owner.StrikeLineZ - ray.origin.z) / ray.direction.z;
        if (distance <= 0f)
            return fallback;

        Vector3 hitPoint = ray.origin + (ray.direction * distance);
        return hitPoint.x;
    }

    private float GetLaneX(int lane)
    {
        int laneCount = GetLaneCount();
        float laneWidth = GetLaneWidth();
        return ((Mathf.Clamp(lane, 0, laneCount - 1) + 0.5f) * laneWidth) - (laneCount * laneWidth * 0.5f);
    }

    private float GetBoundaryX(int boundary)
    {
        int laneCount = GetLaneCount();
        float laneWidth = GetLaneWidth();
        return (Mathf.Clamp(boundary, 0, laneCount) * laneWidth) - (laneCount * laneWidth * 0.5f);
    }

    private Vector3 GetFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.56f,
            0.44f * Mathf.Max(0.2f, owner.highwayNoteHeightScale),
            Mathf.Max(0.48f, owner.FretSpacing * 0.28f));
    }

    private Vector3 GetOpenNoteScale(int laneCount)
    {
        return new Vector3(
            Mathf.Max(owner.FretSpacing * 0.8f, GetTrackWidth(laneCount) - owner.FretSpacing * 0.2f),
            0.20f * Mathf.Max(0.2f, owner.highwayNoteHeightScale),
            Mathf.Max(0.36f, owner.FretSpacing * 0.22f));
    }

    private Color GetLaneColor(int lane)
    {
        switch (lane)
        {
            case 0: return owner.GetStringColor(4);
            case 1: return owner.GetStringColor(0);
            case 2: return owner.GetStringColor(1);
            case 3: return owner.GetStringColor(2);
            case 4: return owner.GetStringColor(3);
            default: return Color.white;
        }
    }

    private Color GetNoteBaseColor(ArcadeNoteData note)
    {
        if (note.isOpen)
            return new Color(0.70f, 0.38f, 1f, 1f);

        Color laneColor = GetLaneColor(note.lane);
        if (note.noteType == ArcadeNoteType.Tap)
            return Color.Lerp(laneColor, Color.white, 0.18f);

        return laneColor;
    }

    private Color GetNoteAccentColor(ArcadeNoteData note)
    {
        if (note.noteType == ArcadeNoteType.Tap)
            return new Color(1f, 0.42f, 1f, 1f);

        if (note.noteType == ArcadeNoteType.Hopo)
            return owner != null ? owner.GetRhythmHopoAccentColor() : Color.white;

        return GetNoteBaseColor(note);
    }

    private Material CreateLaneSurfaceMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayLaneFloorFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayLaneFloorFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.025f, 0.03f, 0.045f, 0.14f), 0f);

        mat.renderQueue = (int)RenderQueue.Transparent - 40;
        Color color = new Color(0.025f, 0.03f, 0.045f, 0.14f);
        mat.SetColor("_Color", color);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_TintColor", color);
        if (mat.HasProperty("_EdgeFadeLeft"))
            mat.SetFloat("_EdgeFadeLeft", 0.008f);
        if (mat.HasProperty("_EdgeFadeRight"))
            mat.SetFloat("_EdgeFadeRight", 0.008f);
        if (mat.HasProperty("_FrontBackFade"))
            mat.SetFloat("_FrontBackFade", 0.1f);
        return mat;
    }

    private Material CreateLaneGuideMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayLaneGuideFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayLaneGuideFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.12f, 0.26f, 0.55f, 0.85f), 0.15f);

        Color color = new Color(0.12f, 0.26f, 0.55f, 0.70f);
        mat.SetColor("_Color", color);
        mat.SetColor("_BaseColor", color);
        mat.SetColor("_TintColor", color);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", new Color(0.10f, 0.26f, 0.55f, 1f) * Mathf.Pow(2f, 0.2f));
        ConfigureOverlayMaterial(mat, 60, true);
        return mat;
    }

    private static float EaseOutCubic(float value)
    {
        value = Mathf.Clamp01(value);
        float inverse = 1f - value;
        return 1f - inverse * inverse * inverse;
    }

    private static float SmoothStep01(float value)
    {
        value = Mathf.Clamp01(value);
        return value * value * (3f - 2f * value);
    }

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }
}
