using System;
using System.Collections.Generic;
using UnityEngine;

public enum AlphaTabVisualTheme
{
    BlackOnWhite = 0,
    WhiteOnDarkBlue = 1
}

public sealed class AlphaTabSheetThemePalette
{
    public Color regionBackdropColor;
    public Color sectionBackgroundColor;
    public Color sectionBorderColor;
    public Color statusTextColor;
    public Color statusBannerColor;
}

public static class AlphaTabVisualThemePalette
{
    public static readonly string[] Options =
    {
        "Black on White",
        "White on Dark Blue"
    };

    private static readonly Color DarkBlue = new Color32(0x07, 0x0B, 0x12, 0xFF);

    public static string Serialize(AlphaTabVisualTheme theme)
    {
        return theme == AlphaTabVisualTheme.WhiteOnDarkBlue ? Options[1] : Options[0];
    }

    public static AlphaTabVisualTheme Parse(string value)
    {
        if (string.Equals(value, Options[1], StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "WhiteOnDarkBlue", StringComparison.OrdinalIgnoreCase))
            return AlphaTabVisualTheme.WhiteOnDarkBlue;

        return AlphaTabVisualTheme.BlackOnWhite;
    }

    public static string GetRequestThemeId(AlphaTabVisualTheme theme)
    {
        return theme == AlphaTabVisualTheme.WhiteOnDarkBlue ? "white_on_dark_blue" : "black_on_white";
    }

    public static AlphaTabSheetThemePalette GetSheetPalette(AlphaTabVisualTheme theme, bool splitViewport)
    {
        if (theme == AlphaTabVisualTheme.WhiteOnDarkBlue)
        {
            return new AlphaTabSheetThemePalette
            {
                regionBackdropColor = splitViewport ? DarkBlue : new Color(0f, 0f, 0f, 0f),
                sectionBackgroundColor = DarkBlue,
                sectionBorderColor = new Color(0.92f, 0.95f, 0.99f, 0.95f),
                statusTextColor = Color.white,
                statusBannerColor = new Color(0.96f, 0.98f, 1f, 0.92f)
            };
        }

        return new AlphaTabSheetThemePalette
        {
            regionBackdropColor = splitViewport ? new Color(0.05f, 0.06f, 0.08f, 1f) : new Color(0f, 0f, 0f, 0f),
            sectionBackgroundColor = new Color(1f, 1f, 1f, 0.995f),
            sectionBorderColor = new Color(0.06f, 0.06f, 0.06f, 0.95f),
            statusTextColor = new Color(0.06f, 0.06f, 0.06f, 0.92f),
            statusBannerColor = new Color(0.62f, 0.78f, 0.97f, 0.90f)
        };
    }
}

[Serializable]
public sealed class AlphaTabRenderRequestData
{
    public string notationPath = string.Empty;
    public int trackIndex;
    public string themeId = "white_on_dark_blue";
    public int renderWidth = 1600;
    public float scale = 1f;
    public int barsPerRow = 2;
    public int barsPerSection = 2;
    public string outputDirectory = string.Empty;
}

[Serializable]
public sealed class AlphaTabRenderResponseData
{
    public bool success;
    public string error = string.Empty;
    public string manifestPath = string.Empty;
    public string notationPath = string.Empty;
    public int trackIndex;
    public string trackLabel = string.Empty;
}

[Serializable]
public sealed class AlphaTabRenderManifestData
{
    public const int CurrentVersion = 12;

    public int version = CurrentVersion;
    public string notationPath = string.Empty;
    public long notationLastWriteTicks;
    public int trackIndex;
    public string trackLabel = string.Empty;
    public string themeId = "white_on_dark_blue";
    public int renderWidth;
    public float scale;
    public int barsPerRow;
    public int barsPerSection;
    public float totalWidth;
    public float totalHeight;
    public List<AlphaTabRenderSectionData> sections = new List<AlphaTabRenderSectionData>();
}

[Serializable]
public sealed class AlphaTabRenderSectionData
{
    public int index;
    public int firstMasterBarIndex;
    public int lastMasterBarIndex;
    public string imagePath = string.Empty;
    public float width;
    public float height;
    public float startTime;
    public float endTime;
    public List<AlphaTabRenderBeatData> beats = new List<AlphaTabRenderBeatData>();
}

[Serializable]
public sealed class AlphaTabRenderBeatData
{
    public long beatId;
    public int masterBarIndex;
    public int sourceEventId = -1;
    public int voiceIndex;
    public float startTime;
    public float endTime;
    public float indicatorX01;
    public float indicatorEndX01;
    public float indicatorY01;
    public float indicatorHeight01;
    public float visualWidth01;
    public bool isRest;
    public bool continuesFromPrevious;
    public bool continuesToNext;
}
