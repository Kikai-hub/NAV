using UnityEngine;
using NAV.Core.Settings;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// A looping 3D sound on a placed object - a campfire's crackle, a forge's roar - heard within
    /// MaxDistance, at the Ambience volume setting. Starts in Start (a build ghost has its
    /// behaviours switched off before then, so a preview stays silent) at a random point of the
    /// loop, so two fires never crackle in step.
    /// </summary>
    public class AmbientEmitter : MonoBehaviour
    {
        [SerializeField] private AudioClip _clip;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.6f;
        [SerializeField, Min(0.1f)] private float _minDistance = 1.5f;
        [SerializeField, Min(1f)] private float _maxDistance = 20f;

        private AudioSource _source;

        private void Start()
        {
            if (_clip == null)
            {
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.clip = _clip;
            _source.loop = true;
            _source.playOnAwake = false;
            _source.spatialBlend = 1f;
            _source.rolloffMode = AudioRolloffMode.Linear;
            _source.minDistance = _minDistance;
            _source.maxDistance = _maxDistance;
            _source.dopplerLevel = 0f;
            _source.volume = _volume * GameSettings.GetVolume(AudioCategory.Ambience);
            _source.time = Random.Range(0f, _clip.length * 0.9f);
            _source.Play();
            GameSettings.Changed += ApplyVolume;
        }

        private void OnDestroy()
        {
            GameSettings.Changed -= ApplyVolume;
        }

        private void OnDisable()
        {
            if (_source != null)
            {
                _source.Stop();
            }
        }

        private void OnEnable()
        {
            if (_source != null && !_source.isPlaying)
            {
                _source.Play();
            }
        }

        private void ApplyVolume()
        {
            if (_source != null)
            {
                _source.volume = _volume * GameSettings.GetVolume(AudioCategory.Ambience);
            }
        }
    }
}
