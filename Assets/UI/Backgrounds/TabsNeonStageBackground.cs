using System.Collections.Generic;
using UnityEngine;
using Unity.Profiling;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif

public sealed class TabsNeonStageBackground : ITabsBackgroundEffect
{
    private const string Enviro3ResourcePath = "Enviro3/Enviro 3";
    private const string Enviro3VolumetricCloudPresetResourcePath = "Enviro3/Default Volumetric Clouds Preset";
    private const string Enviro3SampleTerrainsResourcePath = "Enviro3/Terrains";
#if UNITY_EDITOR
    private const string Enviro3AssetPath = "Assets/Enviro 3 - Sky and Weather/Enviro 3.prefab";
    private const string Enviro3VolumetricCloudPresetAssetPath = "Assets/Enviro 3 - Sky and Weather/Scripts/Runtime/Modules/VolumetricClouds/Preset/Default Volumetric Clouds Preset.asset";
    private const string Enviro3SampleTerrainsAssetPath = "Assets/Enviro 3 - Sky and Weather/Sample/Terrains/Terrains.prefab";
#endif
    private const int Enviro3MoodCount = 10;
    private const int Enviro3MoonModeCount = 4;
    private static readonly ProfilerMarker TickProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.Tick");
    private static readonly ProfilerMarker ApplyVisualStateProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.ApplyVisualState");
    private static readonly ProfilerMarker RefreshFloorTexturesProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.RefreshFloorTextures");
    private static readonly ProfilerMarker RebuildFloorBaseTextureProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.RebuildFloorBaseTexture");
    private static readonly ProfilerMarker RebuildFloorGridTextureProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.RebuildFloorGridTexture");
    private static readonly ProfilerMarker UpdatePlacementProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.UpdatePlacement");
    private static readonly ProfilerMarker UpdateSkyDesignProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.UpdateSkyDesign");
    private static readonly ProfilerMarker ApplyEnviro3SkyProfilerMarker = new ProfilerMarker("StringTheory.Background.Enviro3.ApplySky");
    private static readonly ProfilerMarker ApplyEnviro3RenderSettingsProfilerMarker = new ProfilerMarker("StringTheory.Background.Enviro3.ApplyRenderSettings");
    private static readonly ProfilerMarker Enviro3UpdateModulesProfilerMarker = new ProfilerMarker("StringTheory.Background.Enviro3.UpdateModules");
    private static readonly ProfilerMarker Enviro3SkyUpdateModuleProfilerMarker = new ProfilerMarker("StringTheory.Background.Enviro3.SkyUpdateModule");
    private static readonly ProfilerMarker ApplyEnviro3MoodProfilerMarker = new ProfilerMarker("StringTheory.Background.Enviro3.ApplyMood");
    private static readonly ProfilerMarker UpdateStagePlacementProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.UpdateStagePlacement");
    private static readonly ProfilerMarker UpdateFloorPlacementProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.UpdateFloorPlacement");
    private static readonly ProfilerMarker UpdateHorizonPlacementProfilerMarker = new ProfilerMarker("StringTheory.Background.NeonStage.UpdateHorizonPlacement");
    private static readonly Vector4 UnappliedEnviro3CloudModifiers = new Vector4(-999f, -999f, -999f, -999f);
    private static readonly int LightMatrixShaderId = Shader.PropertyToID("_LightMatrix");
    private static readonly int BaseColorShaderId = Shader.PropertyToID("_BaseColor");
    private static readonly int HorizonColorShaderId = Shader.PropertyToID("_HorizonColor");
    private static readonly int LeftAccentColorShaderId = Shader.PropertyToID("_LeftAccentColor");
    private static readonly int RightAccentColorShaderId = Shader.PropertyToID("_RightAccentColor");
    private static readonly int PulseSpeedShaderId = Shader.PropertyToID("_PulseSpeed");
    private static readonly int HorizonStrengthShaderId = Shader.PropertyToID("_HorizonStrength");
    private static readonly int VignetteStrengthShaderId = Shader.PropertyToID("_VignetteStrength");
    private static readonly int StageTimeShaderId = Shader.PropertyToID("_StageTime");
    private static readonly int SkyLineStyleShaderId = Shader.PropertyToID("_SkyLineStyle");
    private static readonly int SkyLineStrengthShaderId = Shader.PropertyToID("_SkyLineStrength");
    private static readonly int SkyLineOpacityShaderId = Shader.PropertyToID("_SkyLineOpacity");
    private static readonly int SkyLineReflectionStrengthShaderId = Shader.PropertyToID("_SkyLineReflectionStrength");
    private static readonly int SkyDotStrengthShaderId = Shader.PropertyToID("_SkyDotStrength");
    private static readonly int SkySideWashStrengthShaderId = Shader.PropertyToID("_SkySideWashStrength");
    private static readonly int SkyCoreBrightnessShaderId = Shader.PropertyToID("_SkyCoreBrightness");
    private static readonly int SkyCoreSizeShaderId = Shader.PropertyToID("_SkyCoreSize");
    private static readonly int SkyCoreHeightShaderId = Shader.PropertyToID("_SkyCoreHeight");
    private static readonly int SkyCoreXOffsetShaderId = Shader.PropertyToID("_SkyCoreXOffset");
    private static readonly int SkyCoreFalloffShaderId = Shader.PropertyToID("_SkyCoreFalloff");
    private static readonly int SkyOutsideDarknessShaderId = Shader.PropertyToID("_SkyOutsideDarkness");
    private static readonly int SkyCorePurpleStrengthShaderId = Shader.PropertyToID("_SkyCorePurpleStrength");
    private static readonly int SkyCorePurpleFalloffShaderId = Shader.PropertyToID("_SkyCorePurpleFalloff");
    private static readonly int SkyAuroraRidgeStrengthShaderId = Shader.PropertyToID("_SkyAuroraRidgeStrength");
    private static readonly int SkyAuroraRidgeWhiteFalloffPositionShaderId = Shader.PropertyToID("_SkyAuroraRidgeWhiteFalloffPosition");
    private static readonly int SkyAuroraRidgeWhiteFalloffSharpnessShaderId = Shader.PropertyToID("_SkyAuroraRidgeWhiteFalloffSharpness");
    private static readonly int SkyAuroraWaveBumpinessShaderId = Shader.PropertyToID("_SkyAuroraWaveBumpiness");
    private static readonly int LeftColorShaderId = Shader.PropertyToID("_LeftColor");
    private static readonly int RightColorShaderId = Shader.PropertyToID("_RightColor");
    private static readonly int MidColorShaderId = Shader.PropertyToID("_MidColor");
    private static readonly int CenterColorShaderId = Shader.PropertyToID("_CenterColor");
    private static readonly int IntensityShaderId = Shader.PropertyToID("_Intensity");
    private static readonly int CoreStrengthShaderId = Shader.PropertyToID("_CoreStrength");
    private static readonly int VerticalSharpnessShaderId = Shader.PropertyToID("_VerticalSharpness");
    private static readonly int HorizontalSharpnessShaderId = Shader.PropertyToID("_HorizontalSharpness");
    private static readonly int CoreWidthShaderId = Shader.PropertyToID("_CoreWidth");
    private static readonly int CoreSoftnessShaderId = Shader.PropertyToID("_CoreSoftness");
    private static readonly int ShimmerStrengthShaderId = Shader.PropertyToID("_ShimmerStrength");
    private static readonly int AlphaShaderId = Shader.PropertyToID("_Alpha");
    private static readonly int CenterBlendWidthShaderId = Shader.PropertyToID("_CenterBlendWidth");
    private static readonly int CenterBlendFalloffShaderId = Shader.PropertyToID("_CenterBlendFalloff");
    private static readonly int CenterBlendStrengthShaderId = Shader.PropertyToID("_CenterBlendStrength");
    private static readonly int ColorSaturationShaderId = Shader.PropertyToID("_ColorSaturation");
    private static readonly int EdgeBlurStrengthShaderId = Shader.PropertyToID("_EdgeBlurStrength");
    private static readonly int EdgeBlurStartShaderId = Shader.PropertyToID("_EdgeBlurStart");
    private static readonly int EdgeBlurSharpnessShaderId = Shader.PropertyToID("_EdgeBlurSharpness");
    private static readonly int StarsTintShaderId = Shader.PropertyToID("_Tint");
    private static readonly int StarsBrightnessShaderId = Shader.PropertyToID("_Brightness");
    private static readonly int StarsTwinkleStrengthShaderId = Shader.PropertyToID("_TwinkleStrength");
    private static readonly int StarsTwinkleSpeedShaderId = Shader.PropertyToID("_TwinkleSpeed");
    private static readonly int EnviroSkyRotationShaderId = Shader.PropertyToID("_EnviroSkyRotation");
    private static readonly int EnviroFloatingSkyFillShaderId = Shader.PropertyToID("_EnviroFloatingSkyFill");
    private static readonly int EnviroStageSkyPitchShaderId = Shader.PropertyToID("_EnviroStageSkyPitch");
    private static readonly int EnviroStageAuroraColorShaderId = Shader.PropertyToID("_EnviroStageAuroraColor");
    private static readonly int EnviroStageAuroraParamsShaderId = Shader.PropertyToID("_EnviroStageAuroraParams");
    private static readonly int EnviroStageStarDensityShaderId = Shader.PropertyToID("_TabsStarDensity");

    private const float DomeScale = 950f;
    private const int StageRenderQueueBase = (int)RenderQueue.Background + 12;
    private const float StageLightPitch = 25.551f;
    private static readonly Quaternion StageLightRotation = Quaternion.Euler(StageLightPitch, 0f, 0f);
    private const int HorizonMeshSegments = 384;
    private const int FloorMeshSegments = 192;
    private const int FloorDepthSegments = 12;
    // Enviro skyboxes render after opaque geometry and before transparent geometry.
    // Keep the stage backdrop in the first transparent slots so it appears over the
    // Enviro sky, but still behind the highway's character, notes, and glow overlays.
    private const int EnviroStageRenderQueueBase = (int)RenderQueue.GeometryLast + 1;
    private const int DefaultStageSortingOrder = 0;
    private const int EnviroFloorSortingOrder = -120;
    private const int EnviroFloorGridSortingOrder = -119;
    private const int EnviroMountainSortingOrderBase = -105;
    private const int EnviroHorizonSortingOrder = -90;
    private const int EnviroHorizonCoreSortingOrder = -89;
    private const int EnviroMountainSilhouetteStyleVersion = 5;
    private const float EnviroMountainTerrainDistanceOffset = 145f;
    private const float EnviroMountainSilhouetteWidthMultiplier = 2.08f;
    private const float EnviroMountainSilhouetteBaseY = -28f;
    private const float EnviroMountainSilhouettePeakHeight = 24f;
    private const float EnviroMountainSilhouetteYOffset = -11.5f;
    private const int EnviroMountainSilhouetteSamples = 128;
    private const int EnviroMountainSilhouetteLayerCount = 3;
    private static readonly Color[] EnviroMountainLayerColors =
    {
        new Color(0.030f, 0.040f, 0.090f, 1.00f),
        new Color(0.014f, 0.024f, 0.064f, 1.00f),
        new Color(0.003f, 0.008f, 0.026f, 1.00f)
    };
    private static readonly float[] EnviroMountainLayerDefaultOpacities = { 0.34f, 0.58f, 0.96f };
    private static readonly float[] EnviroMountainLayerHeightScales = { 0.50f, 0.68f, 0.56f };
    private static readonly float[] EnviroMountainLayerWidthScales = { 1.16f, 1.08f, 1.00f };
    private static readonly float[] EnviroMountainLayerXOffsets = { -0.060f, 0.040f, -0.015f };
    private static readonly float[] EnviroMountainLayerYOffsets = { 0.0f, 0.0f, 0.0f };
    private static readonly float[] EnviroMountainLayerZOffsets = { 18.0f, 9.0f, 0.0f };
    private static readonly float[] EnviroMountainLayerProfileOffsets = { 0.190f, -0.120f, 0.025f };
    private static readonly float[] EnviroMountainLayerDetailStrengths = { 0.24f, 0.30f, 0.38f };
    private const float EnviroExtendedFloorDistanceMultiplier = 2.35f;
    private const float EnviroExtendedFloorWidthMultiplier = 1.70f;
    private enum EnviroCloudArtStyle
    {
        Default,
        Horror,
        Galaxy,
        Aurora,
        ThickMoonlit
    }
    private const float FloorY = -4.55f;
    private const float FloorWidth = 640f;
    // Floor starts slightly behind/under the camera and ends on the bottom edge of the horizon quad.
    // Increase FloorNearDistance if the floor covers too much foreground.
    private const float FloorNearDistance = -28f;

    // ===== Horizon line geometry tuning =====
    // Distance from the camera in world units. Higher pushes the line farther into the scene.
    private const float HorizonLineDistance = 150f;
    // World Y position. Match this to the back edge of the floor if a gap appears.
    private const float HorizonLineY = 2.6f;
    // Horizontal world width of the horizon quads.
    private const float HorizonLineWidth = 620f;
    // Visible soft blur height around the thin line. Lower = thinner/cleaner, higher = bigger glow.
    private const float HorizonGlowBlurHeight = 2.85f;
    // Actual thin bright line thickness.
    private const float HorizonCoreLineHeight = 0.085f;
    private const float HorizonCoreLineYOffset = 0.012f;
    // Optional center bloom around the vanishing point. Keep subtle to avoid a white slab.
    private const float HorizonCenterGlowWidth = 230f;
    private const float HorizonCenterGlowHeight = 2.20f;
    private const float HorizonCenterGlowYOffset = 0.08f;
    // Slightly overlaps the floor into the visible horizon core so there is no thin screen-space gap.
    private const float FloorHorizonOverlap = 0.035f;

    // ===== Neon sky tuning =====
    // Increase these if the animated sky lines/dots are too subtle.
    private const float SkyLineStrength = 8.10f;
    private const float SkyLineOpacity = 1.0f;
    private const float SkyLineReflectionStrength = 0.35f;
    private const float SkyDotStrength = 1.85f;
    private const float SkySideWashStrength = 1.65f;
    private const float SkyCoreBrightness = 1.42f;
    private const float SkyCoreSize = 0.28f;
    private const float SkyCoreHeight = 0.34f;
    private const float SkyCoreXOffset = 0f;
    private const float SkyCoreFalloff = 2.85f;
    private const float SkyOutsideDarkness = 1.72f;
    private const float SkyCorePurpleStrength = 1.08f;
    private const float SkyCorePurpleFalloff = 1.18f;
    private const float SkyAuroraRidgeStrength = 2.60f;
    private const float SkyAuroraRidgeWhiteFalloffPosition = 0.38f;
    private const float SkyAuroraRidgeWhiteFalloffSharpness = 0.62f;
    private const float SkyAuroraWaveBumpiness = 1.0f;
    private const int SkyLineStyle = 1;
    private const bool DomeStarsDefaultEnabled = true;
    private const int DomeStarsDefaultCount = 260;
    private const float DomeStarsDefaultBrightness = 0.82f;
    private const float DomeStarsDefaultTwinkleStrength = 0.35f;
    private const float DomeStarsDefaultTwinkleSpeed = 0.65f;
    private const float DomeStarsDefaultSize = 1.0f;
    private const int DomeStarsDefaultSeed = 1729;
    private const float DomeStarsNearZ = 120f;
    private const float DomeStarsFarZ = 470f;
    private const float DomeStarsMinY = -180f;
    private const float DomeStarsMaxY = 230f;
    private const float DomeStarsHalfWidth = 420f;
    private const bool StageCloudLayerEnabled = false;
    private const int StageCloudCount = 7;
    private const float StageCloudOpacity = 0.62f;
    private const float StageCloudSpeed = 0.22f;
    private const float StageCloudTextureAlpha = 1.00f;
    private static readonly float[] StageCloudSeedX = { -0.42f, -0.30f, -0.18f, 0.16f, 0.28f, 0.42f, 0.04f };
    private static readonly float[] StageCloudSeedY = { 0.70f, 0.55f, 0.78f, 0.62f, 0.82f, 0.50f, 0.90f };
    private static readonly float[] StageCloudSeedScale = { 1.25f, 0.90f, 1.05f, 1.15f, 0.85f, 1.00f, 0.75f };
    private static readonly float[] StageCloudSeedSpeed = { 0.34f, 0.24f, 0.18f, -0.20f, -0.30f, -0.16f, 0.12f };
    private static readonly int[] StageCloudTextureIndices = { 1, 4, 8, 11, 14, 17, 20 };

    // ===== Horizon color/glow tuning =====
    // Master color brightness. Increase if magenta/cyan are too weak; decrease if colors clip toward white.
    // Master HDR brightness. Use small changes here; very high values can still bloom toward white.
    private const float HorizonColorStrength = 1.20f;
    // Chroma boost. Use this to make the magenta/cyan colors stronger without widening the white center.
    private const float HorizonColorSaturation = 2.10f;
    // Glow intensity for each layer.
    private const float HorizonSoftGlowIntensity = 2.10f;
    private const float HorizonCoreGlowIntensity = 1.70f;
    private const float HorizonCenterGlowIntensity = 0.85f;
    // Layer opacity. If the line looks like a solid band, reduce soft alpha first.
    private const float HorizonSoftAlpha = 0.44f;
    private const float HorizonCoreAlpha = 0.82f;
    private const float HorizonCenterAlpha = 0.22f;
    // Blur/falloff controls. Higher vertical falloff = tighter/thinner glow.
    private const float HorizonSoftBlurFalloff = 3.10f;
    private const float HorizonCoreBlurFalloff = 36.0f;
    // Horizontal color falloff. Lower spreads color farther; higher concentrates it toward the center.
    private const float HorizonSoftColorFalloff = 0.55f;
    private const float HorizonCoreColorFalloff = 0.22f;
    private const float HorizonCenterColorFalloff = 3.20f;
    // Controls only the bright center blend, not the full line width.
    // Lower width = the pale/white center ends sooner and magenta/cyan start closer to the middle.
    private const float HorizonCenterBlendWidth = 0.14f;
    // Higher falloff = faster transition out of the pale center.
    private const float HorizonCenterBlendFalloff = 6.50f;
    // Lower strength = less pale/white center overall. Set to 0 to remove center white entirely.
    private const float HorizonCenterBlendStrength = 0.52f;
    // Shader inner line width/softness. These affect the generated line inside each quad, not world scale.
    private const float HorizonSoftCoreWidth = 0.030f;
    private const float HorizonSoftCoreSoftness = 0.090f;
    private const float HorizonCoreWidth = 0.040f;
    private const float HorizonCoreSoftness = 0.018f;
    private const float HorizonCenterCoreWidth = 0.075f;
    private const float HorizonCenterCoreSoftness = 0.120f;
    private const float HorizonSoftShimmerStrength = 0f;
    private const float HorizonCoreShimmerStrength = 0f;
    private const float HorizonCenterShimmerStrength = 0f;
    // Edge blur affects the shader line itself, not the world-space quad size.
    private const float HorizonEdgeBlurStrength = 0f;
    private const float HorizonEdgeBlurStart = 0.72f;
    private const float HorizonEdgeBlurSharpness = 2.0f;
    // Curvature is geometric. Positive Curve Down lowers the sides; positive Curve Toward Camera pulls the sides closer.
    private const float HorizonCurveDown = 0f;
    private const float HorizonCurveTowardCamera = 0f;

    // ===== Neon stage floor tuning =====
    // Main floor opacity. Set to 1 for a clearly visible floor, lower it only after the design is readable.
    private const float FloorBaseOpacity = 1.00f;
    // How much the floor fades with distance. 0 = never fades, 1 = full fade from the generated texture.
    private const float FloorDistanceFadeStrength = 0.0f; //0.16f;
    // Base floor colors. These are intentionally brighter than pure black so the floor does not disappear into the dome.
    private static readonly Color FloorNearColor = new Color(0.0010f, 0.0015f, 0.0060f, 1f);
    private static readonly Color FloorMidColor = new Color(0.0025f, 0.0040f, 0.0150f, 1f);
    private static readonly Color FloorFarColor = new Color(0.0100f, 0.0180f, 0.0520f, 1f);
    // Keeps the foreground almost black and only lifts the blue/purple brightness near the horizon.
    private const float FloorHorizonLiftStart = 0.58f;
    private const float FloorHorizonLiftPower = 2.25f;
    // Side tint gives the left/right edges the purple/blue stage feel from the reference.
    private static readonly Color FloorLeftTintColor = new Color(0.0300f, 0.0020f, 0.0550f, 1f);
    private static readonly Color FloorRightTintColor = new Color(0.0020f, 0.0180f, 0.0600f, 1f);
    private const float FloorSideTintStrength = 0.12f;
    // Soft reflection/sheen on the floor, mostly near the horizon and center.
    private static readonly Color FloorCenterSheenColor = new Color(0.0060f, 0.0220f, 0.0550f, 0f);
    private static readonly Color FloorHorizonSheenColor = new Color(0.0300f, 0.0080f, 0.0700f, 0f);
    private const float FloorCenterSheenStrength = 0.08f;
    private const float FloorHorizonSheenStrength = 0.14f;
    private static readonly Color FloorPostDarkGradientColor = new Color(0.0060f, 0.0180f, 0.0650f, 0f);
    private const float FloorPostDarkGradientStrength = 0.115f;
    // Faint floor grid layer. Keep reflection strength at 0 unless we intentionally reintroduce floor sheen.
    private const float FloorGridOpacity = 0.46f;
    private const float FloorGridLineStrength = 0.88f;
    private const float FloorGridReflectionStrength = 0.0f;
    private static readonly Color FloorGridLeftColor = new Color(0.68f, 0.10f, 1.00f, 1f);
    private static readonly Color FloorGridRightColor = new Color(0.06f, 0.82f, 1.00f, 1f);
    private static readonly Color FloorGridCenterColor = new Color(0.08f, 0.26f, 1.00f, 1f);

    // ===== Far stage light tuning =====
    // These are cheap additive billboards placed near the horizon.
    private const float FarLightDistanceOffset = 18f;
    private const float FarLightBaseHeight = 0.95f;
    private const float FarLightWidth = 15f;
    private const float FarLightHeight = 3.8f;
    private static readonly float[] FarLightOffsets = { -190f, -128f, -72f, 72f, 128f, 190f };
    private static readonly Color[] FarLightColors =
    {
        new Color(2.20f, 0.06f, 3.20f, 0.55f),
        new Color(0.12f, 1.80f, 3.40f, 0.50f),
        new Color(1.55f, 0.18f, 3.10f, 0.48f),
        new Color(0.10f, 1.70f, 3.35f, 0.48f),
        new Color(2.15f, 0.08f, 3.05f, 0.50f),
        new Color(0.10f, 1.85f, 3.50f, 0.55f)
    };
    private readonly bool applyHighwayOverrides;
    private readonly bool useMainMenuProfile;
    private readonly List<Texture2D> ownedTextures = new List<Texture2D>();
    private GuitarBridgeServer owner;
    private GameObject root;
    private GameObject domeObject;
    private Material domeMaterial;
    private Renderer domeRenderer;
    private GameObject domeStarsObject;
    private Mesh domeStarsMesh;
    private Material domeStarsMaterial;
    private Renderer domeStarsRenderer;
    private GameObject floorObject;
    private Material floorMaterial;
    private Renderer floorRenderer;
    private Texture2D floorTexture;
    private GameObject floorGridObject;
    private Material floorGridMaterial;
    private Renderer floorGridRenderer;
    private Texture2D floorGridTexture;
    private GameObject horizonObject;
    private Material horizonMaterial;
    private Renderer horizonRenderer;
    private GameObject horizonCoreObject;
    private Material horizonCoreMaterial;
    private Renderer horizonCoreRenderer;
    private GameObject enviroMountainPrefab;
    private GameObject enviroMountainObject;
    private Material enviroMountainMaterial;
    private Mesh enviroMountainSilhouetteMesh;
    private MeshFilter enviroMountainSilhouetteMeshFilter;
    private Renderer enviroMountainSilhouetteRenderer;
    private readonly List<Terrain> enviroMountainTerrains = new List<Terrain>();
    private readonly List<Renderer> enviroMountainRenderers = new List<Renderer>();
    private readonly List<Mesh> enviroMountainSilhouetteMeshes = new List<Mesh>();
    private readonly List<Material> enviroMountainLayerMaterials = new List<Material>();
    private int enviroMountainSilhouetteVersion;
    private readonly List<GameObject> farLightObjects = new List<GameObject>();
    private readonly List<Material> farLightMaterials = new List<Material>();
    private readonly List<Renderer> farLightRenderers = new List<Renderer>();
    private readonly List<StageCloud> stageClouds = new List<StageCloud>();
    private readonly CurvedMeshCache floorMeshCache = new CurvedMeshCache();
    private readonly CurvedMeshCache floorGridMeshCache = new CurvedMeshCache();
    private readonly CurvedMeshCache horizonMeshCache = new CurvedMeshCache();
    private readonly CurvedMeshCache horizonCoreMeshCache = new CurvedMeshCache();
    private Texture2D farLightTexture;
    private Texture2D stageCloudTexture;
    private GameObject enviro3Prefab;
    private Enviro.EnviroVolumetricCloudsModule enviro3VolumetricCloudPreset;
    private GameObject enviro3Object;
    private Enviro.EnviroManager enviro3Manager;
    private float enviro3SkyRotationYaw;
    private bool enviro3HasMoonRotationOverride;
    private float enviro3MoonRotationOverrideX;
    private float enviro3MoonRotationOverrideY;
    private Color enviro3StageAuroraColor = Color.black;
    private Vector4 enviro3StageAuroraParams = Vector4.zero;
    private Material originalRenderSettingsSkybox;
    private Light originalRenderSettingsSun;
    private bool originalFog;
    private Color originalFogColor;
    private FogMode originalFogMode;
    private float originalFogDensity;
    private float originalFogStartDistance;
    private float originalFogEndDistance;
    private AmbientMode originalAmbientMode;
    private Color originalAmbientSkyColor;
    private Color originalAmbientEquatorColor;
    private Color originalAmbientGroundColor;
    private float originalAmbientIntensity;
    private Color originalSubtractiveShadowColor;
    private DefaultReflectionMode originalDefaultReflectionMode;
    private int originalDefaultReflectionResolution;
    private int originalReflectionBounces;
    private float originalReflectionIntensity;
    private Camera renderCameraOverride;
    private Camera enviro3ConfiguredCamera;
    private Camera skyboxCamera;
    private CameraClearFlags originalCameraClearFlags;
    private Color originalCameraBackgroundColor;
    private float cachedGroundDarkness = float.NaN;
    private float cachedGroundGradientStart = float.NaN;
    private float cachedGroundGradientBrightness = float.NaN;
    private int cachedFloorHorizonColorPalette = int.MinValue;
    private int cachedFloorSkyLineColorPalette = int.MinValue;
    private bool cachedFloorUnifiedSideColors;
    private bool skyboxOverrideApplied;
    private bool originalRenderSettingsStateCaptured;
    private bool originalCameraStateCaptured;
    private bool warnedMissingEnviro3Prefab;
    private bool warnedMissingEnviro3VolumetricCloudPreset;
    private int appliedEnviro3MoodIndex = int.MinValue;
    private int appliedEnviro3MoonModeIndex = int.MinValue;
    private bool appliedEnviro3CloudsEnabled = true;
    private Vector4 appliedEnviro3CloudModifiers = UnappliedEnviro3CloudModifiers;
    private float appliedEnviro3StarAnimation = -1f;
    private float appliedEnviro3StarDensity = -1f;
    private bool enviro3RenderSettingsApplied;
    private bool enviro3ShaderGlobalsApplied;
    private float appliedEnviro3RenderSkyPitch = float.NaN;
    private float appliedEnviro3RenderStarDensity = float.NaN;
    private bool enviro3CelestialOverridesApplied;
    private int appliedEnviro3CelestialMoonModeIndex = int.MinValue;
    private bool appliedEnviro3CelestialHasMoonOverride;
    private float appliedEnviro3CelestialMoonRotationX = float.NaN;
    private float appliedEnviro3CelestialMoonRotationY = float.NaN;
    private float appliedEnviro3CelestialSkyPitch = float.NaN;
    private float appliedEnviro3CelestialSkyYaw = float.NaN;
    private Color appliedEnviro3CelestialAuroraColor = Color.clear;
    private Vector4 appliedEnviro3CelestialAuroraParams = UnappliedEnviro3CloudModifiers;
    private int cachedDomeStarsCount = -1;
    private int cachedDomeStarsSeed = int.MinValue;
    private float cachedDomeStarsSize = -1f;
    private bool proceduralDomeVisible;
    private bool proceduralStageVisible = true;
    private bool proceduralGroundVisible = true;
    private bool proceduralHorizonVisible = true;
    private bool proceduralMountainVisible;
    private bool proceduralDecorVisible = true;
    private bool proceduralStageRenderedAfterSky;
    public TabsNeonStageBackground(bool applyHighwayOverrides = false, bool useMainMenuProfile = false)
    {
        this.applyHighwayOverrides = applyHighwayOverrides;
        this.useMainMenuProfile = useMainMenuProfile;
    }

    private static float FiniteOr(float value, float fallback)
    {
        return float.IsNaN(value) || float.IsInfinity(value) ? fallback : value;
    }

    private static float ClampFinite(float value, float min, float max, float fallback)
    {
        return Mathf.Clamp(FiniteOr(value, fallback), min, max);
    }

    private static float Clamp01Finite(float value, float fallback)
    {
        return Mathf.Clamp01(FiniteOr(value, fallback));
    }

    private static float MinFinite(float value, float min, float fallback)
    {
        return Mathf.Max(min, FiniteOr(value, fallback));
    }

    private float HorizonLineDistanceValue => MinFinite(owner != null ? owner.neonHorizonLineDistance : HorizonLineDistance, 1f, HorizonLineDistance);
    private float HorizonLineYValue => FiniteOr(owner != null ? owner.neonHorizonLineY : HorizonLineY, HorizonLineY);
    private float HorizonLineWidthValue => MinFinite(owner != null ? owner.neonHorizonLineWidth : HorizonLineWidth, 1f, HorizonLineWidth);
    private float HorizonGlowBlurHeightValue => MinFinite(owner != null ? owner.neonHorizonGlowBlurHeight : HorizonGlowBlurHeight, 0.001f, HorizonGlowBlurHeight);
    private float HorizonCoreLineHeightValue => MinFinite(owner != null ? owner.neonHorizonCoreLineHeight : HorizonCoreLineHeight, 0.001f, HorizonCoreLineHeight);
    private float HorizonCoreLineYOffsetValue => FiniteOr(owner != null ? owner.neonHorizonCoreLineYOffset : HorizonCoreLineYOffset, HorizonCoreLineYOffset);
    private float FloorHorizonOverlapValue => MinFinite(owner != null ? owner.neonHorizonFloorHorizonOverlap : FloorHorizonOverlap, 0f, FloorHorizonOverlap);
    private float SkyLineStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkyLineStrength : SkyLineStrength, 0f, SkyLineStrength);
    private float SkyLineOpacityValue => Clamp01Finite(owner != null ? owner.neonHorizonSkyLineOpacity : SkyLineOpacity, SkyLineOpacity);
    private float SkyLineReflectionStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkyLineReflectionStrength : SkyLineReflectionStrength, 0f, SkyLineReflectionStrength);
    private float SkyDotStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkyDotStrength : SkyDotStrength, 0f, SkyDotStrength);
    private float SkySideWashStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkySideWashStrength : SkySideWashStrength, 0f, SkySideWashStrength);
    private bool UseUnifiedSideColorsValue => owner == null || owner.GetNeonHorizonUseUnifiedSideColors(useMainMenuProfile);
    private float SkyCoreBrightnessValue => MinFinite(owner != null ? owner.neonHorizonSkyCoreBrightness : SkyCoreBrightness, 0f, SkyCoreBrightness);
    private float SkyCoreSizeValue => MinFinite(owner != null ? owner.neonHorizonSkyCoreSize : SkyCoreSize, 0.001f, SkyCoreSize);
    private float SkyCoreHeightValue => FiniteOr(owner != null ? owner.neonHorizonSkyCoreHeight : SkyCoreHeight, SkyCoreHeight);
    private float SkyCoreXOffsetValue => FiniteOr(owner != null ? owner.neonHorizonSkyCoreXOffset : SkyCoreXOffset, SkyCoreXOffset);
    private float SkyCoreFalloffValue => MinFinite(owner != null ? owner.neonHorizonSkyCoreFalloff : SkyCoreFalloff, 0.001f, SkyCoreFalloff);
    private float SkyOutsideDarknessValue => MinFinite(owner != null ? owner.neonHorizonSkyOutsideDarkness : SkyOutsideDarkness, 0f, SkyOutsideDarkness);
    private float SkyCorePurpleStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkyCorePurpleStrength : SkyCorePurpleStrength, 0f, SkyCorePurpleStrength);
    private float SkyCorePurpleFalloffValue => MinFinite(owner != null ? owner.neonHorizonSkyCorePurpleFalloff : SkyCorePurpleFalloff, 0.001f, SkyCorePurpleFalloff);
    private float SkyAuroraRidgeStrengthValue => MinFinite(owner != null ? owner.neonHorizonSkyAuroraRidgeStrength : SkyAuroraRidgeStrength, 0f, SkyAuroraRidgeStrength);
    private float SkyAuroraRidgeWhiteFalloffPositionValue => FiniteOr(owner != null ? owner.neonHorizonSkyAuroraRidgeWhiteFalloffPosition : SkyAuroraRidgeWhiteFalloffPosition, SkyAuroraRidgeWhiteFalloffPosition);
    private float SkyAuroraRidgeWhiteFalloffSharpnessValue => MinFinite(owner != null ? owner.neonHorizonSkyAuroraRidgeWhiteFalloffSharpness : SkyAuroraRidgeWhiteFalloffSharpness, 0.001f, SkyAuroraRidgeWhiteFalloffSharpness);
    private float SkyAuroraWaveBumpinessValue => MinFinite(owner != null ? owner.neonHorizonSkyAuroraWaveBumpiness : SkyAuroraWaveBumpiness, 0f, SkyAuroraWaveBumpiness);
    private int HorizonColorPaletteValue => owner != null ? owner.GetNeonHorizonColorPalette(useMainMenuProfile) : 0;
    private int SkyLineColorPaletteValue => owner != null ? owner.GetNeonHorizonSkyLineColorPalette(useMainMenuProfile) : 0;
    private int SkyLineStyleValue => owner != null ? owner.neonHorizonSkyLineStyle : SkyLineStyle;
    private float GroundDarknessValue => MinFinite(owner != null ? owner.neonHorizonGroundDarkness : 1f, 0.05f, 1f);
    private float GroundGradientStartValue => ClampFinite(owner != null ? owner.neonHorizonGroundGradientStart : FloorHorizonLiftStart, 0.01f, 0.99f, FloorHorizonLiftStart);
    private float GroundGradientBrightnessValue => MinFinite(owner != null ? owner.neonHorizonGroundGradientBrightness : 1f, 0f, 1f);
    private bool StageCloudsEnabledValue => StageCloudLayerEnabled
        && (owner == null || owner.neonHorizonCloudsEnabled);
    private float StageCloudOpacityValue => Clamp01Finite(owner != null ? owner.neonHorizonCloudOpacity : StageCloudOpacity, StageCloudOpacity);
    private float StageCloudSpeedValue => MinFinite(owner != null ? owner.neonHorizonCloudSpeed : StageCloudSpeed, 0f, StageCloudSpeed);
    private bool UseEnviro3SkyValue => owner != null
        && owner.GetNeonStageSkyDesign(useMainMenuProfile) == GuitarBridgeServer.TabsNeonStageSkyDesign.Enviro3;
    private int Enviro3MoodIndexValue => owner != null
        ? Mathf.Clamp((int)owner.GetEnviroSkyMood(useMainMenuProfile), 0, Enviro3MoodCount - 1)
        : 0;
    private int Enviro3MoonModeIndexValue => owner != null
        ? Mathf.Clamp((int)owner.GetCurrentEnviroMoonMode(useMainMenuProfile), 0, Enviro3MoonModeCount - 1)
        : (int)GuitarBridgeServer.TabsEnviroMoonMode.Normal;
    private bool Enviro3CloudsEnabledValue => owner == null || owner.GetCurrentEnviroCloudsEnabled(useMainMenuProfile);
    private float Enviro3CloudAmountValue => ClampFinite(owner != null ? owner.GetCurrentEnviroCloudAmount(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float Enviro3CloudThicknessValue => ClampFinite(owner != null ? owner.GetCurrentEnviroCloudThickness(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float Enviro3CloudConnectivityValue => ClampFinite(owner != null ? owner.GetCurrentEnviroCloudConnectivity(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float Enviro3CloudContrastValue => ClampFinite(owner != null ? owner.GetCurrentEnviroCloudContrast(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float Enviro3SkyCameraPitchValue => ClampFinite(owner != null ? owner.GetCurrentEnviroSkyCameraPitch(useMainMenuProfile) : 0f, -18f, 18f, 0f);
    private float Enviro3StarAnimationValue => ClampFinite(owner != null ? owner.GetCurrentEnviroStarAnimation(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float Enviro3StarDensityValue => ClampFinite(owner != null ? owner.GetCurrentEnviroStarDensity(useMainMenuProfile) : 1f, 0f, 2f, 1f);
    private float GetEnviroMountainLayerOpacityValue(int layerIndex)
    {
        if (owner == null)
            return Mathf.Clamp01(GetMountainLayerValue(EnviroMountainLayerDefaultOpacities, layerIndex, 1f));

        if (UseProceduralDomeValue)
        {
            switch (Mathf.Clamp(layerIndex, 0, EnviroMountainSilhouetteLayerCount - 1))
            {
                case 0: return Clamp01Finite(owner.GetDomeMountainFarOpacity(useMainMenuProfile), GetMountainLayerValue(EnviroMountainLayerDefaultOpacities, layerIndex, 1f));
                case 1: return Clamp01Finite(owner.GetDomeMountainMidOpacity(useMainMenuProfile), GetMountainLayerValue(EnviroMountainLayerDefaultOpacities, layerIndex, 1f));
                default: return Clamp01Finite(owner.GetDomeMountainNearOpacity(useMainMenuProfile), GetMountainLayerValue(EnviroMountainLayerDefaultOpacities, layerIndex, 1f));
            }
        }

        return Clamp01Finite(owner.GetCurrentEnviroMountainLayerOpacity(layerIndex, useMainMenuProfile), GetMountainLayerValue(EnviroMountainLayerDefaultOpacities, layerIndex, 1f));
    }
    private GuitarBridgeServer.TabsEnviroGroundMode Enviro3GroundModeValue
    {
        get
        {
            if (owner == null)
                return GuitarBridgeServer.TabsEnviroGroundMode.Off;

            return owner.GetCurrentEnviroGroundMode(useMainMenuProfile);
        }
    }
    private bool Enviro3FlatGroundEnabledValue => Enviro3GroundModeValue == GuitarBridgeServer.TabsEnviroGroundMode.FlatPlane
        || Enviro3GroundModeValue == GuitarBridgeServer.TabsEnviroGroundMode.Mountains;
    private bool Enviro3ExtendedGroundEnabledValue => Enviro3GroundModeValue == GuitarBridgeServer.TabsEnviroGroundMode.ExtendedPlane;
    private bool Enviro3GroundVisibleValue => Enviro3FlatGroundEnabledValue || Enviro3ExtendedGroundEnabledValue;
    private bool Enviro3MountainsEnabledValue => Enviro3GroundModeValue == GuitarBridgeServer.TabsEnviroGroundMode.Mountains;
    private bool Enviro3AnyGroundEnabledValue => Enviro3GroundModeValue != GuitarBridgeServer.TabsEnviroGroundMode.Off;
    private bool Enviro3HorizonEnabledValue => owner != null && owner.GetEnviroHorizonEnabled(useMainMenuProfile);
    private bool UseProceduralDomeValue => !UseEnviro3SkyValue;
    private bool DomeMountainsEnabledValue => UseProceduralDomeValue
        && owner != null
        && owner.GetDomeMountainsEnabled(useMainMenuProfile);
    private bool DomeStarsEnabledValue => UseProceduralDomeValue
        && (owner == null || owner.GetDomeStarsEnabled(useMainMenuProfile));
    private int DomeStarsCountValue => Mathf.Clamp(owner != null ? owner.GetDomeStarsCount(useMainMenuProfile) : DomeStarsDefaultCount, 0, 1200);
    private float DomeStarsBrightnessValue => MinFinite(owner != null ? owner.GetDomeStarsBrightness(useMainMenuProfile) : DomeStarsDefaultBrightness, 0f, DomeStarsDefaultBrightness);
    private float DomeStarsTwinkleStrengthValue => Clamp01Finite(owner != null ? owner.GetDomeStarsTwinkleStrength(useMainMenuProfile) : DomeStarsDefaultTwinkleStrength, DomeStarsDefaultTwinkleStrength);
    private float DomeStarsTwinkleSpeedValue => MinFinite(owner != null ? owner.GetDomeStarsTwinkleSpeed(useMainMenuProfile) : DomeStarsDefaultTwinkleSpeed, 0f, DomeStarsDefaultTwinkleSpeed);
    private float DomeStarsSizeValue => MinFinite(owner != null ? owner.GetDomeStarsSize(useMainMenuProfile) : DomeStarsDefaultSize, 0.1f, DomeStarsDefaultSize);
    private int DomeStarsSeedValue => owner != null ? owner.GetDomeStarsSeed(useMainMenuProfile) : DomeStarsDefaultSeed;
    private float HorizonColorStrengthValue => MinFinite(owner != null ? owner.neonHorizonColorStrength : HorizonColorStrength, 0f, HorizonColorStrength);
    private float HorizonColorSaturationValue => MinFinite(owner != null ? owner.neonHorizonColorSaturation : HorizonColorSaturation, 0f, HorizonColorSaturation);
    private float HorizonSoftGlowIntensityValue => MinFinite(owner != null ? owner.neonHorizonSoftGlowIntensity : HorizonSoftGlowIntensity, 0f, HorizonSoftGlowIntensity);
    private float HorizonCoreGlowIntensityValue => MinFinite(owner != null ? owner.neonHorizonCoreGlowIntensity : HorizonCoreGlowIntensity, 0f, HorizonCoreGlowIntensity);
    private float HorizonSoftAlphaValue => Clamp01Finite(owner != null ? owner.neonHorizonSoftAlpha : HorizonSoftAlpha, HorizonSoftAlpha);
    private float HorizonCoreAlphaValue => Clamp01Finite(owner != null ? owner.neonHorizonCoreAlpha : HorizonCoreAlpha, HorizonCoreAlpha);
    private float HorizonSoftBlurFalloffValue => MinFinite(owner != null ? owner.neonHorizonSoftBlurFalloff : HorizonSoftBlurFalloff, 0.001f, HorizonSoftBlurFalloff);
    private float HorizonCoreBlurFalloffValue => MinFinite(owner != null ? owner.neonHorizonCoreBlurFalloff : HorizonCoreBlurFalloff, 0.001f, HorizonCoreBlurFalloff);
    private float HorizonSoftColorFalloffValue => MinFinite(owner != null ? owner.neonHorizonSoftColorFalloff : HorizonSoftColorFalloff, 0.001f, HorizonSoftColorFalloff);
    private float HorizonCoreColorFalloffValue => MinFinite(owner != null ? owner.neonHorizonCoreColorFalloff : HorizonCoreColorFalloff, 0.001f, HorizonCoreColorFalloff);
    private float HorizonCenterBlendWidthValue => MinFinite(owner != null ? owner.neonHorizonCenterBlendWidth : HorizonCenterBlendWidth, 0f, HorizonCenterBlendWidth);
    private float HorizonCenterBlendFalloffValue => MinFinite(owner != null ? owner.neonHorizonCenterBlendFalloff : HorizonCenterBlendFalloff, 0.001f, HorizonCenterBlendFalloff);
    private float HorizonCenterBlendStrengthValue => MinFinite(owner != null ? owner.neonHorizonCenterBlendStrength : HorizonCenterBlendStrength, 0f, HorizonCenterBlendStrength);
    private float HorizonSoftCoreWidthValue => MinFinite(owner != null ? owner.neonHorizonSoftCoreWidth : HorizonSoftCoreWidth, 0.001f, HorizonSoftCoreWidth);
    private float HorizonSoftCoreSoftnessValue => MinFinite(owner != null ? owner.neonHorizonSoftCoreSoftness : HorizonSoftCoreSoftness, 0.001f, HorizonSoftCoreSoftness);
    private float HorizonCoreWidthValue => MinFinite(owner != null ? owner.neonHorizonCoreWidth : HorizonCoreWidth, 0.001f, HorizonCoreWidth);
    private float HorizonCoreSoftnessValue => MinFinite(owner != null ? owner.neonHorizonCoreSoftness : HorizonCoreSoftness, 0.001f, HorizonCoreSoftness);
    private float HorizonSoftShimmerStrengthValue => MinFinite(owner != null ? owner.neonHorizonSoftShimmerStrength : HorizonSoftShimmerStrength, 0f, HorizonSoftShimmerStrength);
    private float HorizonCoreShimmerStrengthValue => MinFinite(owner != null ? owner.neonHorizonCoreShimmerStrength : HorizonCoreShimmerStrength, 0f, HorizonCoreShimmerStrength);
    private float HorizonEdgeBlurStrengthValue => MinFinite(owner != null ? owner.neonHorizonEdgeBlurStrength : HorizonEdgeBlurStrength, 0f, HorizonEdgeBlurStrength);
    private float HorizonEdgeBlurStartValue => Clamp01Finite(owner != null ? owner.neonHorizonEdgeBlurStart : HorizonEdgeBlurStart, HorizonEdgeBlurStart);
    private float HorizonEdgeBlurSharpnessValue => MinFinite(owner != null ? owner.neonHorizonEdgeBlurSharpness : HorizonEdgeBlurSharpness, 0.001f, HorizonEdgeBlurSharpness);
    private float HorizonCurveDownValue => FiniteOr(owner != null ? owner.neonHorizonCurveDown : HorizonCurveDown, HorizonCurveDown);
    private float HorizonCurveTowardCameraValue => FiniteOr(owner != null ? owner.neonHorizonCurveTowardCamera : HorizonCurveTowardCamera, HorizonCurveTowardCamera);

    private sealed class CurvedMeshCache
    {
        public Mesh Mesh;
        public Vector3[] Vertices;
        public Vector2[] Uvs;
        public int[] Triangles;
        public int WidthSegments = -1;
        public int HeightSegments = -1;
        public bool HasAppliedFloorShape;
        public float FloorWidth = float.NaN;
        public float FloorDepth = float.NaN;
        public float FloorCurveDown = float.NaN;
        public float FloorCurveTowardCamera = float.NaN;
        public Vector3 FloorForwardFlat;
        public Quaternion FloorWorldRotation;
    }

    private sealed class StageCloud
    {
        public GameObject Object;
        public Renderer Renderer;
        public Material Material;
        public float BaseX;
        public float BaseY;
        public float Scale;
        public float Speed;
    }

    private struct HorizonPaletteColors
    {
        public Color SoftLeft;
        public Color SoftRight;
        public Color SoftMid;
        public Color CoreLeft;
        public Color CoreRight;
        public Color CoreMid;
        public Color SoftCenter;
        public Color CoreCenter;

        public HorizonPaletteColors(
            Color softLeft,
            Color softRight,
            Color softMid,
            Color coreLeft,
            Color coreRight,
            Color coreMid,
            Color softCenter,
            Color coreCenter)
        {
            SoftLeft = softLeft;
            SoftRight = softRight;
            SoftMid = softMid;
            CoreLeft = coreLeft;
            CoreRight = coreRight;
            CoreMid = coreMid;
            SoftCenter = softCenter;
            CoreCenter = coreCenter;
        }
    }

    private static HorizonPaletteColors GetHorizonPalette(int paletteIndex, bool unifiedSides)
    {
        int palette = Mathf.Clamp(paletteIndex, 0, 10);
        switch (palette)
        {
            case 1:
            {
                Color softPrimary = new Color(2.70f, 0.12f, 0.36f, 1f);
                Color corePrimary = new Color(3.75f, 0.18f, 0.48f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(3.10f, 0.08f, 0.28f, 1f),
                    unifiedSides ? softPrimary : new Color(3.10f, 0.46f, 0.12f, 1f),
                    new Color(1.22f, 0.09f, 0.30f, 1f),
                    unifiedSides ? corePrimary : new Color(4.10f, 0.12f, 0.40f, 1f),
                    unifiedSides ? corePrimary : new Color(4.00f, 0.58f, 0.18f, 1f),
                    new Color(1.62f, 0.16f, 0.34f, 1f),
                    new Color(1.60f, 0.50f, 0.58f, 1f),
                    new Color(3.20f, 1.12f, 1.10f, 1f));
            }
            case 2:
            {
                Color softPrimary = new Color(0.10f, 1.65f, 3.40f, 1f);
                Color corePrimary = new Color(0.18f, 2.65f, 4.80f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(0.08f, 1.35f, 3.20f, 1f),
                    unifiedSides ? softPrimary : new Color(0.10f, 2.20f, 3.55f, 1f),
                    new Color(0.12f, 0.48f, 1.65f, 1f),
                    unifiedSides ? corePrimary : new Color(0.18f, 2.15f, 4.70f, 1f),
                    unifiedSides ? corePrimary : new Color(0.22f, 3.00f, 4.90f, 1f),
                    new Color(0.18f, 0.70f, 2.15f, 1f),
                    new Color(0.82f, 1.25f, 1.90f, 1f),
                    new Color(1.65f, 2.80f, 4.10f, 1f));
            }
            case 3:
            {
                Color softPrimary = new Color(2.85f, 0.05f, 3.95f, 1f);
                Color corePrimary = new Color(4.00f, 0.10f, 5.00f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(3.30f, 0.00f, 4.80f, 1f),
                    unifiedSides ? softPrimary : new Color(0.08f, 2.55f, 4.40f, 1f),
                    new Color(1.26f, 0.12f, 2.20f, 1f),
                    unifiedSides ? corePrimary : new Color(4.10f, 0.00f, 5.20f, 1f),
                    unifiedSides ? corePrimary : new Color(0.10f, 3.00f, 5.00f, 1f),
                    new Color(1.55f, 0.22f, 2.85f, 1f),
                    new Color(1.22f, 0.42f, 2.15f, 1f),
                    new Color(3.20f, 1.35f, 4.60f, 1f));
            }
            case 4:
            {
                Color softPrimary = new Color(2.45f, 0.70f, 0.10f, 1f);
                Color corePrimary = new Color(3.70f, 1.05f, 0.18f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(2.70f, 0.58f, 0.04f, 1f),
                    unifiedSides ? softPrimary : new Color(2.20f, 0.12f, 3.50f, 1f),
                    new Color(1.10f, 0.32f, 0.60f, 1f),
                    unifiedSides ? corePrimary : new Color(4.00f, 1.00f, 0.16f, 1f),
                    unifiedSides ? corePrimary : new Color(3.25f, 0.22f, 4.85f, 1f),
                    new Color(1.50f, 0.50f, 1.10f, 1f),
                    new Color(1.85f, 0.92f, 1.25f, 1f),
                    new Color(4.00f, 2.45f, 2.65f, 1f));
            }
            case 5:
            {
                return new HorizonPaletteColors(
                    new Color(3.20f, 0.00f, 4.70f, 1f),
                    new Color(0.00f, 2.85f, 4.90f, 1f),
                    new Color(0.58f, 0.18f, 1.55f, 1f),
                    new Color(3.60f, 0.00f, 5.00f, 1f),
                    new Color(0.00f, 3.10f, 5.10f, 1f),
                    new Color(0.62f, 0.20f, 1.68f, 1f),
                    new Color(1.35f, 1.08f, 1.80f, 1f),
                    new Color(3.35f, 3.00f, 4.15f, 1f));
            }
            case 6:
            {
                Color softPrimary = new Color(2.75f, 0.78f, 0.16f, 1f);
                Color corePrimary = new Color(4.10f, 1.30f, 0.28f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(3.05f, 0.30f, 0.10f, 1f),
                    unifiedSides ? softPrimary : new Color(2.45f, 0.82f, 0.18f, 1f),
                    new Color(1.16f, 0.34f, 0.42f, 1f),
                    unifiedSides ? corePrimary : new Color(4.35f, 0.42f, 0.18f, 1f),
                    unifiedSides ? corePrimary : new Color(3.95f, 1.35f, 0.26f, 1f),
                    new Color(1.55f, 0.52f, 0.70f, 1f),
                    new Color(1.95f, 0.82f, 0.78f, 1f),
                    new Color(4.10f, 2.20f, 1.35f, 1f));
            }
            case 7:
            {
                Color softPrimary = new Color(0.04f, 2.10f, 1.28f, 1f);
                Color corePrimary = new Color(0.08f, 3.10f, 1.90f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(0.04f, 1.95f, 1.10f, 1f),
                    unifiedSides ? softPrimary : new Color(0.10f, 2.50f, 1.95f, 1f),
                    new Color(0.06f, 0.80f, 0.52f, 1f),
                    unifiedSides ? corePrimary : new Color(0.07f, 2.95f, 1.55f, 1f),
                    unifiedSides ? corePrimary : new Color(0.16f, 3.35f, 2.35f, 1f),
                    new Color(0.10f, 1.08f, 0.75f, 1f),
                    new Color(0.72f, 1.45f, 1.18f, 1f),
                    new Color(1.55f, 3.25f, 2.35f, 1f));
            }
            case 8:
            {
                Color softPrimary = new Color(0.08f, 0.92f, 2.65f, 1f);
                Color corePrimary = new Color(0.16f, 1.65f, 4.10f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(0.04f, 0.58f, 2.20f, 1f),
                    unifiedSides ? softPrimary : new Color(0.10f, 1.40f, 2.95f, 1f),
                    new Color(0.08f, 0.30f, 1.20f, 1f),
                    unifiedSides ? corePrimary : new Color(0.12f, 1.05f, 3.70f, 1f),
                    unifiedSides ? corePrimary : new Color(0.18f, 2.05f, 4.50f, 1f),
                    new Color(0.12f, 0.44f, 1.58f, 1f),
                    new Color(0.74f, 1.05f, 1.65f, 1f),
                    new Color(1.45f, 2.35f, 4.20f, 1f));
            }
            case 9:
            {
                Color softPrimary = new Color(0.28f, 1.15f, 0.80f, 1f);
                Color corePrimary = new Color(0.55f, 1.90f, 1.30f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(0.08f, 0.82f, 0.58f, 1f),
                    unifiedSides ? softPrimary : new Color(1.45f, 1.22f, 0.70f, 1f),
                    new Color(0.35f, 0.78f, 0.52f, 1f),
                    unifiedSides ? corePrimary : new Color(0.16f, 1.45f, 0.98f, 1f),
                    unifiedSides ? corePrimary : new Color(2.10f, 1.82f, 1.00f, 1f),
                    new Color(0.64f, 1.16f, 0.72f, 1f),
                    new Color(1.65f, 1.54f, 1.05f, 1f),
                    new Color(3.25f, 2.95f, 1.82f, 1f));
            }
            case 10:
            {
                Color softPrimary = new Color(0.28f, 1.05f, 3.20f, 1f);
                Color corePrimary = new Color(0.72f, 2.05f, 4.90f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(0.16f, 0.72f, 2.95f, 1f),
                    unifiedSides ? softPrimary : new Color(0.54f, 1.42f, 3.60f, 1f),
                    new Color(0.28f, 0.62f, 1.95f, 1f),
                    unifiedSides ? corePrimary : new Color(0.38f, 1.42f, 4.70f, 1f),
                    unifiedSides ? corePrimary : new Color(1.15f, 2.35f, 5.10f, 1f),
                    new Color(0.55f, 1.05f, 2.65f, 1f),
                    new Color(1.42f, 2.45f, 3.80f, 1f),
                    new Color(2.65f, 3.95f, 5.35f, 1f));
            }
            default:
            {
                Color softPrimary = new Color(2.40f, 0.18f, 4.25f, 1f);
                Color corePrimary = new Color(3.10f, 0.26f, 5.10f, 1f);
                return new HorizonPaletteColors(
                    unifiedSides ? softPrimary : new Color(3.20f, 0.00f, 4.70f, 1f),
                    unifiedSides ? softPrimary : new Color(0.00f, 2.85f, 4.90f, 1f),
                    unifiedSides ? new Color(1.18f, 0.28f, 2.25f, 1f) : new Color(0.58f, 0.18f, 1.55f, 1f),
                    unifiedSides ? corePrimary : new Color(3.60f, 0.00f, 5.00f, 1f),
                    unifiedSides ? corePrimary : new Color(0.00f, 3.10f, 5.10f, 1f),
                    unifiedSides ? new Color(1.40f, 0.34f, 2.58f, 1f) : new Color(0.62f, 0.20f, 1.68f, 1f),
                    new Color(1.35f, 1.08f, 1.80f, 1f),
                    new Color(3.35f, 3.00f, 4.15f, 1f));
            }
        }
    }

    private static void GetSkyPalette(int paletteIndex, bool unifiedSides, out Color leftColor, out Color rightColor, out Color horizonColor)
    {
        int palette = Mathf.Clamp(paletteIndex, 0, 12);
        switch (palette)
        {
            case 1:
                leftColor = unifiedSides ? new Color(0.22f, 0.020f, 0.070f, 1f) : new Color(0.40f, 0.020f, 0.070f, 1f);
                rightColor = unifiedSides ? new Color(0.22f, 0.020f, 0.070f, 1f) : new Color(0.18f, 0.035f, 0.220f, 1f);
                horizonColor = new Color(0.72f, 0.10f, 0.28f, 1f);
                return;
            case 2:
                leftColor = unifiedSides ? new Color(0.015f, 0.155f, 0.300f, 1f) : new Color(0.010f, 0.110f, 0.330f, 1f);
                rightColor = unifiedSides ? new Color(0.015f, 0.155f, 0.300f, 1f) : new Color(0.010f, 0.260f, 0.350f, 1f);
                horizonColor = new Color(0.08f, 0.62f, 1.00f, 1f);
                return;
            case 3:
                leftColor = unifiedSides ? new Color(0.125f, 0.030f, 0.240f, 1f) : new Color(0.36f, 0.025f, 0.390f, 1f);
                rightColor = unifiedSides ? new Color(0.125f, 0.030f, 0.240f, 1f) : new Color(0.030f, 0.260f, 0.480f, 1f);
                horizonColor = new Color(0.48f, 0.16f, 0.92f, 1f);
                return;
            case 4:
                leftColor = unifiedSides ? new Color(0.165f, 0.070f, 0.015f, 1f) : new Color(0.28f, 0.110f, 0.015f, 1f);
                rightColor = unifiedSides ? new Color(0.165f, 0.070f, 0.015f, 1f) : new Color(0.090f, 0.035f, 0.260f, 1f);
                horizonColor = new Color(0.72f, 0.34f, 0.66f, 1f);
                return;
            case 5:
                leftColor = new Color(0.48f, 0.05f, 0.58f, 1f);
                rightColor = new Color(0.03f, 0.28f, 0.72f, 1f);
                horizonColor = new Color(0.30f, 0.25f, 0.88f, 1f);
                return;
            case 6:
                leftColor = new Color(0.150f, 0.055f, 0.260f, 1f);
                rightColor = new Color(0.040f, 0.155f, 0.330f, 1f);
                horizonColor = new Color(0.28f, 0.22f, 0.72f, 1f);
                return;
            case 7:
                leftColor = new Color(0.080f, 0.150f, 0.190f, 1f);
                rightColor = new Color(0.150f, 0.070f, 0.205f, 1f);
                horizonColor = new Color(0.18f, 0.34f, 0.55f, 1f);
                return;
            case 8:
                leftColor = new Color(0.190f, 0.060f, 0.155f, 1f);
                rightColor = new Color(0.070f, 0.100f, 0.285f, 1f);
                horizonColor = new Color(0.34f, 0.17f, 0.52f, 1f);
                return;
            case 9:
                leftColor = unifiedSides ? new Color(0.170f, 0.055f, 0.030f, 1f) : new Color(0.280f, 0.065f, 0.030f, 1f);
                rightColor = unifiedSides ? new Color(0.170f, 0.055f, 0.030f, 1f) : new Color(0.110f, 0.035f, 0.230f, 1f);
                horizonColor = new Color(0.88f, 0.34f, 0.30f, 1f);
                return;
            case 10:
                leftColor = unifiedSides ? new Color(0.012f, 0.160f, 0.115f, 1f) : new Color(0.016f, 0.190f, 0.105f, 1f);
                rightColor = unifiedSides ? new Color(0.012f, 0.160f, 0.115f, 1f) : new Color(0.016f, 0.105f, 0.195f, 1f);
                horizonColor = new Color(0.10f, 0.72f, 0.50f, 1f);
                return;
            case 11:
                leftColor = unifiedSides ? new Color(0.015f, 0.090f, 0.070f, 1f) : new Color(0.012f, 0.120f, 0.085f, 1f);
                rightColor = unifiedSides ? new Color(0.015f, 0.090f, 0.070f, 1f) : new Color(0.185f, 0.165f, 0.095f, 1f);
                horizonColor = new Color(0.74f, 0.70f, 0.48f, 1f);
                return;
            case 12:
                leftColor = unifiedSides ? new Color(0.018f, 0.070f, 0.210f, 1f) : new Color(0.018f, 0.095f, 0.310f, 1f);
                rightColor = unifiedSides ? new Color(0.018f, 0.070f, 0.210f, 1f) : new Color(0.115f, 0.255f, 0.560f, 1f);
                horizonColor = new Color(0.58f, 0.82f, 1.00f, 1f);
                return;
            default:
                leftColor = unifiedSides ? new Color(0.025f, 0.115f, 0.360f, 1f) : new Color(0.48f, 0.05f, 0.58f, 1f);
                rightColor = unifiedSides ? new Color(0.025f, 0.115f, 0.360f, 1f) : new Color(0.03f, 0.28f, 0.72f, 1f);
                horizonColor = unifiedSides ? new Color(0.28f, 0.20f, 0.88f, 1f) : new Color(0.30f, 0.25f, 0.88f, 1f);
                return;
        }
    }

    public void Initialize(Transform parent, GuitarBridgeServer owner)
    {
        this.owner = owner;

        root = new GameObject("TabsNeonStageBackground");
        root.transform.SetParent(parent, false);
        TabsNeonStageBackgroundCleanupHook cleanupHook = root.AddComponent<TabsNeonStageBackgroundCleanupHook>();
        cleanupHook.Initialize(this);

        domeObject = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        domeObject.name = "NeonStageDome";
        domeObject.transform.SetParent(root.transform, false);
        Object.Destroy(domeObject.GetComponent<Collider>());
        domeRenderer = ConfigureRenderer(domeObject);
        domeMaterial = CreateMaterial(renderQueue: (int)RenderQueue.Background + 8);
        if (domeRenderer != null)
            domeRenderer.sharedMaterial = domeMaterial;

        CreateDomeStars();

        if (applyHighwayOverrides)
            CreateStageGeometry();

        ApplyVisualState();
        UpdatePlacement();
    }

    public void SetRenderCamera(Camera camera)
    {
        renderCameraOverride = camera;
    }

    public void Tick(float deltaTime)
    {
        if (root == null || owner == null)
            return;

        using (TickProfilerMarker.Auto())
        {
            using (ApplyVisualStateProfilerMarker.Auto())
            {
                ApplyVisualState();
            }

            UpdatePlacement();
        }
    }

    public void Dispose()
    {
        DestroyEnviro3Sky();
        RestoreSkyboxOverride();

        if (domeMaterial != null)
            Object.Destroy(domeMaterial);
        if (domeStarsMaterial != null)
            Object.Destroy(domeStarsMaterial);
        if (domeStarsMesh != null)
            Object.Destroy(domeStarsMesh);
        if (floorMaterial != null)
            Object.Destroy(floorMaterial);
        if (floorGridMaterial != null)
            Object.Destroy(floorGridMaterial);
        if (horizonMaterial != null)
            Object.Destroy(horizonMaterial);
        if (horizonCoreMaterial != null)
            Object.Destroy(horizonCoreMaterial);
        if (enviroMountainLayerMaterials.Count > 0)
        {
            foreach (Material mountainMaterial in enviroMountainLayerMaterials)
            {
                if (mountainMaterial != null)
                    Object.Destroy(mountainMaterial);
            }
        }
        else if (enviroMountainMaterial != null)
        {
            Object.Destroy(enviroMountainMaterial);
        }
        if (enviroMountainSilhouetteMeshes.Count > 0)
        {
            foreach (Mesh mountainMesh in enviroMountainSilhouetteMeshes)
            {
                if (mountainMesh != null)
                    Object.Destroy(mountainMesh);
            }
        }
        else if (enviroMountainSilhouetteMesh != null)
        {
            Object.Destroy(enviroMountainSilhouetteMesh);
        }
        foreach (Material farLightMaterial in farLightMaterials)
        {
            if (farLightMaterial != null)
                Object.Destroy(farLightMaterial);
        }
        foreach (StageCloud cloud in stageClouds)
        {
            if (cloud.Material != null)
                Object.Destroy(cloud.Material);
        }

        foreach (Texture2D texture in ownedTextures)
        {
            if (texture != null)
                Object.Destroy(texture);
        }

        ownedTextures.Clear();
        farLightMaterials.Clear();
        farLightRenderers.Clear();
        farLightObjects.Clear();
        enviroMountainTerrains.Clear();
        enviroMountainRenderers.Clear();
        enviroMountainSilhouetteMeshes.Clear();
        enviroMountainLayerMaterials.Clear();
        stageClouds.Clear();
        if (root != null)
            Object.Destroy(root);

        DestroyMeshCache(floorMeshCache);
        DestroyMeshCache(floorGridMeshCache);
        DestroyMeshCache(horizonMeshCache);
        DestroyMeshCache(horizonCoreMeshCache);

        domeMaterial = null;
        domeStarsMaterial = null;
        domeStarsMesh = null;
        floorMaterial = null;
        floorGridMaterial = null;
        horizonMaterial = null;
        horizonCoreMaterial = null;
        domeRenderer = null;
        domeStarsRenderer = null;
        floorRenderer = null;
        floorGridRenderer = null;
        floorTexture = null;
        floorGridTexture = null;
        horizonRenderer = null;
        horizonCoreRenderer = null;
        enviroMountainPrefab = null;
        enviroMountainObject = null;
        enviroMountainMaterial = null;
        enviroMountainSilhouetteMesh = null;
        enviroMountainSilhouetteMeshFilter = null;
        enviroMountainSilhouetteRenderer = null;
        originalRenderSettingsSkybox = null;
        originalRenderSettingsSun = null;
        skyboxCamera = null;
        skyboxOverrideApplied = false;
        originalRenderSettingsStateCaptured = false;
        farLightTexture = null;
        stageCloudTexture = null;
        domeObject = null;
        domeStarsObject = null;
        floorObject = null;
        floorGridObject = null;
        horizonObject = null;
        horizonCoreObject = null;
        enviroMountainObject = null;
        root = null;
        owner = null;
        cachedDomeStarsCount = -1;
        cachedDomeStarsSeed = int.MinValue;
        cachedDomeStarsSize = -1f;
        proceduralDomeVisible = false;
    }

    private void ApplyVisualState()
    {
        RefreshFloorTexturesIfNeeded();

        bool unifiedSides = UseUnifiedSideColorsValue;
        GetSkyPalette(SkyLineColorPaletteValue, unifiedSides, out Color skyLeftColor, out Color skyRightColor, out Color skyHorizonColor);

        ApplyMaterialState(
            domeMaterial,
            new Color(0.002f, 0.005f, 0.020f, 1f),
            skyHorizonColor,
            skyLeftColor,
            skyRightColor,
            horizonStrength: 0.92f,
            vignetteStrength: 1.0f,
            skyLineStrength: SkyLineStrengthValue,
            skyLineOpacity: SkyLineOpacityValue,
            skyLineReflectionStrength: SkyLineReflectionStrengthValue,
            skyDotStrength: SkyDotStrengthValue,
            skySideWashStrength: SkySideWashStrengthValue,
            skyCoreBrightness: SkyCoreBrightnessValue,
            skyCoreSize: SkyCoreSizeValue,
            skyCoreHeight: SkyCoreHeightValue,
            skyCoreXOffset: SkyCoreXOffsetValue,
            skyCoreFalloff: SkyCoreFalloffValue,
            skyOutsideDarkness: SkyOutsideDarknessValue,
            skyCorePurpleStrength: SkyCorePurpleStrengthValue,
            skyCorePurpleFalloff: SkyCorePurpleFalloffValue,
            skyAuroraRidgeStrength: SkyAuroraRidgeStrengthValue,
            skyAuroraRidgeWhiteFalloffPosition: SkyAuroraRidgeWhiteFalloffPositionValue,
            skyAuroraRidgeWhiteFalloffSharpness: SkyAuroraRidgeWhiteFalloffSharpnessValue,
            skyAuroraWaveBumpiness: SkyAuroraWaveBumpinessValue,
            skyLineStyle: SkyLineStyleValue);
        UpdateDomeStarsMeshIfNeeded();
        ApplyDomeStarsMaterialState();

        float stageTime = Mathf.Repeat(Time.unscaledTime, 4096f);
        float horizonColorStrength = HorizonColorStrengthValue;
        float horizonWhiteStrength = Mathf.Lerp(1f, Mathf.Min(horizonColorStrength, 1.6f), 0.35f);
        HorizonPaletteColors horizonPalette = GetHorizonPalette(HorizonColorPaletteValue, unifiedSides);

        ApplyTransparentMaterialColor(
            floorMaterial,
            new Color(1.00f, 1.00f, 1.00f, 1.00f));

        ApplyTransparentMaterialColor(
            floorGridMaterial,
            new Color(1.00f, 1.00f, 1.00f, FloorGridOpacity));

        ApplyTransparentMaterialColor(
            enviroMountainMaterial,
            new Color(0.003f, 0.004f, 0.008f, 1.00f));

        ApplyHorizonMaterialState(
            horizonMaterial,
            horizonPalette.SoftLeft * horizonColorStrength,
            horizonPalette.SoftRight * horizonColorStrength,
            horizonPalette.SoftMid * horizonColorStrength,
            horizonPalette.SoftCenter * horizonWhiteStrength,
            intensity: 0.48f * HorizonSoftGlowIntensityValue,
            coreStrength: 0.00f,
            verticalSharpness: HorizonSoftBlurFalloffValue,
            horizontalSharpness: HorizonSoftColorFalloffValue,
            coreWidth: HorizonSoftCoreWidthValue,
            coreSoftness: HorizonSoftCoreSoftnessValue,
            shimmerStrength: HorizonSoftShimmerStrengthValue,
            alpha: HorizonSoftAlphaValue,
            centerBlendWidth: HorizonCenterBlendWidthValue,
            centerBlendFalloff: HorizonCenterBlendFalloffValue,
            centerBlendStrength: HorizonCenterBlendStrengthValue,
            colorSaturation: HorizonColorSaturationValue,
            edgeBlurStrength: HorizonEdgeBlurStrengthValue,
            edgeBlurStart: HorizonEdgeBlurStartValue,
            edgeBlurSharpness: HorizonEdgeBlurSharpnessValue,
            stageTime: stageTime);

        ApplyHorizonMaterialState(
            horizonCoreMaterial,
            horizonPalette.CoreLeft * horizonColorStrength,
            horizonPalette.CoreRight * horizonColorStrength,
            horizonPalette.CoreMid * horizonColorStrength,
            horizonPalette.CoreCenter * horizonWhiteStrength,
            intensity: 0.68f * HorizonCoreGlowIntensityValue,
            coreStrength: 0.68f,
            verticalSharpness: HorizonCoreBlurFalloffValue,
            horizontalSharpness: HorizonCoreColorFalloffValue,
            coreWidth: HorizonCoreWidthValue,
            coreSoftness: HorizonCoreSoftnessValue,
            shimmerStrength: HorizonCoreShimmerStrengthValue,
            alpha: HorizonCoreAlphaValue,
            centerBlendWidth: HorizonCenterBlendWidthValue,
            centerBlendFalloff: HorizonCenterBlendFalloffValue,
            centerBlendStrength: HorizonCenterBlendStrengthValue,
            colorSaturation: HorizonColorSaturationValue,
            edgeBlurStrength: HorizonEdgeBlurStrengthValue,
            edgeBlurStart: HorizonEdgeBlurStartValue,
            edgeBlurSharpness: HorizonEdgeBlurSharpnessValue,
            stageTime: stageTime);

        ApplyFarLightMaterialState();
        ApplyStageCloudMaterialState();
    }

    private void ApplyFarLightMaterialState()
    {
        if (farLightMaterials.Count == 0)
            return;

        for (int i = 0; i < farLightMaterials.Count; i++)
        {
            Color color = FarLightColors[i % FarLightColors.Length];
            ApplyTransparentMaterialColor(farLightMaterials[i], color);
        }
    }

    private void ApplyStageCloudMaterialState()
    {
        bool visible = StageCloudsEnabledValue && StageCloudOpacityValue > 0.001f;
        GetSkyPalette(SkyLineColorPaletteValue, UseUnifiedSideColorsValue, out Color skyLeft, out Color skyRight, out Color skyHorizon);

        for (int i = 0; i < stageClouds.Count; i++)
        {
            StageCloud cloud = stageClouds[i];
            if (cloud.Renderer != null)
                cloud.Renderer.enabled = visible;
            if (cloud.Material == null)
                continue;

            float side = Mathf.InverseLerp(-0.5f, 0.5f, cloud.BaseX);
            Color sideTint = Color.Lerp(skyLeft, skyRight, side);
            Color darkBase = new Color(0.006f, 0.009f, 0.026f, 1f);
            Color themeTint = Color.Lerp(sideTint, skyHorizon, 0.22f);
            Color tint = Color.Lerp(darkBase, themeTint, 0.12f);
            tint.r *= 0.22f;
            tint.g *= 0.24f;
            tint.b *= 0.34f;
            tint.a = Mathf.Clamp(StageCloudOpacityValue * 0.18f, 0f, 0.11f);
            ApplyTransparentMaterialColor(cloud.Material, tint);
        }
    }

    private static void ApplyMaterialState(
        Material material,
        Color baseColor,
        Color horizonColor,
        Color leftAccentColor,
        Color rightAccentColor,
        float horizonStrength,
        float vignetteStrength,
        float skyLineStrength,
        float skyLineOpacity,
        float skyLineReflectionStrength,
        float skyDotStrength,
        float skySideWashStrength,
        float skyCoreBrightness,
        float skyCoreSize,
        float skyCoreHeight,
        float skyCoreXOffset,
        float skyCoreFalloff,
        float skyOutsideDarkness,
        float skyCorePurpleStrength,
        float skyCorePurpleFalloff,
        float skyAuroraRidgeStrength,
        float skyAuroraRidgeWhiteFalloffPosition,
        float skyAuroraRidgeWhiteFalloffSharpness,
        float skyAuroraWaveBumpiness,
        int skyLineStyle)
    {
        if (material == null)
            return;

        if (material.HasProperty(BaseColorShaderId))
            material.SetColor(BaseColorShaderId, baseColor);
        if (material.HasProperty(HorizonColorShaderId))
            material.SetColor(HorizonColorShaderId, horizonColor);
        if (material.HasProperty(LeftAccentColorShaderId))
            material.SetColor(LeftAccentColorShaderId, leftAccentColor);
        if (material.HasProperty(RightAccentColorShaderId))
            material.SetColor(RightAccentColorShaderId, rightAccentColor);
        if (material.HasProperty(PulseSpeedShaderId))
            material.SetFloat(PulseSpeedShaderId, 0.72f);
        if (material.HasProperty(HorizonStrengthShaderId))
            material.SetFloat(HorizonStrengthShaderId, horizonStrength);
        if (material.HasProperty(VignetteStrengthShaderId))
            material.SetFloat(VignetteStrengthShaderId, vignetteStrength);
        if (material.HasProperty(StageTimeShaderId))
            material.SetFloat(StageTimeShaderId, Mathf.Repeat(Time.unscaledTime, 4096f));
        if (material.HasProperty(SkyLineStyleShaderId))
            material.SetFloat(SkyLineStyleShaderId, skyLineStyle);
        if (material.HasProperty(SkyLineStrengthShaderId))
            material.SetFloat(SkyLineStrengthShaderId, skyLineStrength);
        if (material.HasProperty(SkyLineOpacityShaderId))
            material.SetFloat(SkyLineOpacityShaderId, Mathf.Clamp01(skyLineOpacity));
        if (material.HasProperty(SkyLineReflectionStrengthShaderId))
            material.SetFloat(SkyLineReflectionStrengthShaderId, Mathf.Max(0f, skyLineReflectionStrength));
        if (material.HasProperty(SkyDotStrengthShaderId))
            material.SetFloat(SkyDotStrengthShaderId, skyDotStrength);
        if (material.HasProperty(SkySideWashStrengthShaderId))
            material.SetFloat(SkySideWashStrengthShaderId, skySideWashStrength);
        if (material.HasProperty(SkyCoreBrightnessShaderId))
            material.SetFloat(SkyCoreBrightnessShaderId, skyCoreBrightness);
        if (material.HasProperty(SkyCoreSizeShaderId))
            material.SetFloat(SkyCoreSizeShaderId, skyCoreSize);
        if (material.HasProperty(SkyCoreHeightShaderId))
            material.SetFloat(SkyCoreHeightShaderId, skyCoreHeight);
        if (material.HasProperty(SkyCoreXOffsetShaderId))
            material.SetFloat(SkyCoreXOffsetShaderId, skyCoreXOffset);
        if (material.HasProperty(SkyCoreFalloffShaderId))
            material.SetFloat(SkyCoreFalloffShaderId, skyCoreFalloff);
        if (material.HasProperty(SkyOutsideDarknessShaderId))
            material.SetFloat(SkyOutsideDarknessShaderId, skyOutsideDarkness);
        if (material.HasProperty(SkyCorePurpleStrengthShaderId))
            material.SetFloat(SkyCorePurpleStrengthShaderId, skyCorePurpleStrength);
        if (material.HasProperty(SkyCorePurpleFalloffShaderId))
            material.SetFloat(SkyCorePurpleFalloffShaderId, skyCorePurpleFalloff);
        if (material.HasProperty(SkyAuroraRidgeStrengthShaderId))
            material.SetFloat(SkyAuroraRidgeStrengthShaderId, skyAuroraRidgeStrength);
        if (material.HasProperty(SkyAuroraRidgeWhiteFalloffPositionShaderId))
            material.SetFloat(SkyAuroraRidgeWhiteFalloffPositionShaderId, skyAuroraRidgeWhiteFalloffPosition);
        if (material.HasProperty(SkyAuroraRidgeWhiteFalloffSharpnessShaderId))
            material.SetFloat(SkyAuroraRidgeWhiteFalloffSharpnessShaderId, skyAuroraRidgeWhiteFalloffSharpness);
        if (material.HasProperty(SkyAuroraWaveBumpinessShaderId))
            material.SetFloat(SkyAuroraWaveBumpinessShaderId, skyAuroraWaveBumpiness);
    }

    private static void ApplyTransparentMaterialColor(Material material, Color color)
    {
        if (material == null)
            return;

        material.color = color;
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", color);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", color);
    }

    private static void ApplyHorizonMaterialState(
        Material material,
        Color leftColor,
        Color rightColor,
        Color midColor,
        Color centerColor,
        float intensity,
        float coreStrength,
        float verticalSharpness,
        float horizontalSharpness,
        float coreWidth,
        float coreSoftness,
        float shimmerStrength,
        float alpha,
        float centerBlendWidth,
        float centerBlendFalloff,
        float centerBlendStrength,
        float colorSaturation,
        float edgeBlurStrength,
        float edgeBlurStart,
        float edgeBlurSharpness,
        float stageTime)
    {
        if (material == null)
            return;

        if (material.HasProperty(LeftColorShaderId))
            material.SetColor(LeftColorShaderId, leftColor);
        if (material.HasProperty(RightColorShaderId))
            material.SetColor(RightColorShaderId, rightColor);
        if (material.HasProperty(MidColorShaderId))
            material.SetColor(MidColorShaderId, midColor);
        if (material.HasProperty(CenterColorShaderId))
            material.SetColor(CenterColorShaderId, centerColor);
        if (material.HasProperty(IntensityShaderId))
            material.SetFloat(IntensityShaderId, intensity);
        if (material.HasProperty(CoreStrengthShaderId))
            material.SetFloat(CoreStrengthShaderId, coreStrength);
        if (material.HasProperty(VerticalSharpnessShaderId))
            material.SetFloat(VerticalSharpnessShaderId, verticalSharpness);
        if (material.HasProperty(HorizontalSharpnessShaderId))
            material.SetFloat(HorizontalSharpnessShaderId, horizontalSharpness);
        if (material.HasProperty(CoreWidthShaderId))
            material.SetFloat(CoreWidthShaderId, coreWidth);
        if (material.HasProperty(CoreSoftnessShaderId))
            material.SetFloat(CoreSoftnessShaderId, coreSoftness);
        if (material.HasProperty(ShimmerStrengthShaderId))
            material.SetFloat(ShimmerStrengthShaderId, shimmerStrength);
        if (material.HasProperty(AlphaShaderId))
            material.SetFloat(AlphaShaderId, alpha);
        if (material.HasProperty(CenterBlendWidthShaderId))
            material.SetFloat(CenterBlendWidthShaderId, centerBlendWidth);
        if (material.HasProperty(CenterBlendFalloffShaderId))
            material.SetFloat(CenterBlendFalloffShaderId, centerBlendFalloff);
        if (material.HasProperty(CenterBlendStrengthShaderId))
            material.SetFloat(CenterBlendStrengthShaderId, centerBlendStrength);
        if (material.HasProperty(ColorSaturationShaderId))
            material.SetFloat(ColorSaturationShaderId, colorSaturation);
        if (material.HasProperty(EdgeBlurStrengthShaderId))
            material.SetFloat(EdgeBlurStrengthShaderId, edgeBlurStrength);
        if (material.HasProperty(EdgeBlurStartShaderId))
            material.SetFloat(EdgeBlurStartShaderId, edgeBlurStart);
        if (material.HasProperty(EdgeBlurSharpnessShaderId))
            material.SetFloat(EdgeBlurSharpnessShaderId, edgeBlurSharpness);
        if (material.HasProperty(StageTimeShaderId))
            material.SetFloat(StageTimeShaderId, stageTime);
    }

    private void UpdatePlacement()
    {
        using (UpdatePlacementProfilerMarker.Auto())
        {
            Camera camera = renderCameraOverride != null ? renderCameraOverride : Camera.main;
            if (camera == null)
                return;

            using (UpdateSkyDesignProfilerMarker.Auto())
            {
                UpdateSkyDesign(camera);
            }

            UpdateDomePlacement(camera);
            if (applyHighwayOverrides)
            {
                using (UpdateStagePlacementProfilerMarker.Auto())
                {
                    UpdateStagePlacement(camera);
                }
            }
        }
    }

    private void UpdateSkyDesign(Camera camera)
    {
        bool wantsEnviro3Sky = UseEnviro3SkyValue;
        int enviro3MoodIndex = Enviro3MoodIndexValue;
        bool useEnviro3Sky = false;
        if (wantsEnviro3Sky)
        {
            CaptureSkyboxOverrideState(camera);
            useEnviro3Sky = EnsureEnviro3Sky(camera);
        }

        bool useEnviro3Ground = useEnviro3Sky && applyHighwayOverrides && Enviro3GroundVisibleValue;
        bool useEnviro3Mountains = useEnviro3Sky && applyHighwayOverrides && Enviro3MountainsEnabledValue;
        if (useEnviro3Mountains)
            useEnviro3Mountains = EnsureEnviroMountains();
        bool useEnviro3Horizon = useEnviro3Sky && applyHighwayOverrides && Enviro3AnyGroundEnabledValue && Enviro3HorizonEnabledValue;
        bool useDomeMountains = !useEnviro3Sky && applyHighwayOverrides && DomeMountainsEnabledValue;
        if (useDomeMountains)
            useDomeMountains = EnsureEnviroMountains();

        if (useEnviro3Sky)
        {
            ApplyEnviro3Sky(camera, enviro3MoodIndex);
        }
        else
        {
            DestroyEnviro3Sky();
            RestoreSkyboxOverride();
            if (wantsEnviro3Sky && !warnedMissingEnviro3Prefab)
            {
                warnedMissingEnviro3Prefab = true;
                Debug.LogWarning("Enviro 3 sky prefab could not be loaded from Resources/Enviro3.");
            }
        }

        SetDomeVisible(!useEnviro3Sky);
        SetProceduralStageRenderMode(useEnviro3Sky && (useEnviro3Ground || useEnviro3Horizon || useEnviro3Mountains));
        if (useEnviro3Sky)
            SetProceduralStageVisible(useEnviro3Ground, useEnviro3Horizon, false, useEnviro3Mountains);
        else
            SetProceduralStageVisible(true, true, true, useDomeMountains);
    }

    private void CaptureSkyboxOverrideState(Camera camera)
    {
        if (!skyboxOverrideApplied)
        {
            CaptureSkyboxRenderSettings();
            skyboxOverrideApplied = true;
        }

        if (camera == null)
            return;

        if (skyboxCamera != camera || !originalCameraStateCaptured)
        {
            RestoreCameraStateOnly();

            skyboxCamera = camera;
            originalCameraClearFlags = camera.clearFlags;
            originalCameraBackgroundColor = camera.backgroundColor;
            originalCameraStateCaptured = true;
        }
    }

    private void RestoreSkyboxOverride()
    {
        if (skyboxOverrideApplied)
        {
            RestoreSkyboxRenderSettings();
            skyboxOverrideApplied = false;
        }

        RestoreCameraStateOnly();
    }

    private void CaptureSkyboxRenderSettings()
    {
        if (originalRenderSettingsStateCaptured)
            return;

        originalRenderSettingsSkybox = RenderSettings.skybox;
        originalRenderSettingsSun = RenderSettings.sun;
        originalFog = RenderSettings.fog;
        originalFogColor = RenderSettings.fogColor;
        originalFogMode = RenderSettings.fogMode;
        originalFogDensity = RenderSettings.fogDensity;
        originalFogStartDistance = RenderSettings.fogStartDistance;
        originalFogEndDistance = RenderSettings.fogEndDistance;
        originalAmbientMode = RenderSettings.ambientMode;
        originalAmbientSkyColor = RenderSettings.ambientSkyColor;
        originalAmbientEquatorColor = RenderSettings.ambientEquatorColor;
        originalAmbientGroundColor = RenderSettings.ambientGroundColor;
        originalAmbientIntensity = RenderSettings.ambientIntensity;
        originalSubtractiveShadowColor = RenderSettings.subtractiveShadowColor;
        originalDefaultReflectionMode = RenderSettings.defaultReflectionMode;
        originalDefaultReflectionResolution = RenderSettings.defaultReflectionResolution;
        originalReflectionBounces = RenderSettings.reflectionBounces;
        originalReflectionIntensity = RenderSettings.reflectionIntensity;
        originalRenderSettingsStateCaptured = true;
    }

    private void RestoreSkyboxRenderSettings()
    {
        if (!originalRenderSettingsStateCaptured)
            return;

        RenderSettings.skybox = originalRenderSettingsSkybox;
        RenderSettings.sun = originalRenderSettingsSun;
        RenderSettings.fog = originalFog;
        RenderSettings.fogColor = originalFogColor;
        RenderSettings.fogMode = originalFogMode;
        RenderSettings.fogDensity = originalFogDensity;
        RenderSettings.fogStartDistance = originalFogStartDistance;
        RenderSettings.fogEndDistance = originalFogEndDistance;
        RenderSettings.ambientMode = originalAmbientMode;
        RenderSettings.ambientSkyColor = originalAmbientSkyColor;
        RenderSettings.ambientEquatorColor = originalAmbientEquatorColor;
        RenderSettings.ambientGroundColor = originalAmbientGroundColor;
        RenderSettings.ambientIntensity = originalAmbientIntensity;
        RenderSettings.subtractiveShadowColor = originalSubtractiveShadowColor;
        RenderSettings.defaultReflectionMode = originalDefaultReflectionMode;
        RenderSettings.defaultReflectionResolution = originalDefaultReflectionResolution;
        RenderSettings.reflectionBounces = originalReflectionBounces;
        RenderSettings.reflectionIntensity = originalReflectionIntensity;

        originalRenderSettingsStateCaptured = false;
        originalRenderSettingsSkybox = null;
        originalRenderSettingsSun = null;
        ResetEnviro3RenderSettingsCache();
    }

    private void RestoreCameraStateOnly()
    {
        if (skyboxCamera != null && originalCameraStateCaptured)
        {
            skyboxCamera.clearFlags = originalCameraClearFlags;
            skyboxCamera.backgroundColor = originalCameraBackgroundColor;
        }

        skyboxCamera = null;
        originalCameraStateCaptured = false;
    }

    private void RestoreRuntimeRenderStateFromHook()
    {
        RestoreSkyboxOverride();
    }

    private void SetDomeVisible(bool visible)
    {
        proceduralDomeVisible = visible;

        if (domeRenderer != null && domeRenderer.enabled != visible)
            domeRenderer.enabled = visible;

        bool starsVisible = visible && DomeStarsEnabledValue && DomeStarsCountValue > 0;
        SetRendererVisible(domeStarsRenderer, starsVisible);
    }

    private GameObject GetEnviro3Prefab()
    {
        if (enviro3Prefab != null)
            return enviro3Prefab;

        enviro3Prefab = Resources.Load<GameObject>(Enviro3ResourcePath);
#if UNITY_EDITOR
        if (enviro3Prefab == null)
            enviro3Prefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enviro3AssetPath);
#endif
        return enviro3Prefab;
    }

    private GameObject GetEnviroSampleTerrainsPrefab()
    {
        if (enviroMountainPrefab != null)
            return enviroMountainPrefab;

        enviroMountainPrefab = Resources.Load<GameObject>(Enviro3SampleTerrainsResourcePath);
#if UNITY_EDITOR
        if (enviroMountainPrefab == null)
            enviroMountainPrefab = AssetDatabase.LoadAssetAtPath<GameObject>(Enviro3SampleTerrainsAssetPath);
#endif
        return enviroMountainPrefab;
    }

    private bool EnsureEnviroMountains()
    {
        if (enviroMountainObject != null && enviroMountainSilhouetteVersion != EnviroMountainSilhouetteStyleVersion)
            DestroyEnviroMountainObjects();

        if (enviroMountainObject != null)
        {
            int queueBase = proceduralStageRenderedAfterSky ? EnviroStageRenderQueueBase : StageRenderQueueBase;
            EnsureEnviroMountainLayerMaterials(queueBase + 2);
            ApplyEnviroMountainMaterialToTerrains();
            return true;
        }

        GameObject prefab = GetEnviroSampleTerrainsPrefab();
        if (prefab == null)
            return false;

        enviroMountainObject = Object.Instantiate(prefab, root != null ? root.transform : null, false);
        enviroMountainObject.name = "EnviroSampleBlackMountains";
        SetLayerRecursively(enviroMountainObject, root != null ? root.layer : 0);
        ConfigureEnviroSampleTerrains(enviroMountainObject);
        enviroMountainObject.SetActive(false);
        enviroMountainSilhouetteVersion = EnviroMountainSilhouetteStyleVersion;
        return true;
    }

    private void DestroyEnviroMountainObjects()
    {
        if (enviroMountainObject != null)
            Object.Destroy(enviroMountainObject);

        for (int i = 0; i < enviroMountainSilhouetteMeshes.Count; i++)
        {
            if (enviroMountainSilhouetteMeshes[i] != null)
                Object.Destroy(enviroMountainSilhouetteMeshes[i]);
        }

        enviroMountainTerrains.Clear();
        enviroMountainRenderers.Clear();
        enviroMountainSilhouetteMeshes.Clear();
        enviroMountainObject = null;
        enviroMountainSilhouetteMesh = null;
        enviroMountainSilhouetteMeshFilter = null;
        enviroMountainSilhouetteRenderer = null;
        enviroMountainSilhouetteVersion = 0;
    }

    private void ConfigureEnviroSampleTerrains(GameObject terrainRoot)
    {
        enviroMountainTerrains.Clear();
        enviroMountainRenderers.Clear();

        EnsureEnviroMountainLayerMaterials(EnviroStageRenderQueueBase + 2);

        DisableImportedSceneBehaviours(terrainRoot);

        Terrain[] terrains = terrainRoot.GetComponentsInChildren<Terrain>(true);
        CreateEnviroMountainSilhouette(terrainRoot, terrains);

        TerrainCollider[] colliders = terrainRoot.GetComponentsInChildren<TerrainCollider>(true);
        for (int i = 0; i < colliders.Length; i++)
            colliders[i].enabled = false;

        Collider[] regularColliders = terrainRoot.GetComponentsInChildren<Collider>(true);
        for (int i = 0; i < regularColliders.Length; i++)
            regularColliders[i].enabled = false;

        Renderer[] renderers = terrainRoot.GetComponentsInChildren<Renderer>(true);
        for (int i = 0; i < renderers.Length; i++)
        {
            Renderer renderer = renderers[i];
            if (renderer == null || enviroMountainRenderers.Contains(renderer))
                continue;

            renderer.enabled = false;
        }

        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null)
                continue;

            terrain.enabled = false;
            GameObject terrainObject = terrain.gameObject;
            if (terrainObject != null)
            {
                terrainObject.SetActive(false);
                if (terrainObject != terrainRoot)
                    Object.Destroy(terrainObject);
            }
        }
    }

    private void CreateEnviroMountainSilhouette(GameObject terrainRoot, Terrain[] terrains)
    {
        if (terrainRoot == null)
            return;

        for (int i = 0; i < enviroMountainSilhouetteMeshes.Count; i++)
        {
            if (enviroMountainSilhouetteMeshes[i] != null)
                Object.Destroy(enviroMountainSilhouetteMeshes[i]);
        }
        enviroMountainSilhouetteMeshes.Clear();
        enviroMountainSilhouetteMesh = null;
        enviroMountainSilhouetteMeshFilter = null;
        enviroMountainSilhouetteRenderer = null;

        float[] profile = BuildEnviroMountainHeightProfile(terrains);
        int samples = profile != null && profile.Length >= 2 ? profile.Length : EnviroMountainSilhouetteSamples;
        if (profile == null || profile.Length < 2)
            profile = BuildFallbackEnviroMountainProfile(samples);

        EnsureEnviroMountainLayerMaterials(EnviroStageRenderQueueBase + 2);

        for (int layerIndex = 0; layerIndex < EnviroMountainSilhouetteLayerCount; layerIndex++)
        {
            Mesh mesh = BuildEnviroMountainSilhouetteMesh(profile, layerIndex);
            enviroMountainSilhouetteMeshes.Add(mesh);

            GameObject silhouetteObject = new GameObject($"EnviroMountainHorizonLayer{layerIndex + 1}");
            silhouetteObject.transform.SetParent(terrainRoot.transform, false);
            silhouetteObject.transform.localPosition = new Vector3(0f, GetMountainLayerValue(EnviroMountainLayerYOffsets, layerIndex, 0f), GetMountainLayerValue(EnviroMountainLayerZOffsets, layerIndex, 0f));
            silhouetteObject.layer = terrainRoot.layer;

            MeshFilter meshFilter = silhouetteObject.AddComponent<MeshFilter>();
            meshFilter.sharedMesh = mesh;

            Renderer renderer = silhouetteObject.AddComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
            renderer.sharedMaterial = layerIndex < enviroMountainLayerMaterials.Count ? enviroMountainLayerMaterials[layerIndex] : enviroMountainMaterial;
            renderer.enabled = false;
            renderer.sortingOrder = EnviroMountainSortingOrderBase + layerIndex;
            enviroMountainRenderers.Add(renderer);

            if (layerIndex == EnviroMountainSilhouetteLayerCount - 1)
            {
                enviroMountainSilhouetteMesh = mesh;
                enviroMountainSilhouetteMeshFilter = meshFilter;
                enviroMountainSilhouetteRenderer = renderer;
            }
        }
    }

    private Mesh BuildEnviroMountainSilhouetteMesh(float[] sourceProfile, int layerIndex)
    {
        float[] profile = sourceProfile;
        int samples = profile != null && profile.Length >= 2 ? profile.Length : EnviroMountainSilhouetteSamples;
        if (profile == null || profile.Length < 2)
            profile = BuildFallbackEnviroMountainProfile(samples);

        Vector3[] vertices = new Vector3[samples * 2];
        Vector2[] uvs = new Vector2[vertices.Length];
        int[] triangles = new int[(samples - 1) * 6];

        float minHeight = float.MaxValue;
        float maxHeight = float.MinValue;
        for (int i = 0; i < profile.Length; i++)
        {
            minHeight = Mathf.Min(minHeight, profile[i]);
            maxHeight = Mathf.Max(maxHeight, profile[i]);
        }

        if (maxHeight - minHeight < 0.001f)
        {
            minHeight = 0f;
            maxHeight = 1f;
        }

        for (int i = 0; i < samples; i++)
        {
            float t = samples <= 1 ? 0f : i / (samples - 1f);
            float profileOffset = GetMountainLayerValue(EnviroMountainLayerProfileOffsets, layerIndex, 0f);
            float shiftedT = Mathf.Repeat(t + profileOffset + 1f, 1f);
            float sampledHeight = SampleEnviroMountainProfile(profile, shiftedT);
            float normalizedHeight = Mathf.InverseLerp(minHeight, maxHeight, sampledHeight);
            float detailStrength = GetMountainLayerValue(EnviroMountainLayerDetailStrengths, layerIndex, 0.05f);
            normalizedHeight = BuildEnviroMountainJaggedHeight(t + profileOffset, normalizedHeight, detailStrength, layerIndex);
            float edgeFade = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(t * 10f)) *
                Mathf.SmoothStep(0f, 1f, Mathf.Clamp01((1f - t) * 10f));
            float heightScale = GetMountainLayerValue(EnviroMountainLayerHeightScales, layerIndex, 1f);
            float ridgeY = Mathf.Lerp(0.25f, EnviroMountainSilhouettePeakHeight * heightScale, normalizedHeight);
            ridgeY *= Mathf.Lerp(0.72f, 1f, edgeFade);
            float widthScale = GetMountainLayerValue(EnviroMountainLayerWidthScales, layerIndex, 1f);
            float xOffset = GetMountainLayerValue(EnviroMountainLayerXOffsets, layerIndex, 0f);
            float x = ((t - 0.5f) * widthScale) + xOffset;
            int vertexIndex = i * 2;
            vertices[vertexIndex] = new Vector3(x, ridgeY, 0f);
            vertices[vertexIndex + 1] = new Vector3(x, EnviroMountainSilhouetteBaseY, 0f);
            uvs[vertexIndex] = new Vector2(t, 1f);
            uvs[vertexIndex + 1] = new Vector2(t, 0f);
        }

        int triangleIndex = 0;
        for (int i = 0; i < samples - 1; i++)
        {
            int topA = i * 2;
            int bottomA = topA + 1;
            int topB = topA + 2;
            int bottomB = topA + 3;

            triangles[triangleIndex++] = topA;
            triangles[triangleIndex++] = bottomA;
            triangles[triangleIndex++] = topB;
            triangles[triangleIndex++] = topB;
            triangles[triangleIndex++] = bottomA;
            triangles[triangleIndex++] = bottomB;
        }

        Mesh mesh = new Mesh();
        mesh.name = $"Enviro Mountain Horizon Layer {layerIndex + 1}";
        mesh.vertices = vertices;
        mesh.uv = uvs;
        mesh.triangles = triangles;
        mesh.RecalculateBounds();
        mesh.RecalculateNormals();
        return mesh;
    }

    private static float SampleEnviroMountainProfile(float[] profile, float t)
    {
        if (profile == null || profile.Length == 0)
            return 0f;
        if (profile.Length == 1)
            return profile[0];

        t = Mathf.Clamp01(t);
        float scaledIndex = t * (profile.Length - 1);
        int indexA = Mathf.Clamp(Mathf.FloorToInt(scaledIndex), 0, profile.Length - 1);
        int indexB = Mathf.Clamp(indexA + 1, 0, profile.Length - 1);
        return Mathf.Lerp(profile[indexA], profile[indexB], scaledIndex - indexA);
    }

    private static float BuildEnviroMountainJaggedHeight(float t, float sourceHeight, float detailStrength, int layerIndex)
    {
        sourceHeight = Mathf.Clamp01(sourceHeight);
        float layerSeed = layerIndex * 19.731f;
        float broad = SampleEnviroMountainAngularNoise(t * 5.5f + layerSeed);
        float mid = SampleEnviroMountainAngularNoise(t * 13.5f + layerSeed * 1.37f);
        float fine = SampleEnviroMountainAngularNoise(t * 36.0f + layerSeed * 0.73f);
        float peakA = EnviroMountainTrianglePeak(t * (7.0f + layerIndex * 1.2f) + layerSeed * 0.11f);
        float peakB = EnviroMountainTrianglePeak(t * (12.0f + layerIndex * 1.5f) + layerSeed * 0.07f + 0.31f);
        float crag = EnviroMountainTrianglePeak(t * (28.0f + layerIndex * 3.0f) + layerSeed * 0.19f);
        float valley = EnviroMountainTrianglePeak(t * (6.0f + layerIndex) + layerSeed * 0.23f + 0.48f);

        float majorPeak = Mathf.Max(peakA, peakB * 0.92f);
        float height = 0.08f +
            sourceHeight * 0.18f +
            broad * 0.12f +
            mid * 0.08f +
            majorPeak * 0.70f +
            crag * detailStrength * 0.38f +
            (fine - 0.5f) * detailStrength * 0.75f -
            valley * 0.28f;

        return Mathf.Pow(Mathf.Clamp01(height), 1.12f);
    }

    private static float SampleEnviroMountainAngularNoise(float x)
    {
        int index = Mathf.FloorToInt(x);
        float t = x - index;
        return Mathf.Lerp(HashEnviroMountainNoise(index), HashEnviroMountainNoise(index + 1), t);
    }

    private static float EnviroMountainTrianglePeak(float x)
    {
        float t = x - Mathf.Floor(x);
        float peak = 1f - Mathf.Abs((t * 2f) - 1f);
        return Mathf.Pow(Mathf.Clamp01(peak), 2.35f);
    }

    private static float HashEnviroMountainNoise(int value)
    {
        unchecked
        {
            uint hash = (uint)value;
            hash ^= 2747636419u;
            hash *= 2654435769u;
            hash ^= hash >> 16;
            hash *= 2654435769u;
            hash ^= hash >> 16;
            return (hash & 0x00FFFFFFu) / 16777215f;
        }
    }

    private static float GetMountainLayerValue(float[] values, int layerIndex, float fallback)
    {
        if (values == null || values.Length == 0)
            return fallback;

        return values[Mathf.Clamp(layerIndex, 0, values.Length - 1)];
    }

    private float[] BuildEnviroMountainHeightProfile(Terrain[] terrains)
    {
        if (terrains == null || terrains.Length == 0)
            return null;

        float farthestLocalZ = float.MinValue;
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            farthestLocalZ = Mathf.Max(farthestLocalZ, terrain.transform.localPosition.z);
        }

        if (farthestLocalZ == float.MinValue)
            return null;

        List<Terrain> sourceTerrains = new List<Terrain>();
        for (int i = 0; i < terrains.Length; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            if (Mathf.Abs(terrain.transform.localPosition.z - farthestLocalZ) < 1f)
                sourceTerrains.Add(terrain);
        }

        if (sourceTerrains.Count == 0)
            return null;

        float minX = float.MaxValue;
        float maxX = float.MinValue;
        for (int i = 0; i < sourceTerrains.Count; i++)
        {
            Terrain terrain = sourceTerrains[i];
            TerrainData data = terrain.terrainData;
            float startX = terrain.transform.localPosition.x;
            float endX = startX + data.size.x;
            minX = Mathf.Min(minX, startX);
            maxX = Mathf.Max(maxX, endX);
        }

        if (maxX - minX < 0.001f)
            return null;

        float[] profile = new float[EnviroMountainSilhouetteSamples];
        for (int i = 0; i < profile.Length; i++)
        {
            float t = profile.Length <= 1 ? 0f : i / (profile.Length - 1f);
            float sampleX = Mathf.Lerp(minX, maxX, t);
            Terrain terrain = FindTerrainAtLocalX(sourceTerrains, sampleX);
            if (terrain == null || terrain.terrainData == null)
            {
                profile[i] = 0f;
                continue;
            }

            TerrainData data = terrain.terrainData;
            float localX = Mathf.Clamp01((sampleX - terrain.transform.localPosition.x) / Mathf.Max(0.001f, data.size.x));
            float height = 0f;
            height = Mathf.Max(height, terrain.transform.localPosition.y + data.GetInterpolatedHeight(localX, 0.28f));
            height = Mathf.Max(height, terrain.transform.localPosition.y + data.GetInterpolatedHeight(localX, 0.47f));
            height = Mathf.Max(height, terrain.transform.localPosition.y + data.GetInterpolatedHeight(localX, 0.66f));
            profile[i] = height;
        }

        return profile;
    }

    private static Terrain FindTerrainAtLocalX(List<Terrain> terrains, float localX)
    {
        if (terrains == null || terrains.Count == 0)
            return null;

        Terrain closest = null;
        float closestDistance = float.MaxValue;
        for (int i = 0; i < terrains.Count; i++)
        {
            Terrain terrain = terrains[i];
            if (terrain == null || terrain.terrainData == null)
                continue;

            float startX = terrain.transform.localPosition.x;
            float endX = startX + terrain.terrainData.size.x;
            if (localX >= startX && localX <= endX)
                return terrain;

            float distance = localX < startX ? startX - localX : localX - endX;
            if (distance < closestDistance)
            {
                closestDistance = distance;
                closest = terrain;
            }
        }

        return closest;
    }

    private static void SmoothEnviroMountainProfile(float[] profile, int iterations)
    {
        if (profile == null || profile.Length < 3)
            return;

        float[] scratch = new float[profile.Length];
        for (int iteration = 0; iteration < iterations; iteration++)
        {
            scratch[0] = profile[0];
            scratch[profile.Length - 1] = profile[profile.Length - 1];
            for (int i = 1; i < profile.Length - 1; i++)
                scratch[i] = (profile[i - 1] + profile[i] * 2f + profile[i + 1]) * 0.25f;

            for (int i = 0; i < profile.Length; i++)
                profile[i] = scratch[i];
        }
    }

    private static float[] BuildFallbackEnviroMountainProfile(int samples)
    {
        samples = Mathf.Max(2, samples);
        float[] profile = new float[samples];
        for (int i = 0; i < samples; i++)
        {
            float t = samples <= 1 ? 0f : i / (samples - 1f);
            float broad = SampleEnviroMountainAngularNoise(t * 5.0f + 2.1f);
            float mid = SampleEnviroMountainAngularNoise(t * 13.0f + 7.4f);
            float peak = EnviroMountainTrianglePeak(t * 9.0f + 0.27f);
            profile[i] = broad * 0.58f + mid * 0.25f + peak * 0.22f;
        }

        return profile;
    }

    private Enviro.EnviroVolumetricCloudsModule GetEnviro3VolumetricCloudPreset()
    {
        if (enviro3VolumetricCloudPreset != null)
            return enviro3VolumetricCloudPreset;

        enviro3VolumetricCloudPreset = Resources.Load<Enviro.EnviroVolumetricCloudsModule>(Enviro3VolumetricCloudPresetResourcePath);
#if UNITY_EDITOR
        if (enviro3VolumetricCloudPreset == null)
            enviro3VolumetricCloudPreset = AssetDatabase.LoadAssetAtPath<Enviro.EnviroVolumetricCloudsModule>(Enviro3VolumetricCloudPresetAssetPath);
#endif
        return enviro3VolumetricCloudPreset;
    }

    private bool EnsureEnviro3Sky(Camera camera)
    {
        if (enviro3Object != null && enviro3Manager != null)
        {
            if (!enviro3Object.activeSelf)
                enviro3Object.SetActive(true);

            ConfigureEnviro3Camera(camera);
            bool needsVolumetricCloudModule = enviro3Manager.VolumetricClouds == null;
            if (EnsureEnviro3VolumetricCloudModule(true) && needsVolumetricCloudModule)
                ConfigureEnviro3PerformanceProfile();
            return true;
        }

        GameObject prefab = GetEnviro3Prefab();
        if (prefab == null)
            return false;

        enviro3Object = Object.Instantiate(prefab, root != null ? root.transform : null, false);
        enviro3Object.name = "NeonStageEnviro3Sky";
        enviro3Manager = enviro3Object.GetComponent<Enviro.EnviroManager>();
        if (enviro3Manager == null)
        {
            Object.Destroy(enviro3Object);
            enviro3Object = null;
            return false;
        }

        DisableImportedSceneBehaviours(enviro3Object);
        SetLayerRecursively(enviro3Object, root != null ? root.layer : enviro3Object.layer);
        ConfigureEnviro3Camera(camera);

        enviro3Manager.dontDestroyOnLoad = false;
        enviro3Manager.LoadConfiguration();
        enviro3Manager.StartModules();
        EnsureEnviro3VolumetricCloudModule(false);
        enviro3Manager.EnableModules();
        ConfigureEnviro3PerformanceProfile();

        if (enviro3Manager.Sky == null || enviro3Manager.Sky.Settings == null)
        {
            DestroyEnviro3Sky();
            return false;
        }

        warnedMissingEnviro3Prefab = false;
        return true;
    }

    private bool EnsureEnviro3VolumetricCloudModule(bool enableImmediately)
    {
        if (enviro3Manager == null)
            return false;

        if (enviro3Manager.VolumetricClouds != null)
            return true;

        Enviro.EnviroVolumetricCloudsModule preset = GetEnviro3VolumetricCloudPreset();
        if (preset == null)
        {
            if (!warnedMissingEnviro3VolumetricCloudPreset)
            {
                warnedMissingEnviro3VolumetricCloudPreset = true;
                Debug.LogWarning("Enviro 3 volumetric cloud preset could not be loaded from Resources/Enviro3.");
            }
            return false;
        }

        Enviro.EnviroVolumetricCloudsModule module = Object.Instantiate(preset);
        module.name = "Neon Stage Volumetric Clouds";
        module.preset = preset;
        module.active = true;
        enviro3Manager.VolumetricClouds = module;
        appliedEnviro3MoodIndex = int.MinValue;
        appliedEnviro3MoonModeIndex = int.MinValue;
        appliedEnviro3CloudsEnabled = true;
        appliedEnviro3CloudModifiers = UnappliedEnviro3CloudModifiers;
        appliedEnviro3StarAnimation = -1f;
        appliedEnviro3StarDensity = -1f;
        warnedMissingEnviro3VolumetricCloudPreset = false;

        if (enableImmediately)
            module.Enable();

        return true;
    }

    private void ConfigureEnviro3Camera(Camera camera)
    {
        if (enviro3Manager == null)
            return;

        if (enviro3ConfiguredCamera != null && enviro3ConfiguredCamera != camera)
            SetEnviroRendererEnabled(enviro3ConfiguredCamera, false);
        Camera mainCamera = Camera.main;
        if (mainCamera != null && mainCamera != camera)
            SetEnviroRendererEnabled(mainCamera, false);

        enviro3Manager.CameraTag = "MainCamera";
        enviro3Manager.Camera = camera;
        enviro3Manager.optionalFollowTransform = camera != null ? camera.transform : null;
        if (enviro3Manager.Cameras != null)
            enviro3Manager.Cameras.Clear();
        if (camera != null)
        {
            enviro3Manager.ChangeCamera(camera);
            SetEnviroRendererEnabled(camera, true);
        }
        enviro3ConfiguredCamera = camera;
    }

    private static void SetEnviroRendererEnabled(Camera camera, bool enabled)
    {
        if (camera == null)
            return;

        Enviro.EnviroRenderer renderer = camera.GetComponent<Enviro.EnviroRenderer>();
        if (renderer != null)
            renderer.enabled = enabled;
    }

    private void ConfigureEnviro3PerformanceProfile()
    {
        if (enviro3Manager == null)
            return;

        enviro3Manager.updateSkyAndLighting = false;
        enviro3Manager.updateSkyAndLightingHDRP = false;
        SetEnviroModuleActive(enviro3Manager.Time, true);
        SetEnviroModuleActive(enviro3Manager.Sky, true);
        SetEnviroModuleActive(enviro3Manager.FlatClouds, false);
        SetEnviroModuleActive(enviro3Manager.Aurora, true);
        SetEnviroModuleActive(enviro3Manager.VolumetricClouds, true);
        SetEnviroModuleActive(enviro3Manager.Lighting, false);
        SetEnviroModuleActive(enviro3Manager.Reflections, false);
        SetEnviroModuleActive(enviro3Manager.Fog, false);
        SetEnviroModuleActive(enviro3Manager.Weather, false);
        SetEnviroModuleActive(enviro3Manager.Audio, false);
        SetEnviroModuleActive(enviro3Manager.Effects, false);
        SetEnviroModuleActive(enviro3Manager.Lightning, false);
        SetEnviroModuleActive(enviro3Manager.Quality, false);
        SetEnviroModuleActive(enviro3Manager.Environment, false);

        if (enviro3Manager.Objects != null)
        {
            if (enviro3Manager.Objects.directionalLight != null)
                enviro3Manager.Objects.directionalLight.enabled = false;
            if (enviro3Manager.Objects.additionalDirectionalLight != null)
                enviro3Manager.Objects.additionalDirectionalLight.enabled = false;
            if (enviro3Manager.Objects.globalReflectionProbe != null)
                enviro3Manager.Objects.globalReflectionProbe.gameObject.SetActive(false);
            if (enviro3Manager.Objects.effects != null)
                enviro3Manager.Objects.effects.SetActive(false);
            if (enviro3Manager.Objects.audio != null)
                enviro3Manager.Objects.audio.SetActive(false);
            if (enviro3Manager.Objects.windZone != null)
                enviro3Manager.Objects.windZone.gameObject.SetActive(false);
        }

        if (enviro3Object != null)
        {
            AudioSource[] audioSources = enviro3Object.GetComponentsInChildren<AudioSource>(true);
            for (int i = 0; i < audioSources.Length; i++)
                audioSources[i].enabled = false;
        }

        enviro3Manager.Lighting = null;
        enviro3Manager.Reflections = null;
        enviro3Manager.Fog = null;
        enviro3Manager.FlatClouds = null;
        enviro3Manager.Weather = null;
        enviro3Manager.Audio = null;
        enviro3Manager.Effects = null;
        enviro3Manager.Lightning = null;
        enviro3Manager.Quality = null;
        enviro3Manager.Environment = null;

        ApplyEnviro3VolumetricClouds(0.20f, 0.90f, 180f, 4600f, 4.8f, 0.50f,
            new Color(0.62f, 1.05f, 0.96f, 1f),
            new Color(0.10f, 0.28f, 0.25f, 1f),
            0.010f);
    }

    private void ApplyEnviro3Sky(Camera camera, int moodIndex)
    {
        using (ApplyEnviro3SkyProfilerMarker.Auto())
        {
            CaptureSkyboxOverrideState(camera);
            ConfigureEnviro3Camera(camera);
            ApplyEnviro3RenderSettings();

            Vector4 cloudModifiers = BuildEnviro3CloudModifierVector();
            bool cloudsEnabled = Enviro3CloudsEnabledValue;
            int moonModeIndex = Enviro3MoonModeIndexValue;
            float starAnimation = Enviro3StarAnimationValue;
            float starDensity = Enviro3StarDensityValue;
            if (appliedEnviro3MoodIndex != moodIndex ||
                appliedEnviro3MoonModeIndex != moonModeIndex ||
                appliedEnviro3CloudsEnabled != cloudsEnabled ||
                Mathf.Abs(appliedEnviro3StarAnimation - starAnimation) > 0.0001f ||
                Mathf.Abs(appliedEnviro3StarDensity - starDensity) > 0.0001f ||
                HasEnviro3CloudModifierChanged(cloudModifiers))
            {
                ApplyEnviro3Mood(moodIndex);
                appliedEnviro3MoodIndex = moodIndex;
                appliedEnviro3MoonModeIndex = moonModeIndex;
                appliedEnviro3CloudsEnabled = cloudsEnabled;
                appliedEnviro3CloudModifiers = cloudModifiers;
                appliedEnviro3StarAnimation = starAnimation;
                appliedEnviro3StarDensity = starDensity;
            }

            if (enviro3Manager != null)
            {
                // EnviroManager.Update() already updates modules every frame. Only reapply our
                // lightweight placement/material overrides here so Render() does not duplicate
                // Enviro's full module update path.
                ApplyEnviro3CelestialOverridesIfNeeded();
            }

            if (camera != null && camera.clearFlags != CameraClearFlags.Skybox)
                camera.clearFlags = CameraClearFlags.Skybox;
        }
    }

    private Vector4 BuildEnviro3CloudModifierVector()
    {
        return new Vector4(
            Enviro3CloudAmountValue,
            Enviro3CloudThicknessValue,
            Enviro3CloudConnectivityValue,
            Enviro3CloudContrastValue);
    }

    private bool HasEnviro3CloudModifierChanged(Vector4 cloudModifiers)
    {
        return Mathf.Abs(appliedEnviro3CloudModifiers.x - cloudModifiers.x) > 0.0001f
            || Mathf.Abs(appliedEnviro3CloudModifiers.y - cloudModifiers.y) > 0.0001f
            || Mathf.Abs(appliedEnviro3CloudModifiers.z - cloudModifiers.z) > 0.0001f
            || Mathf.Abs(appliedEnviro3CloudModifiers.w - cloudModifiers.w) > 0.0001f;
    }

    private void ApplyEnviro3RenderSettings()
    {
        using (ApplyEnviro3RenderSettingsProfilerMarker.Auto())
        {
            bool renderSettingsChanged =
                !enviro3RenderSettingsApplied ||
                RenderSettings.fog ||
                RenderSettings.ambientMode != AmbientMode.Skybox ||
                !Mathf.Approximately(RenderSettings.ambientIntensity, 0.85f) ||
                RenderSettings.defaultReflectionMode != DefaultReflectionMode.Skybox ||
                RenderSettings.defaultReflectionResolution != 64 ||
                RenderSettings.reflectionBounces != 1 ||
                !Mathf.Approximately(RenderSettings.reflectionIntensity, 0.35f) ||
                RenderSettings.sun != null;

            if (renderSettingsChanged)
            {
                RenderSettings.fog = false;
                RenderSettings.ambientMode = AmbientMode.Skybox;
                RenderSettings.ambientIntensity = 0.85f;
                RenderSettings.defaultReflectionMode = DefaultReflectionMode.Skybox;
                RenderSettings.defaultReflectionResolution = 64;
                RenderSettings.reflectionBounces = 1;
                RenderSettings.reflectionIntensity = 0.35f;
                RenderSettings.sun = null;
                enviro3RenderSettingsApplied = true;
            }

            float skyPitch = Enviro3SkyCameraPitchValue;
            float starDensity = Mathf.Max(0f, Enviro3StarDensityValue - 1f);
            if (!enviro3ShaderGlobalsApplied ||
                Mathf.Abs(appliedEnviro3RenderSkyPitch - skyPitch) > 0.0001f ||
                Mathf.Abs(appliedEnviro3RenderStarDensity - starDensity) > 0.0001f)
            {
                Shader.SetGlobalMatrix(LightMatrixShaderId, Matrix4x4.Rotate(StageLightRotation));
                Shader.SetGlobalFloat(EnviroStageSkyPitchShaderId, skyPitch);
                Shader.SetGlobalFloat(EnviroStageStarDensityShaderId, starDensity);
                appliedEnviro3RenderSkyPitch = skyPitch;
                appliedEnviro3RenderStarDensity = starDensity;
                enviro3ShaderGlobalsApplied = true;
            }
        }
    }

    private void ApplyEnviro3Mood(int moodIndex)
    {
        using (ApplyEnviro3MoodProfilerMarker.Auto())
        {
            moodIndex = Mathf.Clamp(moodIndex, 0, Enviro3MoodCount - 1);
            switch ((GuitarBridgeServer.TabsEnviroSkyMood)moodIndex)
            {
                case GuitarBridgeServer.TabsEnviroSkyMood.GoldenSunset:
                    ApplyEnviro3Base(18.4f,
                    new Color(0.09f, 0.08f, 0.18f, 1f),
                    new Color(0.36f, 0.16f, 0.46f, 1f),
                    new Color(0.88f, 0.28f, 0.34f, 1f),
                    new Color(1.35f, 0.56f, 0.20f, 1f),
                    new Color(1.90f, 0.92f, 0.34f, 1f),
                    new Color(0.10f, 0.08f, 0.15f, 1f),
                    new Color(1.05f, 0.84f, 0.62f, 1f),
                    1.10f, 1.05f, 0.34f, 0.05f, 0.00f, false, new Color(1.0f, 0.86f, 0.70f, 1f), 0.0f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.34f, 0.95f, 160f, 4000f, 5.3f, 0.62f,
                    new Color(1.28f, 0.58f, 0.28f, 1f), new Color(0.34f, 0.14f, 0.24f, 1f), 0.012f);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.DarkClouds:
                    ApplyEnviro3Base(23.1f,
                    new Color(0.010f, 0.014f, 0.020f, 1f),
                    new Color(0.030f, 0.036f, 0.050f, 1f),
                    new Color(0.060f, 0.070f, 0.088f, 1f),
                    new Color(0.105f, 0.115f, 0.130f, 1f),
                    new Color(0.180f, 0.170f, 0.155f, 1f),
                    new Color(0.007f, 0.009f, 0.012f, 1f),
                    new Color(0.72f, 0.78f, 0.90f, 1f),
                    0.72f, 1.45f, 0.16f, 1.90f, 0.00f, false, new Color(0.70f, 0.76f, 0.88f, 1f), 0.0f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.58f, 1.35f, 140f, 3900f, 3.8f, 0.42f,
                    new Color(0.56f, 0.60f, 0.66f, 1f), new Color(0.05f, 0.06f, 0.07f, 1f), 0.016f);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.CrimsonDusk:
                    ApplyEnviro3Base(23.25f,
                    new Color(0.006f, 0.001f, 0.006f, 1f),
                    new Color(0.020f, 0.003f, 0.014f, 1f),
                    new Color(0.055f, 0.008f, 0.026f, 1f),
                    new Color(0.110f, 0.014f, 0.030f, 1f),
                    new Color(0.260f, 0.030f, 0.032f, 1f),
                    new Color(0.004f, 0.001f, 0.006f, 1f),
                    new Color(1.00f, 0.18f, 0.12f, 1f),
                    0.78f, 1.48f, 0.018f, 2.80f, 0.00f, false, new Color(1.00f, 0.26f, 0.18f, 1f), 0.0f);
                ApplyEnviro3Aurora(true, new Color(1.00f, 0.055f, 0.020f, 1f), 82f, 9.0f, 0.72f, 0.0042f, 0.0034f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                DisableEnviro3VolumetricClouds();
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.SilverMoon:
                    ApplyEnviro3Base(0.8f,
                    new Color(0.010f, 0.014f, 0.038f, 1f),
                    new Color(0.032f, 0.046f, 0.095f, 1f),
                    new Color(0.070f, 0.090f, 0.160f, 1f),
                    new Color(0.155f, 0.175f, 0.240f, 1f),
                    new Color(0.390f, 0.420f, 0.520f, 1f),
                    new Color(0.008f, 0.010f, 0.025f, 1f),
                    new Color(0.95f, 1.02f, 1.18f, 1f),
                    0.96f, 1.18f, 0.05f, 2.85f, 0.22f, true, new Color(0.92f, 0.96f, 1.10f, 1f), 1.05f, 3.4f, 0f, 3f, 180f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.30f, 0.84f, 260f, 5200f, 3.9f, 0.56f,
                    new Color(0.86f, 0.92f, 1.08f, 1f), new Color(0.12f, 0.15f, 0.25f, 1f), 0.006f);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.AuroraBorealis:
                default:
                    ApplyEnviro3Base(1.2f,
                    new Color(0.004f, 0.012f, 0.035f, 1f),
                    new Color(0.012f, 0.045f, 0.110f, 1f),
                    new Color(0.025f, 0.110f, 0.180f, 1f),
                    new Color(0.045f, 0.220f, 0.260f, 1f),
                    new Color(0.090f, 0.520f, 0.440f, 1f),
                    new Color(0.003f, 0.010f, 0.030f, 1f),
                    new Color(0.54f, 1.10f, 0.96f, 1f),
                    0.96f, 1.28f, 0.04f, 3.15f, 0.10f, true, new Color(0.72f, 0.95f, 1.02f, 1f), 0.46f, 1.75f, 0f, 4f, 180f);
                ApplyEnviro3Aurora(true, new Color(0.20f, 0.82f, 0.96f, 1f), 82f, 7f, 0.72f, 0.0045f, 0.0048f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.22f, 0.72f, 260f, 4300f, 3.35f, 0.36f,
                    new Color(0.78f, 0.62f, 1.10f, 1f), new Color(0.040f, 0.050f, 0.125f, 1f), 0.006f,
                    EnviroCloudArtStyle.Aurora);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.BloodMoonHorror:
                    ApplyEnviro3Base(23.6f,
                    new Color(0.025f, 0.002f, 0.006f, 1f),
                    new Color(0.080f, 0.006f, 0.012f, 1f),
                    new Color(0.190f, 0.018f, 0.026f, 1f),
                    new Color(0.420f, 0.040f, 0.036f, 1f),
                    new Color(0.950f, 0.110f, 0.060f, 1f),
                    new Color(0.018f, 0.002f, 0.006f, 1f),
                    new Color(1.20f, 0.28f, 0.22f, 1f),
                    0.86f, 1.42f, 0.08f, 2.95f, 0.05f, true, new Color(1.35f, 0.26f, 0.18f, 1f), 2.65f, 18.2f, 0f, 0.35f, 180f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.18f, 0.92f, 420f, 3600f, 2.65f, 0.30f,
                    new Color(0.72f, 0.07f, 0.045f, 1f), new Color(0.035f, 0.004f, 0.008f, 1f), 0.010f,
                    EnviroCloudArtStyle.Horror);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.GalaxyFront:
                    ApplyEnviro3Base(0.45f,
                    new Color(0.001f, 0.002f, 0.012f, 1f),
                    new Color(0.006f, 0.006f, 0.040f, 1f),
                    new Color(0.030f, 0.012f, 0.100f, 1f),
                    new Color(0.070f, 0.035f, 0.180f, 1f),
                    new Color(0.020f, 0.050f, 0.160f, 1f),
                    new Color(0.001f, 0.002f, 0.014f, 1f),
                    new Color(0.78f, 0.72f, 1.42f, 1f),
                    0.92f, 1.42f, 0.015f, 3.20f, 0.82f, true, new Color(0.78f, 0.84f, 1.20f, 1f), 0.34f, 1.90f, 94f, 4f, 180f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.28f, 0.82f, 360f, 5400f, 2.45f, 0.38f,
                    new Color(0.50f, 0.38f, 1.05f, 1f), new Color(0.012f, 0.014f, 0.060f, 1f), 0.002f,
                    EnviroCloudArtStyle.Galaxy);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.StarryCloudNight:
                    ApplyEnviro3Base(0.6f,
                    new Color(0.002f, 0.003f, 0.014f, 1f),
                    new Color(0.012f, 0.008f, 0.055f, 1f),
                    new Color(0.055f, 0.020f, 0.125f, 1f),
                    new Color(0.020f, 0.060f, 0.160f, 1f),
                    new Color(0.015f, 0.020f, 0.050f, 1f),
                    new Color(0.004f, 0.004f, 0.018f, 1f),
                    new Color(0.72f, 0.78f, 1.35f, 1f),
                    1.00f, 1.35f, 0.02f, 3.45f, 0.52f, true, new Color(0.74f, 0.84f, 1.24f, 1f), 0.74f, 2.3f, 0f, 4f, 180f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.30f, 0.98f, 165f, 4300f, 4.85f, 0.48f,
                    new Color(0.56f, 0.70f, 1.26f, 1f), new Color(0.020f, 0.026f, 0.090f, 1f), 0.004f);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.CloudyGiantMoon:
                    ApplyEnviro3Base(0.55f,
                    new Color(0.002f, 0.003f, 0.014f, 1f),
                    new Color(0.010f, 0.010f, 0.052f, 1f),
                    new Color(0.045f, 0.018f, 0.115f, 1f),
                    new Color(0.018f, 0.050f, 0.145f, 1f),
                    new Color(0.012f, 0.018f, 0.048f, 1f),
                    new Color(0.003f, 0.003f, 0.016f, 1f),
                    new Color(0.72f, 0.82f, 1.38f, 1f),
                    1.02f, 1.35f, 0.02f, 3.30f, 0.48f, true, new Color(0.70f, 0.84f, 1.48f, 1f), 3.05f, 18.2f, 0f, 0.35f, 180f);
                ApplyEnviro3Aurora(false, Color.black, 0f, 0f, 0f, 0.008f, 0f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.54f, 1.32f, 145f, 4050f, 4.75f, 0.56f,
                    new Color(0.86f, 1.00f, 1.66f, 1f), new Color(0.022f, 0.032f, 0.115f, 1f), 0.004f,
                    EnviroCloudArtStyle.ThickMoonlit);
                    break;

                case GuitarBridgeServer.TabsEnviroSkyMood.VioletAuroraStorm:
                    ApplyEnviro3Base(1.65f,
                    new Color(0.006f, 0.006f, 0.035f, 1f),
                    new Color(0.028f, 0.022f, 0.095f, 1f),
                    new Color(0.080f, 0.055f, 0.190f, 1f),
                    new Color(0.145f, 0.105f, 0.300f, 1f),
                    new Color(0.330f, 0.190f, 0.560f, 1f),
                    new Color(0.005f, 0.006f, 0.028f, 1f),
                    new Color(0.88f, 0.52f, 1.28f, 1f),
                    0.92f, 1.30f, 0.035f, 3.00f, 0.12f, true, new Color(0.76f, 0.78f, 1.16f, 1f), 0.28f, 1.55f, 0f, 4f, 180f);
                ApplyEnviro3Aurora(true, new Color(0.72f, 0.25f, 1.00f, 1f), 80f, 8.4f, 0.72f, 0.0041f, 0.0042f);
                ApplyEnviro3FlatClouds(false, Color.black, 0f, 0f, 0f, 0f, 10f);
                ApplyEnviro3VolumetricClouds(0.30f, 0.94f, 230f, 4500f, 3.75f, 0.44f,
                    new Color(0.74f, 0.44f, 1.30f, 1f), new Color(0.040f, 0.030f, 0.150f, 1f), 0.005f,
                    EnviroCloudArtStyle.Aurora);
                    break;
            }

            if (enviro3Manager != null)
            {
                using (Enviro3UpdateModulesProfilerMarker.Auto())
                {
                    enviro3Manager.UpdateModules();
                }
                if (enviro3Manager.Sky != null)
                {
                    using (Enviro3SkyUpdateModuleProfilerMarker.Auto())
                    {
                        enviro3Manager.Sky.UpdateModule();
                    }
                }

                ApplyEnviro3CelestialOverrides(force: true);
            }
        }
    }

    private void ApplyEnviro3Base(
        float timeOfDay,
        Color zenith,
        Color upper,
        Color middle,
        Color lower,
        Color horizon,
        Color back,
        Color tint,
        float intensity,
        float exponent,
        float mie,
        float stars,
        float galaxy,
        bool moonEnabled,
        Color moonColor,
        float moonGlow,
        float moonScale = 1f,
        float skyRotationYaw = 0f,
        float moonRotationX = -1f,
        float moonRotationY = 0f)
    {
        enviro3SkyRotationYaw = skyRotationYaw;
        enviro3HasMoonRotationOverride = moonRotationX >= 0f;
        enviro3MoonRotationOverrideX = Mathf.Repeat(moonRotationX, 360f);
        enviro3MoonRotationOverrideY = moonRotationY;
        ApplyEnviro3Time(timeOfDay);
        ApplyEnviro3SkyPalette(zenith, upper, middle, lower, horizon, back, tint, intensity, exponent, mie, stars, galaxy, moonEnabled, moonColor, moonGlow, moonScale, skyRotationYaw);
        ApplyEnviro3CelestialOverrides();
    }

    private void ApplyEnviro3Time(float timeOfDay)
    {
        if (enviro3Manager == null)
            return;

        timeOfDay = Mathf.Repeat(timeOfDay, 24f);
        if (enviro3Manager.Time != null && enviro3Manager.Time.Settings != null)
        {
            Enviro.EnviroTime settings = enviro3Manager.Time.Settings;
            settings.simulate = false;
            settings.calenderType = Enviro.EnviroTime.CalenderType.Custom;
            settings.customSunOffset = -8f;
            settings.customSunRotation = 0f;
            enviro3Manager.Time.SetTimeOfDay(timeOfDay);
        }

        enviro3Manager.sunRotationX = Mathf.Repeat((timeOfDay - 8f) * 15f, 360f);
        enviro3Manager.sunRotationY = 0f;
        enviro3Manager.moonRotationX = Mathf.Repeat(enviro3Manager.sunRotationX - 180f, 360f);
        enviro3Manager.moonRotationY = enviro3Manager.sunRotationY;
        enviro3Manager.UpdateNonTime();
    }

    private void ApplyEnviro3SkyPalette(
        Color zenith,
        Color upper,
        Color middle,
        Color lower,
        Color horizon,
        Color back,
        Color tint,
        float intensity,
        float exponent,
        float mie,
        float stars,
        float galaxy,
        bool moonEnabled,
        Color moonColor,
        float moonGlow,
        float moonScale,
        float skyRotationYaw)
    {
        if (enviro3Manager == null || enviro3Manager.Sky == null || enviro3Manager.Sky.Settings == null)
            return;

        SetEnviroModuleActive(enviro3Manager.Sky, true);

        ApplyEnviro3MoonModeOverride(ref moonEnabled, ref moonGlow, ref moonScale);

        Enviro.EnviroSky settings = enviro3Manager.Sky.Settings;
        settings.skyMode = Enviro.EnviroSky.SkyMode.Normal;
        settings.moonMode = moonEnabled ? Enviro.EnviroSky.MoonMode.Simple : Enviro.EnviroSky.MoonMode.Off;
        settings.frontColorGradient0 = BuildConstantGradient(zenith);
        settings.frontColorGradient1 = BuildConstantGradient(upper);
        settings.frontColorGradient2 = BuildConstantGradient(middle);
        settings.frontColorGradient3 = BuildConstantGradient(lower);
        settings.frontColorGradient4 = BuildConstantGradient(horizon);
        settings.frontColorGradient5 = BuildConstantGradient(ScaleColor(horizon, 1.18f));
        settings.backColorGradient0 = BuildConstantGradient(back);
        settings.backColorGradient1 = BuildConstantGradient(ScaleColor(back, 1.08f));
        settings.backColorGradient2 = BuildConstantGradient(ScaleColor(middle, 0.46f));
        settings.backColorGradient3 = BuildConstantGradient(ScaleColor(lower, 0.42f));
        settings.backColorGradient4 = BuildConstantGradient(ScaleColor(horizon, 0.34f));
        settings.backColorGradient5 = BuildConstantGradient(ScaleColor(horizon, 0.26f));
        settings.sunDiscColorGradient = BuildConstantGradient(ScaleColor(horizon, 1.25f));
        settings.moonColorGradient = BuildConstantGradient(moonColor);
        settings.moonGlowColorGradient = BuildConstantGradient(ScaleColor(moonColor, 1.15f));
        settings.distribution0 = 0.18f;
        settings.distribution1 = 0.34f;
        settings.distribution2 = 0.58f;
        settings.distribution3 = 0.78f;
        settings.intensity = Mathf.Max(0f, intensity);
        settings.intensityCurve = BuildConstantCurve(1f);
        settings.skyColorTint = tint;
        settings.skyColorExponent = Mathf.Max(0.05f, exponent);
        settings.mieScatteringMultiplier = 1f;
        settings.mieScatteringIntensityCurve = BuildConstantCurve(Mathf.Max(0f, mie));
        float starAnimation = Enviro3StarAnimationValue;
        float starDensity = Enviro3StarDensityValue;
        float starIntensityMultiplier = starAnimation <= 1f
            ? Mathf.Lerp(0.55f, 1f, starAnimation)
            : Mathf.Lerp(1f, 1.85f, starAnimation - 1f);
        float starDensityMultiplier = starDensity <= 1f
            ? Mathf.Lerp(0f, 1f, starDensity)
            : Mathf.Lerp(1f, 1.22f, starDensity - 1f);
        float galaxyIntensityMultiplier = starAnimation <= 1f
            ? Mathf.Lerp(0.90f, 1f, starAnimation)
            : Mathf.Lerp(1f, 1.12f, starAnimation - 1f);
        settings.starIntensityCurve = BuildConstantCurve(Mathf.Max(0f, stars * starIntensityMultiplier * starDensityMultiplier));
        settings.galaxyIntensityCurve = BuildConstantCurve(Mathf.Max(0f, galaxy * galaxyIntensityMultiplier));
        settings.moonGlowIntensityCurve = BuildConstantCurve(Mathf.Max(0f, moonGlow));
        settings.sunScale = 0.75f;
        settings.moonScale = moonEnabled ? Mathf.Clamp(moonScale, 0.1f, 20.0f) : 0f;
        if (moonEnabled)
            settings.moonPhase = 0f;
        settings.starsTwinklingSpeed = starAnimation <= 0.001f
            ? 0f
            : starAnimation <= 1f
                ? Mathf.Lerp(0.08f, 0.18f, starAnimation)
                : Mathf.Lerp(0.18f, 0.72f, starAnimation - 1f);

        if (enviro3Manager.Sky.mySkyboxMat == null)
            enviro3Manager.Sky.SetupSkybox();

        if (enviro3Manager.Sky.mySkyboxMat != null)
        {
            RenderSettings.skybox = enviro3Manager.Sky.mySkyboxMat;
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroSkyRotationShaderId, BuildEnviro3SkyRotationVector(skyRotationYaw));
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroFloatingSkyFillShaderId, BuildEnviro3FloatingSkyFillVector());
            enviro3Manager.Sky.mySkyboxMat.SetColor(EnviroStageAuroraColorShaderId, enviro3StageAuroraColor);
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroStageAuroraParamsShaderId, enviro3StageAuroraParams);
            enviro3Manager.Sky.mySkyboxMat.SetFloat(EnviroStageStarDensityShaderId, Mathf.Max(0f, starDensity - 1f));
        }
    }

    private void ApplyEnviro3MoonModeOverride(ref bool moonEnabled, ref float moonGlow, ref float moonScale)
    {
        switch ((GuitarBridgeServer.TabsEnviroMoonMode)Enviro3MoonModeIndexValue)
        {
            case GuitarBridgeServer.TabsEnviroMoonMode.Off:
                moonEnabled = false;
                moonGlow = 0f;
                moonScale = 0f;
                break;
            case GuitarBridgeServer.TabsEnviroMoonMode.Big:
                moonEnabled = true;
                moonGlow = Mathf.Max(moonGlow, 1.20f);
                moonScale = Mathf.Max(moonScale, 8.0f);
                break;
            case GuitarBridgeServer.TabsEnviroMoonMode.Giant:
                moonEnabled = true;
                moonGlow = Mathf.Max(moonGlow, 2.35f);
                moonScale = Mathf.Max(moonScale, 18.2f);
                break;
        }
    }

    private void ApplyEnviro3CelestialOverridesIfNeeded()
    {
        if (!ShouldRefreshEnviro3CelestialOverrides())
            return;

        ApplyEnviro3CelestialOverrides();
    }

    private bool ShouldRefreshEnviro3CelestialOverrides()
    {
        if (!enviro3CelestialOverridesApplied)
            return true;

        float skyPitch = Enviro3SkyCameraPitchValue;
        int moonModeIndex = Enviro3MoonModeIndexValue;
        return appliedEnviro3CelestialMoonModeIndex != moonModeIndex ||
               appliedEnviro3CelestialHasMoonOverride != enviro3HasMoonRotationOverride ||
               Mathf.Abs(appliedEnviro3CelestialMoonRotationX - enviro3MoonRotationOverrideX) > 0.0001f ||
               Mathf.Abs(appliedEnviro3CelestialMoonRotationY - enviro3MoonRotationOverrideY) > 0.0001f ||
               Mathf.Abs(appliedEnviro3CelestialSkyPitch - skyPitch) > 0.0001f ||
               Mathf.Abs(appliedEnviro3CelestialSkyYaw - enviro3SkyRotationYaw) > 0.0001f ||
               appliedEnviro3CelestialAuroraColor != enviro3StageAuroraColor ||
               appliedEnviro3CelestialAuroraParams != enviro3StageAuroraParams;
    }

    private void ApplyEnviro3CelestialOverrides(bool force = false)
    {
        if (enviro3Manager == null)
            return;

        if (!force && !ShouldRefreshEnviro3CelestialOverrides())
            return;

        if (enviro3HasMoonRotationOverride)
        {
            enviro3Manager.moonRotationX = enviro3MoonRotationOverrideX;
            enviro3Manager.moonRotationY = enviro3MoonRotationOverrideY;
        }

        ApplyEnviro3MoonModePlacementOverride();

        if (enviro3Manager.Objects != null && enviro3Manager.Objects.moon != null)
            enviro3Manager.Objects.moon.transform.eulerAngles = new Vector3(enviro3Manager.moonRotationX, enviro3Manager.moonRotationY, 0f);

        if (enviro3Manager.Sky != null && enviro3Manager.Sky.mySkyboxMat != null)
        {
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroSkyRotationShaderId, BuildEnviro3SkyRotationVector(enviro3SkyRotationYaw));
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroFloatingSkyFillShaderId, BuildEnviro3FloatingSkyFillVector());
            enviro3Manager.Sky.mySkyboxMat.SetColor(EnviroStageAuroraColorShaderId, enviro3StageAuroraColor);
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroStageAuroraParamsShaderId, enviro3StageAuroraParams);
        }

        enviro3CelestialOverridesApplied = true;
        appliedEnviro3CelestialMoonModeIndex = Enviro3MoonModeIndexValue;
        appliedEnviro3CelestialHasMoonOverride = enviro3HasMoonRotationOverride;
        appliedEnviro3CelestialMoonRotationX = enviro3MoonRotationOverrideX;
        appliedEnviro3CelestialMoonRotationY = enviro3MoonRotationOverrideY;
        appliedEnviro3CelestialSkyPitch = Enviro3SkyCameraPitchValue;
        appliedEnviro3CelestialSkyYaw = enviro3SkyRotationYaw;
        appliedEnviro3CelestialAuroraColor = enviro3StageAuroraColor;
        appliedEnviro3CelestialAuroraParams = enviro3StageAuroraParams;
    }

    private void ApplyEnviro3MoonModePlacementOverride()
    {
        switch ((GuitarBridgeServer.TabsEnviroMoonMode)Enviro3MoonModeIndexValue)
        {
            case GuitarBridgeServer.TabsEnviroMoonMode.Big:
                enviro3Manager.moonRotationX = 1.8f;
                enviro3Manager.moonRotationY = 180f;
                break;
            case GuitarBridgeServer.TabsEnviroMoonMode.Giant:
                enviro3Manager.moonRotationX = 0.35f;
                enviro3Manager.moonRotationY = 180f;
                break;
        }
    }

    private Vector4 BuildEnviro3SkyRotationVector(float yawDegrees)
    {
        float yawRadians = yawDegrees * Mathf.Deg2Rad;
        float pitchRadians = Enviro3SkyCameraPitchValue * Mathf.Deg2Rad;
        return new Vector4(
            Mathf.Cos(yawRadians),
            Mathf.Sin(yawRadians),
            Mathf.Cos(pitchRadians),
            Mathf.Sin(pitchRadians));
    }

    private static Vector4 BuildEnviro3FloatingSkyFillVector()
    {
        // Enviro normally masks stars/galaxy below the horizon. The highway camera can pitch
        // the sky down, so extend that mask beneath the visible lower sky to avoid an empty band.
        return new Vector4(1f, -1.42f, -0.82f, 0f);
    }

    private void ApplyEnviro3Aurora(bool enabled, Color color, float brightness, float contrast, float intensity, float scale, float speed)
    {
        if (enviro3Manager == null || enviro3Manager.Aurora == null || enviro3Manager.Aurora.Settings == null)
        {
            Shader.SetGlobalFloat("_Aurora", 0f);
            SetEnviro3StageAurora(false, Color.black, 0f, 0f);
            return;
        }

        Enviro.EnviroAurora settings = enviro3Manager.Aurora.Settings;
        settings.useAurora = enabled;
        Color stockAuroraColor = color.g > color.r && color.g > color.b * 0.68f
            ? Color.Lerp(color, new Color(0.22f, 0.76f, 0.92f, color.a), 0.46f)
            : color;
        settings.auroraColor = stockAuroraColor;
        settings.auroraBrightness = brightness * 0.58f;
        settings.auroraContrast = contrast;
        settings.auroraIntensityModifier = Mathf.Clamp01(intensity * 0.68f);
        settings.auroraIntensity = BuildConstantCurve(1f);
        settings.auroraHeight = 8200f;
        settings.auroraScale = Mathf.Clamp(scale, 0f, 0.025f);
        settings.auroraSpeed = Mathf.Clamp(speed, 0f, 0.1f);
        settings.auroraSteps = 24;
        settings.auroraLayer1Settings = new Vector4(0.24f, 0.10f, 0f, 0.60f);
        settings.auroraLayer2Settings = new Vector4(2.2f, 3.8f, 0f, 0.42f);
        settings.auroraColorshiftSettings = new Vector4(0.16f, 0.09f, 0f, 4.8f);

        SetEnviroModuleActive(enviro3Manager.Aurora, enabled);
        Shader.SetGlobalFloat("_Aurora", enabled ? 1f : 0f);
        enviro3Manager.Aurora.UpdateAuroraShader();
        SetEnviro3StageAurora(enabled, color, brightness, intensity);
    }

    private void SetEnviro3StageAurora(bool enabled, Color color, float brightness, float intensity)
    {
        float stageIntensity = enabled
            ? Mathf.Clamp01(intensity * 1.45f) * Mathf.Clamp(brightness / 95f, 0.42f, 1.22f)
            : 0f;
        Color painterAuroraColor = color.g > color.r && color.g > color.b * 0.68f
            ? Color.Lerp(color, new Color(0.76f, 0.36f, 1.08f, color.a), 0.24f)
            : Color.Lerp(color, new Color(0.88f, 0.30f, 1.10f, color.a), 0.10f);
        enviro3StageAuroraColor = enabled ? ScaleColor(painterAuroraColor, 1.08f) : Color.black;
        enviro3StageAuroraParams = new Vector4(stageIntensity, -0.30f, 0.62f, 0.10f);
        Shader.SetGlobalColor(EnviroStageAuroraColorShaderId, enviro3StageAuroraColor);
        Shader.SetGlobalVector(EnviroStageAuroraParamsShaderId, enviro3StageAuroraParams);

        if (enviro3Manager != null && enviro3Manager.Sky != null && enviro3Manager.Sky.mySkyboxMat != null)
        {
            enviro3Manager.Sky.mySkyboxMat.SetColor(EnviroStageAuroraColorShaderId, enviro3StageAuroraColor);
            enviro3Manager.Sky.mySkyboxMat.SetVector(EnviroStageAuroraParamsShaderId, enviro3StageAuroraParams);
        }
    }

    private void ApplyEnviro3FlatClouds(bool enabled, Color color, float cirrusAlpha, float cirrusCoverage, float flatCoverage, float density, float altitude)
    {
        if (enviro3Manager == null || enviro3Manager.FlatClouds == null || enviro3Manager.FlatClouds.settings == null)
        {
            Shader.SetGlobalFloat("_CirrusClouds", 0f);
            Shader.SetGlobalFloat("_FlatClouds", 0f);
            return;
        }

        Enviro.EnviroFlatClouds settings = enviro3Manager.FlatClouds.settings;
        settings.useCirrusClouds = enabled;
        settings.useFlatClouds = enabled;
        settings.cirrusCloudsAlpha = Mathf.Clamp01(cirrusAlpha);
        settings.cirrusCloudsCoverage = Mathf.Clamp01(cirrusCoverage);
        settings.cirrusCloudsColorPower = 1.10f;
        settings.cirrusCloudsColor = BuildConstantGradient(color);
        settings.cirrusCloudsWindIntensity = 0.12f;
        settings.flatCloudsLightColor = BuildConstantGradient(ScaleColor(color, 1.15f));
        settings.flatCloudsAmbientColor = BuildConstantGradient(ScaleColor(color, 0.46f));
        settings.flatCloudsLightIntensity = 0.55f;
        settings.flatCloudsAmbientIntensity = 0.35f;
        settings.flatCloudsShadowIntensity = 0.35f;
        settings.flatCloudsShadowSteps = 4f;
        settings.flatCloudsHGPhase = 0.62f;
        settings.flatCloudsCoverage = Mathf.Clamp(flatCoverage, 0f, 2f);
        settings.flatCloudsDensity = Mathf.Clamp(density, 0f, 2f);
        settings.flatCloudsAltitude = Mathf.Max(1f, altitude);
        settings.flatCloudsTonemapping = false;
        settings.flatCloudsBaseTiling = 4.5f;
        settings.flatCloudsDetailTiling = 12f;
        settings.flatCloudsWindIntensity = 0.08f;
            settings.flatCloudsDetailWindIntensity = 0.14f;

        if (enabled)
            SetEnviroModuleActive(enviro3Manager.FlatClouds, true);
        else
        {
            Shader.SetGlobalFloat("_CirrusClouds", 0f);
            Shader.SetGlobalFloat("_FlatClouds", 0f);
            SetEnviroModuleActive(enviro3Manager.FlatClouds, false);
        }
    }

    private void DisableEnviro3VolumetricClouds()
    {
        if (enviro3Manager == null || enviro3Manager.VolumetricClouds == null)
            return;

        Enviro.EnviroVolumetricCloudsModule clouds = enviro3Manager.VolumetricClouds;
        if (clouds.settingsQuality != null)
            clouds.settingsQuality.volumetricClouds = false;
        SetEnviroModuleActive(clouds, false);
    }

    private void ApplyEnviro3VolumetricClouds(
        float coverage,
        float density,
        float bottomHeight,
        float topHeight,
        float scattering,
        float exposure,
        Color directLight,
        Color ambientLight,
        float travelSpeed,
        EnviroCloudArtStyle artStyle = EnviroCloudArtStyle.Default)
    {
        if (enviro3Manager == null || enviro3Manager.VolumetricClouds == null)
            return;

        Enviro.EnviroVolumetricCloudsModule clouds = enviro3Manager.VolumetricClouds;
        if (!Enviro3CloudsEnabledValue)
        {
            if (clouds.settingsQuality != null)
                clouds.settingsQuality.volumetricClouds = false;
            SetEnviroModuleActive(clouds, false);
            return;
        }

        if (clouds.settingsQuality == null || clouds.settingsGlobal == null || clouds.settingsVolume == null)
            return;

        SetEnviroModuleActive(clouds, true);

        clouds.settingsQuality.volumetricClouds = true;
        clouds.settingsQuality.lightningSupport = false;
        clouds.settingsQuality.variableBottomNoise = true;
        clouds.settingsQuality.downsampling = 2;
        clouds.settingsQuality.stepsLayer1 = 128;
        clouds.settingsQuality.stepsLayer2 = 64;
        clouds.settingsQuality.blueNoiseIntensity = 2f;
        clouds.settingsQuality.reprojectionBlendTime = 2f;
        clouds.settingsQuality.lodDistance = 0.7f;

        clouds.settingsGlobal.depthBlending = true;
        clouds.settingsGlobal.depthTest = true;
        clouds.settingsGlobal.cloudShadows = false;
        clouds.settingsGlobal.cloudsTravelSpeed = Mathf.Clamp(travelSpeed, 0f, 0.04f);
        clouds.settingsGlobal.cloudsWorldScale = 5000000f;
        clouds.settingsGlobal.maxRenderDistance = Mathf.Clamp(topHeight * 5.5f, 16000f, 32000f);
        clouds.settingsGlobal.atmosphereColorSaturateDistance = Mathf.Clamp(topHeight * 2.75f, 9000f, 18000f);
        float ambientIntensity = 1f;
        float densitySmoothness = 1.05f;
        float edgeHighlightStrength = 0f;
        float silverLiningSpread = 0.50f;
        float silverLiningIntensity = 1.42f;
        float multiScatterStrength = 0.541f;
        float multiScatterFalloff = 0.201f;
        float ambientFloor = 0.19f;
        float absorbtion = 0.61f;
        float curlIntensity = 0.757f;
        float baseNoiseUV = 34f;
        float detailNoiseUV = 48f;
        float baseErosionIntensity = 0.12f;
        float detailErosionIntensity = 0.34f;
        float baseNoiseMultiplier = 1.1f;
        float detailNoiseMultiplier = 1f;
        float cloudsTypeModifier = 0.94f;
        float dilateCoverage = 0.68f;
        float rampShape = 0.58f;
        float bottomShape = -0.34f;
        float midShape = -0.15f;
        float topShape = -0.70f;
        float topLayer = 0.06f;
        float cloudTypeShaping = 0.82f;

        switch (artStyle)
        {
            case EnviroCloudArtStyle.Horror:
                ambientIntensity = 0.36f;
                densitySmoothness = 0.72f;
                edgeHighlightStrength = 0.12f;
                silverLiningSpread = 0.28f;
                silverLiningIntensity = 0.42f;
                multiScatterStrength = 0.22f;
                multiScatterFalloff = 0.34f;
                ambientFloor = 0.025f;
                absorbtion = 0.86f;
                curlIntensity = 1.06f;
                baseNoiseUV = 25f;
                detailNoiseUV = 70f;
                baseErosionIntensity = 0.34f;
                detailErosionIntensity = 0.62f;
                baseNoiseMultiplier = 1.24f;
                detailNoiseMultiplier = 1.14f;
                cloudsTypeModifier = 0.78f;
                dilateCoverage = 0.46f;
                rampShape = 0.40f;
                bottomShape = -0.60f;
                midShape = -0.36f;
                topShape = -0.92f;
                topLayer = 0.015f;
                cloudTypeShaping = 0.96f;
                break;
            case EnviroCloudArtStyle.Galaxy:
                ambientIntensity = 0.38f;
                densitySmoothness = 1.08f;
                edgeHighlightStrength = 0.07f;
                silverLiningSpread = 0.42f;
                silverLiningIntensity = 0.62f;
                multiScatterStrength = 0.36f;
                multiScatterFalloff = 0.26f;
                ambientFloor = 0.024f;
                absorbtion = 0.86f;
                curlIntensity = 0.46f;
                baseNoiseUV = 12f;
                detailNoiseUV = 24f;
                baseErosionIntensity = 0.10f;
                detailErosionIntensity = 0.28f;
                baseNoiseMultiplier = 0.84f;
                detailNoiseMultiplier = 0.64f;
                cloudsTypeModifier = 0.44f;
                dilateCoverage = 0.95f;
                rampShape = 0.54f;
                bottomShape = -0.40f;
                midShape = 0.02f;
                topShape = -0.72f;
                topLayer = 0.055f;
                cloudTypeShaping = 0.52f;
                break;
            case EnviroCloudArtStyle.ThickMoonlit:
                ambientIntensity = 0.44f;
                densitySmoothness = 0.88f;
                edgeHighlightStrength = 0.14f;
                silverLiningSpread = 0.36f;
                silverLiningIntensity = 0.84f;
                multiScatterStrength = 0.31f;
                multiScatterFalloff = 0.32f;
                ambientFloor = 0.028f;
                absorbtion = 0.86f;
                curlIntensity = 0.92f;
                baseNoiseUV = 30f;
                detailNoiseUV = 46f;
                baseErosionIntensity = 0.14f;
                detailErosionIntensity = 0.38f;
                baseNoiseMultiplier = 1.10f;
                detailNoiseMultiplier = 0.88f;
                cloudsTypeModifier = 0.78f;
                dilateCoverage = 0.58f;
                rampShape = 0.46f;
                bottomShape = -0.36f;
                midShape = -0.18f;
                topShape = -0.82f;
                topLayer = 0.035f;
                cloudTypeShaping = 0.82f;
                break;
            case EnviroCloudArtStyle.Aurora:
                ambientIntensity = 0.50f;
                densitySmoothness = 0.98f;
                edgeHighlightStrength = 0.07f;
                silverLiningSpread = 0.50f;
                silverLiningIntensity = 0.58f;
                multiScatterStrength = 0.32f;
                multiScatterFalloff = 0.29f;
                ambientFloor = 0.038f;
                absorbtion = 0.78f;
                curlIntensity = 0.56f;
                baseNoiseUV = 16f;
                detailNoiseUV = 31f;
                baseErosionIntensity = 0.105f;
                detailErosionIntensity = 0.30f;
                baseNoiseMultiplier = 0.92f;
                detailNoiseMultiplier = 0.68f;
                cloudsTypeModifier = 0.54f;
                dilateCoverage = 0.74f;
                rampShape = 0.54f;
                bottomShape = -0.52f;
                midShape = -0.10f;
                topShape = -0.86f;
                topLayer = 0.020f;
                cloudTypeShaping = 0.62f;
                break;
        }

        float cloudAmount = Enviro3CloudAmountValue;
        float cloudThickness = Enviro3CloudThicknessValue;
        float cloudConnectivity = Enviro3CloudConnectivityValue;
        float cloudContrast = Enviro3CloudContrastValue;
        float amountDelta = cloudAmount - 1f;
        float connectivityDelta = cloudConnectivity - 1f;
        float contrastHigh = Mathf.Max(0f, cloudContrast - 1f);
        float contrastLow = Mathf.Max(0f, 1f - cloudContrast);

        coverage = Mathf.Clamp(coverage + amountDelta * 0.32f, -1f, 1f);
        density = Mathf.Clamp(
            density
            * Mathf.Lerp(0.55f, 1.45f, cloudAmount * 0.5f)
            * Mathf.Lerp(0.38f, 1.62f, cloudThickness * 0.5f),
            0f,
            2f);
        scattering = Mathf.Clamp(scattering * Mathf.Lerp(0.78f, 1.18f, cloudThickness * 0.5f), 0f, 6f);
        exposure = Mathf.Clamp(exposure * Mathf.Lerp(0.84f, 1.12f, cloudAmount * 0.5f), 0f, 2f);

        dilateCoverage = Mathf.Clamp01(dilateCoverage + amountDelta * 0.05f + connectivityDelta * 0.22f);
        baseErosionIntensity = Mathf.Clamp01(baseErosionIntensity - connectivityDelta * 0.13f);
        detailErosionIntensity = Mathf.Clamp01(detailErosionIntensity - connectivityDelta * 0.15f);
        cloudsTypeModifier = Mathf.Clamp(cloudsTypeModifier + connectivityDelta * 0.16f, 0f, 2f);
        cloudTypeShaping = Mathf.Clamp(cloudTypeShaping - connectivityDelta * 0.12f, 0f, 2f);
        curlIntensity = Mathf.Clamp(curlIntensity + Mathf.Max(0f, -connectivityDelta) * 0.18f - Mathf.Max(0f, connectivityDelta) * 0.08f, 0f, 2f);

        directLight = ScaleColor(directLight, 1f + contrastHigh * 0.42f - contrastLow * 0.22f);
        ambientLight = ScaleColor(ambientLight, 1f - contrastHigh * 0.48f + contrastLow * 0.60f);
        ambientIntensity = Mathf.Clamp(ambientIntensity * (1f - contrastHigh * 0.20f + contrastLow * 0.28f), 0f, 2f);
        ambientFloor = Mathf.Clamp01(ambientFloor * (1f - contrastHigh * 0.55f + contrastLow * 0.90f));
        absorbtion = Mathf.Clamp(absorbtion + contrastHigh * 0.16f - contrastLow * 0.12f, 0f, 2f);
        silverLiningIntensity = Mathf.Clamp(silverLiningIntensity * (1f + contrastHigh * 0.45f - contrastLow * 0.30f), 0f, 3f);
        edgeHighlightStrength = Mathf.Clamp(edgeHighlightStrength + contrastHigh * 0.10f - contrastLow * 0.05f, 0f, 1f);
        multiScatterStrength = Mathf.Clamp(multiScatterStrength * (1f + contrastHigh * 0.20f - contrastLow * 0.15f), 0f, 2f);

        clouds.settingsGlobal.ambientLighIntensity = ambientIntensity;
        clouds.settingsGlobal.sunLightColorGradient = BuildConstantGradient(directLight);
        clouds.settingsGlobal.moonLightColorGradient = BuildConstantGradient(ScaleColor(directLight, 0.85f));
        clouds.settingsGlobal.ambientColorGradient = BuildConstantGradient(ambientLight);
        clouds.settingsGlobal.sunLightColor = directLight;
        clouds.settingsGlobal.moonLightColor = ScaleColor(directLight, 0.85f);
        clouds.settingsGlobal.ambientColor = ambientLight;
        clouds.settingsGlobal.floatingPointOriginMod = Vector3.zero;

        Enviro.EnviroCloudLayerSettings volume = clouds.settingsVolume;
        float bottom = Mathf.Max(120f, bottomHeight);
        volume.bottomCloudsHeight = bottom;
        volume.topCloudsHeight = Mathf.Max(bottom + 3400f, topHeight);
        volume.coverage = Mathf.Clamp(coverage, -1f, 1f);
        volume.density = Mathf.Clamp(density, 0f, 2f);
        volume.densitySmoothness = densitySmoothness;
        volume.scatteringIntensity = Mathf.Clamp(scattering, 0f, 6f);
        volume.exposure = Mathf.Clamp(exposure, 0f, 2f);
        volume.lightningIntensity = 0f;
        volume.lightStepModifier = 0.011f;
        volume.edgeHighlightStrength = edgeHighlightStrength;
        volume.silverLiningSpread = silverLiningSpread;
        volume.silverLiningIntensity = silverLiningIntensity;
        volume.multiScatterStrength = multiScatterStrength;
        volume.multiScatterFalloff = multiScatterFalloff;
        volume.ambientFloor = ambientFloor;
        volume.absorbtion = absorbtion;
        volume.curlIntensity = curlIntensity;
        volume.worleyFreq1 = 10f;
        volume.worleyFreq2 = 34f;
        volume.baseNoiseUV = baseNoiseUV;
        volume.detailNoiseUV = detailNoiseUV;
        volume.baseNoiseUVMultiplier = 0.90f;
        volume.detailNoiseUVMultiplier = 1.08f;
        volume.baseErosionIntensity = baseErosionIntensity;
        volume.baseNoiseMultiplier = baseNoiseMultiplier;
        volume.detailErosionIntensity = detailErosionIntensity;
        volume.detailNoiseMultiplier = detailNoiseMultiplier;
        volume.cloudsWindDirectionXModifier = 1f;
        volume.cloudsWindDirectionYModifier = 1f;
        volume.windSpeedModifier = 0.05f;
        volume.windUpwards = 0.025f;
        volume.cloudsTypeModifier = cloudsTypeModifier;
        volume.dilateCoverage = dilateCoverage;
        volume.dilateType = 0f;
        volume.locationOffset = new Vector2(0.8f, 0f);
        volume.rampShape = rampShape;
        volume.bottomShape = bottomShape;
        volume.midShape = midShape;
        volume.topShape = topShape;
        volume.topLayer = topLayer;
        volume.cloudTypeShaping = cloudTypeShaping;
    }

    private void DestroyEnviro3Sky()
    {
        if (enviro3ConfiguredCamera != null)
            SetEnviroRendererEnabled(enviro3ConfiguredCamera, false);
        enviro3ConfiguredCamera = null;

        if (enviro3Manager != null)
        {
            if (enviro3Manager.Aurora != null && enviro3Manager.Aurora.Settings != null)
            {
                enviro3Manager.Aurora.Settings.useAurora = false;
                enviro3Manager.Aurora.UpdateAuroraShader();
            }

            if (enviro3Manager.FlatClouds != null && enviro3Manager.FlatClouds.settings != null)
            {
                enviro3Manager.FlatClouds.settings.useCirrusClouds = false;
                enviro3Manager.FlatClouds.settings.useFlatClouds = false;
            }

            if (enviro3Manager.VolumetricClouds != null && enviro3Manager.VolumetricClouds.settingsQuality != null)
                enviro3Manager.VolumetricClouds.settingsQuality.volumetricClouds = false;

            enviro3Manager.DisableModules();
        }

        if (enviro3Object != null)
        {
            enviro3Object.SetActive(false);
            Object.Destroy(enviro3Object);
        }

        enviro3Object = null;
        enviro3Manager = null;
        appliedEnviro3MoodIndex = int.MinValue;
        appliedEnviro3MoonModeIndex = int.MinValue;
        appliedEnviro3CloudsEnabled = true;
        appliedEnviro3CloudModifiers = UnappliedEnviro3CloudModifiers;
        appliedEnviro3StarAnimation = -1f;
        appliedEnviro3StarDensity = -1f;
        ResetEnviro3RenderSettingsCache();
        enviro3StageAuroraColor = Color.black;
        enviro3StageAuroraParams = Vector4.zero;
        Shader.SetGlobalFloat("_Aurora", 0f);
        Shader.SetGlobalFloat("_CirrusClouds", 0f);
        Shader.SetGlobalFloat("_FlatClouds", 0f);
        Shader.SetGlobalFloat("_EnviroActive", 0f);
        Shader.SetGlobalFloat(EnviroStageSkyPitchShaderId, 0f);
        Shader.SetGlobalColor(EnviroStageAuroraColorShaderId, Color.black);
        Shader.SetGlobalVector(EnviroStageAuroraParamsShaderId, Vector4.zero);
        Shader.SetGlobalFloat(EnviroStageStarDensityShaderId, 0f);
    }

    private void ResetEnviro3RenderSettingsCache()
    {
        enviro3RenderSettingsApplied = false;
        enviro3ShaderGlobalsApplied = false;
        enviro3CelestialOverridesApplied = false;
        appliedEnviro3RenderSkyPitch = float.NaN;
        appliedEnviro3RenderStarDensity = float.NaN;
        appliedEnviro3CelestialMoonModeIndex = int.MinValue;
        appliedEnviro3CelestialHasMoonOverride = false;
        appliedEnviro3CelestialMoonRotationX = float.NaN;
        appliedEnviro3CelestialMoonRotationY = float.NaN;
        appliedEnviro3CelestialSkyPitch = float.NaN;
        appliedEnviro3CelestialSkyYaw = float.NaN;
        appliedEnviro3CelestialAuroraColor = Color.clear;
        appliedEnviro3CelestialAuroraParams = UnappliedEnviro3CloudModifiers;
    }

    private static void SetEnviroModuleActive(EnviroModule module, bool active)
    {
        if (module == null)
            return;

        if (active)
        {
            if (!module.active)
            {
                module.active = true;
                module.Enable();
            }
            return;
        }

        if (module.active)
            module.Disable();
        module.active = false;
    }

    private static Gradient BuildConstantGradient(Color color)
    {
        Gradient gradient = new Gradient();
        gradient.SetKeys(
            new[] { new GradientColorKey(color, 0f), new GradientColorKey(color, 1f) },
            new[] { new GradientAlphaKey(color.a, 0f), new GradientAlphaKey(color.a, 1f) });
        return gradient;
    }

    private static AnimationCurve BuildConstantCurve(float value)
    {
        return new AnimationCurve(new Keyframe(0f, value), new Keyframe(1f, value));
    }

    private static Color ScaleColor(Color color, float multiplier)
    {
        return new Color(color.r * multiplier, color.g * multiplier, color.b * multiplier, color.a);
    }

    private static void DisableImportedSceneBehaviours(GameObject sceneObject)
    {
        if (sceneObject == null)
            return;

        Camera[] cameras = sceneObject.GetComponentsInChildren<Camera>(true);
        for (int i = 0; i < cameras.Length; i++)
            cameras[i].enabled = false;

        AudioListener[] audioListeners = sceneObject.GetComponentsInChildren<AudioListener>(true);
        for (int i = 0; i < audioListeners.Length; i++)
            audioListeners[i].enabled = false;
    }

    private static void SetLayerRecursively(GameObject gameObject, int layer)
    {
        if (gameObject == null)
            return;

        gameObject.layer = layer;
        Transform transform = gameObject.transform;
        for (int i = 0; i < transform.childCount; i++)
            SetLayerRecursively(transform.GetChild(i).gameObject, layer);
    }

    private void SetProceduralStageVisible(bool groundVisible, bool horizonVisible, bool decorVisible, bool mountainVisible = false)
    {
        if (!applyHighwayOverrides)
            return;

        proceduralGroundVisible = groundVisible;
        proceduralHorizonVisible = horizonVisible;
        proceduralMountainVisible = mountainVisible;
        proceduralDecorVisible = decorVisible;
        proceduralStageVisible = groundVisible || horizonVisible || mountainVisible || decorVisible;

        SetRendererVisible(floorRenderer, groundVisible);
        SetRendererVisible(floorGridRenderer, groundVisible);
        SetRendererVisible(horizonRenderer, horizonVisible);
        SetRendererVisible(horizonCoreRenderer, horizonVisible);
        SetEnviroMountainVisible(mountainVisible);

        if (!decorVisible)
        {
            for (int i = 0; i < farLightRenderers.Count; i++)
                SetRendererVisible(farLightRenderers[i], false);
            for (int i = 0; i < stageClouds.Count; i++)
                SetRendererVisible(stageClouds[i].Renderer, false);
        }
    } 

    private void SetProceduralStageRenderMode(bool renderAfterSky)
    {
        proceduralStageRenderedAfterSky = renderAfterSky;
        int queueBase = renderAfterSky ? EnviroStageRenderQueueBase : StageRenderQueueBase;
        int zTest = renderAfterSky ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always;
        int floorSortingOrder = renderAfterSky ? EnviroFloorSortingOrder : DefaultStageSortingOrder;
        int floorGridSortingOrder = renderAfterSky ? EnviroFloorGridSortingOrder : DefaultStageSortingOrder;
        int horizonSortingOrder = renderAfterSky ? EnviroHorizonSortingOrder : DefaultStageSortingOrder;
        int horizonCoreSortingOrder = renderAfterSky ? EnviroHorizonCoreSortingOrder : DefaultStageSortingOrder;

        ApplyMaterialRenderState(floorMaterial, queueBase, zTest);
        ApplyMaterialRenderState(floorGridMaterial, queueBase + 1, zTest);
        ApplyRendererSortingOrder(floorRenderer, floorSortingOrder);
        ApplyRendererSortingOrder(floorGridRenderer, floorGridSortingOrder);
        ApplyEnviroMountainMaterialRenderState(queueBase + 2, zTest);
        // Dome horizon is in the background queue, so Always preserves the authored glow.
        // Enviro horizon has to render after the skybox, which puts it in the transparent
        // phase; there it must respect existing highway depth or it can draw over gameplay.
        int horizonZTest = renderAfterSky ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always;
        ApplyMaterialRenderState(horizonMaterial, queueBase + 24, horizonZTest);
        ApplyMaterialRenderState(horizonCoreMaterial, queueBase + 25, horizonZTest);
        ApplyRendererSortingOrder(horizonRenderer, horizonSortingOrder);
        ApplyRendererSortingOrder(horizonCoreRenderer, horizonCoreSortingOrder);
        ApplyEnviroMountainMaterialToTerrains();

        for (int i = 0; i < farLightMaterials.Count; i++)
            ApplyMaterialRenderState(farLightMaterials[i], queueBase + 2, zTest);
        for (int i = 0; i < stageClouds.Count; i++)
            ApplyMaterialRenderState(stageClouds[i].Material, queueBase + 1, zTest);
    }

    private static void ApplyMaterialRenderState(Material material, int renderQueue, int zTest)
    {
        if (material == null)
            return;

        material.renderQueue = renderQueue;
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", zTest);
    }

    private static void ApplyRendererSortingOrder(Renderer renderer, int sortingOrder)
    {
        if (renderer != null)
            renderer.sortingOrder = sortingOrder;
    }

    private void ApplyEnviroMountainMaterialRenderState(int renderQueue, int zTest)
    {
        if (enviroMountainLayerMaterials.Count == 0)
        {
            ApplyMaterialRenderState(enviroMountainMaterial, renderQueue, zTest);
            return;
        }

        int firstLayerQueue = renderQueue - Mathf.Max(0, EnviroMountainSilhouetteLayerCount - 1);
        for (int i = 0; i < enviroMountainLayerMaterials.Count; i++)
            ApplyMaterialRenderState(enviroMountainLayerMaterials[i], firstLayerQueue + i, zTest);

        for (int i = 0; i < enviroMountainRenderers.Count; i++)
        {
            Renderer renderer = enviroMountainRenderers[i];
            if (renderer != null)
                renderer.sortingOrder = EnviroMountainSortingOrderBase + i;
        }
    }

    private void ApplyEnviroMountainMaterialToTerrains()
    {
        if (enviroMountainMaterial == null && enviroMountainLayerMaterials.Count == 0)
            return;

        for (int i = 0; i < enviroMountainTerrains.Count; i++)
        {
            Terrain terrain = enviroMountainTerrains[i];
            if (terrain != null)
                terrain.materialTemplate = enviroMountainMaterial;
        }

        for (int i = 0; i < enviroMountainRenderers.Count; i++)
        {
            Renderer renderer = enviroMountainRenderers[i];
            if (renderer == null)
                continue;

            Material layerMaterial = i < enviroMountainLayerMaterials.Count
                ? enviroMountainLayerMaterials[i]
                : enviroMountainMaterial;
            if (layerMaterial != null)
                renderer.sharedMaterial = layerMaterial;
        }
    }

    private static void SetRendererVisible(Renderer renderer, bool visible)
    {
        if (renderer != null && renderer.enabled != visible)
            renderer.enabled = visible;
    }

    private void SetEnviroMountainVisible(bool visible)
    {
        if (enviroMountainObject != null && enviroMountainObject.activeSelf != visible)
            enviroMountainObject.SetActive(visible);

        for (int i = 0; i < enviroMountainTerrains.Count; i++)
        {
            Terrain terrain = enviroMountainTerrains[i];
            if (terrain != null && terrain.enabled != visible)
                terrain.enabled = visible;
        }

        for (int i = 0; i < enviroMountainRenderers.Count; i++)
            SetRendererVisible(enviroMountainRenderers[i], visible);
    }

    private void UpdateDomePlacement(Camera camera)
    {
        if (domeObject == null)
            return;

        domeObject.transform.position = camera.transform.position;
        domeObject.transform.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
        domeObject.transform.localScale = Vector3.one * DomeScale;

        if (domeStarsObject != null)
        {
            domeStarsObject.transform.position = camera.transform.position;
            domeStarsObject.transform.rotation = Quaternion.Euler(0f, camera.transform.eulerAngles.y, 0f);
            domeStarsObject.transform.localScale = Vector3.one;
        }
    }

    private Vector3 GetRootWorldOffset()
    {
        return root != null ? root.transform.position : Vector3.zero;
    }

    private void UpdateStagePlacement(Camera camera)
    {
        if (!proceduralStageVisible)
            return;

        if (proceduralGroundVisible)
        {
            using (UpdateFloorPlacementProfilerMarker.Auto())
            {
                UpdateFloorPlacement(camera);
            }
        }

        if (proceduralHorizonVisible)
        {
            using (UpdateHorizonPlacementProfilerMarker.Auto())
            {
                UpdateHorizonPlacement(camera);
            }
        }

        if (proceduralMountainVisible)
            UpdateEnviroMountainPlacement(camera);

        if (proceduralDecorVisible)
        {
            UpdateFarLightPlacement(camera);
            UpdateStageCloudPlacement(camera);
        }
    }

    private void CreateStageGeometry()
    {
        floorObject = CreateQuad("NeonStageFloor", root.transform, out floorRenderer);
        EnsureCurvedMesh(floorObject, floorMeshCache, "NeonStageFloorMesh", FloorMeshSegments, FloorDepthSegments);
        floorTexture = BuildFloorBaseTexture(256, 256);
        floorMaterial = CreateTransparentTexturedMaterial(floorTexture, renderQueue: StageRenderQueueBase);
        if (floorRenderer != null)
            floorRenderer.sharedMaterial = floorMaterial;

        floorGridObject = CreateQuad("NeonStageFloorGrid", root.transform, out floorGridRenderer);
        EnsureCurvedMesh(floorGridObject, floorGridMeshCache, "NeonStageFloorGridMesh", FloorMeshSegments, FloorDepthSegments);
        floorGridTexture = BuildFloorGridTexture(512, 512);
        floorGridMaterial = CreateAdditiveTexturedMaterial(floorGridTexture, renderQueue: StageRenderQueueBase + 1);
        if (floorGridRenderer != null)
            floorGridRenderer.sharedMaterial = floorGridMaterial;

        CacheGroundControls();

        horizonObject = CreateQuad("NeonStageHorizonLine", root.transform, out horizonRenderer);
        EnsureCurvedMesh(horizonObject, horizonMeshCache, "NeonStageHorizonMesh", HorizonMeshSegments, 1);
        horizonMaterial = CreateHorizonMaterial(renderQueue: StageRenderQueueBase + 24);
        if (horizonRenderer != null)
        {
            horizonRenderer.sharedMaterial = horizonMaterial;
            horizonRenderer.sortingOrder = EnviroHorizonSortingOrder;
        }

        horizonCoreObject = CreateQuad("NeonStageHorizonCore", root.transform, out horizonCoreRenderer);
        EnsureCurvedMesh(horizonCoreObject, horizonCoreMeshCache, "NeonStageHorizonCoreMesh", HorizonMeshSegments, 1);
        horizonCoreMaterial = CreateHorizonMaterial(renderQueue: StageRenderQueueBase + 25);
        if (horizonCoreRenderer != null)
        {
            horizonCoreRenderer.sharedMaterial = horizonCoreMaterial;
            horizonCoreRenderer.sortingOrder = EnviroHorizonCoreSortingOrder;
        }

        EnsureEnviroMountainLayerMaterials(StageRenderQueueBase + 2);

        farLightTexture = BuildFarLightTexture(96, 96);

        for (int i = 0; i < FarLightOffsets.Length; i++)
        {
            Renderer farLightRenderer;
            GameObject farLight = CreateQuad($"NeonStageFarLight{i + 1}", root.transform, out farLightRenderer);
            Material farLightMaterial = CreateAdditiveTexturedMaterial(farLightTexture, renderQueue: StageRenderQueueBase + 2);
            farLightObjects.Add(farLight);
            farLightRenderers.Add(farLightRenderer);
            farLightMaterials.Add(farLightMaterial);
            if (farLightRenderer != null)
            {
                farLightRenderer.sharedMaterial = farLightMaterial;
                farLightRenderer.enabled = false;
            }
        }

        CreateStageClouds();
    }

    private void UpdateFloorPlacement(Camera camera)
    {
        if (floorObject == null && floorGridObject == null)
            return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float horizonLineDistance = Mathf.Max(1f, HorizonLineDistanceValue);
        float floorHorizonDistance = GetEffectiveHorizonLineDistance(horizonLineDistance);
        float floorWidth = UseEnviro3SkyValue && Enviro3ExtendedGroundEnabledValue
            ? FloorWidth * EnviroExtendedFloorWidthMultiplier
            : FloorWidth;
        float horizonLineY = HorizonLineYValue;
        float horizonCoreLineYOffset = HorizonCoreLineYOffsetValue;
        float floorHorizonOverlap = Mathf.Max(0f, FloorHorizonOverlapValue);
        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;
        Vector3 horizonCenter = camera.transform.position + (forwardFlat * floorHorizonDistance);
        horizonCenter.x = Mathf.Lerp(horizonCenter.x, boardMidX, 0.46f);
        horizonCenter.y = horizonLineY;

        Vector3 farCenter = horizonCenter;
        // The floor terminates into the visible horizon core, not below it. The horizon
        // renders after the floor, so this small overlap hides screen-space seams when curved.
        farCenter.y = horizonLineY + horizonCoreLineYOffset + floorHorizonOverlap;

        float horizontalDepth = Mathf.Max(1f, floorHorizonDistance - FloorNearDistance);
        Vector3 nearCenter = farCenter - (forwardFlat * horizontalDepth);
        nearCenter.y = FloorY;

        Vector3 center = (nearCenter + farCenter) * 0.5f;
        Vector3 depthAxis = farCenter - nearCenter;
        if (depthAxis.sqrMagnitude < 0.0001f)
            depthAxis = forwardFlat;

        float floorDepth = depthAxis.magnitude;
        depthAxis.Normalize();

        Vector3 rightAxis = Vector3.Cross(Vector3.up, forwardFlat);
        if (rightAxis.sqrMagnitude < 0.0001f)
            rightAxis = Vector3.right;
        rightAxis.Normalize();

        Vector3 normal = Vector3.Cross(rightAxis, depthAxis);
        if (normal.sqrMagnitude < 0.0001f)
            normal = Vector3.up;
        normal.Normalize();

        Quaternion rotation = Quaternion.LookRotation(normal, depthAxis);
        float curveDown = HorizonCurveDownValue;
        float curveTowardCamera = HorizonCurveTowardCameraValue;
        Vector3 rootOffset = GetRootWorldOffset();

        if (floorObject != null)
        {
            floorObject.transform.position = center + rootOffset;
            floorObject.transform.rotation = rotation;
            floorObject.transform.localScale = Vector3.one;
            ApplyCurvedFloorMesh(floorMeshCache, floorWidth, floorDepth, rotation, forwardFlat, curveDown, curveTowardCamera);
        }

        if (floorGridObject != null)
        {
            Vector3 gridCenter = center + (normal * 0.035f);
            floorGridObject.transform.position = gridCenter + rootOffset;
            floorGridObject.transform.rotation = rotation;
            floorGridObject.transform.localScale = Vector3.one;
            ApplyCurvedFloorMesh(floorGridMeshCache, floorWidth, floorDepth, rotation, forwardFlat, curveDown, curveTowardCamera);
        }
    }

    private float GetEffectiveHorizonLineDistance(float baseDistance)
    {
        baseDistance = Mathf.Max(1f, baseDistance);
        return UseEnviro3SkyValue && Enviro3ExtendedGroundEnabledValue
            ? baseDistance * EnviroExtendedFloorDistanceMultiplier
            : baseDistance;
    }

    private void RefreshFloorTexturesIfNeeded()
    {
        using (RefreshFloorTexturesProfilerMarker.Auto())
        {
            if (!applyHighwayOverrides || floorMaterial == null || floorGridMaterial == null)
                return;

            if (!proceduralGroundVisible)
                return;

            float groundDarkness = GroundDarknessValue;
            float groundGradientStart = GroundGradientStartValue;
            float groundGradientBrightness = GroundGradientBrightnessValue;
            int horizonColorPalette = HorizonColorPaletteValue;
            int skyLineColorPalette = SkyLineColorPaletteValue;
            bool unifiedSideColors = UseUnifiedSideColorsValue;

            bool baseChanged =
                !Mathf.Approximately(groundDarkness, cachedGroundDarkness) ||
                !Mathf.Approximately(groundGradientStart, cachedGroundGradientStart) ||
                !Mathf.Approximately(groundGradientBrightness, cachedGroundGradientBrightness) ||
                horizonColorPalette != cachedFloorHorizonColorPalette ||
                skyLineColorPalette != cachedFloorSkyLineColorPalette ||
                unifiedSideColors != cachedFloorUnifiedSideColors;
            bool gridChanged = false;

            if (!baseChanged && !gridChanged)
                return;

            if (baseChanged)
            {
                using (RebuildFloorBaseTextureProfilerMarker.Auto())
                {
                    DestroyOwnedTexture(floorTexture);
                    floorTexture = BuildFloorBaseTexture(256, 256);
                    floorMaterial.mainTexture = floorTexture;
                    floorMaterial.SetTexture("_MainTex", floorTexture);
                }
            }

            if (gridChanged)
            {
                using (RebuildFloorGridTextureProfilerMarker.Auto())
                {
                    DestroyOwnedTexture(floorGridTexture);
                    floorGridTexture = BuildFloorGridTexture(512, 512);
                    floorGridMaterial.mainTexture = floorGridTexture;
                }
            }

            CacheGroundControls(
                groundDarkness,
                groundGradientStart,
                groundGradientBrightness,
                horizonColorPalette,
                skyLineColorPalette,
                unifiedSideColors);
        }
    }

    private void CacheGroundControls()
    {
        CacheGroundControls(
            Mathf.Max(0.05f, GroundDarknessValue),
            Mathf.Clamp(GroundGradientStartValue, 0.01f, 0.99f),
            Mathf.Max(0f, GroundGradientBrightnessValue),
            HorizonColorPaletteValue,
            SkyLineColorPaletteValue,
            UseUnifiedSideColorsValue);
    }

    private void CacheGroundControls(
        float groundDarkness,
        float groundGradientStart,
        float groundGradientBrightness,
        int horizonColorPalette,
        int skyLineColorPalette,
        bool unifiedSideColors)
    {
        cachedGroundDarkness = groundDarkness;
        cachedGroundGradientStart = groundGradientStart;
        cachedGroundGradientBrightness = groundGradientBrightness;
        cachedFloorHorizonColorPalette = horizonColorPalette;
        cachedFloorSkyLineColorPalette = skyLineColorPalette;
        cachedFloorUnifiedSideColors = unifiedSideColors;
    }

    private void UpdateHorizonPlacement(Camera camera)
    {
        if (horizonObject == null && horizonCoreObject == null)
            return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float horizonLineDistance = GetEffectiveHorizonLineDistance(HorizonLineDistanceValue);
        float horizonLineY = HorizonLineYValue;
        float horizonLineWidth = Mathf.Max(1f, HorizonLineWidthValue);
        float horizonGlowBlurHeight = Mathf.Max(0.001f, HorizonGlowBlurHeightValue);
        float horizonCoreLineYOffset = HorizonCoreLineYOffsetValue;
        float horizonCoreLineHeight = Mathf.Max(0.001f, HorizonCoreLineHeightValue);
        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;
        Vector3 center = camera.transform.position + (forwardFlat * horizonLineDistance);
        center.x = Mathf.Lerp(center.x, boardMidX, 0.46f);
        center.y = horizonLineY;

        Quaternion rotation = Quaternion.LookRotation(-forwardFlat, Vector3.up);
        float curveDown = HorizonCurveDownValue;
        float curveTowardCamera = HorizonCurveTowardCameraValue;
        Vector3 rootOffset = GetRootWorldOffset();

        if (horizonObject != null)
        {
            horizonObject.transform.position = center + rootOffset;
            horizonObject.transform.rotation = rotation;
            horizonObject.transform.localScale = Vector3.one;
            ApplyCurvedHorizonMesh(horizonMeshCache, horizonLineWidth, horizonGlowBlurHeight, curveDown, curveTowardCamera);
        }

        if (horizonCoreObject != null)
        {
            Vector3 coreCenter = center;
            coreCenter.y += horizonCoreLineYOffset;
            horizonCoreObject.transform.position = coreCenter + rootOffset;
            horizonCoreObject.transform.rotation = rotation;
            horizonCoreObject.transform.localScale = Vector3.one;
            ApplyCurvedHorizonMesh(horizonCoreMeshCache, horizonLineWidth, horizonCoreLineHeight, curveDown, curveTowardCamera);
        }

    }

    private void UpdateEnviroMountainPlacement(Camera camera)
    {
        if (enviroMountainObject == null || camera == null)
            return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float horizonLineDistance = GetEffectiveHorizonLineDistance(HorizonLineDistanceValue);
        float horizonLineY = HorizonLineYValue;
        float horizonLineWidth = HorizonLineWidthValue;
        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;
        Vector3 center = camera.transform.position + (forwardFlat * (horizonLineDistance + EnviroMountainTerrainDistanceOffset));
        center.x = Mathf.Lerp(center.x, boardMidX, 0.46f);
        center.y = horizonLineY + EnviroMountainSilhouetteYOffset;

        enviroMountainObject.transform.position = center + GetRootWorldOffset();
        enviroMountainObject.transform.rotation = Quaternion.LookRotation(forwardFlat, Vector3.up);
        enviroMountainObject.transform.localScale = new Vector3(
            Mathf.Max(1f, horizonLineWidth * EnviroMountainSilhouetteWidthMultiplier),
            1f,
            1f);
    }

    private void UpdateFarLightPlacement(Camera camera)
    {
        if (farLightObjects.Count == 0)
            return;

        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        Vector3 rightFlat = Vector3.Cross(Vector3.up, forwardFlat);
        if (rightFlat.sqrMagnitude < 0.0001f)
            rightFlat = Vector3.right;
        rightFlat.Normalize();

        float horizonLineDistance = Mathf.Max(1f, HorizonLineDistanceValue);
        float horizonLineY = HorizonLineYValue;
        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;
        Quaternion rotation = Quaternion.LookRotation(-forwardFlat, Vector3.up);

        for (int i = 0; i < farLightObjects.Count; i++)
        {
            Renderer farLightRenderer = i < farLightRenderers.Count ? farLightRenderers[i] : null;
            if (farLightRenderer != null)
                farLightRenderer.enabled = false;

            GameObject farLight = farLightObjects[i];
            if (farLight == null)
                continue;

            float distance = Mathf.Max(4f, horizonLineDistance - FarLightDistanceOffset - (i % 2) * 6f);
            Vector3 center = camera.transform.position + (forwardFlat * distance);
            center.x = Mathf.Lerp(center.x, boardMidX, 0.46f);
            center += rightFlat * FarLightOffsets[i % FarLightOffsets.Length];
            center.y = horizonLineY + FarLightBaseHeight + Mathf.Sin(i * 1.71f) * 0.35f;

            float widthScale = FarLightWidth * (i % 2 == 0 ? 1.0f : 0.82f);
            float heightScale = FarLightHeight * (i % 3 == 0 ? 1.08f : 0.92f);
            farLight.transform.position = center + GetRootWorldOffset();
            farLight.transform.rotation = rotation;
            farLight.transform.localScale = new Vector3(widthScale, heightScale, 1f);
        }
    }

    private void CreateStageClouds()
    {
        Texture2D fallbackTexture = null;
        for (int i = 0; i < StageCloudCount; i++)
        {
            Texture2D cloudTexture = LoadStageCloudTexture(i);
            if (cloudTexture == null)
            {
                if (fallbackTexture == null)
                {
                    fallbackTexture = BuildStageCloudTexture(256, 128);
                    stageCloudTexture = fallbackTexture;
                }

                cloudTexture = fallbackTexture;
            }

            Renderer cloudRenderer;
            GameObject cloudObject = CreateQuad($"NeonStageCloud{i + 1}", root.transform, out cloudRenderer);
            Material cloudMaterial = CreateTransparentTexturedMaterial(cloudTexture, renderQueue: StageRenderQueueBase + 1);
            cloudMaterial.SetInt("_ZTest", (int)CompareFunction.LessEqual);
            stageClouds.Add(new StageCloud
            {
                Object = cloudObject,
                Renderer = cloudRenderer,
                Material = cloudMaterial,
                BaseX = StageCloudSeedX[i % StageCloudSeedX.Length],
                BaseY = StageCloudSeedY[i % StageCloudSeedY.Length],
                Scale = StageCloudSeedScale[i % StageCloudSeedScale.Length],
                Speed = StageCloudSeedSpeed[i % StageCloudSeedSpeed.Length]
            });

            if (cloudRenderer != null)
            {
                cloudRenderer.sharedMaterial = cloudMaterial;
                cloudRenderer.enabled = false;
            }
        }
    }

    private void UpdateStageCloudPlacement(Camera camera)
    {
        if (stageClouds.Count == 0)
            return;

        bool visible = StageCloudsEnabledValue && StageCloudOpacityValue > 0.001f;
        Vector3 forwardFlat = Vector3.ProjectOnPlane(camera.transform.forward, Vector3.up);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        Vector3 rightFlat = Vector3.Cross(Vector3.up, forwardFlat);
        if (rightFlat.sqrMagnitude < 0.0001f)
            rightFlat = Vector3.right;
        rightFlat.Normalize();

        Quaternion rotation = Quaternion.LookRotation(-forwardFlat, Vector3.up);
        float t = Time.unscaledTime * StageCloudSpeedValue;
        float horizonLineDistance = Mathf.Max(1f, HorizonLineDistanceValue);
        float horizonLineY = HorizonLineYValue;
        float horizonLineWidth = Mathf.Max(1f, HorizonLineWidthValue);
        float boardMidX = owner != null ? Mathf.Max(0f, owner.TotalFrets * owner.FretSpacing * 0.5f) : 0f;

        for (int i = 0; i < stageClouds.Count; i++)
        {
            StageCloud cloud = stageClouds[i];
            if (cloud.Renderer != null)
                cloud.Renderer.enabled = visible;
            if (cloud.Object == null)
                continue;

            float xNorm = Mathf.Repeat(cloud.BaseX + 0.5f + t * cloud.Speed * 0.035f, 1f) - 0.5f;
            float distance = horizonLineDistance * Mathf.Lerp(0.72f, 0.98f, cloud.BaseY);
            Vector3 center = camera.transform.position + forwardFlat * distance;
            center.x = Mathf.Lerp(center.x, boardMidX, 0.30f);
            center += rightFlat * (xNorm * horizonLineWidth * 0.42f);
            center.y = horizonLineY + Mathf.Lerp(24.0f, 42.0f, cloud.BaseY);

            cloud.Object.transform.position = center + GetRootWorldOffset();
            cloud.Object.transform.rotation = rotation;
            cloud.Object.transform.localScale = new Vector3(
                Mathf.Lerp(26f, 54f, cloud.Scale),
                Mathf.Lerp(7f, 15f, cloud.Scale),
                1f);
        }
    }

    private static Texture2D LoadStageCloudTexture(int index)
    {
        int textureIndex = StageCloudTextureIndices[index % StageCloudTextureIndices.Length];
        return Resources.Load<Texture2D>($"Cloud Pack/Cloud {textureIndex}");
    }

    private void CreateDomeStars()
    {
        domeStarsObject = new GameObject("NeonStageDomeStars");
        domeStarsObject.transform.SetParent(root.transform, false);
        MeshFilter meshFilter = domeStarsObject.AddComponent<MeshFilter>();
        domeStarsObject.AddComponent<MeshRenderer>();
        domeStarsRenderer = ConfigureRenderer(domeStarsObject);
        domeStarsMesh = new Mesh { name = "NeonStageDomeStarsMesh" };
        domeStarsMesh.MarkDynamic();
        meshFilter.sharedMesh = domeStarsMesh;
        domeStarsMaterial = CreateDomeStarsMaterial((int)RenderQueue.Background + 9);

        if (domeStarsRenderer != null)
        {
            domeStarsRenderer.sharedMaterial = domeStarsMaterial;
            domeStarsRenderer.enabled = false;
        }
    }

    private void UpdateDomeStarsMeshIfNeeded()
    {
        if (domeStarsMesh == null)
            return;

        int count = DomeStarsCountValue;
        int seed = DomeStarsSeedValue;
        float sizeScale = DomeStarsSizeValue;
        if (count == cachedDomeStarsCount &&
            seed == cachedDomeStarsSeed &&
            Mathf.Approximately(sizeScale, cachedDomeStarsSize))
        {
            return;
        }

        cachedDomeStarsCount = count;
        cachedDomeStarsSeed = seed;
        cachedDomeStarsSize = sizeScale;
        domeStarsMesh.Clear(false);

        if (count <= 0)
            return;

        Vector3[] vertices = new Vector3[count * 4];
        Vector2[] uvs = new Vector2[count * 4];
        Vector2[] uv2 = new Vector2[count * 4];
        Color[] colors = new Color[count * 4];
        int[] triangles = new int[count * 6];
        var random = new System.Random(seed);

        for (int i = 0; i < count; i++)
        {
            int vertexIndex = i * 4;
            int triangleIndex = i * 6;
            float depthT = Mathf.Pow((float)random.NextDouble(), 0.72f);
            float z = Mathf.Lerp(DomeStarsNearZ, DomeStarsFarZ, depthT);
            float y = Mathf.Lerp(DomeStarsMinY, DomeStarsMaxY, Mathf.Pow((float)random.NextDouble(), 1.18f));
            float horizontalRange = Mathf.Lerp(DomeStarsHalfWidth * 0.52f, DomeStarsHalfWidth, depthT);
            float x = ((float)random.NextDouble() * 2f - 1f) * horizontalRange;
            float starSize = Mathf.Lerp(0.16f, 0.78f, Mathf.Pow((float)random.NextDouble(), 2f)) * sizeScale;
            Vector3 center = new Vector3(x, y, z);

            vertices[vertexIndex] = center + new Vector3(-starSize, -starSize, 0f);
            vertices[vertexIndex + 1] = center + new Vector3(-starSize, starSize, 0f);
            vertices[vertexIndex + 2] = center + new Vector3(starSize, starSize, 0f);
            vertices[vertexIndex + 3] = center + new Vector3(starSize, -starSize, 0f);

            uvs[vertexIndex] = new Vector2(0f, 0f);
            uvs[vertexIndex + 1] = new Vector2(0f, 1f);
            uvs[vertexIndex + 2] = new Vector2(1f, 1f);
            uvs[vertexIndex + 3] = new Vector2(1f, 0f);

            Vector2 twinkle = new Vector2((float)random.NextDouble() * Mathf.PI * 2f, (float)random.NextDouble());
            uv2[vertexIndex] = twinkle;
            uv2[vertexIndex + 1] = twinkle;
            uv2[vertexIndex + 2] = twinkle;
            uv2[vertexIndex + 3] = twinkle;

            Color color = Color.Lerp(new Color(0.58f, 0.78f, 1f, 1f), Color.white, (float)random.NextDouble() * 0.55f);
            color.a = Mathf.Lerp(0.32f, 1f, Mathf.Pow((float)random.NextDouble(), 0.7f));
            colors[vertexIndex] = color;
            colors[vertexIndex + 1] = color;
            colors[vertexIndex + 2] = color;
            colors[vertexIndex + 3] = color;

            triangles[triangleIndex] = vertexIndex;
            triangles[triangleIndex + 1] = vertexIndex + 1;
            triangles[triangleIndex + 2] = vertexIndex + 2;
            triangles[triangleIndex + 3] = vertexIndex;
            triangles[triangleIndex + 4] = vertexIndex + 2;
            triangles[triangleIndex + 5] = vertexIndex + 3;
        }

        domeStarsMesh.vertices = vertices;
        domeStarsMesh.uv = uvs;
        domeStarsMesh.uv2 = uv2;
        domeStarsMesh.colors = colors;
        domeStarsMesh.triangles = triangles;
        domeStarsMesh.RecalculateBounds();
    }

    private void ApplyDomeStarsMaterialState()
    {
        bool visible = proceduralDomeVisible && DomeStarsEnabledValue && DomeStarsCountValue > 0;
        SetRendererVisible(domeStarsRenderer, visible);

        if (!visible || domeStarsMaterial == null)
            return;

        if (domeStarsMaterial.HasProperty(StarsTintShaderId))
            domeStarsMaterial.SetColor(StarsTintShaderId, new Color(0.78f, 0.90f, 1f, 1f));
        if (domeStarsMaterial.HasProperty(StarsBrightnessShaderId))
            domeStarsMaterial.SetFloat(StarsBrightnessShaderId, DomeStarsBrightnessValue);
        if (domeStarsMaterial.HasProperty(StarsTwinkleStrengthShaderId))
            domeStarsMaterial.SetFloat(StarsTwinkleStrengthShaderId, DomeStarsTwinkleStrengthValue);
        if (domeStarsMaterial.HasProperty(StarsTwinkleSpeedShaderId))
            domeStarsMaterial.SetFloat(StarsTwinkleSpeedShaderId, DomeStarsTwinkleSpeedValue);
        if (domeStarsMaterial.HasProperty(StageTimeShaderId))
            domeStarsMaterial.SetFloat(StageTimeShaderId, Mathf.Repeat(Time.unscaledTime, 4096f));
    }

    private Material CreateDomeStarsMaterial(int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsDomeStars");
        if (shader == null)
            shader = Shader.Find("Custom/TabsDomeStars");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");

        Material material;
        if (shader != null)
        {
            material = new Material(shader);
        }
        else if (owner != null)
        {
            material = owner.CreateSharedTransparentMaterial(new Color(0.78f, 0.90f, 1f, 0.55f), 0.55f);
        }
        else
        {
            Shader fallbackShader = Shader.Find("Unlit/Transparent") ?? Shader.Find("Sprites/Default");
            material = new Material(fallbackShader);
            material.color = new Color(0.78f, 0.90f, 1f, 0.55f);
        }

        material.renderQueue = renderQueue;
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private Material CreateMaterial(int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsNeonStage");
        if (shader == null)
            shader = Shader.Find("Custom/TabsNeonStage");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.004f, 0.006f, 0.025f, 1f), 0.25f);

        material.renderQueue = renderQueue;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private Material CreateTransparentTexturedMaterial(Texture2D texture, int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTexturedTransparent");
        if (shader == null)
            shader = Shader.Find("Custom/TabsTexturedTransparent");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Texture");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.10f, 0.36f, 0.85f, 0.12f), 0.12f);

        material.mainTexture = texture;
        material.color = Color.white;
        material.renderQueue = renderQueue;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private void EnsureEnviroMountainLayerMaterials(int renderQueue)
    {
        int zTest = proceduralStageRenderedAfterSky ? (int)CompareFunction.LessEqual : (int)CompareFunction.Always;
        Shader mountainShader = ResolveEnviroMountainShader();
        bool recreateMaterials = enviroMountainLayerMaterials.Count != EnviroMountainSilhouetteLayerCount;
        if (!recreateMaterials)
        {
            for (int i = 0; i < enviroMountainLayerMaterials.Count; i++)
            {
                Material material = enviroMountainLayerMaterials[i];
                if (material == null || (mountainShader != null && material.shader != mountainShader))
                {
                    recreateMaterials = true;
                    break;
                }
            }
        }

        if (recreateMaterials)
        {
            for (int i = 0; i < enviroMountainLayerMaterials.Count; i++)
            {
                if (enviroMountainLayerMaterials[i] != null)
                    Object.Destroy(enviroMountainLayerMaterials[i]);
            }
            enviroMountainLayerMaterials.Clear();

            for (int i = 0; i < EnviroMountainSilhouetteLayerCount; i++)
            {
                Color layerColor = i < EnviroMountainLayerColors.Length
                    ? EnviroMountainLayerColors[i]
                    : EnviroMountainLayerColors[EnviroMountainLayerColors.Length - 1];
                enviroMountainLayerMaterials.Add(CreateEnviroMountainTerrainMaterial(renderQueue, layerColor, mountainShader, i));
            }
        }

        for (int i = 0; i < enviroMountainLayerMaterials.Count; i++)
        {
            Color layerColor = i < EnviroMountainLayerColors.Length
                ? EnviroMountainLayerColors[i]
                : EnviroMountainLayerColors[EnviroMountainLayerColors.Length - 1];
            ApplyEnviroMountainMaterialProperties(enviroMountainLayerMaterials[i], layerColor, renderQueue, zTest, i);
        }

        enviroMountainMaterial = enviroMountainLayerMaterials[enviroMountainLayerMaterials.Count - 1];
        ApplyEnviroMountainMaterialRenderState(renderQueue, zTest);
    }

    private Material CreateEnviroMountainTerrainMaterial(int renderQueue)
    {
        Color color = EnviroMountainLayerColors.Length > 0
            ? EnviroMountainLayerColors[EnviroMountainLayerColors.Length - 1]
            : new Color(0.003f, 0.004f, 0.008f, 0.6f);
        return CreateEnviroMountainTerrainMaterial(renderQueue, color, ResolveEnviroMountainShader(), EnviroMountainSilhouetteLayerCount - 1);
    }

    private Material CreateEnviroMountainTerrainMaterial(int renderQueue, Color color)
    {
        return CreateEnviroMountainTerrainMaterial(renderQueue, color, ResolveEnviroMountainShader(), EnviroMountainSilhouetteLayerCount - 1);
    }

    private Material CreateEnviroMountainTerrainMaterial(int renderQueue, Color color, Shader shader, int layerIndex)
    {
        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(color, 0f);

        ApplyEnviroMountainMaterialProperties(material, color, renderQueue, (int)CompareFunction.Always, layerIndex);
        return material;
    }

    private Shader ResolveEnviroMountainShader()
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsMountainSilhouette");
        if (shader == null)
            shader = Shader.Find("Custom/TabsMountainSilhouette");
        if (shader == null)
            shader = Shader.Find("Unlit/Transparent");
        if (shader == null)
            shader = Shader.Find("Universal Render Pipeline/Unlit");
        if (shader == null)
            shader = Shader.Find("Sprites/Default");
        if (shader == null)
            shader = Shader.Find("Unlit/Color");
        if (shader == null)
            shader = Shader.Find("Standard");

        return shader;
    }

    private void ApplyEnviroMountainMaterialProperties(Material material, Color color, int renderQueue, int zTest, int layerIndex)
    {
        if (material == null)
            return;

        Color appliedColor = color;
        appliedColor.a = GetEnviroMountainLayerOpacityValue(layerIndex);

        material.color = appliedColor;
        material.SetOverrideTag("RenderType", "Transparent");
        if (material.HasProperty("_Color"))
            material.SetColor("_Color", appliedColor);
        if (material.HasProperty("_BaseColor"))
            material.SetColor("_BaseColor", appliedColor);
        if (material.HasProperty("_EmissionColor"))
            material.SetColor("_EmissionColor", Color.black);
        if (material.HasProperty("_Smoothness"))
            material.SetFloat("_Smoothness", 0f);
        if (material.HasProperty("_Metallic"))
            material.SetFloat("_Metallic", 0f);

        material.renderQueue = renderQueue;
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
        if (material.HasProperty("_ZWrite"))
            material.SetInt("_ZWrite", 0);
        if (material.HasProperty("_Cull"))
            material.SetInt("_Cull", (int)CullMode.Off);
        if (material.HasProperty("_ZTest"))
            material.SetInt("_ZTest", zTest);
        material.EnableKeyword("_ALPHABLEND_ON");
        material.EnableKeyword("_SURFACE_TYPE_TRANSPARENT");
        material.DisableKeyword("_ALPHATEST_ON");
        material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
    }

    private Material CreateAdditiveTexturedMaterial(Texture2D texture, int renderQueue, bool alphaAsColor = false)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsTexturedAdditive");
        if (shader == null)
            shader = Shader.Find("Custom/TabsTexturedAdditive");
        if (shader == null)
            shader = Shader.Find("Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");

        Material material = shader != null
            ? new Material(shader)
            : CreateTransparentTexturedMaterial(texture, renderQueue);

        material.mainTexture = texture;
        material.color = Color.white;
        material.renderQueue = renderQueue;
        if (material.HasProperty("_AlphaAsColor"))
            material.SetFloat("_AlphaAsColor", alphaAsColor ? 1f : 0f);
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private Material CreateHorizonMaterial(int renderQueue)
    {
        Shader shader = Resources.Load<Shader>("Shaders/TabsHorizonGlow");
        if (shader == null)
            shader = Shader.Find("Custom/TabsHorizonGlow");
        if (shader == null)
            shader = Shader.Find("Particles/Additive");
        if (shader == null)
            shader = Shader.Find("Legacy Shaders/Particles/Additive");

        Material material = shader != null
            ? new Material(shader)
            : owner.CreateSharedTransparentMaterial(new Color(0.85f, 0.15f, 1f, 0.65f), 0.65f);

        material.renderQueue = renderQueue;
        material.SetInt("_ZWrite", 0);
        material.SetInt("_Cull", (int)CullMode.Off);
        material.SetInt("_ZTest", (int)CompareFunction.Always);

        return material;
    }

    private static Renderer ConfigureRenderer(GameObject gameObject)
    {
        Renderer renderer = gameObject != null ? gameObject.GetComponent<Renderer>() : null;
        if (renderer != null)
        {
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.motionVectorGenerationMode = MotionVectorGenerationMode.ForceNoMotion;
        }

        return renderer;
    }

    private static GameObject CreateQuad(string name, Transform parent, out Renderer renderer)
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.name = name;
        quad.transform.SetParent(parent, false);
        Object.Destroy(quad.GetComponent<Collider>());

        renderer = ConfigureRenderer(quad);
        return quad;
    }

    private static void EnsureCurvedMesh(GameObject gameObject, CurvedMeshCache cache, string meshName, int widthSegments, int heightSegments)
    {
        if (gameObject == null || cache == null)
            return;

        MeshFilter meshFilter = gameObject.GetComponent<MeshFilter>();
        if (meshFilter == null)
            return;

        widthSegments = Mathf.Max(1, widthSegments);
        heightSegments = Mathf.Max(1, heightSegments);
        if (cache.Mesh != null && cache.WidthSegments == widthSegments && cache.HeightSegments == heightSegments)
        {
            meshFilter.sharedMesh = cache.Mesh;
            return;
        }

        DestroyMeshCache(cache);
        cache.WidthSegments = widthSegments;
        cache.HeightSegments = heightSegments;
        cache.Vertices = new Vector3[(widthSegments + 1) * (heightSegments + 1)];
        cache.Uvs = new Vector2[cache.Vertices.Length];
        cache.Triangles = BuildGridTriangles(widthSegments, heightSegments);
        cache.Mesh = new Mesh { name = meshName };
        cache.Mesh.MarkDynamic();
        InitializeGridUvs(cache);
        ApplyMeshData(cache);
        meshFilter.sharedMesh = cache.Mesh;
    }

    private static void InitializeGridUvs(CurvedMeshCache cache)
    {
        if (cache == null || cache.Uvs == null)
            return;

        int widthSegments = Mathf.Max(1, cache.WidthSegments);
        int heightSegments = Mathf.Max(1, cache.HeightSegments);
        for (int y = 0; y <= heightSegments; y++)
        {
            float v = y / (float)heightSegments;
            for (int x = 0; x <= widthSegments; x++)
            {
                float u = x / (float)widthSegments;
                cache.Uvs[y * (widthSegments + 1) + x] = new Vector2(u, v);
            }
        }
    }

    private static int[] BuildGridTriangles(int widthSegments, int heightSegments)
    {
        int[] triangles = new int[widthSegments * heightSegments * 6];
        int index = 0;
        for (int y = 0; y < heightSegments; y++)
        {
            int row = y * (widthSegments + 1);
            int nextRow = (y + 1) * (widthSegments + 1);
            for (int x = 0; x < widthSegments; x++)
            {
                int bottomLeft = row + x;
                int bottomRight = bottomLeft + 1;
                int topLeft = nextRow + x;
                int topRight = topLeft + 1;

                triangles[index++] = bottomLeft;
                triangles[index++] = topLeft;
                triangles[index++] = topRight;
                triangles[index++] = bottomLeft;
                triangles[index++] = topRight;
                triangles[index++] = bottomRight;
            }
        }

        return triangles;
    }

    private static float SideCurveAmount(float centered)
    {
        float magnitude = Mathf.Abs(centered);
        return magnitude * magnitude;
    }

    private static Vector3 GetCurvedFloorWorldOffset(
        float u,
        float v,
        Vector3 forwardFlat,
        float curveDown,
        float curveTowardCamera)
    {
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();

        float centered = (u - 0.5f) * 2f;
        float sideCurve = SideCurveAmount(centered) * Mathf.Clamp01(v);
        return (Vector3.down * (curveDown * sideCurve)) - (forwardFlat * (curveTowardCamera * sideCurve));
    }

    private static void ApplyCurvedHorizonMesh(
        CurvedMeshCache cache,
        float width,
        float height,
        float curveDown,
        float curveTowardCamera)
    {
        if (cache == null || cache.Mesh == null || cache.Vertices == null || cache.Uvs == null || cache.Triangles == null)
            return;

        width = Mathf.Max(0.001f, width);
        height = Mathf.Max(0.001f, height);
        int widthSegments = Mathf.Max(1, cache.WidthSegments);
        int heightSegments = Mathf.Max(1, cache.HeightSegments);
        for (int y = 0; y <= heightSegments; y++)
        {
            float v = y / (float)heightSegments;
            for (int x = 0; x <= widthSegments; x++)
            {
                float u = x / (float)widthSegments;
                float centered = (u - 0.5f) * 2f;
                float sideCurve = SideCurveAmount(centered);
                int vertexIndex = y * (widthSegments + 1) + x;
                cache.Vertices[vertexIndex] = new Vector3(
                    (u - 0.5f) * width,
                    (v - 0.5f) * height - curveDown * sideCurve,
                    curveTowardCamera * sideCurve);
                cache.Uvs[vertexIndex] = new Vector2(u, v);
            }
        }

        ApplyMeshData(cache);
    }

    private static void ApplyCurvedFloorMesh(
        CurvedMeshCache cache,
        float width,
        float depth,
        Quaternion worldRotation,
        Vector3 forwardFlat,
        float curveDown,
        float curveTowardCamera)
    {
        if (cache == null || cache.Mesh == null || cache.Vertices == null || cache.Uvs == null || cache.Triangles == null)
            return;

        width = Mathf.Max(0.001f, width);
        depth = Mathf.Max(0.001f, depth);
        if (forwardFlat.sqrMagnitude < 0.0001f)
            forwardFlat = Vector3.forward;
        forwardFlat.Normalize();
        if (!ShouldRebuildCurvedFloorMesh(cache, width, depth, worldRotation, forwardFlat, curveDown, curveTowardCamera))
            return;

        Quaternion inverseRotation = Quaternion.Inverse(worldRotation);
        int widthSegments = Mathf.Max(1, cache.WidthSegments);
        int heightSegments = Mathf.Max(1, cache.HeightSegments);
        for (int y = 0; y <= heightSegments; y++)
        {
            float v = y / (float)heightSegments;
            for (int x = 0; x <= widthSegments; x++)
            {
                float u = x / (float)widthSegments;
                Vector3 worldOffset = GetCurvedFloorWorldOffset(u, v, forwardFlat, curveDown, curveTowardCamera);
                Vector3 localOffset = inverseRotation * worldOffset;
                int vertexIndex = y * (widthSegments + 1) + x;
                cache.Vertices[vertexIndex] = new Vector3(
                    (u - 0.5f) * width,
                    (v - 0.5f) * depth,
                    0f) + localOffset;
            }
        }

        ApplyMeshVertices(cache);
        cache.HasAppliedFloorShape = true;
        cache.FloorWidth = width;
        cache.FloorDepth = depth;
        cache.FloorCurveDown = curveDown;
        cache.FloorCurveTowardCamera = curveTowardCamera;
        cache.FloorForwardFlat = forwardFlat;
        cache.FloorWorldRotation = worldRotation;
    }

    private static bool ShouldRebuildCurvedFloorMesh(
        CurvedMeshCache cache,
        float width,
        float depth,
        Quaternion worldRotation,
        Vector3 forwardFlat,
        float curveDown,
        float curveTowardCamera)
    {
        if (cache == null || !cache.HasAppliedFloorShape)
            return true;

        if (!Mathf.Approximately(cache.FloorWidth, width) ||
            !Mathf.Approximately(cache.FloorDepth, depth) ||
            !Mathf.Approximately(cache.FloorCurveDown, curveDown) ||
            !Mathf.Approximately(cache.FloorCurveTowardCamera, curveTowardCamera))
        {
            return true;
        }

        bool curved = Mathf.Abs(curveDown) > 0.0001f || Mathf.Abs(curveTowardCamera) > 0.0001f;
        if (!curved)
            return false;

        return Vector3.SqrMagnitude(cache.FloorForwardFlat - forwardFlat) > 0.000001f ||
               Mathf.Abs(Quaternion.Dot(cache.FloorWorldRotation, worldRotation)) < 0.999999f;
    }

    private static void ApplyMeshData(CurvedMeshCache cache)
    {
        cache.Mesh.Clear(false);
        cache.Mesh.vertices = cache.Vertices;
        cache.Mesh.uv = cache.Uvs;
        cache.Mesh.triangles = cache.Triangles;
        cache.Mesh.RecalculateBounds();
    }

    private static void ApplyMeshVertices(CurvedMeshCache cache)
    {
        if (cache == null || cache.Mesh == null || cache.Vertices == null)
            return;

        cache.Mesh.vertices = cache.Vertices;
        cache.Mesh.RecalculateBounds();
    }

    private static void DestroyMeshCache(CurvedMeshCache cache)
    {
        if (cache == null)
            return;

        if (cache.Mesh != null)
            Object.Destroy(cache.Mesh);

        cache.Mesh = null;
        cache.Vertices = null;
        cache.Uvs = null;
        cache.Triangles = null;
        cache.WidthSegments = -1;
        cache.HeightSegments = -1;
        cache.HasAppliedFloorShape = false;
        cache.FloorWidth = float.NaN;
        cache.FloorDepth = float.NaN;
        cache.FloorCurveDown = float.NaN;
        cache.FloorCurveTowardCamera = float.NaN;
        cache.FloorForwardFlat = Vector3.zero;
        cache.FloorWorldRotation = Quaternion.identity;
    }

    private Texture2D BuildFloorBaseTexture(int width, int height)
    {
        float groundDarkness = Mathf.Max(0.05f, GroundDarknessValue);
        float groundBrightness = Mathf.Clamp(Mathf.Pow(1f / groundDarkness, 1.72f), 0.08f, 120f);
        float lowDarknessLift = Mathf.Pow(Mathf.Clamp01(1f - groundDarkness), 1.35f) * 0.035f;
        float gradientStart = Mathf.Clamp(GroundGradientStartValue, 0.01f, 0.99f);
        float gradientBrightness = Mathf.Max(0f, GroundGradientBrightnessValue);
        float reflectivity = 0f;
        float gradientVisible = gradientBrightness <= 0.001f ? 0f : 1f;
        float gradientEnd = Mathf.Clamp(gradientStart + 0.24f, gradientStart + 0.025f, 0.995f);
        float midLiftEnd = gradientEnd;
        float horizonLiftStart = Mathf.Clamp(gradientStart + 0.08f, gradientStart + 0.010f, 0.995f);
        float gradientExposure = gradientBrightness <= 1f
            ? Mathf.Lerp(0.25f, 1f, gradientBrightness)
            : 1f + ((gradientBrightness - 1f) * 0.95f);
        Color gradientMidColor = FloorMidColor * gradientExposure;
        Color gradientFarColor = FloorFarColor * gradientExposure;

        Texture2D texture = CreateTexture(width, height);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            float nearFade = Mathf.SmoothStep(0.0f, 0.025f, v);
            float surfaceFade = Mathf.Clamp01(nearFade);
            float horizonReflection = Mathf.Pow(Mathf.SmoothStep(0.68f, 0.98f, v), 1.55f);
            float nearWeight = Mathf.Pow(Mathf.Clamp01(1f - v), 1.18f);
            float depthWeight = Mathf.Pow(Mathf.Clamp01(v), 1.16f);
            float midLift = Mathf.Pow(Mathf.SmoothStep(gradientStart, midLiftEnd, v), 1.80f) * 0.32f * gradientVisible;
            float horizonLift = Mathf.Pow(Mathf.SmoothStep(horizonLiftStart, 0.995f, v), FloorHorizonLiftPower) * gradientVisible;
            float postDarkGradient = Mathf.Pow(Mathf.SmoothStep(gradientStart, gradientEnd, v), 1.42f) * gradientVisible;

            for (int x = 0; x < width; x++)
            {
                float u = x / Mathf.Max(1f, width - 1f);
                float centered = Mathf.Abs(u - 0.5f) * 2f;
                float centerSheen = Mathf.Pow(1f - Mathf.SmoothStep(0.0f, 0.56f, centered), 1.65f);
                float edgeTint = Mathf.Pow(Mathf.Clamp01(centered), 1.18f);
                float sideBalance = Mathf.SmoothStep(0.0f, 1.0f, u);
                Color color = Color.Lerp(FloorNearColor, gradientMidColor, midLift);
                color = Color.Lerp(color, gradientFarColor, horizonLift);
                color = Color.Lerp(
                    color,
                    Color.Lerp(FloorLeftTintColor, FloorRightTintColor, sideBalance),
                    edgeTint * (0.45f + nearWeight * 0.55f) * FloorSideTintStrength);

                color += FloorCenterSheenColor * centerSheen * depthWeight * FloorCenterSheenStrength * reflectivity;
                color += FloorHorizonSheenColor * horizonReflection * (FloorHorizonSheenStrength + edgeTint * 0.12f) * reflectivity;
                float vignette = 1f - Mathf.SmoothStep(0.72f, 1.0f, centered) * 0.30f;
                color *= vignette * groundBrightness;
                color += new Color(0.010f, 0.014f, 0.040f, 0f) * lowDarknessLift * surfaceFade;
                color += FloorPostDarkGradientColor * postDarkGradient * gradientBrightness * FloorPostDarkGradientStrength * surfaceFade;
                float distanceAlpha = Mathf.Lerp(1f, surfaceFade, FloorDistanceFadeStrength);
                color.a = Mathf.Clamp01(FloorBaseOpacity * distanceAlpha);
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private Texture2D BuildStageCloudTexture(int width, int height)
    {
        Texture2D texture = CreateTexture(width, height);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            float verticalEdgeFade = Mathf.SmoothStep(0.02f, 0.30f, v) * (1f - Mathf.SmoothStep(0.78f, 1.0f, v));

            for (int x = 0; x < width; x++)
            {
                float u = x / Mathf.Max(1f, width - 1f);
                float horizontalEdgeFade = Mathf.SmoothStep(0.0f, 0.12f, u) * (1f - Mathf.SmoothStep(0.88f, 1.0f, u));

                float body = 0f;
                body += CloudBlob(u, v, 0.20f, 0.52f, 0.28f, 0.23f, 0.55f);
                body += CloudBlob(u, v, 0.36f, 0.60f, 0.30f, 0.20f, 0.78f);
                body += CloudBlob(u, v, 0.54f, 0.56f, 0.36f, 0.22f, 0.82f);
                body += CloudBlob(u, v, 0.72f, 0.51f, 0.31f, 0.20f, 0.58f);
                body += CloudBlob(u, v, 0.46f, 0.38f, 0.42f, 0.18f, 0.32f);

                float wisp = 0.5f + 0.5f * Mathf.Sin(u * 21.0f + v * 7.0f);
                wisp *= 0.5f + 0.5f * Mathf.Sin(u * 43.0f - v * 18.0f + 1.6f);
                float alpha = Mathf.Clamp01(body * (0.82f + wisp * 0.18f));
                alpha *= horizontalEdgeFade * verticalEdgeFade;
                alpha = Mathf.Pow(alpha, 1.34f) * StageCloudTextureAlpha;

                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static float CloudBlob(float u, float v, float centerU, float centerV, float width, float height, float strength)
    {
        float normalizedU = (u - centerU) / Mathf.Max(0.0001f, width);
        float normalizedV = (v - centerV) / Mathf.Max(0.0001f, height);
        return Mathf.Exp(-(normalizedU * normalizedU + normalizedV * normalizedV)) * strength;
    }

    private Texture2D BuildFarLightTexture(int width, int height)
    {
        Texture2D texture = CreateTexture(width, height);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            float yDistance = Mathf.Abs(v - 0.5f) * 2f;
            float verticalGlow = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, yDistance), 1.80f);

            for (int x = 0; x < width; x++)
            {
                float u = x / Mathf.Max(1f, width - 1f);
                float xDistance = Mathf.Abs(u - 0.5f) * 2f;
                float horizontalGlow = Mathf.Pow(1f - Mathf.SmoothStep(0f, 1f, xDistance), 1.45f);
                float core = Mathf.Pow(1f - Mathf.SmoothStep(0f, 0.30f, Mathf.Max(xDistance * 0.64f, yDistance)), 1.25f);
                float alpha = Mathf.Clamp01(horizontalGlow * verticalGlow * 0.78f + core * 0.34f);
                pixels[y * width + x] = new Color(1f, 1f, 1f, alpha);
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private Texture2D BuildFloorGridTexture(int width, int height)
    {
        float reflectedLineStrength = FloorGridReflectionStrength;

        Texture2D texture = CreateTexture(width, height);
        Color[] pixels = new Color[width * height];
        for (int y = 0; y < height; y++)
        {
            float v = y / Mathf.Max(1f, height - 1f);
            float depth = Mathf.Pow(v, 1.35f);
            float fadeNear = Mathf.SmoothStep(0.025f, 0.095f, v);
            float fadeFar = 1f;
            float verticalFade = fadeNear * fadeFar;
            float horizonReflection = Mathf.SmoothStep(0.44f, 0.94f, v);
            float nearReflection = Mathf.Pow(Mathf.Clamp01(1f - v), 1.55f);

            for (int x = 0; x < width; x++)
            {
                float u = x / Mathf.Max(1f, width - 1f);
                float centered = Mathf.Abs(u - 0.5f) * 2f;
                float edgeFade = 1f - Mathf.SmoothStep(0.94f, 1.0f, centered);
                float centerGlow = 1f - Mathf.SmoothStep(0.0f, 0.66f, centered);

                float fineVertical = GridLine(u * 36f, 0.0060f) * (0.020f + depth * 0.090f);
                float fineHorizontal = GridLine(v * 26f, 0.0048f) * (0.010f + depth * 0.040f);

                float laneGlow = 0f;
                for (int lane = 0; lane <= 6; lane++)
                {
                    float laneU = 0.22f + lane * 0.104f;
                    float widthByDepth = Mathf.Lerp(0.0028f, 0.0100f, depth);
                    laneGlow = Mathf.Max(laneGlow, 1f - Mathf.SmoothStep(widthByDepth, widthByDepth * 3.8f, Mathf.Abs(u - laneU)));
                }

                float mirroredRibbon = Mathf.Exp(-Mathf.Pow((centered - 0.28f) * 3.8f, 2f)) * horizonReflection * 0.035f;
                float centerReflection = centerGlow * nearReflection * 0.055f;
                float glow = (laneGlow * (0.12f + depth * 0.36f) + fineVertical + fineHorizontal) * verticalFade * edgeFade;
                glow += centerReflection * reflectedLineStrength;
                glow += mirroredRibbon * reflectedLineStrength;
                glow += horizonReflection * centerGlow * 0.060f * reflectedLineStrength;
                glow *= FloorGridLineStrength;

                Color color = Color.Lerp(FloorGridLeftColor, FloorGridRightColor, Mathf.SmoothStep(0.10f, 0.90f, u));
                color = Color.Lerp(color, FloorGridCenterColor, centerGlow * 0.18f);
                color.a = Mathf.Clamp01(glow * 1.55f);
                pixels[y * width + x] = color;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply();
        return texture;
    }

    private static float GridLine(float value, float width)
    {
        float distance = Mathf.Abs(Mathf.Repeat(value, 1f) - 0.5f);
        return 1f - Mathf.SmoothStep(width, width * 2.4f, distance);
    }

    private static float Gaussian(float value, float center, float width)
    {
        float safeWidth = Mathf.Max(0.0001f, width);
        float normalized = (value - center) / safeWidth;
        return Mathf.Exp(-(normalized * normalized));
    }

    private Texture2D CreateTexture(int width, int height)
    {
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false);
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.filterMode = FilterMode.Bilinear;
        ownedTextures.Add(texture);
        return texture;
    }

    private void DestroyOwnedTexture(Texture2D texture)
    {
        if (texture == null)
            return;

        ownedTextures.Remove(texture);
        Object.Destroy(texture);
    }

    private sealed class TabsNeonStageBackgroundCleanupHook : MonoBehaviour
    {
        private TabsNeonStageBackground background;

        public void Initialize(TabsNeonStageBackground owner)
        {
            background = owner;
        }

        private void OnDisable()
        {
            background?.RestoreRuntimeRenderStateFromHook();
        }

        private void OnDestroy()
        {
            background?.RestoreRuntimeRenderStateFromHook();
            background = null;
        }
    }
}
