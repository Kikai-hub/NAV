using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// The sea surface height and the round world's bounds, published by WorldGenerator when it
    /// builds a world - so gameplay (boats, building on water) can read them without depending on
    /// world generation.
    /// </summary>
    public static class WorldWater
    {
        /// <summary>World-space height of the sea surface; NegativeInfinity before a world exists.</summary>
        public static float SeaHeight { get; set; } = float.NegativeInfinity;

        public static bool HasSea => !float.IsNegativeInfinity(SeaHeight);

        /// <summary>Centre and radius of the round world (land and coastal sea); 0 = unknown.</summary>
        public static Vector3 WorldCentre { get; set; }
        public static float WorldRadius { get; set; }
    }
}
