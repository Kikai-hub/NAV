using System;
using NAV.Core;
using NAV.Gameplay.Environment;
using NAV.Gameplay.Equipment;
using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// The cold of the mountains (tier 5). Where the biome's Cold (WorldClimate) is above what the
    /// player wears (PlayerEquipment.TotalWarmth - a fur cloak, a fur hood) and no fire is near
    /// (HeatSource), the player is Cold; after Freeze Delay of that they are Freezing and lose
    /// health every second (so regeneration stops too). Warmth from clothes or a fire ends it at
    /// once; leaving the cold lets the chill wear off over Freeze Delay. In water the cold bites
    /// as soon as the biome is cold at all. The HUD shows the state (Changed). Not saved.
    /// </summary>
    [RequireComponent(typeof(PlayerHealth))]
    public class PlayerCold : MonoBehaviour
    {
        public enum ColdState
        {
            Warm,
            Cold,
            Freezing,
        }

        [SerializeField] private PlayerEquipment _equipment;
        [Tooltip("Seconds of being cold before it turns to freezing.")]
        [SerializeField, Min(1f)] private float _freezeDelay = 20f;
        [Tooltip("Health lost per second while freezing.")]
        [SerializeField, Min(0f)] private float _freezingDamagePerSecond = 1.5f;
        [Tooltip("How often (s) the cold is checked.")]
        [SerializeField, Min(0.1f)] private float _checkInterval = 0.5f;

        private PlayerHealth _health;
        private PlayerMotor _motor;
        private float _chill;
        private float _nextCheck;
        private bool _exposed;

        public ColdState State { get; private set; }

        /// <summary>Raised when the state changes (warm / cold / freezing).</summary>
        public event Action Changed;

        private void Awake()
        {
            _health = GetComponent<PlayerHealth>();
            _motor = GetComponent<PlayerMotor>();
            if (_equipment == null)
            {
                _equipment = GetComponent<PlayerEquipment>();
            }
        }

        private void Update()
        {
            if (!_health.IsAlive)
            {
                _chill = 0f;
                SetState(ColdState.Warm);
                return;
            }

            if (Time.time >= _nextCheck)
            {
                _nextCheck = Time.time + _checkInterval;
                _exposed = IsExposed();
            }

            _chill = Mathf.Clamp(_chill + (_exposed ? Time.deltaTime : -Time.deltaTime), 0f, _freezeDelay);
            ColdState state = !_exposed && _chill <= 0f ? ColdState.Warm
                : _chill >= _freezeDelay ? ColdState.Freezing
                : _exposed ? ColdState.Cold
                : ColdState.Warm;
            SetState(state);

            if (State == ColdState.Freezing)
            {
                _health.TakeDamage(_freezingDamagePerSecond * Time.deltaTime);
            }
        }

        private bool IsExposed()
        {
            Vector3 position = transform.position;
            float cold = WorldClimate.ColdAt(position);
            if (cold <= 0f || HeatSource.IsWarmAt(position))
            {
                return false;
            }

            // Wet clothes don't keep anyone warm.
            bool wet = _motor != null && _motor.IsSwimming;
            float warmth = _equipment != null && !wet ? _equipment.TotalWarmth : 0f;
            return cold > warmth + 0.001f;
        }

        private void SetState(ColdState state)
        {
            if (state == State)
            {
                return;
            }

            State = state;
            Changed?.Invoke();
        }
    }
}
