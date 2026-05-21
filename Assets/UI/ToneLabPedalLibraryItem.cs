using System;
using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalLibraryItem : VisualElement
{
    public Button ActionButton { get; }
    public bool SuppressNextClick { get; set; }

    public ToneLabPedalLibraryItem(IToneLabPedalDescriptor descriptor, Action onAdd)
    {
        if (descriptor == null)
            throw new ArgumentNullException(nameof(descriptor));

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
        style.minHeight = 116f;
        style.paddingLeft = 4f;
        style.paddingRight = 4f;
        style.paddingTop = 10f;
        style.paddingBottom = 10f;
        style.marginBottom = 2f;

        VisualElement previewWrap = new VisualElement();
        previewWrap.style.width = 86f;
        previewWrap.style.height = 112f;
        previewWrap.style.marginRight = 14f;
        previewWrap.style.alignItems = Align.Center;
        previewWrap.style.justifyContent = Justify.Center;
        previewWrap.style.scale = new Scale(new Vector3(0.74f, 0.74f, 1f));
        previewWrap.Add(ToneLabPedalVisualBuilder.BuildLibraryPreview(descriptor.Appearance, descriptor.ShortName));
        Add(previewWrap);

        VisualElement copyColumn = new VisualElement();
        copyColumn.style.flexGrow = 1f;
        copyColumn.style.marginRight = 14f;
        Add(copyColumn);

        Label titleLabel = new Label(descriptor.DisplayName);
        titleLabel.style.color = new Color(0.95f, 0.95f, 0.93f, 1f);
        titleLabel.style.fontSize = 18f;
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

        RegisterCallback<MouseEnterEvent>(_ =>
        {
            style.backgroundColor = new Color(1f, 1f, 1f, 0.06f);
            style.borderBottomColor = new Color(1f, 1f, 1f, 0.24f);
        });
        RegisterCallback<MouseLeaveEvent>(_ =>
        {
            style.backgroundColor = new Color(0f, 0f, 0f, 0f);
            style.borderBottomColor = new Color(1f, 1f, 1f, 0.13f);
        });
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
