using UnityEngine;

public struct HighwayCharacterTextureData
{
    public Texture2D texture;
    public float aspect;
    public Vector2 textureScale;
    public Vector2 textureOffset;
    public int sourcePixelWidth;
    public int sourcePixelHeight;
}

public static class HighwayCharacterVisualUtility
{
    private const float DefaultHudAspect = 0.79f;
    private const float VerticalFadeCompensationStrength = 1f;
    private const float VerticalPortalCompensationStrength = 0f;
    private static readonly Color PortalEdgeOrangeColor = new Color(0.98f, 0.43f, 0.14f, 1f);
    private static readonly Color PortalEdgeBlackColor = new Color(0f, 0f, 0f, 1f);
    private static readonly Color PortalSwirlOrangeColor = new Color(0.94f, 0.57f, 0.24f, 1f);
    private static readonly Color PortalSwirlBlackColor = new Color(0f, 0f, 0f, 1f);

    private static readonly string[] ResourceCandidates =
    {
        "Hero"
    };

    private static readonly string[] CharacterResourceNames =
    {
        "Hero",
        "Elize_Color_2"
    };

    private static readonly string[] CharacterDisplayNames =
    {
        "Skullhead",
        "Volt"
    };

    private static float currentHudAspect = DefaultHudAspect;
    private static int currentHudSourcePixelWidth = 1;
    private static int currentHudSourcePixelHeight = 1;
    private static float currentHudScale = 1f;
    private static float currentHudOffsetX = 0f;
    private static float currentHudOffsetY = 0f;

    public static int CharacterCount => CharacterResourceNames.Length;

    public static string GetResourceName(HighwayCharacterChoice choice)
    {
        int index = Mathf.Clamp((int)choice, 0, CharacterResourceNames.Length - 1);
        return CharacterResourceNames[index];
    }

    public static string GetDisplayName(HighwayCharacterChoice choice)
    {
        int index = Mathf.Clamp((int)choice, 0, CharacterDisplayNames.Length - 1);
        return CharacterDisplayNames[index];
    }

    public static HighwayCharacterChoice GetFixedMultiplayerChoice(int playerIndex)
    {
        return playerIndex == 1 ? HighwayCharacterChoice.ElizeColor2 : HighwayCharacterChoice.Hero;
    }

    public static bool TryLoadTextureData(out HighwayCharacterTextureData data)
    {
        for (int i = 0; i < ResourceCandidates.Length; i++)
        {
            Sprite sprite = Resources.Load<Sprite>(ResourceCandidates[i]);
            if (sprite != null)
            {
                data = BuildFromSprite(sprite);
                return true;
            }
        }

        for (int i = 0; i < ResourceCandidates.Length; i++)
        {
            Texture2D texture = Resources.Load<Texture2D>(ResourceCandidates[i]);
            if (texture != null)
            {
                data = BuildFromTexture(texture);
                return true;
            }
        }

        data = default;
        return false;
    }

    public static bool TryLoadTextureData(HighwayCharacterChoice choice, out HighwayCharacterTextureData data)
    {
        string resourceName = GetResourceName(choice);
        Sprite sprite = Resources.Load<Sprite>(resourceName);
        if (sprite != null)
        {
            data = BuildFromSprite(sprite);
            return true;
        }

        Texture2D texture = Resources.Load<Texture2D>(resourceName);
        if (texture != null)
        {
            data = BuildFromTexture(texture);
            return true;
        }

        data = default;
        return false;
    }

    public static Rect ComputeViewportRect(
        float screenWidth,
        float screenHeight,
        float characterAspect,
        int sourcePixelWidth,
        int sourcePixelHeight,
        float marginX,
        float marginY,
        float heightFraction,
        float centerY,
        float scale,
        float offsetX,
        float offsetY,
        bool allowHorizontalBleed = false)
    {
        float safeScreenWidth = Mathf.Max(1f, screenWidth);
        float safeScreenHeight = Mathf.Max(1f, screenHeight);
        float screenAspect = safeScreenWidth / safeScreenHeight;

        float viewportHeight = Mathf.Min(heightFraction * Mathf.Max(0.1f, scale), 1f - (2f * marginY));
        float viewportWidth = viewportHeight * characterAspect / Mathf.Max(0.1f, screenAspect);
        float maxViewportWidth = Mathf.Max(0.05f, 1f - (2f * marginX));
        if (viewportWidth > maxViewportWidth)
        {
            float shrink = maxViewportWidth / viewportWidth;
            viewportWidth = maxViewportWidth;
            viewportHeight *= shrink;
        }

        float left = marginX + offsetX;
        float horizontalBleed = allowHorizontalBleed ? viewportWidth * 0.90f : 0f;
        float minLeft = marginX - horizontalBleed;
        float maxLeft = 1f - marginX - viewportWidth + horizontalBleed;
        left = Mathf.Clamp(left, minLeft, maxLeft);
        float clampedCenterY = Mathf.Clamp(
            centerY + offsetY,
            marginY + (viewportHeight * 0.5f),
            1f - marginY - (viewportHeight * 0.5f));
        float bottom = clampedCenterY - (viewportHeight * 0.5f);
        return new Rect(left, bottom, viewportWidth, viewportHeight);
    }

    public static void SetCurrentHudLayout(
        float characterAspect,
        int sourcePixelWidth,
        int sourcePixelHeight,
        float scale,
        float offsetX,
        float offsetY)
    {
        currentHudAspect = Mathf.Max(0.05f, characterAspect);
        currentHudSourcePixelWidth = Mathf.Max(1, sourcePixelWidth);
        currentHudSourcePixelHeight = Mathf.Max(1, sourcePixelHeight);
        currentHudScale = Mathf.Max(0.1f, scale);
        currentHudOffsetX = offsetX;
        currentHudOffsetY = offsetY;
    }

    public static Rect GetCurrentHudScreenRect(
        float screenWidth,
        float screenHeight,
        float marginX,
        float marginY,
        float heightFraction,
        float centerY)
    {
        return ComputeViewportRect(
            screenWidth,
            screenHeight,
            currentHudAspect,
            currentHudSourcePixelWidth,
            currentHudSourcePixelHeight,
            marginX,
            marginY,
            heightFraction,
            centerY,
            currentHudScale,
            currentHudOffsetX,
            currentHudOffsetY);
    }

    public static void ComputeVerticalCompensation(
        float localCharacterYOffset,
        float baseFadeStart,
        float baseFadeEnd,
        float fadeSoftness,
        float basePortalLocalY,
        out float fadeStart,
        out float fadeEnd,
        out float portalLocalY)
    {
        float clampedOffset = Mathf.Clamp(localCharacterYOffset, -0.85f, 0.85f);
        float fadeShift = -clampedOffset * VerticalFadeCompensationStrength;
        float fadeCenter = (((baseFadeStart + baseFadeEnd) * 0.5f) + fadeShift);
        float fadeWidth = Mathf.Clamp(fadeSoftness, 0.02f, 0.90f);
        float halfFadeWidth = fadeWidth * 0.5f;
        fadeStart = Mathf.Clamp(fadeCenter + halfFadeWidth, 0.02f, 0.98f);
        fadeEnd = Mathf.Clamp(fadeCenter - halfFadeWidth, 0f, fadeStart - 0.01f);
        portalLocalY = basePortalLocalY - (clampedOffset * VerticalPortalCompensationStrength);
    }

    public static void ApplyRuntimeTextureSettings(Texture2D texture)
    {
        if (texture == null)
            return;

        texture.filterMode = FilterMode.Point;
        texture.wrapMode = TextureWrapMode.Clamp;
        texture.anisoLevel = 0;
        texture.mipMapBias = 0f;
    }

    public static Color ResolvePortalEdgeColor(GuitarBridgeServer.HighwayCharacterPortalColorPreset preset)
    {
        switch (preset)
        {
            case GuitarBridgeServer.HighwayCharacterPortalColorPreset.Black:
                return PortalEdgeBlackColor;
            default:
                return PortalEdgeOrangeColor;
        }
    }

    public static Color ResolvePortalSwirlColor(GuitarBridgeServer.HighwayCharacterPortalColorPreset preset)
    {
        switch (preset)
        {
            case GuitarBridgeServer.HighwayCharacterPortalColorPreset.Black:
                return PortalSwirlBlackColor;
            default:
                return PortalSwirlOrangeColor;
        }
    }

    private static HighwayCharacterTextureData BuildFromSprite(Sprite sprite)
    {
        Texture2D texture = sprite.texture;
        ApplyRuntimeTextureSettings(texture);

        Rect spriteRect = sprite.textureRect;
        return new HighwayCharacterTextureData
        {
            texture = texture,
            aspect = Mathf.Max(0.05f, spriteRect.width / Mathf.Max(1f, spriteRect.height)),
            textureScale = new Vector2(
                spriteRect.width / Mathf.Max(1f, texture.width),
                spriteRect.height / Mathf.Max(1f, texture.height)),
            textureOffset = new Vector2(
                spriteRect.x / Mathf.Max(1f, texture.width),
                spriteRect.y / Mathf.Max(1f, texture.height)),
            sourcePixelWidth = Mathf.Max(1, Mathf.RoundToInt(spriteRect.width)),
            sourcePixelHeight = Mathf.Max(1, Mathf.RoundToInt(spriteRect.height))
        };
    }

    private static HighwayCharacterTextureData BuildFromTexture(Texture2D texture)
    {
        ApplyRuntimeTextureSettings(texture);

        return new HighwayCharacterTextureData
        {
            texture = texture,
            aspect = Mathf.Max(0.05f, texture.width / (float)Mathf.Max(1, texture.height)),
            textureScale = Vector2.one,
            textureOffset = Vector2.zero,
            sourcePixelWidth = Mathf.Max(1, texture.width),
            sourcePixelHeight = Mathf.Max(1, texture.height)
        };
    }

    private static void QuantizeViewportSize(
        float screenWidth,
        float screenHeight,
        int sourcePixelWidth,
        int sourcePixelHeight,
        ref float viewportWidth,
        ref float viewportHeight)
    {
        if (sourcePixelWidth <= 0 || sourcePixelHeight <= 0)
            return;

        float desiredPixelWidth = viewportWidth * screenWidth;
        float desiredPixelHeight = viewportHeight * screenHeight;
        int maxScaleX = Mathf.FloorToInt(desiredPixelWidth / sourcePixelWidth);
        int maxScaleY = Mathf.FloorToInt(desiredPixelHeight / sourcePixelHeight);
        int maxScale = Mathf.Min(maxScaleX, maxScaleY);
        if (maxScale < 1)
            return;

        int quantizedScale = maxScale;
        if (quantizedScale < 1)
            return;

        viewportWidth = (sourcePixelWidth * quantizedScale) / screenWidth;
        viewportHeight = (sourcePixelHeight * quantizedScale) / screenHeight;
    }

    public static float ComputeCharacterLocalYOffset(float viewportHeight, float offsetY, float rootScaleMultiplier)
    {
        float safeViewportHeight = Mathf.Max(0.0001f, viewportHeight);
        float safeRootScaleMultiplier = Mathf.Max(0.0001f, rootScaleMultiplier);
        return offsetY / (safeViewportHeight * safeRootScaleMultiplier);
    }

    public static float ComputeCharacterLocalXOffset(float viewportWidth, float offsetX, float rootScaleMultiplier)
    {
        float safeViewportWidth = Mathf.Max(0.0001f, viewportWidth);
        float safeRootScaleMultiplier = Mathf.Max(0.0001f, rootScaleMultiplier);
        return offsetX / (safeViewportWidth * safeRootScaleMultiplier);
    }
}
