using System;
using System.Collections.Generic;
using System.IO;

public static class SongNotationFacade
{
    public static bool TryDetectKind(string filePath, out SongNotationSourceKind kind)
    {
        kind = SongNotationSourceKind.None;
        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
            return false;

        if (RocksmithCachedSongLoader.IsRocksmithManifestPath(filePath))
        {
            kind = SongNotationSourceKind.Rocksmith;
            return true;
        }

        string extension = Path.GetExtension(filePath)?.ToLowerInvariant() ?? string.Empty;
        if (extension == ".musicxml" || extension == ".xml")
        {
            kind = SongNotationSourceKind.MusicXml;
            return true;
        }

        if (extension == ".gp5" ||
            extension == ".gp4" ||
            extension == ".gp3" ||
            extension == ".gpx" ||
            extension == ".gp")
        {
            kind = SongNotationSourceKind.Gp5;
            return true;
        }

        return false;
    }

    public static List<MusicXmlLoader.MusicXmlPartSummary> GetPartSummaries(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.GetPartSummaries(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.GetPartSummaries(filePath);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlLoader.GetPartSummaries(filePath);
            default:
                return new List<MusicXmlLoader.MusicXmlPartSummary>();
        }
    }

    public static List<NoteData> LoadSong(string filePath, SongNotationSourceKind kind, int targetPartIndex = -1)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.LoadSong(filePath, targetPartIndex);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.LoadSong(filePath, targetPartIndex);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlLoader.LoadMusicXmlSong(filePath, targetPartIndex);
            default:
                return null;
        }
    }

    public static List<ArpeggioGuideData> LoadArpeggioGuides(string filePath, SongNotationSourceKind kind, int targetPartIndex = -1)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.LoadArpeggioGuides(filePath, targetPartIndex);
            default:
                return new List<ArpeggioGuideData>();
        }
    }

    public static GeneratedPlaybackArrangement LoadGeneratedArrangement(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.LoadGeneratedArrangement(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpBandPlaybackLoader.LoadArrangement(filePath);
            case SongNotationSourceKind.MusicXml:
                return MusicXmlBandPlaybackLoader.LoadArrangement(filePath);
            default:
                return null;
        }
    }

    public static string TryReadDisplayName(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.TryReadDisplayName(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.TryReadTitle(filePath);
            case SongNotationSourceKind.MusicXml:
                return SongLibraryService.TryReadDisplayNameFromXml(filePath);
            default:
                return null;
        }
    }

    public static string TryReadCreator(string filePath, SongNotationSourceKind kind)
    {
        switch (kind)
        {
            case SongNotationSourceKind.Rocksmith:
                return RocksmithCachedSongLoader.TryReadArtist(filePath);
            case SongNotationSourceKind.Gp5:
                return AlphaTabGpLoader.TryReadArtist(filePath);
            case SongNotationSourceKind.MusicXml:
                return SongLibraryService.TryReadCreatorFromXml(filePath);
            default:
                return null;
        }
    }
}
