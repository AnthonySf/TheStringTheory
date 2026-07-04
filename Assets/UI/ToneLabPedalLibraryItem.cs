using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class ToneLabPedalLibraryItem : VisualElement
{
    public Button ActionButton { get; }
    public bool SuppressNextClick { get; set; }

    public ToneLabPedalLibraryItem(
        IToneLabPedalDescriptor descriptor,
        Action onAdd,
        float visualScale = 1f,
        FontDefinition? fontDefinition = null,
        float textScale = 1f)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

        float scale = Mathf.Clamp(visualScale, 0.80f, 1.50f);
        float resolvedTextScale = Mathf.Clamp(textScale, 0.75f, 1.80f);
        ApplyFont(this, fontDefinition);
        style.flexDirection = FlexDirection.Row;
        style.alignItems = Align.Center;
        style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        style.borderTopWidth = 0f;
        style.borderRightWidth = 0f;
        style.borderBottomWidth = 1f;
        style.borderLeftWidth = 0f;
        style.borderBottomColor = new Color(1f, 1f, 1f, 0.13f);
        style.borderTopLeftRadius = 0f;
        style.borderTopRightRadius = 0f;
        style.borderBottomLeftRadius = 0f;
        style.borderBottomRightRadius = 0f;
        style.minHeight = 116f * scale;
        style.paddingLeft = 4f * scale;
        style.paddingRight = 4f * scale;
        style.paddingTop = 10f * scale;
        style.paddingBottom = 10f * scale;
        style.marginBottom = 2f * scale;

        VisualElement previewWrap = new VisualElement();
        previewWrap.style.width = 86f * scale;
        previewWrap.style.height = 112f * scale;
        previewWrap.style.marginRight = 14f * scale;
        previewWrap.style.alignItems = Align.Center;
        previewWrap.style.justifyContent = Justify.Center;
        previewWrap.style.scale = new Scale(new Vector3(0.74f * scale, 0.74f * scale, 1f));
        previewWrap.Add(ToneLabPedalVisualBuilder.BuildLibraryPreview(descriptor.Appearance, descriptor.ShortName));
        Add(previewWrap);

        VisualElement copyColumn = new VisualElement();
        copyColumn.style.flexGrow = 1f;
        copyColumn.style.marginRight = 14f * scale;
        Add(copyColumn);

        Label titleLabel = new Label(descriptor.DisplayName);
        ApplyFont(titleLabel, fontDefinition);
        titleLabel.style.color = new Color(0.95f, 0.95f, 0.93f, 1f);
        titleLabel.style.fontSize = 18f * scale * resolvedTextScale;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        titleLabel.style.marginBottom = 4f * scale;
        copyColumn.Add(titleLabel);

        Label typeLabel = new Label(GetCategoryLabel(descriptor.PedalType));
        ApplyFont(typeLabel, fontDefinition);
        typeLabel.style.color = new Color(0.78f, 0.71f, 0.60f, 0.98f);
        typeLabel.style.fontSize = 12f * scale * resolvedTextScale;
        typeLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        typeLabel.style.marginBottom = 6f * scale;
        copyColumn.Add(typeLabel);

        Label descriptionLabel = new Label(descriptor.Description);
        ApplyFont(descriptionLabel, fontDefinition);
        descriptionLabel.style.color = new Color(0.63f, 0.66f, 0.70f, 0.98f);
        descriptionLabel.style.fontSize = 13f * scale * resolvedTextScale;
        descriptionLabel.style.whiteSpace = WhiteSpace.Normal;
        descriptionLabel.style.maxWidth = 230f * scale;
        copyColumn.Add(descriptionLabel);

        ActionButton = new Button(onAdd) { text = "Add" };
        ApplyFont(ActionButton, fontDefinition);
        ActionButton.style.minWidth = 82f * scale;
        ActionButton.style.height = 38f * scale;
        ActionButton.style.paddingLeft = 16f * scale;
        ActionButton.style.paddingRight = 16f * scale;
        ActionButton.style.fontSize = 15f * scale * resolvedTextScale;
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
        ActionButton.RegisterCallback<PointerDownEvent>(evt => evt.StopPropagation());
        ActionButton.RegisterCallback<PointerUpEvent>(evt => evt.StopPropagation());
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

        RegisterCallback<ClickEvent>(evt =>
        {
            if (SuppressNextClick)
            {
                SuppressNextClick = false;
                return;
            }

            if (ActionButton != null && evt.target is VisualElement target && ActionButton.Contains(target))
                return;

            onAdd?.Invoke();
        });

        RegisterCallback<MouseEnterEvent>(_ => SetControllerHovered(true));
        RegisterCallback<MouseLeaveEvent>(_ => SetControllerHovered(false));
    }

    private static void ApplyFont(VisualElement element, FontDefinition? fontDefinition)
    {
        if (element == null || !fontDefinition.HasValue)
            return;

        element.style.unityFontDefinition = fontDefinition.Value;
        element.style.letterSpacing = 0f;
    }

    public void SetControllerHovered(bool hovered)
    {
        style.backgroundColor = hovered ? new Color(1f, 1f, 1f, 0.06f) : new Color(0f, 0f, 0f, 0f);
        style.borderBottomColor = hovered ? new Color(1f, 1f, 1f, 0.24f) : new Color(1f, 1f, 1f, 0.13f);
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
            case UnityToneLabRuntime.ToneLabPedalType.Lv2Plugin:
                return "External LV2";
            case UnityToneLabRuntime.ToneLabPedalType.NamModel:
                return "NAM Amp";
            default:
                return "Pedal";
        }
    }
}
