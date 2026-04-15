using System;

using System.Collections.Generic;

using System.Globalization;

using System.IO;

using System.Linq;

using UnityEngine;

using UnityEngine.Rendering;

#if UNITY_RENDER_PIPELINE_UNIVERSAL

using UnityEngine.Rendering.Universal;

#endif

using UnityEngine.TextCore.Text;

using UnityEngine.UIElements;

#if UNITY_EDITOR

using UnityEditor;

#endif



public sealed class TabsSongHeaderOverlay

{

    private static readonly Color MainMenuSelectedColor = new Color(0.44f, 0.84f, 1f, 1f);

    private static readonly Color LibraryPrimaryAccentColor = new Color(0.98f, 0.62f, 0.42f, 1f);

    private static readonly Color LibraryPrimaryAccentTextColor = new Color(0.20f, 0.11f, 0.07f, 1f);

    private static readonly Color LibrarySecondaryColor = new Color(0.271f, 0.780f, 0.757f, 1f);

    private static readonly Color LibrarySecondarySoftColor = new Color(0.271f, 0.780f, 0.757f, 0.88f);

    private static readonly Color LibrarySecondaryTextColor = new Color(0.12f, 0.18f, 0.24f, 1f);

    private static readonly Color LibraryPrimaryColor = LibrarySecondaryColor;

    private static readonly Color LibraryPrimarySoftColor = LibrarySecondarySoftColor;

    private static readonly Color LibraryPrimaryTextColor = LibrarySecondaryTextColor;

    private static readonly Color LibraryConfirmedSongColor = LibraryPrimaryAccentColor;

    private static readonly Color LibraryConfirmedSongTextColor = LibraryPrimaryAccentTextColor;

    private static readonly Color LibraryPanelColor = new Color(0.10f, 0.10f, 0.11f, 1f);

    private static readonly Color LibrarySectionLabelColor = new Color(0.74f, 0.88f, 0.98f, 0.94f);

    private static readonly Color LibraryCardColor = new Color(0.05f, 0.09f, 0.14f, 0.94f);

    private static readonly Color LibraryTrackRowColor = new Color(0.06f, 0.06f, 0.07f, 1f);

    private static readonly Color LibraryTrackPanelColor = new Color(0.04f, 0.07f, 0.11f, 0.56f);

    private static readonly Color MenuDarkButtonColor = new Color(0.03f, 0.03f, 0.04f, 1f);

    private static readonly Color MenuDarkButtonBorderColor = new Color(0.13f, 0.14f, 0.16f, 1f);

    private static readonly Color MenuOutlineNeutralColor = new Color(0.42f, 0.44f, 0.47f, 1f);

    private static readonly Color PrimaryAccentGradientLightColor = new Color(1.00f, 0.74f, 0.47f, 0.98f);

    private static readonly Color PrimaryAccentGradientMidColor = new Color(0.94f, 0.55f, 0.28f, 0.98f);

    private static readonly Color PrimaryAccentGradientDarkColor = new Color(0.71f, 0.31f, 0.18f, 0.98f);

    private const float PauseMenuButtonFontScale = 1.5f;

    private const float LibraryFooterButtonFontScale = 1.45f;

    private const float SongEndButtonFontScale = 1.32f;

    private const float SongEndButtonHeightScale = 1.18f;



    public static Color GlobalPrimaryAccentColor => LibraryConfirmedSongColor;

    public static Color GlobalSecondaryAccentColor => LibraryPrimaryColor;

    public static Color GlobalPanelColor => LibraryPanelColor;

    public static Color GlobalDeepPanelColor => new Color(0.03f, 0.03f, 0.035f, 1f);

    private const int CharacterHealthHeartCount = 30;
    private static readonly Color GameplayHudCardBackgroundColor = new Color(0.024f, 0.034f, 0.060f, 0.955f);

    private static readonly Color GameplayHudCardBorderTopColor = new Color(0.28f, 0.39f, 0.56f, 0.94f);

    private static readonly Color GameplayHudCardBorderSideColor = new Color(0.10f, 0.15f, 0.24f, 0.96f);

    private static readonly Color GameplayHudCardBorderBottomColor = new Color(0.09f, 0.13f, 0.20f, 0.96f);

    private static readonly Color GameplayHudAccentCoolColor = new Color(0.70f, 0.84f, 0.95f, 0.98f);

    private static readonly Color GameplayHudAccentWarmColor = new Color(0.96f, 0.73f, 0.49f, 0.98f);

    private static readonly Color GameplayHudPrimaryTextColor = new Color(0.97f, 0.98f, 1f, 1f);

    private static readonly Color GameplayHudSecondaryTextColor = new Color(0.82f, 0.88f, 0.94f, 0.98f);

    private static readonly Color GameplayHudMutedTextColor = new Color(0.64f, 0.72f, 0.81f, 0.96f);

    private static readonly Color GameplayHudEyebrowColor = new Color(0.57f, 0.68f, 0.81f, 0.94f);

    private static readonly Color GameplayHudChipBackgroundColor = new Color(0.055f, 0.082f, 0.135f, 0.96f);

    private static readonly Color GameplayHudChipBorderColor = new Color(0.14f, 0.20f, 0.31f, 0.98f);

    private static readonly Color GameplayHudDetectorOnlineColor = new Color(0.68f, 0.91f, 0.76f, 1f);

    private static readonly Color GameplayHudDetectorOfflineColor = new Color(0.94f, 0.63f, 0.61f, 1f);
    private static readonly Color GameplayHudHealthHeartColor = new Color(0.92f, 0.28f, 0.34f, 1f);
    private static readonly Color GameplayHudHealthHeartEmptyColor = new Color(0.41f, 0.45f, 0.54f, 0.95f);



    private static readonly Color[] LogoStringColors =

    {

        new Color(0.91f, 0.30f, 0.24f, 1f),

        new Color(0.95f, 0.77f, 0.06f, 1f),

        new Color(0.20f, 0.60f, 0.86f, 1f),

        new Color(0.90f, 0.49f, 0.13f, 1f), 

        new Color(0.18f, 0.80f, 0.44f, 1f),

        new Color(0.61f, 0.35f, 0.71f, 1f)

    };



    private readonly GuitarBridgeServer owner;

    private readonly GameObject rootObject;

    private readonly UIDocument document;

    private readonly PanelSettings panelSettings;



    private readonly VisualElement songCard;
    private readonly VisualElement characterHealthRow;

    private readonly VisualElement gameplayScoreDock;

    private readonly VisualElement songCardScoreBlock;

    private readonly Label songNameLabel;

    private readonly Label trackNameLabel;

    private readonly Label speedBadgeLabel;

    private readonly Label statusDotLabel;

    private readonly Label detectorStatusLabel;
    private readonly List<Label> characterHealthHeartLabels = new List<Label>();
    private int displayedCharacterHealthHeartCount = 5;

    private readonly VisualElement techniqueLegendCard;

    private readonly List<Label> techniqueLegendIconLabels = new List<Label>();

    private readonly List<Label> techniqueLegendTextLabels = new List<Label>();

    private readonly VisualElement scorePlate;

    private readonly VisualElement scorePedalBody;

    private readonly VisualElement scorePedalScreen;

    private readonly VisualElement scorePedalKnobLeft;

    private readonly VisualElement scorePedalKnobMid;

    private readonly VisualElement scorePedalKnobRight;

    private readonly VisualElement scorePedalLed;

    private readonly VisualElement scorePedalFootswitch;

    private readonly VisualElement scorePedalFootswitchRight;

    private readonly VisualElement scorePedalInputJack;

    private readonly VisualElement scorePedalOutputJack;

    private readonly Label scorePedalBrandLabel;

    private readonly Label scoreTitleLabel;

    private readonly Label scorePercentLabel;

    private readonly Label noteTallyLabel;

    private readonly VisualElement inputMeterWrap;

    private readonly Label inputMeterLabel;

    private readonly VisualElement inputMeterFace;

    private readonly VisualElement inputMeterArcViewport;

    private readonly VisualElement inputMeterArc;

    private readonly VisualElement inputMeterNeedle;

    private readonly VisualElement inputMeterNeedleCap;

    private readonly VisualElement songProgressTrack;

    private readonly VisualElement songProgressFill;

    private readonly List<VisualElement> inputMeterTicks = new List<VisualElement>();

    private readonly VisualElement judgePopupLayer;



    private readonly List<JudgePopupEntry> activeJudgePopups = new List<JudgePopupEntry>();



    private sealed class SongSelectionRow

    {

        public Button button;

        public VisualElement artworkTile;

        public Label artworkGlyphLabel;

        public VisualElement slantPlate;

        public VisualElement slantEdge;

        public VisualElement accentBar;

        public VisualElement scoreBadge;

        public Label indexLabel;

        public Label nameLabel;

        public Label metaLabel;

        public Label scoreLabel;

    }



    private sealed class TrackSelectionRow

    {

        public Button button;

        public VisualElement accentBar;

        public Label indexLabel;

        public Label nameLabel;

        public Label metaLabel;

        public Label scoreLabel;

    }



    private sealed class LibraryTrackRow

    {

        public Button button;

        public VisualElement accent;

        public Label nameLabel;

        public Label scoreLabel;

    }



    private sealed class MainMenuEntry

    {

        public Button button;

        public Label arrowLabel;

        public Label titleLabel;

        public Label subtitleLabel;

        public Color accentColor;

    }



    private sealed class GlobalSettingsMenuRow

    {

        public VisualElement row;

        public Label titleLabel;

        public Label valueLabel;

        public Button leftButton;

        public Button rightButton;

        public Label metaLabel;

    }



    private sealed class JudgePopupEntry

    {

        public Label label;

        public float startTime;

        public float startY;

        public float endY;

        public float duration;

        public float fontSize;

    }



    private sealed class LibraryBackdropBlurController : MonoBehaviour

    {

        private const string BlurShaderName = "Hidden/StringTheory/UIBackdropBlur";

        private const string BlurShaderResourcePath = "Shaders/UIBackdropBlur";

        private static readonly int BlurDirectionId = Shader.PropertyToID("_BlurDirection");

        private static readonly int BlurSizeId = Shader.PropertyToID("_BlurSize");

        private const int Downsample = 2;

        // Increase this value to make the live library backdrop blur stronger.

        private const float BlurSize = 1.5f;

        // Lower this value to make the live blurred backdrop darker.

        private const float DefaultBlurBrightness = 0.55f;

        private const int BlurPassPairs = 2; 



        public VisualElement TargetElement { get; set; }

        public Camera SourceCamera { get; set; }

        public bool UpdateContinuously { get; set; } = true;

        public float Brightness { get; set; } = DefaultBlurBrightness;



        private Camera captureCamera;

        private Material blurMaterial;

        private RenderTexture sceneTexture;

        private RenderTexture blurTextureA;

        private RenderTexture blurTextureB;

        private int textureWidth = -1;

        private int textureHeight = -1;

        private bool hasRenderedVisibleFrame;



        private void LateUpdate()

        {

            if (!ShouldRender())

            {

                hasRenderedVisibleFrame = false;

                return;

            }



            Camera sourceCamera = ResolveSourceCamera();

            if (sourceCamera == null)

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

            if (!UpdateContinuously && hasRenderedVisibleFrame && width == textureWidth && height == textureHeight)

                return;



            EnsureRenderTextures(width, height);

            EnsureCaptureCamera();



            captureCamera.CopyFrom(sourceCamera);

            captureCamera.enabled = false;

            captureCamera.targetTexture = sceneTexture;

            captureCamera.forceIntoRenderTexture = true;

            captureCamera.rect = new Rect(0f, 0f, 1f, 1f);

            captureCamera.stereoTargetEye = StereoTargetEyeMask.None;

#if UNITY_RENDER_PIPELINE_UNIVERSAL

            SyncUniversalCameraSettings(sourceCamera, captureCamera);

#endif

            captureCamera.Render();



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



            if (TargetElement != null)

            {

                TargetElement.style.backgroundImage = StyleKeyword.None;

                TargetElement.style.backgroundImage = new StyleBackground(Background.FromRenderTexture(blurTextureB));

                TargetElement.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

                TargetElement.style.unityBackgroundImageTintColor = new Color(Brightness, Brightness, Brightness, 1f);

            }



            hasRenderedVisibleFrame = true;

        }



        private bool ShouldRender()

        {

            return TargetElement != null

                && TargetElement.style.display.value != DisplayStyle.None

                && TargetElement.worldBound.width > 16f

                && TargetElement.worldBound.height > 16f;

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



            GameObject cameraObject = new GameObject("LibraryBackdropBlurCamera");

            cameraObject.hideFlags = HideFlags.HideAndDontSave;

            cameraObject.transform.SetParent(transform, false);

            captureCamera = cameraObject.AddComponent<Camera>();

            captureCamera.enabled = false;

#if UNITY_RENDER_PIPELINE_UNIVERSAL

            cameraObject.AddComponent<UniversalAdditionalCameraData>();

#endif

        }



        private void EnsureRenderTextures(int width, int height)

        {

            if (width == textureWidth && height == textureHeight && sceneTexture != null && blurTextureA != null && blurTextureB != null)

                return;



            ReleaseRenderTextures();

            textureWidth = width;

            textureHeight = height;

            sceneTexture = CreateBlurTexture("LibraryBackdropScene", width, height);

            blurTextureA = CreateBlurTexture("LibraryBackdropBlurA", width, height);

            blurTextureB = CreateBlurTexture("LibraryBackdropBlurB", width, height);

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

            textureWidth = -1;

            textureHeight = -1;

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



    private sealed class EnumCycleControl : VisualElement

    {

        private readonly List<string> options;

        private readonly Label valueLabel;

        private bool suppress;



        public Action<string> OnValueChanged;



        public EnumCycleControl(IEnumerable<string> enumOptions, string initialValue, Func<string, float, Color, bool, TextAnchor, bool, Label> createLabel, Func<string, Action, Button> createButton)

        {

            options = enumOptions?.Where(option => !string.IsNullOrWhiteSpace(option)).Distinct().ToList() ?? new List<string>();

            style.flexDirection = FlexDirection.Row;

            style.alignItems = Align.Center;

            style.marginBottom = 6f;



            Button prev = createButton("\u25C0", () => Shift(-1));

            prev.style.minWidth = 90f;

            prev.style.height = 58f;

            prev.style.marginRight = 8f;



            valueLabel = createLabel(string.Empty, 34f, new Color(0.90f, 0.96f, 1f, 1f), true, TextAnchor.MiddleCenter, false);

            valueLabel.style.flexGrow = 1f;

            valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            valueLabel.AddToClassList("global-setting-enum-value");



            Button next = createButton("\u25B6", () => Shift(1));

            next.style.minWidth = 90f;

            next.style.height = 58f;

            next.style.marginLeft = 8f;



            Add(prev);

            Add(valueLabel);

            Add(next);



            SetValueWithoutNotify(initialValue);

        }



        public void SetValueWithoutNotify(string value)

        {

            if (options.Count == 0)

            {

                valueLabel.text = string.IsNullOrEmpty(value) ? "--" : value;

                return;

            }



            string resolved = options.FirstOrDefault(option => string.Equals(option, value, StringComparison.OrdinalIgnoreCase)) ?? options[0];

            suppress = true;

            valueLabel.text = resolved;

            suppress = false;

        }



        private void Shift(int delta)

        {

            if (options.Count == 0)

                return;



            string current = valueLabel.text;

            int currentIndex = options.FindIndex(option => string.Equals(option, current, StringComparison.OrdinalIgnoreCase));

            if (currentIndex < 0)

                currentIndex = 0;



            int nextIndex = (currentIndex + delta + options.Count) % options.Count;

            string next = options[nextIndex];

            valueLabel.text = next;

            if (!suppress)

                OnValueChanged?.Invoke(next);

        }

    }



    private readonly VisualElement pauseOverlay;

    private readonly VisualElement pauseBlurBackdrop;
    private readonly VisualElement pauseBackplate;
    private readonly VisualElement pauseFrontPlate;
    private readonly VisualElement pauseRegion;
    private readonly VisualElement pauseShell;
    private readonly VisualElement pauseCard;
    private readonly VisualElement pauseButtons;
    private readonly VisualElement pauseEndRow;

    private readonly Label pauseTitleLabel;

    private readonly Label pauseHintLabel;

    private readonly Label pauseInfoLabel;

    private readonly VisualElement loopSetupOverlay;

    private readonly VisualElement loopSetupBar;

    private readonly Label loopSetupStatusLabel;

    private readonly Label loopSetupHintLabel;

    private readonly VisualElement offsetHelperOverlay;

    private readonly VisualElement offsetHelperBar;

    private readonly Label offsetHelperTitleLabel;

    private readonly Label offsetHelperStatusLabel;

    private readonly Label offsetHelperHintLabel;

    private readonly VisualElement gameModesOverlay;
    private readonly VisualElement gameModesBackplate;
    private readonly VisualElement gameModesFrontPlate;
    private readonly VisualElement gameModesRegion;
    private readonly VisualElement gameModesShell;
    private readonly VisualElement gameModesCard;
    private readonly VisualElement gameModesButtons;
    private readonly VisualElement gameModesFooterRow;
    private readonly Label gameModesHintLabel;
    private readonly Label gameModesInfoLabel;
    private readonly Button gameModesBackButton;
    private readonly ModernMenuPopup gameModesPopup;
    private readonly VisualElement heroModeSettingsOverlay;
    private readonly ModernMenuPopup heroModeSettingsPopup;
    private readonly Label heroModeSettingsMessageLabel;
    private readonly Label heroModeSettingsCalloutLabel;
    private readonly Label heroModeSettingsHintLabel;
    private readonly VisualElement heroModeSettingsValueRow;
    private readonly Label heroModeSettingsValueLabel;
    private readonly Button heroModeSettingsLeftArrowButton;
    private readonly Button heroModeSettingsRightArrowButton;
    private readonly Button gameModesButton;
    private readonly Button loopButton;
    private readonly Button gameModesLoopConfigButton;
    private readonly Button noteByNoteButton;
    private readonly Button heroModeButton;
    private readonly Button heroModeSettingsButton;
    private readonly Button audioModeButton;
    private readonly VisualElement notesDetectorTestOverlay;
    private readonly VisualElement notesDetectorTestBackplate;
    private readonly VisualElement notesDetectorTestFrontPlate;
    private readonly VisualElement notesDetectorTestRegion;
    private readonly VisualElement notesDetectorTestShell;
    private readonly VisualElement notesDetectorTestCard;
    private readonly VisualElement notesDetectorTestButtons;
    private readonly VisualElement notesDetectorTestFooterRow;
    private readonly Label notesDetectorTestInfoLabel;
    private readonly Label notesDetectorTestStatusLabel;
    private readonly Label notesDetectorTestDetailLabel;
    private readonly Label notesDetectorTestRuntimeLabel;
    private readonly Label notesDetectorTestFastNotesLabel;
    private readonly Label notesDetectorTestAiNotesLabel;
    private readonly DropdownField notesDetectorTestInputDeviceDropdown;
    private readonly DropdownField notesDetectorPresetDropdown;
    private readonly Button notesDetectorRestartButton;
    private readonly Button notesDetectorRunTestButton;
    private readonly Button notesDetectorBackButton;
    private readonly Button notesDetectorRefreshDevicesButton;
    private readonly Button notesDetectorSavePresetButton;
    private readonly Button notesDetectorAdvancedSettingsToggleButton;
    private readonly ScrollView notesDetectorSettingsScrollView;
    private readonly VisualElement notesDetectorRoutineOverlay;
    private readonly VisualElement notesDetectorRoutineTopBar;
    private readonly Button notesDetectorRoutineRestartButton;
    private readonly Button notesDetectorRoutineExitButton;
    private readonly Label notesDetectorRoutineMessageLabel;
    private readonly Label notesDetectorRoutineCalloutLabel;
    private readonly Label notesDetectorRoutineHintLabel;
    private readonly Label notesDetectorRoutineStatusLabel;
    private readonly Label notesDetectorRoutineFastNotesLabel;
    private readonly Label notesDetectorRoutineAiNotesLabel;
    private readonly List<Label> notesDetectorRoutineTabRowLabels = new List<Label>();
    private readonly Dictionary<string, Slider> notesDetectorSettingSliders = new Dictionary<string, Slider>();
    private readonly Dictionary<string, Label> notesDetectorSettingValueLabels = new Dictionary<string, Label>();
    private readonly Dictionary<string, VisualElement> notesDetectorSettingRows = new Dictionary<string, VisualElement>();
    private bool suppressNotesDetectorDropdownCallbacks;
    private bool suppressNotesDetectorSettingCallbacks;
    private bool showNotesDetectorAdvancedSettings;

    private readonly List<Button> pauseActionButtons = new List<Button>();
    private readonly List<Button> gameModesActionButtons = new List<Button>();
    private readonly List<Button> notesDetectorTestActionButtons = new List<Button>();



    private readonly VisualElement mainMenuOverlay;

    private readonly VisualElement mainMenuBackgroundPlane;

    private readonly VisualElement mainMenuShell;

    private readonly VisualElement mainMenuLeftColumn;

    private readonly VisualElement mainMenuRightColumn;

    private readonly VisualElement mainMenuNavColumn;

    private readonly Label mainMenuEyebrowLabel;

    private readonly Label mainMenuTitleLabel;

    private readonly Label mainMenuSubtitleLabel;

    private readonly Label mainMenuFooterHintLabel;

    private readonly Label mainMenuCurrentSongValueLabel;

    private readonly Label mainMenuCurrentTrackValueLabel;

    private readonly Label mainMenuCurrentSpeedValueLabel;

    private readonly Label mainMenuCurrentDetectorValueLabel;

    private readonly List<MainMenuEntry> mainMenuEntries = new List<MainMenuEntry>();

    private readonly Slider speedSlider;

    private readonly Label speedValueLabel;



    private readonly VisualElement settingsOverlay;

    private readonly VisualElement settingsBlurBackdrop;

    private readonly VisualElement settingsBackplate;

    private readonly VisualElement settingsFrontPlate;

    private readonly VisualElement settingsRegion;

    private readonly VisualElement settingsShell;

    private readonly VisualElement settingsCard;

    private readonly Label settingsTrackLabel;

    private readonly Label settingsHintLabel;

    private readonly Label settingsOffsetLabel;

    private readonly Slider settingsOffsetSlider;

    private readonly Label settingsTabSpeedLabel;

    private readonly Slider settingsTabSpeedSlider;

    private readonly Label settingsStartDelayLabel;

    private readonly Slider settingsStartDelaySlider;

    private readonly Label settingsVolumeLabel;

    private readonly Slider settingsVolumeSlider;

    private readonly VisualElement settingsOffsetRow;

    private readonly VisualElement settingsTabSpeedRow;

    private readonly VisualElement settingsStartDelayRow;

    private readonly VisualElement settingsVolumeRow;

    private readonly VisualElement settingsTrackRow;

    private readonly VisualElement settingsOffsetScopeRow;
    private readonly Button settingsPrimaryToolButton;
    private readonly Label settingsPrimaryToolHelpLabel;
    private readonly Label settingsOffsetHelpLabel;
    private readonly Label settingsOffsetScopeHelpLabel;

    private readonly Button settingsTrackButton;

    private readonly Button settingsOffsetScopeButton;

    private readonly Button settingsTrackLeftArrowButton;

    private readonly Button settingsTrackRightArrowButton;

    private readonly Button settingsOffsetScopeLeftArrowButton;

    private readonly Button settingsOffsetScopeRightArrowButton;

    private readonly VisualElement generatedAudioTracksPopupOverlay;
    private readonly Label generatedAudioTracksPopupSummaryLabel;
    private readonly Label generatedAudioTracksPopupHintLabel;
    private readonly Button generatedAudioTracksSelectAllButton;
    private readonly Button generatedAudioTracksDeselectAllButton;
    private readonly Button generatedAudioTracksCloseButton;
    private readonly ScrollView generatedAudioTracksPopupScrollView;
    private readonly List<Button> generatedAudioTracksPopupTrackButtons = new List<Button>();

    private readonly List<Button> songSettingsActionButtons = new List<Button>();
    private readonly List<int> songSettingsActionSelectionIndices = new List<int>();



    private readonly VisualElement globalSettingsOverlay;

    private readonly VisualElement globalSettingsCard;

    private readonly ScrollView globalSettingsScrollView;

    private readonly Button resetDefaultsButton;

    private readonly Label globalSettingsTitleLabel;

    private readonly Label globalSettingsHelpLabel;

    private readonly List<GlobalSettingsMenuRow> globalSettingsMenuRows = new List<GlobalSettingsMenuRow>();

    private readonly Dictionary<string, VisualElement> globalSettingInputs = new Dictionary<string, VisualElement>();

    private readonly Dictionary<string, Label> globalSettingValueLabels = new Dictionary<string, Label>();

    private readonly Dictionary<string, VisualElement> globalSettingsColumns = new Dictionary<string, VisualElement>();



    private readonly VisualElement selectionOverlay;

    private readonly VisualElement selectionLeftBackdrop;

    private readonly VisualElement selectionShell;

    private readonly VisualElement selectionInfoColumn;

    private readonly VisualElement selectionInfoCard;

    private readonly VisualElement selectionArrangementCard;

    private readonly VisualElement selectionRailPanel;

    private readonly VisualElement selectionRailBackdrop;

    private readonly VisualElement selectionRailBackdropGradient;

    private readonly VisualElement selectionSplitDivider;

    private readonly Label selectionSubtitleLabel;

    private readonly Label selectionInfoInstructionLabel;

    private readonly Label selectionInfoSummaryLabel;

    private readonly VisualElement selectionArtworkCard;

    private readonly Label selectionArtworkGlyphLabel;

    private readonly Label selectionArtworkCaptionLabel;

    private readonly Label selectionInfoTitleLabel;

    private readonly Label selectionInfoMetaLabel;

    private readonly Label selectionInfoScoreLabel;

    private readonly Label selectionInfoBestTrackLabel;

    private readonly Label selectionInfoArrangementValueLabel;

    private readonly Label selectionInfoAudioValueLabel;

    private readonly Label selectionInfoHeroHeartsValueLabel;

    private readonly Label selectionInfoHintLabel;

    private readonly List<Label> selectionInfoStatTitleLabels = new List<Label>();

    private readonly ScrollView selectionTrackScrollView;

    private readonly List<LibraryTrackRow> selectionTrackRows = new List<LibraryTrackRow>();

    private readonly Button selectionBackButton;

    private readonly Button selectionSongsFolderButton;

    private readonly Button selectionRefreshButton;

    private readonly Button selectionStartButton;

    private readonly VisualElement selectionFooter;

    private readonly ScrollView selectionScrollView;

    private readonly List<SongSelectionRow> selectionRows = new List<SongSelectionRow>();

    private float currentMenuLayoutScale = 1f;

    private bool currentCompactSelectionLayout;



    private readonly VisualElement trackSelectionOverlay;

    private readonly VisualElement trackSelectionShell;

    private readonly VisualElement trackSelectionInfoCard;

    private readonly Label trackSelectionTitleLabel;

    private readonly Label trackSelectionSubtitleLabel;

    private readonly Label trackSelectionInfoTitleLabel;

    private readonly Label trackSelectionInfoMetaLabel;

    private readonly Label trackSelectionInfoScoreLabel;

    private readonly Label trackSelectionInfoHintLabel;

    private readonly ScrollView trackSelectionScrollView;

    private readonly List<TrackSelectionRow> trackSelectionRows = new List<TrackSelectionRow>();



    private readonly VisualElement songEndBlurBackdrop;

    private readonly VisualElement songEndOverlay;

    private readonly VisualElement songEndCard;

    private readonly Label songEndTitleLabel;

    private readonly Label songEndSongLabel;

    private readonly Label songEndMetaLabel;

    private readonly Label songEndSpeedValueLabel;

    private readonly Label songEndScoreLabel;

    private readonly Label songEndBestLabel;

    private readonly Label songEndDeltaLabel;

    private readonly Label songEndRatingLabel;

    private readonly Label songEndStatsLabel;

    private readonly Label songEndNoteLabel;

    private readonly Button songEndRetryButton;

    private readonly Button songEndSelectionButton;

    private readonly Button songEndMainMenuButton;



    private readonly VisualElement startupTuningReminderOverlay;

    private readonly ModernMenuPopup startupTuningReminderPopup;

    private readonly VisualElement loopPausePopupOverlay;

    private readonly ModernMenuPopup loopPausePopup;

    private readonly Label loopPauseDurationLabel;

    private readonly Slider loopPauseDurationSlider;

    private readonly Button loopPauseAcceptButton;

    private readonly VisualElement loopPauseCountdownHost;

    private readonly VisualElement loopPauseCountdownDial;



    private int lastScreenHeight = -1;

    private int lastScreenWidth = -1;

    private bool suppressCallbacks;

    private bool hasSeenSnapshot;

    private int lastResolvedCount;

    private int hitStreak;

    private int lastMainMenuSelectionIndex = -1;

    private float judgePopupFontSize = 82f;

    private bool highwayCharacterVisible = true;

    private float displayedInputMeterLevel;

    private int lastAutoScrolledSongIndex = -1;

    private int lastAutoScrolledTrackIndex = -1;

    private int hoveredSongRowIndex = -1;

    private int hoveredLibraryTrackRowIndex = -1;

    private string lastSongEndSignature;

    private Texture2D libraryConfirmedSongGradientTexture;

    private Texture2D librarySelectionRailGradientTexture;

    private readonly Dictionary<string, Texture2D> libraryArtworkTextureCache = new Dictionary<string, Texture2D>(StringComparer.OrdinalIgnoreCase);

    private readonly HashSet<string> failedLibraryArtworkPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

    private Texture2D pauseBackplateGradientTexture;

    private Texture2D songEndAccentGradientTexture;

    private Texture2D songEndPanelBackdropTexture;

    private Texture2D songEndHeroGlowTexture;

    private Texture2D loopPauseCountdownTexture;

    private readonly LibraryBackdropBlurController libraryBackdropBlur;

    private readonly LibraryBackdropBlurController settingsBackdropBlur;

    private readonly LibraryBackdropBlurController songEndBackdropBlur;



    private readonly HashSet<int> scoredNoteIds = new HashSet<int>();

    private int scoreHits;

    private int scoreMisses;

    private float lastSongTime = -1f;

    private bool wasLoopEnabled;

    private string lastLoopSignature = string.Empty;

    private readonly FontDefinition bodyFontDefinition;

    private readonly FontDefinition titleFontDefinition;

    private readonly FontDefinition logoFontDefinition;

    private readonly FontDefinition modernUiFontDefinition;

    private string globalSettingsLayoutSignature = string.Empty;

    private Vector2 globalSettingsScrollOffset = Vector2.zero;

    private string globalSettingsFullscreenSignature = string.Empty;

    private float globalSettingsManualScrollUntil = -1f;

    private string lastGlobalSettingsCenteredSelectionSignature = string.Empty;

    private float lastLoopPauseCountdownProgress = -1f;



    public TabsSongHeaderOverlay(GuitarBridgeServer owner)

    {

        this.owner = owner;



        GameObject existingHeaderUi = GameObject.Find("TabsSongHeaderUI");

        if (existingHeaderUi != null)

            UnityEngine.Object.Destroy(existingHeaderUi);



        rootObject = new GameObject("TabsSongHeaderUI");

        document = rootObject.AddComponent<UIDocument>();



        panelSettings = ResolvePanelSettings();

        panelSettings.scaleMode = PanelScaleMode.ScaleWithScreenSize;

        panelSettings.referenceResolution = new Vector2Int(3840, 2160);

        panelSettings.screenMatchMode = PanelScreenMatchMode.MatchWidthOrHeight;

        panelSettings.match = 1f;

        panelSettings.scale = 1f;

        panelSettings.targetDisplay = 0;

        panelSettings.sortingOrder = 220;

        EnsurePanelSettingsSupportAssets(panelSettings);

        document.panelSettings = panelSettings;



        Font fallbackFont = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");

        (Font bodyFont, Font titleFont) = ResolveUiFonts(fallbackFont);

        Font logoFont = LoadRuntimeFont("MetalLord") ?? LoadRuntimeFont("Fonts/MetalLord");

        logoFont ??= LoadProjectFont("Assets/Resources/MetalLord.otf");

        logoFont ??= titleFont;

        bodyFontDefinition = FontDefinition.FromFont(bodyFont);

        titleFontDefinition = FontDefinition.FromFont(titleFont);

        logoFontDefinition = FontDefinition.FromFont(logoFont);

        modernUiFontDefinition = FontDefinition.FromFont(fallbackFont);



        VisualElement root = document.rootVisualElement;

        root.style.flexGrow = 1f;

        root.style.paddingTop = 30f;

        root.style.paddingLeft = 34f;

        root.style.paddingRight = 34f;

        root.style.paddingBottom = 30f;

        root.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.20f);



        songCard = new VisualElement();

        songCard.style.minWidth = 560f;

        songCard.style.maxWidth = 960f;

        songCard.style.paddingLeft = 34f;

        songCard.style.paddingRight = 34f;

        songCard.style.paddingTop = 18f;

        songCard.style.paddingBottom = 20f;

        songCard.style.marginBottom = 14f;

        songCard.style.marginRight = 24f;

        StyleGameplayHudCard(songCard, GameplayHudCardBackgroundColor, radius: 16f);

        VisualElement songCardTopRow = new VisualElement();

        songCardTopRow.style.flexDirection = FlexDirection.Row;

        songCardTopRow.style.alignItems = Align.Center;

        songCardTopRow.style.justifyContent = Justify.SpaceBetween;

        songCardTopRow.style.marginBottom = 12f;

        Label songCardEyebrowLabel = CreateLabel("NOW PLAYING", 16f, GameplayHudEyebrowColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        songCardEyebrowLabel.style.letterSpacing = 1.3f;

        songCardEyebrowLabel.style.unityFontDefinition = modernUiFontDefinition;



        songNameLabel = CreateLabel("Song", 42f, GameplayHudPrimaryTextColor, bold: true, useTitleFont: true);

        songNameLabel.style.marginBottom = 8f;

        songNameLabel.style.letterSpacing = 0.25f;

        songNameLabel.style.whiteSpace = WhiteSpace.Normal;

        songNameLabel.style.textOverflow = TextOverflow.Clip;

        songNameLabel.style.overflow = Overflow.Visible;

        songNameLabel.style.maxWidth = 1200f;



        VisualElement compactSongCardLogo = CreateStringTheoryLogo(34f, 32f, 22f, 0.7f, -4f, 1f);

        compactSongCardLogo.style.alignSelf = Align.Center;

        compactSongCardLogo.style.marginBottom = 0f;

        compactSongCardLogo.style.opacity = 0.72f;

        compactSongCardLogo.style.scale = new Scale(new Vector3(0.84f, 0.84f, 1f));

        songCardTopRow.Add(songCardEyebrowLabel);

        songCardTopRow.Add(compactSongCardLogo);



        trackNameLabel = CreateLabel("Lead Guitar", 26f, GameplayHudSecondaryTextColor, bold: false);

        trackNameLabel.style.letterSpacing = 0.85f;

        trackNameLabel.style.unityFontDefinition = modernUiFontDefinition;

        trackNameLabel.style.whiteSpace = WhiteSpace.NoWrap;

        trackNameLabel.style.textOverflow = TextOverflow.Ellipsis;

        trackNameLabel.style.overflow = Overflow.Hidden;

        trackNameLabel.style.maxWidth = 1200f;

        VisualElement trackChip = new VisualElement();

        trackChip.style.alignSelf = Align.FlexStart;

        trackChip.style.marginBottom = 12f;

        trackChip.Add(trackNameLabel);



        VisualElement statusRow = new VisualElement();

        statusRow.style.flexDirection = FlexDirection.Row;

        statusRow.style.alignItems = Align.Center;

        statusRow.style.marginTop = 0f;



        speedBadgeLabel = CreateLabel("Speed 100%", 24f, GameplayHudAccentWarmColor, bold: true, useTitleFont: false);

        speedBadgeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        speedBadgeLabel.style.letterSpacing = 0.35f;

        speedBadgeLabel.style.unityFontDefinition = modernUiFontDefinition;

        VisualElement speedChip = new VisualElement();

        speedChip.style.marginRight = 10f;

        speedChip.Add(speedBadgeLabel);



        statusDotLabel = CreateLabel(" \u2022 ", 24f, GameplayHudMutedTextColor, bold: true, useTitleFont: false);

        statusDotLabel.style.display = DisplayStyle.Flex;

        statusDotLabel.style.marginLeft = 2f;

        statusDotLabel.style.marginRight = 10f;



        detectorStatusLabel = CreateLabel("Detector Offline", 24f, GameplayHudDetectorOfflineColor, bold: true, useTitleFont: false);

        detectorStatusLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        detectorStatusLabel.style.letterSpacing = 0.22f;

        detectorStatusLabel.style.unityFontDefinition = modernUiFontDefinition;

        VisualElement detectorChip = new VisualElement();

        detectorChip.Add(detectorStatusLabel);



        statusRow.Add(speedChip);

        statusRow.Add(statusDotLabel);

        statusRow.Add(detectorChip);



        characterHealthRow = new VisualElement();

        characterHealthRow.style.flexDirection = FlexDirection.Row;

        characterHealthRow.style.alignItems = Align.Center;

        characterHealthRow.style.alignSelf = Align.FlexStart;

        characterHealthRow.style.marginTop = 0f;

        characterHealthRow.style.marginBottom = 20f;

        characterHealthRow.style.paddingLeft = 12f;

        characterHealthRow.style.height = 42f;

        characterHealthRow.style.transformOrigin = new TransformOrigin(Length.Percent(0f), Length.Percent(0f), 0f);

        characterHealthRow.style.scale = new Scale(new Vector3(0.8f, 0.8f, 1f));

        characterHealthRow.style.display = DisplayStyle.None;

        characterHealthRow.pickingMode = PickingMode.Ignore;

        for (int i = 0; i < CharacterHealthHeartCount; i++)

        {

            Label heartLabel = CreateLabel("\u2665", 40f, GameplayHudHealthHeartColor, bold: true, TextAnchor.MiddleLeft, useTitleFont: false);

            heartLabel.style.unityFontDefinition = modernUiFontDefinition;

            heartLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

            heartLabel.style.width = 28f;

            heartLabel.style.height = 28f;

            heartLabel.style.marginRight = i < CharacterHealthHeartCount - 1 ? 6f : 0f;

            heartLabel.style.opacity = 0.98f;

            characterHealthHeartLabels.Add(heartLabel);

            characterHealthRow.Add(heartLabel);

        }

        UpdateCharacterHealthHearts(0, displayedCharacterHealthHeartCount);



        gameplayScoreDock = new VisualElement();

        gameplayScoreDock.style.position = Position.Absolute;

        gameplayScoreDock.style.left = 0f;

        gameplayScoreDock.style.right = 0f;

        gameplayScoreDock.style.top = 12f;

        gameplayScoreDock.style.alignItems = Align.Center;

        gameplayScoreDock.style.justifyContent = Justify.FlexStart;

        gameplayScoreDock.style.display = DisplayStyle.None;

        gameplayScoreDock.pickingMode = PickingMode.Ignore;



        songCardScoreBlock = new VisualElement();

        songCardScoreBlock.style.alignItems = Align.Center;

        songCardScoreBlock.style.justifyContent = Justify.Center;

        songCardScoreBlock.style.flexShrink = 0f;

        songCardScoreBlock.style.paddingLeft = 28f;

        songCardScoreBlock.style.paddingRight = 28f;

        songCardScoreBlock.style.paddingTop = 16f;

        songCardScoreBlock.style.paddingBottom = 16f;

        StyleGameplayHudCard(songCardScoreBlock, new Color(0.022f, 0.040f, 0.074f, 0.95f), radius: 14f);

        songCardScoreBlock.pickingMode = PickingMode.Ignore;



        techniqueLegendCard = new VisualElement();

        techniqueLegendCard.style.position = Position.Absolute;

        techniqueLegendCard.style.top = 24f;

        techniqueLegendCard.style.right = 24f;

        techniqueLegendCard.style.paddingLeft = 16f;

        techniqueLegendCard.style.paddingRight = 16f;

        techniqueLegendCard.style.paddingTop = 12f;

        techniqueLegendCard.style.paddingBottom = 12f;

        techniqueLegendCard.style.alignItems = Align.FlexStart;

        techniqueLegendCard.style.minWidth = 255f;

        techniqueLegendCard.style.display = DisplayStyle.None;

        StyleCard(techniqueLegendCard, new Color(0.03f, 0.07f, 0.14f, 0.90f), radius: 14f);

        techniqueLegendCard.style.borderTopWidth = 1f;

        techniqueLegendCard.style.borderRightWidth = 1f;

        techniqueLegendCard.style.borderBottomWidth = 1f;

        techniqueLegendCard.style.borderLeftWidth = 1f;

        Color legendBorder = new Color(0.41f, 0.65f, 0.93f, 0.55f);

        techniqueLegendCard.style.borderTopColor = legendBorder;

        techniqueLegendCard.style.borderRightColor = legendBorder;

        techniqueLegendCard.style.borderBottomColor = legendBorder;

        techniqueLegendCard.style.borderLeftColor = legendBorder;



        AddTechniqueLegendRow("H", "Hammer-on", new Color(0.55f, 0.91f, 1f, 1f));

        AddTechniqueLegendRow("P", "Pull-off", new Color(0.57f, 1f, 0.74f, 1f));

        AddTechniqueLegendRow("/", "Slide up", new Color(1f, 0.89f, 0.48f, 1f));

        AddTechniqueLegendRow("\\", "Slide down", new Color(1f, 0.78f, 0.48f, 1f));

        AddTechniqueLegendRow("^", "Bend", new Color(1f, 0.66f, 0.73f, 1f));

        AddTechniqueLegendRow("~", "Vibrato", new Color(0.83f, 0.73f, 1f, 1f));



        scorePlate = new VisualElement();

        scorePlate.style.position = Position.Absolute;

        scorePlate.style.top = 8f;

        scorePlate.style.left = 0f;

        scorePlate.style.right = 0f;

        scorePlate.style.alignItems = Align.Center;

        scorePlate.style.justifyContent = Justify.Center;

        scorePlate.style.height = 252f;

        scorePlate.style.display = DisplayStyle.None;



        scorePedalBody = new VisualElement();

        scorePedalBody.style.width = 600f;

        scorePedalBody.style.height = 226f;

        scorePedalBody.style.paddingTop = 10f;

        scorePedalBody.style.paddingBottom = 12f;

        scorePedalBody.style.paddingLeft = 14f;

        scorePedalBody.style.paddingRight = 14f;

        scorePedalBody.style.backgroundColor = new Color(0.07f, 0.57f, 0.62f, 0.98f);

        scorePedalBody.style.borderTopWidth = 4f;

        scorePedalBody.style.borderRightWidth = 4f;

        scorePedalBody.style.borderBottomWidth = 12f;

        scorePedalBody.style.borderLeftWidth = 4f;

        Color pedalBorderColor = new Color(0.05f, 0.40f, 0.45f, 0.98f);

        scorePedalBody.style.borderTopColor = pedalBorderColor;

        scorePedalBody.style.borderRightColor = pedalBorderColor;

        scorePedalBody.style.borderBottomColor = pedalBorderColor;

        scorePedalBody.style.borderLeftColor = pedalBorderColor;

        scorePedalBody.style.borderTopLeftRadius = 12f;

        scorePedalBody.style.borderTopRightRadius = 12f;

        scorePedalBody.style.borderBottomLeftRadius = 18f;

        scorePedalBody.style.borderBottomRightRadius = 18f;

        scorePedalBody.style.alignItems = Align.Stretch;



        scorePedalInputJack = CreatePedalJack();

        scorePedalInputJack.style.position = Position.Absolute;

        scorePedalInputJack.style.left = -24f;

        scorePedalInputJack.style.top = 102f;



        scorePedalOutputJack = CreatePedalJack();

        scorePedalOutputJack.style.position = Position.Absolute;

        scorePedalOutputJack.style.scale = new Scale(new Vector3(-1f, 1f, 1f));

        scorePedalOutputJack.style.right = -24f;

        scorePedalOutputJack.style.top = 102f;



        VisualElement pedalFace = new VisualElement();

        pedalFace.style.flexGrow = 1f;

        pedalFace.style.paddingTop = 8f;

        pedalFace.style.paddingBottom = 8f;

        pedalFace.style.paddingLeft = 10f;

        pedalFace.style.paddingRight = 10f;

        pedalFace.style.borderTopWidth = 3f;

        pedalFace.style.borderRightWidth = 3f;

        pedalFace.style.borderBottomWidth = 3f;

        pedalFace.style.borderLeftWidth = 3f;

        pedalFace.style.borderTopColor = new Color(0.99f, 0.99f, 0.99f, 0.98f);

        pedalFace.style.borderRightColor = new Color(0.94f, 0.98f, 0.99f, 0.95f);

        pedalFace.style.borderBottomColor = new Color(0.88f, 0.95f, 0.97f, 0.93f);

        pedalFace.style.borderLeftColor = new Color(0.94f, 0.98f, 0.99f, 0.95f);

        pedalFace.style.borderTopLeftRadius = 8f;

        pedalFace.style.borderTopRightRadius = 8f;

        pedalFace.style.borderBottomLeftRadius = 12f;

        pedalFace.style.borderBottomRightRadius = 12f;



        VisualElement pedalTopRow = new VisualElement();

        pedalTopRow.style.flexDirection = FlexDirection.Row;

        pedalTopRow.style.alignItems = Align.Center;

        pedalTopRow.style.justifyContent = Justify.SpaceBetween;

        pedalTopRow.style.marginBottom = 12f;



        scorePedalBrandLabel = CreateLabel("STRING THEORY", 18f, new Color(0.95f, 0.99f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: true);

        scorePedalBrandLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        scorePedalBrandLabel.style.letterSpacing = 0.9f;

        scorePedalBrandLabel.style.marginBottom = 4f;



        scorePedalLed = new VisualElement();

        scorePedalLed.style.width = 14f;

        scorePedalLed.style.height = 14f;

        scorePedalLed.style.backgroundColor = new Color(1f, 0.35f, 0.22f, 1f);

        scorePedalLed.style.borderTopLeftRadius = 7f;

        scorePedalLed.style.borderTopRightRadius = 7f;

        scorePedalLed.style.borderBottomLeftRadius = 7f;

        scorePedalLed.style.borderBottomRightRadius = 7f;

        scorePedalLed.style.borderTopWidth = 2f;

        scorePedalLed.style.borderRightWidth = 2f;

        scorePedalLed.style.borderBottomWidth = 2f;

        scorePedalLed.style.borderLeftWidth = 2f;

        scorePedalLed.style.borderTopColor = new Color(1f, 0.72f, 0.62f, 1f);

        scorePedalLed.style.borderRightColor = new Color(0.66f, 0.14f, 0.10f, 1f);

        scorePedalLed.style.borderBottomColor = new Color(0.53f, 0.10f, 0.08f, 1f);

        scorePedalLed.style.borderLeftColor = new Color(0.66f, 0.14f, 0.10f, 1f);



        pedalTopRow.Add(scorePedalBrandLabel);

        pedalTopRow.Add(scorePedalLed);



        VisualElement pedalKnobRow = new VisualElement();

        pedalKnobRow.style.flexDirection = FlexDirection.Row;

        pedalKnobRow.style.justifyContent = Justify.SpaceAround;

        pedalKnobRow.style.alignItems = Align.Center;

        pedalKnobRow.style.marginBottom = 24f;



        scorePedalKnobLeft = CreatePedalKnob();

        scorePedalKnobMid = CreatePedalKnob();

        scorePedalKnobRight = CreatePedalKnob();

        SetKnobIndicatorAngle(scorePedalKnobLeft, -28f);

        SetKnobIndicatorAngle(scorePedalKnobMid, -8f);

        SetKnobIndicatorAngle(scorePedalKnobRight, 22f);

        pedalKnobRow.Add(scorePedalKnobLeft);

        pedalKnobRow.Add(scorePedalKnobMid);

        pedalKnobRow.Add(scorePedalKnobRight);



        scorePedalScreen = new VisualElement();

        scorePedalScreen.style.flexGrow = 0f;

        scorePedalScreen.style.paddingTop = 18f;

        scorePedalScreen.style.paddingBottom = 8f;

        scorePedalScreen.style.paddingLeft = 20f;

        scorePedalScreen.style.paddingRight = 20f;

        scorePedalScreen.style.marginBottom = 8f;

        scorePedalScreen.style.borderTopWidth = 3f;

        scorePedalScreen.style.borderRightWidth = 3f;

        scorePedalScreen.style.borderBottomWidth = 5f;

        scorePedalScreen.style.borderLeftWidth = 3f;

        scorePedalScreen.style.borderTopColor = new Color(0.72f, 0.89f, 0.79f, 1f);

        scorePedalScreen.style.borderRightColor = new Color(0.24f, 0.42f, 0.35f, 1f);

        scorePedalScreen.style.borderBottomColor = new Color(0.12f, 0.23f, 0.18f, 1f);

        scorePedalScreen.style.borderLeftColor = new Color(0.24f, 0.42f, 0.35f, 1f);

        scorePedalScreen.style.backgroundColor = new Color(0.70f, 0.88f, 0.76f, 0.95f);

        scorePedalScreen.style.borderTopLeftRadius = 8f;

        scorePedalScreen.style.borderTopRightRadius = 8f;

        scorePedalScreen.style.borderBottomLeftRadius = 8f;

        scorePedalScreen.style.borderBottomRightRadius = 8f;

        scorePedalScreen.style.alignItems = Align.Center;

        scorePedalScreen.style.justifyContent = Justify.FlexStart;

        scorePedalScreen.style.minHeight = 120f;

        scorePedalScreen.style.flexShrink = 0f;

        scorePedalScreen.style.overflow = Overflow.Hidden;



        inputMeterWrap = new VisualElement();

        inputMeterWrap.style.width = 240f;

        inputMeterWrap.style.alignItems = Align.Center;

        inputMeterWrap.style.justifyContent = Justify.Center;

        inputMeterWrap.style.marginBottom = 8f;

        inputMeterWrap.style.flexShrink = 0f;



        inputMeterLabel = CreateLabel("INPUT", 16f, new Color(0.08f, 0.28f, 0.29f, 0.9f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        inputMeterLabel.style.letterSpacing = 0.9f;

        inputMeterLabel.style.marginBottom = 1f;



        inputMeterFace = new VisualElement();

        inputMeterFace.style.width = 220f;

        inputMeterFace.style.height = 84f;

        inputMeterFace.style.position = Position.Relative;

        inputMeterFace.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        inputMeterFace.style.borderTopWidth = 0f;

        inputMeterFace.style.borderRightWidth = 0f;

        inputMeterFace.style.borderBottomWidth = 0f;

        inputMeterFace.style.borderLeftWidth = 0f;

        inputMeterFace.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

        inputMeterFace.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

        inputMeterFace.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

        inputMeterFace.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        inputMeterFace.style.borderTopLeftRadius = 6f;

        inputMeterFace.style.borderTopRightRadius = 6f;

        inputMeterFace.style.borderBottomLeftRadius = 4f;

        inputMeterFace.style.borderBottomRightRadius = 4f;

        inputMeterFace.style.flexShrink = 0f;



        inputMeterArcViewport = new VisualElement();

        inputMeterArcViewport.style.position = Position.Absolute;

        inputMeterArcViewport.style.left = 12f;

        inputMeterArcViewport.style.right = 12f;

        inputMeterArcViewport.style.top = 10f;

        inputMeterArcViewport.style.height = 36f;

        inputMeterArcViewport.style.overflow = Overflow.Hidden;



        inputMeterArc = new VisualElement();

        inputMeterArc.style.position = Position.Absolute;

        inputMeterArc.style.left = 0f;

        inputMeterArc.style.right = 0f;

        inputMeterArc.style.top = 0f;

        inputMeterArc.style.height = 72f;

        inputMeterArc.style.borderTopWidth = 3f;

        inputMeterArc.style.borderRightWidth = 3f;

        inputMeterArc.style.borderBottomWidth = 3f;

        inputMeterArc.style.borderLeftWidth = 3f;

        inputMeterArc.style.borderTopColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);

        inputMeterArc.style.borderRightColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);

        inputMeterArc.style.borderBottomColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);

        inputMeterArc.style.borderLeftColor = new Color(0.07f, 0.23f, 0.24f, 0.94f);

        inputMeterArc.style.borderTopLeftRadius = 120f;

        inputMeterArc.style.borderTopRightRadius = 120f;

        inputMeterArc.style.borderBottomLeftRadius = 120f;

        inputMeterArc.style.borderBottomRightRadius = 120f;



        for (int i = 0; i <= 10; i++)

        {

            VisualElement tick = new VisualElement();

            bool major = i % 2 == 0;

            tick.style.position = Position.Absolute;

            tick.style.width = major ? 3f : 2f;

            tick.style.height = major ? 10f : 6f;

            tick.style.backgroundColor = major ? new Color(0.08f, 0.24f, 0.25f, 0.95f) : new Color(0.09f, 0.26f, 0.27f, 0.82f);

            tick.style.borderTopLeftRadius = 1f;

            tick.style.borderTopRightRadius = 1f;

            tick.style.borderBottomLeftRadius = 1f;

            tick.style.borderBottomRightRadius = 1f;

            inputMeterTicks.Add(tick);

            inputMeterFace.Add(tick);

        }



        inputMeterNeedle = new VisualElement();

        inputMeterNeedle.style.position = Position.Absolute;

        inputMeterNeedle.style.width = 3f;

        inputMeterNeedle.style.height = 30f;

        inputMeterNeedle.style.backgroundColor = new Color(0.05f, 0.19f, 0.20f, 0.98f);

        inputMeterNeedle.style.borderTopLeftRadius = 1f;

        inputMeterNeedle.style.borderTopRightRadius = 1f;

        inputMeterNeedle.style.borderBottomLeftRadius = 1f;

        inputMeterNeedle.style.borderBottomRightRadius = 1f;

        inputMeterNeedle.style.transformOrigin = new TransformOrigin(Length.Percent(50f), Length.Percent(100f), 0f);

        inputMeterNeedle.style.rotate = new Rotate(new Angle(-65f, AngleUnit.Degree));



        inputMeterNeedleCap = new VisualElement();

        inputMeterNeedleCap.style.position = Position.Absolute;

        inputMeterNeedleCap.style.width = 12f;

        inputMeterNeedleCap.style.height = 12f;

        inputMeterNeedleCap.style.backgroundColor = new Color(0.07f, 0.23f, 0.24f, 0.98f);

        inputMeterNeedleCap.style.borderTopWidth = 2f;

        inputMeterNeedleCap.style.borderRightWidth = 2f;

        inputMeterNeedleCap.style.borderBottomWidth = 3f;

        inputMeterNeedleCap.style.borderLeftWidth = 2f;

        inputMeterNeedleCap.style.borderTopColor = new Color(0.12f, 0.30f, 0.31f, 0.92f);

        inputMeterNeedleCap.style.borderRightColor = new Color(0.04f, 0.15f, 0.16f, 0.95f);

        inputMeterNeedleCap.style.borderBottomColor = new Color(0.03f, 0.12f, 0.13f, 0.95f);

        inputMeterNeedleCap.style.borderLeftColor = new Color(0.04f, 0.15f, 0.16f, 0.95f);



        inputMeterArcViewport.Add(inputMeterArc);

        inputMeterFace.Add(inputMeterArcViewport);

        inputMeterFace.Add(inputMeterNeedle);

        inputMeterFace.Add(inputMeterNeedleCap);

        inputMeterWrap.Add(inputMeterLabel);

        inputMeterWrap.Add(inputMeterFace);



        songProgressTrack = new VisualElement();

        songProgressTrack.style.width = 220f;

        songProgressTrack.style.height = 10f;

        songProgressTrack.style.marginTop = 6f;

        songProgressTrack.style.backgroundColor = new Color(0.06f, 0.18f, 0.19f, 0.92f);

        songProgressTrack.style.borderTopLeftRadius = 5f;

        songProgressTrack.style.borderTopRightRadius = 5f;

        songProgressTrack.style.borderBottomLeftRadius = 5f;

        songProgressTrack.style.borderBottomRightRadius = 5f;

        songProgressTrack.style.borderTopWidth = 1f;

        songProgressTrack.style.borderRightWidth = 1f;

        songProgressTrack.style.borderBottomWidth = 1f;

        songProgressTrack.style.borderLeftWidth = 1f;

        Color progressBorderColor = new Color(0.09f, 0.30f, 0.31f, 0.95f);

        songProgressTrack.style.borderTopColor = progressBorderColor;

        songProgressTrack.style.borderRightColor = progressBorderColor;

        songProgressTrack.style.borderBottomColor = progressBorderColor;

        songProgressTrack.style.borderLeftColor = progressBorderColor;



        songProgressFill = new VisualElement();

        songProgressFill.style.width = 0f;

        songProgressFill.style.height = Length.Percent(100f);

        songProgressFill.style.backgroundColor = new Color(0.83f, 0.96f, 1f, 0.98f);

        songProgressFill.style.borderTopLeftRadius = 5f;

        songProgressFill.style.borderBottomLeftRadius = 5f;

        songProgressFill.style.borderTopRightRadius = 5f;

        songProgressFill.style.borderBottomRightRadius = 5f;

        songProgressTrack.Add(songProgressFill);



        inputMeterWrap.Add(songProgressTrack);

        LayoutInputMeterGraphics(220f, 84f);



        scoreTitleLabel = CreateLabel("RUN SCORE", 20f, GameplayHudEyebrowColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        scoreTitleLabel.style.letterSpacing = 1.2f;

        scoreTitleLabel.style.marginTop = 0f;

        scoreTitleLabel.style.marginBottom = 6f;

        scoreTitleLabel.style.flexShrink = 0f;

        scoreTitleLabel.style.width = Length.Percent(100f);

        scoreTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        scoreTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        scoreTitleLabel.style.whiteSpace = WhiteSpace.NoWrap;

        scoreTitleLabel.style.overflow = Overflow.Hidden;

        scoreTitleLabel.style.textOverflow = TextOverflow.Ellipsis;



        scorePercentLabel = CreateLabel("0.0%", 58f, GameplayHudAccentWarmColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        scorePercentLabel.style.letterSpacing = 0.12f;

        scorePercentLabel.style.marginTop = -2f;

        scorePercentLabel.style.marginBottom = 4f;

        scorePercentLabel.style.flexShrink = 0f;

        scorePercentLabel.style.width = Length.Percent(100f);

        scorePercentLabel.style.unityFontDefinition = modernUiFontDefinition;

        scorePercentLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        scorePercentLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        noteTallyLabel = CreateLabel("Hits 0  \u2022  Misses 0", 24f, GameplayHudMutedTextColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        noteTallyLabel.text = "Hits 0  /  Misses 0";

        noteTallyLabel.style.marginTop = 0f;

        noteTallyLabel.style.flexShrink = 0f;

        noteTallyLabel.style.width = Length.Percent(100f);

        noteTallyLabel.style.letterSpacing = 0.18f;

        noteTallyLabel.style.unityFontDefinition = modernUiFontDefinition;

        noteTallyLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        VisualElement pedalFooter = new VisualElement();

        pedalFooter.style.flexDirection = FlexDirection.Row;

        pedalFooter.style.justifyContent = Justify.Center;



        scorePedalFootswitch = CreateFootswitch();

        scorePedalFootswitchRight = CreateFootswitch();

        scorePedalFootswitch.style.marginRight = 36f;

        scorePedalFootswitchRight.style.marginLeft = 36f;



        scorePedalScreen.Add(inputMeterWrap);

        pedalFooter.Add(scorePedalFootswitch);

        pedalFooter.Add(scorePedalFootswitchRight);

        pedalFace.Add(pedalTopRow);

        pedalFace.Add(pedalKnobRow);

        pedalFace.Add(scorePedalScreen);

        pedalFace.Add(pedalFooter);

        scorePedalBody.Add(scorePedalInputJack);

        scorePedalBody.Add(scorePedalOutputJack);

        scorePedalBody.Add(pedalFace);

        scorePlate.Add(scorePedalBody);

        songCardScoreBlock.Add(scoreTitleLabel);

        songCardScoreBlock.Add(scorePercentLabel);

        songCardScoreBlock.Add(noteTallyLabel);



        judgePopupLayer = new VisualElement();

        judgePopupLayer.style.position = Position.Absolute;

        judgePopupLayer.style.left = 0f;

        judgePopupLayer.style.right = 0f;

        judgePopupLayer.style.top = 0f;

        judgePopupLayer.style.bottom = 0f;

        judgePopupLayer.pickingMode = PickingMode.Ignore;

        pauseOverlay = CreateFullscreenOverlay();

        pauseOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.52f);

        pauseOverlay.style.alignItems = Align.Center;

        pauseOverlay.style.justifyContent = Justify.Center;

        pauseBlurBackdrop = new VisualElement();

        pauseBlurBackdrop.style.position = Position.Absolute;

        pauseBlurBackdrop.style.left = 0f;

        pauseBlurBackdrop.style.right = 0f;

        pauseBlurBackdrop.style.top = 0f;

        pauseBlurBackdrop.style.bottom = 0f;

        pauseBlurBackdrop.style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.12f);

        pauseBlurBackdrop.pickingMode = PickingMode.Ignore;

        pauseBackplate = new VisualElement();

        pauseBackplate.style.position = Position.Absolute;

        // Change this value to move the orange accent plate horizontally.

        pauseBackplate.style.right = -160f;

        pauseBackplate.style.top = -220f;

        pauseBackplate.style.bottom = -220f;

        pauseBackplate.style.width = 1540f;

        // Change this angle to adjust the orange accent plate slant.

        pauseBackplate.style.rotate = new Rotate(new Angle(-8f, AngleUnit.Degree));

        pauseBackplate.style.backgroundColor = LibraryConfirmedSongColor;

        pauseBackplate.style.opacity = 0.92f;

        pauseBackplate.pickingMode = PickingMode.Ignore;



        VisualElement pauseBackplateGradient = new VisualElement();

        pauseBackplateGradient.style.position = Position.Absolute;

        pauseBackplateGradient.style.left = 0f;

        pauseBackplateGradient.style.right = 0f;

        pauseBackplateGradient.style.top = 0f;

        pauseBackplateGradient.style.bottom = 0f;

        pauseBackplateGradient.style.backgroundImage = new StyleBackground(GetPauseBackplateGradientTexture());

        pauseBackplateGradient.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

        pauseBackplateGradient.pickingMode = PickingMode.Ignore;

        pauseBackplate.Add(pauseBackplateGradient); 



        pauseFrontPlate = new VisualElement();

        pauseFrontPlate.style.position = Position.Absolute;

        // Change this value to move the dark main panel horizontally.

        pauseFrontPlate.style.right = -120f;

        pauseFrontPlate.style.top = -170f;

        pauseFrontPlate.style.bottom = -170f;

        pauseFrontPlate.style.width = 1460f;

        // Change this angle to adjust the dark main panel slant.

        pauseFrontPlate.style.rotate = new Rotate(new Angle(-6f, AngleUnit.Degree));

        pauseFrontPlate.style.backgroundColor = LibraryPanelColor;

        pauseFrontPlate.style.borderTopWidth = 0f;

        pauseFrontPlate.style.borderRightWidth = 0f;

        pauseFrontPlate.style.borderBottomWidth = 0f;

        pauseFrontPlate.style.borderLeftWidth = 0f;

        pauseFrontPlate.style.borderTopColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        pauseFrontPlate.style.borderRightColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        pauseFrontPlate.style.borderBottomColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        pauseFrontPlate.style.borderLeftColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        pauseFrontPlate.pickingMode = PickingMode.Ignore;



        pauseRegion = new VisualElement();

        pauseRegion.style.position = Position.Absolute;

        pauseRegion.style.right = 0f;

        pauseRegion.style.top = 0f;

        pauseRegion.style.bottom = 0f;

        pauseRegion.style.width = Length.Percent(44f);

        pauseRegion.style.alignItems = Align.Center;

        pauseRegion.style.justifyContent = Justify.FlexStart;

        pauseRegion.style.paddingTop = 88f;

        pauseRegion.style.paddingBottom = 0f;

        pauseRegion.style.paddingLeft = 220f;

        pauseRegion.style.paddingRight = 0f;

        pauseRegion.pickingMode = PickingMode.Position;



        pauseShell = new VisualElement();

        pauseShell.style.width = 820f;

        pauseShell.style.maxWidth = 920f;

        pauseShell.style.paddingLeft = 36f;

        pauseShell.style.paddingRight = 36f;

        pauseShell.style.paddingTop = 12f;

        pauseShell.style.paddingBottom = 18f;

        pauseShell.style.alignItems = Align.Center;

        pauseShell.style.position = Position.Relative;

        pauseShell.style.justifyContent = Justify.FlexStart;

        pauseShell.style.alignSelf = Align.Center;

        Label pauseStarsLabel = CreateLabel("\u2605 \u2605 \u2605", 34f, new Color(1f, 0.74f, 0.32f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        pauseStarsLabel.style.display = DisplayStyle.None;

        Label pauseEyebrowLabel = CreateLabel("PRESS ESC TO RETURN", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        pauseEyebrowLabel.style.unityFontDefinition = modernUiFontDefinition;

        pauseEyebrowLabel.style.letterSpacing = 1.8f;

        pauseEyebrowLabel.style.marginBottom = 10f;

        pauseTitleLabel = CreateLabel("PAUSED", 132f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        pauseTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        pauseTitleLabel.style.letterSpacing = 0f;

        pauseTitleLabel.style.marginBottom = 8f;

        pauseHintLabel = CreateLabel("SPACE Resume   \u2022   Left/Right Seek   \u2022   1/2 Marker", 34f, new Color(0.82f, 0.92f, 1f, 1f), false, TextAnchor.MiddleCenter);

        pauseHintLabel.style.unityFontDefinition = modernUiFontDefinition;

        pauseHintLabel.text = "Space resumes  \u2022  Left/Right seeks  \u2022  1/2 drops markers";

        pauseHintLabel.text = "Up/Down selects  \u2022  Left/Right seeks";

        pauseHintLabel.style.color = new Color(0.78f, 0.86f, 0.93f, 0.94f);

        pauseHintLabel.style.marginTop = 0f;

        pauseHintLabel.style.marginBottom = 34f;



        pauseCard = new VisualElement();

        pauseCard.style.width = Length.Percent(100f);

        pauseCard.style.maxWidth = 660f;

        pauseCard.style.paddingLeft = 0f;

        pauseCard.style.paddingRight = -100f;

        pauseCard.style.paddingTop = 0f;

        pauseCard.style.paddingBottom = 0f;

        pauseCard.style.alignSelf = Align.Center;

        pauseCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        VisualElement pauseStatusCard = new VisualElement();

        pauseStatusCard.style.paddingLeft = 0f;

        pauseStatusCard.style.paddingRight = 0f;

        pauseStatusCard.style.paddingTop = 0f;

        pauseStatusCard.style.paddingBottom = 0f;

        pauseStatusCard.style.marginBottom = 20f;



        Label pauseStatusTitle = CreateLabel("PLAYBACK STATUS", 20f, LibraryPrimaryColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        pauseStatusTitle.style.unityFontDefinition = modernUiFontDefinition;

        pauseStatusTitle.style.letterSpacing = 1.2f;

        pauseStatusTitle.style.unityTextAlign = TextAnchor.MiddleCenter;

        pauseStatusTitle.style.display = DisplayStyle.None;



        VisualElement pauseSpeedCard = new VisualElement();

        pauseSpeedCard.style.paddingLeft = 0f;

        pauseSpeedCard.style.paddingRight = 0f;

        pauseSpeedCard.style.paddingTop = 0f;

        pauseSpeedCard.style.paddingBottom = 0f;

        pauseSpeedCard.style.marginBottom = 26f;



        Label pauseSpeedTitle = CreateLabel("PLAYBACK SPEED", 20f, LibraryPrimaryColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        pauseSpeedTitle.style.unityFontDefinition = modernUiFontDefinition;

        pauseSpeedTitle.style.letterSpacing = 1.2f;

        pauseSpeedTitle.style.unityTextAlign = TextAnchor.MiddleCenter;

        pauseSpeedTitle.style.display = DisplayStyle.None;



        VisualElement pauseActionsCard = new VisualElement();

        pauseActionsCard.style.paddingLeft = 0f;

        pauseActionsCard.style.paddingRight = 0f;

        pauseActionsCard.style.paddingTop = 0f;

        pauseActionsCard.style.paddingBottom = 0f;



        Label pauseActionsTitle = CreateLabel("ACTIONS", 20f, LibraryPrimaryColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        pauseActionsTitle.style.unityFontDefinition = modernUiFontDefinition;

        pauseActionsTitle.style.letterSpacing = 1.2f;

        pauseActionsTitle.style.unityTextAlign = TextAnchor.MiddleCenter;

        pauseActionsTitle.style.display = DisplayStyle.None;



        pauseInfoLabel = CreateLabel("", 32f, new Color(0.90f, 0.96f, 1f, 1f));

        pauseInfoLabel.style.unityFontDefinition = modernUiFontDefinition;

        pauseInfoLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        pauseInfoLabel.style.maxWidth = 760f;

        pauseInfoLabel.style.marginBottom = 12f;



        speedValueLabel = CreateLabel("Song Speed 100%", 34f, new Color(1f, 0.96f, 0.87f, 1f), true, useTitleFont: false);

        speedValueLabel.style.unityFontDefinition = modernUiFontDefinition;

        speedValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        speedValueLabel.style.alignSelf = Align.Center;

        speedValueLabel.style.minWidth = 520f;

        speedValueLabel.style.marginBottom = 16f;

        speedSlider = new Slider(1f, 200f);

        speedSlider.focusable = false;

        speedSlider.style.marginTop = 0f;

        speedSlider.style.marginBottom = 0f;

        speedSlider.style.height = 74f;

        speedSlider.style.width = Length.Percent(100f);

        speedSlider.style.maxWidth = 620f;

        speedSlider.style.alignSelf = Align.Center;

        speedSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetPlaybackSpeedPercentFromUi(evt.newValue); });



        pauseButtons = new VisualElement();

        pauseButtons.style.flexDirection = FlexDirection.Column;

        pauseButtons.style.flexWrap = Wrap.NoWrap;

        pauseButtons.style.alignItems = Align.Stretch;

        pauseButtons.style.width = Length.Percent(100f);

        pauseButtons.style.maxWidth = 620f;

        pauseButtons.style.marginTop = 0f;


        gameModesButton = CreateActionButton("Game Modes", () => owner?.OpenGameModesFromUi());
        loopButton = CreateActionButton("Loop: OFF", () => owner?.ToggleLoopFromUi());
        gameModesLoopConfigButton = CreateActionButton("Loop Configuration", () => owner?.OpenLoopConfigurationFromUi());
        noteByNoteButton = CreateActionButton("Note By Note: OFF", () => owner?.ToggleNoteByNoteModeFromUi());
        heroModeButton = CreateActionButton("Hero Mode: ON", () => owner?.ToggleHeroModeFromUi());
        heroModeSettingsButton = CreateActionButton("Hero Mode Settings", () => owner?.OpenHeroModeSettingsFromUi());

        Button songSettingsButton = CreateActionButton("Song Settings", () => owner?.OpenSongSettingsFromUi());

        audioModeButton = CreateActionButton("Audio: Generated", () => owner?.CycleSongPlaybackAudioModeFromUi(1));

        Button songSelectButton = CreateActionButton("Library", () => owner?.OpenLibraryFromPauseFromUi());

        Button globalSettingsButton = CreateActionButton("Settings", () => owner?.OpenGlobalSettingsFromUi());

        Button toneLabButton = CreateActionButton("Tone Lab", () => owner?.OpenToneLabFromUi());

        Button mainMenuButton = CreateActionButton("Main Menu", () => owner?.OpenMainMenuFromUi());

        Button resumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        Button endButton = CreateActionButton("End", () => owner?.EndSongFromUi());

        Button restartButton = CreateActionButton("Restart", () => owner?.RetrySongFromUi());

        pauseActionButtons.AddRange(new[] { gameModesButton, songSettingsButton, audioModeButton, songSelectButton, globalSettingsButton, toneLabButton, mainMenuButton, resumeButton, endButton, restartButton });



        for (int i = 0; i < pauseActionButtons.Count; i++)

        {

            int pauseActionIndex = i + 1;

            pauseActionButtons[i].RegisterCallback<MouseEnterEvent>(_ => owner?.HoverPauseActionSelectionFromUi(pauseActionIndex));

        }



        foreach (Button button in new[] { gameModesButton, songSettingsButton, audioModeButton, songSelectButton, globalSettingsButton, toneLabButton })

        {

            button.style.marginRight = 0f;

            button.style.marginTop = 0f;

            button.style.marginBottom = 14f;

            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

            button.style.borderTopWidth = 2f;

            button.style.borderRightWidth = 2f;

            button.style.borderBottomWidth = 2f;

            button.style.borderLeftWidth = 2f;

            button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

            button.style.height = 132f;

            button.style.minWidth = 0f;

            button.style.width = Length.Percent(100f);

            button.style.fontSize = 92f;

            button.style.unityFontDefinition = modernUiFontDefinition;

            pauseButtons.Add(button);

        }



        mainMenuButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        mainMenuButton.style.color = new Color(0.93f, 0.95f, 0.98f, 1f);

        mainMenuButton.style.borderTopWidth = 2f;

        mainMenuButton.style.borderRightWidth = 2f;

        mainMenuButton.style.borderBottomWidth = 2f;

        mainMenuButton.style.borderLeftWidth = 2f;

        mainMenuButton.style.borderTopColor = MenuOutlineNeutralColor;

        mainMenuButton.style.borderRightColor = MenuOutlineNeutralColor;

        mainMenuButton.style.borderBottomColor = MenuOutlineNeutralColor;

        mainMenuButton.style.borderLeftColor = MenuOutlineNeutralColor;

        mainMenuButton.style.height = 128f;

        mainMenuButton.style.minWidth = 0f;

        mainMenuButton.style.width = Length.Percent(100f);

        mainMenuButton.style.fontSize = 92f;

        mainMenuButton.style.unityFontDefinition = modernUiFontDefinition;

        mainMenuButton.style.marginBottom = 14f;



        resumeButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        resumeButton.style.color = LibraryPrimaryColor;

        resumeButton.style.borderTopWidth = 2f;

        resumeButton.style.borderRightWidth = 2f;

        resumeButton.style.borderBottomWidth = 2f;

        resumeButton.style.borderLeftWidth = 2f;

        resumeButton.style.borderTopColor = LibraryPrimaryColor;

        resumeButton.style.borderRightColor = LibraryPrimaryColor;

        resumeButton.style.borderBottomColor = LibraryPrimaryColor;

        resumeButton.style.borderLeftColor = LibraryPrimaryColor;

        resumeButton.style.height = 128f;

        resumeButton.style.minWidth = 0f;

        resumeButton.style.width = Length.Percent(100f);

        resumeButton.style.fontSize = 96f;

        resumeButton.style.unityFontDefinition = modernUiFontDefinition;

        resumeButton.style.marginBottom = 0f;



        foreach (Button button in new[] { endButton, restartButton })

        {

            button.style.marginTop = 0f;

            button.style.marginBottom = 0f;

            button.style.minWidth = 0f;

            button.style.width = StyleKeyword.Auto;

            button.style.flexBasis = 0f;

            button.style.height = 108f;

            button.style.fontSize = 82f;

            button.style.unityFontDefinition = modernUiFontDefinition;

            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

            button.style.borderTopWidth = 2f;

            button.style.borderRightWidth = 2f;

            button.style.borderBottomWidth = 2f;

            button.style.borderLeftWidth = 2f;

            button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        }



        pauseEndRow = new VisualElement();

        pauseEndRow.style.flexDirection = FlexDirection.Row;

        pauseEndRow.style.alignItems = Align.Stretch;

        pauseEndRow.style.width = Length.Percent(100f);

        pauseEndRow.style.maxWidth = 620f;

        pauseEndRow.style.marginTop = 14f;

        endButton.style.marginRight = 14f;

        endButton.style.flexGrow = 1f;

        endButton.style.flexShrink = 1f;

        restartButton.style.flexGrow = 1f;

        restartButton.style.flexShrink = 1f;

        pauseEndRow.Add(endButton);

        pauseEndRow.Add(restartButton);



        pauseButtons.Add(mainMenuButton);

        pauseButtons.Add(resumeButton);

        pauseButtons.Add(pauseEndRow);

        pauseStatusCard.Add(pauseStatusTitle);

        pauseStatusCard.Add(pauseInfoLabel);

        pauseSpeedCard.Add(pauseSpeedTitle);

        pauseSpeedCard.Add(speedValueLabel);

        pauseSpeedCard.Add(speedSlider);

        pauseActionsCard.Add(pauseActionsTitle);

        pauseActionsCard.Add(pauseButtons);

        pauseCard.Add(pauseStatusCard);

        pauseCard.Add(pauseSpeedCard);

        pauseCard.Add(pauseActionsCard);

        pauseShell.Add(pauseEyebrowLabel);

        pauseShell.Add(pauseTitleLabel);

        pauseShell.Add(pauseHintLabel);

        pauseShell.Add(pauseCard);

        pauseRegion.Add(pauseShell);

        pauseOverlay.Add(pauseBackplate);

        pauseOverlay.Add(pauseFrontPlate);

        pauseOverlay.Add(pauseRegion);

        gameModesPopup = null;
        gameModesOverlay = CreateFullscreenOverlay();
        gameModesOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.52f);
        gameModesOverlay.style.alignItems = Align.Center;
        gameModesOverlay.style.justifyContent = Justify.Center;
        gameModesOverlay.style.display = DisplayStyle.None;

        gameModesBackplate = new VisualElement();
        gameModesBackplate.style.position = Position.Absolute;
        gameModesBackplate.style.right = -160f;
        gameModesBackplate.style.top = -220f;
        gameModesBackplate.style.bottom = -220f;
        gameModesBackplate.style.width = 1540f;
        gameModesBackplate.style.rotate = new Rotate(new Angle(-8f, AngleUnit.Degree));
        gameModesBackplate.style.backgroundColor = LibraryConfirmedSongColor;
        gameModesBackplate.style.opacity = 0.92f;
        gameModesBackplate.pickingMode = PickingMode.Ignore;

        VisualElement gameModesBackplateGradient = new VisualElement();
        gameModesBackplateGradient.style.position = Position.Absolute;
        gameModesBackplateGradient.style.left = 0f;
        gameModesBackplateGradient.style.right = 0f;
        gameModesBackplateGradient.style.top = 0f;
        gameModesBackplateGradient.style.bottom = 0f;
        gameModesBackplateGradient.style.backgroundImage = new StyleBackground(GetPauseBackplateGradientTexture());
        gameModesBackplateGradient.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;
        gameModesBackplateGradient.pickingMode = PickingMode.Ignore;
        gameModesBackplate.Add(gameModesBackplateGradient);

        gameModesFrontPlate = new VisualElement();
        gameModesFrontPlate.style.position = Position.Absolute;
        gameModesFrontPlate.style.right = -120f;
        gameModesFrontPlate.style.top = -170f;
        gameModesFrontPlate.style.bottom = -170f;
        gameModesFrontPlate.style.width = 1460f;
        gameModesFrontPlate.style.rotate = new Rotate(new Angle(-6f, AngleUnit.Degree));
        gameModesFrontPlate.style.backgroundColor = LibraryPanelColor;
        gameModesFrontPlate.pickingMode = PickingMode.Ignore;

        gameModesRegion = new VisualElement();
        gameModesRegion.style.position = Position.Absolute;
        gameModesRegion.style.right = 0f;
        gameModesRegion.style.top = 0f;
        gameModesRegion.style.bottom = 0f;
        gameModesRegion.style.width = Length.Percent(44f);
        gameModesRegion.style.alignItems = Align.Center;
        gameModesRegion.style.justifyContent = Justify.FlexStart;
        gameModesRegion.style.paddingTop = 88f;
        gameModesRegion.style.paddingBottom = 0f;
        gameModesRegion.style.paddingLeft = 220f;
        gameModesRegion.style.paddingRight = 0f;
        gameModesRegion.pickingMode = PickingMode.Position;

        gameModesShell = new VisualElement();
        gameModesShell.style.width = 820f;
        gameModesShell.style.maxWidth = 920f;
        gameModesShell.style.paddingLeft = 36f;
        gameModesShell.style.paddingRight = 36f;
        gameModesShell.style.paddingTop = 12f;
        gameModesShell.style.paddingBottom = 18f;
        gameModesShell.style.alignItems = Align.Center;
        gameModesShell.style.position = Position.Relative;
        gameModesShell.style.justifyContent = Justify.FlexStart;
        gameModesShell.style.alignSelf = Align.Center;

        Label gameModesEyebrowLabel = CreateLabel("PRESS ESC TO RETURN", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);
        gameModesEyebrowLabel.style.unityFontDefinition = modernUiFontDefinition;
        gameModesEyebrowLabel.style.letterSpacing = 1.8f;
        gameModesEyebrowLabel.style.marginBottom = 10f;

        Label gameModesTitleLabel = CreateLabel("GAME MODES", 112f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        gameModesTitleLabel.style.unityFontDefinition = modernUiFontDefinition;
        gameModesTitleLabel.style.marginBottom = 8f;

        gameModesHintLabel = CreateLabel("Up/Down selects  \u2022  Enter activates  \u2022  Esc back", 34f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        gameModesHintLabel.style.unityFontDefinition = modernUiFontDefinition;
        gameModesHintLabel.style.marginBottom = 18f;

        gameModesInfoLabel = CreateLabel("Mode changes restart the song.", 30f, new Color(0.90f, 0.96f, 1f, 1f), false, TextAnchor.MiddleCenter, useTitleFont: false);
        gameModesInfoLabel.style.unityFontDefinition = modernUiFontDefinition;
        gameModesInfoLabel.style.maxWidth = 760f;
        gameModesInfoLabel.style.marginBottom = 22f;

        gameModesCard = new VisualElement();
        gameModesCard.style.width = Length.Percent(100f);
        gameModesCard.style.maxWidth = 660f;
        gameModesCard.style.paddingLeft = 0f;
        gameModesCard.style.paddingRight = 0f;
        gameModesCard.style.paddingTop = 0f;
        gameModesCard.style.paddingBottom = 0f;
        gameModesCard.style.marginBottom = 18f;
        gameModesCard.style.alignSelf = Align.Center;
        gameModesCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        gameModesButtons = new VisualElement();
        gameModesButtons.style.flexDirection = FlexDirection.Column;
        gameModesButtons.style.flexWrap = Wrap.NoWrap;
        gameModesButtons.style.alignItems = Align.Stretch;
        gameModesButtons.style.alignSelf = Align.Center;
        gameModesButtons.style.flexShrink = 0f;
        gameModesButtons.style.width = Length.Percent(100f);
        gameModesButtons.style.maxWidth = 620f;
        gameModesButtons.style.marginTop = 0f;
        gameModesFooterRow = new VisualElement();
        gameModesFooterRow.style.flexDirection = FlexDirection.Row;
        gameModesFooterRow.style.justifyContent = Justify.Center;
        gameModesFooterRow.style.alignItems = Align.Center;
        gameModesFooterRow.style.alignSelf = Align.Center;
        gameModesFooterRow.style.width = Length.Percent(100f);
        gameModesFooterRow.style.maxWidth = 620f;
        gameModesFooterRow.style.marginTop = 8f;
        gameModesActionButtons.AddRange(new[] { loopButton, gameModesLoopConfigButton, noteByNoteButton, heroModeButton, heroModeSettingsButton });
        for (int i = 0; i < gameModesActionButtons.Count; i++)
        {
            int gameModeIndex = i;
            gameModesActionButtons[i].RegisterCallback<MouseEnterEvent>(_ => owner?.HoverGameModesSelectionFromUi(gameModeIndex));
        }
        foreach (Button button in gameModesActionButtons)
        {
            button.style.width = Length.Percent(100f);
            button.style.maxWidth = 620f;
            button.style.height = 118f;
            button.style.minWidth = 0f;
            button.style.marginBottom = 10f;
            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);
            button.style.borderTopWidth = 2f;
            button.style.borderRightWidth = 2f;
            button.style.borderBottomWidth = 2f;
            button.style.borderLeftWidth = 2f;
            button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);
            button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);
            button.style.fontSize = 84f;
            button.style.unityFontDefinition = modernUiFontDefinition;
            gameModesButtons.Add(button);
        }
        gameModesLoopConfigButton.style.display = DisplayStyle.None;
        heroModeSettingsButton.style.display = DisplayStyle.None;
        gameModesBackButton = CreateActionButton("Back", () => owner?.CloseGameModesFromUi());
        gameModesBackButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        gameModesBackButton.style.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        gameModesBackButton.style.borderTopWidth = 2f;
        gameModesBackButton.style.borderRightWidth = 2f;
        gameModesBackButton.style.borderBottomWidth = 2f;
        gameModesBackButton.style.borderLeftWidth = 2f;
        gameModesBackButton.style.borderTopColor = MenuOutlineNeutralColor;
        gameModesBackButton.style.borderRightColor = MenuOutlineNeutralColor;
        gameModesBackButton.style.borderBottomColor = MenuOutlineNeutralColor;
        gameModesBackButton.style.borderLeftColor = MenuOutlineNeutralColor;
        gameModesBackButton.style.height = 108f;
        gameModesBackButton.style.minWidth = 320f;
        gameModesBackButton.style.width = 360f;
        gameModesBackButton.style.maxWidth = 420f;
        gameModesBackButton.style.fontSize = 84f;
        gameModesBackButton.style.unityFontDefinition = modernUiFontDefinition;
        gameModesBackButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverGameModesSelectionFromUi(5));
        gameModesFooterRow.Add(gameModesBackButton);
        gameModesCard.Add(gameModesButtons);
        gameModesShell.Add(gameModesEyebrowLabel);
        gameModesShell.Add(gameModesTitleLabel);
        gameModesShell.Add(gameModesHintLabel);
        gameModesShell.Add(gameModesInfoLabel);
        gameModesShell.Add(gameModesCard);
        gameModesShell.Add(gameModesFooterRow);
        gameModesRegion.Add(gameModesShell);
        gameModesOverlay.Add(gameModesBackplate);
        gameModesOverlay.Add(gameModesFrontPlate);
        gameModesOverlay.Add(gameModesRegion);

        notesDetectorTestOverlay = CreateFullscreenOverlay();
        notesDetectorTestOverlay.style.backgroundColor = new Color(0.04f, 0.05f, 0.07f, 0.58f);
        notesDetectorTestOverlay.style.alignItems = Align.Stretch;
        notesDetectorTestOverlay.style.justifyContent = Justify.FlexStart;
        notesDetectorTestOverlay.style.display = DisplayStyle.None;

        notesDetectorTestBackplate = new VisualElement();
        notesDetectorTestBackplate.style.position = Position.Absolute;
        notesDetectorTestBackplate.style.left = 0f;
        notesDetectorTestBackplate.style.right = 0f;
        notesDetectorTestBackplate.style.top = 0f;
        notesDetectorTestBackplate.style.height = 2f;
        notesDetectorTestBackplate.style.backgroundColor = new Color(0.84f, 0.60f, 0.36f, 0.82f);
        notesDetectorTestBackplate.pickingMode = PickingMode.Ignore;

        notesDetectorTestFrontPlate = new VisualElement();
        notesDetectorTestFrontPlate.style.position = Position.Absolute;
        notesDetectorTestFrontPlate.style.left = 0f;
        notesDetectorTestFrontPlate.style.right = 0f;
        notesDetectorTestFrontPlate.style.top = 0f;
        notesDetectorTestFrontPlate.style.bottom = 0f;
        notesDetectorTestFrontPlate.style.backgroundColor = new Color(0.10f, 0.10f, 0.11f, 0.985f);
        notesDetectorTestFrontPlate.pickingMode = PickingMode.Ignore;

        notesDetectorTestRegion = new VisualElement();
        notesDetectorTestRegion.style.position = Position.Absolute;
        notesDetectorTestRegion.style.left = 0f;
        notesDetectorTestRegion.style.right = 0f;
        notesDetectorTestRegion.style.top = 0f;
        notesDetectorTestRegion.style.bottom = 0f;
        notesDetectorTestRegion.style.alignItems = Align.Stretch;
        notesDetectorTestRegion.style.justifyContent = Justify.FlexStart;
        notesDetectorTestRegion.style.paddingTop = 28f;
        notesDetectorTestRegion.style.paddingBottom = 24f;
        notesDetectorTestRegion.style.paddingLeft = 34f;
        notesDetectorTestRegion.style.paddingRight = 34f;
        notesDetectorTestRegion.pickingMode = PickingMode.Position;

        notesDetectorTestShell = new VisualElement();
        notesDetectorTestShell.style.flexGrow = 1f;
        notesDetectorTestShell.style.paddingLeft = 0f;
        notesDetectorTestShell.style.paddingRight = 0f;
        notesDetectorTestShell.style.paddingTop = 0f;
        notesDetectorTestShell.style.paddingBottom = 0f;
        notesDetectorTestShell.style.alignItems = Align.Stretch;
        notesDetectorTestShell.style.position = Position.Relative;
        notesDetectorTestShell.style.justifyContent = Justify.FlexStart;
        notesDetectorTestShell.style.alignSelf = Align.Stretch;

        Label notesDetectorTestEyebrowLabel = CreateLabel("COMPARE DETECTOR BACKENDS", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);
        notesDetectorTestEyebrowLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestEyebrowLabel.style.letterSpacing = 1.8f;
        notesDetectorTestEyebrowLabel.style.marginBottom = 10f;
        notesDetectorTestEyebrowLabel.style.display = DisplayStyle.None;

        Label notesDetectorTestTitleLabel = CreateLabel("NOTES DETECTOR", 78f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestTitleLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestTitleLabel.style.marginBottom = 6f;

        Label notesDetectorTestHintLabel = CreateLabel("Use mouse or Up/Down  •  Enter activates  •  Esc back", 34f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);
        notesDetectorTestHintLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestHintLabel.style.marginBottom = 18f;
        notesDetectorTestHintLabel.style.display = DisplayStyle.None;

        notesDetectorTestInfoLabel = CreateLabel("The native notes detector runs continuously in the background. Adjust the live detector, choose the input device, and save only to Custom when the calibration feels right.", 25f, new Color(0.80f, 0.84f, 0.89f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestInfoLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestInfoLabel.style.maxWidth = Length.Percent(100f);
        notesDetectorTestInfoLabel.style.marginBottom = 22f;
        notesDetectorTestInfoLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorTestInfoLabel.style.display = DisplayStyle.None;

        VisualElement notesDetectorToolbarRow = new VisualElement();
        notesDetectorToolbarRow.style.flexDirection = FlexDirection.Column;
        notesDetectorToolbarRow.style.flexWrap = Wrap.NoWrap;
        notesDetectorToolbarRow.style.justifyContent = Justify.FlexStart;
        notesDetectorToolbarRow.style.alignItems = Align.Stretch;
        notesDetectorToolbarRow.style.marginBottom = 32f;

        VisualElement notesDetectorDeviceColumn = new VisualElement();
        notesDetectorDeviceColumn.style.flexDirection = FlexDirection.Column;
        notesDetectorDeviceColumn.style.alignItems = Align.FlexStart;
        notesDetectorDeviceColumn.style.justifyContent = Justify.FlexStart;
        notesDetectorDeviceColumn.style.flexGrow = 1f;
        notesDetectorDeviceColumn.style.minWidth = 420f;
        notesDetectorDeviceColumn.style.width = Length.Percent(100f);
        notesDetectorDeviceColumn.style.marginRight = 18f;
        notesDetectorDeviceColumn.style.marginBottom = 10f;

        Label notesDetectorDeviceLabel = CreateLabel("Native Input Device", 34f, new Color(0.73f, 0.78f, 0.84f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorDeviceLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorDeviceLabel.style.marginBottom = 10f;
        notesDetectorDeviceColumn.Add(notesDetectorDeviceLabel);

        VisualElement notesDetectorDeviceRow = new VisualElement();
        notesDetectorDeviceRow.style.flexDirection = FlexDirection.Row;
        notesDetectorDeviceRow.style.alignItems = Align.Center;
        notesDetectorDeviceRow.style.justifyContent = Justify.FlexStart;
        notesDetectorDeviceRow.style.flexWrap = Wrap.Wrap;

        notesDetectorTestInputDeviceDropdown = new DropdownField();
        notesDetectorTestInputDeviceDropdown.choices = new List<string> { "Automatic" };
        notesDetectorTestInputDeviceDropdown.value = "Automatic";
        notesDetectorTestInputDeviceDropdown.style.minWidth = 620f;
        notesDetectorTestInputDeviceDropdown.style.height = 72f;
        notesDetectorTestInputDeviceDropdown.style.fontSize = 26f;
        notesDetectorTestInputDeviceDropdown.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.98f);
        notesDetectorTestInputDeviceDropdown.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        notesDetectorTestInputDeviceDropdown.style.borderTopWidth = 1f;
        notesDetectorTestInputDeviceDropdown.style.borderRightWidth = 1f;
        notesDetectorTestInputDeviceDropdown.style.borderBottomWidth = 1f;
        notesDetectorTestInputDeviceDropdown.style.borderLeftWidth = 1f;
        notesDetectorTestInputDeviceDropdown.style.borderTopColor = new Color(0.31f, 0.33f, 0.37f, 0.90f);
        notesDetectorTestInputDeviceDropdown.style.borderRightColor = new Color(0.22f, 0.24f, 0.27f, 0.98f);
        notesDetectorTestInputDeviceDropdown.style.borderBottomColor = new Color(0.17f, 0.18f, 0.21f, 0.98f);
        notesDetectorTestInputDeviceDropdown.style.borderLeftColor = new Color(0.22f, 0.24f, 0.27f, 0.98f);
        notesDetectorTestInputDeviceDropdown.style.borderTopLeftRadius = 10f;
        notesDetectorTestInputDeviceDropdown.style.borderTopRightRadius = 10f;
        notesDetectorTestInputDeviceDropdown.style.borderBottomLeftRadius = 10f;
        notesDetectorTestInputDeviceDropdown.style.borderBottomRightRadius = 10f;
        notesDetectorTestInputDeviceDropdown.style.marginRight = 10f;
        notesDetectorTestInputDeviceDropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement inputElement = notesDetectorTestInputDeviceDropdown.Q(className: "unity-base-field__input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
                inputElement.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            }

            Label textLabel = notesDetectorTestInputDeviceDropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (textLabel != null)
            {
                textLabel.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
                textLabel.style.fontSize = 26f;
            }

            VisualElement arrowElement = notesDetectorTestInputDeviceDropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrowElement != null)
                arrowElement.style.unityBackgroundImageTintColor = new Color(0.84f, 0.86f, 0.90f, 1f);
        });
        notesDetectorTestInputDeviceDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressNotesDetectorDropdownCallbacks)
                return;

            int choiceIndex = notesDetectorTestInputDeviceDropdown.choices.IndexOf(evt.newValue);
            owner?.SetNativeNotesDetectorInputDeviceFromUi(choiceIndex < 0 ? 0 : choiceIndex);
        });
        notesDetectorDeviceRow.Add(notesDetectorTestInputDeviceDropdown);

        notesDetectorRefreshDevicesButton = new Button(() => owner?.RefreshNativeNotesDetectorDevicesFromUi())
        {
            text = "Refresh Devices"
        };
        notesDetectorRefreshDevicesButton.style.height = 72f;
        notesDetectorRefreshDevicesButton.style.minWidth = 220f;
        notesDetectorRefreshDevicesButton.style.paddingLeft = 16f;
        notesDetectorRefreshDevicesButton.style.paddingRight = 16f;
        notesDetectorRefreshDevicesButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        notesDetectorRefreshDevicesButton.style.color = new Color(0.93f, 0.95f, 0.98f, 1f);
        notesDetectorRefreshDevicesButton.style.borderTopWidth = 1f;
        notesDetectorRefreshDevicesButton.style.borderRightWidth = 1f;
        notesDetectorRefreshDevicesButton.style.borderBottomWidth = 1f;
        notesDetectorRefreshDevicesButton.style.borderLeftWidth = 1f;
        notesDetectorRefreshDevicesButton.style.borderTopColor = MenuOutlineNeutralColor;
        notesDetectorRefreshDevicesButton.style.borderRightColor = MenuOutlineNeutralColor;
        notesDetectorRefreshDevicesButton.style.borderBottomColor = MenuOutlineNeutralColor;
        notesDetectorRefreshDevicesButton.style.borderLeftColor = MenuOutlineNeutralColor;
        notesDetectorRefreshDevicesButton.style.borderTopLeftRadius = 10f;
        notesDetectorRefreshDevicesButton.style.borderTopRightRadius = 10f;
        notesDetectorRefreshDevicesButton.style.borderBottomLeftRadius = 10f;
        notesDetectorRefreshDevicesButton.style.borderBottomRightRadius = 10f;
        notesDetectorRefreshDevicesButton.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRefreshDevicesButton.style.fontSize = 24f;
        notesDetectorDeviceRow.Add(notesDetectorRefreshDevicesButton);

        notesDetectorDeviceColumn.Add(notesDetectorDeviceRow);
        notesDetectorToolbarRow.Add(notesDetectorDeviceColumn);

        notesDetectorTestRuntimeLabel = CreateLabel(string.Empty, 28f, new Color(0.78f, 0.82f, 0.87f, 0.94f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestRuntimeLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestRuntimeLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorTestRuntimeLabel.style.maxWidth = Length.Percent(100f);
        notesDetectorTestRuntimeLabel.style.marginTop = 14f;
        notesDetectorTestRuntimeLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
        notesDetectorToolbarRow.Add(notesDetectorTestRuntimeLabel);

        notesDetectorTestStatusLabel = CreateLabel("Status unavailable.", 38f, LibraryConfirmedSongColor, true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestStatusLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestStatusLabel.style.maxWidth = Length.Percent(100f);
        notesDetectorTestStatusLabel.style.marginBottom = 12f;
        notesDetectorTestStatusLabel.style.whiteSpace = WhiteSpace.Normal;

        notesDetectorTestDetailLabel = CreateLabel(string.Empty, 27f, new Color(0.82f, 0.88f, 0.94f, 0.96f), false, TextAnchor.UpperLeft, useTitleFont: false);
        notesDetectorTestDetailLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestDetailLabel.style.maxWidth = Length.Percent(100f);
        notesDetectorTestDetailLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorTestDetailLabel.style.marginBottom = 0f;
        notesDetectorTestDetailLabel.style.unityTextAlign = TextAnchor.UpperLeft;

        VisualElement notesDetectorTestStatusPanel = new VisualElement();
        notesDetectorTestStatusPanel.style.flexGrow = 0f;
        notesDetectorTestStatusPanel.style.flexShrink = 0f;
        notesDetectorTestStatusPanel.style.paddingLeft = 28f;
        notesDetectorTestStatusPanel.style.paddingRight = 28f;
        notesDetectorTestStatusPanel.style.paddingTop = 24f;
        notesDetectorTestStatusPanel.style.paddingBottom = 24f;
        notesDetectorTestStatusPanel.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.65f);
        notesDetectorTestStatusPanel.style.borderTopWidth = 1f;
        notesDetectorTestStatusPanel.style.borderRightWidth = 1f;
        notesDetectorTestStatusPanel.style.borderBottomWidth = 1f;
        notesDetectorTestStatusPanel.style.borderLeftWidth = 1f;
        notesDetectorTestStatusPanel.style.borderTopColor = new Color(0.20f, 0.22f, 0.25f, 1f);
        notesDetectorTestStatusPanel.style.borderRightColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        notesDetectorTestStatusPanel.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 1f);
        notesDetectorTestStatusPanel.style.borderLeftColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        notesDetectorTestStatusPanel.style.borderTopLeftRadius = 16f;
        notesDetectorTestStatusPanel.style.borderTopRightRadius = 16f;
        notesDetectorTestStatusPanel.style.borderBottomLeftRadius = 16f;
        notesDetectorTestStatusPanel.style.borderBottomRightRadius = 16f;
        notesDetectorTestStatusPanel.style.flexDirection = FlexDirection.Column;
        notesDetectorTestStatusPanel.style.marginRight = 28f;

        Label notesDetectorStateHeader = CreateLabel("Live Detector State", 34f, new Color(0.95f, 0.97f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorStateHeader.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorStateHeader.style.marginBottom = 10f;
        notesDetectorTestStatusPanel.Add(notesDetectorStateHeader);
        notesDetectorTestStatusPanel.Add(notesDetectorTestStatusLabel);
        notesDetectorTestStatusPanel.Add(notesDetectorTestDetailLabel);

        VisualElement notesDetectorNotesBlock = new VisualElement();
        notesDetectorNotesBlock.style.marginTop = 18f;
        notesDetectorNotesBlock.style.paddingTop = 18f;
        notesDetectorNotesBlock.style.borderTopWidth = 1f;
        notesDetectorNotesBlock.style.borderTopColor = new Color(0.18f, 0.20f, 0.23f, 0.98f);
        notesDetectorNotesBlock.style.flexDirection = FlexDirection.Column;

        notesDetectorTestFastNotesLabel = CreateLabel("Fast Path Notes: --", 32f, new Color(0.94f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestFastNotesLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestFastNotesLabel.style.marginBottom = 10f;
        notesDetectorTestFastNotesLabel.style.whiteSpace = WhiteSpace.Normal;

        notesDetectorTestAiNotesLabel = CreateLabel("AI Result Notes: --", 32f, new Color(0.95f, 0.78f, 0.54f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorTestAiNotesLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorTestAiNotesLabel.style.whiteSpace = WhiteSpace.Normal;

        notesDetectorNotesBlock.Add(notesDetectorTestFastNotesLabel);
        notesDetectorNotesBlock.Add(notesDetectorTestAiNotesLabel);
        notesDetectorTestStatusPanel.Add(notesDetectorNotesBlock);

        notesDetectorTestCard = new VisualElement();
        notesDetectorTestCard.style.flexGrow = 1f;
        notesDetectorTestCard.style.width = Length.Percent(100f);
        notesDetectorTestCard.style.maxWidth = Length.Percent(100f);
        notesDetectorTestCard.style.paddingLeft = 0f;
        notesDetectorTestCard.style.paddingRight = 0f;
        notesDetectorTestCard.style.paddingTop = 0f;
        notesDetectorTestCard.style.paddingBottom = 0f;
        notesDetectorTestCard.style.marginTop = 10f;
        notesDetectorTestCard.style.marginBottom = 0f;
        notesDetectorTestCard.style.alignSelf = Align.Stretch;
        notesDetectorTestCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        notesDetectorTestCard.style.flexDirection = FlexDirection.Column;
        notesDetectorTestCard.style.alignItems = Align.Stretch;
        notesDetectorTestCard.style.justifyContent = Justify.FlexStart;

        notesDetectorTestStatusPanel.style.marginRight = 0f;
        notesDetectorTestStatusPanel.style.marginBottom = 28f;

        notesDetectorTestButtons = new VisualElement();
        notesDetectorTestButtons.style.flexDirection = FlexDirection.Column;
        notesDetectorTestButtons.style.flexWrap = Wrap.NoWrap;
        notesDetectorTestButtons.style.alignItems = Align.Stretch;
        notesDetectorTestButtons.style.justifyContent = Justify.FlexStart;
        notesDetectorTestButtons.style.alignSelf = Align.Stretch;
        notesDetectorTestButtons.style.width = Length.Percent(100f);
        notesDetectorTestButtons.style.maxWidth = Length.Percent(100f);
        notesDetectorTestButtons.style.paddingTop = 0f;
        notesDetectorTestButtons.style.marginTop = 4f;
        notesDetectorTestButtons.style.marginBottom = 28f;
        notesDetectorTestButtons.style.flexShrink = 0f;

        VisualElement notesDetectorPrimaryButtonsRow = new VisualElement();
        notesDetectorPrimaryButtonsRow.style.flexDirection = FlexDirection.Row;
        notesDetectorPrimaryButtonsRow.style.flexWrap = Wrap.Wrap;
        notesDetectorPrimaryButtonsRow.style.alignItems = Align.Center;
        notesDetectorPrimaryButtonsRow.style.justifyContent = Justify.FlexStart;
        notesDetectorPrimaryButtonsRow.style.alignSelf = Align.Stretch;
        notesDetectorPrimaryButtonsRow.style.width = Length.Percent(100f);
        notesDetectorPrimaryButtonsRow.style.maxWidth = Length.Percent(100f);
        notesDetectorPrimaryButtonsRow.style.marginBottom = 12f;

        notesDetectorRestartButton = CreateActionButton("Restart Detector", () =>
        {
            owner?.SetNotesDetectorTestSelectionFromUi(0);
            owner?.ActivateSelectedNotesDetectorTestActionFromUi();
        });
        notesDetectorRunTestButton = CreateActionButton("Run Test", () =>
        {
            owner?.SetNotesDetectorTestSelectionFromUi(1);
            owner?.ActivateSelectedNotesDetectorTestActionFromUi();
        });
        notesDetectorBackButton = CreateActionButton("Back", () => owner?.CloseNotesDetectorTestFromUi());
        notesDetectorSavePresetButton = CreateActionButton("Save To Custom", () => owner?.SaveNativeNotesDetectorCustomPresetFromUi());
        notesDetectorAdvancedSettingsToggleButton = CreateActionButton("Show Advanced Settings", () =>
        {
            showNotesDetectorAdvancedSettings = !showNotesDetectorAdvancedSettings;
            UpdateNotesDetectorAdvancedSettingsVisibility();
        });

        notesDetectorTestActionButtons.AddRange(new[] { notesDetectorRestartButton, notesDetectorRunTestButton });
        for (int i = 0; i < notesDetectorTestActionButtons.Count; i++)
        {
            int detectorActionIndex = i;
            notesDetectorTestActionButtons[i].RegisterCallback<MouseEnterEvent>(_ => owner?.HoverNotesDetectorTestSelectionFromUi(detectorActionIndex));
            StyleNotesDetectorActionButton(notesDetectorTestActionButtons[i], 220f, 72f);
            notesDetectorTestActionButtons[i].style.marginRight = 12f;
            notesDetectorTestActionButtons[i].style.marginBottom = 12f;
            notesDetectorPrimaryButtonsRow.Add(notesDetectorTestActionButtons[i]);
        }

        notesDetectorPresetDropdown = new DropdownField();
        notesDetectorPresetDropdown.choices = NativeDetectorSettingCatalog.BuildPresetLabels();
        notesDetectorPresetDropdown.value = notesDetectorPresetDropdown.choices.Count > 0 ? notesDetectorPresetDropdown.choices[0] : string.Empty;
        StyleNotesDetectorDropdown(notesDetectorPresetDropdown, 280f);
        notesDetectorPresetDropdown.style.marginRight = 12f;
        notesDetectorPresetDropdown.style.marginBottom = 12f;
        notesDetectorPresetDropdown.RegisterValueChangedCallback(evt =>
        {
            if (suppressNotesDetectorDropdownCallbacks)
                return;

            int presetIndex = notesDetectorPresetDropdown.choices.IndexOf(evt.newValue);
            owner?.SetNativeNotesDetectorPresetFromUi(presetIndex < 0 ? 0 : presetIndex);
        });
        notesDetectorPrimaryButtonsRow.Add(notesDetectorPresetDropdown);

        StyleNotesDetectorActionButton(notesDetectorSavePresetButton, 220f, 72f);
        notesDetectorSavePresetButton.style.marginRight = 22f;
        notesDetectorSavePresetButton.style.marginBottom = 12f;
        notesDetectorSavePresetButton.style.color = LibraryConfirmedSongColor;
        notesDetectorSavePresetButton.style.borderTopColor = LibraryConfirmedSongColor;
        notesDetectorSavePresetButton.style.borderRightColor = LibraryConfirmedSongColor;
        notesDetectorSavePresetButton.style.borderBottomColor = LibraryConfirmedSongColor;
        notesDetectorSavePresetButton.style.borderLeftColor = LibraryConfirmedSongColor;
        notesDetectorPrimaryButtonsRow.Add(notesDetectorSavePresetButton);

        StyleNotesDetectorDropdown(notesDetectorTestInputDeviceDropdown, 500f);
        notesDetectorTestInputDeviceDropdown.style.marginRight = 12f;
        notesDetectorTestInputDeviceDropdown.style.marginBottom = 12f;

        notesDetectorRefreshDevicesButton.style.height = 72f;
        notesDetectorRefreshDevicesButton.style.minWidth = 220f;
        notesDetectorRefreshDevicesButton.style.fontSize = 24f;
        notesDetectorRefreshDevicesButton.style.marginRight = 12f;
        notesDetectorRefreshDevicesButton.style.marginBottom = 12f;

        notesDetectorTestFooterRow = new VisualElement();
        notesDetectorTestFooterRow.style.flexDirection = FlexDirection.Row;
        notesDetectorTestFooterRow.style.justifyContent = Justify.FlexEnd;
        notesDetectorTestFooterRow.style.alignItems = Align.Center;
        notesDetectorTestFooterRow.style.alignSelf = Align.Stretch;
        notesDetectorTestFooterRow.style.marginTop = 2f;

        StyleNotesDetectorActionButton(notesDetectorBackButton, 180f, 72f);
        notesDetectorBackButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverNotesDetectorTestSelectionFromUi(2));
        notesDetectorTestFooterRow.Add(notesDetectorBackButton);

        notesDetectorTestButtons.Add(notesDetectorPrimaryButtonsRow);
        notesDetectorTestButtons.Add(notesDetectorTestFooterRow);
        notesDetectorSettingsScrollView = new ScrollView(ScrollViewMode.Vertical);
        notesDetectorSettingsScrollView.style.flexGrow = 1f;
        notesDetectorSettingsScrollView.style.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 0.48f);
        notesDetectorSettingsScrollView.style.borderTopWidth = 1f;
        notesDetectorSettingsScrollView.style.borderRightWidth = 1f;
        notesDetectorSettingsScrollView.style.borderBottomWidth = 1f;
        notesDetectorSettingsScrollView.style.borderLeftWidth = 1f;
        notesDetectorSettingsScrollView.style.borderTopColor = new Color(0.20f, 0.22f, 0.25f, 0.98f);
        notesDetectorSettingsScrollView.style.borderRightColor = new Color(0.13f, 0.14f, 0.16f, 0.98f);
        notesDetectorSettingsScrollView.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 0.98f);
        notesDetectorSettingsScrollView.style.borderLeftColor = new Color(0.13f, 0.14f, 0.16f, 0.98f);
        notesDetectorSettingsScrollView.style.borderTopLeftRadius = 18f;
        notesDetectorSettingsScrollView.style.borderTopRightRadius = 18f;
        notesDetectorSettingsScrollView.style.borderBottomLeftRadius = 18f;
        notesDetectorSettingsScrollView.style.borderBottomRightRadius = 18f;
        notesDetectorSettingsScrollView.style.paddingLeft = 20f;
        notesDetectorSettingsScrollView.style.paddingRight = 20f;
        notesDetectorSettingsScrollView.style.paddingTop = 18f;
        notesDetectorSettingsScrollView.style.paddingBottom = 18f;

        Label notesDetectorSettingsTitleLabel = CreateLabel("Advanced Detection Settings", 52f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorSettingsTitleLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorSettingsTitleLabel.style.marginBottom = 10f;
        notesDetectorSettingsScrollView.Add(notesDetectorSettingsTitleLabel);

        Label notesDetectorSettingsHintLabel = CreateLabel("Safe accuracy controls are shown by default. Built-in levels stay protected, and only Custom is saved.", 30f, new Color(0.78f, 0.82f, 0.88f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorSettingsHintLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorSettingsHintLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorSettingsHintLabel.style.marginBottom = 26f;
        notesDetectorSettingsScrollView.Add(notesDetectorSettingsHintLabel);

        StyleNotesDetectorActionButton(notesDetectorAdvancedSettingsToggleButton, 320f, 66f);
        notesDetectorAdvancedSettingsToggleButton.style.marginBottom = 24f;
        notesDetectorAdvancedSettingsToggleButton.style.alignSelf = Align.FlexStart;
        notesDetectorSettingsScrollView.Add(notesDetectorAdvancedSettingsToggleButton);

        VisualElement notesDetectorSettingsColumns = new VisualElement();
        notesDetectorSettingsColumns.style.flexDirection = FlexDirection.Row;
        notesDetectorSettingsColumns.style.alignItems = Align.FlexStart;
        notesDetectorSettingsColumns.style.justifyContent = Justify.SpaceBetween;
        notesDetectorSettingsColumns.style.width = Length.Percent(100f);

        VisualElement notesDetectorSettingsLeftColumn = new VisualElement();
        notesDetectorSettingsLeftColumn.style.flexDirection = FlexDirection.Column;
        notesDetectorSettingsLeftColumn.style.flexGrow = 1f;
        notesDetectorSettingsLeftColumn.style.marginRight = 12f;

        VisualElement notesDetectorSettingsRightColumn = new VisualElement();
        notesDetectorSettingsRightColumn.style.flexDirection = FlexDirection.Column;
        notesDetectorSettingsRightColumn.style.flexGrow = 1f;
        notesDetectorSettingsRightColumn.style.marginLeft = 12f;

        for (int i = 0; i < NativeDetectorSettingCatalog.Definitions.Count; i++)
        {
            VisualElement row = CreateNotesDetectorSettingRow(NativeDetectorSettingCatalog.Definitions[i]);
            if ((i & 1) == 0)
                notesDetectorSettingsLeftColumn.Add(row);
            else
                notesDetectorSettingsRightColumn.Add(row);
        }

        notesDetectorSettingsColumns.Add(notesDetectorSettingsLeftColumn);
        notesDetectorSettingsColumns.Add(notesDetectorSettingsRightColumn);
        notesDetectorSettingsScrollView.Add(notesDetectorSettingsColumns);
        UpdateNotesDetectorAdvancedSettingsVisibility();

        notesDetectorTestCard.Add(notesDetectorTestStatusPanel);
        notesDetectorTestCard.Add(notesDetectorTestButtons);
        notesDetectorTestCard.Add(notesDetectorSettingsScrollView);
        notesDetectorTestShell.Add(notesDetectorTestEyebrowLabel);
        notesDetectorTestShell.Add(notesDetectorTestTitleLabel);
        notesDetectorTestShell.Add(notesDetectorTestHintLabel);
        notesDetectorTestShell.Add(notesDetectorToolbarRow);
        notesDetectorTestShell.Add(notesDetectorTestCard);
        notesDetectorTestRegion.Add(notesDetectorTestShell);
        notesDetectorTestOverlay.Add(notesDetectorTestBackplate);
        notesDetectorTestOverlay.Add(notesDetectorTestFrontPlate);
        notesDetectorTestOverlay.Add(notesDetectorTestRegion);

        notesDetectorRoutineOverlay = new VisualElement();
        notesDetectorRoutineOverlay.style.position = Position.Absolute;
        notesDetectorRoutineOverlay.style.left = 0f;
        notesDetectorRoutineOverlay.style.right = 0f;
        notesDetectorRoutineOverlay.style.top = 0f;
        notesDetectorRoutineOverlay.style.bottom = 0f;
        notesDetectorRoutineOverlay.style.display = DisplayStyle.None;
        notesDetectorRoutineOverlay.style.flexDirection = FlexDirection.Column;
        notesDetectorRoutineOverlay.style.backgroundColor = new Color(0.07f, 0.08f, 0.09f, 0.985f);
        notesDetectorRoutineOverlay.style.paddingLeft = 34f;
        notesDetectorRoutineOverlay.style.paddingRight = 34f;
        notesDetectorRoutineOverlay.style.paddingTop = 30f;
        notesDetectorRoutineOverlay.style.paddingBottom = 28f;

        notesDetectorRoutineTopBar = new VisualElement();
        notesDetectorRoutineTopBar.style.flexDirection = FlexDirection.Row;
        notesDetectorRoutineTopBar.style.justifyContent = Justify.SpaceBetween;
        notesDetectorRoutineTopBar.style.alignItems = Align.FlexStart;
        notesDetectorRoutineTopBar.style.marginBottom = 24f;

        VisualElement notesDetectorRoutineHeading = new VisualElement();
        notesDetectorRoutineHeading.style.flexDirection = FlexDirection.Column;
        notesDetectorRoutineHeading.style.flexGrow = 1f;
        notesDetectorRoutineHeading.style.marginRight = 18f;

        Label notesDetectorRoutineTitleLabel = CreateLabel("DETECTION CHECK", 78f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineTitleLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineTitleLabel.style.marginBottom = 8f;
        notesDetectorRoutineHeading.Add(notesDetectorRoutineTitleLabel);

        notesDetectorRoutineHintLabel = CreateLabel("Step 1/10", 28f, new Color(0.72f, 0.79f, 0.86f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineHintLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineHintLabel.style.marginBottom = 10f;
        notesDetectorRoutineHeading.Add(notesDetectorRoutineHintLabel);

        notesDetectorRoutineMessageLabel = CreateLabel("Silence the strings and stop any ringing or handling noise.", 34f, new Color(0.90f, 0.94f, 0.98f, 1f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineMessageLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineMessageLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorRoutineMessageLabel.style.marginBottom = 10f;
        notesDetectorRoutineHeading.Add(notesDetectorRoutineMessageLabel);

        notesDetectorRoutineCalloutLabel = CreateLabel("TARGET  SILENCE", 40f, new Color(0.96f, 0.73f, 0.49f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineCalloutLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineCalloutLabel.style.whiteSpace = WhiteSpace.Normal;
        notesDetectorRoutineHeading.Add(notesDetectorRoutineCalloutLabel);
        notesDetectorRoutineTopBar.Add(notesDetectorRoutineHeading);

        VisualElement notesDetectorRoutineButtons = new VisualElement();
        notesDetectorRoutineButtons.style.flexDirection = FlexDirection.Row;
        notesDetectorRoutineButtons.style.alignItems = Align.Center;
        notesDetectorRoutineButtons.style.justifyContent = Justify.FlexEnd;

        notesDetectorRoutineRestartButton = CreateActionButton("Restart", () => owner?.RunNotesDetectorRoutineFromUi());
        StyleNotesDetectorActionButton(notesDetectorRoutineRestartButton, 190f, 72f);
        notesDetectorRoutineRestartButton.style.marginRight = 12f;
        notesDetectorRoutineButtons.Add(notesDetectorRoutineRestartButton);

        notesDetectorRoutineExitButton = CreateActionButton("Exit", () => owner?.CloseNotesDetectorRoutineFromUi());
        StyleNotesDetectorActionButton(notesDetectorRoutineExitButton, 170f, 72f);
        notesDetectorRoutineButtons.Add(notesDetectorRoutineExitButton);
        notesDetectorRoutineTopBar.Add(notesDetectorRoutineButtons);
        notesDetectorRoutineOverlay.Add(notesDetectorRoutineTopBar);

        notesDetectorRoutineStatusLabel = CreateLabel("NOT DETECTED", 86f, new Color(0.92f, 0.33f, 0.29f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);
        notesDetectorRoutineStatusLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineStatusLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        notesDetectorRoutineStatusLabel.style.marginBottom = 18f;
        notesDetectorRoutineStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        notesDetectorRoutineOverlay.Add(notesDetectorRoutineStatusLabel);

        VisualElement notesDetectorRoutineTabCard = new VisualElement();
        notesDetectorRoutineTabCard.style.flexDirection = FlexDirection.Column;
        notesDetectorRoutineTabCard.style.paddingLeft = 26f;
        notesDetectorRoutineTabCard.style.paddingRight = 26f;
        notesDetectorRoutineTabCard.style.paddingTop = 22f;
        notesDetectorRoutineTabCard.style.paddingBottom = 22f;
        notesDetectorRoutineTabCard.style.marginBottom = 22f;
        notesDetectorRoutineTabCard.style.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 0.82f);
        notesDetectorRoutineTabCard.style.borderTopWidth = 1f;
        notesDetectorRoutineTabCard.style.borderRightWidth = 1f;
        notesDetectorRoutineTabCard.style.borderBottomWidth = 1f;
        notesDetectorRoutineTabCard.style.borderLeftWidth = 1f;
        notesDetectorRoutineTabCard.style.borderTopColor = new Color(0.20f, 0.22f, 0.25f, 0.98f);
        notesDetectorRoutineTabCard.style.borderRightColor = new Color(0.13f, 0.14f, 0.16f, 0.98f);
        notesDetectorRoutineTabCard.style.borderBottomColor = new Color(0.12f, 0.13f, 0.15f, 0.98f);
        notesDetectorRoutineTabCard.style.borderLeftColor = new Color(0.13f, 0.14f, 0.16f, 0.98f);
        notesDetectorRoutineTabCard.style.borderTopLeftRadius = 18f;
        notesDetectorRoutineTabCard.style.borderTopRightRadius = 18f;
        notesDetectorRoutineTabCard.style.borderBottomLeftRadius = 18f;
        notesDetectorRoutineTabCard.style.borderBottomRightRadius = 18f;
        notesDetectorRoutineTabCard.style.flexGrow = 1f;

        Label notesDetectorRoutineTabTitle = CreateLabel("Expected Tab", 34f, new Color(0.96f, 0.99f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineTabTitle.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineTabTitle.style.marginBottom = 12f;
        notesDetectorRoutineTabCard.Add(notesDetectorRoutineTabTitle);

        for (int i = 0; i < 6; i++)
        {
            Label rowLabel = CreateLabel("e|-------|", 46f, new Color(0.92f, 0.95f, 0.99f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
            rowLabel.style.unityFontDefinition = modernUiFontDefinition;
            rowLabel.style.unityTextAlign = TextAnchor.MiddleLeft;
            rowLabel.style.marginBottom = i < 5 ? 8f : 0f;
            notesDetectorRoutineTabRowLabels.Add(rowLabel);
            notesDetectorRoutineTabCard.Add(rowLabel);
        }
        notesDetectorRoutineOverlay.Add(notesDetectorRoutineTabCard);

        VisualElement notesDetectorRoutineReceivedCard = new VisualElement();
        notesDetectorRoutineReceivedCard.style.flexDirection = FlexDirection.Column;
        notesDetectorRoutineReceivedCard.style.paddingLeft = 24f;
        notesDetectorRoutineReceivedCard.style.paddingRight = 24f;
        notesDetectorRoutineReceivedCard.style.paddingTop = 20f;
        notesDetectorRoutineReceivedCard.style.paddingBottom = 20f;
        notesDetectorRoutineReceivedCard.style.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 0.72f);
        notesDetectorRoutineReceivedCard.style.borderTopLeftRadius = 18f;
        notesDetectorRoutineReceivedCard.style.borderTopRightRadius = 18f;
        notesDetectorRoutineReceivedCard.style.borderBottomLeftRadius = 18f;
        notesDetectorRoutineReceivedCard.style.borderBottomRightRadius = 18f;

        notesDetectorRoutineFastNotesLabel = CreateLabel("Fast Path Notes: --", 30f, new Color(0.94f, 0.96f, 0.98f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineFastNotesLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineFastNotesLabel.style.marginBottom = 10f;
        notesDetectorRoutineReceivedCard.Add(notesDetectorRoutineFastNotesLabel);

        notesDetectorRoutineAiNotesLabel = CreateLabel("AI Result Notes: --", 30f, new Color(0.95f, 0.78f, 0.54f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        notesDetectorRoutineAiNotesLabel.style.unityFontDefinition = modernUiFontDefinition;
        notesDetectorRoutineReceivedCard.Add(notesDetectorRoutineAiNotesLabel);
        notesDetectorRoutineOverlay.Add(notesDetectorRoutineReceivedCard);

        notesDetectorTestOverlay.Add(notesDetectorRoutineOverlay);

        heroModeSettingsPopup = new ModernMenuPopup(
            "HERO MODE",
            "Hero Mode Settings",
            "Adjust starting health.",
            "5 hearts",
            "Left/Right adjusts  \u2022  Enter or Esc back",
            "Back",
            () => owner?.CloseHeroModeSettingsFromUi(),
            modernUiFontDefinition,
            titleFontDefinition);
        heroModeSettingsOverlay = heroModeSettingsPopup.Root;
        heroModeSettingsOverlay.style.display = DisplayStyle.None;
        heroModeSettingsMessageLabel = heroModeSettingsPopup.Root.QRequired<Label>("modern-popup-message");
        heroModeSettingsCalloutLabel = heroModeSettingsPopup.Root.QRequired<Label>("modern-popup-callout");
        heroModeSettingsHintLabel = heroModeSettingsPopup.Root.QRequired<Label>("modern-popup-hint");
        heroModeSettingsMessageLabel.style.whiteSpace = WhiteSpace.Normal;
        heroModeSettingsCalloutLabel.style.whiteSpace = WhiteSpace.Normal;
        heroModeSettingsHintLabel.style.whiteSpace = WhiteSpace.Normal;
        heroModeSettingsCalloutLabel.text = "STARTING HEARTS";
        heroModeSettingsCalloutLabel.style.marginBottom = 8f;

        heroModeSettingsValueRow = new VisualElement();
        heroModeSettingsValueRow.style.flexDirection = FlexDirection.Row;
        heroModeSettingsValueRow.style.alignItems = Align.Center;
        heroModeSettingsValueRow.style.justifyContent = Justify.Center;
        heroModeSettingsValueRow.style.width = Length.Percent(100f);
        heroModeSettingsValueRow.style.marginTop = 6f;
        heroModeSettingsValueRow.style.marginBottom = 8f;

        heroModeSettingsLeftArrowButton = CreateGlobalSettingsArrowButton("\u2039", () => owner?.AdjustHeroModeHeartCountFromUi(-1));
        heroModeSettingsRightArrowButton = CreateGlobalSettingsArrowButton("\u203A", () => owner?.AdjustHeroModeHeartCountFromUi(1));

        heroModeSettingsValueLabel = CreateLabel("5 hearts", 54f, GlobalSecondaryAccentColor, true, TextAnchor.MiddleCenter, useTitleFont: false);
        heroModeSettingsValueLabel.style.unityFontDefinition = modernUiFontDefinition;
        heroModeSettingsValueLabel.style.minWidth = 220f;
        heroModeSettingsValueLabel.style.marginLeft = 18f;
        heroModeSettingsValueLabel.style.marginRight = 18f;
        heroModeSettingsValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        heroModeSettingsLeftArrowButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverHeroModeSettingsSelectionFromUi(0));
        heroModeSettingsRightArrowButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverHeroModeSettingsSelectionFromUi(0));
        heroModeSettingsValueRow.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverHeroModeSettingsSelectionFromUi(0));
        heroModeSettingsValueLabel.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverHeroModeSettingsSelectionFromUi(0));
        heroModeSettingsPopup.PrimaryButton.focusable = false;
        heroModeSettingsPopup.PrimaryButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverHeroModeSettingsSelectionFromUi(1));

        heroModeSettingsValueRow.Add(heroModeSettingsLeftArrowButton);
        heroModeSettingsValueRow.Add(heroModeSettingsValueLabel);
        heroModeSettingsValueRow.Add(heroModeSettingsRightArrowButton);
        heroModeSettingsPopup.ContentHost.Add(heroModeSettingsValueRow);



        loopSetupOverlay = CreateFullscreenOverlay();

        loopSetupOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        loopSetupOverlay.style.alignItems = Align.Center;

        loopSetupOverlay.style.justifyContent = Justify.FlexEnd;

        loopSetupOverlay.style.paddingTop = 0f;

        loopSetupOverlay.style.paddingBottom = 36f;

        loopSetupOverlay.style.display = DisplayStyle.None;

        loopSetupOverlay.pickingMode = PickingMode.Ignore;



        loopSetupBar = new VisualElement();

        loopSetupBar.style.width = Length.Percent(82f);

        loopSetupBar.style.maxWidth = 1620f;

        loopSetupBar.style.minWidth = 720f;

        loopSetupBar.style.paddingLeft = 34f;

        loopSetupBar.style.paddingRight = 34f;

        loopSetupBar.style.paddingTop = 24f;

        loopSetupBar.style.paddingBottom = 24f;

        loopSetupBar.style.backgroundColor = LibraryPanelColor;

        loopSetupBar.style.borderTopLeftRadius = 20f;

        loopSetupBar.style.borderTopRightRadius = 20f;

        loopSetupBar.style.borderBottomLeftRadius = 20f;

        loopSetupBar.style.borderBottomRightRadius = 20f;

        loopSetupBar.style.borderTopWidth = 4f;

        loopSetupBar.style.borderRightWidth = 4f;

        loopSetupBar.style.borderBottomWidth = 4f;

        loopSetupBar.style.borderLeftWidth = 4f;

        loopSetupBar.style.borderTopColor = GlobalDeepPanelColor;

        loopSetupBar.style.borderRightColor = GlobalDeepPanelColor;

        loopSetupBar.style.borderBottomColor = GlobalDeepPanelColor;

        loopSetupBar.style.borderLeftColor = GlobalDeepPanelColor;

        loopSetupBar.style.alignItems = Align.Center;

        loopSetupBar.style.justifyContent = Justify.Center;

        loopSetupBar.pickingMode = PickingMode.Ignore;



        loopSetupStatusLabel = CreateLabel("Loop Setup", 34f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: false);

        loopSetupStatusLabel.style.unityFontDefinition = modernUiFontDefinition;

        loopSetupStatusLabel.style.marginBottom = 10f;

        loopSetupStatusLabel.style.whiteSpace = WhiteSpace.Normal;

        loopSetupStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        loopSetupHintLabel = CreateLabel("Space preview  \u2022  1 set start  \u2022  2 set end  \u2022  3 move time  \u2022  Esc back", 34f, new Color(0.78f, 0.86f, 0.93f, 0.96f), false, TextAnchor.MiddleCenter, useTitleFont: false);

        loopSetupHintLabel.style.unityFontDefinition = modernUiFontDefinition;

        loopSetupHintLabel.style.whiteSpace = WhiteSpace.Normal;

        loopSetupHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        loopSetupBar.Add(loopSetupStatusLabel);

        loopSetupBar.Add(loopSetupHintLabel);

        loopSetupOverlay.Add(loopSetupBar);

        offsetHelperOverlay = CreateFullscreenOverlay();

        offsetHelperOverlay.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        offsetHelperOverlay.style.alignItems = Align.Center;

        offsetHelperOverlay.style.justifyContent = Justify.FlexEnd;

        offsetHelperOverlay.style.paddingTop = 0f;

        offsetHelperOverlay.style.paddingBottom = 36f;

        offsetHelperOverlay.style.display = DisplayStyle.None;

        offsetHelperOverlay.pickingMode = PickingMode.Ignore;

        offsetHelperBar = new VisualElement();

        offsetHelperBar.style.width = Length.Percent(84f);

        offsetHelperBar.style.maxWidth = 1680f;

        offsetHelperBar.style.minWidth = 760f;

        offsetHelperBar.style.paddingLeft = 34f;

        offsetHelperBar.style.paddingRight = 34f;

        offsetHelperBar.style.paddingTop = 24f;

        offsetHelperBar.style.paddingBottom = 24f;

        offsetHelperBar.style.backgroundColor = LibraryPanelColor;

        offsetHelperBar.style.borderTopLeftRadius = 20f;

        offsetHelperBar.style.borderTopRightRadius = 20f;

        offsetHelperBar.style.borderBottomLeftRadius = 20f;

        offsetHelperBar.style.borderBottomRightRadius = 20f;

        offsetHelperBar.style.borderTopWidth = 4f;

        offsetHelperBar.style.borderRightWidth = 4f;

        offsetHelperBar.style.borderBottomWidth = 4f;

        offsetHelperBar.style.borderLeftWidth = 4f;

        offsetHelperBar.style.borderTopColor = GlobalDeepPanelColor;

        offsetHelperBar.style.borderRightColor = GlobalDeepPanelColor;

        offsetHelperBar.style.borderBottomColor = GlobalDeepPanelColor;

        offsetHelperBar.style.borderLeftColor = GlobalDeepPanelColor;

        offsetHelperBar.style.alignItems = Align.Center;

        offsetHelperBar.style.justifyContent = Justify.Center;

        offsetHelperBar.pickingMode = PickingMode.Ignore;

        offsetHelperTitleLabel = CreateLabel("OFFSET HELPER \u2022 SELECT ANCHOR", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        offsetHelperTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        offsetHelperTitleLabel.style.marginBottom = 10f;

        offsetHelperTitleLabel.style.letterSpacing = 1.2f;

        offsetHelperTitleLabel.style.whiteSpace = WhiteSpace.Normal;

        offsetHelperTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        offsetHelperStatusLabel = CreateLabel("ANCHOR 0.00s \u2022 CURRENT OFFSET 0 ms", 34f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: false);

        offsetHelperStatusLabel.style.unityFontDefinition = modernUiFontDefinition;

        offsetHelperStatusLabel.style.marginBottom = 10f;

        offsetHelperStatusLabel.style.whiteSpace = WhiteSpace.Normal;

        offsetHelperStatusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        offsetHelperHintLabel = CreateLabel("Left/Right or Up/Down select note\nEnter start calibration \u2022 Esc back to Song Settings", 40f, new Color(0.82f, 0.90f, 0.97f, 0.98f), false, TextAnchor.MiddleCenter, useTitleFont: false);

        offsetHelperHintLabel.style.unityFontDefinition = modernUiFontDefinition;

        offsetHelperHintLabel.style.whiteSpace = WhiteSpace.Normal;

        offsetHelperHintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        offsetHelperBar.Add(offsetHelperTitleLabel);

        offsetHelperBar.Add(offsetHelperStatusLabel);

        offsetHelperBar.Add(offsetHelperHintLabel);

        offsetHelperOverlay.Add(offsetHelperBar);



        mainMenuOverlay = CreateFullscreenOverlay();

        Label mainMenuTopTag = CreateLabel("\u25C9 INTERACTIVE MUSIC EXPERIENCE \u25C9", 30f, new Color(1f, 0.73f, 0.33f, 0.95f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        mainMenuTopTag.style.marginBottom = 6f;

        mainMenuTopTag.style.letterSpacing = 1.4f;



        VisualElement logoWrap = new VisualElement();

        logoWrap.style.alignItems = Align.Center;

        logoWrap.style.marginBottom = 18f;



        logoWrap.Add(CreateStringTheoryLogo(132f, 124f, 84f, 2.2f, -8f, 2f));



        VisualElement mainMenuCard = new VisualElement();

        mainMenuCard.style.width = 1040f;

        mainMenuCard.style.maxWidth = 1200f;

        mainMenuCard.style.paddingLeft = 32f;

        mainMenuCard.style.paddingRight = 32f;

        mainMenuCard.style.paddingTop = 26f;

        mainMenuCard.style.paddingBottom = 26f;

        StyleCard(mainMenuCard, new Color(0.04f, 0.07f, 0.14f, 0.96f), radius: 20f);



        Label mainMenuHint = CreateLabel("Choose your next move", 30f, new Color(0.82f, 0.92f, 1f, 0.98f), false, TextAnchor.MiddleCenter);

        mainMenuHint.style.marginBottom = 14f;



        VisualElement mainMenuButtons = new VisualElement();

        mainMenuButtons.style.flexDirection = FlexDirection.Column;

        mainMenuButtons.style.alignItems = Align.Center;



        Button continueButton = CreateActionButton("Continue", () => owner?.ContinueFromMainMenuFromUi());

        Button libraryButton = CreateActionButton("Song Selection", () => owner?.OpenSongSelectionFromUi());

        Button settingsButton = CreateActionButton("Settings", () => owner?.OpenGlobalSettingsFromUi());

        Button mainMenuToneLabButton = CreateActionButton("Tone Lab", () => owner?.OpenToneLabFromUi());

        Button tunerButton = CreateActionButton("Tuner (Coming Soon)", null);

        tunerButton.SetEnabled(false);

        tunerButton.style.opacity = 0.60f;

        Button exitButton = CreateActionButton("Exit", () => owner?.ExitGameFromUi());



        foreach (Button button in new[] { continueButton, libraryButton, settingsButton, mainMenuToneLabButton, tunerButton, exitButton })

        {

            button.style.width = 620f;

            button.style.maxWidth = Length.Percent(94f);

            button.style.marginTop = 8f;

            button.style.marginBottom = 8f;

            ApplyDefaultButtonEdgeColor(button);

            mainMenuButtons.Add(button);

        }



        mainMenuCard.Add(mainMenuHint);

        mainMenuCard.Add(mainMenuButtons);

        mainMenuOverlay.Add(mainMenuTopTag);

        mainMenuOverlay.Add(logoWrap);

        mainMenuOverlay.Add(mainMenuCard);

        mainMenuOverlay.Clear();

        mainMenuOverlay.style.alignItems = Align.Stretch;

        mainMenuOverlay.style.justifyContent = Justify.FlexStart;

        mainMenuOverlay.style.paddingTop = 180f;

        mainMenuOverlay.style.paddingLeft = 48f;

        mainMenuOverlay.style.paddingRight = 32f;

        mainMenuOverlay.style.paddingBottom = 36f;

        mainMenuOverlay.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.44f);



        mainMenuBackgroundPlane = new VisualElement();

        mainMenuBackgroundPlane.style.position = Position.Absolute;

        mainMenuBackgroundPlane.pickingMode = PickingMode.Ignore;

        mainMenuBackgroundPlane.style.left = Length.Percent(31f);

        mainMenuBackgroundPlane.style.top = -120f;

        mainMenuBackgroundPlane.style.width = Length.Percent(78f);

        mainMenuBackgroundPlane.style.height = Length.Percent(140f);

        mainMenuBackgroundPlane.style.backgroundColor = new Color(0.53f, 0.30f, 0.35f, 1f);

        mainMenuBackgroundPlane.style.rotate = new Rotate(new Angle(8f, AngleUnit.Degree));



        mainMenuBackgroundPlane.style.display = DisplayStyle.None;

        mainMenuOverlay.Add(mainMenuBackgroundPlane);



        mainMenuShell = new VisualElement();

        mainMenuShell.style.flexDirection = FlexDirection.Row;

        mainMenuShell.style.alignItems = Align.Stretch;

        mainMenuShell.style.flexGrow = 1f;

        mainMenuShell.style.maxWidth = 980f;

        mainMenuShell.style.width = Length.Percent(100f);

        mainMenuShell.style.alignSelf = Align.FlexStart;

        mainMenuShell.style.marginTop = 120f;



        mainMenuLeftColumn = new VisualElement();

        mainMenuLeftColumn.style.flexBasis = 0f;

        mainMenuLeftColumn.style.flexGrow = 1f;

        mainMenuLeftColumn.style.flexShrink = 1f;

        mainMenuLeftColumn.style.paddingTop = 0f;

        mainMenuLeftColumn.style.paddingBottom = 12f;

        mainMenuLeftColumn.style.paddingRight = 0f;

        mainMenuLeftColumn.style.paddingLeft = 0f;

        mainMenuLeftColumn.style.justifyContent = Justify.FlexStart;



        VisualElement mainMenuContentRail = new VisualElement();

        mainMenuContentRail.style.paddingLeft = 78f;

        mainMenuContentRail.style.alignItems = Align.FlexStart;

        mainMenuContentRail.style.width = Length.Percent(100f);



        mainMenuEyebrowLabel = CreateLabel("INTERACTIVE MUSIC EXPERIENCE", 30f, new Color(0.66f, 0.86f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuEyebrowLabel.style.letterSpacing = 4.6f;

        mainMenuEyebrowLabel.style.marginLeft = 0f;

        mainMenuEyebrowLabel.style.marginBottom = 28f;



        VisualElement mainMenuBrandWrap = new VisualElement();

        mainMenuBrandWrap.style.marginLeft = 0f;

        mainMenuBrandWrap.style.marginBottom = 48f;

        mainMenuBrandWrap.style.alignItems = Align.FlexStart;

        mainMenuBrandWrap.Add(CreateStringTheoryLogo(188f, 178f, 0f, 3.0f, -14f, 2.8f));



        mainMenuTitleLabel = CreateLabel(string.Empty, 110f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

        mainMenuTitleLabel.style.letterSpacing = 0.4f;

        mainMenuTitleLabel.style.marginBottom = 0f;

        mainMenuTitleLabel.style.whiteSpace = WhiteSpace.Normal;

        mainMenuTitleLabel.style.display = DisplayStyle.None;



        mainMenuSubtitleLabel = CreateLabel(string.Empty, 31f, new Color(0.80f, 0.90f, 1f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuSubtitleLabel.style.maxWidth = 720f;

        mainMenuSubtitleLabel.style.whiteSpace = WhiteSpace.Normal;

        mainMenuSubtitleLabel.style.marginBottom = 0f;

        mainMenuSubtitleLabel.style.display = DisplayStyle.None;



        mainMenuNavColumn = new VisualElement();

        mainMenuNavColumn.style.flexDirection = FlexDirection.Column;

        mainMenuNavColumn.style.width = Length.Percent(100f);

        mainMenuNavColumn.style.maxWidth = 900f;

        mainMenuNavColumn.style.marginBottom = 0f;

        mainMenuNavColumn.style.marginTop = 34f;



        CreateMainMenuEntry("Continue", string.Empty, new Color(0.29f, 0.85f, 0.58f, 1f), () => owner?.ContinueFromMainMenuFromUi());

        CreateMainMenuEntry("Library", string.Empty, new Color(0.28f, 0.77f, 1f, 1f), () => owner?.OpenSongSelectionFromUi());

        CreateMainMenuEntry("Settings", string.Empty, new Color(0.68f, 0.61f, 1f, 1f), () => owner?.OpenGlobalSettingsFromUi());

        CreateMainMenuEntry("Notes Detector", string.Empty, new Color(0.49f, 0.82f, 1f, 1f), () => owner?.OpenNotesDetectorTestFromUi());

        CreateMainMenuEntry("Tone Lab", string.Empty, new Color(0.21f, 0.88f, 0.84f, 1f), () => owner?.OpenToneLabFromUi());

        CreateMainMenuEntry("Exit", string.Empty, new Color(0.96f, 0.46f, 0.55f, 1f), () => owner?.ExitGameFromUi());



        mainMenuFooterHintLabel = CreateLabel("Use mouse, \u2191/\u2193, and Enter. Esc returns to pause.", 24f, new Color(0.62f, 0.78f, 0.94f, 0.92f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuFooterHintLabel.style.letterSpacing = 0.45f;

        mainMenuFooterHintLabel.style.display = DisplayStyle.None;



        mainMenuContentRail.Add(mainMenuBrandWrap);

        mainMenuContentRail.Add(mainMenuEyebrowLabel);

        mainMenuContentRail.Add(mainMenuTitleLabel);

        mainMenuContentRail.Add(mainMenuSubtitleLabel);

        mainMenuContentRail.Add(mainMenuNavColumn);

        mainMenuContentRail.Add(mainMenuFooterHintLabel);

        mainMenuLeftColumn.Add(mainMenuContentRail);



        mainMenuRightColumn = new VisualElement();

        mainMenuRightColumn.style.flexBasis = 0f;

        mainMenuRightColumn.style.flexGrow = 0f;

        mainMenuRightColumn.style.flexShrink = 1f;

        mainMenuRightColumn.style.justifyContent = Justify.Center;

        mainMenuRightColumn.style.alignItems = Align.Stretch;

        mainMenuRightColumn.style.display = DisplayStyle.None;



        VisualElement mainMenuSpotlightCard = new VisualElement();

        mainMenuSpotlightCard.style.flexGrow = 1f;

        mainMenuSpotlightCard.style.minHeight = 520f;

        mainMenuSpotlightCard.style.paddingLeft = 32f;

        mainMenuSpotlightCard.style.paddingRight = 32f;

        mainMenuSpotlightCard.style.paddingTop = 28f;

        mainMenuSpotlightCard.style.paddingBottom = 28f;

        StyleCard(mainMenuSpotlightCard, new Color(0.05f, 0.10f, 0.18f, 0.76f), radius: 28f);

        mainMenuSpotlightCard.style.borderTopColor = new Color(0.30f, 0.63f, 1f, 0.72f);

        mainMenuSpotlightCard.style.borderRightColor = new Color(0.20f, 0.47f, 0.83f, 0.58f);

        mainMenuSpotlightCard.style.borderBottomColor = new Color(0.10f, 0.23f, 0.42f, 0.82f);

        mainMenuSpotlightCard.style.borderLeftColor = new Color(0.20f, 0.47f, 0.83f, 0.58f);



        Label mainMenuSpotlightEyebrow = CreateLabel("CURRENT SESSION", 22f, new Color(0.61f, 0.82f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuSpotlightEyebrow.style.letterSpacing = 3.2f;

        mainMenuSpotlightEyebrow.style.marginBottom = 12f;



        mainMenuCurrentSongValueLabel = CreateLabel("No song loaded", 56f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

        mainMenuCurrentSongValueLabel.style.whiteSpace = WhiteSpace.Normal;

        mainMenuCurrentSongValueLabel.style.marginBottom = 10f;



        mainMenuCurrentTrackValueLabel = CreateLabel("Track", 28f, new Color(0.76f, 0.90f, 1f, 0.95f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuCurrentTrackValueLabel.style.marginBottom = 26f;



        VisualElement mainMenuMetricGrid = new VisualElement();

        mainMenuMetricGrid.style.flexDirection = FlexDirection.Row;

        mainMenuMetricGrid.style.flexWrap = Wrap.Wrap;

        mainMenuMetricGrid.style.marginLeft = -6f;

        mainMenuMetricGrid.style.marginRight = -6f;

        mainMenuMetricGrid.style.marginBottom = 24f;



        mainMenuCurrentSpeedValueLabel = CreateMainMenuMetricCard(mainMenuMetricGrid, "PLAYBACK", "100%");

        mainMenuCurrentDetectorValueLabel = CreateMainMenuMetricCard(mainMenuMetricGrid, "DETECTOR", "Offline");



        VisualElement mainMenuStatusCard = new VisualElement();

        mainMenuStatusCard.style.paddingLeft = 22f;

        mainMenuStatusCard.style.paddingRight = 22f;

        mainMenuStatusCard.style.paddingTop = 18f;

        mainMenuStatusCard.style.paddingBottom = 18f;

        mainMenuStatusCard.style.backgroundColor = new Color(0.07f, 0.13f, 0.22f, 0.92f);

        mainMenuStatusCard.style.borderTopLeftRadius = 20f;

        mainMenuStatusCard.style.borderTopRightRadius = 20f;

        mainMenuStatusCard.style.borderBottomLeftRadius = 20f;

        mainMenuStatusCard.style.borderBottomRightRadius = 20f;

        mainMenuStatusCard.style.borderTopWidth = 1f;

        mainMenuStatusCard.style.borderRightWidth = 1f;

        mainMenuStatusCard.style.borderBottomWidth = 1f;

        mainMenuStatusCard.style.borderLeftWidth = 1f;

        mainMenuStatusCard.style.borderTopColor = new Color(0.30f, 0.62f, 0.98f, 0.42f);

        mainMenuStatusCard.style.borderRightColor = new Color(0.30f, 0.62f, 0.98f, 0.30f);

        mainMenuStatusCard.style.borderBottomColor = new Color(0.30f, 0.62f, 0.98f, 0.20f);

        mainMenuStatusCard.style.borderLeftColor = new Color(0.30f, 0.62f, 0.98f, 0.30f);



        Label mainMenuStatusTitle = CreateLabel("Ready for focused practice", 28f, new Color(0.96f, 0.99f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuStatusTitle.style.marginBottom = 8f;



        Label mainMenuStatusBody = CreateLabel("Start from Continue, jump into Library, or fine tune the experience from Settings. Tuner support can be added next as a dedicated flow instead of a dead menu item.", 24f, new Color(0.77f, 0.88f, 0.98f, 0.94f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        mainMenuStatusBody.style.whiteSpace = WhiteSpace.Normal;



        mainMenuStatusCard.Add(mainMenuStatusTitle);

        mainMenuStatusCard.Add(mainMenuStatusBody);



        mainMenuSpotlightCard.Add(mainMenuSpotlightEyebrow);

        mainMenuSpotlightCard.Add(mainMenuCurrentSongValueLabel);

        mainMenuSpotlightCard.Add(mainMenuCurrentTrackValueLabel);

        mainMenuSpotlightCard.Add(mainMenuMetricGrid);

        mainMenuSpotlightCard.Add(mainMenuStatusCard);



        mainMenuRightColumn.Add(mainMenuSpotlightCard);



        mainMenuShell.Add(mainMenuLeftColumn);

        mainMenuShell.Add(mainMenuRightColumn);

        mainMenuOverlay.Add(mainMenuShell);



        settingsOverlay = CreateFullscreenOverlay();

        settingsOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.06f, 0.52f);

        settingsOverlay.style.alignItems = Align.Center;

        settingsOverlay.style.justifyContent = Justify.Center;



        settingsBlurBackdrop = new VisualElement();

        settingsBlurBackdrop.style.position = Position.Absolute;

        settingsBlurBackdrop.style.left = 0f;

        settingsBlurBackdrop.style.right = 0f;

        settingsBlurBackdrop.style.top = 0f;

        settingsBlurBackdrop.style.bottom = 0f;

        settingsBlurBackdrop.style.backgroundColor = new Color(0.03f, 0.04f, 0.06f, 0.12f);

        settingsBlurBackdrop.pickingMode = PickingMode.Ignore;



        settingsBackplate = new VisualElement();

        settingsBackplate.style.position = Position.Absolute;

        settingsBackplate.style.right = -160f;

        settingsBackplate.style.top = -220f;

        settingsBackplate.style.bottom = -220f;

        settingsBackplate.style.width = 1540f;

        settingsBackplate.style.rotate = new Rotate(new Angle(-8f, AngleUnit.Degree));

        settingsBackplate.style.backgroundColor = LibraryConfirmedSongColor;

        settingsBackplate.style.opacity = 0.92f;

        settingsBackplate.pickingMode = PickingMode.Ignore;



        VisualElement settingsBackplateGradient = new VisualElement();

        settingsBackplateGradient.style.position = Position.Absolute;

        settingsBackplateGradient.style.left = 0f;

        settingsBackplateGradient.style.right = 0f;

        settingsBackplateGradient.style.top = 0f;

        settingsBackplateGradient.style.bottom = 0f;

        settingsBackplateGradient.style.backgroundImage = new StyleBackground(GetPauseBackplateGradientTexture());

        settingsBackplateGradient.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

        settingsBackplateGradient.pickingMode = PickingMode.Ignore;

        settingsBackplate.Add(settingsBackplateGradient);



        settingsFrontPlate = new VisualElement();

        settingsFrontPlate.style.position = Position.Absolute;

        settingsFrontPlate.style.right = -120f;

        settingsFrontPlate.style.top = -170f;

        settingsFrontPlate.style.bottom = -170f;

        settingsFrontPlate.style.width = 1460f;

        settingsFrontPlate.style.rotate = new Rotate(new Angle(-6f, AngleUnit.Degree));

        settingsFrontPlate.style.backgroundColor = LibraryPanelColor;

        settingsFrontPlate.pickingMode = PickingMode.Ignore;

        settingsRegion = new VisualElement();

        settingsRegion.style.position = Position.Absolute;

        settingsRegion.style.right = 0f;

        settingsRegion.style.top = 0f;

        settingsRegion.style.bottom = 0f;

        settingsRegion.style.width = Length.Percent(44f);

        settingsRegion.style.alignItems = Align.Center;

        settingsRegion.style.justifyContent = Justify.FlexStart;

        settingsRegion.style.paddingTop = 88f;

        settingsRegion.style.paddingBottom = 0f;

        settingsRegion.style.paddingLeft = 220f;

        settingsRegion.style.paddingRight = 0f;

        settingsRegion.pickingMode = PickingMode.Position;



        settingsShell = new VisualElement();

        settingsShell.style.width = 820f;

        settingsShell.style.maxWidth = 920f;

        settingsShell.style.paddingLeft = 36f;

        settingsShell.style.paddingRight = 36f;

        settingsShell.style.paddingTop = 12f;

        settingsShell.style.paddingBottom = 18f;

        settingsShell.style.alignItems = Align.Center;

        settingsShell.style.position = Position.Relative;

        settingsShell.style.justifyContent = Justify.FlexStart;

        settingsShell.style.alignSelf = Align.Center;

        Label settingsTopTag = CreateLabel("PRESS ESC TO RETURN", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsTopTag.style.unityFontDefinition = modernUiFontDefinition;

        settingsTopTag.style.marginBottom = 10f;

        settingsTopTag.style.letterSpacing = 1.8f;



        Label settingsTitle = CreateLabel("SONG SETTINGS", 112f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsTitle.style.marginBottom = 8f;

        settingsTitle.style.unityFontDefinition = modernUiFontDefinition;

        settingsHintLabel = CreateLabel("Up/Down selects  \u2022  Left/Right adjusts  \u2022  Enter confirms", 34f, new Color(0.82f, 0.92f, 1f, 0.96f), false, TextAnchor.MiddleCenter);

        settingsHintLabel.style.unityFontDefinition = modernUiFontDefinition;

        settingsHintLabel.style.marginBottom = 34f;



        settingsCard = new VisualElement();

        settingsCard.style.width = Length.Percent(100f);

        settingsCard.style.maxWidth = 660f;

        settingsCard.style.paddingLeft = 0f;

        settingsCard.style.paddingRight = -100f;

        settingsCard.style.paddingTop = 0f;

        settingsCard.style.paddingBottom = 0f;

        settingsCard.style.alignSelf = Align.Center;

        settingsCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);



        settingsTrackLabel = CreateLabel(string.Empty, 1f, new Color(0f, 0f, 0f, 0f));

        settingsTrackLabel.style.display = DisplayStyle.None;

        settingsOffsetRow = new VisualElement();

        settingsOffsetRow.style.width = Length.Percent(100f);

        settingsOffsetRow.style.maxWidth = 680f;

        settingsOffsetRow.style.marginBottom = 14f;

        settingsOffsetLabel = CreateLabel("Audio Offset", 34f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsOffsetLabel.style.unityFontDefinition = modernUiFontDefinition;

        settingsOffsetLabel.style.marginBottom = 12f;

        settingsOffsetHelpLabel = CreateSongSettingsHelpLabel("Shifts note timing earlier or later to line up with the music.");

        settingsOffsetSlider = new Slider(-2000f, 2000f);

        settingsOffsetSlider.focusable = false;

        settingsOffsetSlider.style.marginTop = 0f;

        settingsOffsetSlider.style.marginBottom = 0f;

        settingsOffsetSlider.style.height = 68f;

        settingsOffsetSlider.style.width = Length.Percent(100f);

        settingsOffsetSlider.style.maxWidth = 620f;

        settingsOffsetSlider.style.alignSelf = Align.Center;

        settingsOffsetSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetAudioOffsetMsFromUi(evt.newValue); });



        settingsTabSpeedRow = new VisualElement();

        settingsTabSpeedRow.style.width = Length.Percent(100f);

        settingsTabSpeedRow.style.maxWidth = 680f;

        settingsTabSpeedRow.style.marginBottom = 14f;

        settingsTabSpeedLabel = CreateLabel("Note Spacing", 34f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsTabSpeedLabel.style.unityFontDefinition = modernUiFontDefinition;

        settingsTabSpeedLabel.style.marginBottom = 12f;

        Label settingsTabSpeedHelpLabel = CreateSongSettingsHelpLabel("Changes note spacing on screen for this song by making notes travel faster or slower.");

        settingsTabSpeedSlider = new Slider(50f, 150f);

        settingsTabSpeedSlider.focusable = false;

        settingsTabSpeedSlider.style.marginTop = 0f;

        settingsTabSpeedSlider.style.marginBottom = 0f;

        settingsTabSpeedSlider.style.height = 68f;

        settingsTabSpeedSlider.style.width = Length.Percent(100f);

        settingsTabSpeedSlider.style.maxWidth = 620f;

        settingsTabSpeedSlider.style.alignSelf = Align.Center;

        settingsTabSpeedSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetTabSpeedOffsetPercentFromUi(evt.newValue); });



        settingsStartDelayRow = new VisualElement();

        settingsStartDelayRow.style.width = Length.Percent(100f);

        settingsStartDelayRow.style.maxWidth = 680f;

        settingsStartDelayRow.style.marginBottom = 16f;

        settingsStartDelayLabel = CreateLabel("Start Delay", 34f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsStartDelayLabel.style.unityFontDefinition = modernUiFontDefinition;

        settingsStartDelayLabel.style.marginBottom = 12f;

        Label settingsStartDelayHelpLabel = CreateSongSettingsHelpLabel("Adds extra countdown time before the song begins.");

        settingsStartDelaySlider = new Slider(0f, 8f);

        settingsStartDelaySlider.focusable = false;

        settingsStartDelaySlider.style.marginTop = 0f;

        settingsStartDelaySlider.style.marginBottom = 0f;

        settingsStartDelaySlider.style.height = 68f;

        settingsStartDelaySlider.style.width = Length.Percent(100f);

        settingsStartDelaySlider.style.maxWidth = 620f;

        settingsStartDelaySlider.style.alignSelf = Align.Center;

        settingsStartDelaySlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetSongStartDelaySecondsFromUi(evt.newValue); });



        settingsVolumeRow = new VisualElement();

        settingsVolumeRow.style.width = Length.Percent(100f);

        settingsVolumeRow.style.maxWidth = 680f;

        settingsVolumeRow.style.marginBottom = 14f;

        settingsVolumeLabel = CreateLabel("Song Volume", 34f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        settingsVolumeLabel.style.unityFontDefinition = modernUiFontDefinition;

        settingsVolumeLabel.style.marginBottom = 12f;

        Label settingsVolumeHelpLabel = CreateSongSettingsHelpLabel("Controls backing track loudness for this song.");

        settingsVolumeSlider = new Slider(0f, 100f);

        settingsVolumeSlider.focusable = false;

        settingsVolumeSlider.style.marginTop = 0f;

        settingsVolumeSlider.style.marginBottom = 0f;

        settingsVolumeSlider.style.height = 68f;

        settingsVolumeSlider.style.width = Length.Percent(100f);

        settingsVolumeSlider.style.maxWidth = 620f;

        settingsVolumeSlider.style.alignSelf = Align.Center;

        settingsVolumeSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetSongVolumePercentFromUi(evt.newValue); });



        settingsTrackRow = new VisualElement();

        settingsTrackRow.style.width = Length.Percent(100f);

        settingsTrackRow.style.maxWidth = 620f;

        settingsTrackRow.style.marginBottom = 14f;

        settingsTrackRow.style.alignSelf = Align.Center;

        settingsTrackRow.style.position = Position.Relative;

        settingsTrackRow.style.overflow = Overflow.Visible;

        Label settingsTrackHelpLabel = CreateSongSettingsHelpLabel("Choose which arrangement or part this song should use.");

        settingsTrackLeftArrowButton = CreateSongSettingsSelectorArrowButton("\u2039", () => owner?.MoveTrackSelectionFromUi(-1), 5);

        settingsTrackButton = CreateActionButton("Track", () => owner?.MoveTrackSelectionFromUi(1));

        settingsTrackButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        settingsTrackButton.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

        settingsTrackButton.style.borderTopWidth = 2f;

        settingsTrackButton.style.borderRightWidth = 2f;

        settingsTrackButton.style.borderBottomWidth = 2f;

        settingsTrackButton.style.borderLeftWidth = 2f;

        settingsTrackButton.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

        settingsTrackButton.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

        settingsTrackButton.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

        settingsTrackButton.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        settingsTrackButton.style.height = 132f;

        settingsTrackButton.style.minWidth = 0f;

        settingsTrackButton.style.width = Length.Percent(100f);

        settingsTrackButton.style.maxWidth = 620f;

        settingsTrackButton.style.fontSize = 92f;

        settingsTrackButton.style.unityFontDefinition = modernUiFontDefinition;

        settingsTrackRightArrowButton = CreateSongSettingsSelectorArrowButton("\u203A", () => owner?.MoveTrackSelectionFromUi(1), 5);



        settingsOffsetScopeRow = new VisualElement();

        settingsOffsetScopeRow.style.width = Length.Percent(100f);

        settingsOffsetScopeRow.style.maxWidth = 620f;

        settingsOffsetScopeRow.style.marginBottom = 14f;

        settingsOffsetScopeRow.style.alignSelf = Align.Center;

        settingsOffsetScopeRow.style.position = Position.Relative;

        settingsOffsetScopeRow.style.overflow = Overflow.Visible;

        settingsOffsetScopeHelpLabel = CreateSongSettingsHelpLabel("Song applies offset to the whole song. Track applies it only to this arrangement.");

        settingsOffsetScopeLeftArrowButton = CreateSongSettingsSelectorArrowButton("\u2039", () => owner?.ToggleOffsetScopeFromUi(), 6);

        settingsOffsetScopeButton = CreateActionButton("Offset Scope: Song", () => owner?.ToggleOffsetScopeFromUi());

        settingsOffsetScopeButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        settingsOffsetScopeButton.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

        settingsOffsetScopeButton.style.borderTopWidth = 2f;

        settingsOffsetScopeButton.style.borderRightWidth = 2f;

        settingsOffsetScopeButton.style.borderBottomWidth = 2f;

        settingsOffsetScopeButton.style.borderLeftWidth = 2f;

        settingsOffsetScopeButton.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

        settingsOffsetScopeButton.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

        settingsOffsetScopeButton.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

        settingsOffsetScopeButton.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        settingsOffsetScopeButton.style.height = 132f;

        settingsOffsetScopeButton.style.minWidth = 0f;

        settingsOffsetScopeButton.style.width = Length.Percent(100f);

        settingsOffsetScopeButton.style.maxWidth = 620f;

        settingsOffsetScopeButton.style.fontSize = 92f;

        settingsOffsetScopeButton.style.unityFontDefinition = modernUiFontDefinition;

        settingsOffsetScopeRightArrowButton = CreateSongSettingsSelectorArrowButton("\u203A", () => owner?.ToggleOffsetScopeFromUi(), 6);

        settingsPrimaryToolButton = CreateActionButton("Offset Helper", () => owner?.OpenPrimarySongSettingsToolFromUi());

        Button backPauseButton = CreateActionButton("Back", () => owner?.CloseSongSettingsFromUi());

        Button resumeFromSettingsButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        settingsPrimaryToolHelpLabel = CreateSongSettingsHelpLabel("Choose a note to calibrate around, then preview a short loop and nudge offset until the note lines up with the audio.");

        songSettingsActionButtons.AddRange(new[] { settingsPrimaryToolButton, settingsTrackButton, settingsOffsetScopeButton, backPauseButton, resumeFromSettingsButton });
        songSettingsActionSelectionIndices.AddRange(new[] { 0, 5, 6, 7, 8 });



        for (int i = 0; i < songSettingsActionButtons.Count; i++)

        {

            int settingsActionIndex = songSettingsActionSelectionIndices[i];

            Button button = songSettingsActionButtons[i];

            button.style.marginRight = 0f;

            button.style.marginTop = 0f;

            button.style.marginBottom = 14f;

            button.style.height = (button == backPauseButton || button == resumeFromSettingsButton) ? 128f : 132f;

            button.style.minWidth = 0f;

            button.style.width = Length.Percent(100f);

            button.style.maxWidth = 620f;

            button.style.alignSelf = Align.Center;

            button.style.fontSize = (button == backPauseButton || button == resumeFromSettingsButton) ? 96f : 92f;

            button.style.unityFontDefinition = modernUiFontDefinition;

            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

            button.style.borderTopWidth = 2f;

            button.style.borderRightWidth = 2f;

            button.style.borderBottomWidth = 2f;

            button.style.borderLeftWidth = 2f;

            button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

            button.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverSongSettingsSelectionFromUi(settingsActionIndex));

        }



        backPauseButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        backPauseButton.style.color = new Color(0.93f, 0.95f, 0.98f, 1f);

        backPauseButton.style.borderTopColor = MenuOutlineNeutralColor;

        backPauseButton.style.borderRightColor = MenuOutlineNeutralColor;

        backPauseButton.style.borderBottomColor = MenuOutlineNeutralColor;

        backPauseButton.style.borderLeftColor = MenuOutlineNeutralColor;



        resumeFromSettingsButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        resumeFromSettingsButton.style.color = LibraryPrimaryColor;

        resumeFromSettingsButton.style.borderTopColor = LibraryPrimaryColor;

        resumeFromSettingsButton.style.borderRightColor = LibraryPrimaryColor;

        resumeFromSettingsButton.style.borderBottomColor = LibraryPrimaryColor;

        resumeFromSettingsButton.style.borderLeftColor = LibraryPrimaryColor;



        settingsOffsetRow.Add(settingsOffsetLabel);

        settingsOffsetRow.Add(settingsOffsetSlider);

        settingsTabSpeedRow.Add(settingsTabSpeedLabel);

        settingsTabSpeedRow.Add(settingsTabSpeedSlider);

        settingsStartDelayRow.Add(settingsStartDelayLabel);

        settingsStartDelayRow.Add(settingsStartDelaySlider);

        settingsVolumeRow.Add(settingsVolumeLabel);

        settingsVolumeRow.Add(settingsVolumeSlider);

        settingsTrackRow.Add(settingsTrackLeftArrowButton);

        settingsTrackRow.Add(settingsTrackButton);

        settingsTrackRow.Add(settingsTrackRightArrowButton);

        settingsTrackLeftArrowButton.style.left = -128f;

        settingsTrackRightArrowButton.style.right = -128f;

        settingsOffsetScopeRow.Add(settingsOffsetScopeLeftArrowButton);

        settingsOffsetScopeRow.Add(settingsOffsetScopeButton);

        settingsOffsetScopeRow.Add(settingsOffsetScopeRightArrowButton);

        settingsOffsetScopeLeftArrowButton.style.left = -128f;

        settingsOffsetScopeRightArrowButton.style.right = -128f;

        settingsCard.Add(settingsPrimaryToolButton);

        settingsCard.Add(settingsPrimaryToolHelpLabel);

        settingsCard.Add(settingsOffsetRow);

        settingsCard.Add(settingsOffsetHelpLabel);

        settingsCard.Add(settingsTabSpeedRow);

        settingsCard.Add(settingsTabSpeedHelpLabel);

        settingsCard.Add(settingsStartDelayRow);

        settingsCard.Add(settingsStartDelayHelpLabel);

        settingsCard.Add(settingsVolumeRow);

        settingsCard.Add(settingsVolumeHelpLabel);

        settingsCard.Add(settingsTrackRow);

        settingsCard.Add(settingsTrackHelpLabel);

        settingsCard.Add(settingsOffsetScopeRow);

        settingsCard.Add(settingsOffsetScopeHelpLabel);

        settingsCard.Add(backPauseButton);

        settingsCard.Add(resumeFromSettingsButton);



        settingsShell.Add(settingsTopTag);

        settingsShell.Add(settingsTitle);

        settingsShell.Add(settingsHintLabel);

        settingsShell.Add(settingsCard);

        settingsRegion.Add(settingsShell);



        settingsOverlay.Add(settingsBlurBackdrop);

        settingsOverlay.Add(settingsBackplate);

        settingsOverlay.Add(settingsFrontPlate);

        settingsOverlay.Add(settingsRegion);

        generatedAudioTracksPopupOverlay = CreateFullscreenOverlay();
        generatedAudioTracksPopupOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 0.72f);
        generatedAudioTracksPopupOverlay.style.alignItems = Align.Center;
        generatedAudioTracksPopupOverlay.style.justifyContent = Justify.Center;
        generatedAudioTracksPopupOverlay.style.display = DisplayStyle.None;

        VisualElement generatedAudioTracksPopupCard = new VisualElement();
        generatedAudioTracksPopupCard.style.width = Length.Percent(82f);
        generatedAudioTracksPopupCard.style.maxWidth = 1320f;
        generatedAudioTracksPopupCard.style.minWidth = 760f;
        generatedAudioTracksPopupCard.style.maxHeight = Length.Percent(82f);
        generatedAudioTracksPopupCard.style.paddingLeft = 34f;
        generatedAudioTracksPopupCard.style.paddingRight = 34f;
        generatedAudioTracksPopupCard.style.paddingTop = 28f;
        generatedAudioTracksPopupCard.style.paddingBottom = 28f;
        generatedAudioTracksPopupCard.style.backgroundColor = GlobalPanelColor;
        generatedAudioTracksPopupCard.style.borderTopLeftRadius = 24f;
        generatedAudioTracksPopupCard.style.borderTopRightRadius = 24f;
        generatedAudioTracksPopupCard.style.borderBottomLeftRadius = 24f;
        generatedAudioTracksPopupCard.style.borderBottomRightRadius = 24f;
        generatedAudioTracksPopupCard.style.borderTopWidth = 3f;
        generatedAudioTracksPopupCard.style.borderRightWidth = 3f;
        generatedAudioTracksPopupCard.style.borderBottomWidth = 3f;
        generatedAudioTracksPopupCard.style.borderLeftWidth = 3f;
        generatedAudioTracksPopupCard.style.borderTopColor = GlobalDeepPanelColor;
        generatedAudioTracksPopupCard.style.borderRightColor = GlobalDeepPanelColor;
        generatedAudioTracksPopupCard.style.borderBottomColor = GlobalDeepPanelColor;
        generatedAudioTracksPopupCard.style.borderLeftColor = GlobalDeepPanelColor;
        generatedAudioTracksPopupCard.style.flexDirection = FlexDirection.Column;

        Label generatedAudioTracksPopupEyebrow = CreateLabel("GENERATED AUDIO", 24f, GlobalSecondaryAccentColor, true, TextAnchor.MiddleCenter, useTitleFont: false);
        generatedAudioTracksPopupEyebrow.style.unityFontDefinition = modernUiFontDefinition;
        generatedAudioTracksPopupEyebrow.style.marginBottom = 10f;

        Label generatedAudioTracksPopupTitle = CreateLabel("AUDIO TRACKS", 92f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: false);
        generatedAudioTracksPopupTitle.style.unityFontDefinition = modernUiFontDefinition;
        generatedAudioTracksPopupTitle.style.marginBottom = 10f;

        generatedAudioTracksPopupSummaryLabel = CreateLabel("Select which MusicXML parts should be played in generated mode.", 30f, new Color(0.95f, 0.97f, 0.99f, 1f), false, TextAnchor.MiddleCenter, useTitleFont: false);
        generatedAudioTracksPopupSummaryLabel.style.unityFontDefinition = modernUiFontDefinition;
        generatedAudioTracksPopupSummaryLabel.style.whiteSpace = WhiteSpace.Normal;
        generatedAudioTracksPopupSummaryLabel.style.marginBottom = 18f;

        VisualElement generatedAudioTracksPopupButtonsRow = new VisualElement();
        generatedAudioTracksPopupButtonsRow.style.flexDirection = FlexDirection.Row;
        generatedAudioTracksPopupButtonsRow.style.justifyContent = Justify.Center;
        generatedAudioTracksPopupButtonsRow.style.alignItems = Align.Center;
        generatedAudioTracksPopupButtonsRow.style.marginBottom = 16f;

        generatedAudioTracksSelectAllButton = CreateActionButton("Select All", () => owner?.SelectAllGeneratedPlaybackPartsFromUi());
        generatedAudioTracksDeselectAllButton = CreateActionButton("Deselect All", () => owner?.DeselectAllGeneratedPlaybackPartsFromUi());
        generatedAudioTracksCloseButton = CreateActionButton("Close", () => owner?.CloseGeneratedAudioTrackSelectionFromUi());

        Button[] generatedAudioTrackTopButtons = { generatedAudioTracksSelectAllButton, generatedAudioTracksDeselectAllButton, generatedAudioTracksCloseButton };
        for (int i = 0; i < generatedAudioTrackTopButtons.Length; i++)
        {
            Button button = generatedAudioTrackTopButtons[i];
            int popupSelectionIndex = i;
            button.style.minWidth = 220f;
            button.style.height = 96f;
            button.style.marginLeft = 10f;
            button.style.marginRight = 10f;
            button.style.fontSize = 40f;
            button.style.unityFontDefinition = modernUiFontDefinition;
            button.RegisterCallback<MouseEnterEvent>(_ => owner?.SetSelectedGeneratedAudioTrackSelectionIndexFromUi(popupSelectionIndex));
            generatedAudioTracksPopupButtonsRow.Add(button);
        }

        generatedAudioTracksPopupScrollView = new ScrollView(ScrollViewMode.Vertical);
        generatedAudioTracksPopupScrollView.style.flexGrow = 1f;
        generatedAudioTracksPopupScrollView.style.marginTop = 4f;
        generatedAudioTracksPopupScrollView.style.marginBottom = 12f;
        generatedAudioTracksPopupScrollView.style.backgroundColor = new Color(0.05f, 0.06f, 0.07f, 0.62f);
        generatedAudioTracksPopupScrollView.style.borderTopLeftRadius = 18f;
        generatedAudioTracksPopupScrollView.style.borderTopRightRadius = 18f;
        generatedAudioTracksPopupScrollView.style.borderBottomLeftRadius = 18f;
        generatedAudioTracksPopupScrollView.style.borderBottomRightRadius = 18f;
        generatedAudioTracksPopupScrollView.style.paddingLeft = 18f;
        generatedAudioTracksPopupScrollView.style.paddingRight = 18f;
        generatedAudioTracksPopupScrollView.style.paddingTop = 18f;
        generatedAudioTracksPopupScrollView.style.paddingBottom = 18f;

        generatedAudioTracksPopupHintLabel = CreateLabel("Up/Down selects  •  Enter toggles  •  Esc closes", 28f, new Color(0.78f, 0.86f, 0.93f, 0.96f), false, TextAnchor.MiddleCenter, useTitleFont: false);
        generatedAudioTracksPopupHintLabel.style.unityFontDefinition = modernUiFontDefinition;
        generatedAudioTracksPopupHintLabel.style.whiteSpace = WhiteSpace.Normal;

        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupEyebrow);
        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupTitle);
        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupSummaryLabel);
        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupButtonsRow);
        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupScrollView);
        generatedAudioTracksPopupCard.Add(generatedAudioTracksPopupHintLabel);
        generatedAudioTracksPopupOverlay.Add(generatedAudioTracksPopupCard);

        globalSettingsOverlay = CreateFullscreenOverlay();

        globalSettingsOverlay.style.backgroundColor = new Color(0.08f, 0.08f, 0.09f, 0.992f);

        globalSettingsOverlay.style.paddingTop = 56f;

        globalSettingsOverlay.style.paddingBottom = 28f;

        globalSettingsOverlay.style.paddingLeft = 52f;

        globalSettingsOverlay.style.paddingRight = 52f;

        globalSettingsOverlay.style.alignItems = Align.Stretch;

        globalSettingsOverlay.style.justifyContent = Justify.FlexStart;

        Label globalSettingsTopTag = CreateLabel("PRESS ESC TO RETURN", 24f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        globalSettingsTopTag.style.unityFontDefinition = modernUiFontDefinition;

        globalSettingsTopTag.style.marginBottom = 12f;

        globalSettingsTopTag.style.letterSpacing = 1.8f;

        globalSettingsTopTag.style.alignSelf = Align.Center;



        globalSettingsTitleLabel = CreateLabel("SETTINGS", 112f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: false);

        globalSettingsTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        globalSettingsTitleLabel.style.marginBottom = 8f;

        globalSettingsTitleLabel.style.alignSelf = Align.Center;

        globalSettingsHelpLabel = CreateLabel("Choose a category, then adjust values with left and right.", 30f, new Color(0.78f, 0.86f, 0.93f, 0.94f), false, TextAnchor.MiddleCenter);

        globalSettingsHelpLabel.style.unityFontDefinition = modernUiFontDefinition;

        globalSettingsHelpLabel.style.marginBottom = 28f;

        globalSettingsHelpLabel.style.alignSelf = Align.Center;



        globalSettingsCard = new VisualElement();

        globalSettingsCard.style.width = Length.Percent(100f);

        globalSettingsCard.style.maxWidth = StyleKeyword.None;

        globalSettingsCard.style.minWidth = 0f;

        globalSettingsCard.style.flexGrow = 1f;

        globalSettingsCard.style.minHeight = 0f;

        globalSettingsCard.style.paddingLeft = 0f;

        globalSettingsCard.style.paddingRight = 0f;

        globalSettingsCard.style.paddingTop = 0f;

        globalSettingsCard.style.paddingBottom = 0f;

        globalSettingsCard.style.flexDirection = FlexDirection.Column;

        globalSettingsCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        globalSettingsCard.style.borderTopWidth = 0f;

        globalSettingsCard.style.borderRightWidth = 0f;

        globalSettingsCard.style.borderBottomWidth = 0f;

        globalSettingsCard.style.borderLeftWidth = 0f;

        globalSettingsCard.style.alignItems = Align.Stretch;



        VisualElement globalTopButtons = new VisualElement();

        globalTopButtons.style.flexDirection = FlexDirection.Row;

        globalTopButtons.style.flexWrap = Wrap.Wrap;

        globalTopButtons.style.marginBottom = 12f;

        globalTopButtons.style.flexShrink = 0f;

        globalTopButtons.style.display = DisplayStyle.None;



        resetDefaultsButton = CreateActionButton("Reset Settings", () => owner?.ResetGlobalSettingsToDefaultsFromUi());

        resetDefaultsButton.tooltip = "Reload default gameplay and visual tuning values.";

        resetDefaultsButton.style.backgroundColor = LibraryConfirmedSongColor;

        resetDefaultsButton.style.color = LibraryConfirmedSongTextColor;

        resetDefaultsButton.style.borderTopColor = LibraryConfirmedSongColor;

        resetDefaultsButton.style.borderRightColor = LibraryConfirmedSongColor;

        resetDefaultsButton.style.borderBottomColor = LibraryConfirmedSongColor;

        resetDefaultsButton.style.borderLeftColor = LibraryConfirmedSongColor;

        resetDefaultsButton.style.height = 132f;

        resetDefaultsButton.style.fontSize = 92f;

        resetDefaultsButton.style.unityFontDefinition = modernUiFontDefinition;

        resetDefaultsButton.style.minWidth = 360f;

        ConfigureInteractiveButtonHover(resetDefaultsButton, LibraryConfirmedSongColor, LibraryConfirmedSongTextColor);

        globalTopButtons.Add(resetDefaultsButton);

        globalSettingsCard.Add(globalTopButtons);



        globalSettingsScrollView = new ScrollView(ScrollViewMode.Vertical);

        globalSettingsScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        globalSettingsScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

        globalSettingsScrollView.style.flexGrow = 1f;

        globalSettingsScrollView.style.flexShrink = 1f;

        globalSettingsScrollView.style.position = Position.Relative;

        globalSettingsScrollView.style.minHeight = 0f;

        globalSettingsScrollView.style.marginTop = 0f;

        globalSettingsScrollView.style.marginBottom = 0f;

        ConfigureRuntimeScrollView(globalSettingsScrollView);

        AttachGlobalSettingsWheelScrolling(globalSettingsScrollView);

        AttachGlobalSettingsWheelScrolling(globalSettingsScrollView.contentViewport);

        AttachGlobalSettingsWheelScrolling(globalSettingsScrollView.contentContainer);

        globalSettingsCard.style.overflow = Overflow.Hidden;

        globalSettingsCard.Add(globalSettingsScrollView);



        Button globalBackButton = CreateActionButton("Back", () => owner?.CloseGlobalSettingsFromUi());

        Button globalResumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        globalBackButton.style.backgroundColor = new Color(0.24f, 0.30f, 0.43f, 1f);

        globalBackButton.style.color = new Color(0.93f, 0.95f, 0.98f, 1f);

        globalBackButton.style.borderTopColor = new Color(0.30f, 0.36f, 0.50f, 1f);

        globalBackButton.style.borderRightColor = new Color(0.30f, 0.36f, 0.50f, 1f);

        globalBackButton.style.borderBottomColor = new Color(0.30f, 0.36f, 0.50f, 1f);

        globalBackButton.style.borderLeftColor = new Color(0.30f, 0.36f, 0.50f, 1f);

        globalBackButton.style.height = 108f;

        globalBackButton.style.fontSize = 92f;

        globalBackButton.style.unityFontDefinition = modernUiFontDefinition;

        ConfigureInteractiveButtonHover(globalBackButton, new Color(0.24f, 0.30f, 0.43f, 1f), new Color(0.93f, 0.95f, 0.98f, 1f));



        globalResumeButton.style.backgroundColor = LibraryPrimaryColor;

        globalResumeButton.style.color = LibraryPrimaryTextColor;

        globalResumeButton.style.borderTopColor = LibraryPrimaryColor;

        globalResumeButton.style.borderRightColor = LibraryPrimaryColor;

        globalResumeButton.style.borderBottomColor = LibraryPrimaryColor;

        globalResumeButton.style.borderLeftColor = LibraryPrimaryColor;

        globalResumeButton.style.height = 116f;

        globalResumeButton.style.fontSize = 104f;

        globalResumeButton.style.unityFontDefinition = modernUiFontDefinition;

        ConfigureInteractiveButtonHover(globalResumeButton, LibraryPrimaryColor, LibraryPrimaryTextColor);

        AddBottomRightPrimaryButtons(globalSettingsCard, globalBackButton, globalResumeButton);

        globalSettingsOverlay.Add(globalSettingsTopTag);

        globalSettingsOverlay.Add(globalSettingsTitleLabel);

        globalSettingsOverlay.Add(globalSettingsHelpLabel);

        globalSettingsOverlay.Add(globalSettingsCard);



        selectionOverlay = CreateFullscreenOverlay();

        selectionOverlay.style.alignItems = Align.Stretch;

        selectionOverlay.style.backgroundColor = new Color(0.02f, 0.03f, 0.05f, 0.82f);

        selectionOverlay.style.paddingLeft = 42f;

        selectionOverlay.style.paddingRight = 42f;

        selectionOverlay.style.paddingTop = 92f;

        selectionOverlay.style.paddingBottom = 26f;

        selectionOverlay.style.justifyContent = Justify.FlexStart;



        VisualElement selectionHeader = new VisualElement();

        selectionHeader.style.flexDirection = FlexDirection.Column;

        selectionHeader.style.alignItems = Align.FlexStart;

        selectionHeader.style.marginBottom = 12f; 

        selectionHeader.style.position = Position.Absolute;

        selectionHeader.style.left = 42f;

        selectionHeader.style.top = 42f;

        selectionHeader.style.right = StyleKeyword.Auto;

        selectionHeader.style.bottom = StyleKeyword.Auto;



        Label selectionTopTag = CreateLabel("LIBRARY", 24f, new Color(0.52f, 0.87f, 1f, 0.96f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionTopTag.style.display = DisplayStyle.None;



        Label selectionTitle = CreateLabel("Library", 98f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionTitle.style.letterSpacing = -0.4f;

        selectionTitle.style.marginBottom = 0f;  

        selectionTitle.style.unityFontDefinition = modernUiFontDefinition;

        selectionTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

        selectionSubtitleLabel = CreateLabel("", 38f, new Color(0.77f, 0.88f, 0.97f, 0.94f), false, TextAnchor.MiddleLeft);

        selectionSubtitleLabel.style.marginTop = 6f; 

        selectionSubtitleLabel.style.marginBottom = 0f; 

        selectionSubtitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionSubtitleLabel.style.fontSize = 32f;



        selectionHeader.Add(selectionTopTag);

        selectionHeader.Add(selectionTitle);



        selectionLeftBackdrop = new VisualElement();

        selectionLeftBackdrop.style.position = Position.Absolute;

        selectionLeftBackdrop.style.left = 0f;

        selectionLeftBackdrop.style.top = 0f;

        selectionLeftBackdrop.style.bottom = 0f;

        selectionLeftBackdrop.style.right = 0f;

        selectionLeftBackdrop.style.width = StyleKeyword.Auto;

        selectionLeftBackdrop.style.backgroundColor = new Color(0.04f, 0.05f, 0.06f, 0.20f);

        selectionLeftBackdrop.pickingMode = PickingMode.Ignore;



        selectionShell = new VisualElement();

        selectionShell.style.position = Position.Relative;

        selectionShell.style.width = Length.Percent(100f);

        selectionShell.style.maxWidth = 2400f;

        selectionShell.style.flexDirection = FlexDirection.Row;

        selectionShell.style.alignItems = Align.Stretch;

        selectionShell.style.justifyContent = Justify.FlexStart;

        selectionShell.style.flexGrow = 1f;

        selectionShell.style.alignSelf = Align.Center;

        selectionShell.style.paddingLeft = 0f;

        selectionShell.style.paddingRight = 0f;

        selectionShell.pickingMode = PickingMode.Ignore;

        selectionInfoColumn = new VisualElement();

        selectionInfoColumn.style.position = Position.Relative;

        selectionInfoColumn.style.flexBasis = 0f;

        selectionInfoColumn.style.flexGrow = 1f;

        selectionInfoColumn.style.flexShrink = 1f;

        selectionInfoColumn.style.flexDirection = FlexDirection.Column;

        selectionInfoColumn.style.alignItems = Align.Stretch;

        selectionInfoColumn.style.justifyContent = Justify.FlexStart;

        selectionInfoColumn.style.width = Length.Percent(100f);

        selectionInfoColumn.style.minWidth = 0f;

        selectionInfoColumn.style.maxWidth = StyleKeyword.None;

        selectionInfoColumn.pickingMode = PickingMode.Ignore;



        selectionInfoCard = new VisualElement();

        selectionInfoCard.style.flexBasis = 0f;

        selectionInfoCard.style.flexGrow = 1f;

        selectionInfoCard.style.width = Length.Percent(100f);

        selectionInfoCard.style.minWidth = 0f;

        selectionInfoCard.style.maxWidth = StyleKeyword.None;

        selectionInfoCard.style.marginLeft = 28f;

        selectionInfoCard.style.marginRight = 0f;

        selectionInfoCard.style.paddingLeft = 34f;

        selectionInfoCard.style.paddingRight = 34f;

        selectionInfoCard.style.paddingTop = 30f;

        selectionInfoCard.style.paddingBottom = 28f;

        selectionInfoCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.96f);

        selectionInfoCard.style.borderTopWidth = 1f;

        selectionInfoCard.style.borderRightWidth = 1f;

        selectionInfoCard.style.borderBottomWidth = 1f;

        selectionInfoCard.style.borderLeftWidth = 1f;

        selectionInfoCard.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);

        selectionInfoCard.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);

        selectionInfoCard.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);

        selectionInfoCard.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);

        selectionInfoCard.style.borderTopLeftRadius = 22f;

        selectionInfoCard.style.borderTopRightRadius = 22f;

        selectionInfoCard.style.borderBottomLeftRadius = 22f;

        selectionInfoCard.style.borderBottomRightRadius = 22f;

        selectionInfoCard.style.flexDirection = FlexDirection.Column;

        selectionInfoCard.style.overflow = Overflow.Hidden;

        selectionInfoCard.style.alignItems = Align.Stretch;

        selectionInfoCard.style.justifyContent = Justify.FlexStart;

        selectionInfoCard.pickingMode = PickingMode.Position;



        selectionInfoInstructionLabel = CreateLabel("Choose a song on the left, then launch the highlighted track.", 28f, new Color(0.84f, 0.88f, 0.92f, 0.92f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoInstructionLabel.style.letterSpacing = 0.3f;

        selectionInfoInstructionLabel.style.marginBottom = 24f;

        selectionInfoInstructionLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionInfoInstructionLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        Label selectionInfoEyebrow = CreateLabel("SELECTED SONG", 18f, new Color(0.73f, 0.79f, 0.84f, 0.92f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoEyebrow.style.letterSpacing = 1.8f;

        selectionInfoEyebrow.style.marginBottom = 12f;

        selectionInfoEyebrow.style.unityFontDefinition = modernUiFontDefinition;

        selectionInfoEyebrow.style.unityTextAlign = TextAnchor.MiddleLeft;

        VisualElement selectionInfoHero = new VisualElement();

        selectionInfoHero.style.width = Length.Percent(100f);

        selectionInfoHero.style.flexDirection = FlexDirection.Row;

        selectionInfoHero.style.alignItems = Align.Stretch;

        selectionInfoHero.style.justifyContent = Justify.FlexStart;

        selectionInfoHero.style.marginBottom = 24f;

        selectionArtworkCard = new VisualElement();

        selectionArtworkCard.style.width = 320f;

        selectionArtworkCard.style.minWidth = 320f;

        selectionArtworkCard.style.height = 320f;

        selectionArtworkCard.style.marginRight = 28f;

        selectionArtworkCard.style.paddingLeft = 24f;

        selectionArtworkCard.style.paddingRight = 24f;

        selectionArtworkCard.style.paddingTop = 22f;

        selectionArtworkCard.style.paddingBottom = 18f;

        selectionArtworkCard.style.backgroundColor = new Color(0.76f, 0.98f, 0.54f, 1f);

        selectionArtworkCard.style.borderTopLeftRadius = 18f;

        selectionArtworkCard.style.borderTopRightRadius = 18f;

        selectionArtworkCard.style.borderBottomLeftRadius = 18f;

        selectionArtworkCard.style.borderBottomRightRadius = 18f;

        selectionArtworkCard.style.justifyContent = Justify.SpaceBetween;

        selectionArtworkCard.style.alignItems = Align.Stretch;

        selectionArtworkGlyphLabel = CreateLabel("A", 184f, new Color(0.03f, 0.05f, 0.03f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        selectionArtworkGlyphLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionArtworkGlyphLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        selectionArtworkGlyphLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        selectionArtworkGlyphLabel.style.flexGrow = 1f;

        selectionArtworkCaptionLabel = CreateLabel("LIBRARY SELECT", 18f, new Color(0.04f, 0.06f, 0.04f, 0.86f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionArtworkCaptionLabel.style.letterSpacing = 1.8f;

        selectionArtworkCaptionLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionArtworkCard.Add(selectionArtworkGlyphLabel);

        selectionArtworkCard.Add(selectionArtworkCaptionLabel);

        VisualElement selectionInfoTextColumn = new VisualElement();

        selectionInfoTextColumn.style.flexGrow = 1f;

        selectionInfoTextColumn.style.flexBasis = 0f;

        selectionInfoTextColumn.style.minWidth = 0f;

        selectionInfoTextColumn.style.justifyContent = Justify.FlexStart;

        selectionInfoTitleLabel = CreateLabel("Library", 112f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoTitleLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoTitleLabel.style.marginBottom = 10f;

        selectionInfoTitleLabel.style.width = Length.Percent(100f);

        selectionInfoTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionInfoTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        selectionInfoTitleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        selectionInfoMetaLabel = CreateLabel("Artist", 42f, new Color(0.92f, 0.94f, 0.96f, 0.96f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoMetaLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoMetaLabel.style.marginBottom = 10f;

        selectionInfoMetaLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionInfoMetaLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        selectionInfoMetaLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        selectionInfoSummaryLabel = CreateLabel("Song details", 28f, new Color(0.70f, 0.76f, 0.82f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoSummaryLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoSummaryLabel.style.marginBottom = 18f;

        selectionInfoSummaryLabel.style.unityFontDefinition = modernUiFontDefinition;

        VisualElement selectionInfoStatsRow = new VisualElement();

        selectionInfoStatsRow.style.flexDirection = FlexDirection.Row;

        selectionInfoStatsRow.style.alignItems = Align.FlexStart;

        selectionInfoStatsRow.style.justifyContent = Justify.FlexStart;

        selectionInfoStatsRow.style.flexWrap = Wrap.Wrap;

        selectionInfoStatsRow.style.marginTop = 2f;

        VisualElement selectionInfoScoreCard = CreateLibrarySelectionInlineStatBlock("BEST SCORE", out selectionInfoScoreLabel, Color.white);

        selectionInfoScoreLabel.style.fontSize = 52f;

        selectionInfoScoreLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoBestTrackLabel = CreateLabel("--", 22f, new Color(0.82f, 0.85f, 0.90f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoBestTrackLabel.style.marginTop = 4f;

        selectionInfoBestTrackLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionInfoScoreCard.Add(selectionInfoBestTrackLabel);

        VisualElement selectionTracksStatBlock = CreateLibrarySelectionInlineStatBlock("TRACKS", out selectionInfoArrangementValueLabel, Color.white);

        selectionInfoArrangementValueLabel.style.fontSize = 52f;

        VisualElement selectionAudioCard = CreateLibrarySelectionInlineStatBlock("AUDIO", out selectionInfoAudioValueLabel, Color.white);

        selectionInfoAudioValueLabel.style.fontSize = 52f;

        selectionInfoAudioValueLabel.style.whiteSpace = WhiteSpace.Normal;

        VisualElement selectionHeroHeartsCard = CreateLibrarySelectionInlineStatBlock("HERO HEARTS", out selectionInfoHeroHeartsValueLabel, Color.white);

        selectionInfoHeroHeartsValueLabel.style.fontSize = 52f;

        selectionInfoHeroHeartsValueLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoHeroHeartsValueLabel.enableRichText = true;

        selectionInfoStatsRow.Add(selectionInfoScoreCard);

        selectionInfoStatsRow.Add(selectionTracksStatBlock);

        selectionInfoStatsRow.Add(selectionAudioCard);

        selectionInfoStatsRow.Add(selectionHeroHeartsCard);

        selectionInfoTextColumn.Add(selectionInfoEyebrow);

        selectionInfoTextColumn.Add(selectionInfoTitleLabel);

        selectionInfoTextColumn.Add(selectionInfoMetaLabel);

        selectionInfoTextColumn.Add(selectionInfoSummaryLabel);

        selectionInfoTextColumn.Add(selectionInfoStatsRow);

        selectionInfoHero.Add(selectionArtworkCard);

        selectionInfoHero.Add(selectionInfoTextColumn);

        selectionInfoHintLabel = CreateLabel("Review the song details, then choose the arrangement you want to play below.", 22f, new Color(0.82f, 0.85f, 0.90f, 1f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionInfoHintLabel.style.whiteSpace = WhiteSpace.Normal;

        selectionInfoHintLabel.style.marginTop = 18f;

        selectionInfoHintLabel.style.unityFontDefinition = modernUiFontDefinition;

        selectionArrangementCard = new VisualElement();

        selectionArrangementCard.style.flexGrow = 1f;

        selectionArrangementCard.style.flexBasis = 0f;

        selectionArrangementCard.style.flexDirection = FlexDirection.Column;

        selectionArrangementCard.style.width = Length.Percent(100f);

        selectionArrangementCard.style.minHeight = 0f;

        selectionArrangementCard.style.marginTop = 18f;

        selectionArrangementCard.style.paddingLeft = 24f;

        selectionArrangementCard.style.paddingRight = 24f;

        selectionArrangementCard.style.paddingTop = 24f;

        selectionArrangementCard.style.paddingBottom = 20f;

        selectionArrangementCard.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.96f);

        selectionArrangementCard.style.borderTopLeftRadius = 18f;

        selectionArrangementCard.style.borderTopRightRadius = 18f;

        selectionArrangementCard.style.borderBottomLeftRadius = 18f;

        selectionArrangementCard.style.borderBottomRightRadius = 18f;

        selectionArrangementCard.style.borderTopWidth = 1f;

        selectionArrangementCard.style.borderRightWidth = 1f;

        selectionArrangementCard.style.borderBottomWidth = 1f;

        selectionArrangementCard.style.borderLeftWidth = 1f;

        selectionArrangementCard.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);

        selectionArrangementCard.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);

        selectionArrangementCard.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);

        selectionArrangementCard.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);

        selectionArrangementCard.style.overflow = Overflow.Hidden;

        selectionArrangementCard.pickingMode = PickingMode.Position;

        Label arrangementTitle = CreateLabel("TRACKS", 60f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

        arrangementTitle.style.letterSpacing = 1.2f;

        arrangementTitle.style.marginBottom = 8f;

        arrangementTitle.style.unityFontDefinition = modernUiFontDefinition;

        arrangementTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

        Label arrangementSubtitle = CreateLabel("The highlighted arrangement is what Start will launch.", 30f, new Color(0.70f, 0.76f, 0.82f, 0.92f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        arrangementSubtitle.style.marginBottom = 18f;

        arrangementSubtitle.style.unityFontDefinition = modernUiFontDefinition;



        selectionTrackScrollView = new ScrollView(ScrollViewMode.Vertical);

        selectionTrackScrollView.style.flexGrow = 1f;

        selectionTrackScrollView.style.flexBasis = 0f;

        selectionTrackScrollView.style.height = StyleKeyword.Auto;

        selectionTrackScrollView.style.maxHeight = StyleKeyword.None;

        selectionTrackScrollView.style.minHeight = 0f;

        selectionTrackScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        selectionTrackScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

        selectionTrackScrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        selectionTrackScrollView.style.marginBottom = 0f;

        ConfigureRuntimeScrollView(selectionTrackScrollView);

        selectionTrackScrollView.contentContainer.style.paddingLeft = 12f;

        selectionTrackScrollView.contentContainer.style.paddingRight = 12f;

        selectionTrackScrollView.contentContainer.style.paddingTop = 6f;

        selectionTrackScrollView.contentContainer.style.paddingBottom = 6f;

        AttachWheelScrolling(selectionTrackScrollView, selectionTrackScrollView);

        AttachWheelScrolling(selectionTrackScrollView.contentViewport, selectionTrackScrollView);

        AttachWheelScrolling(selectionTrackScrollView.contentContainer, selectionTrackScrollView);



        selectionArrangementCard.Add(arrangementTitle);

        selectionArrangementCard.Add(arrangementSubtitle);

        selectionArrangementCard.Add(selectionTrackScrollView);

        selectionArrangementCard.Add(selectionInfoHintLabel);



        selectionInfoCard.Add(selectionInfoInstructionLabel);

        selectionInfoCard.Add(selectionInfoHero);

        selectionInfoColumn.Add(selectionInfoCard);

        selectionInfoColumn.Add(selectionArrangementCard);



        selectionRailPanel = new VisualElement();

        selectionRailPanel.style.position = Position.Relative;

        selectionRailPanel.style.flexGrow = 0f;

        selectionRailPanel.style.flexShrink = 0f;

        selectionRailPanel.style.flexDirection = FlexDirection.Column;

        selectionRailPanel.style.width = Length.Percent(44f);

        selectionRailPanel.style.minWidth = 640f;

        selectionRailPanel.style.maxWidth = 920f;

        selectionRailPanel.style.right = StyleKeyword.Auto;

        selectionRailPanel.style.top = 0f;

        selectionRailPanel.style.bottom = 0f;

        selectionRailPanel.style.paddingLeft = 24f;

        selectionRailPanel.style.paddingRight = 24f;

        selectionRailPanel.style.paddingTop = 24f;

        selectionRailPanel.style.paddingBottom = 24f;

        selectionRailPanel.style.backgroundColor = new Color(0.06f, 0.07f, 0.08f, 0.96f);

        selectionRailPanel.style.borderTopLeftRadius = 22f;

        selectionRailPanel.style.borderTopRightRadius = 22f;

        selectionRailPanel.style.borderBottomLeftRadius = 22f;

        selectionRailPanel.style.borderBottomRightRadius = 22f;

        selectionRailPanel.style.borderTopWidth = 1f;

        selectionRailPanel.style.borderRightWidth = 1f;

        selectionRailPanel.style.borderBottomWidth = 1f;

        selectionRailPanel.style.borderLeftWidth = 1f;

        selectionRailPanel.style.borderTopColor = new Color(1f, 1f, 1f, 0.08f);

        selectionRailPanel.style.borderRightColor = new Color(1f, 1f, 1f, 0.08f);

        selectionRailPanel.style.borderBottomColor = new Color(1f, 1f, 1f, 0.08f);

        selectionRailPanel.style.borderLeftColor = new Color(1f, 1f, 1f, 0.08f);

        selectionRailPanel.style.overflow = Overflow.Hidden;

        selectionRailPanel.pickingMode = PickingMode.Position;



        selectionRailBackdrop = new VisualElement();

        selectionRailBackdrop.style.position = Position.Absolute;

        selectionRailBackdrop.style.right = -220f;

        selectionRailBackdrop.style.top = -320f;

        selectionRailBackdrop.style.width = 1860f;

        selectionRailBackdrop.style.bottom = -320f;

        selectionRailBackdrop.style.rotate = new Rotate(new Angle(10f, AngleUnit.Degree));

        selectionRailBackdrop.style.backgroundColor = Color.clear;

        selectionRailBackdrop.style.borderTopLeftRadius = 0f;

        selectionRailBackdrop.style.borderBottomLeftRadius = 0f;

        selectionRailBackdrop.style.borderTopRightRadius = 0f;

        selectionRailBackdrop.style.borderBottomRightRadius = 0f;

        selectionRailBackdrop.style.display = DisplayStyle.None;

        selectionRailBackdrop.pickingMode = PickingMode.Ignore;



        selectionRailBackdropGradient = new VisualElement();

        selectionRailBackdropGradient.style.position = Position.Absolute;

        selectionRailBackdropGradient.style.left = 0f;

        selectionRailBackdropGradient.style.right = 0f;

        selectionRailBackdropGradient.style.top = 0f;

        selectionRailBackdropGradient.style.bottom = 0f;

        selectionRailBackdropGradient.style.backgroundColor = new Color(0.03f, 0.03f, 0.035f, 1f);

        selectionRailBackdropGradient.style.backgroundImage = StyleKeyword.None;

        selectionRailBackdropGradient.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

        selectionRailBackdropGradient.pickingMode = PickingMode.Ignore;

        selectionRailBackdrop.Add(selectionRailBackdropGradient);



        selectionSplitDivider = new VisualElement();

        selectionSplitDivider.style.position = Position.Absolute;

        selectionSplitDivider.style.right = -235f;

        selectionSplitDivider.style.top = -260f;

        selectionSplitDivider.style.width = 1800f;

        selectionSplitDivider.style.bottom = -260f;

        selectionSplitDivider.style.rotate = new Rotate(new Angle(9f, AngleUnit.Degree));

        selectionSplitDivider.style.backgroundColor = LibraryPanelColor;

        selectionSplitDivider.style.borderTopLeftRadius = 0f;

        selectionSplitDivider.style.borderBottomLeftRadius = 0f;

        selectionSplitDivider.style.borderTopRightRadius = 0f;

        selectionSplitDivider.style.borderBottomRightRadius = 0f;

        selectionSplitDivider.style.display = DisplayStyle.None;

        selectionSplitDivider.pickingMode = PickingMode.Ignore;



        VisualElement selectionListCard = new VisualElement();

        selectionListCard.style.position = Position.Relative;

        selectionListCard.style.flexBasis = 0f;

        selectionListCard.style.flexGrow = 1f;

        selectionListCard.style.flexDirection = FlexDirection.Column;

        selectionListCard.style.minWidth = 0f;

        selectionListCard.style.maxWidth = StyleKeyword.None;

        selectionListCard.style.marginRight = 0f;

        selectionListCard.style.marginLeft = 0f;

        selectionListCard.style.paddingLeft = 0f;

        selectionListCard.style.paddingRight = 0f;

        selectionListCard.style.paddingTop = 8f;

        selectionListCard.style.paddingBottom = 8f;

        selectionListCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        selectionListCard.style.alignItems = Align.Stretch;

        selectionListCard.pickingMode = PickingMode.Position;



        Label selectionSongsListTitle = CreateLabel("Songs", 92f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

        selectionSongsListTitle.style.letterSpacing = 0.4f;

        selectionSongsListTitle.style.marginBottom = 6f;

        selectionSongsListTitle.style.unityFontDefinition = modernUiFontDefinition;

        selectionSongsListTitle.style.unityFontStyleAndWeight = FontStyle.Bold;

        selectionSongsListTitle.style.alignSelf = Align.FlexStart;

        selectionSubtitleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        selectionSubtitleLabel.style.alignSelf = Align.FlexStart;

        selectionSubtitleLabel.style.marginBottom = 18f;



        selectionScrollView = new ScrollView(ScrollViewMode.Vertical);

        selectionScrollView.style.flexGrow = 1f;

        selectionScrollView.style.flexBasis = 0f;

        selectionScrollView.style.maxHeight = StyleKeyword.None;

        selectionScrollView.style.minHeight = 0f;

        selectionScrollView.style.width = Length.Percent(100f);

        selectionScrollView.style.maxWidth = StyleKeyword.None;

        selectionScrollView.style.marginTop = 0f;

        selectionScrollView.style.marginBottom = 0f;

        selectionScrollView.verticalScrollerVisibility = ScrollerVisibility.Hidden;

        selectionScrollView.horizontalScrollerVisibility = ScrollerVisibility.Hidden;

        selectionScrollView.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        selectionScrollView.pickingMode = PickingMode.Position;

        ConfigureRuntimeScrollView(selectionScrollView);

        selectionScrollView.contentContainer.style.paddingLeft = 0f;

        selectionScrollView.contentContainer.style.paddingRight = 0f;

        AttachWheelScrolling(selectionRailPanel, selectionScrollView);

        AttachWheelScrolling(selectionListCard, selectionScrollView);

        AttachWheelScrolling(selectionScrollView, selectionScrollView);

        AttachWheelScrolling(selectionScrollView.contentViewport, selectionScrollView);

        AttachWheelScrolling(selectionScrollView.contentContainer, selectionScrollView);

        selectionListCard.style.overflow = Overflow.Visible;

        selectionListCard.Add(selectionSongsListTitle);

        selectionListCard.Add(selectionSubtitleLabel);

        selectionListCard.Add(selectionScrollView);

        selectionRailPanel.Add(selectionRailBackdrop);

        selectionRailPanel.Add(selectionSplitDivider);

        selectionRailPanel.Add(selectionListCard);



        selectionFooter = new VisualElement();

        selectionFooter.style.flexDirection = FlexDirection.Row;

        selectionFooter.style.position = Position.Absolute;

        selectionFooter.style.justifyContent = Justify.FlexStart;

        selectionFooter.style.alignItems = Align.Center;

        selectionFooter.style.left = 12f;

        selectionFooter.style.bottom = 12f;

        selectionFooter.style.marginTop = 0f;

        selectionFooter.style.alignSelf = Align.FlexStart;

        selectionFooter.style.marginLeft = 0f;

        selectionFooter.style.marginBottom = 0f;

        selectionFooter.style.paddingLeft = 26f;

        selectionFooter.style.paddingRight = 26f;

        selectionFooter.style.paddingTop = 16f;

        selectionFooter.style.paddingBottom = 16f;

        selectionFooter.style.backgroundColor = LibraryPanelColor;

        selectionFooter.style.borderTopWidth = 0f;

        selectionFooter.style.borderRightWidth = 0f;

        selectionFooter.style.borderBottomWidth = 0f;

        selectionFooter.style.borderLeftWidth = 0f;

        selectionFooter.style.borderTopColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        selectionFooter.style.borderRightColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        selectionFooter.style.borderBottomColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        selectionFooter.style.borderLeftColor = new Color(0.03f, 0.03f, 0.04f, 1f);

        selectionFooter.style.borderTopLeftRadius = 18f;

        selectionFooter.style.borderTopRightRadius = 18f;

        selectionFooter.style.borderBottomLeftRadius = 18f;

        selectionFooter.style.borderBottomRightRadius = 18f;



        VisualElement selectionUtilityButtons = new VisualElement();

        selectionUtilityButtons.style.flexDirection = FlexDirection.Row;

        selectionUtilityButtons.style.alignItems = Align.Center;



        selectionSongsFolderButton = CreateLibraryFooterButton("Songs Folder", new Color(0.149f, 0.169f, 0.20f, 1f), () => owner?.OpenSongsFolderFromUi());

        selectionRefreshButton = CreateLibraryFooterButton("Refresh", new Color(0.149f, 0.169f, 0.20f, 1f), () => owner?.RefreshSongsFromUi());

        selectionSongsFolderButton.style.marginRight = 14f;

        selectionUtilityButtons.Add(selectionSongsFolderButton);

        selectionUtilityButtons.Add(selectionRefreshButton);



        VisualElement selectionPrimaryButtons = new VisualElement();

        selectionPrimaryButtons.style.flexDirection = FlexDirection.Row;

        selectionPrimaryButtons.style.alignItems = Align.Center;

        selectionPrimaryButtons.style.marginLeft = 14f;



        selectionBackButton = CreateLibraryFooterButton("Back", new Color(0.149f, 0.169f, 0.20f, 1f), () => owner?.CloseSongSelectionFromUi());

        selectionStartButton = CreateLibraryFooterButton("Start", LibraryConfirmedSongColor, () => owner?.StartSelectedSongFromUi());

        selectionBackButton.style.marginRight = 14f;

        selectionPrimaryButtons.Add(selectionBackButton);

        selectionPrimaryButtons.Add(selectionStartButton);



        selectionFooter.Add(selectionUtilityButtons);

        selectionFooter.Add(selectionPrimaryButtons);



        selectionOverlay.Add(selectionHeader);

        selectionOverlay.Add(selectionLeftBackdrop);

        selectionShell.Add(selectionRailPanel);

        selectionShell.Add(selectionInfoColumn);

        selectionOverlay.Add(selectionShell);

        selectionOverlay.Add(selectionFooter);

        selectionHeader.BringToFront();



        libraryBackdropBlur = rootObject.AddComponent<LibraryBackdropBlurController>();

        libraryBackdropBlur.TargetElement = selectionLeftBackdrop;

        libraryBackdropBlur.SourceCamera = Camera.main;

        settingsBackdropBlur = rootObject.AddComponent<LibraryBackdropBlurController>();

        settingsBackdropBlur.TargetElement = settingsBlurBackdrop;

        settingsBackdropBlur.SourceCamera = Camera.main;

        songEndBackdropBlur = rootObject.AddComponent<LibraryBackdropBlurController>();

        songEndBackdropBlur.SourceCamera = Camera.main;

        songEndBackdropBlur.UpdateContinuously = false;

        songEndBackdropBlur.Brightness = 0.34f;



        trackSelectionOverlay = CreateFullscreenOverlay();

        trackSelectionOverlay.style.alignItems = Align.Stretch;

        Label trackSelectionTopTag = CreateLabel("CHOOSE YOUR ARRANGEMENT", 28f, new Color(0.58f, 0.86f, 1f, 0.98f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        trackSelectionTopTag.style.marginBottom = 6f;

        trackSelectionTopTag.style.letterSpacing = 1f;



        trackSelectionTitleLabel = CreateLabel("TRACKS", 88f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);

        trackSelectionTitleLabel.style.letterSpacing = 1.2f;

        trackSelectionSubtitleLabel = CreateLabel("", 30f, new Color(0.86f, 0.95f, 1f, 0.98f), false, TextAnchor.MiddleCenter);

        trackSelectionSubtitleLabel.style.marginBottom = 16f;



        trackSelectionShell = new VisualElement();

        trackSelectionShell.style.width = Length.Percent(100f);

        trackSelectionShell.style.maxWidth = 1700f;

        trackSelectionShell.style.flexDirection = FlexDirection.Row;

        trackSelectionShell.style.alignItems = Align.Stretch;

        trackSelectionShell.style.justifyContent = Justify.Center;



        trackSelectionInfoCard = new VisualElement();

        trackSelectionInfoCard.style.flexBasis = 0f;

        trackSelectionInfoCard.style.flexGrow = 0.9f;

        trackSelectionInfoCard.style.minWidth = 320f;

        trackSelectionInfoCard.style.maxWidth = 430f;

        trackSelectionInfoCard.style.marginRight = 24f;

        trackSelectionInfoCard.style.paddingLeft = 28f;

        trackSelectionInfoCard.style.paddingRight = 28f;

        trackSelectionInfoCard.style.paddingTop = 28f;

        trackSelectionInfoCard.style.paddingBottom = 28f;

        StyleCard(trackSelectionInfoCard, new Color(0.04f, 0.11f, 0.20f, 0.90f), radius: 26f);

        trackSelectionInfoCard.style.borderTopColor = new Color(0.44f, 0.90f, 1f, 0.62f);

        trackSelectionInfoCard.style.borderRightColor = new Color(0.22f, 0.58f, 0.88f, 0.48f);

        trackSelectionInfoCard.style.borderBottomColor = new Color(0.10f, 0.24f, 0.38f, 0.84f);

        trackSelectionInfoCard.style.borderLeftColor = new Color(0.22f, 0.58f, 0.88f, 0.48f);



        Label trackSelectionInfoEyebrow = CreateLabel("ARRANGEMENT VIEW", 22f, new Color(0.69f, 0.90f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        trackSelectionInfoEyebrow.style.letterSpacing = 3f;

        trackSelectionInfoEyebrow.style.marginBottom = 20f;

        trackSelectionInfoTitleLabel = CreateLabel("Track", 52f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

        trackSelectionInfoTitleLabel.style.whiteSpace = WhiteSpace.Normal;

        trackSelectionInfoTitleLabel.style.marginBottom = 10f;

        trackSelectionInfoMetaLabel = CreateLabel("Pick the arrangement you want to load.", 27f, new Color(0.76f, 0.89f, 0.98f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        trackSelectionInfoMetaLabel.style.whiteSpace = WhiteSpace.Normal;

        trackSelectionInfoMetaLabel.style.marginBottom = 24f;



        VisualElement trackSelectionInfoScoreCard = new VisualElement();

        trackSelectionInfoScoreCard.style.paddingLeft = 20f;

        trackSelectionInfoScoreCard.style.paddingRight = 20f;

        trackSelectionInfoScoreCard.style.paddingTop = 18f;

        trackSelectionInfoScoreCard.style.paddingBottom = 18f;

        trackSelectionInfoScoreCard.style.marginBottom = 20f;

        trackSelectionInfoScoreCard.style.backgroundColor = new Color(0.06f, 0.17f, 0.27f, 0.96f);

        trackSelectionInfoScoreCard.style.borderTopLeftRadius = 18f;

        trackSelectionInfoScoreCard.style.borderTopRightRadius = 18f;

        trackSelectionInfoScoreCard.style.borderBottomLeftRadius = 18f;

        trackSelectionInfoScoreCard.style.borderBottomRightRadius = 18f;

        trackSelectionInfoScoreCard.style.borderTopWidth = 1f;

        trackSelectionInfoScoreCard.style.borderRightWidth = 1f;

        trackSelectionInfoScoreCard.style.borderBottomWidth = 1f;

        trackSelectionInfoScoreCard.style.borderLeftWidth = 1f;

        trackSelectionInfoScoreCard.style.borderTopColor = new Color(0.41f, 0.84f, 1f, 0.34f);

        trackSelectionInfoScoreCard.style.borderRightColor = new Color(0.41f, 0.84f, 1f, 0.22f);

        trackSelectionInfoScoreCard.style.borderBottomColor = new Color(0.41f, 0.84f, 1f, 0.15f);

        trackSelectionInfoScoreCard.style.borderLeftColor = new Color(0.41f, 0.84f, 1f, 0.22f);



        Label trackSelectionInfoScoreTitle = CreateLabel("BEST SCORE", 18f, new Color(0.67f, 0.88f, 1f, 0.92f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        trackSelectionInfoScoreTitle.style.letterSpacing = 2.2f;

        trackSelectionInfoScoreTitle.style.marginBottom = 8f;

        trackSelectionInfoScoreLabel = CreateLabel("--", 42f, new Color(0.64f, 0.94f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: true);

        trackSelectionInfoScoreCard.Add(trackSelectionInfoScoreTitle);

        trackSelectionInfoScoreCard.Add(trackSelectionInfoScoreLabel);



        trackSelectionInfoHintLabel = CreateLabel("Focus shifts the whole row. Confirm the highlighted arrangement to launch straight into play.", 24f, new Color(0.74f, 0.86f, 0.96f, 0.94f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        trackSelectionInfoHintLabel.style.whiteSpace = WhiteSpace.Normal;



        trackSelectionInfoCard.Add(trackSelectionInfoEyebrow);

        trackSelectionInfoCard.Add(trackSelectionInfoTitleLabel);

        trackSelectionInfoCard.Add(trackSelectionInfoMetaLabel);

        trackSelectionInfoCard.Add(trackSelectionInfoScoreCard);

        trackSelectionInfoCard.Add(trackSelectionInfoHintLabel);



        VisualElement trackSelectionListCard = new VisualElement();

        trackSelectionListCard.style.flexBasis = 0f;

        trackSelectionListCard.style.flexGrow = 1.5f;

        trackSelectionListCard.style.minWidth = 520f;

        trackSelectionListCard.style.paddingLeft = 26f;

        trackSelectionListCard.style.paddingRight = 26f;

        trackSelectionListCard.style.paddingTop = 22f;

        trackSelectionListCard.style.paddingBottom = 22f;

        StyleCard(trackSelectionListCard, new Color(0.03f, 0.10f, 0.18f, 0.98f), radius: 24f);



        trackSelectionScrollView = new ScrollView(ScrollViewMode.Vertical);

        trackSelectionScrollView.style.flexGrow = 1f;

        trackSelectionScrollView.style.maxHeight = 720f;

        trackSelectionScrollView.style.minHeight = 420f;

        trackSelectionScrollView.verticalScrollerVisibility = ScrollerVisibility.Auto;

        ConfigureRuntimeScrollView(trackSelectionScrollView);

        trackSelectionListCard.style.overflow = Overflow.Hidden;

        trackSelectionListCard.Add(trackSelectionScrollView);



        Button trackSelectionBackButton = CreateActionButton("Back", () => owner?.BackToSongSelectionFromUi());

        Button trackSelectionResumeButton = CreateActionButton("Resume", () => owner?.ResumePlaybackFromUi());

        AddBottomRightPrimaryButtons(trackSelectionListCard, trackSelectionBackButton, trackSelectionResumeButton);



        trackSelectionShell.Add(trackSelectionInfoCard);

        trackSelectionShell.Add(trackSelectionListCard);

        trackSelectionOverlay.Add(trackSelectionTopTag);

        trackSelectionOverlay.Add(trackSelectionTitleLabel);

        trackSelectionOverlay.Add(trackSelectionSubtitleLabel);

        trackSelectionOverlay.Add(trackSelectionShell);



        ApplyFont(root, bodyFontDefinition);





        songEndOverlay = CreateFullscreenOverlay();

        songEndOverlay.style.display = DisplayStyle.None;

        songEndOverlay.style.justifyContent = Justify.FlexStart;

        songEndOverlay.style.paddingTop = 0f;

        songEndOverlay.style.paddingBottom = 0f;

        songEndOverlay.style.backgroundColor = new Color(0.010f, 0.016f, 0.028f, 0.58f);

        songEndOverlay.style.overflow = Overflow.Hidden;



        songEndBlurBackdrop = new VisualElement();

        songEndBlurBackdrop.style.position = Position.Absolute;

        songEndBlurBackdrop.style.left = 0f;

        songEndBlurBackdrop.style.right = 0f;

        songEndBlurBackdrop.style.top = 0f;

        songEndBlurBackdrop.style.bottom = 0f;

        songEndBlurBackdrop.style.backgroundColor = new Color(0.010f, 0.016f, 0.028f, 0.40f);

        songEndBlurBackdrop.pickingMode = PickingMode.Ignore;

        if (songEndBackdropBlur != null)

        {

            songEndBackdropBlur.TargetElement = songEndBlurBackdrop;

            songEndBackdropBlur.SourceCamera = Camera.main;

        }



        songEndCard = new VisualElement();

        songEndCard.style.width = Length.Percent(94f);

        songEndCard.style.maxWidth = 1720f;

        songEndCard.style.paddingLeft = 64f;

        songEndCard.style.paddingRight = 64f;

        songEndCard.style.paddingTop = 42f;

        songEndCard.style.paddingBottom = 34f;

        songEndCard.style.flexDirection = FlexDirection.Column;

        songEndCard.style.justifyContent = Justify.SpaceBetween;

        StyleCard(songEndCard, new Color(0.04f, 0.07f, 0.14f, 0.985f), radius: 22f);

        songEndCard.style.borderTopWidth = 3f;

        songEndCard.style.borderRightWidth = 3f;

        songEndCard.style.borderBottomWidth = 3f;

        songEndCard.style.borderLeftWidth = 3f;

        Color endCardBorder = new Color(0.83f, 0.89f, 0.99f, 0.90f);

        songEndCard.style.borderTopColor = endCardBorder;

        songEndCard.style.borderRightColor = endCardBorder;

        songEndCard.style.borderBottomColor = endCardBorder;

        songEndCard.style.borderLeftColor = endCardBorder;

        songEndCard.style.alignItems = Align.Center;



        VisualElement songEndMain = new VisualElement();

        songEndMain.style.flexDirection = FlexDirection.Column;

        songEndMain.style.alignItems = Align.Center;

        songEndMain.style.width = Length.Percent(100f);

        songEndMain.style.flexGrow = 1f;

        songEndMain.style.justifyContent = Justify.Center;



        songEndTitleLabel = CreateLabel("SONG COMPLETE", 106f, new Color(0.94f, 0.97f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        songEndTitleLabel.style.marginBottom = 20f;



        songEndSongLabel = CreateLabel("Song", 72f, Color.white, true, TextAnchor.MiddleCenter, useTitleFont: true);

        songEndSongLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndSongLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndSongLabel.style.maxWidth = Length.Percent(100f);

        songEndSongLabel.style.marginBottom = 14f;



        VisualElement songEndMetaRow = new VisualElement();

        songEndMetaRow.style.flexDirection = FlexDirection.Row;

        songEndMetaRow.style.alignItems = Align.Center;

        songEndMetaRow.style.justifyContent = Justify.Center;

        songEndMetaRow.style.marginBottom = 16f;



        songEndMetaLabel = CreateLabel("Track: Lead  \u2022  Speed", 40f, new Color(0.83f, 0.90f, 1f, 1f), true, TextAnchor.MiddleCenter);

        songEndMetaLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndMetaLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        songEndSpeedValueLabel = CreateLabel("100%", 40f, new Color(1f, 0.86f, 0.45f, 1f), true, TextAnchor.MiddleCenter);

        songEndSpeedValueLabel.style.marginLeft = 10f;



        songEndMetaRow.Add(songEndMetaLabel);

        songEndMetaRow.Add(songEndSpeedValueLabel);



        songEndScoreLabel = CreateLabel("RUN SCORE 100.0%", 62f, new Color(1f, 0.94f, 0.76f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        songEndScoreLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndScoreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndScoreLabel.style.maxWidth = Length.Percent(100f);

        songEndScoreLabel.style.marginBottom = 6f;



        VisualElement songEndBestPanel = new VisualElement();

        songEndBestPanel.style.flexDirection = FlexDirection.Column;

        songEndBestPanel.style.alignItems = Align.Center;

        songEndBestPanel.style.justifyContent = Justify.Center;

        songEndBestPanel.style.width = Length.Percent(72f);

        songEndBestPanel.style.maxWidth = 1020f;

        songEndBestPanel.style.paddingTop = 16f;

        songEndBestPanel.style.paddingBottom = 14f;

        songEndBestPanel.style.paddingLeft = 20f;

        songEndBestPanel.style.paddingRight = 20f;

        songEndBestPanel.style.marginBottom = 12f;

        StyleCard(songEndBestPanel, new Color(0.06f, 0.13f, 0.22f, 0.96f), radius: 16f);

        songEndBestPanel.style.borderTopWidth = 2f;

        songEndBestPanel.style.borderRightWidth = 2f;

        songEndBestPanel.style.borderBottomWidth = 2f;

        songEndBestPanel.style.borderLeftWidth = 2f;

        Color bestPanelBorder = new Color(0.52f, 0.84f, 1f, 0.92f);

        songEndBestPanel.style.borderTopColor = bestPanelBorder;

        songEndBestPanel.style.borderRightColor = bestPanelBorder;

        songEndBestPanel.style.borderBottomColor = bestPanelBorder;

        songEndBestPanel.style.borderLeftColor = bestPanelBorder;



        songEndBestLabel = CreateLabel("TRACK BEST 100.0%", 44f, new Color(0.72f, 0.94f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        songEndBestLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndBestLabel.style.whiteSpace = WhiteSpace.Normal;



        songEndDeltaLabel = CreateLabel("New record +0.0%", 30f, new Color(0.66f, 0.95f, 0.76f, 0.98f), true, TextAnchor.MiddleCenter);

        songEndDeltaLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndDeltaLabel.style.marginTop = 4f;



        songEndBestPanel.Add(songEndBestLabel);

        songEndBestPanel.Add(songEndDeltaLabel);



        songEndRatingLabel = CreateLabel("Perfect!", 54f, new Color(0.62f, 0.90f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        songEndRatingLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndRatingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndRatingLabel.style.maxWidth = Length.Percent(100f);

        songEndRatingLabel.style.marginBottom = 6f;



        songEndStatsLabel = CreateLabel("Hits 0  \u2022  Misses 0", 34f, new Color(0.83f, 0.90f, 1f, 0.95f), true, TextAnchor.MiddleCenter);

        songEndStatsLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndStatsLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndStatsLabel.style.marginBottom = 10f;



        songEndMain.Add(songEndTitleLabel);

        songEndMain.Add(songEndSongLabel);

        songEndMain.Add(songEndMetaRow);

        songEndMain.Add(songEndScoreLabel);

        songEndMain.Add(songEndRatingLabel);

        songEndMain.Add(songEndStatsLabel);

        songEndMain.Add(songEndBestPanel);



        VisualElement songEndButtons = new VisualElement();

        songEndButtons.style.flexDirection = FlexDirection.Row;

        songEndButtons.style.width = Length.Percent(100f);

        songEndButtons.style.justifyContent = Justify.Center;

        songEndButtons.style.marginTop = 28f;



        songEndRetryButton = CreateActionButton("Retry", () => owner?.RetrySongFromUi());

        songEndSelectionButton = CreateActionButton("Song Selection", () => owner?.OpenSongSelectionFromSongEndFromUi());

        songEndMainMenuButton = CreateActionButton("Main Menu", () => owner?.OpenMainMenuFromSongEndFromUi());

        songEndRetryButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverSongEndActionSelectionFromUi(0));

        songEndSelectionButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverSongEndActionSelectionFromUi(1));

        songEndMainMenuButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverSongEndActionSelectionFromUi(2));

        songEndRetryButton.style.marginRight = 14f;

        songEndSelectionButton.style.marginLeft = 14f;

        songEndSelectionButton.style.marginRight = 14f;

        songEndMainMenuButton.style.marginLeft = 14f;

        songEndButtons.Add(songEndRetryButton);

        songEndButtons.Add(songEndSelectionButton);

        songEndButtons.Add(songEndMainMenuButton);



        songEndOverlay.style.justifyContent = Justify.FlexStart;

        songEndOverlay.style.paddingTop = 0f;

        songEndOverlay.style.paddingBottom = 0f;



        songEndCard.style.position = Position.Relative;

        songEndCard.style.width = Length.Percent(100f);

        songEndCard.style.height = Length.Percent(100f);

        songEndCard.style.maxWidth = 100000f;

        songEndCard.style.paddingLeft = 0f;

        songEndCard.style.paddingRight = 0f;

        songEndCard.style.paddingTop = 0f;

        songEndCard.style.paddingBottom = 0f;

        songEndCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        songEndCard.style.borderTopWidth = 0f;

        songEndCard.style.borderRightWidth = 0f;

        songEndCard.style.borderBottomWidth = 0f;

        songEndCard.style.borderLeftWidth = 0f;

        songEndCard.style.overflow = Overflow.Hidden;



        songEndMain.Clear();

        songEndButtons.Clear();

        songEndCard.Clear();

        TemplateContainer songEndTemplate = UiTemplateLoader.CloneRequired("UI/SongEndScreen", "UI/SongEndScreen");

        songEndTemplate.style.width = Length.Percent(100f);

        songEndTemplate.style.height = Length.Percent(100f);

        VisualElement songEndTemplateRoot = songEndTemplate.QRequired<VisualElement>("song-end-card");

        songEndTemplateRoot.style.width = Length.Percent(100f);

        songEndTemplateRoot.style.height = Length.Percent(100f);



        VisualElement songEndAccentPlate = songEndTemplate.QRequired<VisualElement>("song-end-accent-plate");

        VisualElement songEndMainPlate = songEndTemplate.QRequired<VisualElement>("song-end-main-plate");

        VisualElement songEndPanelGrid = songEndTemplate.QRequired<VisualElement>("song-end-panel-grid");

        VisualElement songEndTitleColumn = songEndTemplate.QRequired<VisualElement>("song-end-title-column");

        VisualElement songEndScoreColumn = songEndTemplate.QRequired<VisualElement>("song-end-score-column");

        VisualElement songEndStatsStrip = songEndTemplate.QRequired<VisualElement>("song-end-stats-strip");

        VisualElement songEndGradeGlow = songEndTemplate.QRequired<VisualElement>("song-end-grade-glow");

        VisualElement songEndGradeBlock = songEndTemplate.QRequired<VisualElement>("song-end-grade-block");

        VisualElement songEndFooterRow = songEndTemplate.QRequired<VisualElement>("song-end-footer-row");

        VisualElement songEndTemplateButtons = songEndTemplate.QRequired<VisualElement>("song-end-buttons");

        Label songEndEyebrow = songEndTemplate.QRequired<Label>("song-end-eyebrow");

        Label songEndScoreEyebrow = songEndTemplate.QRequired<Label>("song-end-score-eyebrow");

        Label songEndGradeCaption = songEndTemplate.QRequired<Label>("song-end-grade-caption");

        VisualElement songEndRule = songEndTemplate.QRequired<VisualElement>("song-end-rule");

        songEndNoteLabel = songEndTemplate.QRequired<Label>("song-end-note");



        songEndAccentPlate.style.backgroundImage = StyleKeyword.None;
        songEndAccentPlate.style.backgroundColor = new Color(GameplayHudAccentWarmColor.r, GameplayHudAccentWarmColor.g, GameplayHudAccentWarmColor.b, 0.10f);
        songEndAccentPlate.style.opacity = 1f;

        songEndPanelGrid.style.backgroundImage = StyleKeyword.None;
        songEndPanelGrid.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndPanelGrid.style.display = DisplayStyle.None;

        songEndGradeGlow.style.backgroundImage = StyleKeyword.None;
        songEndGradeGlow.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndGradeGlow.style.display = DisplayStyle.None;

        songEndMainPlate.style.backgroundColor = new Color(0.018f, 0.026f, 0.043f, 1f);

        songEndMainPlate.style.borderTopWidth = 1f;

        songEndMainPlate.style.borderRightWidth = 1f;

        songEndMainPlate.style.borderBottomWidth = 1f;

        songEndMainPlate.style.borderLeftWidth = 1f;

        songEndMainPlate.style.borderTopColor = GameplayHudCardBorderTopColor;

        songEndMainPlate.style.borderRightColor = GameplayHudCardBorderSideColor;

        songEndMainPlate.style.borderBottomColor = GameplayHudCardBorderBottomColor;

        songEndMainPlate.style.borderLeftColor = GameplayHudCardBorderSideColor;



        songEndEyebrow.text = "RUN COMPLETE";

        songEndEyebrow.style.fontSize = 24f;

        songEndEyebrow.style.color = GameplayHudAccentCoolColor;

        songEndEyebrow.style.unityFontDefinition = modernUiFontDefinition;

        songEndEyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndEyebrow.style.letterSpacing = 1.6f;

        songEndTitleLabel.text = "SONG COMPLETE";

        songEndScoreEyebrow.text = "TRACK BEST";

        songEndGradeCaption.text = "GRADE";



        songEndTitleColumn.Clear();

        songEndScoreColumn.Clear();

        songEndStatsStrip.Clear();

        songEndGradeBlock.Clear();

        songEndFooterRow.Clear();

        songEndTemplateButtons.Clear();

        songEndStatsStrip.style.width = Length.Percent(78f);
        songEndStatsStrip.style.alignSelf = Align.Center;
        songEndStatsStrip.style.justifyContent = Justify.SpaceBetween;
        songEndStatsStrip.style.alignItems = Align.Center;



        songEndTitleLabel.style.color = GameplayHudPrimaryTextColor;

        songEndTitleLabel.style.whiteSpace = WhiteSpace.Normal;

        songEndTitleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndTitleLabel.style.fontSize = 74f;

        songEndTitleLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndTitleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndTitleLabel.style.marginLeft = 0f;

        songEndTitleLabel.style.marginTop = 0f;

        songEndTitleLabel.style.marginRight = 0f;

        songEndTitleLabel.style.marginBottom = 8f;



        songEndSongLabel.style.color = GameplayHudPrimaryTextColor;

        songEndSongLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndSongLabel.style.fontSize = 50f;

        songEndSongLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndSongLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndSongLabel.style.marginBottom = 12f;



        songEndMetaLabel.style.color = GameplayHudSecondaryTextColor;

        songEndMetaLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndMetaLabel.style.fontSize = 28f;

        songEndMetaLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndMetaLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndMetaLabel.style.marginBottom = 0f;



        songEndStatsLabel.style.color = GameplayHudMutedTextColor;

        songEndStatsLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        songEndStatsLabel.style.fontSize = 30f;

        songEndStatsLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndStatsLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndStatsLabel.style.marginBottom = 0f;
        songEndStatsLabel.style.marginRight = 0f;
        songEndStatsLabel.style.paddingLeft = 0f;
        songEndStatsLabel.style.paddingRight = 0f;
        songEndStatsLabel.style.paddingTop = 0f;
        songEndStatsLabel.style.paddingBottom = 0f;
        songEndStatsLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndStatsLabel.style.borderTopWidth = 0f;
        songEndStatsLabel.style.borderRightWidth = 0f;
        songEndStatsLabel.style.borderBottomWidth = 0f;
        songEndStatsLabel.style.borderLeftWidth = 0f;
        songEndStatsLabel.style.letterSpacing = 0.2f;
        songEndStatsLabel.style.width = Length.Percent(30f);



        songEndScoreEyebrow.style.fontSize = 22f;

        songEndScoreEyebrow.style.color = GameplayHudEyebrowColor;

        songEndScoreEyebrow.style.unityFontDefinition = modernUiFontDefinition;

        songEndScoreEyebrow.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndScoreEyebrow.style.letterSpacing = 1.3f;



        songEndBestLabel.style.color = GameplayHudPrimaryTextColor;

        songEndBestLabel.style.unityTextAlign = TextAnchor.MiddleRight;

        songEndBestLabel.style.fontSize = 58f;

        songEndBestLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndBestLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndBestLabel.style.marginBottom = 8f;



        songEndSpeedValueLabel.style.color = GameplayHudAccentWarmColor;

        songEndSpeedValueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndSpeedValueLabel.style.fontSize = 30f;

        songEndSpeedValueLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndSpeedValueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndSpeedValueLabel.style.marginLeft = 0f;

        songEndSpeedValueLabel.style.marginBottom = 0f;
        songEndSpeedValueLabel.style.paddingLeft = 0f;
        songEndSpeedValueLabel.style.paddingRight = 0f;
        songEndSpeedValueLabel.style.paddingTop = 0f;
        songEndSpeedValueLabel.style.paddingBottom = 0f;
        songEndSpeedValueLabel.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndSpeedValueLabel.style.borderTopWidth = 0f;
        songEndSpeedValueLabel.style.borderRightWidth = 0f;
        songEndSpeedValueLabel.style.borderBottomWidth = 0f;
        songEndSpeedValueLabel.style.borderLeftWidth = 0f;
        songEndSpeedValueLabel.style.width = Length.Percent(20f);



        songEndDeltaLabel.style.unityTextAlign = TextAnchor.MiddleRight;

        songEndDeltaLabel.style.fontSize = 26f;

        songEndDeltaLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndDeltaLabel.style.unityFontStyleAndWeight = FontStyle.Bold;



        songEndGradeCaption.style.fontSize = 22f;

        songEndGradeCaption.style.color = GameplayHudMutedTextColor;

        songEndGradeCaption.style.unityFontDefinition = modernUiFontDefinition;

        songEndGradeCaption.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndGradeCaption.style.letterSpacing = 1.6f;



        songEndRatingLabel.style.color = GetScoreLetterGradeColor(100f);

        songEndRatingLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndRatingLabel.style.fontSize = 60f;

        songEndRatingLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndRatingLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndRatingLabel.style.marginBottom = -4f;



        songEndScoreLabel.style.color = GameplayHudAccentWarmColor;

        songEndScoreLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndScoreLabel.style.fontSize = 148f;

        songEndScoreLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndScoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        songEndScoreLabel.style.marginBottom = 14f;



        songEndRule.style.backgroundColor = new Color(GameplayHudAccentWarmColor.r, GameplayHudAccentWarmColor.g, GameplayHudAccentWarmColor.b, 0.34f);

        songEndNoteLabel.style.fontSize = 26f;
        songEndNoteLabel.style.width = 700f;

        songEndNoteLabel.style.color = GameplayHudMutedTextColor;

        songEndNoteLabel.style.unityFontDefinition = modernUiFontDefinition;

        songEndNoteLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        songEndTitleColumn.style.width = Length.Percent(100f);
        songEndTitleColumn.style.alignItems = Align.Center;
        songEndTitleColumn.style.justifyContent = Justify.Center;
        songEndTitleColumn.style.paddingLeft = 0f;
        songEndTitleColumn.style.paddingRight = 0f;
        songEndTitleColumn.style.paddingTop = 0f;
        songEndTitleColumn.style.paddingBottom = 0f;
        songEndTitleColumn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndTitleColumn.style.borderTopWidth = 0f;
        songEndTitleColumn.style.borderRightWidth = 0f;
        songEndTitleColumn.style.borderBottomWidth = 0f;
        songEndTitleColumn.style.borderLeftWidth = 0f;

        songEndScoreColumn.style.width = Length.Percent(30f);
        songEndScoreColumn.style.alignItems = Align.FlexEnd;
        songEndScoreColumn.style.justifyContent = Justify.Center;
        songEndScoreColumn.style.paddingLeft = 0f;
        songEndScoreColumn.style.paddingRight = 0f;
        songEndScoreColumn.style.paddingTop = 0f;
        songEndScoreColumn.style.paddingBottom = 0f;
        songEndScoreColumn.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndScoreColumn.style.borderTopWidth = 0f;
        songEndScoreColumn.style.borderRightWidth = 0f;
        songEndScoreColumn.style.borderBottomWidth = 0f;
        songEndScoreColumn.style.borderLeftWidth = 0f;

        songEndGradeBlock.style.width = Length.Percent(100f);
        songEndGradeBlock.style.minHeight = 0f;
        songEndGradeBlock.style.paddingLeft = 0f;
        songEndGradeBlock.style.paddingRight = 0f;
        songEndGradeBlock.style.paddingTop = 0f;
        songEndGradeBlock.style.paddingBottom = 0f;
        songEndGradeBlock.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        songEndGradeBlock.style.borderTopWidth = 0f;
        songEndGradeBlock.style.borderRightWidth = 0f;
        songEndGradeBlock.style.borderBottomWidth = 0f;
        songEndGradeBlock.style.borderLeftWidth = 0f;



        songEndTitleColumn.Add(songEndEyebrow);

        songEndTitleColumn.Add(songEndTitleLabel);

        songEndTitleColumn.Add(songEndSongLabel);

        songEndTitleColumn.Add(songEndMetaLabel);



        songEndScoreColumn.Add(songEndScoreEyebrow);

        songEndScoreColumn.Add(songEndBestLabel);

        songEndScoreColumn.Add(songEndDeltaLabel);

        songEndStatsStrip.Add(songEndStatsLabel);

        songEndStatsStrip.Add(songEndSpeedValueLabel);

        songEndStatsStrip.Add(songEndScoreColumn);



        songEndGradeBlock.Add(songEndGradeCaption);

        songEndGradeBlock.Add(songEndRatingLabel);

        songEndGradeBlock.Add(songEndScoreLabel);

        songEndGradeBlock.Add(songEndRule);

        songEndGradeBlock.Add(songEndNoteLabel);



        StyleSongEndActionButton(songEndRetryButton, GlobalPrimaryAccentColor, true);

        StyleSongEndActionButton(songEndSelectionButton, MenuOutlineNeutralColor, false);

        StyleSongEndActionButton(songEndMainMenuButton, MenuOutlineNeutralColor, false);

        songEndTemplateButtons.Add(songEndRetryButton);

        songEndTemplateButtons.Add(songEndSelectionButton);

        songEndTemplateButtons.Add(songEndMainMenuButton);

        songEndFooterRow.Add(songEndTemplateButtons);



        songEndCard.Add(songEndTemplate);



        startupTuningReminderPopup = new ModernMenuPopup(

            "INSTRUMENT CHECK",

            "Please make sure your strings are tuned.",

            "Tuner coming soon.",

            "If the song and notes feel mismatched, adjust the song/notes offset in Song Settings.",

            "If notes look too close or too far apart, adjust Tabs Sections duration settings.",

            "Continue",

            () => owner?.DismissStartupTuningReminderFromUi(),

            modernUiFontDefinition,

            titleFontDefinition);

        startupTuningReminderOverlay = startupTuningReminderPopup.Root;



        loopPausePopup = new ModernMenuPopup(

            "LOOP SETTINGS",

            "Loop Pause",

            "Set how long to wait before the loop restarts.",

            string.Empty,

            "Left/Right adjusts  \u2022  Enter saves  \u2022  Esc back",

            "Save & Return",

            () => owner?.ConfirmLoopPausePopupFromUi(),

            modernUiFontDefinition,

            titleFontDefinition);

        loopPausePopupOverlay = loopPausePopup.Root;

        loopPauseAcceptButton = loopPausePopup.PrimaryButton;

        loopPauseAcceptButton.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverLoopPausePopupSelectionFromUi(1));



        VisualElement loopPauseDurationRow = new VisualElement();

        loopPauseDurationRow.style.width = Length.Percent(100f);

        loopPauseDurationRow.style.maxWidth = 680f;

        loopPauseDurationRow.style.marginBottom = 0f;

        loopPauseDurationRow.style.alignSelf = Align.Center;



        loopPauseDurationLabel = CreateLabel("Loop Pause  0.00s", 34f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        loopPauseDurationLabel.style.unityFontDefinition = modernUiFontDefinition;

        loopPauseDurationLabel.style.marginBottom = 12f;



        loopPauseDurationSlider = new Slider(0f, 8f);

        loopPauseDurationSlider.focusable = false;

        loopPauseDurationSlider.style.marginTop = 0f;

        loopPauseDurationSlider.style.marginBottom = 0f;

        loopPauseDurationSlider.style.height = 68f;

        loopPauseDurationSlider.style.width = Length.Percent(100f);

        loopPauseDurationSlider.style.maxWidth = 620f;

        loopPauseDurationSlider.style.alignSelf = Align.Center;

        loopPauseDurationSlider.RegisterValueChangedCallback(evt => { if (!suppressCallbacks) owner?.SetLoopPauseDurationFromUi(evt.newValue); });

        loopPauseDurationRow.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverLoopPausePopupSelectionFromUi(0));

        loopPauseDurationLabel.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverLoopPausePopupSelectionFromUi(0));

        loopPauseDurationSlider.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverLoopPausePopupSelectionFromUi(0));



        Label loopPauseHelpLabel = CreateSongSettingsHelpLabel("Adds a short pause at loop start before playback resumes.");

        loopPauseHelpLabel.style.marginBottom = 2f;

        loopPauseDurationRow.Add(loopPauseDurationLabel);

        loopPauseDurationRow.Add(loopPauseDurationSlider);

        loopPausePopup.ContentHost.Add(loopPauseDurationRow);

        loopPausePopup.ContentHost.Add(loopPauseHelpLabel);



        loopPauseCountdownHost = new VisualElement();

        loopPauseCountdownHost.style.position = Position.Absolute;

        loopPauseCountdownHost.style.left = 0f;

        loopPauseCountdownHost.style.right = 0f;

        loopPauseCountdownHost.style.top = 20f;

        loopPauseCountdownHost.style.height = 72f;

        loopPauseCountdownHost.style.alignItems = Align.Center;

        loopPauseCountdownHost.style.justifyContent = Justify.Center;

        loopPauseCountdownHost.style.display = DisplayStyle.None;

        loopPauseCountdownHost.pickingMode = PickingMode.Ignore;



        loopPauseCountdownDial = new VisualElement();

        loopPauseCountdownDial.style.width = 52f;

        loopPauseCountdownDial.style.height = 52f;

        loopPauseCountdownDial.style.unityBackgroundScaleMode = ScaleMode.StretchToFill;

        loopPauseCountdownDial.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        loopPauseCountdownDial.style.opacity = 0.94f;

        loopPauseCountdownDial.pickingMode = PickingMode.Ignore;

        loopPauseCountdownHost.Add(loopPauseCountdownDial);



        songCard.Add(songCardTopRow);

        songCard.Add(songNameLabel);

        songCard.Add(trackChip);

        songCard.Add(statusRow);

        gameplayScoreDock.Add(songCardScoreBlock);

        root.Add(songCard);

        root.Add(characterHealthRow);

        root.Add(gameplayScoreDock);

        root.Add(techniqueLegendCard);

        root.Add(scorePlate);

        root.Add(judgePopupLayer);

        root.Add(loopPauseCountdownHost);

        root.Add(pauseOverlay);

        root.Add(gameModesOverlay);

        root.Add(notesDetectorTestOverlay);

        root.Add(heroModeSettingsOverlay);

        root.Add(loopSetupOverlay);

        root.Add(offsetHelperOverlay);

        root.Add(mainMenuOverlay);

        root.Add(settingsOverlay);
        root.Add(generatedAudioTracksPopupOverlay);

        root.Add(globalSettingsOverlay);

        root.Add(selectionOverlay);

        root.Add(trackSelectionOverlay);

        root.Add(startupTuningReminderOverlay);

        root.Add(loopPausePopupOverlay);

        songEndOverlay.Add(songEndBlurBackdrop);

        songEndOverlay.Add(songEndCard);

        root.Add(songEndOverlay);



        ApplyResponsiveSizing(force: true);

    }



    public void UpdateFromSnapshot(GuitarGameplaySnapshot snapshot)

    {

        ApplyResponsiveSizing(force: false);

        if (snapshot == null)

            return;



        string songName = string.IsNullOrWhiteSpace(snapshot.currentSongDisplayName)

            ? "No song loaded"

            : snapshot.currentSongDisplayName;



        if (string.IsNullOrWhiteSpace(songName) && snapshot.availableSongNames != null && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < snapshot.availableSongNames.Count)

            songName = snapshot.availableSongNames[snapshot.selectedSongIndex];



        string trackName = FormatTrackName(snapshot.selectedTrackDisplayName);

        songNameLabel.text = songName;

        trackNameLabel.text = trackName;

        int resolvedCount = 0;

        GameplayNoteState latestResolved = null;



        bool loopEnabled = snapshot.loopEnabled;

        string loopSignature = loopEnabled

            ? FormattableString.Invariant($"{snapshot.loopStartTime:F3}|{snapshot.loopEndTime:F3}|{snapshot.selectedLoopMarker}")

            : string.Empty;



        bool loopJustExited = wasLoopEnabled && !loopEnabled;

        bool loopJustEntered = !wasLoopEnabled && loopEnabled;

        bool loopDefinitionChanged = loopEnabled && wasLoopEnabled && loopSignature != lastLoopSignature;

        bool loopWrapped = loopEnabled && wasLoopEnabled && snapshot.songTime + 0.02f < lastSongTime;



        if (loopJustExited || loopJustEntered || loopDefinitionChanged || loopWrapped)

            ResetScoreCounters();



        if (snapshot.noteStates != null)

        { 

            for (int i = 0; i < snapshot.noteStates.Count; i++)

            {

                GameplayNoteState noteState = snapshot.noteStates[i];

                if (noteState == null)

                    continue;



                bool inLoopWindow = !loopEnabled || IsNoteInsideLoopWindow(noteState.data.time, snapshot.loopStartTime, snapshot.loopEndTime);

                if (!inLoopWindow)

                    continue;



                if (!noteState.IsResolved)

                    continue;



                resolvedCount++;

                if (latestResolved == null || noteState.resolvedAt > latestResolved.resolvedAt)

                    latestResolved = noteState;



                int noteKey = noteState.data.id >= 0 ? noteState.data.id : i;

                if (scoredNoteIds.Contains(noteKey))

                    continue;



                scoredNoteIds.Add(noteKey);

                if (noteState.IsHit)

                    scoreHits++;

                else if (noteState.IsMissed)

                    scoreMisses++;

            }

        }



        int denominator = snapshot.noteStates?.Count(state => state != null && (!loopEnabled || IsNoteInsideLoopWindow(state.data.time, snapshot.loopStartTime, snapshot.loopEndTime))) ?? 0;



        float scorePercent;
        int displayHits;
        int displayMisses;
        float loopDurationSeconds = Mathf.Max(0f, snapshot.loopEndTime - snapshot.loopStartTime);
        if (loopEnabled)
        {
            scorePercent = denominator > 0
                ? (100f * scoreHits / denominator)
                : 0f;
            displayHits = scoreHits;
            displayMisses = scoreMisses;
        }
        else
        {
            displayHits = Mathf.Max(0, snapshot.currentSessionScoreHits);
            displayMisses = Mathf.Max(0, snapshot.currentSessionScoreMisses);
            int liveResolvedCount = displayHits + displayMisses;
            scorePercent = snapshot.songEnded
                ? Mathf.Clamp(snapshot.currentSessionScorePercent, 0f, 100f)
                : (liveResolvedCount > 0 ? Mathf.Clamp(100f * displayHits / liveResolvedCount, 0f, 100f) : 0f);
        }

        string scoreTitle = snapshot.scoreSaveInvalidated ? "RUN SCORE (x)" : "RUN SCORE";
        scoreTitleLabel.text = loopEnabled && loopDurationSeconds > 0.01f
            ? $"{scoreTitle}  ?  LOOP: {loopDurationSeconds:F2}s"
            : scoreTitle;
        scorePercentLabel.text = $"{scorePercent:F1}%";
        noteTallyLabel.text = $"Hits {displayHits}  /  Misses {displayMisses}";
        UpdateCharacterHealthHearts(displayMisses, snapshot.heroModeHeartCount);

        wasLoopEnabled = loopEnabled;

        lastLoopSignature = loopSignature;

        lastSongTime = snapshot.songTime;



        if (!hasSeenSnapshot)

        {

            hasSeenSnapshot = true;

            lastResolvedCount = resolvedCount;

        }

        else if (resolvedCount > lastResolvedCount && latestResolved != null)

        {

            bool success = latestResolved.IsHit;

            if (success)

                hitStreak++;

            else

                hitStreak = 0;



            SpawnJudgePopup(success, hitStreak);

            lastResolvedCount = resolvedCount;

        }

        else if (resolvedCount < lastResolvedCount)

        {

            lastResolvedCount = resolvedCount;

            hitStreak = 0;

        }



        UpdateJudgePopups();



        float speedPercent = Mathf.Clamp(snapshot.playbackSpeedPercent, 1f, 200f);

        speedBadgeLabel.text = $"Speed {speedPercent:F0}%";

        speedValueLabel.text = $"Song Speed {speedPercent:F0}%";



        bool detectorConnected = snapshot.noteDetectorConnected;

        detectorStatusLabel.text = detectorConnected

            ? "Detector Connected"

            : "Detector Offline";

        detectorStatusLabel.style.color = detectorConnected

            ? GameplayHudDetectorOnlineColor

            : GameplayHudDetectorOfflineColor;



        float liveInputLevel = detectorConnected ? Mathf.Clamp01(snapshot.inputLevelNormalized) : 0f;

        displayedInputMeterLevel = Mathf.Lerp(displayedInputMeterLevel, liveInputLevel, 0.22f);

        float needleAngle = Mathf.Lerp(-65f, 65f, displayedInputMeterLevel);

        inputMeterNeedle.style.rotate = new Rotate(new Angle(needleAngle, AngleUnit.Degree));



        float songProgress = Mathf.Clamp01(snapshot.songProgressNormalized);

        float progressWidth = Mathf.Max(0f, inputMeterWrap.resolvedStyle.width > 1f ? inputMeterWrap.resolvedStyle.width : 220f);

        songProgressFill.style.width = Mathf.Lerp(songProgressFill.resolvedStyle.width, progressWidth * songProgress, 0.32f);



        suppressCallbacks = true;

        speedSlider.SetValueWithoutNotify(speedPercent);

        settingsOffsetSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.audioOffsetMs, -2000f, 2000f));

        settingsTabSpeedSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.tabSpeedOffsetPercent, 50f, 150f));

        settingsStartDelaySlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.songStartDelaySeconds, 0f, 8f));

        settingsVolumeSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.songVolumePercent, 0f, 100f));

        loopPauseDurationSlider.SetValueWithoutNotify(Mathf.Clamp(snapshot.loopPauseDurationSeconds, 0f, 8f));

        suppressCallbacks = false;



        settingsOffsetLabel.text = $"Audio Offset  {snapshot.audioOffsetMs:F0} ms";

        settingsTabSpeedLabel.text = $"Note Spacing  {snapshot.tabSpeedOffsetPercent:F0}%";

        settingsStartDelayLabel.text = $"Start Delay  {snapshot.songStartDelaySeconds:F2}s";

        settingsVolumeLabel.text = $"Song Volume  {snapshot.songVolumePercent:F0}%";

        loopPauseDurationLabel.text = $"Loop Pause  {snapshot.loopPauseDurationSeconds:F2}s";

        bool generatedSongMode = snapshot.songPlaybackUsesGeneratedMode;
        settingsPrimaryToolButton.text = generatedSongMode ? "Audio Track Selection" : "Offset Helper";
        settingsPrimaryToolHelpLabel.text = generatedSongMode
            ? "Choose which MusicXML parts should play in generated mode. You can solo a lead track or build your own backing mix."
            : "Choose a note to calibrate around, then preview a short loop and nudge offset until the note lines up with the audio.";
        settingsOffsetRow.style.display = generatedSongMode ? DisplayStyle.None : DisplayStyle.Flex;
        settingsOffsetHelpLabel.style.display = generatedSongMode ? DisplayStyle.None : DisplayStyle.Flex;
        settingsOffsetScopeRow.style.display = generatedSongMode ? DisplayStyle.None : DisplayStyle.Flex;
        settingsOffsetScopeHelpLabel.style.display = generatedSongMode ? DisplayStyle.None : DisplayStyle.Flex;

        if (settingsTrackButton != null)

            settingsTrackButton.text = $"Track: {FormatTrackName(snapshot.selectedTrackDisplayName)}";

        if (settingsOffsetScopeButton != null)

            settingsOffsetScopeButton.text = $"Offset Scope: {snapshot.offsetScopeLabel}";

        if (generatedSongMode)
            settingsPrimaryToolButton.text = $"Audio Tracks: {snapshot.generatedAudioTrackSelectionSummary}";



        bool showEnd = snapshot.songEnded;
        bool showToneLab = snapshot.showToneLab && !showEnd;
        bool showNotesDetectorTest = snapshot.showNotesDetectorTestMenu && !showEnd && !showToneLab;

        bool showMainMenu = snapshot.showMainMenu && !showEnd && !showToneLab && !showNotesDetectorTest;

        bool showLoopPausePopup = snapshot.showLoopPausePopup && !showEnd;

        bool showLoopSetup = snapshot.showLoopSettings && !showLoopPausePopup && !showEnd && !showToneLab;

        bool showGameModes = snapshot.showGameModes && !showEnd && !showToneLab;

        bool showHeroModeSettings = snapshot.showHeroModeSettings && !showEnd && !showToneLab;

        bool showOffsetHelper = snapshot.showOffsetHelper && !showEnd && !showToneLab;

        bool showPause = snapshot.isPaused && !showEnd && !showToneLab && !showNotesDetectorTest && !showLoopSetup && !showLoopPausePopup && !showGameModes && !showHeroModeSettings && !showOffsetHelper && !snapshot.showStartupTuningReminder && !snapshot.mainMenuFlowActive && !snapshot.showSongSettings && !snapshot.showSongSelection && !snapshot.showTrackSelection && !snapshot.showGlobalSettings;

        bool showSettings = snapshot.showSongSettings && !showEnd && !showToneLab;

        bool showSelection = snapshot.showSongSelection && !showEnd && !showToneLab;

        bool showTrackSelection = snapshot.showTrackSelection && !showEnd && !showToneLab;

        bool showGlobalSettings = snapshot.showGlobalSettings && !showEnd && !showToneLab;

        bool showStartupTuningReminder = snapshot.showStartupTuningReminder && !showEnd && !showToneLab && !showMainMenu && !showSelection && !showTrackSelection;

        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;

        bool showTechniqueLegend = !isHighway3D && !showEnd && !showToneLab && !showNotesDetectorTest && !showPause && !showLoopSetup && !showLoopPausePopup && !showGameModes && !showHeroModeSettings && !showOffsetHelper && !showMainMenu && !showSettings && !showSelection && !showTrackSelection && !showGlobalSettings && !showStartupTuningReminder && !snapshot.mainMenuFlowActive;



        if (showMainMenu)

        {

            mainMenuCurrentSongValueLabel.text = songName;

            mainMenuCurrentTrackValueLabel.text = $"Track: {trackName}";

            mainMenuCurrentSpeedValueLabel.text = $"{speedPercent:F0}%";

            mainMenuCurrentDetectorValueLabel.text = detectorConnected ? "Connected" : "Offline";

            mainMenuCurrentDetectorValueLabel.style.color = detectorConnected

                ? new Color(0.56f, 0.96f, 0.72f, 1f)

                : new Color(1f, 0.73f, 0.78f, 1f);

            UpdateMainMenuSelection(snapshot.selectedMainMenuIndex);

        }



        pauseOverlay.style.display = showPause ? DisplayStyle.Flex : DisplayStyle.None;

        gameModesOverlay.style.display = showGameModes ? DisplayStyle.Flex : DisplayStyle.None;

        notesDetectorTestOverlay.style.display = showNotesDetectorTest ? DisplayStyle.Flex : DisplayStyle.None;

        heroModeSettingsOverlay.style.display = showHeroModeSettings ? DisplayStyle.Flex : DisplayStyle.None;

        loopSetupOverlay.style.display = showLoopSetup ? DisplayStyle.Flex : DisplayStyle.None;

        offsetHelperOverlay.style.display = showOffsetHelper ? DisplayStyle.Flex : DisplayStyle.None;

        loopPausePopupOverlay.style.display = showLoopPausePopup ? DisplayStyle.Flex : DisplayStyle.None;

        mainMenuOverlay.style.display = showMainMenu ? DisplayStyle.Flex : DisplayStyle.None;

        settingsOverlay.style.display = showSettings ? DisplayStyle.Flex : DisplayStyle.None;
        generatedAudioTracksPopupOverlay.style.display = showSettings && snapshot.showGeneratedAudioTrackSelectionPopup ? DisplayStyle.Flex : DisplayStyle.None;

        selectionOverlay.style.display = showSelection ? DisplayStyle.Flex : DisplayStyle.None;

        trackSelectionOverlay.style.display = showTrackSelection ? DisplayStyle.Flex : DisplayStyle.None;

        globalSettingsOverlay.style.display = showGlobalSettings ? DisplayStyle.Flex : DisplayStyle.None;

        startupTuningReminderOverlay.style.display = showStartupTuningReminder ? DisplayStyle.Flex : DisplayStyle.None;

        songEndOverlay.style.display = showEnd ? DisplayStyle.Flex : DisplayStyle.None;

        techniqueLegendCard.style.display = showTechniqueLegend ? DisplayStyle.Flex : DisplayStyle.None;

        bool showLoopPauseCountdown = snapshot.loopRestartPauseRemainingSeconds > 0.0001f

            && snapshot.loopPauseDurationSeconds > 0.0001f

            && !showEnd

            && !showMainMenu

            && !showSettings

            && !showSelection

            && !showTrackSelection

            && !showGlobalSettings

            && !showStartupTuningReminder

            && !showLoopPausePopup;

        UpdateLoopPauseCountdownIndicator(snapshot.loopRestartPauseRemainingSeconds, snapshot.loopPauseDurationSeconds, showLoopPauseCountdown);



        bool hideGameplayHudCards = snapshot.mainMenuFlowActive || showSelection || showTrackSelection || showGlobalSettings || showEnd || showToneLab || showNotesDetectorTest || showGameModes || showHeroModeSettings || showOffsetHelper;
        highwayCharacterVisible = snapshot.showHighwayCharacter;

        songCard.style.display = hideGameplayHudCards ? DisplayStyle.None : DisplayStyle.Flex;

        characterHealthRow.style.display = hideGameplayHudCards || !snapshot.heroModeEnabled ? DisplayStyle.None : DisplayStyle.Flex;

        gameplayScoreDock.style.display = hideGameplayHudCards ? DisplayStyle.None : DisplayStyle.Flex;

        scorePlate.style.display = DisplayStyle.None;

        judgePopupLayer.style.display = hideGameplayHudCards || showLoopSetup || showLoopPausePopup || showOffsetHelper ? DisplayStyle.None : DisplayStyle.Flex;

        if (showPause || showSettings || showNotesDetectorTest || showGameModes || showHeroModeSettings)

        {

            songCard.BringToFront();

            characterHealthRow.BringToFront();

            gameplayScoreDock.BringToFront();

        }

        if (showNotesDetectorTest)
        {
            notesDetectorTestInfoLabel.text = "The native notes detector runs continuously in the background. Tweak the safe settings below, then save to Custom when you want to keep your calibration.";
            notesDetectorTestStatusLabel.text = snapshot.notesDetectorStatusText ?? "Status unavailable.";
            notesDetectorTestDetailLabel.text = snapshot.notesDetectorDetailText ?? string.Empty;
            notesDetectorTestRuntimeLabel.text = "Type: DLL native plugin  |  ONNX Runtime + native low-latency capture";
            if (notesDetectorTestFastNotesLabel != null)
                notesDetectorTestFastNotesLabel.text = $"Fast Path Notes: {(!string.IsNullOrWhiteSpace(snapshot.notesDetectorFastNotesText) ? snapshot.notesDetectorFastNotesText : "--")}";
            if (notesDetectorTestAiNotesLabel != null)
                notesDetectorTestAiNotesLabel.text = $"AI Result Notes: {(!string.IsNullOrWhiteSpace(snapshot.notesDetectorAiNotesText) ? snapshot.notesDetectorAiNotesText : "--")}";
            if (notesDetectorTestInputDeviceDropdown != null)
            {
                List<string> deviceChoices = snapshot.notesDetectorAvailableInputDevices != null && snapshot.notesDetectorAvailableInputDevices.Count > 0
                    ? snapshot.notesDetectorAvailableInputDevices
                    : new List<string> { "Automatic" };
                suppressNotesDetectorDropdownCallbacks = true;
                notesDetectorTestInputDeviceDropdown.choices = deviceChoices;
                int selectedDeviceUiIndex = Mathf.Clamp(snapshot.selectedNotesDetectorInputDeviceIndex, 0, deviceChoices.Count - 1);
                string selectedDeviceLabel = deviceChoices[selectedDeviceUiIndex];
                notesDetectorTestInputDeviceDropdown.SetValueWithoutNotify(selectedDeviceLabel);
                notesDetectorTestInputDeviceDropdown.SetEnabled(deviceChoices.Count > 0);
                suppressNotesDetectorDropdownCallbacks = false;
            }
            if (notesDetectorPresetDropdown != null)
            {
                List<string> presetChoices = snapshot.notesDetectorPresetLabels != null && snapshot.notesDetectorPresetLabels.Count > 0
                    ? snapshot.notesDetectorPresetLabels
                    : NativeDetectorSettingCatalog.BuildPresetLabels();
                suppressNotesDetectorDropdownCallbacks = true;
                notesDetectorPresetDropdown.choices = presetChoices;
                int selectedPresetIndex = Mathf.Clamp(snapshot.selectedNotesDetectorPresetIndex, 0, presetChoices.Count - 1);
                string selectedPresetLabel = presetChoices[selectedPresetIndex];
                notesDetectorPresetDropdown.SetValueWithoutNotify(selectedPresetLabel);
                suppressNotesDetectorDropdownCallbacks = false;
            }
            if (notesDetectorRefreshDevicesButton != null)
                notesDetectorRefreshDevicesButton.SetEnabled(true);
            if (notesDetectorSavePresetButton != null)
                notesDetectorSavePresetButton.text = string.Equals(snapshot.notesDetectorSelectedPresetLabel, "Custom", StringComparison.OrdinalIgnoreCase) ? "Save Custom" : "Save To Custom";
            RefreshNotesDetectorSettingsControls(snapshot.notesDetectorSettings);
            UpdateNotesDetectorTestSelection(snapshot.selectedNotesDetectorTestIndex);
            if (notesDetectorRoutineOverlay != null)
            {
                notesDetectorRoutineOverlay.style.display = snapshot.showNotesDetectorRoutinePopup ? DisplayStyle.Flex : DisplayStyle.None;
                if (snapshot.showNotesDetectorRoutinePopup)
                {
                    if (notesDetectorRoutineMessageLabel != null)
                        notesDetectorRoutineMessageLabel.text = snapshot.notesDetectorRoutineInstructionText ?? string.Empty;
                    if (notesDetectorRoutineCalloutLabel != null)
                        notesDetectorRoutineCalloutLabel.text = snapshot.notesDetectorRoutineTargetText ?? string.Empty;
                    if (notesDetectorRoutineHintLabel != null)
                        notesDetectorRoutineHintLabel.text = snapshot.notesDetectorRoutineProgressText ?? string.Empty;
                    if (notesDetectorRoutineStatusLabel != null)
                    {
                        notesDetectorRoutineStatusLabel.text = snapshot.notesDetectorRoutineStatusText ?? string.Empty;
                        notesDetectorRoutineStatusLabel.style.color = snapshot.notesDetectorRoutineCompleted || snapshot.notesDetectorRoutineStatusOk
                            ? new Color(0.54f, 0.94f, 0.67f, 1f)
                            : new Color(0.92f, 0.33f, 0.29f, 1f);
                    }
                    if (notesDetectorRoutineFastNotesLabel != null)
                        notesDetectorRoutineFastNotesLabel.text = $"Fast Path Notes: {(!string.IsNullOrWhiteSpace(snapshot.notesDetectorFastNotesText) ? snapshot.notesDetectorFastNotesText : "--")}";
                    if (notesDetectorRoutineAiNotesLabel != null)
                        notesDetectorRoutineAiNotesLabel.text = $"AI Result Notes: {(!string.IsNullOrWhiteSpace(snapshot.notesDetectorAiNotesText) ? snapshot.notesDetectorAiNotesText : "--")}";
                    for (int i = 0; i < notesDetectorRoutineTabRowLabels.Count; i++)
                    {
                        if (notesDetectorRoutineTabRowLabels[i] == null)
                            continue;

                        string rowText = snapshot.notesDetectorRoutineTabRows != null && i < snapshot.notesDetectorRoutineTabRows.Count
                            ? snapshot.notesDetectorRoutineTabRows[i]
                            : "--";
                        notesDetectorRoutineTabRowLabels[i].text = rowText;
                    }
                    if (notesDetectorRoutineRestartButton != null)
                        notesDetectorRoutineRestartButton.text = snapshot.notesDetectorRoutineCompleted ? "Run Again" : "Restart";
                }
            }
        }



        if (showEnd)

        {

            UpdateSongEndSelection(snapshot.selectedSongEndActionIndex);

            string rating = GetScoreLetterGrade(scorePercent);

            float savedTrackBest = Mathf.Clamp(snapshot.currentTrackBestScorePercent, 0f, 100f);
            float savedHeroTrackBest = Mathf.Clamp(snapshot.currentTrackHeroBestScorePercent, 0f, 100f);
            int savedHeroHeartsRemaining = Mathf.Max(0, snapshot.currentTrackHeroBestHeartsRemaining);
            int savedHeroHeartsTotal = Mathf.Max(0, snapshot.currentTrackHeroBestHeartsTotal);
            bool hasHeroBest = savedHeroTrackBest > 0.01f && savedHeroHeartsTotal > 0;
            float comparisonBest = snapshot.heroModeEnabled
                ? savedHeroTrackBest
                : savedTrackBest;

            float deltaToBest = scorePercent - comparisonBest;

            bool newRecord = deltaToBest >= -0.05f;
            string bestSummaryText = hasHeroBest
                ? FormattableString.Invariant($"Normal {savedTrackBest:F1}%\nHero {savedHeroTrackBest:F1}% ({savedHeroHeartsRemaining}/{savedHeroHeartsTotal})")
                : $"{savedTrackBest:F1}%";
            string deltaText = snapshot.heroModeEnabled
                ? (newRecord
                    ? FormattableString.Invariant($"New hero record saved ({snapshot.currentHeroHeartsRemaining}/{snapshot.heroModeHeartCount})")
                    : $"{Mathf.Abs(deltaToBest):F1}% to beat your hero best")
                : (newRecord
                    ? "New record saved"
                    : $"{Mathf.Abs(deltaToBest):F1}% to beat your best");
            string statsText = snapshot.heroModeEnabled
                ? FormattableString.Invariant($"HITS {scoreHits}   |   MISSES {scoreMisses}   |   HEARTS {snapshot.currentHeroHeartsRemaining}/{snapshot.heroModeHeartCount}")
                : FormattableString.Invariant($"HITS {scoreHits}   |   MISSES {scoreMisses}");
            string songEndTitleText = snapshot.songEndedAsGameOver ? "GAME OVER" : "SONG COMPLETE";

            string songEndSignature = FormattableString.Invariant(

                $"{songEndTitleText}|{songName}|{trackName}|{speedPercent:F0}|{scorePercent:F1}|{savedTrackBest:F1}|{savedHeroTrackBest:F1}|{savedHeroHeartsRemaining}|{savedHeroHeartsTotal}|{scoreHits}|{scoreMisses}|{snapshot.heroModeEnabled}|{snapshot.currentHeroHeartsRemaining}|{snapshot.heroModeHeartCount}|{snapshot.songEndedAsGameOver}|{newRecord}|{rating}");



            if (!string.Equals(songEndSignature, lastSongEndSignature, StringComparison.Ordinal))

            {

                lastSongEndSignature = songEndSignature;

                songEndTitleLabel.text = songEndTitleText;

                songEndSongLabel.text = songName;

                songEndMetaLabel.text = $"TRACK  {trackName}";

                songEndSpeedValueLabel.text = $"SPEED  {speedPercent:F0}%";

                songEndScoreLabel.text = $"{scorePercent:F1}%";

                songEndBestLabel.text = bestSummaryText;

                songEndDeltaLabel.text = deltaText;

                songEndRatingLabel.text = rating;

                songEndStatsLabel.text = statsText;

                songEndNoteLabel.text = GetSongEndPerformanceNote(scorePercent, newRecord);

                songEndSongLabel.style.color = GameplayHudPrimaryTextColor;

                songEndMetaLabel.style.color = GameplayHudSecondaryTextColor;

                songEndStatsLabel.style.color = GameplayHudMutedTextColor;

                songEndSpeedValueLabel.style.color = GameplayHudAccentWarmColor;

                songEndScoreLabel.style.color = GameplayHudAccentWarmColor;

                songEndBestLabel.style.color = GameplayHudPrimaryTextColor;

                songEndDeltaLabel.style.color = newRecord

                    ? GlobalPrimaryAccentColor

                    : GameplayHudAccentWarmColor;

                songEndRatingLabel.style.color = GetScoreLetterGradeColor(scorePercent);

            }

        }

        else

        {

            lastSongEndSignature = null;

        }



        if (showPause)

        {

            pauseInfoLabel.text =

                $"Marker: {snapshot.selectedLoopMarker}   " +

                $"Time: {snapshot.songTime:F2}s";

            if (snapshot.selectedPauseActionIndex == 0)
            {
                pauseHintLabel.text = "Up/Down selects  \u2022  Left/Right changes speed";
            }
            else if (snapshot.selectedPauseActionIndex == 3)
            {
                pauseHintLabel.text = "Up/Down selects  \u2022  Left/Right changes audio mode";
            }
            else
            {
                pauseHintLabel.text = "Up/Down selects  \u2022  Left/Right moves song time";
            }

            gameModesButton.text = "Game Modes";
            if (audioModeButton != null)
                audioModeButton.text = $"Audio: {snapshot.songPlaybackAudioModeLabel}";

            UpdatePauseActionSelection(snapshot.selectedPauseActionIndex);

        }



        if (showGameModes)

        {

            loopButton.text = snapshot.loopEnabled ? "Loop: ON" : "Loop: OFF";
            gameModesLoopConfigButton.style.display = snapshot.loopEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            noteByNoteButton.text = snapshot.noteByNoteModeEnabled
                ? (snapshot.noteByNoteWaitingForMatch ? "Note By Note: ON  (Waiting)" : "Note By Note: ON")
                : "Note By Note: OFF";
            heroModeButton.text = snapshot.heroModeEnabled ? "Hero Mode: ON" : "Hero Mode: OFF";
            heroModeSettingsButton.style.display = snapshot.heroModeEnabled ? DisplayStyle.Flex : DisplayStyle.None;
            UpdateGameModesSelection(snapshot.selectedGameModesIndex);

        }



        if (showHeroModeSettings)

        {

            heroModeSettingsMessageLabel.text = "Adjust starting health for hero runs.";
            heroModeSettingsCalloutLabel.text = "STARTING HEARTS";
            heroModeSettingsCalloutLabel.style.display = DisplayStyle.Flex;
            heroModeSettingsValueLabel.text = $"{Mathf.Clamp(snapshot.heroModeHeartCount, 1, CharacterHealthHeartCount)} hearts";
            heroModeSettingsHintLabel.text = "Left/Right adjusts  \u2022  Down selects Back  \u2022  Enter goes back";
            UpdateHeroModeSettingsSelection(snapshot.selectedHeroModeSettingsIndex);

        }



        if (showLoopSetup)

        {

            string selectedLoopTarget = snapshot.selectedLoopMarker switch

            {

                1 => "START",

                2 => "END",

                _ => "TIME"

            };

            float loopWindowSeconds = Mathf.Max(0f, snapshot.loopEndTime - snapshot.loopStartTime);

            loopSetupStatusLabel.text =

                $"EDITING {selectedLoopTarget}  \u2022  START {snapshot.loopStartTime:F2}s  \u2022  END {snapshot.loopEndTime:F2}s  \u2022  LOOP {loopWindowSeconds:F2}s";

            loopSetupHintLabel.text = "Space preview  \u2022  1 set start  \u2022  2 set end  \u2022  3 move time  \u2022  Esc continue";

        }



        if (showLoopPausePopup)

            UpdateLoopPausePopupSelection(snapshot.selectedLoopPausePopupIndex);



        if (showOffsetHelper)

        {
            offsetHelperOverlay.BringToFront();

            float helperLoopLength = Mathf.Max(0f, snapshot.offsetHelperPreviewEndTime - snapshot.offsetHelperPreviewStartTime);

            offsetHelperTitleLabel.text = snapshot.offsetHelperAdjusting

                ? $"OFFSET HELPER \u2022 {snapshot.offsetScopeLabel.ToUpperInvariant()} OFFSET \u2022 CALIBRATE"

                : $"OFFSET HELPER \u2022 {snapshot.offsetScopeLabel.ToUpperInvariant()} OFFSET \u2022 SELECT ANCHOR";

            offsetHelperStatusLabel.text = snapshot.offsetHelperAdjusting

                ? $"ANCHOR {snapshot.offsetHelperAnchorTime:F2}s \u2022 OFFSET {snapshot.audioOffsetMs:F0} ms \u2022 LOOP {helperLoopLength:F2}s"

                : $"ANCHOR {snapshot.offsetHelperAnchorTime:F2}s \u2022 CURRENT OFFSET {snapshot.audioOffsetMs:F0} ms";

            offsetHelperHintLabel.text = snapshot.offsetHelperAdjusting

                ? (snapshot.offsetHelperPreviewPlaying

                    ? "Space pause preview \u2022 Left/Right adjust by 10 ms\nEnter save offset \u2022 Esc choose another note"

                    : "Space play preview \u2022 Left/Right adjust by 10 ms\nEnter save offset \u2022 Esc choose another note")

                : "Left/Right or Up/Down select note\nEnter start calibration \u2022 Esc back to Song Settings";

        }


        if (showSettings)

        {
            if (snapshot.showGeneratedAudioTrackSelectionPopup)
            {
                settingsHintLabel.text = "Track picker open  \u2022  Enter toggles  \u2022  Esc closes";
            }
            else if (snapshot.songPlaybackUsesGeneratedMode)
            {
                settingsHintLabel.text = snapshot.selectedSongSettingsIndex switch
                {
                    0 => "Up/Down selects  \u2022  Enter opens track picker  \u2022  Esc returns",
                    2 or 3 or 4 or 5 => "Up/Down selects  \u2022  Left/Right adjusts  \u2022  Enter confirms",
                    _ => "Up/Down selects  \u2022  Enter confirms  \u2022  Esc returns"
                };
            }
            else
            {
                settingsHintLabel.text = snapshot.selectedSongSettingsIndex == 0
                    ? "Up/Down selects  \u2022  Enter opens helper  \u2022  Esc returns"
                    : (snapshot.selectedSongSettingsIndex >= 1 && snapshot.selectedSongSettingsIndex <= 6
                        ? "Up/Down selects  \u2022  Left/Right adjusts  \u2022  Enter confirms"
                        : "Up/Down selects  \u2022  Enter confirms  \u2022  Esc returns");
            }

            UpdateSongSettingsSelection(snapshot.selectedSongSettingsIndex);
            UpdateGeneratedAudioTrackPopup(snapshot);

        }



        if (showSelection)

            UpdateModernSongSelectionRows(snapshot);



        if (showTrackSelection)

            UpdateTrackSelectionRows(snapshot);



        if (showGlobalSettings)

            UpdateGlobalSettings(snapshot);

    }



    private static string GetScoreLetterGrade(float scorePercent)

    {

        if (scorePercent >= 99.5f) return "S+";

        if (scorePercent >= 98f) return "S";

        if (scorePercent >= 96f) return "S-";

        if (scorePercent >= 93f) return "A+";

        if (scorePercent >= 90f) return "A";

        if (scorePercent >= 87f) return "A-";

        if (scorePercent >= 83f) return "B+";

        if (scorePercent >= 79f) return "B";

        if (scorePercent >= 75f) return "B-";

        if (scorePercent >= 70f) return "C+";

        if (scorePercent >= 65f) return "C";

        if (scorePercent >= 60f) return "C-";

        if (scorePercent >= 55f) return "D+";

        if (scorePercent >= 50f) return "D";

        if (scorePercent >= 45f) return "D-";

        return "F";

    }



    private static Color GetScoreLetterGradeColor(float scorePercent)

    {

        if (scorePercent >= 96f)

            return GlobalPrimaryAccentColor;

        if (scorePercent >= 79f)

            return GlobalSecondaryAccentColor;

        if (scorePercent >= 60f)

            return new Color(0.96f, 0.96f, 0.92f, 1f);



        return new Color(0.98f, 0.63f, 0.48f, 1f);

    }



    private static string GetSongEndPerformanceNote(float scorePercent, bool newRecord)

    {

        if (newRecord)

            return "New track best saved. Push the next run harder.";

        if (scorePercent >= 95f)

            return "Excellent control. One cleaner pass could push this over the top.";

        if (scorePercent >= 80f)

            return "Strong run. Clean up the misses and the benchmark jumps fast.";

        if (scorePercent >= 60f)

            return "Solid progress. Tighten consistency and the score will rise quickly.";

        return "Tough pass. Rebuild the streak and steady the timing on the next run.";

    }



    private void UpdateLoopPauseCountdownIndicator(float remainingSeconds, float durationSeconds, bool visible)

    {

        if (!visible || loopPauseCountdownHost == null || loopPauseCountdownDial == null || durationSeconds <= 0.0001f)

        {

            if (loopPauseCountdownHost != null)

                loopPauseCountdownHost.style.display = DisplayStyle.None;

            lastLoopPauseCountdownProgress = -1f;

            return;

        }



        loopPauseCountdownHost.style.display = DisplayStyle.Flex;

        loopPauseCountdownHost.BringToFront();

        float progress = Mathf.Clamp01(remainingSeconds / Mathf.Max(0.01f, durationSeconds));

        if (loopPauseCountdownTexture == null)

        {

            loopPauseCountdownTexture = CreateLoopPauseCountdownTexture(96);

            loopPauseCountdownDial.style.backgroundImage = new StyleBackground(loopPauseCountdownTexture);

            lastLoopPauseCountdownProgress = -1f;

        }



        if (Mathf.Abs(progress - lastLoopPauseCountdownProgress) > 0.003f)

        {

            UpdateLoopPauseCountdownTexture(loopPauseCountdownTexture, progress);

            loopPauseCountdownDial.style.backgroundImage = new StyleBackground(loopPauseCountdownTexture);

            lastLoopPauseCountdownProgress = progress;

        }

    }



    private static Texture2D CreateLoopPauseCountdownTexture(int size)

    {

        Texture2D texture = new Texture2D(size, size, TextureFormat.RGBA32, false)

        {

            name = "LoopPauseCountdown",

            filterMode = FilterMode.Bilinear,

            wrapMode = TextureWrapMode.Clamp

        };

        UpdateLoopPauseCountdownTexture(texture, 1f);

        return texture;

    }



    private static void UpdateLoopPauseCountdownTexture(Texture2D texture, float progress)

    {

        int width = texture.width;

        int height = texture.height;

        Color32[] pixels = new Color32[width * height];

        float outerRadius = Mathf.Min(width, height) * 0.47f;

        float innerRadius = outerRadius * 0.58f;

        float softEdge = 1.6f;

        float arcLimit = Mathf.Clamp01(progress) * Mathf.PI * 2f;

        float arcFeather = 0.12f;



        for (int y = 0; y < height; y++)

        {

            for (int x = 0; x < width; x++)

            {

                float dx = x + 0.5f - width * 0.5f;

                float dy = height * 0.5f - (y + 0.5f);

                float distance = Mathf.Sqrt(dx * dx + dy * dy);

                if (distance > outerRadius + softEdge || distance < innerRadius - softEdge)

                {

                    pixels[(y * width) + x] = new Color32(255, 255, 255, 0);

                    continue;

                }



                float outerAlpha = Mathf.Clamp01((outerRadius + softEdge - distance) / softEdge);

                float innerAlpha = Mathf.Clamp01((distance - (innerRadius - softEdge)) / softEdge);

                float radialAlpha = Mathf.Min(outerAlpha, innerAlpha);

                float angle = Mathf.Atan2(dx, dy);

                if (angle < 0f)

                    angle += Mathf.PI * 2f;



                float angularAlpha = arcLimit >= Mathf.PI * 2f - 0.0001f

                    ? 1f

                    : Mathf.Clamp01((arcLimit - angle) / arcFeather);

                byte alpha = (byte)Mathf.RoundToInt(255f * radialAlpha * angularAlpha);

                pixels[(y * width) + x] = new Color32(255, 255, 255, alpha);

            }

        }



        texture.SetPixels32(pixels);

        texture.Apply(false, false);

    }



    public void Dispose()

    {

        if (libraryConfirmedSongGradientTexture != null)

            UnityEngine.Object.Destroy(libraryConfirmedSongGradientTexture);

        if (librarySelectionRailGradientTexture != null)

            UnityEngine.Object.Destroy(librarySelectionRailGradientTexture);

        if (pauseBackplateGradientTexture != null)

            UnityEngine.Object.Destroy(pauseBackplateGradientTexture);

        if (songEndAccentGradientTexture != null)

            UnityEngine.Object.Destroy(songEndAccentGradientTexture);

        if (songEndPanelBackdropTexture != null)

            UnityEngine.Object.Destroy(songEndPanelBackdropTexture);

        if (songEndHeroGlowTexture != null)

            UnityEngine.Object.Destroy(songEndHeroGlowTexture);

        if (loopPauseCountdownTexture != null)

            UnityEngine.Object.Destroy(loopPauseCountdownTexture);

        foreach (Texture2D texture in libraryArtworkTextureCache.Values)
        {
            if (texture != null)
                UnityEngine.Object.Destroy(texture);
        }

        libraryArtworkTextureCache.Clear();
        failedLibraryArtworkPaths.Clear();

        if (rootObject != null)

            UnityEngine.Object.Destroy(rootObject);

    }



    private static string GetAvailableSongScoreText(GuitarGameplaySnapshot snapshot, int songIndex)

    {

        if (snapshot.availableSongScoreTexts != null && songIndex >= 0 && songIndex < snapshot.availableSongScoreTexts.Count)

        {

            string text = snapshot.availableSongScoreTexts[songIndex];

            if (!string.IsNullOrWhiteSpace(text))

                return text;

        }



        float score = (snapshot.availableSongScores != null && songIndex >= 0 && songIndex < snapshot.availableSongScores.Count)

            ? snapshot.availableSongScores[songIndex]

            : 0f;

        return $"{score:F1}%";

    }



    private static string GetAvailableTrackScoreText(GuitarGameplaySnapshot snapshot, int trackIndex)

    {

        if (snapshot.availableTrackScoreTexts != null && trackIndex >= 0 && trackIndex < snapshot.availableTrackScoreTexts.Count)

        {

            string text = snapshot.availableTrackScoreTexts[trackIndex];

            if (!string.IsNullOrWhiteSpace(text))

                return text;

        }



        float score = (snapshot.availableTrackScores != null && trackIndex >= 0 && trackIndex < snapshot.availableTrackScores.Count)

            ? snapshot.availableTrackScores[trackIndex]

            : 0f;

        return $"{score:F1}%";

    }

    private static string FormatLibraryHeroHeartsText(int heartsRemaining, int heartsTotal)

    {

        int safeRemaining = Mathf.Max(0, heartsRemaining);

        int safeTotal = Mathf.Max(0, heartsTotal);

        const string redHeart = "<color=#EB4757>\u2665</color>";

        return $"{safeRemaining} {redHeart} / {safeTotal} {redHeart}";

    }



    private VisualElement CreateLibrarySelectionInlineStatBlock(string title, out Label valueLabel, Color valueColor)

    {

        VisualElement block = new VisualElement();

        block.style.minWidth = 178f;

        block.style.flexGrow = 1f;

        block.style.flexBasis = 0f;

        block.style.maxWidth = StyleKeyword.None;

        block.style.marginRight = 18f;

        block.style.marginBottom = 14f;

        block.style.paddingTop = 2f;

        block.style.paddingBottom = 4f;

        block.style.justifyContent = Justify.FlexStart;

        block.style.backgroundColor = StyleKeyword.None;

        block.style.borderTopWidth = 0f;

        block.style.borderRightWidth = 0f;

        block.style.borderBottomWidth = 0f;

        block.style.borderLeftWidth = 0f;

        Label titleLabel = CreateLabel(title, 18f, new Color(0.72f, 0.78f, 0.84f, 0.92f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        titleLabel.style.letterSpacing = 1.6f;

        titleLabel.style.marginBottom = 6f;

        titleLabel.style.unityFontDefinition = modernUiFontDefinition;

        titleLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        selectionInfoStatTitleLabels.Add(titleLabel);

        valueLabel = CreateLabel("--", 44f, valueColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        valueLabel.style.unityFontDefinition = modernUiFontDefinition;

        valueLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        valueLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

        valueLabel.style.whiteSpace = WhiteSpace.Normal;

        valueLabel.style.width = Length.Percent(100f);

        block.Add(titleLabel);

        block.Add(valueLabel);

        return block;

    }



    private static string BuildLibraryArtworkGlyph(string songName)

    {

        if (string.IsNullOrWhiteSpace(songName))

            return "?";



        string normalized = new string(songName

            .Where(ch => char.IsLetterOrDigit(ch) || char.IsWhiteSpace(ch))

            .ToArray())

            .Trim();

        if (string.IsNullOrWhiteSpace(normalized))

            normalized = songName.Trim();



        string[] parts = normalized

            .Split(new[] { ' ' }, StringSplitOptions.RemoveEmptyEntries);

        if (parts.Length >= 2)

        {

            char a = char.ToUpperInvariant(parts[0][0]);

            char b = char.ToUpperInvariant(parts[1][0]);

            return $"{a}{b}";

        }



        string compact = normalized.Replace(" ", string.Empty);

        if (compact.Length >= 2)

            return compact.Substring(0, 2).ToUpperInvariant();

        return compact.ToUpperInvariant();

    }

    private Texture2D GetLibraryArtworkTexture(string artworkPath)
    {
        if (string.IsNullOrWhiteSpace(artworkPath) || !File.Exists(artworkPath))
            return null;

        if (failedLibraryArtworkPaths.Contains(artworkPath))
            return null;

        if (libraryArtworkTextureCache.TryGetValue(artworkPath, out Texture2D cached) && cached != null)
            return cached;

        try
        {
            byte[] bytes = File.ReadAllBytes(artworkPath);
            Texture2D texture = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            texture.wrapMode = TextureWrapMode.Clamp;
            texture.filterMode = FilterMode.Bilinear;
            if (!texture.LoadImage(bytes, false))
            {
                UnityEngine.Object.Destroy(texture);
                failedLibraryArtworkPaths.Add(artworkPath);
                return null;
            }

            libraryArtworkTextureCache[artworkPath] = texture;
            return texture;
        }
        catch
        {
            failedLibraryArtworkPaths.Add(artworkPath);
            return null;
        }
    }

    private void ApplyLibraryArtworkToTile(VisualElement tile, Label glyphLabel, string artworkPath, string fallbackSongName)
    {
        if (tile == null)
            return;

        Texture2D artworkTexture = GetLibraryArtworkTexture(artworkPath);
        if (artworkTexture != null)
        {
            tile.style.backgroundImage = new StyleBackground(artworkTexture);
            tile.style.unityBackgroundScaleMode = ScaleMode.ScaleAndCrop;
            tile.style.backgroundColor = new Color(1f, 1f, 1f, 1f);
            if (glyphLabel != null)
            {
                glyphLabel.text = string.Empty;
                glyphLabel.style.display = DisplayStyle.None;
            }

            return;
        }

        tile.style.backgroundImage = StyleKeyword.None;
        tile.style.backgroundColor = new Color(0.76f, 0.98f, 0.54f, 1f);
        if (glyphLabel != null)
        {
            glyphLabel.text = BuildLibraryArtworkGlyph(fallbackSongName);
            glyphLabel.style.display = DisplayStyle.Flex;
        }
    }



    private void UpdateModernSongSelectionRows(GuitarGameplaySnapshot snapshot)

    {

        int total = snapshot.availableSongNames?.Count ?? 0;

        int selectedIndex = Mathf.Clamp(snapshot.selectedSongIndex, 0, Mathf.Max(0, total - 1));

        int selectedTrackIndex = Mathf.Clamp(snapshot.selectedTrackIndex, 0, Mathf.Max(0, (snapshot.availableTrackNames?.Count ?? 0) - 1));

        if (selectionInfoInstructionLabel != null)

        {

            selectionInfoInstructionLabel.text = total <= 0

                ? "No songs loaded."

                : snapshot.songSelectionSongConfirmed

                    ? "Song selected. Review the tracks on the right and launch the highlighted arrangement."

                    : "Choose a song on the left, then inspect its arrangements on the right.";

            selectionInfoInstructionLabel.style.opacity = 0.92f;

        }



        selectionSubtitleLabel.text = total > 0

            ? $"{total} songs loaded"

            : "No songs";



        EnsureModernSongSelectionRows(total);



        for (int songIndex = 0; songIndex < selectionRows.Count; songIndex++)

        {

            SongSelectionRow row = selectionRows[songIndex];

            bool isSelected = songIndex == snapshot.selectedSongIndex;

            bool isConfirmed = isSelected && snapshot.songSelectionSongConfirmed;

            bool isHovered = songIndex == hoveredSongRowIndex;

            string name = snapshot.availableSongNames[songIndex];

            row.indexLabel.text = (songIndex + 1).ToString("00");

            row.nameLabel.text = name;
            string artworkPath = snapshot.availableSongArtworkPaths != null && songIndex < snapshot.availableSongArtworkPaths.Count
                ? snapshot.availableSongArtworkPaths[songIndex]
                : string.Empty;
            ApplyLibraryArtworkToTile(row.artworkTile, row.artworkGlyphLabel, artworkPath, name);

            row.metaLabel.text = snapshot.availableSongSubtitles != null && songIndex < snapshot.availableSongSubtitles.Count

                ? snapshot.availableSongSubtitles[songIndex]

                : string.Empty;

            row.scoreLabel.text = GetAvailableSongScoreText(snapshot, songIndex);



            row.button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            row.button.style.backgroundImage = StyleKeyword.None;

            Color borderColor = isSelected || isHovered

                ? new Color(1f, 1f, 1f, 0.96f)

                : new Color(1f, 1f, 1f, 0f);

            row.button.style.borderTopColor = borderColor;

            row.button.style.borderRightColor = borderColor;

            row.button.style.borderBottomColor = borderColor;

            row.button.style.borderLeftColor = borderColor;

            float borderWidth = isSelected || isHovered ? 3f : 3f;

            row.button.style.borderTopWidth = borderWidth;

            row.button.style.borderRightWidth = borderWidth;

            row.button.style.borderBottomWidth = borderWidth;

            row.button.style.borderLeftWidth = borderWidth;

            row.button.style.height = (isSelected
                ? (currentCompactSelectionLayout ? 176f : 168f)
                : (currentCompactSelectionLayout ? 164f : 156f)) * currentMenuLayoutScale;

            row.button.style.marginLeft = 0f;

            row.button.style.marginRight = 0f;

            row.button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

            row.button.style.translate = new Translate(0f, 0f, 0f);

            row.button.style.opacity = 1f;

            row.slantPlate.style.display = DisplayStyle.None;

            row.slantEdge.style.display = DisplayStyle.None;

            row.accentBar.style.display = DisplayStyle.None;

            row.scoreBadge.style.display = DisplayStyle.None;

            row.indexLabel.style.display = DisplayStyle.None;

            if (row.artworkTile != null)

            {

                float rowHeight = (isSelected
                    ? (currentCompactSelectionLayout ? 176f : 168f)
                    : (currentCompactSelectionLayout ? 164f : 156f)) * currentMenuLayoutScale;

                row.artworkTile.style.width = rowHeight;

                row.artworkTile.style.minWidth = rowHeight;

                row.artworkTile.style.height = Length.Percent(100f);

                row.artworkTile.style.opacity = isSelected ? 1f : 0.94f;

                row.artworkTile.style.scale = isSelected ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);

            }

            row.nameLabel.style.color = new Color(1f, 1f, 1f, isSelected ? 1f : 0.94f);

            row.metaLabel.style.display = string.IsNullOrWhiteSpace(row.metaLabel.text) ? DisplayStyle.None : DisplayStyle.Flex;

            row.metaLabel.style.color = new Color(1f, 1f, 1f, isSelected ? 0.72f : 0.56f);

            row.scoreLabel.style.color = new Color(1f, 1f, 1f, isSelected ? 0.92f : 0.70f);

        }



        if (total > 0)

        {

            string selectedSongName = snapshot.availableSongNames[selectedIndex];
            string selectedSongSubtitle = !string.IsNullOrWhiteSpace(snapshot.selectedLibrarySongSubtitle)
                ? snapshot.selectedLibrarySongSubtitle
                : "Unknown Artist";
            string selectedTrackName = (snapshot.availableTrackNames != null && selectedTrackIndex >= 0 && selectedTrackIndex < snapshot.availableTrackNames.Count)
                ? snapshot.availableTrackNames[selectedTrackIndex]
                : "No arrangement selected";
            string audioSummary = snapshot.selectedLibrarySongHasMp3
                ? (snapshot.selectedLibrarySongHasMidi ? "MP3 / XML / MIDI" : "MP3 / XML")
                : (snapshot.selectedLibrarySongHasMidi ? "XML / MIDI" : "XML only");

            selectionInfoTitleLabel.text = selectedSongName;
            selectionInfoMetaLabel.text = selectedSongSubtitle;

            if (selectionInfoSummaryLabel != null)
                selectionInfoSummaryLabel.text = $"Song {selectedIndex + 1} of {total} | Focused track: {selectedTrackName}";
            if (selectionArtworkGlyphLabel != null)
                selectionArtworkGlyphLabel.text = BuildLibraryArtworkGlyph(selectedSongName);
            ApplyLibraryArtworkToTile(selectionArtworkCard, selectionArtworkGlyphLabel, snapshot.selectedLibrarySongArtworkPath, selectedSongName);
            if (selectionArtworkCaptionLabel != null)
            {
                selectionArtworkCaptionLabel.text = string.IsNullOrWhiteSpace(selectedSongSubtitle) ? "SONG LIBRARY" : selectedSongSubtitle.ToUpperInvariant();
                selectionArtworkCaptionLabel.style.display = string.IsNullOrWhiteSpace(snapshot.selectedLibrarySongArtworkPath) || GetLibraryArtworkTexture(snapshot.selectedLibrarySongArtworkPath) == null
                    ? DisplayStyle.Flex
                    : DisplayStyle.None;
            }
            if (selectionInfoArrangementValueLabel != null)
                selectionInfoArrangementValueLabel.text = Mathf.Max(0, snapshot.selectedLibrarySongTrackCount).ToString();
            if (selectionInfoAudioValueLabel != null)
                selectionInfoAudioValueLabel.text = audioSummary;
            if (selectionInfoHeroHeartsValueLabel != null)
                selectionInfoHeroHeartsValueLabel.text = FormatLibraryHeroHeartsText(snapshot.selectedLibrarySongHeroBestHeartsRemaining, snapshot.selectedLibrarySongHeroBestHeartsTotal);

            UpdateModernLibraryTrackRows(snapshot);

        }

        else

        {

            selectionInfoTitleLabel.text = "No songs available";

            selectionInfoMetaLabel.text = "Import songs into your library to populate this screen.";

            if (selectionInfoSummaryLabel != null)
                selectionInfoSummaryLabel.text = "No song details available.";
            if (selectionArtworkGlyphLabel != null)
            {
                selectionArtworkGlyphLabel.text = "--";
                selectionArtworkGlyphLabel.style.display = DisplayStyle.Flex;
            }
            if (selectionArtworkCaptionLabel != null)
            {
                selectionArtworkCaptionLabel.text = "SONG LIBRARY";
                selectionArtworkCaptionLabel.style.display = DisplayStyle.Flex;
            }
            if (selectionArtworkCard != null)
            {
                selectionArtworkCard.style.backgroundImage = StyleKeyword.None;
                selectionArtworkCard.style.backgroundColor = new Color(0.76f, 0.98f, 0.54f, 1f);
            }

            selectionInfoScoreLabel.text = "--";

            selectionInfoBestTrackLabel.text = "--";

            if (selectionInfoArrangementValueLabel != null)
                selectionInfoArrangementValueLabel.text = "--";
            if (selectionInfoAudioValueLabel != null)
                selectionInfoAudioValueLabel.text = "--";
            if (selectionInfoHeroHeartsValueLabel != null)
                selectionInfoHeroHeartsValueLabel.text = FormatLibraryHeroHeartsText(0, 0);

            EnsureModernLibraryTrackRows(0);

            selectionStartButton.SetEnabled(false);

            selectionStartButton.style.opacity = 0.45f;

        }



        if (snapshot.selectedSongIndex != lastAutoScrolledSongIndex &&

            snapshot.selectedSongIndex >= 0 &&

            snapshot.selectedSongIndex < selectionRows.Count)

        {

            CenterModernSongSelectionRow(snapshot.selectedSongIndex);

            lastAutoScrolledSongIndex = snapshot.selectedSongIndex;

        }

    }



    private void EnsureModernSongSelectionRows(int count)

    {

        if (selectionRows.Count != count)

        {

            selectionScrollView.Clear();

            selectionRows.Clear();

            lastAutoScrolledSongIndex = -1;

            hoveredSongRowIndex = -1;



            for (int i = 0; i < count; i++)

            {

                int songIndex = i;

                Button rowButton = new Button(() => OnSongRowClicked(songIndex));

                rowButton.focusable = false;

                rowButton.text = string.Empty;

                rowButton.style.height = 176f;

                rowButton.style.marginTop = 6f;

                rowButton.style.marginBottom = 6f;

                rowButton.style.marginLeft = 0f;

                rowButton.style.marginRight = 0f;

                rowButton.style.paddingLeft = 0f;

                rowButton.style.paddingRight = 20f;

                rowButton.style.paddingTop = 0f;

                rowButton.style.paddingBottom = 0f;

                rowButton.style.position = Position.Relative;

                rowButton.style.overflow = Overflow.Hidden;

                rowButton.style.width = Length.Percent(100f);

                rowButton.style.alignSelf = Align.Stretch;

                rowButton.style.borderTopLeftRadius = 14f;

                rowButton.style.borderTopRightRadius = 14f;

                rowButton.style.borderBottomLeftRadius = 14f;

                rowButton.style.borderBottomRightRadius = 14f;

                rowButton.style.borderTopWidth = 3f;

                rowButton.style.borderRightWidth = 3f;

                rowButton.style.borderBottomWidth = 3f;

                rowButton.style.borderLeftWidth = 3f;

                rowButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                rowButton.style.borderTopColor = new Color(1f, 1f, 1f, 0f);

                rowButton.style.borderRightColor = new Color(1f, 1f, 1f, 0f);

                rowButton.style.borderBottomColor = new Color(1f, 1f, 1f, 0f);

                rowButton.style.borderLeftColor = new Color(1f, 1f, 1f, 0f);

                rowButton.style.color = Color.white;

                rowButton.style.unityFontDefinition = bodyFontDefinition;

                rowButton.style.minWidth = 0f;

                rowButton.style.maxWidth = StyleKeyword.None;

                rowButton.RegisterCallback<MouseEnterEvent>(_ => hoveredSongRowIndex = songIndex);

                rowButton.RegisterCallback<MouseLeaveEvent>(_ =>

                {

                    if (hoveredSongRowIndex == songIndex)

                        hoveredSongRowIndex = -1;

                });



                VisualElement slantPlate = new VisualElement();

                slantPlate.style.position = Position.Absolute;

                slantPlate.style.left = -54f;

                slantPlate.style.top = -28f;

                slantPlate.style.width = 310f;

                slantPlate.style.height = 320f;

                slantPlate.style.rotate = new Rotate(new Angle(-13f, AngleUnit.Degree));

                slantPlate.style.backgroundColor = new Color(0.24f, 0.24f, 0.24f, 1f);

                slantPlate.pickingMode = PickingMode.Ignore;



                VisualElement slantEdge = new VisualElement();

                slantEdge.style.position = Position.Absolute;

                slantEdge.style.right = -6f;

                slantEdge.style.top = -18f;

                slantEdge.style.width = 10f;

                slantEdge.style.height = 340f;

                slantEdge.style.rotate = new Rotate(new Angle(0f, AngleUnit.Degree));

                slantEdge.style.backgroundColor = new Color(0.46f, 0.52f, 0.58f, 0.40f);

                slantEdge.style.borderTopLeftRadius = 8f;

                slantEdge.style.borderTopRightRadius = 8f;

                slantEdge.style.borderBottomLeftRadius = 8f;

                slantEdge.style.borderBottomRightRadius = 8f;

                slantEdge.pickingMode = PickingMode.Ignore;

                slantPlate.Add(slantEdge);



                VisualElement content = new VisualElement();

                content.style.flexDirection = FlexDirection.Row;

                content.style.justifyContent = Justify.FlexStart;

                content.style.alignItems = Align.Stretch;

                content.style.flexGrow = 1f;

                content.style.height = Length.Percent(100f);

                content.style.width = Length.Percent(100f);

                content.style.position = Position.Relative;

                content.style.paddingLeft = 0f;

                content.style.paddingRight = 0f;



                VisualElement accentBar = new VisualElement();

                accentBar.style.display = DisplayStyle.None;

                VisualElement artworkTile = new VisualElement();

                artworkTile.style.width = 156f;

                artworkTile.style.minWidth = 156f;

                artworkTile.style.height = Length.Percent(100f);

                artworkTile.style.marginRight = 18f;

                artworkTile.style.alignItems = Align.Center;

                artworkTile.style.justifyContent = Justify.Center;

                artworkTile.style.backgroundColor = new Color(0.76f, 0.98f, 0.54f, 1f);

                artworkTile.style.borderTopLeftRadius = 14f;

                artworkTile.style.borderTopRightRadius = 0f;

                artworkTile.style.borderBottomLeftRadius = 14f;

                artworkTile.style.borderBottomRightRadius = 0f;

                artworkTile.style.flexShrink = 0f;

                artworkTile.style.alignSelf = Align.Stretch;

                Label artworkGlyphLabel = CreateLabel("--", 44f, new Color(0.03f, 0.05f, 0.03f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

                artworkGlyphLabel.style.unityFontDefinition = modernUiFontDefinition;

                artworkGlyphLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                artworkGlyphLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

                artworkTile.Add(artworkGlyphLabel);



                Label indexLabel = CreateLabel("00", 28f, new Color(0.58f, 0.81f, 0.97f, 0.90f), true, TextAnchor.MiddleCenter, useTitleFont: false);

                indexLabel.style.minWidth = 0f;

                indexLabel.style.marginRight = 0f;

                indexLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                indexLabel.style.unityFontDefinition = modernUiFontDefinition;

                indexLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                indexLabel.style.alignSelf = Align.Center;



                VisualElement textColumn = new VisualElement();

                textColumn.style.flexGrow = 1f;

                textColumn.style.flexBasis = 0f;

                textColumn.style.flexShrink = 1f;

                textColumn.style.minWidth = 0f;

                textColumn.style.justifyContent = Justify.Center;

                textColumn.style.overflow = Overflow.Hidden;

                textColumn.style.paddingRight = 8f;

                textColumn.style.marginRight = 10f;

                textColumn.style.alignSelf = Align.Stretch;



                Label nameLabel = CreateLabel(string.Empty, 37.2f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

                nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

                nameLabel.style.whiteSpace = WhiteSpace.NoWrap;

                nameLabel.style.textOverflow = TextOverflow.Ellipsis;

                nameLabel.style.flexShrink = 1f;

                nameLabel.style.maxWidth = Length.Percent(100f);

                nameLabel.style.overflow = Overflow.Hidden;

                nameLabel.style.unityFontDefinition = modernUiFontDefinition;

                nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;



                Label metaLabel = CreateLabel(string.Empty, 36f, new Color(0.64f, 0.76f, 0.90f, 0.88f), false, TextAnchor.MiddleLeft, useTitleFont: false);

                metaLabel.style.display = DisplayStyle.None;

                metaLabel.style.marginTop = 2f;

                metaLabel.style.unityFontDefinition = modernUiFontDefinition;

                metaLabel.style.whiteSpace = WhiteSpace.NoWrap;

                metaLabel.style.textOverflow = TextOverflow.Ellipsis;



                VisualElement scoreBadge = new VisualElement();

                scoreBadge.style.display = DisplayStyle.None;



                Label scoreLabel = CreateLabel("0%", 44f, Color.white, true, TextAnchor.MiddleRight, useTitleFont: false);

                scoreLabel.style.width = 176f;

                scoreLabel.style.minWidth = 176f;

                scoreLabel.style.maxWidth = 176f;

                scoreLabel.style.flexShrink = 0f;

                scoreLabel.style.unityTextAlign = TextAnchor.MiddleRight;

                scoreLabel.style.unityFontDefinition = modernUiFontDefinition;

                scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

                scoreLabel.style.alignSelf = Align.Center;



                textColumn.Add(nameLabel);

                textColumn.Add(metaLabel);

                content.Add(artworkTile);

                content.Add(indexLabel);

                content.Add(textColumn);

                content.Add(scoreLabel);

                rowButton.Add(content);

                AttachWheelScrolling(rowButton, selectionScrollView);

                AttachWheelScrolling(content, selectionScrollView);

                AttachWheelScrolling(indexLabel, selectionScrollView);

                AttachWheelScrolling(textColumn, selectionScrollView);

                AttachWheelScrolling(nameLabel, selectionScrollView);

                AttachWheelScrolling(scoreLabel, selectionScrollView);

                content.RegisterCallback<ClickEvent>(_ => OnSongRowClicked(songIndex));

                indexLabel.RegisterCallback<ClickEvent>(_ => OnSongRowClicked(songIndex));

                textColumn.RegisterCallback<ClickEvent>(_ => OnSongRowClicked(songIndex));

                nameLabel.RegisterCallback<ClickEvent>(_ => OnSongRowClicked(songIndex));

                scoreLabel.RegisterCallback<ClickEvent>(_ => OnSongRowClicked(songIndex));

                selectionScrollView.Add(rowButton);



                selectionRows.Add(new SongSelectionRow

                {

                    button = rowButton,

                    artworkTile = artworkTile,

                    artworkGlyphLabel = artworkGlyphLabel,

                    slantPlate = slantPlate,

                    slantEdge = slantEdge,

                    accentBar = accentBar,

                    scoreBadge = scoreBadge,

                    indexLabel = indexLabel,

                    nameLabel = nameLabel,

                    metaLabel = metaLabel,

                    scoreLabel = scoreLabel

                });

            }

        }



        selectionScrollView.contentContainer.style.paddingTop = 8f;

        selectionScrollView.contentContainer.style.paddingBottom = 8f;

    }



    private void CenterModernSongSelectionRow(int rowIndex)

    {

        if (rowIndex < 0 || rowIndex >= selectionRows.Count || selectionScrollView == null)

            return;



        selectionScrollView.ScrollTo(selectionRows[rowIndex].button);

    }



    private void UpdateModernLibraryTrackRows(GuitarGameplaySnapshot snapshot)

    {

        int total = snapshot.availableTrackNames?.Count ?? 0;

        int selectedTrackIndex = Mathf.Clamp(snapshot.selectedTrackIndex, 0, Mathf.Max(0, total - 1));

        EnsureModernLibraryTrackRows(total);



        float bestScore = -1f;

        int bestTrackIndex = -1;

        string bestTrackName = "--";



        for (int trackIndex = 0; trackIndex < selectionTrackRows.Count; trackIndex++)

        {

            LibraryTrackRow row = selectionTrackRows[trackIndex];

            bool isSelected = trackIndex == snapshot.selectedTrackIndex;

            string trackName = snapshot.availableTrackNames[trackIndex];

            float score = (snapshot.availableTrackScores != null && trackIndex < snapshot.availableTrackScores.Count)

                ? snapshot.availableTrackScores[trackIndex]

                : 0f;



            if (score > bestScore)

            {

                bestScore = score;

                bestTrackIndex = trackIndex;

                bestTrackName = trackName;

            }



            row.nameLabel.text = trackName;

            row.scoreLabel.text = GetAvailableTrackScoreText(snapshot, trackIndex);

            row.button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            row.button.style.backgroundImage = StyleKeyword.None;

            Color trackBorderColor = isSelected || trackIndex == hoveredLibraryTrackRowIndex

                ? new Color(1f, 1f, 1f, 0.96f)

                : new Color(1f, 1f, 1f, 0f);

            row.button.style.borderTopColor = trackBorderColor;

            row.button.style.borderRightColor = row.button.style.borderTopColor;

            row.button.style.borderBottomColor = row.button.style.borderTopColor;

            row.button.style.borderLeftColor = row.button.style.borderTopColor;

            row.button.style.borderTopWidth = 3f;

            row.button.style.borderRightWidth = 3f;

            row.button.style.borderBottomWidth = 3f;

            row.button.style.borderLeftWidth = 3f;

            row.button.style.scale = new Scale(Vector3.one);

            row.button.style.translate = new Translate(0f, 0f, 0f);

            row.nameLabel.style.color = new Color(1f, 1f, 1f, isSelected ? 1f : 0.94f);

            row.scoreLabel.style.color = new Color(1f, 1f, 1f, isSelected ? 0.92f : 0.72f);

        }



        if (total > 0)

        {

            selectionInfoScoreLabel.text = bestTrackIndex >= 0 ? GetAvailableTrackScoreText(snapshot, bestTrackIndex) : "--";

            selectionInfoBestTrackLabel.text = bestTrackName;

            selectionInfoHintLabel.text = total > 1

                ? "Pick the arrangement you want to play. The outlined row is what Start will launch."

                : "This song exposes a single arrangement. Press Start to launch it.";

            if (selectedTrackIndex != lastAutoScrolledTrackIndex &&

                selectedTrackIndex >= 0 &&

                selectedTrackIndex < selectionTrackRows.Count)

            {

                selectionTrackScrollView.ScrollTo(selectionTrackRows[selectedTrackIndex].button);

                lastAutoScrolledTrackIndex = selectedTrackIndex;

            }

            selectionStartButton.SetEnabled(true);

            selectionStartButton.style.opacity = 1f;

        }

        else

        {

            selectionInfoScoreLabel.text = "--";

            selectionInfoBestTrackLabel.text = "--";

            selectionInfoHintLabel.text = "No arrangements were found for this song.";

            selectionStartButton.SetEnabled(false);

            selectionStartButton.style.opacity = 0.45f;

            lastAutoScrolledTrackIndex = -1;

        }

    }



    private void EnsureModernLibraryTrackRows(int count)

    {

        if (selectionTrackRows.Count == count)

            return;



        selectionTrackScrollView.Clear();

        selectionTrackRows.Clear();

        lastAutoScrolledTrackIndex = -1;

        hoveredLibraryTrackRowIndex = -1;



        for (int i = 0; i < count; i++)

        {

            int trackIndex = i;

            Button rowButton = new Button(() => OnTrackRowClicked(trackIndex));

            rowButton.focusable = false;

            rowButton.style.height = 142f;

            rowButton.style.marginTop = 8f;

            rowButton.style.marginBottom = 8f;

            rowButton.style.marginLeft = 0f;

            rowButton.style.marginRight = 0f;

            rowButton.style.paddingLeft = 0f;

            rowButton.style.paddingRight = 22f;

            rowButton.style.paddingTop = 0f;

            rowButton.style.paddingBottom = 0f;

            rowButton.style.borderTopLeftRadius = 14f;

            rowButton.style.borderTopRightRadius = 14f;

            rowButton.style.borderBottomLeftRadius = 14f;

            rowButton.style.borderBottomRightRadius = 14f;

            rowButton.style.borderTopWidth = 3f;

            rowButton.style.borderRightWidth = 3f;

            rowButton.style.borderBottomWidth = 3f;

            rowButton.style.borderLeftWidth = 3f;

            rowButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            rowButton.style.borderTopColor = new Color(1f, 1f, 1f, 0f);

            rowButton.style.borderRightColor = new Color(1f, 1f, 1f, 0f);

            rowButton.style.borderBottomColor = new Color(1f, 1f, 1f, 0f);

            rowButton.style.borderLeftColor = new Color(1f, 1f, 1f, 0f);

            rowButton.style.overflow = Overflow.Visible;

            rowButton.RegisterCallback<MouseEnterEvent>(_ => hoveredLibraryTrackRowIndex = trackIndex);

            rowButton.RegisterCallback<MouseLeaveEvent>(_ =>

            {

                if (hoveredLibraryTrackRowIndex == trackIndex)

                    hoveredLibraryTrackRowIndex = -1;

            });



            VisualElement content = new VisualElement();

            content.style.flexDirection = FlexDirection.Row;

            content.style.alignItems = Align.Center;

            content.style.justifyContent = Justify.SpaceBetween;

            content.style.height = Length.Percent(100f);



            Label nameLabel = CreateLabel(string.Empty, 30f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: false);

            nameLabel.style.flexGrow = 1f;

            nameLabel.style.marginLeft = 20f;

            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            nameLabel.style.unityFontDefinition = modernUiFontDefinition;

            nameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;



            Label scoreLabel = CreateLabel("0%", 34f, Color.white, true, TextAnchor.MiddleRight, useTitleFont: false);

            scoreLabel.style.minWidth = 176f;

            scoreLabel.style.unityFontDefinition = modernUiFontDefinition;

            scoreLabel.style.unityFontStyleAndWeight = FontStyle.Bold;



            content.Add(nameLabel);

            content.Add(scoreLabel);

            rowButton.Add(content);

            AttachWheelScrolling(rowButton, selectionTrackScrollView);

            selectionTrackScrollView.Add(rowButton);



            selectionTrackRows.Add(new LibraryTrackRow

            {

                button = rowButton,

                accent = null,

                nameLabel = nameLabel,

                scoreLabel = scoreLabel

            });

        }

    }



    private void UpdateSongSelectionRows(GuitarGameplaySnapshot snapshot)

    {

        int total = snapshot.availableSongNames?.Count ?? 0;

        int selectedIndex = Mathf.Clamp(snapshot.selectedSongIndex, 0, Mathf.Max(0, total - 1));

        selectionSubtitleLabel.text = total > 0

            ? $"{total} songs  \u2022  Focused #{selectedIndex + 1}"

            : "No songs found  \u2022  Drop files into the songs folder and refresh";



        EnsureSongSelectionRows(total);



        for (int songIndex = 0; songIndex < selectionRows.Count; songIndex++)

        {

            SongSelectionRow row = selectionRows[songIndex];

            bool isSelected = songIndex == snapshot.selectedSongIndex;

            string name = snapshot.availableSongNames[songIndex];

            row.indexLabel.text = (songIndex + 1).ToString("00");

            row.nameLabel.text = name;

            row.metaLabel.text = isSelected ? "READY TO OPEN ARRANGEMENTS" : "PRESS ENTER OR CLICK";

            row.scoreLabel.text = GetAvailableSongScoreText(snapshot, songIndex);



            row.button.style.backgroundColor = isSelected

                ? new Color(0.92f, 0.98f, 0.22f, 0.98f)

                : new Color(0.07f, 0.11f, 0.18f, 0.94f);

            Color borderColor = isSelected

                ? new Color(0.35f, 0.98f, 1f, 1f)

                : new Color(0.24f, 0.52f, 0.82f, 0.62f);

            row.button.style.borderTopColor = borderColor;

            row.button.style.borderRightColor = borderColor;

            row.button.style.borderBottomColor = isSelected ? new Color(0.18f, 0.70f, 0.82f, 1f) : borderColor;

            row.button.style.borderLeftColor = borderColor;

            row.button.style.scale = isSelected

                ? new Scale(new Vector3(1.035f, 1.035f, 1f))

                : new Scale(new Vector3(1f, 1f, 1f));

            row.button.style.translate = isSelected

                ? new Translate(-10f, 0f, 0f)

                : new Translate(0f, 0f, 0f);

            row.accentBar.style.backgroundColor = isSelected

                ? new Color(0.14f, 0.90f, 0.96f, 1f)

                : new Color(0.13f, 0.24f, 0.35f, 0.88f);

            row.indexLabel.style.color = isSelected

                ? new Color(0.08f, 0.15f, 0.21f, 0.98f)

                : new Color(0.58f, 0.81f, 0.97f, 0.90f);

            row.nameLabel.style.color = isSelected

                ? new Color(0.08f, 0.13f, 0.17f, 1f)

                : Color.white;

            row.metaLabel.style.color = isSelected

                ? new Color(0.14f, 0.24f, 0.26f, 0.92f)

                : new Color(0.64f, 0.76f, 0.90f, 0.88f);

            row.scoreLabel.style.color = isSelected

                ? new Color(0.08f, 0.16f, 0.18f, 0.98f)

                : new Color(1f, 0.85f, 0.45f, 0.98f);

        }



        if (total > 0)

        {

            string selectedName = snapshot.availableSongNames[selectedIndex];

            selectionInfoTitleLabel.text = selectedName;

            selectionInfoMetaLabel.text = $"Song {selectedIndex + 1} of {total}  \u2022  Open to inspect track arrangements and launch directly into play.";

            selectionInfoScoreLabel.text = GetAvailableSongScoreText(snapshot, selectedIndex);

            selectionInfoHintLabel.text = "Scroll naturally with mouse or wheel. The focused song expands and brightens so the current target always reads clearly.";

        }

        else

        {

            selectionInfoTitleLabel.text = "No songs available";

            selectionInfoMetaLabel.text = "Import songs into the library, then refresh to populate the selector.";

            selectionInfoScoreLabel.text = "--";

            selectionInfoHintLabel.text = "Use Songs Folder to jump to your library location, then refresh this screen.";

        }



        if (snapshot.selectedSongIndex != lastAutoScrolledSongIndex && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < selectionRows.Count)

        {

            selectionScrollView.ScrollTo(selectionRows[snapshot.selectedSongIndex].button);

            lastAutoScrolledSongIndex = snapshot.selectedSongIndex;

        }

    }



    private void EnsureSongSelectionRows(int count)

    {

        if (selectionRows.Count == count)

            return;



        selectionScrollView.Clear();

        selectionRows.Clear();

        lastAutoScrolledSongIndex = -1;



        for (int i = 0; i < count; i++)

        {

            int songIndex = i;

            Button rowButton = CreateActionButton(string.Empty, () => OnSongRowClicked(songIndex));

            rowButton.style.height = 124f;

            rowButton.style.marginTop = 8f;

            rowButton.style.marginBottom = 6f;

            rowButton.style.paddingLeft = 0f;

            rowButton.style.paddingRight = 18f;

            rowButton.style.paddingTop = 0f;

            rowButton.style.paddingBottom = 0f;

            rowButton.style.borderTopLeftRadius = 20f;

            rowButton.style.borderTopRightRadius = 20f;

            rowButton.style.borderBottomLeftRadius = 20f;

            rowButton.style.borderBottomRightRadius = 20f;

            rowButton.style.borderTopWidth = 2f;

            rowButton.style.borderRightWidth = 2f;

            rowButton.style.borderBottomWidth = 4f;

            rowButton.style.borderLeftWidth = 2f;



            VisualElement content = new VisualElement();

            content.style.flexDirection = FlexDirection.Row;

            content.style.justifyContent = Justify.SpaceBetween;

            content.style.alignItems = Align.Center;

            content.style.flexGrow = 1f;

            content.style.height = Length.Percent(100f);



            VisualElement accentBar = new VisualElement();

            accentBar.style.width = 12f;

            accentBar.style.height = Length.Percent(100f);

            accentBar.style.marginRight = 18f;

            accentBar.style.borderTopLeftRadius = 20f;

            accentBar.style.borderBottomLeftRadius = 20f;



            Label indexLabel = CreateLabel("00", 24f, new Color(0.58f, 0.81f, 0.97f, 0.90f), true, TextAnchor.MiddleCenter, useTitleFont: true);

            indexLabel.style.minWidth = 78f;

            indexLabel.style.marginRight = 12f;



            VisualElement textColumn = new VisualElement();

            textColumn.style.flexGrow = 1f;

            textColumn.style.justifyContent = Justify.Center;



            Label nameLabel = CreateLabel(string.Empty, 38f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

            nameLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            nameLabel.style.whiteSpace = WhiteSpace.Normal;



            Label metaLabel = CreateLabel(string.Empty, 18f, new Color(0.64f, 0.76f, 0.90f, 0.88f), true, TextAnchor.MiddleLeft, useTitleFont: false);

            metaLabel.style.letterSpacing = 1.4f;

            metaLabel.style.marginTop = 4f;



            Label scoreLabel = CreateLabel("0%", 34f, new Color(1f, 0.85f, 0.45f, 0.98f), true, TextAnchor.MiddleRight, useTitleFont: true);

            scoreLabel.style.minWidth = 130f;

            scoreLabel.style.unityTextAlign = TextAnchor.MiddleRight;



            textColumn.Add(nameLabel);

            textColumn.Add(metaLabel);

            content.Add(accentBar);

            content.Add(indexLabel);

            content.Add(textColumn);

            content.Add(scoreLabel);

            rowButton.Add(content);

            selectionScrollView.Add(rowButton);



            selectionRows.Add(new SongSelectionRow

            {

                button = rowButton,

                accentBar = accentBar,

                indexLabel = indexLabel,

                nameLabel = nameLabel,

                metaLabel = metaLabel,

                scoreLabel = scoreLabel

            });

        }

    }



    private void OnSongRowClicked(int rowIndex)

    {

        if (owner == null)

            return;



        owner.SelectSongByIndexFromUi(rowIndex);

    }





    private void UpdateTrackSelectionRows(GuitarGameplaySnapshot snapshot)

    {

        int total = snapshot.availableTrackNames?.Count ?? 0;

        trackSelectionTitleLabel.text = "TRACKS";

        int selectedIndex = Mathf.Clamp(snapshot.selectedTrackIndex, 0, Mathf.Max(0, total - 1));

        trackSelectionSubtitleLabel.text = total > 0

            ? $"{total} arrangements  \u2022  Focused #{selectedIndex + 1}"

            : "No arrangements available";



        EnsureTrackSelectionRows(total);



        for (int trackIndex = 0; trackIndex < trackSelectionRows.Count; trackIndex++)

        {

            TrackSelectionRow row = trackSelectionRows[trackIndex];

            bool isSelected = trackIndex == snapshot.selectedTrackIndex;

            string name = snapshot.availableTrackNames[trackIndex];

            row.indexLabel.text = (trackIndex + 1).ToString("00");

            row.nameLabel.text = name;

            row.metaLabel.text = isSelected ? "PRIMARY ARRANGEMENT IN FOCUS" : "SELECT THIS ARRANGEMENT";

            row.scoreLabel.text = GetAvailableTrackScoreText(snapshot, trackIndex);



            row.button.style.backgroundColor = isSelected

                ? new Color(0.14f, 0.95f, 0.98f, 0.98f)

                : new Color(0.05f, 0.12f, 0.20f, 0.95f);

            Color borderColor = isSelected ? new Color(0.62f, 0.96f, 1f, 1f) : new Color(0.35f, 0.66f, 0.94f, 0.74f);

            row.button.style.borderTopColor = borderColor;

            row.button.style.borderRightColor = borderColor;

            row.button.style.borderBottomColor = isSelected ? new Color(0.16f, 0.72f, 0.82f, 1f) : borderColor;

            row.button.style.borderLeftColor = borderColor;

            row.button.style.scale = isSelected

                ? new Scale(new Vector3(1.03f, 1.03f, 1f))

                : new Scale(new Vector3(1f, 1f, 1f));

            row.button.style.translate = isSelected

                ? new Translate(-8f, 0f, 0f)

                : new Translate(0f, 0f, 0f);

            row.accentBar.style.backgroundColor = isSelected

                ? new Color(0.72f, 0.97f, 1f, 1f)

                : new Color(0.12f, 0.23f, 0.31f, 0.90f);

            row.indexLabel.style.color = isSelected

                ? new Color(0.06f, 0.17f, 0.20f, 1f)

                : new Color(0.57f, 0.86f, 1f, 0.92f);

            row.nameLabel.style.color = isSelected

                ? new Color(0.06f, 0.17f, 0.20f, 1f)

                : Color.white;

            row.metaLabel.style.color = isSelected

                ? new Color(0.07f, 0.21f, 0.24f, 0.94f)

                : new Color(0.70f, 0.86f, 0.96f, 0.88f);

            row.scoreLabel.style.color = isSelected

                ? new Color(0.06f, 0.17f, 0.20f, 1f)

                : new Color(0.54f, 0.92f, 1f, 0.99f);

        }



        if (total > 0)

        {

            string selectedName = snapshot.availableTrackNames[selectedIndex];

            trackSelectionInfoTitleLabel.text = selectedName;

            trackSelectionInfoMetaLabel.text = $"{snapshot.currentSongDisplayName}  \u2022  Arrangement {selectedIndex + 1} of {total}";

            trackSelectionInfoScoreLabel.text = GetAvailableTrackScoreText(snapshot, selectedIndex);

            trackSelectionInfoHintLabel.text = string.IsNullOrWhiteSpace(snapshot.trackSelectionHint)

                ? "Track selection is fully scrollable now. Focus the row you want and confirm to load it."

                : snapshot.trackSelectionHint;

        }

        else

        {

            trackSelectionInfoTitleLabel.text = "No arrangements";

            trackSelectionInfoMetaLabel.text = snapshot.currentSongDisplayName;

            trackSelectionInfoScoreLabel.text = "--";

            trackSelectionInfoHintLabel.text = "This song did not expose any arrangement choices.";

        }



        if (snapshot.selectedTrackIndex != lastAutoScrolledTrackIndex && snapshot.selectedTrackIndex >= 0 && snapshot.selectedTrackIndex < trackSelectionRows.Count)

        {

            trackSelectionScrollView.ScrollTo(trackSelectionRows[snapshot.selectedTrackIndex].button);

            lastAutoScrolledTrackIndex = snapshot.selectedTrackIndex;

        }

    }



    private void EnsureTrackSelectionRows(int count)

    {

        if (trackSelectionRows.Count == count)

            return;



        trackSelectionScrollView.Clear();

        trackSelectionRows.Clear();

        lastAutoScrolledTrackIndex = -1;



        for (int i = 0; i < count; i++)

        {

            int trackIndex = i;

            Button rowButton = CreateActionButton(string.Empty, () => OnTrackRowClicked(trackIndex));

            rowButton.style.height = 126f;

            rowButton.style.marginTop = 8f;

            rowButton.style.marginBottom = 6f;

            rowButton.style.paddingLeft = 0f;

            rowButton.style.paddingRight = 18f;

            rowButton.style.paddingTop = 0f;

            rowButton.style.paddingBottom = 0f;

            rowButton.style.borderTopLeftRadius = 20f;

            rowButton.style.borderTopRightRadius = 20f;

            rowButton.style.borderBottomLeftRadius = 20f;

            rowButton.style.borderBottomRightRadius = 20f;

            rowButton.style.borderTopWidth = 2f;

            rowButton.style.borderRightWidth = 2f;

            rowButton.style.borderBottomWidth = 4f;

            rowButton.style.borderLeftWidth = 2f;



            VisualElement content = new VisualElement();

            content.style.flexDirection = FlexDirection.Row;

            content.style.justifyContent = Justify.SpaceBetween;

            content.style.alignItems = Align.Center;

            content.style.flexGrow = 1f;

            content.style.height = Length.Percent(100f);



            VisualElement accentBar = new VisualElement();

            accentBar.style.width = 12f;

            accentBar.style.height = Length.Percent(100f);

            accentBar.style.marginRight = 18f;

            accentBar.style.borderTopLeftRadius = 20f;

            accentBar.style.borderBottomLeftRadius = 20f;



            Label indexLabel = CreateLabel("00", 24f, new Color(0.57f, 0.86f, 1f, 0.92f), true, TextAnchor.MiddleCenter, useTitleFont: true);

            indexLabel.style.minWidth = 78f;

            indexLabel.style.marginRight = 12f;



            VisualElement textColumn = new VisualElement();

            textColumn.style.flexGrow = 1f;

            textColumn.style.justifyContent = Justify.Center;



            Label nameLabel = CreateLabel(string.Empty, 38f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

            nameLabel.style.flexGrow = 1f;

            nameLabel.style.whiteSpace = WhiteSpace.Normal;

            Label metaLabel = CreateLabel(string.Empty, 18f, new Color(0.70f, 0.86f, 0.96f, 0.88f), true, TextAnchor.MiddleLeft, useTitleFont: false);

            metaLabel.style.letterSpacing = 1.4f;

            metaLabel.style.marginTop = 4f;



            Label scoreLabel = CreateLabel("0%", 34f, new Color(0.54f, 0.92f, 1f, 0.99f), true, TextAnchor.MiddleRight, useTitleFont: true);

            scoreLabel.style.minWidth = 130f;



            textColumn.Add(nameLabel);

            textColumn.Add(metaLabel);

            content.Add(accentBar);

            content.Add(indexLabel);

            content.Add(textColumn);

            content.Add(scoreLabel);

            rowButton.Add(content);

            trackSelectionScrollView.Add(rowButton);



            trackSelectionRows.Add(new TrackSelectionRow

            {

                button = rowButton,

                accentBar = accentBar,

                indexLabel = indexLabel,

                nameLabel = nameLabel,

                metaLabel = metaLabel,

                scoreLabel = scoreLabel

            });

        }

    }



    private void OnTrackRowClicked(int rowIndex)

    {

        if (owner == null)

            return;



        owner.SelectTrackByIndexFromUi(rowIndex);

    }





    private void UpdateGlobalSettings(GuitarGameplaySnapshot snapshot)

    {

        BuildGlobalSettingsFullscreenMenu(snapshot);

    }



    private void BuildGlobalSettingsMenu(GuitarGameplaySnapshot snapshot)

    {

        if (globalSettingsScrollView == null)

            return;



        globalSettingsScrollView.Clear();

        globalSettingsMenuRows.Clear();



        bool inSubmenu = !string.IsNullOrEmpty(snapshot.activeGlobalSettingsCategory);

        globalSettingsTitleLabel.text = inSubmenu ? snapshot.activeGlobalSettingsCategory : "SETTINGS";

        globalSettingsHelpLabel.text = inSubmenu

            ? "Up/Down selects  \u2022  Left/Right changes value  \u2022  Esc goes back"

            : "Up/Down selects  \u2022  Enter opens category  \u2022  Left/Right changes values";



        VisualElement menuList = new VisualElement();

        menuList.style.width = Length.Percent(100f);

        menuList.style.maxWidth = 1280f;

        menuList.style.alignSelf = Align.Center;

        menuList.style.paddingTop = 8f;

        menuList.style.paddingBottom = 12f;

        globalSettingsScrollView.Add(menuList);



        if (!inSubmenu)

        {

            AddGlobalSettingsTopRow(menuList, 0, "Invert Strings", snapshot.selectedGlobalSettingsTopIndex == 0, snapshot.availableSongNames != null, snapshot.activeGlobalSettingsCategory, snapshot, snapshot.selectedGlobalSettingsTopIndex, snapshot.selectedGlobalSettingsItemIndex, snapshot.runtimeSettingsSections);

            AddGlobalSettingsTopRow(menuList, 1, "Render Mode", snapshot.selectedGlobalSettingsTopIndex == 1, snapshot.availableSongNames != null, snapshot.activeGlobalSettingsCategory, snapshot, snapshot.selectedGlobalSettingsTopIndex, snapshot.selectedGlobalSettingsItemIndex, snapshot.runtimeSettingsSections);

            AddGlobalSettingsTopCategoryRow(menuList, 2, "Gameplay", snapshot.selectedGlobalSettingsTopIndex == 2);

            AddGlobalSettingsTopCategoryRow(menuList, 3, "2D Tabs", snapshot.selectedGlobalSettingsTopIndex == 3);

            AddGlobalSettingsTopCategoryRow(menuList, 4, "Highway3D", snapshot.selectedGlobalSettingsTopIndex == 4);

            AddGlobalSettingsTopCategoryRow(menuList, 5, "Visuals", snapshot.selectedGlobalSettingsTopIndex == 5);

            return;

        }



        List<RuntimeSettingSnapshot> items = GetGlobalSettingsMenuItems(snapshot);

        for (int i = 0; i < items.Count; i++)

        {

            RuntimeSettingSnapshot setting = items[i];

            AddGlobalSettingsSettingRow(menuList, setting, i, i == snapshot.selectedGlobalSettingsItemIndex);

        }

    }



    private List<RuntimeSettingSnapshot> GetGlobalSettingsMenuItems(GuitarGameplaySnapshot snapshot)

    {

        if (snapshot.runtimeSettingsSections == null || string.IsNullOrEmpty(snapshot.activeGlobalSettingsCategory))

            return new List<RuntimeSettingSnapshot>();



        return snapshot.runtimeSettingsSections

            .Where(section => string.Equals(CategorizeGlobalSettingsSection(section), snapshot.activeGlobalSettingsCategory, StringComparison.OrdinalIgnoreCase))

            .SelectMany(section => section.settings ?? new List<RuntimeSettingSnapshot>())

            .Where(setting => setting != null && !string.Equals(setting.id, "core.invertStrings", StringComparison.OrdinalIgnoreCase) && !string.Equals(setting.id, "render.mode", StringComparison.OrdinalIgnoreCase))

            .ToList();

    }



    private void AddGlobalSettingsTopRow(VisualElement parent, int index, string title, bool isSelected, bool _, string __, GuitarGameplaySnapshot snapshot, int ___, int ____, List<RuntimeSettingSectionSnapshot> _____)

    {

        string value = index == 0

            ? ResolveGlobalSettingValue(snapshot.runtimeSettingsSections, "core.invertStrings", string.Empty)

            : ResolveGlobalSettingValue(snapshot.runtimeSettingsSections, "render.mode", string.Empty);

        value = index == 0

            ? (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF")

            : value;



        GlobalSettingsMenuRow row = CreateGlobalSettingsMenuRow(title, value, isSelected, showArrows: true, onHover: () => owner?.HoverGlobalSettingsTopSelectionFromUi(index), onActivate: () => owner?.ActivateGlobalSettingsTopSelectionFromUi(index), onLeft: () => owner?.AdjustGlobalSettingsTopValueFromUi(index, -1), onRight: () => owner?.AdjustGlobalSettingsTopValueFromUi(index, 1));

        parent.Add(row.row);

        globalSettingsMenuRows.Add(row);

    }



    private void AddGlobalSettingsTopCategoryRow(VisualElement parent, int index, string title, bool isSelected)

    {

        GlobalSettingsMenuRow row = CreateGlobalSettingsMenuRow(title, "ENTER", isSelected, showArrows: false, onHover: () => owner?.HoverGlobalSettingsTopSelectionFromUi(index), onActivate: () => owner?.ActivateGlobalSettingsTopSelectionFromUi(index), onLeft: null, onRight: null);

        parent.Add(row.row);

        globalSettingsMenuRows.Add(row);

    }



    private void AddGlobalSettingsSettingRow(VisualElement parent, RuntimeSettingSnapshot setting, int index, bool isSelected)

    {

        if (setting == null)

            return;



        string value = FormatGlobalSettingsValue(setting);

        GlobalSettingsMenuRow row = CreateGlobalSettingsMenuRow(setting.label, value, isSelected, showArrows: true, onHover: () => owner?.HoverGlobalSettingsItemSelectionFromUi(index), onActivate: () => owner?.ActivateGlobalSettingsItemSelectionFromUi(index), onLeft: () => owner?.AdjustGlobalSettingsItemValueFromUi(index, -1), onRight: () => owner?.AdjustGlobalSettingsItemValueFromUi(index, 1), metaText: setting.tooltip);

        parent.Add(row.row);

        globalSettingsMenuRows.Add(row);

    }



    private GlobalSettingsMenuRow CreateGlobalSettingsMenuRow(string title, string value, bool isSelected, bool showArrows, Action onHover, Action onActivate, Action onLeft, Action onRight, string metaText = null)

    {

        VisualElement row = new VisualElement();

        row.style.flexDirection = FlexDirection.Column;

        row.style.width = Length.Percent(100f);

        row.style.marginBottom = 18f;

        row.style.paddingLeft = 12f;

        row.style.paddingRight = 12f;

        row.style.paddingTop = 8f;

        row.style.paddingBottom = 8f;



        VisualElement topLine = new VisualElement();

        topLine.style.flexDirection = FlexDirection.Row;

        topLine.style.alignItems = Align.Center;

        topLine.style.justifyContent = Justify.SpaceBetween;

        topLine.style.width = Length.Percent(100f);



        Button mainButton = new Button(() => onActivate?.Invoke());

        mainButton.text = title;

        mainButton.focusable = false;

        mainButton.style.flexGrow = 1f;

        mainButton.style.height = 84f;

        mainButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        mainButton.style.color = isSelected ? Color.white : new Color(0.86f, 0.89f, 0.92f, 1f);

        mainButton.style.fontSize = 46f;

        mainButton.style.unityFontDefinition = modernUiFontDefinition;

        mainButton.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainButton.style.unityTextAlign = TextAnchor.MiddleLeft;

        mainButton.style.paddingLeft = 0f;

        mainButton.style.paddingRight = 16f;

        mainButton.style.borderTopWidth = 0f;

        mainButton.style.borderRightWidth = 0f;

        mainButton.style.borderBottomWidth = 0f;

        mainButton.style.borderLeftWidth = 0f;

        mainButton.style.backgroundImage = StyleKeyword.None;

        mainButton.RegisterCallback<MouseEnterEvent>(_ => onHover?.Invoke());

        AttachWheelScrolling(mainButton, globalSettingsScrollView);



        VisualElement valueWrap = new VisualElement();

        valueWrap.style.flexDirection = FlexDirection.Row;

        valueWrap.style.alignItems = Align.Center;

        valueWrap.style.justifyContent = Justify.FlexEnd;

        valueWrap.style.minWidth = 360f;



        Button leftButton = null;

        Button rightButton = null;

        if (showArrows)

        {

            leftButton = CreateGlobalSettingsArrowButton("\u2039", onLeft);

            rightButton = CreateGlobalSettingsArrowButton("\u203A", onRight);

            valueWrap.Add(leftButton);

        }



        Label valueLabel = CreateLabel(value, 40f, isSelected ? LibraryPrimaryColor : new Color(0.72f, 0.77f, 0.82f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        valueLabel.style.unityFontDefinition = modernUiFontDefinition;

        valueLabel.style.minWidth = 180f;

        valueLabel.style.unityTextAlign = TextAnchor.MiddleCenter;

        valueWrap.Add(valueLabel);



        if (showArrows)

        {

            valueWrap.Add(rightButton);

        }



        topLine.Add(mainButton);

        topLine.Add(valueWrap);

        row.Add(topLine);



        Label metaLabel = null;

        if (!string.IsNullOrWhiteSpace(metaText))

        {

            metaLabel = CreateLabel(metaText, 20f, new Color(0.56f, 0.63f, 0.70f, 0.96f), false, TextAnchor.MiddleLeft, useTitleFont: false);

            metaLabel.style.unityFontDefinition = modernUiFontDefinition;

            metaLabel.style.whiteSpace = WhiteSpace.Normal;

            metaLabel.style.marginTop = 4f;

            row.Add(metaLabel);

        }



        ConfigureGlobalSettingsMenuRowHover(row, mainButton, valueLabel, isSelected, onHover);



        return new GlobalSettingsMenuRow

        {

            row = row,

            titleLabel = null,

            valueLabel = valueLabel,

            leftButton = leftButton,

            rightButton = rightButton,

            metaLabel = metaLabel

        };

    }



    private static string ResolveGlobalSettingValue(List<RuntimeSettingSectionSnapshot> sections, string settingId, string fallback)

    {

        if (sections == null || string.IsNullOrEmpty(settingId))

            return fallback;



        foreach (RuntimeSettingSectionSnapshot section in sections)

        {

            if (section?.settings == null)

                continue;



            RuntimeSettingSnapshot match = section.settings.FirstOrDefault(setting => setting != null && string.Equals(setting.id, settingId, StringComparison.OrdinalIgnoreCase));

            if (match != null)

                return match.value ?? fallback;

        }



        return fallback;

    }



    private static string FormatGlobalSettingsValue(RuntimeSettingSnapshot setting)

    {

        if (setting == null)

            return string.Empty;



        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))

            return string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF";



        return setting.value ?? string.Empty;

    }



    private Button CreateGlobalSettingsArrowButton(string text, Action onClick)

    {

        Button button = new Button(() => onClick?.Invoke()) { text = text };

        button.focusable = false;

        button.style.width = 72f;

        button.style.height = 72f;

        button.style.marginLeft = 10f;

        button.style.marginRight = 10f;

        button.style.backgroundColor = new Color(0.15f, 0.17f, 0.20f, 1f);

        button.style.color = new Color(0.90f, 0.94f, 0.97f, 1f);

        button.style.fontSize = 34f;

        button.style.unityFontDefinition = modernUiFontDefinition;

        button.style.borderTopWidth = 1f;

        button.style.borderRightWidth = 1f;

        button.style.borderBottomWidth = 1f;

        button.style.borderLeftWidth = 1f;

        button.style.borderTopColor = new Color(0.20f, 0.24f, 0.28f, 1f);

        button.style.borderRightColor = new Color(0.20f, 0.24f, 0.28f, 1f);

        button.style.borderBottomColor = new Color(0.20f, 0.24f, 0.28f, 1f);

        button.style.borderLeftColor = new Color(0.20f, 0.24f, 0.28f, 1f);

        button.style.borderTopLeftRadius = 10f;

        button.style.borderTopRightRadius = 10f;

        button.style.borderBottomLeftRadius = 10f;

        button.style.borderBottomRightRadius = 10f;

        ConfigureInteractiveButtonHover(button, new Color(0.15f, 0.17f, 0.20f, 1f), new Color(0.90f, 0.94f, 0.97f, 1f));

        return button;

    }



    private static void ConfigureGlobalSettingsMenuRowHover(VisualElement row, Button mainButton, Label valueLabel, bool isSelected, Action onHover)

    {

        void ApplyState(bool hovered)

        {

            bool active = hovered || isSelected;

            row.style.translate = active ? new Translate(-6f, 0f) : new Translate(0f, 0f);

            mainButton.style.color = active ? Color.white : new Color(0.86f, 0.89f, 0.92f, 1f);

            valueLabel.style.color = active ? LibraryPrimaryColor : new Color(0.72f, 0.77f, 0.82f, 1f);

        }



        row.RegisterCallback<MouseEnterEvent>(_ =>

        {

            onHover?.Invoke();

            ApplyState(true);

        });

        row.RegisterCallback<MouseLeaveEvent>(_ => ApplyState(false));

        ApplyState(false);

    }



    private void BuildGlobalSettingsFullscreenMenu(GuitarGameplaySnapshot snapshot)

    {

        if (globalSettingsScrollView == null)

            return;



        string fullscreenSignature = BuildGlobalSettingsFullscreenSignature(snapshot);

        bool needsRebuild = fullscreenSignature != globalSettingsFullscreenSignature || globalSettingsMenuRows.Count == 0;

        globalSettingsScrollOffset = globalSettingsScrollView.scrollOffset;



        if (resetDefaultsButton != null)

            resetDefaultsButton.style.display = DisplayStyle.None;



        VisualElement globalSettingsDock = globalSettingsCard?.Q<VisualElement>("primary-actions-dock");

        if (globalSettingsDock != null)

            globalSettingsDock.style.display = DisplayStyle.None;



        VisualElement globalSettingsDockSpacer = globalSettingsCard?.Q<VisualElement>("primary-actions-dock-spacer");

        if (globalSettingsDockSpacer != null)

            globalSettingsDockSpacer.style.display = DisplayStyle.None;



        globalSettingsCard.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        globalSettingsCard.style.borderTopWidth = 0f;

        globalSettingsCard.style.borderRightWidth = 0f;

        globalSettingsCard.style.borderBottomWidth = 0f;

        globalSettingsCard.style.borderLeftWidth = 0f;

        globalSettingsCard.style.maxHeight = StyleKeyword.None;

        globalSettingsCard.style.paddingLeft = 0f;

        globalSettingsCard.style.paddingRight = 0f;

        globalSettingsCard.style.paddingTop = 0f;

        globalSettingsCard.style.paddingBottom = 0f;



        bool inSubmenu = !string.IsNullOrEmpty(snapshot.activeGlobalSettingsCategory);

        globalSettingsTitleLabel.text = inSubmenu ? snapshot.activeGlobalSettingsCategory : "SETTINGS";

        globalSettingsHelpLabel.text = inSubmenu

            ? "UP/DOWN NAVIGATES   LEFT/RIGHT CHANGES VALUES   ENTER TOGGLES OR CYCLES   ESC GOES BACK"

            : "UP/DOWN NAVIGATES   LEFT/RIGHT CHANGES VALUES   ENTER OPENS A CATEGORY   ESC RETURNS";



        if (!needsRebuild)

        {

            int selectedIndexNoRebuild = inSubmenu

                ? Mathf.Clamp(snapshot.selectedGlobalSettingsItemIndex, 0, Mathf.Max(0, globalSettingsMenuRows.Count - 1))

                : Mathf.Clamp(snapshot.selectedGlobalSettingsTopIndex, 0, Mathf.Max(0, globalSettingsMenuRows.Count - 1));

            string selectionSignature = $"{(inSubmenu ? "submenu" : "top")}:{selectedIndexNoRebuild}:{snapshot.activeGlobalSettingsCategory}";

            if (Time.unscaledTime >= globalSettingsManualScrollUntil && selectionSignature != lastGlobalSettingsCenteredSelectionSignature)

            {

                lastGlobalSettingsCenteredSelectionSignature = selectionSignature;

                ScrollGlobalSettingsSelectionIntoView(inSubmenu, selectedIndexNoRebuild);

            }

            return;

        }



        globalSettingsFullscreenSignature = fullscreenSignature;

        globalSettingsScrollView.Clear();

        globalSettingsMenuRows.Clear();



        VisualElement menuList = new VisualElement();

        menuList.style.width = Length.Percent(100f);

        menuList.style.maxWidth = 1500f;

        menuList.style.alignSelf = Align.Center;

        menuList.style.paddingTop = 18f;

        menuList.style.paddingBottom = 24f;

        menuList.style.paddingLeft = 64f;

        menuList.style.paddingRight = 64f;

        globalSettingsScrollView.Add(menuList);

        globalSettingsScrollView.scrollOffset = globalSettingsScrollOffset;



        if (!inSubmenu)

        {

            AddGlobalSettingsFullscreenTopValueRow(menuList, snapshot, 0, "Invert Strings");

            AddGlobalSettingsFullscreenTopValueRow(menuList, snapshot, 1, "Render Mode");

            AddGlobalSettingsFullscreenTopCategoryRow(menuList, 2, "Gameplay", snapshot.selectedGlobalSettingsTopIndex == 2);

            AddGlobalSettingsFullscreenTopCategoryRow(menuList, 3, "2D Tabs", snapshot.selectedGlobalSettingsTopIndex == 3);

            AddGlobalSettingsFullscreenTopCategoryRow(menuList, 4, "Highway3D", snapshot.selectedGlobalSettingsTopIndex == 4);

            AddGlobalSettingsFullscreenTopCategoryRow(menuList, 5, "Visuals", snapshot.selectedGlobalSettingsTopIndex == 5);

            AddGlobalSettingsFullscreenTopCategoryRow(menuList, 6, "Reset Settings", snapshot.selectedGlobalSettingsTopIndex == 6, "DEFAULTS");

            return;

        }



        List<RuntimeSettingSnapshot> items = GetGlobalSettingsMenuItems(snapshot);

        for (int i = 0; i < items.Count; i++)

        {

            RuntimeSettingSnapshot setting = items[i];

            if (setting == null)

                continue;



            int rowIndex = i;

            string value = FormatGlobalSettingsValue(setting);

            GlobalSettingsMenuRow row = CreateGlobalSettingsTextMenuRow(

                setting.label,

                value,

                rowIndex == snapshot.selectedGlobalSettingsItemIndex,

                showArrows: true,

                metaText: setting.tooltip,

                onHover: null,

                onActivate: () => owner?.ActivateGlobalSettingsItemSelectionFromUi(rowIndex),

                onLeft: () => owner?.AdjustGlobalSettingsItemValueFromUi(rowIndex, -1),

                onRight: () => owner?.AdjustGlobalSettingsItemValueFromUi(rowIndex, 1));

            menuList.Add(row.row);

            globalSettingsMenuRows.Add(row);

        }



        int selectedIndex = inSubmenu

            ? Mathf.Clamp(snapshot.selectedGlobalSettingsItemIndex, 0, Mathf.Max(0, globalSettingsMenuRows.Count - 1))

            : Mathf.Clamp(snapshot.selectedGlobalSettingsTopIndex, 0, Mathf.Max(0, globalSettingsMenuRows.Count - 1));

        string rebuiltSelectionSignature = $"{(inSubmenu ? "submenu" : "top")}:{selectedIndex}:{snapshot.activeGlobalSettingsCategory}";

        if (Time.unscaledTime >= globalSettingsManualScrollUntil)

        {

            lastGlobalSettingsCenteredSelectionSignature = rebuiltSelectionSignature;

            ScrollGlobalSettingsSelectionIntoView(inSubmenu, selectedIndex);

        }

    }



    private static string BuildGlobalSettingsFullscreenSignature(GuitarGameplaySnapshot snapshot)

    {

        if (snapshot == null)

            return string.Empty;



        IEnumerable<string> sectionBits = (snapshot.runtimeSettingsSections ?? new List<RuntimeSettingSectionSnapshot>())

            .Select(section =>

            {

                string sectionTitle = section?.title ?? string.Empty;

                string settings = string.Join("|", (section?.settings ?? new List<RuntimeSettingSnapshot>())

                    .Where(setting => setting != null)

                    .Select(setting => $"{setting.id}={setting.value}"));

                return $"{sectionTitle}[{settings}]";

            });



        return string.Join("||", new[]

        {

            snapshot.activeGlobalSettingsCategory ?? string.Empty,

            snapshot.selectedGlobalSettingsTopIndex.ToString(CultureInfo.InvariantCulture),

            snapshot.selectedGlobalSettingsItemIndex.ToString(CultureInfo.InvariantCulture),

            string.Join("||", sectionBits)

        });

    }



    private void ScrollGlobalSettingsSelectionIntoView(bool inSubmenu, int selectedIndex)

    {

        if (globalSettingsScrollView == null || selectedIndex < 0 || selectedIndex >= globalSettingsMenuRows.Count)

            return;



        VisualElement target = globalSettingsMenuRows[selectedIndex]?.row;

        if (target == null)

            return;



        globalSettingsScrollView.schedule.Execute(() =>

        {

            if (globalSettingsScrollView == null || target.panel == null || globalSettingsScrollView.contentViewport == null || globalSettingsScrollView.contentContainer == null)

                return;



            globalSettingsScrollView.ScrollTo(target);



            globalSettingsScrollView.schedule.Execute(() =>

            {

                if (globalSettingsScrollView == null || target.panel == null || globalSettingsScrollView.contentViewport == null || globalSettingsScrollView.contentContainer == null)

                    return;



                const float visibilityPadding = 24f;

                Rect viewport = globalSettingsScrollView.contentViewport.worldBound;

                Rect rowBounds = target.worldBound;

                float currentOffset = globalSettingsScrollView.scrollOffset.y;

                float targetOffset = currentOffset;



                if (rowBounds.yMin < viewport.yMin + visibilityPadding)

                {

                    float delta = (viewport.yMin + visibilityPadding) - rowBounds.yMin;

                    targetOffset = Mathf.Max(0f, currentOffset - delta);

                }

                else if (rowBounds.yMax > viewport.yMax - visibilityPadding)

                {

                    float delta = rowBounds.yMax - (viewport.yMax - visibilityPadding);

                    targetOffset = Mathf.Max(0f, currentOffset + delta);

                }



                if (!Mathf.Approximately(targetOffset, currentOffset))

                    globalSettingsScrollView.scrollOffset = new Vector2(globalSettingsScrollView.scrollOffset.x, targetOffset);



                globalSettingsScrollOffset = globalSettingsScrollView.scrollOffset;

            }).ExecuteLater(0);

        }).ExecuteLater(0);

    }



    private void AddGlobalSettingsFullscreenTopValueRow(VisualElement parent, GuitarGameplaySnapshot snapshot, int index, string title)

    {

        string value = index == 0

            ? ResolveGlobalSettingValue(snapshot.runtimeSettingsSections, "core.invertStrings", string.Empty)

            : ResolveGlobalSettingValue(snapshot.runtimeSettingsSections, "render.mode", string.Empty);

        value = index == 0

            ? (string.Equals(value, "true", StringComparison.OrdinalIgnoreCase) ? "ON" : "OFF")

            : value;



        GlobalSettingsMenuRow row = CreateGlobalSettingsTextMenuRow(

            title,

            value,

            snapshot.selectedGlobalSettingsTopIndex == index,

            showArrows: true,

            metaText: null,

            onHover: null,

            onActivate: () => owner?.ActivateGlobalSettingsTopSelectionFromUi(index),

            onLeft: () => owner?.AdjustGlobalSettingsTopValueFromUi(index, -1),

            onRight: () => owner?.AdjustGlobalSettingsTopValueFromUi(index, 1));

        parent.Add(row.row);

        globalSettingsMenuRows.Add(row);

    }



    private void AddGlobalSettingsFullscreenTopCategoryRow(VisualElement parent, int index, string title, bool isSelected, string value = "ENTER")

    {

        GlobalSettingsMenuRow row = CreateGlobalSettingsTextMenuRow(

            title,

            value,

            isSelected,

            showArrows: false,

            metaText: null,

            onHover: null,

            onActivate: () => owner?.ActivateGlobalSettingsTopSelectionFromUi(index),

            onLeft: null,

            onRight: null);

        parent.Add(row.row);

        globalSettingsMenuRows.Add(row);

    }



    private GlobalSettingsMenuRow CreateGlobalSettingsTextMenuRow(string title, string value, bool isSelected, bool showArrows, string metaText, Action onHover, Action onActivate, Action onLeft, Action onRight)

    {

        VisualElement row = new VisualElement();

        row.style.flexDirection = FlexDirection.Column;

        row.style.width = Length.Percent(100f);

        row.style.marginBottom = 10f;

        row.style.paddingTop = 8f;

        row.style.paddingBottom = 8f;



        VisualElement topLine = new VisualElement();

        topLine.style.flexDirection = FlexDirection.Row;

        topLine.style.alignItems = Align.Center;

        topLine.style.justifyContent = Justify.Center;

        topLine.style.width = Length.Percent(100f);

        topLine.style.minHeight = 92f;

        topLine.style.position = Position.Relative;



        Button leftButton = null;

        Button rightButton = null;

        if (showArrows)

        {

            leftButton = CreateGlobalSettingsTextArrowButton("<", onLeft);

            rightButton = CreateGlobalSettingsTextArrowButton(">", onRight);

            leftButton.style.position = Position.Absolute;

            leftButton.style.left = -74f;

            leftButton.style.top = 0f;

            leftButton.style.bottom = 0f;

            leftButton.style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;

            rightButton.style.position = Position.Absolute;

            rightButton.style.right = -74f;

            rightButton.style.top = 0f;

            rightButton.style.bottom = 0f;

            rightButton.style.display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;

            topLine.Add(leftButton);

        }



        VisualElement contentBox = new VisualElement();

        contentBox.style.width = Length.Percent(100f);

        contentBox.style.maxWidth = 1180f;

        contentBox.style.minHeight = !string.IsNullOrWhiteSpace(metaText) ? 132f : 92f;

        contentBox.style.paddingLeft = 28f;

        contentBox.style.paddingRight = 28f;

        contentBox.style.paddingTop = !string.IsNullOrWhiteSpace(metaText) ? 16f : 0f;

        contentBox.style.paddingBottom = !string.IsNullOrWhiteSpace(metaText) ? 16f : 0f;

        contentBox.style.flexDirection = FlexDirection.Row;

        contentBox.style.alignItems = Align.Center;

        contentBox.style.justifyContent = Justify.SpaceBetween;

        contentBox.style.borderTopWidth = 3f;

        contentBox.style.borderRightWidth = 3f;

        contentBox.style.borderBottomWidth = 3f;

        contentBox.style.borderLeftWidth = 3f;

        contentBox.style.borderTopLeftRadius = 12f;

        contentBox.style.borderTopRightRadius = 12f;

        contentBox.style.borderBottomLeftRadius = 12f;

        contentBox.style.borderBottomRightRadius = 12f;

        contentBox.style.alignSelf = Align.Center;



        VisualElement textColumn = new VisualElement();

        textColumn.style.flexGrow = 1f;

        textColumn.style.flexShrink = 1f;

        textColumn.style.flexDirection = FlexDirection.Column;

        textColumn.style.justifyContent = Justify.Center;

        textColumn.style.alignItems = Align.FlexStart;

        textColumn.style.minHeight = 0f;



        Button mainButton = new Button(() => onActivate?.Invoke());

        mainButton.text = title;

        mainButton.focusable = false;

        mainButton.style.flexGrow = 0f;

        mainButton.style.flexShrink = 1f;

        mainButton.style.width = Length.Percent(100f);

        mainButton.style.height = 86f;

        mainButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        mainButton.style.backgroundImage = StyleKeyword.None;

        mainButton.style.fontSize = 54f;

        mainButton.style.unityFontDefinition = modernUiFontDefinition;

        mainButton.style.unityFontStyleAndWeight = FontStyle.Bold;

        mainButton.style.unityTextAlign = TextAnchor.MiddleLeft;

        mainButton.style.paddingLeft = 0f;

        mainButton.style.paddingRight = 24f;

        mainButton.style.borderTopWidth = 0f;

        mainButton.style.borderRightWidth = 0f;

        mainButton.style.borderBottomWidth = 0f;

        mainButton.style.borderLeftWidth = 0f;

        mainButton.RegisterCallback<MouseEnterEvent>(_ => onHover?.Invoke());

        AttachGlobalSettingsWheelScrolling(mainButton);



        Label valueLabel = CreateLabel(value, 36f, isSelected ? LibraryPrimaryColor : new Color(0.72f, 0.77f, 0.82f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: false);

        valueLabel.style.unityFontDefinition = modernUiFontDefinition;

        valueLabel.style.minWidth = showArrows ? 240f : 180f;

        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;

        valueLabel.style.flexShrink = 0f;

        valueLabel.style.marginLeft = 24f;



        Label metaLabel = null;

        if (!string.IsNullOrWhiteSpace(metaText))

        {

            metaLabel = CreateLabel(metaText, 28f, new Color(0.66f, 0.70f, 0.75f, 0.98f), false, TextAnchor.MiddleLeft, useTitleFont: false);

            metaLabel.style.unityFontDefinition = modernUiFontDefinition;

            metaLabel.style.whiteSpace = WhiteSpace.Normal;

            metaLabel.style.unityTextAlign = TextAnchor.MiddleLeft;

            metaLabel.style.marginTop = 4f;

            metaLabel.style.marginLeft = 0f;

            metaLabel.style.marginRight = 0f;

            metaLabel.style.flexShrink = 1f;

        }



        textColumn.Add(mainButton);

        if (metaLabel != null)

            textColumn.Add(metaLabel);



        contentBox.Add(textColumn);

        contentBox.Add(valueLabel);

        topLine.Add(contentBox);



        if (showArrows)

        {

            topLine.Add(rightButton);

        }



        row.Add(topLine);



        ConfigureGlobalSettingsTextMenuRowHover(row, contentBox, mainButton, valueLabel, leftButton, rightButton, isSelected, onHover);

        AttachGlobalSettingsWheelScrolling(row);

        AttachGlobalSettingsWheelScrolling(contentBox);

        AttachGlobalSettingsWheelScrolling(textColumn);

        AttachGlobalSettingsWheelScrolling(valueLabel);

        AttachGlobalSettingsWheelScrolling(topLine);

        if (metaLabel != null)

            AttachGlobalSettingsWheelScrolling(metaLabel);



        return new GlobalSettingsMenuRow

        {

            row = row,

            titleLabel = null,

            valueLabel = valueLabel,

            leftButton = leftButton,

            rightButton = rightButton,

            metaLabel = metaLabel

        };

    }



    private Button CreateGlobalSettingsTextArrowButton(string text, Action onClick)

    {

        Button button = new Button(() => onClick?.Invoke()) { text = text };

        button.focusable = false;

        button.style.width = 110f;

        button.style.height = 110f;

        button.style.marginLeft = 0f;

        button.style.marginRight = 0f;

        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        button.style.backgroundImage = StyleKeyword.None;

        button.style.color = new Color(0.90f, 0.94f, 0.97f, 1f);

        button.style.fontSize = 76f;

        button.style.unityFontDefinition = modernUiFontDefinition;

        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        button.style.alignItems = Align.Center;

        button.style.justifyContent = Justify.Center;

        button.style.unityTextAlign = TextAnchor.MiddleCenter;

        button.style.borderTopWidth = 0f;

        button.style.borderRightWidth = 0f;

        button.style.borderBottomWidth = 0f;

        button.style.borderLeftWidth = 0f;

        button.RegisterCallback<MouseEnterEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1.10f, 1.10f, 1f));

            button.style.color = LibraryPrimaryColor;

        });

        button.RegisterCallback<MouseLeaveEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

            button.style.color = new Color(0.90f, 0.94f, 0.97f, 1f);

        });

        return button;

    }



    private static void ConfigureGlobalSettingsTextMenuRowHover(VisualElement row, VisualElement contentBox, Button mainButton, Label valueLabel, Button leftButton, Button rightButton, bool isSelected, Action onHover)

    {

        Color idleBorder = new Color(0.42f, 0.44f, 0.47f, 0.95f);

        Color activeBorder = LibraryConfirmedSongColor;

        Color idleText = new Color(0.86f, 0.89f, 0.92f, 1f);

        Color activeText = LibraryConfirmedSongColor;



        void ApplyState(bool hovered)

        {

            bool active = hovered || isSelected;

            row.style.translate = active ? new Translate(-6f, 0f) : new Translate(0f, 0f);

            contentBox.style.borderTopColor = active ? activeBorder : idleBorder;

            contentBox.style.borderRightColor = active ? activeBorder : idleBorder;

            contentBox.style.borderBottomColor = active ? activeBorder : idleBorder;

            contentBox.style.borderLeftColor = active ? activeBorder : idleBorder;

            mainButton.style.color = active ? activeText : idleText;

            valueLabel.style.color = active ? activeText : idleText;

            if (leftButton != null)

            {

                leftButton.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

                leftButton.style.color = active ? activeText : idleText;

            }

            if (rightButton != null)

            {

                rightButton.style.display = active ? DisplayStyle.Flex : DisplayStyle.None;

                rightButton.style.color = active ? activeText : idleText;

            }

        }



        row.RegisterCallback<MouseEnterEvent>(_ =>

        {

            onHover?.Invoke();

            ApplyState(true);

        });

        row.RegisterCallback<MouseLeaveEvent>(_ => ApplyState(false));

        ApplyState(false);

    }



    private void AttachGlobalSettingsWheelScrolling(VisualElement element)

    {

        if (element == null || globalSettingsScrollView == null)

            return;



        element.RegisterCallback<WheelEvent>(evt =>

        {

            globalSettingsManualScrollUntil = Time.unscaledTime + 0.8f;

            ApplyWheelScroll(globalSettingsScrollView, evt);

            globalSettingsScrollOffset = globalSettingsScrollView.scrollOffset;

        }, TrickleDown.TrickleDown);

    }



    private bool BuildGlobalSettingsUi(List<RuntimeSettingSectionSnapshot> sections)

    {

        if (sections == null)

            return false;



        string signature = BuildGlobalSettingsLayoutSignature(sections);

        if (signature == globalSettingsLayoutSignature && globalSettingInputs.Count > 0)

            return false;



        globalSettingsScrollOffset = globalSettingsScrollView != null ? globalSettingsScrollView.scrollOffset : globalSettingsScrollOffset;

        globalSettingsLayoutSignature = signature;

        globalSettingsScrollView.Clear();

        globalSettingInputs.Clear();

        globalSettingValueLabels.Clear();

        globalSettingsColumns.Clear();



        VisualElement columnsWrapper = new VisualElement();

        columnsWrapper.style.flexDirection = FlexDirection.Row;

        columnsWrapper.style.alignItems = Align.FlexStart;

        columnsWrapper.style.justifyContent = Justify.SpaceBetween;

        columnsWrapper.style.flexWrap = Wrap.NoWrap;

        columnsWrapper.style.minWidth = 1380f;

        columnsWrapper.style.width = Length.Percent(100f);



        AddGlobalSettingsColumn(columnsWrapper, "Gameplay Mechanics", addRightSpacing: true);

        AddGlobalSettingsColumn(columnsWrapper, "Tabs Visuals", addRightSpacing: true);

        AddGlobalSettingsColumn(columnsWrapper, "Highway 3D", addRightSpacing: true);

        AddGlobalSettingsColumn(columnsWrapper, "General Visuals", addRightSpacing: false);



        globalSettingsScrollView.Add(columnsWrapper);



        foreach (RuntimeSettingSectionSnapshot section in sections)

        {

            if (section == null)

                continue;



            VisualElement sectionCard = new VisualElement();

            sectionCard.style.marginBottom = 14f;

            sectionCard.style.paddingLeft = 18f;

            sectionCard.style.paddingRight = 18f;

            sectionCard.style.paddingTop = 16f;

            sectionCard.style.paddingBottom = 16f;

            StyleCard(sectionCard, new Color(0.13f, 0.14f, 0.16f, 1f), 14f);

            sectionCard.style.borderTopWidth = 1f;

            sectionCard.style.borderRightWidth = 1f;

            sectionCard.style.borderBottomWidth = 1f;

            sectionCard.style.borderLeftWidth = 1f;

            Color sectionBorderColor = new Color(0.20f, 0.24f, 0.28f, 1f);

            sectionCard.style.borderTopColor = sectionBorderColor;

            sectionCard.style.borderRightColor = sectionBorderColor;

            sectionCard.style.borderBottomColor = sectionBorderColor;

            sectionCard.style.borderLeftColor = sectionBorderColor;



            Label sectionTitle = CreateLabel(section.title, 28f, LibraryPrimaryColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

            sectionTitle.style.unityFontDefinition = modernUiFontDefinition;

            sectionTitle.AddToClassList("global-section-title");

            sectionTitle.style.marginBottom = 10f;

            sectionTitle.style.whiteSpace = WhiteSpace.Normal;

            sectionTitle.style.flexShrink = 1f;

            sectionCard.Add(sectionTitle);



            if (section.settings != null)

            {

                foreach (RuntimeSettingSnapshot setting in section.settings)

                    sectionCard.Add(CreateGlobalSettingRow(setting));

            }



            string category = CategorizeGlobalSettingsSection(section);

            if (globalSettingsColumns.TryGetValue(category, out VisualElement column))

                column.Add(sectionCard);

            else

                globalSettingsScrollView.Add(sectionCard);

        }



        ApplyResponsiveSizing(force: true);

        return true;

    }



    private void RestoreGlobalSettingsScrollOffset()

    {

        if (globalSettingsScrollView == null)

            return;



        Vector2 preservedOffset = globalSettingsScrollOffset;

        globalSettingsScrollView.schedule.Execute(() =>

        {

            if (globalSettingsScrollView == null)

                return;



            globalSettingsScrollView.scrollOffset = preservedOffset;

        }).ExecuteLater(0);

    }



    private void PreserveGlobalSettingsScrollOffset()

    {

        if (globalSettingsScrollView == null)

            return;



        globalSettingsScrollOffset = globalSettingsScrollView.scrollOffset;

        RestoreGlobalSettingsScrollOffset();

    }



    private void AddGlobalSettingsColumn(VisualElement parent, string title, bool addRightSpacing)

    {

        VisualElement column = new VisualElement();

        column.style.flexGrow = 1f;

        column.style.flexShrink = 1f;

        column.style.flexBasis = 0f;

        column.style.minWidth = 420f;

        if (addRightSpacing)

            column.style.marginRight = 14f;



        Label columnTitle = CreateLabel(title, 30f, LibraryPrimaryColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        columnTitle.style.unityFontDefinition = modernUiFontDefinition;

        columnTitle.style.marginBottom = 10f;

        columnTitle.style.unityTextAlign = TextAnchor.MiddleCenter;

        columnTitle.style.paddingTop = 6f;

        columnTitle.style.paddingBottom = 6f;

        columnTitle.style.backgroundColor = new Color(0.15f, 0.17f, 0.20f, 1f);

        columnTitle.style.borderTopLeftRadius = 8f;

        columnTitle.style.borderTopRightRadius = 8f;

        columnTitle.style.borderBottomLeftRadius = 8f;

        columnTitle.style.borderBottomRightRadius = 8f;

        columnTitle.style.borderTopWidth = 1f;

        columnTitle.style.borderRightWidth = 1f;

        columnTitle.style.borderBottomWidth = 1f;

        columnTitle.style.borderLeftWidth = 1f;

        Color titleBorder = new Color(0.20f, 0.24f, 0.28f, 1f);

        columnTitle.style.borderTopColor = titleBorder;

        columnTitle.style.borderRightColor = titleBorder;

        columnTitle.style.borderBottomColor = titleBorder;

        columnTitle.style.borderLeftColor = titleBorder;

        column.Add(columnTitle);



        parent.Add(column);

        globalSettingsColumns[title] = column;

    }



    private static string CategorizeGlobalSettingsSection(RuntimeSettingSectionSnapshot section)

    {

        string normalizedTitle = section.title?.ToLowerInvariant() ?? string.Empty;

        List<RuntimeSettingSnapshot> sectionSettings = section.settings;



        if (normalizedTitle.Contains("timing") || normalizedTitle.Contains("forgiveness") || normalizedTitle.Contains("settings") || IsSectionIdPrefix(sectionSettings, "core.") || IsSectionIdPrefix(sectionSettings, "timing."))

            return "Gameplay";



        if (normalizedTitle.Contains("tab") || normalizedTitle.Contains("layout") || IsSectionIdPrefix(sectionSettings, "layout."))

            return "2D Tabs";



        if (normalizedTitle.Contains("highway") || IsSectionIdPrefix(sectionSettings, "highway.") || IsSectionIdPrefix(sectionSettings, "render."))

            return "Highway3D";



        if (normalizedTitle.Contains("visual") || normalizedTitle.Contains("color") || normalizedTitle.Contains("background") || IsSectionIdPrefix(sectionSettings, "fx.") || IsSectionIdPrefix(sectionSettings, "bg."))

            return "Visuals";



        return "Gameplay";

    }



    private static bool IsSectionIdPrefix(List<RuntimeSettingSnapshot> settings, string prefix)

    {

        if (settings == null || string.IsNullOrEmpty(prefix))

            return false;



        return settings.Any(setting =>

            setting != null &&

            !string.IsNullOrEmpty(setting.id) &&

            setting.id.StartsWith(prefix, StringComparison.OrdinalIgnoreCase));

    }



    private VisualElement CreateGlobalSettingRow(RuntimeSettingSnapshot setting)

    {

        VisualElement row = new VisualElement();

        row.style.marginBottom = 12f;

        row.style.paddingLeft = 14f;

        row.style.paddingRight = 14f;

        row.style.paddingTop = 12f;

        row.style.paddingBottom = 12f;

        Color rowBaseColor = new Color(0.12f, 0.13f, 0.15f, 1f);

        row.style.backgroundColor = rowBaseColor;

        row.style.borderTopWidth = 1f;

        row.style.borderRightWidth = 1f;

        row.style.borderBottomWidth = 1f;

        row.style.borderLeftWidth = 1f;

        Color rowBorderColor = new Color(0.18f, 0.20f, 0.23f, 1f);

        row.style.borderTopColor = rowBorderColor;

        row.style.borderRightColor = rowBorderColor;

        row.style.borderBottomColor = rowBorderColor;

        row.style.borderLeftColor = rowBorderColor;

        row.style.borderTopLeftRadius = 12f;

        row.style.borderTopRightRadius = 12f;

        row.style.borderBottomLeftRadius = 12f;

        row.style.borderBottomRightRadius = 12f;

        row.style.width = Length.Percent(100f);

        ConfigureInteractiveCardHover(row, rowBaseColor);



        Label label = CreateLabel(setting.label, 30f, new Color(0.96f, 0.98f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        label.style.unityFontDefinition = modernUiFontDefinition;

        label.AddToClassList("global-setting-title");

        label.tooltip = setting.tooltip;

        label.style.whiteSpace = WhiteSpace.Normal;

        label.style.flexShrink = 1f;

        row.Add(label);



        Label help = CreateLabel(setting.tooltip, 22f, new Color(0.69f, 0.78f, 0.86f, 0.92f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        help.style.unityFontDefinition = modernUiFontDefinition;

        help.AddToClassList("global-setting-help");

        help.style.marginTop = 2f;

        help.style.marginBottom = 8f;

        help.tooltip = setting.tooltip;

        help.style.whiteSpace = WhiteSpace.Normal;

        help.style.flexShrink = 1f;

        row.Add(help);



        VisualElement input = null;

        if (string.Equals(setting.valueType, "bool", StringComparison.OrdinalIgnoreCase))

        {

            Toggle toggle = new Toggle();

            toggle.value = string.Equals(setting.value, "true", StringComparison.OrdinalIgnoreCase);

            toggle.focusable = false;

            toggle.style.height = 48f;

            toggle.style.marginTop = 4f;

            toggle.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());

            toggle.RegisterValueChangedCallback(evt =>

            {

                if (suppressCallbacks)

                    return;



                PreserveGlobalSettingsScrollOffset();

                owner?.SetGlobalRuntimeSettingFromUi(setting.id, evt.newValue ? "true" : "false");

            });

            input = toggle;

        }

        else if (string.Equals(setting.valueType, "enum", StringComparison.OrdinalIgnoreCase))

        {

            EnumCycleControl enumCycle = new EnumCycleControl(setting.enumOptions, setting.value, CreateLabel, CreateActionButton);

            enumCycle.focusable = false;

            enumCycle.style.marginTop = 4f;

            enumCycle.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());

            enumCycle.OnValueChanged += value =>

            {

                if (!suppressCallbacks)

                {

                    PreserveGlobalSettingsScrollOffset();

                    owner?.SetGlobalRuntimeSettingFromUi(setting.id, value);

                }

            };

            input = enumCycle;

        }

        else

        {

            Slider slider = new Slider(setting.min, setting.max) { value = ParseFloat(setting.value, setting.min) };

            slider.focusable = false;

            slider.style.height = 64f;

            slider.style.marginTop = 4f;

            slider.RegisterCallback<PointerDownEvent>(_ => PreserveGlobalSettingsScrollOffset());

            slider.RegisterValueChangedCallback(evt =>

            {

                if (suppressCallbacks)

                    return;



                PreserveGlobalSettingsScrollOffset();

                float snapped = setting.step > 0.0001f ? Mathf.Round(evt.newValue / setting.step) * setting.step : evt.newValue;

                string serialized = string.Equals(setting.valueType, "int", StringComparison.OrdinalIgnoreCase)

                    ? Mathf.RoundToInt(snapped).ToString(CultureInfo.InvariantCulture)

                    : snapped.ToString("0.###", CultureInfo.InvariantCulture);

                owner?.SetGlobalRuntimeSettingFromUi(setting.id, serialized);

            });

            input = slider;

        }



        input.tooltip = setting.tooltip;

        input.style.marginBottom = 6f;

        input.style.width = Length.Percent(100f);

        row.Add(input);



        Label valueLabel = CreateLabel(setting.value, 24f, LibraryPrimaryColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        valueLabel.style.unityFontDefinition = modernUiFontDefinition;

        valueLabel.style.whiteSpace = WhiteSpace.Normal;

        valueLabel.style.flexShrink = 1f;

        valueLabel.AddToClassList("global-setting-value");

        row.Add(valueLabel);



        globalSettingInputs[setting.id] = input;

        globalSettingValueLabels[setting.id] = valueLabel;

        return row;

    }



    private static void ConfigureRuntimeScrollView(ScrollView scrollView)

    {

        if (scrollView == null)

            return;



        scrollView.style.overflow = Overflow.Hidden;

        scrollView.contentViewport.style.overflow = Overflow.Hidden;

        scrollView.contentContainer.style.flexShrink = 0f;

        scrollView.mouseWheelScrollSize = 220f;

        scrollView.verticalPageSize = 240f;

    }



    private static void ApplyWheelScroll(ScrollView scrollView, WheelEvent evt)

    {

        if (scrollView == null || scrollView.contentViewport == null || scrollView.contentContainer == null)

            return;



        float delta = evt.delta.y;

        if (Mathf.Abs(delta) < 0.01f)

            return;



        float viewportHeight = scrollView.contentViewport.layout.height > 0f

            ? scrollView.contentViewport.layout.height

            : scrollView.contentViewport.resolvedStyle.height;

        float contentHeight = scrollView.contentContainer.layout.height > 0f

            ? scrollView.contentContainer.layout.height

            : scrollView.contentContainer.resolvedStyle.height;

        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);

        float nextY = Mathf.Clamp(scrollView.scrollOffset.y + delta * scrollView.mouseWheelScrollSize, 0f, maxOffset);

        scrollView.scrollOffset = new Vector2(scrollView.scrollOffset.x, nextY);

        evt.PreventDefault();

        evt.StopImmediatePropagation();

        evt.StopPropagation();

    }



    private static void AttachWheelScrolling(VisualElement element, ScrollView scrollView)

    {

        if (element == null || scrollView == null)

            return;



        element.RegisterCallback<WheelEvent>(evt => ApplyWheelScroll(scrollView, evt), TrickleDown.TrickleDown);

    }



    private Texture2D GetLibraryConfirmedSongGradientTexture()

    {

        if (libraryConfirmedSongGradientTexture != null)

            return libraryConfirmedSongGradientTexture;



        const int width = 128;

        libraryConfirmedSongGradientTexture = new Texture2D(width, 1, TextureFormat.RGBA32, false)

        {

            name = "LibraryConfirmedSongGradient",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        Color dark = new Color(0.88f, 0.46f, 0.24f, 1f);

        Color light = new Color(1.00f, 0.72f, 0.44f, 1f);

        for (int x = 0; x < width; x++)

        {

            float t = x / (width - 1f);

            float eased = Mathf.SmoothStep(0f, 1f, t);

            libraryConfirmedSongGradientTexture.SetPixel(x, 0, Color.Lerp(light, dark, eased));

        }



        libraryConfirmedSongGradientTexture.Apply(false, true);

        return libraryConfirmedSongGradientTexture;

    }



    private Texture2D GetLibrarySelectionRailGradientTexture()

    {

        if (librarySelectionRailGradientTexture != null)

            return librarySelectionRailGradientTexture;



        const int width = 8;

        const int height = 512;

        librarySelectionRailGradientTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)

        {

            name = "LibrarySelectionRailGradient",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        Color light = new Color(0.43f, 0.82f, 0.95f, 0.98f);

        Color mid = new Color(0.27f, 0.69f, 0.82f, 0.98f);

        Color dark = new Color(0.10f, 0.30f, 0.46f, 0.98f);

        for (int y = 0; y < height; y++)

        {

            float t = y / (height - 1f);

            Color color = t < 0.28f

                ? Color.Lerp(light, mid, Mathf.SmoothStep(0f, 1f, t / 0.28f))

                : Color.Lerp(mid, dark, Mathf.SmoothStep(0f, 1f, (t - 0.28f) / 0.72f));

            for (int x = 0; x < width; x++)

                librarySelectionRailGradientTexture.SetPixel(x, y, color);

        }



        librarySelectionRailGradientTexture.Apply(false, true);

        return librarySelectionRailGradientTexture;

    }



    private Texture2D GetPauseBackplateGradientTexture()

    {

        if (pauseBackplateGradientTexture != null)

            return pauseBackplateGradientTexture;



        const int width = 8;

        const int height = 512;

        pauseBackplateGradientTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)

        {

            name = "PauseBackplateGradient",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        for (int y = 0; y < height; y++)

        {

            float t = y / (height - 1f);

            Color color = t < 0.34f

                ? Color.Lerp(PrimaryAccentGradientLightColor, PrimaryAccentGradientMidColor, Mathf.SmoothStep(0f, 1f, t / 0.34f))

                : Color.Lerp(PrimaryAccentGradientMidColor, PrimaryAccentGradientDarkColor, Mathf.SmoothStep(0f, 1f, (t - 0.34f) / 0.66f));

            for (int x = 0; x < width; x++)

                pauseBackplateGradientTexture.SetPixel(x, y, color);

        }



        pauseBackplateGradientTexture.Apply(false, true);

        return pauseBackplateGradientTexture;

    }



    private Texture2D GetSongEndAccentGradientTexture()

    {

        if (songEndAccentGradientTexture != null)

            return songEndAccentGradientTexture;



        const int width = 512;

        const int height = 8;

        songEndAccentGradientTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)

        {

            name = "SongEndAccentGradient",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        for (int x = 0; x < width; x++)

        {

            float t = x / (width - 1f);

            Color color = t < 0.34f

                ? Color.Lerp(PrimaryAccentGradientLightColor, PrimaryAccentGradientMidColor, Mathf.SmoothStep(0f, 1f, t / 0.34f))

                : Color.Lerp(PrimaryAccentGradientMidColor, PrimaryAccentGradientDarkColor, Mathf.SmoothStep(0f, 1f, (t - 0.34f) / 0.66f));

            for (int y = 0; y < height; y++)

                songEndAccentGradientTexture.SetPixel(x, y, color);

        }



        songEndAccentGradientTexture.Apply(false, true);

        return songEndAccentGradientTexture;

    }

    private Texture2D GetSongEndPanelBackdropTexture()

    {

        if (songEndPanelBackdropTexture != null)

            return songEndPanelBackdropTexture;



        const int width = 768;

        const int height = 512;

        songEndPanelBackdropTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)

        {

            name = "SongEndPanelBackdrop",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        Vector2 warmCenter = new Vector2(0.14f, 0.78f);

        Vector2 coolCenter = new Vector2(0.82f, 0.10f);

        Vector2 topFocus = new Vector2(0.54f, 0.00f);

        for (int y = 0; y < height; y++)

        {

            float v = y / (height - 1f);

            for (int x = 0; x < width; x++)

            {

                float u = x / (width - 1f);

                Vector2 uv = new Vector2(u, v);

                float warmGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(uv, warmCenter) / 0.48f), 2.3f);

                float coolGlow = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(uv, coolCenter) / 0.78f), 2.1f);

                float focusMask = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(uv, topFocus) / 1.08f), 2.6f);

                float rayPhase = (u - 0.50f) * 1.42f + v * 0.78f;

                float rayDistance = Mathf.Abs(Mathf.Repeat(rayPhase, 0.115f) - 0.0575f);

                float ray = Mathf.SmoothStep(0.024f, 0.0f, rayDistance) * Mathf.Pow(Mathf.Clamp01(1f - v), 2.2f) * focusMask;

                float vignette = Mathf.Clamp01(Mathf.Max(Mathf.Abs(u - 0.5f) * 1.25f, v * 0.82f));

                Color color = new Color(0f, 0f, 0f, 0f);

                color += new Color(1.00f, 0.43f, 0.18f, warmGlow * 0.16f);

                color += new Color(0.22f, 0.49f, 0.93f, coolGlow * 0.12f);

                color += new Color(0.19f, 0.43f, 0.92f, ray * 0.26f);

                color.a *= 1f - (vignette * 0.34f);

                songEndPanelBackdropTexture.SetPixel(x, y, color);

            }

        }



        songEndPanelBackdropTexture.Apply(false, true);

        return songEndPanelBackdropTexture;

    }

    private Texture2D GetSongEndHeroGlowTexture()

    {

        if (songEndHeroGlowTexture != null)

            return songEndHeroGlowTexture;



        const int width = 640;

        const int height = 320;

        songEndHeroGlowTexture = new Texture2D(width, height, TextureFormat.RGBA32, false)

        {

            name = "SongEndHeroGlow",

            wrapMode = TextureWrapMode.Clamp,

            filterMode = FilterMode.Bilinear

        };



        for (int y = 0; y < height; y++)

        {

            float v = y / (height - 1f);

            for (int x = 0; x < width; x++)

            {

                float u = x / (width - 1f);

                float dx = (u - 0.5f) / 0.58f;

                float dy = (v - 0.55f) / 0.42f;

                float dist = Mathf.Sqrt((dx * dx) + (dy * dy));

                float core = Mathf.Pow(Mathf.Clamp01(1f - dist), 2.3f);

                float ring = Mathf.Pow(Mathf.Clamp01(1f - Mathf.Abs(dist - 0.60f) / 0.20f), 1.9f);

                float topCool = Mathf.Pow(Mathf.Clamp01(1f - Vector2.Distance(new Vector2(u, v), new Vector2(0.50f, 0.18f)) / 0.70f), 2.2f);

                Color color = new Color(0f, 0f, 0f, 0f);

                color += new Color(1.00f, 0.49f, 0.21f, core * 0.12f);

                color += new Color(1.00f, 0.55f, 0.24f, ring * 0.22f);

                color += new Color(0.20f, 0.44f, 0.92f, topCool * 0.09f);

                songEndHeroGlowTexture.SetPixel(x, y, color);

            }

        }



        songEndHeroGlowTexture.Apply(false, true);

        return songEndHeroGlowTexture;

    }



    private static string BuildGlobalSettingsLayoutSignature(List<RuntimeSettingSectionSnapshot> sections)

    {

        if (sections == null)

            return string.Empty;



        List<string> tokens = new List<string>();

        foreach (RuntimeSettingSectionSnapshot section in sections)

        {

            tokens.Add(section?.title ?? string.Empty);

            if (section?.settings == null)

                continue;



            foreach (RuntimeSettingSnapshot setting in section.settings)

                tokens.Add($"{setting?.id}:{setting?.valueType}");

        }



        return string.Join("|", tokens);

    }



    private static float ParseFloat(string value, float fallback)

    {

        return float.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out float parsed) ? parsed : fallback;

    }



    private static bool IsNoteInsideLoopWindow(float noteTime, float loopStart, float loopEnd)

    {

        if (loopEnd <= loopStart)

            return false;



        return noteTime >= loopStart - 0.0001f && noteTime <= loopEnd + 0.0001f;

    }



    private void ResetScoreCounters()

    {

        scoreHits = 0;

        scoreMisses = 0;

        scoredNoteIds.Clear();

    }



    private void SpawnJudgePopup(bool success, int streak)

    {

        string text;

        if (success)

        {

            if (streak >= 8)

                text = "UNSTOPPABLE!";

            else if (streak >= 5)

                text = "ON FIRE!";

            else

            {

                string[] hitTexts = { "Great!", "Awesome!", "Perfect!", "Nice!" };

                text = hitTexts[UnityEngine.Random.Range(0, hitTexts.Length)];

            }

        }

        else

        {

            string[] missTexts = { "Miss!", "Oops!", "Late!" };

            text = missTexts[UnityEngine.Random.Range(0, missTexts.Length)];

        }



        Color popupColor = success

            ? GameplayHudAccentCoolColor

            : GameplayHudAccentWarmColor;



        Label popup = CreateLabel(text, judgePopupFontSize, popupColor, true, TextAnchor.MiddleCenter, useTitleFont: false);

        popup.style.position = Position.Absolute;

        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;

        if (isHighway3D && highwayCharacterVisible)

        {

            float panelWidth = document != null && document.rootVisualElement != null && document.rootVisualElement.worldBound.width > 1f
                ? document.rootVisualElement.worldBound.width
                : Screen.width;

            float panelHeight = document != null && document.rootVisualElement != null && document.rootVisualElement.worldBound.height > 1f
                ? document.rootVisualElement.worldBound.height
                : Screen.height;

            Rect popupLayerBounds = judgePopupLayer != null && judgePopupLayer.worldBound.width > 1f && judgePopupLayer.worldBound.height > 1f
                ? judgePopupLayer.worldBound
                : new Rect(0f, 0f, panelWidth, panelHeight);

            Rect characterRect = GuitarHighway3DRenderer.GetHighwayCharacterHudScreenRect(panelWidth, panelHeight);
            characterRect.position -= popupLayerBounds.position;

            float layerWidth = popupLayerBounds.width > 1f ? popupLayerBounds.width : panelWidth;
            float layerHeight = popupLayerBounds.height > 1f ? popupLayerBounds.height : panelHeight;

            const float popupTopAnchorY01 = 0.12f;

            const float popupRightGapX01 = -0.2f;

            float popupWidth = Mathf.Clamp(characterRect.width * 0.50f, 150f, 240f);

            float popupHeight = Mathf.Clamp(characterRect.height * 0.115f, 42f, characterRect.height * 0.18f);

            float popupFontSize = Mathf.Clamp(characterRect.height * 0.072f, 30f, 58f);

            float popupLeft = characterRect.xMax + (characterRect.width * popupRightGapX01);

            float maxPopupWidth = Mathf.Max(140f, layerWidth - popupLeft - 18f);
            popupWidth = Mathf.Min(popupWidth, maxPopupWidth);

            popupLeft = Mathf.Clamp(
                popupLeft,
                18f,
                layerWidth - popupWidth - 18f);

            float highwayPopupBaseY = Mathf.Clamp(
                characterRect.yMin + (characterRect.height * popupTopAnchorY01),
                18f,
                layerHeight - popupHeight - 18f);

            int highwayPopupLayer = Mathf.Min(activeJudgePopups.Count, 4);

            float highwayPopupStartY = highwayPopupBaseY + highwayPopupLayer * Mathf.Clamp(judgePopupFontSize * 0.42f, 18f, 28f);



            popup.style.left = popupLeft;

            popup.style.right = StyleKeyword.Auto;

            popup.style.width = popupWidth;

            popup.style.minHeight = popupHeight;

            popup.style.fontSize = popupFontSize;

            popup.style.unityTextAlign = TextAnchor.MiddleLeft;

            popup.style.letterSpacing = 0.8f;

            popup.style.scale = new Scale(new Vector3(1.06f, 1.06f, 1f));

            popup.style.top = highwayPopupStartY;



            judgePopupLayer.Add(popup);

            activeJudgePopups.Add(new JudgePopupEntry

            {

                label = popup,

                startTime = Time.unscaledTime,

                startY = highwayPopupStartY,

                endY = highwayPopupStartY - (popupHeight * 1.2f),

                duration = 1.05f,

                fontSize = popupFontSize

            });

            return;

        }



        popup.style.left = 0f;

        popup.style.right = 0f;

        popup.style.unityTextAlign = TextAnchor.MiddleCenter;

        popup.style.letterSpacing = 1.2f;

        popup.style.opacity = 1f;

        popup.style.scale = new Scale(new Vector3(1.14f, 1.14f, 1f));



        float baseY = Mathf.Clamp(Screen.height * 0.62f, 430f, 780f);

        int layer = Mathf.Min(activeJudgePopups.Count, 4);

        float startY = baseY + layer * 24f;

        popup.style.top = startY;



        judgePopupLayer.Add(popup);

        activeJudgePopups.Add(new JudgePopupEntry

        {

            label = popup,

            startTime = Time.unscaledTime,

            startY = startY,

            endY = startY - 150f,

            duration = 1.05f

        });

    }



    private void UpdateJudgePopups()

    {

        float now = Time.unscaledTime;

        for (int i = activeJudgePopups.Count - 1; i >= 0; i--)

        {

            JudgePopupEntry popup = activeJudgePopups[i];

            if (popup == null || popup.label == null)

            {

                activeJudgePopups.RemoveAt(i);

                continue;

            }



            float elapsed = now - popup.startTime;

            if (elapsed >= popup.duration)

            {

                judgePopupLayer.Remove(popup.label);

                activeJudgePopups.RemoveAt(i);

                continue;

            }



            float t = Mathf.Clamp01(elapsed / popup.duration);

            float moveEase = 1f - Mathf.Pow(1f - t, 2.2f);

            popup.label.style.top = Mathf.Lerp(popup.startY, popup.endY, moveEase);

            popup.label.style.opacity = 1f - Mathf.Pow(t, 1.35f);



            float scale;

            if (t < 0.16f)

            {

                float popT = t / 0.16f;

                scale = Mathf.Lerp(1.22f, 1.00f, popT);

            }

            else

            {

                float settleT = (t - 0.16f) / 0.84f;

                scale = Mathf.Lerp(1.00f, 0.92f, settleT);

            }



            popup.label.style.scale = new Scale(new Vector3(scale, scale, 1f));

            popup.label.style.fontSize = popup.fontSize > 0f ? popup.fontSize : judgePopupFontSize;

        }

    }



    private Label CreateLabel(string text, float size, Color color, bool bold = false, TextAnchor anchor = TextAnchor.MiddleLeft, bool useTitleFont = false)

    {

        Label label = new Label(text);

        label.style.fontSize = size;

        label.style.color = color;

        label.style.unityTextAlign = anchor;

        label.style.unityFontDefinition = useTitleFont ? titleFontDefinition : bodyFontDefinition;

        if (bold)

            label.style.unityFontStyleAndWeight = FontStyle.Bold;

        return label;

    }



    private void AddTechniqueLegendRow(string icon, string description, Color iconColor)

    {

        if (techniqueLegendCard == null)

            return;



        VisualElement row = new VisualElement();

        row.style.flexDirection = FlexDirection.Row;

        row.style.alignItems = Align.Center;

        row.style.marginTop = 1f;

        row.style.marginBottom = 1f;



        Label iconLabel = CreateLabel(icon, 24f, iconColor, true, TextAnchor.MiddleLeft, useTitleFont: false);

        iconLabel.style.minWidth = 26f;

        iconLabel.style.marginRight = 8f;

        iconLabel.style.unityTextAlign = TextAnchor.MiddleCenter;



        Label textLabel = CreateLabel($": {description}", 24f, new Color(0.90f, 0.96f, 1f, 0.98f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        textLabel.style.unityTextAlign = TextAnchor.MiddleLeft;



        techniqueLegendIconLabels.Add(iconLabel);

        techniqueLegendTextLabels.Add(textLabel);

        row.Add(iconLabel);

        row.Add(textLabel);

        techniqueLegendCard.Add(row);

    }



    private VisualElement CreateStringTheoryLogo(float stringSize, float theorySize, float theoryShiftLeft, float theoryLetterSpacing, float rowBottomMargin, float stringLetterMargin)

    {

        VisualElement logoWrap = new VisualElement();

        logoWrap.style.alignItems = Align.Center;



        VisualElement stringRow = new VisualElement();

        stringRow.style.flexDirection = FlexDirection.Row;

        stringRow.style.justifyContent = Justify.Center;

        stringRow.style.marginBottom = rowBottomMargin;



        const string stringWord = "STRING";

        for (int i = 0; i < stringWord.Length; i++)

        {

            Label letter = CreateLabel(stringWord[i].ToString(), stringSize, LogoStringColors[i % LogoStringColors.Length], true, TextAnchor.MiddleCenter, useTitleFont: true);

            letter.style.unityFontDefinition = logoFontDefinition;

            letter.style.marginLeft = stringLetterMargin;

            letter.style.marginRight = stringLetterMargin;

            letter.style.unityFontStyleAndWeight = FontStyle.Bold;

            stringRow.Add(letter);

        }



        Label theoryLabel = CreateLabel("THEORY", theorySize, new Color(0.87f, 0.95f, 1f, 1f), true, TextAnchor.MiddleCenter, useTitleFont: true);

        theoryLabel.style.unityFontDefinition = logoFontDefinition;

        theoryLabel.style.marginLeft = theoryShiftLeft;

        theoryLabel.style.letterSpacing = theoryLetterSpacing;



        logoWrap.Add(stringRow);

        logoWrap.Add(theoryLabel);

        return logoWrap;

    }



    private Button CreateActionButton(string text, Action onClick)

    {

        Button button = new Button(() => onClick?.Invoke()) { text = text };

        button.focusable = false;

        button.style.height = 64f;

        button.style.minWidth = 220f;

        button.style.paddingLeft = 18f;

        button.style.paddingRight = 18f;

        button.style.backgroundColor = new Color(0.08f, 0.15f, 0.24f, 0.96f);

        button.style.color = Color.white;

        button.style.fontSize = 28f;

        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        button.style.borderTopLeftRadius = 12f;

        button.style.borderTopRightRadius = 12f;

        button.style.borderBottomLeftRadius = 12f;

        button.style.borderBottomRightRadius = 12f;

        button.style.borderTopWidth = 2f;

        button.style.borderRightWidth = 2f;

        button.style.borderBottomWidth = 6f;

        button.style.borderLeftWidth = 2f;

        ApplyButtonEdgeColorByLabel(button, text);

        button.style.unityTextAlign = TextAnchor.MiddleCenter;

        button.style.letterSpacing = 0.35f;

        button.style.marginBottom = 3f;

        button.style.unityFontDefinition = bodyFontDefinition;

        return button;

    }



    private void StyleSongEndActionButton(Button button, Color idleColor, bool isPrimaryAction)

    {

        if (button == null)

            return;



        Color idleBorderColor = isPrimaryAction

            ? new Color(idleColor.r, idleColor.g, idleColor.b, 0.92f)

            : new Color(0f, 0f, 0f, 0f);

        Color idleTextColor = isPrimaryAction

            ? GameplayHudPrimaryTextColor

            : GameplayHudSecondaryTextColor;

        Color idleBackgroundColor = isPrimaryAction

            ? new Color(idleColor.r, idleColor.g, idleColor.b, 0.84f)

            : new Color(0f, 0f, 0f, 0f);

        Color hoverBorderColor = isPrimaryAction ? GlobalPrimaryAccentColor : GameplayHudAccentCoolColor;

        Color hoverTextColor = isPrimaryAction ? GameplayHudPrimaryTextColor : GameplayHudPrimaryTextColor;

        Color hoverBackgroundColor = isPrimaryAction

            ? new Color(GlobalPrimaryAccentColor.r, GlobalPrimaryAccentColor.g, GlobalPrimaryAccentColor.b, 0.94f)

            : new Color(GameplayHudAccentCoolColor.r, GameplayHudAccentCoolColor.g, GameplayHudAccentCoolColor.b, 0.12f);

        button.style.backgroundImage = StyleKeyword.None;

        button.style.backgroundColor = idleBackgroundColor;

        button.style.color = idleTextColor;

        button.style.borderTopWidth = 1f;

        button.style.borderRightWidth = 1f;

        button.style.borderBottomWidth = 1f;

        button.style.borderLeftWidth = 1f;

        button.style.borderTopColor = idleBorderColor;

        button.style.borderRightColor = idleBorderColor;

        button.style.borderBottomColor = idleBorderColor;

        button.style.borderLeftColor = idleBorderColor;

        button.style.borderTopLeftRadius = 20f;

        button.style.borderTopRightRadius = 20f;

        button.style.borderBottomLeftRadius = 20f;

        button.style.borderBottomRightRadius = 20f;

        button.style.unityFontDefinition = modernUiFontDefinition;



        button.RegisterCallback<MouseEnterEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));

            button.style.translate = new Translate(0f, -2f);

            button.style.backgroundColor = hoverBackgroundColor;

            button.style.color = hoverTextColor;

            button.style.borderTopColor = hoverBorderColor;

            button.style.borderRightColor = hoverBorderColor;

            button.style.borderBottomColor = hoverBorderColor;

            button.style.borderLeftColor = hoverBorderColor;

        });



        button.RegisterCallback<MouseLeaveEvent>(_ =>

        {

            button.style.scale = new Scale(Vector3.one);

            button.style.translate = new Translate(0f, 0f);

            button.style.backgroundColor = idleBackgroundColor;

            button.style.color = idleTextColor;

            button.style.borderTopColor = idleBorderColor;

            button.style.borderRightColor = idleBorderColor;

            button.style.borderBottomColor = idleBorderColor;

            button.style.borderLeftColor = idleBorderColor;

        });

    }



    private static void ConfigureInteractiveButtonHover(Button button, Color baseColor, Color textColor)

    {

        if (button == null)

            return;



        button.RegisterCallback<MouseEnterEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));

            button.style.translate = new Translate(-8f, 0f);

            button.style.backgroundColor = baseColor;

            button.style.color = textColor;

        });



        button.RegisterCallback<MouseLeaveEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

            button.style.translate = new Translate(0f, 0f);

            button.style.backgroundColor = baseColor;

            button.style.color = textColor;

        });

    }



    private static void ConfigureInteractiveCardHover(VisualElement card, Color baseColor)

    {

        if (card == null)

            return;



        Color hoverColor = new Color(

            Mathf.Min(baseColor.r + 0.035f, 1f),

            Mathf.Min(baseColor.g + 0.035f, 1f),

            Mathf.Min(baseColor.b + 0.035f, 1f),

            baseColor.a);



        card.RegisterCallback<MouseEnterEvent>(_ =>

        {

            card.style.scale = new Scale(new Vector3(1.012f, 1.012f, 1f));

            card.style.translate = new Translate(-4f, 0f);

            card.style.backgroundColor = hoverColor;

        });



        card.RegisterCallback<MouseLeaveEvent>(_ =>

        {

            card.style.scale = new Scale(new Vector3(1f, 1f, 1f));

            card.style.translate = new Translate(0f, 0f);

            card.style.backgroundColor = baseColor;

        });

    }



    private Button CreateLibraryFooterButton(string text, Color backgroundColor, Action onClick)

    {

        Button button = new Button(() => onClick?.Invoke()) { text = text };

        button.focusable = false;

        button.style.minWidth = 214f;

        button.style.height = 68f;

        button.style.paddingLeft = 24f;

        button.style.paddingRight = 24f;

        bool isPrimary = backgroundColor == LibraryPrimaryColor || backgroundColor == LibraryConfirmedSongColor;

        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        button.style.color = isPrimary

            ? LibraryConfirmedSongColor

            : new Color(0.93f, 0.95f, 0.97f, 1f);

        button.style.fontSize = 24f;

        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        button.style.borderTopLeftRadius = 12f;

        button.style.borderTopRightRadius = 12f;

        button.style.borderBottomLeftRadius = 12f;

        button.style.borderBottomRightRadius = 12f;

        button.style.borderTopWidth = 1f;

        button.style.borderRightWidth = 1f;

        button.style.borderBottomWidth = 1f;

        button.style.borderLeftWidth = 1f;

        Color borderColor = isPrimary

            ? new Color(0f, 0f, 0f, 0f)

            : new Color(0f, 0f, 0f, 0f);

        button.style.borderTopColor = borderColor;

        button.style.borderRightColor = borderColor;

        button.style.borderBottomColor = borderColor;

        button.style.borderLeftColor = borderColor;

        button.style.unityTextAlign = TextAnchor.MiddleCenter;

        button.style.letterSpacing = 0f;

        button.style.unityFontDefinition = modernUiFontDefinition;

        button.RegisterCallback<MouseEnterEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));

            button.style.translate = new Translate(-8f, 0f);

            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            button.style.color = LibraryConfirmedSongColor;

            button.style.borderTopColor = LibraryConfirmedSongColor;

            button.style.borderRightColor = LibraryConfirmedSongColor;

            button.style.borderBottomColor = LibraryConfirmedSongColor;

            button.style.borderLeftColor = LibraryConfirmedSongColor;

        });

        button.RegisterCallback<MouseLeaveEvent>(_ =>

        {

            button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

            button.style.translate = new Translate(0f, 0f);

            button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            button.style.color = isPrimary

                ? LibraryConfirmedSongColor

                : new Color(0.93f, 0.95f, 0.97f, 1f);

            button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

            button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        });

        return button;

    }



    private void CreateMainMenuEntry(string title, string subtitle, Color accentColor, Action onClick)

    {

        int menuIndex = mainMenuEntries.Count;

        Button button = new Button(() =>

        {

            owner?.SetMainMenuSelectionFromUi(menuIndex);

            onClick?.Invoke();

        });

        button.focusable = false;

        button.text = string.Empty;

        button.style.minHeight = 114f;

        button.style.paddingLeft = 0f;

        button.style.paddingRight = 0f;

        button.style.paddingTop = 16f;

        button.style.paddingBottom = 16f;

        button.style.marginBottom = 18f;

        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        button.style.borderTopLeftRadius = 0f;

        button.style.borderTopRightRadius = 0f;

        button.style.borderBottomLeftRadius = 0f;

        button.style.borderBottomRightRadius = 0f;

        button.style.borderTopWidth = 0f;

        button.style.borderRightWidth = 0f;

        button.style.borderBottomWidth = 0f;

        button.style.borderLeftWidth = 0f;

        button.style.unityTextAlign = TextAnchor.MiddleLeft;

        button.RegisterCallback<MouseEnterEvent>(_ => owner?.HoverMainMenuSelectionFromUi(menuIndex));



        VisualElement row = new VisualElement();

        row.style.flexDirection = FlexDirection.Row;

        row.style.alignItems = Align.Center;



        Label arrowLabel = CreateLabel("\u25B6", 54f, MainMenuSelectedColor, true, TextAnchor.MiddleCenter, useTitleFont: true);

        arrowLabel.style.minWidth = 68f;

        arrowLabel.style.marginRight = 26f;

        arrowLabel.style.opacity = 0f;



        VisualElement textColumn = new VisualElement();

        textColumn.style.flexGrow = 1f;

        textColumn.style.flexShrink = 1f;



        Label titleLabel = CreateLabel(title, 52f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);

        titleLabel.style.marginBottom = 0f;

        titleLabel.style.letterSpacing = 1.1f;



        Label subtitleLabel = CreateLabel(subtitle, 22f, new Color(0.71f, 0.82f, 0.94f, 0.90f), false, TextAnchor.MiddleLeft, useTitleFont: false);

        subtitleLabel.style.whiteSpace = WhiteSpace.Normal;

        subtitleLabel.style.display = DisplayStyle.None;



        textColumn.Add(titleLabel);

        textColumn.Add(subtitleLabel);

        row.Add(arrowLabel);

        row.Add(textColumn);

        button.Add(row);

        mainMenuNavColumn.Add(button);



        mainMenuEntries.Add(new MainMenuEntry

        {

            button = button,

            arrowLabel = arrowLabel,

            titleLabel = titleLabel,

            subtitleLabel = subtitleLabel,

            accentColor = accentColor

        });

    }



    private Label CreateMainMenuMetricCard(VisualElement parent, string title, string value)

    {

        VisualElement card = new VisualElement();

        card.style.flexBasis = 0f;

        card.style.flexGrow = 1f;

        card.style.minWidth = 180f;

        card.style.marginLeft = 6f;

        card.style.marginRight = 6f;

        card.style.marginBottom = 12f;

        card.style.paddingLeft = 18f;

        card.style.paddingRight = 18f;

        card.style.paddingTop = 16f;

        card.style.paddingBottom = 16f;

        card.style.backgroundColor = new Color(0.07f, 0.15f, 0.26f, 0.84f);

        card.style.borderTopLeftRadius = 18f;

        card.style.borderTopRightRadius = 18f;

        card.style.borderBottomLeftRadius = 18f;

        card.style.borderBottomRightRadius = 18f;

        card.style.borderTopWidth = 1f;

        card.style.borderRightWidth = 1f;

        card.style.borderBottomWidth = 1f;

        card.style.borderLeftWidth = 1f;

        card.style.borderTopColor = new Color(0.35f, 0.68f, 1f, 0.28f);

        card.style.borderRightColor = new Color(0.35f, 0.68f, 1f, 0.18f);

        card.style.borderBottomColor = new Color(0.35f, 0.68f, 1f, 0.12f);

        card.style.borderLeftColor = new Color(0.35f, 0.68f, 1f, 0.18f);



        Label titleLabel = CreateLabel(title, 18f, new Color(0.60f, 0.79f, 0.97f, 0.90f), true, TextAnchor.MiddleLeft, useTitleFont: false);

        titleLabel.style.letterSpacing = 2.2f;

        titleLabel.style.marginBottom = 8f;



        Label valueLabel = CreateLabel(value, 34f, Color.white, true, TextAnchor.MiddleLeft, useTitleFont: true);



        card.Add(titleLabel);

        card.Add(valueLabel);

        parent.Add(card);

        return valueLabel;

    }



    private void UpdateMainMenuSelection(int selectedIndex)

    {

        int resolvedIndex = mainMenuEntries.Count == 0

            ? -1

            : Mathf.Clamp(selectedIndex, 0, mainMenuEntries.Count - 1);



        if (resolvedIndex == lastMainMenuSelectionIndex)

            return;



        lastMainMenuSelectionIndex = resolvedIndex;



        for (int i = 0; i < mainMenuEntries.Count; i++)

        {

            MainMenuEntry entry = mainMenuEntries[i];

            bool isSelected = i == resolvedIndex;

            entry.arrowLabel.style.opacity = isSelected ? 1f : 0f;

            entry.arrowLabel.style.color = MainMenuSelectedColor;

            entry.titleLabel.style.color = isSelected

                ? MainMenuSelectedColor

                : new Color(0.68f, 0.75f, 0.84f, 0.86f);

            entry.subtitleLabel.style.color = new Color(0.71f, 0.82f, 0.94f, 0.90f);



            entry.button.style.translate = new Translate(0f, 0f);

            entry.button.style.scale = isSelected

                ? new Scale(new Vector3(1.10f, 1.10f, 1f))

                : new Scale(new Vector3(1f, 1f, 1f));

            entry.button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

            entry.button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

            entry.button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

            entry.button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

            entry.button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

        }

    }



    private void UpdatePauseActionSelection(int selectedIndex)

    {

        if (pauseActionButtons.Count == 0 || speedValueLabel == null || speedSlider == null)

            return;



        int resolvedIndex = Mathf.Clamp(selectedIndex, 0, pauseActionButtons.Count);

        bool speedSelected = resolvedIndex == 0;

        speedValueLabel.style.color = speedSelected

            ? LibraryConfirmedSongTextColor

            : new Color(1f, 0.96f, 0.87f, 1f);

        speedValueLabel.style.backgroundImage = speedSelected

            ? new StyleBackground(GetLibraryConfirmedSongGradientTexture())

            : StyleKeyword.None;

        speedValueLabel.style.backgroundColor = speedSelected

            ? LibraryConfirmedSongColor

            : new Color(0f, 0f, 0f, 0f);

        speedValueLabel.style.paddingLeft = speedSelected ? 18f : 0f;

        speedValueLabel.style.paddingRight = speedSelected ? 18f : 0f;

        speedValueLabel.style.paddingTop = speedSelected ? 12f : 0f;

        speedValueLabel.style.paddingBottom = speedSelected ? 12f : 0f;

        speedValueLabel.style.borderTopLeftRadius = speedSelected ? 14f : 0f;

        speedValueLabel.style.borderTopRightRadius = speedSelected ? 14f : 0f;

        speedValueLabel.style.borderBottomLeftRadius = speedSelected ? 14f : 0f;

        speedValueLabel.style.borderBottomRightRadius = speedSelected ? 14f : 0f;

        speedSlider.style.opacity = speedSelected ? 1f : 0.72f;



        for (int i = 0; i < pauseActionButtons.Count; i++)

        {

            Button button = pauseActionButtons[i];

            if (button == null)

                continue;



            bool isSelected = (i + 1) == resolvedIndex;

            bool isPrimaryAction = string.Equals(button.text, "Resume", StringComparison.OrdinalIgnoreCase);

            bool isSecondaryAction = string.Equals(button.text, "Main Menu", StringComparison.OrdinalIgnoreCase);



            if (isSelected)

            {

                button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));

                button.style.translate = new Translate(-8f, 0f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = LibraryConfirmedSongColor;

                button.style.borderTopColor = LibraryConfirmedSongColor;

                button.style.borderRightColor = LibraryConfirmedSongColor;

                button.style.borderBottomColor = LibraryConfirmedSongColor;

                button.style.borderLeftColor = LibraryConfirmedSongColor;

            }

            else

            {

                button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

                button.style.translate = new Translate(0f, 0f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = isPrimaryAction

                    ? LibraryPrimaryColor

                    : isSecondaryAction

                        ? new Color(0.93f, 0.95f, 0.98f, 1f)

                        : new Color(0.94f, 0.96f, 0.98f, 1f);

                button.style.borderTopColor = isPrimaryAction

                    ? LibraryPrimaryColor

                    : isSecondaryAction

                        ? MenuOutlineNeutralColor

                        : new Color(0f, 0f, 0f, 0f);

                button.style.borderRightColor = button.style.borderTopColor;

                button.style.borderBottomColor = button.style.borderTopColor;

                button.style.borderLeftColor = button.style.borderTopColor;

            }

        }

    }



    private void UpdateGameModesSelection(int selectedIndex)

    {

        if (gameModesActionButtons.Count == 0)

            return;



        int resolvedIndex = Mathf.Clamp(selectedIndex, 0, 5);
        if (resolvedIndex < gameModesActionButtons.Count &&
            gameModesActionButtons[resolvedIndex].style.display.value == DisplayStyle.None)
        {
            resolvedIndex = gameModesActionButtons.FindIndex(button => button != null && button.style.display.value != DisplayStyle.None);
            if (resolvedIndex < 0)
                resolvedIndex = 5;
            owner?.SetGameModesSelectionFromUi(resolvedIndex);
        }

        for (int i = 0; i < gameModesActionButtons.Count; i++)

        {

            Button button = gameModesActionButtons[i];

            if (button == null)

                continue;



            bool isVisible = button.style.display.value != DisplayStyle.None;
            bool isSelected = isVisible && i == resolvedIndex;

            if (isSelected)

            {

                button.style.scale = new Scale(new Vector3(1.03f, 1.03f, 1f));

                button.style.translate = new Translate(-6f, 0f);

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = LibraryConfirmedSongColor;

                button.style.borderTopColor = LibraryConfirmedSongColor;

                button.style.borderRightColor = LibraryConfirmedSongColor;

                button.style.borderBottomColor = LibraryConfirmedSongColor;

                button.style.borderLeftColor = LibraryConfirmedSongColor;

            }

            else

            {

                button.style.scale = new Scale(Vector3.one);

                button.style.translate = new Translate(0f, 0f);

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

                button.style.borderTopColor = new Color(0f, 0f, 0f, 0f);

                button.style.borderRightColor = new Color(0f, 0f, 0f, 0f);

                button.style.borderBottomColor = new Color(0f, 0f, 0f, 0f);

                button.style.borderLeftColor = new Color(0f, 0f, 0f, 0f);

            }

        }

        if (gameModesBackButton != null)
        {
            bool backSelected = resolvedIndex == 5;
            gameModesBackButton.style.scale = backSelected ? new Scale(new Vector3(1.03f, 1.03f, 1f)) : new Scale(Vector3.one);
            gameModesBackButton.style.translate = backSelected ? new Translate(-6f, 0f) : new Translate(0f, 0f);
            gameModesBackButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            gameModesBackButton.style.color = backSelected ? LibraryConfirmedSongColor : new Color(0.93f, 0.95f, 0.98f, 1f);
            gameModesBackButton.style.borderTopColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            gameModesBackButton.style.borderRightColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            gameModesBackButton.style.borderBottomColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            gameModesBackButton.style.borderLeftColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
        }

    }

    private void StyleNotesDetectorActionButton(Button button, float minWidth, float height)
    {
        if (button == null)
            return;

        button.style.minWidth = minWidth;
        button.style.height = height;
        button.style.paddingLeft = 18f;
        button.style.paddingRight = 18f;
        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);
        button.style.borderTopWidth = 2f;
        button.style.borderRightWidth = 2f;
        button.style.borderBottomWidth = 2f;
        button.style.borderLeftWidth = 2f;
        button.style.borderTopColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);
        button.style.borderRightColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);
        button.style.borderBottomColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);
        button.style.borderLeftColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);
        button.style.borderTopLeftRadius = 12f;
        button.style.borderTopRightRadius = 12f;
        button.style.borderBottomLeftRadius = 12f;
        button.style.borderBottomRightRadius = 12f;
        button.style.fontSize = 28f;
        button.style.unityFontDefinition = modernUiFontDefinition;
    }

    private void StyleNotesDetectorDropdown(DropdownField dropdown, float minWidth)
    {
        if (dropdown == null)
            return;

        dropdown.style.minWidth = minWidth;
        dropdown.style.height = 80f;
        dropdown.style.fontSize = 28f;
        dropdown.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 0.98f);
        dropdown.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        dropdown.style.borderTopWidth = 1f;
        dropdown.style.borderRightWidth = 1f;
        dropdown.style.borderBottomWidth = 1f;
        dropdown.style.borderLeftWidth = 1f;
        dropdown.style.borderTopColor = new Color(0.31f, 0.33f, 0.37f, 0.90f);
        dropdown.style.borderRightColor = new Color(0.22f, 0.24f, 0.27f, 0.98f);
        dropdown.style.borderBottomColor = new Color(0.17f, 0.18f, 0.21f, 0.98f);
        dropdown.style.borderLeftColor = new Color(0.22f, 0.24f, 0.27f, 0.98f);
        dropdown.style.borderTopLeftRadius = 10f;
        dropdown.style.borderTopRightRadius = 10f;
        dropdown.style.borderBottomLeftRadius = 10f;
        dropdown.style.borderBottomRightRadius = 10f;
        dropdown.RegisterCallback<AttachToPanelEvent>(_ =>
        {
            VisualElement inputElement = dropdown.Q(className: "unity-base-field__input");
            if (inputElement != null)
            {
                inputElement.style.backgroundColor = new Color(0.06f, 0.07f, 0.09f, 1f);
                inputElement.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            }

            Label textLabel = dropdown.Q<Label>(className: "unity-base-popup-field__text");
            if (textLabel != null)
            {
                textLabel.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
                textLabel.style.fontSize = 28f;
            }

            VisualElement arrowElement = dropdown.Q(className: "unity-base-popup-field__arrow");
            if (arrowElement != null)
                arrowElement.style.unityBackgroundImageTintColor = new Color(0.84f, 0.86f, 0.90f, 1f);
        });
    }

    private VisualElement CreateNotesDetectorSettingRow(NativeDetectorSettingDefinition definition)
    {
        VisualElement row = new VisualElement();
        row.style.flexDirection = FlexDirection.Column;
        row.style.width = Length.Percent(100f);
        row.style.paddingLeft = 24f;
        row.style.paddingRight = 24f;
        row.style.paddingTop = 22f;
        row.style.paddingBottom = 22f;
        row.style.marginBottom = 16f;
        row.style.backgroundColor = new Color(0.07f, 0.08f, 0.09f, 0.78f);
        row.style.borderTopWidth = 1f;
        row.style.borderRightWidth = 1f;
        row.style.borderBottomWidth = 1f;
        row.style.borderLeftWidth = 1f;
        row.style.borderTopColor = new Color(0.18f, 0.20f, 0.24f, 1f);
        row.style.borderRightColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        row.style.borderBottomColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        row.style.borderLeftColor = new Color(0.13f, 0.14f, 0.16f, 1f);
        row.style.borderTopLeftRadius = 14f;
        row.style.borderTopRightRadius = 14f;
        row.style.borderBottomLeftRadius = 14f;
        row.style.borderBottomRightRadius = 14f;

        VisualElement headerRow = new VisualElement();
        headerRow.style.flexDirection = FlexDirection.Row;
        headerRow.style.justifyContent = Justify.SpaceBetween;
        headerRow.style.alignItems = Align.FlexStart;
        headerRow.style.marginBottom = 8f;

        Label titleLabel = CreateLabel(definition.Label, 36f, new Color(0.95f, 0.97f, 1f, 1f), true, TextAnchor.MiddleLeft, useTitleFont: false);
        titleLabel.style.unityFontDefinition = modernUiFontDefinition;
        titleLabel.style.flexGrow = 1f;
        titleLabel.style.marginRight = 16f;
        titleLabel.style.whiteSpace = WhiteSpace.Normal;

        Label valueLabel = CreateLabel(NativeDetectorSettingCatalog.FormatValue(definition, definition.Min), 34f, LibraryConfirmedSongColor, true, TextAnchor.MiddleRight, useTitleFont: false);
        valueLabel.style.unityFontDefinition = modernUiFontDefinition;
        valueLabel.style.minWidth = 132f;
        valueLabel.style.unityTextAlign = TextAnchor.MiddleRight;

        headerRow.Add(titleLabel);
        headerRow.Add(valueLabel);

        Label descriptionLabel = CreateLabel(definition.Description, 26f, new Color(0.78f, 0.82f, 0.88f, 0.94f), false, TextAnchor.MiddleLeft, useTitleFont: false);
        descriptionLabel.style.unityFontDefinition = modernUiFontDefinition;
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.marginBottom = 14f;

        Slider slider = new Slider(definition.Min, definition.Max)
        {
            name = definition.Key
        };
        slider.style.height = 38f;
        slider.style.marginTop = 4f;
        slider.style.marginBottom = 2f;
        slider.RegisterValueChangedCallback(evt =>
        {
            if (suppressNotesDetectorSettingCallbacks)
                return;

            float resolvedValue = definition.WholeNumbers ? Mathf.Round(evt.newValue) : evt.newValue;
            if (definition.WholeNumbers && !Mathf.Approximately(resolvedValue, evt.newValue))
                slider.SetValueWithoutNotify(resolvedValue);

            valueLabel.text = NativeDetectorSettingCatalog.FormatValue(definition, resolvedValue);
            owner?.SetNativeNotesDetectorSettingFromUi(definition.Key, resolvedValue);
        });

        row.Add(headerRow);
        row.Add(descriptionLabel);
        row.Add(slider);

        notesDetectorSettingSliders[definition.Key] = slider;
        notesDetectorSettingValueLabels[definition.Key] = valueLabel;
        notesDetectorSettingRows[definition.Key] = row;
        return row;
    }

    private void UpdateNotesDetectorAdvancedSettingsVisibility()
    {
        if (notesDetectorAdvancedSettingsToggleButton != null)
            notesDetectorAdvancedSettingsToggleButton.text = showNotesDetectorAdvancedSettings ? "Hide Advanced Settings" : "Show Advanced Settings";

        IReadOnlyList<NativeDetectorSettingDefinition> definitions = NativeDetectorSettingCatalog.Definitions;
        for (int i = 0; i < definitions.Count; i++)
        {
            NativeDetectorSettingDefinition definition = definitions[i];
            if (definition == null || !definition.IsAdvanced)
                continue;

            if (notesDetectorSettingRows.TryGetValue(definition.Key, out VisualElement row) && row != null)
                row.style.display = showNotesDetectorAdvancedSettings ? DisplayStyle.Flex : DisplayStyle.None;
        }
    }

    private void RefreshNotesDetectorSettingsControls(List<NativeDetectorSettingSnapshot> settings)
    {
        if (settings == null)
            return;

        suppressNotesDetectorSettingCallbacks = true;
        for (int i = 0; i < settings.Count; i++)
        {
            NativeDetectorSettingSnapshot snapshot = settings[i];
            if (snapshot == null || string.IsNullOrWhiteSpace(snapshot.key))
                continue;

            if (notesDetectorSettingSliders.TryGetValue(snapshot.key, out Slider slider))
            {
                slider.lowValue = snapshot.min;
                slider.highValue = snapshot.max;
                slider.SetValueWithoutNotify(snapshot.value);
            }

            if (notesDetectorSettingValueLabels.TryGetValue(snapshot.key, out Label valueLabel))
                valueLabel.text = string.IsNullOrWhiteSpace(snapshot.valueText) ? snapshot.value.ToString("F2") : snapshot.valueText;
        }
        suppressNotesDetectorSettingCallbacks = false;
    }

    private void UpdateNotesDetectorTestSelection(int selectedIndex)

    {

        if (notesDetectorTestActionButtons.Count == 0)

            return;



        int resolvedIndex = Mathf.Clamp(selectedIndex, 0, 2);

        for (int i = 0; i < notesDetectorTestActionButtons.Count; i++)

        {

            Button button = notesDetectorTestActionButtons[i];

            if (button == null)

                continue;



            bool isSelected = i == resolvedIndex;

            if (isSelected)

            {

                button.style.scale = new Scale(new Vector3(1.03f, 1.03f, 1f));

                button.style.translate = new Translate(-6f, 0f);

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = LibraryConfirmedSongColor;

                button.style.borderTopColor = LibraryConfirmedSongColor;

                button.style.borderRightColor = LibraryConfirmedSongColor;

                button.style.borderBottomColor = LibraryConfirmedSongColor;

                button.style.borderLeftColor = LibraryConfirmedSongColor;

            }

            else

            {

                button.style.scale = new Scale(Vector3.one);

                button.style.translate = new Translate(0f, 0f);

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = new Color(0.94f, 0.96f, 0.98f, 1f);

                button.style.borderTopColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);

                button.style.borderRightColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);

                button.style.borderBottomColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);

                button.style.borderLeftColor = new Color(0.22f, 0.24f, 0.28f, 0.98f);

            }

        }

        if (notesDetectorBackButton != null)
        {
            bool backSelected = resolvedIndex == 2;
            notesDetectorBackButton.style.scale = backSelected ? new Scale(new Vector3(1.03f, 1.03f, 1f)) : new Scale(Vector3.one);
            notesDetectorBackButton.style.translate = backSelected ? new Translate(-6f, 0f) : new Translate(0f, 0f);
            notesDetectorBackButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            notesDetectorBackButton.style.color = backSelected ? LibraryConfirmedSongColor : new Color(0.93f, 0.95f, 0.98f, 1f);
            notesDetectorBackButton.style.borderTopColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            notesDetectorBackButton.style.borderRightColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            notesDetectorBackButton.style.borderBottomColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
            notesDetectorBackButton.style.borderLeftColor = backSelected ? LibraryConfirmedSongColor : MenuOutlineNeutralColor;
        }

    }


    private void UpdateHeroModeSettingsSelection(int selectedIndex)

    {

        bool heartsSelected = Mathf.Clamp(selectedIndex, 0, 1) == 0;
        Color idleArrowBackground = new Color(0.15f, 0.17f, 0.20f, 1f);
        Color idleArrowBorder = new Color(0.20f, 0.24f, 0.28f, 1f);
        Color idleArrowText = new Color(0.90f, 0.94f, 0.97f, 1f);

        if (heroModeSettingsValueLabel != null)
        {
            heroModeSettingsValueLabel.style.color = heartsSelected ? GlobalSecondaryAccentColor : new Color(0.78f, 0.84f, 0.90f, 0.96f);
            heroModeSettingsValueLabel.style.scale = heartsSelected ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);
        }

        foreach (Button arrowButton in new[] { heroModeSettingsLeftArrowButton, heroModeSettingsRightArrowButton })
        {
            if (arrowButton == null)
                continue;

            arrowButton.style.backgroundColor = heartsSelected ? new Color(0.21f, 0.25f, 0.31f, 1f) : idleArrowBackground;
            arrowButton.style.borderTopColor = heartsSelected ? GlobalSecondaryAccentColor : idleArrowBorder;
            arrowButton.style.borderRightColor = heartsSelected ? GlobalSecondaryAccentColor : idleArrowBorder;
            arrowButton.style.borderBottomColor = heartsSelected ? GlobalSecondaryAccentColor : idleArrowBorder;
            arrowButton.style.borderLeftColor = heartsSelected ? GlobalSecondaryAccentColor : idleArrowBorder;
            arrowButton.style.color = heartsSelected ? Color.white : idleArrowText;
            arrowButton.style.scale = heartsSelected ? new Scale(new Vector3(1.05f, 1.05f, 1f)) : new Scale(Vector3.one);
        }

        Button backButton = heroModeSettingsPopup?.PrimaryButton;
        if (backButton != null)
        {
            bool backSelected = !heartsSelected;
            backButton.style.color = backSelected ? GlobalSecondaryAccentColor : GlobalPrimaryAccentColor;
            backButton.style.borderTopColor = backSelected ? GlobalSecondaryAccentColor : GlobalPrimaryAccentColor;
            backButton.style.borderRightColor = backSelected ? GlobalSecondaryAccentColor : GlobalPrimaryAccentColor;
            backButton.style.borderBottomColor = backSelected ? GlobalSecondaryAccentColor : GlobalPrimaryAccentColor;
            backButton.style.borderLeftColor = backSelected ? GlobalSecondaryAccentColor : GlobalPrimaryAccentColor;
            backButton.style.scale = backSelected ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);
            backButton.style.translate = backSelected ? new Translate(0f, -2f) : new Translate(0f, 0f);
        }

    }

    private void UpdateSongSettingsSelection(int selectedIndex)

    {

        int resolvedIndex = Mathf.Clamp(selectedIndex, 0, 8);



        UpdateSongSettingsSliderSelection(settingsOffsetLabel, settingsOffsetSlider, resolvedIndex == 1);

        UpdateSongSettingsSliderSelection(settingsTabSpeedLabel, settingsTabSpeedSlider, resolvedIndex == 2);

        UpdateSongSettingsSliderSelection(settingsStartDelayLabel, settingsStartDelaySlider, resolvedIndex == 3);

        UpdateSongSettingsSliderSelection(settingsVolumeLabel, settingsVolumeSlider, resolvedIndex == 4);



        for (int i = 0; i < songSettingsActionButtons.Count; i++)

        {

            Button button = songSettingsActionButtons[i];

            if (button == null)

                continue;



            bool isSelected = songSettingsActionSelectionIndices[i] == resolvedIndex;

            bool isPrimaryAction = string.Equals(button.text, "Resume", StringComparison.OrdinalIgnoreCase);

            bool isBackAction = string.Equals(button.text, "Back", StringComparison.OrdinalIgnoreCase);



            if (isSelected)

            {

                button.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));

                button.style.translate = new Translate(-8f, 0f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = LibraryConfirmedSongColor;

                button.style.borderTopColor = LibraryConfirmedSongColor;

                button.style.borderRightColor = LibraryConfirmedSongColor;

                button.style.borderBottomColor = LibraryConfirmedSongColor;

                button.style.borderLeftColor = LibraryConfirmedSongColor;

            }

            else

            {

                button.style.scale = new Scale(new Vector3(1f, 1f, 1f));

                button.style.translate = new Translate(0f, 0f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

                button.style.color = isPrimaryAction

                    ? LibraryPrimaryColor

                    : isBackAction

                        ? new Color(0.93f, 0.95f, 0.98f, 1f)

                        : new Color(0.94f, 0.96f, 0.98f, 1f);

                button.style.borderTopColor = isPrimaryAction

                    ? LibraryPrimaryColor

                    : isBackAction

                        ? MenuOutlineNeutralColor

                        : new Color(0f, 0f, 0f, 0f);

                button.style.borderRightColor = button.style.borderTopColor;

                button.style.borderBottomColor = button.style.borderTopColor;

                button.style.borderLeftColor = button.style.borderTopColor;

            }

        }



        UpdateSongSettingsSelectorArrows(settingsTrackLeftArrowButton, settingsTrackRightArrowButton, resolvedIndex == 5);

        UpdateSongSettingsSelectorArrows(settingsOffsetScopeLeftArrowButton, settingsOffsetScopeRightArrowButton, resolvedIndex == 6);

    }

    private void UpdateGeneratedAudioTrackPopup(GuitarGameplaySnapshot snapshot)

    {

        if (snapshot == null || generatedAudioTracksPopupOverlay == null)

            return;



        generatedAudioTracksPopupSummaryLabel.text = snapshot.generatedAudioTrackSelectionAvailable

            ? $"Now playing: {snapshot.generatedAudioTrackSelectionSummary}"

            : "No generated MusicXML parts are available for this song.";



        Button[] topButtons = { generatedAudioTracksSelectAllButton, generatedAudioTracksDeselectAllButton, generatedAudioTracksCloseButton };

        for (int i = 0; i < topButtons.Length; i++)

        {

            Button button = topButtons[i];

            bool isSelected = snapshot.selectedGeneratedAudioTrackIndex == i;

            button.style.color = isSelected ? LibraryConfirmedSongColor : LibraryPrimaryColor;

            button.style.borderTopColor = isSelected ? LibraryConfirmedSongColor : LibraryPrimaryColor;

            button.style.borderRightColor = isSelected ? LibraryConfirmedSongColor : LibraryPrimaryColor;

            button.style.borderBottomColor = isSelected ? LibraryConfirmedSongColor : LibraryPrimaryColor;

            button.style.borderLeftColor = isSelected ? LibraryConfirmedSongColor : LibraryPrimaryColor;

            button.style.scale = isSelected ? new Scale(new Vector3(1.02f, 1.02f, 1f)) : new Scale(Vector3.one);

            button.style.translate = isSelected ? new Translate(0f, -2f) : new Translate(0f, 0f);

        }



        generatedAudioTracksPopupTrackButtons.Clear();

        generatedAudioTracksPopupScrollView.contentContainer.Clear();



        int trackCount = Mathf.Min(snapshot.generatedAudioTrackNames?.Count ?? 0, snapshot.generatedAudioTrackEnabled?.Count ?? 0);

        for (int i = 0; i < trackCount; i++)

        {

            int popupSelectionIndex = i + 3;

            bool enabled = snapshot.generatedAudioTrackEnabled[i];

            string trackName = snapshot.generatedAudioTrackNames[i];



            Button rowButton = new Button(() =>

            {

                owner?.SetSelectedGeneratedAudioTrackSelectionIndexFromUi(popupSelectionIndex);

                owner?.ActivateSelectedGeneratedAudioTrackSelectionFromUi();

            });

            rowButton.focusable = false;

            rowButton.text = $"{(enabled ? "[ON] " : "[OFF] ")}{FormatTrackName(trackName)}";

            rowButton.style.height = 92f;

            rowButton.style.marginBottom = 10f;

            rowButton.style.paddingLeft = 22f;

            rowButton.style.paddingRight = 22f;

            rowButton.style.unityTextAlign = TextAnchor.MiddleLeft;

            rowButton.style.fontSize = 34f;

            rowButton.style.unityFontDefinition = modernUiFontDefinition;

            rowButton.style.backgroundColor = new Color(0.08f, 0.09f, 0.11f, 0.90f);

            rowButton.style.borderTopWidth = 2f;

            rowButton.style.borderRightWidth = 2f;

            rowButton.style.borderBottomWidth = 2f;

            rowButton.style.borderLeftWidth = 2f;

            rowButton.style.borderTopLeftRadius = 16f;

            rowButton.style.borderTopRightRadius = 16f;

            rowButton.style.borderBottomLeftRadius = 16f;

            rowButton.style.borderBottomRightRadius = 16f;

            rowButton.RegisterCallback<MouseEnterEvent>(_ => owner?.SetSelectedGeneratedAudioTrackSelectionIndexFromUi(popupSelectionIndex));



            bool isSelected = snapshot.selectedGeneratedAudioTrackIndex == popupSelectionIndex;

            rowButton.style.color = enabled ? new Color(0.95f, 0.97f, 0.99f, 1f) : new Color(0.62f, 0.68f, 0.75f, 1f);

            rowButton.style.borderTopColor = isSelected ? LibraryConfirmedSongColor : new Color(0.19f, 0.24f, 0.30f, 1f);

            rowButton.style.borderRightColor = rowButton.style.borderTopColor;

            rowButton.style.borderBottomColor = rowButton.style.borderTopColor;

            rowButton.style.borderLeftColor = rowButton.style.borderTopColor;

            rowButton.style.scale = isSelected ? new Scale(new Vector3(1.01f, 1.01f, 1f)) : new Scale(Vector3.one);



            generatedAudioTracksPopupTrackButtons.Add(rowButton);

            generatedAudioTracksPopupScrollView.Add(rowButton);

        }

    }



    private void UpdateLoopPausePopupSelection(int selectedIndex)

    {

        int resolvedIndex = Mathf.Clamp(selectedIndex, 0, 1);

        UpdateSongSettingsSliderSelection(loopPauseDurationLabel, loopPauseDurationSlider, resolvedIndex == 0);



        if (loopPauseAcceptButton == null)

            return;



        if (resolvedIndex == 1)

        {

            loopPauseAcceptButton.style.scale = new Scale(new Vector3(1.04f, 1.04f, 1f));

            loopPauseAcceptButton.style.translate = new Translate(-8f, 0f);

            loopPauseAcceptButton.style.color = GlobalSecondaryAccentColor;

            loopPauseAcceptButton.style.borderTopColor = GlobalSecondaryAccentColor;

            loopPauseAcceptButton.style.borderRightColor = GlobalSecondaryAccentColor;

            loopPauseAcceptButton.style.borderBottomColor = GlobalSecondaryAccentColor;

            loopPauseAcceptButton.style.borderLeftColor = GlobalSecondaryAccentColor;

        }

        else

        {

            loopPauseAcceptButton.style.scale = new Scale(Vector3.one);

            loopPauseAcceptButton.style.translate = new Translate(0f, 0f);

            loopPauseAcceptButton.style.color = GlobalPrimaryAccentColor;

            loopPauseAcceptButton.style.borderTopColor = GlobalPrimaryAccentColor;

            loopPauseAcceptButton.style.borderRightColor = GlobalPrimaryAccentColor;

            loopPauseAcceptButton.style.borderBottomColor = GlobalPrimaryAccentColor;

            loopPauseAcceptButton.style.borderLeftColor = GlobalPrimaryAccentColor;

        }

    }



    private void UpdateSongEndSelection(int selectedIndex)

    {

        Button[] buttons = { songEndRetryButton, songEndSelectionButton, songEndMainMenuButton };

        for (int i = 0; i < buttons.Length; i++)

        {

            Button button = buttons[i];

            if (button == null)

                continue;



            bool isSelected = i == Mathf.Clamp(selectedIndex, 0, buttons.Length - 1);

            bool isPrimaryAction = i == 0;

            Color idleBorderColor = isPrimaryAction

                ? new Color(GlobalPrimaryAccentColor.r, GlobalPrimaryAccentColor.g, GlobalPrimaryAccentColor.b, 0.92f)

                : new Color(0f, 0f, 0f, 0f);

            Color idleTextColor = isPrimaryAction

                ? GameplayHudPrimaryTextColor

                : GameplayHudSecondaryTextColor;

            Color idleBackgroundColor = isPrimaryAction

                ? new Color(GlobalPrimaryAccentColor.r, GlobalPrimaryAccentColor.g, GlobalPrimaryAccentColor.b, 0.84f)

                : new Color(0f, 0f, 0f, 0f);

            Color activeBorderColor = isPrimaryAction ? GlobalPrimaryAccentColor : GameplayHudAccentCoolColor;

            Color activeBackgroundColor = isPrimaryAction

                ? new Color(GlobalPrimaryAccentColor.r, GlobalPrimaryAccentColor.g, GlobalPrimaryAccentColor.b, 0.94f)

                : new Color(GameplayHudAccentCoolColor.r, GameplayHudAccentCoolColor.g, GameplayHudAccentCoolColor.b, 0.12f);



            if (isSelected)

            {

                button.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));

                button.style.translate = new Translate(0f, -2f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = activeBackgroundColor;

                button.style.color = GameplayHudPrimaryTextColor;

                button.style.borderTopColor = activeBorderColor;

                button.style.borderRightColor = activeBorderColor;

                button.style.borderBottomColor = activeBorderColor;

                button.style.borderLeftColor = activeBorderColor;

            }

            else

            {

                button.style.scale = new Scale(Vector3.one);

                button.style.translate = new Translate(0f, 0f);

                button.style.backgroundImage = StyleKeyword.None;

                button.style.backgroundColor = idleBackgroundColor;

                button.style.color = idleTextColor;

                button.style.borderTopColor = idleBorderColor;

                button.style.borderRightColor = idleBorderColor;

                button.style.borderBottomColor = idleBorderColor;

                button.style.borderLeftColor = idleBorderColor;

            }

        }

    }



    private void UpdateSongSettingsSelectorArrows(Button leftArrow, Button rightArrow, bool isSelected)

    {

        DisplayStyle display = isSelected ? DisplayStyle.Flex : DisplayStyle.None;

        Color activeColor = LibraryConfirmedSongColor;



        if (leftArrow != null)

        {

            leftArrow.style.display = display;

            leftArrow.style.color = activeColor;

        }



        if (rightArrow != null)

        {

            rightArrow.style.display = display;

            rightArrow.style.color = activeColor;

        }

    }



    private Button CreateSongSettingsSelectorArrowButton(string text, Action onClick, int selectionIndex)

    {

        Button button = new Button(() => onClick?.Invoke()) { text = text };

        button.focusable = false;

        button.style.display = DisplayStyle.None;

        button.style.position = Position.Absolute;

        button.style.top = 0f;

        button.style.bottom = 0f;

        button.style.width = 110f;

        button.style.height = 110f;

        button.style.marginLeft = 0f;

        button.style.marginRight = 0f;

        button.style.alignSelf = Align.Center;

        button.style.backgroundColor = new Color(0f, 0f, 0f, 0f);

        button.style.backgroundImage = StyleKeyword.None;

        button.style.color = LibraryConfirmedSongColor;

        button.style.fontSize = 76f;

        button.style.unityFontDefinition = modernUiFontDefinition;

        button.style.unityFontStyleAndWeight = FontStyle.Bold;

        button.style.unityTextAlign = TextAnchor.MiddleCenter;

        button.style.borderTopWidth = 0f;

        button.style.borderRightWidth = 0f;

        button.style.borderBottomWidth = 0f;

        button.style.borderLeftWidth = 0f;

        button.RegisterCallback<MouseEnterEvent>(_ =>

        {

            owner?.HoverSongSettingsSelectionFromUi(selectionIndex);

            button.style.scale = new Scale(new Vector3(1.08f, 1.08f, 1f));

        });

        button.RegisterCallback<MouseLeaveEvent>(_ => button.style.scale = new Scale(Vector3.one));

        return button;

    }



    private Label CreateSongSettingsHelpLabel(string text)

    {

        Label label = CreateLabel(text, 24f, new Color(0.72f, 0.79f, 0.86f, 0.96f), false, TextAnchor.MiddleCenter, useTitleFont: false);

        label.style.unityFontDefinition = modernUiFontDefinition;

        label.style.whiteSpace = WhiteSpace.Normal;

        label.style.unityTextAlign = TextAnchor.MiddleCenter;

        label.style.maxWidth = 680f;

        label.style.alignSelf = Align.Center;

        label.style.marginTop = -6f;

        label.style.marginBottom = 16f;

        return label;

    }



    private void UpdateSongSettingsSliderSelection(Label label, Slider slider, bool isSelected)

    {

        if (label == null || slider == null)

            return;



        label.style.color = isSelected

            ? LibraryConfirmedSongTextColor

            : new Color(0.96f, 0.98f, 1f, 1f);

        label.style.backgroundImage = isSelected

            ? new StyleBackground(GetLibraryConfirmedSongGradientTexture())

            : StyleKeyword.None;

        label.style.backgroundColor = isSelected

            ? LibraryConfirmedSongColor

            : new Color(0f, 0f, 0f, 0f);

        label.style.paddingLeft = isSelected ? 18f : 0f;

        label.style.paddingRight = isSelected ? 18f : 0f;

        label.style.paddingTop = isSelected ? 12f : 0f;

        label.style.paddingBottom = isSelected ? 12f : 0f;

        label.style.borderTopLeftRadius = isSelected ? 14f : 0f;

        label.style.borderTopRightRadius = isSelected ? 14f : 0f;

        label.style.borderBottomLeftRadius = isSelected ? 14f : 0f;

        label.style.borderBottomRightRadius = isSelected ? 14f : 0f;

        slider.style.opacity = isSelected ? 1f : 0.72f;

    }



    private static void AddBottomRightPrimaryButtons(VisualElement container, params Button[] buttons)

    {

        if (container == null || buttons == null || buttons.Length == 0)

            return;



        const string spacerName = "primary-actions-dock-spacer";

        const string dockName = "primary-actions-dock";



        container.style.position = Position.Relative;



        VisualElement existingDock = container.Q<VisualElement>(dockName);

        if (existingDock != null)

            existingDock.RemoveFromHierarchy();



        VisualElement spacer = container.Q<VisualElement>(spacerName);

        if (spacer == null)

        {

            spacer = new VisualElement();

            spacer.name = spacerName;

            spacer.style.height = 96f;

            spacer.style.flexShrink = 0f;

            spacer.style.marginTop = 18f;

            spacer.pickingMode = PickingMode.Ignore;

            container.Add(spacer);

        }



        VisualElement dock = new VisualElement();

        dock.name = dockName;

        dock.style.position = Position.Absolute;

        dock.style.right = 0f;

        dock.style.bottom = 0f;

        dock.style.flexDirection = FlexDirection.Row;

        dock.style.justifyContent = Justify.FlexEnd;

        dock.style.alignItems = Align.Center;

        dock.style.paddingRight = 12f;

        dock.style.paddingBottom = 12f;



        foreach (Button button in buttons)

        {

            if (button == null)

                continue;



            button.style.marginLeft = 12f;

            button.style.marginTop = 0f;

            button.style.marginBottom = 0f;

            dock.Add(button);

        }



        container.Add(dock);

    }





    private static void ApplyDefaultButtonEdgeColor(Button button)

    {

        if (button == null)

            return;



        Color buttonBorderColor = new Color(0.30f, 0.50f, 0.90f, 0.88f);

        button.style.borderTopColor = buttonBorderColor;

        button.style.borderRightColor = buttonBorderColor;

        button.style.borderBottomColor = buttonBorderColor;

        button.style.borderLeftColor = buttonBorderColor;

    }



    private static void ApplyButtonEdgeColorByLabel(Button button, string text)

    {

        if (button == null)

            return;



        string normalized = (text ?? string.Empty).Trim();

        Color accent = new Color(0.30f, 0.50f, 0.90f, 0.88f);



        if (normalized.StartsWith("Loop", StringComparison.OrdinalIgnoreCase))

            accent = new Color(1.00f, 0.20f, 0.20f, 0.94f);

        else if (ContainsIgnoreCase(normalized, "Resume") || ContainsIgnoreCase(normalized, "Continue"))

            accent = new Color(0.19f, 0.81f, 0.55f, 0.94f);

        else if (ContainsIgnoreCase(normalized, "Back") || ContainsIgnoreCase(normalized, "Main Menu"))

            accent = new Color(0.57f, 0.67f, 0.94f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Song Selection") || ContainsIgnoreCase(normalized, "Library"))

            accent = new Color(0.31f, 0.79f, 0.94f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Song Settings") || ContainsIgnoreCase(normalized, "Settings"))

            accent = new Color(0.66f, 0.56f, 0.95f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Tone Lab"))

            accent = new Color(0.17f, 0.84f, 0.85f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Track") || ContainsIgnoreCase(normalized, "Offset"))

            accent = new Color(0.95f, 0.74f, 0.33f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Up") || ContainsIgnoreCase(normalized, "Down") || ContainsIgnoreCase(normalized, "Refresh"))

            accent = new Color(0.37f, 0.67f, 0.97f, 0.90f);

        else if (ContainsIgnoreCase(normalized, "Folder"))

            accent = new Color(0.41f, 0.81f, 0.58f, 0.92f);

        else if (ContainsIgnoreCase(normalized, "Exit"))

            accent = new Color(0.92f, 0.37f, 0.45f, 0.92f);



        Color top = new Color(Mathf.Clamp01(accent.r * 1.12f), Mathf.Clamp01(accent.g * 1.12f), Mathf.Clamp01(accent.b * 1.12f), accent.a);

        Color side = new Color(Mathf.Clamp01(accent.r * 0.92f), Mathf.Clamp01(accent.g * 0.92f), Mathf.Clamp01(accent.b * 0.92f), accent.a);

        Color bottom = new Color(Mathf.Clamp01(accent.r * 0.70f), Mathf.Clamp01(accent.g * 0.70f), Mathf.Clamp01(accent.b * 0.70f), accent.a);



        button.style.borderTopColor = top;

        button.style.borderRightColor = side;

        button.style.borderBottomColor = bottom;

        button.style.borderLeftColor = side;

    }



    private static bool ContainsIgnoreCase(string source, string token)

    {

        if (string.IsNullOrEmpty(source) || string.IsNullOrEmpty(token))

            return false;



        return source.IndexOf(token, StringComparison.OrdinalIgnoreCase) >= 0;

    }



    private static VisualElement CreatePedalKnob()

    {

        VisualElement knob = new VisualElement();

        knob.style.width = 30f;

        knob.style.height = 30f;

        knob.style.backgroundColor = new Color(0.20f, 0.27f, 0.34f, 1f);

        knob.style.borderTopWidth = 3f;

        knob.style.borderRightWidth = 3f;

        knob.style.borderBottomWidth = 4f;

        knob.style.borderLeftWidth = 3f;

        knob.style.borderTopColor = new Color(0.51f, 0.66f, 0.75f, 1f);

        knob.style.borderRightColor = new Color(0.06f, 0.16f, 0.23f, 1f);

        knob.style.borderBottomColor = new Color(0.03f, 0.11f, 0.16f, 1f);

        knob.style.borderLeftColor = new Color(0.06f, 0.16f, 0.23f, 1f);

        knob.style.borderTopLeftRadius = 999f;

        knob.style.borderTopRightRadius = 999f;

        knob.style.borderBottomLeftRadius = 999f;

        knob.style.borderBottomRightRadius = 999f;

        knob.style.marginLeft = 10f;

        knob.style.marginRight = 10f;



        VisualElement indicator = new VisualElement();

        indicator.name = "knob-indicator";

        indicator.style.position = Position.Absolute;

        indicator.style.width = 3f;

        indicator.style.height = 11f;

        indicator.style.left = 13f;

        indicator.style.top = 4f;

        indicator.style.backgroundColor = new Color(0.96f, 0.98f, 1f, 0.98f);

        indicator.style.borderTopLeftRadius = 2f;

        indicator.style.borderTopRightRadius = 2f;

        indicator.style.borderBottomLeftRadius = 2f;

        indicator.style.borderBottomRightRadius = 2f;

        knob.Add(indicator);

        return knob;

    }



    private static void SetKnobIndicatorAngle(VisualElement knob, float degrees)

    {

        if (knob == null)

            return;



        VisualElement indicator = knob.Q<VisualElement>("knob-indicator");

        if (indicator == null)

            return;



        indicator.style.rotate = new Rotate(new Angle(degrees, AngleUnit.Degree));

    }



    private static void SetKnobIndicatorSize(VisualElement knob, float knobSize)

    {

        if (knob == null)

            return;



        VisualElement indicator = knob.Q<VisualElement>("knob-indicator");

        if (indicator == null)

            return;



        float indicatorWidth = Mathf.Clamp(knobSize * 0.10f, 2f, 5f);

        float indicatorHeight = Mathf.Clamp(knobSize * 0.35f, 8f, 16f);

        indicator.style.width = indicatorWidth;

        indicator.style.height = indicatorHeight;

        indicator.style.left = (knobSize - indicatorWidth) * 0.5f;

        indicator.style.top = Mathf.Clamp(knobSize * 0.12f, 3f, 8f);

    }



    private void LayoutInputMeterGraphics(float meterWidth, float meterHeight)

    {

        float inset = Mathf.Clamp(meterWidth * 0.08f, 10f, 18f);

        float arcViewportTop = Mathf.Clamp(meterHeight * 0.12f, 8f, 14f);

        float arcViewportHeight = Mathf.Clamp(meterHeight * 0.48f, 28f, 52f);

        float arcHeight = arcViewportHeight * 2f;

        float arcWidth = Mathf.Max(1f, meterWidth - (inset * 2f));

        float rx = arcWidth * 0.5f;

        float ry = arcHeight * 0.5f;

        float centerX = meterWidth * 0.5f;

        float centerY = arcViewportTop + ry;



        inputMeterArcViewport.style.left = inset;

        inputMeterArcViewport.style.right = inset;

        inputMeterArcViewport.style.top = arcViewportTop;

        inputMeterArcViewport.style.height = arcViewportHeight;



        inputMeterArc.style.height = arcHeight;

        inputMeterArc.style.borderTopLeftRadius = arcHeight;

        inputMeterArc.style.borderTopRightRadius = arcHeight;

        inputMeterArc.style.borderBottomLeftRadius = arcHeight;

        inputMeterArc.style.borderBottomRightRadius = arcHeight;



        int tickCount = inputMeterTicks.Count;

        for (int i = 0; i < tickCount; i++)

        {

            VisualElement tick = inputMeterTicks[i];

            if (tick == null)

                continue;



            float t = tickCount <= 1 ? 0f : i / (tickCount - 1f);

            float theta = Mathf.Lerp(Mathf.PI * 0.96f, Mathf.PI * 0.04f, t);

            float arcX = centerX + Mathf.Cos(theta) * rx;

            float arcY = centerY - Mathf.Sin(theta) * ry;

            float tickHeight = i % 2 == 0 ? Mathf.Clamp(meterHeight * 0.13f, 8f, 14f) : Mathf.Clamp(meterHeight * 0.08f, 5f, 9f);

            float tickWidth = i % 2 == 0 ? 3f : 2f;



            tick.style.width = tickWidth;

            tick.style.height = tickHeight;

            tick.style.left = arcX - (tickWidth * 0.5f);

            tick.style.top = arcY - 1f;

        }



        float capSize = Mathf.Clamp(meterHeight * 0.16f, 10f, 16f);

        float pivotY = arcViewportTop + arcViewportHeight + Mathf.Clamp(meterHeight * 0.10f, 6f, 12f);

        float needleHeight = Mathf.Clamp(meterHeight * 0.42f, 24f, 44f);

        inputMeterNeedle.style.height = needleHeight;

        inputMeterNeedle.style.left = centerX - 1.5f;

        inputMeterNeedle.style.top = pivotY - needleHeight;



        inputMeterNeedleCap.style.width = capSize;

        inputMeterNeedleCap.style.height = capSize;

        inputMeterNeedleCap.style.left = centerX - (capSize * 0.5f);

        inputMeterNeedleCap.style.top = pivotY - (capSize * 0.5f);

        inputMeterNeedleCap.style.borderTopLeftRadius = capSize * 0.5f;

        inputMeterNeedleCap.style.borderTopRightRadius = capSize * 0.5f;

        inputMeterNeedleCap.style.borderBottomLeftRadius = capSize * 0.5f;

        inputMeterNeedleCap.style.borderBottomRightRadius = capSize * 0.5f;

    }



    private static VisualElement CreateFootswitch()

    {

        VisualElement footswitch = new VisualElement();

        footswitch.style.width = 46f;

        footswitch.style.height = 46f;

        footswitch.style.backgroundColor = new Color(0.81f, 0.85f, 0.90f, 1f);

        footswitch.style.borderTopWidth = 3f;

        footswitch.style.borderRightWidth = 3f;

        footswitch.style.borderBottomWidth = 6f;

        footswitch.style.borderLeftWidth = 3f;

        footswitch.style.borderTopColor = new Color(0.98f, 0.99f, 1f, 1f);

        footswitch.style.borderRightColor = new Color(0.45f, 0.52f, 0.58f, 1f);

        footswitch.style.borderBottomColor = new Color(0.23f, 0.28f, 0.33f, 1f);

        footswitch.style.borderLeftColor = new Color(0.45f, 0.52f, 0.58f, 1f);

        footswitch.style.borderTopLeftRadius = 23f;

        footswitch.style.borderTopRightRadius = 23f;

        footswitch.style.borderBottomLeftRadius = 23f;

        footswitch.style.borderBottomRightRadius = 23f;

        return footswitch;

    }



    private static VisualElement CreatePedalJack()

    {

        VisualElement jack = new VisualElement();

        jack.name = "pedal-jack";

        jack.style.width = 20f;

        jack.style.height = 52f;

        jack.style.flexDirection = FlexDirection.Row;

        jack.style.alignItems = Align.Center;

        jack.style.justifyContent = Justify.FlexStart;



        VisualElement jackOuter = new VisualElement();

        jackOuter.name = "pedal-jack-outer";

        jackOuter.style.width = 8f;

        jackOuter.style.height = 38f;

        jackOuter.style.backgroundColor = new Color(0.33f, 0.36f, 0.40f, 1f);

        jackOuter.style.borderTopWidth = 2f;

        jackOuter.style.borderRightWidth = 1f;

        jackOuter.style.borderBottomWidth = 3f;

        jackOuter.style.borderLeftWidth = 2f;

        jackOuter.style.borderTopColor = new Color(0.54f, 0.58f, 0.64f, 1f);

        jackOuter.style.borderRightColor = new Color(0.22f, 0.25f, 0.29f, 1f);

        jackOuter.style.borderBottomColor = new Color(0.12f, 0.14f, 0.17f, 1f);

        jackOuter.style.borderLeftColor = new Color(0.26f, 0.30f, 0.34f, 1f);



        VisualElement jackInner = new VisualElement();

        jackInner.name = "pedal-jack-inner";

        jackInner.style.width = 12f;

        jackInner.style.height = 50f;

        jackInner.style.marginLeft = 0f;

        jackInner.style.backgroundColor = new Color(0.27f, 0.30f, 0.34f, 1f);

        jackInner.style.borderTopWidth = 1f;

        jackInner.style.borderRightWidth = 1f;

        jackInner.style.borderBottomWidth = 2f;

        jackInner.style.borderLeftWidth = 1f;

        jackInner.style.borderTopColor = new Color(0.47f, 0.52f, 0.58f, 1f);

        jackInner.style.borderRightColor = new Color(0.17f, 0.20f, 0.24f, 1f);

        jackInner.style.borderBottomColor = new Color(0.09f, 0.11f, 0.14f, 1f);

        jackInner.style.borderLeftColor = new Color(0.20f, 0.24f, 0.28f, 1f);



        VisualElement jackReflection = new VisualElement();

        jackReflection.name = "pedal-jack-reflection";

        jackReflection.style.position = Position.Absolute;

        jackReflection.style.left = 1f;

        jackReflection.style.top = 4f;

        jackReflection.style.width = 2f;

        jackReflection.style.height = 16f;

        jackReflection.style.backgroundColor = new Color(0.80f, 0.86f, 0.92f, 0.30f);



        jackOuter.Add(jackReflection);

        jack.Add(jackOuter);

        jack.Add(jackInner);

        return jack;

    }



    private static void SetPedalJackSize(VisualElement jack, float width, float height)

    {

        if (jack == null)

            return;



        jack.style.width = width;

        jack.style.height = height;



        VisualElement jackOuter = jack.Q<VisualElement>("pedal-jack-outer");

        if (jackOuter != null)

        {

            float outerWidth = Mathf.Clamp(width * 0.40f, 6f, 16f);

            float outerHeight = Mathf.Clamp(height * 0.74f, 18f, 50f);

            jackOuter.style.width = outerWidth;

            jackOuter.style.height = outerHeight;

        }



        VisualElement jackInner = jack.Q<VisualElement>("pedal-jack-inner");

        if (jackInner != null)

        {

            float innerWidth = Mathf.Clamp(width * 0.60f, 10f, 22f);

            float innerHeight = Mathf.Clamp(height * 0.94f, 24f, 64f);

            jackInner.style.width = innerWidth;

            jackInner.style.height = innerHeight;

            jackInner.style.marginLeft = 0f;

        }



        VisualElement jackReflection = jack.Q<VisualElement>("pedal-jack-reflection");

        if (jackReflection != null)

        {

            float outerWidth = jackOuter != null ? jackOuter.resolvedStyle.width : width * 0.34f;

            float outerHeight = jackOuter != null ? jackOuter.resolvedStyle.height : height * 0.74f;

            jackReflection.style.left = Mathf.Clamp(outerWidth * 0.16f, 1f, 4f);

            jackReflection.style.top = Mathf.Clamp(outerHeight * 0.12f, 1f, 7f);

            jackReflection.style.width = Mathf.Clamp(outerWidth * 0.22f, 2f, 5f);

            jackReflection.style.height = Mathf.Clamp(outerHeight * 0.44f, 10f, 26f);

        }

    }



    private static VisualElement CreateFullscreenOverlay()

    {

        VisualElement overlay = new VisualElement();

        overlay.style.position = Position.Absolute;

        overlay.style.left = 0f;

        overlay.style.right = 0f;

        overlay.style.top = 0f;

        overlay.style.bottom = 0f;

        overlay.style.alignItems = Align.Center;

        overlay.style.justifyContent = Justify.FlexStart;

        overlay.style.paddingTop = 66f;

        overlay.style.backgroundColor = new Color(0.01f, 0.01f, 0.03f, 0.84f);

        return overlay;

    }



    private static void StyleCard(VisualElement element, Color backgroundColor, float radius = 16f)

    {

        element.style.backgroundColor = backgroundColor;

        element.style.borderTopLeftRadius = radius;

        element.style.borderTopRightRadius = radius;

        element.style.borderBottomLeftRadius = radius;

        element.style.borderBottomRightRadius = radius;

        element.style.borderTopWidth = 2f;

        element.style.borderBottomWidth = 1f;

        element.style.borderLeftWidth = 1f;

        element.style.borderRightWidth = 1f;

        Color borderColor = new Color(0.50f, 0.47f, 0.82f, 0.95f);

        element.style.borderTopColor = borderColor;

        element.style.borderBottomColor = borderColor;

        element.style.borderLeftColor = borderColor;

        element.style.borderRightColor = borderColor;

    }

    private static void StyleGameplayHudCard(VisualElement element, Color backgroundColor, float radius = 16f)

    {

        StyleCard(element, backgroundColor, radius);

        element.style.position = Position.Relative;

        element.style.overflow = Overflow.Hidden;

        element.style.borderTopWidth = 1f;

        element.style.borderRightWidth = 1f;

        element.style.borderBottomWidth = 1f;

        element.style.borderLeftWidth = 1f;

        element.style.borderTopColor = GameplayHudCardBorderTopColor;

        element.style.borderRightColor = GameplayHudCardBorderSideColor;

        element.style.borderBottomColor = GameplayHudCardBorderBottomColor;

        element.style.borderLeftColor = GameplayHudCardBorderSideColor;

    }

    private static void StyleGameplayHudChip(VisualElement element)

    {

        element.style.paddingLeft = 12f;

        element.style.paddingRight = 12f;

        element.style.paddingTop = 7f;

        element.style.paddingBottom = 7f;

        element.style.backgroundColor = GameplayHudChipBackgroundColor;

        element.style.borderTopLeftRadius = 999f;

        element.style.borderTopRightRadius = 999f;

        element.style.borderBottomLeftRadius = 999f;

        element.style.borderBottomRightRadius = 999f;

        element.style.borderTopWidth = 1f;

        element.style.borderRightWidth = 1f;

        element.style.borderBottomWidth = 1f;

        element.style.borderLeftWidth = 1f;

        element.style.borderTopColor = GameplayHudChipBorderColor;

        element.style.borderRightColor = GameplayHudChipBorderColor;

        element.style.borderBottomColor = GameplayHudChipBorderColor;

        element.style.borderLeftColor = GameplayHudChipBorderColor;

        element.style.alignItems = Align.Center;

        element.style.justifyContent = Justify.Center;

    }

    private static VisualElement CreateGameplayHudAccentLine(Color color, bool anchorToBottom = false)

    {

        VisualElement line = new VisualElement();

        line.style.position = Position.Absolute;

        line.style.left = 18f;

        line.style.right = 18f;

        if (anchorToBottom)

            line.style.bottom = 0f;

        else

            line.style.top = 0f;

        line.style.height = 2f;

        line.style.backgroundColor = color;

        line.style.borderTopLeftRadius = 1f;

        line.style.borderTopRightRadius = 1f;

        line.style.borderBottomLeftRadius = 1f;

        line.style.borderBottomRightRadius = 1f;

        line.pickingMode = PickingMode.Ignore;

        return line;

    }



    private static void ApplyFont(VisualElement root, FontDefinition font)

    {

        root.style.unityFontDefinition = font;

        foreach (VisualElement child in root.Children())

            ApplyFont(child, font);

    }



    private static Texture2D TrimTextureWhitespace(Texture2D source)

    {

        if (source == null)

            return null;



        try

        {

            Color32[] pixels = source.GetPixels32();

            if (pixels == null || pixels.Length == 0)

                return null;



            int width = source.width;

            int height = source.height;

            int minX = width;

            int minY = height;

            int maxX = -1;

            int maxY = -1;



            for (int y = 0; y < height; y++)

            {

                for (int x = 0; x < width; x++)

                {

                    Color32 pixel = pixels[(y * width) + x];

                    bool isTransparent = pixel.a < 12;

                    bool isNearWhite = pixel.r > 245 && pixel.g > 245 && pixel.b > 245;

                    if (isTransparent || isNearWhite)

                        continue;



                    if (x < minX) minX = x;

                    if (y < minY) minY = y;

                    if (x > maxX) maxX = x;

                    if (y > maxY) maxY = y;

                }

            }



            if (maxX < minX || maxY < minY)

                return null;



            int padding = 12;

            minX = Mathf.Max(0, minX - padding);

            minY = Mathf.Max(0, minY - padding);

            maxX = Mathf.Min(width - 1, maxX + padding);

            maxY = Mathf.Min(height - 1, maxY + padding);



            int trimmedWidth = maxX - minX + 1;

            int trimmedHeight = maxY - minY + 1;

            if (trimmedWidth >= width && trimmedHeight >= height)

                return null;



            Texture2D trimmed = new Texture2D(trimmedWidth, trimmedHeight, TextureFormat.RGBA32, false);

            trimmed.wrapMode = TextureWrapMode.Clamp;

            trimmed.filterMode = FilterMode.Bilinear;

            trimmed.SetPixels(source.GetPixels(minX, minY, trimmedWidth, trimmedHeight));

            trimmed.Apply();

            return trimmed;

        }

        catch

        {

            return null;

        }

    }



    private static (Font body, Font title) ResolveUiFonts(Font fallbackFont)

    {

        Font body = LoadRuntimeFont("Fonts/PixelArtFont") ?? LoadRuntimeFont("PixelArtFont");

        Font title = LoadRuntimeFont("rock") ?? LoadRuntimeFont("Fonts/rock");

        title ??= LoadRuntimeFont("rock-italic") ?? LoadRuntimeFont("Fonts/rock-italic");

        title ??= LoadRuntimeFont("Fonts/ArcadeFont") ?? LoadRuntimeFont("ArcadeFont");



        body ??= LoadProjectFont("Assets/UI/PixelArtFont.TTF");

        title ??= LoadProjectFont("Assets/Resources/rock.ttf");

        title ??= LoadProjectFont("Assets/Resources/rock-italic.ttf");

        title ??= LoadProjectFont("Assets/UI/ArcadeFont.ttf");



        body ??= TryFindFontByName("pixelartfont", "pixel_art", "pixel");

        title ??= TryFindFontByName("rock", "rock-italic", "arcadefont", "arcade", "shadow");



        body ??= fallbackFont;

        title ??= body ?? fallbackFont;



        return (body, title);

    }



    private static Font TryFindFontByName(params string[] keywords)

    {

        if (keywords == null || keywords.Length == 0)

            return null;



        Font[] availableFonts = Resources.FindObjectsOfTypeAll<Font>();

        Font best = null;



        foreach (Font font in availableFonts)

        {

            if (font == null || string.IsNullOrWhiteSpace(font.name))

                continue;



            string normalized = font.name.ToLowerInvariant();

            for (int i = 0; i < keywords.Length; i++)

            {

                string keyword = keywords[i];

                if (string.IsNullOrWhiteSpace(keyword))

                    continue;



                string normalizedKeyword = keyword.ToLowerInvariant();

                if (!normalized.Contains(normalizedKeyword))

                    continue;



                if (normalized == normalizedKeyword)

                    return font;



                best ??= font;

            }

        }



        return best;

    }



    private static Font LoadProjectFont(string assetPath)

    {

#if UNITY_EDITOR

        if (string.IsNullOrWhiteSpace(assetPath))

            return null;



        return AssetDatabase.LoadAssetAtPath<Font>(assetPath);

#else

        return null;

#endif

    }



    private static Font LoadRuntimeFont(string resourcesPath)

    {

        if (string.IsNullOrWhiteSpace(resourcesPath))

            return null;



        return Resources.Load<Font>(resourcesPath);

    }



    private static string FormatTrackName(string trackDisplayName)

    {

        if (string.IsNullOrWhiteSpace(trackDisplayName))

            return "Default";



        int metricsIndex = trackDisplayName.IndexOf(" [", StringComparison.Ordinal);

        string trimmed = metricsIndex >= 0 ? trackDisplayName.Substring(0, metricsIndex) : trackDisplayName;



        if (trimmed.StartsWith("Auto (", StringComparison.OrdinalIgnoreCase) && trimmed.EndsWith(")", StringComparison.Ordinal))

            trimmed = trimmed.Substring(6, trimmed.Length - 7);



        return trimmed.Trim();

    }



    private static PanelSettings sharedPanelSettings;



    private static PanelSettings ResolvePanelSettings()

    {

        if (sharedPanelSettings != null)

            return sharedPanelSettings;



        PanelSettings existing = Resources.FindObjectsOfTypeAll<PanelSettings>()

            .Where(candidate => candidate != null)

            .OrderByDescending(candidate => candidate.themeStyleSheet != null)

            .ThenByDescending(candidate => candidate.textSettings != null)

            .ThenByDescending(candidate => candidate.name == "PanelSettings")

            .FirstOrDefault();



        if (existing != null)

        {

            sharedPanelSettings = existing;

            return sharedPanelSettings;

        }



        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");

        sharedPanelSettings = runtimeAsset != null

            ? ScriptableObject.Instantiate(runtimeAsset)

            : ScriptableObject.CreateInstance<PanelSettings>();

        sharedPanelSettings.name = "TabsSongHeaderRuntimePanelSettings";

        return sharedPanelSettings;

    }



    private static void EnsurePanelSettingsSupportAssets(PanelSettings settings)

    {

        if (settings == null)

            return;



        if (settings.themeStyleSheet == null)

            settings.themeStyleSheet = Resources.FindObjectsOfTypeAll<ThemeStyleSheet>().FirstOrDefault();



        if (settings.textSettings == null)

            settings.textSettings = Resources.FindObjectsOfTypeAll<PanelTextSettings>().FirstOrDefault();

    }



    private void UpdateCharacterHealthHearts(int misses, int totalHearts)

    {

        displayedCharacterHealthHeartCount = Mathf.Clamp(totalHearts, 1, CharacterHealthHeartCount);
        int activeHearts = Mathf.Clamp(displayedCharacterHealthHeartCount - Mathf.Max(0, misses), 0, displayedCharacterHealthHeartCount);

        for (int i = 0; i < characterHealthHeartLabels.Count; i++)

        {

            Label heartLabel = characterHealthHeartLabels[i];

            heartLabel.style.display = i < displayedCharacterHealthHeartCount ? DisplayStyle.Flex : DisplayStyle.None;
            bool isActive = i < activeHearts;

            heartLabel.style.color = isActive ? GameplayHudHealthHeartColor : GameplayHudHealthHeartEmptyColor;

            heartLabel.style.opacity = isActive ? 0.98f : 0.74f;

        }

    }



    private void ApplyResponsiveSizing(bool force)

    {

        int screenHeight = Mathf.Max(1, Screen.height);

        int screenWidth = Mathf.Max(1, Screen.width);

        if (!force && screenHeight == lastScreenHeight && screenWidth == lastScreenWidth)

            return;



        lastScreenHeight = screenHeight;

        lastScreenWidth = screenWidth;

        bool isHighway3D = owner != null && owner.renderMode == GuitarRenderMode.Highway3D;



        // Menu and library should keep their authored size on 1080p and larger displays.

        // Only smaller screens should effectively compress them.

        float menuLayoutWidth = Mathf.Min(screenWidth, 1600f);

        float menuLayoutHeight = Mathf.Min(screenHeight, 900f);

        float menuLayoutScale = Mathf.Clamp(menuLayoutHeight / 900f, 0.82f, 1f);



        float songSize = Mathf.Clamp(screenHeight * 0.052f, 40f, 64f);

        float pauseSize = Mathf.Clamp(screenHeight * 0.135f, 112f, 170f);

        float bodySize = Mathf.Clamp(screenHeight * 0.036f, 30f, 50f);

        float menuTitleSize = Mathf.Clamp(menuLayoutHeight * 0.105f, 70f, 126f);

        float menuSubtitleSize = Mathf.Clamp(menuLayoutHeight * 0.038f, 30f, 46f);

        float menuEyebrowSize = Mathf.Clamp(menuLayoutHeight * 0.026f, 22f, 32f);

        float menuItemTitleSize = Mathf.Clamp(menuLayoutHeight * 0.068f, 58f, 88f);

        float menuItemSubtitleSize = Mathf.Clamp(menuLayoutHeight * 0.020f, 17f, 22f);

        float menuSongSize = Mathf.Clamp(menuLayoutHeight * 0.055f, 34f, 58f);

        float menuMetaSize = Mathf.Clamp(menuLayoutHeight * 0.024f, 20f, 30f);

        float pedalWidth = Mathf.Clamp(Screen.width * 0.15f, 430f, 620f);

        float pedalHeight = Mathf.Clamp(screenHeight * 0.30f, 280f, 560f);

        float knobSize = Mathf.Clamp(pedalHeight * 0.24f, 42f, 78f);

        float ledSize = Mathf.Clamp(knobSize * 0.42f, 12f, 20f);

        float footswitchSize = Mathf.Clamp(pedalHeight * 0.23f, 42f, 74f);

        float meterWidth = Mathf.Clamp(pedalWidth * 0.34f, 200f, 300f);

        float meterHeight = Mathf.Clamp(pedalHeight * 0.30f, 72f, 120f);



        songNameLabel.style.fontSize = songSize;

        trackNameLabel.style.fontSize = Mathf.Clamp(bodySize * 0.52f, 18f, 25f);

        speedBadgeLabel.style.fontSize = Mathf.Clamp(bodySize * 0.48f, 17f, 23f);

        detectorStatusLabel.style.fontSize = Mathf.Clamp(bodySize * 0.48f, 17f, 23f);

        statusDotLabel.style.fontSize = bodySize * 0.66f;

        float heartDensityScale = Mathf.Clamp(8f / Mathf.Max(5f, displayedCharacterHealthHeartCount), 0.34f, 1f);
        float characterHealthHeartSize = Mathf.Clamp(bodySize * 1.74f * heartDensityScale, 22f, 82f);

        float characterHealthHeartBoxSize = Mathf.Clamp(bodySize * 1.14f * heartDensityScale, 18f, 50f);

        float characterHealthRowScale = Mathf.Clamp(screenHeight / 900f, 0.78f, 1.08f);

        characterHealthRow.style.height = characterHealthHeartBoxSize;

        characterHealthRow.style.marginBottom = Mathf.Clamp(bodySize * 0.36f, 10f, 16f);

        characterHealthRow.style.scale = new Scale(new Vector3(characterHealthRowScale, characterHealthRowScale, 1f));

        for (int i = 0; i < characterHealthHeartLabels.Count; i++)

        {

            Label heartLabel = characterHealthHeartLabels[i];

            heartLabel.style.fontSize = characterHealthHeartSize;

            heartLabel.style.width = characterHealthHeartBoxSize;

            heartLabel.style.height = characterHealthHeartBoxSize;

            heartLabel.style.marginRight = i < characterHealthHeartLabels.Count - 1
                ? Mathf.Clamp(bodySize * 0.20f * heartDensityScale, 2f, 11f)
                : 0f;

        }

        float techniqueLegendSize = bodySize * 0.66f;

        foreach (Label iconLabel in techniqueLegendIconLabels)

            iconLabel.style.fontSize = techniqueLegendSize;

        foreach (Label textLabel in techniqueLegendTextLabels)

            textLabel.style.fontSize = techniqueLegendSize;

        mainMenuEyebrowLabel.style.fontSize = menuEyebrowSize;

        mainMenuTitleLabel.style.fontSize = menuTitleSize;

        mainMenuSubtitleLabel.style.fontSize = menuSubtitleSize;

        mainMenuFooterHintLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.020f, 17f, 24f);

        mainMenuCurrentSongValueLabel.style.fontSize = menuSongSize;

        mainMenuCurrentTrackValueLabel.style.fontSize = menuMetaSize;

        mainMenuCurrentSpeedValueLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.030f, 24f, 36f);

        mainMenuCurrentDetectorValueLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.030f, 24f, 36f);

        foreach (MainMenuEntry entry in mainMenuEntries)

        {

            entry.button.style.minHeight = Mathf.Clamp(menuLayoutHeight * 0.145f, 108f, 148f);

            entry.arrowLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.062f, 48f, 70f);

            entry.titleLabel.style.fontSize = menuItemTitleSize;

            entry.subtitleLabel.style.fontSize = menuItemSubtitleSize;

        }

        scoreTitleLabel.style.fontSize = bodySize * 0.42f;

        scorePercentLabel.style.fontSize = bodySize * 1.44f;

        noteTallyLabel.style.fontSize = bodySize * 0.58f;

        float gameplayScoreTop = Mathf.Clamp(screenHeight * 0.010f, 10f, 24f);

        float gameplayScoreWidth = isHighway3D

            ? Mathf.Clamp(screenWidth * 0.20f, 380f, 640f)

            : Mathf.Clamp(screenWidth * 0.26f, 420f, 780f);

        float gameplayScoreHorizontalPadding = Mathf.Clamp(screenWidth * 0.008f, 22f, 36f);

        float gameplayScoreVerticalPadding = Mathf.Clamp(screenHeight * 0.009f, 12f, 22f);

        gameplayScoreDock.style.top = gameplayScoreTop;

        songCardScoreBlock.style.width = gameplayScoreWidth;

        songCardScoreBlock.style.minWidth = gameplayScoreWidth;

        songCardScoreBlock.style.maxWidth = gameplayScoreWidth;

        songCardScoreBlock.style.paddingLeft = gameplayScoreHorizontalPadding;

        songCardScoreBlock.style.paddingRight = gameplayScoreHorizontalPadding;

        songCardScoreBlock.style.paddingTop = gameplayScoreVerticalPadding;

        songCardScoreBlock.style.paddingBottom = gameplayScoreVerticalPadding;

        scorePedalBrandLabel.style.fontSize = Mathf.Clamp(bodySize * 0.43f, 14f, 24f);

        inputMeterLabel.style.fontSize = Mathf.Clamp(bodySize * 0.44f, 13f, 20f);

        inputMeterWrap.style.width = meterWidth;

        inputMeterFace.style.width = meterWidth;

        songProgressTrack.style.width = meterWidth;

        inputMeterFace.style.height = meterHeight;

        LayoutInputMeterGraphics(meterWidth, meterHeight);

        scorePedalBody.style.width = pedalWidth;

        float meterLabelFont = Mathf.Clamp(bodySize * 0.44f, 13f, 20f);

        float scoreTitleFont = bodySize * 0.48f;

        float scoreFont = bodySize * 1.30f;

        float tallyFont = bodySize * 0.58f;

        float meterLabelHeight = meterLabelFont * 1.45f;

        float scoreTitleLineHeight = scoreTitleFont * 1.35f;

        float scoreLineHeight = scoreFont * 1.35f;

        float tallyLineHeight = tallyFont * 1.65f;

        float screenPaddingAndSpacing = 10f + 8f + 8f + 1f + 1f + 12f;

        float requiredScreenHeight = meterHeight + meterLabelHeight + scoreTitleLineHeight + scoreLineHeight + tallyLineHeight + screenPaddingAndSpacing;



        float topRowHeight = Mathf.Max(ledSize, Mathf.Clamp(bodySize * 0.33f, 12f, 19f) * 1.25f);

        float fixedPedalContentHeight = 16f + topRowHeight + 6f + knobSize + 7f + 8f + footswitchSize + 16f;

        float minPedalHeightForContent = fixedPedalContentHeight + requiredScreenHeight;

        if (pedalHeight < minPedalHeightForContent)

        {

            pedalHeight = Mathf.Clamp(minPedalHeightForContent, 300f, 640f);

            knobSize = Mathf.Clamp(pedalHeight * 0.24f, 42f, 80f);

            ledSize = Mathf.Clamp(knobSize * 0.42f, 12f, 20f);

            footswitchSize = Mathf.Clamp(pedalHeight * 0.23f, 42f, 76f);

            meterHeight = Mathf.Clamp(pedalHeight * 0.30f, 72f, 130f);

            inputMeterFace.style.height = meterHeight;

            LayoutInputMeterGraphics(meterWidth, meterHeight);

            requiredScreenHeight = meterHeight + meterLabelHeight + scoreTitleLineHeight + scoreLineHeight + tallyLineHeight + screenPaddingAndSpacing;

        }



        scorePlate.style.height = pedalHeight + 44f;

        scorePedalBody.style.height = pedalHeight;

        float screenHeightTarget = Mathf.Max(140f, requiredScreenHeight);

        scorePedalScreen.style.height = screenHeightTarget;

        scorePedalScreen.style.minHeight = screenHeightTarget;

        scorePedalScreen.style.maxHeight = screenHeightTarget;



        float jackHeight = Mathf.Clamp(pedalHeight * 0.34f, 60f, 102f);

        float jackWidth = Mathf.Clamp(jackHeight * 0.44f, 22f, 40f);

        float jackOffset = Mathf.Max(0f, jackWidth);

        float jackTop = pedalHeight * 0.36f;

        SetPedalJackSize(scorePedalInputJack, jackWidth, jackHeight);

        scorePedalInputJack.style.left = -jackOffset;

        scorePedalInputJack.style.top = jackTop;

        SetPedalJackSize(scorePedalOutputJack, jackWidth, jackHeight);

        scorePedalOutputJack.style.right = -jackOffset;

        scorePedalOutputJack.style.top = jackTop;

        scorePedalKnobLeft.style.width = knobSize;

        scorePedalKnobLeft.style.height = knobSize;

        scorePedalKnobLeft.style.borderTopLeftRadius = knobSize * 0.5f;

        scorePedalKnobLeft.style.borderTopRightRadius = knobSize * 0.5f;

        scorePedalKnobLeft.style.borderBottomLeftRadius = knobSize * 0.5f;

        scorePedalKnobLeft.style.borderBottomRightRadius = knobSize * 0.5f;

        scorePedalKnobMid.style.width = knobSize;

        scorePedalKnobMid.style.height = knobSize;

        scorePedalKnobMid.style.borderTopLeftRadius = knobSize * 0.5f;

        scorePedalKnobMid.style.borderTopRightRadius = knobSize * 0.5f;

        scorePedalKnobMid.style.borderBottomLeftRadius = knobSize * 0.5f;

        scorePedalKnobMid.style.borderBottomRightRadius = knobSize * 0.5f;

        scorePedalKnobRight.style.width = knobSize;

        scorePedalKnobRight.style.height = knobSize;

        scorePedalKnobRight.style.borderTopLeftRadius = knobSize * 0.5f;

        scorePedalKnobRight.style.borderTopRightRadius = knobSize * 0.5f;

        scorePedalKnobRight.style.borderBottomLeftRadius = knobSize * 0.5f;

        scorePedalKnobRight.style.borderBottomRightRadius = knobSize * 0.5f;

        SetKnobIndicatorSize(scorePedalKnobLeft, knobSize);

        SetKnobIndicatorSize(scorePedalKnobMid, knobSize);

        SetKnobIndicatorSize(scorePedalKnobRight, knobSize);

        scorePedalLed.style.width = ledSize;

        scorePedalLed.style.height = ledSize;

        scorePedalLed.style.borderTopLeftRadius = ledSize * 0.5f;

        scorePedalLed.style.borderTopRightRadius = ledSize * 0.5f;

        scorePedalLed.style.borderBottomLeftRadius = ledSize * 0.5f;

        scorePedalLed.style.borderBottomRightRadius = ledSize * 0.5f;

        scorePedalFootswitch.style.width = footswitchSize;

        scorePedalFootswitch.style.height = footswitchSize;

        scorePedalFootswitch.style.borderTopLeftRadius = footswitchSize * 0.5f;

        scorePedalFootswitch.style.borderTopRightRadius = footswitchSize * 0.5f;

        scorePedalFootswitch.style.borderBottomLeftRadius = footswitchSize * 0.5f;

        scorePedalFootswitch.style.borderBottomRightRadius = footswitchSize * 0.5f;

        scorePedalFootswitchRight.style.width = footswitchSize;

        scorePedalFootswitchRight.style.height = footswitchSize;

        scorePedalFootswitchRight.style.borderTopLeftRadius = footswitchSize * 0.5f;

        scorePedalFootswitchRight.style.borderTopRightRadius = footswitchSize * 0.5f;

        scorePedalFootswitchRight.style.borderBottomLeftRadius = footswitchSize * 0.5f;

        scorePedalFootswitchRight.style.borderBottomRightRadius = footswitchSize * 0.5f;

        judgePopupFontSize = Mathf.Clamp(screenHeight * 0.046f, 38f, 66f);

        pauseTitleLabel.style.fontSize = pauseSize;

        pauseHintLabel.style.fontSize = bodySize * 0.85f;

        pauseInfoLabel.style.fontSize = bodySize * 0.80f;

        loopSetupStatusLabel.style.fontSize = bodySize * 0.82f;

        loopSetupHintLabel.style.fontSize = bodySize * 0.76f;

        loopSetupBar.style.maxWidth = Mathf.Clamp(menuLayoutWidth * 1.25f, 980f, 1840f);

        loopSetupBar.style.minWidth = Mathf.Clamp(menuLayoutWidth * 0.58f, 620f, 980f);

        loopSetupBar.style.paddingLeft = Mathf.Clamp(menuLayoutWidth * 0.020f, 26f, 40f);

        loopSetupBar.style.paddingRight = Mathf.Clamp(menuLayoutWidth * 0.020f, 26f, 40f);

        loopSetupBar.style.paddingTop = Mathf.Clamp(menuLayoutHeight * 0.022f, 20f, 28f);

        loopSetupBar.style.paddingBottom = Mathf.Clamp(menuLayoutHeight * 0.022f, 20f, 28f);

        speedValueLabel.style.fontSize = bodySize * 0.85f;

        settingsTrackLabel.style.fontSize = bodySize * 0.90f;

        settingsOffsetLabel.style.fontSize = bodySize * 0.80f;

        settingsTabSpeedLabel.style.fontSize = bodySize * 0.80f;

        settingsStartDelayLabel.style.fontSize = bodySize * 0.80f;

        settingsVolumeLabel.style.fontSize = bodySize * 0.80f;

        loopPauseDurationLabel.style.fontSize = bodySize * 0.80f;

        offsetHelperOverlay.style.paddingBottom = Mathf.Clamp(screenHeight * 0.052f, 30f, 64f);

        offsetHelperTitleLabel.style.fontSize = Mathf.Clamp(bodySize * 0.50f, 20f, 28f);

        offsetHelperStatusLabel.style.fontSize = Mathf.Clamp(bodySize * 0.68f, 24f, 40f);

        offsetHelperHintLabel.style.fontSize = Mathf.Clamp(bodySize * 0.70f, 26f, 42f);



        float buttonFontSize = Mathf.Clamp(menuLayoutHeight * 0.030f, 28f, 44f);

        float buttonHeight = Mathf.Clamp(menuLayoutHeight * 0.078f, 64f, 98f);

        float globalCardMaxHeight = Mathf.Clamp(screenHeight * 0.90f, 580f, 1720f);

        songEndCard.style.minHeight = 0f;



        foreach (SongSelectionRow row in selectionRows)

        {

            if (row == null) 

                continue;



            if (row.nameLabel != null)

                row.nameLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.024f, 22.8f, 37.2f);

            if (row.metaLabel != null)

                row.metaLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.022f, 24f, 38f);

            if (row.indexLabel != null)

                row.indexLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.020f, 18f, 28f);

            if (row.scoreLabel != null)

                row.scoreLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.035f, 28f, 46f);

        }



        foreach (TrackSelectionRow row in trackSelectionRows)

        {

            if (row == null)

                continue;



            if (row.nameLabel != null)

                row.nameLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.032f, 24f, 40f);

            if (row.metaLabel != null)

                row.metaLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.015f, 16f, 22f);

            if (row.indexLabel != null)

                row.indexLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.020f, 18f, 28f);

            if (row.scoreLabel != null)

                row.scoreLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.029f, 22f, 34f);

        }



        foreach (LibraryTrackRow row in selectionTrackRows)

        {

            if (row == null)

                continue;



            if (row.nameLabel != null)

                row.nameLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.018f, 16.8f, 30f);

            if (row.scoreLabel != null)

                row.scoreLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.026f, 24f, 38f);

        }



        foreach (Button button in document.rootVisualElement.Query<Button>().ToList())

        {

            button.style.fontSize = buttonFontSize;

            if (button.style.height.value.value < buttonHeight)

                button.style.height = buttonHeight;

        }



        float pauseMenuButtonFontSize = buttonFontSize * PauseMenuButtonFontScale;

        float libraryFooterButtonFontSize = buttonFontSize * LibraryFooterButtonFontScale;

        float songEndButtonFontSize = buttonFontSize * SongEndButtonFontScale;

        float songEndButtonHeight = buttonHeight * SongEndButtonHeightScale;

        float songEndButtonMinWidth = Mathf.Clamp(menuLayoutWidth * 0.19f, 320f, 420f);

        float pauseFrontPlateWidth = Mathf.Clamp(screenWidth * 0.34f, 1180f, 1460f);
        float pauseBackplateWidth = pauseFrontPlateWidth + 80f;
        float pauseRegionWidth = pauseFrontPlateWidth - Mathf.Clamp(screenWidth * 0.04f, 140f, 220f);
        float pauseShellWidth = Mathf.Clamp(pauseRegionWidth - Mathf.Clamp(screenWidth * 0.035f, 150f, 220f), 700f, 860f);
        float pauseInnerMaxWidth = Mathf.Clamp(pauseShellWidth - 120f, 520f, 660f);
        float pauseInfoMaxWidth = Mathf.Clamp(pauseShellWidth - 32f, 560f, 760f);

        pauseBackplate.style.width = pauseBackplateWidth;
        pauseFrontPlate.style.width = pauseFrontPlateWidth;
        pauseRegion.style.width = pauseRegionWidth;
        pauseRegion.style.paddingLeft = Mathf.Clamp(screenWidth * 0.05f, 120f, 220f);
        pauseShell.style.width = pauseShellWidth;
        pauseShell.style.maxWidth = pauseShellWidth;
        pauseCard.style.maxWidth = pauseInnerMaxWidth;
        pauseButtons.style.maxWidth = pauseInnerMaxWidth - 40f;
        pauseEndRow.style.maxWidth = pauseInnerMaxWidth - 40f;
        pauseInfoLabel.style.maxWidth = pauseInfoMaxWidth;
        speedSlider.style.maxWidth = pauseInnerMaxWidth - 40f;
        speedValueLabel.style.minWidth = Mathf.Clamp(pauseInnerMaxWidth - 80f, 420f, 520f);

        float settingsFrontPlateWidth = pauseFrontPlateWidth;
        float settingsBackplateWidth = pauseBackplateWidth;
        float settingsRegionWidth = pauseRegionWidth;
        float settingsShellWidth = pauseShellWidth;
        float settingsInnerMaxWidth = pauseInnerMaxWidth;
        float settingsControlMaxWidth = settingsInnerMaxWidth - 40f;

        settingsBackplate.style.width = settingsBackplateWidth;
        settingsFrontPlate.style.width = settingsFrontPlateWidth;
        settingsRegion.style.width = settingsRegionWidth;
        settingsRegion.style.paddingLeft = Mathf.Clamp(screenWidth * 0.05f, 120f, 220f);
        settingsShell.style.width = settingsShellWidth;
        settingsShell.style.maxWidth = settingsShellWidth;
        settingsCard.style.maxWidth = settingsInnerMaxWidth;
        settingsOffsetRow.style.maxWidth = settingsInnerMaxWidth;
        settingsTabSpeedRow.style.maxWidth = settingsInnerMaxWidth;
        settingsStartDelayRow.style.maxWidth = settingsInnerMaxWidth;
        settingsVolumeRow.style.maxWidth = settingsInnerMaxWidth;
        settingsTrackRow.style.maxWidth = settingsControlMaxWidth;
        settingsOffsetScopeRow.style.maxWidth = settingsControlMaxWidth;
        settingsOffsetSlider.style.maxWidth = settingsControlMaxWidth;
        settingsTabSpeedSlider.style.maxWidth = settingsControlMaxWidth;
        settingsStartDelaySlider.style.maxWidth = settingsControlMaxWidth;
        settingsVolumeSlider.style.maxWidth = settingsControlMaxWidth;
        settingsTrackButton.style.maxWidth = settingsControlMaxWidth;
        settingsOffsetScopeButton.style.maxWidth = settingsControlMaxWidth;
        gameModesBackplate.style.width = pauseBackplateWidth;
        gameModesFrontPlate.style.width = pauseFrontPlateWidth;
        gameModesRegion.style.width = pauseRegionWidth;
        gameModesRegion.style.paddingTop = Mathf.Clamp(screenHeight * 0.05f, 36f, 88f);
        gameModesRegion.style.paddingLeft = Mathf.Clamp(screenWidth * 0.05f, 120f, 220f);
        gameModesShell.style.width = pauseShellWidth;
        gameModesShell.style.maxWidth = pauseShellWidth;
        gameModesCard.style.maxWidth = pauseInnerMaxWidth;
        gameModesButtons.style.maxWidth = settingsControlMaxWidth;
        gameModesFooterRow.style.maxWidth = settingsControlMaxWidth;
        gameModesHintLabel.style.maxWidth = pauseInfoMaxWidth;
        gameModesHintLabel.style.fontSize = Mathf.Clamp(bodySize * 0.80f, 22f, 30f);
        gameModesHintLabel.style.marginBottom = Mathf.Clamp(menuLayoutHeight * 0.014f, 10f, 16f);
        gameModesInfoLabel.style.maxWidth = pauseInfoMaxWidth;
        gameModesInfoLabel.style.fontSize = Mathf.Clamp(bodySize * 0.72f, 20f, 27f);
        gameModesInfoLabel.style.marginBottom = Mathf.Clamp(menuLayoutHeight * 0.018f, 14f, 22f);
        startupTuningReminderPopup?.ApplyResponsiveSizing(menuLayoutHeight, buttonFontSize);

        loopPausePopup?.ApplyResponsiveSizing(menuLayoutHeight, buttonFontSize);
        gameModesPopup?.ApplyResponsiveSizing(menuLayoutHeight, buttonFontSize);
        heroModeSettingsPopup?.ApplyResponsiveSizing(menuLayoutHeight, buttonFontSize);
        if (notesDetectorRoutineMessageLabel != null)
            notesDetectorRoutineMessageLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.032f, 26f, 42f);
        if (notesDetectorRoutineCalloutLabel != null)
            notesDetectorRoutineCalloutLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.038f, 32f, 48f);
        if (notesDetectorRoutineHintLabel != null)
            notesDetectorRoutineHintLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.026f, 22f, 34f);
        if (notesDetectorRoutineStatusLabel != null)
            notesDetectorRoutineStatusLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.078f, 58f, 92f);
        if (notesDetectorRoutineFastNotesLabel != null)
            notesDetectorRoutineFastNotesLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.028f, 24f, 36f);
        if (notesDetectorRoutineAiNotesLabel != null)
            notesDetectorRoutineAiNotesLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.028f, 24f, 36f);
        foreach (Label tabRowLabel in notesDetectorRoutineTabRowLabels)
        {
            if (tabRowLabel != null)
                tabRowLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.040f, 34f, 52f);
        }
        heroModeSettingsCalloutLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.028f, 24f, 34f);
        heroModeSettingsValueLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.048f, 40f, 58f);
        heroModeSettingsValueLabel.style.minWidth = Mathf.Clamp(menuLayoutWidth * 0.16f, 220f, 300f);
        float heroModeArrowSize = Mathf.Clamp(menuLayoutHeight * 0.066f, 64f, 84f);
        float heroModeArrowFontSize = Mathf.Clamp(menuLayoutHeight * 0.034f, 30f, 40f);
        foreach (Button arrowButton in new[] { heroModeSettingsLeftArrowButton, heroModeSettingsRightArrowButton })
        {
            if (arrowButton == null)
                continue;

            arrowButton.style.width = heroModeArrowSize;
            arrowButton.style.height = heroModeArrowSize;
            arrowButton.style.fontSize = heroModeArrowFontSize;
        }
        heroModeSettingsPopup.PrimaryButton.style.minWidth = Mathf.Clamp(menuLayoutWidth * 0.18f, 280f, 360f);

        loopPauseDurationSlider.style.height = Mathf.Clamp(menuLayoutHeight * 0.075f, 62f, 76f);

        loopPauseCountdownHost.style.top = Mathf.Clamp(screenHeight * 0.020f, 18f, 34f);

        loopPauseCountdownHost.style.height = Mathf.Clamp(menuLayoutHeight * 0.10f, 72f, 96f);

        float loopCountdownSize = Mathf.Clamp(menuLayoutHeight * 0.060f, 52f, 72f);

        loopPauseCountdownDial.style.width = loopCountdownSize;

        loopPauseCountdownDial.style.height = loopCountdownSize;



        if (songEndRetryButton != null)

        {

            songEndRetryButton.style.fontSize = songEndButtonFontSize;

            songEndRetryButton.style.height = songEndButtonHeight;

            songEndRetryButton.style.minWidth = songEndButtonMinWidth;

        }



        if (songEndSelectionButton != null)

        {

            songEndSelectionButton.style.fontSize = songEndButtonFontSize;

            songEndSelectionButton.style.height = songEndButtonHeight;

            songEndSelectionButton.style.minWidth = songEndButtonMinWidth;

        }



        if (songEndMainMenuButton != null)

        {

            songEndMainMenuButton.style.fontSize = songEndButtonFontSize;

            songEndMainMenuButton.style.height = songEndButtonHeight;

            songEndMainMenuButton.style.minWidth = songEndButtonMinWidth;

        }



        foreach (Button button in pauseActionButtons)

        {

            if (button == null)

                continue;



            button.style.fontSize = pauseMenuButtonFontSize;

        }



        float gameModesButtonFontSize = pauseMenuButtonFontSize * 0.88f;
        float gameModesButtonHeight = Mathf.Clamp(buttonHeight * 1.12f, 90f, 118f);
        float gameModesBackButtonHeight = Mathf.Clamp(buttonHeight * 1.06f, 84f, 108f);

        foreach (Button button in gameModesActionButtons)
        {
            if (button == null)
                continue;

            button.style.fontSize = gameModesButtonFontSize;
            button.style.height = gameModesButtonHeight;
            button.style.maxWidth = settingsControlMaxWidth;
        }

        if (gameModesBackButton != null)
        {
            gameModesBackButton.style.fontSize = gameModesButtonFontSize;
            gameModesBackButton.style.height = gameModesBackButtonHeight;
        }
        foreach (Button button in songSettingsActionButtons)

        {

            if (button == null)

                continue;



            button.style.fontSize = pauseMenuButtonFontSize;
            button.style.maxWidth = settingsControlMaxWidth;

        }



        foreach (Button arrowButton in new[] { settingsTrackLeftArrowButton, settingsTrackRightArrowButton, settingsOffsetScopeLeftArrowButton, settingsOffsetScopeRightArrowButton })

        {

            if (arrowButton == null)

                continue;



            arrowButton.style.width = 110f;

            arrowButton.style.height = 110f;

            arrowButton.style.fontSize = 76f;

        }



        foreach (Button button in new[] { selectionSongsFolderButton, selectionRefreshButton, selectionBackButton, selectionStartButton })

        {

            if (button == null)

                continue;



            button.style.fontSize = libraryFooterButtonFontSize;

        }



        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-section-title").ToList())

            label.style.fontSize = buttonFontSize * 0.95f;



        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-title").ToList())

            label.style.fontSize = buttonFontSize;



        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-help").ToList())

            label.style.fontSize = buttonFontSize * 0.78f;



        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-value").ToList())

            label.style.fontSize = buttonFontSize * 0.82f;



        foreach (Label label in document.rootVisualElement.Query<Label>().Class("global-setting-enum-value").ToList())

            label.style.fontSize = buttonFontSize;



        globalSettingsCard.style.maxHeight = globalCardMaxHeight;

        bool compactMainMenu = menuLayoutWidth < 1320f;

        mainMenuShell.style.flexDirection = FlexDirection.Row;

        mainMenuShell.style.marginTop = (compactMainMenu ? 96f : 120f) * menuLayoutScale;

        mainMenuLeftColumn.style.paddingLeft = 0f;

        mainMenuLeftColumn.style.paddingRight = 0f;

        mainMenuLeftColumn.style.marginBottom = 0f;

        mainMenuOverlay.style.paddingLeft = (compactMainMenu ? 28f : 48f) * menuLayoutScale;

        mainMenuOverlay.style.paddingRight = (compactMainMenu ? 20f : 32f) * menuLayoutScale; 

        mainMenuOverlay.style.paddingTop = (compactMainMenu ? 126f : 180f) * menuLayoutScale;

        mainMenuOverlay.style.paddingBottom = (compactMainMenu ? 22f : 36f) * menuLayoutScale;

        mainMenuBackgroundPlane.style.left = Length.Percent(compactMainMenu ? 58f : 38f);

        mainMenuBackgroundPlane.style.top = (compactMainMenu ? -80f : -120f) * menuLayoutScale;

        mainMenuBackgroundPlane.style.width = Length.Percent(compactMainMenu ? 92f : 78f);

        mainMenuBackgroundPlane.style.height = Length.Percent(compactMainMenu ? 132f : 140f);

        mainMenuBackgroundPlane.style.rotate = new Rotate(new Angle(compactMainMenu ? 10f : 8f, AngleUnit.Degree));

        if (compactMainMenu)

        {

            mainMenuRightColumn.style.maxWidth = StyleKeyword.None;

            mainMenuRightColumn.style.width = 0f;

            mainMenuNavColumn.style.maxWidth = 900f;

            mainMenuTitleLabel.style.maxWidth = StyleKeyword.None;

            mainMenuSubtitleLabel.style.maxWidth = StyleKeyword.None;

        }

        else

        {

            mainMenuRightColumn.style.maxWidth = 0f;

            mainMenuRightColumn.style.width = 0f;

            mainMenuNavColumn.style.maxWidth = 900f;

            mainMenuTitleLabel.style.maxWidth = 820f;

            mainMenuSubtitleLabel.style.maxWidth = 720f;

        }



        scorePlate.style.display = DisplayStyle.None;

        scorePlate.style.left = StyleKeyword.Auto;

        scorePlate.style.right = StyleKeyword.Auto;

        scorePlate.style.width = StyleKeyword.Auto;

        scorePlate.style.alignItems = Align.Center;



        float songCardPreferredWidth = isHighway3D

            ? Mathf.Clamp(Screen.width * 0.34f, 560f, 1320f)

            : Mathf.Clamp(Screen.width * 0.42f, 640f, 1520f);

        float centeredScoreLeftEdge = (screenWidth - gameplayScoreWidth) * 0.5f;

        float gameplayHudGap = Mathf.Clamp(screenWidth * 0.012f, 18f, 40f);

        float maxSongCardWidthBeforeScore = Mathf.Max(420f, centeredScoreLeftEdge - 34f - gameplayHudGap);

        float songCardWidth = Mathf.Min(songCardPreferredWidth, maxSongCardWidthBeforeScore);

        songCard.style.width = songCardWidth;

        songCard.style.minWidth = songCardWidth;

        songCard.style.maxWidth = songCardWidth;

        songCard.style.marginRight = 0f;



        float titleMaxWidth = Mathf.Max(340f, songCardWidth - 36f);

        songNameLabel.style.maxWidth = titleMaxWidth;

        trackNameLabel.style.maxWidth = titleMaxWidth;



        bool compactSelection = menuLayoutWidth < 1180f;

        float libraryLayoutScale = Mathf.Clamp(screenWidth / 1600f, 1.0f, 1.12f);

        float libraryLeftInset = (compactSelection ? 24f : 40f) * libraryLayoutScale;

        float libraryRightInset = (compactSelection ? 24f : 40f) * libraryLayoutScale;

        float libraryTopInset = (compactSelection ? 148f : 172f) * libraryLayoutScale;

        float libraryBottomInset = (compactSelection ? 152f : 176f) * libraryLayoutScale;

        float compactLibraryScrollHeight = 420f * menuLayoutScale;

        float libraryShellHeight = Mathf.Max(320f, screenHeight - libraryTopInset - libraryBottomInset);

        currentMenuLayoutScale = libraryLayoutScale;

        currentCompactSelectionLayout = compactSelection;

        selectionOverlay.style.paddingLeft = libraryLeftInset;

        selectionOverlay.style.paddingRight = libraryRightInset;

        selectionOverlay.style.paddingTop = libraryTopInset;

        selectionOverlay.style.paddingBottom = libraryBottomInset;

        selectionOverlay.style.justifyContent = Justify.FlexStart;

        selectionOverlay.style.alignItems = Align.Stretch;

        selectionFooter.style.left = libraryLeftInset;

        selectionFooter.style.bottom = Mathf.Clamp(screenHeight * 0.03f, 18f, 28f) * libraryLayoutScale;

        selectionShell.style.flexDirection = compactSelection ? FlexDirection.Column : FlexDirection.Row;

        selectionShell.style.flexGrow = compactSelection ? 0f : 1f;

        selectionShell.style.position = Position.Relative;

        selectionShell.style.left = StyleKeyword.Auto;

        selectionShell.style.right = StyleKeyword.Auto;

        selectionShell.style.top = StyleKeyword.Auto;

        selectionShell.style.bottom = StyleKeyword.Auto;

        selectionShell.style.width = Length.Percent(100f);

        selectionShell.style.maxWidth = StyleKeyword.None;

        selectionShell.style.alignSelf = Align.Stretch;

        selectionShell.style.height = compactSelection ? StyleKeyword.Auto : StyleKeyword.Auto;

        selectionShell.style.minHeight = compactSelection ? 0f : libraryShellHeight;

        selectionShell.style.maxHeight = StyleKeyword.None;

        selectionShell.style.paddingLeft = 0f;

        selectionShell.style.paddingRight = 0f;

        trackSelectionShell.style.flexDirection = compactSelection ? FlexDirection.Column : FlexDirection.Row;

        selectionInfoColumn.style.flexGrow = compactSelection ? 0f : 1f;

        selectionInfoColumn.style.flexBasis = compactSelection ? StyleKeyword.Auto : 0f;

        selectionInfoColumn.style.flexShrink = 1f;

        selectionInfoColumn.style.width = compactSelection ? Length.Percent(100f) : StyleKeyword.Auto;

        selectionInfoColumn.style.minWidth = 0f;

        selectionInfoColumn.style.maxWidth = StyleKeyword.None;

        selectionInfoColumn.style.height = compactSelection ? StyleKeyword.Auto : Length.Percent(100f);

        selectionInfoColumn.style.minHeight = compactSelection ? StyleKeyword.Auto : 0f;

        selectionInfoColumn.style.marginLeft = compactSelection ? 0f : 20f * libraryLayoutScale;

        selectionInfoColumn.style.marginBottom = compactSelection ? 16f * libraryLayoutScale : 0f;

        selectionInfoCard.style.marginLeft = 0f;

        selectionInfoCard.style.marginRight = 0f;

        selectionInfoCard.style.paddingLeft = (compactSelection ? 28f : 40f) * libraryLayoutScale;

        selectionInfoCard.style.paddingRight = (compactSelection ? 28f : 40f) * libraryLayoutScale;

        selectionInfoCard.style.paddingTop = (compactSelection ? 24f : 30f) * libraryLayoutScale;

        selectionInfoCard.style.paddingBottom = (compactSelection ? 22f : 26f) * libraryLayoutScale;

        selectionInfoCard.style.flexGrow = 0f;

        selectionInfoCard.style.flexBasis = StyleKeyword.Auto;

        selectionInfoCard.style.width = Length.Percent(100f);

        selectionInfoCard.style.height = StyleKeyword.Auto;

        selectionInfoCard.style.minHeight = StyleKeyword.Auto;

        selectionInfoCard.style.maxHeight = StyleKeyword.None;

        selectionArrangementCard.style.width = Length.Percent(100f);

        selectionArrangementCard.style.flexGrow = compactSelection ? 0f : 1f;

        selectionArrangementCard.style.flexBasis = compactSelection ? StyleKeyword.Auto : 0f;

        selectionArrangementCard.style.minHeight = compactSelection ? 260f * libraryLayoutScale : 0f;

        selectionArrangementCard.style.marginTop = (compactSelection ? 16f : 18f) * libraryLayoutScale;

        selectionRailPanel.style.flexGrow = compactSelection ? 0f : 1f;

        selectionRailPanel.style.flexBasis = compactSelection ? StyleKeyword.Auto : 0f;

        selectionRailPanel.style.flexShrink = 1f;

        selectionRailPanel.style.width = compactSelection ? Length.Percent(100f) : StyleKeyword.Auto;

        selectionRailPanel.style.minWidth = 0f;

        selectionRailPanel.style.maxWidth = StyleKeyword.None;

        selectionRailPanel.style.paddingLeft = (compactSelection ? 18f : 26f) * libraryLayoutScale;

        selectionRailPanel.style.paddingRight = (compactSelection ? 18f : 26f) * libraryLayoutScale;

        selectionRailPanel.style.paddingTop = (compactSelection ? 18f : 26f) * libraryLayoutScale;

        selectionRailPanel.style.paddingBottom = (compactSelection ? 18f : 26f) * libraryLayoutScale;

        selectionRailPanel.style.height = compactSelection ? StyleKeyword.Auto : Length.Percent(100f);

        selectionRailPanel.style.minHeight = compactSelection ? StyleKeyword.Auto : 0f;

        selectionRailPanel.style.maxHeight = StyleKeyword.None;

        selectionRailPanel.style.position = Position.Relative;

        selectionRailPanel.style.right = StyleKeyword.Auto;

        selectionRailPanel.style.top = 0f;

        selectionRailPanel.style.bottom = 0f;

        selectionRailBackdrop.style.display = DisplayStyle.None;

        selectionRailBackdropGradient.style.display = DisplayStyle.None;

        selectionSplitDivider.style.display = DisplayStyle.None;

        trackSelectionInfoCard.style.marginRight = compactSelection ? 0f : 24f * menuLayoutScale;

        selectionInfoCard.style.marginBottom = 0f;

        trackSelectionInfoCard.style.marginBottom = compactSelection ? 18f * menuLayoutScale : 0f;

        selectionArtworkCard.style.width = (compactSelection ? 270f : 320f) * libraryLayoutScale;

        selectionArtworkCard.style.minWidth = (compactSelection ? 270f : 320f) * libraryLayoutScale;

        selectionArtworkCard.style.height = (compactSelection ? 270f : 320f) * libraryLayoutScale;

        selectionArtworkCard.style.marginRight = (compactSelection ? 20f : 28f) * libraryLayoutScale;

        selectionArtworkGlyphLabel.style.fontSize = (compactSelection ? 156f : 184f) * libraryLayoutScale;

        selectionArtworkCaptionLabel.style.fontSize = (compactSelection ? 20f : 22f) * libraryLayoutScale;

        selectionScrollView.style.minHeight = compactSelection ? compactLibraryScrollHeight : 0f;

        selectionScrollView.style.height = compactSelection ? compactLibraryScrollHeight : StyleKeyword.Auto;

        selectionScrollView.style.maxHeight = StyleKeyword.None;

        selectionTrackScrollView.style.minHeight = compactSelection ? 240f * menuLayoutScale : 0f;

        selectionTrackScrollView.style.height = compactSelection ? compactLibraryScrollHeight : StyleKeyword.Auto;

        selectionTrackScrollView.style.maxHeight = compactSelection ? StyleKeyword.None : StyleKeyword.None;

        trackSelectionScrollView.style.minHeight = compactSelection ? 340f * menuLayoutScale : 420f * menuLayoutScale;

        selectionInfoInstructionLabel.style.fontSize = (compactSelection ? 24f : 28f) * libraryLayoutScale;

        selectionInfoTitleLabel.style.fontSize = (compactSelection ? 90f : 116f) * libraryLayoutScale;

        selectionInfoMetaLabel.style.fontSize = (compactSelection ? 38f : 46f) * libraryLayoutScale;

        selectionInfoSummaryLabel.style.fontSize = (compactSelection ? 24f : 28f) * libraryLayoutScale;

        float librarySelectionStatValueFontSize = (compactSelection ? 44f : 52f) * libraryLayoutScale;

        selectionInfoScoreLabel.style.fontSize = librarySelectionStatValueFontSize;

        selectionInfoBestTrackLabel.style.fontSize = (compactSelection ? 20f : 22f) * libraryLayoutScale;

        selectionInfoArrangementValueLabel.style.fontSize = librarySelectionStatValueFontSize;

        selectionInfoAudioValueLabel.style.fontSize = librarySelectionStatValueFontSize;

        selectionInfoHeroHeartsValueLabel.style.fontSize = librarySelectionStatValueFontSize;

        float selectionInfoStatTitleFontSize = (compactSelection ? 16f : 18f) * libraryLayoutScale;

        foreach (Label statTitleLabel in selectionInfoStatTitleLabels)

        {

            if (statTitleLabel == null)

                continue;

            statTitleLabel.style.fontSize = selectionInfoStatTitleFontSize;

        }

        selectionInfoHintLabel.style.fontSize = (compactSelection ? 20f : 22f) * libraryLayoutScale;

        trackSelectionInfoTitleLabel.style.fontSize = (compactSelection ? 42f : 52f) * menuLayoutScale;

        selectionSubtitleLabel.style.fontSize = (compactSelection ? 46f : 52f) * libraryLayoutScale;

        trackSelectionSubtitleLabel.style.fontSize = (compactSelection ? 24f : 30f) * menuLayoutScale;

        if (compactSelection)

        {

            selectionInfoCard.style.maxWidth = StyleKeyword.None;

            trackSelectionInfoCard.style.maxWidth = StyleKeyword.None; 

        }

        else

        {

            selectionInfoCard.style.maxWidth = StyleKeyword.None;

            trackSelectionInfoCard.style.maxWidth = 430f * menuLayoutScale;

        }



        VisualElement selectionListCard = selectionScrollView?.parent;

        if (selectionListCard != null)

        {

            selectionListCard.style.flexGrow = 1f;

            selectionListCard.style.flexBasis = 0f;

            selectionListCard.style.minHeight = 0f;

            selectionListCard.style.height = compactSelection ? StyleKeyword.Auto : Length.Percent(100f);

            selectionListCard.style.paddingLeft = 0f;

            selectionListCard.style.paddingRight = 0f;

            selectionListCard.style.paddingTop = compactSelection ? 0f : 8f * menuLayoutScale;

            selectionListCard.style.paddingBottom = 0f;

            selectionListCard.style.maxHeight = StyleKeyword.None;

        }

        VisualElement arrangementPanel = selectionTrackScrollView?.parent;

        if (arrangementPanel != null)

        {

            arrangementPanel.style.flexGrow = 1f;

            arrangementPanel.style.flexBasis = 0f;

            arrangementPanel.style.minHeight = 0f;

            arrangementPanel.style.height = compactSelection ? StyleKeyword.Auto : Length.Percent(100f);

            arrangementPanel.style.maxHeight = StyleKeyword.None;

        }

        selectionTrackScrollView.contentContainer.style.paddingLeft = 12f * menuLayoutScale;

        selectionTrackScrollView.contentContainer.style.paddingRight = 12f * menuLayoutScale;

        selectionTrackScrollView.contentContainer.style.paddingTop = 6f * menuLayoutScale;

        selectionTrackScrollView.contentContainer.style.paddingBottom = 6f * menuLayoutScale;



        foreach (SongSelectionRow row in selectionRows)

        {

            if (row?.button == null)

                continue;



            row.button.style.minWidth = 0f;

            row.button.style.height = (compactSelection ? 184f : 176f) * currentMenuLayoutScale;

            row.nameLabel.style.fontSize = (compactSelection ? 12.096f : 13.104f) * currentMenuLayoutScale;

            row.metaLabel.style.fontSize = (compactSelection ? 50.4f : 55.2f) * currentMenuLayoutScale;

            row.scoreLabel.style.fontSize = (compactSelection ? 48f : 52.8f) * currentMenuLayoutScale;

            row.scoreLabel.style.width = (compactSelection ? 186f : 198f) * currentMenuLayoutScale;

            row.scoreLabel.style.minWidth = (compactSelection ? 186f : 198f) * currentMenuLayoutScale;

            row.scoreLabel.style.maxWidth = (compactSelection ? 186f : 198f) * currentMenuLayoutScale;

            if (row.artworkTile != null)

            {

                float artworkSize = (compactSelection ? 184f : 176f) * currentMenuLayoutScale;

                row.artworkTile.style.width = artworkSize;

                row.artworkTile.style.minWidth = artworkSize;

                row.artworkTile.style.height = Length.Percent(100f);

                row.artworkTile.style.marginRight = (compactSelection ? 14f : 18f) * currentMenuLayoutScale;

            }

            if (row.artworkGlyphLabel != null)

                row.artworkGlyphLabel.style.fontSize = (compactSelection ? 40f : 44f) * currentMenuLayoutScale;

        }



        foreach (LibraryTrackRow row in selectionTrackRows)

        {

            if (row?.button == null)

                continue;



            row.button.style.height = (compactSelection ? 148f : 142f) * currentMenuLayoutScale;

            row.nameLabel.style.fontSize = (compactSelection ? 12.096f : 13.104f) * currentMenuLayoutScale;

            row.scoreLabel.style.fontSize = (compactSelection ? 36f : 40f) * currentMenuLayoutScale;

            row.scoreLabel.style.minWidth = (compactSelection ? 168f : 178f) * currentMenuLayoutScale;

        }



        selectionBackButton.style.minWidth = compactSelection ? 210f * menuLayoutScale : 244f * menuLayoutScale;

        selectionSongsFolderButton.style.minWidth = compactSelection ? 210f * menuLayoutScale : 244f * menuLayoutScale;

        selectionRefreshButton.style.minWidth = compactSelection ? 188f * menuLayoutScale : 230f * menuLayoutScale;

        selectionStartButton.style.minWidth = compactSelection ? 244f * menuLayoutScale : 284f * menuLayoutScale;

        selectionBackButton.style.height = compactSelection ? 78f * menuLayoutScale : 88f * menuLayoutScale;

        selectionSongsFolderButton.style.height = compactSelection ? 78f * menuLayoutScale : 88f * menuLayoutScale;

        selectionRefreshButton.style.height = compactSelection ? 78f * menuLayoutScale : 88f * menuLayoutScale;

        selectionStartButton.style.height = compactSelection ? 82f * menuLayoutScale : 94f * menuLayoutScale;

    }

}



