using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;

public sealed class ArcadeHighway3DRenderHost
{
    public Camera Camera;
    public RenderTexture TargetTexture;
    public bool ManualRender;
    public bool EnableBackground = true;
    public bool EnableHighwayCharacter = true;
    public bool EnableSongHeaderOverlay = true;
    public bool EnableDrumKit = true;
    public int RenderLayer = -1;
    public string RootName = "ArcadeHighway3DRendererRoot";
    public int? LaneCountOverride;
}

public sealed class ArcadeHighway3DRenderer : IGuitarGameplayRenderer
{
    private static readonly ProfilerMarker RenderProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.Render");
    private static readonly ProfilerMarker UpdateNotesProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateNotes");
    private static readonly ProfilerMarker GetRenderSongTimeProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.GetRenderSongTime");
    private static readonly ProfilerMarker UpdateDrumKitVisualProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateDrumKitVisual");
    private static readonly ProfilerMarker UpdateDrumKitShaderStateProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateDrumKitShaderState");
    private static readonly ProfilerMarker UpdateLaneVisualsProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateLaneVisuals");
    private static readonly ProfilerMarker UpdateFeedbackEffectsProfilerMarker = new ProfilerMarker("StringTheory.ArcadeHighway3D.UpdateFeedbackEffects");

    private const int DefaultLaneCount = 5;
    private const int MaxLaneCount = 8;
    private const float LaneBackOverhang = 8f;
    private const float LaneGuideDepth = 150f;
    private const float LaneSurfaceY = 1.02f;
    private const float NoteY = 1.30f;
    private const float EndpointY = 1.36f;
    private const float EndpointDepth = 0.34f;
    private const int BackgroundLayer = 2;
    private const string DrumKitResourcePath = "3d/uploads_files_6189797_DrumsLowpoly";
    private const string DrumKitFrontBaseColorTexturePath = "3d/uploads_files_6189797_DrumsTextures/Front_BC";
    private const string DrumKitFrontNormalTexturePath = "3d/uploads_files_6189797_DrumsTextures/Front_N";
    private const string DrumKitFrontRmoTexturePath = "3d/uploads_files_6189797_DrumsTextures/Front_RMO";
    private const string DrumKitBackBaseColorTexturePath = "3d/uploads_files_6189797_DrumsTextures/Back_BC";
    private const string DrumKitBackNormalTexturePath = "3d/uploads_files_6189797_DrumsTextures/Back_N";
    private const string DrumKitBackRmoTexturePath = "3d/uploads_files_6189797_DrumsTextures/Back_RMO";
    private const string DrumKitShaderResourcePath = "Shaders/ArcadeDrumKit";
    private const float DrumKitTunedWorldScale = 3.289094f;
    private const int DrumKitRenderQueueOffset = 80;
    private const float DrumKitYawDegrees = 0f;
    private const float DrumKitHitGlowLookaheadSeconds = 1.15f;
    private const float DrumKitHitGlowHoldSeconds = 0.08f;
    private const float DrumKitHitGlowEdgeStrength = 3.10f;
    private const float DrumKitIdleGlowStrength = 0.46f;
    private const float DrumKitCymbalHitAnimationDurationSeconds = 0.72f;
    private const float DrumKitCymbalHitTriggerCooldownSeconds = 0.025f;
    private const float DrumKitCymbalHitPrimaryTiltDegrees = 11.5f;
    private const float DrumKitCymbalHitSecondaryTiltDegrees = 5.0f;
    private const float DrumKitCymbalHitDropLocalUnits = 0.030f;
    private const float DrumKitImpactDurationSeconds = 0.36f;
    private const float DrumKitSuccessImpactDurationSeconds = 0.54f;
    private const float DrumKitImpactTriggerCooldownSeconds = 0.030f;
    private static readonly string[] DrumKitKickSurfaceNames = { "polySurface16_low" };
    private static readonly string[] DrumKitSnareSurfaceNames = { "polySurface40_low" };
    private static readonly string[] DrumKitRackTomSurfaceNames = { "polySurface36_low" };
    private static readonly string[] DrumKitFloorTomSurfaceNames = { "polySurface58_low" };
    private static readonly string[] DrumKitCymbalSurfaceNames = { "pCylinder593_low" };
    private static readonly Vector3 DrumKitRendererRootWorldPosition = new Vector3(0f, 6.00f, 3.80f);
    private static readonly Vector3 DrumKitTunedWorldPosition = new Vector3(0f, 8.313232f, -14.475312f);
    private static readonly Quaternion DrumKitTunedWorldRotation = new Quaternion(0.20707831f, 0f, 0f, 0.97832441f);
    private static readonly string[][] DrumKitExplicitHitTargetNames =
    {
        new string[] { "DrumHit_HiHat", "DrumGlow_HiHat", "HiHat_HitFace" },
        new string[] { "DrumHit_Crash", "DrumGlow_Crash", "Crash_HitFace" },
        new string[] { "DrumHit_Snare", "DrumGlow_Snare", "Snare_HitFace" },
        new string[] { "DrumHit_Tom1", "DrumGlow_Tom1", "Tom1_HitFace" },
        new string[] { "DrumHit_Kick", "DrumGlow_Kick", "Kick_HitFace" },
        new string[] { "DrumHit_Tom2", "DrumGlow_Tom2", "Tom2_HitFace" },
        new string[] { "DrumHit_FloorTom", "DrumGlow_FloorTom", "FloorTom_HitFace" },
        new string[] { "DrumHit_Ride", "DrumGlow_Ride", "Ride_HitFace" }
    };
    private static readonly int DrumKitHitGlowEdgeStrengthShaderId = Shader.PropertyToID("_HitGlowEdgeStrength");
    private static readonly int DrumKitTargetGlowColorShaderId = Shader.PropertyToID("_TargetGlowColor");
    private static readonly int DrumKitTargetGlowStrengthShaderId = Shader.PropertyToID("_TargetGlowStrength");
    private static readonly int DrumKitTargetGlowCenterShaderId = Shader.PropertyToID("_TargetGlowCenter");
    private static readonly int DrumKitTargetGlowExtentsShaderId = Shader.PropertyToID("_TargetGlowExtents");
    private static readonly int DrumKitTargetGlowPlaneMaskShaderId = Shader.PropertyToID("_TargetGlowPlaneMask");
    private static readonly int DrumKitTargetGlowDepthSideShaderId = Shader.PropertyToID("_TargetGlowDepthSide");
    private static readonly int DrumKitTargetGlowSurfaceModeShaderId = Shader.PropertyToID("_TargetGlowSurfaceMode");
    private static readonly int DrumKitImpactColorShaderId = Shader.PropertyToID("_DrumImpactColor");
    private static readonly int DrumKitImpactStrengthShaderId = Shader.PropertyToID("_DrumImpactStrength");
    private static readonly int DrumKitImpactProgressShaderId = Shader.PropertyToID("_DrumImpactProgress");
    private static readonly int DrumKitSuccessImpactColorShaderId = Shader.PropertyToID("_DrumSuccessImpactColor");
    private static readonly int DrumKitSuccessImpactStrengthShaderId = Shader.PropertyToID("_DrumSuccessImpactStrength");
    private static readonly int DrumKitSuccessImpactProgressShaderId = Shader.PropertyToID("_DrumSuccessImpactProgress");
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
    private readonly List<Material> drumKitOwnedMaterials = new List<Material>();
    private readonly List<Mesh> drumKitOwnedMeshes = new List<Mesh>();
    private readonly List<DrumKitMaterialBinding> drumKitMaterialBindings = new List<DrumKitMaterialBinding>();
    private readonly List<DrumKitCymbalAnimationBinding> drumKitCymbalAnimationBindings = new List<DrumKitCymbalAnimationBinding>();
    private readonly List<Renderer> drumKitRenderers = new List<Renderer>();
    private readonly Material[] laneMaterials = new Material[MaxLaneCount];
    private readonly Material[] endpointMaterials = new Material[MaxLaneCount];
    private readonly Renderer[] endpointRenderers = new Renderer[MaxLaneCount];
    private readonly Renderer[] laneRenderers = new Renderer[MaxLaneCount];
    private readonly float[] lanePulseUntil = new float[MaxLaneCount];
    private readonly float[] lanePulseStrength = new float[MaxLaneCount];
    private readonly float[] drumKitLaneGlowStrength = new float[MaxLaneCount];
    private readonly float[] drumKitLaneLastCymbalTriggerTime = new float[MaxLaneCount];
    private readonly float[] drumKitLaneImpactStartedAt = new float[MaxLaneCount];
    private readonly float[] drumKitLaneImpactStrength = new float[MaxLaneCount];
    private readonly float[] drumKitLaneSuccessImpactStartedAt = new float[MaxLaneCount];
    private readonly float[] drumKitLaneSuccessImpactStrength = new float[MaxLaneCount];
    private readonly float[] drumKitLaneLastImpactTriggerTime = new float[MaxLaneCount];
    private readonly bool[] drumKitLaneHasExplicitTargetBinding = new bool[MaxLaneCount];
    private readonly bool[] drumKitLaneHasTargetSurfaceBinding = new bool[MaxLaneCount];
    private readonly bool[] heldLaneSnapshot = new bool[MaxLaneCount];
    private readonly bool[] incomingLaneSnapshot = new bool[MaxLaneCount];
    private readonly ArcadeHighway3DRenderHost renderHost;

    private enum BackgroundProfile
    {
        Gameplay,
        MainMenu,
        MiniGames
    }

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private Camera backgroundCamera;
    private GameObject root;
    private GameObject gameplayRoot;
    private GameObject backgroundRoot;
    private GameObject drumKitRoot;
    private GameObject drumKitModelInstance;
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
    private BackgroundProfile backgroundProfile = BackgroundProfile.Gameplay;
    private string backgroundSignature = string.Empty;
    private TabsSongHeaderOverlay songHeaderOverlay;
    private Texture2D drumKitFrontBaseColorTexture;
    private Texture2D drumKitFrontNormalTexture;
    private Texture2D drumKitFrontRmoTexture;
    private Texture2D drumKitBackBaseColorTexture;
    private Texture2D drumKitBackNormalTexture;
    private Texture2D drumKitBackRmoTexture;
    private Material drumKitStaticFrontMaterial;
    private Material drumKitStaticBackMaterial;
    private Bounds drumKitLocalBounds;
    private Vector3 currentRendererRootWorldOffset;
    private bool drumKitLoadAttempted;
    private bool drumKitReady;
    private bool warnedMissingDrumKitAsset;
    private bool loggedDrumKitDiagnostics;
    private int drumKitStaticCombinedRendererCount;
    private int drumKitStaticCombinedSourceRendererCount;
    private int drumKitStaticCombineUnreadableSkipCount;
    private CameraClearFlags originalMainCameraClearFlags;
    private Color originalMainCameraBackgroundColor;
    private int originalMainCameraCullingMask = -1;
    private float originalMainCameraDepth;
    private bool originalMainCameraOrthographic;
    private RenderTexture originalMainCameraTargetTexture;
    private Rect originalMainCameraRect;
    private bool originalMainCameraEnabled = true;
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

    private sealed class DrumKitMaterialBinding
    {
        public Renderer renderer;
        public Material material;
        public string rendererName;
        public string rendererPath;
        public Bounds localBounds;
        public Vector4 planeMask;
        public Vector4 targetGlowCenter;
        public Vector4 targetGlowExtents;
        public int explicitLaneMask;
        public int targetSurfaceLaneMask;
        public int allLaneMask;
        public bool isCymbalSurface;
        public bool targetDirectionUsesCamera;
        public bool hasDynamicMaterialState;
        public Color lastTargetGlowColor;
        public float lastTargetGlowStrength;
        public float lastTargetGlowDepthSide;
        public Color lastImpactColor;
        public float lastImpactStrength;
        public float lastImpactProgress;
        public Color lastSuccessImpactColor;
        public float lastSuccessImpactStrength;
        public float lastSuccessImpactProgress;
    }

    private sealed class DrumKitCymbalAnimationBinding
    {
        public int lane;
        public Transform transform;
        public Vector3 baseLocalPosition;
        public Quaternion baseLocalRotation;
        public Vector3 baseLocalScale;
        public float startedAt = -100f;
        public float strength;
        public float primaryDirection = 1f;
        public float secondaryDirection = 1f;
        public float phase;
    }

    public ArcadeHighway3DRenderer()
        : this(null)
    {
    }

    public ArcadeHighway3DRenderer(ArcadeHighway3DRenderHost renderHost)
    {
        this.renderHost = renderHost;
    }

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = renderHost?.Camera != null ? renderHost.Camera : Camera.main;
        root = new GameObject(string.IsNullOrEmpty(renderHost?.RootName) ? "ArcadeHighway3DRendererRoot" : renderHost.RootName);
        backgroundRoot = new GameObject("ArcadeHighway3DBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        drumKitRoot = new GameObject("ArcadeHighway3DDrumKitRoot");
        drumKitRoot.transform.SetParent(root.transform, false);
        drumKitRoot.SetActive(false);
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
            originalMainCameraTargetTexture = mainCamera.targetTexture;
            originalMainCameraRect = mainCamera.rect;
            originalMainCameraEnabled = mainCamera.enabled;
        }
        lastObservedHighwayCharacterMissCount = -1;
        lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
        lastHighwayCharacterBopSourceNoteCount = -1;
        highwayCharacterBopEvents.Clear();

        if (IsBackgroundEnabled())
        {
            InitializeBackgroundCamera();
            InitializeBackgroundEffect(BackgroundProfile.Gameplay);
        }
        else
        {
            backgroundProfile = BackgroundProfile.Gameplay;
            backgroundRoot.SetActive(false);
        }
        ConfigureCamera();
        if (IsHighwayCharacterEnabled())
            InitializeHighwayCharacter();
        else
            characterRoot.SetActive(false);
        if (renderHost == null || renderHost.EnableSongHeaderOverlay)
            songHeaderOverlay = new TabsSongHeaderOverlay(owner);
        gameplayBuilt = false;
        ApplyHostRenderLayer();
        if (renderHost?.ManualRender == true && root != null)
            root.SetActive(false);
    }

    private bool IsBackgroundEnabled()
    {
        return renderHost == null || renderHost.EnableBackground;
    }

    private bool IsHighwayCharacterEnabled()
    {
        return renderHost == null || renderHost.EnableHighwayCharacter;
    }

    private bool IsDrumKitVisualEnabled()
    {
        return renderHost == null || renderHost.EnableDrumKit;
    }

    private void ApplyHostCameraOverrides()
    {
        if (renderHost == null || mainCamera == null)
            return;

        if (renderHost.TargetTexture != null)
            mainCamera.targetTexture = renderHost.TargetTexture;
        mainCamera.rect = new Rect(0f, 0f, 1f, 1f);
        if (renderHost.ManualRender)
            mainCamera.enabled = false;
        if (renderHost.RenderLayer >= 0 && renderHost.RenderLayer < 32)
            mainCamera.cullingMask = 1 << renderHost.RenderLayer;
        if (!renderHost.EnableBackground)
        {
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            mainCamera.backgroundColor = Color.black;
        }
    }

    private void ApplyHostRenderLayer()
    {
        if (renderHost == null || renderHost.RenderLayer < 0 || renderHost.RenderLayer >= 32 || root == null)
            return;

        SetLayerRecursively(root, renderHost.RenderLayer);
    }

    private void RenderHostCameraIfNeeded()
    {
        if (renderHost == null || !renderHost.ManualRender || mainCamera == null)
            return;

        RenderTexture previousActive = RenderTexture.active;
        mainCamera.Render();
        RenderTexture.active = previousActive;
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        DestroyDrumKitVisual();

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

        bool deactivateHostRootAfterRender = renderHost?.ManualRender == true && root != null && !root.activeSelf;
        if (deactivateHostRootAfterRender)
            root.SetActive(true);

        try
        {
            using (RenderProfilerMarker.Auto())
            {
                BackgroundProfile targetBackgroundProfile = ResolveBackgroundProfile(snapshot);
                if (IsBackgroundEnabled())
                {
                    EnsureBackgroundMode(targetBackgroundProfile);
                }
                else
                {
                    backgroundProfile = BackgroundProfile.Gameplay;
                    if (backgroundRoot != null && backgroundRoot.activeSelf)
                        backgroundRoot.SetActive(false);
                    if (backgroundCamera != null)
                        backgroundCamera.enabled = false;
                }

                bool suppressGameplay = snapshot.mainMenuFlowActive || snapshot.songEnded || snapshot.showToneLab || snapshot.showTuner || snapshot.showMiniGames;
                bool useDrumHighwayPlacement = !suppressGameplay && IsDrumKitVisualEnabled() && IsDrumKitSnapshot(snapshot);
                currentRendererRootWorldOffset = useDrumHighwayPlacement ? DrumKitRendererRootWorldPosition : Vector3.zero;
                ApplyRendererRootPlacement();
                ConfigureCamera();
                EnsureGameplayVisualsBuilt();
                if (gameplayRoot != null && gameplayRoot.activeSelf == suppressGameplay)
                    gameplayRoot.SetActive(!suppressGameplay);
                UpdateDrumKitVisual(snapshot, suppressGameplay);

                if (IsBackgroundEnabled() && !suppressGameplay && backgroundProfile == BackgroundProfile.Gameplay)
                    UpdateBackgroundPlacement();

                if (!suppressGameplay)
                {
                    if (IsHighwayCharacterEnabled())
                        UpdateHighwayCharacter(snapshot, suppressGameplay);
                    UpdateResolvedFeedback(snapshot);
                    UpdateGameplayRootShake();
                    UpdateLaneVisuals(snapshot);
                    UpdateNotes(snapshot);
                    UpdateFeedbackEffects();
                }
                else
                {
                    if (IsHighwayCharacterEnabled())
                        UpdateHighwayCharacter(snapshot, suppressGameplay);
                    ResetGameplayRootShake();
                }

                if (IsBackgroundEnabled())
                    backgroundEffect?.Tick(Time.deltaTime);
                songHeaderOverlay?.UpdateFromSnapshot(snapshot);
                ApplyHostRenderLayer();
                ApplyHostCameraOverrides();
                RenderHostCameraIfNeeded();
            }
        }
        finally
        {
            if (deactivateHostRootAfterRender && root != null)
                root.SetActive(false);
        }
    }

    public void DisposeRenderer()
    {
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;

        backgroundEffect?.Dispose();
        backgroundEffect = null;

        DestroyDrumKitVisual();

        if (mainCamera != null && originalMainCameraCullingMask >= 0)
        {
            mainCamera.clearFlags = originalMainCameraClearFlags;
            mainCamera.backgroundColor = originalMainCameraBackgroundColor;
            mainCamera.cullingMask = originalMainCameraCullingMask;
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.orthographic = originalMainCameraOrthographic;
            mainCamera.targetTexture = originalMainCameraTargetTexture;
            mainCamera.rect = originalMainCameraRect;
            mainCamera.enabled = originalMainCameraEnabled;
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

    private void InitializeBackgroundEffect(BackgroundProfile profile)
    {
        backgroundEffect?.Dispose();
        backgroundProfile = profile;
        if (profile == BackgroundProfile.MiniGames && !IsMiniGameEnviroSkyActive())
            ConfigureMiniGameBackgroundCamera();

        GuitarBridgeServer.TabsBackgroundContext ownerContext = ToOwnerBackgroundContext(profile);
        bool applyHighwayOverrides = profile == BackgroundProfile.Gameplay;
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides, ownerContext);
        backgroundSignature = GetBackgroundSignature(profile);
        SetBackgroundEffectRenderCamera(GetBackgroundEffectRenderCamera(profile));
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

        if (backgroundProfile == BackgroundProfile.MainMenu)
        {
            backgroundRoot.transform.localPosition = Vector3.zero;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;
            return;
        }

        if (backgroundProfile == BackgroundProfile.MiniGames)
        {
            backgroundRoot.transform.localPosition = new Vector3(0f, 0.85f, 0f);
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one * 1.24f;
            return;
        }

        GuitarBridgeServer.TabsBackgroundMode activeBackgroundMode = owner.GetBackgroundModeForContext(ToOwnerBackgroundContext(backgroundProfile));
        Vector3 rootOffset = GetCurrentRendererRootWorldOffset();
        if (activeBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.NeonStage)
        {
            backgroundRoot.transform.position = rootOffset;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;
            return;
        }

        backgroundRoot.transform.position = new Vector3(
            Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f),
            owner.highwayBackgroundCenterY + rootOffset.y,
            owner.highwayBackgroundDistance + rootOffset.z);
        backgroundRoot.transform.localRotation = Quaternion.identity;
        backgroundRoot.transform.localScale = Vector3.one * owner.highwayBackgroundScale;
    }

    private void UpdateDrumKitVisual(GuitarGameplaySnapshot snapshot, bool suppressGameplay)
    {
        using (UpdateDrumKitVisualProfilerMarker.Auto())
        {
            bool shouldShow = snapshot != null &&
                              IsDrumKitVisualEnabled() &&
                              !suppressGameplay &&
                              IsDrumKitSnapshot(snapshot);
            if (!shouldShow)
            {
                ResetDrumKitCymbalAnimations();
                ResetDrumKitImpactAnimations();
                if (drumKitRoot != null && drumKitRoot.activeSelf)
                    drumKitRoot.SetActive(false);
                return;
            }

            if (!EnsureDrumKitVisual())
                return;

            PositionDrumKit();
            TriggerDrumKitInputImpactAnimations();
            TriggerDrumKitCymbalInputAnimations();
            UpdateDrumKitCymbalAnimations();
            UpdateDrumKitShaderState(snapshot);
            if (drumKitRoot != null && !drumKitRoot.activeSelf)
                drumKitRoot.SetActive(true);
        }
    }

    private void TriggerDrumKitInputImpactAnimations()
    {
        if (owner == null)
            return;

        int laneCount = Mathf.Min(GetBuiltLaneCount(), MaxLaneCount);
        for (int lane = 0; lane < laneCount; lane++)
        {
            if (!owner.IsArcadeInputLanePressed(lane))
                continue;
            if (drumKitLaneLastImpactTriggerTime[lane] > 0f &&
                Time.time - drumKitLaneLastImpactTriggerTime[lane] < DrumKitImpactTriggerCooldownSeconds)
            {
                continue;
            }

            drumKitLaneLastImpactTriggerTime[lane] = Time.time;
            TriggerDrumKitLaneImpact(lane, 1f);
        }
    }

    private void TriggerDrumKitLaneImpact(int lane, float strength)
    {
        if (lane < 0 || lane >= drumKitLaneImpactStartedAt.Length)
            return;

        drumKitLaneImpactStartedAt[lane] = Time.time;
        drumKitLaneImpactStrength[lane] = Mathf.Clamp01(Mathf.Max(drumKitLaneImpactStrength[lane] * 0.35f, strength));
    }

    private void TriggerDrumKitLaneSuccessImpact(int lane, float strength)
    {
        if (lane < 0 || lane >= drumKitLaneSuccessImpactStartedAt.Length)
            return;

        drumKitLaneSuccessImpactStartedAt[lane] = Time.time;
        drumKitLaneSuccessImpactStrength[lane] = Mathf.Clamp01(Mathf.Max(drumKitLaneSuccessImpactStrength[lane] * 0.35f, strength));
    }

    private void TriggerDrumKitCymbalInputAnimations()
    {
        if (owner == null || drumKitCymbalAnimationBindings.Count == 0)
            return;

        for (int i = 0; i < drumKitCymbalAnimationBindings.Count; i++)
        {
            DrumKitCymbalAnimationBinding binding = drumKitCymbalAnimationBindings[i];
            int lane = binding != null ? binding.lane : -1;
            if (lane < 0 || lane >= drumKitLaneLastCymbalTriggerTime.Length)
                continue;
            if (!owner.IsArcadeInputLanePressed(lane))
                continue;
            if (drumKitLaneLastCymbalTriggerTime[lane] > 0f &&
                Time.time - drumKitLaneLastCymbalTriggerTime[lane] < DrumKitCymbalHitTriggerCooldownSeconds)
            {
                continue;
            }

            drumKitLaneLastCymbalTriggerTime[lane] = Time.time;
            TriggerDrumKitCymbalHitAnimation(lane, 1f);
        }
    }

    private void TriggerDrumKitCymbalHitAnimation(int lane, float strength)
    {
        float now = Time.time;
        for (int i = 0; i < drumKitCymbalAnimationBindings.Count; i++)
        {
            DrumKitCymbalAnimationBinding binding = drumKitCymbalAnimationBindings[i];
            if (binding?.transform == null || binding.lane != lane)
                continue;

            float seed = (now * 41.37f) + (lane * 17.19f) + (binding.transform.GetInstanceID() * 0.011f);
            binding.startedAt = now;
            binding.strength = Mathf.Clamp01(Mathf.Max(binding.strength * 0.42f, strength));
            binding.primaryDirection = PseudoRandom01(seed) < 0.5f ? -1f : 1f;
            binding.secondaryDirection = PseudoRandom01(seed + 13.37f) < 0.5f ? -1f : 1f;
            binding.phase = PseudoRandom01(seed + 27.53f) * Mathf.PI * 2f;
        }
    }

    private void UpdateDrumKitCymbalAnimations()
    {
        if (drumKitCymbalAnimationBindings.Count == 0)
            return;

        float now = Time.time;
        for (int i = 0; i < drumKitCymbalAnimationBindings.Count; i++)
        {
            DrumKitCymbalAnimationBinding binding = drumKitCymbalAnimationBindings[i];
            if (binding?.transform == null)
                continue;

            float elapsed = now - binding.startedAt;
            if (elapsed < 0f || elapsed >= DrumKitCymbalHitAnimationDurationSeconds)
            {
                binding.transform.localPosition = binding.baseLocalPosition;
                binding.transform.localRotation = binding.baseLocalRotation;
                binding.transform.localScale = binding.baseLocalScale;
                continue;
            }

            float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / 0.045f));
            float decay = Mathf.Exp(-elapsed * 4.85f);
            float normalized = Mathf.Clamp01(elapsed / DrumKitCymbalHitAnimationDurationSeconds);
            float fadeOut = 1f - Mathf.SmoothStep(0.70f, 1f, normalized);
            float envelope = attack * decay * fadeOut * Mathf.Clamp01(binding.strength);
            float initialDip = attack * Mathf.Exp(-elapsed * 10.5f) * Mathf.Clamp01(binding.strength);
            float wobbleFast = Mathf.Sin((elapsed * 37.5f) + binding.phase) * envelope;
            float wobbleSlow = Mathf.Sin((elapsed * 24.0f) + binding.phase * 0.71f) * envelope;

            float primaryAngle = binding.primaryDirection *
                                 ((initialDip * DrumKitCymbalHitPrimaryTiltDegrees) +
                                  (wobbleFast * DrumKitCymbalHitPrimaryTiltDegrees * 0.74f));
            float secondaryAngle = binding.secondaryDirection *
                                   ((initialDip * DrumKitCymbalHitSecondaryTiltDegrees) +
                                    (wobbleSlow * DrumKitCymbalHitSecondaryTiltDegrees));
            Vector3 localDrop = new Vector3(0f, -DrumKitCymbalHitDropLocalUnits * initialDip, 0f);
            float scalePulse = 1f + (initialDip * 0.010f) + (Mathf.Abs(wobbleFast) * 0.008f);

            binding.transform.localPosition = binding.baseLocalPosition + localDrop;
            binding.transform.localRotation = binding.baseLocalRotation * Quaternion.Euler(primaryAngle, 0f, secondaryAngle);
            binding.transform.localScale = binding.baseLocalScale * scalePulse;
        }
    }

    private void ResetDrumKitCymbalAnimations()
    {
        for (int i = 0; i < drumKitCymbalAnimationBindings.Count; i++)
        {
            DrumKitCymbalAnimationBinding binding = drumKitCymbalAnimationBindings[i];
            if (binding?.transform == null)
                continue;

            binding.startedAt = -100f;
            binding.strength = 0f;
            binding.transform.localPosition = binding.baseLocalPosition;
            binding.transform.localRotation = binding.baseLocalRotation;
            binding.transform.localScale = binding.baseLocalScale;
        }

        for (int lane = 0; lane < drumKitLaneLastCymbalTriggerTime.Length; lane++)
            drumKitLaneLastCymbalTriggerTime[lane] = -100f;
    }

    private void ResetDrumKitImpactAnimations()
    {
        for (int lane = 0; lane < drumKitLaneImpactStartedAt.Length; lane++)
        {
            drumKitLaneImpactStartedAt[lane] = -100f;
            drumKitLaneImpactStrength[lane] = 0f;
            drumKitLaneSuccessImpactStartedAt[lane] = -100f;
            drumKitLaneSuccessImpactStrength[lane] = 0f;
            drumKitLaneLastImpactTriggerTime[lane] = -100f;
        }
    }

    private static bool IsDrumKitSnapshot(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return false;

        if (snapshot.selectedArcadeInstrument == ArcadeInstrument.Drums)
            return true;

        if (snapshot.arcadeLaneCount >= 8)
            return true;

        return ContainsDrumText(snapshot.selectedArcadeArrangementDisplayName) ||
               ContainsDrumText(snapshot.selectedArcadeArrangementId);
    }

    private static bool ContainsDrumText(string value)
    {
        return !string.IsNullOrWhiteSpace(value) &&
               value.IndexOf("drum", System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private bool EnsureDrumKitVisual()
    {
        if (drumKitReady)
            return true;
        if (drumKitLoadAttempted)
            return false;

        drumKitLoadAttempted = true;
        if (drumKitRoot == null)
            return false;

        GameObject drumKitAsset = Resources.Load<GameObject>(DrumKitResourcePath);
        if (drumKitAsset == null)
        {
            if (!warnedMissingDrumKitAsset)
            {
                warnedMissingDrumKitAsset = true;
                Debug.LogWarning($"[ArcadeHighway3D] Could not load drum kit resource '{DrumKitResourcePath}'. Drums gameplay will continue without the 3D kit.");
            }
            return false;
        }

        LoadDrumKitTextures();
        bool wasRootActive = drumKitRoot.activeSelf;
        drumKitRoot.SetActive(true);
        drumKitModelInstance = Object.Instantiate(drumKitAsset, drumKitRoot.transform);
        drumKitModelInstance.name = "ArcadeDrumKitModel";
        drumKitModelInstance.hideFlags = HideFlags.DontSave;
        drumKitModelInstance.transform.localPosition = Vector3.zero;
        drumKitModelInstance.transform.localRotation = Quaternion.Euler(0f, DrumKitYawDegrees, 0f);
        drumKitModelInstance.transform.localScale = Vector3.one;
        SetLayerRecursively(drumKitModelInstance, 0);
        ApplyDrumKitLayoutOffsets(drumKitModelInstance);
        PrepareDrumKitRenderers(drumKitModelInstance);

        if (!TryNormalizeDrumKitModelBounds())
        {
            Debug.LogWarning("[ArcadeHighway3D] The drum kit model has no renderable bounds. Drums gameplay will continue without the 3D kit.");
            DestroyDrumKitVisual();
            return false;
        }

        drumKitRoot.SetActive(wasRootActive);
        drumKitReady = true;
        return true;
    }

    private void LoadDrumKitTextures()
    {
        drumKitFrontBaseColorTexture = Resources.Load<Texture2D>(DrumKitFrontBaseColorTexturePath);
        drumKitFrontNormalTexture = Resources.Load<Texture2D>(DrumKitFrontNormalTexturePath);
        drumKitFrontRmoTexture = Resources.Load<Texture2D>(DrumKitFrontRmoTexturePath);
        drumKitBackBaseColorTexture = Resources.Load<Texture2D>(DrumKitBackBaseColorTexturePath);
        drumKitBackNormalTexture = Resources.Load<Texture2D>(DrumKitBackNormalTexturePath);
        drumKitBackRmoTexture = Resources.Load<Texture2D>(DrumKitBackRmoTexturePath);
    }

    private static void ApplyDrumKitLayoutOffsets(GameObject modelInstance)
    {
        if (modelInstance == null)
            return;

        Transform root = modelInstance.transform;
        OffsetDrumKitPart(root, "PlatilloconPedal_low", new Vector3(-0.25f, 0.0f, 0.05f));
        OffsetDrumKitPart(root, "PlatilloIzquierda_low", new Vector3(-0.34f, 0.04f, -0.08f), 1.02f);
        OffsetDrumKitPart(root, "PlatilloDerecha_low", new Vector3(0.54f, 0.04f, -0.08f), 1.02f);
        OffsetDrumKitPart(root, "BomboIzquierdo_low", new Vector3(-0.24f, 0f, -0.03f));
        OffsetDrumKitPart(root, "BomboGrande_low", new Vector3(0.30f, 0f, -0.03f));
        OffsetDrumKitPart(root, "Izquierdo_low", new Vector3(-0.14f, 0.03f, -0.02f), 1f, "BombosSuperiores_low");
        OffsetDrumKitPart(root, "Derecho_low", new Vector3(0.14f, 0.03f, -0.02f), 1f, "BombosSuperiores_low");
    }

    private static void OffsetDrumKitPart(Transform root, string partName, Vector3 localOffset, float localScaleMultiplier = 1f, string requiredAncestorName = null)
    {
        if (root == null || string.IsNullOrWhiteSpace(partName))
            return;

        Transform[] transforms = root.GetComponentsInChildren<Transform>(true);
        for (int i = 0; i < transforms.Length; i++)
        {
            Transform part = transforms[i];
            if (part == null || !string.Equals(part.name, partName, System.StringComparison.OrdinalIgnoreCase))
                continue;
            if (!string.IsNullOrWhiteSpace(requiredAncestorName) && !HasDrumKitAncestor(part, root, requiredAncestorName))
                continue;

            part.localPosition += localOffset;
            if (!Mathf.Approximately(localScaleMultiplier, 1f))
                part.localScale *= localScaleMultiplier;
        }
    }

    private static bool HasDrumKitAncestor(Transform transform, Transform stopAt, string ancestorName)
    {
        Transform current = transform != null ? transform.parent : null;
        while (current != null && current != stopAt)
        {
            if (string.Equals(current.name, ancestorName, System.StringComparison.OrdinalIgnoreCase))
                return true;

            current = current.parent;
        }

        return false;
    }

    private void PrepareDrumKitRenderers(GameObject modelInstance)
    {
        if (modelInstance == null)
            return;

        drumKitMaterialBindings.Clear();
        drumKitRenderers.Clear();
        drumKitOwnedMeshes.Clear();
        drumKitStaticCombinedRendererCount = 0;
        drumKitStaticCombinedSourceRendererCount = 0;
        drumKitStaticCombineUnreadableSkipCount = 0;
        CombineStaticDrumKitRenderers(modelInstance.transform, modelInstance.GetComponentsInChildren<Renderer>(true));

        Renderer[] renderers = modelInstance.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;
            if (!renderer.enabled || renderer.forceRenderingOff)
                continue;

            drumKitRenderers.Add(renderer);
            string rendererPath = BuildDrumKitRendererSearchPath(renderer, drumKitModelInstance != null ? drumKitModelInstance.transform : null);
            bool interactiveRenderer = IsInteractiveDrumKitRenderer(renderer, rendererPath);
            Material[] sourceMaterials = renderer.sharedMaterials;
            if (sourceMaterials == null || sourceMaterials.Length == 0)
            {
                Material material = interactiveRenderer
                    ? CreateDrumKitMaterial(null, renderer.name, 0)
                    : GetOrCreateDrumKitStaticMaterial(useBackTexture: false);
                renderer.sharedMaterial = material;
                if (interactiveRenderer)
                    RegisterDrumKitMaterialBinding(renderer, material, rendererPath);
            }
            else
            {
                Material[] materials = new Material[sourceMaterials.Length];
                for (int materialIndex = 0; materialIndex < sourceMaterials.Length; materialIndex++)
                {
                    Material source = sourceMaterials[materialIndex];
                    string materialKey = source != null ? source.name : renderer.name;
                    materials[materialIndex] = interactiveRenderer
                        ? CreateDrumKitMaterial(source, materialKey, materialIndex)
                        : GetOrCreateDrumKitStaticMaterial(IsBackDrumKitMaterial(materialKey, materialIndex));
                    if (interactiveRenderer)
                        RegisterDrumKitMaterialBinding(renderer, materials[materialIndex], rendererPath);
                }
                renderer.sharedMaterials = materials;
            }

            ConfigureDrumKitRenderer(renderer);
        }

        RefreshDrumKitLaneTargetSurfaceAvailability();
        RegisterDrumKitCymbalAnimationBindings();
    }

    private void CombineStaticDrumKitRenderers(Transform modelRoot, Renderer[] sourceRenderers)
    {
        if (modelRoot == null || sourceRenderers == null || sourceRenderers.Length == 0)
            return;

        List<CombineInstance> frontCombines = new List<CombineInstance>();
        List<CombineInstance> backCombines = new List<CombineInstance>();
        Matrix4x4 rootWorldToLocal = modelRoot.worldToLocalMatrix;
        for (int i = 0; i < sourceRenderers.Length; i++)
        {
            Renderer renderer = sourceRenderers[i];
            if (renderer == null || !renderer.enabled || renderer.forceRenderingOff)
                continue;

            string rendererPath = BuildDrumKitRendererSearchPath(renderer, modelRoot);
            if (IsInteractiveDrumKitRenderer(renderer, rendererPath))
                continue;
            if (!TryGetDrumKitStaticMesh(renderer, out Mesh mesh) || mesh == null)
                continue;
            if (!mesh.isReadable)
            {
                drumKitStaticCombineUnreadableSkipCount++;
                continue;
            }

            Material[] materials = renderer.sharedMaterials;
            int subMeshCount = Mathf.Max(1, mesh.subMeshCount);
            Matrix4x4 transform = rootWorldToLocal * renderer.transform.localToWorldMatrix;
            for (int subMeshIndex = 0; subMeshIndex < subMeshCount; subMeshIndex++)
            {
                int materialIndex = materials != null && materials.Length > 0
                    ? Mathf.Min(subMeshIndex, materials.Length - 1)
                    : 0;
                Material sourceMaterial = materials != null && materialIndex >= 0 && materialIndex < materials.Length
                    ? materials[materialIndex]
                    : null;
                string materialKey = sourceMaterial != null ? sourceMaterial.name : renderer.name;
                List<CombineInstance> targetCombines = IsBackDrumKitMaterial(materialKey, materialIndex)
                    ? backCombines
                    : frontCombines;
                targetCombines.Add(new CombineInstance
                {
                    mesh = mesh,
                    subMeshIndex = subMeshIndex,
                    transform = transform
                });
            }

            renderer.enabled = false;
            renderer.forceRenderingOff = true;
            drumKitStaticCombinedSourceRendererCount++;
        }

        CreateCombinedStaticDrumKitRenderer(modelRoot, "Front", frontCombines, useBackTexture: false);
        CreateCombinedStaticDrumKitRenderer(modelRoot, "Back", backCombines, useBackTexture: true);
    }

    private void CreateCombinedStaticDrumKitRenderer(Transform modelRoot, string suffix, List<CombineInstance> combines, bool useBackTexture)
    {
        if (modelRoot == null || combines == null || combines.Count == 0)
            return;

        Mesh mesh = new Mesh
        {
            name = $"ArcadeDrumKitStaticCombined{suffix}Mesh",
            hideFlags = HideFlags.DontSave,
            indexFormat = IndexFormat.UInt32
        };
        mesh.CombineMeshes(combines.ToArray(), mergeSubMeshes: true, useMatrices: true, hasLightmapData: false);
        mesh.RecalculateBounds();
        drumKitOwnedMeshes.Add(mesh);

        GameObject combinedObject = new GameObject($"ArcadeDrumKitStaticCombined{suffix}");
        combinedObject.hideFlags = HideFlags.DontSave;
        combinedObject.transform.SetParent(modelRoot, false);
        combinedObject.transform.localPosition = Vector3.zero;
        combinedObject.transform.localRotation = Quaternion.identity;
        combinedObject.transform.localScale = Vector3.one;
        SetLayerRecursively(combinedObject, 0);

        MeshFilter meshFilter = combinedObject.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer meshRenderer = combinedObject.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = GetOrCreateDrumKitStaticMaterial(useBackTexture);
        ConfigureDrumKitRenderer(meshRenderer);
        drumKitStaticCombinedRendererCount++;
    }

    private static bool TryGetDrumKitStaticMesh(Renderer renderer, out Mesh mesh)
    {
        mesh = null;
        if (renderer == null)
            return false;

        MeshFilter meshFilter = renderer.GetComponent<MeshFilter>();
        if (meshFilter == null || meshFilter.sharedMesh == null)
            return false;

        mesh = meshFilter.sharedMesh;
        return true;
    }

    private static bool IsInteractiveDrumKitRenderer(Renderer renderer, string rendererPath)
    {
        string rendererName = renderer != null && renderer.transform != null ? renderer.transform.name : renderer != null ? renderer.name : string.Empty;
        return BuildExplicitDrumKitLaneMask(rendererName) != 0 ||
               BuildDrumKitPathLaneMask(rendererPath, targetSurfaceOnly: true) != 0;
    }

    private Material GetOrCreateDrumKitStaticMaterial(bool useBackTexture)
    {
        if (useBackTexture)
        {
            if (drumKitStaticBackMaterial == null)
            {
                drumKitStaticBackMaterial = CreateDrumKitMaterial(null, "Back", 1);
                drumKitStaticBackMaterial.name = "ArcadeDrumKit_StaticBack";
            }
            return drumKitStaticBackMaterial;
        }

        if (drumKitStaticFrontMaterial == null)
        {
            drumKitStaticFrontMaterial = CreateDrumKitMaterial(null, "Front", 0);
            drumKitStaticFrontMaterial.name = "ArcadeDrumKit_StaticFront";
        }
        return drumKitStaticFrontMaterial;
    }

    private static void ConfigureDrumKitRenderer(Renderer renderer)
    {
        if (renderer == null)
            return;

        renderer.enabled = true;
        renderer.forceRenderingOff = false;
        renderer.allowOcclusionWhenDynamic = false;
        renderer.sortingOrder = DrumKitRenderQueueOffset;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
    }

    private void RegisterDrumKitMaterialBinding(Renderer renderer, Material material, string rendererPath)
    {
        if (renderer == null || material == null)
            return;

        DrumKitMaterialBinding binding = new DrumKitMaterialBinding
        {
            renderer = renderer,
            material = material,
            rendererName = renderer.transform != null ? renderer.transform.name : renderer.name,
            rendererPath = rendererPath,
            localBounds = renderer.localBounds,
            planeMask = GetLargestBoundsPlaneMask(renderer.localBounds)
        };
        InitializeDrumKitBindingCache(binding);
        ApplyDrumKitStaticMaterialState(binding);
        drumKitMaterialBindings.Add(binding);
    }

    private static void InitializeDrumKitBindingCache(DrumKitMaterialBinding binding)
    {
        if (binding == null)
            return;

        binding.targetGlowCenter = new Vector4(
            binding.localBounds.center.x,
            binding.localBounds.center.y,
            binding.localBounds.center.z,
            0f);
        binding.targetGlowExtents = new Vector4(
            Mathf.Max(0.001f, binding.localBounds.extents.x),
            Mathf.Max(0.001f, binding.localBounds.extents.y),
            Mathf.Max(0.001f, binding.localBounds.extents.z),
            0f);
        binding.explicitLaneMask = BuildExplicitDrumKitLaneMask(binding.rendererName);
        binding.targetSurfaceLaneMask = binding.explicitLaneMask | BuildDrumKitPathLaneMask(binding.rendererPath, targetSurfaceOnly: true);
        binding.allLaneMask = binding.explicitLaneMask | BuildDrumKitPathLaneMask(binding.rendererPath, targetSurfaceOnly: false);
        binding.isCymbalSurface = ContainsAnyDrumKitObjectName(binding.rendererPath, DrumKitCymbalSurfaceNames) &&
                                  (ContainsDrumKitObjectName(binding.rendererPath, "PlatilloconPedal") ||
                                   ContainsDrumKitObjectName(binding.rendererPath, "PlatilloIzquierda") ||
                                   ContainsDrumKitObjectName(binding.rendererPath, "PlatilloDerecha"));
        binding.targetDirectionUsesCamera = ContainsDrumKitObjectName(binding.rendererPath, "BomboPrincipal") ||
                                            ContainsDrumKitObjectName(binding.rendererPath, "polySurface16_low");
    }

    private static void ApplyDrumKitStaticMaterialState(DrumKitMaterialBinding binding)
    {
        Material material = binding?.material;
        if (material == null)
            return;

        SetDrumKitMaterialFloat(material, DrumKitHitGlowEdgeStrengthShaderId, DrumKitHitGlowEdgeStrength);
        SetDrumKitMaterialVector(material, DrumKitTargetGlowCenterShaderId, binding.targetGlowCenter);
        SetDrumKitMaterialVector(material, DrumKitTargetGlowExtentsShaderId, binding.targetGlowExtents);
        SetDrumKitMaterialVector(material, DrumKitTargetGlowPlaneMaskShaderId, binding.planeMask);
        SetDrumKitMaterialFloat(material, DrumKitTargetGlowSurfaceModeShaderId, binding.isCymbalSurface ? 1f : 0f);
    }

    private Material CreateDrumKitMaterial(Material source, string materialKey, int materialIndex)
    {
        Shader shader = Resources.Load<Shader>(DrumKitShaderResourcePath);
        if (shader == null)
            shader = Shader.Find("Custom/ArcadeDrumKit");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        Material material = new Material(shader)
        {
            name = source != null ? $"ArcadeDrumKit_{source.name}" : $"ArcadeDrumKit_Material{materialIndex}",
            hideFlags = HideFlags.DontSave
        };

        bool useBackTexture = IsBackDrumKitMaterial(materialKey, materialIndex);
        Texture2D baseColorTexture = useBackTexture ? drumKitBackBaseColorTexture : drumKitFrontBaseColorTexture;
        Texture2D normalTexture = useBackTexture ? drumKitBackNormalTexture : drumKitFrontNormalTexture;
        Texture2D rmoTexture = useBackTexture ? drumKitBackRmoTexture : drumKitFrontRmoTexture;

        SetDrumKitMaterialColor(material, Color.white);
        SetDrumKitMaterialColor(material, "_RimColor", new Color(0.05f, 0.42f, 0.58f, 1f));
        SetDrumKitMaterialColor(material, "_AccentColor", new Color(0.42f, 0.05f, 0.54f, 1f));
        SetDrumKitMaterialColor(material, "_ShellGlowColor", new Color(0.46f, 0.035f, 0.06f, 1f));
        SetDrumKitMaterialVector(material, "_FakeLightDirection", new Vector4(-0.30f, 0.90f, -0.31f, 0f));
        SetDrumKitMaterialFloat(material, "_StageExposure", 0.58f);
        SetDrumKitMaterialColor(material, "_ShadowColor", new Color(0.018f, 0.026f, 0.065f, 1f));
        SetDrumKitMaterialColor(material, "_KeyLightColor", new Color(0.72f, 0.82f, 0.94f, 1f));
        SetDrumKitMaterialColor(material, "_FillLightColor", new Color(0.11f, 0.055f, 0.24f, 1f));
        SetDrumKitMaterialFloat(material, "_AmbientStrength", 0.095f);
        SetDrumKitMaterialFloat(material, "_KeyLightStrength", 0.78f);
        SetDrumKitMaterialFloat(material, "_TopLightStrength", 0.10f);
        SetDrumKitMaterialFloat(material, "_FillLightStrength", 0.035f);
        SetDrumKitMaterialFloat(material, "_PulseSpeed", 4.15f);
        SetDrumKitMaterialFloat(material, "_PulseStrength", 0.055f);
        SetDrumKitMaterialFloat(material, "_StripeStrength", 0.018f);
        SetDrumKitMaterialFloat(material, "_RimStrength", 0.10f);
        SetDrumKitMaterialFloat(material, "_HitGlowEdgeStrength", DrumKitHitGlowEdgeStrength);
        SetDrumKitMaterialColor(material, "_TargetGlowColor", Color.black);
        SetDrumKitMaterialFloat(material, "_TargetGlowStrength", 0f);
        SetDrumKitMaterialVector(material, "_TargetGlowCenter", Vector4.zero);
        SetDrumKitMaterialVector(material, "_TargetGlowExtents", Vector4.one);
        SetDrumKitMaterialVector(material, "_TargetGlowPlaneMask", new Vector4(1f, 1f, 0f, 0f));
        SetDrumKitMaterialFloat(material, "_TargetGlowDepthSide", 1f);
        SetDrumKitMaterialFloat(material, "_TargetGlowSurfaceMode", 0f);
        SetDrumKitMaterialColor(material, "_DrumImpactColor", Color.black);
        SetDrumKitMaterialFloat(material, "_DrumImpactStrength", 0f);
        SetDrumKitMaterialFloat(material, "_DrumImpactProgress", 1f);
        SetDrumKitMaterialColor(material, "_DrumSuccessImpactColor", Color.black);
        SetDrumKitMaterialFloat(material, "_DrumSuccessImpactStrength", 0f);
        SetDrumKitMaterialFloat(material, "_DrumSuccessImpactProgress", 1f);
        SetDrumKitMaterialTexture(material, "_BaseMap", baseColorTexture);
        SetDrumKitMaterialTexture(material, "_MainTex", baseColorTexture);
        material.mainTexture = baseColorTexture;
        SetDrumKitMaterialTexture(material, "_BumpMap", normalTexture);
        SetDrumKitMaterialTexture(material, "_RmoMap", rmoTexture);
        if (normalTexture != null)
            material.EnableKeyword("_NORMALMAP");

        if (material.HasProperty("_Surface"))
            material.SetFloat("_Surface", 0f);
        if (material.HasProperty("_AlphaClip"))
            material.SetFloat("_AlphaClip", 0f);
        if (material.HasProperty("_SrcBlend"))
            material.SetInt("_SrcBlend", (int)BlendMode.One);
        if (material.HasProperty("_DstBlend"))
            material.SetInt("_DstBlend", (int)BlendMode.Zero);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        SetDrumKitMaterialFloat(material, "_Metallic", 0.02f);
        SetDrumKitMaterialFloat(material, "_Smoothness", 0.36f);
        SetDrumKitMaterialFloat(material, "_Glossiness", 0.36f);
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 1);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)CompareFunction.LessEqual);
        material.renderQueue = (int)RenderQueue.Geometry + DrumKitRenderQueueOffset;

        drumKitOwnedMaterials.Add(material);
        return material;
    }

    private static bool IsBackDrumKitMaterial(string materialKey, int materialIndex)
    {
        if (!string.IsNullOrWhiteSpace(materialKey))
        {
            string normalized = materialKey.ToLowerInvariant();
            if (normalized.Contains("traser") || normalized.Contains("back"))
                return true;
            if (normalized.Contains("front"))
                return false;
        }

        return materialIndex == 1;
    }

    private bool TryNormalizeDrumKitModelBounds()
    {
        if (drumKitRoot == null || drumKitModelInstance == null)
            return false;

        if (!TryCalculateLocalBounds(drumKitRoot.transform, out Bounds bounds) || bounds.size.sqrMagnitude <= 0.0001f)
            return false;

        Vector3 centerBottomOffset = new Vector3(bounds.center.x, bounds.min.y, bounds.center.z);
        drumKitModelInstance.transform.localPosition -= centerBottomOffset;

        if (!TryCalculateLocalBounds(drumKitRoot.transform, out drumKitLocalBounds) || drumKitLocalBounds.size.sqrMagnitude <= 0.0001f)
            return false;

        return true;
    }

    private void PositionDrumKit()
    {
        if (drumKitRoot == null || owner == null || mainCamera == null || drumKitLocalBounds.size.sqrMagnitude <= 0.0001f)
            return;

        Transform cameraTransform = mainCamera.transform;
        if (drumKitRoot.transform.parent != cameraTransform)
            drumKitRoot.transform.SetParent(cameraTransform, false);

        drumKitRoot.transform.position = DrumKitTunedWorldPosition;
        drumKitRoot.transform.rotation = DrumKitTunedWorldRotation;
        drumKitRoot.transform.localScale = Vector3.one * DrumKitTunedWorldScale;

        if (!loggedDrumKitDiagnostics)
        {
            loggedDrumKitDiagnostics = true;
            Debug.Log($"[ArcadeHighway3D] Drum kit visible. renderers={CountEnabledRenderers(drumKitRoot)} staticCombinedRenderers={drumKitStaticCombinedRendererCount} staticCombinedSources={drumKitStaticCombinedSourceRendererCount} combineSkippedUnreadable={drumKitStaticCombineUnreadableSkipCount} boundsCenter={drumKitLocalBounds.center} boundsSize={drumKitLocalBounds.size} worldPos={drumKitRoot.transform.position} worldRot={drumKitRoot.transform.rotation.eulerAngles} scale={DrumKitTunedWorldScale:F4}");
        }
    }

    private void UpdateDrumKitShaderState(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateDrumKitShaderStateProfilerMarker.Auto())
        {
            if (drumKitRoot == null || drumKitMaterialBindings.Count == 0)
                return;

            BuildDrumKitLaneGlowStrengths(snapshot);
            for (int bindingIndex = 0; bindingIndex < drumKitMaterialBindings.Count; bindingIndex++)
            {
                DrumKitMaterialBinding binding = drumKitMaterialBindings[bindingIndex];
                Material material = binding?.material;
                if (material == null)
                    continue;

                float strength = GetDrumKitBindingGlowStrength(binding, out Color glowColor);
                float depthSide = GetDrumKitTargetDepthSide(binding);
                GetDrumKitBindingImpactState(
                    binding,
                    out Color impactColor,
                    out float impactStrength,
                    out float impactProgress,
                    out Color successImpactColor,
                    out float successImpactStrength,
                    out float successImpactProgress);
                ApplyDrumKitDynamicMaterialState(
                    binding,
                    glowColor,
                    strength,
                    depthSide,
                    impactColor,
                    impactStrength,
                    impactProgress,
                    successImpactColor,
                    successImpactStrength,
                    successImpactProgress);
            }
        }
    }

    private static void ApplyDrumKitDynamicMaterialState(
        DrumKitMaterialBinding binding,
        Color glowColor,
        float glowStrength,
        float depthSide,
        Color impactColor,
        float impactStrength,
        float impactProgress,
        Color successImpactColor,
        float successImpactStrength,
        float successImpactProgress)
    {
        Material material = binding?.material;
        if (material == null)
            return;

        bool force = !binding.hasDynamicMaterialState;
        if (force || !Approximately(binding.lastTargetGlowColor, glowColor))
        {
            SetDrumKitMaterialColor(material, DrumKitTargetGlowColorShaderId, glowColor);
            binding.lastTargetGlowColor = glowColor;
        }

        if (force || !Mathf.Approximately(binding.lastTargetGlowStrength, glowStrength))
        {
            SetDrumKitMaterialFloat(material, DrumKitTargetGlowStrengthShaderId, glowStrength);
            binding.lastTargetGlowStrength = glowStrength;
        }

        if (force || !Mathf.Approximately(binding.lastTargetGlowDepthSide, depthSide))
        {
            SetDrumKitMaterialFloat(material, DrumKitTargetGlowDepthSideShaderId, depthSide);
            binding.lastTargetGlowDepthSide = depthSide;
        }

        if (force || !Approximately(binding.lastImpactColor, impactColor))
        {
            SetDrumKitMaterialColor(material, DrumKitImpactColorShaderId, impactColor);
            binding.lastImpactColor = impactColor;
        }

        if (force || !Mathf.Approximately(binding.lastImpactStrength, impactStrength))
        {
            SetDrumKitMaterialFloat(material, DrumKitImpactStrengthShaderId, impactStrength);
            binding.lastImpactStrength = impactStrength;
        }

        if (force || !Mathf.Approximately(binding.lastImpactProgress, impactProgress))
        {
            SetDrumKitMaterialFloat(material, DrumKitImpactProgressShaderId, impactProgress);
            binding.lastImpactProgress = impactProgress;
        }

        if (force || !Approximately(binding.lastSuccessImpactColor, successImpactColor))
        {
            SetDrumKitMaterialColor(material, DrumKitSuccessImpactColorShaderId, successImpactColor);
            binding.lastSuccessImpactColor = successImpactColor;
        }

        if (force || !Mathf.Approximately(binding.lastSuccessImpactStrength, successImpactStrength))
        {
            SetDrumKitMaterialFloat(material, DrumKitSuccessImpactStrengthShaderId, successImpactStrength);
            binding.lastSuccessImpactStrength = successImpactStrength;
        }

        if (force || !Mathf.Approximately(binding.lastSuccessImpactProgress, successImpactProgress))
        {
            SetDrumKitMaterialFloat(material, DrumKitSuccessImpactProgressShaderId, successImpactProgress);
            binding.lastSuccessImpactProgress = successImpactProgress;
        }

        binding.hasDynamicMaterialState = true;
    }

    private static bool Approximately(Color a, Color b)
    {
        return Mathf.Abs(a.r - b.r) <= 0.001f &&
               Mathf.Abs(a.g - b.g) <= 0.001f &&
               Mathf.Abs(a.b - b.b) <= 0.001f &&
               Mathf.Abs(a.a - b.a) <= 0.001f;
    }

    private float GetDrumKitBindingGlowStrength(DrumKitMaterialBinding binding, out Color glowColor)
    {
        glowColor = Color.black;
        if (binding == null)
            return 0f;

        float bestStrength = 0f;
        bool hasMappedLane = false;
        for (int lane = 0; lane < Mathf.Min(MaxLaneCount, drumKitLaneGlowStrength.Length); lane++)
        {
            if (!ShouldUseDrumKitBindingForLane(binding, lane))
                continue;

            float laneStrength = drumKitLaneGlowStrength[lane];
            float visibleStrength = Mathf.Max(DrumKitIdleGlowStrength, Mathf.Lerp(DrumKitIdleGlowStrength, 1f, laneStrength));
            if (!hasMappedLane || visibleStrength > bestStrength)
            {
                hasMappedLane = true;
                bestStrength = visibleStrength;
                glowColor = GetLaneColor(lane);
            }
        }

        return hasMappedLane ? bestStrength : 0f;
    }

    private void GetDrumKitBindingImpactState(
        DrumKitMaterialBinding binding,
        out Color impactColor,
        out float impactStrength,
        out float impactProgress,
        out Color successImpactColor,
        out float successImpactStrength,
        out float successImpactProgress)
    {
        impactColor = Color.black;
        successImpactColor = Color.black;
        impactStrength = 0f;
        successImpactStrength = 0f;
        impactProgress = 1f;
        successImpactProgress = 1f;
        if (binding == null)
            return;

        for (int lane = 0; lane < Mathf.Min(MaxLaneCount, drumKitLaneImpactStartedAt.Length); lane++)
        {
            if (!ShouldUseDrumKitBindingForLane(binding, lane))
                continue;

            if (TryGetDrumKitLaneImpactState(lane, success: false, out float laneImpactStrength, out float laneImpactProgress) &&
                laneImpactStrength > impactStrength)
            {
                impactStrength = laneImpactStrength;
                impactProgress = laneImpactProgress;
                impactColor = GetDrumKitImpactColor(lane);
            }

            if (TryGetDrumKitLaneImpactState(lane, success: true, out float laneSuccessStrength, out float laneSuccessProgress) &&
                laneSuccessStrength > successImpactStrength)
            {
                successImpactStrength = laneSuccessStrength;
                successImpactProgress = laneSuccessProgress;
                successImpactColor = GetDrumKitSuccessImpactColor(lane);
            }
        }
    }

    private bool TryGetDrumKitLaneImpactState(int lane, bool success, out float strength, out float progress)
    {
        strength = 0f;
        progress = 1f;
        if (lane < 0 || lane >= drumKitLaneImpactStartedAt.Length)
            return false;

        float startedAt = success ? drumKitLaneSuccessImpactStartedAt[lane] : drumKitLaneImpactStartedAt[lane];
        float baseStrength = success ? drumKitLaneSuccessImpactStrength[lane] : drumKitLaneImpactStrength[lane];
        float duration = success ? DrumKitSuccessImpactDurationSeconds : DrumKitImpactDurationSeconds;
        float elapsed = Time.time - startedAt;
        if (elapsed < 0f || elapsed > duration || baseStrength <= 0f)
            return false;

        progress = Mathf.Clamp01(elapsed / Mathf.Max(0.01f, duration));
        float attack = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(elapsed / (success ? 0.030f : 0.024f)));
        float tail = 1f - Mathf.SmoothStep(0.86f, 1f, progress);
        float energy = Mathf.Lerp(1f, success ? 0.80f : 0.70f, progress) * attack * tail;
        strength = Mathf.Clamp01(baseStrength * energy * (success ? 1f : 0.96f));
        return strength > 0.001f;
    }

    private Color GetDrumKitImpactColor(int lane)
    {
        Color laneColor = GetLaneColor(lane);
        return Color.Lerp(laneColor, new Color(0.95f, 0.98f, 1f, 1f), 0.08f);
    }

    private Color GetDrumKitSuccessImpactColor(int lane)
    {
        Color laneColor = GetLaneColor(lane);
        Color successCore = new Color(0.72f, 1f, 0.64f, 1f);
        Color successSpark = new Color(1f, 0.86f, 0.30f, 1f);
        return Color.Lerp(Color.Lerp(successCore, successSpark, 0.32f), laneColor, 0.24f);
    }

    private void CreateDrumKitSuccessEmberBurst(int lane, float precision)
    {
        if (drumKitRoot == null || owner == null || !drumKitRoot.activeInHierarchy)
            return;
        if (!TryGetDrumKitLaneHitLocalPoint(lane, out Vector3 hitLocalPoint))
            return;

        Color laneColor = GetLaneColor(lane);
        ArcadeFeedbackEffect effect = new ArcadeFeedbackEffect
        {
            root = new GameObject("DrumHitEmbers"),
            startTime = Time.time,
            duration = Mathf.Lerp(0.34f, 0.54f, Mathf.Clamp01(precision)),
            miss = false
        };
        effect.root.transform.SetParent(drumKitRoot.transform, false);
        effect.root.transform.localPosition = hitLocalPoint;
        effect.root.transform.localRotation = Quaternion.identity;
        effect.root.transform.localScale = Vector3.one;

        int emberCount = Mathf.RoundToInt(Mathf.Lerp(8f, 14f, Mathf.Clamp01(precision)));
        for (int i = 0; i < emberCount; i++)
        {
            float seed = (lane + 1) * 31.73f + (i + 1) * 9.17f + Time.time * 3.11f;
            float angle = PseudoRandom01(seed) * Mathf.PI * 2f;
            float radius = Mathf.Lerp(0.004f, 0.034f, PseudoRandom01(seed + 4.7f));
            float side = Mathf.Cos(angle) * Mathf.Lerp(0.07f, 0.18f, PseudoRandom01(seed + 1.3f));
            float back = Mathf.Sin(angle) * Mathf.Lerp(0.05f, 0.16f, PseudoRandom01(seed + 2.1f));
            float upward = Mathf.Lerp(0.16f, 0.34f, PseudoRandom01(seed + 3.9f)) * Mathf.Lerp(0.90f, 1.28f, precision);
            float size = Mathf.Lerp(0.010f, 0.023f, PseudoRandom01(seed + 5.5f));
            Color emberColor = Color.Lerp(
                new Color(1f, 0.34f, 0.055f, 0.88f),
                new Color(1f, 0.86f, 0.22f, 0.92f),
                PseudoRandom01(seed + 6.8f));
            emberColor = Color.Lerp(emberColor, laneColor, 0.18f);

            CreateFeedbackPiece(
                effect,
                "DrumEmber",
                emberColor,
                new Vector3(Mathf.Cos(angle) * radius, 0.010f, Mathf.Sin(angle) * radius),
                new Vector3(size * 0.62f, size, size * 0.62f),
                new Vector3(side, upward, back),
                -0.42f,
                new Vector3(
                    Mathf.Lerp(-320f, 320f, PseudoRandom01(seed + 7.4f)),
                    Mathf.Lerp(-260f, 260f, PseudoRandom01(seed + 8.6f)),
                    Mathf.Lerp(-300f, 300f, PseudoRandom01(seed + 9.2f))));
        }

        feedbackEffects.Add(effect);
    }

    private bool TryGetDrumKitLaneHitLocalPoint(int lane, out Vector3 hitLocalPoint)
    {
        hitLocalPoint = Vector3.zero;
        if (drumKitRoot == null || drumKitMaterialBindings.Count == 0)
            return false;

        DrumKitMaterialBinding bestBinding = null;
        float bestScore = float.NegativeInfinity;
        for (int i = 0; i < drumKitMaterialBindings.Count; i++)
        {
            DrumKitMaterialBinding binding = drumKitMaterialBindings[i];
            if (binding?.renderer == null)
                continue;

            bool targetSurface = DoesDrumKitBindingHandleLane(binding, lane, explicitTargetOnly: false, targetSurfaceOnly: true);
            bool fallbackObject = !targetSurface && DoesDrumKitBindingHandleLane(binding, lane, explicitTargetOnly: false, targetSurfaceOnly: false);
            if (!targetSurface && !fallbackObject)
                continue;

            float score = targetSurface ? 100f : 0f;
            if (IsExplicitDrumKitLaneTarget(binding, lane))
                score += 30f;
            if (mainCamera != null)
                score -= Vector3.Distance(mainCamera.transform.position, binding.renderer.bounds.center) * 0.01f;

            if (score > bestScore)
            {
                bestScore = score;
                bestBinding = binding;
            }
        }

        if (bestBinding?.renderer == null)
            return false;

        Vector3 hitWorldPoint = GetDrumKitBindingHitWorldPoint(bestBinding);
        hitLocalPoint = drumKitRoot.transform.InverseTransformPoint(hitWorldPoint);
        return true;
    }

    private Vector3 GetDrumKitBindingHitWorldPoint(DrumKitMaterialBinding binding)
    {
        Bounds bounds = binding.renderer.bounds;
        Vector3 point = bounds.center;
        string path = binding.rendererPath;
        if (ContainsDrumKitObjectName(path, "BomboPrincipal") ||
            ContainsDrumKitObjectName(path, "polySurface16_low"))
        {
            Vector3 direction = mainCamera != null
                ? mainCamera.transform.position - bounds.center
                : -binding.renderer.transform.forward;
            if (direction.sqrMagnitude > 0.0001f)
                point += direction.normalized * Mathf.Max(0.035f, Mathf.Min(bounds.extents.x, bounds.extents.z) * 0.56f);
            return point + Vector3.up * Mathf.Max(0.018f, bounds.extents.y * 0.10f);
        }

        return point + Vector3.up * Mathf.Max(0.020f, bounds.extents.y * 0.55f);
    }

    private void RefreshDrumKitLaneTargetSurfaceAvailability()
    {
        for (int lane = 0; lane < drumKitLaneHasTargetSurfaceBinding.Length; lane++)
        {
            drumKitLaneHasExplicitTargetBinding[lane] = false;
            drumKitLaneHasTargetSurfaceBinding[lane] = false;
        }

        for (int i = 0; i < drumKitMaterialBindings.Count; i++)
        {
            DrumKitMaterialBinding binding = drumKitMaterialBindings[i];
            for (int lane = 0; lane < drumKitLaneHasTargetSurfaceBinding.Length; lane++)
            {
                int laneBit = GetDrumKitLaneBit(lane);
                if (!drumKitLaneHasExplicitTargetBinding[lane])
                    drumKitLaneHasExplicitTargetBinding[lane] = binding != null && (binding.explicitLaneMask & laneBit) != 0;

                if (!drumKitLaneHasTargetSurfaceBinding[lane])
                    drumKitLaneHasTargetSurfaceBinding[lane] = binding != null && (binding.targetSurfaceLaneMask & laneBit) != 0;
            }
        }
    }

    private bool ShouldUseDrumKitBindingForLane(DrumKitMaterialBinding binding, int lane)
    {
        int laneBit = GetDrumKitLaneBit(lane);
        if (binding == null || laneBit == 0)
            return false;

        if (lane < drumKitLaneHasExplicitTargetBinding.Length && drumKitLaneHasExplicitTargetBinding[lane])
            return (binding.explicitLaneMask & laneBit) != 0;
        if (lane < drumKitLaneHasTargetSurfaceBinding.Length && drumKitLaneHasTargetSurfaceBinding[lane])
            return (binding.targetSurfaceLaneMask & laneBit) != 0;

        return (binding.allLaneMask & laneBit) != 0;
    }

    private static bool DoesDrumKitBindingHandleLane(DrumKitMaterialBinding binding, int lane, bool explicitTargetOnly, bool targetSurfaceOnly)
    {
        int laneBit = GetDrumKitLaneBit(lane);
        if (binding == null || laneBit == 0)
            return false;

        if (explicitTargetOnly)
            return (binding.explicitLaneMask & laneBit) != 0;
        if (targetSurfaceOnly)
            return (binding.targetSurfaceLaneMask & laneBit) != 0;

        return (binding.allLaneMask & laneBit) != 0;
    }

    private static int BuildExplicitDrumKitLaneMask(string rendererName)
    {
        int mask = 0;
        if (string.IsNullOrWhiteSpace(rendererName))
            return mask;

        for (int lane = 0; lane < DrumKitExplicitHitTargetNames.Length && lane < MaxLaneCount; lane++)
        {
            if (MatchesAnyExplicitDrumKitObjectName(rendererName, DrumKitExplicitHitTargetNames[lane]))
                mask |= GetDrumKitLaneBit(lane);
        }

        return mask;
    }

    private static int BuildDrumKitPathLaneMask(string path, bool targetSurfaceOnly)
    {
        if (string.IsNullOrWhiteSpace(path))
            return 0;

        int mask = 0;
        if (MatchesDrumKitLaneObject(path, "PlatilloconPedal", DrumKitCymbalSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(0); // Hi-hat
        if (MatchesDrumKitLaneObject(path, "PlatilloIzquierda", DrumKitCymbalSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(1); // Crash
        if (MatchesDrumKitLaneObject(path, "BomboIzquierdo", DrumKitSnareSurfaceNames, targetSurfaceOnly) ||
            (!targetSurfaceOnly && ContainsDrumKitObjectName(path, "Caja")))
            mask |= GetDrumKitLaneBit(2); // Snare
        if (MatchesDrumKitLaneObject(path, "BombosSuperiores", "Izquierdo", DrumKitRackTomSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(3); // High tom
        if (MatchesDrumKitLaneObject(path, "BomboPrincipal", DrumKitKickSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(4); // Kick
        if (MatchesDrumKitLaneObject(path, "BombosSuperiores", "Derecho", DrumKitRackTomSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(5); // Mid tom
        if (MatchesDrumKitLaneObject(path, "BomboGrande", DrumKitFloorTomSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(6); // Floor tom
        if (MatchesDrumKitLaneObject(path, "PlatilloDerecha", DrumKitCymbalSurfaceNames, targetSurfaceOnly))
            mask |= GetDrumKitLaneBit(7); // Ride

        return mask;
    }

    private static int GetDrumKitLaneBit(int lane)
    {
        return lane >= 0 && lane < MaxLaneCount ? 1 << lane : 0;
    }

    private static bool IsExplicitDrumKitLaneTarget(DrumKitMaterialBinding binding, int lane)
    {
        int laneBit = GetDrumKitLaneBit(lane);
        return binding != null && laneBit != 0 && (binding.explicitLaneMask & laneBit) != 0;
    }

    private static bool MatchesAnyExplicitDrumKitObjectName(string rendererName, string[] objectNames)
    {
        if (string.IsNullOrWhiteSpace(rendererName) || objectNames == null)
            return false;

        for (int i = 0; i < objectNames.Length; i++)
        {
            if (MatchesExplicitDrumKitObjectName(rendererName, objectNames[i]))
                return true;
        }

        return false;
    }

    private static bool MatchesExplicitDrumKitObjectName(string rendererName, string objectName)
    {
        if (string.IsNullOrWhiteSpace(rendererName) || string.IsNullOrWhiteSpace(objectName))
            return false;

        return string.Equals(rendererName, objectName, System.StringComparison.OrdinalIgnoreCase) ||
               rendererName.StartsWith(objectName + ".", System.StringComparison.OrdinalIgnoreCase) ||
               rendererName.StartsWith(objectName + "_", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool MatchesDrumKitLaneObject(string path, string objectName, string[] targetSurfaceNames, bool targetSurfaceOnly)
    {
        return ContainsDrumKitObjectName(path, objectName) &&
               (!targetSurfaceOnly || ContainsAnyDrumKitObjectName(path, targetSurfaceNames));
    }

    private static bool MatchesDrumKitLaneObject(string path, string parentObjectName, string childObjectName, string[] targetSurfaceNames, bool targetSurfaceOnly)
    {
        return ContainsDrumKitObjectName(path, parentObjectName) &&
               ContainsDrumKitObjectName(path, childObjectName) &&
               (!targetSurfaceOnly || ContainsAnyDrumKitObjectName(path, targetSurfaceNames));
    }

    private static bool ContainsAnyDrumKitObjectName(string path, string[] objectNames)
    {
        if (objectNames == null)
            return false;

        for (int i = 0; i < objectNames.Length; i++)
        {
            if (ContainsDrumKitObjectName(path, objectNames[i]))
                return true;
        }

        return false;
    }

    private static bool ContainsDrumKitObjectName(string path, string objectName)
    {
        if (string.IsNullOrWhiteSpace(path) || string.IsNullOrWhiteSpace(objectName))
            return false;

        return path.IndexOf(objectName, System.StringComparison.OrdinalIgnoreCase) >= 0;
    }

    private static bool IsDrumKitCymbalSurfaceBinding(DrumKitMaterialBinding binding)
    {
        return binding != null && binding.isCymbalSurface;
    }

    private void RegisterDrumKitCymbalAnimationBindings()
    {
        drumKitCymbalAnimationBindings.Clear();
        HashSet<Transform> registeredTransforms = new HashSet<Transform>();
        for (int i = 0; i < drumKitMaterialBindings.Count; i++)
        {
            DrumKitMaterialBinding binding = drumKitMaterialBindings[i];
            if (binding?.renderer == null || !IsDrumKitCymbalSurfaceBinding(binding))
                continue;

            int lane = ResolveDrumKitCymbalLane(binding.rendererPath);
            if (lane < 0)
                continue;

            Transform cymbalTransform = binding.renderer.transform;
            if (cymbalTransform == null || !registeredTransforms.Add(cymbalTransform))
                continue;

            drumKitCymbalAnimationBindings.Add(new DrumKitCymbalAnimationBinding
            {
                lane = lane,
                transform = cymbalTransform,
                baseLocalPosition = cymbalTransform.localPosition,
                baseLocalRotation = cymbalTransform.localRotation,
                baseLocalScale = cymbalTransform.localScale,
                phase = PseudoRandom01(lane * 23.71f + cymbalTransform.GetInstanceID() * 0.0031f) * Mathf.PI * 2f
            });
        }
    }

    private static int ResolveDrumKitCymbalLane(string path)
    {
        if (ContainsDrumKitObjectName(path, "PlatilloconPedal"))
            return 0;
        if (ContainsDrumKitObjectName(path, "PlatilloIzquierda"))
            return 1;
        if (ContainsDrumKitObjectName(path, "PlatilloDerecha"))
            return 7;

        return -1;
    }

    private static string BuildTransformPath(Transform transform, Transform stopAt)
    {
        if (transform == null)
            return string.Empty;

        string path = transform.name;
        Transform current = transform.parent;
        while (current != null && current != stopAt)
        {
            path = current.name + "/" + path;
            current = current.parent;
        }

        return path;
    }

    private static string BuildDrumKitRendererSearchPath(Renderer renderer, Transform stopAt)
    {
        if (renderer == null)
            return string.Empty;

        string path = BuildTransformPath(renderer.transform, stopAt);
        string meshName = GetRendererMeshName(renderer);
        if (!string.IsNullOrWhiteSpace(meshName) &&
            path.IndexOf(meshName, System.StringComparison.OrdinalIgnoreCase) < 0)
        {
            path = string.IsNullOrWhiteSpace(path)
                ? meshName
                : path + "/" + meshName;
        }

        return path;
    }

    private static string GetRendererMeshName(Renderer renderer)
    {
        if (renderer is SkinnedMeshRenderer skinnedMeshRenderer &&
            skinnedMeshRenderer.sharedMesh != null)
        {
            return skinnedMeshRenderer.sharedMesh.name;
        }

        MeshFilter meshFilter = renderer != null ? renderer.GetComponent<MeshFilter>() : null;
        return meshFilter != null && meshFilter.sharedMesh != null
            ? meshFilter.sharedMesh.name
            : string.Empty;
    }

    private float GetDrumKitTargetDepthSide(DrumKitMaterialBinding binding)
    {
        if (binding?.renderer == null)
            return 1f;

        Vector3 localDepthAxis = GetDepthAxisFromPlaneMask(binding.planeMask);
        Vector3 worldDepthAxis = binding.renderer.transform.TransformDirection(localDepthAxis);
        if (worldDepthAxis.sqrMagnitude <= 0.0001f)
            return 1f;

        worldDepthAxis.Normalize();
        Vector3 targetDirection = GetDrumKitTargetWorldDirection(binding);
        if (targetDirection.sqrMagnitude <= 0.0001f)
            return 1f;

        targetDirection.Normalize();
        return Vector3.Dot(worldDepthAxis, targetDirection) >= 0f ? 1f : -1f;
    }

    private Vector3 GetDrumKitTargetWorldDirection(DrumKitMaterialBinding binding)
    {
        if (binding != null && binding.targetDirectionUsesCamera)
        {
            Bounds bounds = binding.renderer.bounds;
            if (mainCamera != null)
                return mainCamera.transform.position - bounds.center;
        }

        return Vector3.up;
    }

    private static Vector3 GetDepthAxisFromPlaneMask(Vector4 planeMask)
    {
        Vector3 depthMask = Vector3.one - new Vector3(planeMask.x, planeMask.y, planeMask.z);
        if (depthMask.x >= depthMask.y && depthMask.x >= depthMask.z)
            return Vector3.right;
        if (depthMask.y >= depthMask.x && depthMask.y >= depthMask.z)
            return Vector3.up;

        return Vector3.forward;
    }

    private static Vector4 GetLargestBoundsPlaneMask(Bounds bounds)
    {
        Vector3 size = bounds.size;
        if (size.x <= size.y && size.x <= size.z)
            return new Vector4(0f, 1f, 1f, 0f);
        if (size.y <= size.x && size.y <= size.z)
            return new Vector4(1f, 0f, 1f, 0f);

        return new Vector4(1f, 1f, 0f, 0f);
    }

    private void BuildDrumKitLaneGlowStrengths(GuitarGameplaySnapshot snapshot)
    {
        for (int lane = 0; lane < drumKitLaneGlowStrength.Length; lane++)
            drumKitLaneGlowStrength[lane] = 0f;

        if (snapshot?.arcadeNoteStates == null)
            return;

        int laneCount = GetBuiltLaneCount();
        float start = snapshot.songTime - Mathf.Max(0.05f, owner != null ? owner.arcadeHitWindowLate : DrumKitHitGlowHoldSeconds);
        float end = snapshot.songTime + DrumKitHitGlowLookaheadSeconds;
        for (int i = 0; i < snapshot.arcadeNoteStates.Count; i++)
        {
            ArcadeNoteState state = snapshot.arcadeNoteStates[i];
            if (state == null || state.IsResolved || state.data.time < start || state.data.time > end)
                continue;

            float leadTime = state.data.time - snapshot.songTime;
            float normalized = Mathf.InverseLerp(DrumKitHitGlowLookaheadSeconds, -DrumKitHitGlowHoldSeconds, leadTime);
            float strength = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(normalized));
            if (leadTime <= 0.05f)
                strength = Mathf.Max(strength, 0.95f);

            if (state.data.isOpen)
            {
                for (int lane = 0; lane < Mathf.Min(laneCount, drumKitLaneGlowStrength.Length); lane++)
                    drumKitLaneGlowStrength[lane] = Mathf.Max(drumKitLaneGlowStrength[lane], strength);
            }
            else if (state.data.lane >= 0 && state.data.lane < laneCount && state.data.lane < drumKitLaneGlowStrength.Length)
            {
                drumKitLaneGlowStrength[state.data.lane] = Mathf.Max(drumKitLaneGlowStrength[state.data.lane], strength);
            }
        }
    }

    private static int CountEnabledRenderers(GameObject target)
    {
        if (target == null)
            return 0;

        Renderer[] renderers = target.GetComponentsInChildren<Renderer>(true);
        int count = 0;
        for (int i = 0; i < renderers.Length; i++)
        {
            if (renderers[i] != null && renderers[i].enabled && !renderers[i].forceRenderingOff)
                count++;
        }

        return count;
    }

    private void DestroyDrumKitVisual()
    {
        ResetDrumKitCymbalAnimations();
        ResetDrumKitImpactAnimations();
        if (drumKitRoot != null)
            Object.Destroy(drumKitRoot);

        for (int i = 0; i < drumKitOwnedMaterials.Count; i++)
        {
            if (drumKitOwnedMaterials[i] != null)
                Object.Destroy(drumKitOwnedMaterials[i]);
        }

        for (int i = 0; i < drumKitOwnedMeshes.Count; i++)
        {
            if (drumKitOwnedMeshes[i] != null)
                Object.Destroy(drumKitOwnedMeshes[i]);
        }

        drumKitOwnedMaterials.Clear();
        drumKitOwnedMeshes.Clear();
        drumKitMaterialBindings.Clear();
        drumKitCymbalAnimationBindings.Clear();
        drumKitRenderers.Clear();
        drumKitRoot = null;
        drumKitModelInstance = null;
        drumKitFrontBaseColorTexture = null;
        drumKitFrontNormalTexture = null;
        drumKitBackBaseColorTexture = null;
        drumKitBackNormalTexture = null;
        drumKitFrontRmoTexture = null;
        drumKitBackRmoTexture = null;
        drumKitStaticFrontMaterial = null;
        drumKitStaticBackMaterial = null;
        drumKitLocalBounds = new Bounds(Vector3.zero, Vector3.zero);
        drumKitLoadAttempted = false;
        drumKitReady = false;
        loggedDrumKitDiagnostics = false;
        drumKitStaticCombinedRendererCount = 0;
        drumKitStaticCombinedSourceRendererCount = 0;
        drumKitStaticCombineUnreadableSkipCount = 0;
    }

    private static bool TryCalculateLocalBounds(Transform rootTransform, out Bounds bounds)
    {
        bounds = new Bounds(Vector3.zero, Vector3.zero);
        if (rootTransform == null)
            return false;

        Renderer[] renderers = rootTransform.GetComponentsInChildren<Renderer>(true);
        bool hasBounds = false;
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null)
                continue;

            Bounds worldBounds = renderer.bounds;
            Vector3 min = worldBounds.min;
            Vector3 max = worldBounds.max;
            Vector3[] corners =
            {
                new Vector3(min.x, min.y, min.z),
                new Vector3(min.x, min.y, max.z),
                new Vector3(min.x, max.y, min.z),
                new Vector3(min.x, max.y, max.z),
                new Vector3(max.x, min.y, min.z),
                new Vector3(max.x, min.y, max.z),
                new Vector3(max.x, max.y, min.z),
                new Vector3(max.x, max.y, max.z)
            };

            for (int cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
            {
                Vector3 localCorner = rootTransform.InverseTransformPoint(corners[cornerIndex]);
                if (!hasBounds)
                {
                    bounds = new Bounds(localCorner, Vector3.zero);
                    hasBounds = true;
                }
                else
                {
                    bounds.Encapsulate(localCorner);
                }
            }
        }

        return hasBounds;
    }

    private static void SetDrumKitMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
    }

    private static void SetDrumKitMaterialColor(Material material, string propertyName, Color color)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetColor(propertyName, color);
    }

    private static void SetDrumKitMaterialColor(Material material, int propertyId, Color color)
    {
        if (material != null)
            material.SetColor(propertyId, color);
    }

    private static void SetDrumKitMaterialFloat(Material material, string propertyName, float value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetFloat(propertyName, value);
    }

    private static void SetDrumKitMaterialFloat(Material material, int propertyId, float value)
    {
        if (material != null)
            material.SetFloat(propertyId, value);
    }

    private static void SetDrumKitMaterialVector(Material material, string propertyName, Vector4 value)
    {
        if (material != null && material.HasProperty(propertyName))
            material.SetVector(propertyName, value);
    }

    private static void SetDrumKitMaterialVector(Material material, int propertyId, Vector4 value)
    {
        if (material != null)
            material.SetVector(propertyId, value);
    }

    private static void SetDrumKitMaterialTexture(Material material, string propertyName, Texture texture)
    {
        if (material != null && texture != null && material.HasProperty(propertyName))
            material.SetTexture(propertyName, texture);
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
        if (!IsBackgroundEnabled())
        {
            if (backgroundCamera != null)
                backgroundCamera.enabled = false;
            return;
        }

        if (backgroundCamera != null && backgroundProfile != BackgroundProfile.Gameplay)
        {
            backgroundCamera.enabled = false;
            return;
        }

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

        GuitarBridgeServer.TabsBackgroundMode activeBackgroundMode = owner.GetBackgroundModeForContext(ToOwnerBackgroundContext(backgroundProfile));
        if (activeBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.BlueSky)
        {
            if (backgroundProfile == BackgroundProfile.Gameplay)
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

            switch (owner.tabSkyMood)
            {
                case GuitarBridgeServer.TabsSkyMood.Sunset:
                    return owner.tabSkySunsetBottomColor;
                case GuitarBridgeServer.TabsSkyMood.Midnight:
                    return owner.tabSkyMidnightBottomColor;
                default:
                    return owner.tabSkyBottomColor;
            }
        }

        if (activeBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.NeonStage)
            return new Color(0.006f, 0.008f, 0.030f, 1f);

        return owner.tabBackgroundColor;
    }

    private static BackgroundProfile ResolveBackgroundProfile(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot != null && snapshot.showMiniGames)
            return BackgroundProfile.MiniGames;

        if (snapshot != null && (snapshot.mainMenuFlowActive || snapshot.showToneLab || snapshot.showTuner))
            return BackgroundProfile.MainMenu;

        return BackgroundProfile.Gameplay;
    }

    private static GuitarBridgeServer.TabsBackgroundContext ToOwnerBackgroundContext(BackgroundProfile profile)
    {
        switch (profile)
        {
            case BackgroundProfile.MainMenu:
                return GuitarBridgeServer.TabsBackgroundContext.MainMenu;
            case BackgroundProfile.MiniGames:
                return GuitarBridgeServer.TabsBackgroundContext.MiniGames;
            case BackgroundProfile.Gameplay:
            default:
                return GuitarBridgeServer.TabsBackgroundContext.Gameplay;
        }
    }

    private void EnsureBackgroundMode(BackgroundProfile profile)
    {
        if (profile != backgroundProfile || backgroundSignature != GetBackgroundSignature(profile))
            InitializeBackgroundEffect(profile);
    }

    private string GetBackgroundSignature(BackgroundProfile profile)
    {
        if (owner == null)
            return string.Empty;

        return owner.GetBackgroundSignatureForContext(ToOwnerBackgroundContext(profile));
    }

    private void ConfigureMiniGameBackgroundCamera()
    {
        if (backgroundCamera == null || owner == null)
            return;

        backgroundCamera.enabled = true;
        backgroundCamera.orthographic = true;
        backgroundCamera.orthographicSize = owner.tabCameraSize;
        backgroundCamera.clearFlags = CameraClearFlags.SolidColor;
        backgroundCamera.backgroundColor = GetCameraBackgroundColor();
        backgroundCamera.cullingMask = 1 << BackgroundLayer;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
        backgroundCamera.nearClipPlane = 0.01f;
        backgroundCamera.farClipPlane = Mathf.Max(100f, owner.highwayCameraFarClip);
        backgroundCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
        backgroundCamera.transform.rotation = Quaternion.identity;
    }

    private bool IsMiniGameEnviroSkyActive()
    {
        return owner != null &&
               owner.GetBackgroundModeForContext(GuitarBridgeServer.TabsBackgroundContext.MiniGames) == GuitarBridgeServer.TabsBackgroundMode.NeonStage &&
               owner.GetNeonStageSkyDesign(false) == GuitarBridgeServer.TabsNeonStageSkyDesign.Enviro3;
    }

    private Camera GetBackgroundEffectRenderCamera(BackgroundProfile profile)
    {
        return profile == BackgroundProfile.MiniGames && backgroundCamera != null && !IsMiniGameEnviroSkyActive()
            ? backgroundCamera
            : mainCamera;
    }

    private bool ShouldUsePerspectiveBackgroundCamera()
    {
        if (owner == null || backgroundProfile == BackgroundProfile.Gameplay)
            return false;

        GuitarBridgeServer.TabsBackgroundContext context = ToOwnerBackgroundContext(backgroundProfile);
        if (owner.GetBackgroundModeForContext(context) != GuitarBridgeServer.TabsBackgroundMode.NeonStage)
            return false;

        bool useMainMenuProfile = context == GuitarBridgeServer.TabsBackgroundContext.MainMenu;
        return owner.GetNeonStageSkyDesign(useMainMenuProfile) == GuitarBridgeServer.TabsNeonStageSkyDesign.Enviro3;
    }

    private void SetBackgroundEffectRenderCamera(Camera camera)
    {
        if (backgroundEffect is TabsNeonStageBackground neonStageBackground)
            neonStageBackground.SetRenderCamera(camera);
        if (backgroundEffect is TabsBlueSkyBackground blueSkyBackground)
            blueSkyBackground.SetRenderCamera(camera);
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

        if (backgroundProfile == BackgroundProfile.MainMenu)
        {
            bool usePerspectiveCamera = ShouldUsePerspectiveBackgroundCamera();
            if (backgroundCamera != null)
                backgroundCamera.enabled = false;

            mainCamera.orthographic = !usePerspectiveCamera;
            if (!usePerspectiveCamera)
                mainCamera.orthographicSize = owner.tabCameraSize;
            mainCamera.clearFlags = usePerspectiveCamera ? CameraClearFlags.Skybox : CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask | (1 << BackgroundLayer);
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
            if (usePerspectiveCamera)
            {
                mainCamera.transform.position = new Vector3(0f, owner.highwayCameraY, owner.highwayCameraZ);
                mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
                mainCamera.fieldOfView = 60f;
            }
            else
            {
                mainCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
                mainCamera.transform.rotation = Quaternion.identity;
            }

            mainCamera.backgroundColor = GetCameraBackgroundColor();
            SetBackgroundEffectRenderCamera(mainCamera);
            ApplyHostCameraOverrides();
            return;
        }

        if (backgroundProfile == BackgroundProfile.MiniGames)
        {
            bool useEnviroSkyCamera = IsMiniGameEnviroSkyActive();
            if (useEnviroSkyCamera)
            {
                if (backgroundCamera != null)
                    backgroundCamera.enabled = false;
            }
            else
            {
                ConfigureMiniGameBackgroundCamera();
            }

            mainCamera.orthographic = false;
            mainCamera.clearFlags = useEnviroSkyCamera
                ? CameraClearFlags.Skybox
                : backgroundCamera != null
                    ? CameraClearFlags.Depth
                    : CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = useEnviroSkyCamera
                    ? (originalMainCameraCullingMask | (1 << BackgroundLayer) | MiniGameFightStage3DRenderer.StageUnityLayerMask)
                    : (originalMainCameraCullingMask & ~(1 << BackgroundLayer)) | MiniGameFightStage3DRenderer.StageUnityLayerMask;
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
            mainCamera.transform.position = new Vector3(0f, owner.highwayCameraY, owner.highwayCameraZ);
            mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
            mainCamera.fieldOfView = 60f;
            mainCamera.backgroundColor = GetCameraBackgroundColor();
            SetBackgroundEffectRenderCamera(GetBackgroundEffectRenderCamera(backgroundProfile));
            ApplyHostCameraOverrides();
            return;
        }

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
        SetBackgroundEffectRenderCamera(mainCamera);
        SyncBackgroundCamera();
        ApplyHostCameraOverrides();
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
        deck.transform.localPosition = new Vector3(0f, LaneSurfaceY - 0.025f, surfaceCenterZ);
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
            laneSurface.transform.localPosition = new Vector3(GetLaneX(lane), LaneSurfaceY, surfaceCenterZ);
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
            laneGuide.transform.localPosition = new Vector3(GetBoundaryX(boundary), LaneSurfaceY + 0.064f, guideCenterZ);
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
            endpoint.transform.localPosition = new Vector3(GetLaneX(lane), EndpointY, owner.StrikeLineZ);
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
        strikeLine.transform.localPosition = new Vector3(0f, EndpointY - 0.04f, owner.StrikeLineZ - 0.04f);
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
        rail.transform.localPosition = new Vector3(side * ((trackWidth * 0.5f) + laneWidth * 0.08f), LaneSurfaceY + 0.05f, centerZ);
        rail.transform.localScale = new Vector3(Mathf.Max(0.04f, laneWidth * 0.04f), 0.075f, guideDepth);
        Object.Destroy(rail.GetComponent<Collider>());
        Material railMaterial = owner.CreateSharedTransparentMaterial(new Color(0.74f, 0.86f, 1f, 0.20f), 0.12f);
        ConfigureOverlayMaterial(railMaterial, 65, true);
        rail.GetComponent<Renderer>().material = railMaterial;
    }

    private void UpdateLaneVisuals(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateLaneVisualsProfilerMarker.Auto())
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
    }

    private bool[] BuildHeldLaneSnapshot()
    {
        int laneCount = GetBuiltLaneCount();
        System.Array.Clear(heldLaneSnapshot, 0, heldLaneSnapshot.Length);
        for (int lane = 0; lane < laneCount; lane++)
            heldLaneSnapshot[lane] = owner != null && owner.IsArcadeInputLaneHeld(lane);
        return heldLaneSnapshot;
    }

    private bool[] BuildIncomingLaneSnapshot(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = GetBuiltLaneCount();
        System.Array.Clear(incomingLaneSnapshot, 0, incomingLaneSnapshot.Length);
        if (snapshot?.arcadeNoteStates == null)
            return incomingLaneSnapshot;

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
                    incomingLaneSnapshot[lane] = true;
            }
            else if (state.data.lane >= 0 && state.data.lane < laneCount)
            {
                incomingLaneSnapshot[state.data.lane] = true;
            }
        }

        return incomingLaneSnapshot;
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
            {
                SetLanePulse(lane, hit ? Mathf.Lerp(0.76f, 1.15f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);
                if (hit)
                {
                    TriggerDrumKitLaneSuccessImpact(lane, Mathf.Lerp(0.88f, 1f, precision));
                    CreateDrumKitSuccessEmberBurst(lane, precision);
                }
            }

            CreateFeedbackBurst(0f, Mathf.Max(owner.FretSpacing, GetTrackWidth(laneCount) * 0.92f), color, hit, precision, openNote: true);
        }
        else
        {
            int lane = Mathf.Clamp(state.data.lane, 0, GetBuiltLaneCount() - 1);
            SetLanePulse(lane, hit ? Mathf.Lerp(0.85f, 1.25f, precision) : -1f, hit ? HitPulseDuration : MissPulseDuration);
            if (hit)
            {
                TriggerDrumKitLaneSuccessImpact(lane, Mathf.Lerp(0.92f, 1f, precision));
                CreateDrumKitSuccessEmberBurst(lane, precision);
            }
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

        for (int lane = 0; lane < drumKitLaneLastCymbalTriggerTime.Length; lane++)
            drumKitLaneLastCymbalTriggerTime[lane] = -100f;

        ResetDrumKitImpactAnimations();

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
        effect.root.transform.localPosition = new Vector3(x, EndpointY + (hit ? 0.10f : 0.08f), owner.StrikeLineZ - 0.035f);

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
        using (UpdateFeedbackEffectsProfilerMarker.Auto())
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

    private Vector3 GetCurrentRendererRootWorldOffset()
    {
        return currentRendererRootWorldOffset;
    }

    private void ApplyRendererRootPlacement()
    {
        if (root == null)
            return;

        root.transform.position = currentRendererRootWorldOffset;
        root.transform.rotation = Quaternion.identity;
        root.transform.localScale = Vector3.one;
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

                float visualStrikeLineZ = GetVisualStrikeLineZ();
                float visualSpawnZ = GetVisualSpawnZ();
                float travelZ = visualStrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
                // Keep sustain tails tied to the real song clock so they do not collapse
                // when the next note head is intentionally parked at spawn during long gaps.
                float sustainEndZ = visualStrikeLineZ + (((state.data.time + Mathf.Max(0f, state.data.duration)) - sustainSongTime) * currentVisualNoteSpeed);
                bool keepResolvedBriefly = state.IsResolved && owner.ArcadeResolvedHoldTime > 0f && renderSongTime <= state.resolvedAt + owner.ArcadeResolvedHoldTime;
                bool showHead = travelZ <= visualSpawnZ && travelZ >= visualStrikeLineZ && (!state.IsResolved || keepResolvedBriefly);
                bool showSustain = Mathf.Max(0f, state.data.duration) > 0.08f &&
                                   sustainEndZ > visualStrikeLineZ &&
                                   travelZ <= visualSpawnZ;
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
        float visualStrikeLineZ = GetVisualStrikeLineZ();
        float visualSpawnZ = GetVisualSpawnZ();
        view.root.transform.position = new Vector3(x, NoteY + currentRendererRootWorldOffset.y, visualStrikeLineZ);
        float bodyWorldZ = showHead ? Mathf.Clamp(headZ, visualStrikeLineZ, visualSpawnZ) : visualStrikeLineZ;
        float bodyLocalZ = bodyWorldZ - visualStrikeLineZ;
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
                    ? Mathf.Min(visualSpawnZ, bodyWorldZ + Mathf.Max(0.02f, bodyScale.z * 0.5f))
                    : visualStrikeLineZ;
                float clippedEndZ = Mathf.Min(visualSpawnZ, sustainEndZ);
                float tailLength = Mathf.Max(0f, clippedEndZ - sustainStartZ);
                showSustain = tailLength > 0.02f;
                if (showSustain)
                {
                    float baseWidth = state.data.isOpen ? width * 0.88f : Mathf.Max(0.16f, owner.FretSpacing * 0.08f);
                    float baseHeight = Mathf.Max(0.08f, bodyScale.y * 0.42f);
                    float animatedWidth = sustainActivelyHeld ? baseWidth * Mathf.Lerp(1f, SustainActiveWidthScale, sustainPulse01) : baseWidth;
                    float animatedHeight = sustainActivelyHeld ? baseHeight * Mathf.Lerp(1f, SustainActiveHeightScale, sustainPulse01) : baseHeight;
                    view.sustain.transform.localPosition = new Vector3(0f, -0.02f, ((sustainStartZ + clippedEndZ) * 0.5f) - visualStrikeLineZ);
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

    private static float PseudoRandom01(float seed)
    {
        return Mathf.Repeat(Mathf.Sin(seed * 12.9898f) * 43758.5453f, 1f);
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

    private float GetVisualStrikeLineZ()
    {
        return (owner != null ? owner.StrikeLineZ : 0f) + currentRendererRootWorldOffset.z;
    }

    private float GetVisualSpawnZ()
    {
        return (owner != null ? owner.ArcadeSpawnZ : 0f) + currentRendererRootWorldOffset.z;
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
        if (renderHost?.LaneCountOverride.HasValue == true)
            return Mathf.Clamp(renderHost.LaneCountOverride.Value, 1, MaxLaneCount);

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
            case 5:
                return new Color(0.95f, 0.58f, 0.18f, 1f);
            case 6:
                return new Color(0.10f, 0.78f, 0.82f, 1f);
            case 7:
                return new Color(0.82f, 0.34f, 0.95f, 1f);
            default:
                return Color.white;
        }
    }
}
