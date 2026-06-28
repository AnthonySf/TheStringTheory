using System;
using System.Collections.Generic;
using System.Linq;

namespace SynchTheory
{
    public static class SynchTheoryEngine
    {
        private const double BeatEpsilon = 0.0001;

        public static SynchTheoryAlignmentResult Align(SynchTheoryScoreMap score, SynchTheoryAudioData audio, SynchTheoryOptions options = null)
        {
            options ??= SynchTheoryOptions.Default();
            SynchTheoryAlignmentResult result = new SynchTheoryAlignmentResult
            {
                success = false,
                message = "SynchTheory did not run."
            };

            if (score == null)
            {
                result.message = "No score map was provided.";
                return result;
            }

            if (audio == null || !audio.IsValid)
            {
                result.message = "No decoded audio was available for SynchTheory.";
                return result;
            }

            List<SynchTheoryBeat> orderedBeats = (score.beats ?? new List<SynchTheoryBeat>())
                .Where(beat => beat != null)
                .OrderBy(beat => beat.beatPosition)
                .ToList();
            if (orderedBeats.Count < 2)
            {
                result.message = "The score has too few beats to synchronize.";
                return result;
            }

            double startBeat = options.scope == SynchTheoryRunScope.BeatRange ? options.startBeat : orderedBeats[0].beatPosition;
            double endBeat = options.scope == SynchTheoryRunScope.BeatRange ? options.endBeat : orderedBeats[orderedBeats.Count - 1].beatPosition;
            if (endBeat <= startBeat + BeatEpsilon)
            {
                result.message = "The requested sync range is empty.";
                return result;
            }

            result.startBeat = startBeat;
            result.endBeat = endBeat;

            List<SynchTheoryAnchor> controls = BuildRegionControls(score, orderedBeats, audio, startBeat, endBeat, options);
            if (controls.Count < 2)
            {
                result.message = "SynchTheory could not resolve region boundaries.";
                return result;
            }

            Dictionary<string, SynchTheoryBeat> generatedByBeat = new Dictionary<string, SynchTheoryBeat>(StringComparer.Ordinal);
            for (int i = 0; i < controls.Count - 1; i++)
            {
                SynchTheoryAnchor left = controls[i];
                SynchTheoryAnchor right = controls[i + 1];
                if (right.beatPosition <= left.beatPosition + BeatEpsilon ||
                    right.audioTimeSeconds <= left.audioTimeSeconds + 0.02)
                {
                    continue;
                }

                SynchTheoryRegionResult region = AlignRegion(score, audio, orderedBeats, left, right, options, generatedByBeat);
                result.regions.Add(region);
            }

            result.generatedBeats = generatedByBeat.Values
                .OrderBy(beat => beat.beatPosition)
                .ToList();

            if (options.smoothGeneratedTempo)
                SmoothGeneratedBeatTimes(result.generatedBeats, controls, options);

            result.confidence = result.regions.Count == 0
                ? 0.0
                : result.regions.Average(region => region.confidence);
            result.success = result.generatedBeats.Count > 0;
            result.message = result.success
                ? $"SynchTheory generated {result.generatedBeats.Count} beat timings with {result.confidence:P0} confidence."
                : "SynchTheory did not find enough musical information to adjust the beat map.";

            if (result.confidence < 0.35)
                result.warnings.Add("Low alignment confidence. Add manual anchors around clear drum, bass, or guitar attacks and run SynchTheory between anchors.");

            return result;
        }

        private static SynchTheoryRegionResult AlignRegion(
            SynchTheoryScoreMap score,
            SynchTheoryAudioData audio,
            List<SynchTheoryBeat> orderedBeats,
            SynchTheoryAnchor left,
            SynchTheoryAnchor right,
            SynchTheoryOptions options,
            Dictionary<string, SynchTheoryBeat> generatedByBeat)
        {
            List<SynchTheoryBeat> regionBeats = orderedBeats
                .Where(beat => beat.beatPosition >= left.beatPosition - BeatEpsilon &&
                               beat.beatPosition <= right.beatPosition + BeatEpsilon)
                .OrderBy(beat => beat.beatPosition)
                .ToList();

            SynchTheoryRegionResult region = new SynchTheoryRegionResult
            {
                startBeat = left.beatPosition,
                endBeat = right.beatPosition,
                startAudioTimeSeconds = left.audioTimeSeconds,
                endAudioTimeSeconds = right.audioTimeSeconds,
                message = "Skipped."
            };

            if (regionBeats.Count < 2)
                return region;

            double scoreStart = regionBeats[0].chartTimeSeconds;
            double scoreEnd = regionBeats[regionBeats.Count - 1].chartTimeSeconds;
            if (scoreEnd <= scoreStart + 0.02)
            {
                scoreStart = left.audioTimeSeconds;
                scoreEnd = right.audioTimeSeconds;
            }

            SynchTheoryFeatureSequence scoreFeatures = SynchTheoryScoreFeatureExtractor.Extract(score, left.beatPosition, right.beatPosition, scoreStart, scoreEnd, options);
            SynchTheoryFeatureSequence audioFeatures = SynchTheoryAudioFeatureExtractor.Extract(audio, left.audioTimeSeconds, right.audioTimeSeconds, options);
            region.scoreFrameCount = scoreFeatures.Count;
            region.audioFrameCount = audioFeatures.Count;

            if (scoreFeatures.Count < 2 || audioFeatures.Count < 2)
            {
                region.message = "Region has insufficient feature data.";
                return region;
            }

            int[] scoreToAudio = AlignFeatureFrames(scoreFeatures, audioFeatures, options, out double confidence);
            region.confidence = confidence;
            region.alignedFrameCount = scoreToAudio.Length;
            region.message = "Aligned.";

            int snapRadius = Math.Max(1, (int)Math.Round(Math.Max(0.0, options.localOnsetSnapSeconds) * audioFeatures.frameRate));
            for (int i = 0; i < regionBeats.Count; i++)
            {
                SynchTheoryBeat sourceBeat = regionBeats[i];
                bool isLeftBoundary = Math.Abs(sourceBeat.beatPosition - left.beatPosition) <= BeatEpsilon;
                bool isRightBoundary = Math.Abs(sourceBeat.beatPosition - right.beatPosition) <= BeatEpsilon;
                double audioTime;
                double beatConfidence = confidence;

                if (isLeftBoundary)
                {
                    audioTime = left.audioTimeSeconds;
                    beatConfidence = 1.0;
                }
                else if (isRightBoundary)
                {
                    audioTime = right.audioTimeSeconds;
                    beatConfidence = 1.0;
                }
                else
                {
                    int scoreFrame = Clamp((int)Math.Round((sourceBeat.chartTimeSeconds - scoreStart) * scoreFeatures.frameRate), 0, scoreToAudio.Length - 1);
                    int audioFrame = Clamp(scoreToAudio[scoreFrame], 0, Math.Max(0, audioFeatures.Count - 1));
                    if (sourceBeat.isDownbeat || HasNearbyScoreEvent(score, sourceBeat.beatPosition))
                        audioFrame = SynchTheoryAudioFeatureExtractor.FindStrongestOnsetFrame(audioFeatures, audioFrame, snapRadius);
                    audioTime = audioFeatures.TimeAtFrame(audioFrame);
                    audioTime = Math.Max(left.audioTimeSeconds + 0.001, Math.Min(right.audioTimeSeconds - 0.001, audioTime));
                }

                string key = BeatKey(sourceBeat.beatPosition);
                generatedByBeat[key] = new SynchTheoryBeat
                {
                    index = sourceBeat.index,
                    beatPosition = sourceBeat.beatPosition,
                    chartTimeSeconds = sourceBeat.chartTimeSeconds,
                    audioTimeSeconds = audioTime,
                    isDownbeat = sourceBeat.isDownbeat,
                    isAnchor = isLeftBoundary || isRightBoundary,
                    isGenerated = !isLeftBoundary && !isRightBoundary,
                    confidence = beatConfidence
                };
            }

            return region;
        }

        private static int[] AlignFeatureFrames(
            SynchTheoryFeatureSequence score,
            SynchTheoryFeatureSequence audio,
            SynchTheoryOptions options,
            out double confidence)
        {
            int n = score.Count;
            int m = audio.Count;
            int[] mapping = new int[n];
            confidence = 0.0;
            if (n <= 1 || m <= 1)
                return mapping;

            int extraBand = (int)Math.Round(Math.Max(1.0, options.maxWarpWindowSeconds) * score.frameRate);
            int band = Math.Max(extraBand, Math.Abs(m - n) + extraBand);
            band = Math.Min(Math.Max(8, band), Math.Max(n, m));

            double[] previous = new double[m];
            double[] current = new double[m];
            for (int j = 0; j < m; j++)
            {
                previous[j] = double.PositiveInfinity;
                current[j] = double.PositiveInfinity;
            }

            int[] rowStarts = new int[n];
            byte[][] back = new byte[n][];
            const byte FromDiag = 0;
            const byte FromUp = 1;
            const byte FromLeft = 2;

            for (int i = 0; i < n; i++)
            {
                int predicted = (int)Math.Round(i * (m - 1) / (double)Math.Max(1, n - 1));
                int start = Math.Max(0, predicted - band);
                int end = Math.Min(m - 1, predicted + band);
                rowStarts[i] = start;
                back[i] = new byte[end - start + 1];

                for (int j = start; j <= end; j++)
                {
                    double featureCost = FeatureCost(score, audio, i, j, options);
                    double best;
                    byte direction;
                    if (i == 0 && j == 0)
                    {
                        best = 0.0;
                        direction = FromDiag;
                    }
                    else
                    {
                        double diag = i > 0 && j > 0 ? previous[j - 1] : double.PositiveInfinity;
                        double up = i > 0 ? previous[j] + 0.018 : double.PositiveInfinity;
                        double left = j > 0 ? current[j - 1] + 0.018 : double.PositiveInfinity;
                        best = diag;
                        direction = FromDiag;
                        if (up < best)
                        {
                            best = up;
                            direction = FromUp;
                        }
                        if (left < best)
                        {
                            best = left;
                            direction = FromLeft;
                        }
                    }

                    current[j] = featureCost + best;
                    back[i][j - start] = direction;
                }

                double[] swap = previous;
                previous = current;
                current = swap;
                for (int j = 0; j < m; j++)
                    current[j] = double.PositiveInfinity;
            }

            int endFrame = m - 1;
            double bestEnd = previous[endFrame];
            if (double.IsInfinity(bestEnd))
            {
                bestEnd = double.PositiveInfinity;
                for (int j = 0; j < m; j++)
                {
                    if (previous[j] < bestEnd)
                    {
                        bestEnd = previous[j];
                        endFrame = j;
                    }
                }
            }

            int audioFrame = endFrame;
            int scoreFrame = n - 1;
            int[] lastAudioForScore = new int[n];
            for (int i = 0; i < n; i++)
                lastAudioForScore[i] = -1;

            int guard = 0;
            while (scoreFrame >= 0 && audioFrame >= 0 && guard++ < (n + m) * 3)
            {
                lastAudioForScore[scoreFrame] = audioFrame;
                int start = rowStarts[scoreFrame];
                int local = audioFrame - start;
                byte direction = local >= 0 && local < back[scoreFrame].Length ? back[scoreFrame][local] : FromDiag;
                if (scoreFrame == 0 && audioFrame == 0)
                    break;

                if (direction == FromDiag)
                {
                    scoreFrame--;
                    audioFrame--;
                }
                else if (direction == FromUp)
                {
                    scoreFrame--;
                }
                else
                {
                    audioFrame--;
                }
            }

            int lastKnown = 0;
            for (int i = 0; i < n; i++)
            {
                if (lastAudioForScore[i] >= 0)
                    lastKnown = lastAudioForScore[i];
                mapping[i] = lastKnown;
            }

            int nextKnown = m - 1;
            for (int i = n - 1; i >= 0; i--)
            {
                if (lastAudioForScore[i] >= 0)
                    nextKnown = lastAudioForScore[i];
                else
                    mapping[i] = nextKnown;
            }

            confidence = 1.0 / (1.0 + Math.Max(0.0, bestEnd / Math.Max(1, n + m)));
            return mapping;
        }

        private static double FeatureCost(SynchTheoryFeatureSequence score, SynchTheoryFeatureSequence audio, int i, int j, SynchTheoryOptions options)
        {
            double scoreOnset = score.onset != null && i < score.onset.Length ? score.onset[i] : 0.0;
            double audioOnset = audio.onset != null && j < audio.onset.Length ? audio.onset[j] : 0.0;
            double scoreEnergy = score.energy != null && i < score.energy.Length ? score.energy[i] : 0.0;
            double audioEnergy = audio.energy != null && j < audio.energy.Length ? audio.energy[j] : 0.0;
            double scoreBeat = score.beat != null && i < score.beat.Length ? score.beat[i] : 0.0;

            double onsetCost = Math.Abs(scoreOnset - audioOnset);
            double energyCost = Math.Abs(scoreEnergy - audioEnergy);
            double beatCost = scoreBeat > 0.001 ? Math.Max(0.0, 1.0 - audioOnset) * scoreBeat : 0.0;
            return onsetCost * Math.Max(0.0, options.onsetWeight) +
                   energyCost * Math.Max(0.0, options.energyWeight) +
                   beatCost * Math.Max(0.0, options.beatWeight);
        }

        private static List<SynchTheoryAnchor> BuildRegionControls(
            SynchTheoryScoreMap score,
            List<SynchTheoryBeat> orderedBeats,
            SynchTheoryAudioData audio,
            double startBeat,
            double endBeat,
            SynchTheoryOptions options)
        {
            List<SynchTheoryAnchor> controls = new List<SynchTheoryAnchor>();
            SynchTheoryBeat firstBeat = FindNearestBeat(orderedBeats, startBeat);
            SynchTheoryBeat lastBeat = FindNearestBeat(orderedBeats, endBeat);
            if (firstBeat == null || lastBeat == null)
                return controls;

            SynchTheoryAnchor manualStart = score.anchors?
                .Where(anchor => anchor != null && Math.Abs(anchor.beatPosition - startBeat) <= BeatEpsilon)
                .OrderBy(anchor => Math.Abs(anchor.audioTimeSeconds - firstBeat.audioTimeSeconds))
                .FirstOrDefault();

            double startAudio = manualStart != null
                ? manualStart.audioTimeSeconds
                : EstimateStartBoundaryAudioTime(score, audio, firstBeat, options);

            controls.Add(new SynchTheoryAnchor
            {
                id = manualStart?.id ?? "synchtheory_start",
                beatPosition = startBeat,
                audioTimeSeconds = Math.Max(0.0, Math.Min(audio.DurationSeconds, startAudio)),
                locked = manualStart?.locked ?? false,
                label = manualStart?.label ?? "Start"
            });

            if (options.keepManualAnchors && score.anchors != null)
            {
                foreach (SynchTheoryAnchor anchor in score.anchors
                             .Where(anchor => anchor != null &&
                                              anchor.beatPosition > startBeat + BeatEpsilon &&
                                              anchor.beatPosition < endBeat - BeatEpsilon)
                             .OrderBy(anchor => anchor.beatPosition))
                {
                    controls.Add(new SynchTheoryAnchor
                    {
                        id = anchor.id,
                        beatPosition = anchor.beatPosition,
                        audioTimeSeconds = Math.Max(0.0, Math.Min(audio.DurationSeconds, anchor.audioTimeSeconds)),
                        locked = anchor.locked,
                        label = anchor.label
                    });
                }
            }

            SynchTheoryAnchor manualEnd = score.anchors?
                .Where(anchor => anchor != null && Math.Abs(anchor.beatPosition - endBeat) <= BeatEpsilon)
                .OrderBy(anchor => Math.Abs(anchor.audioTimeSeconds - lastBeat.audioTimeSeconds))
                .FirstOrDefault();

            double endAudio = manualEnd != null
                ? manualEnd.audioTimeSeconds
                : EstimateEndBoundaryAudioTime(score, audio, lastBeat, options);
            if (endAudio <= controls[0].audioTimeSeconds + 0.05 && audio.DurationSeconds > controls[0].audioTimeSeconds + 0.05)
                endAudio = audio.DurationSeconds;

            controls.Add(new SynchTheoryAnchor
            {
                id = manualEnd?.id ?? "synchtheory_end",
                beatPosition = endBeat,
                audioTimeSeconds = endAudio,
                locked = manualEnd?.locked ?? false,
                label = manualEnd?.label ?? "End"
            });

            return controls
                .GroupBy(anchor => BeatKey(anchor.beatPosition), StringComparer.Ordinal)
                .Select(group => group.First())
                .OrderBy(anchor => anchor.beatPosition)
                .ToList();
        }

        private static double EstimateStartBoundaryAudioTime(
            SynchTheoryScoreMap score,
            SynchTheoryAudioData audio,
            SynchTheoryBeat firstBeat,
            SynchTheoryOptions options)
        {
            if (score == null || audio == null || firstBeat == null || !audio.IsValid)
                return Math.Max(0.0, firstBeat?.audioTimeSeconds ?? 0.0);

            SynchTheoryScoreEvent firstEvent = score.events?
                .Where(evt => evt != null && evt.beatPosition >= firstBeat.beatPosition - BeatEpsilon)
                .OrderBy(evt => evt.chartTimeSeconds)
                .FirstOrDefault();

            double eventOffset = firstEvent == null
                ? 0.0
                : Math.Max(0.0, firstEvent.chartTimeSeconds - firstBeat.chartTimeSeconds);
            double fallback = Math.Max(0.0, firstBeat.audioTimeSeconds);
            double boundarySearch = Math.Max(1.0, options.boundarySearchSeconds);
            double searchStart = Math.Max(0.0, fallback + eventOffset - Math.Min(2.0, boundarySearch * 0.35));
            double searchEnd = Math.Min(audio.DurationSeconds, fallback + eventOffset + boundarySearch);
            if (searchEnd <= searchStart + 0.1)
                return fallback;

            SynchTheoryFeatureSequence features = SynchTheoryAudioFeatureExtractor.Extract(audio, searchStart, searchEnd, options);
            if (features.Count == 0)
                return fallback;

            int expectedFrame = (int)Math.Round(Math.Max(0.0, fallback + eventOffset - searchStart) * features.frameRate);
            int bestFrame = SynchTheoryAudioFeatureExtractor.FindEarliestCredibleOnsetFrame(
                features,
                Math.Max(0, expectedFrame - (int)Math.Round(features.frameRate * 0.35)),
                Math.Max(0, features.Count - 1));
            double estimatedEventTime = features.TimeAtFrame(bestFrame);
            return Math.Max(0.0, estimatedEventTime - eventOffset);
        }

        private static double EstimateEndBoundaryAudioTime(
            SynchTheoryScoreMap score,
            SynchTheoryAudioData audio,
            SynchTheoryBeat lastBeat,
            SynchTheoryOptions options)
        {
            if (score == null || audio == null || lastBeat == null || !audio.IsValid)
                return Math.Max(0.0, lastBeat?.audioTimeSeconds ?? 0.0);

            SynchTheoryScoreEvent lastEvent = score.events?
                .Where(evt => evt != null && evt.beatPosition <= lastBeat.beatPosition + BeatEpsilon)
                .OrderByDescending(evt => evt.chartTimeSeconds)
                .FirstOrDefault();

            double eventOffset = lastEvent == null
                ? 0.0
                : Math.Max(0.0, lastBeat.chartTimeSeconds - lastEvent.chartTimeSeconds);
            double fallback = Math.Max(0.0, lastBeat.audioTimeSeconds);
            double boundarySearch = Math.Max(1.0, options.boundarySearchSeconds);
            double searchStart = Math.Max(0.0, fallback - eventOffset - boundarySearch);
            double searchEnd = Math.Min(audio.DurationSeconds, fallback - eventOffset + Math.Min(2.0, boundarySearch * 0.35));
            if (searchEnd <= searchStart + 0.1)
                return fallback;

            SynchTheoryFeatureSequence features = SynchTheoryAudioFeatureExtractor.Extract(audio, searchStart, searchEnd, options);
            if (features.Count == 0)
                return fallback;

            int bestFrame = 0;
            double bestScore = double.NegativeInfinity;
            for (int i = 0; i < features.Count; i++)
            {
                double onset = features.onset != null && i < features.onset.Length ? features.onset[i] : 0.0;
                double energy = features.energy != null && i < features.energy.Length ? features.energy[i] : 0.0;
                double lateBias = i / (double)Math.Max(1, features.Count - 1) * 0.03;
                double scoreValue = onset * 0.85 + energy * 0.15 + lateBias;
                if (scoreValue > bestScore)
                {
                    bestScore = scoreValue;
                    bestFrame = i;
                }
            }
            double estimatedEventTime = features.TimeAtFrame(bestFrame);
            return Math.Max(0.0, Math.Min(audio.DurationSeconds, estimatedEventTime + eventOffset));
        }

        private static void SmoothGeneratedBeatTimes(List<SynchTheoryBeat> beats, List<SynchTheoryAnchor> controls, SynchTheoryOptions options)
        {
            if (beats == null || beats.Count < 4)
                return;

            beats.Sort((a, b) => a.beatPosition.CompareTo(b.beatPosition));
            int passes = Math.Max(0, options.tempoSmoothingPasses);
            for (int pass = 0; pass < passes; pass++)
            {
                for (int i = 1; i < beats.Count - 1; i++)
                {
                    if (beats[i].isAnchor)
                        continue;

                    double previous = beats[i - 1].audioTimeSeconds;
                    double next = beats[i + 1].audioTimeSeconds;
                    double desired = (previous + next) * 0.5;
                    beats[i].audioTimeSeconds = beats[i].audioTimeSeconds * 0.65 + desired * 0.35;
                }
            }

            for (int i = 1; i < beats.Count; i++)
            {
                if (beats[i].isAnchor)
                    continue;

                double beatSpan = Math.Max(BeatEpsilon, beats[i].beatPosition - beats[i - 1].beatPosition);
                double minGap = beatSpan * 60.0 / Math.Max(1.0, options.maximumTempoBpm);
                double maxGap = beatSpan * 60.0 / Math.Max(1.0, options.minimumTempoBpm);
                double gap = beats[i].audioTimeSeconds - beats[i - 1].audioTimeSeconds;
                if (gap < minGap)
                    beats[i].audioTimeSeconds = beats[i - 1].audioTimeSeconds + minGap;
                else if (gap > maxGap)
                    beats[i].audioTimeSeconds = beats[i - 1].audioTimeSeconds + maxGap;
            }
        }

        private static bool HasNearbyScoreEvent(SynchTheoryScoreMap score, double beatPosition)
        {
            if (score?.events == null)
                return false;

            return score.events.Any(evt => evt != null && Math.Abs(evt.beatPosition - beatPosition) <= 0.12);
        }

        private static SynchTheoryBeat FindNearestBeat(List<SynchTheoryBeat> beats, double beatPosition)
        {
            return beats?
                .OrderBy(beat => Math.Abs((beat?.beatPosition ?? 0.0) - beatPosition))
                .FirstOrDefault();
        }

        private static string BeatKey(double beatPosition)
        {
            return Math.Round(beatPosition, 4).ToString("0.####", System.Globalization.CultureInfo.InvariantCulture);
        }

        private static int Clamp(int value, int min, int max)
        {
            if (value < min)
                return min;
            if (value > max)
                return max;
            return value;
        }
    }
}
