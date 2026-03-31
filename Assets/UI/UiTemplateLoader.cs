using System;
using UnityEngine;
using UnityEngine.UIElements;

internal static class UiTemplateLoader
{
    public static TemplateContainer CloneRequired(string resourcePath, string styleSheetResourcePath = null)
    {
        VisualTreeAsset asset = Resources.Load<VisualTreeAsset>(resourcePath);
        if (asset == null)
            throw new InvalidOperationException($"Missing UI template at Resources/{resourcePath}.uxml");

        TemplateContainer tree = asset.CloneTree();

        if (!string.IsNullOrWhiteSpace(styleSheetResourcePath))
        {
            StyleSheet styleSheet = Resources.Load<StyleSheet>(styleSheetResourcePath);
            if (styleSheet == null)
                throw new InvalidOperationException($"Missing UI stylesheet at Resources/{styleSheetResourcePath}.uss");

            tree.styleSheets.Add(styleSheet);
        }

        return tree;
    }

    public static T QRequired<T>(this VisualElement root, string name) where T : VisualElement
    {
        T element = root.Q<T>(name);
        if (element == null)
            throw new InvalidOperationException($"Missing UI element '{name}' in template '{root.name}'.");

        return element;
    }
}
