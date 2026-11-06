using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Environment
{
    /// <summary>
    /// Something that warms the air around it - a campfire, a forge, a furnace. Within Radius the
    /// cold doesn't bite (PlayerCold). Registered while enabled; a placed building piece only counts
    /// once it is placed (not its ghost).
    /// </summary>
    public class HeatSource : MonoBehaviour
    {
        private static readonly List<HeatSource> Active = new();

        [SerializeField, Min(0.5f)] private float _radius = 5f;

        private NAV.Gameplay.Building.BuildingPiece _piece;

        private void Awake()
        {
            _piece = GetComponentInParent<NAV.Gameplay.Building.BuildingPiece>();
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        /// <summary>True when a heat source warms the point.</summary>
        public static bool IsWarmAt(Vector3 position)
        {
            foreach (HeatSource source in Active)
            {
                if (source._piece != null && !source._piece.IsPlaced)
                {
                    continue;
                }

                if ((source.transform.position - position).sqrMagnitude <= source._radius * source._radius)
                {
                    return true;
                }
            }

            return false;
        }
    }
}
