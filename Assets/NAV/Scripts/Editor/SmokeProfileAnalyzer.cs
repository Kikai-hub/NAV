using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using UnityEditor;
using UnityEditor.Profiling;
using UnityEditorInternal;
using UnityEngine;

namespace NAV.Editor
{
    /// <summary>
    /// Reads a Profiler capture written by BuildSmokeTest (<c>-navSmokeProfile &lt;path&gt;</c> in a
    /// Development Build) and lists the slow frames of the main thread: for each, the markers
    /// with the most self time and where they sit (PlayerLoop > ... > marker), then the markers
    /// that are slow most often across all the slow frames. NAV > Dev > Analyze Smoke Profile asks
    /// for the .raw file; <see cref="Analyze"/> is callable from code (execute_code).
    /// </summary>
    internal static class SmokeProfileAnalyzer
    {
        [MenuItem("NAV/Dev/Analyze Smoke Profile")]
        private static void AnalyzeFromMenu()
        {
            string path = EditorUtility.OpenFilePanel("Smoke test profile", "", "raw");
            if (!string.IsNullOrEmpty(path))
            {
                Debug.Log(Analyze(path, 40f, 25));
            }
        }

        internal static string Analyze(string rawPath, float slowMs, int maxFrames)
        {
            if (!File.Exists(rawPath))
            {
                return $"SmokeProfileAnalyzer: no file {rawPath}";
            }

            if (!ProfilerDriver.LoadProfile(rawPath, false))
            {
                return $"SmokeProfileAnalyzer: couldn't load {rawPath}";
            }

            int first = ProfilerDriver.firstFrameIndex;
            int last = ProfilerDriver.lastFrameIndex;
            var text = new StringBuilder();
            var often = new Dictionary<string, (int Count, float Ms)>();
            var times = new List<float>();
            int slow = 0;
            for (int frame = first; frame <= last; frame++)
            {
                using HierarchyFrameDataView view = ProfilerDriver.GetHierarchyFrameDataView(frame, 0,
                    HierarchyFrameDataView.ViewModes.MergeSamplesWithTheSameName, HierarchyFrameDataView.columnTotalTime, false);
                if (view == null || !view.valid)
                {
                    continue;
                }

                float frameMs = view.frameTimeMs;
                times.Add(frameMs);
                if (frameMs < slowMs)
                {
                    continue;
                }

                slow++;
                var items = new List<(string Path, float Self, float Total)>();
                Walk(view, view.GetRootItemID(), "", items, 0);
                List<(string Path, float Self, float Total)> top = items.OrderByDescending(i => i.Self).Take(6).ToList();
                foreach ((string path, float self, float _) in top.Where(t => t.Self >= 3f))
                {
                    string name = path.Substring(path.LastIndexOf('>') + 1).Trim();
                    often.TryGetValue(name, out (int Count, float Ms) seen);
                    often[name] = (seen.Count + 1, seen.Ms + self);
                }

                if (slow <= maxFrames)
                {
                    text.AppendLine($"frame {frame - first}: {frameMs:0.0} ms");
                    foreach ((string path, float self, float total) in top)
                    {
                        text.AppendLine($"    self {self,6:0.0}  total {total,6:0.0}  {path}");
                    }
                }
            }

            times.Sort();
            string header = times.Count == 0
                ? "no frames"
                : $"{times.Count} frames, median {times[times.Count / 2]:0.0} ms, p99 {times[(int)(times.Count * 0.99f)]:0.0} ms, max {times[times.Count - 1]:0.0} ms; {slow} frame(s) >= {slowMs} ms";
            var summary = new StringBuilder();
            summary.AppendLine($"SmokeProfileAnalyzer: {Path.GetFileName(rawPath)} - {header}");
            summary.AppendLine("Most often slow (self >= 3 ms in a slow frame): " + string.Join("; ",
                often.OrderByDescending(o => o.Value.Ms).Take(12).Select(o => $"{o.Key} x{o.Value.Count} ({o.Value.Ms / o.Value.Count:0.0} ms avg)")));
            summary.Append(text);
            return summary.ToString();
        }

        private static void Walk(HierarchyFrameDataView view, int id, string path, List<(string, float, float)> items, int depth)
        {
            var children = new List<int>();
            view.GetItemChildren(id, children);
            foreach (int child in children)
            {
                string name = view.GetItemName(child);
                string childPath = depth == 0 ? name : path + " > " + name;
                float total = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnTotalTime);
                float self = view.GetItemColumnDataAsFloat(child, HierarchyFrameDataView.columnSelfTime);
                if (total < 0.5f)
                {
                    continue;
                }

                items.Add((childPath, self, total));
                if (depth < 40)
                {
                    Walk(view, child, childPath, items, depth + 1);
                }
            }
        }
    }
}
