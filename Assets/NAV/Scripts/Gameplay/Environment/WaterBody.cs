using System.Collections.Generic;
using NAV.Core;
using UnityEngine;

namespace NAV.Gameplay.Environment
{
    /// <summary>
    /// A body of water: its still surface is this transform's height, its extent the XZ bounds of
    /// its renderer (or everywhere, for the endless sea). The sea has waves (Has Waves - NAV.Core.
    /// WaveField): the surface under a point rises and falls with them. Not solid - the player
    /// wades and swims in it (PlayerMotor), the camera stays above it, and the water reacts to
    /// what moves in it (WaterInteraction). Query any position with TryGetSurface.
    /// </summary>
    public class WaterBody : MonoBehaviour
    {
        [Tooltip("Defines the water's XZ extent. Defaults to this object's Renderer.")]
        [SerializeField] private Renderer _surface;
        [Tooltip("Covers every XZ position (the sea around the world) - the renderer's bounds are ignored.")]
        [SerializeField] private bool _endless;
        [Tooltip("The surface moves with NAV.Core.WaveField's waves (the sea). Off for still water.")]
        [SerializeField] private bool _hasWaves;

        private static readonly List<WaterBody> Active = new();

        public float SurfaceHeight => transform.position.y;

        public bool Endless { get => _endless; set => _endless = value; }
        public bool HasWaves { get => _hasWaves; set => _hasWaves = value; }

        /// <summary>The surface height at a point, waves included.</summary>
        public float SurfaceAt(Vector3 position) => _hasWaves ? SurfaceHeight + WaveField.HeightAt(position.x, position.z) : SurfaceHeight;

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
            if (_endless || _surface == null)
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
                if (!water.ContainsXZ(position))
                {
                    continue;
                }

                float surface = water.SurfaceAt(position);
                if (surface > height)
                {
                    height = surface;
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
