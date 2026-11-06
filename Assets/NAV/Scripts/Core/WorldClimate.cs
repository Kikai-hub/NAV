using System;
using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// How cold the world is at a point (0 = mild, 1 = cold - mountains, 1.5 = bitter - snowy
    /// peaks, permafrost), published by WorldGenerator from the biomes so gameplay (PlayerCold)
    /// can ask without depending on world generation - like WorldWater.
    /// </summary>
    public static class WorldClimate
    {
        /// <summary>Set by WorldGenerator; null before a world exists (no cold anywhere).</summary>
        public static Func<Vector3, float> ColdProvider { get; set; }

        public static float ColdAt(Vector3 position) => ColdProvider != null ? ColdProvider(position) : 0f;
    }
}
