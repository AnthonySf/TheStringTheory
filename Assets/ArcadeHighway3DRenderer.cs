using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

public sealed class ArcadeHighway3DRenderer : IGuitarGameplayRenderer
{
    private static readonly ProfilerMarker RenderProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.Render");
    private static readonly ProfilerMarker UpdateNotesProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateNotes");
    private static readonly ProfilerMarker GetRenderSongTimeProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.GetRenderSongTime");

    private const int DefaultLaneCount = 5;
    private const int MaxLaneCount = 8;
    private const float LaneBackOverhang = 8f;
    private const float LaneGuideDepth = 150f;
    private const float LaneSurfaceY = 1.02f;
    private const float NoteY = 1.30f;
    private const float EndpointY = 1.36f;
    private const float EndpointDepth = 0.34f;
    private const int BackgroundLayer = 2;
    private const float SustainActivePulseSpeed = 9.2f;
    private const float SustainActiveGlowBoost = 1.55f;
    private const float SustainActiveWidthScale = 1.12f;
    private const float SustainActiveHeightScale = 1.38f;
    private const float HitPulseDuration = 0.34f;
    private const float MissPulseDuration = 0.28f;
    private const float MaxFeedbackTriggerAge = 0.25f;
    private const float HighwayCharacterViewportMarginX = 0.035f;
    private const float HighwayCharacterViewportMarginY = 0.035f;
    private const float HighwayCharacterDepth = 44f;
    private const float HighwayCharacterHeightViewportFraction = 0.375f;
    private const float HighwayCharacterViewportCenterY = 0.57f;
    private const float HighwayCharacterScaleMultiplier = 1.1f;
    private const float HighwayCharacterBottomFadeStart01 = 0.22f;
    private const float HighwayCharacterBottomFadeEnd01 = 0.12f;
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
    private static readonly Color HighwayCharacterMissFlashColor = new Color(1f, 0.34f, 0.10f, 1f);
    private const float HighwayCharacterMissFlashBandSpeed = 14f;
    private const int HighwayCharacterMissParticleBurstCount = 28;
    private const int HighwayCharacterMissAuraParticleBurstCount = 11;
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
    private static readonly Color HighwayCharacterMissParticleColor = WithAlpha(new Color(0.98f, 0.43f, 0.14f, 1f), 0.95f);
    private static readonly Color HighwayCharacterMissParticleEdgeColor = WithAlpha(new Color(0.98f, 0.43f, 0.14f, 1f), 1f);
    private const float HighwayCharacterMissParticleGlow = 1.55f;
    private static readonly Color HighwayCharacterMissAuraParticleColor = WithAlpha(new Color(0.98f, 0.43f, 0.14f, 1f), 0.68f);
    private static readonly Color HighwayCharacterMissAuraParticleEdgeColor = WithAlpha(new Color(0.98f, 0.43f, 0.14f, 1f), 0.9f);
    private const float HighwayCharacterMissAuraParticleGlow = 1.1f;
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
    private static readonly int CharacterMissFlashColorShaderId = Shader.PropertyToID("_MissFlashColor");
    private static readonly int CharacterMissFlashStrengthShaderId = Shader.PropertyToID("_MissFlashStrength");
    private static readonly int CharacterMissFlashSpeedShaderId = Shader.PropertyToID("_MissFlashSpeed");

    private readonly Dictionary<int, ArcadeNoteView> noteViews = new Dictionary<int, ArcadeNoteView>();
    private readonly Dictionary<int, GameplayNoteResult> resolvedFeedbackResults = new Dictionary<int, GameplayNoteResult>();
    private readonly HashSet<int> visibleNoteIds = new HashSet<int>();
    private readonly List<int> removalBuffer = new List<int>();
    private readonly List<ArcadeFeedbackEffect> feedbackEffects = new List<ArcadeFeedbackEffect>();
    private readonly List<HighwayCharacterBopEvent> highwayCharacterBopEvents = new List<HighwayCharacterBopEvent>();
    private readonly Material[] laneMaterials = new Material[MaxLaneCount];
    private readonly Material[] endpointMaterials = new Material[MaxLaneCount];
    private readonly Renderer[] endpointRenderers = new Renderer[MaxLaneCount];
    private readonly Renderer[] laneRenderers = new Renderer[MaxLaneCount];
    private readonly float[] lanePulseUntil = new float[MaxLaneCount];
    private readonly float[] lanePulseStrength = new float[MaxLaneCount];

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private Camera backgroundCamera;
    private GameObject root;
    private GameObject gameplayRoot;
    private GameObject backgroundRoot;
    private GameObject characterRoot;
    private Transform highwayCharacterTransform;
    private Renderer highwayCharacterRenderer;
    private Renderer highwayCharacterPortalBackRenderer;
    private Renderer highwayCharacterPortalFrontRenderer;
    private Material sharedHighwayCharacterMaterial;
    private Material sharedHighwayCharacterPortalBackMaterial;
    private Material sharedHighwayCharacterPortalFrontMaterial;
    private Material sharedHighwayCharacterMissParticleMaterial;
    private Material sharedHighwayCharacterMissAuraParticleMaterial;
    private ParticleSystem highwayCharacterMissParticles;
    private ParticleSystem highwayCharacterMissAuraParticles;
    private Texture2D highwayCharacterTexture;
    private float highwayCharacterAspect = 1f;
    private int highwayCharacterSourcePixelWidth = 1;
    private int highwayCharacterSourcePixelHeight = 1;
    private float highwayCharacterManualLocalXOffset;
    private float highwayCharacterManualLocalYOffset;
    private Vector2 highwayCharacterTextureScale = Vector2.one;
    private Vector2 highwayCharacterTextureOffset = Vector2.zero;
    private HighwayCharacterChoice loadedHighwayCharacterChoice = HighwayCharacterChoice.Hero;
    private ITabsBackgroundEffect backgroundEffect;
    private TabsSongHeaderOverlay songHeaderOverlay;
    private CameraClearFlags originalMainCameraClearFlags;
    private Color originalMainCameraBackgroundColor;
    private int originalMainCameraCullingMask = -1;
    private float originalMainCameraDepth;
    private bool originalMainCameraOrthographic;
    private bool gameplayBuilt;
    private float currentVisualNoteSpeed = 12f;
    private int builtLaneCount = DefaultLaneCount;
    private float missShakeUntil;
    private float missShakeSeed;
    private int lastObservedHighwayCharacterMissCount = -1;
    private float lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
    private int lastHighwayCharacterBopSourceNoteCount = -1;

    private struct HighwayCharacterBopEvent
    {
        public float time;
        public float strength;
    }

    private sealed class ArcadeNoteView
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

    private sealed class ArcadeFeedbackEffect
    {
        public GameObject root;
        public readonly List<ArcadeFeedbackPiece> pieces = new List<ArcadeFeedbackPiece>();
        public float startTime;
        public float duration;
        public bool miss;
    }

    private sealed class ArcadeFeedbackPiece
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

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("ArcadeHighway3DRendererRoot");
        backgroundRoot = new GameObject("ArcadeHighway3DBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        characterRoot = new GameObject("ArcadeHighway3DCharacterRoot");
        characterRoot.transform.SetParent(root.transform, false);
        gameplayRoot = new GameObject("ArcadeHighway3DGameplayRoot");
        gameplayRoot.transform.SetParent(root.transform, false);

        if (mainCamera != null)
        {
            originalMainCameraClearFlags = mainCamera.clearFlags;
            originalMainCameraBackgroundColor = mainCamera.backgroundColor;
            originalMainCameraCullingMask = mainCamera.cullingMask;
            originalMainCameraDepth = mainCamera.depth;
            originalMainCameraOrthographic = mainCamera.orthographic;
        }
        lastObservedHighwayCharacterMissCount = -1;
        lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
        lastHighwayCharacterBopSourceNoteCount = -1;
        highwayCharacterBopEvents.Clear();

        InitializeBackgroundCamera();
        InitializeBackgroundEffect();
        ConfigureCamera();
        InitializeHighwayCharacter();
        songHeaderOverlay = new TabsSongHeaderOverlay(owner);
        gameplayBuilt = false;
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        if (root != null)
            Object.Destroy(root);

        noteViews.Clear();
        resolvedFeedbackResults.Clear();
        feedbackEffects.Clear();
        highwayCharacterBopEvents.Clear();
        ResetLanePulses();
        gameplayBuilt = false;
        Initialize(owner, chartNotes, sections);
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || mainCamera == null)
            return;

        using (RenderProfilerMarker.Auto())
        {
            bool suppressGameplay = snapshot.mainMenuFlowActive;
            ConfigureCamera();
            EnsureGameplayVisualsBuilt();
            if (gameplayRoot != null && gameplayRoot.activeSelf == suppressGameplay)
                gameplayRoot.SetActive(!suppressGameplay);

            if (!suppressGameplay)
                UpdateBackgroundPlacement();

            if (!suppressGameplay)
            {
                UpdateHighwayCharacter(snapshot, suppressGameplay);
                UpdateResolvedFeedback(snapshot);
                UpdateGameplayRootShake();
                UpdateLaneVisuals(snapshot);
                UpdateNotes(snapshot);
                UpdateFeedbackEffects();
            }
            else
            {
                UpdateHighwayCharacter(snapshot, suppressGameplay);
                ResetGameplayRootShake();
            }

            backgroundEffect?.Tick(Time.deltaTime);
            songHeaderOverlay?.UpdateFromSnapshot(snapshot);
        }
    }

    public void DisposeRenderer()
    {
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;

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

        noteViews.Clear();
        resolvedFeedbackResults.Clear();
        feedbackEffects.Clear();
        highwayCharacterBopEvents.Clear();
        ResetLanePulses();
        sharedHighwayCharacterMaterial = null;
        if (sharedHighwayCharacterPortalBackMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterPortalBackMaterial);
            sharedHighwayCharacterPortalBackMaterial = null;
        }
        if (sharedHighwayCharacterPortalFrontMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterPortalFrontMaterial);
            sharedHighwayCharacterPortalFrontMaterial = null;
        }
        if (sharedHighwayCharacterMissParticleMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterMissParticleMaterial);
            sharedHighwayCharacterMissParticleMaterial = null;
        }
        if (sharedHighwayCharacterMissAuraParticleMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterMissAuraParticleMaterial);
            sharedHighwayCharacterMissAuraParticleMaterial = null;
        }
        highwayCharacterRenderer = null;
        highwayCharacterPortalBackRenderer = null;
        highwayCharacterPortalFrontRenderer = null;
        highwayCharacterMissParticles = null;
        highwayCharacterMissAuraParticles = null;
        highwayCharacterTransform = null;
        highwayCharacterTexture = null;
    }

    private void InitializeBackgroundEffect()
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: true);
        if (backgroundEffect != null && backgroundRoot != null)
        {
            backgroundEffect.Initialize(backgroundRoot.transform, owner);
            SetLayerRecursively(backgroundRoot, BackgroundLayer);
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

    private void InitializeHighwayCharacter()
    {
        if (characterRoot == null)
            return;

        if (!TryLoadCurrentHighwayCharacterTexture())
            return;

        GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        characterObject.name = "ArcadeHighwayCharacter";
        characterObject.transform.SetParent(characterRoot.transform, false);
        highwayCharacterTransform = characterObject.transform;
        Object.Destroy(characterObject.GetComponent<Collider>());

        highwayCharacterRenderer = characterObject.GetComponent<Renderer>();
        highwayCharacterRenderer.sharedMaterial = GetHighwayCharacterMaterial();
        highwayCharacterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        highwayCharacterRenderer.receiveShadows = false;
        InitializeHighwayCharacterMissParticles();
        highwayCharacterPortalBackRenderer = CreateHighwayCharacterPortalRenderer(
            "ArcadeHighwayCharacterPortalBack",
            GetHighwayCharacterPortalBackMaterial(),
            HighwayCharacterPortalBackForwardOffset);
        highwayCharacterPortalFrontRenderer = CreateHighwayCharacterPortalRenderer(
            "ArcadeHighwayCharacterPortalFront",
            GetHighwayCharacterPortalFrontMaterial(),
            HighwayCharacterPortalFrontForwardOffset);

        SetLayerRecursively(characterRoot, 0);
        characterRoot.SetActive(false);
    }

    private void UpdateHighwayCharacter(GuitarGameplaySnapshot snapshot, bool suppressGameplay)
    {
        if (characterRoot == null)
            return;

        EnsureHighwayCharacterTextureCurrent();

        bool shouldShow = !suppressGameplay &&
                          snapshot != null &&
                          snapshot.showHighwayCharacter &&
                          mainCamera != null &&
                          highwayCharacterRenderer != null &&
                          highwayCharacterTexture != null;
        if (characterRoot.activeSelf != shouldShow)
            characterRoot.SetActive(shouldShow);

        if (!shouldShow)
        {
            UpdateHighwayCharacterPortalVisuals(false);
            ResetHighwayCharacterAnimation();
            return;
        }

        SyncHighwayCharacterHudLayoutState();
        Rect viewportRect = HighwayCharacterVisualUtility.ComputeViewportRect(
            mainCamera.pixelWidth,
            mainCamera.pixelHeight,
            highwayCharacterAspect,
            highwayCharacterSourcePixelWidth,
            highwayCharacterSourcePixelHeight,
            HighwayCharacterViewportMarginX,
            HighwayCharacterViewportMarginY,
            HighwayCharacterHeightViewportFraction,
            HighwayCharacterViewportCenterY,
            owner != null ? owner.highwayCharacterScale : 1f,
            owner != null ? owner.highwayCharacterRigOffsetX : 0f,
            owner != null ? owner.highwayCharacterRigOffsetY : 0f,
            true);
        highwayCharacterManualLocalXOffset = HighwayCharacterVisualUtility.ComputeCharacterLocalXOffset(
            viewportRect.width,
            owner != null ? owner.highwayCharacterOffsetX : 0f,
            HighwayCharacterScaleMultiplier);
        highwayCharacterManualLocalYOffset = HighwayCharacterVisualUtility.ComputeCharacterLocalYOffset(
            viewportRect.height,
            owner != null ? owner.highwayCharacterOffsetY : 0f,
            HighwayCharacterScaleMultiplier);

        Vector3 lowerLeft = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMin, HighwayCharacterDepth));
        Vector3 lowerRight = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMin, HighwayCharacterDepth));
        Vector3 upperLeft = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMin, viewportRect.yMax, HighwayCharacterDepth));
        Vector3 upperRight = mainCamera.ViewportToWorldPoint(new Vector3(viewportRect.xMax, viewportRect.yMax, HighwayCharacterDepth));
        float targetWidth = Vector3.Distance(lowerLeft, lowerRight);
        float targetHeight = Vector3.Distance(lowerLeft, upperLeft);
        Vector3 worldPosition = (lowerLeft + upperRight) * 0.5f;

        characterRoot.transform.position = worldPosition;
        characterRoot.transform.rotation = mainCamera.transform.rotation;
        characterRoot.transform.localScale = new Vector3(
            targetWidth * HighwayCharacterScaleMultiplier,
            targetHeight * HighwayCharacterScaleMultiplier,
            1f);
        ApplyHighwayCharacterVerticalCompensation();
        UpdateHighwayCharacterPortalVisuals(true);

        UpdateHighwayCharacterAnimation(snapshot);
    }

    private void SyncHighwayCharacterHudLayoutState()
    {
        HighwayCharacterVisualUtility.SetCurrentHudLayout(
            highwayCharacterAspect,
            highwayCharacterSourcePixelWidth,
            highwayCharacterSourcePixelHeight,
            owner != null ? owner.highwayCharacterScale : 1f,
            (owner != null ? owner.highwayCharacterRigOffsetX : 0f) + (owner != null ? owner.highwayCharacterOffsetX : 0f),
            (owner != null ? owner.highwayCharacterRigOffsetY : 0f) + (owner != null ? owner.highwayCharacterOffsetY : 0f));
    }

    private void EnsureHighwayCharacterTextureCurrent()
    {
        HighwayCharacterChoice targetChoice = owner != null ? owner.SelectedHighwayCharacterChoice : HighwayCharacterChoice.Hero;
        if (targetChoice == loadedHighwayCharacterChoice && highwayCharacterTexture != null)
            return;

        TryLoadCurrentHighwayCharacterTexture();
    }

    private bool TryLoadCurrentHighwayCharacterTexture()
    {
        HighwayCharacterChoice targetChoice = owner != null ? owner.SelectedHighwayCharacterChoice : HighwayCharacterChoice.Hero;
        if (!HighwayCharacterVisualUtility.TryLoadTextureData(targetChoice, out HighwayCharacterTextureData characterData) ||
            characterData.texture == null)
            return false;

        loadedHighwayCharacterChoice = targetChoice;
        highwayCharacterTexture = characterData.texture;
        highwayCharacterAspect = characterData.aspect;
        highwayCharacterSourcePixelWidth = characterData.sourcePixelWidth;
        highwayCharacterSourcePixelHeight = characterData.sourcePixelHeight;
        highwayCharacterTextureScale = characterData.textureScale;
        highwayCharacterTextureOffset = characterData.textureOffset;
        SyncHighwayCharacterHudLayoutState();
        ApplyHighwayCharacterTextureToMaterial(sharedHighwayCharacterMaterial);
        if (highwayCharacterRenderer != null && sharedHighwayCharacterMaterial != null)
            highwayCharacterRenderer.sharedMaterial = sharedHighwayCharacterMaterial;
        return true;
    }

    private void ApplyHighwayCharacterTextureToMaterial(Material material)
    {
        if (material == null || highwayCharacterTexture == null)
            return;

        material.mainTexture = highwayCharacterTexture;
        material.SetTexture("_MainTex", highwayCharacterTexture);
        material.mainTextureScale = highwayCharacterTextureScale;
        material.mainTextureOffset = highwayCharacterTextureOffset;
        material.SetTextureScale("_MainTex", highwayCharacterTextureScale);
        material.SetTextureOffset("_MainTex", highwayCharacterTextureOffset);
    }

    private void ApplyHighwayCharacterVerticalCompensation()
    {
        HighwayCharacterVisualUtility.ComputeVerticalCompensation(
            highwayCharacterManualLocalYOffset,
            HighwayCharacterBottomFadeStart01,
            HighwayCharacterBottomFadeEnd01,
            owner != null ? owner.highwayCharacterFadeSoftness : (HighwayCharacterBottomFadeStart01 - HighwayCharacterBottomFadeEnd01),
            HighwayCharacterPortalLocalYInCharacterHeights,
            out float fadeStart,
            out float fadeEnd,
            out float portalLocalY);

        if (sharedHighwayCharacterMaterial != null)
        {
            if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeStartShaderId))
                sharedHighwayCharacterMaterial.SetFloat(CharacterFadeStartShaderId, fadeStart);
            if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeEndShaderId))
                sharedHighwayCharacterMaterial.SetFloat(CharacterFadeEndShaderId, fadeEnd);
        }

        if (highwayCharacterPortalBackRenderer != null)
        {
            Vector3 position = highwayCharacterPortalBackRenderer.transform.localPosition;
            position.y = portalLocalY;
            highwayCharacterPortalBackRenderer.transform.localPosition = position;
        }

        if (highwayCharacterPortalFrontRenderer != null)
        {
            Vector3 position = highwayCharacterPortalFrontRenderer.transform.localPosition;
            position.y = portalLocalY;
            highwayCharacterPortalFrontRenderer.transform.localPosition = position;
        }
    }

    private void UpdateHighwayCharacterAnimation(GuitarGameplaySnapshot snapshot)
    {
        if (highwayCharacterTransform == null)
            return;

        if (snapshot == null ||
            snapshot.isPaused ||
            characterRoot == null ||
            !characterRoot.activeSelf)
        {
            ResetHighwayCharacterAnimation();
            return;
        }

        bool movementEnabled = owner == null || owner.highwayCharacterMovementEnabled;
        bool missColorEnabled = owner == null || owner.highwayCharacterMissColorEnabled;

        if (movementEnabled)
            EnsureHighwayCharacterBopEvents(snapshot);
        float songTime = snapshot.songTime;
        UpdateHighwayCharacterMissState(snapshot);

        float missAge = songTime - lastHighwayCharacterMissTriggerSongTime;
        float missStrength = GetHighwayCharacterMissStrength(missAge);
        float missFlashWave = 0.62f + (((Mathf.Sin((missAge * HighwayCharacterMissFlashBandSpeed) + 0.85f) * 0.5f) + 0.5f) * 0.38f);
        ApplyHighwayCharacterMissMaterialState(missColorEnabled ? missStrength * missFlashWave : 0f);

        if (!movementEnabled)
        {
            highwayCharacterTransform.localPosition = new Vector3(highwayCharacterManualLocalXOffset, highwayCharacterManualLocalYOffset, 0f);
            highwayCharacterTransform.localRotation = Quaternion.identity;
            highwayCharacterTransform.localScale = Vector3.one;
            return;
        }

        float missMotionSuppression = 1f - Mathf.Clamp01(missStrength * 0.76f);
        int eventIndex = FindLastHighwayCharacterBopEventIndex(songTime);
        float lift = 0f;
        float squash = 0f;
        float stretch = 0f;
        float tilt = 0f;
        float bopPresence = 0f;

        for (int i = eventIndex; i >= 0 && i >= eventIndex - 2; i--)
        {
            HighwayCharacterBopEvent bopEvent = highwayCharacterBopEvents[i];
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
        highwayCharacterTransform.localPosition = new Vector3(highwayCharacterManualLocalXOffset + idleLocalX, highwayCharacterManualLocalYOffset + localLift, 0f);
        highwayCharacterTransform.localRotation = Quaternion.Euler(0f, 0f, rotationZ);
        highwayCharacterTransform.localScale = new Vector3(uniformScale, uniformScale, 1f);
    }

    private void ResetHighwayCharacterAnimation()
    {
        if (highwayCharacterTransform == null)
            return;

        highwayCharacterTransform.localPosition = new Vector3(highwayCharacterManualLocalXOffset, highwayCharacterManualLocalYOffset, 0f);
        highwayCharacterTransform.localRotation = Quaternion.identity;
        highwayCharacterTransform.localScale = Vector3.one;
        ClearHighwayCharacterMissFeedback();
    }

    private void EnsureHighwayCharacterBopEvents(GuitarGameplaySnapshot snapshot)
    {
        int noteCount = snapshot?.arcadeNoteStates?.Count ?? 0;
        if (noteCount == lastHighwayCharacterBopSourceNoteCount)
            return;

        lastHighwayCharacterBopSourceNoteCount = noteCount;
        BuildHighwayCharacterBopEvents(snapshot?.arcadeNoteStates);
    }

    private void BuildHighwayCharacterBopEvents(List<ArcadeNoteState> noteStates)
    {
        highwayCharacterBopEvents.Clear();

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

            AddHighwayCharacterBopEvent(groupTime, noteCount, hasTechnique);
            groupTime = note.time;
            noteCount = 1;
            hasTechnique = note.isHopo || note.isTap;
        }

        AddHighwayCharacterBopEvent(groupTime, noteCount, hasTechnique);
    }

    private void AddHighwayCharacterBopEvent(float time, int noteCount, bool hasTechnique)
    {
        float strength = 0.95f;
        strength += Mathf.Min(0.24f, Mathf.Max(0, noteCount - 1) * 0.08f);
        if (hasTechnique)
            strength += 0.06f;

        strength = Mathf.Clamp(strength, 0.9f, 1.3f);

        if (highwayCharacterBopEvents.Count > 0)
        {
            int lastIndex = highwayCharacterBopEvents.Count - 1;
            HighwayCharacterBopEvent previous = highwayCharacterBopEvents[lastIndex];
            if (time - previous.time < HighwayCharacterBopMinimumSpacingSeconds)
            {
                previous.time = Mathf.Lerp(previous.time, time, 0.35f);
                previous.strength = Mathf.Max(previous.strength, strength);
                highwayCharacterBopEvents[lastIndex] = previous;
                return;
            }
        }

        highwayCharacterBopEvents.Add(new HighwayCharacterBopEvent
        {
            time = time,
            strength = strength
        });
    }

    private void ClearHighwayCharacterMissFeedback()
    {
        lastObservedHighwayCharacterMissCount = 0;
        lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
        ApplyHighwayCharacterMissMaterialState(0f);
        if (highwayCharacterMissParticles != null)
            highwayCharacterMissParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        if (highwayCharacterMissAuraParticles != null)
            highwayCharacterMissAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
    }

    private void UpdateHighwayCharacterMissState(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        if (snapshot.loopEnabled)
        {
            ClearHighwayCharacterMissFeedback();
            return;
        }

        int missCount = Mathf.Max(0, snapshot.currentSessionScoreMisses);
        if (lastObservedHighwayCharacterMissCount < 0 || missCount < lastObservedHighwayCharacterMissCount)
        {
            lastObservedHighwayCharacterMissCount = missCount;
            if (missCount == 0)
                lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
            return;
        }

        if (missCount > lastObservedHighwayCharacterMissCount)
            TriggerHighwayCharacterMissFeedback(snapshot.songTime, missCount - lastObservedHighwayCharacterMissCount);

        lastObservedHighwayCharacterMissCount = missCount;
    }

    private float GetHighwayCharacterMissStrength(float missAge)
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

    private void TriggerHighwayCharacterMissFeedback(float songTime, int missDelta)
    {
        lastHighwayCharacterMissTriggerSongTime = songTime;
        if (owner != null && !owner.highwayCharacterMissParticlesEnabled)
            return;

        if (highwayCharacterMissParticles == null && highwayCharacterMissAuraParticles == null)
            return;

        int burstCount = Mathf.Clamp(HighwayCharacterMissParticleBurstCount + (Mathf.Max(0, missDelta - 1) * 6), HighwayCharacterMissParticleBurstCount, HighwayCharacterMissParticleBurstCount * 2);
        highwayCharacterMissParticles?.Play(true);
        highwayCharacterMissParticles?.Emit(burstCount);

        int auraBurstCount = Mathf.Clamp(HighwayCharacterMissAuraParticleBurstCount + (Mathf.Max(0, missDelta - 1) * 2), HighwayCharacterMissAuraParticleBurstCount, HighwayCharacterMissAuraParticleBurstCount * 2);
        highwayCharacterMissAuraParticles?.Play(true);
        highwayCharacterMissAuraParticles?.Emit(auraBurstCount);
    }

    private int FindLastHighwayCharacterBopEventIndex(float songTime)
    {
        int low = 0;
        int high = highwayCharacterBopEvents.Count - 1;
        int result = -1;

        while (low <= high)
        {
            int mid = low + ((high - low) / 2);
            if (highwayCharacterBopEvents[mid].time <= songTime + 0.0001f)
            {
                result = mid;
                low = mid + 1;
            }
            else
                high = mid - 1;
        }

        return result;
    }

    private Material GetHighwayCharacterMaterial()
    {
        if (sharedHighwayCharacterMaterial != null)
            return sharedHighwayCharacterMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterFade");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");

        sharedHighwayCharacterMaterial = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(Color.white, 0f);
        sharedHighwayCharacterMaterial.color = Color.white;
        ApplyHighwayCharacterTextureToMaterial(sharedHighwayCharacterMaterial);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeStartShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterFadeStartShaderId, HighwayCharacterBottomFadeStart01);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterFadeEndShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterFadeEndShaderId, HighwayCharacterBottomFadeEnd01);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterMissFlashColorShaderId))
            sharedHighwayCharacterMaterial.SetColor(CharacterMissFlashColorShaderId, HighwayCharacterMissFlashColor);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterMissFlashStrengthShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterMissFlashStrengthShaderId, 0f);
        if (sharedHighwayCharacterMaterial.HasProperty(CharacterMissFlashSpeedShaderId))
            sharedHighwayCharacterMaterial.SetFloat(CharacterMissFlashSpeedShaderId, HighwayCharacterMissFlashBandSpeed);
        sharedHighwayCharacterMaterial.renderQueue = (int)RenderQueue.Transparent - 50;
        sharedHighwayCharacterMaterial.SetInt("_ZWrite", 0);
        sharedHighwayCharacterMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedHighwayCharacterMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        return sharedHighwayCharacterMaterial;
    }

    private void InitializeHighwayCharacterMissParticles()
    {
        if (characterRoot == null || highwayCharacterMissParticles != null)
            return;

        GameObject particleObject = new GameObject("ArcadeHighwayCharacterMissParticles");
        particleObject.transform.SetParent(characterRoot.transform, false);
        particleObject.transform.localPosition = new Vector3(0f, -0.12f, 0.02f);
        particleObject.transform.localRotation = Quaternion.identity;
        particleObject.transform.localScale = Vector3.one;
        particleObject.SetActive(false);

        highwayCharacterMissParticles = particleObject.AddComponent<ParticleSystem>();
        var main = highwayCharacterMissParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.8f;
        main.maxParticles = 96;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.34f, 0.62f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.16f, 0.34f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.24f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        var emission = highwayCharacterMissParticles.emission;
        emission.enabled = false;

        var shape = highwayCharacterMissParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.18f;
        shape.radiusThickness = 0.7f;

        var velocityOverLifetime = highwayCharacterMissParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = CreateTwoConstantsCurve(0f, 0f);
        velocityOverLifetime.y = CreateTwoConstantsCurve(0.08f, 0.22f);
        velocityOverLifetime.z = CreateTwoConstantsCurve(0f, 0f);
        velocityOverLifetime.radial = CreateTwoConstantsCurve(0.42f, 0.72f);

        var sizeOverLifetime = highwayCharacterMissParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.38f),
            new Keyframe(0.14f, 1f),
            new Keyframe(0.48f, 0.74f),
            new Keyframe(1f, 0f)));

        var colorOverLifetime = highwayCharacterMissParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(Color.white, 0f),
                new GradientColorKey(new Color(1f, 0.76f, 0.44f), 0.25f),
                new GradientColorKey(new Color(0.86f, 0.26f, 0.10f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.92f, 0.12f),
                new GradientAlphaKey(0.78f, 0.55f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var rotationOverLifetime = highwayCharacterMissParticles.rotationOverLifetime;
        rotationOverLifetime.enabled = true;
        rotationOverLifetime.z = new ParticleSystem.MinMaxCurve(-2.4f, 2.4f);

        var trails = highwayCharacterMissParticles.trails;
        trails.enabled = false;

        var noise = highwayCharacterMissParticles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.08f;
        noise.frequency = 0.55f;
        noise.scrollSpeed = 0.5f;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.sharedMaterial = GetHighwayCharacterMissParticleMaterial();
        particleRenderer.lengthScale = 1f;
        particleRenderer.velocityScale = 0f;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sortingFudge = 2f;

        highwayCharacterMissParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleObject.SetActive(true);
        InitializeHighwayCharacterMissAuraParticles();
    }

    private Material GetHighwayCharacterMissParticleMaterial()
    {
        if (sharedHighwayCharacterMissParticleMaterial != null)
            return sharedHighwayCharacterMissParticleMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterMissParticle");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterMissParticle");

        sharedHighwayCharacterMissParticleMaterial = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(HighwayCharacterMissParticleColor, 0.9f);
        sharedHighwayCharacterMissParticleMaterial.renderQueue = (int)RenderQueue.Transparent - 50;
        sharedHighwayCharacterMissParticleMaterial.SetInt("_ZWrite", 0);
        sharedHighwayCharacterMissParticleMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedHighwayCharacterMissParticleMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        sharedHighwayCharacterMissParticleMaterial.SetColor("_Color", HighwayCharacterMissParticleColor);
        if (sharedHighwayCharacterMissParticleMaterial.HasProperty("_EdgeColor"))
            sharedHighwayCharacterMissParticleMaterial.SetColor("_EdgeColor", HighwayCharacterMissParticleEdgeColor);
        if (sharedHighwayCharacterMissParticleMaterial.HasProperty("_Glow"))
            sharedHighwayCharacterMissParticleMaterial.SetFloat("_Glow", HighwayCharacterMissParticleGlow);
        return sharedHighwayCharacterMissParticleMaterial;
    }

    private void InitializeHighwayCharacterMissAuraParticles()
    {
        if (characterRoot == null || highwayCharacterMissAuraParticles != null)
            return;

        GameObject particleObject = new GameObject("ArcadeHighwayCharacterMissAuraParticles");
        particleObject.transform.SetParent(characterRoot.transform, false);
        particleObject.transform.localPosition = new Vector3(0f, HighwayCharacterPortalLocalYInCharacterHeights + 0.07f, 0.024f);
        particleObject.transform.localRotation = Quaternion.identity;
        particleObject.transform.localScale = Vector3.one;
        particleObject.SetActive(false);

        highwayCharacterMissAuraParticles = particleObject.AddComponent<ParticleSystem>();
        var main = highwayCharacterMissAuraParticles.main;
        main.loop = false;
        main.playOnAwake = false;
        main.duration = 0.9f;
        main.maxParticles = 48;
        main.simulationSpace = ParticleSystemSimulationSpace.Local;
        main.scalingMode = ParticleSystemScalingMode.Hierarchy;
        main.startLifetime = new ParticleSystem.MinMaxCurve(0.4f, 0.8f);
        main.startSpeed = new ParticleSystem.MinMaxCurve(0.03f, 0.08f);
        main.startSize = new ParticleSystem.MinMaxCurve(0.18f, 0.42f);
        main.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
        main.startColor = Color.white;
        main.gravityModifier = 0f;

        var emission = highwayCharacterMissAuraParticles.emission;
        emission.enabled = false;

        var shape = highwayCharacterMissAuraParticles.shape;
        shape.enabled = true;
        shape.shapeType = ParticleSystemShapeType.Circle;
        shape.radius = 0.14f;
        shape.radiusThickness = 1f;

        var velocityOverLifetime = highwayCharacterMissAuraParticles.velocityOverLifetime;
        velocityOverLifetime.enabled = true;
        velocityOverLifetime.space = ParticleSystemSimulationSpace.Local;
        velocityOverLifetime.x = CreateTwoConstantsCurve(0f, 0f);
        velocityOverLifetime.y = CreateTwoConstantsCurve(0.03f, 0.09f);
        velocityOverLifetime.z = CreateTwoConstantsCurve(0f, 0f);
        velocityOverLifetime.radial = CreateTwoConstantsCurve(0.07f, 0.14f);

        var sizeOverLifetime = highwayCharacterMissAuraParticles.sizeOverLifetime;
        sizeOverLifetime.enabled = true;
        sizeOverLifetime.size = new ParticleSystem.MinMaxCurve(1f, new AnimationCurve(
            new Keyframe(0f, 0.42f),
            new Keyframe(0.16f, 1f),
            new Keyframe(0.55f, 1.08f),
            new Keyframe(1f, 0f)));

        var colorOverLifetime = highwayCharacterMissAuraParticles.colorOverLifetime;
        colorOverLifetime.enabled = true;
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[]
            {
                new GradientColorKey(new Color(1f, 0.86f, 0.5f), 0f),
                new GradientColorKey(new Color(1f, 0.48f, 0.18f), 0.34f),
                new GradientColorKey(new Color(0.42f, 0.06f, 0.03f), 1f)
            },
            new[]
            {
                new GradientAlphaKey(0f, 0f),
                new GradientAlphaKey(0.62f, 0.15f),
                new GradientAlphaKey(0.28f, 0.72f),
                new GradientAlphaKey(0f, 1f)
            });
        colorOverLifetime.color = new ParticleSystem.MinMaxGradient(gradient);

        var noise = highwayCharacterMissAuraParticles.noise;
        noise.enabled = true;
        noise.quality = ParticleSystemNoiseQuality.Low;
        noise.strength = 0.05f;
        noise.frequency = 0.42f;
        noise.scrollSpeed = 0.32f;

        ParticleSystemRenderer particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
        particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
        particleRenderer.alignment = ParticleSystemRenderSpace.View;
        particleRenderer.sortMode = ParticleSystemSortMode.Distance;
        particleRenderer.sharedMaterial = GetHighwayCharacterMissAuraParticleMaterial();
        particleRenderer.lengthScale = 1f;
        particleRenderer.velocityScale = 0f;
        particleRenderer.shadowCastingMode = ShadowCastingMode.Off;
        particleRenderer.receiveShadows = false;
        particleRenderer.sortingFudge = 1.5f;

        highwayCharacterMissAuraParticles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        particleObject.SetActive(true);
    }

    private Material GetHighwayCharacterMissAuraParticleMaterial()
    {
        if (sharedHighwayCharacterMissAuraParticleMaterial != null)
            return sharedHighwayCharacterMissAuraParticleMaterial;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayCharacterMissParticle");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayCharacterMissParticle");

        sharedHighwayCharacterMissAuraParticleMaterial = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(HighwayCharacterMissAuraParticleColor, 0.65f);
        sharedHighwayCharacterMissAuraParticleMaterial.renderQueue = (int)RenderQueue.Transparent - 50;
        sharedHighwayCharacterMissAuraParticleMaterial.SetInt("_ZWrite", 0);
        sharedHighwayCharacterMissAuraParticleMaterial.SetInt("_Cull", (int)CullMode.Off);
        sharedHighwayCharacterMissAuraParticleMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        sharedHighwayCharacterMissAuraParticleMaterial.SetColor("_Color", HighwayCharacterMissAuraParticleColor);
        if (sharedHighwayCharacterMissAuraParticleMaterial.HasProperty("_EdgeColor"))
            sharedHighwayCharacterMissAuraParticleMaterial.SetColor("_EdgeColor", HighwayCharacterMissAuraParticleEdgeColor);
        if (sharedHighwayCharacterMissAuraParticleMaterial.HasProperty("_Glow"))
            sharedHighwayCharacterMissAuraParticleMaterial.SetFloat("_Glow", HighwayCharacterMissAuraParticleGlow);
        return sharedHighwayCharacterMissAuraParticleMaterial;
    }

    private void ApplyHighwayCharacterMissMaterialState(float missFlashStrength)
    {
        if (sharedHighwayCharacterMaterial == null)
            return;

        bool usesCustomMissShader = sharedHighwayCharacterMaterial.HasProperty(CharacterMissFlashStrengthShaderId);
        if (usesCustomMissShader)
        {
            sharedHighwayCharacterMaterial.color = Color.white;
            sharedHighwayCharacterMaterial.SetColor(CharacterMissFlashColorShaderId, HighwayCharacterMissFlashColor);
            sharedHighwayCharacterMaterial.SetFloat(CharacterMissFlashStrengthShaderId, Mathf.Clamp01(missFlashStrength));
            if (sharedHighwayCharacterMaterial.HasProperty(CharacterMissFlashSpeedShaderId))
                sharedHighwayCharacterMaterial.SetFloat(CharacterMissFlashSpeedShaderId, HighwayCharacterMissFlashBandSpeed);
            return;
        }

        Color fallbackTint = Color.Lerp(Color.white, HighwayCharacterMissFlashColor, 0.32f);
        sharedHighwayCharacterMaterial.color = Color.Lerp(Color.white, fallbackTint, Mathf.Clamp01(missFlashStrength * 0.45f));
    }

    private Renderer CreateHighwayCharacterPortalRenderer(string name, Material material, float localForwardOffset)
    {
        GameObject portalObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        portalObject.name = name;
        portalObject.transform.SetParent(characterRoot.transform, false);
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
        return portalRenderer;
    }

    private Material GetHighwayCharacterPortalBackMaterial()
    {
        if (sharedHighwayCharacterPortalBackMaterial != null)
            return sharedHighwayCharacterPortalBackMaterial;

        sharedHighwayCharacterPortalBackMaterial = CreateHighwayCharacterPortalMaterial(halfMode: -1f, renderQueue: (int)RenderQueue.Transparent - 52);
        return sharedHighwayCharacterPortalBackMaterial;
    }

    private Material GetHighwayCharacterPortalFrontMaterial()
    {
        if (sharedHighwayCharacterPortalFrontMaterial != null)
            return sharedHighwayCharacterPortalFrontMaterial;

        sharedHighwayCharacterPortalFrontMaterial = CreateHighwayCharacterPortalMaterial(halfMode: 1f, renderQueue: (int)RenderQueue.Transparent - 51);
        return sharedHighwayCharacterPortalFrontMaterial;
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

    private void UpdateHighwayCharacterPortalVisuals(bool shouldShowPortal)
    {
        bool portalEnabled = shouldShowPortal && owner != null && owner.highwayCharacterPortalEnabled;
        if (highwayCharacterPortalBackRenderer != null)
            highwayCharacterPortalBackRenderer.enabled = portalEnabled;
        if (highwayCharacterPortalFrontRenderer != null)
            highwayCharacterPortalFrontRenderer.enabled = portalEnabled;

        if (!portalEnabled)
            return;

        ApplyHighwayCharacterPortalPalette(sharedHighwayCharacterPortalBackMaterial);
        ApplyHighwayCharacterPortalPalette(sharedHighwayCharacterPortalFrontMaterial);
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

    private static ParticleSystem.MinMaxCurve CreateTwoConstantsCurve(float min, float max)
    {
        ParticleSystem.MinMaxCurve curve = new ParticleSystem.MinMaxCurve();
        curve.mode = ParticleSystemCurveMode.TwoConstants;
        curve.constantMin = min;
        curve.constantMax = max;
        return curve;
    }

    private void SyncBackgroundCamera()
    {
        if (mainCamera == null || backgroundCamera == null)
            return;

        backgroundCamera.enabled = true;
        backgroundCamera.CopyFrom(mainCamera);
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = GetCameraBackgroundColor();
        backgroundCamera.cullingMask = 1 << BackgroundLayer;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
    }

    private Color GetCameraBackgroundColor()
    {
        if (owner == null)
            return Color.black;

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

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void ConfigureCamera()
    {
        if (mainCamera == null || owner == null)
            return;

        float centerX = 0f;
        mainCamera.orthographic = false;
        mainCamera.clearFlags = CameraClearFlags.Depth;
        if (originalMainCameraCullingMask >= 0)
            mainCamera.cullingMask = originalMainCameraCullingMask & ~(1 << BackgroundLayer);
        mainCamera.depth = originalMainCameraDepth;
        mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
        mainCamera.transform.position = new Vector3(centerX, owner.highwayCameraY, owner.highwayCameraZ);
        mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
        mainCamera.fieldOfView = 60f;
        mainCamera.backgroundColor = GetCameraBackgroundColor();
        SyncBackgroundCamera();
    }

    private void InitializeBackgroundCamera()
    {
        if (mainCamera == null || backgroundCamera != null)
            return;

        GameObject cameraObject = new GameObject("ArcadeHighway3DBackgroundCamera");
        cameraObject.transform.SetParent(root.transform, false);
        backgroundCamera = cameraObject.AddComponent<Camera>();
        backgroundCamera.enabled = false;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
    }

    private void EnsureGameplayVisualsBuilt()
    {
        if (gameplayBuilt || gameplayRoot == null || owner == null)
            return;

        BuildBoard();
        gameplayBuilt = true;
    }

    private void BuildBoard()
    {
        builtLaneCount = GetLaneCount();
        float trackWidth = GetTrackWidth(builtLaneCount);
        float guideDepth = GetLaneGuideDepth();
        float surfaceDepth = guideDepth + LaneBackOverhang;
        float surfaceCenterZ = owner.StrikeLineZ - LaneBackOverhang + (surfaceDepth * 0.5f);
        float guideCenterZ = owner.StrikeLineZ + (guideDepth * 0.5f);
        float laneWidth = GetLaneWidth();

        GameObject deck = GameObject.CreatePrimitive(PrimitiveType.Cube);
        deck.name = "ArcadeHighwayDeck";
        deck.transform.SetParent(gameplayRoot.transform, false);
        deck.transform.position = new Vector3(0f, LaneSurfaceY - 0.025f, surfaceCenterZ);
        deck.transform.localScale = new Vector3(trackWidth + laneWidth * 0.30f, 0.030f, surfaceDepth);
        Object.Destroy(deck.GetComponent<Collider>());
        Material deckMaterial = owner.CreateSharedTransparentMaterial(new Color(0.010f, 0.014f, 0.024f, 0.64f), 0.04f);
        ConfigureOverlayMaterial(deckMaterial, 30, false);
        deck.GetComponent<Renderer>().material = deckMaterial;

        for (int lane = 0; lane < builtLaneCount; lane++)
        {
            GameObject laneSurface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            laneSurface.name = $"ArcadeLane_{lane}";
            laneSurface.transform.SetParent(gameplayRoot.transform, false);
            laneSurface.transform.position = new Vector3(GetLaneX(lane), LaneSurfaceY, surfaceCenterZ);
            laneSurface.transform.localScale = new Vector3(laneWidth, 0.025f, surfaceDepth);
            Object.Destroy(laneSurface.GetComponent<Collider>());

            Material laneMaterial = CreateLaneSurfaceMaterial();
            Renderer laneRenderer = laneSurface.GetComponent<Renderer>();
            laneRenderer.material = laneMaterial;
            laneMaterials[lane] = laneMaterial;
            laneRenderers[lane] = laneRenderer;
        }

        for (int boundary = 0; boundary <= builtLaneCount; boundary++)
        {
            GameObject laneGuide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            laneGuide.name = $"ArcadeLaneGuide_{boundary}";
            laneGuide.transform.SetParent(gameplayRoot.transform, false);
            laneGuide.transform.position = new Vector3(GetBoundaryX(boundary), LaneSurfaceY + 0.064f, guideCenterZ);
            laneGuide.transform.localScale = new Vector3(Mathf.Max(Mathf.Max(0.02f, owner.highwayLaneGuideThickness), laneWidth * 0.03f), 0.085f, guideDepth);
            Object.Destroy(laneGuide.GetComponent<Collider>());
            Material guideMaterial = CreateLaneGuideMaterial();
            laneGuide.GetComponent<Renderer>().material = guideMaterial;
        }

        for (int lane = 0; lane < builtLaneCount; lane++)
        {
            Color laneColor = GetLaneColor(lane);
            GameObject endpoint = GameObject.CreatePrimitive(PrimitiveType.Cube);
            endpoint.name = $"ArcadeEndpoint_{lane}";
            endpoint.transform.SetParent(gameplayRoot.transform, false);
            endpoint.transform.position = new Vector3(GetLaneX(lane), EndpointY, owner.StrikeLineZ);
            endpoint.transform.localScale = new Vector3(laneWidth * 0.56f, 0.16f, EndpointDepth);
            Object.Destroy(endpoint.GetComponent<Collider>());

            Material endpointMaterial = owner.CreateSharedGlowMaterial(laneColor, 1.15f);
            ConfigureOverlayMaterial(endpointMaterial, 100, true);
            Renderer endpointRenderer = endpoint.GetComponent<Renderer>();
            endpointRenderer.material = endpointMaterial;
            endpointMaterials[lane] = endpointMaterial;
            endpointRenderers[lane] = endpointRenderer;
        }

        GameObject strikeLine = GameObject.CreatePrimitive(PrimitiveType.Cube);
        strikeLine.name = "ArcadeStrikeLine";
        strikeLine.transform.SetParent(gameplayRoot.transform, false);
        strikeLine.transform.position = new Vector3(0f, EndpointY - 0.04f, owner.StrikeLineZ - 0.04f);
        strikeLine.transform.localScale = new Vector3(trackWidth + laneWidth * 0.18f, 0.055f, 0.08f);
        Object.Destroy(strikeLine.GetComponent<Collider>());
        Material strikeMaterial = owner.CreateSharedTransparentMaterial(new Color(0.92f, 0.96f, 1f, 0.74f), 0.35f);
        ConfigureOverlayMaterial(strikeMaterial, 120, true);
        strikeLine.GetComponent<Renderer>().material = strikeMaterial;

        CreateSideRail(-1, trackWidth, guideCenterZ, guideDepth);
        CreateSideRail(1, trackWidth, guideCenterZ, guideDepth);
    }

    private void CreateSideRail(int side, float trackWidth, float centerZ, float guideDepth)
    {
        GameObject rail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        rail.name = side < 0 ? "ArcadeLeftRail" : "ArcadeRightRail";
        rail.transform.SetParent(gameplayRoot.transform, false);
        float laneWidth = GetLaneWidth();
        rail.transform.position = new Vector3(side * ((trackWidth * 0.5f) + laneWidth * 0.08f), LaneSurfaceY + 0.05f, centerZ);
        rail.transform.localScale = new Vector3(Mathf.Max(0.04f, laneWidth * 0.04f), 0.075f, guideDepth);
        Object.Destroy(rail.GetComponent<Collider>());
        Material railMaterial = owner.CreateSharedTransparentMaterial(new Color(0.74f, 0.86f, 1f, 0.20f), 0.12f);
        ConfigureOverlayMaterial(railMaterial, 65, true);
        rail.GetComponent<Renderer>().material = railMaterial;
    }

    private void UpdateLaneVisuals(GuitarGameplaySnapshot snapshot)
    {
        bool[] held = BuildHeldLaneSnapshot();
        bool[] incoming = BuildIncomingLaneSnapshot(snapshot);
        int laneCount = GetBuiltLaneCount();
        for (int lane = 0; lane < laneCount; lane++)
        {
            Color laneColor = GetLaneColor(lane);
            bool active = held[lane] || incoming[lane];
            float pulse = GetLanePulse(lane);
            bool missPulse = pulse < -0.001f;
            float pulseAmount = Mathf.Abs(pulse);

            if (laneMaterials[lane] != null)
            {
                Color color = active
                    ? new Color(0.08f, 0.10f, 0.14f, 1f)
                    : new Color(0.025f, 0.03f, 0.045f, 0.14f);
                if (pulseAmount > 0f)
                    color = missPulse
                        ? Color.Lerp(color, new Color(0.42f, 0.05f, 0.06f, 0.78f), pulseAmount)
                        : Color.Lerp(color, new Color(laneColor.r, laneColor.g, laneColor.b, 0.76f), pulseAmount * 0.42f);
                laneMaterials[lane].color = color;
                laneMaterials[lane].SetColor("_Color", color);
                laneMaterials[lane].SetColor("_BaseColor", color);
                laneMaterials[lane].SetColor("_TintColor", color);
                laneMaterials[lane].EnableKeyword("_EMISSION");
                Color laneEmission = active ? new Color(0.18f, 0.32f, 0.46f, 1f) * Mathf.Pow(2f, 0.15f) : Color.black;
                if (pulseAmount > 0f)
                    laneEmission = missPulse
                        ? owner.highwayMissColor * Mathf.Pow(2f, 0.7f + pulseAmount)
                        : laneColor * Mathf.Pow(2f, 0.65f + pulseAmount * 1.2f);
                laneMaterials[lane].SetColor("_EmissionColor", laneEmission);
                if (laneMaterials[lane].HasProperty("_FrontBackFade"))
                    laneMaterials[lane].SetFloat("_FrontBackFade", 0.1f);
            }

            if (endpointMaterials[lane] != null)
            {
                float emission = held[lane] ? 3.35f : incoming[lane] ? 1.55f : 0.65f;
                if (pulseAmount > 0f)
                    emission = Mathf.Max(emission, missPulse ? 2.2f * pulseAmount : 2.85f * pulseAmount);
                Color endpointColor = held[lane] ? new Color(0.985f, 0.99f, 1f, 1f) : laneColor;
                if (pulseAmount > 0f)
                    endpointColor = missPulse
                        ? Color.Lerp(endpointColor, owner.highwayMissColor, pulseAmount)
                        : Color.Lerp(endpointColor, new Color(0.95f, 0.99f, 1f, 1f), pulseAmount * 0.62f);
                endpointMaterials[lane].color = endpointColor;
                endpointMaterials[lane].SetColor("_Color", endpointColor);
                endpointMaterials[lane].SetColor("_BaseColor", endpointColor);
                endpointMaterials[lane].EnableKeyword("_EMISSION");
                endpointMaterials[lane].SetColor("_EmissionColor", endpointColor * Mathf.Pow(2f, emission));
            }
        }
    }

    private bool[] BuildHeldLaneSnapshot()
    {
        int laneCount = GetBuiltLaneCount();
        bool[] held = new bool[laneCount];
        for (int lane = 0; lane < laneCount; lane++)
            held[lane] = owner != null && owner.IsArcadeInputLaneHeld(lane);
        return held;
    }

    private bool[] BuildIncomingLaneSnapshot(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = GetBuiltLaneCount();
        bool[] incoming = new bool[laneCount];
        if (snapshot?.arcadeNoteStates == null)
            return incoming;

        float start = snapshot.songTime;
        float end = snapshot.songTime + 2.2f;
        for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = snapshot.arcadeNoteStates[i];
            if (state == null || state.IsResolved || state.data.time < start || state.data.time > end)
                continue;

            if (state.data.isOpen)
            {
                for (int lane = 0; lane < laneCount; lane++)
                    incoming[lane] = true;
            }
            else if (state.data.lane >= 0 && state.data.lane < laneCount)
            {
                incoming[state.data.lane] = true;
            }
        }

        return incoming;
    }

    private void UpdateResolvedFeedback(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot?.arcadeNoteStates == null || owner == null)
            return;

        float songTime = snapshot.songTime;
        for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = snapshot.arcadeNoteStates[i];
            if (state == null)
                continue;

            if (!state.IsResolved)
            {
                resolvedFeedbackResults.Remove(state.data.id);
                continue;
            }

            if (resolvedFeedbackResults.TryGetValue(state.data.id, out GameplayNoteResult recorded) && recorded == state.result)
                continue;

            resolvedFeedbackResults[state.data.id] = state.result;
            if (state.resolvedAt < 0f || Mathf.Abs(songTime - state.resolvedAt) > MaxFeedbackTriggerAge)
                continue;

            TriggerFeedbackForResolvedNote(state);
        }
    }

    private void TriggerFeedbackForResolvedNote(ArcadeNoteState state)
    {
        if (state == null || owner == null)
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
            int laneCount = GetBuiltLaneCount();
            for (int lane = 0; lane < laneCount; lane++)
                SetLanePulse(lane, hit ? Mathf.Lerp(0.76f, 1.15f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);

            CreateFeedbackBurst(0f, Mathf.Max(owner.FretSpacing, GetTrackWidth(laneCount) * 0.92f), color, hit, precision, openNote: true);
        }
        else
        {
            int lane = Mathf.Clamp(state.data.lane, 0, GetBuiltLaneCount() - 1);
            SetLanePulse(lane, hit ? Mathf.Lerp(0.85f, 1.25f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);
            CreateFeedbackBurst(GetLaneX(lane), owner.FretSpacing * 0.72f, color, hit, precision, openNote: false);
        }

        if (miss)
        {
            missShakeUntil = Time.time + 0.20f;
            missShakeSeed = (state.data.id * 19.17f) + Time.time;
        }
    }

    private void SetLanePulse(int lane, float strength, float duration)
    {
        if (lane < 0 || lane >= lanePulseUntil.Length)
            return;

        bool expired = lanePulseUntil[lane] <= Time.time;
        bool changedSign = !Mathf.Approximately(Mathf.Sign(strength), Mathf.Sign(lanePulseStrength[lane]));
        lanePulseUntil[lane] = Mathf.Max(lanePulseUntil[lane], Time.time + Mathf.Max(0.01f, duration));
        if (expired || changedSign || Mathf.Abs(strength) >= Mathf.Abs(lanePulseStrength[lane]))
            lanePulseStrength[lane] = strength;
    }

    private float GetLanePulse(int lane)
    {
        if (lane < 0 || lane >= lanePulseUntil.Length || lanePulseUntil[lane] <= Time.time)
            return 0f;

        float duration = lanePulseStrength[lane] < 0f ? MissPulseDuration : HitPulseDuration;
        float remaining = Mathf.Clamp01((lanePulseUntil[lane] - Time.time) / Mathf.Max(0.01f, duration));
        return lanePulseStrength[lane] * EaseOutCubic(remaining);
    }

    private void ResetLanePulses()
    {
        for (int i = 0; i < lanePulseUntil.Length; i++)
        {
            lanePulseUntil[i] = 0f;
            lanePulseStrength[i] = 0f;
        }

        missShakeUntil = 0f;
        missShakeSeed = 0f;
    }

    private void CreateFeedbackBurst(float x, float width, Color color, bool hit, float precision, bool openNote)
    {
        if (gameplayRoot == null || owner == null)
            return;

        ArcadeFeedbackEffect effect = new ArcadeFeedbackEffect
        {
            root = new GameObject(hit ? "ArcadeHitFeedback" : "ArcadeMissFeedback"),
            startTime = Time.time,
            duration = hit ? Mathf.Lerp(0.22f, 0.38f, precision) : 0.26f,
            miss = !hit
        };
        effect.root.transform.SetParent(gameplayRoot.transform, false);
        effect.root.transform.position = new Vector3(x, EndpointY + (hit ? 0.10f : 0.08f), owner.StrikeLineZ - 0.035f);

        Color hitHighlightColor = new Color(0.92f, 0.99f, 1f, 1f);
        Color missColor = owner.highwayMissColor;
        Color coreColor = hit ? Color.Lerp(color, hitHighlightColor, 0.68f + precision * 0.18f) : missColor;
        CreateFeedbackPiece(
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
                effect,
                "JudgeFlash",
                new Color(hitHighlightColor.r, hitHighlightColor.g, hitHighlightColor.b, 0.88f),
                new Vector3(0f, 0.01f, 0f),
                new Vector3(Mathf.Max(0.14f, width * 0.62f), 0.040f, openNote ? 0.26f : 0.16f),
                Vector3.zero,
                Mathf.Lerp(1.8f, 2.8f, precision),
                Vector3.zero);

            CreateFeedbackPiece(
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
                effect,
                hit ? "Spark" : "MissShard",
                new Color(sparkColor.r, sparkColor.g, sparkColor.b, hit ? 0.82f : 0.66f),
                start,
                new Vector3(sparkWidth, hit ? 0.035f : 0.028f, hit ? 0.09f : 0.16f),
                velocity,
                hit ? -0.35f : 0.10f,
                new Vector3(0f, Mathf.Sin(seed) * 280f, Mathf.Cos(seed) * 220f));
        }

        feedbackEffects.Add(effect);
    }

    private void CreateFeedbackPiece(ArcadeFeedbackEffect effect, string name, Color color, Vector3 localPosition, Vector3 scale, Vector3 velocity, float expand, Vector3 spin)
    {
        if (effect == null || effect.root == null || owner == null)
            return;

        GameObject pieceObject = GameObject.CreatePrimitive(PrimitiveType.Cube);
        pieceObject.name = name;
        pieceObject.transform.SetParent(effect.root.transform, false);
        pieceObject.transform.localPosition = localPosition;
        pieceObject.transform.localScale = scale;
        Object.Destroy(pieceObject.GetComponent<Collider>());

        Material material = owner.CreateSharedTransparentMaterial(color, 0.08f);
        ConfigureOverlayMaterial(material, effect.miss ? 175 : 185, true);
        Renderer renderer = pieceObject.GetComponent<Renderer>();
        renderer.material = material;
        ApplyMaterialColor(material, color, effect.miss ? 1.0f : 2.2f);

        effect.pieces.Add(new ArcadeFeedbackPiece
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

    private void UpdateFeedbackEffects()
    {
        for (int i = feedbackEffects.Count - 1; i >= 0; i--)
        {
            ArcadeFeedbackEffect effect = feedbackEffects[i];
            if (effect == null || effect.root == null)
            {
                feedbackEffects.RemoveAt(i);
                continue;
            }

            float age = Time.time - effect.startTime;
            float t = Mathf.Clamp01(age / Mathf.Max(0.01f, effect.duration));
            float fade = 1f - SmoothStep01(t);
            float moveEase = EaseOutCubic(t);

            for (int pieceIndex = 0; pieceIndex < effect.pieces.Count; pieceIndex++)
            {
                ArcadeFeedbackPiece piece = effect.pieces[pieceIndex];
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
                feedbackEffects.RemoveAt(i);
            }
        }
    }

    private void UpdateGameplayRootShake()
    {
        if (gameplayRoot == null || owner == null)
            return;

        if (Time.time >= missShakeUntil)
        {
            ResetGameplayRootShake();
            return;
        }

        float remaining = Mathf.Clamp01((missShakeUntil - Time.time) / 0.20f);
        float amplitude = owner.FretSpacing * 0.028f * EaseOutCubic(remaining);
        float x = Mathf.Sin((Time.time * 95f) + missShakeSeed) * amplitude;
        float y = Mathf.Cos((Time.time * 73f) + missShakeSeed * 0.7f) * amplitude * 0.34f;
        gameplayRoot.transform.localPosition = new Vector3(x, y, 0f);
    }

    private void ResetGameplayRootShake()
    {
        if (gameplayRoot != null)
            gameplayRoot.transform.localPosition = Vector3.zero;
    }

    private void UpdateNotes(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateNotesProfilerMarker.Auto())
        {
            visibleNoteIds.Clear();
            if (snapshot.arcadeNoteStates == null)
                return;

            currentVisualNoteSpeed = GetVisualNoteSpeed(snapshot);
            float renderSongTime = GetRenderSongTime(snapshot);
            float sustainSongTime = snapshot.songTime;

            for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState state = snapshot.arcadeNoteStates[i];
                if (state == null)
                    continue;

                float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
                // Keep sustain tails tied to the real song clock so they do not collapse
                // when the next note head is intentionally parked at spawn during long gaps.
                float sustainEndZ = owner.StrikeLineZ + (((state.data.time + Mathf.Max(0f, state.data.duration)) - sustainSongTime) * currentVisualNoteSpeed);
                bool keepResolvedBriefly = state.IsResolved && owner.ArcadeResolvedHoldTime > 0f && renderSongTime <= state.resolvedAt + owner.ArcadeResolvedHoldTime;
                bool showHead = travelZ <= owner.ArcadeSpawnZ && travelZ >= owner.StrikeLineZ && (!state.IsResolved || keepResolvedBriefly);
                bool showSustain = Mathf.Max(0f, state.data.duration) > 0.08f &&
                                   sustainEndZ > owner.StrikeLineZ &&
                                   travelZ <= owner.ArcadeSpawnZ;
                bool visible = showHead || showSustain;
                if (!visible)
                    continue;

                visibleNoteIds.Add(state.data.id);
                ArcadeNoteView view = GetOrCreateNoteView(state.data);
                UpdateNoteView(view, state, snapshot, travelZ, sustainEndZ, showHead);
            }

            removalBuffer.Clear();
            foreach (int id in noteViews.Keys)
            {
                if (!visibleNoteIds.Contains(id))
                    removalBuffer.Add(id);
            }

            for (int i = 0; i < removalBuffer.Count; i++)
            {
                int id = removalBuffer[i];
                if (!noteViews.TryGetValue(id, out ArcadeNoteView view))
                    continue;

                if (view.root != null)
                    Object.Destroy(view.root);
                noteViews.Remove(id);
            }
        }
    }

    private ArcadeNoteView GetOrCreateNoteView(ArcadeNoteData note)
    {
        if (noteViews.TryGetValue(note.id, out ArcadeNoteView existing))
            return existing;

        Color baseColor = GetNoteBaseColor(note);
        GameObject rootObject = new GameObject($"ArcadeNote_{note.id}");
        rootObject.transform.SetParent(gameplayRoot.transform, false);

        GameObject body = new GameObject("Body");
        body.name = "Body";
        body.transform.SetParent(rootObject.transform, false);
        Material bodyMaterial = owner.CreateSharedGlowMaterial(baseColor, 1.1f);
        ConfigureOverlayMaterial(bodyMaterial, 130, true);
        Renderer bodyRenderer = BuildStandardNoteBody(body.transform, bodyMaterial);

        GameObject outline = GameObject.CreatePrimitive(PrimitiveType.Cube);
        outline.name = "Outline";
        outline.transform.SetParent(rootObject.transform, false);
        Object.Destroy(outline.GetComponent<Collider>());
        Material outlineMaterial = owner.CreateSharedTransparentMaterial(new Color(0.01f, 0.015f, 0.03f, 0.96f), 0f);
        ConfigureOverlayMaterial(outlineMaterial, 123, true);
        Renderer outlineRenderer = outline.GetComponent<Renderer>();
        outlineRenderer.material = outlineMaterial;

        GameObject accent = GameObject.CreatePrimitive(PrimitiveType.Cube);
        accent.name = "Accent";
        accent.transform.SetParent(rootObject.transform, false);
        Object.Destroy(accent.GetComponent<Collider>());
        Material accentMaterial = owner.CreateSharedGlowMaterial(GetNoteAccentColor(note), 1.45f);
        ConfigureOverlayMaterial(accentMaterial, 116, true);
        Renderer accentRenderer = accent.GetComponent<Renderer>();
        accentRenderer.material = accentMaterial;

        GameObject sustain = GameObject.CreatePrimitive(PrimitiveType.Cube);
        sustain.name = "Sustain";
        sustain.transform.SetParent(rootObject.transform, false);
        Object.Destroy(sustain.GetComponent<Collider>());
        Material sustainMaterial = CreateTransparentGlowMaterial(new Color(baseColor.r, baseColor.g, baseColor.b, 0.48f), 0.32f);
        ConfigureOverlayMaterial(sustainMaterial, 90, true);
        Renderer sustainRenderer = sustain.GetComponent<Renderer>();
        sustainRenderer.material = sustainMaterial;

        ArcadeNoteView view = new ArcadeNoteView
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
        noteViews[note.id] = view;
        return view;
    }

    private Renderer BuildStandardNoteBody(Transform parent, Material material)
    {
        GameObject bodyFill = GameObject.CreatePrimitive(PrimitiveType.Cube);
        bodyFill.name = "BodyFill";
        bodyFill.transform.SetParent(parent, false);
        Object.Destroy(bodyFill.GetComponent<Collider>());
        Renderer renderer = bodyFill.GetComponent<Renderer>();
        renderer.material = material;
        return renderer;
    }

    private Material CreateTransparentGlowMaterial(Color color, float emission)
    {
        Material material = owner.CreateSharedRuntimeTransparentGlowMaterial(color, emission);
        material.renderQueue = (int)RenderQueue.Transparent;
        material.SetOverrideTag("RenderType", "Transparent");
        return material;
    }

    private void UpdateNoteView(ArcadeNoteView view, ArcadeNoteState state, GuitarGameplaySnapshot snapshot, float headZ, float sustainEndZ, bool showHead)
    {
        if (view == null || state == null)
            return;

        int laneCount = GetBuiltLaneCount();
        float x = state.data.isOpen ? 0f : GetLaneX(state.data.lane);
        Vector3 bodyScale = state.data.isOpen ? GetOpenNoteScale(laneCount) : GetFrettedNoteScale();
        float width = bodyScale.x;
        view.root.transform.position = new Vector3(x, NoteY, owner.StrikeLineZ);
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

        float duration = Mathf.Max(0f, state.data.duration);
        bool hasMeaningfulSustain = owner != null && owner.HasArcadeVisibleSustain(state.data);
        bool sustainActivelyHeld = hasMeaningfulSustain && owner != null && owner.IsArcadeSustainActivelyHeld(state.data);
        float sustainPulse01 = sustainActivelyHeld
            ? 0.5f + (0.5f * Mathf.Sin((snapshot != null ? snapshot.songTime : 0f) * SustainActivePulseSpeed + (state.data.chordId * 0.73f)))
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
                    view.sustain.transform.localScale = new Vector3(
                        animatedWidth,
                        animatedHeight,
                        tailLength);
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
            Color sustainBaseColor = state.IsMissed
                ? owner.highwayMissColor
                : view.baseColor;
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

    private void UpdateAccentView(ArcadeNoteView view, ArcadeNoteData note, Vector3 bodyScale, float bodyLocalZ, bool showBody)
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

    private static void ApplyMaterialColor(Material material, Color color, float emission)
    {
        if (material == null)
            return;

        material.color = color;
        material.SetColor("_Color", color);
        material.SetColor("_BaseColor", color);
        material.SetColor("_TintColor", color);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emission > 0f ? color * Mathf.Pow(2f, emission) : Color.black);
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

    private static void ConfigureOverlayMaterial(Material material, int renderQueueOffset, bool renderOnTop)
    {
        if (material == null)
            return;

        material.renderQueue = (int)RenderQueue.Transparent + renderQueueOffset;
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        if (renderOnTop && material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
    }

    private float GetVisualNoteSpeed(GuitarGameplaySnapshot snapshot)
    {
        float spacingScale = 1f;
        if (snapshot != null)
            spacingScale = Mathf.Clamp(snapshot.tabSpeedOffsetPercent / 100f, 0.5f, 1.5f);

        return Mathf.Max(0.01f, owner.noteSpeed * spacingScale);
    }

    private float GetRenderSongTime(GuitarGameplaySnapshot snapshot)
    {
        using (GetRenderSongTimeProfilerMarker.Auto())
        {
            if (snapshot == null || snapshot.arcadeNoteStates == null)
                return 0f;

            float songTime = snapshot.songTime;
            if (snapshot.isPaused || snapshot.noteByNoteModeEnabled || snapshot.noteByNoteWaitingForMatch)
                return songTime;

            float visibleWindow = Mathf.Max(0.01f, (owner.ArcadeSpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, currentVisualNoteSpeed));
            for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState state = snapshot.arcadeNoteStates[i];
                if (state != null && !state.IsResolved && state.data.time >= songTime && state.data.time <= songTime + visibleWindow)
                    return songTime;
            }

            ArcadeNoteState nextPending = null;
            for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
            {
                ArcadeNoteState state = snapshot.arcadeNoteStates[i];
                if (state == null || state.IsResolved || state.data.time < songTime)
                    continue;

                if (nextPending == null || state.data.time < nextPending.data.time)
                    nextPending = state;
            }

            if (nextPending == null)
                return songTime;

            return Mathf.Max(0f, nextPending.data.time - (visibleWindow * 0.85f));
        }
    }

    private int GetLaneCount()
    {
        return Mathf.Clamp(owner != null ? owner.ArcadeHighwayLaneCount : DefaultLaneCount, 1, MaxLaneCount);
    }

    private int GetBuiltLaneCount()
    {
        return Mathf.Clamp(builtLaneCount > 0 ? builtLaneCount : GetLaneCount(), 1, MaxLaneCount);
    }

    private float GetLaneWidth()
    {
        return Mathf.Max(0.01f, owner != null ? owner.FretSpacing : 1f);
    }

    private float GetTrackWidth(int laneCount)
    {
        return Mathf.Max(GetLaneWidth(), laneCount * GetLaneWidth());
    }

    private float GetLaneX(int lane)
    {
        int laneCount = GetBuiltLaneCount();
        float laneWidth = GetLaneWidth();
        return ((Mathf.Clamp(lane, 0, laneCount - 1) + 0.5f) * laneWidth) - (laneCount * laneWidth * 0.5f);
    }

    private float GetBoundaryX(int boundary)
    {
        int laneCount = GetBuiltLaneCount();
        float laneWidth = GetLaneWidth();
        return (Mathf.Clamp(boundary, 0, laneCount) * laneWidth) - (laneCount * laneWidth * 0.5f);
    }

    private Vector3 GetFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.56f,
            0.44f * GetNoteHeightScale(),
            Mathf.Max(0.48f, owner.FretSpacing * 0.28f));
    }

    private Vector3 GetOpenNoteScale(int laneCount)
    {
        return new Vector3(
            Mathf.Max(owner.FretSpacing * 0.8f, GetTrackWidth(laneCount) - owner.FretSpacing * 0.2f),
            0.2f * GetNoteHeightScale(),
            Mathf.Max(0.36f, owner.FretSpacing * 0.22f));
    }

    private float GetNoteHeightScale()
    {
        return Mathf.Max(0.2f, owner.highwayNoteHeightScale);
    }

    private float GetLaneGuideDepth()
    {
        return Mathf.Max(LaneGuideDepth, owner != null ? owner.ArcadeSpawnZ - owner.StrikeLineZ : LaneGuideDepth);
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

    private Color GetLaneColor(int lane)
    {
        switch (lane)
        {
            case 0:
                return owner.GetStringColor(4);
            case 1:
                return owner.GetStringColor(0);
            case 2:
                return owner.GetStringColor(1);
            case 3:
                return owner.GetStringColor(2);
            case 4:
                return owner.GetStringColor(3);
            default:
                return Color.white;
        }
    }
}
