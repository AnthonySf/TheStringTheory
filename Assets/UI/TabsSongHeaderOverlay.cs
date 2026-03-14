using UnityEngine;
using UnityEngine.UIElements;

public sealed class TabsSongHeaderOverlay
{
    private readonly GameObject rootObject;
    private readonly UIDocument document;
    private readonly PanelSettings runtimePanelSettings;
    private readonly Label songNameLabel;
    private readonly Label trackNameLabel;

    public TabsSongHeaderOverlay()
    {
        rootObject = new GameObject("TabsSongHeaderUI");
        document = rootObject.AddComponent<UIDocument>();

        runtimePanelSettings = ScriptableObject.CreateInstance<PanelSettings>();
        runtimePanelSettings.scaleMode = PanelScaleMode.ConstantPhysicalSize;
        runtimePanelSettings.targetDisplay = 0;
        runtimePanelSettings.sortingOrder = 120;

        document.panelSettings = runtimePanelSettings;

        VisualElement root = document.rootVisualElement;
        root.style.flexGrow = 1f;
        root.style.paddingTop = 18f;
        root.style.paddingLeft = 24f;
        root.style.paddingRight = 24f;
        root.style.justifyContent = Justify.FlexStart;
        root.style.alignItems = Align.FlexStart;
        root.pickingMode = PickingMode.Ignore;

        VisualElement card = new VisualElement();
        card.style.backgroundColor = new Color(0.06f, 0.08f, 0.12f, 0.82f);
        card.style.borderTopLeftRadius = 10f;
        card.style.borderTopRightRadius = 10f;
        card.style.borderBottomLeftRadius = 10f;
        card.style.borderBottomRightRadius = 10f;
        card.style.borderTopWidth = 1f;
        card.style.borderBottomWidth = 1f;
        card.style.borderLeftWidth = 1f;
        card.style.borderRightWidth = 1f;
        card.style.borderTopColor = new Color(0.38f, 0.61f, 0.85f, 0.7f);
        card.style.borderBottomColor = new Color(0.23f, 0.31f, 0.44f, 0.7f);
        card.style.borderLeftColor = new Color(0.23f, 0.31f, 0.44f, 0.7f);
        card.style.borderRightColor = new Color(0.23f, 0.31f, 0.44f, 0.7f);
        card.style.paddingLeft = 16f;
        card.style.paddingRight = 16f;
        card.style.paddingTop = 10f;
        card.style.paddingBottom = 10f;
        card.style.minWidth = 360f;
        card.style.maxWidth = 760f;
        card.style.unityTextAlign = TextAnchor.UpperLeft;

        songNameLabel = new Label("Song");
        songNameLabel.style.color = new Color(0.95f, 0.98f, 1f, 1f);
        songNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        songNameLabel.style.fontSize = 24f;
        songNameLabel.style.letterSpacing = 0.4f;
        songNameLabel.style.marginBottom = 3f;

        trackNameLabel = new Label("Track");
        trackNameLabel.style.color = new Color(0.74f, 0.85f, 0.98f, 0.95f);
        trackNameLabel.style.fontSize = 16f;

        card.Add(songNameLabel);
        card.Add(trackNameLabel);
        root.Add(card);
    }

    public void UpdateFromSnapshot(GuitarGameplaySnapshot snapshot)
    {
        if (snapshot == null)
            return;

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

        if (runtimePanelSettings != null)
            Object.Destroy(runtimePanelSettings);
    }
}
