using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Combat;

namespace NAV.Presentation.Combat
{
    /// <summary>
    /// Which whooshes each weapon class swings with, and how HitFeedback times them so the
    /// whoosh's loudest moment lands on the strike (the weapon's fastest motion):
    ///  - an attack style with a fixed strike moment (Style Timings - the two-handed chop always
    ///    strikes at the same point of its clip) starts its whoosh during the backswing, so the
    ///    build-up is heard and the peak hits the strike;
    ///  - every other style (one-handed combos and the spear thrust, whose variants strike at
    ///    different moments) plays it when the animation actually starts striking, skipping the
    ///    part of the clip before its peak.
    /// Pure presentation data - gameplay never reads it.
    /// </summary>
    [CreateAssetMenu(fileName = "SwingSoundSet", menuName = "NAV/Audio/Swing Sound Set")]
    public class SwingSoundSet : ScriptableObject
    {
        [Serializable]
        public class Entry
        {
            public WeaponClass WeaponClass;
            [Tooltip("Picked at random per swing.")]
            public AudioClip[] Clips = new AudioClip[0];
            [Range(0f, 1f)] public float Volume = 0.6f;
            [Tooltip("Base pitch - lower for heavier weapons of the same clip family.")]
            [Range(0.5f, 1.5f)] public float Pitch = 1f;
        }

        [Serializable]
        public class StyleTiming
        {
            public WeaponAttackStyle Style;
            [Tooltip("Seconds from attack start to the strike at Attack Animation Speed 1 (divided by the weapon's speed at runtime).")]
            [Min(0f)] public float StrikeTime;
        }

        [SerializeField] private List<Entry> _entries = new();
        [SerializeField] private List<StyleTiming> _styleTimings = new();
        [Tooltip("How far into the strike the whoosh's peak lands (s).")]
        [SerializeField, Min(0f)] private float _strikeLead = 0.04f;
        [SerializeField, Range(0f, 0.3f)] private float _pitchJitter = 0.06f;

        public IReadOnlyList<Entry> Entries => _entries;
        public float StrikeLead => _strikeLead;
        public float PitchJitter => _pitchJitter;

        public Entry Get(WeaponClass weaponClass)
        {
            foreach (Entry entry in _entries)
            {
                if (entry.WeaponClass == weaponClass && entry.Clips.Length > 0)
                {
                    return entry;
                }
            }

            return null;
        }

        /// <summary>Seconds from attack start to the strike for this weapon, or 0 when the style
        /// has no fixed strike moment (play at the real strike instead).</summary>
        public float GetStrikeTime(WeaponDefinition weapon)
        {
            foreach (StyleTiming timing in _styleTimings)
            {
                if (timing.Style == weapon.AttackStyle)
                {
                    return timing.StrikeTime / Mathf.Max(weapon.AttackAnimationSpeed, 0.01f);
                }
            }

            return 0f;
        }
    }
}
