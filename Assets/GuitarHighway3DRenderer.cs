using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

public sealed class GuitarHighway3DRenderer : IGuitarGameplayRenderer
{
    private static readonly ProfilerMarker RenderProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.Render");
    private static readonly ProfilerMarker UpdateStringVisualsProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateStringVisuals");
    private static readonly ProfilerMarker UpdateFretBoundariesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateFretBoundaries");
    private static readonly ProfilerMarker UpdateLaneGuidesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateLaneGuides");
    private static readonly ProfilerMarker UpdateLaneSurfacesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateLaneSurfaces");
    private static readonly ProfilerMarker UpdateNotesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateNotes");
    private static readonly ProfilerMarker UpdateNotesIterateProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateNotes.Iterate");
    private static readonly ProfilerMarker UpdateNotesCleanupProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateNotes.Cleanup");
    private static readonly ProfilerMarker CreateNoteViewProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.CreateNoteView");
    private static readonly ProfilerMarker UpdateNoteViewProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateNoteView");
    private static readonly ProfilerMarker UpdateTechniqueViewProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateTechniqueView");
    private static readonly ProfilerMarker UpdateSlideTechniqueProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateSlideTechnique");
    private static readonly ProfilerMarker UpdateBendTechniqueProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateBendTechnique");
    private static readonly ProfilerMarker UpdateNoteSustainTechniqueProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateNoteSustainTechnique");
    private static readonly ProfilerMarker UpdateTechniqueSegmentRibbonsProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateTechniqueSegmentRibbons");
    private static readonly ProfilerMarker RebuildVisibleNoteStateCacheProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.RebuildVisibleNoteStateCache");
    private static readonly ProfilerMarker UpdateArpeggioGuidesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateArpeggioGuides");
    private static readonly ProfilerMarker UpdateChordFramesProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateChordFrames");
    private static readonly ProfilerMarker UpdateFretboardLightsProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateFretboardLights");
    private static readonly ProfilerMarker UpdateSectionCameraProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.UpdateSectionCamera");
    private static readonly ProfilerMarker GetRenderSongTimeProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.GetRenderSongTime");
    private static readonly ProfilerMarker EnsureGameplayVisualsBuiltProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.EnsureGameplayVisualsBuilt");
    private static readonly ProfilerMarker OverlayUpdateProfilerMarker = new ProfilerMarker("StringTheory.GuitarHighway3D.OverlayUpdate");

    private readonly Dictionary<int, NoteData> chartById = new Dictionary<int, NoteData>();
    private readonly Dictionary<int, List<NoteData>> chordGroups = new Dictionary<int, List<NoteData>>();
    private readonly Dictionary<int, HighwayNoteView> noteViews = new Dictionary<int, HighwayNoteView>();
    private readonly Dictionary<int, GameObject> arpeggioGuideFrames = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, GameObject> chordFrames = new Dictionary<int, GameObject>();
    private readonly Dictionary<int, int> slideDestinationBySourceId = new Dictionary<int, int>();
    private readonly Dictionary<int, int> bendDestinationBySourceId = new Dictionary<int, int>();
    private readonly Dictionary<int, int> bendSourceByDestinationId = new Dictionary<int, int>();
    private readonly Dictionary<int, GameplayNoteState> noteStatesById = new Dictionary<int, GameplayNoteState>();
    private readonly Dictionary<int, string> noteLaneTagTextById = new Dictionary<int, string>();
    private readonly List<LaneHighlightChunk> laneHighlightChunks = new List<LaneHighlightChunk>();
    private readonly HashSet<int> debugLoggedBendProfileIds = new HashSet<int>();
    private readonly HashSet<int> debugLoggedBendNearStrikeIds = new HashSet<int>();
    private readonly HashSet<int> visibleNoteIdsThisFrame = new HashSet<int>();
    private readonly List<int> noteViewRemovalBuffer = new List<int>();
    private readonly HashSet<int> activeArpeggioGuideIdsThisFrame = new HashSet<int>();
    private readonly List<int> arpeggioGuideRemovalBuffer = new List<int>();
    private readonly HashSet<int> activeChordIdsThisFrame = new HashSet<int>();
    private readonly List<int> chordFrameRemovalBuffer = new List<int>();
    private readonly List<HighwayCharacterBopEvent> highwayCharacterBopEvents = new List<HighwayCharacterBopEvent>();
    private readonly List<int> activeFretLightIndices = new List<int>();
    private bool[] stringHasIncomingNotesBuffer = Array.Empty<bool>();
    private float[] laneSurfaceHitFeedback = Array.Empty<float>();
    private float[] laneSurfaceMissFeedback = Array.Empty<float>();
    private float[] fretBoundaryHitFeedback = Array.Empty<float>();
    private float[] fretBoundaryMissFeedback = Array.Empty<float>();
    private float[] fretBoundaryHitExpansionFeedback = Array.Empty<float>();
    private float[] fretBoundaryMissExpansionFeedback = Array.Empty<float>();
    private Mesh techniqueRibbonMesh;
    private Material sharedTechniqueRibbonMaterial;
    private Material sharedBendArrowMaterial;
    private Material sharedMuteSymbolMaterial;
    private Material sharedHighwayCharacterMaterial;
    private Material sharedHighwayCharacterPortalBackMaterial;
    private Material sharedHighwayCharacterPortalFrontMaterial;
    private Material sharedHighwayCharacterMissParticleMaterial;
    private Material sharedHighwayCharacterMissAuraParticleMaterial;

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private Camera backgroundCamera;
    private GameObject root;
    private GameObject gameplayRoot;
    private GameObject characterRoot;
    private Transform highwayCharacterTransform;
    private Renderer highwayCharacterRenderer;
    private Renderer highwayCharacterPortalBackRenderer;
    private Renderer highwayCharacterPortalFrontRenderer;
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
    private readonly GameObject[] stringVisuals = new GameObject[6];
    private readonly Material[] stringVisualMats = new Material[6];
    private readonly Renderer[] stringVisualRenderers = new Renderer[6];
    private readonly GameObject[] loopStartMarkerLines = new GameObject[6];
    private readonly GameObject[] loopEndMarkerLines = new GameObject[6];
    private readonly Renderer[] loopStartMarkerRenderers = new Renderer[6];
    private readonly Renderer[] loopEndMarkerRenderers = new Renderer[6];
    private readonly Dictionary<int, TextMeshPro> fretNumberLabels = new Dictionary<int, TextMeshPro>();
    private Material[] fretBoundaryMats;
    private Renderer[] fretBoundaryRenderers;
    private Material[] laneSurfaceMats;
    private Renderer[] laneSurfaceRenderers;
    private Material[] laneGuideMats;
    private Renderer[] laneGuideRenderers;
    private Material[,] fretLightMats;
    private Renderer[,] fretLightRenderers;
    private ITabsBackgroundEffect backgroundEffect;
    private GameObject backgroundRoot;
    private bool backgroundUsingMenuMode = true;
    private TabsSongHeaderOverlay songHeaderOverlay;
    private int originalMainCameraCullingMask = -1;
    private CameraClearFlags originalMainCameraClearFlags; 
    private float originalMainCameraDepth;
    private float currentVisualNoteSpeed = 12f;
    private bool currentNoteByNoteModeEnabled;
    private bool currentNoteByNoteWaitingForMatch;
    private int lastObservedHighwayCharacterMissCount = -1;
    private float lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;
    private float cameraTargetX;
    private float cameraTargetFOV = 60f;
    private float cameraXVelocity;
    private float cameraFovVelocity;
    private float urgentCameraHoldUntil = float.NegativeInfinity;
    private float urgentCameraHeldTargetX;
    private float urgentCameraHeldTargetFOV = 60f;
    private int lastFretLightLayoutStringCount = -1;
    private int lastFretLightLayoutColumnCount = -1;
    private float lastFretLightLayoutOpenAnchorFret = float.NaN;
    private float lastFretLightLayoutStrikeLineZ = float.NaN;
    private float lastFretLightLayoutFretSpacing = float.NaN;
    private GuitarGameplaySnapshot renderSongTimeCacheSnapshot;
    private float renderSongTimeCacheValue;
    private bool renderSongTimeCacheValid;
    private bool gameplayVisualsVisible = true;
    private bool gameplayBuilt;
    private bool loopCountdownStaticVisualsPrimed;
    private float loopCountdownStaticVisualsSongTime = float.NaN;
    private Material sharedLoopMarkerMaterial;
    private float highwayCharacterViewportHeightScale = 1f;
    private float highwayCharacterViewportCenterYOffset = 0f;
    private const int BackgroundLayer = 2;
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
    private const float HighwayCharacterMissDurationSeconds = 0.42f; // [CHARACTER MISS] : timing - total duration of the miss reaction
    private const float HighwayCharacterMissAttackSeconds = 0.06f; // [CHARACTER MISS] : timing - how quickly the miss hits
    private const float HighwayCharacterMissDropInCharacterHeights = 0.058f; // [CHARACTER MISS] : motion - downward recoil amount
    private const float HighwayCharacterMissSwayInCharacterWidths = 0.028f; // [CHARACTER MISS] : motion - horizontal shake amount
    private const float HighwayCharacterMissScaleXAmount = 0.06f; // [CHARACTER MISS] : motion - width squash during the miss
    private const float HighwayCharacterMissScaleYAmount = 0.095f; // [CHARACTER MISS] : motion - height compression during the miss
    private const float HighwayCharacterMissTiltDegrees = 8.5f; // [CHARACTER MISS] : motion - rotational recoil amount
    private static readonly Color HighwayCharacterMissFlashColor = new Color(1f, 0.34f, 0.10f, 1f); // [CHARACTER MISS] : color - animated flash tint on the character
    private const float HighwayCharacterMissFlashBandSpeed = 14f; // [CHARACTER MISS] : shader - speed of the animated miss flash bands
    private const int HighwayCharacterMissParticleBurstCount = 28; // [CHARACTER MISS] : particles - base ember burst size
    private const int HighwayCharacterMissAuraParticleBurstCount = 11; // [CHARACTER MISS] : particles - softer secondary flare count
    private static readonly Color HighwayCharacterMissParticleColor = WithAlpha(HighwayCharacterPortalRimColor, 0.95f); // [CHARACTER MISS] : particles - ember body color synced to portal rim
    private static readonly Color HighwayCharacterMissParticleEdgeColor = WithAlpha(HighwayCharacterPortalRimColor, 1f); // [CHARACTER MISS] : particles - ember edge color synced to portal rim
    private const float HighwayCharacterMissParticleGlow = 1.55f; // [CHARACTER MISS] : particles - brightness of the ember burst
    private static readonly Color HighwayCharacterMissAuraParticleColor = WithAlpha(HighwayCharacterPortalRimColor, 0.68f); // [CHARACTER MISS] : particles - secondary flare body synced to portal rim
    private static readonly Color HighwayCharacterMissAuraParticleEdgeColor = WithAlpha(HighwayCharacterPortalRimColor, 0.9f); // [CHARACTER MISS] : particles - secondary flare edge synced to portal rim
    private const float HighwayCharacterMissAuraParticleGlow = 1.1f; // [CHARACTER MISS] : particles - brightness of the secondary flare
    private const float HighwayResolvedFretFeedbackDurationSeconds = 0.52f;
    private const float HighwayResolvedFretFeedbackAttackSeconds = 0.045f;
    private const float HighwayHitFretLightEmissionMultiplier = 12f;
    private const float HighwayMissFretLightEmissionMultiplier = 5.5f;
    private static readonly Color HighwayHitFretBoundaryColor = new Color(0.12f, 0.70f, 1f, 1f);
    private static readonly Color HighwayHitFretBoundaryEdgeColor = new Color(0.72f, 0.96f, 1f, 1f);
    private static readonly Color HighwayMissFretBoundaryColor = new Color(1f, 0.08f, 0.06f, 1f);
    private static readonly Color HighwayHitNoteFeedbackColor = new Color(0.14f, 0.72f, 1f, 0.98f);
    private static readonly Color HighwayHitNoteFeedbackSheenColor = new Color(0.78f, 0.96f, 1f, 0.96f);
    private static readonly Color HighwayMissNoteFeedbackColor = new Color(1f, 0.08f, 0.06f, 0.98f);
    private static readonly Color HighwayHitLaneSurfaceColor = new Color(0.10f, 0.62f, 1f, 0.64f);
    private static readonly Color HighwayHitFretLightColor = new Color(0.22f, 0.76f, 1f, 1f);
    private static readonly Color HighwayMissLaneSurfaceColor = new Color(1f, 0.06f, 0.04f, 0.64f);
    private static readonly Color HighwayMissFretLightColor = new Color(1f, 0.08f, 0.06f, 1f);
    private const float HighwayNutBoundaryBaseWidth = 0.5f;
    private const float HighwayNutBoundaryBaseDepth = 0.3f;
    private const float HighwayFretBoundaryBaseWidth = 0.15f;
    private const float HighwayFretBoundaryBaseDepth = 0.15f;
    private const float HighwayCharacterPortalLocalYInCharacterHeights = -0.34f;
    private const float HighwayCharacterPortalWidthInCharacterWidths = 0.96f;
    private const float HighwayCharacterPortalHeightInCharacterHeights = 0.34f;
    private const float HighwayCharacterPortalBackForwardOffset = 0.01f;
    private const float HighwayCharacterPortalFrontForwardOffset = -0.015f;
    private const float HighwayCharacterPortalSplitY01 = 0.5f; // [PORTAL] : placement - front/back split line
    private const float HighwayCharacterPortalSplitSoftness01 = 0.035f; // [PORTAL] : transparency - softness of the front/back split fade
    private const float HighwayCharacterPortalRingThickness = 0.07f; // [PORTAL] : detail - thickness of the glowing rim
    private const float HighwayCharacterPortalEdgeSoftness = 0.085f; // [PORTAL] : transparency - softness of the outer portal edge fade
    private const float HighwayCharacterPortalRimSoftness = 0.0065f; // [PORTAL] : detail - softness of the rim itself
    private const float HighwayCharacterPortalInteriorAlphaFloor = 0.8f; // [PORTAL] : transparency - minimum opacity inside the portal body
    private static readonly Color HighwayCharacterPortalBaseColor = new Color(0.07f, 0.10f, 0.19f, 1f); // [PORTAL] : color - outer/base fill
    private static readonly Color HighwayCharacterPortalCoreColor = new Color(0.03f, 0.05f, 0.12f, 1f); // [PORTAL] : color - inner dark void
    private static Color HighwayCharacterPortalRimColor => new Color(0.98f, 0.43f, 0.14f, 1f); // [PORTAL] : color - glowing orange rim, shared by miss particles
    private const float HighwayCharacterPortalBaseOpacity = 1f; // [PORTAL] : transparency - outer/base fill opacity
    private const float HighwayCharacterPortalCoreOpacity = 1f; // [PORTAL] : transparency - inner dark void opacity
    private const float HighwayCharacterPortalRimOpacity = 1f; // [PORTAL] : transparency - rim opacity
    private const float HighwayCharacterPortalSwirlOpacity = 0.9f; // [PORTAL] : transparency - swirl opacity
    private const float HighwayCharacterPortalGlowStrength = 1.82f; // [PORTAL] : intensity - rim glow strength
    private const float HighwayCharacterPortalSwirlSpeed = 0.55f; // [PORTAL] : motion - swirl animation speed
    private const float HighwayCharacterPortalSwirlSharpness = 5f; // [PORTAL] : detail - swirl sharpness
    private const float HighwayCharacterPortalPreviewTintMix = 0.18f; // [PORTAL] : mix - material preview tint
    private const float StringLaneSpacing = 1.2f;
    private const float BendRibbonVisualHeightInStrings = 2f;
    private const float BendRibbonLeadOutDistance = 0.9f;
    private const float BendRibbonCornerDepth = 0.25f;
    private const float BendRibbonCornerRoundness = 3f;
    private const float BendRibbonMinimumTopHoldDistance = 0.45f;
    private const float BendRibbonHeadMaximumFlatHoldSeconds = 1.2f;
    private const float BendRibbonFlatLightStrength = 0.85f;
    private const float BendRibbonDarkBandPaddingDistance = 0.32f;
    private const float BendArrowWidthFraction = 0.82f;
    private const float BendArrowFrontOffset = 0.035f;
    private const float BendArrowStackOffsetFraction = 0.72f;
    private const float LegatoCurveWidthFraction = 0.22f;
    private const int LegatoCurveSamples = 18;
    private const float MuteSymbolScaleFraction = 1.76f;
    private const float MuteSymbolFrontOffset = 0.04f;
    private const bool ForceMuteSymbolPreviewOnAllNotes = false;
    private const float VibratoRibbonAmplitudeInStrings = 0.42f;
    private const float VibratoCyclesPerSecond = 5f;
    private const int VibratoMinimumHalfWaves = 4;
    private const int VibratoMaximumHalfWaves = 12;
    private const float TechniqueSegmentJoinToleranceSeconds = 0.03f;
    private const float ChordNameLabelVerticalPadding = 1.75f;
    private const float ChordNameLabelLeftPadding = 0.06f;
    private const bool DebugBendRibbonLogs = false;
    private static readonly string[] ChordPitchClassNames = { "C", "C#", "D", "D#", "E", "F", "F#", "G", "G#", "A", "A#", "B" };
    private string backgroundSignature = string.Empty;
    private static readonly int CurveP0ShaderId = Shader.PropertyToID("_CurveP0");
    private static readonly int CurveP1ShaderId = Shader.PropertyToID("_CurveP1");
    private static readonly int CurveP2ShaderId = Shader.PropertyToID("_CurveP2");
    private static readonly int CurveP3ShaderId = Shader.PropertyToID("_CurveP3");
    private static readonly int HalfWidthShaderId = Shader.PropertyToID("_HalfWidth");
    private static readonly int CenterColorShaderId = Shader.PropertyToID("_CenterColor");
    private static readonly int EdgeColorShaderId = Shader.PropertyToID("_EdgeColor");
    private static readonly int EmissionColorShaderId = Shader.PropertyToID("_EmissionColor");
    private static readonly int VisibleStart01ShaderId = Shader.PropertyToID("_VisibleStart01");
    private static readonly int VisibleFadeSoftness01ShaderId = Shader.PropertyToID("_VisibleFadeSoftness01");
    private static readonly int LengthFadeSoftness01ShaderId = Shader.PropertyToID("_LengthFadeSoftness01");
    private static readonly int FlatLightStrengthShaderId = Shader.PropertyToID("_FlatLightStrength");
    private static readonly int PathModeShaderId = Shader.PropertyToID("_PathMode");
    private static readonly int CornerRoundnessShaderId = Shader.PropertyToID("_CornerRoundness");
    private static readonly int DarkBandStart01ShaderId = Shader.PropertyToID("_DarkBandStart01");
    private static readonly int DarkBandEnd01ShaderId = Shader.PropertyToID("_DarkBandEnd01");
    private static readonly int BendArrowBaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int CharacterFadeStartShaderId = Shader.PropertyToID("_FadeStartY");
    private static readonly int CharacterFadeEndShaderId = Shader.PropertyToID("_FadeEndY");
    private static readonly int CharacterMissFlashColorShaderId = Shader.PropertyToID("_MissFlashColor");
    private static readonly int CharacterMissFlashStrengthShaderId = Shader.PropertyToID("_MissFlashStrength");
    private static readonly int CharacterMissFlashSpeedShaderId = Shader.PropertyToID("_MissFlashSpeed");
    private static readonly int CharacterPortalBaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int CharacterPortalRimColorShaderId = Shader.PropertyToID("_RimColor");
    private static readonly int CharacterPortalAccentColorShaderId = Shader.PropertyToID("_AccentColor");
    private static readonly int CharacterPortalCoreColorShaderId = Shader.PropertyToID("_CoreColor");
    private static readonly int CharacterPortalGlowStrengthShaderId = Shader.PropertyToID("_GlowStrength");
    private static readonly int CharacterPortalSwirlSpeedShaderId = Shader.PropertyToID("_SwirlSpeed");
    private static readonly int CharacterPortalSwirlSharpnessShaderId = Shader.PropertyToID("_SwirlSharpness");
    private static readonly int CharacterPortalRingThicknessShaderId = Shader.PropertyToID("_RingThickness");
    private static readonly int FretBoundaryFlashColorShaderId = Shader.PropertyToID("_FlashColor");
    private static readonly int FretBoundaryFlashProgressShaderId = Shader.PropertyToID("_FlashProgress");
    private static readonly int FretBoundaryFlashStrengthShaderId = Shader.PropertyToID("_FlashStrength");
    private static readonly int FretBoundaryFlashSoftnessShaderId = Shader.PropertyToID("_FlashSoftness");
    private static readonly int FretBoundaryGlowWidthShaderId = Shader.PropertyToID("_GlowWidth");
    private static readonly int CharacterPortalSoftnessShaderId = Shader.PropertyToID("_Softness");
    private static readonly int CharacterPortalRimSoftnessShaderId = Shader.PropertyToID("_RimSoftness");
    private static readonly int CharacterPortalAlphaFloorShaderId = Shader.PropertyToID("_AlphaFloor");
    private static readonly int CharacterPortalHalfModeShaderId = Shader.PropertyToID("_HalfMode");
    private static readonly int CharacterPortalSplitYShaderId = Shader.PropertyToID("_SplitY");
    private static readonly int CharacterPortalSplitSoftnessShaderId = Shader.PropertyToID("_SplitSoftness");

    private struct TechniqueRibbonProfile
    {
        public Vector3 start;
        public Vector3 control1;
        public Vector3 control2;
        public Vector3 end;
        public float halfWidth;
        public float pathMode;
        public float cornerRoundness;
        public float darkBandStart01;
        public float darkBandEnd01;
    }
  
    private struct SlideRibbonFadeState
    {
        public bool freezeActive;
        public float fadeStartSongTime;
        public float fadeEndSongTime;
    }

    private struct ChordMatch
    {
        public int rootPitchClass;
        public int bassPitchClass;
        public string suffix;
        public int score;
    }

    private struct HighwayCharacterBopEvent
    {
        public float time;
        public float strength; 
    }

    private sealed class LaneHighlightChunk
    {
        public float startTime;
        public float endTime;
        public bool[] laneSurfaceMask;
        public bool[] laneGuideMask;
    }

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        root = new GameObject("Highway3DRendererRoot");
        backgroundRoot = new GameObject("Highway3DBackgroundRoot");
        backgroundRoot.transform.SetParent(root.transform, false);
        characterRoot = new GameObject("Highway3DCharacterRoot");
        characterRoot.transform.SetParent(root.transform, false);
        gameplayRoot = new GameObject("Highway3DGameplayRoot");
        gameplayRoot.transform.SetParent(root.transform, false);
        originalMainCameraClearFlags = mainCamera != null ? mainCamera.clearFlags : CameraClearFlags.SolidColor;
        originalMainCameraCullingMask = mainCamera != null ? mainCamera.cullingMask : -1;
        originalMainCameraDepth = mainCamera != null ? mainCamera.depth : 0f;
        lastObservedHighwayCharacterMissCount = -1;
        lastHighwayCharacterMissTriggerSongTime = float.NegativeInfinity;

        BuildChartCaches(chartNotes);
        BuildLaneHighlightChunks(chartNotes, sections);
        InitializeBackgroundEffect(menuMode: true);
        InitializeHighwayCharacter();
        InitializeBackgroundCamera();
        ConfigureCamera();
        songHeaderOverlay = new TabsSongHeaderOverlay(owner);
        gameplayBuilt = false;
    }

    internal static Rect GetHighwayCharacterHudScreenRect(float screenWidth, float screenHeight)
    {
        float safeScreenWidth = Mathf.Max(1f, screenWidth);
        float safeScreenHeight = Mathf.Max(1f, screenHeight);
        Rect viewportRect = HighwayCharacterVisualUtility.GetCurrentHudScreenRect(
            safeScreenWidth,
            safeScreenHeight,
            HighwayCharacterViewportMarginX,
            HighwayCharacterViewportMarginY,
            HighwayCharacterHeightViewportFraction,
            HighwayCharacterViewportCenterY);
        float characterLeft = viewportRect.x * safeScreenWidth;
        float characterTop = (1f - (viewportRect.y + viewportRect.height)) * safeScreenHeight;
        float characterWidth = viewportRect.width * safeScreenWidth;
        float characterHeight = viewportRect.height * safeScreenHeight;
        return new Rect(characterLeft, characterTop, characterWidth, characterHeight);
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        if (root != null)
            Object.Destroy(root);

        noteViews.Clear();
        arpeggioGuideFrames.Clear();
        chordFrames.Clear();
        fretNumberLabels.Clear();
        Initialize(owner, chartNotes, sections);
    }

    public void SetHighwayCharacterViewportHeightScale(float scale)
    {
        highwayCharacterViewportHeightScale = Mathf.Clamp(scale, 0.5f, 2.5f);
    }

    public void SetHighwayCharacterViewportCenterYOffset(float offsetY)
    {
        highwayCharacterViewportCenterYOffset = Mathf.Clamp(offsetY, -0.25f, 0.25f);
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        if (mainCamera == null)
            return;

        using (RenderProfilerMarker.Auto())
        {
            renderSongTimeCacheSnapshot = snapshot;
            renderSongTimeCacheValid = false;
            currentVisualNoteSpeed = GetVisualNoteSpeed(snapshot);
            currentNoteByNoteModeEnabled = snapshot.noteByNoteModeEnabled;
            currentNoteByNoteWaitingForMatch = snapshot.noteByNoteWaitingForMatch;
            bool loopCountdownActive = snapshot.loopRestartPauseRemainingSeconds > 0.0001f;
            bool loopCountdownNeedsHeavyRefresh = !loopCountdownActive ||
                                                  !loopCountdownStaticVisualsPrimed ||
                                                  !Mathf.Approximately(loopCountdownStaticVisualsSongTime, snapshot.songTime);
            bool logLoopCountdownDetail = loopCountdownActive && owner != null && owner.ShouldLogLoopCountdownRendererDetail();
            long renderStartTicks = 0L;
            long phaseStartTicks = 0L;
            double setupMs = 0d;
            double characterMs = 0d;
            double backgroundPlacementMs = 0d;
            double ensureGameplayMs = 0d;
            double stringVisualsMs = 0d;
            double fretBoundariesMs = 0d;
            double laneSurfacesMs = 0d;
            double laneGuidesMs = 0d;
            double fretboardLightsMs = 0d;
            double notesMs = 0d;
            double arpeggioGuidesMs = 0d;
            double chordFramesMs = 0d;
            double sectionCameraMs = 0d;
            double backgroundTickMs = 0d;
            double overlayMs = 0d;
            if (logLoopCountdownDetail)
            {
                renderStartTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                phaseStartTicks = renderStartTicks;
            }

            bool useMenuBackgroundMode = snapshot.mainMenuFlowActive || snapshot.showToneLab || snapshot.showTuner;
            bool suppressGameplay = snapshot.mainMenuFlowActive || snapshot.songEnded || snapshot.showToneLab || snapshot.showTuner;
            EnsureBackgroundMode(useMenuBackgroundMode);
            ConfigureCamera();
            if (logLoopCountdownDetail)
            {
                long afterSetupTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                setupMs = (afterSetupTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                phaseStartTicks = afterSetupTicks;
            }

            bool showHighwayCharacter = snapshot.showHighwayCharacter && !snapshot.showTuner;
            if (characterRoot != null && characterRoot.activeSelf != showHighwayCharacter)
                characterRoot.SetActive(showHighwayCharacter);

            if (showHighwayCharacter)
            {
                UpdateHighwayCharacterPlacement();
                UpdateHighwayCharacterAnimation(snapshot);
            }
            if (logLoopCountdownDetail)
            {
                long afterCharacterTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                characterMs = (afterCharacterTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                phaseStartTicks = afterCharacterTicks;
            }

            if (!suppressGameplay)
                UpdateBackgroundPlacement();
            if (logLoopCountdownDetail)
            {
                long afterBackgroundPlacementTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                backgroundPlacementMs = (afterBackgroundPlacementTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                phaseStartTicks = afterBackgroundPlacementTicks;
            }

            SetGameplayVisualsVisible(!suppressGameplay);

            if (!suppressGameplay)
            {
                using (EnsureGameplayVisualsBuiltProfilerMarker.Auto())
                {
                    EnsureGameplayVisualsBuilt();
                }
                if (logLoopCountdownDetail)
                {
                    long afterEnsureGameplayTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    ensureGameplayMs = (afterEnsureGameplayTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    phaseStartTicks = afterEnsureGameplayTicks;
                }

                if (loopCountdownNeedsHeavyRefresh)
                {
                    UpdateStringVisuals(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterStringVisualsTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        stringVisualsMs = (afterStringVisualsTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterStringVisualsTicks;
                    }

                    UpdateFretBoundaries(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterFretBoundariesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        fretBoundariesMs = (afterFretBoundariesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterFretBoundariesTicks;
                    }

                    UpdateLaneSurfaces(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterLaneSurfacesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        laneSurfacesMs = (afterLaneSurfacesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterLaneSurfacesTicks;
                    }

                    UpdateLaneGuides(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterLaneGuidesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        laneGuidesMs = (afterLaneGuidesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterLaneGuidesTicks;
                    }

                    UpdateFretboardLights(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterFretboardLightsTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        fretboardLightsMs = (afterFretboardLightsTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterFretboardLightsTicks;
                    }

                    UpdateNotes(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterNotesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        notesMs = (afterNotesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterNotesTicks;
                    }

                    UpdateArpeggioGuides(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterArpeggioGuidesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        arpeggioGuidesMs = (afterArpeggioGuidesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterArpeggioGuidesTicks;
                    }

                    UpdateChordFrames(snapshot);
                    if (logLoopCountdownDetail)
                    {
                        long afterChordFramesTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                        chordFramesMs = (afterChordFramesTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                        phaseStartTicks = afterChordFramesTicks;
                    }
                }

                UpdateSectionCamera(snapshot);
                UpdateLoopConfigurationMarkers(snapshot);
                if (logLoopCountdownDetail)
                {
                    long afterSectionCameraTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                    sectionCameraMs = (afterSectionCameraTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                    phaseStartTicks = afterSectionCameraTicks;
                }
            }

            backgroundEffect?.Tick(Time.deltaTime);
            if (logLoopCountdownDetail)
            {
                long afterBackgroundTickTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                backgroundTickMs = (afterBackgroundTickTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                phaseStartTicks = afterBackgroundTickTicks;
            }

            if (songHeaderOverlay != null)
            {
                using (OverlayUpdateProfilerMarker.Auto())
                {
                    songHeaderOverlay.UpdateFromSnapshot(snapshot);
                }
            }
            if (logLoopCountdownDetail)
            {
                long afterOverlayTicks = System.Diagnostics.Stopwatch.GetTimestamp();
                overlayMs = (afterOverlayTicks - phaseStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                double totalMs = (afterOverlayTicks - renderStartTicks) * 1000d / System.Diagnostics.Stopwatch.Frequency;
                owner.LogLoopCountdownRendererDetail(
                    $"RENDER_DETAIL song={snapshot.songTime:F3} remaining={snapshot.loopRestartPauseRemainingSeconds:F3} " +
                    $"setup={setupMs:F3} character={characterMs:F3} bgPlace={backgroundPlacementMs:F3} ensure={ensureGameplayMs:F3} " +
                    $"strings={stringVisualsMs:F3} frets={fretBoundariesMs:F3} laneSurf={laneSurfacesMs:F3} laneGuides={laneGuidesMs:F3} " +
                    $"lights={fretboardLightsMs:F3} notes={notesMs:F3} arp={arpeggioGuidesMs:F3} chords={chordFramesMs:F3} camera={sectionCameraMs:F3} " +
                    $"bgTick={backgroundTickMs:F3} overlay={overlayMs:F3} total={totalMs:F3}");
            }
            if (loopCountdownActive)
            {
                if (loopCountdownNeedsHeavyRefresh)
                {
                    loopCountdownStaticVisualsPrimed = true;
                    loopCountdownStaticVisualsSongTime = snapshot.songTime;
                }
            }
            else
            {
                loopCountdownStaticVisualsPrimed = false;
                loopCountdownStaticVisualsSongTime = float.NaN;
            }
            renderSongTimeCacheValid = false;
            renderSongTimeCacheSnapshot = null;
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
            mainCamera.cullingMask = originalMainCameraCullingMask;
            mainCamera.clearFlags = originalMainCameraClearFlags;
            mainCamera.depth = originalMainCameraDepth;
        }

        if (backgroundCamera != null)
            backgroundCamera.enabled = false;

        if (root != null)
            Object.Destroy(root);

        if (sharedTechniqueRibbonMaterial != null)
        {
            Object.Destroy(sharedTechniqueRibbonMaterial);
            sharedTechniqueRibbonMaterial = null;
        }

        if (sharedBendArrowMaterial != null)
        {
            Object.Destroy(sharedBendArrowMaterial);
            sharedBendArrowMaterial = null;
        }

        if (sharedMuteSymbolMaterial != null)
        {
            Object.Destroy(sharedMuteSymbolMaterial);
            sharedMuteSymbolMaterial = null;
        }

        if (sharedLoopMarkerMaterial != null)
        {
            Object.Destroy(sharedLoopMarkerMaterial);
            sharedLoopMarkerMaterial = null;
        }

        if (sharedHighwayCharacterMaterial != null)
        {
            Object.Destroy(sharedHighwayCharacterMaterial);
            sharedHighwayCharacterMaterial = null;
        }

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

        if (techniqueRibbonMesh != null)
        {
            Object.Destroy(techniqueRibbonMesh);
            techniqueRibbonMesh = null;
        }

        highwayCharacterTexture = null;
    }

    private void SetGameplayVisualsVisible(bool visible)
    {
        if (gameplayVisualsVisible == visible)
            return;

        gameplayVisualsVisible = visible;
        if (gameplayRoot != null)
            gameplayRoot.SetActive(visible);
    }

    private void BuildChartCaches(List<NoteData> chartNotes)
    {
        chartById.Clear();
        chordGroups.Clear();
        slideDestinationBySourceId.Clear();
        bendDestinationBySourceId.Clear();
        bendSourceByDestinationId.Clear();
        noteLaneTagTextById.Clear();
        highwayCharacterBopEvents.Clear();
        debugLoggedBendProfileIds.Clear();
        debugLoggedBendNearStrikeIds.Clear();
        urgentCameraHoldUntil = float.NegativeInfinity;
        urgentCameraHeldTargetX = 0f;
        urgentCameraHeldTargetFOV = 60f;

        if (chartNotes == null)
            return;

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData note = chartNotes[i];
            chartById[note.id] = note;

            if (DebugBendRibbonLogs && HasBendRibbon(note))
            {
                Debug.Log(
                    $"[BEND CACHE] id={note.id} t={note.time:F3} dur={note.duration:F3} string={note.stringIdx} fret={note.fret} " +
                    $"bend={note.bendStep:F2} pre={note.bendPreBend} rel={note.bendRelease} " +
                    $"visualStart={note.bendVisualStartTime:F3} visualDur={note.bendVisualDuration:F3}");
            }

            if (note.linkedFromNoteId >= 0)
                slideDestinationBySourceId[note.linkedFromNoteId] = note.id;

            if (note.chordId >= 0)
            {
                if (!chordGroups.TryGetValue(note.chordId, out List<NoteData> group))
                {
                    group = new List<NoteData>();
                    chordGroups[note.chordId] = group;
                }

                group.Add(note);
            }
        }

        foreach (var key in chordGroups.Keys.ToList())
            chordGroups[key] = chordGroups[key].OrderBy(n => n.stringIdx).ThenBy(n => n.fret).ToList();

        for (int i = 0; i < chartNotes.Count; i++)
        {
            NoteData source = chartNotes[i];
            if (!HasBendRibbon(source))
                continue;

            int destinationIndex = FindBendDestinationIndex(chartNotes, i);
            if (destinationIndex < 0)
                continue;

            NoteData destination = chartNotes[destinationIndex];
            bendDestinationBySourceId[source.id] = destination.id;
            bendSourceByDestinationId[destination.id] = source.id;
        }

        BuildLaneTagNoteMap(chartNotes);
        BuildHighwayCharacterBopEvents(chartNotes);
    }

    private void BuildHighwayCharacterBopEvents(List<NoteData> chartNotes)
    {
        highwayCharacterBopEvents.Clear();

        if (chartNotes == null || chartNotes.Count == 0)
            return;

        List<NoteData> orderedNotes = chartNotes
            .Where(note => note.time >= 0f)
            .OrderBy(note => note.time)
            .ToList();

        if (orderedNotes.Count == 0)
            return;

        float groupTime = orderedNotes[0].time;
        int noteCount = 0;
        bool hasTechnique = false;

        for (int i = 0; i < orderedNotes.Count; i++)
        {
            NoteData note = orderedNotes[i];
            if (note.time - groupTime <= HighwayCharacterBopGroupWindowSeconds)
            {
                noteCount++;
                hasTechnique |= note.technique != NoteTechnique.None || (note.techniqueSegments != null && note.techniqueSegments.Count > 0);
                continue;
            }

            AddHighwayCharacterBopEvent(groupTime, noteCount, hasTechnique);
            groupTime = note.time;
            noteCount = 1;
            hasTechnique = note.technique != NoteTechnique.None || (note.techniqueSegments != null && note.techniqueSegments.Count > 0);
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

    private static int FindBendDestinationIndex(List<NoteData> chartNotes, int sourceIndex)
    {
        NoteData source = chartNotes[sourceIndex];
        float expectedEndTime = source.time + Mathf.Max(0.05f, source.duration);
        const float earlyTolerance = 0.06f;
        const float lateTolerance = 0.14f;

        int bestIndex = -1;
        float bestDelta = float.MaxValue;

        for (int i = sourceIndex + 1; i < chartNotes.Count; i++)
        {
            NoteData candidate = chartNotes[i];
            if (candidate.time > expectedEndTime + lateTolerance)
                break;

            if (candidate.stringIdx != source.stringIdx || candidate.fret != source.fret)
                continue;

            // If the candidate has its own explicit technique content, it is a real
            // attacked note and should keep its travelling box instead of being hidden
            // as a passive bend continuation anchor.
            bool candidateHasOwnTechnique =
                candidate.technique != NoteTechnique.None ||
                (candidate.techniqueSegments != null && candidate.techniqueSegments.Count > 0) ||
                candidate.bendStep > 0f ||
                candidate.bendPreBend ||
                candidate.bendRelease ||
                candidate.slideTargetFret >= 0 ||
                candidate.isMuted ||
                candidate.isLegato;
            if (candidateHasOwnTechnique)
                continue;

            if (candidate.time < expectedEndTime - earlyTolerance)
                continue;

            float delta = Mathf.Abs(candidate.time - expectedEndTime);
            if (delta < bestDelta)
            {
                bestDelta = delta;
                bestIndex = i;
            }
        }

        return bestIndex;
    }

    private void BuildLaneTagNoteMap(List<NoteData> chartNotes)
    {
        if (chartNotes == null || chartNotes.Count == 0)
            return;

        List<NoteData> orderedNotes = chartNotes
            .OrderBy(note => note.time)
            .ThenBy(note => note.id)
            .ToList();

        const int notesPerSection = 5;
        for (int start = 0; start < orderedNotes.Count; start += notesPerSection)
        {
            int endExclusive = Mathf.Min(start + notesPerSection, orderedNotes.Count);
            HashSet<int> seenFrets = new HashSet<int>();

            for (int i = start; i < endExclusive; i++)
            {
                NoteData note = orderedNotes[i];
                if (note.fret <= 0)
                    continue;

                if (!seenFrets.Add(note.fret))
                    continue;

                noteLaneTagTextById[note.id] = note.fret.ToString();
            }
        }
    }

    private void BuildLaneHighlightChunks(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        laneHighlightChunks.Clear();
        if (chartNotes == null || chartNotes.Count == 0)
            return;

        List<TabSectionData> sourceSections = sections != null && sections.Count > 0
            ? sections
            : BuildFallbackLaneSections(chartNotes);

        int laneCount = GetFretLightColumnCount();
        HashSet<int> processedChordIds = new HashSet<int>();

        for (int sectionIndex = 0; sectionIndex < sourceSections.Count; sectionIndex++)
        {
            TabSectionData section = sourceSections[sectionIndex];
            if (section == null)
                continue;

            bool[] surfaceMask = new bool[laneCount];
            bool[] guideMask = new bool[laneCount];
            List<int> frettedSurfaceAnchors = new List<int>();
            List<int> frettedGuideAnchors = new List<int>();

            if (section.noteIds != null && section.noteIds.Count > 0)
            {
                for (int noteIndex = 0; noteIndex < section.noteIds.Count; noteIndex++)
                {
                    if (!chartById.TryGetValue(section.noteIds[noteIndex], out NoteData note))
                        continue;

                    if (note.chordId >= 0)
                    {
                        if (!processedChordIds.Add(note.chordId))
                            continue;

                        if (chordGroups.TryGetValue(note.chordId, out List<NoteData> chordGroup))
                            AddGroupToChunkMasks(chordGroup, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);
                        else
                            AddGroupToChunkMasks(new List<NoteData> { note }, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);

                        continue;
                    }

                    AddGroupToChunkMasks(new List<NoteData> { note }, surfaceMask, guideMask, frettedSurfaceAnchors, frettedGuideAnchors);
                }
            }

            processedChordIds.Clear();
            MarkChunkedLaneRanges(surfaceMask, frettedSurfaceAnchors, maxChunkGap: 3);
            MarkChunkedLaneRanges(guideMask, frettedGuideAnchors, maxChunkGap: 2);

            laneHighlightChunks.Add(new LaneHighlightChunk
            {
                startTime = section.startTime,
                endTime = section.endTime,
                laneSurfaceMask = surfaceMask,
                laneGuideMask = guideMask
            });
        }
    }

    private List<TabSectionData> BuildFallbackLaneSections(List<NoteData> chartNotes)
    {
        List<TabSectionData> generatedSections = new List<TabSectionData>();
        if (chartNotes == null || chartNotes.Count == 0)
            return generatedSections;

        float chunkDuration = Mathf.Max(0.75f, GetVisibleLeadTime() * 0.75f);
        float maxTime = chartNotes.Max(n => n.time + n.duration);
        int totalSections = Mathf.Max(1, Mathf.CeilToInt(maxTime / chunkDuration) + 1);

        for (int i = 0; i < totalSections; i++)
        {
            float start = i * chunkDuration;
            float end = start + chunkDuration;
            List<int> noteIds = chartNotes
                .Where(n => n.time >= start && n.time < end)
                .Select(n => n.id)
                .ToList();

            generatedSections.Add(new TabSectionData
            {
                index = i,
                startTime = start,
                endTime = end,
                noteIds = noteIds
            });
        }

        return generatedSections;
    }

    private void ConfigureCamera()
    {
        if (mainCamera == null)
            return;

        if (backgroundUsingMenuMode)
        {
            if (backgroundCamera != null)
                backgroundCamera.enabled = false;

            mainCamera.orthographic = true;
            mainCamera.orthographicSize = owner.tabCameraSize;
            mainCamera.clearFlags = CameraClearFlags.SolidColor;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask | (1 << BackgroundLayer);
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.transform.position = new Vector3(0f, 0f, owner.tabCameraZ);
            mainCamera.transform.rotation = Quaternion.identity;
        }
        else
        {
            mainCamera.orthographic = false;
            mainCamera.clearFlags = CameraClearFlags.Depth;
            if (originalMainCameraCullingMask >= 0)
                mainCamera.cullingMask = originalMainCameraCullingMask & ~(1 << BackgroundLayer);
            mainCamera.depth = originalMainCameraDepth;
            mainCamera.farClipPlane = Mathf.Max(mainCamera.farClipPlane, owner.highwayCameraFarClip);
            mainCamera.transform.position = new Vector3(cameraTargetX, owner.highwayCameraY, owner.highwayCameraZ);
            mainCamera.transform.rotation = Quaternion.Euler(owner.highwayCameraPitch, 0f, 0f);
            SyncBackgroundCamera();
        }

        mainCamera.backgroundColor = GetCameraBackgroundColor();
    }

    private Color GetCameraBackgroundColor()
    {
        if (owner == null)
            return Color.black;

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.BlueSky)
        {
            if (!backgroundUsingMenuMode)
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

        if (owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.Space)
            return owner.tabSpaceBackgroundColor;

        return owner.tabBackgroundColor;
    }

    private void EnsureGameplayVisualsBuilt()
    {
        if (gameplayBuilt)
            return;

        fretLightMats = new Material[stringVisuals.Length, GetFretLightColumnCount()];
        fretLightRenderers = new Renderer[stringVisuals.Length, GetFretLightColumnCount()];
        fretBoundaryMats = new Material[GetFretLightColumnCount()];
        fretBoundaryRenderers = new Renderer[GetFretLightColumnCount()];
        laneSurfaceMats = new Material[GetFretLightColumnCount()];
        laneSurfaceRenderers = new Renderer[GetFretLightColumnCount()];
        laneGuideMats = new Material[GetFretLightColumnCount()];
        laneGuideRenderers = new Renderer[GetFretLightColumnCount()];
        GenerateFretboard();
        GenerateStrings();
        GenerateLoopMarkers();
        GenerateLaneSurfaces();
        GenerateLaneGuides();
        GenerateFretLightGrid();
        gameplayBuilt = true;
    }

    private void InitializeHighwayCharacter()
    {
        if (characterRoot == null)
            return;

        if (!TryLoadCurrentHighwayCharacterTexture())
            return;

        GameObject characterObject = GameObject.CreatePrimitive(PrimitiveType.Quad);
        characterObject.name = "HighwayCharacter";
        characterObject.transform.SetParent(characterRoot.transform, false);
        highwayCharacterTransform = characterObject.transform;
        Object.Destroy(characterObject.GetComponent<Collider>());

        highwayCharacterRenderer = characterObject.GetComponent<Renderer>();
        highwayCharacterRenderer.sharedMaterial = GetHighwayCharacterMaterial();
        highwayCharacterRenderer.shadowCastingMode = ShadowCastingMode.Off;
        highwayCharacterRenderer.receiveShadows = false;
        InitializeHighwayCharacterMissParticles();

        highwayCharacterPortalBackRenderer = CreateHighwayCharacterPortalRenderer(
            "HighwayCharacterPortalBack",
            GetHighwayCharacterPortalBackMaterial(),
            HighwayCharacterPortalBackForwardOffset);
        highwayCharacterPortalFrontRenderer = CreateHighwayCharacterPortalRenderer(
            "HighwayCharacterPortalFront",
            GetHighwayCharacterPortalFrontMaterial(),
            HighwayCharacterPortalFrontForwardOffset);

        SetLayerRecursively(characterRoot, 0);
        characterRoot.SetActive(false);
    }

    private void InitializeBackgroundCamera()
    {
        if (mainCamera == null || backgroundCamera != null)
            return;

        GameObject cameraObject = new GameObject("Highway3DBackgroundCamera");
        cameraObject.transform.SetParent(root.transform, false);
        backgroundCamera = cameraObject.AddComponent<Camera>();
        backgroundCamera.enabled = false;
        backgroundCamera.depth = originalMainCameraDepth - 1f;
    }

    private void InitializeBackgroundEffect(bool menuMode)
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: !menuMode);
        backgroundUsingMenuMode = menuMode;
        backgroundSignature = GetBackgroundSignature(menuMode);

        if (backgroundRoot == null || backgroundEffect == null)
            return;

        backgroundEffect.Initialize(backgroundRoot.transform, owner);
        SetLayerRecursively(backgroundRoot, BackgroundLayer);
        if (menuMode)
        {
            backgroundRoot.transform.localPosition = Vector3.zero;
            backgroundRoot.transform.localRotation = Quaternion.identity;
            backgroundRoot.transform.localScale = Vector3.one;
        }
        else
            UpdateBackgroundPlacement();
    }

    private void UpdateBackgroundPlacement()
    {
        if (backgroundRoot == null || mainCamera == null)
            return;

        if (owner != null && owner.tabBackgroundMode == GuitarBridgeServer.TabsBackgroundMode.Space)
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
        backgroundRoot.transform.localScale = Vector3.one * owner.highwayBackgroundScale;
    }

    private void UpdateHighwayCharacterPlacement()
    {
        if (characterRoot == null)
            return;

        EnsureHighwayCharacterTextureCurrent();

        bool shouldShow = !backgroundUsingMenuMode && mainCamera != null && highwayCharacterRenderer != null && highwayCharacterTexture != null;
        if (characterRoot.activeSelf != shouldShow)
            characterRoot.SetActive(shouldShow);

        if (!shouldShow)
        {
            UpdateHighwayCharacterPortalVisuals(false);
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
            HighwayCharacterHeightViewportFraction * highwayCharacterViewportHeightScale,
            HighwayCharacterViewportCenterY + highwayCharacterViewportCenterYOffset,
            owner != null ? owner.highwayCharacterScale : 1f,
            0f,
            owner != null ? owner.highwayCharacterRigOffsetY : 0f);
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
    }

    private void SyncHighwayCharacterHudLayoutState()
    {
        HighwayCharacterVisualUtility.SetCurrentHudLayout(
            highwayCharacterAspect,
            highwayCharacterSourcePixelWidth,
            highwayCharacterSourcePixelHeight,
            (owner != null ? owner.highwayCharacterScale : 1f) * highwayCharacterViewportHeightScale,
            owner != null ? owner.highwayCharacterOffsetX : 0f,
            (owner != null ? owner.highwayCharacterRigOffsetY : 0f) + (owner != null ? owner.highwayCharacterOffsetY : 0f) + highwayCharacterViewportCenterYOffset);
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
            position.y = HighwayCharacterPortalLocalYInCharacterHeights;
            highwayCharacterPortalBackRenderer.transform.localPosition = position;
        }

        if (highwayCharacterPortalFrontRenderer != null)
        {
            Vector3 position = highwayCharacterPortalFrontRenderer.transform.localPosition;
            position.y = HighwayCharacterPortalLocalYInCharacterHeights;
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

        // Blend the most recent few note-on pulses so dense passages still read as a smooth groove.
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

        float uniformScale = Mathf.Max(0.82f, ((scaleX + scaleY) * 0.5f));
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

    private void EnsureBackgroundMode(bool menuMode)
    {
        if (backgroundEffect == null || menuMode != backgroundUsingMenuMode || backgroundSignature != GetBackgroundSignature(menuMode))
            InitializeBackgroundEffect(menuMode);
    }

    private string GetBackgroundSignature(bool menuMode)
    {
        if (owner == null)
            return string.Empty;

        return $"{owner.tabBackgroundMode}|{owner.tabSkyUseStageBackdrop}|{menuMode}";
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
        // Render the character before lane transparencies so they can blend over it,
        // while opaque gameplay geometry still occludes it by depth.
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

        GameObject particleObject = new GameObject("HighwayCharacterMissParticles");
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

        GameObject particleObject = new GameObject("HighwayCharacterMissAuraParticles");
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

        float bodyOpacity = owner != null ? Mathf.Clamp01(owner.highwayCharacterPortalBodyOpacity) : HighwayCharacterPortalInteriorAlphaFloor;
        Color baseColor = WithAlpha(HighwayCharacterPortalBaseColor, HighwayCharacterPortalBaseOpacity);
        Color coreColor = WithAlpha(HighwayCharacterPortalCoreColor, HighwayCharacterPortalCoreOpacity);
        Color rimColor = WithAlpha(
            HighwayCharacterVisualUtility.ResolvePortalEdgeColor(owner.highwayCharacterPortalEdgeColor),
            HighwayCharacterPortalRimOpacity);
        Color accentColor = owner != null && owner.highwayCharacterPortalSwirlsEnabled
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

    private static void SetLayerRecursively(GameObject target, int layer)
    {
        if (target == null)
            return;

        target.layer = layer;
        foreach (Transform child in target.transform)
            SetLayerRecursively(child.gameObject, layer);
    }

    private void GenerateFretboard()
    {
        fretNumberLabels.Clear();
        float fretLineCenterY = GetFretLineCenterY();
        float fretLineHeight = GetFretLineHeight();

        GameObject nut = GameObject.CreatePrimitive(PrimitiveType.Cube);
        nut.transform.SetParent(gameplayRoot.transform, false);
        nut.transform.position = new Vector3(0f, fretLineCenterY, owner.StrikeLineZ + 0.05f);
        nut.transform.localScale = new Vector3(HighwayNutBoundaryBaseWidth, fretLineHeight, HighwayNutBoundaryBaseDepth);
        Renderer nutRenderer = nut.GetComponent<Renderer>();
        Material nutMat = CreateFretBoundaryMaterial(new Color(0.22f, 0.23f, 0.27f, 0.28f));
        nutRenderer.material = nutMat;
        fretBoundaryMats[0] = nutMat;
        fretBoundaryRenderers[0] = nutRenderer;

        for (int fret = 1; fret <= owner.TotalFrets; fret++)
        {
            float wireX = fret * owner.FretSpacing;

            GameObject wire = GameObject.CreatePrimitive(PrimitiveType.Cube);
            wire.transform.SetParent(gameplayRoot.transform, false);
            wire.transform.position = new Vector3(wireX, fretLineCenterY, owner.StrikeLineZ + 0.05f);
            wire.transform.localScale = new Vector3(HighwayFretBoundaryBaseWidth, fretLineHeight, HighwayFretBoundaryBaseDepth);
            Renderer wireRenderer = wire.GetComponent<Renderer>();
            Material wireMat = CreateFretBoundaryMaterial(new Color(0.22f, 0.23f, 0.27f, 0.28f));
            wireRenderer.material = wireMat;
            fretBoundaryMats[fret] = wireMat;
            fretBoundaryRenderers[fret] = wireRenderer;

            if (fret % 3 == 0 || fret == 5 || fret == 7 || fret == 9 || fret == 12 || fret == 15)
            {
                CreateFretNumberLabel(fret, GetFretNumberX(fret));
            }
        }

        if (!owner.hideOpenFretNumber)
            CreateFretNumberLabel(0, GetOpenFretNumberX());
    }

    private Material CreateFretBoundaryMaterial(Color initialColor)
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayFretBoundaryGlow");
        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(initialColor, 0f);
        ConfigureOverlayMaterial(material, 120, true);
        ApplyFretBoundaryMaterialState(material, initialColor, Color.black, Color.clear, 0f, 0f);
        if (material.HasProperty(FretBoundaryFlashSoftnessShaderId))
            material.SetFloat(FretBoundaryFlashSoftnessShaderId, 0.22f);
        if (material.HasProperty(FretBoundaryGlowWidthShaderId))
            material.SetFloat(FretBoundaryGlowWidthShaderId, 0.62f);
        return material;
    }

    private void GenerateLaneSurfaces()
    {
        int laneCount = GetFretLightColumnCount();
        float laneSurfaceY = GetLaneSurfaceY();
        const float laneBackOverhang = 8f;
        float depth = 150f + laneBackOverhang;
        float centerZ = owner.StrikeLineZ - laneBackOverhang + (depth * 0.5f);
        // Keep adjacent lane floors from overlapping while leaving only a hairline seam.
        float laneWidth = Mathf.Max(0.0f, owner.FretSpacing * 1);
        const float laneHeight = 0.025f;

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject surface = GameObject.CreatePrimitive(PrimitiveType.Cube);
            surface.name = "LaneSurface_" + lane;
            surface.transform.SetParent(gameplayRoot.transform, false);
            surface.transform.position = new Vector3(GetNoteX(lane), laneSurfaceY, centerZ);
            surface.transform.localScale = new Vector3(laneWidth, laneHeight, depth);

            Material mat = CreateLaneSurfaceMaterial();
            Renderer renderer = surface.GetComponent<Renderer>();
            renderer.material = mat;
            laneSurfaceMats[lane] = mat;
            laneSurfaceRenderers[lane] = renderer;

            Object.Destroy(surface.GetComponent<Collider>());
        }
    }

    private void GenerateStrings()
    {
        float stringStartX = 0f;
        float stringEndX = (owner.TotalFrets * owner.FretSpacing) + (owner.FretSpacing * 0.75f);
        float stringLength = Mathf.Max(0.01f, stringEndX - stringStartX);
        float stringCenterX = stringStartX + (stringLength * 0.5f);
        int activeStringCount = GetRenderableStringCount();

        for (int i = 0; i < stringVisuals.Length; i++)
        {
            GameObject s = GameObject.CreatePrimitive(PrimitiveType.Cube);
            s.name = "String_" + i;
            s.transform.SetParent(gameplayRoot.transform, false);
            s.transform.position = new Vector3(stringCenterX, GetStringY(i), owner.StrikeLineZ);
            s.transform.localScale = new Vector3(stringLength, 0.1f, 0.1f);
            Material mat = owner.CreateSharedGlowMaterial(owner.GetStringColor(i), 0.9f);
            Renderer renderer = s.GetComponent<Renderer>();
            renderer.material = mat;
            renderer.enabled = i < activeStringCount;
            stringVisuals[i] = s;
            stringVisualMats[i] = mat;
            stringVisualRenderers[i] = renderer;
        }
    }

    private void GenerateLoopMarkers()
    {
        float stringStartX = 0f;
        float stringEndX = (owner.TotalFrets * owner.FretSpacing) + (owner.FretSpacing * 0.75f);
        float stringLength = Mathf.Max(0.01f, stringEndX - stringStartX);
        float stringCenterX = stringStartX + (stringLength * 0.5f);

        if (sharedLoopMarkerMaterial == null)
        {
            sharedLoopMarkerMaterial = owner.CreateSharedGlowMaterial(new Color(1f, 0.18f, 0.18f, 0.92f), 1.1f);
            ConfigureOverlayMaterial(sharedLoopMarkerMaterial, 130, true);
        }

        for (int i = 0; i < stringVisuals.Length; i++)
        {
            loopStartMarkerLines[i] = CreateLoopMarkerLine($"LoopStartMarker_{i}", stringCenterX, stringLength, i, out loopStartMarkerRenderers[i]);
            loopEndMarkerLines[i] = CreateLoopMarkerLine($"LoopEndMarker_{i}", stringCenterX, stringLength, i, out loopEndMarkerRenderers[i]);
        }
    }

    private GameObject CreateLoopMarkerLine(string name, float centerX, float length, int stringIdx, out Renderer renderer)
    {
        GameObject line = GameObject.CreatePrimitive(PrimitiveType.Cube);
        line.name = name;
        line.transform.SetParent(gameplayRoot.transform, false);
        line.transform.position = new Vector3(centerX, GetStringY(stringIdx), owner.StrikeLineZ);
        line.transform.localScale = new Vector3(length, Mathf.Max(0.045f, owner.FretSpacing * 0.025f), Mathf.Max(0.07f, owner.FretSpacing * 0.035f));
        renderer = line.GetComponent<Renderer>();
        renderer.sharedMaterial = sharedLoopMarkerMaterial;
        renderer.shadowCastingMode = ShadowCastingMode.Off;
        renderer.receiveShadows = false;
        Object.Destroy(line.GetComponent<Collider>());
        line.SetActive(false);
        return line;
    }

    private void UpdateLoopConfigurationMarkers(GuitarGameplaySnapshot snapshot)
    {
        bool showMarkers = snapshot != null && snapshot.showLoopSettings && owner != null;
        UpdateLoopMarkerLineGroup(loopStartMarkerLines, snapshot?.loopStartTime ?? 0f, showMarkers);
        UpdateLoopMarkerLineGroup(loopEndMarkerLines, snapshot?.loopEndTime ?? 0f, showMarkers);
    }

    private void UpdateLoopMarkerLineGroup(GameObject[] lines, float markerTime, bool visible)
    {
        if (lines == null || owner == null)
            return;

        int activeStringCount = GetRenderableStringCount();
        float markerZ = owner.StrikeLineZ;
        if (visible && renderSongTimeCacheSnapshot != null)
        {
            markerZ = Mathf.Clamp(
                owner.StrikeLineZ + ((markerTime - GetRenderSongTime(renderSongTimeCacheSnapshot)) * currentVisualNoteSpeed),
                owner.StrikeLineZ,
                owner.SpawnZ);
        }

        for (int i = 0; i < lines.Length; i++)
        {
            GameObject line = lines[i];
            if (line == null)
                continue;

            bool lineVisible = visible && i < activeStringCount;
            line.SetActive(lineVisible);
            if (!lineVisible)
                continue;

            Vector3 position = line.transform.position;
            position.y = GetStringY(i);
            position.z = markerZ;
            line.transform.position = position;
        }
    }

    private void UpdateStringVisuals(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        using (UpdateStringVisualsProfilerMarker.Auto())
        {
            float renderSongTime = GetRenderSongTime(snapshot);
            int activeStringCount = GetRenderableStringCount();
            EnsureStringHasIncomingNotesBuffer(activeStringCount);
            Array.Clear(stringHasIncomingNotesBuffer, 0, activeStringCount);
            float spawnLeadSeconds = Mathf.Max(0f, (owner.SpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, currentVisualNoteSpeed));
            float maxVisibleTime = renderSongTime + spawnLeadSeconds;

            if (snapshot.noteStates != null)
            {
                for (int i = 0; i < snapshot.noteStates.Count; i++)
                {
                    GameplayNoteState state = snapshot.noteStates[i];
                    if (state == null || state.IsResolved)
                        continue;

                    if (state.data.time > maxVisibleTime)
                        break;

                    int stringIdx = state.data.stringIdx;
                    if (stringIdx < 0 || stringIdx >= activeStringCount)
                        continue;

                    stringHasIncomingNotesBuffer[stringIdx] = true;
                }
            }

            for (int i = 0; i < stringVisualMats.Length; i++)
            {
                Material mat = stringVisualMats[i];
                if (mat == null)
                    continue;

                if (i >= activeStringCount)
                {
                    if (stringVisualRenderers[i] != null)
                        stringVisualRenderers[i].enabled = false;
                    continue;
                }

                if (stringVisuals[i] != null)
                {
                    Vector3 position = stringVisuals[i].transform.position;
                    position.y = GetStringY(i);
                    stringVisuals[i].transform.position = position;
                }

                Color baseColor = owner.GetStringColor(i);
                bool isActive = stringHasIncomingNotesBuffer[i];
                Color appliedColor = isActive
                    ? new Color(baseColor.r, baseColor.g, baseColor.b, 0.95f)
                    : new Color(baseColor.r * 0.28f, baseColor.g * 0.28f, baseColor.b * 0.28f, 0.42f);
                float emission = isActive ? 0.6f : 0f;

                mat.color = appliedColor;
                mat.SetColor("_Color", appliedColor);
                mat.SetColor("_BaseColor", appliedColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission > 0f ? baseColor * Mathf.Pow(2f, emission) : Color.black);

                if (stringVisualRenderers[i] != null)
                    stringVisualRenderers[i].enabled = true;
            }
        }
    }

    private void GenerateFretLightGrid()
    {
        int fretLightColumns = GetFretLightColumnCount();

        for (int s = 0; s < stringVisuals.Length; s++)
        {
            for (int f = 0; f < fretLightColumns; f++)
            {
                GameObject light = GameObject.CreatePrimitive(PrimitiveType.Sphere);
                light.transform.SetParent(gameplayRoot.transform, false);
                float xPos = f == 0 ? GetNoteX(Mathf.RoundToInt(owner.defaultOpenAnchorFret)) : GetNoteX(f);
                light.transform.position = new Vector3(xPos, GetStringY(s), owner.StrikeLineZ);
                light.transform.localScale = new Vector3(0.6f, 0.6f, 0.2f);

                Material mat = owner.CreateSharedGlowMaterial(Color.black, 0f);
                Renderer lightRenderer = light.GetComponent<Renderer>();
                lightRenderer.material = mat;
                lightRenderer.enabled = false;
                fretLightMats[s, f] = mat;
                fretLightRenderers[s, f] = lightRenderer;
            }
        }
    }

    private void GenerateLaneGuides()
    {
        int laneCount = GetFretLightColumnCount();
        float laneSurfaceY = GetLaneGuideStringY();
        float depth = 150f;
        float centerZ = owner.StrikeLineZ + (depth * 0.5f);
        // Make lane guides read more like slim glowing planes that bridge the lane seams.
        const float laneGuideHeight = 0.085f;
        const float laneGuideLift = 0.038f;

        for (int lane = 0; lane < laneCount; lane++)
        {
            GameObject guide = GameObject.CreatePrimitive(PrimitiveType.Cube);
            guide.name = "LaneGuide_" + lane;
            guide.transform.SetParent(gameplayRoot.transform, false);
            float xPos = lane * owner.FretSpacing;
            float guideWidth = Mathf.Max(Mathf.Max(0.02f, owner.highwayLaneGuideThickness), owner.FretSpacing * 0.03f);
            guide.transform.position = new Vector3(xPos, laneSurfaceY + laneGuideLift, centerZ);
            guide.transform.localScale = new Vector3(guideWidth, laneGuideHeight, depth);

            Material mat = CreateLaneGuideMaterial();
            Renderer renderer = guide.GetComponent<Renderer>();
            renderer.material = mat;
            laneGuideMats[lane] = mat;
            laneGuideRenderers[lane] = renderer;

            Object.Destroy(guide.GetComponent<Collider>());
        }
    }

    private void UpdateFretBoundaries(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || fretBoundaryMats == null || fretBoundaryRenderers == null)
            return;

        using (UpdateFretBoundariesProfilerMarker.Auto())
        {
            bool[] boundaryActive = BuildFretBoundaryActivityFlags(snapshot);
            float renderSongTime = GetRenderSongTime(snapshot);
            EnsureFretBoundaryFeedbackBuffers(fretBoundaryMats.Length);
            BuildResolvedFretBoundaryFeedback(snapshot, renderSongTime, fretBoundaryMats.Length);

            Color activeColor = new Color(0.46f, 0.50f, 0.56f, 0.92f);
            Color idleColor = new Color(0.20f, 0.22f, 0.25f, 0.18f);

            for (int i = 0; i < fretBoundaryMats.Length; i++)
            {
                Material mat = fretBoundaryMats[i];
                Renderer renderer = fretBoundaryRenderers[i];
                if (mat == null || renderer == null)
                    continue;

                float hitPulse = i < fretBoundaryHitFeedback.Length ? fretBoundaryHitFeedback[i] : 0f;
                float missPulse = i < fretBoundaryMissFeedback.Length ? fretBoundaryMissFeedback[i] : 0f;
                float feedbackPulse = Mathf.Max(hitPulse, missPulse);
                bool feedbackIsMiss = missPulse > hitPulse;
                float shapedPulse = feedbackPulse > 0f
                    ? Mathf.Sin(Mathf.Clamp01(feedbackPulse) * Mathf.PI * 0.5f)
                    : 0f;
                float flashExpansion = feedbackIsMiss
                    ? (i < fretBoundaryMissExpansionFeedback.Length ? fretBoundaryMissExpansionFeedback[i] : 0f)
                    : (i < fretBoundaryHitExpansionFeedback.Length ? fretBoundaryHitExpansionFeedback[i] : 0f);

                Color color = boundaryActive[i] ? activeColor : idleColor;
                float emission = boundaryActive[i]
                    ? (owner.highwayHighlightFretBoundaries ? 0.18f : 0.04f)
                    : 0f;
                Color emissionColor = emission > 0f ? color * Mathf.Pow(2f, emission) : Color.black;
                Color flashColor = feedbackIsMiss ? HighwayMissFretBoundaryColor : HighwayHitFretBoundaryColor;
                float flashHdrIntensity = feedbackIsMiss ? 4.8f : 4.2f;
                flashColor = new Color(
                    flashColor.r * flashHdrIntensity,
                    flashColor.g * flashHdrIntensity,
                    flashColor.b * flashHdrIntensity,
                    1f);
                bool fretLineFeedbackEnabled = IsFretLineFeedbackEnabled(feedbackIsMiss, feedbackPulse);
                float flashStrength = fretLineFeedbackEnabled ? shapedPulse : 0f;
                float flashProgress = fretLineFeedbackEnabled ? Mathf.Clamp01(flashExpansion) : 0f;

                ApplyFretBoundaryMaterialState(mat, color, emissionColor, flashColor, flashProgress, flashStrength);
                ApplyFretBoundaryTransform(renderer, i, fretLineFeedbackEnabled && owner.highwayShowFretLineScaleFeedback ? shapedPulse : 0f);
                renderer.enabled = true;
            }

            UpdateFretNumberLabels(boundaryActive);
        }
    }

    private void ApplyFretBoundaryTransform(Renderer renderer, int boundaryIndex, float pulse)
    {
        if (renderer == null)
            return;

        Transform boundaryTransform = renderer.transform;
        if (boundaryTransform == null)
            return;

        float fretLineHeight = GetFretLineHeight();
        float widthBase = boundaryIndex == 0 ? HighwayNutBoundaryBaseWidth : HighwayFretBoundaryBaseWidth;
        float depthBase = boundaryIndex == 0 ? HighwayNutBoundaryBaseDepth : HighwayFretBoundaryBaseDepth;
        float width = widthBase * (1f + (pulse * (boundaryIndex == 0 ? 0.42f : 1.85f)));
        float height = fretLineHeight * (1f + (pulse * 0.16f));
        float depth = depthBase * (1f + (pulse * 1.45f));
        boundaryTransform.localScale = new Vector3(width, height, depth);

        Vector3 position = boundaryTransform.position;
        position.y = GetFretLineCenterY();
        position.z = owner.StrikeLineZ + 0.05f - (pulse * 0.015f);
        boundaryTransform.position = position;
    }

    private void ApplyFretBoundaryMaterialState(Material material, Color baseColor, Color emissionColor, Color flashColor, float flashProgress, float flashStrength)
    {
        if (material == null)
            return;

        material.color = baseColor;
        material.SetColor("_Color", baseColor);
        material.SetColor("_BaseColor", baseColor);
        material.EnableKeyword("_EMISSION");
        material.SetColor("_EmissionColor", emissionColor);
        if (material.HasProperty(FretBoundaryFlashColorShaderId))
            material.SetColor(FretBoundaryFlashColorShaderId, flashColor);
        if (material.HasProperty(FretBoundaryFlashProgressShaderId))
            material.SetFloat(FretBoundaryFlashProgressShaderId, Mathf.Clamp01(flashProgress));
        if (material.HasProperty(FretBoundaryFlashStrengthShaderId))
            material.SetFloat(FretBoundaryFlashStrengthShaderId, Mathf.Clamp01(flashStrength));
    }

    private void EnsureFretBoundaryFeedbackBuffers(int boundaryCount)
    {
        if (boundaryCount <= 0)
            return;

        if (fretBoundaryHitFeedback.Length != boundaryCount)
            fretBoundaryHitFeedback = new float[boundaryCount];
        else
            Array.Clear(fretBoundaryHitFeedback, 0, fretBoundaryHitFeedback.Length);

        if (fretBoundaryMissFeedback.Length != boundaryCount)
            fretBoundaryMissFeedback = new float[boundaryCount];
        else
            Array.Clear(fretBoundaryMissFeedback, 0, fretBoundaryMissFeedback.Length);

        if (fretBoundaryHitExpansionFeedback.Length != boundaryCount)
            fretBoundaryHitExpansionFeedback = new float[boundaryCount];
        else
            Array.Clear(fretBoundaryHitExpansionFeedback, 0, fretBoundaryHitExpansionFeedback.Length);

        if (fretBoundaryMissExpansionFeedback.Length != boundaryCount)
            fretBoundaryMissExpansionFeedback = new float[boundaryCount];
        else
            Array.Clear(fretBoundaryMissExpansionFeedback, 0, fretBoundaryMissExpansionFeedback.Length);
    }

    private void BuildResolvedFretBoundaryFeedback(GuitarGameplaySnapshot snapshot, float renderSongTime, int boundaryCount)
    {
        if (snapshot?.noteStates == null || boundaryCount <= 0)
            return;

        GetResolvedFeedbackScanWindow(renderSongTime, out float earliestNoteTime, out float latestNoteTime);
        int startIndex = FindFirstNoteStateIndexAtOrAfter(snapshot.noteStates, earliestNoteTime);
        for (int i = startIndex; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null)
                continue;

            if (state.data.time > latestNoteTime)
                break;
            if (!IsResolvedFretLineFeedbackEnabled(state))
                continue;
            if (!TryGetResolvedFeedbackPulse(state, renderSongTime, out float pulse))
                continue;

            float[] buffer = state.IsMissed ? fretBoundaryMissFeedback : fretBoundaryHitFeedback;
            float[] expansionBuffer = state.IsMissed ? fretBoundaryMissExpansionFeedback : fretBoundaryHitExpansionFeedback;
            float progress = Mathf.Clamp01((renderSongTime - state.resolvedAt) / HighwayResolvedFretFeedbackDurationSeconds);
            float expansion = Mathf.SmoothStep(0.08f, 1f, Mathf.Clamp01(progress / 0.38f));
            if (state.data.fret <= 0)
            {
                ApplyMaxFeedback(buffer, 0, pulse);
                ApplyMaxFeedback(expansionBuffer, 0, expansion);
                continue;
            }

            int lowerBoundary = Mathf.Clamp(state.data.fret - 1, 0, boundaryCount - 1);
            int upperBoundary = Mathf.Clamp(state.data.fret, 0, boundaryCount - 1);
            ApplyMaxFeedback(buffer, lowerBoundary, pulse * 0.58f);
            ApplyMaxFeedback(buffer, upperBoundary, pulse);
            ApplyMaxFeedback(expansionBuffer, lowerBoundary, expansion);
            ApplyMaxFeedback(expansionBuffer, upperBoundary, expansion);
        }
    }

    private bool[] BuildFretBoundaryActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int boundaryCount = fretBoundaryMats != null ? fretBoundaryMats.Length : GetFretLightColumnCount();
        bool[] boundaryActive = new bool[boundaryCount];
        bool[] laneMask = GetChunkLaneMask(snapshot, boundaryCount, useGuideMask: false);

        for (int fret = 0; fret < boundaryCount; fret++)
        {
            if (fret == 0)
            {
                boundaryActive[fret] = laneMask.Length > 1 && laneMask[1];
                continue;
            }

            bool lowerFretLaneActive = fret < laneMask.Length && laneMask[fret];
            bool higherFretLaneActive = fret + 1 < laneMask.Length && laneMask[fret + 1];
            boundaryActive[fret] = lowerFretLaneActive || higherFretLaneActive;
        }

        return boundaryActive;
    }

    private void UpdateLaneGuides(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || laneGuideMats == null || laneGuideRenderers == null)
            return;

        using (UpdateLaneGuidesProfilerMarker.Auto())
        {
            bool[] laneHasIncomingNotes = BuildLaneGuideActivityFlags(snapshot);

            for (int lane = 0; lane < laneGuideMats.Length; lane++)
            {
                Material mat = laneGuideMats[lane];
                Renderer renderer = laneGuideRenderers[lane];
                if (mat == null || renderer == null)
                    continue;

            bool isActive = laneHasIncomingNotes[lane];
            Color laneColor = isActive
                ? new Color(0.34f, 0.74f, 1f, 1f)
                : new Color(0.03f, 0.07f, 0.14f, 0.18f);
            float emission = isActive ? 2.2f : 0f;

            mat.color = laneColor;
            mat.SetColor("_Color", laneColor);
            mat.SetColor("_BaseColor", laneColor);
            mat.SetColor("_TintColor", laneColor);
            mat.EnableKeyword("_EMISSION");
            mat.SetColor("_EmissionColor", new Color(0.18f, 0.45f, 1f, 1f) * Mathf.Pow(2f, emission));
                renderer.enabled = true;
            }
        }
    }

    private float GetFretNumberY()
    {
        float lowestStringY = float.MaxValue;
        for (int stringIdx = 0; stringIdx < GetRenderableStringCount(); stringIdx++)
            lowestStringY = Mathf.Min(lowestStringY, GetStringY(stringIdx));

        // Adjust this value to move fret numbers lower or higher relative to the lowest string.
        return lowestStringY - 2f + owner.highwayFretNumberYOffset;
    }

    private float GetFretLineCenterY()
    {
        GetStringVerticalBounds(out float minY, out float maxY);
        return (minY + maxY) * 0.5f;
    }

    private float GetLaneGuideY()
    {
        GetStringVerticalBounds(out float minY, out _);
        const float laneGuideHeight = 0.045f;
        const float laneGuideLift = 0.14f;
        const float noteClearanceMargin = 0.03f;

        float lowestNoteHalfHeight = Mathf.Max(
            GetSingleFrettedNoteScale().y * 0.5f,
            GetGroupedFrettedNoteScale().y * 0.5f);

        float highestSafeGuideTop = minY - lowestNoteHalfHeight - noteClearanceMargin;
        return highestSafeGuideTop - laneGuideLift - (laneGuideHeight * 0.5f) + owner.highwayLaneGuideYOffset;
    }

    private float GetLaneGuideStringY()
    {
        GetStringVerticalBounds(out float minY, out _);
        return minY + owner.highwayLaneGuideYOffset;
    }

    private float GetLaneSurfaceY()
    {
        return GetLaneGuideY() - 0.03f;
    }

    private float GetLaneSurfaceTopY()
    {
        const float laneHeight = 0.025f;
        return GetLaneSurfaceY() + (laneHeight * 0.5f);
    }

    private float GetFretLineHeight()
    {
        GetStringVerticalBounds(out float minY, out float maxY);
        float endOverhang = GetTrackLowerEdgeOverhang();
        return Mathf.Max(0.2f, (maxY - minY) + (endOverhang * 2f));
    }

    private float GetTrackLowerEdgeOverhang()
    {
        return 0.12f;
    }

    private void GetStringVerticalBounds(out float minY, out float maxY)
    {
        minY = float.MaxValue;
        maxY = float.MinValue;

        for (int stringIdx = 0; stringIdx < GetRenderableStringCount(); stringIdx++)
        {
            float y = GetStringY(stringIdx);
            minY = Mathf.Min(minY, y);
            maxY = Mathf.Max(maxY, y);
        }
    }

    private float GetFretNumberX(int fret)
    {
        float leftBoundaryX = fret <= 1 ? 0f : (fret - 1) * owner.FretSpacing;
        float rightBoundaryX = fret * owner.FretSpacing;
        return (leftBoundaryX + rightBoundaryX) * 0.5f;
    }

    private float GetOpenFretNumberX()
    {
        int openAnchorFret = Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets);
        return GetFretNumberX(openAnchorFret);
    }

    private string FormatFretNumberLabel(int fret)
    {
        return Mathf.Max(0, fret).ToString();
    }

    private void CreateFretNumberLabel(int fret, float x)
    {
        GameObject textObj = new GameObject("FretNum_" + fret);
        textObj.transform.SetParent(gameplayRoot.transform, false);
        textObj.transform.position = new Vector3(x, GetFretNumberY(), owner.StrikeLineZ + 0.18f + owner.highwayFretNumberZOffset);
        textObj.transform.rotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = FormatFretNumberLabel(fret);
        // Adjust this value to change fret number size.
        tm.fontSize = 14f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.enableWordWrapping = false;
        tm.characterSpacing = 0f;
        tm.lineSpacing = 0f; 
        tm.rectTransform.sizeDelta = new Vector2(12f, 10f);
        tm.color = new Color(0.38f, 0.62f, 1f, 0.92f);
        tm.sortingOrder = 250;

        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);

        fretNumberLabels[fret] = tm;
        ApplyFretNumberLabelStyle(tm, false);
    }

    private TextMeshPro CreateLaneTagLabelIfNeeded(NoteData data)
    {
        if (!noteLaneTagTextById.TryGetValue(data.id, out string laneText))
            return null;

        GameObject textObj = new GameObject("LaneTag_" + data.id);
        textObj.transform.SetParent(gameplayRoot.transform, false);
        textObj.transform.rotation = Quaternion.identity;
        textObj.transform.localScale = Vector3.one;

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.text = laneText;
        tm.fontSize = 22f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.Center;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.enableWordWrapping = false;
        tm.characterSpacing = 0f;
        tm.lineSpacing = 0f;
        tm.rectTransform.sizeDelta = new Vector2(16f, 14f);
        tm.sortingOrder = 255;

        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);

        ConfigureLaneTagLabelMaterial(tm);

        ApplyFretNumberLabelStyle(tm, true);
        return tm;
    }

    private static void ConfigureLaneTagLabelMaterial(TextMeshPro label)
    {
        if (label == null)
            return;

        Material fontMat = label.fontMaterial;
        if (fontMat == null)
            return;

        // Keep moving lane tags above the lane floor while staying below higher overlay elements.
        fontMat.renderQueue = (int)RenderQueue.Transparent + 89;
        if (fontMat.HasProperty("_ZWrite"))
            fontMat.SetFloat("_ZWrite", 0f);
        if (fontMat.HasProperty("_CullMode"))
            fontMat.SetFloat("_CullMode", 0f);
        if (fontMat.HasProperty("_ZTestMode"))
            fontMat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
        else if (fontMat.HasProperty("_ZTest"))
            fontMat.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    private void UpdateFretNumberLabels(bool[] boundaryActive)
    {
        if (fretNumberLabels.Count == 0)
            return;

        foreach (KeyValuePair<int, TextMeshPro> pair in fretNumberLabels)
        {
            TextMeshPro label = pair.Value;
            if (label == null)
                continue;

            bool isActive = pair.Key >= 0 && pair.Key < boundaryActive.Length && boundaryActive[pair.Key];
            ApplyFretNumberLabelStyle(label, isActive);
        }
    }

    private void ApplyFretNumberLabelStyle(TextMeshPro label, bool isActive)
    {
        if (label == null)
            return;

        Color faceColor = isActive
            ? new Color(1f, 0.90f, 0.20f, 1f)
            : new Color(0.38f, 0.62f, 1f, 0.92f);
        label.color = faceColor;

        Material fontMat = label.fontMaterial;
        if (fontMat == null)
            return;

        fontMat.SetColor("_FaceColor", faceColor);
        if (fontMat.HasProperty("_GlowColor"))
        {
            fontMat.SetFloat("_GlowPower", isActive ? 0.55f : 0f);
            fontMat.SetFloat("_GlowInner", isActive ? 0.04f : 0f);
            fontMat.SetFloat("_GlowOuter", isActive ? 0.18f : 0f);
            fontMat.SetColor("_GlowColor", isActive ? new Color(1f, 0.84f, 0.12f, 0.9f) : Color.clear);
        }
        if (fontMat.HasProperty("_UnderlaySoftness"))
        {
            fontMat.SetFloat("_UnderlaySoftness", 0f);
            fontMat.SetFloat("_UnderlayDilate", 0f);
            fontMat.SetColor("_UnderlayColor", Color.clear);
        }
    }

    private void UpdateLaneSurfaces(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null || laneSurfaceMats == null || laneSurfaceRenderers == null)
            return;

        using (UpdateLaneSurfacesProfilerMarker.Auto())
        {
            bool[] activeLanes = BuildLaneSurfaceActivityFlags(snapshot);
            float renderSongTime = GetRenderSongTime(snapshot);
            EnsureLaneSurfaceFeedbackBuffers(laneSurfaceMats.Length);
            BuildResolvedLaneSurfaceFeedback(snapshot, renderSongTime, laneSurfaceMats.Length);

            for (int lane = 0; lane < laneSurfaceMats.Length; lane++)
            {
                Material mat = laneSurfaceMats[lane];
                Renderer renderer = laneSurfaceRenderers[lane];
                if (mat == null || renderer == null)
                    continue;

                bool isActive = activeLanes[lane];
                bool hasLeftNeighbor = lane > 0 && activeLanes[lane - 1];
                bool hasRightNeighbor = lane + 1 < activeLanes.Length && activeLanes[lane + 1];
                float hitPulse = lane < laneSurfaceHitFeedback.Length ? laneSurfaceHitFeedback[lane] : 0f;
                float missPulse = lane < laneSurfaceMissFeedback.Length ? laneSurfaceMissFeedback[lane] : 0f;
                float feedbackPulse = Mathf.Max(hitPulse, missPulse);
                bool feedbackIsMiss = missPulse > hitPulse;
                bool hasFeedback = feedbackPulse > 0f;

                Color baseColor = isActive
                    ? new Color(0.08f, 0.10f, 0.14f, 1f)
                    : new Color(0.025f, 0.03f, 0.045f, 0.14f);
                Color feedbackColor = feedbackIsMiss ? HighwayMissLaneSurfaceColor : HighwayHitLaneSurfaceColor;
                if (!feedbackIsMiss && feedbackPulse > 0.58f)
                    feedbackColor = Color.Lerp(feedbackColor, HighwayHitFretBoundaryEdgeColor, Mathf.Clamp01((feedbackPulse - 0.58f) / 0.42f) * 0.24f);
                Color laneColor = hasFeedback
                    ? Color.Lerp(baseColor, feedbackColor, Mathf.Clamp01(feedbackPulse * (feedbackIsMiss ? 0.82f : 0.86f)))
                    : baseColor;
                float emission = isActive ? 0.15f : 0f;
                emission += feedbackIsMiss ? feedbackPulse * 1.25f : feedbackPulse * 1.90f;

                mat.color = laneColor;
                mat.SetColor("_Color", laneColor);
                mat.SetColor("_BaseColor", laneColor);
                mat.SetColor("_TintColor", laneColor);
                mat.EnableKeyword("_EMISSION");
                mat.SetColor("_EmissionColor", emission > 0f ? laneColor * Mathf.Pow(2f, emission) : Color.black);
                if (mat.HasProperty("_EdgeFadeLeft"))
                    mat.SetFloat("_EdgeFadeLeft", hasFeedback ? 0.20f : (isActive && !hasLeftNeighbor ? 0.12f : 0.008f));
                if (mat.HasProperty("_EdgeFadeRight"))
                    mat.SetFloat("_EdgeFadeRight", hasFeedback ? 0.20f : (isActive && !hasRightNeighbor ? 0.12f : 0.008f));
                if (mat.HasProperty("_FrontBackFade"))
                    mat.SetFloat("_FrontBackFade", hasFeedback ? 0.26f : 0.1f);
                renderer.enabled = true;
            }
        }
    }

    private void EnsureLaneSurfaceFeedbackBuffers(int laneCount)
    {
        if (laneCount <= 0)
            return;

        if (laneSurfaceHitFeedback.Length != laneCount)
            laneSurfaceHitFeedback = new float[laneCount];
        else
            Array.Clear(laneSurfaceHitFeedback, 0, laneSurfaceHitFeedback.Length);

        if (laneSurfaceMissFeedback.Length != laneCount)
            laneSurfaceMissFeedback = new float[laneCount];
        else
            Array.Clear(laneSurfaceMissFeedback, 0, laneSurfaceMissFeedback.Length);
    }

    private void BuildResolvedLaneSurfaceFeedback(GuitarGameplaySnapshot snapshot, float renderSongTime, int laneCount)
    {
        if (snapshot?.noteStates == null || laneCount <= 0 || owner == null || !owner.highwayShowLaneFlashFeedback)
            return;

        GetResolvedFeedbackScanWindow(renderSongTime, out float earliestNoteTime, out float latestNoteTime);
        int startIndex = FindFirstNoteStateIndexAtOrAfter(snapshot.noteStates, earliestNoteTime);
        for (int i = startIndex; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null)
                continue;

            if (state.data.time > latestNoteTime)
                break;
            if (!IsResolvedFretFeedbackEnabled(state))
                continue;
            if (!TryGetResolvedFeedbackPulse(state, renderSongTime, out float pulse))
                continue;

            float[] buffer = state.IsMissed ? laneSurfaceMissFeedback : laneSurfaceHitFeedback;
            int laneIndex = GetFeedbackFretLightIndex(state.data, laneCount);
            ApplyMaxFeedback(buffer, laneIndex, pulse);
            if (state.data.fret > 0)
                ApplyMaxFeedback(buffer, Mathf.Clamp(laneIndex - 1, 0, laneCount - 1), pulse * 0.28f);
        }
    }

    private bool[] BuildLaneGuideActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = laneGuideMats != null ? laneGuideMats.Length : GetFretLightColumnCount();
        bool[] laneMask = GetChunkLaneMask(snapshot, laneCount, useGuideMask: false);
        bool[] guideMask = new bool[laneCount];

        for (int guide = 0; guide < laneCount; guide++)
        {
            bool lowerLaneActive = guide < laneMask.Length && laneMask[guide];
            bool higherLaneActive = guide + 1 < laneMask.Length && laneMask[guide + 1];
            guideMask[guide] = lowerLaneActive || higherLaneActive;
        }

        return guideMask;
    }

    private bool[] BuildLaneSurfaceActivityFlags(GuitarGameplaySnapshot snapshot)
    {
        int laneCount = laneSurfaceMats != null ? laneSurfaceMats.Length : GetFretLightColumnCount();
        return ExpandLaneMask(GetChunkLaneMask(snapshot, laneCount, useGuideMask: false), 1);
    }

    private bool[] ExpandLaneMask(bool[] sourceMask, int extraLanesPerSide)
    {
        if (sourceMask == null || sourceMask.Length == 0 || extraLanesPerSide <= 0)
            return sourceMask ?? new bool[0];

        bool[] expanded = new bool[sourceMask.Length];
        for (int lane = 0; lane < sourceMask.Length; lane++)
        {
            if (!sourceMask[lane])
                continue;

            int start = Mathf.Clamp(lane - extraLanesPerSide, 0, sourceMask.Length - 1);
            int end = Mathf.Clamp(lane + extraLanesPerSide, 0, sourceMask.Length - 1);
            for (int i = start; i <= end; i++)
                expanded[i] = true;
        }

        return expanded;
    }

    private bool[] GetChunkLaneMask(GuitarGameplaySnapshot snapshot, int laneCount, bool useGuideMask)
    {
        bool[] emptyMask = new bool[laneCount];
        if (laneHighlightChunks == null || laneHighlightChunks.Count == 0 || snapshot == null)
            return emptyMask;

        float renderSongTime = GetRenderSongTime(snapshot);
        if (renderSongTime < laneHighlightChunks[0].startTime)
            return CloneChunkMask(laneHighlightChunks[0], laneCount, useGuideMask);

        for (int i = 0; i < laneHighlightChunks.Count; i++)
        {
            LaneHighlightChunk chunk = laneHighlightChunks[i];
            if (chunk == null)
                continue;

            bool isInChunk = renderSongTime >= chunk.startTime && renderSongTime < chunk.endTime;
            bool isLastChunk = i == laneHighlightChunks.Count - 1 && renderSongTime >= chunk.startTime;
            if (!isInChunk && !isLastChunk)
                continue;

            return CloneChunkMask(chunk, laneCount, useGuideMask);
        }

        return emptyMask;
    }

    private bool[] CloneChunkMask(LaneHighlightChunk chunk, int laneCount, bool useGuideMask)
    {
        bool[] clonedMask = new bool[laneCount];
        if (chunk == null)
            return clonedMask;

        bool[] sourceMask = useGuideMask ? chunk.laneGuideMask : chunk.laneSurfaceMask;
        if (sourceMask == null)
            return clonedMask;

        int copyLength = Mathf.Min(laneCount, sourceMask.Length);
        for (int lane = 0; lane < copyLength; lane++)
            clonedMask[lane] = sourceMask[lane];
        return clonedMask;
    }

    private void AddGroupToChunkMasks(List<NoteData> group, bool[] surfaceMask, bool[] guideMask, List<int> frettedSurfaceAnchors, List<int> frettedGuideAnchors)
    {
        if (group == null || group.Count == 0)
            return;

        List<NoteData> fretted = group.Where(n => n.fret > 0).ToList();
        if (fretted.Count == 0)
        {
            int handFret = GetGroupHandFret(group);
            MarkOpenGroupRange(surfaceMask, handFret, group);
            MarkOpenGroupRange(guideMask, handFret, group);
            return;
        }

        for (int i = 0; i < fretted.Count; i++)
        {
            NoteData note = fretted[i];
            int laneIndex = Mathf.Clamp(note.fret, 0, surfaceMask.Length - 1);
            frettedSurfaceAnchors.Add(laneIndex);
            frettedGuideAnchors.Add(laneIndex);
            MarkLaneRange(guideMask, laneIndex - 1, laneIndex);
        }
    }

    private void MarkChunkedLaneRanges(bool[] activeFlags, List<int> anchors, int maxChunkGap)
    {
        if (activeFlags == null || anchors == null || anchors.Count == 0)
            return;

        int[] ordered = anchors
            .Where(index => index >= 0 && index < activeFlags.Length)
            .Distinct()
            .OrderBy(index => index)
            .ToArray();

        if (ordered.Length == 0)
            return;

        int chunkStart = ordered[0];
        int previous = ordered[0];
        for (int i = 1; i < ordered.Length; i++)
        {
            int current = ordered[i];
            if (current - previous > maxChunkGap)
            {
                MarkLaneRange(activeFlags, chunkStart, previous);
                chunkStart = current;
            }

            previous = current;
        }

        MarkLaneRange(activeFlags, chunkStart, previous);
    }

    private static void MarkLaneRange(bool[] activeFlags, int startIndex, int endIndex)
    {
        if (activeFlags == null || activeFlags.Length == 0)
            return;

        int clampedStart = Mathf.Clamp(Mathf.Min(startIndex, endIndex), 0, activeFlags.Length - 1);
        int clampedEnd = Mathf.Clamp(Mathf.Max(startIndex, endIndex), 0, activeFlags.Length - 1);
        for (int i = clampedStart; i <= clampedEnd; i++)
            activeFlags[i] = true;
    }

    private void MarkOpenGroupRange(bool[] activeFlags, int handFret, List<NoteData> group)
    {
        int startLane = Mathf.Clamp(handFret - 1, 0, activeFlags.Length - 1);
        int endLane = Mathf.Clamp(GetOpenGroupEndLane(handFret, group), 0, activeFlags.Length - 1);
        MarkLaneRange(activeFlags, startLane, endLane);
    }

    private int GetOpenGroupEndLane(int handFret, List<NoteData> group)
    {
        int furthestFret = handFret + 3;
        if (group != null)
        {
            int highestGroupFret = group.Where(n => n.fret > 0).Select(n => n.fret).DefaultIfEmpty(furthestFret).Max();
            furthestFret = Mathf.Max(furthestFret, highestGroupFret);
        }

        return furthestFret;
    }

    private void UpdateNotes(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateNotesProfilerMarker.Auto())
        {
            float renderSongTime = GetRenderSongTime(snapshot);
            float spawnLeadSeconds = Mathf.Max(0f, (owner.SpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, currentVisualNoteSpeed));
            float maxVisibleTime = renderSongTime + spawnLeadSeconds;
            float resolvedFadeTime = GetResolvedFadeTime();
            float floorY = GetLaneSurfaceTopY();
            float laneTagY = GetLaneGuideStringY() + 0.15f;
            visibleNoteIdsThisFrame.Clear();
            RebuildVisibleNoteStateCache(snapshot);

            using (UpdateNotesIterateProfilerMarker.Auto())
            {
                for (int i = 0; i < snapshot.noteStates.Count; i++)
                {
                    GameplayNoteState state = snapshot.noteStates[i];
                    if (state == null)
                        continue;

                    if (!state.IsResolved && state.data.time > maxVisibleTime)
                        break;

                    float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
                    bool resolvedAtOrBeforeRenderTime = state.IsResolved && state.resolvedAt >= 0f && renderSongTime >= state.resolvedAt;
                    bool keepForResult = resolvedAtOrBeforeRenderTime && renderSongTime - state.resolvedAt <= resolvedFadeTime;
                    bool keepForTechnique = resolvedAtOrBeforeRenderTime && ShouldKeepTechniqueAliveAfterResolution(state.data, renderSongTime);
                    bool visible = travelZ <= owner.SpawnZ && (!state.IsResolved || keepForResult || keepForTechnique || travelZ >= owner.StrikeLineZ);

                    if (!visible)
                        continue;

                    visibleNoteIdsThisFrame.Add(state.data.id);

                    if (!noteViews.TryGetValue(state.data.id, out HighwayNoteView view) || view == null)
                    {
                        view = CreateNoteView(state.data);
                        noteViews[state.data.id] = view;
                    }

                    float displayZ = Mathf.Max(owner.StrikeLineZ, travelZ);
                    UpdateNoteView(view, state, displayZ, travelZ, renderSongTime, floorY, laneTagY);
                }
            }

            using (UpdateNotesCleanupProfilerMarker.Auto())
            {
                noteViewRemovalBuffer.Clear();
                foreach (KeyValuePair<int, HighwayNoteView> pair in noteViews)
                {
                    if (visibleNoteIdsThisFrame.Contains(pair.Key))
                        continue;

                    noteViewRemovalBuffer.Add(pair.Key);
                }

                for (int i = 0; i < noteViewRemovalBuffer.Count; i++)
                {
                    int key = noteViewRemovalBuffer[i];
                    noteViews[key].Destroy();
                    noteViews.Remove(key);
                }
            }
        }
    }

    private HighwayNoteView CreateNoteView(NoteData data)
    {
        using (CreateNoteViewProfilerMarker.Auto())
        {
            List<NoteData> group = GetChordGroup(data);
            bool isGrouped = group.Count > 1;
            bool isOpen = data.fret == 0;

            float xPos = isOpen ? GetGroupAnchorX(group) : GetNoteX(data.fret);
            float yPos = GetStringY(data.stringIdx);

        GameObject cube = GameObject.CreatePrimitive(PrimitiveType.Cube);
        cube.name = "HighwayNote_" + data.id;
        cube.transform.SetParent(gameplayRoot.transform, false);
        cube.transform.position = new Vector3(xPos, yPos, owner.SpawnZ);

        Material noteMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 0.8f);
        ConfigureOverlayMaterial(noteMat, 120, true);
        cube.GetComponent<Renderer>().material = noteMat;

        GameObject textObj = null;
        TextMeshPro laneTagLabel = CreateLaneTagLabelIfNeeded(data);

        if (isGrouped)
        {
            if (isOpen)
            {
                float leftX = GetHandWindowStartX(GetGroupHandFret(group));
                float rightX = GetHandWindowEndX(GetGroupHandFret(group), group);
                cube.transform.localScale = new Vector3(Mathf.Max(owner.FretSpacing * 0.8f, rightX - leftX), GetScaledOpenHeight(), GetScaledOpenDepth());
            }
            else
            {
                cube.transform.localScale = GetGroupedFrettedNoteScale();
            }
        }
        else
        {
            if (isOpen)
                cube.transform.localScale = GetSingleOpenNoteScale();
            else
                cube.transform.localScale = GetSingleFrettedNoteScale();
        }

        GameObject tail = GameObject.CreatePrimitive(PrimitiveType.Cube);
        tail.name = "Tail_" + data.id;
        tail.transform.SetParent(gameplayRoot.transform, false);
        Material tailMat = owner.CreateSharedTransparentMaterial(owner.GetStringColor(data.stringIdx) * 0.4f, 0.2f);
        ConfigureOverlayMaterial(tailMat, 90, true);
        tail.GetComponent<Renderer>().material = tailMat;
        tail.SetActive(owner.highwayShowApproachLine);

        GameObject tether = null;
        Material tetherMat = null;
        Renderer tetherRenderer = null;
        if (!isOpen && !isGrouped)
        {
            tether = GameObject.CreatePrimitive(PrimitiveType.Cube);
            tether.name = "LaneTether_" + data.id;
            tether.transform.SetParent(gameplayRoot.transform, false);
            tetherMat = CreateNoteTetherMaterial(owner.GetStringColor(data.stringIdx));
            tetherRenderer = tether.GetComponent<Renderer>();
            tetherRenderer.material = tetherMat;
            Object.Destroy(tether.GetComponent<Collider>());
        }

        GameObject marker = null; 
        Renderer markerRenderer = null;
        Material markerMaterial = null;
        if (!isOpen) 
        {
            marker = GameObject.CreatePrimitive(PrimitiveType.Sphere);
            marker.name = "Marker_" + data.id;
            marker.transform.SetParent(gameplayRoot.transform, false);
            marker.transform.position = new Vector3(xPos, yPos, owner.StrikeLineZ);
            marker.transform.localScale = GetMarkerScale();
            markerMaterial = owner.CreateSharedTransparentMaterial(owner.GetStringColor(data.stringIdx), 1.1f);
            ConfigureOverlayMaterial(markerMaterial, 130, true);
            markerRenderer = marker.GetComponent<Renderer>();
            markerRenderer.material = markerMaterial;
            SetGameObjectActive(marker, owner.highwayShowLandingDot);
        }

        GameObject bendArrow = null;
        Renderer bendArrowRenderer = null;
        MaterialPropertyBlock bendArrowPropertyBlock = null;
        GameObject bendArrowSecondary = null;
        Renderer bendArrowSecondaryRenderer = null;
        MaterialPropertyBlock bendArrowSecondaryPropertyBlock = null;
        if (HasBendRibbon(data))
        {
            EnsureBendArrowResources();
            if (sharedBendArrowMaterial != null)
            {
                bendArrow = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bendArrow.name = "BendArrow_" + data.id;
                bendArrow.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(bendArrow.GetComponent<Collider>());
                bendArrowRenderer = bendArrow.GetComponent<Renderer>();
                bendArrowRenderer.sharedMaterial = sharedBendArrowMaterial;
                bendArrowRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bendArrowRenderer.receiveShadows = false;
                bendArrowRenderer.lightProbeUsage = LightProbeUsage.Off;
                bendArrowRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                bendArrowPropertyBlock = new MaterialPropertyBlock();

                bendArrowSecondary = GameObject.CreatePrimitive(PrimitiveType.Quad);
                bendArrowSecondary.name = "BendArrowSecondary_" + data.id;
                bendArrowSecondary.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(bendArrowSecondary.GetComponent<Collider>());
                bendArrowSecondaryRenderer = bendArrowSecondary.GetComponent<Renderer>();
                bendArrowSecondaryRenderer.sharedMaterial = sharedBendArrowMaterial;
                bendArrowSecondaryRenderer.shadowCastingMode = ShadowCastingMode.Off;
                bendArrowSecondaryRenderer.receiveShadows = false;
                bendArrowSecondaryRenderer.lightProbeUsage = LightProbeUsage.Off;
                bendArrowSecondaryRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                bendArrowSecondaryPropertyBlock = new MaterialPropertyBlock();
            }
        }

        GameObject muteSymbol = null;
        Renderer muteSymbolRenderer = null;
        if (ShouldShowMuteSymbolForNote(data))
        {
            EnsureMuteSymbolResources();
            if (sharedMuteSymbolMaterial != null)
            {
                muteSymbol = GameObject.CreatePrimitive(PrimitiveType.Quad);
                muteSymbol.name = "MuteSymbol_" + data.id;
                muteSymbol.transform.SetParent(gameplayRoot.transform, false);
                Object.Destroy(muteSymbol.GetComponent<Collider>());
                muteSymbolRenderer = muteSymbol.GetComponent<Renderer>();
                muteSymbolRenderer.sharedMaterial = sharedMuteSymbolMaterial;
                muteSymbolRenderer.shadowCastingMode = ShadowCastingMode.Off;
                muteSymbolRenderer.receiveShadows = false;
                muteSymbolRenderer.lightProbeUsage = LightProbeUsage.Off;
                muteSymbolRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            }
        }

        GameObject outlineRoot = CreateNoteOutline(cube.transform.localScale, owner.GetStringColor(data.stringIdx));
        outlineRoot.SetActive(false);

        GameObject techniqueRoot = new GameObject("Technique_" + data.id);
        techniqueRoot.transform.SetParent(gameplayRoot.transform, false);

        GameObject[] techniqueSegmentRibbons = null;
        Renderer[] techniqueSegmentRibbonRenderers = null;
        MaterialPropertyBlock[] techniqueSegmentRibbonPropertyBlocks = null;
        if (HasTechniqueSegments(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                int slotCount = GetTechniqueSegmentRibbonSlotCount(data);
                techniqueSegmentRibbons = new GameObject[slotCount];
                techniqueSegmentRibbonRenderers = new Renderer[slotCount];
                techniqueSegmentRibbonPropertyBlocks = new MaterialPropertyBlock[slotCount];

                for (int i = 0; i < slotCount; i++)
                {
                    techniqueSegmentRibbons[i] = CreateTechniqueRibbonObject(
                        "TechniqueSegmentRibbon_" + data.id + "_" + i,
                        techniqueRoot.transform,
                        techniqueRibbonMesh,
                        sharedTechniqueRibbonMaterial,
                        out techniqueSegmentRibbonRenderers[i]);
                    techniqueSegmentRibbonPropertyBlocks[i] = techniqueSegmentRibbonRenderers[i] != null ? new MaterialPropertyBlock() : null;
                }
            }
        }

        GameObject slideRibbon = null;
        Renderer slideRibbonRenderer = null;
        GameObject legatoCurve = null;
        LineRenderer legatoCurveRenderer = null;
        if (!HasTechniqueSegments(data) && data.slideTargetFret >= 0)
        {
            if (IsLegatoCurveTechnique(data))
            {
                legatoCurve = new GameObject("LegatoCurve_" + data.id);
                legatoCurve.transform.SetParent(techniqueRoot.transform, false);
                legatoCurveRenderer = legatoCurve.AddComponent<LineRenderer>();
                legatoCurveRenderer.useWorldSpace = true;
                legatoCurveRenderer.loop = false;
                legatoCurveRenderer.alignment = LineAlignment.View;
                legatoCurveRenderer.textureMode = LineTextureMode.Stretch;
                legatoCurveRenderer.numCapVertices = 6;
                legatoCurveRenderer.numCornerVertices = 4;
                legatoCurveRenderer.shadowCastingMode = ShadowCastingMode.Off;
                legatoCurveRenderer.receiveShadows = false;
                legatoCurveRenderer.lightProbeUsage = LightProbeUsage.Off;
                legatoCurveRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
                Material legatoMat = owner.CreateSharedGlowMaterial(owner.GetStringColor(data.stringIdx), 1.4f);
                ConfigureOverlayMaterial(legatoMat, 101, true);
                legatoCurveRenderer.material = legatoMat;
            }
            else
            {
                EnsureTechniqueRibbonResources();
                if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
                {
                    slideRibbon = CreateTechniqueRibbonObject("SlideRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out slideRibbonRenderer);
                }
            }
        }

        GameObject bendRibbon = null;
        Renderer bendRibbonRenderer = null;
        GameObject bendSustainRibbon = null;
        Renderer bendSustainRibbonRenderer = null;
        GameObject sustainRibbon = null;
        Renderer sustainRibbonRenderer = null;
        if (!HasTechniqueSegments(data) && HasBendRibbon(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                bendRibbon = CreateTechniqueRibbonObject("BendRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out bendRibbonRenderer);
                bendSustainRibbon = CreateTechniqueRibbonObject("BendSustainRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out bendSustainRibbonRenderer);
            }
        }

        if (!HasTechniqueSegments(data) && HasNoteSustainRibbon(data))
        {
            EnsureTechniqueRibbonResources();
            if (techniqueRibbonMesh != null && sharedTechniqueRibbonMaterial != null)
            {
                sustainRibbon = CreateTechniqueRibbonObject("SustainRibbon_" + data.id, techniqueRoot.transform, techniqueRibbonMesh, sharedTechniqueRibbonMaterial, out sustainRibbonRenderer);
            }
        }

            return new HighwayNoteView
            {
                noteRoot = cube,
                noteTransform = cube.transform,
                noteRenderer = cube.GetComponent<Renderer>(),
                noteMaterial = noteMat,
                label = textObj != null ? textObj.GetComponent<TextMeshPro>() : null,
                laneTagLabel = laneTagLabel,
                laneTagTransform = laneTagLabel != null ? laneTagLabel.transform : null,
                tail = tail,
                tailTransform = tail != null ? tail.transform : null,
                tether = tether,
                tetherTransform = tether != null ? tether.transform : null,
                tetherRenderer = tetherRenderer,
                tetherMaterial = tetherMat,
                marker = marker,
                markerTransform = marker != null ? marker.transform : null,
                markerRenderer = markerRenderer,
                markerMaterial = markerMaterial,
                bendArrow = bendArrow,
                bendArrowTransform = bendArrow != null ? bendArrow.transform : null,
                bendArrowRenderer = bendArrowRenderer,
                bendArrowPropertyBlock = bendArrowPropertyBlock,
                bendArrowSecondary = bendArrowSecondary,
                bendArrowSecondaryTransform = bendArrowSecondary != null ? bendArrowSecondary.transform : null,
                bendArrowSecondaryRenderer = bendArrowSecondaryRenderer,
                bendArrowSecondaryPropertyBlock = bendArrowSecondaryPropertyBlock,
                muteSymbol = muteSymbol,
                muteSymbolTransform = muteSymbol != null ? muteSymbol.transform : null,
                muteSymbolRenderer = muteSymbolRenderer,
                outlineRoot = outlineRoot,
                outlineTransform = outlineRoot != null ? outlineRoot.transform : null,
                techniqueRoot = techniqueRoot,
                techniqueRootTransform = techniqueRoot.transform,
                techniqueSegmentRibbons = techniqueSegmentRibbons,
                techniqueSegmentRibbonRenderers = techniqueSegmentRibbonRenderers,
                techniqueSegmentRibbonPropertyBlocks = techniqueSegmentRibbonPropertyBlocks,
                slideRibbon = slideRibbon,
                slideRibbonRenderer = slideRibbonRenderer,
                slideRibbonPropertyBlock = slideRibbonRenderer != null ? new MaterialPropertyBlock() : null,
                legatoCurve = legatoCurve,
                legatoCurveRenderer = legatoCurveRenderer,
                bendRibbon = bendRibbon,
                bendRibbonRenderer = bendRibbonRenderer,
                bendRibbonPropertyBlock = bendRibbonRenderer != null ? new MaterialPropertyBlock() : null,
                bendSustainRibbon = bendSustainRibbon,
                bendSustainRibbonRenderer = bendSustainRibbonRenderer,
                bendSustainRibbonPropertyBlock = bendSustainRibbonRenderer != null ? new MaterialPropertyBlock() : null,
                sustainRibbon = sustainRibbon,
                sustainRibbonRenderer = sustainRibbonRenderer,
                sustainRibbonPropertyBlock = sustainRibbonRenderer != null ? new MaterialPropertyBlock() : null,
                baseColor = owner.GetStringColor(data.stringIdx),
                baseScale = cube.transform.localScale,
                noteX = xPos,
                noteY = yPos,
                noteStrikeOffset = GetVisualNoteStrikeOffset(cube.transform.localScale),
                hasAnyTechniqueVisual = techniqueSegmentRibbons != null ||
                                       slideRibbon != null ||
                                       legatoCurve != null ||
                                       bendRibbon != null ||
                                       bendSustainRibbon != null ||
                                       sustainRibbon != null
            };
        }
    }

    private void UpdateNoteView(HighwayNoteView view, GameplayNoteState state, float z, float rawTravelZ, float songTime, float floorY, float laneTagY)
    {
        using (UpdateNoteViewProfilerMarker.Auto())
        {
            if (view.noteRoot == null || view.noteTransform == null)
                return;

            float x = view.noteX;
            float y = view.noteY;
            float visualNoteZ = z - view.noteStrikeOffset;
            float rawVisualNoteZ = rawTravelZ - view.noteStrikeOffset;
            float laneTagZ = visualNoteZ - 0.55f;

        view.noteTransform.position = new Vector3(x, y, visualNoteZ);

        bool isStuckOnString = !state.IsResolved && z <= owner.StrikeLineZ + 0.001f;
        bool hideBendTargetBox = bendSourceByDestinationId.ContainsKey(state.data.id);
        bool hideSlideTargetBox = IsSlideDestinationNote(state.data);
        bool hideTravelingNoteBox = hideBendTargetBox || hideSlideTargetBox;
        bool keepNoteBoxVisibleOnString =
            currentNoteByNoteModeEnabled &&
            currentNoteByNoteWaitingForMatch &&
            !state.IsResolved &&
            isStuckOnString &&
            !hideTravelingNoteBox;
        bool hideResolvedCoreVisuals = state.IsResolved &&
            state.resolvedAt >= 0f &&
            songTime >= state.resolvedAt &&
            songTime - state.resolvedAt > GetResolvedFadeTime() &&
            ShouldKeepTechniqueAliveAfterResolution(state.data, songTime);
        bool resolvedNoteFeedbackBoxEnabled = IsResolvedNoteFeedbackBoxEnabled(state);
        bool noteRendererEnabled = resolvedNoteFeedbackBoxEnabled && !hideResolvedCoreVisuals && (!isStuckOnString || keepNoteBoxVisibleOnString) && !hideTravelingNoteBox;
        if (view.noteRenderer != null)
        {
            if (!view.hasCachedNoteRendererEnabled || view.cachedNoteRendererEnabled != noteRendererEnabled)
            {
                view.noteRenderer.enabled = noteRendererEnabled;
                view.cachedNoteRendererEnabled = noteRendererEnabled;
                view.hasCachedNoteRendererEnabled = true;
            }
        }
        bool showOutline = !hideResolvedCoreVisuals && isStuckOnString && !keepNoteBoxVisibleOnString;
        if (view.outlineRoot != null)
        {
            if (showOutline && view.outlineTransform != null)
            {
                view.outlineTransform.position = new Vector3(x, y, GetStuckOutlineCenterZ());
                view.outlineTransform.localScale = Vector3.one;
            }
            SetGameObjectActive(view.outlineRoot, showOutline);
        }
        if (view.label != null)
            SetGameObjectActive(view.label.gameObject, resolvedNoteFeedbackBoxEnabled && !hideResolvedCoreVisuals);

        float tailLength = Mathf.Max(0f, z - owner.StrikeLineZ);
        if (view.tail != null)
        {
            bool showTail = owner.highwayShowApproachLine && tailLength > 0.01f && !state.IsResolved;
            if (showTail && view.tailTransform != null)
            {
                view.tailTransform.position = new Vector3(x, y, owner.StrikeLineZ + (tailLength * 0.5f));
                view.tailTransform.localScale = new Vector3(owner.FretSpacing * 0.06f, 0.06f, tailLength);
            }
            SetGameObjectActive(view.tail, showTail);
        }

        if (view.tether != null && view.tetherRenderer != null && view.tetherMaterial != null)
        {
            float noteBottomY = y - (view.baseScale.y * 0.5f);
            float tetherTopGap = Mathf.Max(0.18f, view.baseScale.y * 0.7f);
            float tetherTopY = noteBottomY - tetherTopGap;
            float tetherLength = Mathf.Max(0f, tetherTopY - floorY);
            bool showTether = tetherLength > 0.02f && z > owner.StrikeLineZ + 0.001f && !state.IsResolved;
            if (showTether && view.tetherTransform != null)
            {
                view.tetherTransform.position = new Vector3(x, floorY + (tetherLength * 0.5f), visualNoteZ);
                view.tetherTransform.localScale = new Vector3(Mathf.Max(0.04f, owner.FretSpacing * 0.05f), tetherLength, Mathf.Max(0.03f, owner.FretSpacing * 0.04f));
            }
            SetGameObjectActive(view.tether, showTether);
        }

        if (view.laneTagLabel != null)
        {
            bool showLaneTag = z > owner.StrikeLineZ + 0.001f && !state.IsResolved;
            if (showLaneTag && view.laneTagTransform != null)
                view.laneTagTransform.position = new Vector3(x, laneTagY, laneTagZ);
            SetGameObjectActive(view.laneTagLabel.gameObject, showLaneTag);
        }

        Vector3 targetScale = view.baseScale;

        Color finalColor = view.baseColor;
        float emission = 0.8f;

        if (state.IsHit || state.IsMissed)
        {
            float fade = Mathf.Clamp01((songTime - state.resolvedAt) / Mathf.Max(0.01f, GetResolvedFadeTime()));
            float hitPulse = 0f;
            bool hasHitPulse = state.IsHit && TryGetResolvedFeedbackPulse(state, songTime, out hitPulse);
            Color resolvedColor = state.IsHit
                ? HighwayHitNoteFeedbackColor
                : HighwayMissNoteFeedbackColor;
            if (hasHitPulse)
                resolvedColor = Color.Lerp(resolvedColor, HighwayHitNoteFeedbackSheenColor, Mathf.Clamp01(hitPulse * 0.32f));
            finalColor = Color.Lerp(resolvedColor, owner.highwayBackgroundColor, fade);
            emission = Mathf.Lerp(state.IsHit ? 2.35f : 0.45f, 0f, fade);
            if (state.IsHit)
            {
                float scalePulse = hasHitPulse ? hitPulse * 0.08f : 0f;
                targetScale = view.baseScale * (Mathf.Lerp(1.14f, 1f, fade) + scalePulse);
            }
        }
        else if (state.isJudgeable)
        {
            emission = 0.95f;
            finalColor = view.baseColor;
        }

        if (!view.hasCachedAppliedNoteScale || !ApproximatelyVector3(view.cachedAppliedNoteScale, targetScale))
        {
            view.noteTransform.localScale = targetScale;
            view.cachedAppliedNoteScale = targetScale;
            view.hasCachedAppliedNoteScale = true;
        }

        if (!view.hasCachedNoteAppearance ||
            view.cachedNoteColor != finalColor ||
            !Mathf.Approximately(view.cachedNoteEmission, emission))
        {
            view.noteMaterial.color = finalColor;
            view.noteMaterial.EnableKeyword("_EMISSION");
            view.noteMaterial.SetColor("_EmissionColor", finalColor * Mathf.Pow(2f, emission));
            view.cachedNoteColor = finalColor;
            view.cachedNoteEmission = emission;
            view.hasCachedNoteAppearance = true;
        }

        if (view.tetherMaterial != null)
        {
            Color tetherColor = new Color(finalColor.r, finalColor.g, finalColor.b, state.IsResolved ? Mathf.Clamp01(finalColor.a * 0.55f) : 0.95f);
            if (!view.hasCachedTetherColor || view.cachedTetherColor != tetherColor)
            {
                view.tetherMaterial.color = tetherColor;
                view.tetherMaterial.SetColor("_Color", tetherColor);
                view.tetherMaterial.SetColor("_BaseColor", tetherColor);
                view.tetherMaterial.SetColor("_TintColor", tetherColor);
                view.cachedTetherColor = tetherColor;
                view.hasCachedTetherColor = true;
            }
        }

        if (view.marker != null)
        {
            bool showMarker = owner.highwayShowLandingDot && !hideResolvedCoreVisuals;
            SetGameObjectActive(view.marker, showMarker);
            if (showMarker && view.markerMaterial != null)
            {
                Color markerColor = state.IsHit ? HighwayHitFretLightColor : (state.IsMissed ? HighwayMissFretLightColor : view.baseColor);
                if (!view.hasCachedMarkerColor || view.cachedMarkerColor != markerColor || !Mathf.Approximately(view.cachedMarkerEmissionMultiplier, state.IsHit ? 2f : 0.8f))
                {
                    float markerEmissionMultiplier = state.IsHit ? 2f : 0.8f;
                    view.markerMaterial.color = markerColor;
                    view.markerMaterial.SetColor("_EmissionColor", markerColor * markerEmissionMultiplier);
                    view.cachedMarkerColor = markerColor;
                    view.cachedMarkerEmissionMultiplier = markerEmissionMultiplier;
                    view.hasCachedMarkerColor = true;
                }
            }
        }

        bool hideOverlaySymbol = songTime >= state.data.time - 0.0001f;

        if (view.bendArrow != null && view.bendArrowRenderer != null && view.bendArrowPropertyBlock != null)
        {
            Vector3 currentScale = targetScale;
            float arrowWidth = Mathf.Max(0.05f, currentScale.x * BendArrowWidthFraction);
            float arrowHeight = Mathf.Max(0.05f, currentScale.y);
            float arrowFrontZ = visualNoteZ - (currentScale.z * 0.5f) - BendArrowFrontOffset;
            float arrowBaseY = y + (currentScale.y * 0.5f);
            bool showPrimaryArrow = !hideResolvedCoreVisuals && !hideTravelingNoteBox && !hideOverlaySymbol;
            int bendArrowCount = GetDisplayedBendArrowCount(state.data);
            bool showSecondaryArrow = showPrimaryArrow && bendArrowCount > 1;

            if (showPrimaryArrow && view.bendArrowTransform != null)
            {
                view.bendArrowTransform.position = new Vector3(x, arrowBaseY, arrowFrontZ);
                view.bendArrowTransform.rotation = Quaternion.identity;
                view.bendArrowTransform.localScale = new Vector3(arrowWidth, arrowHeight, 1f);
            }
            SetGameObjectActive(view.bendArrow, showPrimaryArrow);
            if (showPrimaryArrow)
            {
                view.bendArrowRenderer.GetPropertyBlock(view.bendArrowPropertyBlock);
                view.bendArrowPropertyBlock.SetColor(BendArrowBaseColorShaderId, finalColor);
                view.bendArrowRenderer.SetPropertyBlock(view.bendArrowPropertyBlock);
            }

            if (view.bendArrowSecondary != null && view.bendArrowSecondaryRenderer != null && view.bendArrowSecondaryPropertyBlock != null)
            {
                if (showSecondaryArrow && view.bendArrowSecondaryTransform != null)
                {
                    view.bendArrowSecondaryTransform.position = new Vector3(x, arrowBaseY + (arrowHeight * BendArrowStackOffsetFraction), arrowFrontZ);
                    view.bendArrowSecondaryTransform.rotation = Quaternion.identity;
                    view.bendArrowSecondaryTransform.localScale = new Vector3(arrowWidth, arrowHeight, 1f);
                }
                SetGameObjectActive(view.bendArrowSecondary, showSecondaryArrow);
                if (showSecondaryArrow)
                {
                    view.bendArrowSecondaryRenderer.GetPropertyBlock(view.bendArrowSecondaryPropertyBlock);
                    view.bendArrowSecondaryPropertyBlock.SetColor(BendArrowBaseColorShaderId, finalColor);
                    view.bendArrowSecondaryRenderer.SetPropertyBlock(view.bendArrowSecondaryPropertyBlock);
                }
            }
        }

        if (view.muteSymbol != null)
        {
            Vector3 currentScale = targetScale;
            float referenceNoteSize = Mathf.Max(GetSingleFrettedNoteScale().y, currentScale.y);
            float symbolSize = Mathf.Max(0.05f, referenceNoteSize * MuteSymbolScaleFraction);
            float symbolFrontZ = visualNoteZ - (currentScale.z * 0.5f) - MuteSymbolFrontOffset;
            bool showMuteSymbol = ShouldShowMuteSymbolForNote(state.data) && !hideResolvedCoreVisuals && !hideTravelingNoteBox && !hideOverlaySymbol;

            if (showMuteSymbol && view.muteSymbolTransform != null)
            {
                view.muteSymbolTransform.position = new Vector3(x, y, symbolFrontZ);
                view.muteSymbolTransform.rotation = Quaternion.identity;
                view.muteSymbolTransform.localScale = new Vector3(symbolSize, symbolSize, 1f);
            }
            SetGameObjectActive(view.muteSymbol, showMuteSymbol);
        }

            if (view.hasAnyTechniqueVisual)
                UpdateTechniqueView(view, state, visualNoteZ, rawVisualNoteZ, songTime);
        }
    }


    private void RebuildVisibleNoteStateCache(GuitarGameplaySnapshot snapshot)
    {
        using (RebuildVisibleNoteStateCacheProfilerMarker.Auto())
        {
            noteStatesById.Clear();
            if (snapshot == null || snapshot.noteStates == null)
                return;

            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null)
                    continue;

                noteStatesById[state.data.id] = state;
            }
        }
    }

    private void UpdateTechniqueView(HighwayNoteView view, GameplayNoteState state, float displayVisualZ, float rawVisualNoteZ, float songTime)
    {
        using (UpdateTechniqueViewProfilerMarker.Auto())
        {
            if (view.techniqueRoot == null)
                return;

            if (IsSlideDestinationNote(state.data))
            {
                HideTechniqueView(view);
                return;
            }

            if (HasTechniqueSegments(state.data))
            {
                bool showSegments = UpdateTechniqueSegmentRibbons(view, state, rawVisualNoteZ, songTime);
                SetGameObjectActive(view.techniqueRoot, showSegments);
                return;
            }

            bool showSlide = UpdateSlideTechnique(view, state, displayVisualZ, songTime);
            bool showBend = UpdateBendTechnique(view, state, rawVisualNoteZ, songTime);
            bool showSustain = UpdateNoteSustainTechnique(view, state, rawVisualNoteZ, songTime);
            SetGameObjectActive(view.techniqueRoot, showSlide || showBend || showSustain);
        }
    }

    private void HideTechniqueView(HighwayNoteView view)
    {
        if (view.slideRibbon != null)
            SetGameObjectActive(view.slideRibbon, false);
        if (view.legatoCurve != null)
            SetGameObjectActive(view.legatoCurve, false);
        if (view.bendRibbon != null)
            SetGameObjectActive(view.bendRibbon, false);
        if (view.bendSustainRibbon != null)
            SetGameObjectActive(view.bendSustainRibbon, false);
        if (view.sustainRibbon != null)
            SetGameObjectActive(view.sustainRibbon, false);

        if (view.techniqueSegmentRibbons != null)
        {
            for (int i = 0; i < view.techniqueSegmentRibbons.Length; i++)
                SetGameObjectActive(view.techniqueSegmentRibbons[i], false);
        }

        SetGameObjectActive(view.techniqueRoot, false);
    }

    private bool UpdateSlideTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        using (UpdateSlideTechniqueProfilerMarker.Auto())
        {
            bool useLegatoCurve = view.legatoCurve != null && view.legatoCurveRenderer != null;
            if (!useLegatoCurve && (view.slideRibbon == null || view.slideRibbonRenderer == null))
                return false;

            if (state.data.linkedFromNoteId >= 0 && state.data.slideTargetFret < 0)
            {
                if (view.slideRibbon != null)
                    view.slideRibbon.SetActive(false);
                if (view.legatoCurve != null)
                    view.legatoCurve.SetActive(false);
                return false;
            }

        NoteData anchorData = state.data;
        int targetFret = anchorData.slideTargetFret;
        if (targetFret < 0 || anchorData.fret <= 0)
        {
            if (view.slideRibbon != null)
                view.slideRibbon.SetActive(false);
            if (view.legatoCurve != null)
                view.legatoCurve.SetActive(false);
            return false;
        }

        if (!TryBuildSlideRibbonProfile(view, state, z, songTime, out TechniqueRibbonProfile liveProfile))
        {
            view.slideRibbonFadeState.freezeActive = false;
            if (view.slideRibbon != null)
                view.slideRibbon.SetActive(false);
            if (view.legatoCurve != null)
                view.legatoCurve.SetActive(false);
            return false;
        }

        float fadeStartSongTime = anchorData.time;
        float fadeEndSongTime = GetSlideRibbonFadeEndTime(anchorData, songTime, liveProfile);
        bool shouldFreezeRibbon = songTime >= fadeStartSongTime - 0.0001f;

        if (shouldFreezeRibbon)
        {
            if (!view.slideRibbonFadeState.freezeActive)
            {
                view.slideRibbonFadeState.freezeActive = true;
                view.slideRibbonFadeState.fadeStartSongTime = fadeStartSongTime;
                view.slideRibbonFadeState.fadeEndSongTime = Mathf.Max(fadeStartSongTime + 0.02f, fadeEndSongTime);
            }
        }
        else
        {
            view.slideRibbonFadeState.freezeActive = false;
        }

        float visibleStart01 = 0f;
        if (view.slideRibbonFadeState.freezeActive)
        {
            float fadeDuration = Mathf.Max(0.02f, view.slideRibbonFadeState.fadeEndSongTime - view.slideRibbonFadeState.fadeStartSongTime);
            visibleStart01 = Mathf.Clamp01((songTime - view.slideRibbonFadeState.fadeStartSongTime) / fadeDuration);
            if (visibleStart01 >= 0.999f)
            {
                if (view.slideRibbon != null)
                    view.slideRibbon.SetActive(false);
                if (view.legatoCurve != null)
                    view.legatoCurve.SetActive(false);
                return false;
            }
        }

        if (useLegatoCurve)
            ApplyLegatoCurveTechnique(view, liveProfile, state.IsResolved, visibleStart01);
        else
            ApplySlideTechniqueRibbon(view, liveProfile, state.IsResolved, visibleStart01);
        return true;
        }
    }

    private float GetSlideRibbonFadeEndTime(NoteData anchorData, float songTime, TechniqueRibbonProfile profile)
    {
        if (slideDestinationBySourceId.TryGetValue(anchorData.id, out int destinationId) &&
            chartById.TryGetValue(destinationId, out NoteData destinationData))
        {
            return destinationData.time;
        }

        float estimatedTravelSeconds = Vector3.Distance(profile.start, profile.end) / Mathf.Max(0.01f, currentVisualNoteSpeed);
        return Mathf.Max(anchorData.time + 0.1f, songTime + estimatedTravelSeconds);
    }

    private bool TryBuildSlideRibbonProfile(HighwayNoteView view, GameplayNoteState state, float noteCenterZ, float songTime, out TechniqueRibbonProfile profile)
    {
        profile = default;

        NoteData anchorData = state.data;
        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startTravelZ = noteCenterZ + (startDepth * 0.5f);
        float startAttachZ = startTravelZ;
        float startX = GetVisualNoteX(anchorData);
        float startY = GetStringY(anchorData.stringIdx);

        NoteData? destinationData = null;
        if (slideDestinationBySourceId.TryGetValue(anchorData.id, out int destinationId) && chartById.TryGetValue(destinationId, out NoteData resolvedDestination))
            destinationData = resolvedDestination;

        float endX = destinationData.HasValue ? GetVisualNoteX(destinationData.Value) : GetNoteX(anchorData.slideTargetFret);
        float endY = destinationData.HasValue ? GetStringY(destinationData.Value.stringIdx) : startY;
        float endDepth = startDepth;
        if (destinationData.HasValue)
        {
            if (noteViews.TryGetValue(destinationData.Value.id, out HighwayNoteView destinationView) && destinationView != null)
                endDepth = Mathf.Max(0.1f, destinationView.baseScale.z);
            else
                endDepth = GetApproximateTechniqueNoteDepth(destinationData.Value);
        }
        float endTravelZ;
        if (destinationData.HasValue && noteStatesById.TryGetValue(destinationData.Value.id, out GameplayNoteState destinationState))
        {
            endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((destinationState.data.time - songTime) * currentVisualNoteSpeed));
        }
        else
        {
            endTravelZ = Mathf.Max(startTravelZ + 0.75f, startTravelZ + Mathf.Abs(endX - startX) * 0.50f);
        }

        float endAttachZ = endTravelZ - (endDepth * 0.95f);
        if (endAttachZ <= startAttachZ + 0.05f)
            endAttachZ = startAttachZ + 0.05f;

        Vector3 start = new Vector3(startX, startY, startAttachZ);
        Vector3 end = new Vector3(endX, endY, endAttachZ);
        float length = Vector3.Distance(start, end);
        if (length <= 0.05f)
            return false;

        float leadDistance = Mathf.Clamp(Mathf.Abs(endX - startX) * 0.55f + Mathf.Abs(endAttachZ - startAttachZ) * 0.16f, 0.35f, 2.2f);
        profile.start = start;
        profile.control1 = start + new Vector3(0f, 0f, leadDistance);
        profile.control2 = end - new Vector3(0f, 0f, leadDistance);
        profile.end = end;
        profile.halfWidth = Mathf.Max(0.18f, view.baseScale.x * 0.38f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        return true;
    }

    private float GetApproximateTechniqueNoteDepth(NoteData data)
    {
        if (data.fret <= 0)
            return GetSingleOpenNoteScale().z;

        return GetSingleFrettedNoteScale().z;
    }

    private bool IsSlideDestinationNote(NoteData data)
    {
        if (data.linkedFromNoteId < 0)
            return false;

        return chartById.TryGetValue(data.linkedFromNoteId, out NoteData source) &&
               source.technique == NoteTechnique.Slide;
    }

    private static bool IsLegatoCurveTechnique(NoteData data)
    {
        return data.technique == NoteTechnique.HammerOn || data.technique == NoteTechnique.PullOff;
    }

    private static bool HasBendRibbon(NoteData data)
    {
        return data.technique == NoteTechnique.Bend || data.bendStep > 0f || data.bendPreBend || data.bendRelease;
    }

    private static int GetDisplayedBendArrowCount(NoteData data)
    {
        float bendAmount = Mathf.Max(0f, data.bendStep);
        if (data.techniqueSegments != null)
        {
            for (int i = 0; i < data.techniqueSegments.Count; i++)
            {
                NoteTechniqueSegmentData segment = data.techniqueSegments[i];
                if (segment.type != NoteTechniqueSegmentType.Bend &&
                    segment.type != NoteTechniqueSegmentType.Sustain &&
                    segment.type != NoteTechniqueSegmentType.Vibrato)
                {
                    continue;
                }

                bendAmount = Mathf.Max(
                    bendAmount,
                    Mathf.Abs(segment.startBend),
                    Mathf.Abs(segment.endBend));
            }
        }

        if (bendAmount <= 0.01f)
            return 0;

        return bendAmount > 1.01f ? 2 : 1;
    }

    private static bool HasTechniqueSegments(NoteData data)
    {
        return data.techniqueSegments != null && data.techniqueSegments.Count > 0;
    }

    private static int GetTechniqueSegmentRibbonSlotCount(NoteData data)
    {
        if (!HasTechniqueSegments(data))
            return 0;

        int slotCount = 0;
        for (int i = 0; i < data.techniqueSegments.Count; i++)
            slotCount += GetTechniqueSegmentVisualSlotCount(data.techniqueSegments[i]);

        return Mathf.Max(1, slotCount);
    }

    private static int GetTechniqueSegmentVisualSlotCount(NoteTechniqueSegmentData segment)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Bend:
                return 2;
            case NoteTechniqueSegmentType.Vibrato:
                return GetVibratoSubSegmentCount(segment);
            default:
                return 1;
        }
    }

    private static int GetVibratoSubSegmentCount(NoteTechniqueSegmentData segment)
    {
        float duration = Mathf.Max(0.02f, segment.endOffset - segment.startOffset);
        int cycles = Mathf.Max(2, Mathf.RoundToInt(duration * VibratoCyclesPerSecond));
        return Mathf.Clamp(cycles * 2, VibratoMinimumHalfWaves, VibratoMaximumHalfWaves);
    }

    private static bool HasNoteSustainRibbon(NoteData data)
    {
        return data.duration > GuitarTechniqueVisualThresholds.SustainSeconds &&
               data.fret > 0 &&
               !HasBendRibbon(data) &&
               data.slideTargetFret < 0 &&
               data.linkedFromNoteId < 0;
    }

    private bool TryBuildBendRibbonProfiles(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        out TechniqueRibbonProfile headProfile,
        out bool hasSustainTail,
        out TechniqueRibbonProfile sustainTailProfile,
        out float totalDisplayedDepth)
    {
        headProfile = default;
        sustainTailProfile = default;
        hasSustainTail = false;
        totalDisplayedDepth = 0f;

        float bendAmount = Mathf.Max(0f, state.data.bendStep);
        bool startsBent = state.data.bendPreBend || state.data.bendRelease;
        if (bendAmount <= 0f && !startsBent)
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startAttachZ = noteCenterZ + (startDepth * 0.5f);
        float startX = GetVisualNoteX(state.data);
        float startY = GetStringY(state.data.stringIdx);
        float bendHeight = GetStringLaneSpacing() * BendRibbonVisualHeightInStrings;
        float targetY = startY + bendHeight;

        float bendEndTime = state.data.time + Mathf.Max(0.14f, state.data.duration);
        float fullEndTravelZ = Mathf.Max(
            owner.StrikeLineZ,
            owner.StrikeLineZ + ((bendEndTime - songTime) * currentVisualNoteSpeed));
        float minimumVisualDepth = BendRibbonLeadOutDistance + BendRibbonCornerDepth + BendRibbonMinimumTopHoldDistance;
        float fullEndAttachZ = Mathf.Max(startAttachZ + minimumVisualDepth, Mathf.Max(startAttachZ + 0.4f, fullEndTravelZ));
        float totalDepth = Mathf.Max(minimumVisualDepth, fullEndAttachZ - startAttachZ);
        float leadOutZ = Mathf.Clamp(BendRibbonLeadOutDistance, 0.12f, totalDepth - 0.16f);
        float riseDepth = Mathf.Clamp(BendRibbonCornerDepth, 0.03f, totalDepth - leadOutZ - 0.12f);
        float topHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, totalDepth - leadOutZ - riseDepth);
        float maxHeadTopHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, currentVisualNoteSpeed * BendRibbonHeadMaximumFlatHoldSeconds);
        float headTopHoldLength = Mathf.Min(topHoldLength, maxHeadTopHoldLength);
        float curveEntryZ = startAttachZ + leadOutZ;
        float curvePeakZ = curveEntryZ + riseDepth;
        float headEndAttachZ = curvePeakZ + headTopHoldLength;

        headProfile.start = new Vector3(startX, startY, startAttachZ);
        headProfile.control1 = new Vector3(startX, startY, curveEntryZ);
        headProfile.control2 = new Vector3(startX, targetY, curvePeakZ);
        headProfile.end = new Vector3(startX, targetY, headEndAttachZ);

        headProfile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.34f);
        headProfile.pathMode = 1f;
        headProfile.cornerRoundness = Mathf.Max(0f, BendRibbonCornerRoundness);
        float totalSpan = Mathf.Max(0.01f, headEndAttachZ - startAttachZ);
        float darkBandPadding = Mathf.Clamp(BendRibbonDarkBandPaddingDistance, 0.04f, totalSpan * 0.35f);
        headProfile.darkBandStart01 = Mathf.Clamp01(((curveEntryZ - darkBandPadding) - startAttachZ) / totalSpan);
        headProfile.darkBandEnd01 = Mathf.Clamp01(((curvePeakZ + darkBandPadding) - startAttachZ) / totalSpan);

        if (fullEndAttachZ > headEndAttachZ + 0.05f)
        {
            hasSustainTail = true;
            float headFlatTopLength = Mathf.Max(0.01f, headEndAttachZ - curvePeakZ);
            float initialTailStartZ = headEndAttachZ;
            float initialTailDepth = Mathf.Max(0.01f, fullEndAttachZ - initialTailStartZ);
            TechniqueRibbonProfile initialTailProfile = default;
            initialTailProfile.start = new Vector3(startX, targetY, initialTailStartZ);
            initialTailProfile.control1 = new Vector3(startX, targetY, initialTailStartZ + (initialTailDepth / 3f));
            initialTailProfile.control2 = new Vector3(startX, targetY, initialTailStartZ + ((initialTailDepth * 2f) / 3f));
            initialTailProfile.end = new Vector3(startX, targetY, fullEndAttachZ);

            float joinOverlap = Mathf.Min(
                headFlatTopLength,
                GetRibbonLengthFadeWorldDistance(headProfile) + GetRibbonLengthFadeWorldDistance(initialTailProfile));
            float tailStartZ = headEndAttachZ - joinOverlap;
            float tailEndZ = fullEndAttachZ;
            float tailDepth = Mathf.Max(0.01f, tailEndZ - tailStartZ);
            float firstControlZ = tailStartZ + (tailDepth / 3f);
            float secondControlZ = tailStartZ + ((tailDepth * 2f) / 3f);

            sustainTailProfile.start = new Vector3(startX, targetY, tailStartZ);
            sustainTailProfile.control1 = new Vector3(startX, targetY, firstControlZ);
            sustainTailProfile.control2 = new Vector3(startX, targetY, secondControlZ);
            sustainTailProfile.end = new Vector3(startX, targetY, tailEndZ);
            sustainTailProfile.halfWidth = headProfile.halfWidth;
            sustainTailProfile.pathMode = 0f;
            sustainTailProfile.cornerRoundness = 0f;
            sustainTailProfile.darkBandStart01 = 1f;
            sustainTailProfile.darkBandEnd01 = 1f;
        }

        totalDisplayedDepth = Mathf.Max(0.01f, fullEndAttachZ - startAttachZ);
        return true;
    }

    private static float GetRibbonLengthFadeWorldDistance(TechniqueRibbonProfile profile)
    {
        float approximateLength =
            Vector3.Distance(profile.start, profile.control1) +
            Vector3.Distance(profile.control1, profile.control2) +
            Vector3.Distance(profile.control2, profile.end);
        float fadeSoftness01 = Mathf.Clamp(0.75f / Mathf.Max(0.01f, approximateLength), 0.005f, 0.05f);
        return approximateLength * fadeSoftness01;
    }

    private void ApplySlideTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.04f : 0.08f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.28f : 0.58f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.64f);
        edgeColor.a = isResolved ? 0.46f : 0.98f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.82f) * Mathf.Pow(2f, isResolved ? 0.40f : 1.32f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.slideRibbon,
            view.slideRibbonRenderer,
            view.slideRibbonPropertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            0f);
    }

    private void ApplyLegatoCurveTechnique(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        if (view.legatoCurve == null || view.legatoCurveRenderer == null)
            return;

        SetGameObjectActive(view.legatoCurve, true);

        LineRenderer line = view.legatoCurveRenderer;
        int sampleCount = Mathf.Max(2, LegatoCurveSamples);
        float startT = Mathf.Clamp01(visibleStart01);
        float remaining = 1f - startT;
        if (remaining <= 0.001f)
        {
            view.legatoCurve.SetActive(false);
            return;
        }

        line.positionCount = sampleCount;
        for (int i = 0; i < sampleCount; i++)
        {
            float normalized = i / (float)(sampleCount - 1);
            float t = startT + (normalized * remaining);
            line.SetPosition(i, EvaluateTechniqueBezier(profile, t));
        }

        float width = Mathf.Max(0.04f, view.baseScale.x * LegatoCurveWidthFraction);
        line.startWidth = width;
        line.endWidth = width * 0.92f;

        Color lineColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.12f : 0.08f);
        float alpha = isResolved ? 0.58f : 0.96f;
        lineColor.a = alpha * (1f - (visibleStart01 * 0.8f));
        line.startColor = lineColor;
        line.endColor = lineColor;

        Material lineMat = line.material;
        if (lineMat != null)
        {
            lineMat.color = lineColor;
            lineMat.EnableKeyword("_EMISSION");
            lineMat.SetColor("_EmissionColor", Color.Lerp(view.baseColor, Color.white, 0.18f) * Mathf.Pow(2f, isResolved ? 0.55f : 1.35f));
        }

        if (view.slideRibbon != null)
            view.slideRibbon.SetActive(false);
    }

    private static Vector3 EvaluateTechniqueBezier(TechniqueRibbonProfile profile, float t)
    {
        float u = 1f - t;
        return
            (u * u * u * profile.start) +
            (3f * u * u * t * profile.control1) +
            (3f * u * t * t * profile.control2) +
            (t * t * t * profile.end);
    }

    private void ApplyBendTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        ApplyBendTechniqueRibbon(
            view,
            view.bendRibbon,
            view.bendRibbonRenderer,
            view.bendRibbonPropertyBlock,
            profile,
            isResolved,
            visibleStart01);
    }

    private void ApplyBendTechniqueRibbon(
        HighwayNoteView view,
        GameObject ribbon,
        Renderer ribbonRenderer,
        MaterialPropertyBlock propertyBlock,
        TechniqueRibbonProfile profile,
        bool isResolved,
        float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.10f : 0.16f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.34f : 0.70f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.42f : 0.70f);
        edgeColor.a = isResolved ? 0.50f : 1.0f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.44f : 0.90f) * Mathf.Pow(2f, isResolved ? 0.46f : 1.38f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            ribbon,
            ribbonRenderer,
            propertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            BendRibbonFlatLightStrength);
    }

    private void ApplyNoteSustainTechniqueRibbon(HighwayNoteView view, TechniqueRibbonProfile profile, bool isResolved, float visibleStart01)
    {
        Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.06f : 0.10f);
        Color centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.26f : 0.54f);
        Color edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.52f);
        edgeColor.a = isResolved ? 0.40f : 0.88f;
        Color emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.62f) * Mathf.Pow(2f, isResolved ? 0.34f : 1.0f);

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.sustainRibbon,
            view.sustainRibbonRenderer,
            view.sustainRibbonPropertyBlock,
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            0f);
    }

    private void ApplyTechniqueRibbon(
        Transform techniqueRoot,
        GameObject ribbon,
        Renderer ribbonRenderer,
        MaterialPropertyBlock propertyBlock,
        TechniqueRibbonProfile profile,
        Color centerColor,
        Color edgeColor,
        Color emissionColor,
        float visibleStart01,
        float flatLightStrength)
    {
        if (ribbon == null || ribbonRenderer == null || techniqueRoot == null || propertyBlock == null)
            return;

        Vector3 center = (profile.start + profile.end) * 0.5f;
        ribbon.transform.localPosition = center;
        ribbon.transform.localRotation = Quaternion.identity;
        ribbon.transform.localScale = Vector3.one;

        propertyBlock.Clear();

        propertyBlock.SetVector(CurveP0ShaderId, profile.start - center);
        propertyBlock.SetVector(CurveP1ShaderId, profile.control1 - center);
        propertyBlock.SetVector(CurveP2ShaderId, profile.control2 - center);
        propertyBlock.SetVector(CurveP3ShaderId, profile.end - center);
        propertyBlock.SetFloat(HalfWidthShaderId, profile.halfWidth);
        propertyBlock.SetColor(CenterColorShaderId, centerColor);
        propertyBlock.SetColor(EdgeColorShaderId, edgeColor);
        propertyBlock.SetColor(EmissionColorShaderId, emissionColor);
        float approxRibbonLength =
            Vector3.Distance(profile.start, profile.control1) +
            Vector3.Distance(profile.control1, profile.control2) +
            Vector3.Distance(profile.control2, profile.end);
        float visibleFadeSoftness01 = Mathf.Clamp(0.55f / Mathf.Max(0.01f, approxRibbonLength), 0.0025f, 0.03f);
        float lengthFadeSoftness01 = Mathf.Clamp(0.75f / Mathf.Max(0.01f, approxRibbonLength), 0.005f, 0.05f);
        propertyBlock.SetFloat(VisibleStart01ShaderId, Mathf.Clamp01(visibleStart01));
        propertyBlock.SetFloat(VisibleFadeSoftness01ShaderId, visibleFadeSoftness01);
        propertyBlock.SetFloat(LengthFadeSoftness01ShaderId, lengthFadeSoftness01);
        propertyBlock.SetFloat(FlatLightStrengthShaderId, Mathf.Clamp01(flatLightStrength));
        propertyBlock.SetFloat(PathModeShaderId, profile.pathMode);
        propertyBlock.SetFloat(CornerRoundnessShaderId, Mathf.Max(0f, profile.cornerRoundness));
        propertyBlock.SetFloat(DarkBandStart01ShaderId, Mathf.Clamp01(profile.darkBandStart01));
        propertyBlock.SetFloat(DarkBandEnd01ShaderId, Mathf.Clamp01(profile.darkBandEnd01));
        ribbonRenderer.SetPropertyBlock(propertyBlock);
        ribbon.SetActive(true);
    }

    private bool UpdateBendTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        using (UpdateBendTechniqueProfilerMarker.Auto())
        {
            if (view.bendRibbon == null || view.bendRibbonRenderer == null)
                return false;

            float bendAmount = Mathf.Max(0f, state.data.bendStep);
            if (bendAmount <= 0f && !state.data.bendPreBend && !state.data.bendRelease)
            {
                view.bendRibbon.SetActive(false);
                if (view.bendSustainRibbon != null)
                    view.bendSustainRibbon.SetActive(false);
                return false;
            }

        if (!TryBuildBendRibbonProfiles(
                view,
                state,
                z,
                songTime,
                out TechniqueRibbonProfile headProfile,
                out bool hasSustainTail,
                out TechniqueRibbonProfile sustainTailProfile,
                out float totalDisplayedDepth))
        {
            view.bendRibbon.SetActive(false);
            if (view.bendSustainRibbon != null)
                view.bendSustainRibbon.SetActive(false);
            return false;
        }

        float fadeStartSongTime = state.data.time;
        float displayedRibbonDuration = Mathf.Max(0.02f, totalDisplayedDepth / Mathf.Max(0.01f, currentVisualNoteSpeed));
        float fadeEndSongTime = fadeStartSongTime + displayedRibbonDuration;
        float visibleDistance = 0f;
        if (songTime >= fadeStartSongTime)
        {
            float overallVisibleStart01 = Mathf.Clamp01((songTime - fadeStartSongTime) / Mathf.Max(0.02f, fadeEndSongTime - fadeStartSongTime));
            visibleDistance = overallVisibleStart01 * totalDisplayedDepth;
        }

        float headDepth = Mathf.Max(0.01f, headProfile.end.z - headProfile.start.z);
        float headVisibleStart01 = Mathf.Clamp01(visibleDistance / headDepth);
        float tailVisibleStart01 = 0f;
        if (hasSustainTail)
        {
            float tailDepth = Mathf.Max(0.01f, sustainTailProfile.end.z - sustainTailProfile.start.z);
            tailVisibleStart01 = Mathf.Clamp01((visibleDistance - headDepth) / tailDepth);
        }

        if (DebugBendRibbonLogs && !debugLoggedBendProfileIds.Contains(state.data.id))
        {
            debugLoggedBendProfileIds.Add(state.data.id);
            Debug.Log(
                $"[BEND RENDER] id={state.data.id} songTime={songTime:F3} noteTime={state.data.time:F3} " +
                $"dur={state.data.duration:F3} visualStart={state.data.bendVisualStartTime:F3} visualDur={state.data.bendVisualDuration:F3} " +
                $"bend={state.data.bendStep:F2} pre={state.data.bendPreBend} rel={state.data.bendRelease} " +
                $"visibleStart01={headVisibleStart01:F3} start={headProfile.start} c1={headProfile.control1} c2={headProfile.control2} end={headProfile.end}");
        }

        if (DebugBendRibbonLogs &&
            !debugLoggedBendNearStrikeIds.Contains(state.data.id) &&
            Mathf.Abs(songTime - state.data.time) <= 0.08f)
        {
            debugLoggedBendNearStrikeIds.Add(state.data.id);
            Debug.Log(
                $"[BEND NEAR STRIKE] id={state.data.id} songTime={songTime:F3} noteTime={state.data.time:F3} " +
                $"dur={state.data.duration:F3} visualStart={state.data.bendVisualStartTime:F3} visualDur={state.data.bendVisualDuration:F3} " +
                $"bend={state.data.bendStep:F2} pre={state.data.bendPreBend} rel={state.data.bendRelease} " +
                $"visibleStart01={headVisibleStart01:F3} z={z:F3} start={headProfile.start} c1={headProfile.control1} c2={headProfile.control2} end={headProfile.end}");
        }

        bool anyVisible = false;
        if (headVisibleStart01 < 0.999f)
        {
            ApplyBendTechniqueRibbon(view, headProfile, state.IsResolved, headVisibleStart01);
            anyVisible = true;
        }
        else
        {
            view.bendRibbon.SetActive(false);
        }

        if (hasSustainTail && view.bendSustainRibbon != null && view.bendSustainRibbonRenderer != null && tailVisibleStart01 < 0.999f)
        {
            ApplyBendTechniqueRibbon(
                view,
                view.bendSustainRibbon,
                view.bendSustainRibbonRenderer,
                view.bendSustainRibbonPropertyBlock,
                sustainTailProfile,
                state.IsResolved,
                tailVisibleStart01);
            anyVisible = true;
        }
        else if (view.bendSustainRibbon != null)
        {
            view.bendSustainRibbon.SetActive(false);
        }

            return anyVisible;
        }
    }

    private bool TryBuildNoteSustainRibbonProfile(HighwayNoteView view, GameplayNoteState state, float noteCenterZ, float songTime, out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (!HasNoteSustainRibbon(state.data))
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float startAttachZ = noteCenterZ + (startDepth * 0.5f);
        float startX = GetVisualNoteX(state.data);
        float startY = GetStringY(state.data.stringIdx);
        float sustainEndTime = state.data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, state.data.duration);
        float endTravelZ = Mathf.Max(
            owner.StrikeLineZ,
            owner.StrikeLineZ + ((sustainEndTime - songTime) * currentVisualNoteSpeed));
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        float totalDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        float firstControlZ = startAttachZ + (totalDepth / 3f);
        float secondControlZ = startAttachZ + ((totalDepth * 2f) / 3f);

        profile.start = new Vector3(startX, startY, startAttachZ);
        profile.control1 = new Vector3(startX, startY, firstControlZ);
        profile.control2 = new Vector3(startX, startY, secondControlZ);
        profile.end = new Vector3(startX, startY, startAttachZ + totalDepth);
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool UpdateNoteSustainTechnique(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        using (UpdateNoteSustainTechniqueProfilerMarker.Auto())
        {
            if (view.sustainRibbon == null || view.sustainRibbonRenderer == null)
                return false;

            if (!TryBuildNoteSustainRibbonProfile(view, state, z, songTime, out TechniqueRibbonProfile profile))
            {
                view.sustainRibbon.SetActive(false);
                return false;
            }

        float fadeStartSongTime = state.data.time;
        float fadeEndSongTime = state.data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, state.data.duration);
        float visibleStart01 = 0f;
        if (songTime >= fadeStartSongTime)
        {
            visibleStart01 = Mathf.Clamp01((songTime - fadeStartSongTime) / Mathf.Max(0.02f, fadeEndSongTime - fadeStartSongTime));
            if (visibleStart01 >= 0.999f)
            {
                view.sustainRibbon.SetActive(false);
                return false;
            }
        }

            ApplyNoteSustainTechniqueRibbon(view, profile, state.IsResolved, visibleStart01);
            return true;
        }
    }

    private bool UpdateTechniqueSegmentRibbons(HighwayNoteView view, GameplayNoteState state, float z, float songTime)
    {
        using (UpdateTechniqueSegmentRibbonsProfilerMarker.Auto())
        {
            if (view.techniqueSegmentRibbons == null ||
                view.techniqueSegmentRibbonRenderers == null ||
                view.techniqueSegmentRibbonPropertyBlocks == null ||
                state.data.techniqueSegments == null)
                return false;

            List<NoteTechniqueSegmentData> orderedSegments = state.data.techniqueSegments
                .OrderBy(segment => segment.startOffset)
                .ToList();

        int slotIndex = 0;
        bool anyVisible = false;
        TechniqueRibbonProfile previousProfile = default;
        bool hasPreviousProfile = false;
        float previousEndOffset = -1f;

        for (int segmentIndex = 0; segmentIndex < orderedSegments.Count; segmentIndex++)
        {
            NoteTechniqueSegmentData segment = orderedSegments[segmentIndex];
            if (slotIndex >= view.techniqueSegmentRibbons.Length)
                break;

            bool connectsToNextSegment =
                segmentIndex + 1 < orderedSegments.Count &&
                AreTechniqueSegmentsContinuous(segment.endOffset, orderedSegments[segmentIndex + 1].startOffset);

            float segmentStartTime = state.data.time + segment.startOffset;
            float segmentEndTime = state.data.time + segment.endOffset;
            float segmentVisibleStart01 = 0f;
            if (songTime >= segmentStartTime)
            {
                segmentVisibleStart01 = Mathf.Clamp01((songTime - segmentStartTime) / Mathf.Max(0.02f, segmentEndTime - segmentStartTime));
                if (segmentVisibleStart01 >= 0.999f)
                {
                    int consumedSlots = GetTechniqueSegmentVisualSlotCount(segment);
                    for (int i = 0; i < consumedSlots && slotIndex + i < view.techniqueSegmentRibbons.Length; i++)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + i], false);
                    slotIndex += consumedSlots;
                    continue;
                }
            }

            if (segment.type == NoteTechniqueSegmentType.Bend)
            {
                if (!TryBuildBendSegmentRibbonProfiles(
                        view,
                        state,
                        z,
                        songTime,
                        segment,
                        out TechniqueRibbonProfile headProfile,
                        out bool hasSustainTail,
                        out TechniqueRibbonProfile sustainTailProfile,
                        out float totalDisplayedDepth))
                {
                    if (slotIndex < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                    if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    slotIndex += 2;
                    continue;
                }

                if (hasPreviousProfile && AreTechniqueSegmentsContinuous(previousEndOffset, segment.startOffset))
                    ApplyRibbonJoinOverlap(previousProfile, ref headProfile);

                float visibleDistance = segmentVisibleStart01 * Mathf.Max(0.01f, totalDisplayedDepth);
                float headDepth = Mathf.Max(0.01f, headProfile.end.z - headProfile.start.z);
                float headVisibleStart01 = Mathf.Clamp01(visibleDistance / headDepth);
                float tailVisibleStart01 = 0f;

                if (headVisibleStart01 < 0.999f)
                {
                    ApplyTechniqueSegmentRibbon(view, slotIndex, segment.type, headProfile, state.IsResolved, headVisibleStart01);
                    anyVisible = true;
                }
                else if (slotIndex < view.techniqueSegmentRibbons.Length)
                {
                    SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                }

                if (hasSustainTail)
                {
                    float tailDepth = Mathf.Max(0.01f, sustainTailProfile.end.z - sustainTailProfile.start.z);
                    tailVisibleStart01 = Mathf.Clamp01((visibleDistance - headDepth) / tailDepth);
                    if (tailVisibleStart01 < 0.999f && slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                    {
                        ApplyTechniqueSegmentRibbon(view, slotIndex + 1, NoteTechniqueSegmentType.Sustain, sustainTailProfile, state.IsResolved, tailVisibleStart01);
                        anyVisible = true;
                    }
                    else if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    }
                    previousProfile = sustainTailProfile;
                }
                else
                {
                    if (slotIndex + 1 < view.techniqueSegmentRibbons.Length)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + 1], false);
                    previousProfile = headProfile;
                }

                previousEndOffset = segment.endOffset;
                hasPreviousProfile = true;
                slotIndex += 2;
                continue;
            }

            if (segment.type == NoteTechniqueSegmentType.Vibrato)
            {
                int vibratoSlotCount = GetVibratoSubSegmentCount(segment);
                if (!TryBuildVibratoSegmentMetrics(
                        view,
                        state,
                        z,
                        songTime,
                        segment,
                        out float vibratoStartX,
                        out float vibratoEndX,
                        out float vibratoBaseStartY,
                        out float vibratoBaseEndY,
                        out float vibratoStartAttachZ,
                        out float vibratoTotalDepth))
                {
                    for (int i = 0; i < vibratoSlotCount && slotIndex + i < view.techniqueSegmentRibbons.Length; i++)
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + i], false);
                    slotIndex += vibratoSlotCount;
                    continue;
                }

                float visibleDistance = segmentVisibleStart01 * Mathf.Max(0.01f, vibratoTotalDepth);
                float cumulativeDepth = 0f;
                TechniqueRibbonProfile lastVibratoProfile = default;
                bool hasLastVibratoProfile = false;

                for (int vibratoIndex = 0; vibratoIndex < vibratoSlotCount && slotIndex + vibratoIndex < view.techniqueSegmentRibbons.Length; vibratoIndex++)
                {
                    if (!TryBuildVibratoSubRibbonProfile(
                            view,
                            segment,
                            vibratoStartX,
                            vibratoEndX,
                            vibratoBaseStartY,
                            vibratoBaseEndY,
                            vibratoStartAttachZ,
                            vibratoTotalDepth,
                            vibratoIndex,
                            vibratoSlotCount,
                            out TechniqueRibbonProfile vibratoProfile))
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + vibratoIndex], false);
                        continue;
                    }

                    if (vibratoIndex == 0 &&
                        hasPreviousProfile &&
                        AreTechniqueSegmentsContinuous(previousEndOffset, segment.startOffset))
                    {
                        ApplyRibbonJoinOverlap(previousProfile, ref vibratoProfile);
                    }
                    else if (vibratoIndex > 0 && hasLastVibratoProfile)
                    {
                        ApplyRibbonJoinOverlap(lastVibratoProfile, ref vibratoProfile);
                    }

                    float vibratoDepth = Mathf.Max(0.01f, vibratoProfile.end.z - vibratoProfile.start.z);
                    float vibratoVisibleStart01 = Mathf.Clamp01((visibleDistance - cumulativeDepth) / vibratoDepth);
                    if (vibratoVisibleStart01 < 0.999f)
                    {
                        ApplyTechniqueSegmentRibbon(view, slotIndex + vibratoIndex, segment.type, vibratoProfile, state.IsResolved, vibratoVisibleStart01);
                        anyVisible = true;
                    }
                    else
                    {
                        SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex + vibratoIndex], false);
                    }

                    cumulativeDepth += vibratoDepth;
                    lastVibratoProfile = vibratoProfile;
                    hasLastVibratoProfile = true;
                }

                previousEndOffset = segment.endOffset;
                if (hasLastVibratoProfile)
                {
                    previousProfile = lastVibratoProfile;
                    hasPreviousProfile = true;
                }
                slotIndex += vibratoSlotCount;
                continue;
            }

            if (!TryBuildTechniqueSegmentRibbonProfile(view, state, z, songTime, segment, connectsToNextSegment, out TechniqueRibbonProfile profile))
            {
                if (slotIndex < view.techniqueSegmentRibbons.Length)
                    SetGameObjectActive(view.techniqueSegmentRibbons[slotIndex], false);
                slotIndex++;
                continue;
            }

            if (hasPreviousProfile && AreTechniqueSegmentsContinuous(previousEndOffset, segment.startOffset))
                ApplyRibbonJoinOverlap(previousProfile, ref profile);

            ApplyTechniqueSegmentRibbon(view, slotIndex, segment.type, profile, state.IsResolved, segmentVisibleStart01);
            anyVisible = true;
            previousProfile = profile;
            previousEndOffset = segment.endOffset;
            hasPreviousProfile = true;
            slotIndex++;
        }

        for (int i = slotIndex; i < view.techniqueSegmentRibbons.Length; i++)
            SetGameObjectActive(view.techniqueSegmentRibbons[i], false);

            return anyVisible;
        }
    }

    private bool TryBuildTechniqueSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        bool connectsToNextSegment,
        out TechniqueRibbonProfile profile)
    {
        switch (segment.type)
        {
            case NoteTechniqueSegmentType.Slide:
                return TryBuildSlideSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, connectsToNextSegment, out profile);
            case NoteTechniqueSegmentType.Bend:
                return TryBuildBendSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, out profile);
            case NoteTechniqueSegmentType.Sustain:
                return TryBuildFlatSegmentRibbonProfile(view, state, noteCenterZ, songTime, segment, out profile);
            case NoteTechniqueSegmentType.Vibrato:
                if (!TryBuildVibratoSegmentMetrics(
                        view,
                        state,
                        noteCenterZ,
                        songTime,
                        segment,
                        out float vibratoStartX,
                        out float vibratoEndX,
                        out float vibratoBaseStartY,
                        out float vibratoBaseEndY,
                        out float vibratoStartAttachZ,
                        out float vibratoTotalDepth))
                {
                    profile = default;
                    return false;
                }

                return TryBuildVibratoSubRibbonProfile(
                    view,
                    segment,
                    vibratoStartX,
                    vibratoEndX,
                    vibratoBaseStartY,
                    vibratoBaseEndY,
                    vibratoStartAttachZ,
                    vibratoTotalDepth,
                    0,
                    GetVibratoSubSegmentCount(segment),
                    out profile);
            default:
                profile = default;
                return false;
        }
    }

    private bool TryBuildSlideSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        bool connectsToNextSegment,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (segment.startFret <= 0)
            return false;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = connectsToNextSegment
            ? endTravelZ + (startDepth * 0.5f)
            : endTravelZ - (startDepth * 0.95f);
        if (endAttachZ <= startAttachZ + 0.05f)
            endAttachZ = startAttachZ + 0.05f;

        float startX = GetNoteX(segment.startFret);
        float endX = GetNoteX(segment.endFret);
        float startY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        float endY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        Vector3 start = new Vector3(startX, startY, startAttachZ);
        Vector3 end = new Vector3(endX, endY, endAttachZ);
        float length = Vector3.Distance(start, end);
        if (length <= 0.05f)
            return false;

        float leadDistance = Mathf.Clamp(Mathf.Abs(endX - startX) * 0.55f + Mathf.Abs(endAttachZ - startAttachZ) * 0.16f, 0.35f, 2.2f);
        profile.start = start;
        profile.control1 = start + new Vector3(0f, 0f, leadDistance);
        profile.control2 = end - new Vector3(0f, 0f, leadDistance);
        profile.end = end;
        profile.halfWidth = Mathf.Max(0.18f, view.baseScale.x * 0.38f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool TryBuildFlatSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        float totalDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        float firstControlZ = startAttachZ + (totalDepth / 3f);
        float secondControlZ = startAttachZ + ((totalDepth * 2f) / 3f);
        float x = GetSegmentFretVisualX(state.data, segment.endFret);
        float y = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        profile.start = new Vector3(x, y, startAttachZ);
        profile.control1 = new Vector3(x, y, firstControlZ);
        profile.control2 = new Vector3(x, y, secondControlZ);
        profile.end = new Vector3(x, y, startAttachZ + totalDepth);
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return true;
    }

    private bool TryBuildBendSegmentRibbonProfile(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile profile)
    {
        return TryBuildBendSegmentRibbonProfiles(
            view,
            state,
            noteCenterZ,
            songTime,
            segment,
            out profile,
            out _,
            out _,
            out _);
    }

    private bool TryBuildBendSegmentRibbonProfiles(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out TechniqueRibbonProfile headProfile,
        out bool hasSustainTail,
        out TechniqueRibbonProfile sustainTailProfile,
        out float totalDisplayedDepth)
    {
        headProfile = default;
        sustainTailProfile = default;
        hasSustainTail = false;
        totalDisplayedDepth = 0f;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float fullEndTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        float startAttachZ = startTravelZ + (startDepth * 0.5f);
        float startX = GetNoteX(segment.startFret);
        float endX = GetNoteX(segment.endFret);
        float startY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        float targetY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);

        float minimumVisualDepth = BendRibbonLeadOutDistance + BendRibbonCornerDepth + BendRibbonMinimumTopHoldDistance;
        float fullEndAttachZ = Mathf.Max(startAttachZ + minimumVisualDepth, Mathf.Max(startAttachZ + 0.4f, fullEndTravelZ));
        float totalDepth = Mathf.Max(minimumVisualDepth, fullEndAttachZ - startAttachZ);
        float leadOutZ = Mathf.Clamp(BendRibbonLeadOutDistance, 0.12f, totalDepth - 0.16f);
        float riseDepth = Mathf.Clamp(BendRibbonCornerDepth, 0.03f, totalDepth - leadOutZ - 0.12f);
        float topHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, totalDepth - leadOutZ - riseDepth);
        float maxHeadTopHoldLength = Mathf.Max(BendRibbonMinimumTopHoldDistance, currentVisualNoteSpeed * BendRibbonHeadMaximumFlatHoldSeconds);
        float headTopHoldLength = Mathf.Min(topHoldLength, maxHeadTopHoldLength);
        float curveEntryZ = startAttachZ + leadOutZ;
        float curvePeakZ = curveEntryZ + riseDepth;
        float headEndAttachZ = curvePeakZ + headTopHoldLength;

        headProfile.start = new Vector3(startX, startY, startAttachZ);
        headProfile.control1 = new Vector3(startX, startY, curveEntryZ);
        headProfile.control2 = new Vector3(endX, targetY, curvePeakZ);
        headProfile.end = new Vector3(endX, targetY, headEndAttachZ);
        headProfile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.34f);
        headProfile.pathMode = 1f;
        headProfile.cornerRoundness = Mathf.Max(0f, BendRibbonCornerRoundness);
        float totalSpan = Mathf.Max(0.01f, headEndAttachZ - startAttachZ);
        float darkBandPadding = Mathf.Clamp(BendRibbonDarkBandPaddingDistance, 0.04f, totalSpan * 0.35f);
        headProfile.darkBandStart01 = Mathf.Clamp01(((curveEntryZ - darkBandPadding) - startAttachZ) / totalSpan);
        headProfile.darkBandEnd01 = Mathf.Clamp01(((curvePeakZ + darkBandPadding) - startAttachZ) / totalSpan);

        if (fullEndAttachZ > headEndAttachZ + 0.05f)
        {
            hasSustainTail = true;
            float headFlatTopLength = Mathf.Max(0.01f, headEndAttachZ - curvePeakZ);
            float initialTailStartZ = headEndAttachZ;
            float initialTailDepth = Mathf.Max(0.01f, fullEndAttachZ - initialTailStartZ);
            TechniqueRibbonProfile initialTailProfile = default;
            initialTailProfile.start = new Vector3(endX, targetY, initialTailStartZ);
            initialTailProfile.control1 = new Vector3(endX, targetY, initialTailStartZ + (initialTailDepth / 3f));
            initialTailProfile.control2 = new Vector3(endX, targetY, initialTailStartZ + ((initialTailDepth * 2f) / 3f));
            initialTailProfile.end = new Vector3(endX, targetY, fullEndAttachZ);

            float joinOverlap = Mathf.Min(
                headFlatTopLength,
                GetRibbonLengthFadeWorldDistance(headProfile) + GetRibbonLengthFadeWorldDistance(initialTailProfile));
            float tailStartZ = headEndAttachZ - joinOverlap;
            float tailEndZ = fullEndAttachZ;
            float tailDepth = Mathf.Max(0.01f, tailEndZ - tailStartZ);
            float firstControlZ = tailStartZ + (tailDepth / 3f);
            float secondControlZ = tailStartZ + ((tailDepth * 2f) / 3f);

            sustainTailProfile.start = new Vector3(endX, targetY, tailStartZ);
            sustainTailProfile.control1 = new Vector3(endX, targetY, firstControlZ);
            sustainTailProfile.control2 = new Vector3(endX, targetY, secondControlZ);
            sustainTailProfile.end = new Vector3(endX, targetY, tailEndZ);
            sustainTailProfile.halfWidth = headProfile.halfWidth;
            sustainTailProfile.pathMode = 0f;
            sustainTailProfile.cornerRoundness = 0f;
            sustainTailProfile.darkBandStart01 = 1f;
            sustainTailProfile.darkBandEnd01 = 1f;
        }

        totalDisplayedDepth = Mathf.Max(0.01f, fullEndAttachZ - startAttachZ);
        return true;
    }

    private bool TryBuildVibratoSegmentMetrics(
        HighwayNoteView view,
        GameplayNoteState state,
        float noteCenterZ,
        float songTime,
        NoteTechniqueSegmentData segment,
        out float startX,
        out float endX,
        out float baseStartY,
        out float baseEndY,
        out float startAttachZ,
        out float totalDisplayedDepth)
    {
        startX = 0f;
        endX = 0f;
        baseStartY = 0f;
        baseEndY = 0f;
        startAttachZ = 0f;
        totalDisplayedDepth = 0f;

        float startDepth = Mathf.Max(0.1f, view.baseScale.z);
        float segmentStartTime = state.data.time + segment.startOffset;
        float segmentEndTime = state.data.time + segment.endOffset;
        float startTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentStartTime - songTime) * currentVisualNoteSpeed));
        float endTravelZ = Mathf.Max(owner.StrikeLineZ, owner.StrikeLineZ + ((segmentEndTime - songTime) * currentVisualNoteSpeed));
        startAttachZ = startTravelZ + (startDepth * 0.5f);
        float endAttachZ = Mathf.Max(startAttachZ + 0.35f, endTravelZ);
        totalDisplayedDepth = Mathf.Max(0.35f, endAttachZ - startAttachZ);
        if (totalDisplayedDepth <= 0.05f)
            return false;

        startX = GetNoteX(segment.startFret);
        endX = GetNoteX(segment.endFret);
        baseStartY = GetSegmentBendVisualY(state.data.stringIdx, segment.startBend);
        baseEndY = GetSegmentBendVisualY(state.data.stringIdx, segment.endBend);
        return true;
    }

    private bool TryBuildVibratoSubRibbonProfile(
        HighwayNoteView view,
        NoteTechniqueSegmentData segment,
        float startX,
        float endX,
        float baseStartY,
        float baseEndY,
        float startAttachZ,
        float totalDisplayedDepth,
        int vibratoIndex,
        int vibratoSlotCount,
        out TechniqueRibbonProfile profile)
    {
        profile = default;

        if (vibratoSlotCount <= 0 || vibratoIndex < 0 || vibratoIndex >= vibratoSlotCount)
            return false;

        float t0 = vibratoIndex / (float)vibratoSlotCount;
        float t1 = (vibratoIndex + 1) / (float)vibratoSlotCount;
        float cycles = vibratoSlotCount * 0.5f;
        float omega = cycles * Mathf.PI * 2f;
        float amplitude = GetStringLaneSpacing() * VibratoRibbonAmplitudeInStrings;
        float baselineSlopeY = baseEndY - baseStartY;
        float subTSpan = t1 - t0;

        Vector3 p0 = EvaluateVibratoPoint(startX, endX, baseStartY, baseEndY, startAttachZ, totalDisplayedDepth, amplitude, omega, t0);
        Vector3 p3 = EvaluateVibratoPoint(startX, endX, baseStartY, baseEndY, startAttachZ, totalDisplayedDepth, amplitude, omega, t1);
        Vector3 d0 = EvaluateVibratoDerivative(startX, endX, baselineSlopeY, totalDisplayedDepth, amplitude, omega, t0) * subTSpan;
        Vector3 d1 = EvaluateVibratoDerivative(startX, endX, baselineSlopeY, totalDisplayedDepth, amplitude, omega, t1) * subTSpan;

        profile.start = p0;
        profile.control1 = p0 + (d0 / 3f);
        profile.control2 = p3 - (d1 / 3f);
        profile.end = p3;
        profile.halfWidth = Mathf.Max(0.16f, view.baseScale.x * 0.30f);
        profile.pathMode = 0f;
        profile.cornerRoundness = 0f;
        profile.darkBandStart01 = 1f;
        profile.darkBandEnd01 = 1f;
        return profile.end.z > profile.start.z + 0.01f;
    }

    private static Vector3 EvaluateVibratoPoint(
        float startX,
        float endX,
        float baseStartY,
        float baseEndY,
        float startAttachZ,
        float totalDisplayedDepth,
        float amplitude,
        float omega,
        float t)
    {
        float x = Mathf.Lerp(startX, endX, t);
        float baselineY = Mathf.Lerp(baseStartY, baseEndY, t);
        float y = baselineY + (Mathf.Sin(t * omega) * amplitude);
        float z = startAttachZ + (totalDisplayedDepth * t);
        return new Vector3(x, y, z);
    }

    private static Vector3 EvaluateVibratoDerivative(
        float startX,
        float endX,
        float baselineSlopeY,
        float totalDisplayedDepth,
        float amplitude,
        float omega,
        float t)
    {
        float dx = endX - startX;
        float dy = baselineSlopeY + (Mathf.Cos(t * omega) * amplitude * omega);
        float dz = totalDisplayedDepth;
        return new Vector3(dx, dy, dz);
    }

    private void ApplyRibbonJoinOverlap(TechniqueRibbonProfile previousProfile, ref TechniqueRibbonProfile currentProfile)
    {
        Vector3 snapDelta = previousProfile.end - currentProfile.start;
        currentProfile.start += snapDelta;
        currentProfile.control1 += snapDelta;

        float overlap = GetRibbonLengthFadeWorldDistance(previousProfile) + GetRibbonLengthFadeWorldDistance(currentProfile);
        Vector3 direction = currentProfile.control1 - currentProfile.start;
        if (direction.sqrMagnitude <= 0.0001f)
            direction = currentProfile.end - currentProfile.start;
        if (direction.sqrMagnitude <= 0.0001f)
            return;

        direction.Normalize();
        Vector3 offset = direction * Mathf.Min(overlap, Vector3.Distance(currentProfile.start, currentProfile.end) * 0.45f);
        currentProfile.start -= offset;
        currentProfile.control1 -= offset;
    }

    private static bool AreTechniqueSegmentsContinuous(float previousEndOffset, float nextStartOffset)
    {
        return Mathf.Abs(nextStartOffset - previousEndOffset) <= TechniqueSegmentJoinToleranceSeconds;
    }

    private float GetSegmentBendVisualY(int stringIdx, float bendValue)
    {
        float baseY = GetStringY(stringIdx);
        if (Mathf.Abs(bendValue) <= 0.01f)
            return baseY;

        return baseY + (GetStringLaneSpacing() * BendRibbonVisualHeightInStrings);
    }

    private void ApplyTechniqueSegmentRibbon(
        HighwayNoteView view,
        int slotIndex,
        NoteTechniqueSegmentType segmentType,
        TechniqueRibbonProfile profile,
        bool isResolved,
        float visibleStart01)
    {
        if (view.techniqueSegmentRibbons == null ||
            slotIndex < 0 ||
            slotIndex >= view.techniqueSegmentRibbons.Length)
            return;

        float flatLightStrength = segmentType == NoteTechniqueSegmentType.Bend ? BendRibbonFlatLightStrength : 0f;

        Color centerColor;
        Color edgeColor;
        Color emissionColor;
        if (segmentType == NoteTechniqueSegmentType.Slide)
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.04f : 0.08f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.28f : 0.58f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.64f);
            edgeColor.a = isResolved ? 0.46f : 0.98f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.34f : 0.82f) * Mathf.Pow(2f, isResolved ? 0.40f : 1.32f);
        }
        else if (segmentType == NoteTechniqueSegmentType.Bend)
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.10f : 0.16f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.34f : 0.70f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.42f : 0.70f);
            edgeColor.a = isResolved ? 0.50f : 1.0f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.44f : 0.90f) * Mathf.Pow(2f, isResolved ? 0.46f : 1.38f);
        }
        else
        {
            Color centerBaseColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.06f : 0.10f);
            centerColor = new Color(centerBaseColor.r, centerBaseColor.g, centerBaseColor.b, isResolved ? 0.26f : 0.54f);
            edgeColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.52f);
            edgeColor.a = isResolved ? 0.40f : 0.88f;
            emissionColor = Color.Lerp(view.baseColor, Color.white, isResolved ? 0.26f : 0.62f) * Mathf.Pow(2f, isResolved ? 0.34f : 1.0f);
        }

        ApplyTechniqueRibbon(
            view.techniqueRoot.transform,
            view.techniqueSegmentRibbons[slotIndex],
            view.techniqueSegmentRibbonRenderers[slotIndex],
            view.techniqueSegmentRibbonPropertyBlocks[slotIndex],
            profile,
            centerColor,
            edgeColor,
            emissionColor,
            visibleStart01,
            flatLightStrength);
    }

    private void UpdateArpeggioGuides(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateArpeggioGuidesProfilerMarker.Auto())
        {
            activeArpeggioGuideIdsThisFrame.Clear();
            List<ArpeggioGuideData> guides = snapshot?.arpeggioGuides;
            if (guides == null || guides.Count == 0)
            {
                ClearArpeggioGuideFrames();
                return;
            }

            float renderSongTime = GetRenderSongTime(snapshot);
            for (int i = 0; i < guides.Count; i++)
            {
                ArpeggioGuideData guide = guides[i];
                if (!IsRenderableArpeggioGuide(guide))
                    continue;

                float z = owner.StrikeLineZ + ((guide.startTime - renderSongTime) * currentVisualNoteSpeed);
                bool visible = z <= owner.SpawnZ && z > owner.StrikeLineZ + 0.001f;
                if (!visible)
                    continue;

                activeArpeggioGuideIdsThisFrame.Add(guide.id);
                if (!arpeggioGuideFrames.TryGetValue(guide.id, out GameObject frame) || frame == null)
                {
                    frame = CreateArpeggioGuideFrame(guide);
                    arpeggioGuideFrames[guide.id] = frame;
                }

                if (frame != null)
                {
                    frame.transform.position = new Vector3(frame.transform.position.x, frame.transform.position.y, z + 0.012f);
                    UpdateArpeggioGuideIndicatorStates(frame, guide, snapshot, renderSongTime);
                }
            }

            arpeggioGuideRemovalBuffer.Clear();
            foreach (KeyValuePair<int, GameObject> pair in arpeggioGuideFrames)
            {
                if (!activeArpeggioGuideIdsThisFrame.Contains(pair.Key))
                    arpeggioGuideRemovalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < arpeggioGuideRemovalBuffer.Count; i++)
            {
                int key = arpeggioGuideRemovalBuffer[i];
                if (arpeggioGuideFrames[key] != null)
                    Object.Destroy(arpeggioGuideFrames[key]);
                arpeggioGuideFrames.Remove(key);
            }
        }
    }

    private void ClearArpeggioGuideFrames()
    {
        if (arpeggioGuideFrames.Count == 0)
            return;

        arpeggioGuideRemovalBuffer.Clear();
        foreach (int key in arpeggioGuideFrames.Keys)
            arpeggioGuideRemovalBuffer.Add(key);

        for (int i = 0; i < arpeggioGuideRemovalBuffer.Count; i++)
        {
            int key = arpeggioGuideRemovalBuffer[i];
            if (arpeggioGuideFrames[key] != null)
                Object.Destroy(arpeggioGuideFrames[key]);
            arpeggioGuideFrames.Remove(key);
        }
    }

    private bool IsRenderableArpeggioGuide(ArpeggioGuideData guide)
    {
        if (guide == null || guide.stringFrets == null || guide.stringFrets.Length == 0)
            return false;

        for (int i = 0; i < guide.stringFrets.Length; i++)
        {
            if (guide.stringFrets[i] >= 0)
                return true;
        }

        return false;
    }

    private int GetArpeggioHandFret(ArpeggioGuideData guide)
    {
        if (guide?.stringFrets == null || guide.stringFrets.Length == 0)
            return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);

        int minFret = int.MaxValue;
        for (int i = 0; i < guide.stringFrets.Length; i++)
        {
            int fret = guide.stringFrets[i];
            if (fret > 0)
                minFret = Mathf.Min(minFret, fret);
        }

        if (minFret != int.MaxValue)
            return Mathf.Clamp(minFret, 1, owner.TotalFrets - 3);

        return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);
    }

    private float GetArpeggioHandWindowEndX(int handFret, ArpeggioGuideData guide)
    {
        int furthestFret = handFret + 3;
        if (guide?.stringFrets != null)
        {
            for (int i = 0; i < guide.stringFrets.Length; i++)
            {
                int fret = guide.stringFrets[i];
                if (fret > 0)
                    furthestFret = Mathf.Max(furthestFret, fret);
            }
        }

        return GetNoteX(furthestFret) + (owner.FretSpacing * 0.2f);
    }

    private float GetArpeggioBoxHeight(ArpeggioGuideData guide)
    {
        int renderableStringCount = GetRenderableStringCount();
        if (renderableStringCount <= 0)
            return GetStringLaneSpacing();

        if (renderableStringCount == 1)
            return GetStringLaneSpacing();

        float minY = GetStringY(0);
        float maxY = GetStringY(renderableStringCount - 1);
        return Mathf.Max(1f, Mathf.Abs(maxY - minY) + owner.chordFrameVerticalPadding);
    }

    private float GetArpeggioBoxCenterY(ArpeggioGuideData guide)
    {
        int renderableStringCount = GetRenderableStringCount();
        if (renderableStringCount <= 0)
            return 0f;

        return (GetStringY(0) + GetStringY(renderableStringCount - 1)) * 0.5f;
    }

    private GameObject CreateArpeggioGuideFrame(ArpeggioGuideData guide)
    {
        int handFret = GetArpeggioHandFret(guide);
        float leftX = GetHandWindowStartX(handFret);
        float rightX = GetArpeggioHandWindowEndX(handFret, guide);
        float height = GetArpeggioBoxHeight(guide);
        float width = Mathf.Max(0.5f, rightX - leftX);
        float centerY = GetArpeggioBoxCenterY(guide);

        GameObject frame = CreateChordFrame(leftX, rightX, centerY, height, new Color(0.62f, 0.12f, 1f), 2.2f);
        UpdateChordFrameLabel(frame, GetArpeggioDisplayName(guide), width, height);
        CreateArpeggioGuideShape(frame.transform, guide, (leftX + rightX) * 0.5f, centerY, width);
        return frame;
    }

    private void CreateChordFrameBackground(Transform parent, float width, float height)
    {
        if (parent == null)
            return;

        float inset = owner.chordFrameThickness * 1.35f;
        float backgroundWidth = Mathf.Max(0.16f, width - inset);
        float backgroundHeight = Mathf.Max(0.16f, height - inset);
        float backgroundDepth = 0.025f;
        Material backgroundMat = owner.CreateSharedTransparentMaterial(new Color(0.18f, 0.20f, 0.24f, 0.42f), 0.08f);
        ConfigureOverlayMaterial(backgroundMat, 100, true);
        CreateFramePiece(parent, new Vector3(0f, 0f, 0.012f), new Vector3(backgroundWidth, backgroundHeight, backgroundDepth), backgroundMat);
    }

    private void CreateArpeggioGuideShape(Transform parent, ArpeggioGuideData guide, float centerX, float centerY, float width)
    {
        if (guide?.stringFrets == null)
            return;

        int maxStrings = Mathf.Min(guide.stringFrets.Length, GetRenderableStringCount());
        for (int stringIndex = 0; stringIndex < maxStrings; stringIndex++)
        {
            int fret = guide.stringFrets[stringIndex];
            if (fret < 0)
                continue;

            float localY = GetStringY(stringIndex) - centerY;
            Color stringColor = owner.GetStringColor(stringIndex);
            if (fret == 0)
            {
                CreateArpeggioOpenStringDottedLine(parent, stringIndex, localY, width, stringColor);
                continue;
            }

            GameObject outline = CreateArpeggioNoteOutline(GetSingleFrettedNoteScale(), stringColor);
            outline.name = $"ArpeggioSlot_{stringIndex}";
            outline.transform.SetParent(parent, false);
            outline.transform.localPosition = new Vector3(GetNoteX(fret) - centerX, localY, -0.01f);
            outline.transform.localRotation = Quaternion.identity;
            outline.transform.localScale = Vector3.one;
            SetGameObjectActive(outline, true);
        }
    }

    private void CreateArpeggioOpenStringDottedLine(Transform parent, int stringIndex, float localY, float width, Color color)
    {
        GameObject dottedRoot = new GameObject($"ArpeggioOpen_{stringIndex}");
        dottedRoot.transform.SetParent(parent, false);
        dottedRoot.transform.localPosition = Vector3.zero;
        dottedRoot.transform.localRotation = Quaternion.identity;
        dottedRoot.transform.localScale = Vector3.one;

        Material dashMat = owner.CreateSharedGlowMaterial(color, 0.8f);
        ConfigureOverlayMaterial(dashMat, 120, true);
        float usableWidth = Mathf.Max(owner.FretSpacing * 1.2f, width - (owner.FretSpacing * 0.35f));
        float openHeight = GetScaledOpenHeight();
        float openDepth = GetScaledOpenDepth();
        float targetDashWidth = Mathf.Max(owner.FretSpacing * 0.34f, usableWidth * 0.1f);
        float gapWidth = Mathf.Max(owner.FretSpacing * 0.16f, targetDashWidth * 0.45f);
        int dashCount = Mathf.Max(3, Mathf.FloorToInt((usableWidth + gapWidth) / (targetDashWidth + gapWidth)));
        float totalGapWidth = gapWidth * Mathf.Max(0, dashCount - 1);
        float dashWidth = dashCount > 0 ? Mathf.Max(owner.FretSpacing * 0.22f, (usableWidth - totalGapWidth) / dashCount) : usableWidth;
        float occupiedWidth = (dashWidth * dashCount) + totalGapWidth;
        float startX = -occupiedWidth * 0.5f + (dashWidth * 0.5f);

        for (int i = 0; i < dashCount; i++)
        {
            float dashX = startX + ((dashWidth + gapWidth) * i);
            CreateFramePiece(
                dottedRoot.transform,
                new Vector3(dashX, localY, -0.01f),
                new Vector3(dashWidth, openHeight, openDepth),
                dashMat);
        }
    }

    private GameObject CreateArpeggioNoteOutline(Vector3 noteScale, Color color)
    {
        GameObject outlineRoot = new GameObject("ArpeggioNoteOutline");
        outlineRoot.transform.SetParent(gameplayRoot.transform, false);

        float width = Mathf.Max(0.12f, noteScale.x);
        float height = Mathf.Max(0.12f, noteScale.y);
        float depth = 0.012f;
        float thickness = Mathf.Clamp(Mathf.Min(width, height) * 0.16f, 0.045f, 0.085f);
        float insetHalfWidth = Mathf.Max(0f, (width - thickness) * 0.5f);
        float insetHalfHeight = Mathf.Max(0f, (height - thickness) * 0.5f);
        Material outlineMat = owner.CreateSharedTransparentMaterial(new Color(color.r, color.g, color.b, 0.92f), 0.12f);
        ConfigureOverlayMaterial(outlineMat, 125, true);

        CreateFramePiece(outlineRoot.transform, new Vector3(0f, insetHalfHeight, 0f), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(0f, -insetHalfHeight, 0f), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(-insetHalfWidth, 0f, 0f), new Vector3(thickness, height, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(insetHalfWidth, 0f, 0f), new Vector3(thickness, height, depth), outlineMat);
        return outlineRoot;
    }

    private string GetArpeggioDisplayName(ArpeggioGuideData guide)
    {
        if (guide == null)
            return string.Empty;

        if (!string.IsNullOrWhiteSpace(guide.chordName))
            return guide.chordName.Trim();

        return DeriveArpeggioDisplayName(guide);
    }

    private string DeriveArpeggioDisplayName(ArpeggioGuideData guide)
    {
        if (guide?.stringFrets == null)
            return string.Empty;

        HashSet<int> pitchClassSet = new HashSet<int>();
        int? bassPitchClass = null;
        int bassStringIndex = int.MaxValue;
        int maxStrings = Mathf.Min(guide.stringFrets.Length, GetRenderableStringCount());
        for (int stringIndex = 0; stringIndex < maxStrings; stringIndex++)
        {
            int fret = guide.stringFrets[stringIndex];
            if (fret < 0)
                continue;

            int pitchClass = Mod12(owner.GetStringBasePitch(stringIndex) + fret);
            pitchClassSet.Add(pitchClass);
            if (!bassPitchClass.HasValue || stringIndex < bassStringIndex)
            {
                bassPitchClass = pitchClass;
                bassStringIndex = stringIndex;
            }
        }

        if (pitchClassSet.Count < 2)
            return string.Empty;

        int resolvedBassPitchClass = bassPitchClass ?? pitchClassSet.First();
        ChordMatch bestMatch = default;
        bool foundMatch = false;
        foreach (int candidateRoot in pitchClassSet)
        {
            if (!TryMatchChordQuality(pitchClassSet, candidateRoot, out string suffix, out int score))
                continue;

            if (!foundMatch || score > bestMatch.score || (score == bestMatch.score && candidateRoot == resolvedBassPitchClass))
            {
                bestMatch = new ChordMatch
                {
                    rootPitchClass = candidateRoot,
                    bassPitchClass = resolvedBassPitchClass,
                    suffix = suffix,
                    score = score
                };
                foundMatch = true;
            }
        }

        if (!foundMatch)
            return string.Empty;

        string label = PitchClassToChordName(bestMatch.rootPitchClass) + bestMatch.suffix;
        if (bestMatch.bassPitchClass != bestMatch.rootPitchClass && pitchClassSet.Contains(bestMatch.bassPitchClass))
            label += "/" + PitchClassToChordName(bestMatch.bassPitchClass);
        return label;
    }

    private void UpdateArpeggioGuideIndicatorStates(GameObject frame, ArpeggioGuideData guide, GuitarGameplaySnapshot snapshot, float renderSongTime)
    {
        if (frame == null || guide?.stringFrets == null)
            return;

        int maxStrings = Mathf.Min(guide.stringFrets.Length, GetRenderableStringCount());
        for (int stringIndex = 0; stringIndex < maxStrings; stringIndex++)
        {
            int fret = guide.stringFrets[stringIndex];
            if (fret < 0)
                continue;

            bool coveredByVisibleNote = IsArpeggioIndicatorCoveredByVisibleNote(guide, stringIndex, fret, snapshot, renderSongTime);
            Transform slot = frame.transform.Find($"ArpeggioSlot_{stringIndex}");
            if (slot != null)
                SetGameObjectActive(slot.gameObject, !coveredByVisibleNote);

            Transform open = frame.transform.Find($"ArpeggioOpen_{stringIndex}");
            if (open != null)
                SetGameObjectActive(open.gameObject, !coveredByVisibleNote);
        }
    }

    private bool IsArpeggioIndicatorCoveredByVisibleNote(ArpeggioGuideData guide, int stringIndex, int fret, GuitarGameplaySnapshot snapshot, float renderSongTime)
    {
        if (guide == null || snapshot?.noteStates == null)
            return false;

        float spawnLeadSeconds = GetVisibleLeadTime();
        float maxRelevantTime = guide.endTime + spawnLeadSeconds;
        float resolvedFadeTime = GetResolvedFadeTime();
        for (int i = 0; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null)
                continue;

            if (state.data.time > maxRelevantTime)
                break;

            if (state.data.stringIdx != stringIndex || state.data.fret != fret)
                continue;

            if (state.data.time < guide.startTime - 0.001f || state.data.time > guide.endTime + 0.001f)
                continue;

            float travelZ = owner.StrikeLineZ + ((state.data.time - renderSongTime) * currentVisualNoteSpeed);
            bool resolvedAtOrBeforeRenderTime = state.IsResolved && state.resolvedAt >= 0f && renderSongTime >= state.resolvedAt;
            bool keepForResult = resolvedAtOrBeforeRenderTime && renderSongTime - state.resolvedAt <= resolvedFadeTime;
            bool keepForTechnique = resolvedAtOrBeforeRenderTime && ShouldKeepTechniqueAliveAfterResolution(state.data, renderSongTime);
            bool visible = travelZ <= owner.SpawnZ && (!state.IsResolved || keepForResult || keepForTechnique || travelZ >= owner.StrikeLineZ);
            if (visible)
                return true;
        }

        return false;
    }

    private void UpdateChordFrames(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateChordFramesProfilerMarker.Auto())
        {
            float renderSongTime = GetRenderSongTime(snapshot);
            activeChordIdsThisFrame.Clear();

            foreach (var pair in chordGroups)
            {
                List<NoteData> group = pair.Value;
                if (group == null || group.Count < 2)
                    continue;

                float anchorTime = group[0].time;
                float z = owner.StrikeLineZ + ((anchorTime - renderSongTime) * currentVisualNoteSpeed);
                bool anyRecent = false;
                float resolvedFadeTime = GetResolvedFadeTime();
                for (int noteIndex = 0; noteIndex < group.Count; noteIndex++)
                {
                    NoteData note = group[noteIndex];
                    if (!noteStatesById.TryGetValue(note.id, out GameplayNoteState state))
                        continue;

                    if (state.IsResolved &&
                        state.resolvedAt >= 0f &&
                        renderSongTime >= state.resolvedAt &&
                        renderSongTime - state.resolvedAt <= resolvedFadeTime)
                    {
                        anyRecent = true;
                        break;
                    }
                }

                bool visible = z <= owner.SpawnZ && z > owner.StrikeLineZ + 0.001f;

                if (!visible && !anyRecent)
                    continue;

                activeChordIdsThisFrame.Add(pair.Key);

                int handFret = GetGroupHandFret(group);
                float leftX = GetHandWindowStartX(handFret);
                float rightX = GetHandWindowEndX(handFret, group);

                if (!chordFrames.TryGetValue(pair.Key, out GameObject frame) || frame == null)
                {
                    frame = CreateChordFrame(leftX, rightX, GetChordBoxCenterY(group), GetChordBoxHeight(group));
                    chordFrames[pair.Key] = frame;
                }

                frame.transform.position = new Vector3(frame.transform.position.x, frame.transform.position.y, z + 0.01f);
                UpdateChordFrameLabel(frame, group, rightX - leftX, GetChordBoxHeight(group));
            }

            chordFrameRemovalBuffer.Clear();
            foreach (KeyValuePair<int, GameObject> pair in chordFrames)
            {
                if (activeChordIdsThisFrame.Contains(pair.Key))
                    continue;

                chordFrameRemovalBuffer.Add(pair.Key);
            }

            for (int i = 0; i < chordFrameRemovalBuffer.Count; i++)
            {
                int key = chordFrameRemovalBuffer[i];
                if (chordFrames[key] != null)
                    Object.Destroy(chordFrames[key]);
                chordFrames.Remove(key);
            }
        }
    }

    private void UpdateFretboardLights(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateFretboardLightsProfilerMarker.Auto())
        {
            if (fretLightMats == null || fretLightRenderers == null)
                return;

            int fretLightColumns = GetFretLightColumnCount();
            int activeStringCount = GetRenderableStringCount();
            UpdateFretLightLayoutIfNeeded(fretLightColumns, activeStringCount);

            for (int i = 0; i < activeFretLightIndices.Count; i++)
            {
                int encodedIndex = activeFretLightIndices[i];
                int stringIndex = encodedIndex / fretLightColumns;
                int fretIndex = encodedIndex % fretLightColumns;
                if (stringIndex < 0 || stringIndex >= fretLightMats.GetLength(0) || fretIndex < 0 || fretIndex >= fretLightColumns)
                    continue;

                fretLightMats[stringIndex, fretIndex].SetColor("_EmissionColor", Color.black);
                if (fretLightRenderers[stringIndex, fretIndex] != null)
                    fretLightRenderers[stringIndex, fretIndex].enabled = false;
            }

            activeFretLightIndices.Clear();

            HashSet<int> pitchesToLight = snapshot?.latestDetectedPitches;

            if (pitchesToLight != null && pitchesToLight.Count > 0)
            {
                for (int s = 0; s < activeStringCount; s++)
                {
                    Color stringColor = owner.GetStringColor(s);
                    int stringBasePitch = owner.GetStringBasePitch(s);
                    for (int f = 0; f < fretLightColumns; f++)
                    {
                        int exactFretPitch = stringBasePitch + f;
                        int genericFretPitch = exactFretPitch % 12;
                        if (!pitchesToLight.Contains(exactFretPitch) && !pitchesToLight.Contains(genericFretPitch))
                            continue;

                        ApplyFretLightState(s, f, stringColor, stringColor * 8f, fretLightColumns);
                    }
                }
            }

            ApplyResolvedFretLightFeedback(snapshot, fretLightColumns, activeStringCount);
        }
    }

    private void ApplyResolvedFretLightFeedback(GuitarGameplaySnapshot snapshot, int fretLightColumns, int activeStringCount)
    {
        if (snapshot?.noteStates == null || fretLightColumns <= 0 || activeStringCount <= 0)
            return;

        float renderSongTime = GetRenderSongTime(snapshot);
        GetResolvedFeedbackScanWindow(renderSongTime, out float earliestNoteTime, out float latestNoteTime);

        int startIndex = FindFirstNoteStateIndexAtOrAfter(snapshot.noteStates, earliestNoteTime);
        for (int i = startIndex; i < snapshot.noteStates.Count; i++)
        {
            GameplayNoteState state = snapshot.noteStates[i];
            if (state == null)
                continue;

            if (state.data.time > latestNoteTime)
                break;
            if (!IsResolvedFretFeedbackEnabled(state))
                continue;
            if (!TryGetResolvedFeedbackPulse(state, renderSongTime, out float pulse))
                continue;

            int stringIndex = state.data.stringIdx;
            if (stringIndex < 0 || stringIndex >= activeStringCount)
                continue;

            int fretIndex = GetFeedbackFretLightIndex(state.data, fretLightColumns);
            Color baseColor = state.IsMissed ? HighwayMissFretLightColor : HighwayHitFretLightColor;
            float emissionMultiplier = state.IsMissed ? HighwayMissFretLightEmissionMultiplier : HighwayHitFretLightEmissionMultiplier;
            ApplyFretLightState(stringIndex, fretIndex, baseColor, baseColor * (emissionMultiplier * pulse), fretLightColumns);
        }
    }

    private void ApplyFretLightState(int stringIndex, int fretIndex, Color baseColor, Color emissionColor, int fretLightColumns)
    {
        if (stringIndex < 0 || fretIndex < 0 ||
            stringIndex >= fretLightMats.GetLength(0) ||
            fretIndex >= fretLightColumns ||
            fretIndex >= fretLightMats.GetLength(1))
        {
            return;
        }

        Material mat = fretLightMats[stringIndex, fretIndex];
        Renderer renderer = fretLightRenderers[stringIndex, fretIndex];
        if (mat == null || renderer == null)
            return;

        Color appliedBase = new Color(baseColor.r, baseColor.g, baseColor.b, 0.92f);
        mat.color = appliedBase;
        mat.SetColor("_Color", appliedBase);
        mat.SetColor("_BaseColor", appliedBase);
        mat.EnableKeyword("_EMISSION");
        mat.SetColor("_EmissionColor", emissionColor);
        renderer.enabled = true;
        activeFretLightIndices.Add((stringIndex * fretLightColumns) + fretIndex);
    }

    private void GetResolvedFeedbackScanWindow(float renderSongTime, out float earliestNoteTime, out float latestNoteTime)
    {
        float earlyWindow = owner != null ? Mathf.Max(0f, owner.hitWindowEarly) : 0.3f;
        float lateWindow = owner != null ? Mathf.Max(0f, owner.hitWindowLate) : 0.5f;
        float graceWindow = owner != null ? Mathf.Max(0f, owner.judgmentGrace) : 0.75f;
        earliestNoteTime = renderSongTime - lateWindow - graceWindow - HighwayResolvedFretFeedbackDurationSeconds - 0.12f;
        latestNoteTime = renderSongTime + earlyWindow + 0.12f;
    }

    private static bool TryGetResolvedFeedbackPulse(GameplayNoteState state, float renderSongTime, out float pulse)
    {
        pulse = 0f;
        if (state == null || !state.IsResolved || state.resolvedAt < 0f)
            return false;

        float age = renderSongTime - state.resolvedAt;
        if (age < 0f || age > HighwayResolvedFretFeedbackDurationSeconds)
            return false;

        float attack = Mathf.Clamp01(age / HighwayResolvedFretFeedbackAttackSeconds);
        float release = 1f - Mathf.Clamp01(age / HighwayResolvedFretFeedbackDurationSeconds);
        pulse = attack * release * release;
        return pulse > 0.001f;
    }

    private bool IsResolvedNoteFeedbackBoxEnabled(GameplayNoteState state)
    {
        if (state == null || !state.IsResolved || owner == null)
            return true;

        if (state.IsHit)
            return owner.highwayShowHitNoteFeedbackBox;

        if (state.IsMissed)
            return owner.highwayShowMissNoteFeedbackBox;

        return true;
    }

    private bool IsResolvedFretFeedbackEnabled(GameplayNoteState state)
    {
        if (state == null || !state.IsResolved || owner == null)
            return true;

        if (state.IsHit)
            return owner.highwayShowHitFretFeedback;

        if (state.IsMissed)
            return owner.highwayShowMissFretFeedback;

        return true;
    }

    private bool IsResolvedFretLineFeedbackEnabled(GameplayNoteState state)
    {
        if (state == null || !state.IsResolved || owner == null)
            return true;

        if (state.IsHit)
            return owner.highwayShowHitFretLineFlashFeedback;

        if (state.IsMissed)
            return owner.highwayShowMissFretLineFlashFeedback;

        return true;
    }

    private bool IsFretLineFeedbackEnabled(bool isMiss, float feedbackPulse)
    {
        if (owner == null || feedbackPulse <= 0.001f)
            return false;

        return isMiss
            ? owner.highwayShowMissFretLineFlashFeedback
            : owner.highwayShowHitFretLineFlashFeedback;
    }

    private static void ApplyMaxFeedback(float[] buffer, int index, float value)
    {
        if (buffer == null || index < 0 || index >= buffer.Length)
            return;

        if (value > buffer[index])
            buffer[index] = value;
    }

    private static int GetFeedbackFretLightIndex(NoteData data, int fretLightColumns)
    {
        if (fretLightColumns <= 0)
            return 0;

        if (data.fret <= 0)
            return 0;

        return Mathf.Clamp(data.fret, 0, fretLightColumns - 1);
    }

    private static int FindFirstNoteStateIndexAtOrAfter(IReadOnlyList<GameplayNoteState> states, float thresholdTime)
    {
        if (states == null || states.Count == 0)
            return 0;

        int low = 0;
        int high = states.Count;
        while (low < high)
        {
            int mid = low + ((high - low) / 2);
            GameplayNoteState state = states[mid];
            float noteTime = state != null ? state.data.time : float.PositiveInfinity;
            if (noteTime < thresholdTime)
                low = mid + 1;
            else
                high = mid;
        }

        return low;
    }

    private void UpdateFretLightLayoutIfNeeded(int fretLightColumns, int activeStringCount)
    {
        if (fretLightRenderers == null)
            return;

        float openAnchorFret = owner.defaultOpenAnchorFret;
        float strikeLineZ = owner.StrikeLineZ;
        float fretSpacing = owner.FretSpacing;
        bool layoutDirty =
            lastFretLightLayoutStringCount != activeStringCount ||
            lastFretLightLayoutColumnCount != fretLightColumns ||
            !Mathf.Approximately(lastFretLightLayoutOpenAnchorFret, openAnchorFret) ||
            !Mathf.Approximately(lastFretLightLayoutStrikeLineZ, strikeLineZ) ||
            !Mathf.Approximately(lastFretLightLayoutFretSpacing, fretSpacing);

        if (!layoutDirty)
            return;

        for (int s = 0; s < fretLightRenderers.GetLength(0); s++)
        {
            for (int f = 0; f < fretLightColumns; f++)
            {
                if (fretLightRenderers[s, f] == null)
                    continue;

                float xPos = f == 0 ? GetNoteX(Mathf.RoundToInt(openAnchorFret)) : GetNoteX(f);
                Vector3 position = fretLightRenderers[s, f].transform.position;
                position.x = xPos;
                position.y = GetStringY(s);
                position.z = strikeLineZ;
                fretLightRenderers[s, f].transform.position = position;
            }
        }

        lastFretLightLayoutStringCount = activeStringCount;
        lastFretLightLayoutColumnCount = fretLightColumns;
        lastFretLightLayoutOpenAnchorFret = openAnchorFret;
        lastFretLightLayoutStrikeLineZ = strikeLineZ;
        lastFretLightLayoutFretSpacing = fretSpacing;
    }

    private void UpdateSectionCamera(GuitarGameplaySnapshot snapshot)
    {
        using (UpdateSectionCameraProfilerMarker.Auto())
        {
            float renderSongTime = GetRenderSongTime(snapshot);
            if (renderSongTime >= urgentCameraHoldUntil)
            {
                urgentCameraHoldUntil = float.NegativeInfinity;
                urgentCameraHeldTargetX = cameraTargetX;
                urgentCameraHeldTargetFOV = cameraTargetFOV;
            }

            float previewWindow = Mathf.Max(1.6f, owner.lookaheadWindow * 1.75f);
            float urgentWindow = Mathf.Clamp(previewWindow * 0.18f, 0.55f, 0.95f);
            float urgentClusterWindow = Mathf.Clamp(previewWindow * 0.34f, 0.95f, 1.55f);
            float weightedCenterSum = 0f;
            float weightSum = 0f;
            float requiredMin = 0f;
            float requiredMax = 0f;
            float urgentRequiredMin = 0f;
            float urgentRequiredMax = 0f;
            float urgentClusterMin = 0f;
            float urgentClusterMax = 0f;
            bool foundFraming = false;
            bool foundUrgentFraming = false;
            bool foundUrgentCluster = false;
            float mostUrgentTimeUntil = float.PositiveInfinity;
            float urgentClusterLastTimeUntil = float.NegativeInfinity;

            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null)
                    continue;

                float timeUntilNote = state.data.time - renderSongTime;
                if (timeUntilNote > previewWindow)
                    break;

                if (state.IsResolved)
                    continue;

                if (timeUntilNote < -0.1f || timeUntilNote > previewWindow)
                    continue;

                GetFramingRange(state.data, out float minX, out float maxX);
                float noteCenter = (minX + maxX) * 0.5f;
                float noteWeight = Mathf.Lerp(1.15f, 0.75f, Mathf.Clamp01(timeUntilNote / previewWindow));

                weightedCenterSum += noteCenter * noteWeight;
                weightSum += noteWeight;

                if (timeUntilNote <= urgentWindow)
                {
                    if (!foundUrgentFraming)
                    {
                        urgentRequiredMin = minX;
                        urgentRequiredMax = maxX;
                        foundUrgentFraming = true;
                    }
                    else
                    {
                        urgentRequiredMin = Mathf.Min(urgentRequiredMin, minX);
                        urgentRequiredMax = Mathf.Max(urgentRequiredMax, maxX);
                    }

                    mostUrgentTimeUntil = Mathf.Min(mostUrgentTimeUntil, timeUntilNote);
                }

                if (timeUntilNote <= urgentClusterWindow)
                {
                    if (!foundUrgentCluster)
                    {
                        urgentClusterMin = minX;
                        urgentClusterMax = maxX;
                        foundUrgentCluster = true;
                    }
                    else
                    {
                        urgentClusterMin = Mathf.Min(urgentClusterMin, minX);
                        urgentClusterMax = Mathf.Max(urgentClusterMax, maxX);
                    }

                    urgentClusterLastTimeUntil = Mathf.Max(urgentClusterLastTimeUntil, timeUntilNote);
                }

                if (!foundFraming)
                {
                    requiredMin = minX;
                    requiredMax = maxX;
                    foundFraming = true;
                }
                else
                {
                    requiredMin = Mathf.Min(requiredMin, minX);
                    requiredMax = Mathf.Max(requiredMax, maxX);
                }
            }

            if (foundFraming && weightSum > 0.0001f)
            {
                float desiredTargetX = weightedCenterSum / weightSum;
                float horizontalPadding = Mathf.Max(owner.FretSpacing * 0.8f, 0.8f);
                float halfSpan = Mathf.Max(
                    desiredTargetX - requiredMin,
                    requiredMax - desiredTargetX) + horizontalPadding;
                float desiredSpread = (halfSpan * 2f) / Mathf.Max(0.01f, owner.FretSpacing);
                const float NormalMaxFov = 90f;
                const float EmergencyMaxFov = 96f;
                float desiredFov = Mathf.Clamp(50f + (desiredSpread * 3.0f), 50f, NormalMaxFov);

                if (foundUrgentFraming && desiredFov >= NormalMaxFov - 0.5f)
                {
                    float guardedMin = foundUrgentCluster ? urgentClusterMin : urgentRequiredMin;
                    float guardedMax = foundUrgentCluster ? urgentClusterMax : urgentRequiredMax;
                    float urgentBreathingRoom = Mathf.Max(owner.FretSpacing * 0.3f, 0.45f);
                    guardedMin -= urgentBreathingRoom;
                    guardedMax += urgentBreathingRoom;
                    float emergencyHalfSpan = Mathf.Max(
                        desiredTargetX - guardedMin,
                        guardedMax - desiredTargetX) + horizontalPadding;
                    float emergencySpread = (emergencyHalfSpan * 2f) / Mathf.Max(0.01f, owner.FretSpacing);
                    float emergencyDesiredFov = Mathf.Clamp(50f + (emergencySpread * 3.0f), 50f, EmergencyMaxFov);
                    desiredFov = Mathf.Max(desiredFov, emergencyDesiredFov);

                    float maxVisibleHalfSpan = (((desiredFov - 50f) / 3f) * owner.FretSpacing) * 0.5f;
                    float visibilityMargin = Mathf.Max(owner.FretSpacing * 0.9f, 1.1f);
                    float safeHalfSpan = Mathf.Max(0.01f, maxVisibleHalfSpan - visibilityMargin);
                    float safeLeft = desiredTargetX - safeHalfSpan;
                    float safeRight = desiredTargetX + safeHalfSpan;

                    if (guardedMin < safeLeft || guardedMax > safeRight)
                    {
                        float feasibleMinCenter = guardedMax - safeHalfSpan;
                        float feasibleMaxCenter = guardedMin + safeHalfSpan;
                        float guardedCenter =
                            feasibleMinCenter <= feasibleMaxCenter
                                ? Mathf.Clamp(desiredTargetX, feasibleMinCenter, feasibleMaxCenter)
                                : (guardedMin + guardedMax) * 0.5f;

                        float urgency = 1f - Mathf.Clamp01(mostUrgentTimeUntil / urgentWindow);
                        urgency = urgency * urgency * (3f - (2f * urgency));
                        float guardBlend = Mathf.Lerp(0.22f, 1f, urgency);
                        desiredTargetX = Mathf.Lerp(desiredTargetX, guardedCenter, guardBlend);

                        halfSpan = Mathf.Max(
                            desiredTargetX - requiredMin,
                            requiredMax - desiredTargetX) + horizontalPadding;
                        desiredSpread = (halfSpan * 2f) / Mathf.Max(0.01f, owner.FretSpacing);
                        desiredFov = Mathf.Clamp(
                            Mathf.Max(50f + (desiredSpread * 3.0f), emergencyDesiredFov),
                            50f,
                            EmergencyMaxFov);

                        urgentCameraHeldTargetX = guardedCenter;
                        urgentCameraHeldTargetFOV = Mathf.Max(urgentCameraHeldTargetFOV, desiredFov);
                        urgentCameraHoldUntil = Mathf.Max(
                            urgentCameraHoldUntil,
                            renderSongTime + Mathf.Max(0.18f, urgentClusterLastTimeUntil + 0.14f));
                    }
                }

                if (renderSongTime < urgentCameraHoldUntil)
                {
                    float holdRemaining = urgentCameraHoldUntil - renderSongTime;
                    float holdBlend = Mathf.Clamp01(holdRemaining / Mathf.Max(0.001f, urgentClusterWindow * 0.9f));
                    holdBlend = holdBlend * holdBlend * (3f - (2f * holdBlend));
                    desiredFov = Mathf.Max(desiredFov, Mathf.Lerp(desiredFov, urgentCameraHeldTargetFOV, holdBlend));
                    desiredTargetX = Mathf.Lerp(desiredTargetX, urgentCameraHeldTargetX, holdBlend * 0.45f);
                }

                float targetBlend = 1f - Mathf.Exp(-Time.deltaTime * 1.35f);
                cameraTargetX = Mathf.Lerp(cameraTargetX, desiredTargetX, targetBlend);
                cameraTargetFOV = Mathf.Lerp(cameraTargetFOV, desiredFov, targetBlend * 0.75f);
            }

            float smoothedX = Mathf.SmoothDamp(mainCamera.transform.position.x, cameraTargetX, ref cameraXVelocity, 0.46f, Mathf.Infinity, Time.deltaTime);
            mainCamera.transform.position = new Vector3(smoothedX, owner.highwayCameraY, owner.highwayCameraZ);
            mainCamera.fieldOfView = Mathf.SmoothDamp(mainCamera.fieldOfView, cameraTargetFOV, ref cameraFovVelocity, 0.58f, Mathf.Infinity, Time.deltaTime);
        }
    }

    private float GetRenderSongTime(GuitarGameplaySnapshot snapshot)
    {
        using (GetRenderSongTimeProfilerMarker.Auto())
        {
            if (snapshot == null)
                return 0f;

            if (renderSongTimeCacheValid && ReferenceEquals(renderSongTimeCacheSnapshot, snapshot))
                return renderSongTimeCacheValue;

            float renderSongTime = snapshot.songTime;
            float visibleWindow = GetVisibleLeadTime();

            if (snapshot.noteStates == null || snapshot.noteStates.Count == 0)
            {
                CacheRenderSongTime(snapshot, renderSongTime);
                return renderSongTime;
            }

            bool shouldPreviewUpcoming = snapshot.showMainMenu || snapshot.showSongSelection || snapshot.showTrackSelection;
            if (!shouldPreviewUpcoming)
            {
                CacheRenderSongTime(snapshot, renderSongTime);
                return renderSongTime;
            }

            float maxVisibleTime = renderSongTime + visibleWindow;
            GameplayNoteState nextPending = null;
            for (int i = 0; i < snapshot.noteStates.Count; i++)
            {
                GameplayNoteState state = snapshot.noteStates[i];
                if (state == null || state.IsResolved)
                    continue;

                float noteTime = state.data.time;
                if (noteTime < renderSongTime)
                    continue;

                if (noteTime <= maxVisibleTime)
                {
                    CacheRenderSongTime(snapshot, renderSongTime);
                    return renderSongTime;
                }

                nextPending = state;
                break;
            }

            if (nextPending == null)
            {
                CacheRenderSongTime(snapshot, renderSongTime);
                return renderSongTime;
            }

            float previewRenderTime = Mathf.Max(0f, nextPending.data.time - (visibleWindow * 0.85f));
            CacheRenderSongTime(snapshot, previewRenderTime);
            return previewRenderTime;
        }
    }

    private void CacheRenderSongTime(GuitarGameplaySnapshot snapshot, float value)
    {
        if (!ReferenceEquals(renderSongTimeCacheSnapshot, snapshot))
            return;

        renderSongTimeCacheValue = value;
        renderSongTimeCacheValid = true;
    }

    private void EnsureStringHasIncomingNotesBuffer(int activeStringCount)
    {
        if (stringHasIncomingNotesBuffer.Length < activeStringCount)
            stringHasIncomingNotesBuffer = new bool[activeStringCount];
    }

    private float GetVisibleLeadTime()
    {
        return Mathf.Max(0.01f, (owner.SpawnZ - owner.StrikeLineZ) / Mathf.Max(0.01f, currentVisualNoteSpeed));
    }

    private float GetVisualNoteSpeed(GuitarGameplaySnapshot snapshot)
    {
        float spacingScale = 1f;
        if (snapshot != null)
            spacingScale = Mathf.Clamp(snapshot.tabSpeedOffsetPercent / 100f, 0.5f, 1.5f);

        return Mathf.Max(0.01f, owner.noteSpeed * spacingScale);
    }

    private List<NoteData> GetChordGroup(NoteData data)
    {
        if (data.chordId >= 0 && chordGroups.TryGetValue(data.chordId, out List<NoteData> group))
            return group;

        return new List<NoteData> { data };
    }

    private int GetGroupHandFret(List<NoteData> group)
    {
        if (group == null || group.Count == 0)
            return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);

        List<NoteData> fretted = group.Where(n => n.fret > 0).ToList();
        if (fretted.Count > 0)
            return Mathf.Clamp(fretted.Min(n => n.fret), 1, owner.TotalFrets - 3);

        float groupTime = group[0].time;
        List<NoteData> futureFretted = chartById.Values.Where(n => n.time > groupTime + 0.0001f && n.fret > 0).OrderBy(n => n.time).ToList();
        if (futureFretted.Count > 0)
            return Mathf.Clamp(futureFretted[0].fret, 1, owner.TotalFrets - 3);

        return Mathf.Clamp(Mathf.RoundToInt(owner.defaultOpenAnchorFret), 1, owner.TotalFrets - 3);
    }

    private float GetHandWindowStartX(int handFret)
    {
        return GetNoteX(handFret - 1) - (owner.FretSpacing * 0.2f);
    }

    private float GetHandWindowEndX(int handFret, List<NoteData> group = null)
    {
        int furthestFret = handFret + 3;
        if (group != null)
        {
            int highestGroupFret = group.Where(n => n.fret > 0).Select(n => n.fret).DefaultIfEmpty(furthestFret).Max();
            furthestFret = Mathf.Max(furthestFret, highestGroupFret);
        }

        return GetNoteX(furthestFret) + (owner.FretSpacing * 0.2f);
    }

    private float GetGroupAnchorX(List<NoteData> group)
    {
        int handFret = GetGroupHandFret(group);
        return (GetHandWindowStartX(handFret) + GetHandWindowEndX(handFret, group)) * 0.5f;
    }

    private float GetVisualNoteX(NoteData data)
    {
        List<NoteData> group = GetChordGroup(data);
        if (data.fret == 0)
            return GetGroupAnchorX(group);

        return GetNoteX(data.fret);
    }

    private float GetSegmentFretVisualX(NoteData anchorData, int segmentFret)
    {
        if (segmentFret <= 0)
            return GetVisualNoteX(anchorData);

        return GetNoteX(segmentFret);
    }

    private void GetFramingRange(NoteData data, out float minX, out float maxX)
    {
        List<NoteData> group = GetChordGroup(data);
        bool isGrouped = group.Count > 1;

        if (isGrouped || data.fret == 0)
        {
            int handFret = GetGroupHandFret(group);
            minX = GetHandWindowStartX(handFret);
            maxX = GetHandWindowEndX(handFret, group);
            return;
        }

        float x = GetNoteX(data.fret);
        minX = x;
        maxX = x;
    }

    private float GetChordBoxHeight(List<NoteData> group)
    {
        int renderableStringCount = GetRenderableStringCount();
        if (renderableStringCount <= 0)
            return GetStringLaneSpacing();

        if (renderableStringCount == 1)
            return GetStringLaneSpacing();

        float minY = GetStringY(0);
        float maxY = GetStringY(renderableStringCount - 1);
        return Mathf.Max(1f, Mathf.Abs(maxY - minY) + owner.chordFrameVerticalPadding);
    }

    private float GetChordBoxCenterY(List<NoteData> group)
    {
        int renderableStringCount = GetRenderableStringCount();
        if (renderableStringCount <= 0)
            return 0f;

        return (GetStringY(0) + GetStringY(renderableStringCount - 1)) * 0.5f;
    }

    private Vector3 GetSingleFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.56f,
            0.44f * GetNoteHeightScale(),
            Mathf.Max(0.48f, owner.FretSpacing * 0.28f));
    }

    private Vector3 GetGroupedFrettedNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 0.54f,
            0.4f * GetNoteHeightScale(),
            Mathf.Max(0.44f, owner.FretSpacing * 0.26f));
    }

    private Vector3 GetSingleOpenNoteScale()
    {
        return new Vector3(
            owner.FretSpacing * 3.6f,
            GetScaledOpenHeight(),
            GetScaledOpenDepth());
    }

    private float GetScaledOpenHeight()
    {
        return 0.2f * GetNoteHeightScale();
    }

    private float GetNoteHeightScale()
    {
        return Mathf.Max(0.2f, owner.highwayNoteHeightScale);
    }

    private float GetScaledOpenDepth()
    {
        return Mathf.Max(0.36f, owner.FretSpacing * 0.22f);
    }

    private Vector3 GetMarkerScale()
    {
        float diameter = Mathf.Max(0.38f, owner.FretSpacing * 0.16f);
        return new Vector3(diameter, diameter, Mathf.Max(0.16f, diameter * 0.35f));
    }

    private static float GetVisualNoteStrikeOffset(HighwayNoteView view)
    {
        return Mathf.Max(0f, view.baseScale.z * 0.5f);
    }

    private static float GetVisualNoteStrikeOffset(Vector3 scale)
    {
        return Mathf.Max(0f, scale.z * 0.5f);
    }

    private static bool ApproximatelyVector3(Vector3 a, Vector3 b)
    {
        return Mathf.Approximately(a.x, b.x) &&
               Mathf.Approximately(a.y, b.y) &&
               Mathf.Approximately(a.z, b.z);
    }

    private float GetResolvedFadeTime()
    {
        return Mathf.Max(0.45f, owner.highwayResolvedHoldTime);
    }

    private static void SetGameObjectActive(GameObject target, bool active)
    {
        if (target != null && target.activeSelf != active)
            target.SetActive(active);
    }

    private bool ShouldKeepTechniqueAliveAfterResolution(NoteData data, float songTime)
    {
        if (!HasPersistentTechniqueVisual(data))
            return false;

        return songTime <= GetTechniqueVisualEndTime(data) + 0.02f;
    }

    private bool HasPersistentTechniqueVisual(NoteData data)
    {
        return HasTechniqueSegments(data) || HasBendRibbon(data) || data.slideTargetFret >= 0 || HasNoteSustainRibbon(data);
    }

    private float GetTechniqueVisualEndTime(NoteData data)
    {
        float endTime = data.time;
        if (HasTechniqueSegments(data))
            endTime = Mathf.Max(endTime, data.time + data.techniqueSegments.Max(segment => segment.endOffset));
        if (HasBendRibbon(data))
            endTime = Mathf.Max(endTime, data.time + Mathf.Max(0.14f, data.duration));
        if (HasNoteSustainRibbon(data))
            endTime = Mathf.Max(endTime, data.time + Mathf.Max(GuitarTechniqueVisualThresholds.SustainSeconds, data.duration));

        if (data.slideTargetFret >= 0 &&
            slideDestinationBySourceId.TryGetValue(data.id, out int targetId) &&
            chartById.TryGetValue(targetId, out NoteData slideTarget))
        {
            endTime = Mathf.Max(endTime, slideTarget.time);
        }

        return endTime;
    }

    private GameObject CreateChordFrame(float leftX, float rightX, float centerY, float height, Color? frameColorOverride = null, float frameGlowIntensity = 1.6f)
    {
        GameObject parent = new GameObject("ChordFrame");
        parent.transform.SetParent(gameplayRoot.transform, false);
        float centerX = (leftX + rightX) * 0.5f;
        float width = Mathf.Max(0.5f, rightX - leftX);
        parent.transform.position = new Vector3(centerX, centerY, owner.SpawnZ);

        Material frameMat = owner.CreateSharedGlowMaterial(frameColorOverride ?? new Color(0.55f, 0.95f, 1f), frameGlowIntensity);
        float halfW = width * 0.5f;
        float halfH = height * 0.5f;

        CreateChordFrameBackground(parent.transform, width, height);
        CreateFramePiece(parent.transform, new Vector3(0f, halfH, 0f), new Vector3(width, owner.chordFrameThickness, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(0f, -halfH, 0f), new Vector3(width, owner.chordFrameThickness, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(-halfW, 0f, 0f), new Vector3(owner.chordFrameThickness, height, 0.08f), frameMat);
        CreateFramePiece(parent.transform, new Vector3(halfW, 0f, 0f), new Vector3(owner.chordFrameThickness, height, 0.08f), frameMat);
        return parent;
    }

    private void UpdateChordFrameLabel(GameObject frame, List<NoteData> group, float width, float height)
    {
        if (frame == null)
            return;

        UpdateChordFrameLabel(frame, GetChordDisplayName(group), width, height);
    }

    private void UpdateChordFrameLabel(GameObject frame, string chordLabel, float width, float height)
    {
        if (frame == null)
            return;

        Transform labelTransform = frame.transform.Find("ChordNameLabel");
        TextMeshPro label = labelTransform != null ? labelTransform.GetComponent<TextMeshPro>() : null;

        if (string.IsNullOrWhiteSpace(chordLabel))
        {
            if (label != null)
                SetGameObjectActive(label.gameObject, false);
            return;
        }

        if (label == null)
            label = CreateChordFrameLabel(frame.transform);

        label.text = chordLabel.Trim();
        label.alignment = TextAlignmentOptions.TopLeft;
        label.rectTransform.pivot = new Vector2(0f, 1f);
        label.transform.localPosition = new Vector3(
            (-width * 0.5f) + ChordNameLabelLeftPadding,
            (height * 0.5f) + ChordNameLabelVerticalPadding,
            -0.02f);
        label.transform.localRotation = Quaternion.identity;
        label.transform.localScale = Vector3.one;
        SetGameObjectActive(label.gameObject, true);
    }

    private TextMeshPro CreateChordFrameLabel(Transform parent)
    {
        GameObject textObj = new GameObject("ChordNameLabel");
        textObj.transform.SetParent(parent, false);

        TextMeshPro tm = textObj.AddComponent<TextMeshPro>();
        tm.fontSize = 15.5f;
        tm.fontStyle = FontStyles.Bold;
        tm.alignment = TextAlignmentOptions.TopLeft;
        tm.overflowMode = TextOverflowModes.Overflow;
        tm.enableWordWrapping = false;
        tm.characterSpacing = 0.35f;
        tm.lineSpacing = 0f;
        tm.rectTransform.pivot = new Vector2(0f, 1f);
        tm.rectTransform.sizeDelta = new Vector2(48f, 14f);
        tm.color = new Color(0.96f, 0.98f, 1f, 0.98f);
        tm.sortingOrder = 265;

        if (tm.fontSharedMaterial != null)
            tm.fontMaterial = new Material(tm.fontSharedMaterial);

        ConfigureChordFrameLabelMaterial(tm);
        return tm;
    }

    private static void ConfigureChordFrameLabelMaterial(TextMeshPro label)
    {
        if (label == null || label.fontMaterial == null)
            return;

        Material fontMat = label.fontMaterial;
        fontMat.SetColor("_FaceColor", new Color(0.96f, 0.98f, 1f, 0.98f));
        if (fontMat.HasProperty("_OutlineWidth"))
            fontMat.SetFloat("_OutlineWidth", 0.18f);
        if (fontMat.HasProperty("_OutlineColor"))
            fontMat.SetColor("_OutlineColor", new Color(0.02f, 0.06f, 0.10f, 0.92f));
        if (fontMat.HasProperty("_GlowColor"))
        {
            fontMat.SetFloat("_GlowPower", 0.68f);
            fontMat.SetFloat("_GlowInner", 0.035f);
            fontMat.SetFloat("_GlowOuter", 0.28f);
            fontMat.SetColor("_GlowColor", new Color(0.48f, 0.88f, 1f, 0.9f));
        }

        fontMat.renderQueue = (int)RenderQueue.Transparent + 155;
        if (fontMat.HasProperty("_ZWrite"))
            fontMat.SetFloat("_ZWrite", 0f);
        if (fontMat.HasProperty("_CullMode"))
            fontMat.SetFloat("_CullMode", 0f);
        if (fontMat.HasProperty("_ZTestMode"))
            fontMat.SetFloat("_ZTestMode", (float)CompareFunction.Always);
        else if (fontMat.HasProperty("_ZTest"))
            fontMat.SetFloat("_ZTest", (float)CompareFunction.Always);
    }

    private string GetChordDisplayName(List<NoteData> group)
    {
        if (group == null || group.Count < 2)
            return string.Empty;

        for (int i = 0; i < group.Count; i++)
        {
            string chordName = group[i].chordName;
            if (!string.IsNullOrWhiteSpace(chordName))
                return chordName.Trim();
        }

        return DeriveChordDisplayName(group);
    }

    private string DeriveChordDisplayName(List<NoteData> group)
    {
        if (group == null || group.Count < 2)
            return string.Empty;

        HashSet<int> pitchClassSet = new HashSet<int>();
        int? bassPitchClass = null;
        int bassStringIndex = int.MaxValue;
        for (int i = 0; i < group.Count; i++)
        {
            NoteData note = group[i];
            if (!TryParsePitchClass(note.note, out int pitchClass))
                continue;

            pitchClassSet.Add(pitchClass);
            if (!bassPitchClass.HasValue || note.stringIdx < bassStringIndex)
            {
                bassStringIndex = note.stringIdx;
                bassPitchClass = pitchClass;
            }
        }

        if (pitchClassSet.Count < 2)
            return string.Empty;

        int resolvedBassPitchClass = bassPitchClass ?? pitchClassSet.First();
        ChordMatch bestMatch = default;
        bool foundMatch = false;

        foreach (int candidateRoot in pitchClassSet)
        {
            if (!TryMatchChordQuality(pitchClassSet, candidateRoot, out string suffix, out int score))
                continue;

            if (!foundMatch || score > bestMatch.score || (score == bestMatch.score && candidateRoot == resolvedBassPitchClass))
            {
                bestMatch = new ChordMatch
                {
                    rootPitchClass = candidateRoot,
                    bassPitchClass = resolvedBassPitchClass,
                    suffix = suffix,
                    score = score
                };
                foundMatch = true;
            }
        }

        if (!foundMatch)
            return string.Empty;

        string label = PitchClassToChordName(bestMatch.rootPitchClass) + bestMatch.suffix;
        if (bestMatch.bassPitchClass != bestMatch.rootPitchClass && pitchClassSet.Contains(bestMatch.bassPitchClass))
            label += "/" + PitchClassToChordName(bestMatch.bassPitchClass);
        return label;
    }

    private static bool TryParsePitchClass(string noteName, out int pitchClass)
    {
        pitchClass = 0;
        if (string.IsNullOrWhiteSpace(noteName))
            return false;

        string normalized = noteName.Trim().ToUpperInvariant();
        switch (normalized)
        {
            case "C": pitchClass = 0; return true;
            case "C#":
            case "DB": pitchClass = 1; return true;
            case "D": pitchClass = 2; return true;
            case "D#":
            case "EB": pitchClass = 3; return true;
            case "E":
            case "FB": pitchClass = 4; return true;
            case "F":
            case "E#": pitchClass = 5; return true;
            case "F#":
            case "GB": pitchClass = 6; return true;
            case "G": pitchClass = 7; return true;
            case "G#":
            case "AB": pitchClass = 8; return true;
            case "A": pitchClass = 9; return true;
            case "A#":
            case "BB": pitchClass = 10; return true;
            case "B":
            case "CB": pitchClass = 11; return true;
            default: return false;
        }
    }

    private static bool TryMatchChordQuality(HashSet<int> pitchClasses, int rootPitchClass, out string suffix, out int score)
    {
        suffix = string.Empty;
        score = int.MinValue;
        if (pitchClasses == null || pitchClasses.Count == 0)
            return false;

        HashSet<int> intervals = new HashSet<int>();
        foreach (int pitchClass in pitchClasses)
            intervals.Add(Mod12(pitchClass - rootPitchClass));

        if (!intervals.Contains(0))
            return false;

        if (MatchesExact(intervals, 0, 4, 7, 11)) { suffix = "maj7"; score = 160; return true; }
        if (MatchesExact(intervals, 0, 4, 7, 10)) { suffix = "7"; score = 155; return true; }
        if (MatchesExact(intervals, 0, 3, 7, 10)) { suffix = "m7"; score = 150; return true; }
        if (MatchesExact(intervals, 0, 3, 7, 11)) { suffix = "m(maj7)"; score = 148; return true; }
        if (MatchesExact(intervals, 0, 3, 6, 10)) { suffix = "m7b5"; score = 146; return true; }
        if (MatchesExact(intervals, 0, 3, 6, 9)) { suffix = "dim7"; score = 144; return true; }
        if (MatchesExact(intervals, 0, 4, 7, 9)) { suffix = "6"; score = 140; return true; }
        if (MatchesExact(intervals, 0, 3, 7, 9)) { suffix = "m6"; score = 138; return true; }
        if (MatchesExact(intervals, 0, 2, 4, 7)) { suffix = "add9"; score = 132; return true; }
        if (MatchesExact(intervals, 0, 2, 3, 7)) { suffix = "m(add9)"; score = 130; return true; }
        if (MatchesExact(intervals, 0, 4, 8)) { suffix = "aug"; score = 126; return true; }
        if (MatchesExact(intervals, 0, 3, 6)) { suffix = "dim"; score = 124; return true; }
        if (MatchesExact(intervals, 0, 5, 7)) { suffix = "sus4"; score = 122; return true; }
        if (MatchesExact(intervals, 0, 2, 7)) { suffix = "sus2"; score = 120; return true; }
        if (MatchesExact(intervals, 0, 4, 7)) { suffix = string.Empty; score = 118; return true; }
        if (MatchesExact(intervals, 0, 3, 7)) { suffix = "m"; score = 116; return true; }
        if (MatchesExact(intervals, 0, 7)) { suffix = "5"; score = 104; return true; }
        if (MatchesExact(intervals, 0, 3)) { suffix = "m"; score = 86; return true; }
        if (MatchesExact(intervals, 0, 4)) { suffix = string.Empty; score = 84; return true; }
        if (MatchesExact(intervals, 0, 5)) { suffix = "sus4"; score = 82; return true; }
        if (MatchesExact(intervals, 0, 2)) { suffix = "sus2"; score = 80; return true; }

        if (intervals.Contains(4) && intervals.Contains(7))
        {
            suffix = intervals.Contains(11) ? "maj7" : intervals.Contains(10) ? "7" : string.Empty;
            score = 96;
            return true;
        }

        if (intervals.Contains(3) && intervals.Contains(7))
        {
            suffix = intervals.Contains(10) ? "m7" : "m";
            score = 94;
            return true;
        }

        if (intervals.Contains(5) && intervals.Contains(7))
        {
            suffix = "sus4";
            score = 88;
            return true;
        }

        if (intervals.Contains(2) && intervals.Contains(7))
        {
            suffix = "sus2";
            score = 86;
            return true;
        }

        if (intervals.Contains(7))
        {
            suffix = "5";
            score = 72;
            return true;
        }

        return false;
    }

    private static bool MatchesExact(HashSet<int> intervals, params int[] expected)
    {
        if (intervals == null || expected == null || intervals.Count != expected.Length)
            return false;

        for (int i = 0; i < expected.Length; i++)
        {
            if (!intervals.Contains(expected[i]))
                return false;
        }

        return true;
    }

    private static int Mod12(int value)
    {
        int mod = value % 12;
        return mod < 0 ? mod + 12 : mod;
    }

    private static string PitchClassToChordName(int pitchClass)
    {
        int normalized = Mod12(pitchClass);
        return ChordPitchClassNames[normalized];
    }

    private void CreateFramePiece(Transform parent, Vector3 localPosition, Vector3 localScale, Material material)
    {
        GameObject piece = GameObject.CreatePrimitive(PrimitiveType.Cube);
        piece.transform.SetParent(parent, false);
        piece.transform.localPosition = localPosition;
        piece.transform.localScale = localScale;
        piece.GetComponent<Renderer>().material = material;
    }

    private GameObject CreateNoteOutline(Vector3 noteScale, Color color)
    {
        GameObject outlineRoot = new GameObject("NoteOutline");
        outlineRoot.transform.SetParent(gameplayRoot.transform, false);

        float thickness = Mathf.Max(0.02f, owner.highwayStuckOutlineThickness);
        float depth = Mathf.Max(0.01f, owner.highwayStuckOutlineDepth);
        float width = Mathf.Max(thickness * 2f, noteScale.x);
        float height = Mathf.Max(thickness * 2f, noteScale.y);
        float insetHalfWidth = Mathf.Max(0f, (width - thickness) * 0.5f);
        float insetHalfHeight = Mathf.Max(0f, (height - thickness) * 0.5f);
        Material outlineMat = owner.CreateSharedTransparentMaterial(new Color(color.r, color.g, color.b, 0.38f), 0.12f);
        ConfigureOverlayMaterial(outlineMat, 110, true);
        float outlinePlaneZ = 0f;

        CreateFramePiece(outlineRoot.transform, new Vector3(0f, insetHalfHeight, outlinePlaneZ), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(0f, -insetHalfHeight, outlinePlaneZ), new Vector3(width, thickness, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(-insetHalfWidth, 0f, outlinePlaneZ), new Vector3(thickness, height, depth), outlineMat);
        CreateFramePiece(outlineRoot.transform, new Vector3(insetHalfWidth, 0f, outlinePlaneZ), new Vector3(thickness, height, depth), outlineMat);
        return outlineRoot;
    }

    private float GetStuckOutlineCenterZ()
    {
        return owner.StrikeLineZ + (Mathf.Max(0.01f, owner.highwayStuckOutlineDepth) * 0.5f);
    }

    private static void ConfigureOverlayMaterial(Material material, int renderQueueOffset, bool renderOnTop)
    {
        if (material == null)
            return;

        material.renderQueue = (int)RenderQueue.Transparent + renderQueueOffset;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)(renderOnTop ? CompareFunction.Always : CompareFunction.LessEqual));
    }

    private void EnsureTechniqueRibbonResources()
    {
        if (techniqueRibbonMesh == null)
            techniqueRibbonMesh = CreateTechniqueRibbonMesh(28);

        if (sharedTechniqueRibbonMaterial == null)
        {
            Shader shader = Resources.Load<Shader>("Shaders/HighwaySlideRibbon");
            if (shader == null)
                shader = Shader.Find("Custom/HighwaySlideRibbon");
            if (shader == null)
                return;

            sharedTechniqueRibbonMaterial = new Material(shader);
            ConfigureOverlayMaterial(sharedTechniqueRibbonMaterial, 100, true);
        }
    }

    private void EnsureBendArrowResources()
    {
        if (sharedBendArrowMaterial != null)
            return;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayNoteArrow");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayNoteArrow");
        if (shader == null)
            return;

        sharedBendArrowMaterial = new Material(shader);
        ConfigureOverlayMaterial(sharedBendArrowMaterial, 145, true);
    }

    private void EnsureMuteSymbolResources()
    {
        if (sharedMuteSymbolMaterial != null)
            return;

        Shader shader = Resources.Load<Shader>("Shaders/HighwayMuteSymbol");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayMuteSymbol");
        if (shader == null)
            return;

        sharedMuteSymbolMaterial = new Material(shader);
        ConfigureOverlayMaterial(sharedMuteSymbolMaterial, 146, true);
    }

    private static bool IsMutedNoteVisual(NoteData data)
    {
        if (data.isMuted)
            return true;

        if (data.fret < 0)
            return true;

        string noteName = data.note ?? string.Empty;
        return noteName.Equals("x", System.StringComparison.OrdinalIgnoreCase)
            || noteName.Equals("mute", System.StringComparison.OrdinalIgnoreCase)
            || noteName.Equals("muted", System.StringComparison.OrdinalIgnoreCase);
    }

    private static bool ShouldShowMuteSymbolForNote(NoteData data)
    {
        return ForceMuteSymbolPreviewOnAllNotes || IsMutedNoteVisual(data);
    }

    private static Mesh CreateTechniqueRibbonMesh(int segments)
    {
        int clampedSegments = Mathf.Max(8, segments);
        int vertexPairs = clampedSegments + 1;
        Vector3[] vertices = new Vector3[vertexPairs * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[clampedSegments * 6];

        for (int i = 0; i < vertexPairs; i++)
        {
            float t = i / (float)clampedSegments;
            int baseIndex = i * 2;
            vertices[baseIndex] = new Vector3(-1f, 0f, t);
            vertices[baseIndex + 1] = new Vector3(1f, 0f, t);
            uvs[baseIndex] = new Vector2(0f, t);
            uvs[baseIndex + 1] = new Vector2(1f, t);

            if (i >= clampedSegments)
                continue;

            int triangleIndex = i * 6;
            triangles[triangleIndex] = baseIndex;
            triangles[triangleIndex + 1] = baseIndex + 2;
            triangles[triangleIndex + 2] = baseIndex + 1;
            triangles[triangleIndex + 3] = baseIndex + 1;
            triangles[triangleIndex + 4] = baseIndex + 2;
            triangles[triangleIndex + 5] = baseIndex + 3;
        }

        Mesh mesh = new Mesh
        {
            name = "HighwayTechniqueRibbonMesh"
        };
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateNormals();
        mesh.bounds = new Bounds(Vector3.zero, new Vector3(256f, 64f, 256f));
        return mesh;
    }

    private static GameObject CreateTechniqueRibbonObject(string name, Transform parent, Mesh mesh, Material material, out Renderer renderer)
    {
        GameObject ribbon = new GameObject(name);
        ribbon.transform.SetParent(parent, false);
        MeshFilter meshFilter = ribbon.AddComponent<MeshFilter>();
        meshFilter.sharedMesh = mesh;
        MeshRenderer meshRenderer = ribbon.AddComponent<MeshRenderer>();
        meshRenderer.sharedMaterial = material;
        meshRenderer.shadowCastingMode = ShadowCastingMode.Off;
        meshRenderer.receiveShadows = false;
        meshRenderer.lightProbeUsage = LightProbeUsage.Off;
        meshRenderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
        renderer = meshRenderer;
        return ribbon;
    }

    private Material CreateLaneSurfaceMaterial()
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayLaneFloorFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayLaneFloorFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.025f, 0.03f, 0.045f, 0.14f), 0f);

        // Keep lane floors behind strings and overlay effects in both editor and player.
        mat.renderQueue = (int)RenderQueue.Transparent - 40;

        mat.SetColor("_Color", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        mat.SetColor("_BaseColor", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        mat.SetColor("_TintColor", new Color(0.025f, 0.03f, 0.045f, 0.14f));
        if (mat.HasProperty("_EdgeFadeLeft"))
            mat.SetFloat("_EdgeFadeLeft", 0.008f);
        if (mat.HasProperty("_EdgeFadeRight"))
            mat.SetFloat("_EdgeFadeRight", 0.008f);
        if (mat.HasProperty("_FrontBackFade"))
            mat.SetFloat("_FrontBackFade", 0.45f);
        return mat;
    }

    private Material CreateNoteTetherMaterial(Color color)
    {
        Shader shader = Resources.Load<Shader>("Shaders/HighwayNoteTetherFade");
        if (shader == null)
            shader = Shader.Find("Custom/HighwayNoteTetherFade");
        Material mat = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(color.r, color.g, color.b, 0.95f), 0f);

        Color tetherColor = new Color(color.r, color.g, color.b, 0.95f);
        mat.SetColor("_Color", tetherColor);
        mat.SetColor("_BaseColor", tetherColor);
        mat.SetColor("_TintColor", tetherColor);
        if (mat.HasProperty("_FadeTop"))
            mat.SetFloat("_FadeTop", 0.5f);
        ConfigureOverlayMaterial(mat, 92, true);
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

        ConfigureOverlayMaterial(mat, 95, true);
        mat.SetColor("_Color", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        mat.SetColor("_BaseColor", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        mat.SetColor("_TintColor", new Color(0.12f, 0.26f, 0.55f, 0.85f));
        if (mat.HasProperty("_FadeStart"))
            mat.SetFloat("_FadeStart", 0.0f);
        if (mat.HasProperty("_FadeEnd"))
            mat.SetFloat("_FadeEnd", 0.38f);
        return mat;
    }

    private int GetFretLightColumnCount()
    {
        return Mathf.Max(1, owner.TotalFrets + 1);
    }

    private float GetStringY(int stringIdx)
    {
        int stringCount = GetRenderableStringCount();
        int clampedString = Mathf.Clamp(stringIdx, 0, stringCount - 1);
        int row = owner.invertStrings ? ((stringCount - 1) - clampedString) : clampedString;
        return (row * GetStringLaneSpacing()) + GetStringLaneSpacing();
    }

    private int GetRenderableStringCount()
    {
        int requested = owner != null ? owner.ActiveStringCount : stringVisuals.Length;
        return Mathf.Clamp(requested, 1, stringVisuals.Length);
    }

    private static float GetStringLaneSpacing()
    {
        return StringLaneSpacing;
    }

    private float GetNoteX(int fret)
    {
        if (fret <= 0)
            return -owner.FretSpacing * 0.5f;

        return (fret * owner.FretSpacing) - (owner.FretSpacing * 0.5f);
    }

    private sealed class HighwayNoteView
    {
        public GameObject noteRoot;
        public Transform noteTransform;
        public Renderer noteRenderer;
        public Material noteMaterial;
        public TextMeshPro label;
        public TextMeshPro laneTagLabel;
        public Transform laneTagTransform;
        public GameObject tail;
        public Transform tailTransform;
        public GameObject tether;
        public Transform tetherTransform;
        public Renderer tetherRenderer;
        public Material tetherMaterial;
        public GameObject marker;
        public Transform markerTransform;
        public Renderer markerRenderer;
        public Material markerMaterial;
        public GameObject bendArrow;
        public Transform bendArrowTransform;
        public Renderer bendArrowRenderer;
        public MaterialPropertyBlock bendArrowPropertyBlock;
        public GameObject bendArrowSecondary;
        public Transform bendArrowSecondaryTransform;
        public Renderer bendArrowSecondaryRenderer;
        public MaterialPropertyBlock bendArrowSecondaryPropertyBlock;
        public GameObject muteSymbol;
        public Transform muteSymbolTransform;
        public Renderer muteSymbolRenderer;
        public GameObject outlineRoot;
        public Transform outlineTransform;
        public GameObject techniqueRoot;
        public Transform techniqueRootTransform;
        public GameObject[] techniqueSegmentRibbons;
        public Renderer[] techniqueSegmentRibbonRenderers;
        public MaterialPropertyBlock[] techniqueSegmentRibbonPropertyBlocks;
        public GameObject slideRibbon;
        public Renderer slideRibbonRenderer;
        public MaterialPropertyBlock slideRibbonPropertyBlock;
        public GameObject legatoCurve;
        public LineRenderer legatoCurveRenderer;
        public SlideRibbonFadeState slideRibbonFadeState;
        public GameObject bendRibbon;
        public Renderer bendRibbonRenderer;
        public MaterialPropertyBlock bendRibbonPropertyBlock;
        public GameObject bendSustainRibbon;
        public Renderer bendSustainRibbonRenderer;
        public MaterialPropertyBlock bendSustainRibbonPropertyBlock;
        public GameObject sustainRibbon;
        public Renderer sustainRibbonRenderer;
        public MaterialPropertyBlock sustainRibbonPropertyBlock;
        public Color baseColor;
        public Vector3 baseScale;
        public float noteX;
        public float noteY;
        public float noteStrikeOffset;
        public bool hasAnyTechniqueVisual;
        public bool hasCachedNoteAppearance;
        public Color cachedNoteColor;
        public float cachedNoteEmission;
        public bool hasCachedAppliedNoteScale;
        public Vector3 cachedAppliedNoteScale;
        public bool hasCachedNoteRendererEnabled;
        public bool cachedNoteRendererEnabled;
        public bool hasCachedTetherColor;
        public Color cachedTetherColor;
        public bool hasCachedMarkerColor;
        public Color cachedMarkerColor;
        public float cachedMarkerEmissionMultiplier;

        public void Destroy()
        {
            if (noteRoot != null)
                Object.Destroy(noteRoot);
            if (laneTagLabel != null)
                Object.Destroy(laneTagLabel.gameObject);
            if (tail != null)
                Object.Destroy(tail);
            if (tether != null)
                Object.Destroy(tether);
            if (marker != null)
                Object.Destroy(marker);
            if (bendArrow != null)
                Object.Destroy(bendArrow);
            if (bendArrowSecondary != null)
                Object.Destroy(bendArrowSecondary);
            if (muteSymbol != null)
                Object.Destroy(muteSymbol);
            if (legatoCurve != null)
                Object.Destroy(legatoCurve);
            if (outlineRoot != null)
                Object.Destroy(outlineRoot);
            if (techniqueRoot != null)
                Object.Destroy(techniqueRoot);
        }
    }
}
