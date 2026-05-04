using UnityEngine;
using UnityEngine.UIElements;

public sealed class ReusableLoadingOverlay
{
    private const float PulseMinOpacity = 0.58f;
    private const float PulseMaxOpacity = 1.0f;
    private const float PulseSpeed = 1.9f;

    private readonly VisualElement overlay;
    private readonly VisualElement animatedContent;
    private readonly VisualElement contentHost;

    private ReusableLoadingOverlay(VisualElement overlay, VisualElement animatedContent, VisualElement contentHost)
    {
        this.overlay = overlay;
        this.animatedContent = animatedContent;
        this.contentHost = contentHost;
    }

    public VisualElement RootElement => overlay;
    public VisualElement ContentHost => contentHost;

    public static ReusableLoadingOverlay CreateStringTheoryLibraryLoadingOverlay(VisualElement parent)
    {
        if (parent == null)
            throw new System.ArgumentNullException(nameof(parent));

        VisualElement overlay = new VisualElement();
        overlay.style.position = Position.Absolute;
        overlay.style.left = 0f;
        overlay.style.right = 0f;
        overlay.style.top = 0f;
        overlay.style.bottom = 0f;
        overlay.style.alignItems = Align.Center;
        overlay.style.justifyContent = Justify.Center;
        overlay.style.paddingLeft = 48f;
        overlay.style.paddingRight = 48f;
        overlay.style.paddingTop = 48f;
        overlay.style.paddingBottom = 48f;
        overlay.style.backgroundColor = new Color(0.01f, 0.02f, 0.05f, 0.44f);
        overlay.style.display = DisplayStyle.None;

        VisualElement shell = new VisualElement();
        shell.style.alignItems = Align.Center;
        shell.style.justifyContent = Justify.Center;
        shell.style.flexDirection = FlexDirection.Column;
        shell.style.opacity = 1f;
        shell.pickingMode = PickingMode.Ignore;

        VisualElement contentHost = new VisualElement();
        contentHost.style.alignItems = Align.Center;
        contentHost.style.justifyContent = Justify.Center;
        contentHost.pickingMode = PickingMode.Ignore;

        shell.Add(contentHost);
        overlay.Add(shell);
        parent.Add(overlay);

        return new ReusableLoadingOverlay(overlay, shell, contentHost);
    }

    public void SetVisible(bool visible, float unscaledTime)
    {
        overlay.style.display = visible ? DisplayStyle.Flex : DisplayStyle.None;
        if (!visible)
        {
            animatedContent.style.opacity = 1f;
            return;
        }

        float wave = 0.5f + 0.5f * Mathf.Sin(unscaledTime * PulseSpeed * Mathf.PI * 2f);
        animatedContent.style.opacity = Mathf.Lerp(PulseMinOpacity, PulseMaxOpacity, wave);
    }
}
