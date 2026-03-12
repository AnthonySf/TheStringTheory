using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Collections.Generic;
using UnityEngine.EventSystems;

public class GuitarManualPluckCalibration : MonoBehaviour
{
    [Header("Overlay")]
    public bool openIfNoProfileFound = true;
    public KeyCode toggleKey = KeyCode.F8;

    [Header("UDP")]
    public int telemetryPort = 9001;

    [Header("Live Test")]
    public float gameplayPluckVisibleSeconds = 0.20f;

    [Header("Export")]
    public string exportPathOverride = "";

    private Canvas canvas;
    private GameObject rootPanel;
    private RectTransform contentRt;

    private TextMeshProUGUI titleText;
    private TextMeshProUGUI helperText;
    private TextMeshProUGUI liveText;
    private TextMeshProUGUI fileText;

    private readonly string[] stringNames = { "Low E", "A", "D", "G", "B", "High E" };
    private readonly string[] openNotes = { "E2", "A2", "D3", "G3", "B3", "E4" };

    private readonly List<RowRefs> rows = new List<RowRefs>();

    private UdpClient udpClient;
    private Thread receiveThread;
    private volatile bool isRunning;
    private volatile string latestPacketRaw;

    private TelemetryState latestTelemetry = new TelemetryState();
    private int lastSeenGameplayPluckId = -1;
    private float lastGameplayPluckUiTime = -999f;
    private bool screenOpen = false;

    private ManualThresholdProfile profile;

    private const float COL_STRING = 70f;
    private const float COL_NOTE = 42f;
    private const float COL_LIVE = 52f;
    private const float COL_THRESHOLD = 64f;
    private const float COL_ARMED = 42f;
    private const float COL_TRIG = 42f;
    private const float ROW_HEIGHT = 28f;
    private const float GAP = 3f;

    private string ExportPath
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(exportPathOverride))
                return exportPathOverride;

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            return Path.Combine(projectRoot, "guitar_manual_pluck_thresholds.json");
        }
    }

    private float TableWidth =>
        COL_STRING + COL_NOTE + COL_LIVE + COL_THRESHOLD + COL_ARMED + COL_TRIG + GAP * 5f + 16f;

    void Start()
    {
        EnsureEventSystem();
        BuildDefaultProfile();
        BuildUI();
        LoadProfileIntoUi();

        isRunning = true;
        receiveThread = new Thread(ReceiveLoop);
        receiveThread.IsBackground = true;
        receiveThread.Start();

        if (openIfNoProfileFound && !File.Exists(ExportPath))
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

        UpdateLiveUi();
        UpdateRowsUi();
    }

    void OnDestroy()
    {
        isRunning = false;
        if (udpClient != null) udpClient.Close();
        if (receiveThread != null && receiveThread.IsAlive) receiveThread.Abort();
    }

    void EnsureEventSystem()
    {
        if (FindObjectOfType<EventSystem>() != null) return;

        GameObject es = new GameObject("EventSystem");
        es.AddComponent<EventSystem>();
        es.AddComponent<StandaloneInputModule>();
    }

    void BuildUI()
    {
        GameObject canvasObj = new GameObject("ManualPluckCalibrationCanvas");
        canvas = canvasObj.AddComponent<Canvas>();
        canvas.renderMode = RenderMode.ScreenSpaceOverlay;
        canvas.sortingOrder = 9999;
        canvasObj.AddComponent<CanvasScaler>().uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        canvasObj.AddComponent<GraphicRaycaster>();

        rootPanel = CreatePanel(canvas.transform, "RootPanel", new Color(0.05f, 0.08f, 0.12f, 0.95f));
        RectTransform rootRt = rootPanel.GetComponent<RectTransform>();
        rootRt.anchorMin = new Vector2(0.06f, 0.05f);
        rootRt.anchorMax = new Vector2(0.94f, 0.95f);
        rootRt.offsetMin = Vector2.zero;
        rootRt.offsetMax = Vector2.zero;

        VerticalLayoutGroup rootLayout = rootPanel.AddComponent<VerticalLayoutGroup>();
        rootLayout.padding = new RectOffset(8, 8, 8, 8);
        rootLayout.spacing = 4;
        rootLayout.childForceExpandHeight = false;
        rootLayout.childForceExpandWidth = true;

        titleText = CreateText(rootPanel.transform, "Title", "Manual Pluck Thresholds", 18, FontStyles.Bold, TextAlignmentOptions.Center);
        helperText = CreateText(rootPanel.transform, "Helper", "Edit Threshold values, then Save. Use the scrollbars to see all columns.", 10, FontStyles.Normal, TextAlignmentOptions.Center);

        BuildScrollArea(rootPanel.transform);

        fileText = CreateText(rootPanel.transform, "FileText", "", 9, FontStyles.Italic, TextAlignmentOptions.Center);

        GameObject buttonsRow = new GameObject("ButtonsRow");
        buttonsRow.transform.SetParent(rootPanel.transform, false);
        HorizontalLayoutGroup btnHlg = buttonsRow.AddComponent<HorizontalLayoutGroup>();
        btnHlg.spacing = 4;
        btnHlg.childForceExpandWidth = true;
        btnHlg.childForceExpandHeight = false;

        CreateButton(buttonsRow.transform, "Save", SaveFromUiToFile);
        CreateButton(buttonsRow.transform, "Reload", LoadProfileIntoUi);
        CreateButton(buttonsRow.transform, "Close", CloseScreen);

        fileText.text = $"File: {ExportPath}";
    }

    void BuildScrollArea(Transform parent)
    {
        GameObject wrapper = new GameObject("ScrollAreaWrapper");
        wrapper.transform.SetParent(parent, false);
        LayoutElement wrapperLE = wrapper.AddComponent<LayoutElement>();
        wrapperLE.flexibleHeight = 1f;
        wrapperLE.minHeight = 260;

        VerticalLayoutGroup wrapLayout = wrapper.AddComponent<VerticalLayoutGroup>();
        wrapLayout.spacing = 2;
        wrapLayout.padding = new RectOffset(0, 0, 0, 0);
        wrapLayout.childForceExpandWidth = true;
        wrapLayout.childForceExpandHeight = false;

        GameObject scrollObj = new GameObject("ScrollView");
        scrollObj.transform.SetParent(wrapper.transform, false);
        Image scrollBg = scrollObj.AddComponent<Image>();
        scrollBg.color = new Color(0.08f, 0.11f, 0.16f, 0.55f);
        LayoutElement scrollLE = scrollObj.AddComponent<LayoutElement>();
        scrollLE.flexibleHeight = 1f;
        scrollLE.minHeight = 240;

        ScrollRect sr = scrollObj.AddComponent<ScrollRect>();
        sr.horizontal = true;
        sr.vertical = true;
        sr.movementType = ScrollRect.MovementType.Clamped;
        sr.scrollSensitivity = 25f;

        RectTransform scrollRt = scrollObj.GetComponent<RectTransform>();

        GameObject viewport = new GameObject("Viewport");
        viewport.transform.SetParent(scrollObj.transform, false);
        Image vpImg = viewport.AddComponent<Image>();
        vpImg.color = new Color(1, 1, 1, 0.01f);
        Mask mask = viewport.AddComponent<Mask>();
        mask.showMaskGraphic = false;

        RectTransform vpRt = viewport.GetComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(2, 14);
        vpRt.offsetMax = new Vector2(-14, -14);

        GameObject content = new GameObject("Content");
        content.transform.SetParent(viewport.transform, false);
        contentRt = content.AddComponent<RectTransform>();
        contentRt.anchorMin = new Vector2(0, 1);
        contentRt.anchorMax = new Vector2(0, 1);
        contentRt.pivot = new Vector2(0, 1);

        VerticalLayoutGroup contentLayout = content.AddComponent<VerticalLayoutGroup>();
        contentLayout.padding = new RectOffset(4, 4, 4, 4);
        contentLayout.spacing = 4;
        contentLayout.childForceExpandWidth = false;
        contentLayout.childForceExpandHeight = false;

        ContentSizeFitter fitter = content.AddComponent<ContentSizeFitter>();
        fitter.horizontalFit = ContentSizeFitter.FitMode.PreferredSize;
        fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

        sr.viewport = vpRt;
        sr.content = contentRt;

        GameObject hScrollObj = new GameObject("HorizontalScrollbar");
        hScrollObj.transform.SetParent(scrollObj.transform, false);
        Image hBg = hScrollObj.AddComponent<Image>();
        hBg.color = new Color(0.14f, 0.18f, 0.24f, 0.9f);
        Scrollbar hScrollbar = hScrollObj.AddComponent<Scrollbar>();
        hScrollbar.direction = Scrollbar.Direction.LeftToRight;

        RectTransform hRt = hScrollObj.GetComponent<RectTransform>();
        hRt.anchorMin = new Vector2(0, 0);
        hRt.anchorMax = new Vector2(1, 0);
        hRt.pivot = new Vector2(0.5f, 0);
        hRt.sizeDelta = new Vector2(0, 12);

        GameObject hHandle = new GameObject("Handle");
        hHandle.transform.SetParent(hScrollObj.transform, false);
        Image hHandleImg = hHandle.AddComponent<Image>();
        hHandleImg.color = new Color(0.35f, 0.65f, 1f, 0.95f);
        RectTransform hHandleRt = hHandle.GetComponent<RectTransform>();
        hHandleRt.anchorMin = Vector2.zero;
        hHandleRt.anchorMax = Vector2.one;
        hHandleRt.offsetMin = Vector2.zero;
        hHandleRt.offsetMax = Vector2.zero;
        hScrollbar.targetGraphic = hHandleImg;
        hScrollbar.handleRect = hHandleRt;

        GameObject vScrollObj = new GameObject("VerticalScrollbar");
        vScrollObj.transform.SetParent(scrollObj.transform, false);
        Image vBg = vScrollObj.AddComponent<Image>();
        vBg.color = new Color(0.14f, 0.18f, 0.24f, 0.9f);
        Scrollbar vScrollbar = vScrollObj.AddComponent<Scrollbar>();
        vScrollbar.direction = Scrollbar.Direction.BottomToTop;

        RectTransform vRt = vScrollObj.GetComponent<RectTransform>();
        vRt.anchorMin = new Vector2(1, 0);
        vRt.anchorMax = new Vector2(1, 1);
        vRt.pivot = new Vector2(1, 0.5f);
        vRt.sizeDelta = new Vector2(12, 0);

        GameObject vHandle = new GameObject("Handle");
        vHandle.transform.SetParent(vScrollObj.transform, false);
        Image vHandleImg = vHandle.AddComponent<Image>();
        vHandleImg.color = new Color(0.35f, 0.65f, 1f, 0.95f);
        RectTransform vHandleRt = vHandle.GetComponent<RectTransform>();
        vHandleRt.anchorMin = Vector2.zero;
        vHandleRt.anchorMax = Vector2.one;
        vHandleRt.offsetMin = Vector2.zero;
        vHandleRt.offsetMax = Vector2.zero;
        vScrollbar.targetGraphic = vHandleImg;
        vScrollbar.handleRect = vHandleRt;

        sr.horizontalScrollbar = hScrollbar;
        sr.verticalScrollbar = vScrollbar;
        sr.horizontalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHideAndExpandViewport;
        sr.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.Permanent;

        BuildScrollableContent();
    }

    void BuildScrollableContent()
    {
        GameObject headerRow = CreateRowContainer(contentRt, "HeaderRow", 22);
        CreateHeaderCell(headerRow.transform, "Str", COL_STRING);
        CreateHeaderCell(headerRow.transform, "Nt", COL_NOTE);
        CreateHeaderCell(headerRow.transform, "Lv", COL_LIVE);
        CreateHeaderCell(headerRow.transform, "Thr", COL_THRESHOLD);
        CreateHeaderCell(headerRow.transform, "A", COL_ARMED);
        CreateHeaderCell(headerRow.transform, "T", COL_TRIG);

        for (int i = 0; i < 6; i++)
            rows.Add(BuildThresholdRow(contentRt, i));

        GameObject liveCard = CreateCard(contentRt, "LiveCard");
        LayoutElement liveLE = liveCard.AddComponent<LayoutElement>();
        liveLE.minHeight = 110;
        liveLE.preferredHeight = 110;
        liveLE.minWidth = TableWidth;
        liveLE.preferredWidth = TableWidth;

        liveText = CreateCardText(liveCard.transform, "LiveText", 10);
    }

    RowRefs BuildThresholdRow(Transform parent, int index)
    {
        GameObject rowObj = CreateRowContainer(parent, $"Row_{index}", ROW_HEIGHT);

        TextMeshProUGUI stringLabel = CreateCellText(rowObj.transform, stringNames[index], COL_STRING);
        TextMeshProUGUI noteLabel = CreateCellText(rowObj.transform, openNotes[index], COL_NOTE);
        TextMeshProUGUI liveScoreLabel = CreateCellText(rowObj.transform, "0.00", COL_LIVE);
        TMP_InputField thresholdInput = CreateInputCell(rowObj.transform, "0.00", COL_THRESHOLD);
        TextMeshProUGUI armedLabel = CreateCellText(rowObj.transform, "Y", COL_ARMED);
        TextMeshProUGUI triggeredLabel = CreateCellText(rowObj.transform, "-", COL_TRIG);

        return new RowRefs
        {
            stringLabel = stringLabel,
            noteLabel = noteLabel,
            liveScoreLabel = liveScoreLabel,
            thresholdInput = thresholdInput,
            armedLabel = armedLabel,
            triggeredLabel = triggeredLabel
        };
    }

    GameObject CreatePanel(Transform parent, string name, Color color)
    {
        GameObject go = new GameObject(name);
        go.transform.SetParent(parent, false);
        Image img = go.AddComponent<Image>();
        img.color = color;
        return go;
    }

    GameObject CreateCard(Transform parent, string name)
    {
        GameObject go = CreatePanel(parent, name, new Color(0.10f, 0.14f, 0.20f, 0.96f));
        VerticalLayoutGroup vlg = go.AddComponent<VerticalLayoutGroup>();
        vlg.padding = new RectOffset(6, 6, 5, 5);
        vlg.spacing = 2;
        vlg.childForceExpandWidth = true;
        vlg.childForceExpandHeight = true;
        return go;
    }

    GameObject CreateRowContainer(Transform parent, string name, float height)
    {
        GameObject row = new GameObject(name);
        row.transform.SetParent(parent, false);

        HorizontalLayoutGroup hlg = row.AddComponent<HorizontalLayoutGroup>();
        hlg.spacing = GAP;
        hlg.childForceExpandWidth = false;
        hlg.childForceExpandHeight = true;
        hlg.childControlWidth = false;
        hlg.childControlHeight = true;

        LayoutElement le = row.AddComponent<LayoutElement>();
        le.minHeight = height;
        le.preferredHeight = height;
        le.minWidth = TableWidth;
        le.preferredWidth = TableWidth;

        return row;
    }

    void CreateHeaderCell(Transform parent, string label, float width)
    {
        TextMeshProUGUI t = CreateText(parent, $"{label}_Header", label, 9, FontStyles.Bold, TextAlignmentOptions.Center);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
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
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;
        return text;
    }

    TextMeshProUGUI CreateCardText(Transform parent, string name, float size)
    {
        TextMeshProUGUI t = CreateText(parent, name, "", size, FontStyles.Normal, TextAlignmentOptions.TopLeft);
        t.enableWordWrapping = true;
        t.overflowMode = TextOverflowModes.Overflow;
        RectTransform rt = t.GetComponent<RectTransform>();
        rt.sizeDelta = new Vector2(TableWidth - 12f, 84);
        return t;
    }

    TextMeshProUGUI CreateCellText(Transform parent, string content, float width)
    {
        TextMeshProUGUI t = CreateText(parent, "Cell", content, 9, FontStyles.Normal, TextAlignmentOptions.Center);
        LayoutElement le = t.gameObject.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        return t;
    }

    TMP_InputField CreateInputCell(Transform parent, string value, float width)
    {
        GameObject go = new GameObject("ThresholdInput");
        go.transform.SetParent(parent, false);

        Image bg = go.AddComponent<Image>();
        bg.color = new Color(0.14f, 0.18f, 0.24f, 1f);

        LayoutElement le = go.AddComponent<LayoutElement>();
        le.minWidth = width;
        le.preferredWidth = width;
        le.minHeight = 22;
        le.preferredHeight = 22;

        TMP_InputField input = go.AddComponent<TMP_InputField>();

        GameObject textViewport = new GameObject("TextViewport");
        textViewport.transform.SetParent(go.transform, false);
        RectTransform vpRt = textViewport.AddComponent<RectTransform>();
        vpRt.anchorMin = Vector2.zero;
        vpRt.anchorMax = Vector2.one;
        vpRt.offsetMin = new Vector2(2, 2);
        vpRt.offsetMax = new Vector2(-2, -2);
        textViewport.AddComponent<RectMask2D>();

        GameObject textGo = new GameObject("Text");
        textGo.transform.SetParent(textViewport.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = value;
        text.fontSize = 9;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;

        RectTransform textRt = text.GetComponent<RectTransform>();
        textRt.anchorMin = Vector2.zero;
        textRt.anchorMax = Vector2.one;
        textRt.offsetMin = Vector2.zero;
        textRt.offsetMax = Vector2.zero;

        GameObject placeholderGo = new GameObject("Placeholder");
        placeholderGo.transform.SetParent(textViewport.transform, false);
        TextMeshProUGUI placeholder = placeholderGo.AddComponent<TextMeshProUGUI>();
        placeholder.text = "0.00";
        placeholder.fontSize = 9;
        placeholder.alignment = TextAlignmentOptions.Center;
        placeholder.color = new Color(1f, 1f, 1f, 0.35f);

        RectTransform phRt = placeholder.GetComponent<RectTransform>();
        phRt.anchorMin = Vector2.zero;
        phRt.anchorMax = Vector2.one;
        phRt.offsetMin = Vector2.zero;
        phRt.offsetMax = Vector2.zero;

        input.textViewport = vpRt;
        input.textComponent = text;
        input.placeholder = placeholder;
        input.contentType = TMP_InputField.ContentType.DecimalNumber;
        input.text = value;

        return input;
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
        le.minHeight = 28;
        le.preferredHeight = 28;
        le.minWidth = 62;

        GameObject textGo = new GameObject("Label");
        textGo.transform.SetParent(go.transform, false);
        TextMeshProUGUI text = textGo.AddComponent<TextMeshProUGUI>();
        text.text = label;
        text.fontSize = 10;
        text.fontStyle = FontStyles.Bold;
        text.alignment = TextAlignmentOptions.Center;
        text.color = Color.white;
        text.enableWordWrapping = false;
        text.overflowMode = TextOverflowModes.Ellipsis;

        RectTransform rt = text.GetComponent<RectTransform>();
        rt.anchorMin = Vector2.zero;
        rt.anchorMax = Vector2.one;
        rt.offsetMin = new Vector2(2, 2);
        rt.offsetMax = new Vector2(-2, -2);

        return btn;
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

    void ReceiveLoop()
    {
        try
        {
            udpClient = new UdpClient(telemetryPort);
            IPEndPoint anyIP = new IPEndPoint(IPAddress.Any, 0);

            while (isRunning)
            {
                byte[] data = udpClient.Receive(ref anyIP);
                latestPacketRaw = System.Text.Encoding.UTF8.GetString(data);
            }
        }
        catch { }
    }

    void ParseTelemetry(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw) || !raw.StartsWith("T|"))
            return;

        string[] parts = raw.Split('|');
        if (parts.Length < 15)
            return;

        TelemetryState t = new TelemetryState();
        int.TryParse(parts[1], out t.pluckId);
        t.primaryNote = parts[2];
        t.notesCsv = parts[3];
        float.TryParse(parts[4], out t.freq);
        float.TryParse(parts[5], out t.conf);
        t.gamePluck = parts[6] == "1";
        float.TryParse(parts[7], out t.peak);
        float.TryParse(parts[8], out t.rms);
        float.TryParse(parts[9], out t.brightPeak);
        float.TryParse(parts[10], out t.brightRms);
        t.scores = ParseFloatCsv(parts[11], 6);
        t.thresholds = ParseFloatCsv(parts[12], 6);
        t.armed = ParseBoolCsv(parts[13], 6);
        t.triggered = ParseBoolCsv(parts[14], 6);

        latestTelemetry = t;

        if (t.gamePluck && t.pluckId != lastSeenGameplayPluckId)
        {
            lastSeenGameplayPluckId = t.pluckId;
            lastGameplayPluckUiTime = Time.unscaledTime;
        }
    }

    float[] ParseFloatCsv(string csv, int count)
    {
        float[] arr = new float[count];
        string[] parts = csv.Split(',');
        for (int i = 0; i < Mathf.Min(count, parts.Length); i++)
            float.TryParse(parts[i], out arr[i]);
        return arr;
    }

    bool[] ParseBoolCsv(string csv, int count)
    {
        bool[] arr = new bool[count];
        string[] parts = csv.Split(',');
        for (int i = 0; i < Mathf.Min(count, parts.Length); i++)
            arr[i] = parts[i] == "1";
        return arr;
    }

    void UpdateLiveUi()
    {
        bool gameplayPluckedNow = (Time.unscaledTime - lastGameplayPluckUiTime) <= gameplayPluckVisibleSeconds;
        string pluckText = gameplayPluckedNow ? "<color=#7CFF8D>PLUCKED</color>" : "<color=#B8C0CC>idle</color>";
        string noteText = string.IsNullOrWhiteSpace(latestTelemetry.primaryNote) ? "--" : latestTelemetry.primaryNote;

        liveText.text =
            $"Gameplay: {pluckText}\n" +
            $"Primary: <color=#FFD36E>{noteText}</color>\n" +
            $"Detected: <color=#9FD3FF>{(string.IsNullOrWhiteSpace(latestTelemetry.notesCsv) ? "--" : latestTelemetry.notesCsv)}</color>\n" +
            $"Freq {latestTelemetry.freq:F1}  Conf {latestTelemetry.conf:F2}\n" +
            $"Peak {latestTelemetry.peak:F4}  RMS {latestTelemetry.rms:F4}\n" +
            $"Bright {latestTelemetry.brightPeak:F4}/{latestTelemetry.brightRms:F4}";
    }

    void UpdateRowsUi()
    {
        for (int i = 0; i < rows.Count; i++)
        {
            float score = (latestTelemetry.scores != null && latestTelemetry.scores.Length > i) ? latestTelemetry.scores[i] : 0f;
            bool armed = (latestTelemetry.armed != null && latestTelemetry.armed.Length > i) && latestTelemetry.armed[i];
            bool triggered = (latestTelemetry.triggered != null && latestTelemetry.triggered.Length > i) && latestTelemetry.triggered[i];

            rows[i].liveScoreLabel.text = score.ToString("F2");
            rows[i].armedLabel.text = armed ? "<color=#7CFF8D>Y</color>" : "<color=#E6A96A>N</color>";
            rows[i].triggeredLabel.text = triggered ? "<color=#7CFF8D>Y</color>" : "-";
        }
    }

    void SaveFromUiToFile()
    {
        for (int i = 0; i < profile.strings.Count && i < rows.Count; i++)
        {
            if (float.TryParse(rows[i].thresholdInput.text, out float value))
                profile.strings[i].threshold = value;
        }

        try
        {
            string json = JsonUtility.ToJson(profile, true);
            PlayerPrefs.SetString("guitar_manual_pluck_thresholds_json", json);
            PlayerPrefs.Save();
            File.WriteAllText(ExportPath, json);
            fileText.text = $"Saved ✔  {ExportPath}";
        }
        catch (Exception e)
        {
            fileText.text = $"Save failed: {e.Message}";
        }
    }

    void LoadProfileIntoUi()
    {
        try
        {
            string json = null;

            if (File.Exists(ExportPath))
                json = File.ReadAllText(ExportPath);
            else if (PlayerPrefs.HasKey("guitar_manual_pluck_thresholds_json"))
                json = PlayerPrefs.GetString("guitar_manual_pluck_thresholds_json");

            if (!string.IsNullOrWhiteSpace(json))
            {
                ManualThresholdProfile loaded = JsonUtility.FromJson<ManualThresholdProfile>(json);
                if (loaded != null && loaded.strings != null && loaded.strings.Count == 6)
                    profile = loaded;
            }

            for (int i = 0; i < rows.Count && i < profile.strings.Count; i++)
                rows[i].thresholdInput.text = profile.strings[i].threshold.ToString("F2");

            fileText.text = $"File: {ExportPath}";
        }
        catch (Exception e)
        {
            fileText.text = $"Load failed: {e.Message}";
        }
    }

    void BuildDefaultProfile()
    {
        profile = new ManualThresholdProfile();
        profile.version = 1;
        profile.strings = new List<StringThresholdEntry>();

        float[] defaults = { 14f, 13f, 12f, 11f, 10f, 9f };
        for (int i = 0; i < 6; i++)
        {
            profile.strings.Add(new StringThresholdEntry
            {
                stringName = stringNames[i],
                openNote = openNotes[i],
                threshold = defaults[i]
            });
        }
    }

    [Serializable]
    public class ManualThresholdProfile
    {
        public int version = 1;
        public List<StringThresholdEntry> strings = new List<StringThresholdEntry>();
    }

    [Serializable]
    public class StringThresholdEntry
    {
        public string stringName;
        public string openNote;
        public float threshold;
    }

    public class RowRefs
    {
        public TextMeshProUGUI stringLabel;
        public TextMeshProUGUI noteLabel;
        public TextMeshProUGUI liveScoreLabel;
        public TMP_InputField thresholdInput;
        public TextMeshProUGUI armedLabel;
        public TextMeshProUGUI triggeredLabel;
    }

    public struct TelemetryState
    {
        public int pluckId;
        public string primaryNote;
        public string notesCsv;
        public float freq;
        public float conf;
        public bool gamePluck;
        public float peak;
        public float rms;
        public float brightPeak;
        public float brightRms;
        public float[] scores;
        public float[] thresholds;
        public bool[] armed;
        public bool[] triggered;
    }
}