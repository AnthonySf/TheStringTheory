using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Collections.Generic;

public class GuitarCalibrationManager : MonoBehaviour
{
    [Header("Overlay")]
    public bool openIfNoCalibrationFound = true;
    public KeyCode toggleKey = KeyCode.F8;

    [Header("UDP")]
    public int telemetryPort = 9001;

    [Header("Calibration Flow")]
    public int samplesPerStep = 3;
    public float requiredSilenceSeconds = 0.35f;
    public float captureWindowSeconds = 1.2f;
    public float captureCooldownSeconds = 0.35f;

    [Header("Silence Thresholds")]
    public float silencePeakThreshold = 0.012f;
    public float silenceRmsThreshold = 0.0045f;
    public float silenceBrightPeakThreshold = 0.0030f;
    public float silenceBrightRmsThreshold = 0.0010f;

    [Header("Attack Detection from Silent Baseline")]
    public float minAttackPeakJump = 0.008f;
    public float minAttackRmsJump = 0.003f;
    public float minAttackBrightPeakJump = 0.0025f;
    public float minAttackBrightRmsJump = 0.0008f;

    public float peakMultiplier = 2.2f;
    public float rmsMultiplier = 2.0f;
    public float brightPeakMultiplier = 2.6f;
    public float brightRmsMultiplier = 2.3f;

    [Header("Live Test")]
    public float gameplayPluckVisibleSeconds = 0.20f;
    public float rawCaptureVisibleSeconds = 0.30f;

    [Header("Export")]
    [Tooltip("Optional explicit path. Otherwise exports to persistentDataPath calibration file") ]
    public string calibrationExportPathOverride = "";

    private Canvas canvas;
    private GameObject rootPanel;
    private TextMeshProUGUI titleText;
    private TextMeshProUGUI stepText;
    private TextMeshProUGUI helperText;
    private TextMeshProUGUI liveText;
    private TextMeshProUGUI statsText;
    private TextMeshProUGUI pathText;
    private Button startNextButton;
    private Button resetButton;
    private Button closeButton;
    private Slider progressSlider;

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;
    private volatile string latestPacketRaw;

    private TelemetryState latestTelemetry = new TelemetryState();
    private int lastSeenGameplayPluckId = -1;
    private float lastGameplayPluckUiTime = -999f;
    private float lastRawCaptureUiTime = -999f;

    private CalibrationProfile profile;
    private List<CalibrationStep> steps = new List<CalibrationStep>();
    private int currentStepIndex = -1;
    private bool calibrationComplete = false;
    private bool screenOpen = false;

    private CalibrationCaptureState captureState = CalibrationCaptureState.Idle;
    private float stateEnteredTime = 0f;
    private float silenceStartedTime = -999f;

    private RunningBaseline baseline = new RunningBaseline();
    private bool baselineLocked = false;

    private string ExportPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(calibrationExportPathOverride))
                return calibrationExportPathOverride;

            return Path.Combine(ExternalContentPaths.PersistentRoot, "guitar_calibration_profile.json");
        }
    }

    void Start()
    {
        Directory.CreateDirectory(ExternalContentPaths.PersistentRoot);
        BuildDefaultProfile();
        BuildSteps();
        BuildUI();
        LoadCalibration();

        isRunning = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        if (openIfNoCalibrationFound && !File.Exists(ExportPath))
            OpenScreen();
        else
            CloseScreen();
    }

    void Update()
    {
        if (Input.GetKeyDown(toggleKey))
        {
            if (screenOpen) CloseScreen();
            else OpenScreen();
        }

        if (!string.IsNullOrEmpty(latestPacketRaw))
        {
            string raw = latestPacketRaw;
            latestPacketRaw = null;
            ParseTelemetry(raw);
        }

        UpdateCalibrationFlow();
        UpdateLiveUi();
        UpdateStepUi();
    }

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }

    // ----------------------------
    // UI
    // ----------------------------

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("GuitarCalibrationCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        rootPanel = CreatePanel(canvas.transform, "RootPanel", new Color(0.05f, 0.08f, 0.12f, 0.96f));
        RectTransform rootRt = rootPanel.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.08f, 0.06f);
        rootRt.anchorMax = new Vector2(0.92f, 0.94f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup vlg = rootPanel.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(26, 26, 22, 22);
        vlg.spacing = 14;
        vlg.childForceExpandHeight = false;
        vlg.childForceExpandWidth = true;

        titleText = CreateText(rootPanel.transform, "Title", "Guitar Calibration", 34, FontStyles.Bold, TextAlignmentOptions.Center);
        stepText = CreateText(rootPanel.transform, "Step", "", 24, FontStyles.Bold, TextAlignmentOptions.Center);
        helperText = CreateText(rootPanel.transform, "Helper", "", 18, FontStyles.Normal, TextAlignmentOptions.Center);

        progressSlider = CreateSlider(rootPanel.transform);

        GameObject cardsRow = new GameObject("CardsRow");
        cardsRow.transform.SetParent(rootPanel.transform, false);
        HorizontalLayoutGroup hlg = cardsRow.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = 14;
        hlg.childForceExpandHeight = true;
        hlg.childForceExpandWidth = true;

        GameObject liveCard = CreateCard(cardsRow.transform, "LiveCard");
        LayoutElement liveLE = liveCard.AddComponent<LayoutElement>();
        liveLE.minHeight = 230;
        liveText = CreateCardText(liveCard.transform, "LiveText", 20);

        GameObject statsCard = CreateCard(cardsRow.transform, "StatsCard");
        LayoutElement statsLE = statsCard.AddComponent<LayoutElement>();
        statsLE.minHeight = 230;
        statsText = CreateCardText(statsCard.transform, "StatsText", 17);

        pathText = CreateText(rootPanel.transform, "Path", "", 13, FontStyles.Italic, TextAlignmentOptions.Center);

        GameObject buttonsRow = new GameObject("ButtonsRow");
        buttonsRow.transform.SetParent(rootPanel.transform, false);
        HorizontalLayoutGroup buttonsHlg = buttonsRow.AddComponent<HorizontalLayoutGroup>();
        buttonsHlg.spacing = 12;
        buttonsHlg.childForceExpandHeight = false;
        buttonsHlg.childForceExpandWidth = true;

        startNextButton = CreateButton(buttonsRow.transform, "Start / Next", OnStartOrNextClicked);
        resetButton = CreateButton(buttonsRow.transform, "Reset Calibration", OnResetClicked);
        closeButton = CreateButton(buttonsRow.transform, "Close", CloseScreen);

        pathText.text = $"Profile export: {ExportPath}";
    }

    GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 120);
        return go;
    }

    GameObject CreateCard(Transform parent, string name)
    {
        GameObject go = CreatePanel(parent, name, new Color(0.10f, 0.14f, 0.20f, 0.96f));
        VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(18, 18, 16, 16);
        vlg.spacing = 8;
        vlg.childForceExpandHeight = true;
        vlg.childForceExpandWidth = true;
        return go;
    }

    TextMeshProUGUI CreateCardText(Transform parent, string name, float size)
    {
        TextMeshProUGUI t = CreateText(parent, name, "", size, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 200);
        return t;
    }

    TextMeshProUGUI CreateText(Transform parent, string name, string content, float size, FontStyles style, TextAlignmentOptions alignment)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        TextMeshProUGUI text = go.AddComponent<TextMeshProUGUI>();
        text.text = content;
        text.fontSize = size;
        text.fontStyle = style;
        text.alignment = alignment;
        text.color = Color.white;
        text.enableWordWrapping = true;
        RectTransform rt = go.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 60);
        return text;
    }

    Button CreateButton(Transform parent, string label, Action onClick)
    {
        GameObject go = new GameObject(label);
        go.transform.SetParent(parent, false);

        Image img = go.AddComponent<Image>();
        img.color = new Color(0.17f, 0.33f, 0.50f, 1f);

        Button btn = go.AddComponent<Button>();
        btn.targetGraphic = img;
        btn.onClick.AddListener(() => onClick?.Invoke());

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minHeight = 54;
        le.preferredHeight = 54;
        le.minWidth = 180;

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 20;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(10, 6);
        rt.offsetMax = new Vector2(-10, -6);

        return btn;
    }

    Slider CreateSlider(Transform parent)
    {
        GameObject go = new GameObject("Progress");
        go.transform.SetParent(parent, false);

        RectTransform rt = go.AddComponent<RectTransform>();
        rt.sizeDelta = new Vector2(0, 24);

        Slider slider = go.AddComponent<Slider>();
        slider.interactable = false;

        GameObject bg = new GameObject("Background");
        bg.transform.SetParent(go.transform, false);
        Image bgImage = bg.AddComponent<Image>();
        bgImage.color = new Color(0.16f, 0.18f, 0.22f, 1f);

        GameObject fillArea = new GameObject("FillArea");
        fillArea.transform.SetParent(go.transform, false);
        RectTransform faRt = fillArea.AddComponent<RectTransform>();
        faRt.anchorMin = Vector2.zero;
        faRt.anchorMax = Vector2.one;
        faRt.offsetMin = Vector2.zero;
        faRt.offsetMax = Vector2.zero;

        GameObject fill = new GameObject("Fill");
        fill.transform.SetParent(fillArea.transform, false);
        Image fillImage = fill.AddComponent<Image>();
        fillImage.color = new Color(0.20f, 0.75f, 0.45f, 1f);

        RectTransform bgRt = bg.GetComponent<RectTransform>();
        bgRt.anchorMin = new Vector2(0, 0.2f);
        bgRt.anchorMax = new Vector2(1, 0.8f);
        bgRt.offsetMin = Vector2.zero;
        bgRt.offsetMax = Vector2.zero;

        RectTransform fillRt = fill.GetComponent<RectTransform>();
        fillRt.anchorMin = new Vector2(0, 0);
        fillRt.anchorMax = new Vector2(1, 1);
        fillRt.offsetMin = Vector2.zero;
        fillRt.offsetMax = Vector2.zero;

        slider.fillRect = fillRt;
        slider.targetGraphic = fillImage;

        return slider;
    }

    void OpenScreen()
    {
        screenOpen = true;
        rootPanel.SetActive(true);
        canvas.enabled = true;
    }

    void CloseScreen()
    {
        screenOpen = false;
        if (rootPanel != null) rootPanel.SetActive(false);
        if (canvas != null) canvas.enabled = false;
    }

    void UpdateLiveUi()
    {
        bool gameplayPluckedNow = (Time.unscaledTime - lastGameplayPluckUiTime) <= gameplayPluckVisibleSeconds;
        bool rawCapturedNow = (Time.unscaledTime - lastRawCaptureUiTime) <= rawCaptureVisibleSeconds;

        string gameplayText = gameplayPluckedNow ? "<color=#7CFF8D>PLUCKED</color>" : "<color=#B8C0CC>idle</color>";
        string rawText = rawCapturedNow ? "<color=#8CE1FF>RAW ATTACK</color>" : "<color=#B8C0CC>idle</color>";
        string noteText = string.IsNullOrWhiteSpace(latestTelemetry.primaryNote) ? "--" : latestTelemetry.primaryNote;

        liveText.text =
            "<b>Live Tester</b>\n\n" +
            $"Gameplay pluck: {gameplayText}\n" +
            $"Calibration attack: {rawText}\n" +
            $"Primary note: <color=#FFD36E>{noteText}</color>\n" +
            $"Detected notes: <color=#9FD3FF>{(string.IsNullOrWhiteSpace(latestTelemetry.notesCsv) ? "--" : latestTelemetry.notesCsv)}</color>\n" +
            $"Freq: {latestTelemetry.freq:F1} Hz\n" +
            $"Conf: {latestTelemetry.conf:F2}\n" +
            $"Reason: {latestTelemetry.reason}\n" +
            $"Peak / RMS: {latestTelemetry.peak:F4} / {latestTelemetry.rms:F4}\n" +
            $"Bright Peak / RMS: {latestTelemetry.brightPeak:F4} / {latestTelemetry.brightRms:F4}";
    }

    void UpdateStepUi()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
        {
            if (calibrationComplete)
            {
                stepText.text = "Calibration complete";
                helperText.text = "Saved successfully. You can keep testing here or close the screen.";
            }
            else
            {
                stepText.text = "Ready to calibrate";
                helperText.text =
                    "Press Start to begin.\n" +
                    "For each step: mute / stay silent, wait for the 3..2..1 countdown, then pluck once.\n" +
                    "Calibration capture uses a silent baseline + raw attack jump, so it can record soft plucks even if gameplay pluck detection is weak.";
            }
        }
        else
        {
            CalibrationStep step = steps[currentStepIndex];
            int captured = GetCapturedCount(step);

            stepText.text = $"{step.displayName} • {step.dynamicsLabel}";
            helperText.text =
                $"Expected string: <b>{step.displayName}</b>\n" +
                $"Expected open note: <color=#FFD36E>{step.expectedNote}</color>\n" +
                $"Captured: <color=#7CFF8D>{captured}/{samplesPerStep}</color>\n" +
                $"State: <color=#9FD3FF>{captureState}</color>";

            if (captureState == CalibrationCaptureState.WaitingForSilence)
            {
                helperText.text += "\nMute / stay silent until the system arms.";
            }
            else if (captureState == CalibrationCaptureState.Countdown)
            {
                float t = Time.unscaledTime - stateEnteredTime;
                int count = Mathf.Clamp(3 - Mathf.FloorToInt(t), 1, 3);
                helperText.text += $"\nPluck on: <color=#7CFF8D>{count}</color>";
            }
            else if (captureState == CalibrationCaptureState.AwaitingAttack)
            {
                helperText.text += "\nListening for the next attack...";
            }
        }

        int totalRequired = steps.Count * samplesPerStep;
        int totalCaptured = 0;
        foreach (var s in steps) totalCaptured += GetCapturedCount(s);
        progressSlider.value = totalRequired == 0 ? 0f : (float)totalCaptured / totalRequired;

        statsText.text = BuildStatsSummary();
        pathText.text = $"Profile export: {ExportPath}";
    }

    string BuildStatsSummary()
    {
        StringBuilder sb = new StringBuilder();
        sb.AppendLine("<b>Calibration Progress</b>\n");
        foreach (var stringCal in profile.strings)
        {
            sb.AppendLine($"{stringCal.stringName} ({stringCal.expectedNote})");
            sb.AppendLine($"  Soft: {stringCal.softSamples.Count}/{samplesPerStep}");
            sb.AppendLine($"  Strong: {stringCal.strongSamples.Count}/{samplesPerStep}");
            sb.AppendLine();
        }
        return sb.ToString();
    }

    void OnStartOrNextClicked()
    {
        if (calibrationComplete)
        {
            calibrationComplete = false;
            currentStepIndex = 0;
            ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
            return;
        }

        if (currentStepIndex < 0)
        {
            currentStepIndex = 0;
            ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
            return;
        }

        if (GetCapturedCount(steps[currentStepIndex]) >= samplesPerStep)
            AdvanceStep();
    }

    void OnResetClicked()
    {
        BuildDefaultProfile();
        BuildSteps();
        currentStepIndex = 0;
        calibrationComplete = false;
        ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
        SaveCalibration();
    }

    void AdvanceStep()
    {
        currentStepIndex++;
        if (currentStepIndex >= steps.Count)
        {
            calibrationComplete = true;
            currentStepIndex = -1;
            ResetCaptureState(CalibrationCaptureState.Idle);
            SaveCalibration();
        }
        else
        {
            ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
        }
    }

    void ResetCaptureState(CalibrationCaptureState newState)
    {
        captureState = newState;
        stateEnteredTime = Time.unscaledTime;
        silenceStartedTime = -999f;
        baseline.Reset();
        baselineLocked = false;
    }

    void UpdateCalibrationFlow()
    {
        if (currentStepIndex < 0 || currentStepIndex >= steps.Count)
            return;

        CalibrationStep step = steps[currentStepIndex];
        if (GetCapturedCount(step) >= samplesPerStep)
        {
            AdvanceStep();
            return;
        }

        bool isSilentNow =
            latestTelemetry.peak <= silencePeakThreshold &&
            latestTelemetry.rms <= silenceRmsThreshold &&
            latestTelemetry.brightPeak <= silenceBrightPeakThreshold &&
            latestTelemetry.brightRms <= silenceBrightRmsThreshold;

        switch (captureState)
        {
            case CalibrationCaptureState.Idle:
                break;

            case CalibrationCaptureState.WaitingForSilence:
                if (isSilentNow)
                {
                    if (silenceStartedTime < 0f)
                        silenceStartedTime = Time.unscaledTime;

                    baseline.Push(latestTelemetry);

                    if ((Time.unscaledTime - silenceStartedTime) >= requiredSilenceSeconds)
                    {
                        baselineLocked = true;
                        ResetCaptureState(CalibrationCaptureState.Countdown);
                    }
                }
                else
                {
                    silenceStartedTime = -999f;
                    baseline.Reset();
                }
                break;

            case CalibrationCaptureState.Countdown:
                if (!isSilentNow)
                {
                    ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
                    break;
                }

                baseline.Push(latestTelemetry);

                if ((Time.unscaledTime - stateEnteredTime) >= 3.0f)
                {
                    baselineLocked = true;
                    ResetCaptureState(CalibrationCaptureState.AwaitingAttack);
                }
                break;

            case CalibrationCaptureState.AwaitingAttack:
                if ((Time.unscaledTime - stateEnteredTime) > captureWindowSeconds)
                {
                    ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
                    break;
                }

                if (DetectRawAttack(latestTelemetry, baseline))
                {
                    lastRawCaptureUiTime = Time.unscaledTime;
                    CaptureCurrentTelemetryForStep(step, latestTelemetry);
                    SaveCalibration();

                    if (GetCapturedCount(step) >= samplesPerStep)
                        AdvanceStep();
                    else
                        ResetCaptureState(CalibrationCaptureState.WaitingForSilence);
                }
                break;
        }
    }

    bool DetectRawAttack(TelemetryState t, RunningBaseline b)
    {
        float peakJump = Mathf.Max(minAttackPeakJump, b.avgPeak * peakMultiplier);
        float rmsJump = Mathf.Max(minAttackRmsJump, b.avgRms * rmsMultiplier);
        float brightPeakJump = Mathf.Max(minAttackBrightPeakJump, b.avgBrightPeak * brightPeakMultiplier);
        float brightRmsJump = Mathf.Max(minAttackBrightRmsJump, b.avgBrightRms * brightRmsMultiplier);

        bool attack =
            t.peak >= b.avgPeak + peakJump ||
            t.rms >= b.avgRms + rmsJump ||
            t.brightPeak >= b.avgBrightPeak + brightPeakJump ||
            t.brightRms >= b.avgBrightRms + brightRmsJump ||
            t.pluck;

        return attack;
    }

    void CaptureCurrentTelemetryForStep(CalibrationStep step, TelemetryState t)
    {
        StringCalibration sc = profile.strings.Find(x => x.stringName == step.stringName);
        if (sc == null) return;

        CalibrationSample sample = new CalibrationSample();
        sample.note = t.primaryNote;
        sample.freq = t.freq;
        sample.conf = t.conf;
        sample.peak = t.peak;
        sample.rms = t.rms;
        sample.brightPeak = t.brightPeak;
        sample.brightRms = t.brightRms;
        sample.mainDelta = t.mainDelta;
        sample.mainRatio = t.mainRatio;
        sample.mainSlope = t.mainSlope;
        sample.brightDelta = t.brightDelta;
        sample.brightRatio = t.brightRatio;
        sample.brightSlope = t.brightSlope;
        sample.reason = t.reason;
        sample.unixTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        if (step.strong)
            sc.strongSamples.Add(sample);
        else
            sc.softSamples.Add(sample);
    }

    int GetCapturedCount(CalibrationStep step)
    {
        StringCalibration sc = profile.strings.Find(x => x.stringName == step.stringName);
        if (sc == null) return 0;
        return step.strong ? sc.strongSamples.Count : sc.softSamples.Count;
    }

    void ParseTelemetry(string raw)
    {
        // T|pluckId|primaryNote|notesCsv|freq|conf|pluck|peak|rms|brightPeak|brightRms|mainDelta|mainRatio|mainSlope|brightDelta|brightRatio|brightSlope|reason
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("T|"))
            return;

        string[] parts = raw.Split('|');
        if (parts.Length < 18)
            return;

        TelemetryState t = new TelemetryState();
        int.TryParse(parts[1], out t.pluckId);
        t.primaryNote = parts[2];
        t.notesCsv = parts[3];
        float.TryParse(parts[4], out t.freq);
        float.TryParse(parts[5], out t.conf);
        t.pluck = parts[6] == "1";
        float.TryParse(parts[7], out t.peak);
        float.TryParse(parts[8], out t.rms);
        float.TryParse(parts[9], out t.brightPeak);
        float.TryParse(parts[10], out t.brightRms);
        float.TryParse(parts[11], out t.mainDelta);
        float.TryParse(parts[12], out t.mainRatio);
        float.TryParse(parts[13], out t.mainSlope);
        float.TryParse(parts[14], out t.brightDelta);
        float.TryParse(parts[15], out t.brightRatio);
        float.TryParse(parts[16], out t.brightSlope);
        t.reason = parts[17];

        latestTelemetry = t;

        if (t.pluck && t.pluckId != lastSeenGameplayPluckId)
        {
            lastSeenGameplayPluckId = t.pluckId;
            lastGameplayPluckUiTime = Time.unscaledTime;
        }
    }

    void BuildDefaultProfile()
    {
        profile = new CalibrationProfile();
        profile.version = 1;
        profile.strings = new List<StringCalibration>();

        profile.strings.Add(new StringCalibration { stringName = "Low E", expectedNote = "E2", expectedFrequency = 82.41f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
        profile.strings.Add(new StringCalibration { stringName = "A", expectedNote = "A2", expectedFrequency = 110.00f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
        profile.strings.Add(new StringCalibration { stringName = "D", expectedNote = "D3", expectedFrequency = 146.83f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
        profile.strings.Add(new StringCalibration { stringName = "G", expectedNote = "G3", expectedFrequency = 196.00f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
        profile.strings.Add(new StringCalibration { stringName = "B", expectedNote = "B3", expectedFrequency = 246.94f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
        profile.strings.Add(new StringCalibration { stringName = "High E", expectedNote = "E4", expectedFrequency = 329.63f, softSamples = new List<CalibrationSample>(), strongSamples = new List<CalibrationSample>() });
    }

    void BuildSteps()
    {
        steps.Clear();
        foreach (var s in profile.strings)
        {
            steps.Add(new CalibrationStep
            {
                stringName = s.stringName,
                expectedNote = s.expectedNote,
                expectedFrequency = s.expectedFrequency,
                strong = false,
                displayName = s.stringName,
                dynamicsLabel = "Soft"
            });

            steps.Add(new CalibrationStep
            {
                stringName = s.stringName,
                expectedNote = s.expectedNote,
                expectedFrequency = s.expectedFrequency,
                strong = true,
                displayName = s.stringName,
                dynamicsLabel = "Strong"
            });
        }
    }

    void SaveCalibration()
    {
        try
        {
            string json = JsonUtility.ToJson(profile, true);
            PlayerPrefs.SetString("guitar_calibration_profile_json", json);
            PlayerPrefs.Save();
            File.WriteAllText(ExportPath, json, Encoding.UTF8);
        }
        catch (Exception e)
        {
            Debug.LogWarning("Calibration save failed: " + e.Message);
        }
    }

    void LoadCalibration()
    {
        try
        {
            string json = null;

            if (File.Exists(ExportPath))
                json = File.ReadAllText(ExportPath, Encoding.UTF8);
            else if (PlayerPrefs.HasKey("guitar_calibration_profile_json"))
                json = PlayerPrefs.GetString("guitar_calibration_profile_json");

            if (!string.IsNullOrWhiteSpace(json))
            {
                CalibrationProfile loaded = JsonUtility.FromJson<CalibrationProfile>(json);
                if (loaded != null && loaded.strings != null && loaded.strings.Count > 0)
                {
                    foreach (var s in loaded.strings)
                    {
                        if (s.softSamples == null) s.softSamples = new List<CalibrationSample>();
                        if (s.strongSamples == null) s.strongSamples = new List<CalibrationSample>();
                    }

                    profile = loaded;
                    BuildSteps();
                }
            }
        }
        catch { }
    }

    void ReceiveLoop()
    {
        try
        {
            udpClient = new UdpClient(telemetryPort);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                latestPacketRaw = Encoding.UTF8.GetString(data);
            }
        }
        catch { }
    }

    [Serializable]
    public class CalibrationProfile
    {
        public int version = 1;
        public List<StringCalibration> strings = new List<StringCalibration>();
    }

    [Serializable]
    public class StringCalibration
    {
        public string stringName;
        public string expectedNote;
        public float expectedFrequency;
        public List<CalibrationSample> softSamples;
        public List<CalibrationSample> strongSamples;
    }

    [Serializable]
    public class CalibrationSample
    {
        public string note;
        public float freq;
        public float conf;
        public float peak;
        public float rms;
        public float brightPeak;
        public float brightRms;
        public float mainDelta;
        public float mainRatio;
        public float mainSlope;
        public float brightDelta;
        public float brightRatio;
        public float brightSlope;
        public string reason;
        public long unixTime;
    }

    public class CalibrationStep
    {
        public string stringName;
        public string expectedNote;
        public float expectedFrequency;
        public bool strong;
        public string displayName;
        public string dynamicsLabel;
    }

    public struct TelemetryState
    {
        public int pluckId;
        public string primaryNote;
        public string notesCsv;
        public float freq;
        public float conf;
        public bool pluck;
        public float peak;
        public float rms;
        public float brightPeak;
        public float brightRms;
        public float mainDelta;
        public float mainRatio;
        public float mainSlope;
        public float brightDelta;
        public float brightRatio;
        public float brightSlope;
        public string reason;
    }

    public enum CalibrationCaptureState
    {
        Idle,
        WaitingForSilence,
        Countdown,
        AwaitingAttack
    }

    public class RunningBaseline
    {
        public float avgPeak;
        public float avgRms;
        public float avgBrightPeak;
        public float avgBrightRms;
        public int count;

        public void Reset()
        {
            avgPeak = 0f;
            avgRms = 0f;
            avgBrightPeak = 0f;
            avgBrightRms = 0f;
            count = 0;
        }

        public void Push(TelemetryState t)
        {
            count++;
            float k = 1f / Mathf.Max(1, count);
            avgPeak = Mathf.Lerp(avgPeak, t.peak, k);
            avgRms = Mathf.Lerp(avgRms, t.rms, k);
            avgBrightPeak = Mathf.Lerp(avgBrightPeak, t.brightPeak, k);
            avgBrightRms = Mathf.Lerp(avgBrightRms, t.brightRms, k);
        }
    }
}