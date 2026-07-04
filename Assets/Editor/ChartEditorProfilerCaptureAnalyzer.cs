using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

public static class ChartEditorProfilerCaptureAnalyzer
{
    [MenuItem("Tools/Analyze Profiler Capture (Downloads)")]
    public static void RunFromMenu()
    {
        string capturePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "profiler_chartEditor.data");
        string reportPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), "Downloads", "profiler_chartEditor_report.txt");
        Analyze(capturePath, reportPath);
        Debug.Log($"Profiler analysis written to {reportPath}");
    }

    public static void Run()
    {
        string capturePath = Environment.GetEnvironmentVariable("PROFILER_CAPTURE_PATH");
        string reportPath = Environment.GetEnvironmentVariable("PROFILER_REPORT_PATH");
        int exitCode = 0;
        try
        {
            Analyze(capturePath, reportPath);
        }
        catch (Exception ex)
        {
            Debug.LogError($"Profiler analysis failed: {ex}");
            try
            {
                File.WriteAllText(reportPath ?? "profiler_report_error.txt", "ANALYSIS FAILED: " + ex);
            }
            catch
            {
                // ignored
            }

            exitCode = 1;
        }

        EditorApplication.Exit(exitCode);
    }

    private static void Analyze(string capturePath, string reportPath)
    {
        if (string.IsNullOrWhiteSpace(capturePath) || !File.Exists(capturePath))
            throw new FileNotFoundException($"Capture not found: {capturePath}");

        if (!ProfilerDriver.LoadProfile(capturePath, false))
            throw new InvalidOperationException("ProfilerDriver.LoadProfile failed.");

        int first = ProfilerDriver.firstFrameIndex;
        int last = ProfilerDriver.lastFrameIndex;
        StringBuilder report = new StringBuilder();
        report.AppendLine($"Capture: {capturePath}");
        report.AppendLine($"Frames: {first}..{last} ({last - first + 1})");

        List<(int frame, float ms)> frameTimes = new List<(int, float)>();
        Dictionary<string, float> selfTimeTotals = new Dictionary<string, float>(StringComparer.Ordinal);
        Dictionary<string, int> selfTimeFrames = new Dictionary<string, int>(StringComparer.Ordinal);

        for (int f = first; f <= last; f++)
        {
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
                       f, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                       HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid)
                    continue;

                float frameMs = view.frameTimeMs;
                frameTimes.Add((f, frameMs));

                int root = view.GetRootItemID();
                HashSet<string> seenThisFrame = new HashSet<string>(StringComparer.Ordinal);
                AccumulateSelfTimes(view, root, 0, selfTimeTotals, seenThisFrame);
                foreach (string name in seenThisFrame)
                {
                    selfTimeFrames.TryGetValue(name, out int count);
                    selfTimeFrames[name] = count + 1;
                }
            }
        }

        if (frameTimes.Count == 0)
            throw new InvalidOperationException("No valid main-thread frames in capture.");

        List<float> sorted = frameTimes.Select(t => t.ms).OrderBy(v => v).ToList();
        float Median() => sorted[sorted.Count / 2];
        float Percentile(double p) => sorted[Math.Min(sorted.Count - 1, (int)(sorted.Count * p))];

        report.AppendLine();
        report.AppendLine("== Frame times (main thread) ==");
        report.AppendLine($"avg {sorted.Average():0.00} ms | median {Median():0.00} ms | p90 {Percentile(0.90):0.00} ms | p99 {Percentile(0.99):0.00} ms | max {sorted.Last():0.00} ms");
        report.AppendLine($"frames > 20 ms: {sorted.Count(v => v > 20f)} | > 33 ms: {sorted.Count(v => v > 33f)} | > 50 ms: {sorted.Count(v => v > 50f)} | > 100 ms: {sorted.Count(v => v > 100f)}");

        report.AppendLine();
        report.AppendLine("== Top 40 markers by summed SELF time across capture ==");
        foreach (KeyValuePair<string, float> pair in selfTimeTotals.OrderByDescending(p => p.Value).Take(40))
        {
            selfTimeFrames.TryGetValue(pair.Key, out int frames);
            report.AppendLine($"{pair.Value,10:0.0} ms total | {frames,4} frames | {pair.Key}");
        }

        report.AppendLine();
        report.AppendLine("== Hot paths of the 12 worst frames ==");
        foreach ((int frame, float ms) in frameTimes.OrderByDescending(t => t.ms).Take(12))
        {
            report.AppendLine($"-- frame {frame}: {ms:0.0} ms");
            using (HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(
                       frame, 0, HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName,
                       HierarchyFrameDataView.columnTotalTime, false))
            {
                if (view == null || !view.valid)
                    continue;

                AppendHotPath(view, view.GetRootItemID(), 0, report);
            }
        }

        File.WriteAllText(reportPath, report.ToString(), Encoding.UTF8);
    }

    private static void AccumulateSelfTimes(
        HierarchyFrameDataView view,
        int itemId,
        int depth,
        Dictionary<string, float> totals,
        HashSet<string> seenThisFrame)
    {
        if (depth > 24)
            return;

        List<int> children = new List<int>();
        view.GetItemChildren(itemId, children);
        for (int i = 0; i < children.Count; i++)
        {
            int child = children[i];
            float self = view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnSelfTime);
            if (self >= 0.05f)
            {
                string name = view.GetItemName(child);
                totals.TryGetValue(name, out float sum);
                totals[name] = sum + self;
                seenThisFrame.Add(name);
            }

            float total = view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnTotalTime);
            if (total >= 0.25f)
                AccumulateSelfTimes(view, child, depth + 1, totals, seenThisFrame);
        }
    }

    private static void AppendHotPath(HierarchyFrameDataView view, int itemId, int depth, StringBuilder report)
    {
        if (depth > 16)
            return;

        List<int> children = new List<int>();
        view.GetItemChildren(itemId, children);
        int hottest = -1;
        float hottestTotal = 0f;
        foreach (int child in children)
        {
            float total = view.GetItemColumnDataAsSingle(child, HierarchyFrameDataView.columnTotalTime);
            if (total > hottestTotal)
            {
                hottestTotal = total;
                hottest = child;
            }
        }

        if (hottest < 0 || hottestTotal < 0.5f)
            return;

        float self = view.GetItemColumnDataAsSingle(hottest, HierarchyFrameDataView.columnSelfTime);
        report.AppendLine($"{new string(' ', (depth + 1) * 2)}{view.GetItemName(hottest)}  total {hottestTotal.ToString("0.0", CultureInfo.InvariantCulture)} ms, self {self.ToString("0.0", CultureInfo.InvariantCulture)} ms");
        AppendHotPath(view, hottest, depth + 1, report);
    }
}
