using System.Collections.Generic;
using UnityEngine;

public sealed class GuitarHighway3DAlphaTabRenderer : IGuitarGameplayRenderer
{
    public const float BottomTabViewportHeight = 0.27f;
    public const float TwoTabsBottomTabViewportHeight = 0.40f;
    private const int BaseBarsPerSection = 1;
    private const float BaseRenderScale = 1.15f;
    private const float OneTabCharacterViewportScale = 1.18f;
    private const float TwoTabsCharacterViewportScale = 1.24f;
    private const float OneTabCharacterViewportOffsetY = -0.055f;
    private const float TwoTabsCharacterViewportOffsetY = -0.075f;

    private readonly GuitarHighway3DRenderer highwayRenderer = new GuitarHighway3DRenderer();
    private readonly AlphaTabSheetRuntimeConfig sheetConfig = new AlphaTabSheetRuntimeConfig
    {
        canvasName = "AlphaTabHybridCanvas",
        sortingOrder = 6,
        anchorMin = new Vector2(0f, 0f),
        anchorMax = new Vector2(1f, BottomTabViewportHeight),
        showStatusBanner = false,
        topInsetPixels = 2f,
        bottomInsetPixels = 2f,
        sideInsetPixels = 0f,
        visibleSectionCount = 1,
        sectionGapPixels = 0f,
        sectionInnerPaddingHorizontalPixels = 6f,
        sectionInnerPaddingVerticalPixels = 0f,
        barsPerRow = 1,
        barsPerSection = BaseBarsPerSection,
        renderWidth = 1800,
        renderScale = BaseRenderScale,
        regionBackdropColor = new Color(0.05f, 0.06f, 0.08f, 1f),
        sectionBackgroundColor = new Color(1f, 1f, 1f, 0.995f),
        sectionBorderColor = new Color(0.06f, 0.06f, 0.06f, 0.95f),
        statusTextColor = new Color(0.06f, 0.06f, 0.06f, 0.92f),
        useUnifiedRegionFrame = true,
        useCircularVisibleSectionQueue = true,
        layoutVisibleSectionsHorizontally = true
    };
    private readonly AlphaTabSheetRuntime sheetRuntime;
    private GuitarBridgeServer owner;
    private Camera mainCamera;
    private Rect originalCameraRect = new Rect(0f, 0f, 1f, 1f);
    private bool hasOriginalCameraRect;

    public static float GetBottomTabViewportHeight(int visibleTabs)
    {
        return Mathf.Clamp(visibleTabs, 1, 2) >= 2
            ? TwoTabsBottomTabViewportHeight
            : BottomTabViewportHeight;
    }

    public GuitarHighway3DAlphaTabRenderer()
    {
        sheetRuntime = new AlphaTabSheetRuntime(sheetConfig);
    }

    public void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        this.owner = owner;
        highwayRenderer.Initialize(owner, chartNotes, sections);
        CacheMainCamera();
        ApplyOwnerConfig();
        sheetRuntime.Initialize(owner);
        ApplyTheme();
    }

    public void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections)
    {
        highwayRenderer.ResetRenderer(chartNotes, sections);
        ApplyOwnerConfig();
        ApplyTheme();
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        bool useSplitViewport = snapshot != null && !snapshot.mainMenuFlowActive;
        ApplyViewport(useSplitViewport);
        ApplyHighwayCharacterViewportCompensation(useSplitViewport);
        ApplyOwnerConfig();
        ApplyTheme();
        highwayRenderer.Render(snapshot);
        sheetRuntime.SetVisible(useSplitViewport);
        sheetRuntime.Render(snapshot);
    }

    public void DisposeRenderer()
    {
        RestoreViewport();
        sheetRuntime.Dispose();
        highwayRenderer.DisposeRenderer();
        owner = null;
    }

    private void CacheMainCamera()
    {
        if (mainCamera == null)
            mainCamera = Camera.main;

        if (mainCamera != null && !hasOriginalCameraRect)
        {
            originalCameraRect = mainCamera.rect;
            hasOriginalCameraRect = true;
        }
    }

    private void ApplyViewport(bool split)
    {
        CacheMainCamera();
        if (mainCamera == null)
            return;

        float bottomTabViewportHeight = owner != null
            ? GetBottomTabViewportHeight(owner.alphaTabHybridVisibleTabs)
            : BottomTabViewportHeight;

        Rect target = split
            ? new Rect(0f, bottomTabViewportHeight, 1f, 1f - bottomTabViewportHeight)
            : (hasOriginalCameraRect ? originalCameraRect : new Rect(0f, 0f, 1f, 1f));

        if (mainCamera.rect != target)
            mainCamera.rect = target;
    }

    private void RestoreViewport()
    {
        if (mainCamera != null && hasOriginalCameraRect)
            mainCamera.rect = originalCameraRect;
    }

    private void ApplyTheme()
    {
        if (owner == null)
            return;

        sheetRuntime.ApplyPalette(AlphaTabVisualThemePalette.GetSheetPalette(owner.alphaTabVisualTheme, splitViewport: true));
    }

    private void ApplyHighwayCharacterViewportCompensation(bool split)
    {
        float scale = 1f;
        float offsetY = 0f;
        if (split)
        {
            int visibleTabs = owner != null ? Mathf.Clamp(owner.alphaTabHybridVisibleTabs, 1, 2) : 1;
            scale = visibleTabs >= 2 ? TwoTabsCharacterViewportScale : OneTabCharacterViewportScale;
            offsetY = visibleTabs >= 2 ? TwoTabsCharacterViewportOffsetY : OneTabCharacterViewportOffsetY;
        }

        highwayRenderer.SetHighwayCharacterViewportHeightScale(scale);
        highwayRenderer.SetHighwayCharacterViewportCenterYOffset(offsetY);
    }

    private void ApplyOwnerConfig()
    {
        if (owner == null)
            return;

        int visibleTabs = Mathf.Clamp(owner.alphaTabHybridVisibleTabs, 1, 2);
        sheetConfig.visibleSectionCount = visibleTabs;
        sheetConfig.layoutVisibleSectionsHorizontally = visibleTabs < 2;
        sheetConfig.anchorMax = new Vector2(1f, GetBottomTabViewportHeight(visibleTabs));
        sheetConfig.renderScale = BaseRenderScale;
        sheetConfig.barsPerSection = ResolveBarsPerSection(owner.alphaTabSpanMultiplier);
    }

    private static int ResolveBarsPerSection(float spanMultiplier)
    {
        if (spanMultiplier >= 2.10f)
            return 4;
        if (spanMultiplier >= 1.60f)
            return 3;
        if (spanMultiplier >= 1.15f)
            return 2;
        return BaseBarsPerSection;
    }
}
