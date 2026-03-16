using System.Collections.Generic;

public interface IGuitarGameplayRenderer
{
    void Initialize(GuitarBridgeServer owner, List<NoteData> chartNotes, List<TabSectionData> sections);
    void ResetRenderer(List<NoteData> chartNotes, List<TabSectionData> sections);
    void Render(GuitarGameplaySnapshot snapshot);
    void DisposeRenderer();
}