using System;
using System.Linq;
using NUnit.Framework;

public sealed class ChartEditorTimingServiceTests
{
    [Test]
    public void RemoveOnlyAnchor_DoesNotRecreateAnchorFromLegacySyncPoint()
    {
        ChartEditorProject project = new ChartEditorProject
        {
            audio = new ChartEditorAudioInfo
            {
                durationSeconds = 12.0
            }
        };
        project.EnsureDefaults();

        ChartEditorBeatMarker anchor = ChartEditorTimingService.AddAnchorAtBeat(project, 0.0, 0.0);
        Assert.NotNull(anchor);
        Assert.AreEqual(1, ChartEditorTimingService.GetAnchors(project).Count);
        Assert.AreEqual(1, project.syncPoints.Count);

        ChartEditorTimingService.RemoveAnchor(project, anchor);

        Assert.AreEqual(0, ChartEditorTimingService.GetAnchors(project).Count);
        Assert.AreEqual(0, project.syncPoints.Count);
    }

    [Test]
    public void AddManualAnchor_DemotesGeneratedSynchTheoryTimingSoNeighboringBeatsStretch()
    {
        ChartEditorProject project = CreateGeneratedBeatProject(8, 12.0);

        ChartEditorBeatMarker anchor = ChartEditorTimingService.AddAnchorAtBeat(project, 4.0, 4.8);

        Assert.NotNull(anchor);
        Assert.AreEqual(1, ChartEditorTimingService.GetAnchors(project).Count);
        Assert.IsTrue(ChartEditorTimingService.GetBeatMarkers(project).Any(marker =>
            marker.generatedBySynchTheory && Math.Abs(marker.beatPosition) < 0.0001));
        Assert.IsFalse(ChartEditorTimingService.GetBeatMarkers(project).Any(marker =>
            marker.generatedBySynchTheory && marker.beatPosition > 0.0001));
        Assert.AreEqual(6.0, ChartEditorTimingService.GetAudioTimeForBeat(project, 5.0), 0.001);
    }

    [Test]
    public void MoveTrailingTempoProbe_DemotesGeneratedSynchTheoryTail()
    {
        ChartEditorProject project = CreateGeneratedBeatProject(8, 12.0);

        bool moved = ChartEditorTimingService.MoveTrailingBeatAsTempoProbe(project, 4.0, 5.0, moveContentWithBeatMap: true);

        Assert.IsTrue(moved);
        Assert.IsFalse(ChartEditorTimingService.GetBeatMarkers(project).Any(marker =>
            marker.generatedBySynchTheory && marker.beatPosition > 0.0001));
        Assert.AreEqual(6.25, ChartEditorTimingService.GetAudioTimeForBeat(project, 5.0), 0.001);
    }

    private static ChartEditorProject CreateGeneratedBeatProject(int lastBeat, double durationSeconds)
    {
        ChartEditorProject project = new ChartEditorProject
        {
            audio = new ChartEditorAudioInfo
            {
                durationSeconds = durationSeconds
            }
        };
        project.EnsureDefaults();
        project.beatMap.defaultTempoBpm = 60.0;
        project.beatMap.beatMarkers.Clear();
        for (int beat = 0; beat <= lastBeat; beat++)
        {
            project.beatMap.beatMarkers.Add(new ChartEditorBeatMarker
            {
                id = $"generated-{beat}",
                index = beat,
                beatPosition = beat,
                audioTimeSeconds = beat,
                generatedBySynchTheory = true,
                synchTheoryConfidence = 0.9,
                synchTheorySource = "SynchTheory",
                bpm = beat == 0 ? 60.0 : 0.0
            });
        }

        return project;
    }
}
