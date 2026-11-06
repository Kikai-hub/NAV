using System;
using UnityEngine;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// Every sound of the Leshy (boss 1), one slot per event - drag real clips in to replace the
    /// placeholders NAV > Audio > Build Leshy Sounds made. Each slot holds variants (one is picked
    /// at random, with a small random pitch), a volume, and how far it is heard. Played by
    /// LeshyAudio (on the boss) and ForestWrathAudio (the omens, before he exists).
    /// </summary>
    [CreateAssetMenu(fileName = "LeshySoundSet", menuName = "NAV/Audio/Leshy Sound Set")]
    public class LeshySoundSet : ScriptableObject
    {
        [Serializable]
        public class Slot
        {
            public AudioClip[] Clips = new AudioClip[0];
            [Range(0f, 1f)] public float Volume = 1f;
            [Tooltip("Random pitch range (around 1 = as recorded).")]
            public Vector2 Pitch = new(0.95f, 1.05f);
            [Tooltip("Metres at which it fades out completely.")]
            [Min(5f)] public float MaxDistance = 60f;

            public bool IsEmpty => Clips == null || Clips.Length == 0;

            public AudioClip Pick()
            {
                return IsEmpty ? null : Clips[UnityEngine.Random.Range(0, Clips.Length)];
            }
        }

        [Header("Before he comes")]
        [Tooltip("A whistle, a creak, an echo from the thicket - the angry forest's omen.")]
        public Slot Omen = new() { Volume = 0.8f, MaxDistance = 120f };
        [Tooltip("The omen of a peaceful Leshy - softer.")]
        public Slot PeacefulOmen = new() { Volume = 0.5f, MaxDistance = 120f };

        [Header("While he is about")]
        [Tooltip("A loop round him: creaking trunk, rustling leaves, a low hum.")]
        public Slot Presence = new() { Volume = 0.55f, Pitch = Vector2.one, MaxDistance = 45f };
        public Slot Footstep = new() { Volume = 0.8f, Pitch = new Vector2(0.85f, 1f), MaxDistance = 55f };

        [Header("Fight")]
        public Slot Roar = new() { Volume = 1f, Pitch = new Vector2(0.92f, 1.02f), MaxDistance = 140f };
        [Tooltip("Wood straining as both arms go up.")]
        public Slot SlamWindup = new() { Volume = 0.8f, MaxDistance = 60f };
        [Tooltip("The arms hitting the ground: boom, crack, earth.")]
        public Slot SlamImpact = new() { Volume = 1f, Pitch = new Vector2(0.9f, 1f), MaxDistance = 90f };
        public Slot SweepWhoosh = new() { Volume = 0.9f, Pitch = new Vector2(0.75f, 0.85f), MaxDistance = 45f };
        [Tooltip("A groan of wood when a blow makes him flinch.")]
        public Slot Hurt = new() { Volume = 0.7f, MaxDistance = 50f };

        [Header("Death")]
        [Tooltip("The long creak of a falling tree.")]
        public Slot DeathCreak = new() { Volume = 1f, Pitch = new Vector2(0.95f, 1f), MaxDistance = 120f };
        [Tooltip("The body hitting the ground.")]
        public Slot DeathImpact = new() { Volume = 1f, Pitch = new Vector2(0.9f, 0.95f), MaxDistance = 100f };
    }
}
