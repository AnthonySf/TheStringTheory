using UnityEngine;
using UnityEngine.UIElements;

public sealed class ToneLabPedalVisualParts
{
    public VisualElement Root { get; set; }
    public VisualElement Shadow { get; set; }
    public VisualElement Shell { get; set; }
    public VisualElement DisabledOverlay { get; set; }
    public VisualElement Led { get; set; }
    public Button BypassButton { get; set; }
    public Label FooterStateLabel { get; set; }
    public Label PedalNameLabel { get; set; }
    public Label PedalTypeLabel { get; set; }
}

public static class ToneLabPedalVisualBuilder
{
    public const float BoardTileWidth = 196f;
    public const float BoardTileHeight = 282f;
    public const float LibraryPreviewWidth = 114f;
    public const float LibraryPreviewHeight = 156f;

    public static ToneLabPedalVisualParts BuildBoardTile(ToneLabPedalAppearance appearance, string pedalName, string pedalShortType)
    {
        return BuildVisual(appearance, pedalName, pedalShortType, compact: false);
    }

    public static VisualElement BuildLibraryPreview(ToneLabPedalAppearance appearance, string pedalShortType)
    {
        ToneLabPedalVisualParts parts = BuildVisual(appearance, pedalShortType, pedalShortType, compact: true);
        parts.Root.pickingMode = PickingMode.Ignore;
        return parts.Root;
    }

    private static ToneLabPedalVisualParts BuildVisual(ToneLabPedalAppearance appearance, string pedalName, string pedalShortType, bool compact)
    {
        ToneLabPedalAppearance resolvedAppearance = appearance ?? ToneLabPedalAppearance.CreateDefault();
        float rootWidth = compact ? LibraryPreviewWidth : BoardTileWidth;
        float rootHeight = compact ? LibraryPreviewHeight : BoardTileHeight;
        float shellWidth = compact ? 96f : 186f;
        float shellHeight = compact ? 144f : 270f;
        float facePadding = compact ? 7f : 12f;
        float labelStripHeight = compact ? 24f : 36f;
        float labelFontSize = compact ? 8.5f : 14f;
        float footswitchHousingHeight = compact ? 34f : 76f;
        float knobSize = compact ? 15f : 24f;
        float sliderHeight = compact ? 34f : 64f;

        ToneLabPedalVisualParts parts = new ToneLabPedalVisualParts();

        VisualElement root = new VisualElement();
        root.style.width = rootWidth;
        root.style.height = rootHeight;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;
        parts.Root = root;

        VisualElement outerShadow = new VisualElement();
        outerShadow.style.position = Position.Absolute;
        outerShadow.style.left = compact ? 12f : 6f;
        outerShadow.style.right = compact ? 12f : 6f;
        outerShadow.style.top = compact ? 12f : 16f;
        outerShadow.style.bottom = compact ? 6f : 8f;
        outerShadow.style.backgroundColor = resolvedAppearance.ShadowColor;
        outerShadow.style.borderTopLeftRadius = compact ? 13f : 18f;
        outerShadow.style.borderTopRightRadius = compact ? 13f : 18f;
        outerShadow.style.borderBottomLeftRadius = compact ? 9f : 13f;
        outerShadow.style.borderBottomRightRadius = compact ? 9f : 13f;
        outerShadow.style.translate = new Translate(0f, compact ? 4f : 6f, 0f);
        root.Add(outerShadow);
        parts.Shadow = outerShadow;

        VisualElement shell = new VisualElement();
        shell.style.width = shellWidth;
        shell.style.height = shellHeight;
        shell.style.backgroundColor = resolvedAppearance.BodyColor;
        shell.style.borderTopWidth = 2f;
        shell.style.borderRightWidth = 2f;
        shell.style.borderBottomWidth = 2f;
        shell.style.borderLeftWidth = 2f;
        shell.style.borderTopColor = resolvedAppearance.TopEdgeColor;
        shell.style.borderRightColor = resolvedAppearance.EdgeColor;
        shell.style.borderBottomColor = Darken(resolvedAppearance.EdgeColor, 0.14f);
        shell.style.borderLeftColor = resolvedAppearance.EdgeColor;
        shell.style.borderTopLeftRadius = compact ? 13f : 18f;
        shell.style.borderTopRightRadius = compact ? 13f : 18f;
        shell.style.borderBottomLeftRadius = compact ? 9f : 13f;
        shell.style.borderBottomRightRadius = compact ? 9f : 13f;
        shell.style.paddingLeft = compact ? 8f : 12f;
        shell.style.paddingRight = compact ? 8f : 12f;
        shell.style.paddingTop = compact ? 8f : 12f;
        shell.style.paddingBottom = compact ? 8f : 12f;
        shell.style.flexDirection = FlexDirection.Column;
        root.Add(shell);
        parts.Shell = shell;

        VisualElement faceSection = new VisualElement();
        faceSection.style.backgroundColor = resolvedAppearance.FaceColor;
        faceSection.style.borderTopLeftRadius = compact ? 9f : 13f;
        faceSection.style.borderTopRightRadius = compact ? 9f : 13f;
        faceSection.style.borderBottomLeftRadius = compact ? 8f : 11f;
        faceSection.style.borderBottomRightRadius = compact ? 8f : 11f;
        faceSection.style.borderTopWidth = 1f;
        faceSection.style.borderRightWidth = 1f;
        faceSection.style.borderBottomWidth = 1f;
        faceSection.style.borderLeftWidth = 1f;
        faceSection.style.borderTopColor = Lighten(resolvedAppearance.FaceColor, 0.24f);
        faceSection.style.borderRightColor = Darken(resolvedAppearance.BodyColor, 0.10f);
        faceSection.style.borderBottomColor = Darken(resolvedAppearance.BodyColor, 0.12f);
        faceSection.style.borderLeftColor = Darken(resolvedAppearance.BodyColor, 0.10f);
        faceSection.style.paddingLeft = facePadding;
        faceSection.style.paddingRight = facePadding;
        faceSection.style.paddingTop = facePadding;
        faceSection.style.paddingBottom = compact ? 6f : 8f;
        faceSection.style.marginBottom = compact ? 8f : 12f;
        faceSection.style.flexGrow = 1f;
        shell.Add(faceSection);

        VisualElement labelStrip = new VisualElement();
        labelStrip.style.height = labelStripHeight;
        labelStrip.style.minHeight = labelStripHeight;
        labelStrip.style.width = Length.Percent(100f);
        labelStrip.style.alignSelf = Align.Stretch;
        labelStrip.style.paddingLeft = compact ? 4f : 8f;
        labelStrip.style.paddingRight = compact ? 4f : 8f;
        labelStrip.style.backgroundColor = resolvedAppearance.LabelStripColor;
        labelStrip.style.borderTopLeftRadius = compact ? 6f : 8f;
        labelStrip.style.borderTopRightRadius = compact ? 6f : 8f;
        labelStrip.style.borderBottomLeftRadius = compact ? 6f : 8f;
        labelStrip.style.borderBottomRightRadius = compact ? 6f : 8f;
        labelStrip.style.justifyContent = Justify.Center;
        labelStrip.style.alignItems = Align.Center;
        labelStrip.style.marginBottom = compact ? 7f : 10f;
        faceSection.Add(labelStrip);

        Label pedalNameLabel = new Label((pedalName ?? string.Empty).ToUpperInvariant());
        pedalNameLabel.style.color = resolvedAppearance.TextColor;
        pedalNameLabel.style.fontSize = labelFontSize;
        pedalNameLabel.style.unityFontStyleAndWeight = FontStyle.Bold;
        pedalNameLabel.style.letterSpacing = compact ? 0.35f : 0.8f;
        pedalNameLabel.style.whiteSpace = WhiteSpace.NoWrap;
        pedalNameLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        pedalNameLabel.style.flexShrink = 1f;
        labelStrip.Add(pedalNameLabel);
        parts.PedalNameLabel = pedalNameLabel;

        if (!compact)
        {
            VisualElement metaRow = new VisualElement();
            metaRow.style.flexDirection = FlexDirection.Row;
            metaRow.style.justifyContent = Justify.SpaceBetween;
            metaRow.style.alignItems = Align.Center;
            metaRow.style.marginBottom = 10f;
            faceSection.Add(metaRow);

            VisualElement leftMeta = new VisualElement();
            leftMeta.style.flexDirection = FlexDirection.Row;
            leftMeta.style.alignItems = Align.Center;
            metaRow.Add(leftMeta);

            VisualElement led = CreateLed(resolvedAppearance);
            led.style.marginRight = 6f;
            leftMeta.Add(led);
            parts.Led = led;

            Button bypassButton = CreateBypassButton();
            metaRow.Add(bypassButton);
            parts.BypassButton = bypassButton;
        }

        VisualElement controlsWrap = new VisualElement();
        controlsWrap.style.flexGrow = 1f;
        controlsWrap.style.justifyContent = Justify.Center;
        controlsWrap.style.alignItems = Align.Center;
        controlsWrap.style.marginBottom = compact ? 4f : 8f;
        faceSection.Add(controlsWrap);

        VisualElement controlsStack = new VisualElement();
        controlsStack.style.width = Length.Percent(100f);
        controlsStack.style.justifyContent = Justify.Center;
        controlsStack.style.alignItems = Align.Center;
        controlsWrap.Add(controlsStack);

        if (resolvedAppearance.KnobCount > 0)
        {
            VisualElement knobRow = new VisualElement();
            knobRow.style.flexDirection = FlexDirection.Row;
            knobRow.style.justifyContent = Justify.Center;
            knobRow.style.alignItems = Align.Center;
            knobRow.style.marginBottom = compact ? 4f : 6f;
            controlsStack.Add(knobRow);

            for (int knobIndex = 0; knobIndex < resolvedAppearance.KnobCount; knobIndex++)
            {
                VisualElement knob = CreateKnobVisual(resolvedAppearance, knobSize, GetKnobAngle(knobIndex, resolvedAppearance.KnobCount));
                knob.style.marginLeft = compact ? 4f : 6f;
                knob.style.marginRight = compact ? 4f : 6f;
                knobRow.Add(knob);
            }
        }

        if (resolvedAppearance.SliderCount > 0)
        {
            VisualElement sliderRow = new VisualElement();
            sliderRow.style.flexDirection = FlexDirection.Row;
            sliderRow.style.justifyContent = Justify.Center;
            sliderRow.style.alignItems = Align.FlexEnd;
            controlsStack.Add(sliderRow);

            for (int sliderIndex = 0; sliderIndex < resolvedAppearance.SliderCount; sliderIndex++)
            {
                VisualElement slider = CreateSliderVisual(resolvedAppearance, sliderHeight, sliderIndex, resolvedAppearance.SliderCount);
                slider.style.marginLeft = compact ? 4f : 5f;
                slider.style.marginRight = compact ? 4f : 5f;
                sliderRow.Add(slider);
            }
        }

        VisualElement artArea = new VisualElement();
        artArea.style.height = compact ? 18f : 26f;
        artArea.style.justifyContent = Justify.Center;
        artArea.style.alignItems = Align.Center;
        artArea.style.marginTop = compact ? 2f : 4f;
        faceSection.Add(artArea);
        artArea.Add(CreateDecoration(resolvedAppearance, compact));

        VisualElement footswitchHousing = new VisualElement();
        footswitchHousing.style.height = footswitchHousingHeight;
        footswitchHousing.style.backgroundColor = Darken(resolvedAppearance.BodyColor, 0.08f);
        footswitchHousing.style.borderTopLeftRadius = compact ? 8f : 14f;
        footswitchHousing.style.borderTopRightRadius = compact ? 8f : 14f;
        footswitchHousing.style.borderBottomLeftRadius = compact ? 7f : 12f;
        footswitchHousing.style.borderBottomRightRadius = compact ? 7f : 12f;
        footswitchHousing.style.borderTopWidth = 1f;
        footswitchHousing.style.borderRightWidth = 1f;
        footswitchHousing.style.borderBottomWidth = 1f;
        footswitchHousing.style.borderLeftWidth = 1f;
        footswitchHousing.style.borderTopColor = Lighten(resolvedAppearance.BodyColor, 0.28f);
        footswitchHousing.style.borderRightColor = Darken(resolvedAppearance.EdgeColor, 0.05f);
        footswitchHousing.style.borderBottomColor = Darken(resolvedAppearance.EdgeColor, 0.12f);
        footswitchHousing.style.borderLeftColor = Darken(resolvedAppearance.EdgeColor, 0.05f);
        footswitchHousing.style.justifyContent = Justify.Center;
        footswitchHousing.style.alignItems = Align.Center;
        footswitchHousing.style.flexDirection = FlexDirection.Column;
        shell.Add(footswitchHousing);

        VisualElement footswitch = new VisualElement();
        float footswitchSize = compact ? 18f : 34f;
        footswitch.style.width = footswitchSize;
        footswitch.style.height = footswitchSize;
        footswitch.style.backgroundColor = Lighten(resolvedAppearance.FootswitchColor, 0.15f);
        footswitch.style.borderTopLeftRadius = 999f;
        footswitch.style.borderTopRightRadius = 999f;
        footswitch.style.borderBottomLeftRadius = 999f;
        footswitch.style.borderBottomRightRadius = 999f;
        footswitch.style.borderTopWidth = 1f;
        footswitch.style.borderRightWidth = 1f;
        footswitch.style.borderBottomWidth = 1f;
        footswitch.style.borderLeftWidth = 1f;
        footswitch.style.borderTopColor = new Color(0.98f, 0.98f, 0.98f, 0.96f);
        footswitch.style.borderRightColor = new Color(0.61f, 0.61f, 0.61f, 1f);
        footswitch.style.borderBottomColor = new Color(0.35f, 0.35f, 0.35f, 1f);
        footswitch.style.borderLeftColor = new Color(0.61f, 0.61f, 0.61f, 1f);
        footswitchHousing.Add(footswitch);

        VisualElement disabledOverlay = new VisualElement();
        disabledOverlay.style.position = Position.Absolute;
        disabledOverlay.style.left = 0f;
        disabledOverlay.style.right = 0f;
        disabledOverlay.style.top = 0f;
        disabledOverlay.style.bottom = 0f;
        disabledOverlay.style.backgroundColor = new Color(0f, 0f, 0f, compact ? 0.40f : 0.50f);
        disabledOverlay.style.borderTopLeftRadius = compact ? 13f : 18f;
        disabledOverlay.style.borderTopRightRadius = compact ? 13f : 18f;
        disabledOverlay.style.borderBottomLeftRadius = compact ? 9f : 13f;
        disabledOverlay.style.borderBottomRightRadius = compact ? 9f : 13f;
        disabledOverlay.style.opacity = 0f;
        disabledOverlay.pickingMode = PickingMode.Ignore;
        shell.Add(disabledOverlay);
        parts.DisabledOverlay = disabledOverlay;

        return parts;
    }

    private static VisualElement CreateDecoration(ToneLabPedalAppearance appearance, bool compact)
    {
        VisualElement root = new VisualElement();
        root.style.width = Length.Percent(100f);
        root.style.flexGrow = 1f;
        root.style.justifyContent = Justify.Center;
        root.style.alignItems = Align.Center;

        Color accent = appearance.AccentColor;
        Color softAccent = new Color(accent.r, accent.g, accent.b, compact ? 0.32f : 0.38f);
        Color faintAccent = new Color(accent.r, accent.g, accent.b, compact ? 0.15f : 0.20f);

        switch (appearance.DecorationStyle)
        {
            case ToneLabPedalDecorationStyle.WaveBands:
                for (int i = 0; i < 2; i++)
                {
                    VisualElement band = new VisualElement();
                    band.style.width = compact ? 34f : 54f;
                    band.style.height = compact ? 4f : 6f;
                    band.style.backgroundColor = i == 0 ? softAccent : faintAccent;
                    band.style.borderTopLeftRadius = 999f;
                    band.style.borderTopRightRadius = 999f;
                    band.style.borderBottomLeftRadius = 999f;
                    band.style.borderBottomRightRadius = 999f;
                    band.style.translate = new Translate(0f, i == 0 ? 0f : (compact ? 5f : 7f), 0f);
                    root.Add(band);
                }
                break;

            case ToneLabPedalDecorationStyle.SweepBars:
                for (int i = 0; i < 3; i++)
                {
                    VisualElement bar = new VisualElement();
                    bar.style.position = Position.Absolute;
                    bar.style.width = compact ? 26f : 38f;
                    bar.style.height = compact ? 3f : 5f;
                    bar.style.backgroundColor = i % 2 == 0 ? softAccent : faintAccent;
                    bar.style.borderTopLeftRadius = 999f;
                    bar.style.borderTopRightRadius = 999f;
                    bar.style.borderBottomLeftRadius = 999f;
                    bar.style.borderBottomRightRadius = 999f;
                    bar.style.rotate = new Rotate(new Angle(-28f + (i * 12f)));
                    bar.style.translate = new Translate(0f, (-6f + (i * (compact ? 5f : 7f))), 0f);
                    root.Add(bar);
                }
                break;

            case ToneLabPedalDecorationStyle.CenterStripe:
                VisualElement stripe = new VisualElement();
                stripe.style.width = compact ? 14f : 20f;
                stripe.style.height = compact ? 18f : 24f;
                stripe.style.backgroundColor = faintAccent;
                stripe.style.borderTopLeftRadius = 999f;
                stripe.style.borderTopRightRadius = 999f;
                stripe.style.borderBottomLeftRadius = 999f;
                stripe.style.borderBottomRightRadius = 999f;
                stripe.style.alignSelf = Align.Center;
                root.Add(stripe);

                VisualElement stripeHighlight = new VisualElement();
                stripeHighlight.style.position = Position.Absolute;
                stripeHighlight.style.width = compact ? 5f : 7f;
                stripeHighlight.style.height = compact ? 12f : 18f;
                stripeHighlight.style.backgroundColor = new Color(appearance.TextColor.r, appearance.TextColor.g, appearance.TextColor.b, compact ? 0.18f : 0.22f);
                stripeHighlight.style.borderTopLeftRadius = 999f;
                stripeHighlight.style.borderTopRightRadius = 999f;
                stripeHighlight.style.borderBottomLeftRadius = 999f;
                stripeHighlight.style.borderBottomRightRadius = 999f;
                root.Add(stripeHighlight);
                break;

            case ToneLabPedalDecorationStyle.SparkBars:
                for (int i = 0; i < 4; i++)
                {
                    VisualElement sparkle = new VisualElement();
                    sparkle.style.position = Position.Absolute;
                    sparkle.style.width = compact ? 2f : 3f;
                    sparkle.style.height = compact ? (8f + (i * 3f)) : (12f + (i * 4f));
                    sparkle.style.backgroundColor = i == 2 ? softAccent : faintAccent;
                    sparkle.style.borderTopLeftRadius = 999f;
                    sparkle.style.borderTopRightRadius = 999f;
                    sparkle.style.borderBottomLeftRadius = 999f;
                    sparkle.style.borderBottomRightRadius = 999f;
                    sparkle.style.translate = new Translate((-16f + (i * (compact ? 8f : 11f))), 0f, 0f);
                    root.Add(sparkle);
                }
                break;

            case ToneLabPedalDecorationStyle.MeterDots:
                VisualElement meterRow = new VisualElement();
                meterRow.style.flexDirection = FlexDirection.Row;
                meterRow.style.justifyContent = Justify.Center;
                meterRow.style.alignItems = Align.Center;
                for (int i = 0; i < 6; i++)
                {
                    VisualElement dot = new VisualElement();
                    dot.style.width = compact ? 5f : 7f;
                    dot.style.height = compact ? 5f : 7f;
                    dot.style.marginLeft = compact ? 1f : 3f;
                    dot.style.marginRight = compact ? 1f : 3f;
                    dot.style.backgroundColor = i >= 4 ? softAccent : faintAccent;
                    dot.style.borderTopLeftRadius = 999f;
                    dot.style.borderTopRightRadius = 999f;
                    dot.style.borderBottomLeftRadius = 999f;
                    dot.style.borderBottomRightRadius = 999f;
                    meterRow.Add(dot);
                }
                root.Add(meterRow);
                break;

            case ToneLabPedalDecorationStyle.Grille:
                VisualElement grille = new VisualElement();
                grille.style.width = compact ? 42f : 62f;
                grille.style.justifyContent = Justify.Center;
                grille.style.alignItems = Align.Stretch;
                for (int i = 0; i < 4; i++)
                {
                    VisualElement line = new VisualElement();
                    line.style.height = compact ? 2f : 3f;
                    line.style.marginTop = compact ? 2f : 3f;
                    line.style.marginBottom = compact ? 2f : 3f;
                    line.style.backgroundColor = i % 2 == 0 ? softAccent : faintAccent;
                    line.style.borderTopLeftRadius = 999f;
                    line.style.borderTopRightRadius = 999f;
                    line.style.borderBottomLeftRadius = 999f;
                    line.style.borderBottomRightRadius = 999f;
                    grille.Add(line);
                }
                root.Add(grille);
                break;
        }

        return root;
    }

    private static VisualElement CreateLed(ToneLabPedalAppearance appearance)
    {
        VisualElement led = new VisualElement();
        led.style.width = 14f;
        led.style.height = 14f;
        led.style.borderTopLeftRadius = 999f;
        led.style.borderTopRightRadius = 999f;
        led.style.borderBottomLeftRadius = 999f;
        led.style.borderBottomRightRadius = 999f;
        led.style.borderTopWidth = 1f;
        led.style.borderRightWidth = 1f;
        led.style.borderBottomWidth = 1f;
        led.style.borderLeftWidth = 1f;
        led.style.borderTopColor = Lighten(appearance.LedOnColor, 0.65f);
        led.style.borderRightColor = Darken(appearance.LedOffColor, 0.10f);
        led.style.borderBottomColor = Darken(appearance.LedOffColor, 0.16f);
        led.style.borderLeftColor = Darken(appearance.LedOffColor, 0.10f);
        led.style.backgroundColor = appearance.LedOnColor;
        return led;
    }

    private static Button CreateBypassButton()
    {
        Button button = new Button();
        button.text = "ON";
        button.style.minWidth = 48f;
        button.style.height = 24f;
        button.style.fontSize = 10f;
        button.style.unityFontStyleAndWeight = FontStyle.Bold;
        button.style.borderTopLeftRadius = 7f;
        button.style.borderTopRightRadius = 7f;
        button.style.borderBottomLeftRadius = 7f;
        button.style.borderBottomRightRadius = 7f;
        button.style.marginLeft = 4f;
        return button;
    }

    private static VisualElement CreateKnobVisual(ToneLabPedalAppearance appearance, float size, float angleDegrees)
    {
        VisualElement knobWrap = new VisualElement();
        knobWrap.style.width = size + 8f;
        knobWrap.style.height = size + 8f;
        knobWrap.style.alignItems = Align.Center;
        knobWrap.style.justifyContent = Justify.Center;

        VisualElement knob = new VisualElement();
        knob.style.width = size;
        knob.style.height = size;
        knob.style.backgroundColor = appearance.KnobColor;
        knob.style.borderTopLeftRadius = 999f;
        knob.style.borderTopRightRadius = 999f;
        knob.style.borderBottomLeftRadius = 999f;
        knob.style.borderBottomRightRadius = 999f;
        knob.style.borderTopWidth = 1f;
        knob.style.borderRightWidth = 1f;
        knob.style.borderBottomWidth = 1f;
        knob.style.borderLeftWidth = 1f;
        knob.style.borderTopColor = Lighten(appearance.KnobColor, 0.28f);
        knob.style.borderRightColor = Darken(appearance.KnobColor, 0.18f);
        knob.style.borderBottomColor = Darken(appearance.KnobColor, 0.26f);
        knob.style.borderLeftColor = Darken(appearance.KnobColor, 0.18f);
        knobWrap.Add(knob);

        VisualElement indicator = new VisualElement();
        indicator.style.position = Position.Absolute;
        indicator.style.top = 2f;
        indicator.style.width = Mathf.Max(2f, size * 0.10f);
        indicator.style.height = Mathf.Max(6f, size * 0.34f);
        indicator.style.backgroundColor = appearance.KnobIndicatorColor;
        indicator.style.borderTopLeftRadius = 999f;
        indicator.style.borderTopRightRadius = 999f;
        indicator.style.borderBottomLeftRadius = 999f;
        indicator.style.borderBottomRightRadius = 999f;
        indicator.style.rotate = new Rotate(new Angle(angleDegrees));
        knob.Add(indicator);

        return knobWrap;
    }

    private static VisualElement CreateSliderVisual(ToneLabPedalAppearance appearance, float height, int sliderIndex, int totalSliders)
    {
        VisualElement root = new VisualElement();
        root.style.width = 16f;
        root.style.height = height + 8f;
        root.style.alignItems = Align.Center;
        root.style.justifyContent = Justify.Center;

        VisualElement track = new VisualElement();
        track.style.width = 4f;
        track.style.height = height;
        track.style.backgroundColor = new Color(0.08f, 0.09f, 0.10f, 0.30f);
        track.style.borderTopLeftRadius = 999f;
        track.style.borderTopRightRadius = 999f;
        track.style.borderBottomLeftRadius = 999f;
        track.style.borderBottomRightRadius = 999f;
        root.Add(track);

        VisualElement handle = new VisualElement();
        handle.style.position = Position.Absolute;
        handle.style.width = 14f;
        handle.style.height = 8f;
        handle.style.backgroundColor = appearance.AccentColor;
        handle.style.borderTopLeftRadius = 999f;
        handle.style.borderTopRightRadius = 999f;
        handle.style.borderBottomLeftRadius = 999f;
        handle.style.borderBottomRightRadius = 999f;
        float normalized = totalSliders <= 1 ? 0.42f : (sliderIndex / (float)(totalSliders - 1));
        handle.style.top = Mathf.Lerp(height * 0.18f, height * 0.72f, normalized);
        root.Add(handle);

        return root;
    }

    private static float GetKnobAngle(int knobIndex, int knobCount)
    {
        if (knobCount <= 1)
            return -8f;

        float t = knobIndex / (float)(knobCount - 1);
        return Mathf.Lerp(-36f, 42f, t);
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
