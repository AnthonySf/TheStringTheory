using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalLibraryItem : VisualElement
{
    private static readonly Color CardBackgroundColor = new Color(0.05f, 0.06f, 0.08f, 0.98f);
    private static readonly Color CardBorderColor = new Color(0.22f, 0.23f, 0.27f, 1f);

    public Button ActionButton { get; }

    public ToneLabPedalLibraryItem(IToneLabPedalDescriptor descriptor, Action onAdd)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;
        style.backgroundColor = CardBackgroundColor;
        style.borderTopWidth = 1f;
        style.borderRightWidth = 1f;
        style.borderBottomWidth = 1f;
        style.borderLeftWidth = 1f;
        style.borderTopColor = CardBorderColor;
        style.borderRightColor = CardBorderColor;
        style.borderBottomColor = CardBorderColor;
        style.borderLeftColor = CardBorderColor;
        style.borderTopLeftRadius = 14f;
        style.borderTopRightRadius = 14f;
        style.borderBottomLeftRadius = 14f;
        style.borderBottomRightRadius = 14f;
        style.minHeight = 152f;
        style.paddingLeft = 16f;
        style.paddingRight = 16f;
        style.paddingTop = 14f;
        style.paddingBottom = 14f;
        style.marginBottom = 12f;

        VisualElement previewWrap = new VisualElement();
        previewWrap.style.width = 118f;
        previewWrap.style.height = 148f;
        previewWrap.style.marginRight = 16f;
        previewWrap.style.alignItems = Align.Center;
        previewWrap.style.justifyContent = Justify.Center;
        previewWrap.Add(ToneLabPedalVisualBuilder.BuildLibraryPreview(descriptor.Appearance, descriptor.ShortName));
        Add(previewWrap);

        VisualElement copyColumn = new VisualElement();
        copyColumn.style.flexGrow = 1f;
        copyColumn.style.marginRight = 14f;
        Add(copyColumn);

        Label titleLabel = new Label(descriptor.DisplayName);
        titleLabel.style.color = new Color(0.95f, 0.95f, 0.93f, 1f);
        titleLabel.style.fontSize = 19f;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f;
        copyColumn.Add(titleLabel);

        Label typeLabel = new Label(GetCategoryLabel(descriptor.PedalType));
        typeLabel.style.color = new Color(0.78f, 0.71f, 0.60f, 0.98f);
        typeLabel.style.fontSize = 12f;
        typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        typeLabel.style.marginBottom = 6f;
        copyColumn.Add(typeLabel);

        Label descriptionLabel = new Label(descriptor.Description);
        descriptionLabel.style.color = new Color(0.63f, 0.66f, 0.70f, 0.98f);
        descriptionLabel.style.fontSize = 13f;
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.maxWidth = 230f;
        copyColumn.Add(descriptionLabel);

        ActionButton = new Button(onAdd) { text = "Add" };
        ActionButton.style.minWidth = 82f;
        ActionButton.style.height = 38f;
        ActionButton.style.paddingLeft = 16f;
        ActionButton.style.paddingRight = 16f;
        ActionButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        ActionButton.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
        ActionButton.style.borderTopWidth = 1f;
        ActionButton.style.borderRightWidth = 1f;
        ActionButton.style.borderBottomWidth = 1f;
        ActionButton.style.borderLeftWidth = 1f;
        ActionButton.style.borderTopColor = new Color(0.39f, 0.41f, 0.45f, 1f);
        ActionButton.style.borderRightColor = new Color(0.28f, 0.30f, 0.34f, 1f);
        ActionButton.style.borderBottomColor = new Color(0.22f, 0.24f, 0.28f, 1f);
        ActionButton.style.borderLeftColor = new Color(0.28f, 0.30f, 0.34f, 1f);
        ActionButton.style.borderTopLeftRadius = 10f;
        ActionButton.style.borderTopRightRadius = 10f;
        ActionButton.style.borderBottomLeftRadius = 10f;
        ActionButton.style.borderBottomRightRadius = 10f;
        ActionButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        ActionButton.style.alignSelf = Align.Center;
        ActionButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            ActionButton.style.backgroundColor = new Color(0.11f, 0.12f, 0.14f, 0.98f);
            ActionButton.style.color = Color.white;
            ActionButton.style.borderTopColor = new Color(0.66f, 0.68f, 0.72f, 1f);
            ActionButton.style.borderRightColor = new Color(0.36f, 0.38f, 0.42f, 1f);
            ActionButton.style.borderBottomColor = new Color(0.28f, 0.30f, 0.34f, 1f);
            ActionButton.style.borderLeftColor = new Color(0.36f, 0.38f, 0.42f, 1f);
        });
        ActionButton.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            ActionButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            ActionButton.style.color = new Color(0.92f, 0.93f, 0.95f, 1f);
            ActionButton.style.borderTopColor = new Color(0.39f, 0.41f, 0.45f, 1f);
            ActionButton.style.borderRightColor = new Color(0.28f, 0.30f, 0.34f, 1f);
            ActionButton.style.borderBottomColor = new Color(0.22f, 0.24f, 0.28f, 1f);
            ActionButton.style.borderLeftColor = new Color(0.28f, 0.30f, 0.34f, 1f);
        });
        Add(ActionButton);
    }

    private static string GetCategoryLabel(UnityToneLabRuntime.ToneLabPedalType pedalType)
    {
        switch (pedalType)
        {
            case UnityToneLabRuntime.ToneLabPedalType.NoiseGate:
            case UnityToneLabRuntime.ToneLabPedalType.Compressor:
                return "Dynamics";
            case UnityToneLabRuntime.ToneLabPedalType.Amp:
            case UnityToneLabRuntime.ToneLabPedalType.CabSim:
                return "Amplifier";
            case UnityToneLabRuntime.ToneLabPedalType.StudioEq:
                return "Shaping";
            case UnityToneLabRuntime.ToneLabPedalType.Distortion:
                return "Drive";
            case UnityToneLabRuntime.ToneLabPedalType.Chorus:
            case UnityToneLabRuntime.ToneLabPedalType.Phaser:
                return "Modulation";
            case UnityToneLabRuntime.ToneLabPedalType.Delay:
            case UnityToneLabRuntime.ToneLabPedalType.Reverb:
                return "Ambience";
            default:
                return "Pedal";
        }
    }
}
