using System.Collections.Generic;
using UnityEngine;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// Which footstep sounds go with which ground (FootstepAudio): per terrain layer (the biome
    /// ground textures), and for building pieces (wood), rock (gathered stone), shallow water and
    /// anything else.
    /// </summary>
    [CreateAssetMenu(fileName = "FootstepProfile", menuName = "NAV/Audio/Footstep Profile")]
    public class FootstepProfile : ScriptableObject
    {
        [System.Serializable]
        public class TerrainSurface
        {
            public TerrainLayer Layer;
            public AudioClip[] Clips = new AudioClip[0];
        }

        [SerializeField] private List<TerrainSurface> _terrain = new();
        [SerializeField] private AudioClip[] _wood = new AudioClip[0];
        [SerializeField] private AudioClip[] _stone = new AudioClip[0];
        [Tooltip("Wading in shallow water.")]
        [SerializeField] private AudioClip[] _water = new AudioClip[0];
        [Tooltip("Any other ground (and terrain layers not listed).")]
        [SerializeField] private AudioClip[] _default = new AudioClip[0];

        public AudioClip[] Wood => _wood;
        public AudioClip[] Stone => _stone;
        public AudioClip[] Water => _water;
        public AudioClip[] Default => _default;

        public AudioClip[] ForTerrainLayer(TerrainLayer layer)
        {
            foreach (TerrainSurface surface in _terrain)
            {
                if (surface.Layer == layer && surface.Clips.Length > 0)
                {
                    return surface.Clips;
                }
            }

            return _default;
        }
    }
}
