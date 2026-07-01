public enum SongNotationSourceKind
{
    None = 0,
    MusicXml = 1,
    Gp5 = 2,
    // Reserved for old serialized/cache data. Cache folders are importer inputs, not runtime notation sources.
    ArrangementCache = 3,
    TheoryPackage = 4
}
