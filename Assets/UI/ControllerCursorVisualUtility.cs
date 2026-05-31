using UnityEngine;
#if ENABLE_INPUT_SYSTEM
using UnityEngine.InputSystem;
#endif
using UnityEngine.UIElements;

public static class ControllerCursorVisualUtility
{
    private const float CursorScreenHeightDivisor = 54f;
    private const float InnerDiameterRatio = 0.30f;
    private const float BorderWidthRatio = 0.075f;
    private const float RightStickDeadZone = 0.22f;
    private const float RightStickScrollPixelsPerSecond = 1180f;

    public static void Apply(VisualElement cursor, VisualElement inner, VisualElement panelRoot)
    {
        if (cursor == null)
            return;

        float diameter = ResolveCursorDiameter(panelRoot);
        float borderWidth = Mathf.Clamp(diameter * BorderWidthRatio, 1.5f, 4f);
        float innerDiameter = Mathf.Clamp(diameter * InnerDiameterRatio, 6f, 14f);

        cursor.style.position = Position.Absolute;
        cursor.style.width = diameter;
        cursor.style.height = diameter;
        cursor.style.borderTopWidth = borderWidth;
        cursor.style.borderRightWidth = borderWidth;
        cursor.style.borderBottomWidth = borderWidth;
        cursor.style.borderLeftWidth = borderWidth;
        Color border = new Color(1f, 1f, 1f, 0.96f);
        cursor.style.borderTopColor = border;
        cursor.style.borderRightColor = border;
        cursor.style.borderBottomColor = border;
        cursor.style.borderLeftColor = border;
        cursor.style.borderTopLeftRadius = 999f;
        cursor.style.borderTopRightRadius = 999f;
        cursor.style.borderBottomLeftRadius = 999f;
        cursor.style.borderBottomRightRadius = 999f;
        cursor.style.backgroundColor = new Color(1f, 1f, 1f, 0.08f);
        cursor.style.translate = new Translate(diameter * -0.5f, diameter * -0.5f, 0f);
        cursor.pickingMode = PickingMode.Ignore;

        if (inner == null)
            return;

        inner.style.position = Position.Absolute;
        inner.style.left = new Length(50f, LengthUnit.Percent);
        inner.style.top = new Length(50f, LengthUnit.Percent);
        inner.style.width = innerDiameter;
        inner.style.height = innerDiameter;
        inner.style.translate = new Translate(innerDiameter * -0.5f, innerDiameter * -0.5f, 0f);
        inner.style.borderTopLeftRadius = 999f;
        inner.style.borderTopRightRadius = 999f;
        inner.style.borderBottomLeftRadius = 999f;
        inner.style.borderBottomRightRadius = 999f;
        inner.style.backgroundColor = new Color(1f, 1f, 1f, 0.98f);
        inner.pickingMode = PickingMode.Ignore;
    }

    public static Vector2 ReadRightStickAxis()
    {
#if ENABLE_INPUT_SYSTEM
        Vector2 strongest = Vector2.zero;
        foreach (Gamepad gamepad in Gamepad.all)
        {
            if (gamepad == null)
                continue;

            Vector2 candidate = gamepad.rightStick.ReadValue();
            if (candidate.sqrMagnitude > strongest.sqrMagnitude)
                strongest = candidate;
        }

        if (strongest.magnitude < RightStickDeadZone)
            return Vector2.zero;

        return Vector2.ClampMagnitude(strongest, 1f);
#else
        return Vector2.zero;
#endif
    }

    public static bool TryScrollHoveredScrollView(VisualElement pickedTarget, float deltaTime, out ScrollView scrolledView)
    {
        scrolledView = null;
        if (pickedTarget == null || deltaTime <= 0f)
            return false;

        Vector2 axis = ReadRightStickAxis();
        if (Mathf.Abs(axis.y) < RightStickDeadZone)
            return false;

        scrolledView = FindScrollViewAncestor(pickedTarget);
        if (scrolledView == null)
            return false;

        float deltaY = -axis.y * RightStickScrollPixelsPerSecond * deltaTime;
        return TryScroll(scrolledView, deltaY);
    }

    public static bool TryScroll(ScrollView scrollView, float deltaY)
    {
        if (scrollView == null || Mathf.Abs(deltaY) < 0.001f)
            return false;

        float viewportHeight = ResolveElementHeight(scrollView.contentViewport);
        if (viewportHeight < 1f)
            viewportHeight = ResolveElementHeight(scrollView);

        float contentHeight = ResolveElementHeight(scrollView.contentContainer);
        if (viewportHeight < 1f || contentHeight < viewportHeight + 0.5f)
            return false;

        float maxOffset = Mathf.Max(0f, contentHeight - viewportHeight);
        Vector2 current = scrollView.scrollOffset;
        float nextY = Mathf.Clamp(current.y + deltaY, 0f, maxOffset);
        if (Mathf.Abs(nextY - current.y) < 0.01f)
            return false;

        scrollView.scrollOffset = new Vector2(current.x, nextY);
        return true;
    }

    private static float ResolveCursorDiameter(VisualElement panelRoot)
    {
        float panelHeight = panelRoot != null ? panelRoot.resolvedStyle.height : float.NaN;
        if (!IsFinite(panelHeight) || panelHeight < 8f)
            panelHeight = Screen.height;
        if (!IsFinite(panelHeight) || panelHeight < 8f)
            panelHeight = 1080f;

        return Mathf.Clamp(panelHeight / CursorScreenHeightDivisor, 18f, 44f);
    }

    private static ScrollView FindScrollViewAncestor(VisualElement target)
    {
        for (VisualElement current = target; current != null; current = current.parent)
        {
            if (current is ScrollView scrollView)
                return scrollView;
        }

        return null;
    }

    private static float ResolveElementHeight(VisualElement element)
    {
        if (element == null)
            return 0f;

        float height = element.layout.height;
        if (!IsFinite(height) || height < 1f)
            height = element.resolvedStyle.height;

        return IsFinite(height) ? Mathf.Max(0f, height) : 0f;
    }

    private static bool IsFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }
}
