using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;

[DefaultExecutionOrder(-10000)]
public sealed class StartupSplashBootstrap : MonoBehaviour
{
    private static readonly Color[] LogoStringColors =
    {
        new Color(0.91f, 0.30f, 0.24f, 1f),
        new Color(0.95f, 0.77f, 0.06f, 1f),
        new Color(0.20f, 0.60f, 0.86f, 1f),
        new Color(0.90f, 0.49f, 0.13f, 1f),
        new Color(0.18f, 0.80f, 0.44f, 1f),
        new Color(0.61f, 0.35f, 0.71f, 1f)
    };

    private const string TargetScenePath = "Assets/Scenes/SampleScene.unity";
    private const float MinimumVisibleSeconds = 0.45f;

    private UIDocument document;
    private PanelSettings panelSettings;
    private ReusableLoadingOverlay loadingOverlay;
    private Label statusLabel;
    private float overlayShownAt;

    private void Awake()
    {
        document = gameObject.GetComponent<UIDocument>();
        if (document == null)
            document = gameObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings();
        EnsurePanelSettingsSupportAssets(panelSettings);
        document.panelSettings = panelSettings;

        VisualElement root = document.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 1f);

        loadingOverlay = ReusableLoadingOverlay.CreateStringTheoryLibraryLoadingOverlay(root);
        loadingOverlay.RootElement.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 1f);

        VisualElement shell = new VisualElement();
        shell.style.flexDirection = FlexDirection.Column;
        shell.style.alignItems = Align.Center;
        shell.style.justifyContent = Justify.Center;
        shell.pickingMode = PickingMode.Ignore;

        shell.Add(CreateStringTheoryLogo());

        statusLabel = new Label("Loading...");
        statusLabel.style.marginTop = 18f;
        statusLabel.style.fontSize = 24f;
        statusLabel.style.color = new Color(0.82f, 0.90f, 0.98f, 0.96f);
        statusLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        statusLabel.style.whiteSpace = WhiteSpace.Normal;
        statusLabel.style.unityFontDefinition = ResolveUiFontDefinition();
        shell.Add(statusLabel);

        loadingOverlay.ContentHost.Add(shell);
        overlayShownAt = Time.unscaledTime;
        loadingOverlay.SetVisible(true, overlayShownAt);
    }

    private void Start()
    {
        StartCoroutine(BootstrapRoutine());
    }

    private void Update()
    {
        loadingOverlay?.SetVisible(true, Time.unscaledTime);
    }

    private IEnumerator BootstrapRoutine()
    {
        yield return null;

        if (statusLabel != null)
            statusLabel.text = "Preparing runtime content...";

        ExternalContentBootstrap.EnsureRuntimeContentReady();
        yield return null;

        if (statusLabel != null)
            statusLabel.text = "Loading scene...";

        AsyncOperation loadOperation = SceneManager.LoadSceneAsync(TargetScenePath, LoadSceneMode.Single);
        if (loadOperation == null)
        {
            Debug.LogError($"[StartupSplash] Failed to load startup scene: {TargetScenePath}");
            yield break;
        }

        loadOperation.allowSceneActivation = false;

        while (loadOperation.progress < 0.9f)
            yield return null;

        while (Time.unscaledTime - overlayShownAt < MinimumVisibleSeconds)
            yield return null;

        loadOperation.allowSceneActivation = true;

        while (!loadOperation.isDone)
            yield return null;
    }

    private static VisualElement CreateStringTheoryLogo()
    {
        FontDefinition logoFont = ResolveLogoFontDefinition();

        VisualElement logoWrap = new VisualElement();
        logoWrap.style.alignItems = Align.Center;
        logoWrap.style.justifyContent = Justify.Center;

        VisualElement stringRow = new VisualElement();
        stringRow.style.flexDirection = FlexDirection.Row;
        stringRow.style.justifyContent = Justify.Center;
        stringRow.style.marginBottom = -14f;

        const string stringWord = "STRING";
        for (int i = 0; i < stringWord.Length; i++)
        {
            Label letter = new Label(stringWord[i].ToString());
            letter.style.fontSize = 188f;
            letter.style.color = LogoStringColors[i % LogoStringColors.Length];
            letter.style.unityFontDefinition = logoFont;
            letter.style.unityTextAlign = TextAnchor.MiddleCenter;
            letter.style.unityFontStyleAndWeight = FontStyle.Bold;
            letter.style.marginLeft = 2.8f;
            letter.style.marginRight = 2.8f;
            stringRow.Add(letter);
        }

        Label theoryLabel = new Label("THEORY");
        theoryLabel.style.fontSize = 178f;
        theoryLabel.style.color = new Color(0.87f, 0.95f, 1f, 1f);
        theoryLabel.style.unityFontDefinition = logoFont;
        theoryLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        theoryLabel.style.letterSpacing = 3.0f;

        logoWrap.Add(stringRow);
        logoWrap.Add(theoryLabel);
        return logoWrap;
    }

    private static PanelSettings ResolvePanelSettings()
    {
        PanelSettings existing = Resources.FindObjectsOfTypeAll<PanelSettings>()
            .Where(candidate => candidate != null)
            .OrderByDescending(candidate => candidate.themeStyleSheet != null)
            .ThenByDescending(candidate => candidate.textSettings != null)
            .ThenByDescending(candidate => candidate.name == "PanelSettings")
            .FirstOrDefault();
        if (existing != null)
            return existing;

        PanelSettings runtimeAsset = Resources.Load<PanelSettings>("UIToolkitRuntimePanelSettings");
        PanelSettings settings = runtimeAsset != null
            ? ScriptableObject.Instantiate(runtimeAsset)
            : ScriptableObject.CreateInstance<PanelSettings>();
        settings.name = "StartupSplashRuntimePanelSettings";
        return settings;
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

    private static FontDefinition ResolveLogoFontDefinition()
    {
        Font font = Resources.Load<Font>("MetalLord") ?? Resources.Load<Font>("Fonts/MetalLord");
        return font != null ? FontDefinition.FromFont(font) : default;
    }

    private static FontDefinition ResolveUiFontDefinition()
    {
        Font font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
        return font != null ? FontDefinition.FromFont(font) : default;
    }
}
