using System.Collections.Generic;
using UnityEngine;

public sealed class AlphaTabTabsRenderer : IGuitarGameplayRenderer
{
    public static readonly Vector2 SheetAnchorMin = new Vector2(0.05f, 0.07f);
    public static readonly Vector2 SheetAnchorMax = new Vector2(0.95f, 0.82f);
    public const int SheetVisibleSectionCount = 4;
    public const float SheetTopInsetPixels = 42f;
    public const float SheetBottomInsetPixels = 18f;
    public const float SheetSideInsetPixels = 8f;
    public const float SheetSectionGapPixels = 4f;
    private const int BaseBarsPerRow = 1;
    private const float BaseRenderScale = 1.3f;

    private readonly AlphaTabSheetRuntimeConfig sheetConfig = new AlphaTabSheetRuntimeConfig
    {
        canvasName = "AlphaTabTabsCanvas",
        sortingOrder = 8,
        anchorMin = SheetAnchorMin,
        anchorMax = SheetAnchorMax,
        showStatusBanner = false,
        visibleSectionCount = SheetVisibleSectionCount,
        topInsetPixels = SheetTopInsetPixels,
        bottomInsetPixels = SheetBottomInsetPixels,
        sideInsetPixels = SheetSideInsetPixels,
        sectionGapPixels = SheetSectionGapPixels,
        sectionInnerPaddingHorizontalPixels = 6f,
        sectionInnerPaddingVerticalPixels = 0f,
        barsPerRow = BaseBarsPerRow,
        barsPerSection = BaseBarsPerRow,
        renderWidth = 2200,
        renderScale = BaseRenderScale,
        regionBackdropColor = new Color(0f, 0f, 0f, 0f),
        sectionBackgroundColor = new Color(1f, 1f, 1f, 0.995f),
        sectionBorderColor = new Color(0.06f, 0.06f, 0.06f, 0.95f),
        statusTextColor = new Color(0.06f, 0.06f, 0.06f, 0.92f),
        useUnifiedRegionFrame = true,
        useCircularVisibleSectionQueue = true
    };
    private readonly AlphaTabSheetRuntime sheetRuntime;

    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private TabsSongHeaderOverlay songHeaderOverlay;
    private ITabsBackgroundEffect backgroundEffect;
    private GameObject backgroundRoot;
    private string backgroundSignature = string.Empty;

    public AlphaTabTabsRenderer()
    {
        sheetRuntime = new AlphaTabSheetRuntime(sheetConfig);
    }

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        mainCamera = Camera.main;
        backgroundRoot = new GameObject("AlphaTabBackgroundRoot");
        InitializeBackgroundEffect();
        ConfigureCamera();
        ApplyOwnerConfig();
        sheetRuntime.Initialize(owner);
        ApplyTheme();
        songHeaderOverlay = new TabsSongHeaderOverlay(owner);
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        InitializeBackgroundEffect();
        ConfigureCamera();
        ApplyOwnerConfig();
        ApplyTheme();
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        EnsureBackgroundEffectCurrent();
        ConfigureCamera();
        ApplyOwnerConfig();
        ApplyTheme();
        sheetRuntime.SetVisible(snapshot != null && !snapshot.mainMenuFlowActive && !snapshot.showToneLab);
        sheetRuntime.Render(snapshot);
        backgroundEffect?.Tick(Time.deltaTime);
        songHeaderOverlay?.UpdateFromSnapshot(snapshot);
    }

    public void DisposeRenderer()
    {
        sheetRuntime.Dispose();
        backgroundEffect?.Dispose();
        backgroundEffect = null;
        if (backgroundRoot != null)
            UnityEngine.Object.Destroy(backgroundRoot);
        backgroundRoot = null;
        songHeaderOverlay?.Dispose();
        songHeaderOverlay = null;
        owner = null;
        mainCamera = null;
    }

    private void ConfigureCamera()
    {
        if (mainCamera == null)
            return;

        mainCamera.orthographic = true;
        mainCamera.orthographicSize = owner != null ? owner.tabCameraSize : 8.5f;
        mainCamera.transform.position = new Vector3(0f, 0f, owner != null ? owner.tabCameraZ : -10f);
        mainCamera.transform.rotation = Quaternion.identity;
        mainCamera.backgroundColor = owner != null ? owner.tabBackgroundColor : Color.black;
    }

    private void InitializeBackgroundEffect()
    {
        backgroundEffect?.Dispose();
        backgroundEffect = TabsBackgroundFactory.Create(owner, applyHighwayOverrides: false);
        backgroundSignature = GetBackgroundSignature();

        if (backgroundRoot == null || backgroundEffect == null)
            return;

        backgroundEffect.Initialize(backgroundRoot.transform, owner);
    }

    private void EnsureBackgroundEffectCurrent()
    {
        if (GetBackgroundSignature() != backgroundSignature)
            InitializeBackgroundEffect();
    }

    private string GetBackgroundSignature()
    {
        if (owner == null)
            return string.Empty;

        return $"{owner.tabBackgroundMode}|{owner.tabSkyUseStageBackdrop}";
    }

    private void ApplyTheme()
    {
        if (owner == null)
            return;

        sheetRuntime.ApplyPalette(AlphaTabVisualThemePalette.GetSheetPalette(owner.alphaTabVisualTheme, splitViewport: false));
    }

    private void ApplyOwnerConfig()
    {
        if (owner == null)
            return;

        sheetConfig.renderScale = BaseRenderScale;
        sheetConfig.barsPerRow = ResolveBarsPerRow(owner.alphaTabSpanMultiplier);
        sheetConfig.barsPerSection = sheetConfig.barsPerRow;
    }

    private static int ResolveBarsPerRow(float spanMultiplier)
    {
        if (spanMultiplier >= 2.25f)
            return 4;
        if (spanMultiplier >= 1.90f)
            return 3;
        if (spanMultiplier >= 1.45f)
            return 2;
        return BaseBarsPerRow;
    }

    public static Rect GetUpperSectionHudScreenRect(float panelWidth, float panelHeight)
    {
        float regionLeft = panelWidth * SheetAnchorMin.x;
        float regionTop = panelHeight * (1f - SheetAnchorMax.y);
        float regionWidth = panelWidth * (SheetAnchorMax.x - SheetAnchorMin.x);
        float regionHeight = panelHeight * (SheetAnchorMax.y - SheetAnchorMin.y);
        float totalGap = SheetSectionGapPixels * Mathf.Max(0, SheetVisibleSectionCount - 1);
        float availableHeight = Mathf.Max(32f, regionHeight - totalGap - SheetTopInsetPixels - SheetBottomInsetPixels);
        float slotHeight = availableHeight / Mathf.Max(1, SheetVisibleSectionCount);
        float width = Mathf.Max(240f, regionWidth - (SheetSideInsetPixels * 2f));

        return new Rect(
            regionLeft + SheetSideInsetPixels,
            regionTop + SheetTopInsetPixels,
            width,
            slotHeight);
    }

    public static Rect GetSheetHudScreenRect(float panelWidth, float panelHeight)
    {
        return new Rect(
            panelWidth * SheetAnchorMin.x,
            panelHeight * (1f - SheetAnchorMax.y),
            panelWidth * (SheetAnchorMax.x - SheetAnchorMin.x),
            panelHeight * (SheetAnchorMax.y - SheetAnchorMin.y));
    }
}
