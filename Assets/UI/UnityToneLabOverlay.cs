using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
#if UNITY_RENDER_PIPELINE_UNIVERSAL
using UnityEngine.Rendering.Universal;
#endif
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class UnityToneLabOverlay : MonoBehaviour
{
    private enum ToneLabSidePanelMode
    {
        Presets,
        Library,
        Details
    }

    private enum ToneLabBoardMode
    {
        Pedalboard,
        SongMapping
    }

    private enum ToneLabSongMappingBrowseMode
    {
        All,
        Artists,
        Albums
    }

    private enum ToneLabPresetModalMode
    {
        Create,
        SaveAs,
        SaveGeneratedTone,
        ResetAll
    }

    private enum ToneLabUnsavedAction
    {
        None,
        SelectPreset,
        CloseToneLab
    }

    private enum ToneLabLibraryFilter
    {
        All,
        BuiltIn,
        Lv2,
        Nam
    }

    private enum ToneLabNavigationZone
    {
        Sidebar,
        PedalBoard,
        SongMappingLeft,
        SongMappingTones,
        Footer
    }

    private enum ToneLabNavigationDirection
    {
        None,
        Up,
        Down,
        Left,
        Right
    }

    private sealed class ToneLabNavigationItem
    {
        public VisualElement element;
        public ScrollView scrollView;
        public Action activate;
        public Action<bool> setHovered;
    }

    private sealed class ToneSliderBinding
    {
        public Slider slider;
        public Label valueLabel;
        public Func<UnityToneLabRuntime.ToneLabSettings, float> getter;
        public Action<UnityToneLabRuntime.ToneLabSettings, float> setter;
        public Func<float, string> formatter;
    }

    private sealed class ToneToggleBinding
    {
        public Button toggleButton;
        public Func<UnityToneLabRuntime.ToneLabSettings, bool> getter;
        public Action<UnityToneLabRuntime.ToneLabSettings, bool> setter;
    }

    private sealed class ToneLabBackdropBlurController : MonoBehaviour
    {
        private const string BlurShaderName = "Hidden/StringTheory/UIBackdropBlur";
        private const string BlurShaderResourcePath = "Shaders/UIBackdropBlur";
        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");
        private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");
        private const int Downsample = 4;
        private const int GameplayBackgroundLayer = 2;
        private const float BlurSize = 4.5f;
        private const float DefaultBlurBrightness = 0.48f;
        private const int BlurPassPairs = 3;

        public VisualElement TargetElement { get; set; }
        public Camera SourceCamera { get; set; }
        public float Brightness { get; set; } = DefaultBlurBrightness;

        private Camera captureCamera;
        private Material blurMaterial;
        private RenderTexture sceneTexture;
        private RenderTexture blurTextureA;
        private RenderTexture blurTextureB;
        private RenderTexture targetTexture;
        private int textureWidth = -1;
        private int textureHeight = -1;
        private int targetTextureWidth = -1;
        private int targetTextureHeight = -1;

        private void LateUpdate()
        {
            if (!ShouldRender())
                return;

            EnsureBlurMaterial();
            if (blurMaterial == null || blurMaterial.shader == null || !blurMaterial.shader.isSupported)
            {
                if (TargetElement != null)
                    TargetElement.style.backgroundImage = StyleKeyword.None;
                return;
            }

            int width = Mathf.Max(256, Mathf.CeilToInt(Screen.width / (float)Downsample));
            int height = Mathf.Max(144, Mathf.CeilToInt(Screen.height / (float)Downsample));
            Rect targetScreenBounds = GetTargetScreenBounds();
            int targetWidth = Mathf.Max(16, Mathf.CeilToInt(targetScreenBounds.width / (float)Downsample));
            int targetHeight = Mathf.Max(16, Mathf.CeilToInt(targetScreenBounds.height / (float)Downsample));

            EnsureRenderTextures(width, height);
            if (!RenderLiveCameraToSceneTexture())
            {
                return;
            }

            RenderTexture source = sceneTexture;
            for (int pass = 0; pass < BlurPassPairs; pass++)
            {
                blurMaterial.SetVector(BlurDirectionId, new Vector2(1f / textureWidth, 0f));
                blurMaterial.SetFloat(BlurSizeId, BlurSize);
                Graphics.Blit(source, blurTextureA, blurMaterial, 0);

                blurMaterial.SetVector(BlurDirectionId, new Vector2(0f, 1f / textureHeight));
                Graphics.Blit(blurTextureA, blurTextureB, blurMaterial, 0);
                source = blurTextureB;
            }

            if (TargetElement == null)
                return;

            EnsureTargetTexture(targetWidth, targetHeight);
            int sourceX = Mathf.Clamp(Mathf.FloorToInt(targetScreenBounds.xMin / Downsample), 0, Mathf.Max(0, textureWidth - targetWidth));
            int sourceY = Mathf.Clamp(textureHeight - Mathf.CeilToInt(targetScreenBounds.yMax / Downsample), 0, Mathf.Max(0, textureHeight - targetHeight));
            Vector2 scale = new Vector2(targetWidth / (float)textureWidth, targetHeight / (float)textureHeight);
            Vector2 offset = new Vector2(sourceX / (float)textureWidth, sourceY / (float)textureHeight);
            Graphics.Blit(blurTextureB, targetTexture, scale, offset);

            TargetElement.style.backgroundImage = StyleKeyword.None;
            TargetElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(targetTexture));
            TargetElement.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
            TargetElement.style.unityBackgroundImageTintColor = new Color(Brightness, Brightness, Brightness, 1f);
        }

        private bool ShouldRender()
        {
            return TargetElement != null
                && TargetElement.style.display.value != DisplayStyle.None
                && TargetElement.worldBound.width > 16f
                && TargetElement.worldBound.height > 16f;
        }

        private Rect GetTargetScreenBounds()
        {
            if (TargetElement == null)
                return default;

            Rect targetBounds = TargetElement.worldBound;
            VisualElement panelRoot = TargetElement.panel?.visualTree;
            if (panelRoot == null)
                return targetBounds;

            Rect panelBounds = panelRoot.worldBound;
            if (panelBounds.width <= 1f || panelBounds.height <= 1f)
                return targetBounds;

            float scaleX = Screen.width / panelBounds.width;
            float scaleY = Screen.height / panelBounds.height;
            if (!float.IsFinite(scaleX) || !float.IsFinite(scaleY) || scaleX <= 0f || scaleY <= 0f)
                return targetBounds;

            return new Rect(
                (targetBounds.xMin - panelBounds.xMin) * scaleX,
                (targetBounds.yMin - panelBounds.yMin) * scaleY,
                targetBounds.width * scaleX,
                targetBounds.height * scaleY);
        }

        private Camera ResolveSourceCamera()
        {
            if (SourceCamera != null)
                return SourceCamera;

            SourceCamera = Camera.main;
            return SourceCamera;
        }

        private void EnsureBlurMaterial()
        {
            if (blurMaterial != null)
                return;

            Shader shader = Resources.Load<Shader>(BlurShaderResourcePath);
            if (shader == null)
                shader = Shader.Find(BlurShaderName);
            if (shader == null)
                return;

            blurMaterial = new Material(shader)
            {
                hideFlags = HideFlags.HideAndDontSave
            };
        }

        private void EnsureCaptureCamera()
        {
            if (captureCamera != null)
                return;

            GameObject cameraObject = new GameObject("ToneLabBackdropBlurCamera");
            cameraObject.hideFlags = HideFlags.HideAndDontSave;
            cameraObject.transform.SetParent(transform, false);
            captureCamera = cameraObject.AddComponent<Camera>();
            captureCamera.enabled = false;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            cameraObject.AddComponent<UniversalAdditionalCameraData>();
#endif
        }

        private bool RenderLiveCameraToSceneTexture()
        {
            Camera sourceCamera = ResolveSourceCamera();
            if (sourceCamera == null)
                return false;

            EnsureCaptureCamera();

            captureCamera.CopyFrom(sourceCamera);
            captureCamera.enabled = false;
            captureCamera.targetTexture = sceneTexture;
            captureCamera.forceIntoRenderTexture = true;
            captureCamera.rect = new Rect(0f, 0f, 1f, 1f);
            if (sourceCamera.clearFlags == CameraClearFlags.Depth || sourceCamera.clearFlags == CameraClearFlags.Nothing)
            {
                captureCamera.clearFlags = CameraClearFlags.SolidColor;
                captureCamera.backgroundColor = sourceCamera.backgroundColor;
                captureCamera.cullingMask = sourceCamera.cullingMask | (1 << GameplayBackgroundLayer);
            }
            if (UnityEngine.Rendering.GraphicsSettings.currentRenderPipeline == null)
                captureCamera.stereoTargetEye = StereoTargetEyeMask.None;
#if UNITY_RENDER_PIPELINE_UNIVERSAL
            SyncUniversalCameraSettings(sourceCamera, captureCamera);
#endif
            captureCamera.Render();
            return true;
        }

        private void EnsureRenderTextures(int width, int height)
        {
            if (width == textureWidth && height == textureHeight && sceneTexture != null && blurTextureA != null && blurTextureB != null)
                return;

            ReleaseRenderTextures();
            textureWidth = width;
            textureHeight = height;
            sceneTexture = CreateBlurTexture("ToneLabBackdropScene", width, height);
            blurTextureA = CreateBlurTexture("ToneLabBackdropBlurA", width, height);
            blurTextureB = CreateBlurTexture("ToneLabBackdropBlurB", width, height);
        }

        private void EnsureTargetTexture(int width, int height)
        {
            if (width == targetTextureWidth && height == targetTextureHeight && targetTexture != null)
                return;

            ReleaseTexture(ref targetTexture);
            targetTextureWidth = width;
            targetTextureHeight = height;
            targetTexture = CreateBlurTexture("ToneLabBackdropTarget", width, height);
        }

        private static RenderTexture CreateBlurTexture(string textureName, int width, int height)
        {
            RenderTexture texture = new RenderTexture(width, height, 16, RenderTextureFormat.ARGB32)
            {
                name = textureName,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.Create();
            return texture;
        }

        private void ReleaseRenderTextures()
        {
            ReleaseTexture(ref sceneTexture);
            ReleaseTexture(ref blurTextureA);
            ReleaseTexture(ref blurTextureB);
            ReleaseTexture(ref targetTexture);
            textureWidth = -1;
            textureHeight = -1;
            targetTextureWidth = -1;
            targetTextureHeight = -1;
        }

        private static void ReleaseTexture(ref RenderTexture texture)
        {
            if (texture == null)
                return;

            texture.Release();
            Destroy(texture);
            texture = null;
        }

#if UNITY_RENDER_PIPELINE_UNIVERSAL
        private static void SyncUniversalCameraSettings(Camera source, Camera destination)
        {
            if (source == null || destination == null)
                return;

            if (!source.TryGetComponent(out UniversalAdditionalCameraData sourceData))
                return;

            UniversalAdditionalCameraData destinationData = destination.GetComponent<UniversalAdditionalCameraData>();
            if (destinationData == null)
                return;

            destinationData.renderType = CameraRenderType.Base;
            destinationData.renderPostProcessing = sourceData.renderPostProcessing;
            destinationData.antialiasing = sourceData.antialiasing;
            destinationData.antialiasingQuality = sourceData.antialiasingQuality;
            destinationData.stopNaN = sourceData.stopNaN;
            destinationData.dithering = sourceData.dithering;
            destinationData.renderShadows = sourceData.renderShadows;
            destinationData.requiresColorOption = sourceData.requiresColorOption;
            destinationData.requiresDepthOption = sourceData.requiresDepthOption;
            destinationData.volumeLayerMask = sourceData.volumeLayerMask;
            destinationData.volumeTrigger = sourceData.volumeTrigger;
            destinationData.allowXRRendering = false;
        }
#endif

        private void OnDisable()
        {
            Cleanup();
        }

        private void OnDestroy()
        {
            Cleanup();
        }

        private void Cleanup()
        {
            ReleaseRenderTextures();
            if (blurMaterial != null)
            {
                Destroy(blurMaterial);
                blurMaterial = null;
            }

            if (captureCamera != null)
            {
                Destroy(captureCamera.gameObject);
                captureCamera = null;
            }
        }
    }

    private GuitarBridgeServer owner;
    private UnityToneLabRuntime runtime;
    private UIDocument document;
    private PanelSettings panelSettings;
    private bool isBuilt;
    private bool isVisible;
    private bool suppressCallbacks;

    private VisualElement overlayRoot;
    private VisualElement blurBackdrop;
    private ToneLabBackdropBlurController backdropBlurController;
    private Label statusLabel;
    private Label routeLabel;
    private Label backendLabel;
    private ToneLabPedalBoardView pedalBoardView;
    private VisualElement sidebarRoot;
    private Button presetsTabButton;
    private Button libraryTabButton;
    private Button detailsTabButton;
    private ScrollView presetListScroll;
    private VisualElement presetListHost;
    private VisualElement presetSearchRoot;
    private TextField presetSearchField;
    private VisualElement libraryFilterRoot;
    private Button libraryAllFilterButton;
    private Button libraryBuiltInFilterButton;
    private Button libraryLv2FilterButton;
    private Button libraryNamFilterButton;
    private Button libraryRefreshButton;
    private Button songMappingAllFilterButton;
    private Button songMappingArtistsFilterButton;
    private Button songMappingAlbumsFilterButton;
    private VisualElement librarySearchRoot;
    private TextField librarySearchField;
    private VisualElement songMappingSearchRoot;
    private TextField songMappingSearchField;
    private VisualElement songMappingFilterRoot;
    private VisualElement pedalInspectorHost;
    private VisualElement pedalLibraryHost;
    private DropdownField inputDropdown;
    private DropdownField outputDropdown;
    private DropdownField latencyDropdown;
    private Button createPresetButton;
    private Button refreshDevicesButton;
    private Button advancedAudioButton;
    private Button savePresetButton;
    private Button saveAsPresetButton;
    private Button resetAllButton;
    private Button startButton;
    private Button stopButton;
    private Button backButton;
    private Button effectsFolderButton;
    private Button songMappingButton;
    private VisualElement pedalBoardRoot;
    private VisualElement songMappingRoot;
    private VisualElement songMappingLeftHost;
    private VisualElement songMappingToneHost;
    private ScrollView songMappingLeftScroll;
    private ScrollView songMappingToneScroll;
    private VisualElement songMappingSelectedArtwork;
    private Label songMappingHeaderLabel;
    private Label songMappingSubheaderLabel;
    private Slider guitarVolumeSlider;
    private Label guitarVolumeValueLabel;
    private VisualElement sidePanelHost;
    private ScrollView pedalInspectorScroll;
    private ScrollView pedalLibraryScroll;
    private VisualElement presetModalScrim;
    private VisualElement advancedAudioModalScrim;
    private VisualElement unsavedChangesModalScrim;
    private Label unsavedChangesPresetLabel;
    private Button unsavedChangesSaveButton;
    private Button unsavedChangesDiscardButton;
    private TextField presetNameField;
    private Button presetCreateButton;
    private Button presetCancelButton;
    private Label presetModalTitleLabel;
    private Label presetModalSubtitleLabel;
    private VisualElement presetNameSection;
    private DropdownField advancedInputChannelDropdown;
    private DropdownField advancedBackendDropdown;
    private DropdownField advancedInputDropdown;
    private DropdownField advancedOutputDropdown;
    private DropdownField advancedSampleRateDropdown;
    private DropdownField advancedBufferDropdown;
    private Button advancedBetaToggleButton;
    private Button advancedFallbackToggleButton;
    private Button advancedUnifiedToggleButton;
    private Button advancedRecorderCaptureToggleButton;
    private Button advancedAudioApplyButton;
    private Button advancedAudioCloseButton;
    private Label advancedAudioStatusLabel;
    private Label advancedAudioDiagnosticsLabel;
    private ReusableLoadingOverlay openingLoadingOverlay;
    private VisualElement actionToast;
    private Label actionToastLabel;
    private string selectedPedalInstanceId = string.Empty;
    private ToneLabSidePanelMode sidePanelMode = ToneLabSidePanelMode.Presets;
    private ToneLabBoardMode boardMode = ToneLabBoardMode.Pedalboard;
    private string selectedMappingSongKey = string.Empty;
    private string selectedMappingArrangementKey = string.Empty;
    private string pendingMappingToneName = string.Empty;
    private string pendingMappingArrangementKey = string.Empty;
    private string pendingMappingSongKey = string.Empty;
    private string pendingGeneratedToneName = string.Empty;
    private string pendingGeneratedArrangementKey = string.Empty;
    private string pendingGeneratedSongKey = string.Empty;
    private string pendingGeneratedPresetDefaultName = string.Empty;
    private string songMappingSearchQuery = string.Empty;
    private string songMappingBrowseScopeKey = string.Empty;
    private ToneLabSongMappingBrowseMode songMappingBrowseMode = ToneLabSongMappingBrowseMode.All;
    private VisualElement libraryDragPreview;
    private string libraryDragDescriptorId = string.Empty;
    private Vector2 libraryDragStartPosition;
    private int libraryDragPointerId = -1;
    private bool libraryDragMoved;
    private ToneLabPresetModalMode presetModalMode = ToneLabPresetModalMode.Create;
    private ToneLabUnsavedAction pendingUnsavedAction = ToneLabUnsavedAction.None;
    private SharedAudioAdvancedSettings advancedAudioDraft = new SharedAudioAdvancedSettings();
    private string presetSearchQuery = string.Empty;
    private string librarySearchQuery = string.Empty;
    private string pendingUnsavedPresetId = string.Empty;
    private ToneLabLibraryFilter libraryFilter = ToneLabLibraryFilter.All;
    private Coroutine openRefreshRoutine;
    private bool openingRefreshInProgress;
    private ToneLabNavigationZone navigationZone = ToneLabNavigationZone.Sidebar;
    private int navigationIndex;
    private ToneLabNavigationItem navigationHighlightedItem;
    private ToneLabNavigationDirection heldNavigationDirection = ToneLabNavigationDirection.None;
    private float nextNavigationRepeatTime = -1f;
    private VisualElement controllerCursor;
    private VisualElement controllerCursorInner;
    private VisualElement lastControllerCursorTarget;
    private Vector2 controllerCursorPanelPosition;
    private Vector3 lastPhysicalMousePosition;
    private bool controllerCursorActive;
    private bool controllerCursorInitialized;
    private bool controllerCursorPointerMode;
    private const float NavigationAxisThreshold = 0.55f;
    private const float NavigationInitialRepeatDelay = 0.34f;
    private const float NavigationRepeatDelay = 0.10f;

    private readonly List<ToneSliderBinding> sliderBindings = new List<ToneSliderBinding>();
    private readonly List<ToneToggleBinding> toggleBindings = new List<ToneToggleBinding>();
    private readonly List<ToneLabNavigationItem> presetNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> libraryNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> inspectorNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> songMappingLeftNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> songMappingToneNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> footerNavigationItems = new List<ToneLabNavigationItem>();
    private readonly List<ToneLabNavigationItem> visibleFooterNavigationItems = new List<ToneLabNavigationItem>();
    private static Texture2D presetSelectionGradientTexture;
    private static Texture2D selectedSidebarTabTexture;
    private static readonly Dictionary<string, Texture2D> songArtworkTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    public void Initialize(GuitarBridgeServer owner, UnityToneLabRuntime runtime)
    {
        this.owner = owner;
        this.runtime = runtime;

        if (isBuilt)
            return;

        document = gameObject.GetComponent<UIDocument>();
        if (document == null)
            document = gameObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings();
        document.panelSettings = panelSettings;

        VisualElement root = document.rootVisualElement;
        root.styleSheets.Clear();
        root.style.flexGrow = 1f;
        root.style.width = Length.Percent(100f);
        root.style.height = Length.Percent(100f);
        root.pickingMode = PickingMode.Position;

        BuildUi(root);
        if (overlayRoot != null)
            overlayRoot.style.display = DisplayStyle.None;
        isVisible = false;
        isBuilt = true;
    }

    public bool IsCapturingKeyboardInput
    {
        get
        {
            return isVisible
                && ((presetModalScrim != null && presetModalScrim.style.display == DisplayStyle.Flex)
                    || (advancedAudioModalScrim != null && advancedAudioModalScrim.style.display == DisplayStyle.Flex)
                    || (unsavedChangesModalScrim != null && unsavedChangesModalScrim.style.display == DisplayStyle.Flex)
                    || IsTextFieldFocused(presetSearchField)
                    || IsTextFieldFocused(librarySearchField)
                    || IsTextFieldFocused(songMappingSearchField)
                    || IsTextFieldFocused(presetNameField));
        }
    }

    public void RequestCloseFromUi()
    {
        if (HasUnsavedPresetChanges())
        {
            OpenUnsavedChangesModal(ToneLabUnsavedAction.CloseToneLab, string.Empty);
            return;
        }

        CloseToneLabNow();
    }

    public void SetVisible(bool visible)
    {
        if (!isBuilt || overlayRoot == null)
            return;

        DisplayStyle targetDisplay = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (isVisible == visible && overlayRoot.style.display == targetDisplay)
            return;

        isVisible = visible;
        overlayRoot.style.display = targetDisplay;
        if (visible)
        {
            ResetNavigation(ToneLabNavigationZone.Sidebar);
            overlayRoot.Focus();
            BeginOpeningRefresh();
        }
        else
        {
            CancelOpeningRefresh();
            CloseCreatePresetModal();
            CloseAdvancedAudioModal();
            CloseUnsavedChangesModal();
            CancelLibraryDrag();
            ClearPendingToneMappingAssignment();
            HideActionToast();
            HideControllerCursor();
            ClearNavigationHighlight();
        }
    }

    private void Update()
    {
        if (!isBuilt || !isVisible)
            return;

        bool controllerSubmitConsumed = UpdateControllerCursor();
        HandleNavigationInput(controllerSubmitConsumed);
    }

    public void RefreshUi(bool syncControls, bool refreshDevices = false)
    {
        if (!isBuilt || runtime == null)
            return;

        bool refreshInteractiveState = syncControls || refreshDevices;
        if (refreshInteractiveState)
        {
            if (refreshDevices)
            {
                if (owner != null)
                    owner.RefreshSharedAudioDevicesFromUi();
                else
                    runtime.RefreshInputDevices(forcePortAudioRescan: true);
            }

            UnityToneLabRuntime.ToneLabSettings settings = runtime.CurrentSettings;
            suppressCallbacks = true;

            List<string> deviceChoices = owner?.GetSharedAudioInputDeviceChoices()?.ToList()
                ?? (runtime.InputDevices != null && runtime.InputDevices.Length > 0
                    ? runtime.InputDevices.ToList()
                    : new List<string> { "No microphone inputs" });
            if (inputDropdown != null)
                inputDropdown.choices = deviceChoices;
            string selectedInput = owner != null
                ? owner.GetSharedAudioSelectedInputLabel()
                : (!string.IsNullOrWhiteSpace(settings.input_device_name) && deviceChoices.Contains(settings.input_device_name)
                    ? settings.input_device_name
                    : deviceChoices[0]);
            inputDropdown?.SetValueWithoutNotify(selectedInput);
            inputDropdown?.SetEnabled(deviceChoices.Count > 0);

            List<string> outputChoices = owner?.GetSharedAudioOutputDeviceChoices()?.ToList()
                ?? (runtime.OutputDevices != null && runtime.OutputDevices.Length > 0
                    ? runtime.OutputDevices.ToList()
                    : new List<string> { "No output devices" });
            if (outputDropdown != null)
                outputDropdown.choices = outputChoices;
            string selectedOutput = owner != null
                ? owner.GetSharedAudioSelectedOutputLabel()
                : (!string.IsNullOrWhiteSpace(settings.output_device_name) && outputChoices.Contains(settings.output_device_name)
                    ? settings.output_device_name
                    : outputChoices[0]);
            outputDropdown?.SetValueWithoutNotify(selectedOutput);
            outputDropdown?.SetEnabled(outputChoices.Count > 0);

            List<string> latencyChoices = runtime.MonitoringLatencyOptions != null && runtime.MonitoringLatencyOptions.Length > 0
                ? runtime.MonitoringLatencyOptions.ToList()
                : new List<string> { "Low (128)" };
            if (latencyDropdown != null)
                latencyDropdown.choices = latencyChoices;
            string selectedLatency = owner != null
                ? owner.GetSharedAudioSelectedLatencyLabel()
                : (latencyChoices.Contains(runtime.CurrentMonitoringLatencyOption)
                    ? runtime.CurrentMonitoringLatencyOption
                    : latencyChoices[0]);
            latencyDropdown?.SetValueWithoutNotify(selectedLatency);
            latencyDropdown?.SetEnabled(true);

            float guitarVolumePercent = owner != null
                ? owner.GetSharedAudioGuitarVolumePercent()
                : runtime.MonitorVolumePercent;
            if (guitarVolumeSlider != null)
                guitarVolumeSlider.SetValueWithoutNotify(guitarVolumePercent);
            if (guitarVolumeValueLabel != null)
                guitarVolumeValueLabel.text = $"{guitarVolumePercent:F0}%";

            UnityToneLabRuntime.ToneLabPreset[] presets = runtime.CurrentPresets;
            string currentPresetId = runtime.CurrentPresetId;
            RefreshPresetList(presets, currentPresetId);
            bool songMappingVisible = boardMode == ToneLabBoardMode.SongMapping;
            savePresetButton?.SetEnabled(!songMappingVisible && !string.IsNullOrWhiteSpace(currentPresetId));
            saveAsPresetButton?.SetEnabled(!songMappingVisible);
            resetAllButton?.SetEnabled(!songMappingVisible);
            if (savePresetButton != null)
                savePresetButton.style.display = songMappingVisible ? DisplayStyle.None : DisplayStyle.Flex;
            if (saveAsPresetButton != null)
                saveAsPresetButton.style.display = songMappingVisible ? DisplayStyle.None : DisplayStyle.Flex;
            if (resetAllButton != null)
                resetAllButton.style.display = songMappingVisible ? DisplayStyle.None : DisplayStyle.Flex;

            for (int i = 0; i < sliderBindings.Count; i++)
            {
                ToneSliderBinding binding = sliderBindings[i];
                float value = binding.getter(settings);
                binding.slider.SetValueWithoutNotify(value);
                binding.valueLabel.text = binding.formatter(value);
            }

            for (int i = 0; i < toggleBindings.Count; i++)
            {
                ToneToggleBinding binding = toggleBindings[i];
                bool enabled = binding.getter(settings);
                ApplyToggleButtonState(binding.toggleButton, enabled);
            }
            UnityToneLabRuntime.ToneLabPedalSlot[] pedalChain = runtime.CurrentPedalChain;
            EnsureSelectedPedal(pedalChain);
            pedalBoardView?.Refresh(pedalChain, selectedPedalInstanceId);
            RefreshBoardModeVisuals();
            RefreshSongMappingView();
            RefreshPedalLibrary(pedalChain);
            RefreshSidePanel(pedalChain);

            suppressCallbacks = false;
        }

        string configPath = owner != null ? owner.GetSharedAudioSettingsPathForUi() : ExternalContentPaths.PersistentAudioSettingsPath;
        if (backendLabel != null)
        {
            backendLabel.text = $"Config File  {configPath}";
            backendLabel.style.display = DisplayStyle.Flex;
        }
        if (routeLabel != null)
        {
            string routeText = runtime.IsMonitoring || runtime.IsAwaitingStartup
                ? $"{runtime.ActiveAudioBackendLabel}  \u2022  {runtime.ActiveHostApiLabel}  \u2022  In {runtime.InputRouteLabel}  \u2022  Out {runtime.OutputRouteLabel}"
                : string.Empty;
            routeLabel.text = routeText;
            routeLabel.style.display = string.IsNullOrWhiteSpace(routeText) ? DisplayStyle.None : DisplayStyle.Flex;
        }
        if (statusLabel != null)
        {
            statusLabel.text = string.IsNullOrWhiteSpace(runtime.StatusMessage)
                ? (runtime.IsMonitoring ? "Live monitoring active." : "Monitoring idle.")
                : runtime.StatusMessage;
            statusLabel.style.color = runtime.IsMonitoring || runtime.IsAwaitingStartup
                ? new Color(0.69f, 0.92f, 0.76f, 1f)
                : new Color(0.92f, 0.84f, 0.63f, 1f);
        }

        startButton?.SetEnabled(!runtime.IsMonitoring && !runtime.IsAwaitingStartup);
        stopButton?.SetEnabled(runtime.IsMonitoring || runtime.IsAwaitingStartup);
        RefreshSidePanelButtonStates();
        RefreshAdvancedAudioModalStatus();
        UpdateOpeningLoadingOverlay();
        if (refreshInteractiveState)
            RefreshNavigationHighlight();
    }

    private void BuildUi(VisualElement root)
    {
        Font uiFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        FontDefinition uiFontDefinition = FontDefinition.FromFont(uiFont);
        Font toneLabTitleFont = Resources.Load<Font>("MetalLord") ?? Resources.Load<Font>("Fonts/MetalLord") ?? uiFont;
        FontDefinition toneLabTitleFontDefinition = FontDefinition.FromFont(toneLabTitleFont);

        overlayRoot = new VisualElement();
        overlayRoot.AddToClassList("tone-lab-overlay");
        overlayRoot.pickingMode = PickingMode.Position;
        overlayRoot.focusable = true;
        overlayRoot.style.display = DisplayStyle.None;
        ApplyOverlayRootStyle(overlayRoot);

        blurBackdrop = new VisualElement();
        blurBackdrop.AddToClassList("tone-lab-backdrop");
        ApplyBackdropStyle(blurBackdrop);
        overlayRoot.Add(blurBackdrop);

        backdropBlurController = gameObject.GetComponent<ToneLabBackdropBlurController>();
        if (backdropBlurController == null)
            backdropBlurController = gameObject.AddComponent<ToneLabBackdropBlurController>();
        backdropBlurController.TargetElement = blurBackdrop;

        VisualElement window = new VisualElement();
        window.AddToClassList("tone-lab-window");
        ApplyWindowStyle(window);
        overlayRoot.Add(window);

        VisualElement mainContent = new VisualElement();
        mainContent.style.flexGrow = 1f;
        mainContent.style.minHeight = 0f;
        mainContent.style.flexDirection = FlexDirection.Row;
        mainContent.style.overflow = Overflow.Hidden;
        window.Add(mainContent);

        sidebarRoot = new VisualElement();
        sidebarRoot.style.width = 430f;
        sidebarRoot.style.minWidth = 430f;
        sidebarRoot.style.maxWidth = 430f;
        sidebarRoot.style.flexShrink = 0f;
        sidebarRoot.style.flexGrow = 0f;
        sidebarRoot.style.minHeight = 0f;
        sidebarRoot.style.marginRight = 28f;
        sidebarRoot.style.flexDirection = FlexDirection.Column;
        mainContent.Add(sidebarRoot);

        Label titleLabel = CreateLabel("Tone Lab", "tone-lab-title", toneLabTitleFontDefinition);
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 30f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        titleLabel.style.marginBottom = 14f;
        sidebarRoot.Add(titleLabel);

        VisualElement tabRow = new VisualElement();
        tabRow.style.flexDirection = FlexDirection.Row;
        tabRow.style.height = 44f;
        tabRow.style.flexShrink = 0f;
        tabRow.style.marginBottom = 14f;
        tabRow.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        sidebarRoot.Add(tabRow);

        presetsTabButton = CreateSidebarTabButton("PRESETS", StartupLogoColor(1), () => SetSidePanelMode(ToneLabSidePanelMode.Presets));
        libraryTabButton = CreateSidebarTabButton("LIBRARY", StartupLogoColor(2), () => SetSidePanelMode(ToneLabSidePanelMode.Library));
        detailsTabButton = CreateSidebarTabButton("DETAILS", StartupLogoColor(4), () => SetSidePanelMode(ToneLabSidePanelMode.Details));
        tabRow.Add(presetsTabButton);
        tabRow.Add(libraryTabButton);
        tabRow.Add(detailsTabButton);

        createPresetButton = CreateButton("+", "tone-lab-button tone-lab-button-secondary", () => OpenPresetModal(ToneLabPresetModalMode.Create));
        StyleRoundIconButton(createPresetButton, 48f, 30f);

        presetSearchRoot = CreateSidebarSearchBlock("search...", out presetSearchField, query =>
        {
            presetSearchQuery = query ?? string.Empty;
            if (runtime != null)
                RefreshPresetList(runtime.CurrentPresets, runtime.CurrentPresetId);
        });

        librarySearchRoot = CreateSidebarSearchBlock("search...", out librarySearchField, query =>
        {
            librarySearchQuery = query ?? string.Empty;
            if (runtime != null)
                RefreshPedalLibrary(runtime.CurrentPedalChain);
        });
        libraryFilterRoot = CreateLibraryFilterBar();

        songMappingSearchRoot = CreateSidebarSearchBlock("search...", out songMappingSearchField, query =>
        {
            songMappingSearchQuery = query ?? string.Empty;
            RefreshSongMappingView();
        });
        songMappingFilterRoot = CreateSongMappingFilterBar();

        presetListScroll = new ScrollView(ScrollViewMode.Vertical);
        StyleTransparentScrollView(presetListScroll);
        presetListHost = presetListScroll.contentContainer;
        presetListHost.style.flexDirection = FlexDirection.Column;
        presetListHost.style.paddingBottom = 10f;

        pedalLibraryScroll = new ScrollView(ScrollViewMode.Vertical);
        StyleTransparentScrollView(pedalLibraryScroll);
        pedalLibraryHost = pedalLibraryScroll.contentContainer;
        pedalLibraryHost.style.flexDirection = FlexDirection.Column;
        pedalLibraryHost.style.paddingBottom = 10f;

        pedalInspectorScroll = new ScrollView(ScrollViewMode.Vertical);
        StyleTransparentScrollView(pedalInspectorScroll);
        pedalInspectorHost = pedalInspectorScroll.contentContainer;
        pedalInspectorHost.style.flexDirection = FlexDirection.Column;
        pedalInspectorHost.style.paddingBottom = 10f;

        sidePanelHost = new VisualElement();
        sidePanelHost.style.flexGrow = 1f;
        sidePanelHost.style.minHeight = 0f;
        sidePanelHost.style.overflow = Overflow.Hidden;
        sidebarRoot.Add(sidePanelHost);

        VisualElement rightColumn = new VisualElement();
        rightColumn.style.flexGrow = 1f;
        rightColumn.style.minWidth = 0f;
        rightColumn.style.minHeight = 0f;
        rightColumn.style.flexDirection = FlexDirection.Column;
        mainContent.Add(rightColumn);

        inputDropdown = new DropdownField();
        ApplyDropdownStyle(inputDropdown);
        inputDropdown.style.minWidth = 230f;
        inputDropdown.style.width = 230f;
        inputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            if (owner != null)
                owner.SetSharedAudioInputDeviceFromUi(evt.newValue);
            else
                runtime.UpdateSettings(settings => settings.input_device_name = evt.newValue, restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        outputDropdown = new DropdownField();
        ApplyDropdownStyle(outputDropdown);
        outputDropdown.style.minWidth = 230f;
        outputDropdown.style.width = 230f;
        outputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            if (owner != null)
                owner.SetSharedAudioOutputDeviceFromUi(evt.newValue);
            else
                runtime.UpdateSettings(settings => settings.output_device_name = evt.newValue, restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        latencyDropdown = new DropdownField();
        ApplyDropdownStyle(latencyDropdown);
        latencyDropdown.style.minWidth = 172f;
        latencyDropdown.style.width = 172f;
        latencyDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            if (owner != null)
                owner.SetSharedAudioMonitoringLatencyFromUi(evt.newValue);
            else
                runtime.UpdateSettings(settings => settings.monitoring_buffer_size = ParseLatencyPresetBufferSize(evt.newValue), restartMonitoring: true);
            RefreshUi(syncControls: true);
        });

        refreshDevicesButton = CreateButton("Refresh", "tone-lab-button tone-lab-button-secondary", () =>
        {
            runtime?.RefreshExternalPedalLibrary(force: true);
            RefreshUi(syncControls: true, refreshDevices: true);
        });
        StyleCompactActionButton(refreshDevicesButton, 98f);

        advancedAudioButton = CreateButton("Advanced Audio", "tone-lab-button tone-lab-button-secondary", OpenAdvancedAudioModal);
        StyleCompactActionButton(advancedAudioButton, 158f);
        advancedAudioButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        advancedAudioButton.style.color = new Color(0.98f, 0.62f, 0.42f, 1f);
        advancedAudioButton.style.borderTopColor = new Color(0.98f, 0.62f, 0.42f, 1f);
        advancedAudioButton.style.borderRightColor = new Color(0.98f, 0.62f, 0.42f, 1f);
        advancedAudioButton.style.borderBottomColor = new Color(0.98f, 0.62f, 0.42f, 1f);
        advancedAudioButton.style.borderLeftColor = new Color(0.98f, 0.62f, 0.42f, 1f);

        startButton = CreateButton("Start Audio", "tone-lab-button tone-lab-button-primary", () =>
        {
            runtime?.TryStartMonitoring();
            RefreshUi(syncControls: false);
        });
        StyleCompactActionButton(startButton, 116f);

        stopButton = CreateButton("Stop Audio", "tone-lab-button tone-lab-button-secondary", () =>
        {
            runtime?.StopMonitoring();
            RefreshUi(syncControls: false);
        });
        StyleCompactActionButton(stopButton, 108f);

        VisualElement routingBar = CreateThinDividerSection();
        routingBar.style.height = 66f;
        routingBar.style.flexShrink = 0f;
        routingBar.style.marginBottom = 0f;
        routingBar.style.borderTopWidth = 0f;
        routingBar.style.borderBottomWidth = 0f;
        routingBar.style.flexDirection = FlexDirection.Row;
        routingBar.style.alignItems = Align.Center;
        routingBar.style.justifyContent = Justify.FlexStart;
        routingBar.Add(CreateModernToolbarField("Input", inputDropdown, 230f));
        routingBar.Add(CreateModernToolbarField("Output", outputDropdown, 230f));
        routingBar.Add(CreateModernToolbarField("Latency", latencyDropdown, 172f));
        routingBar.Add(CreateModernToolbarField("Audio", advancedAudioButton, 158f));

        VisualElement routingSpacer = new VisualElement();
        routingSpacer.style.flexGrow = 1f;
        routingSpacer.style.minWidth = 10f;
        routingBar.Add(routingSpacer);

        VisualElement routingActions = new VisualElement();
        routingActions.style.flexDirection = FlexDirection.Row;
        routingActions.style.alignItems = Align.Center;
        routingActions.style.justifyContent = Justify.FlexEnd;
        routingActions.style.flexShrink = 0f;
        routingBar.Add(routingActions);
        routingActions.Add(refreshDevicesButton);
        routingActions.Add(startButton);
        routingActions.Add(stopButton);
        rightColumn.Add(routingBar);

        VisualElement globalSettingsBar = CreateThinDividerSection();
        globalSettingsBar.style.height = 72f;
        globalSettingsBar.style.flexShrink = 0f;
        globalSettingsBar.style.marginBottom = 12f;
        globalSettingsBar.style.borderTopWidth = 0f;
        globalSettingsBar.style.borderBottomWidth = 2f;
        globalSettingsBar.style.borderBottomColor = new Color(1f, 1f, 1f, 0.32f);
        globalSettingsBar.style.flexDirection = FlexDirection.Row;
        globalSettingsBar.style.alignItems = Align.Center;
        globalSettingsBar.style.flexWrap = Wrap.NoWrap;
        rightColumn.Add(globalSettingsBar);

        globalSettingsBar.Add(CreateGainSectionLabel("Global", 78f));

        globalSettingsBar.Add(CreateCompactSliderField(
            "Input Gain",
            -36f,
            12f,
            value => $"{value:F1} dB",
            settings => settings.global_input_trim_db,
            (settings, value) => settings.global_input_trim_db = value,
            260f));

        globalSettingsBar.Add(CreateCompactSliderField(
            "Output Gain",
            -12f,
            12f,
            value => $"{value:F1} dB",
            settings => settings.global_output_gain_db,
            (settings, value) => settings.global_output_gain_db = value,
            260f));

        VisualElement volumeField = CreateSharedVolumeSliderField(
            "Guitar Volume",
            0f,
            100f,
            value => $"{value:F0}%",
            () => owner != null ? owner.GetSharedAudioGuitarVolumePercent() : runtime?.MonitorVolumePercent ?? 100f,
            value =>
            {
                if (owner != null)
                    owner.SetSharedAudioGuitarVolumeFromUi(value);
                else
                    runtime?.SetMonitorVolumePercent(value);
            },
            out guitarVolumeSlider,
            out guitarVolumeValueLabel,
            260f);
        volumeField.style.marginRight = 0f;
        globalSettingsBar.Add(volumeField);

        VisualElement boardSection = new VisualElement();
        boardSection.style.flexGrow = 1f;
        boardSection.style.minHeight = 0f;
        boardSection.style.flexDirection = FlexDirection.Column;
        rightColumn.Add(boardSection);

        pedalBoardView = new ToneLabPedalBoardView();
        VisualElement presetGainControls = pedalBoardView.HeaderControls;
        presetGainControls.Add(CreateGainSectionLabel("Preset", 70f));
        presetGainControls.Add(CreateCompactSliderField(
            "Input Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.input_gain_db,
            (settings, value) => settings.input_gain_db = value,
            236f));
        VisualElement outputGainField = CreateCompactSliderField(
            "Output Gain",
            -36f,
            36f,
            value => $"{value:F1} dB",
            settings => settings.output_gain_db,
            (settings, value) => settings.output_gain_db = value,
            236f);
        outputGainField.style.marginRight = 0f;
        presetGainControls.Add(outputGainField);
        pedalBoardView.AddPedalRequested += () =>
        {
            sidePanelMode = ToneLabSidePanelMode.Library;
            ResetNavigation(ToneLabNavigationZone.Sidebar);
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalSelected += pedalInstanceId =>
        {
            selectedPedalInstanceId = pedalInstanceId;
            ResetNavigation(ToneLabNavigationZone.PedalBoard);
            sidePanelMode = ToneLabSidePanelMode.Details;
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalEnabledChanged += (pedalInstanceId, enabled) =>
        {
            runtime?.SetPedalEnabled(pedalInstanceId, enabled);
            selectedPedalInstanceId = pedalInstanceId;
            ResetNavigation(ToneLabNavigationZone.PedalBoard);
            sidePanelMode = ToneLabSidePanelMode.Details;
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalRemoveRequested += pedalInstanceId =>
        {
            runtime?.RemovePedalFromChain(pedalInstanceId);
            RefreshUi(syncControls: true);
        };
        pedalBoardView.PedalOrderCommitted += orderedPedalIds =>
        {
            runtime?.SetPedalChainOrder(orderedPedalIds);
            RefreshUi(syncControls: true);
        };
        pedalBoardRoot = pedalBoardView.Root;
        pedalBoardRoot.style.flexGrow = 1f;
        pedalBoardRoot.style.minHeight = 0f;
        boardSection.Add(pedalBoardRoot);

        songMappingRoot = CreateSongMappingView();
        songMappingRoot.style.display = DisplayStyle.None;
        boardSection.Add(songMappingRoot);

        VisualElement boardFooter = new VisualElement();
        boardFooter.style.height = 72f;
        boardFooter.style.flexShrink = 0f;
        boardFooter.style.flexDirection = FlexDirection.Row;
        boardFooter.style.alignItems = Align.Center;
        boardFooter.style.justifyContent = Justify.SpaceBetween;
        boardFooter.style.borderTopWidth = 1f;
        boardFooter.style.borderTopColor = new Color(1f, 1f, 1f, 0.16f);
        boardFooter.style.paddingTop = 10f;
        boardSection.Add(boardFooter);

        VisualElement footerLeftActions = new VisualElement();
        footerLeftActions.style.flexDirection = FlexDirection.Row;
        footerLeftActions.style.alignItems = Align.Center;
        footerLeftActions.style.justifyContent = Justify.FlexStart;
        boardFooter.Add(footerLeftActions);

        backButton = CreateButton("Back", "tone-lab-button tone-lab-button-secondary", RequestCloseFromUi);
        StyleFooterActionButton(backButton, 118f);
        footerLeftActions.Add(backButton);

        effectsFolderButton = CreateButton("Effects Folder", "tone-lab-button tone-lab-button-secondary", OpenEffectsFolder);
        StyleFooterActionButton(effectsFolderButton, 178f);
        footerLeftActions.Add(effectsFolderButton);

        songMappingButton = CreateButton("Song Mapping", "tone-lab-button tone-lab-button-secondary", ToggleSongMappingMode);
        StyleFooterActionButton(songMappingButton, 174f);
        songMappingButton.style.marginRight = 0f;
        songMappingButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            songMappingButton.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.30f);
            songMappingButton.style.color = Color.white;
        });
        songMappingButton.RegisterCallback<MouseLeaveEvent>(_ => ApplySongMappingButtonState(boardMode == ToneLabBoardMode.SongMapping));
        footerLeftActions.Add(songMappingButton);

        VisualElement saveActions = new VisualElement();
        saveActions.style.flexDirection = FlexDirection.Row;
        saveActions.style.alignItems = Align.Center;
        saveActions.style.justifyContent = Justify.FlexEnd;
        boardFooter.Add(saveActions);

        resetAllButton = CreateButton("Reset All", "tone-lab-button tone-lab-button-danger", () => OpenPresetModal(ToneLabPresetModalMode.ResetAll));
        StyleFooterActionButton(resetAllButton, 138f);
        saveActions.Add(resetAllButton);

        savePresetButton = CreateButton("Save", "tone-lab-button tone-lab-button-primary", () =>
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.CurrentPresetId))
                return;

            runtime.SaveCurrentToPreset(runtime.CurrentPresetId);
            RefreshUi(syncControls: true);
            ShowActionToast($"Saved preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        });
        StyleFooterActionButton(savePresetButton, 116f);
        saveActions.Add(savePresetButton);

        saveAsPresetButton = CreateButton("Save As", "tone-lab-button tone-lab-button-secondary", () => OpenPresetModal(ToneLabPresetModalMode.SaveAs));
        StyleFooterActionButton(saveAsPresetButton, 134f);
        saveAsPresetButton.style.marginRight = 0f;
        saveActions.Add(saveAsPresetButton);
        RegisterFooterNavigationItems();

        presetModalScrim = new VisualElement();
        presetModalScrim.style.position = Position.Absolute;
        presetModalScrim.style.left = 0f;
        presetModalScrim.style.right = 0f;
        presetModalScrim.style.top = 0f;
        presetModalScrim.style.bottom = 0f;
        presetModalScrim.style.display = DisplayStyle.None;
        presetModalScrim.style.alignItems = Align.Center;
        presetModalScrim.style.justifyContent = Justify.Center;
        presetModalScrim.style.backgroundColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        presetModalScrim.RegisterCallback<MouseDownEvent>(_ => CloseCreatePresetModal());
        overlayRoot.Add(presetModalScrim);

        VisualElement presetModalCard = new VisualElement();
        presetModalCard.style.width = 420f;
        presetModalCard.style.paddingLeft = 22f;
        presetModalCard.style.paddingRight = 22f;
        presetModalCard.style.paddingTop = 20f;
        presetModalCard.style.paddingBottom = 18f;
        presetModalCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        presetModalCard.style.borderTopWidth = 1f;
        presetModalCard.style.borderRightWidth = 1f;
        presetModalCard.style.borderBottomWidth = 1f;
        presetModalCard.style.borderLeftWidth = 1f;
        presetModalCard.style.borderTopColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        presetModalCard.style.borderRightColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        presetModalCard.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        presetModalCard.style.borderLeftColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        presetModalCard.style.borderTopLeftRadius = 16f;
        presetModalCard.style.borderTopRightRadius = 16f;
        presetModalCard.style.borderBottomLeftRadius = 16f;
        presetModalCard.style.borderBottomRightRadius = 16f;
        presetModalCard.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        presetModalScrim.Add(presetModalCard);

        presetModalTitleLabel = new Label("Create Preset");
        presetModalTitleLabel.style.color = Color.white;
        presetModalTitleLabel.style.fontSize = 24f;
        presetModalTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        presetModalTitleLabel.style.marginBottom = 6f;
        presetModalCard.Add(presetModalTitleLabel);

        presetModalSubtitleLabel = new Label("Save the current pedalboard and gain staging as a reusable preset.");
        presetModalSubtitleLabel.style.color = new Color(0.66f, 0.69f, 0.73f, 0.96f);
        presetModalSubtitleLabel.style.fontSize = 13f;
        presetModalSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        presetModalSubtitleLabel.style.marginBottom = 10f;
        presetModalCard.Add(presetModalSubtitleLabel);

        presetNameSection = new VisualElement();
        presetNameSection.style.flexDirection = FlexDirection.Column;
        presetNameSection.style.display = DisplayStyle.Flex;
        presetModalCard.Add(presetNameSection);

        Label presetNameCaption = new Label("Preset Name");
        presetNameCaption.style.color = new Color(0.84f, 0.86f, 0.90f, 0.96f);
        presetNameCaption.style.fontSize = 13f;
        presetNameCaption.style.unityFontStyleAndWeight = FontStyle.Bold;
        presetNameCaption.style.marginBottom = 6f;
        presetNameSection.Add(presetNameCaption);

        presetNameField = new TextField();
        presetNameField.style.marginBottom = 16f;
        presetNameField.style.height = 42f;
        presetNameField.style.color = Color.white;
        presetNameField.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        presetNameField.style.borderTopWidth = 1f;
        presetNameField.style.borderRightWidth = 1f;
        presetNameField.style.borderBottomWidth = 1f;
        presetNameField.style.borderLeftWidth = 1f;
        presetNameField.style.borderTopColor = new Color(0.26f, 0.28f, 0.32f, 1f);
        presetNameField.style.borderRightColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        presetNameField.style.borderBottomColor = new Color(0.14f, 0.16f, 0.18f, 1f);
        presetNameField.style.borderLeftColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        presetNameField.style.borderTopLeftRadius = 10f;
        presetNameField.style.borderTopRightRadius = 10f;
        presetNameField.style.borderBottomLeftRadius = 10f;
        presetNameField.style.borderBottomRightRadius = 10f;
        presetNameField.RegisterCallback<AttachToPanelEvent>(_ => ApplyPresetNameFieldStyle());
        presetNameField.RegisterCallback<KeyDownEvent>(evt =>
        {
            if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
            {
                CommitCreatePreset();
                evt.StopPropagation();
            }
            else if (evt.keyCode == KeyCode.Escape)
            {
                CloseCreatePresetModal();
                evt.StopPropagation();
            }
        });
        presetNameSection.Add(presetNameField);

        VisualElement presetModalActions = new VisualElement();
        presetModalActions.style.flexDirection = FlexDirection.Row;
        presetModalActions.style.justifyContent = Justify.FlexEnd;
        presetModalActions.style.alignItems = Align.Center;
        presetModalCard.Add(presetModalActions);

        presetCancelButton = CreateButton("Cancel", "tone-lab-button tone-lab-button-secondary", CloseCreatePresetModal);
        presetCancelButton.style.minWidth = 110f;
        presetCancelButton.style.height = 40f;
        presetCancelButton.style.fontSize = 15f;
        presetModalActions.Add(presetCancelButton);

        presetCreateButton = CreateButton("Create", "tone-lab-button tone-lab-button-primary", CommitCreatePreset);
        presetCreateButton.style.minWidth = 118f;
        presetCreateButton.style.height = 40f;
        presetCreateButton.style.fontSize = 15f;
        presetCreateButton.style.marginRight = 0f;
        presetModalActions.Add(presetCreateButton);

        advancedAudioModalScrim = new VisualElement();
        advancedAudioModalScrim.style.position = Position.Absolute;
        advancedAudioModalScrim.style.left = 0f;
        advancedAudioModalScrim.style.right = 0f;
        advancedAudioModalScrim.style.top = 0f;
        advancedAudioModalScrim.style.bottom = 0f;
        advancedAudioModalScrim.style.display = DisplayStyle.None;
        advancedAudioModalScrim.style.alignItems = Align.Center;
        advancedAudioModalScrim.style.justifyContent = Justify.Center;
        advancedAudioModalScrim.style.backgroundColor = new Color(0.01f, 0.02f, 0.03f, 0.78f);
        advancedAudioModalScrim.RegisterCallback<MouseDownEvent>(_ => CloseAdvancedAudioModal());
        overlayRoot.Add(advancedAudioModalScrim);

        VisualElement advancedAudioCard = new VisualElement();
        advancedAudioCard.style.width = 760f;
        advancedAudioCard.style.maxWidth = 760f;
        advancedAudioCard.style.minHeight = 0f;
        advancedAudioCard.style.maxHeight = Length.Percent(84f);
        advancedAudioCard.style.paddingLeft = 22f;
        advancedAudioCard.style.paddingRight = 22f;
        advancedAudioCard.style.paddingTop = 20f;
        advancedAudioCard.style.paddingBottom = 18f;
        advancedAudioCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        advancedAudioCard.style.borderTopWidth = 1f;
        advancedAudioCard.style.borderRightWidth = 1f;
        advancedAudioCard.style.borderBottomWidth = 1f;
        advancedAudioCard.style.borderLeftWidth = 1f;
        advancedAudioCard.style.borderTopColor = new Color(0.24f, 0.26f, 0.30f, 1f);
        advancedAudioCard.style.borderRightColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        advancedAudioCard.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        advancedAudioCard.style.borderLeftColor = new Color(0.17f, 0.18f, 0.21f, 1f);
        advancedAudioCard.style.borderTopLeftRadius = 16f;
        advancedAudioCard.style.borderTopRightRadius = 16f;
        advancedAudioCard.style.borderBottomLeftRadius = 16f;
        advancedAudioCard.style.borderBottomRightRadius = 16f;
        advancedAudioCard.style.flexDirection = FlexDirection.Column;
        advancedAudioCard.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        advancedAudioModalScrim.Add(advancedAudioCard);

        Label advancedAudioTitle = new Label("Advanced Audio");
        advancedAudioTitle.style.color = Color.white;
        advancedAudioTitle.style.fontSize = 24f;
        advancedAudioTitle.style.unityFontStyleAndWeight = FontStyle.Bold;
        advancedAudioTitle.style.marginBottom = 6f;
        advancedAudioCard.Add(advancedAudioTitle);

        Label advancedAudioSubtitle = new Label("Input Channel is shared by Tone Lab and Notes Detector. Beta Mode unlocks backend routing. Applying these settings restarts affected audio paths.");
        advancedAudioSubtitle.style.color = new Color(0.66f, 0.69f, 0.73f, 0.96f);
        advancedAudioSubtitle.style.fontSize = 13f;
        advancedAudioSubtitle.style.whiteSpace = WhiteSpace.Normal;
        advancedAudioSubtitle.style.marginBottom = 14f;
        advancedAudioCard.Add(advancedAudioSubtitle);

        ScrollView advancedAudioScroll = new ScrollView(ScrollViewMode.Vertical);
        advancedAudioScroll.style.flexGrow = 1f;
        advancedAudioScroll.style.minHeight = 0f;
        advancedAudioScroll.verticalScrollerVisibility = ScrollerVisibility.Auto;
        advancedAudioScroll.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        advancedAudioCard.Add(advancedAudioScroll);

        VisualElement advancedAudioHost = advancedAudioScroll.contentContainer;
        advancedAudioHost.style.flexDirection = FlexDirection.Column;
        advancedAudioHost.style.paddingRight = 6f;

        advancedBetaToggleButton = CreateButton("OFF", "tone-lab-toggle", ToggleAdvancedBetaDraft);
        advancedFallbackToggleButton = CreateButton("OFF", "tone-lab-toggle", ToggleAdvancedFallbackDraft);
        advancedUnifiedToggleButton = CreateButton("OFF", "tone-lab-toggle", ToggleAdvancedUnifiedDraft);
        advancedRecorderCaptureToggleButton = CreateButton("OFF", "tone-lab-toggle", ToggleAdvancedRecorderCaptureDraft);

        advancedInputChannelDropdown = new DropdownField();
        ApplyDropdownStyle(advancedInputChannelDropdown);
        advancedInputChannelDropdown.style.minWidth = 220f;
        advancedInputChannelDropdown.style.width = 220f;
        advancedInputChannelDropdown.choices = SharedAudioInputChannelModes.Choices.ToList();
        advancedInputChannelDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            advancedAudioDraft.inputChannelMode = SharedAudioInputChannelModes.Normalize(evt.newValue);
        });

        advancedBackendDropdown = new DropdownField();
        ApplyDropdownStyle(advancedBackendDropdown);
        advancedBackendDropdown.style.minWidth = 300f;
        advancedBackendDropdown.style.width = 300f;
        advancedBackendDropdown.choices = SharedAudioBackendModes.GetChoicesForCurrentPlatform();
        advancedBackendDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            advancedAudioDraft.backendMode = SharedAudioBackendModes.NormalizeForCurrentPlatform(evt.newValue);
            RefreshAdvancedAudioDeviceChoices();
        });

        advancedInputDropdown = new DropdownField();
        ApplyDropdownStyle(advancedInputDropdown);
        advancedInputDropdown.style.minWidth = 440f;
        advancedInputDropdown.style.width = 440f;
        advancedInputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;
            advancedAudioDraft.inputDeviceName = NormalizeAdvancedPopupSelection(evt.newValue);
        });

        advancedOutputDropdown = new DropdownField();
        ApplyDropdownStyle(advancedOutputDropdown);
        advancedOutputDropdown.style.minWidth = 440f;
        advancedOutputDropdown.style.width = 440f;
        advancedOutputDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;
            advancedAudioDraft.outputDeviceName = NormalizeAdvancedPopupSelection(evt.newValue);
        });

        advancedSampleRateDropdown = new DropdownField();
        ApplyDropdownStyle(advancedSampleRateDropdown);
        advancedSampleRateDropdown.style.minWidth = 220f;
        advancedSampleRateDropdown.style.width = 220f;
        advancedSampleRateDropdown.choices = new List<string> { "Auto", "44100 Hz", "48000 Hz", "96000 Hz" };
        advancedSampleRateDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;
            advancedAudioDraft.sampleRate = ParseAdvancedSampleRateLabel(evt.newValue);
        });

        advancedBufferDropdown = new DropdownField();
        ApplyDropdownStyle(advancedBufferDropdown);
        advancedBufferDropdown.style.minWidth = 220f;
        advancedBufferDropdown.style.width = 220f;
        advancedBufferDropdown.choices = new List<string> { "64 Samples", "128 Samples", "256 Samples" };
        advancedBufferDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;
            advancedAudioDraft.bufferSize = ParseAdvancedBufferLabel(evt.newValue);
        });

        advancedAudioHost.Add(CreateSettingRow("Input Channel", out VisualElement advancedInputChannelHost));
        advancedInputChannelHost.Add(advancedInputChannelDropdown);
        advancedAudioHost.Add(CreateSettingRow("Unity Recorder Capture", out VisualElement advancedRecorderCaptureHost));
        advancedRecorderCaptureHost.Add(advancedRecorderCaptureToggleButton);
        advancedAudioHost.Add(CreateSectionDivider());
        advancedAudioHost.Add(CreateSettingRow("Beta Mode", out VisualElement advancedBetaHost));
        advancedBetaHost.Add(advancedBetaToggleButton);
        advancedAudioHost.Add(CreateSettingRow("Backend", out VisualElement advancedBackendHost));
        advancedBackendHost.Add(advancedBackendDropdown);
        advancedAudioHost.Add(CreateSettingRow("Input Device", out VisualElement advancedInputHost));
        advancedInputHost.Add(advancedInputDropdown);
        advancedAudioHost.Add(CreateSettingRow("Output Device", out VisualElement advancedOutputHost));
        advancedOutputHost.Add(advancedOutputDropdown);
        advancedAudioHost.Add(CreateSettingRow("Sample Rate", out VisualElement advancedSampleRateHost));
        advancedSampleRateHost.Add(advancedSampleRateDropdown);
        advancedAudioHost.Add(CreateSettingRow("Buffer", out VisualElement advancedBufferHost));
        advancedBufferHost.Add(advancedBufferDropdown);
        advancedAudioHost.Add(CreateSettingRow("Allow Fallback", out VisualElement advancedFallbackHost));
        advancedFallbackHost.Add(advancedFallbackToggleButton);
        advancedAudioHost.Add(CreateSettingRow("Unified Output", out VisualElement advancedUnifiedHost));
        advancedUnifiedHost.Add(advancedUnifiedToggleButton);
        VisualElement advancedStatusCard = new VisualElement();
        advancedStatusCard.style.marginTop = 14f;
        advancedStatusCard.style.paddingLeft = 14f;
        advancedStatusCard.style.paddingRight = 14f;
        advancedStatusCard.style.paddingTop = 12f;
        advancedStatusCard.style.paddingBottom = 12f;
        advancedStatusCard.style.backgroundColor = new Color(0.07f, 0.08f, 0.10f, 0.72f);
        advancedStatusCard.style.borderTopWidth = 1f;
        advancedStatusCard.style.borderRightWidth = 1f;
        advancedStatusCard.style.borderBottomWidth = 1f;
        advancedStatusCard.style.borderLeftWidth = 1f;
        advancedStatusCard.style.borderTopColor = new Color(1f, 1f, 1f, 0.12f);
        advancedStatusCard.style.borderRightColor = new Color(1f, 1f, 1f, 0.12f);
        advancedStatusCard.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        advancedStatusCard.style.borderLeftColor = new Color(1f, 1f, 1f, 0.12f);
        advancedStatusCard.style.borderTopLeftRadius = 10f;
        advancedStatusCard.style.borderTopRightRadius = 10f;
        advancedStatusCard.style.borderBottomLeftRadius = 10f;
        advancedStatusCard.style.borderBottomRightRadius = 10f;
        advancedAudioHost.Add(advancedStatusCard);

        Label runtimeLogsLabel = new Label("Runtime Logs");
        runtimeLogsLabel.style.color = new Color(0.95f, 0.96f, 0.98f, 0.96f);
        runtimeLogsLabel.style.fontSize = 13f;
        runtimeLogsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        runtimeLogsLabel.style.marginBottom = 8f;
        advancedStatusCard.Add(runtimeLogsLabel);

        backendLabel = new Label(string.Empty);
        backendLabel.style.color = new Color(0.82f, 0.86f, 0.91f, 0.96f);
        backendLabel.style.fontSize = 11f;
        backendLabel.style.marginBottom = 4f;
        backendLabel.style.whiteSpace = WhiteSpace.Normal;
        advancedStatusCard.Add(backendLabel);

        routeLabel = new Label(string.Empty);
        routeLabel.style.color = new Color(0.90f, 0.94f, 0.98f, 0.98f);
        routeLabel.style.fontSize = 11f;
        routeLabel.style.marginBottom = 4f;
        routeLabel.style.whiteSpace = WhiteSpace.Normal;
        advancedStatusCard.Add(routeLabel);

        statusLabel = new Label(string.Empty);
        statusLabel.style.color = new Color(0.92f, 0.84f, 0.63f, 1f);
        statusLabel.style.fontSize = 12f;
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        advancedStatusCard.Add(statusLabel);

        VisualElement runtimeLogsDivider = new VisualElement();
        runtimeLogsDivider.style.height = 1f;
        runtimeLogsDivider.style.marginTop = 12f;
        runtimeLogsDivider.style.marginBottom = 12f;
        runtimeLogsDivider.style.backgroundColor = new Color(1f, 1f, 1f, 0.12f);
        advancedStatusCard.Add(runtimeLogsDivider);

        advancedAudioStatusLabel = new Label(string.Empty);
        advancedAudioStatusLabel.style.color = new Color(0.90f, 0.94f, 0.98f, 0.98f);
        advancedAudioStatusLabel.style.fontSize = 12f;
        advancedAudioStatusLabel.style.whiteSpace = WhiteSpace.Normal;
        advancedAudioStatusLabel.style.marginBottom = 8f;
        advancedStatusCard.Add(advancedAudioStatusLabel);

        advancedAudioDiagnosticsLabel = new Label(string.Empty);
        advancedAudioDiagnosticsLabel.style.color = new Color(0.76f, 0.80f, 0.86f, 0.96f);
        advancedAudioDiagnosticsLabel.style.fontSize = 11f;
        advancedAudioDiagnosticsLabel.style.whiteSpace = WhiteSpace.Normal;
        advancedStatusCard.Add(advancedAudioDiagnosticsLabel);

        VisualElement advancedAudioActions = new VisualElement();
        advancedAudioActions.style.flexDirection = FlexDirection.Row;
        advancedAudioActions.style.justifyContent = Justify.FlexEnd;
        advancedAudioActions.style.alignItems = Align.Center;
        advancedAudioActions.style.marginTop = 14f;
        advancedAudioCard.Add(advancedAudioActions);

        advancedAudioCloseButton = CreateButton("Close", "tone-lab-button tone-lab-button-secondary", CloseAdvancedAudioModal);
        advancedAudioCloseButton.style.minWidth = 110f;
        advancedAudioCloseButton.style.height = 40f;
        advancedAudioCloseButton.style.fontSize = 15f;
        advancedAudioActions.Add(advancedAudioCloseButton);

        advancedAudioApplyButton = CreateButton("Apply", "tone-lab-button tone-lab-button-primary", CommitAdvancedAudioSettings);
        advancedAudioApplyButton.style.minWidth = 118f;
        advancedAudioApplyButton.style.height = 40f;
        advancedAudioApplyButton.style.fontSize = 15f;
        advancedAudioApplyButton.style.marginRight = 0f;
        advancedAudioActions.Add(advancedAudioApplyButton);

        BuildUnsavedChangesModal();

        actionToast = new VisualElement();
        actionToast.style.position = Position.Absolute;
        actionToast.style.right = 26f;
        actionToast.style.bottom = 78f;
        actionToast.style.display = DisplayStyle.None;
        actionToast.style.paddingLeft = 14f;
        actionToast.style.paddingRight = 14f;
        actionToast.style.paddingTop = 10f;
        actionToast.style.paddingBottom = 10f;
        actionToast.style.backgroundColor = new Color(0.15f, 0.18f, 0.20f, 0.98f);
        actionToast.style.borderTopLeftRadius = 12f;
        actionToast.style.borderTopRightRadius = 12f;
        actionToast.style.borderBottomLeftRadius = 12f;
        actionToast.style.borderBottomRightRadius = 12f;
        actionToast.style.borderTopWidth = 1f;
        actionToast.style.borderRightWidth = 1f;
        actionToast.style.borderBottomWidth = 1f;
        actionToast.style.borderLeftWidth = 1f;
        actionToast.style.borderTopColor = new Color(0.34f, 0.37f, 0.42f, 0.95f);
        actionToast.style.borderRightColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.borderBottomColor = new Color(0.18f, 0.20f, 0.23f, 0.98f);
        actionToast.style.borderLeftColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        overlayRoot.Add(actionToast);

        actionToastLabel = new Label(string.Empty);
        actionToastLabel.style.color = Color.white;
        actionToastLabel.style.fontSize = 14f;
        actionToastLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        actionToast.Add(actionToastLabel);

        openingLoadingOverlay = ReusableLoadingOverlay.CreateStringTheoryLibraryLoadingOverlay(overlayRoot);
        BuildOpeningLoadingContent(openingLoadingOverlay.ContentHost, toneLabTitleFontDefinition);
        BuildControllerCursor(overlayRoot);

        root.Add(overlayRoot);
        root.pickingMode = PickingMode.Ignore;
        overlayRoot.pickingMode = PickingMode.Position;
    }

    private void BuildOpeningLoadingContent(VisualElement contentHost, FontDefinition titleFont)
    {
        if (contentHost == null)
            return;

        contentHost.style.flexDirection = FlexDirection.Column;
        contentHost.style.alignItems = Align.Center;
        contentHost.style.justifyContent = Justify.Center;

        Label titleLabel = new Label("Tone Lab");
        titleLabel.style.unityFontDefinition = titleFont;
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 54f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Normal;
        titleLabel.style.marginBottom = 14f;
        contentHost.Add(titleLabel);

        Label subtitleLabel = new Label("Loading effects");
        subtitleLabel.style.color = new Color(0.84f, 0.88f, 0.94f, 0.96f);
        subtitleLabel.style.fontSize = 18f;
        subtitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        subtitleLabel.style.letterSpacing = 0f;
        contentHost.Add(subtitleLabel);
    }

    private void BuildControllerCursor(VisualElement root)
    {
        if (root == null)
            return;

        controllerCursor = new VisualElement();
        controllerCursor.style.position = Position.Absolute;
        controllerCursor.style.width = 40f;
        controllerCursor.style.height = 40f;
        controllerCursor.style.borderTopWidth = 3f;
        controllerCursor.style.borderRightWidth = 3f;
        controllerCursor.style.borderBottomWidth = 3f;
        controllerCursor.style.borderLeftWidth = 3f;
        controllerCursor.style.borderTopColor = new Color(1f, 1f, 1f, 0.96f);
        controllerCursor.style.borderRightColor = new Color(1f, 1f, 1f, 0.96f);
        controllerCursor.style.borderBottomColor = new Color(1f, 1f, 1f, 0.96f);
        controllerCursor.style.borderLeftColor = new Color(1f, 1f, 1f, 0.96f);
        controllerCursor.style.borderTopLeftRadius = 999f;
        controllerCursor.style.borderTopRightRadius = 999f;
        controllerCursor.style.borderBottomLeftRadius = 999f;
        controllerCursor.style.borderBottomRightRadius = 999f;
        controllerCursor.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
        controllerCursor.style.opacity = 0f;
        controllerCursor.style.display = DisplayStyle.None;
        controllerCursor.style.translate = new Translate(-20f, -20f, 0f);
        controllerCursor.pickingMode = PickingMode.Ignore;

        controllerCursorInner = new VisualElement();
        controllerCursorInner.style.position = Position.Absolute;
        controllerCursorInner.style.left = new Length(50f, LengthUnit.Percent);
        controllerCursorInner.style.top = new Length(50f, LengthUnit.Percent);
        controllerCursorInner.style.width = 12f;
        controllerCursorInner.style.height = 12f;
        controllerCursorInner.style.translate = new Translate(-6f, -6f, 0f);
        controllerCursorInner.style.borderTopLeftRadius = 999f;
        controllerCursorInner.style.borderTopRightRadius = 999f;
        controllerCursorInner.style.borderBottomLeftRadius = 999f;
        controllerCursorInner.style.borderBottomRightRadius = 999f;
        controllerCursorInner.style.backgroundColor = new Color(1f, 1f, 1f, 0.98f);
        controllerCursorInner.pickingMode = PickingMode.Ignore;
        controllerCursor.Add(controllerCursorInner);

        root.Add(controllerCursor);
    }

    private bool UpdateControllerCursor()
    {
        if (controllerCursor == null || overlayRoot?.panel == null)
            return false;
        if (openingRefreshInProgress)
            return false;

        Vector2 panelSize = ResolveControllerCursorPanelSize();
        if (!controllerCursorInitialized || !IsFiniteVector2(controllerCursorPanelPosition))
        {
            controllerCursorPanelPosition = panelSize * 0.5f;
            lastPhysicalMousePosition = Input.mousePosition;
            controllerCursorInitialized = true;
        }
        else
        {
            controllerCursorPanelPosition = SanitizeControllerCursorPosition(controllerCursorPanelPosition, panelSize);
        }

        Vector3 currentMousePosition = Input.mousePosition;
        bool mouseMoved = (currentMousePosition - lastPhysicalMousePosition).sqrMagnitude > 1f;
        bool mouseClicked = Input.GetMouseButtonDown(0) || Input.GetMouseButtonDown(1) || Input.GetMouseButtonDown(2);
        lastPhysicalMousePosition = currentMousePosition;
        if (mouseMoved || mouseClicked)
        {
            controllerCursorActive = false;
            controllerCursorPointerMode = false;
        }

        float axisX = ReadControllerCursorHorizontalAxis();
        float axisY = ReadControllerCursorVerticalAxis();
        Vector2 movement = new Vector2(axisX, -axisY);
        bool controllerMovementDetected = movement.sqrMagnitude >= 0.04f;
        bool controllerButtonPressed = WasAnyControllerUiButtonPressedThisFrame();
        bool primaryPressed = WasControllerPrimaryActionPressedThisFrame();

        if (!controllerCursorActive && (controllerMovementDetected || controllerButtonPressed))
        {
            controllerCursorActive = true;
            ClearNativeUiFocusForControllerCursor();
            controllerCursorPanelPosition = SanitizeControllerCursorPosition(controllerCursorPanelPosition, panelSize);
        }

        if (!controllerCursorActive)
        {
            controllerCursor.style.display = DisplayStyle.None;
            controllerCursor.style.opacity = 0f;
            return false;
        }

        controllerCursor.style.display = DisplayStyle.Flex;
        controllerCursor.style.opacity = 1f;
        controllerCursor.BringToFront();

        if (movement.sqrMagnitude > 0.0001f)
        {
            controllerCursorPointerMode = true;
            float speed = 2200f;
            if (movement.sqrMagnitude > 1f)
                movement.Normalize();
            controllerCursorPanelPosition += movement * speed * Time.unscaledDeltaTime;
        }

        controllerCursorPanelPosition = SanitizeControllerCursorPosition(controllerCursorPanelPosition, panelSize);
        PositionControllerCursorVisual();

        VisualElement pickedTarget = PickControllerCursorTarget();
        if (pickedTarget != null && pickedTarget != lastControllerCursorTarget && movement.sqrMagnitude > 0.0001f)
            DispatchControllerCursorMove(pickedTarget);
        lastControllerCursorTarget = pickedTarget;

        if (!primaryPressed)
            return false;

        ClearNativeUiFocusForControllerCursor();
        if (controllerCursorPointerMode &&
            TryFindControllerCursorActionTarget(pickedTarget, out VisualElement actionTarget))
        {
            DispatchControllerCursorClick(actionTarget);
            return true;
        }

        if (IsCapturingKeyboardInput)
            return true;

        ActivateCurrentNavigationItem();
        return true;
    }

    private void HideControllerCursor()
    {
        controllerCursorActive = false;
        controllerCursorPointerMode = false;
        lastControllerCursorTarget = null;
        if (controllerCursor != null)
        {
            controllerCursor.style.display = DisplayStyle.None;
            controllerCursor.style.opacity = 0f;
        }
    }

    private void PositionControllerCursorVisual()
    {
        controllerCursorPanelPosition = SanitizeControllerCursorPosition(controllerCursorPanelPosition, ResolveControllerCursorPanelSize());
        controllerCursor.style.left = controllerCursorPanelPosition.x;
        controllerCursor.style.top = controllerCursorPanelPosition.y;
    }

    private void ClearNativeUiFocusForControllerCursor()
    {
        FocusController focusController = overlayRoot?.panel?.focusController;
        if (focusController?.focusedElement is Focusable focusedElement)
            focusedElement.Blur();
    }

    private Vector2 ResolveControllerCursorPanelSize()
    {
        float panelWidth = overlayRoot?.resolvedStyle.width ?? float.NaN;
        float panelHeight = overlayRoot?.resolvedStyle.height ?? float.NaN;
        if (!IsFiniteFloat(panelWidth) || panelWidth < 8f)
            panelWidth = Screen.width;
        if (!IsFiniteFloat(panelHeight) || panelHeight < 8f)
            panelHeight = Screen.height;
        if (!IsFiniteFloat(panelWidth) || panelWidth < 8f)
            panelWidth = 1920f;
        if (!IsFiniteFloat(panelHeight) || panelHeight < 8f)
            panelHeight = 1080f;

        return new Vector2(Mathf.Max(1f, panelWidth), Mathf.Max(1f, panelHeight));
    }

    private static Vector2 SanitizeControllerCursorPosition(Vector2 position, Vector2 panelSize)
    {
        Vector2 fallback = panelSize * 0.5f;
        if (!IsFiniteVector2(position))
            return fallback;

        float minX = 8f;
        float minY = 8f;
        float maxX = Mathf.Max(minX, panelSize.x - 8f);
        float maxY = Mathf.Max(minY, panelSize.y - 8f);
        position.x = Mathf.Clamp(position.x, minX, maxX);
        position.y = Mathf.Clamp(position.y, minY, maxY);

        return IsFiniteVector2(position) ? position : fallback;
    }

    private static bool IsFiniteVector2(Vector2 value)
    {
        return IsFiniteFloat(value.x) && IsFiniteFloat(value.y);
    }

    private static bool IsFiniteFloat(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    private VisualElement PickControllerCursorTarget()
    {
        IPanel panel = overlayRoot?.panel;
        if (panel == null)
            return null;

        VisualElement picked = panel.Pick(controllerCursorPanelPosition);
        if (picked == controllerCursor || picked == controllerCursorInner)
            return null;

        return picked;
    }

    private bool TryFindControllerCursorActionTarget(VisualElement target, out VisualElement actionTarget)
    {
        actionTarget = null;
        for (VisualElement current = target; current != null && current != overlayRoot; current = current.parent)
        {
            if (current == controllerCursor || current == controllerCursorInner || current == blurBackdrop)
                continue;

            if (current is Button ||
                current is DropdownField ||
                current is Slider ||
                current is TextField ||
                current is Toggle ||
                current is ToneLabPedalLibraryItem ||
                current is ToneLabPedalTile)
            {
                actionTarget = current;
                return true;
            }
        }

        return false;
    }

    private void DispatchControllerCursorMove(VisualElement target)
    {
        if (target == null)
            return;

        Event systemEvent = CreateControllerCursorMouseEvent(EventType.MouseMove, 0, 0);
        using (PointerMoveEvent pointerMove = PointerMoveEvent.GetPooled(systemEvent))
        {
            target.SendEvent(pointerMove);
        }
    }

    private void DispatchControllerCursorClick(VisualElement target)
    {
        if (target == null)
            return;

        DispatchControllerCursorMove(target);

        Event downEvent = CreateControllerCursorMouseEvent(EventType.MouseDown, 0, 1);
        using (PointerDownEvent pointerDown = PointerDownEvent.GetPooled(downEvent))
        {
            target.SendEvent(pointerDown);
        }

        Event upEvent = CreateControllerCursorMouseEvent(EventType.MouseUp, 0, 1);
        using (PointerUpEvent pointerUp = PointerUpEvent.GetPooled(upEvent))
        {
            target.SendEvent(pointerUp);
        }
    }

    private Event CreateControllerCursorMouseEvent(EventType eventType, int button, int clickCount)
    {
        return new Event
        {
            type = eventType,
            mousePosition = controllerCursorPanelPosition,
            button = button,
            clickCount = clickCount
        };
    }

    private static float ReadControllerCursorHorizontalAxis()
    {
        float axis = ReadControllerCursorInputSystemAxis().x;
        if (Mathf.Abs(axis) < 0.001f)
            axis = TryGetAxisRaw("JoystickHorizontal");

        return Mathf.Abs(axis) < 0.18f ? 0f : Mathf.Clamp(axis, -1f, 1f);
    }

    private static float ReadControllerCursorVerticalAxis()
    {
        float axis = ReadControllerCursorInputSystemAxis().y;
        if (Mathf.Abs(axis) < 0.001f)
            axis = TryGetAxisRaw("JoystickVertical");

        return Mathf.Abs(axis) < 0.18f ? 0f : Mathf.Clamp(axis, -1f, 1f);
    }

    private static bool WasAnyControllerUiButtonPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            if (gamepad.buttonSouth.wasPressedThisFrame ||
                gamepad.buttonEast.wasPressedThisFrame ||
                gamepad.buttonNorth.wasPressedThisFrame ||
                gamepad.buttonWest.wasPressedThisFrame ||
                gamepad.startButton.wasPressedThisFrame ||
                gamepad.selectButton.wasPressedThisFrame ||
                gamepad.dpad.up.wasPressedThisFrame ||
                gamepad.dpad.down.wasPressedThisFrame ||
                gamepad.dpad.left.wasPressedThisFrame ||
                gamepad.dpad.right.wasPressedThisFrame)
            {
                return true;
            }
        }
#endif

        if (HasInputSystemGamepadConnected())
            return false;

        for (int buttonIndex = 0; buttonIndex <= 19; buttonIndex++)
        {
            KeyCode key = (KeyCode)((int)KeyCode.JoystickButton0 + buttonIndex);
            if (Input.GetKeyDown(key))
                return true;
        }

        return false;
    }

    private static bool WasControllerPrimaryActionPressedThisFrame()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad?.buttonSouth.wasPressedThisFrame == true)
                return true;
        }
#endif
        if (HasInputSystemGamepadConnected())
            return false;

        return TryGetButtonDown("Submit") || Input.GetKeyDown(KeyCode.JoystickButton0);
    }

    private static Vector2 ReadControllerCursorInputSystemAxis()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 strongest = Vector2.zero;
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            Vector2 candidate = gamepad.leftStick.ReadValue();
            if (candidate.sqrMagnitude > strongest.sqrMagnitude)
                strongest = candidate;
        }

        foreach (Joystick joystick in Joystick.all)
        {
            if (joystick == null)
                continue;

            Vector2 candidate = joystick.stick.ReadValue();
            if (candidate.sqrMagnitude > strongest.sqrMagnitude)
                strongest = candidate;
        }

        return Vector2.ClampMagnitude(strongest, 1f);
#else
        return Vector2.zero;
#endif
    }

    private void BeginOpeningRefresh()
    {
        boardMode = ToneLabBoardMode.Pedalboard;
        sidePanelMode = ToneLabSidePanelMode.Presets;
        ResetNavigation(ToneLabNavigationZone.Sidebar);
        ClearPendingToneMappingAssignment();
        CancelOpeningRefresh();
        openingRefreshInProgress = true;
        SetOpeningLoadingVisible(true);
        openRefreshRoutine = StartCoroutine(DeferredOpeningRefresh());
    }

    private IEnumerator DeferredOpeningRefresh()
    {
        yield return null;
        yield return null;

        runtime?.RefreshExternalPedalLibrary();
        RefreshUi(syncControls: true, refreshDevices: false);
        openingRefreshInProgress = false;
        SetOpeningLoadingVisible(false);
        openRefreshRoutine = null;
    }

    private void CancelOpeningRefresh()
    {
        if (openRefreshRoutine != null)
        {
            StopCoroutine(openRefreshRoutine);
            openRefreshRoutine = null;
        }

        openingRefreshInProgress = false;
        SetOpeningLoadingVisible(false);
    }

    private void SetOpeningLoadingVisible(bool visible)
    {
        openingLoadingOverlay?.SetVisible(visible, Time.unscaledTime);
        if (visible)
            openingLoadingOverlay?.RootElement.BringToFront();
    }

    private void UpdateOpeningLoadingOverlay()
    {
        if (openingRefreshInProgress)
            SetOpeningLoadingVisible(true);
    }

    private void BuildUnsavedChangesModal()
    {
        if (overlayRoot == null)
            return;

        unsavedChangesModalScrim = new VisualElement();
        unsavedChangesModalScrim.style.position = Position.Absolute;
        unsavedChangesModalScrim.style.left = 0f;
        unsavedChangesModalScrim.style.right = 0f;
        unsavedChangesModalScrim.style.top = 0f;
        unsavedChangesModalScrim.style.bottom = 0f;
        unsavedChangesModalScrim.style.display = DisplayStyle.None;
        unsavedChangesModalScrim.style.alignItems = Align.Center;
        unsavedChangesModalScrim.style.justifyContent = Justify.Center;
        unsavedChangesModalScrim.style.backgroundColor = new Color(0.01f, 0.02f, 0.03f, 0.80f);
        overlayRoot.Add(unsavedChangesModalScrim);

        VisualElement modalCard = new VisualElement();
        modalCard.style.width = 500f;
        modalCard.style.paddingLeft = 24f;
        modalCard.style.paddingRight = 24f;
        modalCard.style.paddingTop = 22f;
        modalCard.style.paddingBottom = 20f;
        modalCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
        modalCard.style.borderTopWidth = 1f;
        modalCard.style.borderRightWidth = 1f;
        modalCard.style.borderBottomWidth = 1f;
        modalCard.style.borderLeftWidth = 1f;
        modalCard.style.borderTopColor = new Color(0.32f, 0.34f, 0.38f, 1f);
        modalCard.style.borderRightColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        modalCard.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        modalCard.style.borderLeftColor = new Color(0.18f, 0.20f, 0.23f, 1f);
        modalCard.style.borderTopLeftRadius = 16f;
        modalCard.style.borderTopRightRadius = 16f;
        modalCard.style.borderBottomLeftRadius = 16f;
        modalCard.style.borderBottomRightRadius = 16f;
        modalCard.RegisterCallback<MouseDownEvent>(evt => evt.StopPropagation());
        unsavedChangesModalScrim.Add(modalCard);

        Label titleLabel = new Label("Unsaved Changes");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 25f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 8f;
        modalCard.Add(titleLabel);

        unsavedChangesPresetLabel = new Label("you have unsaved changes in preset \"Preset\"");
        unsavedChangesPresetLabel.style.color = new Color(0.90f, 0.92f, 0.96f, 0.96f);
        unsavedChangesPresetLabel.style.fontSize = 17f;
        unsavedChangesPresetLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        unsavedChangesPresetLabel.style.whiteSpace = WhiteSpace.Normal;
        unsavedChangesPresetLabel.style.marginBottom = 10f;
        modalCard.Add(unsavedChangesPresetLabel);

        Label bodyLabel = new Label("Save the current pedalboard before continuing, or discard the changes and keep the saved preset as-is.");
        bodyLabel.style.color = new Color(0.66f, 0.69f, 0.73f, 0.96f);
        bodyLabel.style.fontSize = 13f;
        bodyLabel.style.whiteSpace = WhiteSpace.Normal;
        bodyLabel.style.marginBottom = 18f;
        modalCard.Add(bodyLabel);

        VisualElement separator = new VisualElement();
        separator.style.height = 1f;
        separator.style.backgroundColor = new Color(1f, 1f, 1f, 0.14f);
        separator.style.marginBottom = 18f;
        modalCard.Add(separator);

        VisualElement modalActions = new VisualElement();
        modalActions.style.flexDirection = FlexDirection.Row;
        modalActions.style.justifyContent = Justify.FlexEnd;
        modalActions.style.alignItems = Align.Center;
        modalCard.Add(modalActions);

        unsavedChangesDiscardButton = CreateButton("Discard", "tone-lab-button tone-lab-button-danger", DiscardUnsavedChangesAndContinue);
        StyleUnsavedModalButton(unsavedChangesDiscardButton, 126f, isDanger: true);
        modalActions.Add(unsavedChangesDiscardButton);

        unsavedChangesSaveButton = CreateButton("Save", "tone-lab-button tone-lab-button-primary", SaveUnsavedChangesAndContinue);
        StyleUnsavedModalButton(unsavedChangesSaveButton, 118f, isDanger: false);
        unsavedChangesSaveButton.style.marginRight = 0f;
        modalActions.Add(unsavedChangesSaveButton);
    }

    private void OpenPresetModal(ToneLabPresetModalMode mode)
    {
        if (presetModalScrim == null)
            return;

        presetModalMode = mode;
        bool isSaveAs = mode == ToneLabPresetModalMode.SaveAs;
        bool isSaveGeneratedTone = mode == ToneLabPresetModalMode.SaveGeneratedTone;
        bool isResetAll = mode == ToneLabPresetModalMode.ResetAll;
        if (presetModalTitleLabel != null)
            presetModalTitleLabel.text = isResetAll
                ? "Reset All"
                : (isSaveGeneratedTone ? "Save Generated Tone" : (isSaveAs ? "Save Preset As" : "Create Preset"));
        if (presetModalSubtitleLabel != null)
            presetModalSubtitleLabel.text = isResetAll
                ? "Restore the factory preset library and the active rig. Audio device routing and latency stay as they are."
                : (isSaveGeneratedTone
                    ? "Save this automatic song tone as a reusable preset and assign it to the selected Rocksmith tone."
                    : (isSaveAs
                    ? "Save the current pedalboard as a new custom preset without overwriting the active one."
                    : "Save the current pedalboard and gain staging as a reusable preset."));
        if (presetCreateButton != null)
        {
            presetCreateButton.text = isResetAll ? "Reset All" : (isSaveGeneratedTone ? "Save" : (isSaveAs ? "Save As" : "Create"));
            ApplyModalActionButtonStyle(presetCreateButton, isResetAll);
        }
        if (presetNameSection != null)
            presetNameSection.style.display = isResetAll ? DisplayStyle.None : DisplayStyle.Flex;
        presetNameField?.SetValueWithoutNotify(isSaveGeneratedTone ? pendingGeneratedPresetDefaultName : string.Empty);
        presetModalScrim.style.display = DisplayStyle.Flex;
        ApplyPresetNameFieldStyle();
        if (!isResetAll)
            presetNameField?.Focus();
    }

    private void CloseCreatePresetModal()
    {
        if (presetModalScrim == null)
            return;

        presetModalScrim.style.display = DisplayStyle.None;
        ClearPendingGeneratedTonePresetSave();
    }

    private void OpenUnsavedChangesModal(ToneLabUnsavedAction action, string presetId)
    {
        if (unsavedChangesModalScrim == null)
            return;

        pendingUnsavedAction = action;
        pendingUnsavedPresetId = presetId ?? string.Empty;
        string currentPresetName = GetPresetName(runtime?.CurrentPresetId);
        if (unsavedChangesPresetLabel != null)
            unsavedChangesPresetLabel.text = $"you have unsaved changes in preset \"{currentPresetName}\"";

        unsavedChangesModalScrim.style.display = DisplayStyle.Flex;
        unsavedChangesModalScrim.BringToFront();
    }

    private void CloseUnsavedChangesModal()
    {
        if (unsavedChangesModalScrim != null)
            unsavedChangesModalScrim.style.display = DisplayStyle.None;

        pendingUnsavedAction = ToneLabUnsavedAction.None;
        pendingUnsavedPresetId = string.Empty;
    }

    private void SaveUnsavedChangesAndContinue()
    {
        if (runtime != null && !string.IsNullOrWhiteSpace(runtime.CurrentPresetId))
        {
            string savedPresetId = runtime.CurrentPresetId;
            runtime.SaveCurrentToPreset(savedPresetId);
            ShowActionToast($"Saved preset \"{GetPresetName(savedPresetId)}\".");
        }

        ContinuePendingUnsavedAction();
    }

    private void DiscardUnsavedChangesAndContinue()
    {
        ContinuePendingUnsavedAction();
    }

    private void ContinuePendingUnsavedAction()
    {
        ToneLabUnsavedAction action = pendingUnsavedAction;
        string presetId = pendingUnsavedPresetId;
        CloseUnsavedChangesModal();

        switch (action)
        {
            case ToneLabUnsavedAction.SelectPreset:
                SelectPresetNow(presetId);
                break;
            case ToneLabUnsavedAction.CloseToneLab:
                CloseToneLabNow();
                break;
        }
    }

    private void RequestSelectPreset(string presetId)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(presetId))
            return;

        if (string.Equals(presetId, runtime.CurrentPresetId, StringComparison.Ordinal))
        {
            sidePanelMode = ToneLabSidePanelMode.Presets;
            RefreshUi(syncControls: true);
            return;
        }

        if (HasUnsavedPresetChanges())
        {
            OpenUnsavedChangesModal(ToneLabUnsavedAction.SelectPreset, presetId);
            return;
        }

        SelectPresetNow(presetId);
    }

    private void SelectPresetNow(string presetId)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(presetId))
            return;

        runtime.SelectPreset(presetId);
        selectedPedalInstanceId = string.Empty;
        sidePanelMode = ToneLabSidePanelMode.Presets;
        RefreshUi(syncControls: true);
    }

    private void CloseToneLabNow()
    {
        if (owner != null)
        {
            owner.CloseToneLabFromUi();
            return;
        }

        runtime?.RestoreSelectedPresetWorkingRig();
        SetVisible(false);
    }

    private void OpenEffectsFolder()
    {
        try
        {
            ExternalContentBootstrap.EnsureRuntimeContentReady();
            Directory.CreateDirectory(ExternalContentPaths.PersistentToneLabLv2Directory);
            Directory.CreateDirectory(ExternalContentPaths.PersistentToneLabNamDirectory);

            string folderPath = ExternalContentPaths.PersistentToneLabEffectsDirectory;
            Directory.CreateDirectory(folderPath);
            if (StringTheoryPlatform.TryOpenFolder(folderPath, out string openError))
            {
                ShowActionToast("Opened effects folder.");
                return;
            }

            throw new InvalidOperationException(openError);
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabOverlay] Failed to open effects folder: {ex.Message}");
            ShowActionToast("Could not open effects folder.", true);
        }
    }

    private void RefreshExternalEffectsFromUi()
    {
        runtime?.RefreshExternalPedalLibrary(force: true);
        RefreshUi(syncControls: true);
        ShowActionToast("Effects library refreshed.");
    }

    private void CommitCreatePreset()
    {
        if (runtime == null)
            return;

        if (presetModalMode == ToneLabPresetModalMode.ResetAll)
        {
            runtime.ResetAllToFactoryDefaults();
            ShowActionToast("Restored factory presets and rig.", true);
            CloseCreatePresetModal();
            RefreshUi(syncControls: true);
            return;
        }

        string requestedName = presetNameField?.value ?? string.Empty;
        if (presetModalMode == ToneLabPresetModalMode.SaveGeneratedTone)
        {
            string createdPresetId = owner?.SaveAutoGeneratedTonePresetFromUi(
                pendingGeneratedSongKey,
                pendingGeneratedArrangementKey,
                pendingGeneratedToneName,
                string.IsNullOrWhiteSpace(requestedName) ? pendingGeneratedPresetDefaultName : requestedName) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(createdPresetId))
            {
                ShowActionToast("Could not save generated tone.", true);
                return;
            }

            ShowActionToast($"Saved preset \"{GetPresetName(createdPresetId)}\".");
            CloseCreatePresetModal();
            RefreshUi(syncControls: true);
            return;
        }

        if (presetModalMode == ToneLabPresetModalMode.SaveAs)
        {
            runtime.SaveCurrentAsNewPreset(requestedName);
            ShowActionToast($"Created preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        }
        else
        {
            runtime.CreatePresetFromCurrent(requestedName);
            ShowActionToast($"Created preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        }
        CloseCreatePresetModal();
        RefreshUi(syncControls: true);
    }

    private void OpenAdvancedAudioModal()
    {
        if (advancedAudioModalScrim == null)
            return;

        advancedAudioDraft = owner != null
            ? owner.GetSharedAdvancedAudioSettingsForUi()
            : new SharedAudioAdvancedSettings();
        advancedAudioDraft.inputChannelMode = SharedAudioInputChannelModes.Normalize(advancedAudioDraft.inputChannelMode);
        advancedAudioDraft.backendMode = SharedAudioBackendModes.NormalizeForCurrentPlatform(advancedAudioDraft.backendMode);
        if (advancedAudioDraft.bufferSize <= 0)
            advancedAudioDraft.bufferSize = UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize(UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(advancedAudioDraft.bufferSize));

        RefreshAdvancedAudioModalControls();
        advancedAudioModalScrim.style.display = DisplayStyle.Flex;
    }

    private void CloseAdvancedAudioModal()
    {
        if (advancedAudioModalScrim == null)
            return;

        advancedAudioModalScrim.style.display = DisplayStyle.None;
    }

    private void ToggleAdvancedBetaDraft()
    {
        advancedAudioDraft.betaEnabled = !advancedAudioDraft.betaEnabled;
        RefreshAdvancedAudioToggleStates();
    }

    private void ToggleAdvancedFallbackDraft()
    {
        advancedAudioDraft.allowFallback = !advancedAudioDraft.allowFallback;
        RefreshAdvancedAudioToggleStates();
    }

    private void ToggleAdvancedUnifiedDraft()
    {
        advancedAudioDraft.unifiedOutputEnabled = !advancedAudioDraft.unifiedOutputEnabled;
        RefreshAdvancedAudioToggleStates();
    }

    private void ToggleAdvancedRecorderCaptureDraft()
    {
        advancedAudioDraft.unityRecorderCaptureEnabled = !advancedAudioDraft.unityRecorderCaptureEnabled;
        RefreshAdvancedAudioToggleStates();
    }

    private void RefreshAdvancedAudioModalControls()
    {
        if (advancedAudioModalScrim == null)
            return;

        suppressCallbacks = true;
        advancedInputChannelDropdown?.SetValueWithoutNotify(SharedAudioInputChannelModes.Normalize(advancedAudioDraft.inputChannelMode));
        advancedBackendDropdown?.SetValueWithoutNotify(SharedAudioBackendModes.NormalizeForCurrentPlatform(advancedAudioDraft.backendMode));
        advancedSampleRateDropdown?.SetValueWithoutNotify(FormatAdvancedSampleRateLabel(advancedAudioDraft.sampleRate));
        advancedBufferDropdown?.SetValueWithoutNotify(FormatAdvancedBufferLabel(advancedAudioDraft.bufferSize));
        suppressCallbacks = false;

        RefreshAdvancedAudioToggleStates();
        RefreshAdvancedAudioDeviceChoices();
        RefreshAdvancedAudioModalStatus();
    }

    private void RefreshAdvancedAudioToggleStates()
    {
        ApplyToggleButtonState(advancedBetaToggleButton, advancedAudioDraft.betaEnabled);
        ApplyToggleButtonState(advancedFallbackToggleButton, advancedAudioDraft.allowFallback);
        ApplyToggleButtonState(advancedUnifiedToggleButton, advancedAudioDraft.unifiedOutputEnabled);
        ApplyToggleButtonState(advancedRecorderCaptureToggleButton, advancedAudioDraft.unityRecorderCaptureEnabled);
        advancedInputChannelDropdown?.SetEnabled(true);
        advancedBackendDropdown?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedInputDropdown?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedOutputDropdown?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedSampleRateDropdown?.SetEnabled(advancedAudioDraft.betaEnabled && !advancedAudioDraft.unifiedOutputEnabled && !advancedAudioDraft.unityRecorderCaptureEnabled);
        advancedBufferDropdown?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedFallbackToggleButton?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedUnifiedToggleButton?.SetEnabled(advancedAudioDraft.betaEnabled);
        advancedRecorderCaptureToggleButton?.SetEnabled(true);
    }

    private void RefreshAdvancedAudioDeviceChoices()
    {
        if (advancedInputDropdown == null || advancedOutputDropdown == null)
            return;

        IReadOnlyList<string> inputChoices = owner != null
            ? owner.GetSharedAdvancedAudioInputDeviceChoices(advancedAudioDraft.backendMode)
            : new List<string> { "Automatic" };
        IReadOnlyList<string> outputChoices = owner != null
            ? owner.GetSharedAdvancedAudioOutputDeviceChoices(advancedAudioDraft.backendMode)
            : new List<string> { "Automatic" };

        suppressCallbacks = true;
        advancedInputDropdown.choices = inputChoices.ToList();
        advancedOutputDropdown.choices = outputChoices.ToList();
        string selectedInput = ResolveAdvancedPopupChoice(inputChoices, advancedAudioDraft.inputDeviceName);
        string selectedOutput = ResolveAdvancedPopupChoice(outputChoices, advancedAudioDraft.outputDeviceName);
        advancedInputDropdown.SetValueWithoutNotify(selectedInput);
        advancedOutputDropdown.SetValueWithoutNotify(selectedOutput);
        suppressCallbacks = false;
    }

    private void RefreshAdvancedAudioModalStatus()
    {
        if (advancedAudioStatusLabel == null || runtime == null)
            return;

        string inputChannelMode = advancedAudioDraft != null ? SharedAudioInputChannelModes.Normalize(advancedAudioDraft.inputChannelMode) : SharedAudioInputChannelModes.Input1;
        string summary = $"{runtime.ActiveAudioBackendLabel}  \u2022  {runtime.ActiveHostApiLabel}  \u2022  In {runtime.InputRouteLabel}  \u2022  Out {runtime.OutputRouteLabel}  \u2022  Channel {inputChannelMode}";
        if (advancedAudioDraft != null && advancedAudioDraft.betaEnabled && advancedAudioDraft.unifiedOutputEnabled)
            summary = $"{summary}\nUnified output locks sample rate to Unity output.";
        if (advancedAudioDraft != null && advancedAudioDraft.unityRecorderCaptureEnabled)
            summary = $"{summary}\nUnity Recorder Capture mirrors processed guitar into Unity audio. Use only while recording to avoid hearing a second monitoring path.";
        string diagnostics = runtime.StatusMessage;
        if (!string.IsNullOrWhiteSpace(runtime.LastRoutingAttemptSummary))
            diagnostics = $"{diagnostics}\n\n{runtime.LastRoutingAttemptSummary}";
        if (!string.IsNullOrWhiteSpace(runtime.LastRoutingDiagnostics))
            diagnostics = $"{diagnostics}\n\n{runtime.LastRoutingDiagnostics}";

        advancedAudioStatusLabel.text = summary;
        advancedAudioDiagnosticsLabel.text = diagnostics;
    }

    private void CommitAdvancedAudioSettings()
    {
        if (owner == null)
            return;

        owner.ApplySharedAdvancedAudioSettingsFromUi(advancedAudioDraft);
        RefreshUi(syncControls: true, refreshDevices: true);
        RefreshAdvancedAudioModalStatus();
    }

    private static string NormalizeAdvancedPopupSelection(string value)
    {
        return string.Equals(value, "Automatic", StringComparison.OrdinalIgnoreCase) ? string.Empty : value?.Trim() ?? string.Empty;
    }

    private static string ResolveAdvancedPopupChoice(IReadOnlyList<string> choices, string storedValue)
    {
        if (choices == null || choices.Count == 0)
            return "Automatic";

        if (string.IsNullOrWhiteSpace(storedValue))
            return choices[0];

        for (int i = 0; i < choices.Count; i++)
        {
            if (string.Equals(choices[i], storedValue, StringComparison.OrdinalIgnoreCase))
                return choices[i];
        }

        return choices[0];
    }

    private static string FormatAdvancedSampleRateLabel(int sampleRate)
    {
        return sampleRate <= 0 ? "Auto" : $"{sampleRate} Hz";
    }

    private static int ParseAdvancedSampleRateLabel(string label)
    {
        if (string.IsNullOrWhiteSpace(label) || string.Equals(label.Trim(), "Auto", StringComparison.OrdinalIgnoreCase))
            return 0;

        string digits = new string(label.Where(char.IsDigit).ToArray());
        return int.TryParse(digits, out int parsed) ? parsed : 0;
    }

    private static string FormatAdvancedBufferLabel(int bufferSize)
    {
        int normalized = UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize(UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(bufferSize));
        return $"{normalized} Samples";
    }

    private static int ParseAdvancedBufferLabel(string label)
    {
        string digits = new string((label ?? string.Empty).Where(char.IsDigit).ToArray());
        if (!int.TryParse(digits, out int parsed))
            parsed = 128;
        return UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize(UnityToneLabRuntime.GetSharedMonitoringLatencyLabel(parsed));
    }

    private void EnsureSelectedPedal(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (pedalChain == null || pedalChain.Count == 0)
        {
            selectedPedalInstanceId = string.Empty;
            return;
        }

        for (int i = 0; i < pedalChain.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
                return;
        }

        selectedPedalInstanceId = pedalChain[0]?.pedal_instance_id ?? string.Empty;
    }

    private VisualElement CreateSongMappingView()
    {
        VisualElement root = new VisualElement();
        root.style.flexGrow = 1f;
        root.style.minHeight = 0f;
        root.style.flexDirection = FlexDirection.Column;
        root.style.borderTopWidth = 0f;
        root.style.borderBottomWidth = 1f;
        root.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);

        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.flexShrink = 0f;
        header.style.height = 66f;
        header.style.borderBottomWidth = 1f;
        header.style.borderBottomColor = new Color(1f, 1f, 1f, 0.18f);
        root.Add(header);

        VisualElement titleColumn = new VisualElement();
        titleColumn.style.width = 250f;
        titleColumn.style.minWidth = 250f;
        header.Add(titleColumn);

        songMappingHeaderLabel = new Label("Tone Mapping");
        songMappingHeaderLabel.style.color = Color.white;
        songMappingHeaderLabel.style.fontSize = 24f;
        songMappingHeaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        songMappingHeaderLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleColumn.Add(songMappingHeaderLabel);

        songMappingSubheaderLabel = new Label("Select a song");
        songMappingSubheaderLabel.style.flexGrow = 1f;
        songMappingSubheaderLabel.style.minWidth = 0f;
        songMappingSubheaderLabel.style.color = Color.white;
        songMappingSubheaderLabel.style.fontSize = 30f;
        songMappingSubheaderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        songMappingSubheaderLabel.style.whiteSpace = WhiteSpace.NoWrap;
        songMappingSubheaderLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        songMappingSubheaderLabel.style.overflow = Overflow.Hidden;
        songMappingSubheaderLabel.style.textOverflow = TextOverflow.Ellipsis;
        header.Add(songMappingSubheaderLabel);

        VisualElement headerSpacer = new VisualElement();
        headerSpacer.style.width = 250f;
        headerSpacer.style.minWidth = 250f;
        header.Add(headerSpacer);

        VisualElement body = new VisualElement();
        body.style.flexGrow = 1f;
        body.style.minHeight = 0f;
        body.style.flexDirection = FlexDirection.Row;
        root.Add(body);

        VisualElement leftColumn = new VisualElement();
        leftColumn.style.width = 360f;
        leftColumn.style.minWidth = 360f;
        leftColumn.style.maxWidth = 360f;
        leftColumn.style.flexShrink = 0f;
        leftColumn.style.paddingTop = 14f;
        leftColumn.style.paddingRight = 20f;
        leftColumn.style.borderRightWidth = 1f;
        leftColumn.style.borderRightColor = new Color(1f, 1f, 1f, 0.16f);
        body.Add(leftColumn);

        songMappingSelectedArtwork = new VisualElement();
        songMappingSelectedArtwork.style.width = Length.Percent(100f);
        songMappingSelectedArtwork.style.height = 220f;
        songMappingSelectedArtwork.style.minHeight = 220f;
        songMappingSelectedArtwork.style.marginBottom = 14f;
        songMappingSelectedArtwork.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
        songMappingSelectedArtwork.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        songMappingSelectedArtwork.style.borderTopLeftRadius = 8f;
        songMappingSelectedArtwork.style.borderTopRightRadius = 8f;
        songMappingSelectedArtwork.style.borderBottomLeftRadius = 8f;
        songMappingSelectedArtwork.style.borderBottomRightRadius = 8f;
        songMappingSelectedArtwork.style.display = DisplayStyle.None;
        leftColumn.Add(songMappingSelectedArtwork);

        songMappingLeftScroll = new ScrollView(ScrollViewMode.Vertical);
        StyleTransparentScrollView(songMappingLeftScroll);
        songMappingLeftScroll.style.flexGrow = 1f;
        songMappingLeftScroll.style.minHeight = 0f;
        songMappingLeftHost = songMappingLeftScroll.contentContainer;
        songMappingLeftHost.style.flexDirection = FlexDirection.Column;
        leftColumn.Add(songMappingLeftScroll);

        VisualElement rightColumn = new VisualElement();
        rightColumn.style.flexGrow = 1f;
        rightColumn.style.minWidth = 0f;
        rightColumn.style.minHeight = 0f;
        rightColumn.style.paddingTop = 14f;
        rightColumn.style.paddingLeft = 22f;
        body.Add(rightColumn);

        songMappingToneScroll = new ScrollView(ScrollViewMode.Vertical);
        StyleTransparentScrollView(songMappingToneScroll);
        songMappingToneHost = songMappingToneScroll.contentContainer;
        songMappingToneHost.style.flexDirection = FlexDirection.Column;
        rightColumn.Add(songMappingToneScroll);

        return root;
    }

    private void ToggleSongMappingMode()
    {
        boardMode = boardMode == ToneLabBoardMode.SongMapping ? ToneLabBoardMode.Pedalboard : ToneLabBoardMode.SongMapping;
        if (boardMode == ToneLabBoardMode.SongMapping)
        {
            sidePanelMode = ToneLabSidePanelMode.Presets;
            ResetNavigation(ToneLabNavigationZone.SongMappingLeft);
            SelectCurrentSongMappingContextIfAvailable();
        }
        else
        {
            ClearPendingToneMappingAssignment();
            ResetNavigation(ToneLabNavigationZone.PedalBoard);
        }

        RefreshUi(syncControls: true);
    }

    private void SelectCurrentSongMappingContextIfAvailable()
    {
        if (owner == null)
            return;

        if (!owner.TryGetCurrentToneLabSongMappingSelectionForUi(out string songKey, out string arrangementKey))
            return;

        if (!string.IsNullOrWhiteSpace(songKey))
        {
            selectedMappingSongKey = songKey;
            songMappingBrowseMode = ToneLabSongMappingBrowseMode.All;
            songMappingBrowseScopeKey = string.Empty;
        }

        if (!string.IsNullOrWhiteSpace(arrangementKey))
            selectedMappingArrangementKey = arrangementKey;
    }

    private void RefreshBoardModeVisuals()
    {
        bool mapping = boardMode == ToneLabBoardMode.SongMapping;
        if (pedalBoardRoot != null)
            pedalBoardRoot.style.display = mapping ? DisplayStyle.None : DisplayStyle.Flex;
        if (songMappingRoot != null)
            songMappingRoot.style.display = mapping ? DisplayStyle.Flex : DisplayStyle.None;
        ApplySongMappingButtonState(mapping);
    }

    private void ApplySongMappingButtonState(bool selected)
    {
        if (songMappingButton == null)
            return;

        songMappingButton.text = selected ? "Pedalboard" : "Song Mapping";
        songMappingButton.style.backgroundColor = selected ? new Color(1f, 0.58f, 0.08f, 0.22f) : new Color(1f, 0.58f, 0.08f, 0.12f);
        songMappingButton.style.color = selected ? Color.white : new Color(1f, 0.84f, 0.52f, 0.98f);
        songMappingButton.style.borderTopColor = new Color(1f, 0.88f, 0.48f, selected ? 0.92f : 0.74f);
        songMappingButton.style.borderRightColor = new Color(1f, 0.56f, 0.38f, selected ? 0.86f : 0.64f);
        songMappingButton.style.borderBottomColor = new Color(0.82f, 0.32f, 0.22f, selected ? 0.82f : 0.58f);
        songMappingButton.style.borderLeftColor = new Color(1f, 0.72f, 0.22f, selected ? 0.86f : 0.64f);
    }

    private void RefreshSongMappingView()
    {
        if (boardMode != ToneLabBoardMode.SongMapping || owner == null || songMappingLeftHost == null || songMappingToneHost == null)
            return;

        songMappingLeftHost.Clear();
        songMappingToneHost.Clear();
        songMappingLeftNavigationItems.Clear();
        songMappingToneNavigationItems.Clear();

        List<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> songs = owner.GetToneLabSongMappingSongsForUi();
        if (songs == null || songs.Count == 0)
        {
            AddSongMappingEmpty(songMappingLeftHost, "No imported Rocksmith songs found.");
            AddSongMappingEmpty(songMappingToneHost, "Import a Rocksmith song first, then reopen this screen.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedMappingSongKey) ||
            songs.All(song => !string.Equals(song.songKey, selectedMappingSongKey, StringComparison.OrdinalIgnoreCase)))
        {
            selectedMappingSongKey = string.Empty;
            selectedMappingArrangementKey = string.Empty;
            ClearPendingToneMappingAssignment();
        }

        if (string.IsNullOrWhiteSpace(selectedMappingSongKey))
        {
            SetSongMappingHeaderSongTitle("Select a song");
            ApplySongMappingSelectedArtwork(null);
            songMappingFilterRoot?.RemoveFromHierarchy();
            songMappingLeftHost.Add(songMappingFilterRoot);
            songMappingSearchRoot?.RemoveFromHierarchy();
            songMappingLeftHost.Add(songMappingSearchRoot);
            RefreshSongMappingFilterButtonStates();

            List<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> visibleSongs = BuildVisibleSongMappingSongs(songs);
            if (!string.IsNullOrWhiteSpace(songMappingBrowseScopeKey))
            {
                songMappingLeftHost.Add(CreateSongMappingBackRow(GetSongMappingBrowseModeLabel(songMappingBrowseMode), () =>
                {
                    songMappingBrowseScopeKey = string.Empty;
                    RefreshUi(syncControls: true);
                }));
            }

            if (string.IsNullOrWhiteSpace(songMappingBrowseScopeKey) && songMappingBrowseMode != ToneLabSongMappingBrowseMode.All)
            {
                List<IGrouping<string, GuitarBridgeServer.ToneLabSongMappingSongSnapshot>> groups = BuildSongMappingBrowseGroups(visibleSongs);
                if (groups.Count == 0)
                {
                    AddSongMappingEmpty(songMappingLeftHost, "No matching groups.");
                }
                else
                {
                    for (int i = 0; i < groups.Count; i++)
                    {
                        IGrouping<string, GuitarBridgeServer.ToneLabSongMappingSongSnapshot> group = groups[i];
                        GuitarBridgeServer.ToneLabSongMappingSongSnapshot first = group.FirstOrDefault();
                        songMappingLeftHost.Add(CreateSongMappingGroupRow(group.Key, group.Count(), first?.artworkPath ?? string.Empty, () =>
                        {
                            songMappingBrowseScopeKey = group.Key;
                            RefreshUi(syncControls: true);
                        }));
                    }
                }
            }
            else
            {
                if (visibleSongs.Count == 0)
                {
                    AddSongMappingEmpty(songMappingLeftHost, "No matching songs.");
                }
                else
                {
                    for (int i = 0; i < visibleSongs.Count; i++)
                        songMappingLeftHost.Add(CreateSongMappingSongRow(visibleSongs[i]));
                }
            }

            AddSongMappingEmpty(songMappingToneHost, "Select a song to see arrangements.");
            return;
        }

        GuitarBridgeServer.ToneLabSongMappingSongSnapshot selectedSong = songs.FirstOrDefault(song =>
            string.Equals(song.songKey, selectedMappingSongKey, StringComparison.OrdinalIgnoreCase));
        SetSongMappingHeaderSongTitle(selectedSong != null
            ? TruncateWithEllipsis(selectedSong.displayName, 74)
            : "Choose an arrangement");
        ApplySongMappingSelectedArtwork(selectedSong);

        songMappingLeftHost.Add(CreateSongMappingBackRow("Songs", () =>
        {
            selectedMappingSongKey = string.Empty;
            selectedMappingArrangementKey = string.Empty;
            ClearPendingToneMappingAssignment();
            RefreshUi(syncControls: true);
        }));

        List<GuitarBridgeServer.ToneLabSongMappingArrangementSnapshot> arrangements = owner.GetToneLabSongMappingArrangementsForUi(selectedMappingSongKey);
        if (arrangements == null || arrangements.Count == 0)
        {
            AddSongMappingEmpty(songMappingLeftHost, "No arrangements found.");
            AddSongMappingEmpty(songMappingToneHost, "This song has no arrangement tone cache yet.");
            return;
        }

        if (string.IsNullOrWhiteSpace(selectedMappingArrangementKey) ||
            arrangements.All(arrangement => !string.Equals(arrangement.arrangementKey, selectedMappingArrangementKey, StringComparison.OrdinalIgnoreCase)))
        {
            selectedMappingArrangementKey = arrangements[0].arrangementKey;
            ClearPendingToneMappingAssignment();
        }

        for (int i = 0; i < arrangements.Count; i++)
            songMappingLeftHost.Add(CreateSongMappingArrangementRow(arrangements[i]));

        List<GuitarBridgeServer.ToneLabSongToneMappingSnapshot> tones = owner.GetToneLabSongToneMappingsForUi(selectedMappingSongKey, selectedMappingArrangementKey);
        if (tones == null || tones.Count == 0)
        {
            AddSongMappingEmpty(songMappingToneHost, "No Rocksmith tones were found for this arrangement. Re-importing the song may be needed.");
            return;
        }

        for (int i = 0; i < tones.Count; i++)
            songMappingToneHost.Add(CreateSongMappingToneRow(tones[i], i));
    }

    private Button CreateSongMappingSongRow(GuitarBridgeServer.ToneLabSongMappingSongSnapshot song)
    {
        string title = string.IsNullOrWhiteSpace(song?.displayName) ? "Untitled Song" : song.displayName.Trim();
        string subtitle = string.Empty;
        Action action = () =>
        {
            selectedMappingSongKey = song?.songKey ?? string.Empty;
            selectedMappingArrangementKey = string.Empty;
            ClearPendingToneMappingAssignment();
            ResetNavigation(ToneLabNavigationZone.SongMappingLeft);
            RefreshUi(syncControls: true);
        };
        Button row = CreateSongMappingListRow(title, subtitle, false, action, song?.artworkPath ?? string.Empty);
        RegisterNavigationItem(songMappingLeftNavigationItems, row, songMappingLeftScroll, action, GetNavigationHoverSetter(row));
        return row;
    }

    private Button CreateSongMappingArrangementRow(GuitarBridgeServer.ToneLabSongMappingArrangementSnapshot arrangement)
    {
        bool selected = string.Equals(arrangement?.arrangementKey ?? string.Empty, selectedMappingArrangementKey, StringComparison.OrdinalIgnoreCase);
        string subtitle = arrangement?.toneCount > 0
            ? $"{arrangement.toneCount} tone{(arrangement.toneCount == 1 ? string.Empty : "s")}"
            : "No tones";
        if (!string.IsNullOrWhiteSpace(arrangement?.difficultySummary))
            subtitle = $"{subtitle}  •  {arrangement.difficultySummary}";

        Action action = () =>
        {
            selectedMappingArrangementKey = arrangement?.arrangementKey ?? string.Empty;
            ClearPendingToneMappingAssignment();
            ResetNavigation(ToneLabNavigationZone.SongMappingLeft);
            RefreshUi(syncControls: true);
        };
        Button row = CreateSongMappingListRow(arrangement?.displayName ?? "Arrangement", subtitle, selected, action);
        RegisterNavigationItem(songMappingLeftNavigationItems, row, songMappingLeftScroll, action, GetNavigationHoverSetter(row));
        return row;
    }

    private void SetSongMappingHeaderSongTitle(string title)
    {
        if (songMappingSubheaderLabel == null)
            return;

        string text = string.IsNullOrWhiteSpace(title) ? "Select a song" : title.Trim();
        songMappingSubheaderLabel.text = text;
        songMappingSubheaderLabel.tooltip = text;
    }

    private void ApplySongMappingSelectedArtwork(GuitarBridgeServer.ToneLabSongMappingSongSnapshot song)
    {
        if (songMappingSelectedArtwork == null)
            return;

        if (song == null)
        {
            songMappingSelectedArtwork.style.display = DisplayStyle.None;
            songMappingSelectedArtwork.style.backgroundImage = StyleKeyword.None;
            return;
        }

        songMappingSelectedArtwork.style.display = DisplayStyle.Flex;
        songMappingSelectedArtwork.style.backgroundColor = GetDeterministicAccentColor(song.displayName);
        Texture2D texture = GetSongArtworkTexture(song.artworkPath);
        if (texture != null)
            songMappingSelectedArtwork.style.backgroundImage = new StyleBackground(texture);
        else
            songMappingSelectedArtwork.style.backgroundImage = StyleKeyword.None;
    }

    private Button CreateSongMappingBackRow(string label, Action onClick)
    {
        Action action = () =>
        {
            ResetNavigation(ToneLabNavigationZone.SongMappingLeft);
            onClick?.Invoke();
        };
        Button row = CreateSongMappingListRow($"‹ {label}", string.Empty, false, action);
        row.style.height = 42f;
        row.style.minHeight = 42f;
        RegisterNavigationItem(songMappingLeftNavigationItems, row, songMappingLeftScroll, action, GetNavigationHoverSetter(row));
        return row;
    }

    private Button CreateSongMappingGroupRow(string title, int songCount, string artworkPath, Action onClick)
    {
        string subtitle = songCount == 1 ? "1 song" : $"{songCount} songs";
        Action action = () =>
        {
            ResetNavigation(ToneLabNavigationZone.SongMappingLeft);
            onClick?.Invoke();
        };
        Button row = CreateSongMappingListRow(title, subtitle, false, action, artworkPath);
        RegisterNavigationItem(songMappingLeftNavigationItems, row, songMappingLeftScroll, action, GetNavigationHoverSetter(row));
        return row;
    }

    private Button CreateSongMappingListRow(string title, string subtitle, bool selected, Action onClick, string artworkPath = null)
    {
        Button row = new Button(onClick) { text = string.Empty };
        bool hasArtwork = artworkPath != null;
        row.style.height = hasArtwork ? 64f : (string.IsNullOrWhiteSpace(subtitle) ? 48f : 58f);
        row.style.minHeight = row.style.height;
        row.style.marginBottom = 4f;
        row.style.paddingLeft = selected ? 16f : 10f;
        row.style.paddingRight = 10f;
        row.style.paddingTop = 0f;
        row.style.paddingBottom = 0f;
        row.style.backgroundColor = selected ? new Color(1f, 0.58f, 0.08f, 0.82f) : new Color(0f, 0f, 0f, 0f);
        row.style.borderTopWidth = 0f;
        row.style.borderRightWidth = 0f;
        row.style.borderBottomWidth = 0f;
        row.style.borderLeftWidth = selected ? 4f : 0f;
        row.style.borderBottomColor = Color.clear;
        row.style.borderLeftColor = selected ? new Color(1f, 0.92f, 0.42f, 1f) : Color.clear;
        row.style.borderTopLeftRadius = 0f;
        row.style.borderTopRightRadius = 0f;
        row.style.borderBottomLeftRadius = 0f;
        row.style.borderBottomRightRadius = 0f;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;

        if (hasArtwork)
        {
            VisualElement artwork = CreateSongMappingArtworkElement(artworkPath, title);
            row.Add(artwork);
        }

        VisualElement column = new VisualElement();
        column.style.justifyContent = Justify.Center;
        column.style.flexGrow = 1f;
        column.style.flexShrink = 1f;
        column.style.minWidth = 0f;
        row.Add(column);

        Label titleLabel = new Label(title ?? string.Empty);
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = selected ? 18f : 16f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        titleLabel.style.overflow = Overflow.Hidden;
        titleLabel.style.textOverflow = TextOverflow.Ellipsis;
        titleLabel.style.flexShrink = 1f;
        column.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.style.color = selected ? new Color(1f, 1f, 1f, 0.82f) : new Color(0.78f, 0.81f, 0.86f, 0.72f);
            subtitleLabel.style.fontSize = 12f;
            subtitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            subtitleLabel.style.whiteSpace = WhiteSpace.NoWrap;
            column.Add(subtitleLabel);
        }

        Action<bool> applyHover = hovered =>
        {
            if (!selected)
                row.style.backgroundColor = hovered ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0f);
        };
        row.userData = applyHover;
        row.RegisterCallback<MouseEnterEvent>(_ => applyHover(true));
        row.RegisterCallback<MouseLeaveEvent>(_ => applyHover(false));

        return row;
    }

    private VisualElement CreateSongMappingArtworkElement(string artworkPath, string fallbackKey)
    {
        VisualElement artwork = new VisualElement();
        artwork.style.width = 48f;
        artwork.style.minWidth = 48f;
        artwork.style.height = 48f;
        artwork.style.marginRight = 12f;
        artwork.style.backgroundColor = GetDeterministicAccentColor(fallbackKey);
        artwork.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
        artwork.style.borderTopLeftRadius = 4f;
        artwork.style.borderTopRightRadius = 4f;
        artwork.style.borderBottomLeftRadius = 4f;
        artwork.style.borderBottomRightRadius = 4f;

        Texture2D texture = GetSongArtworkTexture(artworkPath);
        if (texture != null)
            artwork.style.backgroundImage = new StyleBackground(texture);

        return artwork;
    }

    private static Texture2D GetSongArtworkTexture(string artworkPath)
    {
        if (string.IsNullOrWhiteSpace(artworkPath) || !File.Exists(artworkPath))
            return null;

        if (songArtworkTextureCache.TryGetValue(artworkPath, out Texture2D cached))
            return cached;

        try
        {
            byte[] bytes = File.ReadAllBytes(artworkPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
            {
                name = $"ToneLabSongArtwork_{Path.GetFileNameWithoutExtension(artworkPath)}",
                hideFlags = HideFlags.HideAndDontSave,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear
            };

            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                return null;
            }

            songArtworkTextureCache[artworkPath] = texture;
            return texture;
        }
        catch (Exception ex)
        {
            Debug.LogWarning($"[UnityToneLabOverlay] Failed to load song artwork '{artworkPath}': {ex.Message}");
            return null;
        }
    }

    private static Color GetDeterministicAccentColor(string key)
    {
        int hash = string.IsNullOrWhiteSpace(key) ? 0 : StringComparer.OrdinalIgnoreCase.GetHashCode(key.Trim());
        float hue = Mathf.Abs(hash % 360) / 360f;
        return Color.HSVToRGB(hue, 0.58f, 0.78f);
    }

    private VisualElement CreateSongMappingToneRow(GuitarBridgeServer.ToneLabSongToneMappingSnapshot tone, int index)
    {
        VisualElement row = new VisualElement();
        row.style.minHeight = 68f;
        row.style.marginBottom = 4f;
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.borderBottomWidth = 1f;
        row.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        row.style.paddingLeft = 0f;
        row.style.paddingRight = 0f;
        Action<bool> applyToneHover = hovered =>
        {
            row.style.backgroundColor = hovered ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0f);
        };
        row.RegisterCallback<MouseEnterEvent>(_ => applyToneHover(true));
        row.RegisterCallback<MouseLeaveEvent>(_ => applyToneHover(false));

        VisualElement accent = new VisualElement();
        accent.style.width = 4f;
        accent.style.height = 42f;
        accent.style.marginRight = 14f;
        accent.style.backgroundColor = tone != null && tone.isBaseTone ? new Color(0.20f, 0.84f, 0.46f, 1f) : StartupLogoColor(index + 1);
        row.Add(accent);

        VisualElement copy = new VisualElement();
        copy.style.flexGrow = 1f;
        copy.style.minWidth = 0f;
        copy.style.justifyContent = Justify.Center;
        row.Add(copy);

        Label toneLabel = new Label(tone?.toneName ?? "Tone");
        toneLabel.style.color = Color.white;
        toneLabel.style.fontSize = 19f;
        toneLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        toneLabel.style.whiteSpace = WhiteSpace.NoWrap;
        copy.Add(toneLabel);

        string timingText = tone != null && tone.isBaseTone ? "Base tone" : "Tone switch";
        if (tone != null && tone.switchCount > 0)
        {
            string timeText = tone.firstSwitchTimeSeconds >= 0f
                ? $"{tone.firstSwitchTimeSeconds:F1}s"
                : string.Empty;
            timingText = string.IsNullOrWhiteSpace(timeText)
                ? $"{tone.switchCount} switch{(tone.switchCount == 1 ? string.Empty : "es")}"
                : $"{tone.switchCount} switch{(tone.switchCount == 1 ? string.Empty : "es")}  •  first at {timeText}";
        }

        Label timingLabel = new Label(timingText);
        timingLabel.style.color = new Color(0.78f, 0.81f, 0.86f, 0.70f);
        timingLabel.style.fontSize = 12f;
        timingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        timingLabel.style.whiteSpace = WhiteSpace.NoWrap;
        copy.Add(timingLabel);

        bool assigning = IsAssigningTone(tone);
        string buttonText = assigning
            ? "Choose preset..."
            : (!string.IsNullOrWhiteSpace(tone?.assignedPresetName) ? tone.assignedPresetName : "Select preset...");
        Action selectAction = () =>
        {
            pendingMappingSongKey = tone?.songKey ?? selectedMappingSongKey;
            pendingMappingArrangementKey = tone?.arrangementKey ?? selectedMappingArrangementKey;
            pendingMappingToneName = tone?.toneName ?? string.Empty;
            sidePanelMode = ToneLabSidePanelMode.Presets;
            ResetNavigation(ToneLabNavigationZone.Sidebar);
            RefreshUi(syncControls: true);
            ShowActionToast($"Select a preset for \"{pendingMappingToneName}\".");
        };
        Button selectButton = CreateButton(buttonText, "tone-lab-button tone-lab-button-secondary", selectAction);
        StyleCompactActionButton(selectButton, 210f);
        selectButton.style.marginRight = 0f;
        if (assigning)
        {
            selectButton.style.color = Color.white;
            selectButton.style.borderTopColor = new Color(1f, 0.88f, 0.48f, 0.92f);
            selectButton.style.borderRightColor = new Color(1f, 0.56f, 0.38f, 0.86f);
            selectButton.style.borderBottomColor = new Color(0.82f, 0.32f, 0.22f, 0.82f);
            selectButton.style.borderLeftColor = new Color(1f, 0.72f, 0.22f, 0.86f);
        }
        row.Add(selectButton);

        if (!string.IsNullOrWhiteSpace(tone?.assignedPresetId) && tone.isAutomaticPresetMapping)
        {
            Button saveGeneratedButton = CreateButton("Save as Preset", "tone-lab-button tone-lab-button-secondary", () =>
            {
                OpenSaveGeneratedTonePresetModal(tone);
            });
            StyleCompactActionButton(saveGeneratedButton, 154f);
            saveGeneratedButton.style.marginLeft = 8f;
            saveGeneratedButton.style.marginRight = 0f;
            row.Add(saveGeneratedButton);
        }

        if (!string.IsNullOrWhiteSpace(tone?.assignedPresetId) && !tone.isAutomaticPresetMapping)
        {
            Button clearButton = new Button(() =>
            {
                owner?.SetToneLabSongTonePresetMappingFromUi(tone.songKey, tone.arrangementKey, tone.toneName, string.Empty);
                ClearPendingToneMappingAssignment();
                RefreshUi(syncControls: true);
            })
            {
                text = "X"
            };
            StyleTextDeleteButton(clearButton);
            clearButton.style.marginLeft = 8f;
            row.Add(clearButton);
        }

        RegisterNavigationItem(songMappingToneNavigationItems, row, songMappingToneScroll, selectAction, applyToneHover);
        return row;
    }

    private void OpenSaveGeneratedTonePresetModal(GuitarBridgeServer.ToneLabSongToneMappingSnapshot tone)
    {
        if (tone == null || string.IsNullOrWhiteSpace(tone.assignedPresetId) || !tone.isAutomaticPresetMapping)
            return;

        pendingGeneratedSongKey = tone.songKey ?? selectedMappingSongKey;
        pendingGeneratedArrangementKey = tone.arrangementKey ?? selectedMappingArrangementKey;
        pendingGeneratedToneName = tone.toneName ?? string.Empty;
        pendingGeneratedPresetDefaultName = !string.IsNullOrWhiteSpace(tone.saveAsPresetDefaultName)
            ? tone.saveAsPresetDefaultName.Trim()
            : $"{(string.IsNullOrWhiteSpace(pendingGeneratedToneName) ? "Song Tone" : pendingGeneratedToneName.Trim())} - Auto";
        OpenPresetModal(ToneLabPresetModalMode.SaveGeneratedTone);
    }

    private void ClearPendingGeneratedTonePresetSave()
    {
        pendingGeneratedToneName = string.Empty;
        pendingGeneratedArrangementKey = string.Empty;
        pendingGeneratedSongKey = string.Empty;
        pendingGeneratedPresetDefaultName = string.Empty;
    }

    private void AddSongMappingEmpty(VisualElement host, string text)
    {
        if (host == null)
            return;

        Label empty = new Label(text ?? string.Empty);
        empty.style.color = new Color(0.90f, 0.92f, 0.96f, 0.70f);
        empty.style.fontSize = 16f;
        empty.style.unityFontStyleAndWeight = FontStyle.Bold;
        empty.style.marginTop = 18f;
        empty.style.whiteSpace = WhiteSpace.Normal;
        host.Add(empty);
    }

    private bool IsAssigningTone(GuitarBridgeServer.ToneLabSongToneMappingSnapshot tone)
    {
        return tone != null &&
               string.Equals(pendingMappingSongKey, tone.songKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(pendingMappingArrangementKey, tone.arrangementKey, StringComparison.OrdinalIgnoreCase) &&
               string.Equals(pendingMappingToneName, tone.toneName, StringComparison.OrdinalIgnoreCase);
    }

    private bool HasPendingToneMappingAssignment()
    {
        return !string.IsNullOrWhiteSpace(pendingMappingSongKey) &&
               !string.IsNullOrWhiteSpace(pendingMappingArrangementKey) &&
               !string.IsNullOrWhiteSpace(pendingMappingToneName);
    }

    private bool TryAssignPendingToneMapping(UnityToneLabRuntime.ToneLabPreset preset)
    {
        if (owner == null ||
            preset == null ||
            string.IsNullOrWhiteSpace(preset.preset_id) ||
            string.IsNullOrWhiteSpace(pendingMappingSongKey) ||
            string.IsNullOrWhiteSpace(pendingMappingArrangementKey) ||
            string.IsNullOrWhiteSpace(pendingMappingToneName))
        {
            return false;
        }

        owner.SetToneLabSongTonePresetMappingFromUi(pendingMappingSongKey, pendingMappingArrangementKey, pendingMappingToneName, preset.preset_id);
        string toneName = pendingMappingToneName;
        string presetName = string.IsNullOrWhiteSpace(preset.preset_name) ? "Preset" : preset.preset_name.Trim();
        ClearPendingToneMappingAssignment();
        RefreshUi(syncControls: true);
        ShowActionToast($"Mapped \"{toneName}\" to \"{presetName}\".");
        return true;
    }

    private void ClearPendingToneMappingAssignment()
    {
        pendingMappingToneName = string.Empty;
        pendingMappingArrangementKey = string.Empty;
        pendingMappingSongKey = string.Empty;
    }

    private void RefreshPresetList(IReadOnlyList<UnityToneLabRuntime.ToneLabPreset> presets, string selectedPresetId)
    {
        if (presetListHost == null)
            return;

        presetListHost.Clear();
        presetNavigationItems.Clear();
        if (presets == null || presets.Count == 0)
        {
            Label emptyLabel = new Label("No presets yet.");
            emptyLabel.style.color = new Color(0.90f, 0.92f, 0.96f, 0.72f);
            emptyLabel.style.fontSize = 17f;
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            emptyLabel.style.marginTop = 18f;
            presetListHost.Add(emptyLabel);
            return;
        }

        string normalizedQuery = NormalizeSearchQuery(presetSearchQuery);
        int visibleCount = 0;
        for (int i = 0; i < presets.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPreset preset = presets[i];
            if (preset == null || string.IsNullOrWhiteSpace(preset.preset_id))
                continue;

            string presetName = string.IsNullOrWhiteSpace(preset.preset_name) ? $"Preset {i + 1}" : preset.preset_name.Trim();
            if (!MatchesSearch(presetName, normalizedQuery))
                continue;

            bool selected = string.Equals(preset.preset_id, selectedPresetId, StringComparison.Ordinal);
            presetListHost.Add(CreatePresetRow(preset, i, selected, presets.Count));
            visibleCount++;
        }

        if (visibleCount == 0)
        {
            Label emptyLabel = new Label("No matching presets.");
            emptyLabel.style.color = new Color(0.90f, 0.92f, 0.96f, 0.72f);
            emptyLabel.style.fontSize = 17f;
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            emptyLabel.style.marginTop = 18f;
            presetListHost.Add(emptyLabel);
        }
    }

    private VisualElement CreatePresetRow(UnityToneLabRuntime.ToneLabPreset preset, int index, bool selected, int presetCount)
    {
        Action rowAction = () =>
        {
            if (runtime == null || preset == null)
                return;

            if (TryAssignPendingToneMapping(preset))
                return;

            RequestSelectPreset(preset.preset_id);
        };
        Button row = new Button(rowAction);
        row.text = string.Empty;
        row.style.height = 54f;
        row.style.minHeight = 54f;
        row.style.marginBottom = 2f;
        row.style.paddingLeft = 0f;
        row.style.paddingRight = 0f;
        row.style.paddingTop = 0f;
        row.style.paddingBottom = 0f;
        row.style.borderTopWidth = 0f;
        row.style.borderRightWidth = 0f;
        row.style.borderBottomWidth = 1f;
        row.style.borderLeftWidth = 0f;
        row.style.borderBottomColor = new Color(1f, 1f, 1f, selected ? 0f : 0.12f);
        row.style.backgroundColor = selected ? new Color(1f, 0.58f, 0.08f, 0.92f) : new Color(0f, 0f, 0f, 0f);
        if (selected)
            row.style.backgroundImage = new StyleBackground(GetPresetSelectionGradientTexture());
        else
            row.style.backgroundImage = StyleKeyword.None;
        row.style.borderTopLeftRadius = 0f;
        row.style.borderTopRightRadius = 0f;
        row.style.borderBottomLeftRadius = 0f;
        row.style.borderBottomRightRadius = 0f;

        VisualElement content = new VisualElement();
        content.style.flexGrow = 1f;
        content.style.height = Length.Percent(100f);
        content.style.flexDirection = FlexDirection.Row;
        content.style.alignItems = Align.Center;
        content.style.paddingLeft = selected ? 18f : 10f;
        content.style.paddingRight = 8f;
        row.Add(content);

        VisualElement accent = new VisualElement();
        accent.style.width = 4f;
        accent.style.height = 36f;
        accent.style.marginRight = 12f;
        accent.style.backgroundColor = GetPresetContentAccentColor(preset);
        content.Add(accent);

        Label nameLabel = new Label(string.IsNullOrWhiteSpace(preset.preset_name) ? $"Preset {index + 1}" : preset.preset_name.Trim());
        nameLabel.style.flexGrow = 1f;
        nameLabel.style.color = selected ? Color.white : new Color(0.96f, 0.97f, 0.99f, 0.98f);
        nameLabel.style.fontSize = selected ? 22f : 18f;
        nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        nameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        content.Add(nameLabel);

        bool assigningPreset = HasPendingToneMappingAssignment();
        Button deleteButton = new Button(() =>
        {
            if (assigningPreset)
            {
                TryAssignPendingToneMapping(preset);
                return;
            }

            if (runtime == null || presetCount <= 1)
                return;

            string deletedPresetName = string.IsNullOrWhiteSpace(preset.preset_name) ? "Preset" : preset.preset_name.Trim();
            if (runtime.DeletePreset(preset.preset_id))
            {
                RefreshUi(syncControls: true);
                ShowActionToast($"Deleted preset \"{deletedPresetName}\".", true);
            }
        })
        {
            text = assigningPreset ? "Select" : "X"
        };
        if (assigningPreset)
            StylePresetSelectButton(deleteButton);
        else
            StyleTextDeleteButton(deleteButton);
        deleteButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        deleteButton.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
        content.Add(deleteButton);

        Action<bool> applyPresetHover = hovered =>
        {
            if (!selected)
            {
                row.style.backgroundColor = hovered ? new Color(1f, 1f, 1f, 0.08f) : new Color(0f, 0f, 0f, 0f);
                nameLabel.style.color = hovered ? Color.white : new Color(0.96f, 0.97f, 0.99f, 0.98f);
            }
        };
        row.RegisterCallback<MouseEnterEvent>(_ => applyPresetHover(true));
        row.RegisterCallback<MouseLeaveEvent>(_ => applyPresetHover(false));

        RegisterNavigationItem(presetNavigationItems, row, presetListScroll, rowAction, applyPresetHover);
        return row;
    }

    private static Color GetPresetContentAccentColor(UnityToneLabRuntime.ToneLabPreset preset)
    {
        bool hasLv2 = false;
        bool hasNam = false;
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain = preset?.pedal_chain;
        if (chain != null)
        {
            for (int i = 0; i < chain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = chain[i];
                if (slot == null)
                    continue;

                if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin)
                    hasLv2 = true;
                else if (slot.pedal_type == UnityToneLabRuntime.ToneLabPedalType.NamModel)
                    hasNam = true;
            }
        }

        if (hasLv2 && hasNam)
            return new Color(0.68f, 0.36f, 1f, 1f);
        if (hasNam)
            return new Color(1f, 0.72f, 0.10f, 1f);
        if (hasLv2)
            return new Color(1f, 0.22f, 0.28f, 1f);

        return new Color(0.20f, 0.84f, 0.46f, 1f);
    }

    private void RefreshPedalLibrary(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (pedalLibraryHost == null)
            return;

        pedalLibraryHost.Clear();
        libraryNavigationItems.Clear();

        string normalizedQuery = NormalizeSearchQuery(librarySearchQuery);
        int visibleCount = 0;
        IReadOnlyList<IToneLabPedalDescriptor> availablePedals = ToneLabPedalRegistry.AllDescriptors;
        for (int i = 0; i < availablePedals.Count; i++)
        {
            IToneLabPedalDescriptor descriptor = availablePedals[i];
            if (descriptor == null || !MatchesLibraryFilter(descriptor, libraryFilter) || !MatchesLibrarySearch(descriptor, normalizedQuery))
                continue;

            Action addPedalAction = () =>
            {
                selectedPedalInstanceId = runtime?.AddPedalToChain(descriptor.DescriptorId) ?? string.Empty;
                sidePanelMode = ToneLabSidePanelMode.Details;
                ResetNavigation(ToneLabNavigationZone.PedalBoard);
                RefreshUi(syncControls: true);
            };
            ToneLabPedalLibraryItem libraryItem = new ToneLabPedalLibraryItem(
                descriptor,
                addPedalAction);
            RegisterLibraryItemDrag(libraryItem, descriptor.DescriptorId);
            pedalLibraryHost.Add(libraryItem);
            RegisterNavigationItem(libraryNavigationItems, libraryItem, pedalLibraryScroll, addPedalAction, libraryItem.SetControllerHovered);
            visibleCount++;
        }

        if (visibleCount == 0)
        {
            Label emptyLabel = new Label("No matching effects.");
            emptyLabel.style.color = new Color(0.90f, 0.92f, 0.96f, 0.72f);
            emptyLabel.style.fontSize = 17f;
            emptyLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            emptyLabel.style.marginTop = 18f;
            pedalLibraryHost.Add(emptyLabel);
        }
    }

    private void RefreshSidePanel(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> pedalChain)
    {
        if (sidePanelHost == null)
            return;

        UnityToneLabRuntime.ToneLabPedalSlot selectedSlot = null;
        if (pedalChain != null)
        {
            for (int i = 0; i < pedalChain.Count; i++)
            {
                UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
                if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
                {
                    selectedSlot = slot;
                    break;
                }
            }
        }

        RefreshSidePanelButtonStates();
        sidePanelHost.Clear();
        switch (sidePanelMode)
        {
            case ToneLabSidePanelMode.Presets:
                sidePanelHost.Add(CreateSidebarHeader("Presets", null, createPresetButton));
                presetSearchRoot?.RemoveFromHierarchy();
                sidePanelHost.Add(presetSearchRoot);
                presetListScroll?.RemoveFromHierarchy();
                sidePanelHost.Add(presetListScroll);
                break;
            case ToneLabSidePanelMode.Library:
                sidePanelHost.Add(CreateSidebarHeader("Library", null, null));
                RefreshLibraryFilterButtonStates();
                libraryFilterRoot?.RemoveFromHierarchy();
                sidePanelHost.Add(libraryFilterRoot);
                librarySearchRoot?.RemoveFromHierarchy();
                sidePanelHost.Add(librarySearchRoot);
                pedalLibraryScroll?.RemoveFromHierarchy();
                sidePanelHost.Add(pedalLibraryScroll);
                break;
            case ToneLabSidePanelMode.Details:
                IToneLabPedalDescriptor descriptor = selectedSlot != null ? ToneLabPedalRegistry.GetDescriptor(selectedSlot) : null;
                sidePanelHost.Add(CreateSidebarHeader(descriptor?.DisplayName ?? "Details", descriptor?.Description ?? "Pedal settings", null));
                RebuildPedalInspector();
                pedalInspectorScroll?.RemoveFromHierarchy();
                sidePanelHost.Add(pedalInspectorScroll);
                break;
        }
    }

    private void RefreshSidePanelButtonStates()
    {
        ApplySidebarTabState(presetsTabButton, sidePanelMode == ToneLabSidePanelMode.Presets, StartupLogoColor(1));
        ApplySidebarTabState(libraryTabButton, sidePanelMode == ToneLabSidePanelMode.Library, StartupLogoColor(2));
        ApplySidebarTabState(detailsTabButton, sidePanelMode == ToneLabSidePanelMode.Details, StartupLogoColor(4));
    }

    private void SetSidePanelMode(ToneLabSidePanelMode mode)
    {
        sidePanelMode = mode;
        ResetNavigation(ToneLabNavigationZone.Sidebar);
        RefreshUi(syncControls: true);
    }

    private void RegisterFooterNavigationItems()
    {
        footerNavigationItems.Clear();
        RegisterNavigationItem(footerNavigationItems, backButton, null, RequestCloseFromUi, CreateButtonNavigationHover(backButton));
        RegisterNavigationItem(footerNavigationItems, effectsFolderButton, null, OpenEffectsFolder, CreateButtonNavigationHover(effectsFolderButton));
        RegisterNavigationItem(footerNavigationItems, songMappingButton, null, ToggleSongMappingMode, CreateButtonNavigationHover(songMappingButton));
        RegisterNavigationItem(footerNavigationItems, resetAllButton, null, () => OpenPresetModal(ToneLabPresetModalMode.ResetAll), CreateButtonNavigationHover(resetAllButton));
        RegisterNavigationItem(footerNavigationItems, savePresetButton, null, () =>
        {
            if (runtime == null || string.IsNullOrWhiteSpace(runtime.CurrentPresetId))
                return;

            runtime.SaveCurrentToPreset(runtime.CurrentPresetId);
            RefreshUi(syncControls: true);
            ShowActionToast($"Saved preset \"{GetPresetName(runtime.CurrentPresetId)}\".");
        }, CreateButtonNavigationHover(savePresetButton));
        RegisterNavigationItem(footerNavigationItems, saveAsPresetButton, null, () => OpenPresetModal(ToneLabPresetModalMode.SaveAs), CreateButtonNavigationHover(saveAsPresetButton));
    }

    private static void RegisterNavigationItem(
        List<ToneLabNavigationItem> items,
        VisualElement element,
        ScrollView scrollView,
        Action activate,
        Action<bool> setHovered = null)
    {
        if (items == null || element == null)
            return;

        items.Add(new ToneLabNavigationItem
        {
            element = element,
            scrollView = scrollView,
            activate = activate,
            setHovered = setHovered
        });
    }

    private static Action<bool> GetNavigationHoverSetter(VisualElement element)
    {
        return element?.userData as Action<bool>;
    }

    private static Action<bool> CreateButtonNavigationHover(Button button)
    {
        if (button == null)
            return null;

        return hovered =>
        {
            if (button == null)
                return;

            button.style.scale = hovered ? new Scale(new Vector3(1.04f, 1.04f, 1f)) : new Scale(Vector3.one);
            button.style.opacity = hovered ? 1f : 0.96f;
        };
    }

    private void ResetNavigation(ToneLabNavigationZone zone)
    {
        navigationZone = zone;
        navigationIndex = 0;
        heldNavigationDirection = ToneLabNavigationDirection.None;
        nextNavigationRepeatTime = -1f;
        RefreshNavigationHighlight();
    }

    private void HandleNavigationInput(bool suppressSubmit)
    {
        if (openingRefreshInProgress || IsCapturingKeyboardInput)
            return;

        if (TryConsumeNavigationDirection(out ToneLabNavigationDirection direction))
        {
            if (direction != ToneLabNavigationDirection.None)
                controllerCursorPointerMode = false;
            MoveNavigation(direction);
        }

        if (!suppressSubmit && IsNavigationSubmitPressed())
            ActivateCurrentNavigationItem();
    }

    private bool TryConsumeNavigationDirection(out ToneLabNavigationDirection direction)
    {
        direction = ReadNavigationPressedThisFrame();
        if (direction != ToneLabNavigationDirection.None)
        {
            heldNavigationDirection = direction;
            nextNavigationRepeatTime = Time.unscaledTime + NavigationInitialRepeatDelay;
            return true;
        }

        direction = ReadHeldNavigationDirection();
        if (direction == ToneLabNavigationDirection.None)
        {
            heldNavigationDirection = ToneLabNavigationDirection.None;
            nextNavigationRepeatTime = -1f;
            return false;
        }

        if (direction != heldNavigationDirection)
        {
            heldNavigationDirection = direction;
            nextNavigationRepeatTime = Time.unscaledTime + NavigationInitialRepeatDelay;
            return true;
        }

        if (nextNavigationRepeatTime > 0f && Time.unscaledTime >= nextNavigationRepeatTime)
        {
            nextNavigationRepeatTime = Time.unscaledTime + NavigationRepeatDelay;
            return true;
        }

        return false;
    }

    private static ToneLabNavigationDirection ReadNavigationPressedThisFrame()
    {
        if (Input.GetKeyDown(KeyCode.UpArrow))
            return ToneLabNavigationDirection.Up;
        if (Input.GetKeyDown(KeyCode.DownArrow))
            return ToneLabNavigationDirection.Down;
        if (Input.GetKeyDown(KeyCode.LeftArrow))
            return ToneLabNavigationDirection.Left;
        if (Input.GetKeyDown(KeyCode.RightArrow))
            return ToneLabNavigationDirection.Right;

#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            if (gamepad.dpad.up.wasPressedThisFrame)
                return ToneLabNavigationDirection.Up;
            if (gamepad.dpad.down.wasPressedThisFrame)
                return ToneLabNavigationDirection.Down;
            if (gamepad.dpad.left.wasPressedThisFrame)
                return ToneLabNavigationDirection.Left;
            if (gamepad.dpad.right.wasPressedThisFrame)
                return ToneLabNavigationDirection.Right;
        }
#endif

        if (!HasInputSystemGamepadConnected())
        {
            if (Input.GetKeyDown(KeyCode.JoystickButton13))
                return ToneLabNavigationDirection.Up;
            if (Input.GetKeyDown(KeyCode.JoystickButton14))
                return ToneLabNavigationDirection.Down;
            if (Input.GetKeyDown(KeyCode.JoystickButton15))
                return ToneLabNavigationDirection.Left;
            if (Input.GetKeyDown(KeyCode.JoystickButton16))
                return ToneLabNavigationDirection.Right;
        }

        return ToneLabNavigationDirection.None;
    }

    private static ToneLabNavigationDirection ReadHeldNavigationDirection()
    {
        Vector2 axis = Vector2.zero;
        if (Input.GetKey(KeyCode.LeftArrow))
            axis.x -= 1f;
        if (Input.GetKey(KeyCode.RightArrow))
            axis.x += 1f;
        if (Input.GetKey(KeyCode.UpArrow))
            axis.y += 1f;
        if (Input.GetKey(KeyCode.DownArrow))
            axis.y -= 1f;

#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            Vector2 dpad = gamepad.dpad.ReadValue();
            if (dpad.sqrMagnitude > axis.sqrMagnitude)
                axis = dpad;
        }
#endif

        if (!HasInputSystemGamepadConnected())
        {
            Vector2 dpadAxis = new Vector2(
                ReadStrongestAxis("DPadX", "DPad Horizontal"),
                ReadStrongestAxis("DPadY", "DPad Vertical"));
            if (dpadAxis.sqrMagnitude > axis.sqrMagnitude)
                axis = dpadAxis;

            if (Input.GetKey(KeyCode.JoystickButton15))
                axis.x = -1f;
            else if (Input.GetKey(KeyCode.JoystickButton16))
                axis.x = 1f;

            if (Input.GetKey(KeyCode.JoystickButton13))
                axis.y = 1f;
            else if (Input.GetKey(KeyCode.JoystickButton14))
                axis.y = -1f;
        }

        if (Mathf.Abs(axis.x) < NavigationAxisThreshold && Mathf.Abs(axis.y) < NavigationAxisThreshold)
            return ToneLabNavigationDirection.None;

        if (Mathf.Abs(axis.x) > Mathf.Abs(axis.y))
            return axis.x < 0f ? ToneLabNavigationDirection.Left : ToneLabNavigationDirection.Right;

        return axis.y < 0f ? ToneLabNavigationDirection.Down : ToneLabNavigationDirection.Up;
    }

    private static bool IsNavigationSubmitPressed()
    {
        if (Input.GetKeyDown(KeyCode.Return) || Input.GetKeyDown(KeyCode.KeypadEnter) || Input.GetKeyDown(KeyCode.Space))
            return true;

        return false;
    }

    private void MoveNavigation(ToneLabNavigationDirection direction)
    {
        if (direction == ToneLabNavigationDirection.None)
            return;

        CoerceNavigationZone();
        switch (navigationZone)
        {
            case ToneLabNavigationZone.Sidebar:
                MoveSidebarNavigation(direction);
                break;
            case ToneLabNavigationZone.PedalBoard:
                MovePedalBoardNavigation(direction);
                break;
            case ToneLabNavigationZone.SongMappingLeft:
                MoveSongMappingLeftNavigation(direction);
                break;
            case ToneLabNavigationZone.SongMappingTones:
                MoveSongMappingToneNavigation(direction);
                break;
            case ToneLabNavigationZone.Footer:
                MoveFooterNavigation(direction);
                break;
        }

        RefreshNavigationHighlight();
    }

    private void MoveSidebarNavigation(ToneLabNavigationDirection direction)
    {
        List<ToneLabNavigationItem> items = GetActiveSidebarNavigationItems();
        if (direction == ToneLabNavigationDirection.Up || direction == ToneLabNavigationDirection.Down)
        {
            if (items.Count == 0 && direction == ToneLabNavigationDirection.Down)
            {
                SetNavigationZone(ToneLabNavigationZone.Footer);
                return;
            }

            MoveNavigationIndex(items, direction == ToneLabNavigationDirection.Down ? 1 : -1);
            return;
        }

        if (direction == ToneLabNavigationDirection.Right)
        {
            if (boardMode == ToneLabBoardMode.SongMapping)
                SetNavigationZone(songMappingLeftNavigationItems.Count > 0 ? ToneLabNavigationZone.SongMappingLeft : ToneLabNavigationZone.SongMappingTones);
            else
                SetNavigationZone(ToneLabNavigationZone.PedalBoard);
            return;
        }
    }

    private void MovePedalBoardNavigation(ToneLabNavigationDirection direction)
    {
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain = runtime?.CurrentPedalChain;
        int count = chain?.Count ?? 0;
        if (count == 0)
        {
            if (direction == ToneLabNavigationDirection.Left)
                SetNavigationZone(ToneLabNavigationZone.Sidebar);
            else if (direction == ToneLabNavigationDirection.Down)
                SetNavigationZone(ToneLabNavigationZone.Footer);
            return;
        }

        int currentIndex = GetSelectedPedalIndex(chain);
        int columns = Mathf.Max(1, pedalBoardView?.GetEstimatedColumns() ?? 1);
        int nextIndex = currentIndex;
        switch (direction)
        {
            case ToneLabNavigationDirection.Left:
                if (currentIndex % columns == 0)
                {
                    SetNavigationZone(ToneLabNavigationZone.Sidebar);
                    return;
                }
                nextIndex = currentIndex - 1;
                break;
            case ToneLabNavigationDirection.Right:
                nextIndex = Mathf.Min(count - 1, currentIndex + 1);
                break;
            case ToneLabNavigationDirection.Up:
                nextIndex = Mathf.Max(0, currentIndex - columns);
                break;
            case ToneLabNavigationDirection.Down:
                if (currentIndex + columns >= count)
                {
                    SetNavigationZone(ToneLabNavigationZone.Footer);
                    return;
                }
                nextIndex = currentIndex + columns;
                break;
        }

        SelectPedalByIndex(nextIndex, openDetails: false);
    }

    private void MoveSongMappingLeftNavigation(ToneLabNavigationDirection direction)
    {
        if (direction == ToneLabNavigationDirection.Up || direction == ToneLabNavigationDirection.Down)
        {
            MoveNavigationIndex(songMappingLeftNavigationItems, direction == ToneLabNavigationDirection.Down ? 1 : -1);
            return;
        }

        if (direction == ToneLabNavigationDirection.Left)
        {
            SetNavigationZone(ToneLabNavigationZone.Sidebar);
            return;
        }

        if (direction == ToneLabNavigationDirection.Right && songMappingToneNavigationItems.Count > 0)
            SetNavigationZone(ToneLabNavigationZone.SongMappingTones);
    }

    private void MoveSongMappingToneNavigation(ToneLabNavigationDirection direction)
    {
        if (direction == ToneLabNavigationDirection.Up || direction == ToneLabNavigationDirection.Down)
        {
            if (direction == ToneLabNavigationDirection.Down && navigationIndex >= songMappingToneNavigationItems.Count - 1)
            {
                SetNavigationZone(ToneLabNavigationZone.Footer);
                return;
            }

            MoveNavigationIndex(songMappingToneNavigationItems, direction == ToneLabNavigationDirection.Down ? 1 : -1);
            return;
        }

        if (direction == ToneLabNavigationDirection.Left)
            SetNavigationZone(ToneLabNavigationZone.SongMappingLeft);
    }

    private void MoveFooterNavigation(ToneLabNavigationDirection direction)
    {
        List<ToneLabNavigationItem> items = GetVisibleFooterNavigationItems();
        if (direction == ToneLabNavigationDirection.Left || direction == ToneLabNavigationDirection.Right)
        {
            MoveNavigationIndex(items, direction == ToneLabNavigationDirection.Right ? 1 : -1);
            return;
        }

        if (direction == ToneLabNavigationDirection.Up)
        {
            if (boardMode == ToneLabBoardMode.SongMapping)
                SetNavigationZone(songMappingToneNavigationItems.Count > 0 ? ToneLabNavigationZone.SongMappingTones : ToneLabNavigationZone.SongMappingLeft);
            else
                SetNavigationZone(HasPedalBoardItems() ? ToneLabNavigationZone.PedalBoard : ToneLabNavigationZone.Sidebar);
        }
    }

    private void MoveNavigationIndex(List<ToneLabNavigationItem> items, int delta)
    {
        if (items == null || items.Count == 0)
        {
            navigationIndex = 0;
            return;
        }

        navigationIndex = (navigationIndex + delta + items.Count) % items.Count;
    }

    private void SetNavigationZone(ToneLabNavigationZone zone)
    {
        navigationZone = zone;
        navigationIndex = 0;
        CoerceNavigationZone();
    }

    private void CoerceNavigationZone()
    {
        if (boardMode == ToneLabBoardMode.Pedalboard &&
            (navigationZone == ToneLabNavigationZone.SongMappingLeft || navigationZone == ToneLabNavigationZone.SongMappingTones))
        {
            navigationZone = ToneLabNavigationZone.PedalBoard;
            navigationIndex = 0;
        }
        else if (boardMode == ToneLabBoardMode.SongMapping && navigationZone == ToneLabNavigationZone.PedalBoard)
        {
            navigationZone = ToneLabNavigationZone.SongMappingLeft;
            navigationIndex = 0;
        }

        List<ToneLabNavigationItem> items = GetNavigationItemsForZone(navigationZone);
        if (navigationZone != ToneLabNavigationZone.PedalBoard)
            navigationIndex = Mathf.Clamp(navigationIndex, 0, Mathf.Max(0, (items?.Count ?? 0) - 1));

        if (navigationZone == ToneLabNavigationZone.SongMappingLeft && (items?.Count ?? 0) == 0 && songMappingToneNavigationItems.Count > 0)
        {
            navigationZone = ToneLabNavigationZone.SongMappingTones;
            navigationIndex = Mathf.Clamp(navigationIndex, 0, Mathf.Max(0, songMappingToneNavigationItems.Count - 1));
        }
    }

    private void ActivateCurrentNavigationItem()
    {
        CoerceNavigationZone();
        if (navigationZone == ToneLabNavigationZone.PedalBoard)
        {
            if (!HasPedalBoardItems())
            {
                SetSidePanelMode(ToneLabSidePanelMode.Library);
                return;
            }

            OpenSelectedPedalDetailsFromNavigation();
            return;
        }

        List<ToneLabNavigationItem> items = GetNavigationItemsForZone(navigationZone);
        if (items == null || items.Count == 0)
            return;

        ToneLabNavigationItem item = items[Mathf.Clamp(navigationIndex, 0, items.Count - 1)];
        item.activate?.Invoke();
    }

    private List<ToneLabNavigationItem> GetNavigationItemsForZone(ToneLabNavigationZone zone)
    {
        switch (zone)
        {
            case ToneLabNavigationZone.Sidebar:
                return GetActiveSidebarNavigationItems();
            case ToneLabNavigationZone.SongMappingLeft:
                return songMappingLeftNavigationItems;
            case ToneLabNavigationZone.SongMappingTones:
                return songMappingToneNavigationItems;
            case ToneLabNavigationZone.Footer:
                return GetVisibleFooterNavigationItems();
            default:
                return null;
        }
    }

    private List<ToneLabNavigationItem> GetActiveSidebarNavigationItems()
    {
        switch (sidePanelMode)
        {
            case ToneLabSidePanelMode.Library:
                return libraryNavigationItems;
            case ToneLabSidePanelMode.Details:
                return inspectorNavigationItems;
            default:
                return presetNavigationItems;
        }
    }

    private List<ToneLabNavigationItem> GetVisibleFooterNavigationItems()
    {
        visibleFooterNavigationItems.Clear();
        for (int i = 0; i < footerNavigationItems.Count; i++)
        {
            ToneLabNavigationItem item = footerNavigationItems[i];
            if (IsNavigationElementVisible(item?.element))
                visibleFooterNavigationItems.Add(item);
        }

        return visibleFooterNavigationItems;
    }

    private static bool IsNavigationElementVisible(VisualElement element)
    {
        return element != null &&
               element.style.display != DisplayStyle.None &&
               element.enabledInHierarchy;
    }

    private bool HasPedalBoardItems()
    {
        return runtime?.CurrentPedalChain != null && runtime.CurrentPedalChain.Length > 0;
    }

    private int GetSelectedPedalIndex(IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain)
    {
        if (chain == null || chain.Count == 0)
            return 0;

        for (int i = 0; i < chain.Count; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = chain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
                return i;
        }

        selectedPedalInstanceId = chain[0]?.pedal_instance_id ?? string.Empty;
        return 0;
    }

    private void SelectPedalByIndex(int index, bool openDetails)
    {
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain = runtime?.CurrentPedalChain;
        if (chain == null || chain.Count == 0)
            return;

        int clampedIndex = Mathf.Clamp(index, 0, chain.Count - 1);
        selectedPedalInstanceId = chain[clampedIndex]?.pedal_instance_id ?? string.Empty;
        if (openDetails)
        {
            sidePanelMode = ToneLabSidePanelMode.Details;
            RefreshUi(syncControls: true);
            return;
        }

        pedalBoardView?.Refresh(chain, selectedPedalInstanceId);
        pedalBoardView?.ScrollPedalIntoView(selectedPedalInstanceId);
    }

    private void OpenSelectedPedalDetailsFromNavigation()
    {
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> chain = runtime?.CurrentPedalChain;
        if (chain == null || chain.Count == 0)
            return;

        SelectPedalByIndex(GetSelectedPedalIndex(chain), openDetails: true);
    }

    private void RefreshNavigationHighlight()
    {
        ClearNavigationHighlight();
        if (!isVisible)
            return;

        CoerceNavigationZone();
        if (navigationZone == ToneLabNavigationZone.PedalBoard)
        {
            pedalBoardView?.ScrollPedalIntoView(selectedPedalInstanceId);
            return;
        }

        List<ToneLabNavigationItem> items = GetNavigationItemsForZone(navigationZone);
        if (items == null || items.Count == 0)
            return;

        ToneLabNavigationItem item = items[Mathf.Clamp(navigationIndex, 0, items.Count - 1)];
        if (item == null || item.element == null)
            return;

        navigationHighlightedItem = item;
        item.setHovered?.Invoke(true);
        item.scrollView?.ScrollTo(item.element);
    }

    private void ClearNavigationHighlight()
    {
        if (navigationHighlightedItem == null)
            return;

        navigationHighlightedItem.setHovered?.Invoke(false);
        navigationHighlightedItem = null;
    }

    private static float ReadStrongestAxis(params string[] axisNames)
    {
        float strongest = 0f;
        if (axisNames == null)
            return strongest;

        for (int i = 0; i < axisNames.Length; i++)
        {
            float value = TryGetAxisRaw(axisNames[i]);
            if (Mathf.Abs(value) > Mathf.Abs(strongest))
                strongest = value;
        }

        return Mathf.Clamp(strongest, -1f, 1f);
    }

    private static float TryGetAxisRaw(string axisName)
    {
        if (string.IsNullOrWhiteSpace(axisName))
            return 0f;

        try
        {
            return Input.GetAxisRaw(axisName);
        }
        catch (ArgumentException)
        {
            return 0f;
        }
    }

    private static bool TryGetButtonDown(string buttonName)
    {
        if (string.IsNullOrWhiteSpace(buttonName))
            return false;

        try
        {
            return Input.GetButtonDown(buttonName);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }

    private static bool HasInputSystemGamepadConnected()
    {
#if ENABLE_INPUT_SYSTEM
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad != null)
                return true;
        }
#endif
        return false;
    }

    private VisualElement CreateSidebarHeader(string title, string subtitle, VisualElement trailingElement)
    {
        VisualElement header = new VisualElement();
        header.style.flexDirection = FlexDirection.Row;
        header.style.alignItems = Align.Center;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.flexShrink = 0f;
        header.style.marginBottom = 14f;
        header.style.paddingBottom = 12f;
        header.style.borderBottomWidth = 1f;
        header.style.borderBottomColor = new Color(1f, 1f, 1f, 0.16f);

        VisualElement copyColumn = new VisualElement();
        copyColumn.style.flexGrow = 1f;
        copyColumn.style.minWidth = 0f;
        copyColumn.style.marginRight = 12f;
        header.Add(copyColumn);

        Label titleLabel = new Label(title ?? string.Empty);
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 22f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.whiteSpace = WhiteSpace.NoWrap;
        copyColumn.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.style.color = new Color(0.84f, 0.86f, 0.90f, 0.72f);
            subtitleLabel.style.fontSize = 12f;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            subtitleLabel.style.marginTop = 2f;
            copyColumn.Add(subtitleLabel);
        }

        if (trailingElement != null)
        {
            trailingElement.RemoveFromHierarchy();
            header.Add(trailingElement);
        }

        return header;
    }

    private VisualElement CreateLibraryFilterBar()
    {
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.SpaceBetween;
        root.style.flexShrink = 0f;
        root.style.marginBottom = 10f;

        VisualElement filters = new VisualElement();
        filters.style.flexDirection = FlexDirection.Row;
        filters.style.alignItems = Align.Center;
        filters.style.flexGrow = 1f;
        filters.style.minWidth = 0f;
        root.Add(filters);

        libraryAllFilterButton = CreateLibraryFilterButton("All", ToneLabLibraryFilter.All);
        libraryBuiltInFilterButton = CreateLibraryFilterButton("Built-In", ToneLabLibraryFilter.BuiltIn);
        libraryLv2FilterButton = CreateLibraryFilterButton("LV2", ToneLabLibraryFilter.Lv2);
        libraryNamFilterButton = CreateLibraryFilterButton("NAM", ToneLabLibraryFilter.Nam);
        filters.Add(libraryAllFilterButton);
        filters.Add(libraryBuiltInFilterButton);
        filters.Add(libraryLv2FilterButton);
        filters.Add(libraryNamFilterButton);

        libraryRefreshButton = CreateButton("Refresh", "tone-lab-button tone-lab-button-secondary", RefreshExternalEffectsFromUi);
        libraryRefreshButton.style.minWidth = 86f;
        libraryRefreshButton.style.width = 86f;
        libraryRefreshButton.style.height = 34f;
        libraryRefreshButton.style.marginRight = 0f;
        libraryRefreshButton.style.paddingLeft = 8f;
        libraryRefreshButton.style.paddingRight = 8f;
        libraryRefreshButton.style.fontSize = 12f;
        root.Add(libraryRefreshButton);

        RefreshLibraryFilterButtonStates();
        return root;
    }

    private VisualElement CreateSongMappingFilterBar()
    {
        VisualElement root = new VisualElement();
        root.style.flexDirection = FlexDirection.Row;
        root.style.alignItems = Align.Center;
        root.style.flexShrink = 0f;
        root.style.marginBottom = 10f;

        songMappingAllFilterButton = CreateSongMappingFilterButton("All", ToneLabSongMappingBrowseMode.All);
        songMappingArtistsFilterButton = CreateSongMappingFilterButton("Artists", ToneLabSongMappingBrowseMode.Artists);
        songMappingAlbumsFilterButton = CreateSongMappingFilterButton("Albums", ToneLabSongMappingBrowseMode.Albums);
        root.Add(songMappingAllFilterButton);
        root.Add(songMappingArtistsFilterButton);
        root.Add(songMappingAlbumsFilterButton);

        RefreshSongMappingFilterButtonStates();
        return root;
    }

    private Button CreateSongMappingFilterButton(string label, ToneLabSongMappingBrowseMode mode)
    {
        Button button = new Button(() => SetSongMappingBrowseMode(mode)) { text = label };
        button.style.minWidth = label.Length > 4 ? 86f : 56f;
        button.style.height = 34f;
        button.style.marginRight = 6f;
        button.style.paddingLeft = 8f;
        button.style.paddingRight = 8f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = new Color(0.88f, 0.91f, 0.95f, 0.86f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
        button.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
        button.style.borderBottomColor = new Color(1f, 1f, 1f, 0.10f);
        button.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
        button.style.fontSize = 12f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            bool selected = button.userData is bool isSelected && isSelected;
            if (!selected)
            {
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                button.style.color = Color.white;
            }
        });
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplySongMappingFilterButtonState(button, button.userData is bool selected && selected));
        return button;
    }

    private void SetSongMappingBrowseMode(ToneLabSongMappingBrowseMode mode)
    {
        if (songMappingBrowseMode == mode && string.IsNullOrWhiteSpace(songMappingBrowseScopeKey))
            return;

        songMappingBrowseMode = mode;
        songMappingBrowseScopeKey = string.Empty;
        RefreshSongMappingFilterButtonStates();
        RefreshSongMappingView();
    }

    private void RefreshSongMappingFilterButtonStates()
    {
        ApplySongMappingFilterButtonState(songMappingAllFilterButton, songMappingBrowseMode == ToneLabSongMappingBrowseMode.All);
        ApplySongMappingFilterButtonState(songMappingArtistsFilterButton, songMappingBrowseMode == ToneLabSongMappingBrowseMode.Artists);
        ApplySongMappingFilterButtonState(songMappingAlbumsFilterButton, songMappingBrowseMode == ToneLabSongMappingBrowseMode.Albums);
    }

    private static void ApplySongMappingFilterButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        button.userData = selected;
        if (selected)
        {
            button.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.92f);
            button.style.color = Color.white;
            button.style.borderTopColor = new Color(1f, 0.88f, 0.48f, 0.90f);
            button.style.borderRightColor = new Color(1f, 0.56f, 0.38f, 0.82f);
            button.style.borderBottomColor = new Color(0.82f, 0.32f, 0.22f, 0.78f);
            button.style.borderLeftColor = new Color(1f, 0.72f, 0.22f, 0.82f);
        }
        else
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.88f, 0.91f, 0.95f, 0.86f);
            button.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
            button.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
            button.style.borderBottomColor = new Color(1f, 1f, 1f, 0.10f);
            button.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        }
    }

    private Button CreateLibraryFilterButton(string label, ToneLabLibraryFilter filter)
    {
        Button button = new Button(() => SetLibraryFilter(filter)) { text = label };
        button.style.minWidth = label.Length > 4 ? 82f : 56f;
        button.style.height = 34f;
        button.style.marginRight = 6f;
        button.style.paddingLeft = 8f;
        button.style.paddingRight = 8f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = new Color(0.88f, 0.91f, 0.95f, 0.86f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
        button.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
        button.style.borderBottomColor = new Color(1f, 1f, 1f, 0.10f);
        button.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
        button.style.fontSize = 12f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            bool selected = button.userData is bool isSelected && isSelected;
            if (!selected)
            {
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
                button.style.color = Color.white;
            }
        });
        button.RegisterCallback<MouseLeaveEvent>(_ => ApplyLibraryFilterButtonState(button, button.userData is bool selected && selected));
        return button;
    }

    private void SetLibraryFilter(ToneLabLibraryFilter filter)
    {
        if (libraryFilter == filter)
            return;

        libraryFilter = filter;
        RefreshLibraryFilterButtonStates();
        if (runtime != null)
            RefreshPedalLibrary(runtime.CurrentPedalChain);
    }

    private void RefreshLibraryFilterButtonStates()
    {
        ApplyLibraryFilterButtonState(libraryAllFilterButton, libraryFilter == ToneLabLibraryFilter.All);
        ApplyLibraryFilterButtonState(libraryBuiltInFilterButton, libraryFilter == ToneLabLibraryFilter.BuiltIn);
        ApplyLibraryFilterButtonState(libraryLv2FilterButton, libraryFilter == ToneLabLibraryFilter.Lv2);
        ApplyLibraryFilterButtonState(libraryNamFilterButton, libraryFilter == ToneLabLibraryFilter.Nam);
    }

    private static void ApplyLibraryFilterButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        button.userData = selected;
        if (selected)
        {
            button.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.92f);
            button.style.color = Color.white;
            button.style.borderTopColor = new Color(1f, 0.88f, 0.48f, 0.90f);
            button.style.borderRightColor = new Color(1f, 0.56f, 0.38f, 0.82f);
            button.style.borderBottomColor = new Color(0.82f, 0.32f, 0.22f, 0.78f);
            button.style.borderLeftColor = new Color(1f, 0.72f, 0.22f, 0.82f);
        }
        else
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.88f, 0.91f, 0.95f, 0.86f);
            button.style.borderTopColor = new Color(1f, 1f, 1f, 0.18f);
            button.style.borderRightColor = new Color(1f, 1f, 1f, 0.14f);
            button.style.borderBottomColor = new Color(1f, 1f, 1f, 0.10f);
            button.style.borderLeftColor = new Color(1f, 1f, 1f, 0.14f);
        }
    }

    private VisualElement CreateSidebarSearchBlock(string placeholderText, out TextField searchField, Action<string> onQueryChanged)
    {
        VisualElement root = new VisualElement();
        root.style.flexShrink = 0f;
        root.style.height = 44f;
        root.style.marginBottom = 14f;
        root.style.position = Position.Relative;
        root.style.borderBottomWidth = 1f;
        root.style.borderBottomColor = new Color(1f, 1f, 1f, 0.30f);

        TextField field = new TextField();
        searchField = field;
        field.isDelayed = false;
        field.style.position = Position.Absolute;
        field.style.left = 0f;
        field.style.right = 0f;
        field.style.top = 0f;
        field.style.bottom = 0f;
        field.style.height = 44f;
        field.style.minHeight = 44f;
        field.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        field.style.color = Color.white;
        field.style.borderTopWidth = 0f;
        field.style.borderRightWidth = 0f;
        field.style.borderBottomWidth = 0f;
        field.style.borderLeftWidth = 0f;
        field.style.paddingLeft = 0f;
        field.style.paddingRight = 0f;
        field.RegisterCallback<AttachToPanelEvent>(_ => ApplySearchFieldStyle(field));

        Label placeholderLabel = new Label(placeholderText);
        placeholderLabel.pickingMode = PickingMode.Ignore;
        placeholderLabel.style.position = Position.Absolute;
        placeholderLabel.style.left = 0f;
        placeholderLabel.style.right = 0f;
        placeholderLabel.style.top = 0f;
        placeholderLabel.style.bottom = 0f;
        placeholderLabel.style.color = Color.white;
        placeholderLabel.style.fontSize = 20f;
        placeholderLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        placeholderLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        placeholderLabel.style.opacity = 0.72f;

        void RefreshPlaceholder()
        {
            bool hasValue = !string.IsNullOrWhiteSpace(field.value);
            bool isFocused = IsTextFieldFocused(field);
            placeholderLabel.style.display = hasValue || isFocused ? DisplayStyle.None : DisplayStyle.Flex;
            root.style.borderBottomColor = isFocused
                ? new Color(1f, 0.67f, 0.18f, 0.95f)
                : new Color(1f, 1f, 1f, 0.30f);
        }

        field.RegisterCallback<FocusInEvent>(_ =>
        {
            placeholderLabel.style.display = DisplayStyle.None;
            root.style.borderBottomColor = new Color(1f, 0.67f, 0.18f, 0.95f);
        });
        field.RegisterCallback<FocusOutEvent>(_ => RefreshPlaceholder());
        field.RegisterCallback<MouseEnterEvent>(_ =>
        {
            if (!IsTextFieldFocused(field))
                root.style.borderBottomColor = new Color(1f, 1f, 1f, 0.46f);
        });
        field.RegisterCallback<MouseLeaveEvent>(_ => RefreshPlaceholder());
        field.RegisterValueChangedCallback(evt =>
        {
            RefreshPlaceholder();
            onQueryChanged?.Invoke(evt.newValue);
        });
        root.Add(field);
        root.Add(placeholderLabel);
        root.RegisterCallback<AttachToPanelEvent>(_ => RefreshPlaceholder());

        return root;
    }

    private static string NormalizeSearchQuery(string query)
    {
        return string.IsNullOrWhiteSpace(query) ? string.Empty : query.Trim();
    }

    private static string TruncateWithEllipsis(string value, int maxCharacters)
    {
        if (string.IsNullOrWhiteSpace(value))
            return string.Empty;

        string trimmed = value.Trim();
        if (maxCharacters <= 3 || trimmed.Length <= maxCharacters)
            return trimmed;

        return trimmed.Substring(0, maxCharacters - 3).TrimEnd() + "...";
    }

    private static bool IsTextFieldFocused(TextField field)
    {
        if (field == null || field.panel == null)
            return false;

        Focusable focusedElement = field.panel.focusController?.focusedElement;
        if (focusedElement == null)
            return false;

        if (ReferenceEquals(focusedElement, field))
            return true;

        return focusedElement is VisualElement focusedVisual && field.Contains(focusedVisual);
    }

    private static bool MatchesSearch(string value, string normalizedQuery)
    {
        return string.IsNullOrWhiteSpace(normalizedQuery)
            || (!string.IsNullOrWhiteSpace(value) && value.IndexOf(normalizedQuery, StringComparison.OrdinalIgnoreCase) >= 0);
    }

    private static bool MatchesLibraryFilter(IToneLabPedalDescriptor descriptor, ToneLabLibraryFilter filter)
    {
        if (descriptor == null)
            return false;

        switch (filter)
        {
            case ToneLabLibraryFilter.BuiltIn:
                return descriptor.PedalType != UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin
                    && descriptor.PedalType != UnityToneLabRuntime.ToneLabPedalType.NamModel;
            case ToneLabLibraryFilter.Lv2:
                return descriptor.PedalType == UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin;
            case ToneLabLibraryFilter.Nam:
                return descriptor.PedalType == UnityToneLabRuntime.ToneLabPedalType.NamModel;
            default:
                return true;
        }
    }

    private static bool MatchesLibrarySearch(IToneLabPedalDescriptor descriptor, string normalizedQuery)
    {
        if (descriptor == null)
            return false;

        return MatchesSearch(descriptor.DisplayName, normalizedQuery)
            || MatchesSearch(descriptor.ShortName, normalizedQuery)
            || MatchesSearch(descriptor.Description, normalizedQuery)
            || MatchesSearch(descriptor.DescriptorId, normalizedQuery)
            || MatchesSearch(descriptor.PedalType.ToString(), normalizedQuery);
    }

    private List<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> BuildVisibleSongMappingSongs(
        IReadOnlyList<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> songs)
    {
        string normalizedQuery = NormalizeSearchQuery(songMappingSearchQuery);
        IEnumerable<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> visible = songs ?? Array.Empty<GuitarBridgeServer.ToneLabSongMappingSongSnapshot>();

        visible = visible.Where(song => MatchesSongMappingSearch(song, normalizedQuery));

        if (songMappingBrowseMode != ToneLabSongMappingBrowseMode.All && !string.IsNullOrWhiteSpace(songMappingBrowseScopeKey))
        {
            visible = visible.Where(song =>
                string.Equals(GetSongMappingBrowseValue(song, songMappingBrowseMode), songMappingBrowseScopeKey, StringComparison.OrdinalIgnoreCase));
        }

        return visible
            .OrderBy(song => song?.artist ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song?.album ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ThenBy(song => song?.displayName ?? string.Empty, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static bool MatchesSongMappingSearch(GuitarBridgeServer.ToneLabSongMappingSongSnapshot song, string normalizedQuery)
    {
        if (song == null)
            return false;

        return MatchesSearch(song.displayName, normalizedQuery)
            || MatchesSearch(song.artist, normalizedQuery)
            || MatchesSearch(song.album, normalizedQuery)
            || MatchesSearch(song.subtitle, normalizedQuery)
            || MatchesSearch(song.songKey, normalizedQuery);
    }

    private List<IGrouping<string, GuitarBridgeServer.ToneLabSongMappingSongSnapshot>> BuildSongMappingBrowseGroups(
        IReadOnlyList<GuitarBridgeServer.ToneLabSongMappingSongSnapshot> songs)
    {
        return (songs ?? Array.Empty<GuitarBridgeServer.ToneLabSongMappingSongSnapshot>())
            .Where(song => song != null)
            .GroupBy(song => GetSongMappingBrowseValue(song, songMappingBrowseMode), StringComparer.OrdinalIgnoreCase)
            .OrderBy(group => group.Key, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    private static string GetSongMappingBrowseValue(
        GuitarBridgeServer.ToneLabSongMappingSongSnapshot song,
        ToneLabSongMappingBrowseMode browseMode)
    {
        if (song == null)
            return string.Empty;

        if (browseMode == ToneLabSongMappingBrowseMode.Artists)
            return string.IsNullOrWhiteSpace(song.artist) ? "Unknown Artist" : song.artist.Trim();

        if (browseMode == ToneLabSongMappingBrowseMode.Albums)
            return string.IsNullOrWhiteSpace(song.album) ? "Unknown Album" : song.album.Trim();

        return string.Empty;
    }

    private static string GetSongMappingBrowseModeLabel(ToneLabSongMappingBrowseMode browseMode)
    {
        switch (browseMode)
        {
            case ToneLabSongMappingBrowseMode.Artists:
                return "Artists";
            case ToneLabSongMappingBrowseMode.Albums:
                return "Albums";
            default:
                return "Songs";
        }
    }

    private static string BuildSongMappingSongSubtitle(GuitarBridgeServer.ToneLabSongMappingSongSnapshot song)
    {
        if (song == null)
            return string.Empty;

        List<string> parts = new List<string>();
        if (!string.IsNullOrWhiteSpace(song.artist))
            parts.Add(song.artist.Trim());
        if (!string.IsNullOrWhiteSpace(song.album) &&
            !parts.Any(part => string.Equals(part, song.album.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add(song.album.Trim());
        }
        if (!string.IsNullOrWhiteSpace(song.subtitle) &&
            !parts.Any(part => string.Equals(part, song.subtitle.Trim(), StringComparison.OrdinalIgnoreCase)))
        {
            parts.Add(song.subtitle.Trim());
        }

        return string.Join("  •  ", parts);
    }

    private void RegisterLibraryItemDrag(ToneLabPedalLibraryItem item, string descriptorId)
    {
        if (item == null)
            return;

        item.RegisterCallback<PointerDownEvent>(evt =>
        {
            if (evt.button != 0 || libraryDragPointerId >= 0)
                return;

            libraryDragDescriptorId = descriptorId ?? string.Empty;
            libraryDragPointerId = evt.pointerId;
            libraryDragStartPosition = new Vector2(evt.position.x, evt.position.y);
            libraryDragMoved = false;
            item.CapturePointer(libraryDragPointerId);
        });

        item.RegisterCallback<PointerMoveEvent>(evt =>
        {
            if (libraryDragPointerId != evt.pointerId || !item.HasPointerCapture(libraryDragPointerId))
                return;

            Vector2 pointerPosition = new Vector2(evt.position.x, evt.position.y);
            Vector2 delta = pointerPosition - libraryDragStartPosition;
            if (!libraryDragMoved && delta.magnitude < 7f)
                return;

            libraryDragMoved = true;
            EnsureLibraryDragPreview(descriptorId);
            UpdateLibraryDragPreview(pointerPosition);
            evt.StopPropagation();
        });

        item.RegisterCallback<PointerUpEvent>(evt =>
        {
            if (libraryDragPointerId != evt.pointerId)
                return;

            if (item.HasPointerCapture(libraryDragPointerId))
                item.ReleasePointer(libraryDragPointerId);

            if (libraryDragMoved)
            {
                item.SuppressNextClick = true;
                TryDropLibraryPedal(new Vector2(evt.position.x, evt.position.y));
                evt.StopPropagation();
            }

            CancelLibraryDrag();
        });

        item.RegisterCallback<PointerCaptureOutEvent>(_ => CancelLibraryDrag());
    }

    private void EnsureLibraryDragPreview(string descriptorId)
    {
        if (libraryDragPreview != null || overlayRoot == null)
            return;

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(descriptorId);
        libraryDragPreview = ToneLabPedalVisualBuilder.BuildLibraryPreview(descriptor.Appearance, descriptor.ShortName);
        libraryDragPreview.pickingMode = PickingMode.Ignore;
        libraryDragPreview.style.position = Position.Absolute;
        libraryDragPreview.style.opacity = 0.92f;
        libraryDragPreview.style.scale = new Scale(new Vector3(1.18f, 1.18f, 1f));
        overlayRoot.Add(libraryDragPreview);
        libraryDragPreview.BringToFront();
    }

    private void UpdateLibraryDragPreview(Vector2 panelPosition)
    {
        if (libraryDragPreview == null || overlayRoot == null)
            return;

        libraryDragPreview.style.left = panelPosition.x - overlayRoot.worldBound.x - 58f;
        libraryDragPreview.style.top = panelPosition.y - overlayRoot.worldBound.y - 78f;
    }

    private void TryDropLibraryPedal(Vector2 panelPosition)
    {
        if (runtime == null || pedalBoardView == null)
            return;

        int insertionIndex = pedalBoardView.GetInsertionIndex(panelPosition);
        if (insertionIndex < 0)
            return;

        selectedPedalInstanceId = runtime.AddPedalToChain(libraryDragDescriptorId, insertionIndex);
        sidePanelMode = ToneLabSidePanelMode.Details;
        ResetNavigation(ToneLabNavigationZone.PedalBoard);
        RefreshUi(syncControls: true);
    }

    private void CancelLibraryDrag()
    {
        if (libraryDragPreview != null)
        {
            libraryDragPreview.RemoveFromHierarchy();
            libraryDragPreview = null;
        }

        libraryDragPointerId = -1;
        libraryDragDescriptorId = string.Empty;
        libraryDragMoved = false;
    }

    private void RebuildPedalInspector()
    {
        if (pedalInspectorHost == null || runtime == null)
            return;

        pedalInspectorHost.Clear();
        inspectorNavigationItems.Clear();
        UnityToneLabRuntime.ToneLabPedalSlot[] pedalChain = runtime.CurrentPedalChain;
        UnityToneLabRuntime.ToneLabPedalSlot selectedSlot = null;
        for (int i = 0; i < pedalChain.Length; i++)
        {
            UnityToneLabRuntime.ToneLabPedalSlot slot = pedalChain[i];
            if (slot != null && string.Equals(slot.pedal_instance_id, selectedPedalInstanceId, StringComparison.Ordinal))
            {
                selectedSlot = slot;
                break;
            }
        }

        if (selectedSlot == null)
        {
            Label emptyTitleLabel = new Label("No Pedal Selected");
            emptyTitleLabel.style.color = Color.white;
            emptyTitleLabel.style.fontSize = 24f;
            emptyTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
            emptyTitleLabel.style.marginBottom = 6f;
            pedalInspectorHost.Add(emptyTitleLabel);

            Label emptySubtitleLabel = new Label("The board is empty. Open the pedal library and add a pedal to start building your rig.");
            emptySubtitleLabel.style.color = new Color(0.63f, 0.66f, 0.72f, 0.94f);
            emptySubtitleLabel.style.fontSize = 14f;
            emptySubtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            emptySubtitleLabel.style.marginBottom = 14f;
            pedalInspectorHost.Add(emptySubtitleLabel);

            Action openLibraryAction = () =>
            {
                sidePanelMode = ToneLabSidePanelMode.Library;
                ResetNavigation(ToneLabNavigationZone.Sidebar);
                RefreshUi(syncControls: true);
            };
            Button openLibraryButton = CreateButton("Open Library", "tone-lab-button tone-lab-button-secondary", openLibraryAction);
            openLibraryButton.style.minWidth = 200f;
            openLibraryButton.style.marginRight = 0f;
            pedalInspectorHost.Add(openLibraryButton);
            RegisterNavigationItem(inspectorNavigationItems, openLibraryButton, pedalInspectorScroll, openLibraryAction, CreateButtonNavigationHover(openLibraryButton));
            return;
        }

        IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(selectedSlot);
        object pedalSettings = descriptor.DeserializeSettingsObject(selectedSlot.settings_json);

        VisualElement infoRow = new VisualElement();
        infoRow.style.flexDirection = FlexDirection.Row;
        infoRow.style.justifyContent = Justify.FlexStart;
        infoRow.style.alignItems = Align.Center;
        infoRow.style.marginBottom = 12f;
        pedalInspectorHost.Add(infoRow);

        Action toggleAction = () =>
        {
            if (runtime == null)
                return;

            bool nextEnabled = !selectedSlot.enabled;
            runtime.SetPedalEnabled(selectedSlot.pedal_instance_id, nextEnabled);
            RefreshUi(syncControls: true);
        };
        Button pedalToggleButton = CreateButton("ON", "tone-lab-button tone-lab-button-secondary", toggleAction);
        pedalToggleButton.style.minWidth = 96f;
        pedalToggleButton.style.height = 36f;
        pedalToggleButton.style.marginRight = 10f;
        ApplyInspectorToggleStyle(pedalToggleButton, selectedSlot.enabled);
        infoRow.Add(pedalToggleButton);
        RegisterNavigationItem(inspectorNavigationItems, pedalToggleButton, pedalInspectorScroll, toggleAction, CreateButtonNavigationHover(pedalToggleButton));

        Action removeAction = () =>
        {
            runtime?.RemovePedalFromChain(selectedSlot.pedal_instance_id);
            sidePanelMode = ToneLabSidePanelMode.Details;
            RefreshUi(syncControls: true);
        };
        Button removePedalButton = CreateButton("Remove", "tone-lab-button tone-lab-button-danger", removeAction);
        removePedalButton.style.minWidth = 96f;
        removePedalButton.style.height = 36f;
        removePedalButton.style.marginRight = 0f;
        infoRow.Add(removePedalButton);
        RegisterNavigationItem(inspectorNavigationItems, removePedalButton, pedalInspectorScroll, removeAction, CreateButtonNavigationHover(removePedalButton));

        VisualElement divider = new VisualElement();
        divider.style.height = 1f;
        divider.style.backgroundColor = new Color(0.19f, 0.21f, 0.25f, 0.92f);
        divider.style.marginBottom = 10f;
        pedalInspectorHost.Add(divider);

        IReadOnlyList<ToneLabPedalParameterDefinition> parameters = descriptor.Parameters;
        for (int i = 0; i < parameters.Count; i++)
        {
            pedalInspectorHost.Add(CreatePedalParameterSlider(selectedSlot.pedal_instance_id, pedalSettings, parameters[i]));
        }
    }

    private static void ApplyInspectorToggleStyle(Button button, bool enabled)
    {
        button.text = enabled ? "ON" : "OFF";
        button.style.backgroundColor = enabled ? new Color(0.12f, 0.20f, 0.14f, 0.92f) : new Color(0f, 0f, 0f, 0f);
        button.style.color = enabled ? new Color(0.82f, 0.93f, 0.84f, 1f) : new Color(0.88f, 0.90f, 0.94f, 0.96f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = enabled ? new Color(0.35f, 0.58f, 0.41f, 1f) : new Color(0.33f, 0.35f, 0.40f, 1f);
        button.style.borderRightColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.24f, 0.26f, 0.30f, 1f);
        button.style.borderBottomColor = enabled ? new Color(0.17f, 0.28f, 0.20f, 1f) : new Color(0.20f, 0.22f, 0.25f, 1f);
        button.style.borderLeftColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.24f, 0.26f, 0.30f, 1f);
    }

    private VisualElement CreatePedalParameterSlider(string pedalInstanceId, object settingsObject, ToneLabPedalParameterDefinition parameterDefinition)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-slider-row");

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-slider-header");
        row.Add(header);

        Label titleLabel = new Label(parameterDefinition.DisplayName);
        titleLabel.AddToClassList("tone-lab-slider-title");
        titleLabel.style.color = new Color(0.91f, 0.94f, 0.98f, 0.98f);
        titleLabel.style.fontSize = 15f;
        header.Add(titleLabel);

        Label valueLabel = new Label(parameterDefinition.Formatter(parameterDefinition.GetValue(settingsObject)));
        valueLabel.AddToClassList("tone-lab-slider-value");
        valueLabel.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        valueLabel.style.fontSize = 14f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(valueLabel);

        Slider slider = new Slider(parameterDefinition.MinimumValue, parameterDefinition.MaximumValue);
        slider.AddToClassList("tone-lab-slider");
        ApplySliderStyle(slider);
        slider.SetValueWithoutNotify(parameterDefinition.GetValue(settingsObject));
        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = parameterDefinition.Formatter(evt.newValue);
            runtime.UpdatePedalParameter(pedalInstanceId, parameterDefinition.ParameterId, evt.newValue);
        });
        row.Add(slider);
        return row;
    }

    private VisualElement CreateEffectCard(
        string title,
        string subtitle,
        Func<UnityToneLabRuntime.ToneLabSettings, bool> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, bool> setter,
        Color accentColor)
    {
        VisualElement card = CreateCard(title, subtitle);
        card.AddToClassList("tone-lab-effect-card");
        card.style.borderTopColor = accentColor;
        card.style.width = Length.Percent(49f);
        card.style.marginBottom = 16f;

        Button toggleButton = CreateButton("OFF", "tone-lab-toggle", () =>
        {
            if (runtime == null)
                return;

            bool nextValue = !getter(runtime.CurrentSettings);
            runtime.UpdateSettings(settings => setter(settings, nextValue), restartMonitoring: false);
            RefreshUi(syncControls: true);
        });

        ApplyToggleButtonState(toggleButton, getter(runtime.CurrentSettings));
        VisualElement header = card.Q<VisualElement>(className: "tone-lab-card-header");
        header?.Add(toggleButton);
        toggleBindings.Add(new ToneToggleBinding
        {
            toggleButton = toggleButton,
            getter = getter,
            setter = setter
        });

        switch (title)
        {
            case "Distortion":
                card.Add(CreateSliderControl("Drive", 0f, 36f, value => $"{value:F1} dB", settings => settings.dist_drive_db, (settings, value) => settings.dist_drive_db = value));
                break;
            case "Chorus":
                card.Add(CreateSliderControl("Rate", 0.1f, 4f, value => $"{value:F2} Hz", settings => settings.chorus_rate_hz, (settings, value) => settings.chorus_rate_hz = value));
                card.Add(CreateSliderControl("Depth", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.chorus_depth, (settings, value) => settings.chorus_depth = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.chorus_mix, (settings, value) => settings.chorus_mix = value));
                break;
            case "Phaser":
                card.Add(CreateSliderControl("Rate", 0.1f, 3f, value => $"{value:F2} Hz", settings => settings.phaser_rate_hz, (settings, value) => settings.phaser_rate_hz = value));
                card.Add(CreateSliderControl("Depth", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.phaser_depth, (settings, value) => settings.phaser_depth = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.phaser_mix, (settings, value) => settings.phaser_mix = value));
                card.Add(CreateSliderControl("Center", 120f, 4200f, value => $"{value:F0} Hz", settings => settings.phaser_center_hz, (settings, value) => settings.phaser_center_hz = value));
                card.Add(CreateSliderControl("Feedback", -0.9f, 0.9f, value => value.ToString("F2"), settings => settings.phaser_feedback, (settings, value) => settings.phaser_feedback = value));
                break;
            case "Delay":
                card.Add(CreateSliderControl("Time", 0.02f, 1.5f, value => $"{value:F2} s", settings => settings.delay_seconds, (settings, value) => settings.delay_seconds = value));
                card.Add(CreateSliderControl("Feedback", 0f, 0.95f, value => $"{value * 100f:F0}%", settings => settings.delay_feedback, (settings, value) => settings.delay_feedback = value));
                card.Add(CreateSliderControl("Mix", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.delay_mix, (settings, value) => settings.delay_mix = value));
                break;
            case "Reverb":
                card.Add(CreateSliderControl("Room Size", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_room_size, (settings, value) => settings.reverb_room_size = value));
                card.Add(CreateSliderControl("Damping", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_damping, (settings, value) => settings.reverb_damping = value));
                card.Add(CreateSliderControl("Wet", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_wet, (settings, value) => settings.reverb_wet = value));
                card.Add(CreateSliderControl("Dry", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_dry, (settings, value) => settings.reverb_dry = value));
                card.Add(CreateSliderControl("Width", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_width, (settings, value) => settings.reverb_width = value));
                card.Add(CreateSliderControl("Freeze", 0f, 1f, value => $"{value * 100f:F0}%", settings => settings.reverb_freeze, (settings, value) => settings.reverb_freeze = value));
                break;
            case "Compressor":
                card.Add(CreateSliderControl("Threshold", -60f, 0f, value => $"{value:F0} dB", settings => settings.comp_threshold_db, (settings, value) => settings.comp_threshold_db = value));
                card.Add(CreateSliderControl("Ratio", 1f, 8f, value => $"{value:F1}:1", settings => settings.comp_ratio, (settings, value) => settings.comp_ratio = value));
                card.Add(CreateSliderControl("Attack", 1f, 120f, value => $"{value:F0} ms", settings => settings.comp_attack_ms, (settings, value) => settings.comp_attack_ms = value));
                card.Add(CreateSliderControl("Release", 20f, 600f, value => $"{value:F0} ms", settings => settings.comp_release_ms, (settings, value) => settings.comp_release_ms = value));
                break;
        }

        if (title == "Distortion" || title == "Phaser" || title == "Reverb")
            card.style.marginRight = Length.Percent(1f);
        else
            card.style.marginLeft = Length.Percent(1f);

        return card;
    }

    private VisualElement CreateSliderControl(
        string title,
        float min,
        float max,
        Func<float, string> formatter,
        Func<UnityToneLabRuntime.ToneLabSettings, float> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, float> setter,
        bool includeInRefreshBindings = true)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-slider-row");

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-slider-header");
        row.Add(header);

        Label titleLabel = new Label(title);
        titleLabel.AddToClassList("tone-lab-slider-title");
        titleLabel.style.color = new Color(0.91f, 0.94f, 0.98f, 0.98f);
        titleLabel.style.fontSize = 15f;
        header.Add(titleLabel);

        Label valueLabel = new Label(string.Empty);
        valueLabel.AddToClassList("tone-lab-slider-value");
        valueLabel.style.color = new Color(1f, 0.79f, 0.56f, 0.98f);
        valueLabel.style.fontSize = 14f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        header.Add(valueLabel);

        Slider slider = new Slider(min, max);
        slider.AddToClassList("tone-lab-slider");
        ApplySliderStyle(slider);
        float initialValue = runtime != null ? getter(runtime.CurrentSettings) : min;
        slider.SetValueWithoutNotify(initialValue);
        valueLabel.text = formatter(initialValue);
        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = formatter(evt.newValue);
            runtime.UpdateSettings(settings => setter(settings, evt.newValue), restartMonitoring: false);
        });
        row.Add(slider);

        if (includeInRefreshBindings)
        {
            sliderBindings.Add(new ToneSliderBinding
            {
                slider = slider,
                valueLabel = valueLabel,
                getter = getter,
                setter = setter,
                formatter = formatter
            });
        }

        return row;
    }

    private static VisualElement CreateCard(string title, string subtitle)
    {
        VisualElement card = new VisualElement();
        card.AddToClassList("tone-lab-card");
        card.style.flexGrow = 1f;
        card.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.72f);
        card.style.borderTopWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderBottomWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderTopColor = new Color(0.23f, 0.24f, 0.29f, 0.98f);
        card.style.borderRightColor = new Color(0.18f, 0.19f, 0.23f, 0.98f);
        card.style.borderBottomColor = new Color(0.15f, 0.16f, 0.19f, 0.98f);
        card.style.borderLeftColor = new Color(0.18f, 0.19f, 0.23f, 0.98f);
        card.style.borderTopLeftRadius = 16f;
        card.style.borderTopRightRadius = 16f;
        card.style.borderBottomLeftRadius = 16f;
        card.style.borderBottomRightRadius = 16f;
        card.style.paddingLeft = 18f;
        card.style.paddingRight = 18f;
        card.style.paddingTop = 16f;
        card.style.paddingBottom = 14f;

        VisualElement header = new VisualElement();
        header.AddToClassList("tone-lab-card-header");
        header.style.flexDirection = FlexDirection.Row;
        header.style.justifyContent = Justify.SpaceBetween;
        header.style.alignItems = Align.FlexStart;
        header.style.marginBottom = 12f;
        card.Add(header);

        VisualElement textColumn = new VisualElement();
        textColumn.AddToClassList("tone-lab-card-copy");
        textColumn.style.flexGrow = 1f;
        textColumn.style.paddingRight = 10f;
        header.Add(textColumn);

        Label titleLabel = new Label(title);
        titleLabel.AddToClassList("tone-lab-card-title");
        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 20f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        textColumn.Add(titleLabel);

        if (!string.IsNullOrWhiteSpace(subtitle))
        {
            Label subtitleLabel = new Label(subtitle);
            subtitleLabel.AddToClassList("tone-lab-card-subtitle");
            subtitleLabel.style.color = new Color(0.59f, 0.62f, 0.67f, 0.94f);
            subtitleLabel.style.fontSize = 13f;
            subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
            textColumn.Add(subtitleLabel);
        }

        return card;
    }

    private static VisualElement CreateSideSectionCard(
        VisualElement parent,
        string title,
        string subtitle,
        out Label titleLabel,
        out Label subtitleLabel,
        out VisualElement contentHost)
    {
        titleLabel = new Label(title);
        subtitleLabel = new Label(subtitle);
        return CreateSideSectionCard(parent, titleLabel, subtitleLabel, out contentHost);
    }

    private static VisualElement CreateSideSectionCard(
        VisualElement parent,
        Label titleLabel,
        Label subtitleLabel,
        out VisualElement contentHost)
    {
        VisualElement section = new VisualElement();
        section.style.flexDirection = FlexDirection.Column;
        section.style.flexGrow = 1f;
        section.style.minHeight = 0f;
        section.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        section.style.borderTopWidth = 1f;
        section.style.borderRightWidth = 1f;
        section.style.borderBottomWidth = 1f;
        section.style.borderLeftWidth = 1f;
        section.style.borderTopColor = new Color(1f, 1f, 1f, 0.92f);
        section.style.borderRightColor = new Color(1f, 1f, 1f, 0.78f);
        section.style.borderBottomColor = new Color(1f, 1f, 1f, 0.66f);
        section.style.borderLeftColor = new Color(1f, 1f, 1f, 0.78f);
        section.style.borderTopLeftRadius = 18f;
        section.style.borderTopRightRadius = 18f;
        section.style.borderBottomLeftRadius = 18f;
        section.style.borderBottomRightRadius = 18f;
        section.style.paddingLeft = 16f;
        section.style.paddingRight = 12f;
        section.style.paddingTop = 16f;
        section.style.paddingBottom = 14f;
        parent.Add(section);

        titleLabel.style.color = Color.white;
        titleLabel.style.fontSize = 24f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        titleLabel.style.flexShrink = 0f;
        section.Add(titleLabel);

        subtitleLabel.style.color = new Color(0.60f, 0.63f, 0.68f, 0.95f);
        subtitleLabel.style.fontSize = 13f;
        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;
        subtitleLabel.style.marginBottom = 12f;
        subtitleLabel.style.flexShrink = 0f;
        section.Add(subtitleLabel);

        contentHost = new VisualElement();
        contentHost.style.flexGrow = 1f;
        contentHost.style.minHeight = 0f;
        contentHost.style.overflow = Overflow.Hidden;
        section.Add(contentHost);

        return section;
    }

    private static VisualElement CreateToolbarField(string labelText, VisualElement control, float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.flexGrow = 0f;
        field.style.flexShrink = 0f;
        field.style.marginRight = 12f;

        Label label = new Label(labelText);
        label.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 4f;
        field.Add(label);

        field.Add(control);
        return field;
    }

    private static VisualElement CreateToolbarReadout(string labelText, VisualElement valueElement, bool flexGrow)
    {
        VisualElement field = new VisualElement();
        field.style.marginLeft = 12f;
        field.style.alignItems = Align.FlexEnd;
        field.style.justifyContent = Justify.Center;
        if (flexGrow)
            field.style.flexGrow = 1f;

        Label label = new Label(labelText);
        label.style.color = new Color(0.66f, 0.69f, 0.73f, 0.95f);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 4f;
        field.Add(label);

        field.Add(valueElement);
        return field;
    }

    private static Label CreateGainSectionLabel(string text, float width)
    {
        Label label = new Label(text);
        label.style.width = width;
        label.style.minWidth = width;
        label.style.marginRight = 12f;
        label.style.color = new Color(0.88f, 0.90f, 0.94f, 0.88f);
        label.style.fontSize = 13f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.unityTextAlign = TextAnchor.MiddleLeft;
        return label;
    }

    private VisualElement CreateCompactSliderField(
        string title,
        float min,
        float max,
        Func<float, string> formatter,
        Func<UnityToneLabRuntime.ToneLabSettings, float> getter,
        Action<UnityToneLabRuntime.ToneLabSettings, float> setter,
        float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.flexGrow = 0f;
        field.style.flexShrink = 0f;
        field.style.marginRight = 12f;

        Label titleLabel = new Label(title);
        titleLabel.style.color = new Color(0.78f, 0.81f, 0.86f, 0.96f);
        titleLabel.style.fontSize = 14f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        field.Add(titleLabel);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        field.Add(row);

        Slider slider = new Slider(min, max);
        ApplySliderStyle(slider);
        slider.style.flexGrow = 1f;
        slider.style.marginTop = 0f;
        slider.style.marginBottom = 0f;
        slider.style.marginRight = 10f;
        float initialValue = runtime != null ? getter(runtime.CurrentSettings) : min;
        slider.SetValueWithoutNotify(initialValue);
        row.Add(slider);

        Label valueLabel = new Label(formatter(initialValue));
        valueLabel.style.color = new Color(0.84f, 0.81f, 0.74f, 0.98f);
        valueLabel.style.fontSize = 15f;
        valueLabel.style.minWidth = 64f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(valueLabel);

        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks || runtime == null)
                return;

            valueLabel.text = formatter(evt.newValue);
            runtime.UpdateSettings(settings => setter(settings, evt.newValue), restartMonitoring: false, rebuildPedalChain: false);
        });

        sliderBindings.Add(new ToneSliderBinding
        {
            slider = slider,
            valueLabel = valueLabel,
            getter = getter,
            setter = setter,
            formatter = formatter
        });

        return field;
    }

    private VisualElement CreateSharedVolumeSliderField(
        string title,
        float min,
        float max,
        Func<float, string> formatter,
        Func<float> getter,
        Action<float> setter,
        out Slider slider,
        out Label valueLabel,
        float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.flexGrow = 0f;
        field.style.flexShrink = 0f;
        field.style.marginRight = 12f;

        Label titleLabel = new Label(title);
        titleLabel.style.color = new Color(0.78f, 0.81f, 0.86f, 0.96f);
        titleLabel.style.fontSize = 14f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        field.Add(titleLabel);

        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        field.Add(row);

        slider = new Slider(min, max);
        ApplySliderStyle(slider);
        slider.style.flexGrow = 1f;
        slider.style.marginTop = 0f;
        slider.style.marginBottom = 0f;
        slider.style.marginRight = 10f;
        float initialValue = getter != null ? getter() : min;
        slider.SetValueWithoutNotify(initialValue);
        row.Add(slider);

        Label localValueLabel = new Label(formatter(initialValue));
        localValueLabel.style.color = new Color(0.84f, 0.81f, 0.74f, 0.98f);
        localValueLabel.style.fontSize = 15f;
        localValueLabel.style.minWidth = 64f;
        localValueLabel.style.unityTextAlign = TextAnchor.MiddleRight;
        row.Add(localValueLabel);
        valueLabel = localValueLabel;

        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressCallbacks)
                return;

            localValueLabel.text = formatter(evt.newValue);
            setter?.Invoke(evt.newValue);
        });

        return field;
    }

    private static VisualElement CreateSettingRow(string labelText, out VisualElement valueHost)
    {
        VisualElement row = new VisualElement();
        row.AddToClassList("tone-lab-setting-row");
        row.style.flexDirection = FlexDirection.Row;
        row.style.alignItems = Align.Center;
        row.style.justifyContent = Justify.SpaceBetween;
        row.style.marginBottom = 12f;

        Label label = new Label(labelText);
        label.AddToClassList("tone-lab-setting-label");
        label.style.color = new Color(0.80f, 0.84f, 0.91f, 0.96f);
        label.style.fontSize = 15f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        row.Add(label);

        valueHost = new VisualElement();
        valueHost.AddToClassList("tone-lab-setting-value-host");
        valueHost.style.minWidth = 280f;
        valueHost.style.alignItems = Align.FlexEnd;
        row.Add(valueHost);
        return row;
    }

    private static VisualElement CreateSectionDivider()
    {
        VisualElement divider = new VisualElement();
        divider.style.height = 1f;
        divider.style.marginTop = 6f;
        divider.style.marginBottom = 18f;
        divider.style.backgroundColor = new Color(1f, 1f, 1f, 0.16f);
        return divider;
    }

    private static Button CreateButton(string text, string classNames, Action onClick)
    {
        Button button = new Button(onClick) { text = text };
        foreach (string className in classNames.Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries))
            button.AddToClassList(className);

        button.style.minWidth = 150f;
        button.style.height = 42f;
        button.style.marginRight = 10f;
        button.style.paddingLeft = 16f;
        button.style.paddingRight = 16f;
        button.style.borderTopLeftRadius = 11f;
        button.style.borderTopRightRadius = 11f;
        button.style.borderBottomLeftRadius = 11f;
        button.style.borderBottomRightRadius = 11f;
        button.style.fontSize = 15f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.opacity = 0.96f;
        button.style.scale = new Scale(Vector3.one);

        bool isPrimary = classNames.Contains("tone-lab-button-primary");
        bool isSecondary = classNames.Contains("tone-lab-button-secondary");
        bool isDanger = classNames.Contains("tone-lab-button-danger");
        bool isToggle = classNames.Contains("tone-lab-toggle");
        if (isDanger)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.95f, 0.60f, 0.60f, 1f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.62f, 0.30f, 0.30f, 1f);
            button.style.borderRightColor = new Color(0.44f, 0.20f, 0.20f, 1f);
            button.style.borderBottomColor = new Color(0.36f, 0.16f, 0.16f, 1f);
            button.style.borderLeftColor = new Color(0.44f, 0.20f, 0.20f, 1f);
        }
        else if (isPrimary)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.96f, 0.97f, 0.98f, 0.98f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.62f, 0.64f, 0.68f, 0.98f);
            button.style.borderRightColor = new Color(0.38f, 0.40f, 0.44f, 1f);
            button.style.borderBottomColor = new Color(0.29f, 0.30f, 0.34f, 1f);
            button.style.borderLeftColor = new Color(0.38f, 0.40f, 0.44f, 1f);
        }
        else if (isSecondary)
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.84f, 0.86f, 0.90f, 0.98f);
            button.style.borderTopWidth = 1f;
            button.style.borderRightWidth = 1f;
            button.style.borderBottomWidth = 1f;
            button.style.borderLeftWidth = 1f;
            button.style.borderTopColor = new Color(0.36f, 0.38f, 0.42f, 0.90f);
            button.style.borderRightColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
            button.style.borderBottomColor = new Color(0.18f, 0.20f, 0.23f, 0.98f);
            button.style.borderLeftColor = new Color(0.24f, 0.26f, 0.30f, 0.98f);
        }
        else if (isToggle)
        {
            button.style.minWidth = 84f;
            button.style.height = 36f;
            button.style.fontSize = 14f;
            button.style.marginRight = 0f;
        }

        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));
            button.style.opacity = 1f;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.scale = new Scale(Vector3.one);
            button.style.opacity = 0.96f;
        });

        return button;
    }

    private static Button CreateSidebarTabButton(string text, Color accentColor, Action onClick)
    {
        Button button = new Button(onClick) { text = text };
        button.style.flexGrow = 1f;
        button.style.height = 44f;
        button.style.minWidth = 0f;
        button.style.marginRight = 0f;
        button.style.paddingLeft = 8f;
        button.style.paddingRight = 8f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = accentColor;
        button.style.borderTopWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.borderLeftWidth = 0f;
        button.style.borderTopLeftRadius = 0f;
        button.style.borderTopRightRadius = 0f;
        button.style.borderBottomLeftRadius = 0f;
        button.style.borderBottomRightRadius = 0f;
        button.style.fontSize = 15f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            bool selected = button.userData is bool isSelected && isSelected;
            if (!selected)
                button.style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            button.style.color = selected ? new Color(0.05f, 0.05f, 0.055f, 1f) : Color.Lerp(accentColor, Color.white, 0.22f);
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            bool selected = button.userData is bool isSelected && isSelected;
            if (!selected)
            {
                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                button.style.backgroundImage = StyleKeyword.None;
                button.style.color = accentColor;
            }
            else
            {
                button.style.color = new Color(0.05f, 0.05f, 0.055f, 1f);
            }
        });
        return button;
    }

    private static void ApplySidebarTabState(Button button, bool selected, Color accentColor)
    {
        if (button == null)
            return;

        button.userData = selected;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        if (selected)
            button.style.backgroundImage = new StyleBackground(GetSelectedSidebarTabTexture());
        else
            button.style.backgroundImage = StyleKeyword.None;
        button.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
        button.style.color = selected ? new Color(0.05f, 0.05f, 0.055f, 1f) : accentColor;
        button.style.fontSize = selected ? 16f : 15f;
    }

    private static Color StartupLogoColor(int index)
    {
        switch (Mathf.Abs(index) % 6)
        {
            case 0:
                return new Color(0.91f, 0.30f, 0.24f, 1f);
            case 1:
                return new Color(0.95f, 0.77f, 0.06f, 1f);
            case 2:
                return new Color(0.20f, 0.60f, 0.86f, 1f);
            case 3:
                return new Color(0.90f, 0.49f, 0.13f, 1f);
            case 4:
                return new Color(0.18f, 0.80f, 0.44f, 1f);
            default:
                return new Color(0.61f, 0.35f, 0.71f, 1f);
        }
    }

    private static VisualElement CreateThinDividerSection()
    {
        VisualElement section = new VisualElement();
        section.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        section.style.borderTopWidth = 1f;
        section.style.borderBottomWidth = 1f;
        section.style.borderLeftWidth = 0f;
        section.style.borderRightWidth = 0f;
        section.style.borderTopColor = new Color(1f, 1f, 1f, 0.16f);
        section.style.borderBottomColor = new Color(1f, 1f, 1f, 0.12f);
        section.style.paddingLeft = 0f;
        section.style.paddingRight = 0f;
        return section;
    }

    private static VisualElement CreateModernToolbarField(string labelText, VisualElement control, float width)
    {
        VisualElement field = new VisualElement();
        field.style.width = width;
        field.style.minWidth = width;
        field.style.marginRight = 14f;
        field.style.justifyContent = Justify.Center;

        Label label = new Label(labelText);
        label.style.color = new Color(0.88f, 0.90f, 0.94f, 0.82f);
        label.style.fontSize = 12f;
        label.style.unityFontStyleAndWeight = FontStyle.Bold;
        label.style.marginBottom = 3f;
        field.Add(label);

        field.Add(control);
        return field;
    }

    private static void StyleTransparentScrollView(ScrollView scrollView)
    {
        if (scrollView == null)
            return;

        scrollView.style.flexGrow = 1f;
        scrollView.style.minHeight = 0f;
        scrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;
        scrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        scrollView.style.borderTopWidth = 0f;
        scrollView.style.borderRightWidth = 0f;
        scrollView.style.borderBottomWidth = 0f;
        scrollView.style.borderLeftWidth = 0f;
        if (scrollView.contentViewport != null)
            scrollView.contentViewport.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    private static void StyleCompactActionButton(Button button, float width)
    {
        if (button == null)
            return;

        button.style.minWidth = width;
        button.style.width = width;
        button.style.height = 38f;
        button.style.fontSize = 14f;
        button.style.marginRight = 10f;
        button.style.paddingLeft = 8f;
        button.style.paddingRight = 8f;
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
    }

    private static void StyleFooterActionButton(Button button, float width)
    {
        if (button == null)
            return;

        StyleCompactActionButton(button, width);
        button.style.height = 46f;
        button.style.fontSize = 15f;
        button.style.paddingLeft = 14f;
        button.style.paddingRight = 14f;
        button.style.borderTopLeftRadius = 10f;
        button.style.borderTopRightRadius = 10f;
        button.style.borderBottomLeftRadius = 10f;
        button.style.borderBottomRightRadius = 10f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        bool isDanger = button.ClassListContains("tone-lab-button-danger");
        bool isPrimary = button.ClassListContains("tone-lab-button-primary");
        Color restTextColor = isDanger
            ? new Color(0.95f, 0.60f, 0.60f, 1f)
            : (isPrimary ? new Color(0.96f, 0.97f, 0.98f, 0.98f) : new Color(0.84f, 0.86f, 0.90f, 0.98f));
        Color hoverBackground = isDanger
            ? new Color(0.70f, 0.08f, 0.10f, 0.26f)
            : (isPrimary ? new Color(1f, 1f, 1f, 0.18f) : new Color(1f, 1f, 1f, 0.12f));

        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = hoverBackground;
            button.style.color = Color.white;
            button.style.scale = new Scale(new Vector3(1.06f, 1.06f, 1f));
            button.style.opacity = 1f;
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = restTextColor;
            button.style.scale = new Scale(Vector3.one);
            button.style.opacity = 0.96f;
        });
    }

    private static void StyleUnsavedModalButton(Button button, float width, bool isDanger)
    {
        if (button == null)
            return;

        button.style.minWidth = width;
        button.style.height = 42f;
        button.style.fontSize = 15f;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.borderTopLeftRadius = 10f;
        button.style.borderTopRightRadius = 10f;
        button.style.borderBottomLeftRadius = 10f;
        button.style.borderBottomRightRadius = 10f;

        Color restText = isDanger
            ? new Color(1f, 0.52f, 0.55f, 1f)
            : new Color(0.96f, 0.97f, 0.98f, 0.98f);
        Color hoverBackground = isDanger
            ? new Color(0.72f, 0.08f, 0.10f, 0.28f)
            : new Color(1f, 0.58f, 0.10f, 0.22f);

        button.style.color = restText;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = hoverBackground;
            button.style.color = Color.white;
            button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = restText;
            button.style.scale = new Scale(Vector3.one);
        });
    }

    private static void StyleRoundIconButton(Button button, float size, float fontSize)
    {
        if (button == null)
            return;

        button.style.width = size;
        button.style.minWidth = size;
        button.style.height = size;
        button.style.marginRight = 0f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 2f;
        button.style.fontSize = fontSize;
        button.style.borderTopLeftRadius = 999f;
        button.style.borderTopRightRadius = 999f;
        button.style.borderBottomLeftRadius = 999f;
        button.style.borderBottomRightRadius = 999f;
        button.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
        button.style.color = Color.white;
    }

    private static void StyleTextDeleteButton(Button button)
    {
        if (button == null)
            return;

        button.style.width = 34f;
        button.style.minWidth = 34f;
        button.style.height = 34f;
        button.style.marginRight = 0f;
        button.style.paddingLeft = 0f;
        button.style.paddingRight = 0f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = Color.white;
        button.style.fontSize = 18f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopWidth = 0f;
        button.style.borderRightWidth = 0f;
        button.style.borderBottomWidth = 0f;
        button.style.borderLeftWidth = 0f;
        button.RegisterCallback<MouseEnterEvent>(_ => button.style.color = new Color(1f, 0.22f, 0.28f, 1f));
        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.color = Color.white);
    }

    private static void StylePresetSelectButton(Button button)
    {
        if (button == null)
            return;

        button.style.width = 78f;
        button.style.minWidth = 78f;
        button.style.height = 34f;
        button.style.marginRight = 0f;
        button.style.paddingLeft = 10f;
        button.style.paddingRight = 10f;
        button.style.paddingTop = 0f;
        button.style.paddingBottom = 0f;
        button.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.20f);
        button.style.color = Color.white;
        button.style.fontSize = 13f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = new Color(1f, 0.88f, 0.48f, 0.92f);
        button.style.borderRightColor = new Color(1f, 0.56f, 0.38f, 0.86f);
        button.style.borderBottomColor = new Color(0.82f, 0.32f, 0.22f, 0.82f);
        button.style.borderLeftColor = new Color(1f, 0.72f, 0.22f, 0.86f);
        button.style.borderTopLeftRadius = 8f;
        button.style.borderTopRightRadius = 8f;
        button.style.borderBottomLeftRadius = 8f;
        button.style.borderBottomRightRadius = 8f;
        button.RegisterCallback<MouseEnterEvent>(_ =>
        {
            button.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.40f);
            button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));
        });
        button.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            button.style.backgroundColor = new Color(1f, 0.58f, 0.08f, 0.20f);
            button.style.scale = new Scale(Vector3.one);
        });
    }

    private static Texture2D GetPresetSelectionGradientTexture()
    {
        if (presetSelectionGradientTexture != null)
            return presetSelectionGradientTexture;

        const int width = 64;
        Texture2D texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
        {
            name = "ToneLabPresetSelectionGradient",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color left = new Color(0.95f, 0.67f, 0.00f, 0.96f);
        Color right = new Color(1.00f, 0.38f, 0.45f, 0.96f);
        for (int x = 0; x < width; x++)
        {
            float t = x / (float)(width - 1);
            texture.SetPixel(x, 0, Color.Lerp(left, right, t));
        }

        texture.Apply(false, true);
        presetSelectionGradientTexture = texture;
        return presetSelectionGradientTexture;
    }

    private static Texture2D GetSelectedSidebarTabTexture()
    {
        if (selectedSidebarTabTexture != null)
            return selectedSidebarTabTexture;

        const int width = 512;
        const int height = 128;
        const int slant = 88;
        const float edgeSoftness = 2.25f;
        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
        {
            name = "ToneLabSelectedSidebarTab",
            hideFlags = HideFlags.HideAndDontSave,
            wrapMode = TextureWrapMode.Clamp,
            filterMode = FilterMode.Bilinear
        };

        Color fill = new Color(0.94f, 0.94f, 0.92f, 0.98f);
        for (int y = 0; y < height; y++)
        {
            float rowSlant = (y / (float)Mathf.Max(1, height - 1)) * slant;
            float rightEdge = width - slant + rowSlant;
            for (int x = 0; x < width; x++)
            {
                float alpha = Mathf.Clamp01((rightEdge - x + edgeSoftness) / (edgeSoftness * 2f));
                texture.SetPixel(x, y, new Color(fill.r, fill.g, fill.b, fill.a * alpha));
            }
        }

        texture.Apply(false, true);
        selectedSidebarTabTexture = texture;
        return selectedSidebarTabTexture;
    }

    private static void ApplyModalActionButtonStyle(Button button, bool isDanger)
    {
        if (button == null)
            return;

        if (isDanger)
        {
            button.style.color = new Color(0.95f, 0.60f, 0.60f, 1f);
            button.style.borderTopColor = new Color(0.62f, 0.30f, 0.30f, 1f);
            button.style.borderRightColor = new Color(0.44f, 0.20f, 0.20f, 1f);
            button.style.borderBottomColor = new Color(0.36f, 0.16f, 0.16f, 1f);
            button.style.borderLeftColor = new Color(0.44f, 0.20f, 0.20f, 1f);
        }
        else
        {
            button.style.color = new Color(0.96f, 0.97f, 0.98f, 0.98f);
            button.style.borderTopColor = new Color(0.62f, 0.64f, 0.68f, 0.98f);
            button.style.borderRightColor = new Color(0.38f, 0.40f, 0.44f, 1f);
            button.style.borderBottomColor = new Color(0.29f, 0.30f, 0.34f, 1f);
            button.style.borderLeftColor = new Color(0.38f, 0.40f, 0.44f, 1f);
        }
    }

    private static Label CreateLabel(string text, string className, FontDefinition fontDefinition)
    {
        Label label = new Label(text);
        label.AddToClassList(className);
        label.style.unityFontDefinition = fontDefinition;
        return label;
    }

    private static void ApplyToggleButtonState(Button button, bool enabled)
    {
        button.text = enabled ? "ON" : "OFF";
        button.EnableInClassList("tone-lab-toggle-on", enabled);
        button.EnableInClassList("tone-lab-toggle-off", !enabled);
        button.style.backgroundColor = enabled
            ? new Color(0.14f, 0.23f, 0.16f, 0.98f)
            : new Color(0f, 0f, 0f, 0f);
        button.style.color = enabled
            ? new Color(0.83f, 0.92f, 0.85f, 1f)
            : new Color(0.82f, 0.87f, 0.94f, 0.96f);
        button.style.borderTopWidth = 1f;
        button.style.borderRightWidth = 1f;
        button.style.borderBottomWidth = 1f;
        button.style.borderLeftWidth = 1f;
        button.style.borderTopColor = enabled ? new Color(0.35f, 0.58f, 0.41f, 0.98f) : new Color(0.34f, 0.36f, 0.40f, 0.92f);
        button.style.borderRightColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.23f, 0.25f, 0.29f, 0.98f);
        button.style.borderBottomColor = enabled ? new Color(0.17f, 0.28f, 0.20f, 1f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        button.style.borderLeftColor = enabled ? new Color(0.19f, 0.35f, 0.24f, 1f) : new Color(0.23f, 0.25f, 0.29f, 0.98f);
    }

    private static void ApplyModeButtonState(Button button, bool selected)
    {
        if (button == null)
            return;

        button.style.backgroundColor = selected ? new Color(0.10f, 0.11f, 0.13f, 1f) : new Color(0f, 0f, 0f, 0f);
        button.style.color = selected ? Color.white : new Color(0.84f, 0.86f, 0.90f, 0.98f);
        button.style.borderTopColor = selected ? new Color(0.62f, 0.64f, 0.68f, 0.98f) : new Color(0.36f, 0.38f, 0.42f, 0.90f);
        button.style.borderRightColor = selected ? new Color(0.38f, 0.40f, 0.44f, 1f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        button.style.borderBottomColor = selected ? new Color(0.29f, 0.30f, 0.34f, 1f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        button.style.borderLeftColor = selected ? new Color(0.38f, 0.40f, 0.44f, 1f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
    }

    private static void ApplyOverlayRootStyle(VisualElement overlay)
    {
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Stretch;
        overlay.style.justifyContent = Justify.FlexStart;
    }

    private static void ApplyBackdropStyle(VisualElement backdrop)
    {
        backdrop.style.position = Position.Absolute;
        backdrop.style.left = 0f;
        backdrop.style.right = 0f;
        backdrop.style.top = 0f;
        backdrop.style.bottom = 0f;
        backdrop.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
    }

    private static void ApplyWindowStyle(VisualElement window)
    {
        window.style.position = Position.Absolute;
        window.style.left = 0f;
        window.style.right = 0f;
        window.style.top = 0f;
        window.style.bottom = 0f;
        window.style.paddingLeft = 26f;
        window.style.paddingRight = 26f;
        window.style.paddingTop = 22f;
        window.style.paddingBottom = 18f;
        window.style.overflow = Overflow.Hidden;
        window.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        window.style.borderTopWidth = 0f;
        window.style.borderRightWidth = 0f;
        window.style.borderBottomWidth = 0f;
        window.style.borderLeftWidth = 0f;
        window.style.borderTopLeftRadius = 0f;
        window.style.borderTopRightRadius = 0f;
        window.style.borderBottomLeftRadius = 0f;
        window.style.borderBottomRightRadius = 0f;
        window.style.flexDirection = FlexDirection.Column;
    }

    private static void ApplySliderStyle(Slider slider)
    {
        slider.style.height = 24f;
        slider.style.marginTop = 4f;
        slider.style.marginBottom = 10f;

        slider.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement dragContainer = slider.Q<VisualElement>(className: "unity-base-slider__drag-container");
            if (dragContainer != null)
            {
                dragContainer.style.height = 10f;
                dragContainer.style.backgroundColor = new Color(0.14f, 0.15f, 0.18f, 0.98f);
                dragContainer.style.borderTopLeftRadius = 5f;
                dragContainer.style.borderTopRightRadius = 5f;
                dragContainer.style.borderBottomLeftRadius = 5f;
                dragContainer.style.borderBottomRightRadius = 5f;
            }

            VisualElement tracker = slider.Q<VisualElement>(className: "unity-base-slider__tracker");
            if (tracker != null)
            {
                tracker.style.backgroundColor = new Color(0.76f, 0.62f, 0.42f, 0.94f);
                tracker.style.borderTopLeftRadius = 5f;
                tracker.style.borderTopRightRadius = 5f;
                tracker.style.borderBottomLeftRadius = 5f;
                tracker.style.borderBottomRightRadius = 5f;
            }

            VisualElement dragger = slider.Q<VisualElement>(className: "unity-base-slider__dragger");
            if (dragger != null)
            {
                dragger.style.width = 16f;
                dragger.style.height = 16f;
                dragger.style.backgroundColor = new Color(0.94f, 0.94f, 0.92f, 1f);
                dragger.style.borderTopLeftRadius = 8f;
                dragger.style.borderTopRightRadius = 8f;
                dragger.style.borderBottomLeftRadius = 8f;
                dragger.style.borderBottomRightRadius = 8f;
                dragger.style.borderTopWidth = 1f;
                dragger.style.borderRightWidth = 1f;
                dragger.style.borderBottomWidth = 1f;
                dragger.style.borderLeftWidth = 1f;
                dragger.style.borderTopColor = new Color(0.90f, 0.88f, 0.84f, 1f);
                dragger.style.borderRightColor = new Color(0.50f, 0.46f, 0.40f, 1f);
                dragger.style.borderBottomColor = new Color(0.40f, 0.36f, 0.31f, 1f);
                dragger.style.borderLeftColor = new Color(0.50f, 0.46f, 0.40f, 1f);
            }
        });
    }

    private static void ApplyDropdownStyle(DropdownField dropdown)
    {
        dropdown.style.minWidth = 180f;
        dropdown.style.minHeight = 38f;
        dropdown.style.fontSize = 14f;
        dropdown.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        dropdown.style.color = new Color(0.94f, 0.95f, 0.97f, 0.98f);
        dropdown.style.borderTopWidth = 0f;
        dropdown.style.borderRightWidth = 0f;
        dropdown.style.borderBottomWidth = 1f;
        dropdown.style.borderLeftWidth = 0f;
        dropdown.style.borderBottomColor = new Color(1f, 1f, 1f, 0.34f);
        dropdown.style.borderTopLeftRadius = 0f;
        dropdown.style.borderTopRightRadius = 0f;
        dropdown.style.borderBottomLeftRadius = 0f;
        dropdown.style.borderBottomRightRadius = 0f;
        dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement inputElement = dropdown.Q(className: "unity-base-field__input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
                inputElement.style.color = new Color(0.94f, 0.95f, 0.97f, 1f);
                inputElement.style.borderBottomWidth = 0f;
                inputElement.style.fontSize = 14f;
            }

            Label textLabel = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (textLabel != null)
            {
                textLabel.style.color = new Color(0.90f, 0.91f, 0.93f, 1f);
                textLabel.style.fontSize = 14f;
            }

            VisualElement arrowElement = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrowElement != null)
                arrowElement.style.unityBackgroundImageTintColor = new Color(0.82f, 0.84f, 0.88f, 1f);
        });
    }

    private static int ParseLatencyPresetBufferSize(string label)
    {
        return UnityToneLabRuntime.ParseSharedMonitoringLatencyBufferSize(label);
    }

    private bool HasUnsavedPresetChanges()
    {
        if (runtime == null)
            return false;

        string currentPresetId = runtime.CurrentPresetId;
        if (string.IsNullOrWhiteSpace(currentPresetId))
            return false;

        UnityToneLabRuntime.ToneLabSettings settings = runtime.CurrentSettings;
        if (settings == null)
            return false;

        UnityToneLabRuntime.ToneLabPreset selectedPreset = runtime.CurrentPresets.FirstOrDefault(preset =>
            preset != null && string.Equals(preset.preset_id, currentPresetId, StringComparison.Ordinal));
        if (selectedPreset == null)
            return false;

        if (!ApproximatelyEqual(settings.input_gain_db, selectedPreset.input_gain_db))
            return true;
        if (!ApproximatelyEqual(settings.output_gain_db, selectedPreset.output_gain_db))
            return true;

        return !PedalChainsEquivalent(settings.pedal_chain, selectedPreset.pedal_chain);
    }

    private static bool ApproximatelyEqual(float left, float right)
    {
        return Mathf.Abs(left - right) <= 0.0001f;
    }

    private static bool PedalChainsEquivalent(
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> left,
        IReadOnlyList<UnityToneLabRuntime.ToneLabPedalSlot> right)
    {
        int leftCount = left?.Count ?? 0;
        int rightCount = right?.Count ?? 0;
        if (leftCount != rightCount)
            return false;

        for (int i = 0; i < leftCount; i++)
        {
            if (!PedalSlotsEquivalent(left[i], right[i]))
                return false;
        }

        return true;
    }

    private static bool PedalSlotsEquivalent(UnityToneLabRuntime.ToneLabPedalSlot left, UnityToneLabRuntime.ToneLabPedalSlot right)
    {
        if (left == null || right == null)
            return left == null && right == null;

        return left.pedal_type == right.pedal_type
            && string.Equals(left.descriptor_id ?? string.Empty, right.descriptor_id ?? string.Empty, StringComparison.Ordinal)
            && left.enabled == right.enabled
            && PedalSettingsEquivalent(left, right);
    }

    private static bool PedalSettingsEquivalent(UnityToneLabRuntime.ToneLabPedalSlot left, UnityToneLabRuntime.ToneLabPedalSlot right)
    {
        string leftJson = left?.settings_json ?? string.Empty;
        string rightJson = right?.settings_json ?? string.Empty;
        if (string.Equals(leftJson, rightJson, StringComparison.Ordinal))
            return true;

        try
        {
            IToneLabPedalDescriptor descriptor = ToneLabPedalRegistry.GetDescriptor(left);
            string normalizedLeft = descriptor.SerializeSettingsObject(descriptor.DeserializeSettingsObject(leftJson));
            string normalizedRight = descriptor.SerializeSettingsObject(descriptor.DeserializeSettingsObject(rightJson));
            return string.Equals(normalizedLeft, normalizedRight, StringComparison.Ordinal);
        }
        catch
        {
            return false;
        }
    }

    private string GetPresetName(string presetId)
    {
        if (runtime == null || string.IsNullOrWhiteSpace(presetId))
            return "Preset";

        UnityToneLabRuntime.ToneLabPreset[] presets = runtime.CurrentPresets;
        for (int i = 0; i < presets.Length; i++)
        {
            UnityToneLabRuntime.ToneLabPreset preset = presets[i];
            if (preset != null && string.Equals(preset.preset_id, presetId, StringComparison.Ordinal))
                return string.IsNullOrWhiteSpace(preset.preset_name) ? "Preset" : preset.preset_name.Trim();
        }

        return "Preset";
    }

    private void ShowActionToast(string message, bool danger = false)
    {
        if (actionToast == null || actionToastLabel == null || string.IsNullOrWhiteSpace(message))
            return;

        actionToastLabel.text = message;
        actionToast.style.backgroundColor = danger
            ? new Color(0.26f, 0.11f, 0.12f, 0.98f)
            : new Color(0.15f, 0.18f, 0.20f, 0.98f);
        actionToast.style.borderTopColor = danger ? new Color(0.58f, 0.26f, 0.28f, 0.95f) : new Color(0.34f, 0.37f, 0.42f, 0.95f);
        actionToast.style.borderRightColor = danger ? new Color(0.42f, 0.18f, 0.20f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.borderBottomColor = danger ? new Color(0.34f, 0.15f, 0.16f, 0.98f) : new Color(0.18f, 0.20f, 0.23f, 0.98f);
        actionToast.style.borderLeftColor = danger ? new Color(0.42f, 0.18f, 0.20f, 0.98f) : new Color(0.24f, 0.26f, 0.30f, 0.98f);
        actionToast.style.display = DisplayStyle.Flex;

        CancelInvoke(nameof(HideActionToast));
        Invoke(nameof(HideActionToast), 1.8f);
    }

    private void HideActionToast()
    {
        if (actionToast != null)
            actionToast.style.display = DisplayStyle.None;
    }

    private void ApplyPresetNameFieldStyle()
    {
        if (presetNameField == null)
            return;

        presetNameField.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
        presetNameField.style.color = Color.white;

        VisualElement inputElement = presetNameField.Q(className: TextInputBaseField<string>.textInputUssName);
        if (inputElement != null)
        {
            inputElement.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            inputElement.style.color = Color.white;
            inputElement.style.unityTextAlign = TextAnchor.MiddleLeft;
        }

        VisualElement baseInputElement = presetNameField.Q(className: TextInputBaseField<string>.inputUssClassName);
        if (baseInputElement != null)
        {
            baseInputElement.style.backgroundColor = new Color(0.10f, 0.11f, 0.13f, 1f);
            baseInputElement.style.color = Color.white;
        }

        foreach (UnityEngine.UIElements.TextElement textElement in presetNameField.Query<UnityEngine.UIElements.TextElement>().ToList())
        {
            textElement.style.color = Color.white;
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
    }

    private static void ApplySearchFieldStyle(TextField searchField)
    {
        if (searchField == null)
            return;

        searchField.style.color = Color.white;

        VisualElement textInputElement =
            searchField.Q(className: TextInputBaseField<string>.textInputUssName)
            ?? searchField.Q(className: "unity-text-field__input")
            ?? searchField.Q(className: "unity-base-text-field__input")
            ?? searchField.Q(className: "unity-base-field__input");
        if (textInputElement != null)
        {
            textInputElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            textInputElement.style.color = Color.white;
            textInputElement.style.fontSize = 20f;
            textInputElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textInputElement.style.borderTopWidth = 0f;
            textInputElement.style.borderRightWidth = 0f;
            textInputElement.style.borderBottomWidth = 0f;
            textInputElement.style.borderLeftWidth = 0f;
        }

        foreach (UnityEngine.UIElements.TextElement textElement in searchField.Query<UnityEngine.UIElements.TextElement>().ToList())
        {
            textElement.style.color = Color.white;
            textElement.style.fontSize = 20f;
            textElement.style.unityTextAlign = TextAnchor.MiddleLeft;
            textElement.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        }
    }

    private static PanelSettings ResolvePanelSettings()
    {
        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");
        PanelSettings settings = runtimeAsset != null
            ? ScriptableObject.Instantiate(runtimeAsset)
            : ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "UnityToneLabRuntimePanelSettings";
        settings.scaleMode = PanelScaleMode.ScaleWithScreenSize;
        settings.referenceResolution = new Vector2Int(1920, 1080);
        settings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;
        settings.match = 0.5f;
        settings.scale = 1f;
        settings.targetDisplay = 0;
        settings.sortingOrder = 240;

        if (settings.themeStyleSheet == null)
            settings.themeStyleSheet = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>().FirstOrDefault();
        if (settings.textSettings == null)
            settings.textSettings = Resources.FindObjectsOfTypeAll<PanelTextSettings>().FirstOrDefault();

        return settings;
    }

    private void OnDestroy()
    {
        if (panelSettings != null)
            Destroy(panelSettings);
    }
}
