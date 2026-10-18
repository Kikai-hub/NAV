using System;
using UnityEngine;

namespace NAV.World.TimeOfDay
{
    /// <summary>
    /// The world's in-game time: current Day (starting at 1) and Hour (0..24), advanced in
    /// Update at TimeOfDaySettings.HoursPerSecond. Uses scaled time, so the pause menu
    /// (Time.timeScale = 0) stops the clock too. Pure state + events - DayNightLighting
    /// (Presentation) renders it, SaveManager persists it, and future systems (creature night
    /// behaviour, weather) can listen to NightChanged/DayChanged instead of polling.
    /// </summary>
    public class WorldClock : MonoBehaviour
    {
        [SerializeField] private TimeOfDaySettings _settings;

        [Tooltip("Debug: multiplies how fast time passes (e.g. 30 to watch a full day in under a minute). Leave at 1 for normal play.")]
        [SerializeField, Min(0f)] private float _timeMultiplier = 1f;

        private bool _wasNight;

        public TimeOfDaySettings Settings => _settings;
        public int Day { get; private set; } = 1;
        public float Hour { get; private set; }

        /// <summary>0 at 00:00 .. 1 at 24:00.</summary>
        public float NormalizedTime => Hour / 24f;
        public bool IsNight => !_settings.IsDaytime(Hour);
        public int Minute => Mathf.FloorToInt((Hour - Mathf.Floor(Hour)) * 60f);

        /// <summary>Tonight's moon phase, 0 .. Settings.MoonPhaseCount-1 (derived from Day, so
        /// saving Day/Hour already persists it).</summary>
        public int MoonPhaseIndex => _settings.GetMoonPhaseIndex(Day, Hour);

        /// <summary>Tonight's position in the lunar cycle: 0 = new, 0.5 = full.</summary>
        public float MoonCycle => _settings.GetMoonCycle(MoonPhaseIndex);

        /// <summary>Fires with true when night begins (sunset) and false when day begins
        /// (sunrise).</summary>
        public event Action<bool> NightChanged;

        /// <summary>Fires with the new day number when the clock passes 24:00.</summary>
        public event Action<int> DayChanged;

        private void Awake()
        {
            if (_settings == null)
            {
                Debug.LogError($"{nameof(WorldClock)} on '{name}' is missing its Settings.", this);
                enabled = false;
                return;
            }

            Hour = _settings.NewGameStartHour;
            _wasNight = IsNight;
        }

        private void Update()
        {
            Advance(UnityEngine.Time.deltaTime * _settings.HoursPerSecond * _timeMultiplier);
        }

        /// <summary>Jumps straight to a given day/hour (load, debug). Fires NightChanged if the
        /// jump crosses sunrise/sunset, but not DayChanged.</summary>
        public void SetTime(int day, float hour)
        {
            Day = Mathf.Max(1, day);
            Hour = Mathf.Repeat(hour, 24f);
            CheckNightChanged();
        }

        [ContextMenu("Skip 1 Hour")]
        private void SkipHour() => Advance(1f);

        [ContextMenu("Skip 6 Hours")]
        private void SkipSixHours() => Advance(6f);

        [ContextMenu("Skip 1 Day (next moon phase)")]
        private void SkipDay() => Advance(24f);

        private void Advance(float hours)
        {
            if (hours <= 0f || _settings == null)
            {
                return;
            }

            Hour += hours;
            while (Hour >= 24f)
            {
                Hour -= 24f;
                Day++;
                DayChanged?.Invoke(Day);
            }

            CheckNightChanged();
        }

        private void CheckNightChanged()
        {
            bool night = IsNight;
            if (night != _wasNight)
            {
                _wasNight = night;
                NightChanged?.Invoke(night);
            }
        }
    }
}
