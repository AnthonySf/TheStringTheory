using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class TabsSongHeaderOverlay
{
    private readonly GameObject rootObject;
    private readonly UIDocument document;
    private readonly PanelSettings panelSettings;
    private readonly bool ownsPanelSettings;
    private readonly VisualElement card;
    private readonly Label songNameLabel;
    private readonly Label trackNameLabel;

    private int lastScreenHeight = -1;

    public TabsSongHeaderOverlay()
    {
        rootObject = new GameObject("TabsSongHeaderUI");
        document = rootObject.AddComponent<UIDocument>();

        panelSettings = ResolvePanelSettings(out ownsPanelSettings);
        panelSettings.scaleMode = PanelScaleMode.ConstantPixelSize;
        panelSettings.scale = 1f;
        panelSettings.targetDisplay = 0;
        panelSettings.sortingOrder = 220;

        document.panelSettings = panelSettings;

        VisualElement root = document.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.paddingTop = 34f;
        root.style.paddingLeft = 34f;
        root.style.paddingRight = 24f;
        root.style.justifyContent = Justify.FlexStart;
        root.style.alignItems = Align.FlexStart;
        root.pickingMode = PickingMode.Ignore;

        card = new VisualElement();
        card.style.backgroundColor = new Color(0.05f, 0.08f, 0.12f, 0.90f);
        card.style.borderTopLeftRadius = 16f;
        card.style.borderTopRightRadius = 16f;
        card.style.borderBottomLeftRadius = 16f;
        card.style.borderBottomRightRadius = 16f;
        card.style.borderTopWidth = 2f;
        card.style.borderBottomWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderTopColor = new Color(0.46f, 0.75f, 1f, 0.92f);
        card.style.borderBottomColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        card.style.borderLeftColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        card.style.borderRightColor = new Color(0.20f, 0.30f, 0.43f, 0.90f);
        card.style.paddingLeft = 24f;
        card.style.paddingRight = 24f;
        card.style.paddingTop = 18f;
        card.style.paddingBottom = 16f;
        card.style.minWidth = 620f;
        card.style.maxWidth = 980f;
        card.style.unityTextAlign = TextAnchor.UpperLeft;

        songNameLabel = new Label("Song");
        songNameLabel.style.color = new Color(0.95f, 0.98f, 1f, 1f);
        songNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        songNameLabel.style.fontSize = 40f;
        songNameLabel.style.letterSpacing = 0.5f;
        songNameLabel.style.marginBottom = 6f;

        trackNameLabel = new Label("Track");
        trackNameLabel.style.color = new Color(0.71f, 0.88f, 1f, 1f);
        trackNameLabel.style.fontSize = 24f;

        card.Add(songNameLabel);
        card.Add(trackNameLabel);
        root.Add(card);

        ApplyResponsiveSizing(force: true);
    }

    public void UpdateFromSnapshot(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

        ApplyResponsiveSizing(force: false);

        string songName = "No song loaded";
        if (snapshot.availableSongNames != null && snapshot.selectedSongIndex >= 0 && snapshot.selectedSongIndex < snapshot.availableSongNames.Count)
            songName = snapshot.availableSongNames[snapshot.selectedSongIndex];

        string trackName = string.IsNullOrWhiteSpace(snapshot.selectedTrackDisplayName)
            ? "Track: Default"
            : snapshot.selectedTrackDisplayName;

        songNameLabel.text = songName;
        trackNameLabel.text = $"XML Track: {trackName}";
    }

    public void Dispose()
    {
        if (rootObject != null)
            Object.Destroy(rootObject);

        if (ownsPanelSettings && panelSettings != null)
            Object.Destroy(panelSettings);
    }


    private static PanelSettings ResolvePanelSettings(out bool ownsInstance)
    {
        PanelSettings existing = Resources.FindObjectsOfTypeAll<PanelSettings>()
            .FirstOrDefault(candidate => candidate != null && candidate.name == "PanelSettings");

        if (existing != null)
        {
            ownsInstance = false;
            return existing;
        }

        ownsInstance = true;
        return ScriptableObject.CreateInstance<PanelSettings>();
    }

    private void ApplyResponsiveSizing(bool force)
    {
        int screenHeight = Mathf.Max(1, Screen.height);
        if (!force && screenHeight == lastScreenHeight)
            return;

        lastScreenHeight = screenHeight;

        float songSize = Mathf.Clamp(screenHeight * 0.050f, 32f, 54f);
        float trackSize = Mathf.Clamp(screenHeight * 0.030f, 20f, 34f);
        float topPadding = Mathf.Clamp(screenHeight * 0.032f, 24f, 44f);

        document.rootVisualElement.style.paddingTop = topPadding;
        songNameLabel.style.fontSize = songSize;
        trackNameLabel.style.fontSize = trackSize;

        card.style.minWidth = Mathf.Clamp(Screen.width * 0.33f, 520f, 980f);
    }
}
