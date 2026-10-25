using UnityEngine;
using NAV.Core.Settings;
using NAV.Gameplay.Building;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.Presentation.Audio
{
    /// <summary>
    /// The player's footsteps, in step with the animation: a step sounds at each heel strike - a
    /// foot bone that came down suddenly stops falling (not a fixed ground height: the rig's feet
    /// rest at different heights, and differently when sprinting), so walking, sprinting and
    /// turning all match what the legs do. None while swimming; a heavier one on landing. Without a humanoid
    /// rig it falls back to a step every StrideSeconds of moving. The sound
    /// follows what is underfoot (FootstepProfile): a building piece -> wood, a rock node ->
    /// stone, shallow water -> wading, the terrain -> the clips for its strongest ground texture
    /// there (grass on the plains, forest floor, sand on the shore). Never the same clip twice in
    /// a row, a little pitch and volume variation, Effects volume setting.
    /// </summary>
    [RequireComponent(typeof(PlayerMotor))]
    public class FootstepAudio : MonoBehaviour
    {
        [SerializeField] private FootstepProfile _profile;
        [Tooltip("A foot must come down at least this far (m) for its stop to count as a step.")]
        [SerializeField, Min(0.01f)] private float _liftHeight = 0.08f;
        [Tooltip("...falling at least this fast (m/s) on the way - a shuffle isn't a step.")]
        [SerializeField, Min(0.05f)] private float _minDescentSpeed = 0.6f;

        // The step sounds when the foot's fall has slowed below this share of its fastest.
        private const float StrikeSlowdown = 0.3f;
        [Tooltip("No humanoid rig: seconds between steps while moving.")]
        [SerializeField, Min(0.1f)] private float _strideSeconds = 0.5f;
        [SerializeField, Range(0f, 1f)] private float _volume = 0.45f;
        [SerializeField, Range(0f, 1f)] private float _sprintVolume = 0.6f;
        [SerializeField, Range(0f, 1f)] private float _landingVolume = 0.75f;
        [Tooltip("Water deeper than this (m) at the feet counts as wading.")]
        [SerializeField, Min(0f)] private float _wadeDepth = 0.08f;

        private class Foot
        {
            public Transform Bone;
            public float Previous = float.NaN;
            public float Peak = float.MinValue;
            public float FastestDescent;
        }

        private PlayerMotor _motor;
        private AudioSource _source;
        private Foot[] _feet;
        private float _sinceStep;
        private bool _wasGrounded = true;
        private float _airTime;
        private AudioClip _lastClip;

        private void Awake()
        {
            _motor = GetComponent<PlayerMotor>();
            if (_profile == null)
            {
                Debug.LogError($"{nameof(FootstepAudio)} on '{name}' has no Footstep Profile.", this);
                enabled = false;
                return;
            }

            _source = gameObject.AddComponent<AudioSource>();
            _source.playOnAwake = false;
            _source.spatialBlend = 0.5f;
            _source.priority = 64;

            Animator animator = GetComponentInChildren<Animator>();
            if (animator != null && animator.isHuman)
            {
                _feet = new[]
                {
                    new Foot { Bone = animator.GetBoneTransform(HumanBodyBones.LeftFoot) },
                    new Foot { Bone = animator.GetBoneTransform(HumanBodyBones.RightFoot) },
                };
            }
        }

        // After the Animator has posed the legs this frame.
        private void LateUpdate()
        {
            bool grounded = _motor.IsGrounded && !_motor.IsSwimming;
            if (!grounded)
            {
                _airTime += Time.deltaTime;
                _wasGrounded = false;
                return;
            }

            if (!_wasGrounded)
            {
                // Landing after a real fall or jump, not stepping off a pebble.
                if (_airTime > 0.25f)
                {
                    Play(_landingVolume);
                }

                _wasGrounded = true;
            }

            _airTime = 0f;
            bool moving = _motor.CurrentSpeed >= 0.3f;
            float volume = _motor.IsSprinting ? _sprintVolume : _volume;

            if (_feet == null || _feet[0].Bone == null || _feet[1].Bone == null)
            {
                _sinceStep += Time.deltaTime;
                if (moving && _sinceStep >= _strideSeconds * (_motor.IsSprinting ? 0.7f : 1f))
                {
                    _sinceStep = 0f;
                    Play(volume);
                }

                return;
            }

            float root = transform.position.y;
            float deltaTime = Mathf.Max(Time.deltaTime, 1e-4f);
            foreach (Foot foot in _feet)
            {
                float height = foot.Bone.position.y - root;
                if (float.IsNaN(foot.Previous))
                {
                    foot.Previous = height;
                    foot.Peak = height;
                    continue;
                }

                float velocity = (height - foot.Previous) / deltaTime;
                foot.Previous = height;
                foot.Peak = Mathf.Max(foot.Peak, height);
                foot.FastestDescent = Mathf.Min(foot.FastestDescent, velocity);

                // Heel strike: a foot that came down from at least LiftHeight above where it is
                // now suddenly stops falling (below a third of its fastest descent). Height-free,
                // so walk, sprint and slopes - where the feet rest at different heights - all work.
                bool cameDown = foot.Peak - height >= _liftHeight && foot.FastestDescent < -_minDescentSpeed;
                if (cameDown && velocity > foot.FastestDescent * StrikeSlowdown)
                {
                    foot.Peak = height;
                    foot.FastestDescent = 0f;
                    if (moving)
                    {
                        Play(volume);
                    }
                }
            }
        }

        private void Play(float volume)
        {
            AudioClip[] clips = SurfaceClips();
            if (clips == null || clips.Length == 0)
            {
                return;
            }

            AudioClip clip = clips[Random.Range(0, clips.Length)];
            if (clip == _lastClip && clips.Length > 1)
            {
                clip = clips[(System.Array.IndexOf(clips, clip) + 1) % clips.Length];
            }

            _lastClip = clip;
            _source.pitch = Random.Range(0.92f, 1.08f);
            _source.PlayOneShot(clip, volume * Random.Range(0.85f, 1f) * GameSettings.GetVolume(AudioCategory.Effects));
        }

        private AudioClip[] SurfaceClips()
        {
            if (_motor.WaterDepth > _wadeDepth)
            {
                return _profile.Water;
            }

            Vector3 origin = transform.position + Vector3.up * 0.5f;
            if (!Physics.Raycast(origin, Vector3.down, out RaycastHit hit, 1.5f, ~0, QueryTriggerInteraction.Ignore))
            {
                return _profile.Default;
            }

            if (hit.collider.GetComponentInParent<BuildingPiece>() != null)
            {
                return _profile.Wood;
            }

            ResourceNode node = hit.collider.GetComponentInParent<ResourceNode>();
            if (node != null && node.Definition != null && node.Definition.RequiredTool == ToolType.Pickaxe)
            {
                return _profile.Stone;
            }

            if (hit.collider is TerrainCollider terrainCollider && terrainCollider.TryGetComponent(out Terrain terrain))
            {
                return _profile.ForTerrainLayer(DominantLayer(terrain, hit.point));
            }

            return _profile.Default;
        }

        private static TerrainLayer DominantLayer(Terrain terrain, Vector3 point)
        {
            TerrainData data = terrain.terrainData;
            Vector3 local = point - terrain.transform.position;
            int x = Mathf.Clamp(Mathf.FloorToInt(local.x / data.size.x * data.alphamapWidth), 0, data.alphamapWidth - 1);
            int z = Mathf.Clamp(Mathf.FloorToInt(local.z / data.size.z * data.alphamapHeight), 0, data.alphamapHeight - 1);
            float[,,] weights = data.GetAlphamaps(x, z, 1, 1);
            int best = 0;
            for (int i = 1; i < weights.GetLength(2); i++)
            {
                if (weights[0, 0, i] > weights[0, 0, best])
                {
                    best = i;
                }
            }

            TerrainLayer[] layers = data.terrainLayers;
            return best < layers.Length ? layers[best] : null;
        }
    }
}
