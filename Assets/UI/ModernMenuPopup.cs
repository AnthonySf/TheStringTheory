using System;
using UnityEngine;
using UnityEngine.TextCore.Text;
using UnityEngine.UIElements;

public sealed class ModernMenuPopup
{
    public VisualElement Root { get; }
    public VisualElement ContentHost { get; }
    public Button PrimaryButton => primaryButton;

    private readonly VisualElement card;
    private readonly Label eyebrowLabel;
    private readonly Label titleLabel;
    private readonly Label messageLabel;
    private readonly Label calloutLabel;
    private readonly Label hintLabel;
    private readonly Button primaryButton;

    public ModernMenuPopup(string eyebrow, string title, string message, string callout, string hint, string primaryText, Action onPrimaryClick, FontDefinition bodyFont, FontDefinition titleFont)
    {
        TemplateContainer template = UiTemplateLoader.CloneRequired("UI/ModernMenuPopup", "UI/ModernMenuPopup");
        Root = template.QRequired<VisualElement>("modern-popup-root");
        StyleSheet styleSheet = Resources.Load<StyleSheet>("UI/ModernMenuPopup");
        if (styleSheet != null)
            Root.styleSheets.Add(styleSheet);
        Root.style.backgroundColor = new Color(0.02f, 0.03f, 0.04f, 0.58f);

        card = Root.QRequired<VisualElement>("modern-popup-card");
        eyebrowLabel = Root.QRequired<Label>("modern-popup-eyebrow");
        titleLabel = Root.QRequired<Label>("modern-popup-title");
        messageLabel = Root.QRequired<Label>("modern-popup-message");
        calloutLabel = Root.QRequired<Label>("modern-popup-callout");
        hintLabel = Root.QRequired<Label>("modern-popup-hint");
        ContentHost = Root.QRequired<VisualElement>("modern-popup-content");
        primaryButton = Root.QRequired<Button>("modern-popup-primary-button");

        card.style.backgroundColor = TabsSongHeaderOverlay.GlobalPanelColor;
        card.style.borderTopLeftRadius = 24f;
        card.style.borderTopRightRadius = 24f;
        card.style.borderBottomLeftRadius = 24f;
        card.style.borderBottomRightRadius = 24f;
        card.style.borderTopWidth = 3f;
        card.style.borderRightWidth = 3f;
        card.style.borderBottomWidth = 3f;
        card.style.borderLeftWidth = 3f;
        card.style.borderTopColor = TabsSongHeaderOverlay.GlobalDeepPanelColor;
        card.style.borderRightColor = TabsSongHeaderOverlay.GlobalDeepPanelColor;
        card.style.borderBottomColor = TabsSongHeaderOverlay.GlobalDeepPanelColor;
        card.style.borderLeftColor = TabsSongHeaderOverlay.GlobalDeepPanelColor;

        eyebrowLabel.text = eyebrow;
        eyebrowLabel.style.fontSize = 24f;
        eyebrowLabel.style.color = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
        eyebrowLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        eyebrowLabel.style.unityFontDefinition = bodyFont;
        eyebrowLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        titleLabel.text = title;
        titleLabel.style.fontSize = 84f;
        titleLabel.style.color = Color.white;
        titleLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        titleLabel.style.unityFontDefinition = titleFont;
        titleLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        messageLabel.text = message;
        messageLabel.enableRichText = true;
        messageLabel.style.fontSize = 46f;
        messageLabel.style.color = new Color(0.98f, 0.95f, 0.84f, 1f);
        messageLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        messageLabel.style.unityFontDefinition = bodyFont;
        messageLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

        calloutLabel.text = callout;
        calloutLabel.enableRichText = true;
        calloutLabel.style.display = string.IsNullOrWhiteSpace(callout) ? DisplayStyle.None : DisplayStyle.Flex;
        calloutLabel.style.fontSize = 40f;
        calloutLabel.style.color = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
        calloutLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        calloutLabel.style.unityFontDefinition = bodyFont;
        calloutLabel.style.unityFontStyleAndWeight = FontStyle.Bold;

        hintLabel.text = hint;
        hintLabel.enableRichText = true;
        hintLabel.style.display = string.IsNullOrWhiteSpace(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        hintLabel.style.fontSize = 34f;
        hintLabel.style.color = new Color(0.73f, 0.82f, 0.90f, 0.96f);
        hintLabel.style.unityTextAlign = TextAnchor.MiddleCenter;
        hintLabel.style.unityFontDefinition = bodyFont;
        hintLabel.style.unityFontStyleAndWeight = FontStyle.Normal;

        primaryButton.text = primaryText;
        primaryButton.clicked += onPrimaryClick;
        primaryButton.style.backgroundColor = new Color(0f, 0f, 0f, 0f);
        primaryButton.style.color = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
        primaryButton.style.unityFontDefinition = bodyFont;
        primaryButton.style.unityFontStyleAndWeight = FontStyle.Bold;
        primaryButton.style.borderTopWidth = 2f;
        primaryButton.style.borderRightWidth = 2f;
        primaryButton.style.borderBottomWidth = 2f;
        primaryButton.style.borderLeftWidth = 2f;
        primaryButton.style.borderTopColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
        primaryButton.style.borderRightColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
        primaryButton.style.borderBottomColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
        primaryButton.style.borderLeftColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
        primaryButton.style.borderTopLeftRadius = 18f;
        primaryButton.style.borderTopRightRadius = 18f;
        primaryButton.style.borderBottomLeftRadius = 18f;
        primaryButton.style.borderBottomRightRadius = 18f;
        primaryButton.style.translate = new Translate(0f, 0f);

        primaryButton.RegisterCallback<MouseEnterEvent>(_ =>
        {
            primaryButton.style.color = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
            primaryButton.style.borderTopColor = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
            primaryButton.style.borderRightColor = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
            primaryButton.style.borderBottomColor = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
            primaryButton.style.borderLeftColor = TabsSongHeaderOverlay.GlobalSecondaryAccentColor;
            primaryButton.style.scale = new Scale(new Vector3(1.02f, 1.02f, 1f));
            primaryButton.style.translate = new Translate(0f, -2f);
        });

        primaryButton.RegisterCallback<MouseLeaveEvent>(_ =>
        {
            primaryButton.style.color = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
            primaryButton.style.borderTopColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
            primaryButton.style.borderRightColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
            primaryButton.style.borderBottomColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
            primaryButton.style.borderLeftColor = TabsSongHeaderOverlay.GlobalPrimaryAccentColor;
            primaryButton.style.scale = new Scale(Vector3.one);
            primaryButton.style.translate = new Translate(0f, 0f);
        });

    }

    public void SetContent(string eyebrow, string title, string message, string callout, string hint, string primaryText)
    {
        eyebrowLabel.text = eyebrow ?? string.Empty;
        titleLabel.text = title ?? string.Empty;
        messageLabel.text = message ?? string.Empty;
        calloutLabel.text = callout ?? string.Empty;
        calloutLabel.style.display = string.IsNullOrWhiteSpace(callout) ? DisplayStyle.None : DisplayStyle.Flex;
        hintLabel.text = hint ?? string.Empty;
        hintLabel.style.display = string.IsNullOrWhiteSpace(hint) ? DisplayStyle.None : DisplayStyle.Flex;
        primaryButton.text = primaryText ?? string.Empty;
    }

    public void ApplyResponsiveSizing(float menuLayoutHeight, float buttonFontSize)
    {
        eyebrowLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.022f, 20f, 28f);
        titleLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.076f, 66f, 96f);
        messageLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.044f, 38f, 54f);
        calloutLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.040f, 34f, 48f);
        hintLabel.style.fontSize = Mathf.Clamp(menuLayoutHeight * 0.032f, 28f, 40f);
        primaryButton.style.fontSize = buttonFontSize * 1.15f;
        primaryButton.style.height = Mathf.Clamp(menuLayoutHeight * 0.090f, 78f, 98f);
    }
}
