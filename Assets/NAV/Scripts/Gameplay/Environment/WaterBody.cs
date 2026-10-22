using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Environment
{
    /// <summary>
    /// A flat body of water: its surface is this transform's height, its extent the XZ bounds of
    /// its renderer (the world water plane WorldGenerator places at sea level). Not solid - the
    /// player wades and swims in it (PlayerMotor), the camera stays above it, and the water
    /// reacts to what moves in it (WaterInteraction). Query any position with TryGetSurface.
    /// </summary>
    public class WaterBody : MonoBehaviour
    {
        [Tooltip("Defines the water's XZ extent. Defaults to this object's Renderer.")]
        [SerializeField] private Renderer _surface;

        private static readonly List<WaterBody> Active = new();

        public float SurfaceHeight => transform.position.y;

        private void Awake()
        {
            if (_surface == null)
            {
                _surface = GetComponent<Renderer>();
            }
        }

        private void OnEnable()
        {
            Active.Add(this);
        }

        private void OnDisable()
        {
            Active.Remove(this);
        }

        public bool ContainsXZ(Vector3 position)
        {
            if (_surface == null)
            {
                return true;
            }

            Bounds bounds = _surface.bounds;
            return position.x >= bounds.min.x && position.x <= bounds.max.x
                && position.z >= bounds.min.z && position.z <= bounds.max.z;
        }

        /// <summary>Height of the water surface above/below position, if any water body covers
        /// it (the highest one when several do).</summary>
        public static bool TryGetSurface(Vector3 position, out float height)
        {
            height = float.MinValue;
            foreach (WaterBody water in Active)
            {
                if (water.ContainsXZ(position) && water.SurfaceHeight > height)
                {
                    height = water.SurfaceHeight;
                }
            }

            return height > float.MinValue;
        }

        /// <summary>How deep position is under water (0 when above the surface or outside any
        /// water).</summary>
        public static float GetDepth(Vector3 position)
        {
            return TryGetSurface(position, out float height) ? Mathf.Max(0f, height - position.y) : 0f;
        }
    }
}
