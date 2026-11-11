using System.Collections.Generic;
using UnityEngine;

namespace NAV.Core
{
    /// <summary>
    /// The other players online (NetworkAvatar adds its avatar): the map shows them as dots with
    /// names. They don't reveal this player's map - each player explores their own (developer,
    /// 2026-11-10). Neither the network nor the map knows the other.
    /// </summary>
    public static class MapExplorers
    {
        public sealed class Explorer
        {
            public Transform Transform;
            public string Name;
        }

        private static readonly List<Explorer> List = new();

        public static IReadOnlyList<Explorer> All => List;

        public static void Add(Transform transform, string name)
        {
            Remove(transform);
            List.Add(new Explorer { Transform = transform, Name = name });
        }

        public static void Remove(Transform transform)
        {
            List.RemoveAll(e => e.Transform == null || e.Transform == transform);
        }
    }
}
