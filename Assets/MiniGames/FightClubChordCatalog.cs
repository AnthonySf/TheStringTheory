using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;

internal static class FightClubChordCatalog
{
    public static readonly FightClubChordGroupDefinition[] Groups =
    {
        new FightClubChordGroupDefinition(
            "open-major",
            "Open Major",
            "The main campfire major shapes.",
            new[]
            {
                Chord("C", new[] { -1, 3, 2, 0, 1, 0 }, new[] { -1, 3, 2, 0, 1, 0 }, 1),
                Chord("A", new[] { -1, 0, 2, 2, 2, 0 }, new[] { -1, 0, 1, 2, 3, 0 }, 1),
                Chord("G", new[] { 3, 2, 0, 0, 0, 3 }, new[] { 2, 1, 0, 0, 0, 3 }, 1),
                Chord("E", new[] { 0, 2, 2, 1, 0, 0 }, new[] { 0, 2, 3, 1, 0, 0 }, 1),
                Chord("D", new[] { -1, -1, 0, 2, 3, 2 }, new[] { -1, -1, 0, 1, 3, 2 }, 1),
                Chord("Fmaj7", new[] { -1, -1, 3, 2, 1, 0 }, new[] { -1, -1, 3, 2, 1, 0 }, 2),
                Chord("F", new[] { -1, -1, 3, 2, 1, 1 }, new[] { -1, -1, 3, 2, 1, 1 }, 2, Barre(1, 4, 5, 1))
            }),
        new FightClubChordGroupDefinition(
            "open-minor",
            "Open Minor",
            "Common minor shapes for quick changes.",
            new[]
            {
                Chord("Am", new[] { -1, 0, 2, 2, 1, 0 }, new[] { -1, 0, 2, 3, 1, 0 }, 1),
                Chord("Em", new[] { 0, 2, 2, 0, 0, 0 }, new[] { 0, 2, 3, 0, 0, 0 }, 1),
                Chord("Dm", new[] { -1, -1, 0, 2, 3, 1 }, new[] { -1, -1, 0, 2, 3, 1 }, 2)
            }),
        new FightClubChordGroupDefinition(
            "pop-acoustic",
            "Pop Acoustic Shapes",
            "Modern acoustic shapes that connect smoothly around G, Cadd9, D, and Em.",
            new[]
            {
                Chord("G 4-finger", new[] { 3, 2, 0, 0, 3, 3 }, new[] { 2, 1, 0, 0, 3, 4 }, 1),
                Chord("Em7", new[] { 0, 2, 2, 0, 3, 3 }, new[] { 0, 1, 2, 0, 3, 4 }, 1),
                Chord("Cadd9", new[] { -1, 3, 2, 0, 3, 3 }, new[] { -1, 3, 2, 0, 4, 4 }, 1),
                Chord("Cadd9/G", new[] { 3, 3, 2, 0, 3, 3 }, new[] { 2, 3, 1, 0, 4, 4 }, 2),
                Chord("Dsus4", new[] { -1, -1, 0, 2, 3, 3 }, new[] { -1, -1, 0, 1, 2, 3 }, 1)
            }),
        new FightClubChordGroupDefinition(
            "open-dominant-sevenths",
            "Open Dominant Sevenths",
            "Common dominant seventh shapes for blues, rock, and pop.",
            new[]
            {
                Chord("A7", new[] { -1, 0, 2, 0, 2, 0 }, new[] { -1, 0, 1, 0, 2, 0 }, 2),
                Chord("B7", new[] { -1, 2, 1, 2, 0, 2 }, new[] { -1, 2, 1, 3, 0, 4 }, 3),
                Chord("C7", new[] { -1, 3, 2, 3, 1, 0 }, new[] { -1, 3, 2, 4, 1, 0 }, 3),
                Chord("D7", new[] { -1, -1, 0, 2, 1, 2 }, new[] { -1, -1, 0, 2, 1, 3 }, 2),
                Chord("E7", new[] { 0, 2, 0, 1, 0, 0 }, new[] { 0, 2, 0, 1, 0, 0 }, 2),
                Chord("G7", new[] { 3, 2, 0, 0, 0, 1 }, new[] { 3, 2, 0, 0, 0, 1 }, 2),
                Chord("A7sus4", new[] { -1, 0, 2, 0, 3, 0 }, new[] { -1, 0, 1, 0, 3, 0 }, 2),
                Chord("D7sus4", new[] { -1, -1, 0, 2, 1, 3 }, new[] { -1, -1, 0, 2, 1, 3 }, 2),
                Chord("E7sus4", new[] { 0, 2, 0, 2, 0, 0 }, new[] { 0, 2, 0, 3, 0, 0 }, 2)
            }),
        new FightClubChordGroupDefinition(
            "major-sevenths",
            "Major Sevenths",
            "Smooth major seventh sounds for pop, soul, and ballads.",
            new[]
            {
                Chord("Amaj7", new[] { -1, 0, 2, 1, 2, 0 }, new[] { -1, 0, 2, 1, 3, 0 }, 3),
                Chord("Cmaj7", new[] { -1, 3, 2, 0, 0, 0 }, new[] { -1, 3, 2, 0, 0, 0 }, 2),
                Chord("Dmaj7", new[] { -1, -1, 0, 2, 2, 2 }, new[] { -1, -1, 0, 1, 1, 1 }, 3, Barre(2, 3, 5, 1)),
                Chord("Emaj7", new[] { 0, 2, 1, 1, 0, 0 }, new[] { 0, 3, 1, 2, 0, 0 }, 3),
                Chord("Gmaj7", new[] { 3, 2, 0, 0, 0, 2 }, new[] { 3, 2, 0, 0, 0, 1 }, 3)
            }),
        new FightClubChordGroupDefinition(
            "minor-sevenths",
            "Open Minor Sevenths",
            "Common minor seventh sounds that stay near the nut.",
            new[]
            {
                Chord("Am7", new[] { -1, 0, 2, 0, 1, 0 }, new[] { -1, 0, 2, 0, 1, 0 }, 2),
                Chord("Dm7", new[] { -1, -1, 0, 2, 1, 1 }, new[] { -1, -1, 0, 2, 1, 1 }, 3, Barre(1, 4, 5, 1)),
                Chord("Em7 open", new[] { 0, 2, 2, 0, 3, 0 }, new[] { 0, 1, 2, 0, 3, 0 }, 2)
            }),
        new FightClubChordGroupDefinition(
            "movable-sevenths",
            "Movable Sevenths",
            "Barre seventh shapes for more advanced changes.",
            new[]
            {
                Chord("Bm7", new[] { -1, 2, 4, 2, 3, 2 }, new[] { -1, 1, 3, 1, 2, 1 }, 4, Barre(2, 1, 5, 1)),
                Chord("C#m7", new[] { -1, 4, 6, 4, 5, 4 }, new[] { -1, 1, 3, 1, 2, 1 }, 4, Barre(4, 1, 5, 1)),
                Chord("F#m7", new[] { 2, 4, 2, 2, 2, 2 }, new[] { 1, 3, 1, 1, 1, 1 }, 4, Barre(2, 0, 5, 1))
            }),
        new FightClubChordGroupDefinition(
            "sus-add-slash",
            "Sus, Add, Slash",
            "Small changes that fit common progressions.",
            new[]
            {
                Chord("Asus2", new[] { -1, 0, 2, 2, 0, 0 }, new[] { -1, 0, 1, 2, 0, 0 }, 2),
                Chord("Asus4", new[] { -1, 0, 2, 2, 3, 0 }, new[] { -1, 0, 1, 2, 3, 0 }, 2),
                Chord("Dsus2", new[] { -1, -1, 0, 2, 3, 0 }, new[] { -1, -1, 0, 1, 2, 0 }, 2),
                Chord("Esus4", new[] { 0, 2, 2, 2, 0, 0 }, new[] { 0, 1, 2, 3, 0, 0 }, 2),
                Chord("D/F#", new[] { 2, -1, 0, 2, 3, 2 }, new[] { 1, -1, 0, 2, 4, 3 }, 3),
                Chord("G/B", new[] { -1, 2, 0, 0, 0, 3 }, new[] { -1, 1, 0, 0, 0, 3 }, 2),
                Chord("C/G", new[] { 3, 3, 2, 0, 1, 0 }, new[] { 4, 3, 2, 0, 1, 0 }, 3)
            }),
        new FightClubChordGroupDefinition(
            "power-chords",
            "Power Chords",
            "Two and three string rock shapes.",
            new[]
            {
                Chord("E5", new[] { 0, 2, 2, -1, -1, -1 }, new[] { 0, 1, 2, -1, -1, -1 }, 1),
                EPower("F5", 1, 2),
                EPower("G5", 3, 1),
                EPower("A5", 5, 1),
                EPower("B5", 7, 2),
                APower("C5", 3, 2),
                APower("D5", 5, 2),
                APower("Eb5", 6, 3)
            }),
        new FightClubChordGroupDefinition(
            "e-shape-barres",
            "E-Shape Barres",
            "Full barre chords rooted on the low E string.",
            new[]
            {
                EMajor("F barre", 1, 3),
                EMajor("F#", 2, 3),
                EMajor("G barre", 3, 3),
                EMajor("Ab", 4, 4),
                EMajor("A barre", 5, 4),
                EMajor("Bb", 6, 4),
                EMajor("B barre", 7, 4),
                EMinor("Fm", 1, 3),
                EMinor("F#m", 2, 3),
                EMinor("Gm", 3, 3),
                EMinor("Abm", 4, 4),
                EMinor("Am barre", 5, 4),
                EMinor("Bbm", 6, 4),
                EMinor("Bm E-shape", 7, 4)
            }),
        new FightClubChordGroupDefinition(
            "a-shape-barres",
            "A-Shape Barres",
            "Barre chords rooted on the A string.",
            new[]
            {
                AMajor("Bb A-shape", 1, 4),
                AMajor("B", 2, 4),
                AMajor("C barre", 3, 4),
                AMajor("C#", 4, 4),
                AMajor("D barre", 5, 4),
                AMajor("Eb", 6, 4),
                AMinor("Bm", 2, 3),
                AMinor("Cm", 3, 4),
                AMinor("C#m", 4, 4),
                AMinor("Dm barre", 5, 4),
                AMinor("Ebm", 6, 4),
                AMinor("Em barre", 7, 4)
            }),
        new FightClubChordGroupDefinition(
            "sixths",
            "Sixth Chords",
            "Warm major and minor sixth sounds for manual practice.",
            new[]
            {
                Chord("C6", new[] { -1, 3, 2, 2, 1, 0 }, new[] { -1, 3, 2, 2, 1, 0 }, 4),
                Chord("A6", new[] { -1, 0, 2, 2, 2, 2 }, new[] { -1, 0, 1, 1, 1, 1 }, 4, Barre(2, 2, 5, 1)),
                Chord("E6", new[] { 0, 2, 2, 1, 2, 0 }, new[] { 0, 2, 3, 1, 4, 0 }, 4),
                Chord("Am6", new[] { -1, 0, 2, 2, 1, 2 }, new[] { -1, 0, 2, 3, 1, 4 }, 5),
                Chord("Dm6", new[] { -1, -1, 0, 2, 0, 1 }, new[] { -1, -1, 0, 2, 0, 1 }, 5)
            }),
        new FightClubChordGroupDefinition(
            "ninths",
            "Ninth Chords",
            "Blues, funk, and soul color chords for manual practice.",
            new[]
            {
                Chord("E9", new[] { 0, 2, 0, 1, 0, 2 }, new[] { 0, 2, 0, 1, 0, 3 }, 5),
                Chord("A9", new[] { -1, 0, 2, 4, 2, 3 }, new[] { -1, 0, 1, 3, 1, 2 }, 5),
                Chord("B9", new[] { -1, 2, 1, 2, 2, 2 }, new[] { -1, 2, 1, 3, 3, 3 }, 5, Barre(2, 3, 5, 3)),
                Chord("D9", new[] { -1, 5, 4, 5, 5, 5 }, new[] { -1, 2, 1, 3, 3, 3 }, 5, Barre(5, 3, 5, 3))
            }),
        new FightClubChordGroupDefinition(
            "dim-aug",
            "Diminished / Augmented",
            "Tense passing chords and color shapes for manual practice.",
            new[]
            {
                Chord("Bdim", new[] { -1, 2, 3, 1, 3, -1 }, new[] { -1, 2, 3, 1, 4, -1 }, 5),
                Chord("Bdim7", new[] { -1, 2, 3, 1, 3, 1 }, new[] { -1, 2, 3, 1, 4, 1 }, 5, Barre(1, 3, 5, 1)),
                Chord("Caug", new[] { -1, 3, 2, 1, 1, 0 }, new[] { -1, 4, 3, 1, 2, 0 }, 5),
                Chord("Eaug", new[] { 0, 3, 2, 1, 1, 0 }, new[] { 0, 4, 3, 1, 2, 0 }, 5)
            })
    };

    public static readonly FightClubChordDefinition[] Chords = Groups
        .SelectMany(group => group.Chords)
        .GroupBy(chord => chord.Id, StringComparer.OrdinalIgnoreCase)
        .Select(group => group.First())
        .ToArray();

    private static readonly Dictionary<string, FightClubChordDefinition> ChordsById = Chords
        .ToDictionary(chord => chord.Id, StringComparer.OrdinalIgnoreCase);

    private static readonly Dictionary<string, FightClubChordDefinition> ChordsByNormalizedName = Chords
        .GroupBy(chord => NormalizeChordName(chord.Name), StringComparer.OrdinalIgnoreCase)
        .ToDictionary(group => group.Key, group => group.OrderBy(chord => chord.Difficulty).First(), StringComparer.OrdinalIgnoreCase);

    public static readonly FightClubLevelDefinition[] Levels = BuildLevels();

    public static FightClubChordDefinition FindById(string id)
    {
        if (string.IsNullOrWhiteSpace(id))
            return null;

        return ChordsById.TryGetValue(id.Trim(), out FightClubChordDefinition chord) ? chord : null;
    }

    public static FightClubChordDefinition FindByName(string name)
    {
        string normalized = NormalizeChordName(name);
        if (string.IsNullOrWhiteSpace(normalized))
            return null;

        if (ChordsByNormalizedName.TryGetValue(normalized, out FightClubChordDefinition exact))
            return exact;

        int slashIndex = normalized.IndexOf("/", StringComparison.Ordinal);
        if (slashIndex > 0 &&
            ChordsByNormalizedName.TryGetValue(normalized.Substring(0, slashIndex), out FightClubChordDefinition withoutBass))
        {
            return withoutBass;
        }

        string enharmonic = NormalizeEnharmonicRoot(normalized);
        if (!string.Equals(enharmonic, normalized, StringComparison.OrdinalIgnoreCase) &&
            ChordsByNormalizedName.TryGetValue(enharmonic, out FightClubChordDefinition enharmonicMatch))
        {
            return enharmonicMatch;
        }

        normalized = normalized.Replace("MAJOR", string.Empty).Replace("MINOR", "M");
        return ChordsByNormalizedName.TryGetValue(normalized, out FightClubChordDefinition fallback) ? fallback : null;
    }

    public static FightClubChordDefinition[] ResolveIds(IEnumerable<string> ids)
    {
        if (ids == null)
            return Array.Empty<FightClubChordDefinition>();

        var result = new List<FightClubChordDefinition>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (string id in ids)
        {
            FightClubChordDefinition chord = FindById(id);
            if (chord != null && seen.Add(chord.Id))
                result.Add(chord);
        }

        return result.ToArray();
    }

    private static FightClubLevelDefinition[] BuildLevels()
    {
        return new[]
        {
            Level(
                "level-1",
                "Level 1",
                "Open Chord Basics",
                "The core open shapes used in countless songs.",
                10000,
                "C", "A", "G", "E", "D", "Am", "Em"),
            Level(
                "level-2",
                "Level 2",
                "First Color",
                "Adds D minor and friendly F shapes without full barres.",
                12000,
                "C", "A", "G", "E", "D", "Am", "Em", "Dm", "Fmaj7"),
            Level(
                "level-3",
                "Level 3",
                "Pop Changes",
                "Introduces the modern G, Cadd9, Em7, and Dsus4 family.",
                14500,
                "C", "A", "G", "E", "D", "Am", "Em", "Dm", "Fmaj7", "G 4-finger", "Em7", "Cadd9", "Dsus4"),
            Level(
                "level-4",
                "Level 4",
                "Sus Movement",
                "Adds common suspended changes and slightly wider open shapes.",
                17000,
                "C", "A", "G", "E", "D", "Am", "Em", "Dm", "Fmaj7", "G 4-finger", "Em7", "Cadd9", "Cadd9/G", "Dsus4", "Asus2", "Asus4", "Dsus2", "Esus4"),
            Level(
                "level-5",
                "Level 5",
                "Bass Notes",
                "Adds slash chords and first-finger control between open shapes.",
                20000,
                "C", "G", "D", "A", "E", "Am", "Em", "Dm", "Fmaj7", "F", "Cadd9", "D/F#", "G/B", "C/G"),
            Level(
                "level-6",
                "Level 6",
                "Power Basics",
                "Introduces two and three string power chords for rock rhythm.",
                23000,
                "E5", "F5", "G5", "A5", "C5", "D5", "C", "G", "D", "Em", "Am", "F"),
            Level(
                "level-7",
                "Level 7",
                "Power Movement",
                "Moves power chords farther across the neck and adds faster changes.",
                26000,
                "E5", "F5", "G5", "A5", "B5", "C5", "D5", "Eb5", "C", "G", "D", "A", "E", "Am", "Em"),
            Level(
                "level-8",
                "Level 8",
                "Open Sevenths",
                "Adds the most common open dominant sevenths.",
                29000,
                "A7", "D7", "E7", "G7", "B7", "C7", "C", "G", "D", "A", "E", "Am", "Em", "F"),
            Level(
                "level-9",
                "Level 9",
                "Smooth Sevenths",
                "Adds major and minor seventh colors used in ballads and pop.",
                32000,
                "Amaj7", "Cmaj7", "Dmaj7", "Am7", "Dm7", "Em7 open", "A7", "D7", "E7", "G7", "C", "G", "D", "Am", "Em", "Fmaj7"),
            Level(
                "level-10",
                "Level 10",
                "Color Control",
                "Mixes sus, seventh, and slash chords for denser progressions.",
                35000,
                "Asus2", "Asus4", "Dsus2", "Dsus4", "Esus4", "A7sus4", "D7sus4", "E7sus4", "D/F#", "G/B", "C/G", "Amaj7", "Cmaj7", "Dmaj7", "Am7", "Dm7", "Em7 open"),
            Level(
                "level-11",
                "Level 11",
                "First Full Barres",
                "Introduces the practical E-shape major barre family.",
                38000,
                "F barre", "F#", "G barre", "A barre", "Bb", "B barre", "C", "G", "D", "Am", "Em", "E5", "A5", "D5"),
            Level(
                "level-12",
                "Level 12",
                "Minor Barres",
                "Adds E-shape minor barres and longer transitions.",
                41000,
                "Fm", "F#m", "Gm", "Am barre", "Bbm", "Bm E-shape", "F barre", "G barre", "A barre", "C", "G", "D", "Am", "Em"),
            Level(
                "level-13",
                "Level 13",
                "A-Shape Barres",
                "Adds A-string rooted major and minor barre shapes.",
                44000,
                "Bb A-shape", "B", "C barre", "C#", "D barre", "Eb", "Bm", "Cm", "C#m", "Dm barre", "Ebm", "Em barre", "F barre", "G barre", "A barre"),
            Level(
                "level-14",
                "Level 14",
                "Movable Sevenths",
                "Adds barre seventh shapes for harder song sections.",
                47000,
                "Bm7", "C#m7", "F#m7", "Amaj7", "Cmaj7", "Dmaj7", "Emaj7", "Gmaj7", "Am7", "Dm7", "Em7 open", "F barre", "G barre", "A barre", "Bm", "C#m", "Dm barre"),
            Level(
                "level-15",
                "Level 15",
                "Stage Mix",
                "A full practical pool for advanced rhythm drills.",
                0,
                "C", "A", "G", "E", "D", "Am", "Em", "Dm", "F", "Fmaj7",
                "G 4-finger", "Em7", "Cadd9", "Cadd9/G", "Dsus4", "Asus2", "Asus4", "Dsus2", "Esus4",
                "A7", "B7", "C7", "D7", "E7", "G7", "A7sus4", "D7sus4", "E7sus4",
                "Amaj7", "Cmaj7", "Dmaj7", "Emaj7", "Gmaj7", "Am7", "Dm7", "Em7 open",
                "Bm7", "C#m7", "F#m7", "D/F#", "G/B", "C/G",
                "E5", "F5", "G5", "A5", "B5", "C5", "D5", "Eb5",
                "F barre", "F#", "G barre", "Ab", "A barre", "Bb", "B barre",
                "Fm", "F#m", "Gm", "Abm", "Am barre", "Bbm", "Bm E-shape",
                "Bb A-shape", "B", "C barre", "C#", "D barre", "Eb", "Bm", "Cm", "C#m", "Dm barre", "Ebm", "Em barre")
        };
    }

    private static FightClubLevelDefinition Level(string id, string title, string name, string subtitle, int unlockScore, params string[] chordNames)
    {
        var ids = new List<string>();
        var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        if (chordNames != null)
        {
            for (int i = 0; i < chordNames.Length; i++)
            {
                FightClubChordDefinition chord = FindLevelChord(chordNames[i]);
                if (chord != null && seen.Add(chord.Id))
                    ids.Add(chord.Id);
            }
        }

        return new FightClubLevelDefinition(id, title, name, subtitle, ids.ToArray(), unlockScore);
    }

    private static FightClubChordDefinition FindLevelChord(string nameOrId)
    {
        if (string.IsNullOrWhiteSpace(nameOrId))
            return null;

        string trimmed = nameOrId.Trim();
        FightClubChordDefinition byId = FindById(trimmed);
        if (byId != null)
            return byId;

        FightClubChordDefinition exactName = Chords
            .Where(chord => string.Equals(chord.Name, trimmed, StringComparison.OrdinalIgnoreCase))
            .OrderBy(chord => chord.Difficulty)
            .FirstOrDefault();
        return exactName ?? FindByName(trimmed);
    }

    public static string NormalizeChordName(string name)
    {
        if (string.IsNullOrWhiteSpace(name))
            return string.Empty;

        var builder = new StringBuilder(name.Length);
        for (int i = 0; i < name.Length; i++)
        {
            char c = name[i];
            if (char.IsWhiteSpace(c) || c == '(' || c == ')' || c == '-')
                continue;

            builder.Append(char.ToUpperInvariant(c));
        }

        return builder.ToString()
            .Replace("\u266f", "#")
            .Replace("\u266d", "B")
            .Replace("BARRE", string.Empty)
            .Replace("ASHAPE", string.Empty);
    }

    private static string NormalizeEnharmonicRoot(string normalized)
    {
        if (string.IsNullOrWhiteSpace(normalized))
            return string.Empty;

        string[] twoCharacterRoots = { "A#", "BB", "C#", "DB", "D#", "EB", "F#", "GB", "G#", "AB", "CB", "B#", "FB", "E#" };
        foreach (string root in twoCharacterRoots)
        {
            if (!normalized.StartsWith(root, StringComparison.Ordinal))
                continue;

            string suffix = normalized.Substring(root.Length);
            switch (root)
            {
                case "A#":
                    return "BB" + suffix;
                case "DB":
                    return "C#" + suffix;
                case "D#":
                    return "EB" + suffix;
                case "GB":
                    return "F#" + suffix;
                case "G#":
                    return "AB" + suffix;
                case "CB":
                    return "B" + suffix;
                case "B#":
                    return "C" + suffix;
                case "FB":
                    return "E" + suffix;
                case "E#":
                    return "F" + suffix;
            }
        }

        return normalized;
    }

    private static FightClubChordDefinition Chord(string name, int[] frets, int[] fingers, int difficulty, params FightClubBarreDefinition[] barres)
    {
        return new FightClubChordDefinition(name, frets, fingers, difficulty, barres);
    }

    private static FightClubBarreDefinition Barre(int fret, int startString, int endString, int finger)
    {
        return new FightClubBarreDefinition(fret, startString, endString, finger);
    }

    private static FightClubChordDefinition EMajor(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { fret, fret + 2, fret + 2, fret + 1, fret, fret }, new[] { 1, 3, 4, 2, 1, 1 }, difficulty, Barre(fret, 0, 5, 1));
    }

    private static FightClubChordDefinition EMinor(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { fret, fret + 2, fret + 2, fret, fret, fret }, new[] { 1, 3, 4, 1, 1, 1 }, difficulty, Barre(fret, 0, 5, 1));
    }

    private static FightClubChordDefinition AMajor(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { -1, fret, fret + 2, fret + 2, fret + 2, fret }, new[] { -1, 1, 2, 3, 4, 1 }, difficulty, Barre(fret, 1, 5, 1));
    }

    private static FightClubChordDefinition AMinor(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { -1, fret, fret + 2, fret + 2, fret + 1, fret }, new[] { -1, 1, 3, 4, 2, 1 }, difficulty, Barre(fret, 1, 5, 1));
    }

    private static FightClubChordDefinition EPower(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { fret, fret + 2, fret + 2, -1, -1, -1 }, new[] { 1, 3, 4, -1, -1, -1 }, difficulty);
    }

    private static FightClubChordDefinition APower(string name, int fret, int difficulty)
    {
        return Chord(name, new[] { -1, fret, fret + 2, fret + 2, -1, -1 }, new[] { -1, 1, 3, 4, -1, -1 }, difficulty);
    }
}

internal sealed class FightClubChordGroupDefinition
{
    public readonly string Id;
    public readonly string Name;
    public readonly string Subtitle;
    public readonly FightClubChordDefinition[] Chords;

    public FightClubChordGroupDefinition(string id, string name, string subtitle, FightClubChordDefinition[] chords)
    {
        Id = string.IsNullOrWhiteSpace(id) ? FightClubChordDefinition.BuildId(name, null) : id.Trim();
        Name = name ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        Chords = chords ?? Array.Empty<FightClubChordDefinition>();
    }
}

internal sealed class FightClubLevelDefinition
{
    public readonly string Id;
    public readonly string Title;
    public readonly string Name;
    public readonly string Subtitle;
    public readonly string[] ChordIds;
    public readonly int UnlockScore;

    public FightClubLevelDefinition(string id, string title, string name, string subtitle, string[] chordIds, int unlockScore)
    {
        Id = string.IsNullOrWhiteSpace(id) ? FightClubChordDefinition.BuildId(name, null) : id.Trim();
        Title = title ?? string.Empty;
        Name = name ?? string.Empty;
        Subtitle = subtitle ?? string.Empty;
        ChordIds = chordIds ?? Array.Empty<string>();
        UnlockScore = Mathf.Max(0, unlockScore);
    }
}

internal sealed class FightClubChordDefinition
{
    public readonly string Id;
    public readonly string Name;
    public readonly int[] FretsLowToHigh;
    public readonly int[] FingersLowToHigh;
    public readonly int Difficulty;
    private readonly FightClubBarreDefinition[] barres;

    public FightClubChordDefinition(string name, int[] fretsLowToHigh, int[] fingersLowToHigh, int difficulty, FightClubBarreDefinition[] barres = null, string id = null)
    {
        Name = name ?? string.Empty;
        FretsLowToHigh = fretsLowToHigh ?? Array.Empty<int>();
        FingersLowToHigh = fingersLowToHigh != null && fingersLowToHigh.Length == FretsLowToHigh.Length
            ? fingersLowToHigh
            : Array.Empty<int>();
        Difficulty = Mathf.Clamp(difficulty, 1, 5);
        this.barres = barres ?? Array.Empty<FightClubBarreDefinition>();
        Id = string.IsNullOrWhiteSpace(id) ? BuildId(Name, FretsLowToHigh) : id.Trim();
    }

    public static string BuildId(string name, int[] frets)
    {
        string normalizedName = FightClubChordCatalog.NormalizeChordName(name);
        if (string.IsNullOrWhiteSpace(normalizedName))
            normalizedName = "chord";

        string fretPart = frets == null || frets.Length == 0
            ? string.Empty
            : "-" + string.Join("-", frets.Select(fret => fret.ToString()));
        return $"{normalizedName.ToLowerInvariant()}{fretPart}";
    }

    public List<FightClubBarreSnapshot> GetBarres()
    {
        var result = new List<FightClubBarreSnapshot>(barres.Length);
        for (int i = 0; i < barres.Length; i++)
        {
            FightClubBarreDefinition barre = barres[i];
            result.Add(new FightClubBarreSnapshot
            {
                fret = barre.Fret,
                startString = barre.StartString,
                endString = barre.EndString,
                finger = barre.Finger
            });
        }

        return result;
    }

    public int[] GetExpectedMidis(int[] standardTuning)
    {
        var pitches = new HashSet<int>();
        for (int stringIndex = 0; stringIndex < FretsLowToHigh.Length && stringIndex < standardTuning.Length; stringIndex++)
        {
            int fret = FretsLowToHigh[stringIndex];
            if (fret >= 0)
                pitches.Add(standardTuning[stringIndex] + fret);
        }

        return pitches.OrderBy(midi => midi).ToArray();
    }

    public MiniGameExpectedNote[] GetExpectedNotes(int chordIndex, float noteTime, int[] standardTuning, int noteIdBase)
    {
        var notes = new List<MiniGameExpectedNote>();
        int noteOrdinal = 0;
        for (int stringIndex = 0; stringIndex < FretsLowToHigh.Length && stringIndex < standardTuning.Length; stringIndex++)
        {
            int fret = FretsLowToHigh[stringIndex];
            if (fret < 0)
                continue;

            int noteId = noteIdBase + (chordIndex * 16) + noteOrdinal;
            int chordId = noteIdBase + 1000 + chordIndex;
            notes.Add(new MiniGameExpectedNote(
                standardTuning[stringIndex] + fret,
                stringIndex,
                fret,
                standardTuning[stringIndex],
                noteId,
                chordId,
                noteTime));
            noteOrdinal++;
        }

        return notes.ToArray();
    }
}

internal readonly struct FightClubBarreDefinition
{
    public readonly int Fret;
    public readonly int StartString;
    public readonly int EndString;
    public readonly int Finger;

    public FightClubBarreDefinition(int fret, int startString, int endString, int finger)
    {
        Fret = Mathf.Max(0, fret);
        StartString = Mathf.Clamp(startString, 0, 5);
        EndString = Mathf.Clamp(endString, 0, 5);
        Finger = finger;
    }
}
