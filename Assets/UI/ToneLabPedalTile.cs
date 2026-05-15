using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalTile : VisualElement
{
    private readonly ToneLabPedalAppearance appearance;
    private readonly ToneLabPedalVisualParts visualParts;
    private bool isSelected;
    private bool isHovered;
    private bool isDragging;

    public string PedalInstanceId { get; }
    public UnityToneLabRuntime.ToneLabPedalType PedalType { get; }
    public Button BypassButton => visualParts.BypassButton;
    public bool IsPedalEnabled { get; private set; }

    public ToneLabPedalTile(string pedalInstanceId, IToneLabPedalDescriptor descriptor)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        PedalInstanceId = pedalInstanceId ?? string.Empty;
        PedalType = descriptor.PedalType;
        appearance = descriptor.Appearance ?? ToneLabPedalAppearance.CreateDefault();
        visualParts = ToneLabPedalVisualBuilder.BuildBoardTile(appearance, descriptor.DisplayName, descriptor.ShortName);

        name = $"tone-lab-pedal-{PedalInstanceId}";
        pickingMode = PickingMode.Position;
        style.width = ToneLabPedalVisualBuilder.BoardTileWidth;
        style.minWidth = ToneLabPedalVisualBuilder.BoardTileWidth;
        style.height = ToneLabPedalVisualBuilder.BoardTileHeight;
        style.marginRight = 18f;
        style.marginTop = 4f;
        style.marginBottom = 4f;
        style.alignItems = Align.Center;
        style.justifyContent = Justify.Center;

        Add(visualParts.Root);

        RegisterCallback<MouseEnterEvent>(_ =>
        {
            isHovered = true;
            UpdateVisualState();
        });
        RegisterCallback<MouseLeaveEvent>(_ =>
        {
            isHovered = false;
            UpdateVisualState();
        });

        RegisterBypassHover();
        SetPedalEnabledVisual(true);
    }

    public void SetSelected(bool selected)
    {
        isSelected = selected;
        UpdateVisualState();
    }

    public void SetPedalEnabledVisual(bool enabled)
    {
        IsPedalEnabled = enabled;

        if (visualParts.Led != null)
            visualParts.Led.style.backgroundColor = enabled ? appearance.LedOnColor : appearance.LedOffColor;

        if (visualParts.DisabledOverlay != null)
            visualParts.DisabledOverlay.style.opacity = enabled ? 0f : 1f;

        if (visualParts.BypassButton != null)
        {
            visualParts.BypassButton.text = enabled ? "ON" : "OFF";
            visualParts.BypassButton.style.backgroundColor = enabled
                ? Darken(appearance.LabelStripColor, 0.05f)
                : new Color(0.16f, 0.17f, 0.19f, 1f);
            visualParts.BypassButton.style.color = enabled
                ? Lighten(appearance.TextColor, 0.04f)
                : new Color(0.85f, 0.89f, 0.94f, 0.95f);
            visualParts.BypassButton.style.borderTopWidth = 1f;
            visualParts.BypassButton.style.borderRightWidth = 1f;
            visualParts.BypassButton.style.borderBottomWidth = 1f;
            visualParts.BypassButton.style.borderLeftWidth = 1f;
            visualParts.BypassButton.style.borderTopColor = enabled
                ? Lighten(appearance.AccentColor, 0.25f)
                : new Color(0.36f, 0.40f, 0.49f, 1f);
            visualParts.BypassButton.style.borderRightColor = enabled
                ? Darken(appearance.EdgeColor, 0.02f)
                : new Color(0.12f, 0.14f, 0.17f, 1f);
            visualParts.BypassButton.style.borderBottomColor = enabled
                ? Darken(appearance.EdgeColor, 0.08f)
                : new Color(0.10f, 0.12f, 0.15f, 1f);
            visualParts.BypassButton.style.borderLeftColor = enabled
                ? Darken(appearance.EdgeColor, 0.02f)
                : new Color(0.12f, 0.14f, 0.17f, 1f);
        }

        if (visualParts.FooterStateLabel != null)
            visualParts.FooterStateLabel.text = enabled ? appearance.FooterEnabledText : appearance.FooterBypassedText;

        UpdateVisualState();
    }

    public void SetDragging(bool dragging)
    {
        isDragging = dragging;
        UpdateVisualState();
    }

    public void SetSourceHidden(bool hidden)
    {
        style.opacity = hidden ? 0f : 1f;
    }

    private void UpdateVisualState() 
    {
        float scale = 1f;
        if (isSelected)
            scale = 1.10f;
        else if (isHovered)
            scale = 1.05f;

        if (isDragging)
            scale = Mathf.Max(scale, 1.07f);

        if (visualParts.Shell != null)
        {
            visualParts.Shell.style.scale = new Scale(new Vector3(scale, scale, 1f));
            visualParts.Shell.style.opacity = isDragging ? 0.97f : 1f;
 
            if (isSelected)
            {
                visualParts.Shell.style.borderTopColor = Color.white;
                visualParts.Shell.style.borderRightColor = new Color(0.92f, 0.94f, 0.98f, 1f);
                visualParts.Shell.style.borderBottomColor = new Color(0.80f, 0.84f, 0.90f, 1f);
                visualParts.Shell.style.borderLeftColor = new Color(0.92f, 0.94f, 0.98f, 1f);
            }
            else if (isHovered)
            {
                visualParts.Shell.style.borderTopColor = Lighten(appearance.TopEdgeColor, 0.12f);
                visualParts.Shell.style.borderRightColor = Lighten(appearance.EdgeColor, 0.08f);
                visualParts.Shell.style.borderBottomColor = Darken(appearance.EdgeColor, 0.02f);
                visualParts.Shell.style.borderLeftColor = Lighten(appearance.EdgeColor, 0.08f);
            }
            else
            {
                visualParts.Shell.style.borderTopColor = appearance.TopEdgeColor;
                visualParts.Shell.style.borderRightColor = appearance.EdgeColor;
                visualParts.Shell.style.borderBottomColor = Darken(appearance.EdgeColor, 0.14f);
                visualParts.Shell.style.borderLeftColor = appearance.EdgeColor;
            }
        }

        if (visualParts.Shadow != null)
        {
            visualParts.Shadow.style.backgroundColor = isSelected
                ? new Color(0.92f, 0.95f, 1f, 0.20f)
                : (isHovered ? new Color(appearance.AccentColor.r, appearance.AccentColor.g, appearance.AccentColor.b, IsPedalEnabled ? 0.24f : 0.16f) : appearance.ShadowColor);
            visualParts.Shadow.style.translate = isDragging
                ? new Translate(0f, 12f, 0f)
                : (isSelected || isHovered ? new Translate(0f, 7f, 0f) : new Translate(0f, 5f, 0f));
        }
    }

    private void RegisterBypassHover()
    {
        if (visualParts.BypassButton == null)
            return;

        visualParts.BypassButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            bool enabled = string.Equals(visualParts.BypassButton.text, "ON", StringComparison.OrdinalIgnoreCase);
            visualParts.BypassButton.style.backgroundColor = enabled
                ? new Color(appearance.LabelStripColor.r, appearance.LabelStripColor.g, appearance.LabelStripColor.b, 0.92f)
                : new Color(0.22f, 0.24f, 0.27f, 1f);
            visualParts.BypassButton.style.color = Color.white;
        });

        visualParts.BypassButton.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            bool enabled = string.Equals(visualParts.BypassButton.text, "ON", StringComparison.OrdinalIgnoreCase);
            visualParts.BypassButton.style.backgroundColor = enabled
                ? Darken(appearance.LabelStripColor, 0.05f)
                : new Color(0.16f, 0.17f, 0.19f, 1f);
            visualParts.BypassButton.style.color = enabled
                ? Lighten(appearance.TextColor, 0.04f)
                : new Color(0.85f, 0.89f, 0.94f, 0.95f);
        });
    }

    private static Color Lighten(Color color, float amount)
    {
        return Color.Lerp(color, Color.white, Mathf.Clamp01(amount));
    }

    private static Color Darken(Color color, float amount)
    {
        return Color.Lerp(color, Color.black, Mathf.Clamp01(amount));
    }
}
