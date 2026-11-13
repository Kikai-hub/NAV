using System;
using System.IO;
using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// The project was renamed on 2026-11-13 (company "DefaultCompany" -> "FakeStudios", product
    /// "NAVъ" -> "NAV"), which moves Application.persistentDataPath from
    /// LocalLow/DefaultCompany/NAVъ to LocalLow/FakeStudios/NAV. On the first run under the new name
    /// this copies settings.json and every file of Saves/ across, so nobody's worlds or settings
    /// vanish. Nothing is overwritten and the old folder is left as it was; a marker file in the new
    /// folder stops it from running again. Called by GameSettings before the settings load (that is,
    /// before the first scene).
    /// </summary>
    public static class LegacyDataMigration
    {
        private const string LegacyCompany = "DefaultCompany";
        private const string LegacyProduct = "NAVъ";
        private const string MarkerFile = "migrated_from_DefaultCompany.txt";

        private static bool _done;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            _done = false;
        }

        public static void Run()
        {
            if (_done)
            {
                return;
            }

            _done = true;
            try
            {
                string current = Application.persistentDataPath;
                DirectoryInfo companyDir = Directory.GetParent(current);
                if (companyDir?.Parent == null)
                {
                    return;
                }

                string legacy = Path.Combine(companyDir.Parent.FullName, LegacyCompany, LegacyProduct);
                if (!Directory.Exists(legacy)
                    || string.Equals(Path.GetFullPath(legacy).TrimEnd('\\', '/'), Path.GetFullPath(current).TrimEnd('\\', '/'), StringComparison.OrdinalIgnoreCase)
                    || File.Exists(Path.Combine(current, MarkerFile)))
                {
                    return;
                }

                Directory.CreateDirectory(current);
                int copied = CopyIfMissing(Path.Combine(legacy, "settings.json"), Path.Combine(current, "settings.json")) ? 1 : 0;
                string legacySaves = Path.Combine(legacy, "Saves");
                if (Directory.Exists(legacySaves))
                {
                    string saves = Path.Combine(current, "Saves");
                    Directory.CreateDirectory(saves);
                    foreach (string file in Directory.GetFiles(legacySaves))
                    {
                        copied += CopyIfMissing(file, Path.Combine(saves, Path.GetFileName(file))) ? 1 : 0;
                    }
                }

                File.WriteAllText(Path.Combine(current, MarkerFile),
                    $"Copied {copied} file(s) (settings and saves) from {legacy} on {DateTime.Now:u}.\nThe old folder was left as it was.");
                Debug.Log($"{nameof(LegacyDataMigration)}: copied {copied} file(s) from the old data folder {legacy} to {current}.");
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(LegacyDataMigration)}: couldn't copy the old saves/settings ({e.Message}). They are still in LocalLow/{LegacyCompany}/{LegacyProduct}.");
            }
        }

        private static bool CopyIfMissing(string from, string to)
        {
            if (!File.Exists(from) || File.Exists(to))
            {
                return false;
            }

            File.Copy(from, to);
            return true;
        }
    }
}
