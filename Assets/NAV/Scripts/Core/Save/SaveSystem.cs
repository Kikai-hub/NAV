using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace NAV.Core.Save
{
    /// <summary>
    /// Plain file I/O for save games (JsonUtility, one file per save slot under
    /// Application.persistentDataPath/Saves/) plus the small bit of state that has to survive a
    /// scene load (MainMenu -> gameplay scene) without a DontDestroyOnLoad object: which save (if
    /// any) is pending a load, and which save id/seed the current play session is writing
    /// autosaves to. Static fields survive SceneManager.LoadScene within the same Play session
    /// (they only reset on a fresh domain reload, i.e. actually stopping Play or a build restart),
    /// which is the whole point here - see SaveManager, the only thing that reads/writes these.
    ///
    /// A static class, not a MonoBehaviour/singleton - nothing here needs a GameObject or a
    /// per-frame Update, so a persistent scene object would just be unnecessary global state (see
    /// CLAUDE.md's Architecture Rules).
    /// </summary>
    public static class SaveSystem
    {
        private const string SaveFolderName = "Saves";

        /// <summary>Set by MainMenuUIController's Load list before loading the gameplay scene;
        /// null means "start a new game instead". SaveManager reads and immediately clears this
        /// on Start.</summary>
        public static string PendingLoadSaveId { get; set; }

        /// <summary>Name/seed chosen on the Create World screen, set together right before the
        /// gameplay scene loads (only meaningful when PendingLoadSaveId is null). SaveManager
        /// reads and immediately clears PendingNewGameName on Start, same pattern as
        /// PendingLoadSaveId - an empty/null name means "no Create World screen was used" (e.g.
        /// testing the gameplay scene directly), which SaveManager falls back to a fully random
        /// seed and a default name for.</summary>
        public static string PendingNewGameName { get; set; }
        public static int PendingNewGameSeed { get; set; }

        /// <summary>The save slot this play session's autosaves overwrite. Set once by
        /// SaveManager (new random id for a new game, or the loaded save's own id) and never
        /// changed after - this is what makes "the world saves into its own old save" true: every
        /// autosave targets the same file.</summary>
        public static string CurrentSaveId { get; set; }

        public static int CurrentSeed { get; set; }
        public static string CurrentWorldName { get; set; }

        private static string SaveFolder => Path.Combine(Application.persistentDataPath, SaveFolderName);

        public static void Save(SaveGameData data)
        {
            if (data == null || string.IsNullOrEmpty(data.SaveId))
            {
                Debug.LogError($"{nameof(SaveSystem)}.Save was called with no SaveId - refusing to write an unaddressable file.");
                return;
            }

            Directory.CreateDirectory(SaveFolder);
            string json = JsonUtility.ToJson(data, true);
            File.WriteAllText(GetPath(data.SaveId), json);
        }

        /// <summary>Permanently deletes one save file. Safe to call with an id that doesn't
        /// exist (no-op) - callers don't need to check File.Exists themselves first.</summary>
        public static void DeleteSave(string saveId)
        {
            string path = GetPath(saveId);
            if (File.Exists(path))
            {
                File.Delete(path);
            }
        }

        public static SaveGameData Load(string saveId)
        {
            string path = GetPath(saveId);
            if (!File.Exists(path))
            {
                Debug.LogError($"{nameof(SaveSystem)}: no save file for id '{saveId}' at '{path}'.");
                return null;
            }

            return JsonUtility.FromJson<SaveGameData>(File.ReadAllText(path));
        }

        /// <summary>All saves on disk, newest first - what MainMenu's Load list displays.</summary>
        public static List<SaveGameData> ListSaves()
        {
            var result = new List<SaveGameData>();
            if (!Directory.Exists(SaveFolder))
            {
                return result;
            }

            foreach (string file in Directory.GetFiles(SaveFolder, "*.json"))
            {
                try
                {
                    SaveGameData data = JsonUtility.FromJson<SaveGameData>(File.ReadAllText(file));
                    if (data != null)
                    {
                        result.Add(data);
                    }
                }
                catch (Exception e)
                {
                    // One corrupt file shouldn't hide every other save - log loudly (per CLAUDE.md's
                    // Error Handling rule) and skip just that one.
                    Debug.LogError($"{nameof(SaveSystem)}: failed to read save file '{file}': {e.Message}");
                }
            }

            result.Sort((a, b) => b.SavedAtUnixSeconds.CompareTo(a.SavedAtUnixSeconds));
            return result;
        }

        private static string GetPath(string saveId)
        {
            return Path.Combine(SaveFolder, saveId + ".json");
        }
    }
}
