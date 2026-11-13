using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Tutorial
{
    /// <summary>
    /// Which first-steps hints this world has already finished (STEAM_DEMO_PLAN.md stage 3,
    /// 2026-11-13). Saved with the world (SaveGameData.CompletedHints): a new world starts the hints
    /// again, a loaded one carries on. HintsUI decides what is shown; this only remembers.
    /// </summary>
    public static class TutorialProgress
    {
        private static readonly HashSet<string> Done = new();

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            Done.Clear();
        }

        public static bool IsDone(string id) => Done.Contains(id);

        public static void MarkDone(string id)
        {
            if (!string.IsNullOrEmpty(id))
            {
                Done.Add(id);
            }
        }

        /// <summary>For the save file.</summary>
        public static List<string> Save() => new(Done);

        /// <summary>A loaded world's finished hints (null = a new world, none finished).</summary>
        public static void Restore(IEnumerable<string> ids)
        {
            Done.Clear();
            if (ids == null)
            {
                return;
            }

            foreach (string id in ids)
            {
                MarkDone(id);
            }
        }
    }
}
