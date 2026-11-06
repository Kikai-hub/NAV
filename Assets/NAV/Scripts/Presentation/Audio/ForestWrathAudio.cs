using System.Collections;
using UnityEngine;
using NAV.Core.Settings;
using NAV.Gameplay.Player;
using NAV.World.Encounters;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// The omens before the Leshy steps out (ForestWrath.OmenStarted): a sound from the thicket
    /// somewhere around the player - a whistle, a creak, an echo - twice, from different sides,
    /// while the forest's own life falls silent (AmbienceController reads ForestWrath.Silence).
    /// Softer for a peaceful Leshy. Presentation only.
    /// </summary>
    public class ForestWrathAudio : MonoBehaviour
    {
        [SerializeField] private ForestWrath _forestWrath;
        [SerializeField] private LeshySoundSet _sounds;
        [Tooltip("How far from the player (m) the omen sounds come from.")]
        [SerializeField] private Vector2 _distance = new(25f, 45f);
        [Tooltip("Seconds between the first and the second omen sound.")]
        [SerializeField, Min(0f)] private float _secondOmenDelay = 7f;

        private AudioSource _source;
        private Transform _player;

        private void Awake()
        {
            if (_forestWrath == null || _sounds == null)
            {
                Debug.LogError($"{nameof(ForestWrathAudio)} on '{name}' is missing its ForestWrath or Sound Set.", this);
                enabled = false;
                return;
            }

            var go = new GameObject("OmenSound");
            go.transform.SetParent(transform, false);
            _source = go.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = 10f;
            _source.dopplerLevel = 0f;
        }

        private void OnEnable()
        {
            if (_forestWrath != null)
            {
                _forestWrath.OmenStarted += HandleOmen;
            }
        }

        private void OnDisable()
        {
            if (_forestWrath != null)
            {
                _forestWrath.OmenStarted -= HandleOmen;
            }
        }

        private void HandleOmen(bool angry)
        {
            StopAllCoroutines();
            StartCoroutine(Omens(angry ? _sounds.Omen : _sounds.PeacefulOmen));
        }

        private IEnumerator Omens(LeshySoundSet.Slot slot)
        {
            // A moment of silence first - the forest goes quiet, then something answers.
            yield return new WaitForSeconds(2.5f);
            PlayAround(slot);
            yield return new WaitForSeconds(_secondOmenDelay);
            PlayAround(slot);
        }

        private void PlayAround(LeshySoundSet.Slot slot)
        {
            AudioClip clip = slot.Pick();
            if (clip == null)
            {
                return;
            }

            if (_player == null)
            {
                PlayerMotor motor = FindAnyObjectByType<PlayerMotor>();
                if (motor == null)
                {
                    return;
                }

                _player = motor.transform;
            }

            Vector2 direction = Random.insideUnitCircle.normalized;
            float distance = Random.Range(_distance.x, _distance.y);
            _source.transform.position = _player.position + new Vector3(direction.x, 0f, direction.y) * distance + Vector3.up * 4f;
            _source.maxDistance = Mathf.Max(slot.MaxDistance, distance * 2f);
            _source.pitch = Random.Range(slot.Pitch.x, slot.Pitch.y);
            _source.volume = slot.Volume * GameSettings.GetVolume(AudioCategory.Effects);
            _source.clip = clip;
            _source.Play();
        }
    }
}
