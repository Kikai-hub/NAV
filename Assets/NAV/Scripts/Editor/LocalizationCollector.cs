using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using UnityEditor;
using UnityEngine;
using NAV.Core.Localization;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Localization > Collect Strings (2026-11-13): gathers every English text the game shows
    /// into Assets/NAV/Resources/Localization/Strings.json, keeping the translations already there,
    /// and logs how many still have no Russian. Sources:
    /// <list type="bullet">
    /// <item>UXML text/label/tooltip attributes;</item>
    /// <item>display names and descriptions of every data asset (items, pieces, recipes, stations,
    /// creatures, biomes, weather, POIs, resource nodes, weapons) and every string of the Forest
    /// Wrath settings (the Leshy's lines);</item>
    /// <item>prefab texts shown to the player: boss titles, chest and station names;</item>
    /// <item><c>Loc.T("...")</c> / <c>Loc.F("...")</c> literals in the scripts, the Settings window's
    /// row labels and choice lists, and enum names shown as text (armour slots).</item>
    /// </list>
    /// Lines no longer found are kept (marked by nothing) - delete them by hand if wanted.
    /// </summary>
    internal static class LocalizationCollector
    {
        private const string JsonPath = "Assets/NAV/Resources/Localization/Strings.json";
        private const string ScriptsRoot = "Assets/NAV/Scripts";

        [MenuItem("NAV/Localization/Collect Strings")]
        private static void CollectMenu()
        {
            Debug.Log(Collect());
        }

        internal static string Collect()
        {
            var keys = new SortedSet<string>(StringComparer.Ordinal);

            // UXML.
            foreach (string path in Directory.GetFiles("Assets/NAV", "*.uxml", SearchOption.AllDirectories))
            {
                string xml = File.ReadAllText(path);
                foreach (Match m in Regex.Matches(xml, "\\s(?:text|label|tooltip)=\"([^\"]+)\""))
                {
                    Add(keys, System.Net.WebUtility.HtmlDecode(m.Groups[1].Value));
                }
            }

            // Data assets: their display-facing strings.
            string[] fieldNames = { "_displayName", "_description", "_title" };
            foreach (string guid in AssetDatabase.FindAssets("t:ScriptableObject", new[] { "Assets/NAV/ScriptableObjects" }))
            {
                var asset = AssetDatabase.LoadAssetAtPath<ScriptableObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (asset == null)
                {
                    continue;
                }

                var so = new SerializedObject(asset);
                bool allStrings = asset is NAV.World.Encounters.ForestWrathSettings;
                SerializedProperty it = so.GetIterator();
                while (it.Next(true))
                {
                    if (it.propertyType == SerializedPropertyType.String && !it.name.StartsWith("m_") && (allStrings || fieldNames.Contains(it.name)))
                    {
                        Add(keys, it.stringValue);
                    }
                }
            }

            // Prefabs: boss titles, chest titles, station names.
            foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/NAV/Prefabs" }))
            {
                var prefab = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid));
                if (prefab == null)
                {
                    continue;
                }

                foreach (MonoBehaviour behaviour in prefab.GetComponentsInChildren<MonoBehaviour>(true))
                {
                    if (behaviour == null)
                    {
                        continue;
                    }

                    var so = new SerializedObject(behaviour);
                    foreach (string field in new[] { "_title", "_stationName" })
                    {
                        SerializedProperty p = so.FindProperty(field);
                        if (p != null && p.propertyType == SerializedPropertyType.String)
                        {
                            Add(keys, p.stringValue);
                        }
                    }
                }
            }

            // Scripts.
            var literal = new Regex("Loc\\.(?:T|F)\\(\\s*\"((?:[^\"\\\\]|\\\\.)*)\"");
            var settingsRow = new Regex("Add(?:Section|Hint|Toggle|Slider|PercentSlider|Dropdown|KeyRow)\\(page, \"((?:[^\"\\\\]|\\\\.)*)\"");
            var array = new Regex("(?:new\\[\\]|string\\[\\] \\w+ =|new string\\[\\]) \\{([^}]*)\\}");
            var tuple = new Regex("\\(\"([^\"]+)\", \"[^\"]+\", (?:null|\"[^\"]+\")\\)");
            foreach (string path in Directory.GetFiles(ScriptsRoot, "*.cs", SearchOption.AllDirectories))
            {
                if (path.Replace('\\', '/').Contains("/Editor/"))
                {
                    continue;
                }

                string code = File.ReadAllText(path);
                foreach (Match m in literal.Matches(code))
                {
                    Add(keys, Regex.Unescape(m.Groups[1].Value));
                }

                if (Path.GetFileName(path) == "SettingsUIController.cs")
                {
                    foreach (Match m in settingsRow.Matches(code))
                    {
                        Add(keys, Regex.Unescape(m.Groups[1].Value));
                    }

                    foreach (Match m in array.Matches(code))
                    {
                        foreach (Match s in Regex.Matches(m.Groups[1].Value, "\"([^\"]+)\""))
                        {
                            // Shown choices start with a capital ("Off", "MSAA 2x"); tab ids don't.
                            if (char.IsUpper(s.Groups[1].Value[0]))
                            {
                                Add(keys, s.Groups[1].Value);
                            }
                        }
                    }

                    foreach (Match m in tuple.Matches(code))
                    {
                        Add(keys, m.Groups[1].Value);
                    }
                }
            }

            foreach (string slot in Enum.GetNames(typeof(NAV.Gameplay.Equipment.ArmorSlot)))
            {
                Add(keys, slot);
            }

            // Merge with what is there.
            var table = new Loc.Table();
            if (File.Exists(JsonPath))
            {
                table = JsonUtility.FromJson<Loc.Table>(File.ReadAllText(JsonPath)) ?? new Loc.Table();
            }

            var existing = new Dictionary<string, Loc.Entry>(StringComparer.Ordinal);
            foreach (Loc.Entry entry in table.entries.Where(e => e != null && !string.IsNullOrEmpty(e.en)))
            {
                existing[entry.en] = entry;
            }

            int added = 0;
            foreach (string key in keys)
            {
                if (!existing.ContainsKey(key))
                {
                    existing[key] = new Loc.Entry { en = key, ru = string.Empty };
                    added++;
                }
            }

            var merged = new Loc.Table { entries = existing.Values.OrderBy(e => e.en, StringComparer.Ordinal).ToList() };
            Directory.CreateDirectory(Path.GetDirectoryName(JsonPath));
            File.WriteAllText(JsonPath, JsonUtility.ToJson(merged, true), new UTF8Encoding(false));
            AssetDatabase.ImportAsset(JsonPath);

            List<string> missing = merged.entries.Where(e => string.IsNullOrEmpty(e.ru)).Select(e => e.en).ToList();
            List<string> stale = merged.entries.Where(e => !keys.Contains(e.en)).Select(e => e.en).ToList();
            return $"LocalizationCollector: {keys.Count} strings in use, {added} new, {merged.entries.Count} in {JsonPath}; "
                + $"{missing.Count} without Russian; {stale.Count} no longer found{(stale.Count > 0 ? " (" + string.Join(" | ", stale.Take(20)) + ")" : "")}";
        }

        private static void Add(SortedSet<string> keys, string text)
        {
            // Not version placeholders like "v0.1".
            if (!string.IsNullOrWhiteSpace(text) && text.Any(char.IsLetter) && !Regex.IsMatch(text, "^v\\d"))
            {
                keys.Add(text);
            }
        }
    }
}
