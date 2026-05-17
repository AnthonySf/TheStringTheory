using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;

public sealed class AlphaTabSheetRuntime
{
    private sealed class FeedbackOverlayData
    {
        public float startX01;
        public float endX01;
        public float y01;
        public float height01;
        public Color color;
    }

    private sealed class NoteOnsetGroup
    {
        public float time;
        public readonly List<GameplayNoteState> states = new List<GameplayNoteState>();
    }

    private sealed class SectionView
    {
        public readonly RectTransform root;
        public readonly Image background;
        public readonly Outline outline;
        public readonly RawImage image;
        public readonly RectTransform imageRect;
        public readonly Image indicator;
        public readonly Image loopStartIndicator;
        public readonly Image loopEndIndicator;
        public readonly Text statusLabel;
        private readonly List<Image> feedbackOverlays = new List<Image>();

        public SectionView(Transform parent, Font font, Color backgroundColor, Color borderColor, Color indicatorColor, Color statusTextColor)
        {
            GameObject rootObject = new GameObject("AlphaTabSection", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
            root = rootObject.GetComponent<RectTransform>();
            root.SetParent(parent, false);

            background = rootObject.GetComponent<Image>();
            background.color = backgroundColor;

            outline = rootObject.GetComponent<Outline>();
            outline.effectColor = borderColor;
            outline.effectDistance = new Vector2(1.5f, -1.5f);

            GameObject imageObject = new GameObject("Image", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageRect = imageObject.GetComponent<RectTransform>();
            imageRect.SetParent(root, false);
            image = imageObject.GetComponent<RawImage>();
            image.color = Color.white;
            image.raycastTarget = false;

            GameObject indicatorObject = new GameObject("Indicator", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform indicatorRect = indicatorObject.GetComponent<RectTransform>();
            indicatorRect.SetParent(imageRect, false);
            indicator = indicatorObject.GetComponent<Image>();
            indicator.color = indicatorColor;
            indicator.raycastTarget = false;
            indicator.gameObject.SetActive(false);

            loopStartIndicator = CreateLoopMarkerImage("LoopStart", new Color(1f, 0.24f, 0.24f, 0.96f));
            loopEndIndicator = CreateLoopMarkerImage("LoopEnd", new Color(1f, 0.48f, 0.32f, 0.92f));

            GameObject statusObject = new GameObject("Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
            RectTransform statusRect = statusObject.GetComponent<RectTransform>();
            statusRect.SetParent(root, false);
            statusRect.anchorMin = Vector2.zero;
            statusRect.anchorMax = Vector2.one;
            statusRect.offsetMin = new Vector2(12f, 8f);
            statusRect.offsetMax = new Vector2(-12f, -8f);
            statusLabel = statusObject.GetComponent<Text>();
            statusLabel.font = font;
            statusLabel.alignment = TextAnchor.MiddleCenter;
            statusLabel.fontSize = 20;
            statusLabel.color = statusTextColor;
            statusLabel.horizontalOverflow = HorizontalWrapMode.Wrap;
            statusLabel.verticalOverflow = VerticalWrapMode.Overflow;
            statusLabel.text = string.Empty;
            statusLabel.raycastTarget = false;
        }

        private Image CreateLoopMarkerImage(string name, Color color)
        {
            GameObject markerObject = new GameObject(name, typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            RectTransform markerRect = markerObject.GetComponent<RectTransform>();
            markerRect.SetParent(imageRect, false);

            Image marker = markerObject.GetComponent<Image>();
            marker.color = color;
            marker.raycastTarget = false;
            marker.gameObject.SetActive(false);
            return marker;
        }

        public void SetTexture(Texture texture, string statusText)
        {
            image.texture = texture;
            image.enabled = texture != null;
            statusLabel.text = statusText ?? string.Empty;
            statusLabel.gameObject.SetActive(!string.IsNullOrWhiteSpace(statusText));
        }

        public void SetIndicator(bool visible, float x01, float y01, float height01, float widthPixels)
        {
            indicator.gameObject.SetActive(visible);
            if (!visible)
                return;

            RectTransform indicatorRect = indicator.rectTransform;
            indicatorRect.anchorMin = new Vector2(0f, 1f);
            indicatorRect.anchorMax = new Vector2(0f, 1f);
            indicatorRect.pivot = new Vector2(0.5f, 1f);

            Rect rect = imageRect.rect;
            float x = rect.width * Mathf.Clamp01(x01);
            float yTop = rect.height * Mathf.Clamp01(y01);
            float height = Mathf.Max(12f, rect.height * Mathf.Clamp(height01, 0.06f, 1f));

            indicatorRect.sizeDelta = new Vector2(Mathf.Max(2f, widthPixels), height);
            indicatorRect.anchoredPosition = new Vector2(x, -yTop);
        }

        public void SetLoopMarkers(
            bool showStart,
            float startX01,
            float startY01,
            float startHeight01,
            bool showEnd,
            float endX01,
            float endY01,
            float endHeight01,
            float widthPixels)
        {
            SetLoopMarker(loopStartIndicator, showStart, startX01, startY01, startHeight01, widthPixels);
            SetLoopMarker(loopEndIndicator, showEnd, endX01, endY01, endHeight01, widthPixels);
        }

        private void SetLoopMarker(Image marker, bool visible, float x01, float y01, float height01, float widthPixels)
        {
            if (marker == null)
                return;

            marker.gameObject.SetActive(visible);
            if (!visible)
                return;

            RectTransform markerRect = marker.rectTransform;
            markerRect.anchorMin = new Vector2(0f, 1f);
            markerRect.anchorMax = new Vector2(0f, 1f);
            markerRect.pivot = new Vector2(0.5f, 1f);

            Rect rect = imageRect.rect;
            float x = rect.width * Mathf.Clamp01(x01);
            float yTop = rect.height * Mathf.Clamp01(y01);
            float height = Mathf.Max(16f, rect.height * Mathf.Clamp(height01, 0.08f, 1f));

            markerRect.sizeDelta = new Vector2(Mathf.Max(4f, widthPixels), height);
            markerRect.anchoredPosition = new Vector2(x, -yTop);
        }

        public void SetFeedbackOverlays(IReadOnlyList<FeedbackOverlayData> overlays)
        {
            int targetCount = overlays?.Count ?? 0;
            EnsureFeedbackOverlayCount(targetCount);

            for (int i = 0; i < feedbackOverlays.Count; i++)
            {
                Image overlay = feedbackOverlays[i];
                bool visible = i < targetCount;
                overlay.gameObject.SetActive(visible);
                if (!visible)
                    continue;

                FeedbackOverlayData data = overlays[i];
                RectTransform overlayRect = overlay.rectTransform;
                overlayRect.anchorMin = new Vector2(0f, 1f);
                overlayRect.anchorMax = new Vector2(0f, 1f);
                overlayRect.pivot = new Vector2(0.5f, 1f);

                Rect rect = imageRect.rect;
                float left = rect.width * Mathf.Clamp01(data.startX01);
                float right = rect.width * Mathf.Clamp01(data.endX01);
                float centerX = (left + right) * 0.5f;
                float width = Mathf.Max(10f, right - left);
                float yTop = rect.height * Mathf.Clamp01(data.y01);
                float height = Mathf.Max(14f, rect.height * Mathf.Clamp(data.height01, 0.07f, 1f));

                overlay.color = data.color;
                overlayRect.sizeDelta = new Vector2(width, height);
                overlayRect.anchoredPosition = new Vector2(centerX, -yTop);
            }
        }

        private void EnsureFeedbackOverlayCount(int count)
        {
            while (feedbackOverlays.Count < count)
            {
                GameObject overlayObject = new GameObject("Feedback", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
                RectTransform overlayRect = overlayObject.GetComponent<RectTransform>();
                overlayRect.SetParent(imageRect, false);

                Image overlay = overlayObject.GetComponent<Image>();
                overlay.raycastTarget = false;
                overlay.gameObject.SetActive(false);
                overlayRect.SetSiblingIndex(0);
                feedbackOverlays.Add(overlay);
            }
        }
    }

    private readonly AlphaTabSheetRuntimeConfig config;
    private readonly List<SectionView> sectionViews = new List<SectionView>();
    private readonly Dictionary<int, Texture2D> texturesBySectionIndex = new Dictionary<int, Texture2D>();

    private GuitarBridgeServer owner;
    private Canvas canvas;
    private RectTransform canvasRoot;
    private RectTransform regionRoot;
    private Image regionBackdrop;
    private Outline regionOutline;
    private Text statusBanner;
    private Font builtinFont;
    private Task<AlphaTabRenderManifestData> activeTask;
    private AlphaTabRenderManifestData activeManifest;
    private string activeRequestKey = string.Empty;
    private string activeNotationPath = string.Empty;
    private int activeTrackIndex = -1;
    private string pendingError = string.Empty;
    private bool manualVisible = true;
    private float activeTaskStartedRealtime;
    private float lastLongTaskLogRealtime = -999f;
    private AlphaTabRenderManifestData feedbackBindingManifest;
    private List<GameplayNoteState> feedbackBindingNoteStates;
    private readonly Dictionary<int, List<GameplayNoteState>> feedbackStatesBySourceEventId = new Dictionary<int, List<GameplayNoteState>>();
    private readonly List<NoteOnsetGroup> feedbackOnsetGroups = new List<NoteOnsetGroup>();
    private const float FeedbackOnsetTolerance = 0.02f;
    private const float FeedbackOverlayAlpha = 0.28f;
    private const float LoopMarkerWidthPixels = 5f;

    public AlphaTabSheetRuntime(AlphaTabSheetRuntimeConfig config)
    {
        this.config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public void Initialize(GuitarBridgeServer owner)
    {
        this.owner = owner;

        GameObject canvasObject = new GameObject(config.canvasName ?? "AlphaTabCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasRoot = canvasObject.GetComponent<RectTransform>();
        canvas = canvasObject.GetComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = config.sortingOrder;

        CanvasScaler scaler = canvasObject.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
        scaler.matchWidthOrHeight = 0.5f;

        GameObject regionObject = new GameObject("Region", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image), typeof(Outline));
        regionRoot = regionObject.GetComponent<RectTransform>();
        regionRoot.SetParent(canvasRoot, false);
        ApplyRegionAnchors();

        regionBackdrop = regionObject.GetComponent<Image>();
        regionBackdrop.color = config.regionBackdropColor;
        regionBackdrop.raycastTarget = false;
        regionOutline = regionObject.GetComponent<Outline>();
        regionOutline.effectDistance = new Vector2(1.5f, -1.5f);
        regionOutline.effectColor = new Color(0f, 0f, 0f, 0f);

        builtinFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        GameObject statusObject = new GameObject("StatusBanner", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
        RectTransform statusRect = statusObject.GetComponent<RectTransform>();
        statusRect.SetParent(regionRoot, false);
        statusRect.anchorMin = new Vector2(0f, 1f);
        statusRect.anchorMax = new Vector2(1f, 1f);
        statusRect.pivot = new Vector2(0.5f, 1f);
        statusRect.anchoredPosition = new Vector2(0f, -Mathf.Max(0f, config.statusBannerTopOffsetPixels));
        statusRect.sizeDelta = new Vector2(0f, Mathf.Max(0f, config.statusBannerHeightPixels));

        statusBanner = statusObject.GetComponent<Text>();
        statusBanner.font = builtinFont;
        statusBanner.alignment = TextAnchor.MiddleRight;
        statusBanner.fontSize = 20;
        statusBanner.color = new Color(0.62f, 0.78f, 0.97f, 0.90f);
        statusBanner.raycastTarget = false;
        statusBanner.text = string.Empty;
        statusBanner.gameObject.SetActive(config.showStatusBanner);

        EnsureSectionViewCount();
        UpdateSectionLayout();
        SetVisible(true);
    }

    public void Dispose()
    {
        foreach (Texture2D texture in texturesBySectionIndex.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        texturesBySectionIndex.Clear();
        activeManifest = null;
        activeTask = null;
        InvalidateFeedbackBindings();

        if (canvasRoot != null)
            UnityEngine.Object.Destroy(canvasRoot.gameObject);

        canvasRoot = null;
        canvas = null;
        regionRoot = null;
        regionBackdrop = null;
        regionOutline = null;
        statusBanner = null;
        owner = null;
    }

    public void SetVisible(bool visible)
    {
        manualVisible = visible;
        if (canvasRoot != null)
            canvasRoot.gameObject.SetActive(visible);
    }

    public void ApplyPalette(AlphaTabSheetThemePalette palette)
    {
        if (palette == null)
            return;

        EnsureSectionViewCount();

        if (regionBackdrop != null)
            regionBackdrop.color = config.useUnifiedRegionFrame ? palette.sectionBackgroundColor : palette.regionBackdropColor;

        if (regionOutline != null)
            regionOutline.effectColor = config.useUnifiedRegionFrame ? palette.sectionBorderColor : new Color(0f, 0f, 0f, 0f);

        if (statusBanner != null)
            statusBanner.color = palette.statusBannerColor;

        for (int i = 0; i < sectionViews.Count; i++)
        {
            SectionView view = sectionViews[i];
            if (view == null)
                continue;

            view.background.color = config.useUnifiedRegionFrame ? new Color(0f, 0f, 0f, 0f) : palette.sectionBackgroundColor;
            if (view.outline != null)
                view.outline.effectColor = config.useUnifiedRegionFrame ? new Color(0f, 0f, 0f, 0f) : palette.sectionBorderColor;
            if (view.statusLabel != null)
                view.statusLabel.color = palette.statusTextColor;
        }
    }

    public void Render(GuitarGameplaySnapshot snapshot)
    {
        if (owner == null || snapshot == null)
            return;

        bool effectiveVisible = manualVisible && !snapshot.mainMenuFlowActive;
        if (canvasRoot != null && canvasRoot.gameObject.activeSelf != effectiveVisible)
            canvasRoot.gameObject.SetActive(effectiveVisible);

        if (canvasRoot == null || !effectiveVisible)
            return;

        UpdateSectionLayout();
        EnsureManifestCurrent();
        ConsumeCompletedTaskIfAny();
        UpdateStatusBanner();

        if (activeManifest == null || activeManifest.sections == null || activeManifest.sections.Count == 0)
        {
            ApplyLoadingState();
            return;
        }

        EnsureFeedbackBindings(snapshot);

        int activeSectionIndex = FindActiveSectionIndex(snapshot.songTime);
        List<int> visibleSections = BuildVisibleSectionIndices(activeSectionIndex);
        int indicatorSectionIndex = activeSectionIndex;
        HashSet<int> keepAlive = new HashSet<int>(visibleSections);

        for (int slotIndex = 0; slotIndex < sectionViews.Count; slotIndex++)
        {
            SectionView view = sectionViews[slotIndex];
            int sectionIndex = slotIndex < visibleSections.Count ? visibleSections[slotIndex] : -1;
            if (sectionIndex < 0 || sectionIndex >= activeManifest.sections.Count)
            {
                view.root.gameObject.SetActive(false);
                continue;
            }

            AlphaTabRenderSectionData section = activeManifest.sections[sectionIndex];
            view.root.gameObject.SetActive(true);
            Texture2D texture = GetOrLoadTexture(sectionIndex, section.imagePath);
            string statusText = texture == null
                ? (!string.IsNullOrWhiteSpace(pendingError) ? pendingError : "Loading Tabs...")
                : string.Empty;
            view.SetTexture(texture, statusText);

            if (texture != null)
                view.SetFeedbackOverlays(BuildFeedbackOverlays(section, snapshot.songTime));
            else
                view.SetFeedbackOverlays(null);

            bool showLoopMarkers = snapshot.showLoopSettings || snapshot.loopEnabled;
            bool showLoopStart = false;
            bool showLoopEnd = false;
            float loopStartX = 0f;
            float loopStartY = 0f;
            float loopStartHeight = 0f;
            float loopEndX = 0f;
            float loopEndY = 0f;
            float loopEndHeight = 0f;

            if (showLoopMarkers)
            {
                showLoopStart = TryResolveLoopMarker(section, snapshot.loopStartTime, out loopStartX, out loopStartY, out loopStartHeight);
                showLoopEnd = TryResolveLoopMarker(section, snapshot.loopEndTime, out loopEndX, out loopEndY, out loopEndHeight);
            }

            if (sectionIndex == indicatorSectionIndex && TryResolveIndicator(section, snapshot.songTime, out float x, out float y, out float h))
            {
                view.SetIndicator(true, x, y, h, config.indicatorWidthPixels);
            }
            else
            {
                view.SetIndicator(false, 0f, 0f, 0f, config.indicatorWidthPixels);
            }

            view.SetLoopMarkers(
                showLoopStart,
                loopStartX,
                loopStartY,
                loopStartHeight,
                showLoopEnd,
                loopEndX,
                loopEndY,
                loopEndHeight,
                LoopMarkerWidthPixels);
        }

        ReleaseUnusedTextures(keepAlive);
    }

    private void UpdateStatusBanner()
    {
        if (statusBanner == null)
            return;

        statusBanner.gameObject.SetActive(config.showStatusBanner);
        if (!config.showStatusBanner)
        {
            statusBanner.text = string.Empty;
            return;
        }

        if (!string.IsNullOrWhiteSpace(pendingError))
            statusBanner.text = "Tabs unavailable";
        else if (activeTask != null && !activeTask.IsCompleted)
            statusBanner.text = "Rendering Tabs...";
        else if (activeManifest != null)
            statusBanner.text = activeManifest.trackLabel ?? string.Empty;
        else
            statusBanner.text = string.Empty;
    }

    private void ApplyLoadingState()
    {
        for (int i = 0; i < sectionViews.Count; i++)
        {
            SectionView view = sectionViews[i];
            view.root.gameObject.SetActive(true);
            view.SetTexture(null, !string.IsNullOrWhiteSpace(pendingError) ? pendingError : "Loading Tabs...");
            view.SetFeedbackOverlays(null);
            view.SetIndicator(false, 0f, 0f, 0f, config.indicatorWidthPixels);
            view.SetLoopMarkers(false, 0f, 0f, 0f, false, 0f, 0f, 0f, LoopMarkerWidthPixels);
        }
    }

    private void EnsureManifestCurrent()
    {
        if (!owner.TryGetCurrentAlphaTabRenderTarget(out string notationPath, out int trackIndex))
        {
            activeNotationPath = string.Empty;
            activeTrackIndex = -1;
            activeRequestKey = string.Empty;
            activeManifest = null;
            pendingError = "Tabs need a Guitar Pro source.";
            return;
        }

        string normalizedNotationPath = NormalizeNotationPath(notationPath);
        long helperLastWriteTicks = 0L;
        try
        {
            string helperPath = ExternalContentPaths.StreamingAlphaTabRenderHelperExePath;
            if (!string.IsNullOrWhiteSpace(helperPath) && File.Exists(helperPath))
                helperLastWriteTicks = File.GetLastWriteTimeUtc(helperPath).Ticks;
        }
        catch
        {
        }

        string themeId = owner != null ? AlphaTabVisualThemePalette.GetRequestThemeId(owner.alphaTabVisualTheme) : "white_on_dark_blue";
        string requestKey = $"{AlphaTabRenderManifestData.CurrentVersion}|{helperLastWriteTicks}|{themeId}|{normalizedNotationPath}|{trackIndex}|{config.renderWidth}|{config.renderScale}|{config.barsPerRow}|{config.barsPerSection}";
        if (requestKey == activeRequestKey)
            return;

        activeNotationPath = normalizedNotationPath;
        activeTrackIndex = trackIndex;
        activeRequestKey = requestKey;
        activeManifest = null;
        pendingError = string.Empty;
        DestroyAllTextures();
        InvalidateFeedbackBindings();

        AlphaTabRenderRequestData request = new AlphaTabRenderRequestData
        {
            notationPath = notationPath,
            trackIndex = trackIndex,
            themeId = themeId,
            renderWidth = config.renderWidth,
            scale = config.renderScale,
            barsPerRow = config.barsPerRow,
            barsPerSection = config.barsPerSection,
            outputDirectory = ExternalContentPaths.PersistentAlphaTabRenderCacheDirectory
        };

        try
        {
            Debug.Log($"[AlphaTab] Starting render request. notation='{notationPath}', trackIndex={trackIndex}, requestKey='{requestKey}'");
            activeTask = AlphaTabRenderClient.RenderAsync(request);
            activeTaskStartedRealtime = Time.realtimeSinceStartup;
            lastLongTaskLogRealtime = -999f;
        }
        catch (Exception ex)
        {
            activeTask = null;
            activeManifest = null;
            pendingError = ex.GetBaseException().Message;
            Debug.LogError($"[AlphaTab] Failed to start render request: {pendingError}");
        }
    }

    private void ConsumeCompletedTaskIfAny()
    {
        if (activeTask != null &&
            !activeTask.IsCompleted &&
            Time.realtimeSinceStartup - activeTaskStartedRealtime >= 5f &&
            Time.realtimeSinceStartup - lastLongTaskLogRealtime >= 5f)
        {
            lastLongTaskLogRealtime = Time.realtimeSinceStartup;
            Debug.LogWarning($"[AlphaTab] Render task still running after {Time.realtimeSinceStartup - activeTaskStartedRealtime:0.0}s. notation='{activeNotationPath}', trackIndex={activeTrackIndex}");
        }

        if (activeTask == null || !activeTask.IsCompleted)
            return;

        try
        {
            AlphaTabRenderManifestData manifest = activeTask.Result;
            string manifestNotationPath = NormalizeNotationPath(manifest.notationPath);
            if (!string.Equals(manifestNotationPath, activeNotationPath, StringComparison.OrdinalIgnoreCase) || manifest.trackIndex != activeTrackIndex)
            {
                Debug.LogWarning($"[AlphaTab] Completed manifest did not match active request. manifestPath='{manifestNotationPath}', activeNotation='{activeNotationPath}', manifestTrack={manifest.trackIndex}, activeTrack={activeTrackIndex}");
                return;
            }

            manifest.notationPath = manifestNotationPath;
            activeManifest = manifest;
            pendingError = string.Empty;
            InvalidateFeedbackBindings();
            Debug.Log($"[AlphaTab] Loaded manifest with {activeManifest.sections?.Count ?? 0} sections for '{activeNotationPath}' track {activeTrackIndex}.");
        }
        catch (Exception ex)
        {
            activeManifest = null;
            pendingError = ex.GetBaseException().Message;
            Debug.LogError($"[AlphaTab] Render task failed: {pendingError}");
        }
        finally
        {
            activeTask = null;
        }
    }

    private int FindActiveSectionIndex(float songTime)
    {
        if (activeManifest == null || activeManifest.sections == null || activeManifest.sections.Count == 0)
            return 0;

        for (int i = 0; i < activeManifest.sections.Count; i++)
        {
            AlphaTabRenderSectionData section = activeManifest.sections[i];
            if (songTime <= section.endTime + 0.001f)
                return i;
        }

        return Mathf.Max(0, activeManifest.sections.Count - 1);
    }

    private List<int> BuildVisibleSectionIndices(int activeSectionIndex)
    {
        List<int> indices = new List<int>();
        if (activeManifest == null || activeManifest.sections == null)
            return indices;

        if (config.useCircularVisibleSectionQueue)
        {
            int visibleCount = Mathf.Max(1, config.visibleSectionCount);
            int activeSlotIndex = Mathf.Clamp(activeSectionIndex % visibleCount, 0, visibleCount - 1);
            int cycleStartIndex = activeSectionIndex - activeSlotIndex;

            for (int slotIndex = 0; slotIndex < visibleCount; slotIndex++)
            {
                int sectionIndex = slotIndex < activeSlotIndex
                    ? cycleStartIndex + visibleCount + slotIndex
                    : cycleStartIndex + slotIndex;

                if (sectionIndex >= 0 && sectionIndex < activeManifest.sections.Count)
                    indices.Add(sectionIndex);
            }

            return indices;
        }

        for (int i = 0; i < config.visibleSectionCount; i++)
        {
            int sectionIndex = activeSectionIndex + i;
            if (sectionIndex >= 0 && sectionIndex < activeManifest.sections.Count)
                indices.Add(sectionIndex);
        }

        return indices;
    }

    private bool TryResolveIndicator(AlphaTabRenderSectionData section, float songTime, out float x, out float y, out float h)
    {
        x = 0f;
        y = 0f;
        h = 0f;

        if (section == null || section.beats == null || section.beats.Count == 0)
            return false;

        List<AlphaTabRenderBeatData> beats = section.beats;
        if (songTime <= beats[0].startTime)
        {
            AlphaTabRenderBeatData first = beats[0];
            x = first.indicatorX01;
            y = first.indicatorY01;
            h = first.indicatorHeight01;
            return true;
        }

        int selectedStartIndex = FindPreferredActiveClusterStartIndex(beats, songTime);
        if (selectedStartIndex >= 0)
        {
            AlphaTabRenderBeatData current = beats[selectedStartIndex];
            int clusterEndIndex = FindIndicatorClusterEndIndex(beats, selectedStartIndex);
            AlphaTabRenderBeatData clusterLast = beats[clusterEndIndex];
            float beatDuration = Mathf.Max(0.001f, clusterLast.endTime - current.startTime);
            float blend = Mathf.Clamp01((songTime - current.startTime) / beatDuration);
            x = Mathf.Lerp(current.indicatorX01, ResolveIndicatorEndX01(clusterLast), blend);
            y = current.indicatorY01;
            h = Mathf.Max(current.indicatorHeight01, clusterLast.indicatorHeight01);
            return true;
        }

        for (int i = 0; i < beats.Count; )
        {
            AlphaTabRenderBeatData current = beats[i];
            int clusterEndIndex = FindIndicatorClusterEndIndex(beats, i);
            AlphaTabRenderBeatData clusterLast = beats[clusterEndIndex];
            float beatDuration = Mathf.Max(0.001f, clusterLast.endTime - current.startTime);
            if (songTime <= clusterLast.endTime || clusterEndIndex == beats.Count - 1)
            {
                float blend = Mathf.Clamp01((songTime - current.startTime) / beatDuration);
                x = Mathf.Lerp(current.indicatorX01, ResolveIndicatorEndX01(clusterLast), blend);
                y = current.indicatorY01;
                h = Mathf.Max(current.indicatorHeight01, clusterLast.indicatorHeight01);
                return true;
            }

            i = clusterEndIndex + 1;
        }

        AlphaTabRenderBeatData last = beats[beats.Count - 1];
        x = ResolveIndicatorEndX01(last);
        y = last.indicatorY01;
        h = last.indicatorHeight01;
        return true;
    }

    private bool TryResolveLoopMarker(AlphaTabRenderSectionData section, float markerTime, out float x, out float y, out float h)
    {
        x = 0f;
        y = 0f;
        h = 0f;

        if (section == null || section.beats == null || section.beats.Count == 0)
            return false;

        if (markerTime < section.startTime - 0.0005f || markerTime > section.endTime + 0.0005f)
            return false;

        if (!TryResolveIndicator(section, markerTime, out x, out y, out h))
            return false;

        h = Mathf.Clamp(h * 1.12f, 0.16f, 1f);
        return true;
    }

    private static int FindPreferredActiveClusterStartIndex(List<AlphaTabRenderBeatData> beats, float songTime)
    {
        if (beats == null || beats.Count == 0)
            return -1;

        int selectedStartIndex = -1;
        int selectedEndIndex = -1;
        const float epsilon = 0.0005f;

        for (int i = 0; i < beats.Count; )
        {
            AlphaTabRenderBeatData current = beats[i];
            int clusterEndIndex = FindIndicatorClusterEndIndex(beats, i);
            AlphaTabRenderBeatData clusterLast = beats[clusterEndIndex];

            bool isActive = songTime + epsilon >= current.startTime && songTime <= clusterLast.endTime + epsilon;
            if (isActive && IsBetterActiveCluster(beats, i, clusterEndIndex, selectedStartIndex, selectedEndIndex))
            {
                selectedStartIndex = i;
                selectedEndIndex = clusterEndIndex;
            }

            i = clusterEndIndex + 1;
        }

        return selectedStartIndex;
    }

    private static bool IsBetterActiveCluster(
        List<AlphaTabRenderBeatData> beats,
        int candidateStartIndex,
        int candidateEndIndex,
        int selectedStartIndex,
        int selectedEndIndex)
    {
        if (selectedStartIndex < 0 || selectedEndIndex < 0)
            return true;

        AlphaTabRenderBeatData candidate = beats[candidateStartIndex];
        AlphaTabRenderBeatData candidateLast = beats[candidateEndIndex];
        AlphaTabRenderBeatData selected = beats[selectedStartIndex];
        AlphaTabRenderBeatData selectedLast = beats[selectedEndIndex];
        const float epsilon = 0.0005f;

        if (candidate.startTime > selected.startTime + epsilon)
            return true;
        if (candidate.startTime < selected.startTime - epsilon)
            return false;

        if (candidate.continuesFromPrevious != selected.continuesFromPrevious)
            return !candidate.continuesFromPrevious;

        float candidateEndX = ResolveIndicatorEndX01(candidateLast);
        float selectedEndX = ResolveIndicatorEndX01(selectedLast);
        if (candidateEndX > selectedEndX + epsilon)
            return true;
        if (candidateEndX < selectedEndX - epsilon)
            return false;

        float candidateDuration = Mathf.Max(0.001f, candidateLast.endTime - candidate.startTime);
        float selectedDuration = Mathf.Max(0.001f, selectedLast.endTime - selected.startTime);
        if (candidateDuration < selectedDuration - epsilon)
            return true;
        if (candidateDuration > selectedDuration + epsilon)
            return false;

        return candidate.voiceIndex > selected.voiceIndex;
    }


    private static int FindIndicatorClusterEndIndex(List<AlphaTabRenderBeatData> beats, int startIndex)
    {
        if (beats == null || startIndex < 0 || startIndex >= beats.Count)
            return startIndex;

        AlphaTabRenderBeatData first = beats[startIndex];
        if (first == null || first.isRest || first.sourceEventId < 0)
            return startIndex;

        int clusterEndIndex = startIndex;
        for (int i = startIndex + 1; i < beats.Count; i++)
        {
            AlphaTabRenderBeatData next = beats[i];
            if (next == null || next.isRest || next.sourceEventId != first.sourceEventId)
                break;

            clusterEndIndex = i;
        }

        return clusterEndIndex;
    }

    private static float ResolveIndicatorEndX01(AlphaTabRenderBeatData beat)
    {
        if (beat == null)
            return 0f;

        float endX = beat.indicatorEndX01;
        if (endX <= beat.indicatorX01 + 0.0005f)
            endX = beat.indicatorX01 + Mathf.Max(beat.visualWidth01, 0.02f);

        return Mathf.Clamp01(endX);
    }

    private void InvalidateFeedbackBindings()
    {
        feedbackBindingManifest = null;
        feedbackBindingNoteStates = null;
        feedbackStatesBySourceEventId.Clear();
        feedbackOnsetGroups.Clear();
    }

    private void EnsureFeedbackBindings(GuitarGameplaySnapshot snapshot)
    {
        if (owner == null || !owner.alphaTabNoteFeedbackEnabled || activeManifest == null || snapshot?.noteStates == null)
        {
            InvalidateFeedbackBindings();
            return;
        }

        if (ReferenceEquals(feedbackBindingManifest, activeManifest) &&
            ReferenceEquals(feedbackBindingNoteStates, snapshot.noteStates))
            return;

        feedbackBindingManifest = activeManifest;
        feedbackBindingNoteStates = snapshot.noteStates;
        feedbackStatesBySourceEventId.Clear();
        feedbackOnsetGroups.Clear();

        List<GameplayNoteState> orderedStates = snapshot.noteStates
            .Where(state => state != null)
            .OrderBy(state => state.data.time)
            .ThenBy(state => state.data.chordId)
            .ThenBy(state => state.data.stringIdx)
            .ToList();

        for (int i = 0; i < orderedStates.Count; i++)
        {
            GameplayNoteState state = orderedStates[i];
            if (feedbackOnsetGroups.Count == 0 || Mathf.Abs(state.data.time - feedbackOnsetGroups[feedbackOnsetGroups.Count - 1].time) > FeedbackOnsetTolerance)
            {
                feedbackOnsetGroups.Add(new NoteOnsetGroup
                {
                    time = state.data.time
                });
            }

            feedbackOnsetGroups[feedbackOnsetGroups.Count - 1].states.Add(state);
        }

        Dictionary<int, float> sourceEventStartTimes = new Dictionary<int, float>();
        for (int sectionIndex = 0; sectionIndex < activeManifest.sections.Count; sectionIndex++)
        {
            AlphaTabRenderSectionData section = activeManifest.sections[sectionIndex];
            if (section?.beats == null)
                continue;

            for (int beatIndex = 0; beatIndex < section.beats.Count; beatIndex++)
            {
                AlphaTabRenderBeatData beat = section.beats[beatIndex];
                if (beat == null || beat.isRest || beat.sourceEventId < 0)
                    continue;

                if (!sourceEventStartTimes.TryGetValue(beat.sourceEventId, out float existingStart) || beat.startTime < existingStart)
                    sourceEventStartTimes[beat.sourceEventId] = beat.startTime;
            }
        }

        foreach ((int sourceEventId, float startTime) in sourceEventStartTimes.OrderBy(pair => pair.Value))
        {
            if (TryResolveOnsetGroupByTime(startTime, out NoteOnsetGroup onsetGroup))
                feedbackStatesBySourceEventId[sourceEventId] = onsetGroup.states;
        }
    }

    private List<FeedbackOverlayData> BuildFeedbackOverlays(AlphaTabRenderSectionData section, float songTime)
    {
        List<FeedbackOverlayData> overlays = new List<FeedbackOverlayData>();
        if (owner == null || !owner.alphaTabNoteFeedbackEnabled || section?.beats == null || section.beats.Count == 0)
            return overlays;

        for (int beatIndex = 0; beatIndex < section.beats.Count; )
        {
            AlphaTabRenderBeatData beat = section.beats[beatIndex];
            int clusterEndIndex = FindIndicatorClusterEndIndex(section.beats, beatIndex);
            AlphaTabRenderBeatData clusterLast = section.beats[clusterEndIndex];

            if (TryResolveFeedbackStates(beat, out List<GameplayNoteState> states) &&
                TryResolveFeedbackColor(states, songTime, out Color feedbackColor))
            {
                overlays.Add(new FeedbackOverlayData
                {
                    startX01 = Mathf.Clamp01(beat.indicatorX01 - 0.004f),
                    endX01 = Mathf.Clamp01(Mathf.Max(ResolveIndicatorEndX01(clusterLast), beat.indicatorX01 + Mathf.Max(beat.visualWidth01, 0.018f)) + 0.004f),
                    y01 = beat.indicatorY01,
                    height01 = Mathf.Max(beat.indicatorHeight01, clusterLast.indicatorHeight01),
                    color = feedbackColor
                });
            }

            beatIndex = clusterEndIndex + 1;
        }

        return overlays;
    }

    private bool TryResolveFeedbackStates(AlphaTabRenderBeatData beat, out List<GameplayNoteState> states)
    {
        states = null;
        if (beat == null || beat.isRest)
            return false;

        if (beat.sourceEventId >= 0 && feedbackStatesBySourceEventId.TryGetValue(beat.sourceEventId, out states) && states != null && states.Count > 0)
            return true;

        if (TryResolveOnsetGroupByTime(beat.startTime, out NoteOnsetGroup onsetGroup))
        {
            states = onsetGroup.states;
            return states != null && states.Count > 0;
        }

        return false;
    }

    private bool TryResolveOnsetGroupByTime(float time, out NoteOnsetGroup group)
    {
        group = null;
        if (feedbackOnsetGroups.Count == 0)
            return false;

        int low = 0;
        int high = feedbackOnsetGroups.Count - 1;
        while (low <= high)
        {
            int mid = (low + high) >> 1;
            float candidateTime = feedbackOnsetGroups[mid].time;
            if (candidateTime < time)
                low = mid + 1;
            else
                high = mid - 1;
        }

        int[] candidates = { Mathf.Clamp(low, 0, feedbackOnsetGroups.Count - 1), Mathf.Clamp(low - 1, 0, feedbackOnsetGroups.Count - 1) };
        float bestDelta = float.MaxValue;
        for (int i = 0; i < candidates.Length; i++)
        {
            NoteOnsetGroup candidate = feedbackOnsetGroups[candidates[i]];
            float delta = Mathf.Abs(candidate.time - time);
            if (delta <= FeedbackOnsetTolerance && delta < bestDelta)
            {
                bestDelta = delta;
                group = candidate;
            }
        }

        return group != null;
    }

    private bool TryResolveFeedbackColor(List<GameplayNoteState> states, float songTime, out Color color)
    {
        color = Color.clear;
        if (states == null || states.Count == 0 || owner == null)
            return false;

        bool anyResolved = false;
        bool anyHit = false;
        bool allResolved = true;

        for (int i = 0; i < states.Count; i++)
        {
            GameplayNoteState state = states[i];
            if (state == null)
                continue;

            bool resolvedNow = state.IsResolved && state.resolvedAt >= 0f && songTime + 0.0005f >= state.resolvedAt;
            if (!resolvedNow)
            {
                allResolved = false;
                continue;
            }

            anyResolved = true;
            anyHit |= state.IsHit;
        }

        if (!anyResolved)
            return false;

        if (allResolved && anyHit)
        {
            color = owner.tabHitColor;
            color.a = FeedbackOverlayAlpha;
            return true;
        }

        return false;
    }

    private Texture2D GetOrLoadTexture(int sectionIndex, string imagePath)
    {
        if (texturesBySectionIndex.TryGetValue(sectionIndex, out Texture2D cached) && cached != null)
            return cached;

        if (string.IsNullOrWhiteSpace(imagePath) || !File.Exists(imagePath))
        {
            pendingError = $"AlphaTab image was not found: {imagePath}";
            Debug.LogError($"[AlphaTab] {pendingError}");
            return null;
        }

        try
        {
            byte[] bytes = File.ReadAllBytes(imagePath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.name = $"AlphaTabSection_{sectionIndex}";
            texture.LoadImage(bytes, markNonReadable: false);
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            texturesBySectionIndex[sectionIndex] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            pendingError = ex.GetBaseException().Message;
            Debug.LogError($"[AlphaTab] Failed to load texture '{imagePath}': {pendingError}");
            return null;
        }
    }

    private static string NormalizeNotationPath(string path)
    {
        if (string.IsNullOrWhiteSpace(path))
            return string.Empty;

        try
        {
            return Path.GetFullPath(path)
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);
        }
        catch
        {
            return path
                .Replace(Path.AltDirectorySeparatorChar, Path.DirectorySeparatorChar)
                .TrimEnd(Path.DirectorySeparatorChar);
        }
    }

    private void ReleaseUnusedTextures(HashSet<int> keepAlive)
    {
        List<int> staleKeys = texturesBySectionIndex.Keys.Where(index => !keepAlive.Contains(index)).ToList();
        for (int i = 0; i < staleKeys.Count; i++)
        {
            int key = staleKeys[i];
            if (texturesBySectionIndex.TryGetValue(key, out Texture2D texture) && texture != null)
                UnityEngine.Object.Destroy(texture);
            texturesBySectionIndex.Remove(key);
        }
    }

    private void DestroyAllTextures()
    {
        foreach (Texture2D texture in texturesBySectionIndex.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        texturesBySectionIndex.Clear();
    }

    private void UpdateSectionLayout()
    {
        EnsureSectionViewCount();
        if (regionRoot == null || sectionViews.Count == 0)
            return;

        ApplyRegionAnchors();

        float totalGap = Mathf.Max(0f, config.sectionGapPixels) * Mathf.Max(0, sectionViews.Count - 1);
        float topInset = Mathf.Max(0f, config.topInsetPixels);
        if (config.showStatusBanner)
            topInset = Mathf.Max(topInset, config.statusBannerTopOffsetPixels + config.statusBannerHeightPixels + 4f);

        float bottomInset = Mathf.Max(0f, config.bottomInsetPixels);
        float sideInset = Mathf.Max(0f, config.sideInsetPixels);

        if (config.layoutVisibleSectionsHorizontally)
        {
            float availableWidth = Mathf.Max(120f, regionRoot.rect.width - totalGap - (sideInset * 2f));
            float slotWidth = availableWidth / sectionViews.Count;
            float slotHeight = Mathf.Max(32f, regionRoot.rect.height - topInset - bottomInset);
            float x = sideInset;

            for (int i = 0; i < sectionViews.Count; i++)
            {
                SectionView view = sectionViews[i];
                RectTransform rootRect = view.root;
                rootRect.anchorMin = new Vector2(0f, 1f);
                rootRect.anchorMax = new Vector2(0f, 1f);
                rootRect.pivot = new Vector2(0f, 1f);
                rootRect.sizeDelta = new Vector2(slotWidth, slotHeight);
                rootRect.anchoredPosition = new Vector2(x, -topInset);

                view.imageRect.anchorMin = new Vector2(0.5f, 0.5f);
                view.imageRect.anchorMax = new Vector2(0.5f, 0.5f);
                view.imageRect.pivot = new Vector2(0.5f, 0.5f);

                Texture texture = view.image.texture;
                float horizontalPadding = Mathf.Max(0f, config.sectionInnerPaddingHorizontalPixels >= 0f ? config.sectionInnerPaddingHorizontalPixels : config.sectionInnerPaddingPixels);
                float verticalPadding = Mathf.Max(0f, config.sectionInnerPaddingVerticalPixels >= 0f ? config.sectionInnerPaddingVerticalPixels : config.sectionInnerPaddingPixels);
                float maxWidth = rootRect.rect.width - (horizontalPadding * 2f);
                float maxHeight = rootRect.rect.height - (verticalPadding * 2f);
                float imageWidth = maxWidth;
                float imageHeight = maxHeight;

                if (texture != null && texture.height > 0)
                {
                    float aspect = texture.width / (float)texture.height;
                    imageHeight = Mathf.Min(maxHeight, maxWidth / Mathf.Max(0.1f, aspect));
                    imageWidth = Mathf.Min(maxWidth, imageHeight * aspect);
                }

                view.imageRect.sizeDelta = new Vector2(imageWidth, imageHeight);
                view.imageRect.anchoredPosition = Vector2.zero;

                x += slotWidth + config.sectionGapPixels;
            }

            return;
        }

        float availableHeight = Mathf.Max(32f, regionRoot.rect.height - totalGap - topInset - bottomInset);
        float slotHeightVertical = availableHeight / sectionViews.Count;
        float y = -topInset;
        float width = Mathf.Max(240f, regionRoot.rect.width - (sideInset * 2f));

        for (int i = 0; i < sectionViews.Count; i++)
        {
            SectionView view = sectionViews[i];
            RectTransform rootRect = view.root;
            rootRect.anchorMin = new Vector2(0.5f, 1f);
            rootRect.anchorMax = new Vector2(0.5f, 1f);
            rootRect.pivot = new Vector2(0.5f, 1f);
            rootRect.sizeDelta = new Vector2(width, slotHeightVertical);
            rootRect.anchoredPosition = new Vector2(0f, y);

            view.imageRect.anchorMin = new Vector2(0.5f, 0.5f);
            view.imageRect.anchorMax = new Vector2(0.5f, 0.5f);
            view.imageRect.pivot = new Vector2(0.5f, 0.5f);

            Texture texture = view.image.texture;
            float horizontalPadding = Mathf.Max(0f, config.sectionInnerPaddingHorizontalPixels >= 0f ? config.sectionInnerPaddingHorizontalPixels : config.sectionInnerPaddingPixels);
            float verticalPadding = Mathf.Max(0f, config.sectionInnerPaddingVerticalPixels >= 0f ? config.sectionInnerPaddingVerticalPixels : config.sectionInnerPaddingPixels);
            float maxWidth = rootRect.rect.width - (horizontalPadding * 2f);
            float maxHeight = rootRect.rect.height - (verticalPadding * 2f);
            float imageWidth = maxWidth;
            float imageHeight = maxHeight;

            if (texture != null && texture.height > 0)
            {
                float aspect = texture.width / (float)texture.height;
                imageHeight = Mathf.Min(maxHeight, maxWidth / Mathf.Max(0.1f, aspect));
                imageWidth = Mathf.Min(maxWidth, imageHeight * aspect);
            }

            view.imageRect.sizeDelta = new Vector2(imageWidth, imageHeight);
            view.imageRect.anchoredPosition = Vector2.zero;

            y -= slotHeightVertical + config.sectionGapPixels;
        }
    }

    private void ApplyRegionAnchors()
    {
        if (regionRoot == null)
            return;

        regionRoot.anchorMin = config.anchorMin;
        regionRoot.anchorMax = config.anchorMax;
        regionRoot.offsetMin = Vector2.zero;
        regionRoot.offsetMax = Vector2.zero;
    }

    private void EnsureSectionViewCount()
    {
        if (regionRoot == null || builtinFont == null)
            return;

        int targetCount = Mathf.Max(1, config.visibleSectionCount);
        while (sectionViews.Count < targetCount)
        {
            sectionViews.Add(new SectionView(
                regionRoot,
                builtinFont,
                config.sectionBackgroundColor,
                config.sectionBorderColor,
                config.indicatorColor,
                config.statusTextColor));
        }

        while (sectionViews.Count > targetCount)
        {
            int index = sectionViews.Count - 1;
            SectionView view = sectionViews[index];
            if (view != null && view.root != null)
                UnityEngine.Object.Destroy(view.root.gameObject);
            sectionViews.RemoveAt(index);
        }
    }
}

public sealed class AlphaTabSheetRuntimeConfig
{
    public string canvasName = "AlphaTabCanvas";
    public int sortingOrder = 5;
    public Vector2 anchorMin = new Vector2(0.05f, 0.08f);
    public Vector2 anchorMax = new Vector2(0.95f, 0.82f);
    public bool showStatusBanner = true;
    public float statusBannerTopOffsetPixels = 6f;
    public float statusBannerHeightPixels = 32f;
    public float topInsetPixels = 42f;
    public float bottomInsetPixels = 12f;
    public float sideInsetPixels = 8f;
    public int visibleSectionCount = 2;
    public float sectionGapPixels = 18f;
    public float sectionInnerPaddingPixels = 10f;
    public float sectionInnerPaddingHorizontalPixels = -1f;
    public float sectionInnerPaddingVerticalPixels = -1f;
    public float indicatorWidthPixels = 3f;
    public int renderWidth = 1600;
    public float renderScale = 1f;
    public int barsPerRow = 2;
    public int barsPerSection = 2;
    public Color regionBackdropColor = new Color(0.02f, 0.04f, 0.08f, 0.65f);
    public Color sectionBackgroundColor = new Color(0.98f, 0.98f, 0.98f, 0.98f);
    public Color sectionBorderColor = new Color(0.08f, 0.08f, 0.08f, 0.92f);
    public Color indicatorColor = new Color(0.98f, 0.32f, 0.22f, 0.92f);
    public Color statusTextColor = new Color(0.08f, 0.08f, 0.08f, 0.90f);
    public bool useUnifiedRegionFrame = false;
    public bool useCircularVisibleSectionQueue = false;
    public bool layoutVisibleSectionsHorizontally = false;
}
