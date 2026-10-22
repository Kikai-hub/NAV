using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Items;
using NAV.Core.Settings;

namespace NAV.Presentation.Combat
{
    /// <summary>
    /// Makes melee hits readable (Roadmap Stage A "damage feedback"): observes PlayerCombat and
    /// answers every landed hit with a particle burst and a sound picked by the struck surface
    /// (IHitSurface), plus a reaction on the target itself - a creature flashes and squashes,
    /// and tips over when the hit kills it; a tree or rock shakes. Incoming hits on the player
    /// get a hurt / block / parry sound and burst, and every swing a whoosh.
    ///
    /// Purely Presentation: it reads PlayerCombat's events and moves/tints transforms and
    /// renderers only for a fraction of a second; gameplay never depends on it. The stagger and
    /// knockback themselves are gameplay (Creature.ReactToHit). All sounds are procedural
    /// placeholders (PlaceholderCombatAudio); particles are untextured URP Particles/Unlit quads.
    /// One scene object - see the "NAV/Setup/Add Hit Feedback To Scene" menu.
    /// </summary>
    // After PlayerCombat (default order 0), so a reaction started by this frame's hit is applied in
    // this frame's LateUpdate rather than one frame late.
    [DefaultExecutionOrder(100)]
    public class HitFeedback : MonoBehaviour
    {
        private const int ClipVariations = 3;
        private const string BaseColorProperty = "_BaseColor";
        private static readonly Color NeedleDark = new(0.16f, 0.3f, 0.14f);
        private static readonly Color NeedleLight = new(0.3f, 0.45f, 0.2f);

        [SerializeField] private PlayerCombat _playerCombat;
        [Tooltip("HitParticle.mat (URP Particles/Unlit) - a material asset rather than Shader.Find, so the shader is included in builds (same reason as WeatherEffects' Rain Material Template).")]
        [SerializeField] private Material _particleMaterialTemplate;

        [Header("Particles")]
        [SerializeField, Min(0)] private int _combatantParticles = 16;
        [SerializeField, Min(0)] private int _gatherParticles = 18;
        [SerializeField, Min(0)] private int _bouncedParticles = 4;
        [SerializeField, Min(0f)] private float _particleSpeed = 3.5f;
        [SerializeField, Min(0.01f)] private float _particleSize = 0.12f;

        [Header("Audio")]
        [SerializeField, Range(0f, 1f)] private float _volume = 0.8f;
        [Tooltip("Real swing whooshes per weapon class and their timing (see SwingSoundSet). A class without clips - or no set - uses the procedural placeholder whoosh at swing start.")]
        [SerializeField] private SwingSoundSet _swingSounds;
        [Tooltip("Volume of the placeholder whoosh (real whooshes use their Swing Sound Set volume).")]
        [SerializeField, Range(0f, 1f)] private float _swingVolume = 0.35f;
        [SerializeField, Range(0f, 0.3f)] private float _pitchJitter = 0.08f;
        [SerializeField, Min(1)] private int _voices = 10;

        [Header("Target Reaction")]
        [Tooltip("Tint a struck creature flashes to. Reddish rather than white: textured models (white base colour) wouldn't show a white flash.")]
        [SerializeField] private Color _flashColor = new(1f, 0.4f, 0.4f);
        [SerializeField, Min(0f)] private float _flashSeconds = 0.12f;
        [Tooltip("How much a struck creature squashes (fraction of its height).")]
        [SerializeField, Range(0f, 0.5f)] private float _punchAmount = 0.15f;
        [SerializeField, Min(0.01f)] private float _punchSeconds = 0.2f;
        [Tooltip("How far a struck tree/rock sways, degrees.")]
        [SerializeField, Min(0f)] private float _shakeDegrees = 2.5f;
        [SerializeField, Min(0.01f)] private float _shakeSeconds = 0.35f;
        [Tooltip("How long a killed creature takes to fall over.")]
        [SerializeField, Min(0.01f)] private float _deathFallSeconds = 0.5f;

        [Header("Tree Fall")]
        [Tooltip("Needle bursts spread up the felled tree's height, where its crown was.")]
        [SerializeField, Min(0)] private int _treeFelledBursts = 8;
        [SerializeField, Min(0)] private int _needlesPerBurst = 16;

        [Header("Player")]
        [Tooltip("Height above the player's feet where incoming-hit bursts appear.")]
        [SerializeField] private float _playerHitHeight = 1.2f;

        private ParticleSystem _particles;
        private AudioSource[] _sources;
        private int _nextSource;
        private MaterialPropertyBlock _propertyBlock;

        private AudioClip[] _fleshClips;
        private AudioClip[] _woodClips;
        private AudioClip[] _stoneClips;
        private AudioClip[] _dullClips;
        private AudioClip[] _swingClips;
        private readonly Dictionary<AudioClip, float> _clipPeaks = new();
        // A swing whose whoosh waits for the animation's real strike (styles without fixed timing).
        private SwingSoundSet.Entry _pendingStrikeWhoosh;
        private AudioClip[] _hurtClips;
        private AudioClip[] _blockClips;
        private AudioClip[] _parryClips;
        private AudioClip[] _treeFallClips;

        private readonly Dictionary<Transform, Reaction> _reactions = new();
        private readonly List<Transform> _finished = new();

        /// <summary>A short-lived visual reaction on one struck object. Each effect runs from its
        /// own start time (negative = not running); base values are captured once, so repeated
        /// hits never accumulate drift.</summary>
        private class Reaction
        {
            public Transform Target;
            public Renderer[] Renderers;
            public Color[] BaseColors;
            public Vector3 BaseScale;
            public Quaternion BaseRotation;
            public Vector3 BasePosition;
            public Vector3 Axis;
            public float FallDrop;
            public float FlashStart = -1f;
            public float PunchStart = -1f;
            public float ShakeStart = -1f;
            public float FallStart = -1f;
        }

        private void Awake()
        {
            if (_playerCombat == null)
            {
                Debug.LogError($"{nameof(HitFeedback)} on '{name}' is missing its Player Combat reference.", this);
                enabled = false;
                return;
            }

            _propertyBlock = new MaterialPropertyBlock();
            CreateParticleSystem();
            CreateAudioSources();
            CreateClips();

            // Read every whoosh's samples now rather than on its first swing.
            if (_swingSounds != null)
            {
                foreach (SwingSoundSet.Entry entry in _swingSounds.Entries)
                {
                    foreach (AudioClip clip in entry.Clips)
                    {
                        if (clip != null)
                        {
                            GetPeakTime(clip);
                        }
                    }
                }
            }
        }

        private void OnEnable()
        {
            if (_playerCombat == null)
            {
                return;
            }

            _playerCombat.HitLanded += HandleHitLanded;
            _playerCombat.DamageReceived += HandleDamageReceived;
            _playerCombat.AttackStarted += HandleAttackStarted;
            _playerCombat.StrikeStarted += HandleStrikeStarted;
            FallingTree.AnyFelled += HandleTreeFelled;
            FallingTree.AnyLanded += HandleTreeLanded;
        }

        private void OnDisable()
        {
            if (_playerCombat == null)
            {
                return;
            }

            _playerCombat.HitLanded -= HandleHitLanded;
            _playerCombat.DamageReceived -= HandleDamageReceived;
            _playerCombat.AttackStarted -= HandleAttackStarted;
            _playerCombat.StrikeStarted -= HandleStrikeStarted;
            FallingTree.AnyFelled -= HandleTreeFelled;
            FallingTree.AnyLanded -= HandleTreeLanded;
        }

        // ---- Event handlers ----

        /// <summary>A tree was felled and replaced by its bare trunk: a cloud of needles where
        /// the crown was (covers the crown vanishing) and a crack at the stump.</summary>
        private void HandleTreeFelled(FallingTree tree)
        {
            Vector3 foot = tree.transform.position;
            for (int i = 0; i < _treeFelledBursts; i++)
            {
                // Upper two thirds of the tree, where the crown was.
                float height = Mathf.Lerp(tree.TreeHeight * 0.3f, tree.TreeHeight, (i + 0.5f) / _treeFelledBursts);
                Burst(foot + Vector3.up * height, Vector3.up, _needlesPerBurst, NeedleDark, NeedleLight, false, 3f, 1.6f);
            }

            Play(_woodClips, foot + Vector3.up, 1f);
        }

        /// <summary>The falling trunk hit the ground: a deep thud and chips and dust along it.</summary>
        private void HandleTreeLanded(FallingTree tree)
        {
            Transform trunk = tree.transform;
            for (int i = 0; i < 4; i++)
            {
                float along = tree.TreeHeight * (0.2f + 0.2f * i);
                Burst(trunk.position + trunk.up * along, Vector3.up, 6, SurfaceColor(HitSurface.Wood), new Color(0.45f, 0.38f, 0.28f), false);
                Burst(trunk.position + trunk.up * along, Vector3.up, 4, NeedleDark, NeedleLight, false, 2f);
            }

            Play(_treeFallClips, trunk.position + trunk.up * (tree.TreeHeight * 0.5f), 1f);
        }

        /// <summary>Swing whoosh, timed so its peak lands on the strike: styles with a fixed
        /// strike moment start it now (build-up during the backswing), the others wait for
        /// StrikeStarted. See SwingSoundSet.</summary>
        private void HandleAttackStarted(WeaponDefinition weapon)
        {
            _pendingStrikeWhoosh = null;
            SwingSoundSet.Entry entry = _swingSounds != null ? _swingSounds.Get(weapon.WeaponClass) : null;
            if (entry == null)
            {
                Play(_swingClips, _playerCombat.transform.position + Vector3.up * _playerHitHeight, _swingVolume);
                return;
            }

            float strikeTime = _swingSounds.GetStrikeTime(weapon);
            if (strikeTime > 0f)
            {
                PlayWhoosh(entry, strikeTime + _swingSounds.StrikeLead);
            }
            else
            {
                _pendingStrikeWhoosh = entry;
            }
        }

        private void HandleStrikeStarted(WeaponDefinition weapon)
        {
            if (_pendingStrikeWhoosh == null)
            {
                return;
            }

            PlayWhoosh(_pendingStrikeWhoosh, _swingSounds.StrikeLead);
            _pendingStrikeWhoosh = null;
        }

        /// <summary>Plays one of the entry's clips so that its loudest moment is heard peakIn
        /// seconds from now: delayed if the clip's peak comes sooner, started part-way through
        /// if it comes later.</summary>
        private void PlayWhoosh(SwingSoundSet.Entry entry, float peakIn)
        {
            AudioClip clip = entry.Clips[Random.Range(0, entry.Clips.Length)];
            if (clip == null || _sources == null)
            {
                return;
            }

            AudioSource source = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;

            float pitch = entry.Pitch * (1f + Random.Range(-_swingSounds.PitchJitter, _swingSounds.PitchJitter));
            source.transform.position = _playerCombat.transform.position + Vector3.up * _playerHitHeight;
            source.clip = clip;
            source.volume = entry.Volume * GameSettings.GetVolume(AudioCategory.Effects);
            source.pitch = pitch;

            // Clip time runs pitch times faster than real time.
            float wait = peakIn - GetPeakTime(clip) / pitch;
            if (wait >= 0f)
            {
                source.time = 0f;
                source.PlayDelayed(wait);
            }
            else
            {
                source.Play();
                source.time = Mathf.Min(-wait * pitch, clip.length - 0.01f);
            }
        }

        /// <summary>Time of the clip's loudest 10 ms (cached). 0 if its samples can't be read
        /// (a streamed/compressed-in-memory clip) - it then simply plays from the start.</summary>
        private float GetPeakTime(AudioClip clip)
        {
            if (_clipPeaks.TryGetValue(clip, out float cached))
            {
                return cached;
            }

            float peakTime = 0f;
            var data = new float[clip.samples * clip.channels];
            if (clip.GetData(data, 0))
            {
                int window = Mathf.Max(1, clip.frequency / 100);
                float loudest = -1f;
                for (int start = 0; start + window <= clip.samples; start += window)
                {
                    float sum = 0f;
                    for (int i = start; i < start + window; i++)
                    {
                        float sample = data[i * clip.channels];
                        sum += sample * sample;
                    }

                    if (sum > loudest)
                    {
                        loudest = sum;
                        peakTime = (start + window * 0.5f) / clip.frequency;
                    }
                }
            }

            _clipPeaks[clip] = peakTime;
            return peakTime;
        }

        private void HandleHitLanded(MeleeHitInfo hit)
        {
            if (!hit.Counted)
            {
                // Touched but bounced off (wrong tool): a few grey chips and a dull clunk.
                Burst(hit.Point, -hit.Away, _bouncedParticles, SurfaceColor(HitSurface.Other), SurfaceColor(HitSurface.Other), false);
                Play(_dullClips, hit.Point, _volume);
                return;
            }

            switch (hit.Surface)
            {
                case HitSurface.Flesh:
                    Burst(hit.Point, hit.Away, _combatantParticles, SurfaceColor(HitSurface.Flesh), new Color(0.35f, 0.02f, 0.02f), true);
                    Play(_fleshClips, hit.Point, _volume);
                    break;
                case HitSurface.Wood:
                    Burst(hit.Point, -hit.Away, _gatherParticles, SurfaceColor(HitSurface.Wood), new Color(0.88f, 0.76f, 0.52f), false);
                    Play(_woodClips, hit.Point, _volume);
                    break;
                case HitSurface.Stone:
                    Burst(hit.Point, -hit.Away, _gatherParticles, SurfaceColor(HitSurface.Stone), new Color(0.38f, 0.38f, 0.4f), false);
                    Burst(hit.Point, -hit.Away, 3, new Color(1f, 0.9f, 0.6f), new Color(1f, 0.75f, 0.35f), false);
                    Play(_stoneClips, hit.Point, _volume);
                    break;
                default:
                    Burst(hit.Point, -hit.Away, _bouncedParticles * 2, SurfaceColor(HitSurface.Other), SurfaceColor(HitSurface.Other), false);
                    Play(hit.IsCombatant ? _fleshClips : _dullClips, hit.Point, _volume);
                    break;
            }

            StartReaction(hit);
        }

        private void HandleDamageReceived(IncomingHitResult result, float amount)
        {
            Transform player = _playerCombat.transform;
            Vector3 point = player.position + Vector3.up * _playerHitHeight + player.forward * 0.3f;

            switch (result)
            {
                case IncomingHitResult.Parried:
                    Burst(point, player.forward, 8, new Color(1f, 0.92f, 0.6f), new Color(1f, 0.7f, 0.3f), false);
                    Play(_parryClips, point, _volume);
                    break;
                case IncomingHitResult.Blocked:
                    Burst(point, player.forward, 5, SurfaceColor(HitSurface.Wood), SurfaceColor(HitSurface.Other), false);
                    Play(_blockClips, point, _volume);
                    break;
                default:
                    Burst(point - player.forward * 0.3f, -player.forward, 6, SurfaceColor(HitSurface.Flesh), new Color(0.35f, 0.02f, 0.02f), true);
                    Play(_hurtClips, point, _volume);
                    break;
            }
        }

        // ---- Target reactions ----

        private void StartReaction(MeleeHitInfo hit)
        {
            // The object that owns the gameplay component, not the collider's child.
            Component owner = hit.IsCombatant
                ? hit.Collider.GetComponentInParent<IDamageable>() as Component
                : hit.Collider.GetComponentInParent<IMeleeHitReceiver>() as Component;
            if (owner == null)
            {
                return;
            }

            Transform target = owner.transform;

            // A physics-driven node (fallen trunk, log) is moved by its Rigidbody - a scripted sway
            // would fight it, and the hit particles/sound are enough there.
            var body = owner.GetComponent<Rigidbody>();
            if (!hit.IsCombatant && body != null && !body.isKinematic)
            {
                return;
            }

            if (!_reactions.TryGetValue(target, out Reaction reaction))
            {
                reaction = new Reaction
                {
                    Target = target,
                    Renderers = target.GetComponentsInChildren<Renderer>(),
                    BaseScale = target.localScale,
                    BaseRotation = target.rotation
                };
                reaction.BaseColors = new Color[reaction.Renderers.Length];
                for (int i = 0; i < reaction.Renderers.Length; i++)
                {
                    Material material = reaction.Renderers[i].sharedMaterial;
                    reaction.BaseColors[i] = material != null && material.HasProperty(BaseColorProperty) ? material.GetColor(BaseColorProperty) : Color.white;
                }

                _reactions.Add(target, reaction);
            }

            // Sway and fall go away from the player (hit.Away is already flat and normalized).
            reaction.Axis = Vector3.Cross(Vector3.up, hit.Away);

            float now = Time.time;
            if (hit.IsCombatant)
            {
                reaction.FlashStart = now;
                if (hit.Killed && target.GetComponentInChildren<NAV.Presentation.Animation.CreatureAnimator>() != null)
                {
                    // An animated creature plays its own death (CreatureAnimator) - nothing here.
                }
                else if (hit.Killed)
                {
                    // Falls away from the blow. Rotation/position are captured now: the creature
                    // has stopped (its agent is disabled on death), and a living one's rotation
                    // belongs to its NavMeshAgent, so it's never touched before this.
                    reaction.FallStart = now;
                    reaction.BaseRotation = target.rotation;
                    reaction.BasePosition = target.position;
                    reaction.FallDrop = FallDrop(reaction.Renderers);
                }
                else
                {
                    reaction.PunchStart = now;
                }
            }
            else
            {
                reaction.ShakeStart = now;
            }
        }

        /// <summary>How far an upright body must sink to lie on the ground once tipped 90° about
        /// its centre: half its height minus half its thickness.</summary>
        private static float FallDrop(Renderer[] renderers)
        {
            if (renderers.Length == 0)
            {
                return 0f;
            }

            Bounds bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return Mathf.Max(0f, bounds.extents.y - Mathf.Min(bounds.extents.x, bounds.extents.z));
        }

        // After creatures' NavMeshAgents and the Animator have moved things this frame.
        private void LateUpdate()
        {
            float now = Time.time;
            _finished.Clear();

            foreach (Reaction reaction in _reactions.Values)
            {
                // Destroyed (depleted node, despawned creature) - nothing left to animate.
                if (reaction.Target == null)
                {
                    _finished.Add(reaction.Target);
                    continue;
                }

                bool running = false;
                running |= TickFlash(reaction, now);
                running |= TickPunch(reaction, now);
                running |= TickShake(reaction, now);
                running |= TickFall(reaction, now);

                if (!running)
                {
                    _finished.Add(reaction.Target);
                }
            }

            // A destroyed Transform is still the same C# reference with the same hash, so it
            // removes fine even though it now "== null".
            foreach (Transform target in _finished)
            {
                _reactions.Remove(target);
            }
        }

        private bool TickFlash(Reaction reaction, float now)
        {
            if (reaction.FlashStart < 0f)
            {
                return false;
            }

            float progress = (now - reaction.FlashStart) / Mathf.Max(_flashSeconds, 0.0001f);
            bool done = progress >= 1f;
            for (int i = 0; i < reaction.Renderers.Length; i++)
            {
                Renderer renderer = reaction.Renderers[i];
                if (renderer == null)
                {
                    continue;
                }

                if (done)
                {
                    renderer.SetPropertyBlock(null);
                    continue;
                }

                renderer.GetPropertyBlock(_propertyBlock);
                _propertyBlock.SetColor(BaseColorProperty, Color.Lerp(_flashColor, reaction.BaseColors[i], progress));
                renderer.SetPropertyBlock(_propertyBlock);
            }

            if (done)
            {
                reaction.FlashStart = -1f;
            }

            return !done;
        }

        private bool TickPunch(Reaction reaction, float now)
        {
            if (reaction.PunchStart < 0f)
            {
                return false;
            }

            float progress = (now - reaction.PunchStart) / _punchSeconds;
            if (progress >= 1f)
            {
                reaction.Target.localScale = reaction.BaseScale;
                reaction.PunchStart = -1f;
                return false;
            }

            // Squash: shorter and wider, back to normal by the end.
            float amount = _punchAmount * Mathf.Sin(Mathf.PI * progress);
            Vector3 scale = reaction.BaseScale;
            reaction.Target.localScale = new Vector3(scale.x * (1f + amount), scale.y * (1f - amount), scale.z * (1f + amount));
            return true;
        }

        private bool TickShake(Reaction reaction, float now)
        {
            if (reaction.ShakeStart < 0f)
            {
                return false;
            }

            float progress = (now - reaction.ShakeStart) / _shakeSeconds;
            if (progress >= 1f)
            {
                reaction.Target.rotation = reaction.BaseRotation;
                reaction.ShakeStart = -1f;
                return false;
            }

            // A damped sway about the horizontal axis across the blow - first away from it.
            float angle = _shakeDegrees * Mathf.Sin(progress * Mathf.PI * 5f) * (1f - progress);
            reaction.Target.rotation = Quaternion.AngleAxis(angle, reaction.Axis) * reaction.BaseRotation;
            return true;
        }

        private bool TickFall(Reaction reaction, float now)
        {
            if (reaction.FallStart < 0f)
            {
                return false;
            }

            float progress = Mathf.Clamp01((now - reaction.FallStart) / _deathFallSeconds);
            // Accelerating fall, like toppling under gravity.
            float eased = progress * progress;
            reaction.Target.rotation = Quaternion.AngleAxis(90f * eased, reaction.Axis) * reaction.BaseRotation;
            reaction.Target.position = reaction.BasePosition - Vector3.up * (reaction.FallDrop * eased);

            if (progress >= 1f)
            {
                // Stays lying there until Creature despawns it.
                reaction.FallStart = -1f;
                return false;
            }

            return true;
        }

        // ---- Particles ----

        private void CreateParticleSystem()
        {
            var particleObject = new GameObject("Hit Particles");
            particleObject.transform.SetParent(transform, false);
            _particles = particleObject.AddComponent<ParticleSystem>();
            _particles.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = _particles.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = 1.2f;
            main.maxParticles = 600;

            ParticleSystem.EmissionModule emission = _particles.emission;
            emission.enabled = false;

            ParticleSystem.ShapeModule shape = _particles.shape;
            shape.enabled = false;

            // Shrink away instead of popping out.
            ParticleSystem.SizeOverLifetimeModule size = _particles.sizeOverLifetime;
            size.enabled = true;
            size.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.Linear(0f, 1f, 1f, 0f));

            // Settle on the ground instead of falling through it.
            ParticleSystem.CollisionModule collision = _particles.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Low;
            collision.dampen = 0.6f;
            collision.bounce = 0.2f;

            var particleRenderer = particleObject.GetComponent<ParticleSystemRenderer>();
            particleRenderer.renderMode = ParticleSystemRenderMode.Billboard;
            particleRenderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            particleRenderer.receiveShadows = false;

            Shader fallbackShader = Shader.Find("Universal Render Pipeline/Particles/Unlit");
            if (_particleMaterialTemplate != null)
            {
                particleRenderer.material = new Material(_particleMaterialTemplate) { name = "Hit Particles (runtime)" };
            }
            else if (fallbackShader != null)
            {
                Debug.LogWarning($"{nameof(HitFeedback)} on '{name}' has no Particle Material Template - using Shader.Find, which may be missing in a player build.", this);
                particleRenderer.material = new Material(fallbackShader) { name = "Hit Particles (runtime)" };
            }
            else
            {
                Debug.LogError($"{nameof(HitFeedback)} on '{name}': no Particle Material Template and the URP particle shader wasn't found - hit particles will render pink.", this);
            }

            _particles.Play();
        }

        /// <summary>Emits count particles at point. Spray = through the target along the blow
        /// (blood); otherwise they bounce back toward the attacker (chips, shards). Each particle
        /// gets a colour between colorA and colorB.</summary>
        private void Burst(Vector3 point, Vector3 direction, int count, Color colorA, Color colorB, bool spray, float sizeScale = 1f, float speedScale = 1f)
        {
            if (count <= 0 || _particles == null)
            {
                return;
            }

            Vector3 along = direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
            var emit = new ParticleSystem.EmitParams();
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (along * (spray ? 0.8f : 0.6f) + Random.onUnitSphere * 0.6f + Vector3.up * 0.7f).normalized;
                emit.position = point;
                emit.velocity = velocity * (_particleSpeed * speedScale * Random.Range(0.4f, 1f));
                emit.startColor = Color.Lerp(colorA, colorB, Random.value);
                emit.startSize = _particleSize * sizeScale * Random.Range(0.6f, 1.4f);
                emit.startLifetime = Random.Range(0.45f, 0.9f);
                emit.rotation = Random.Range(0f, 360f);
                _particles.Emit(emit, 1);
            }
        }

        private static Color SurfaceColor(HitSurface surface)
        {
            switch (surface)
            {
                case HitSurface.Flesh: return new Color(0.6f, 0.05f, 0.05f);
                case HitSurface.Wood: return new Color(0.6f, 0.43f, 0.24f);
                case HitSurface.Stone: return new Color(0.58f, 0.57f, 0.55f);
                default: return new Color(0.72f, 0.7f, 0.64f);
            }
        }

        // ---- Audio ----

        private void CreateAudioSources()
        {
            _sources = new AudioSource[_voices];
            for (int i = 0; i < _voices; i++)
            {
                var sourceObject = new GameObject($"Hit Audio {i}");
                sourceObject.transform.SetParent(transform, false);
                AudioSource source = sourceObject.AddComponent<AudioSource>();
                source.playOnAwake = false;
                source.spatialBlend = 1f;
                source.rolloffMode = AudioRolloffMode.Linear;
                source.minDistance = 2f;
                source.maxDistance = 35f;
                source.dopplerLevel = 0f;
                _sources[i] = source;
            }
        }

        private void CreateClips()
        {
            _fleshClips = new AudioClip[ClipVariations];
            _woodClips = new AudioClip[ClipVariations];
            _stoneClips = new AudioClip[ClipVariations];
            _dullClips = new AudioClip[ClipVariations];
            _swingClips = new AudioClip[ClipVariations];
            _hurtClips = new AudioClip[ClipVariations];
            _blockClips = new AudioClip[ClipVariations];
            _parryClips = new AudioClip[ClipVariations];
            _treeFallClips = new AudioClip[ClipVariations];

            for (int i = 0; i < ClipVariations; i++)
            {
                _fleshClips[i] = PlaceholderCombatAudio.CreateFleshHit(i);
                _woodClips[i] = PlaceholderCombatAudio.CreateWoodHit(i);
                _stoneClips[i] = PlaceholderCombatAudio.CreateStoneHit(i);
                _dullClips[i] = PlaceholderCombatAudio.CreateDullHit(i);
                _swingClips[i] = PlaceholderCombatAudio.CreateSwing(i);
                _hurtClips[i] = PlaceholderCombatAudio.CreateHurt(i);
                _blockClips[i] = PlaceholderCombatAudio.CreateBlock(i);
                _parryClips[i] = PlaceholderCombatAudio.CreateParry(i);
                _treeFallClips[i] = PlaceholderCombatAudio.CreateTreeFall(i);
            }
        }

        /// <summary>Plays a random variation at a world position on the next voice (round robin -
        /// the oldest sound is cut if all voices are busy).</summary>
        private void Play(AudioClip[] clips, Vector3 position, float volume)
        {
            if (clips == null || clips.Length == 0 || _sources == null)
            {
                return;
            }

            AudioSource source = _sources[_nextSource];
            _nextSource = (_nextSource + 1) % _sources.Length;

            source.transform.position = position;
            source.clip = clips[Random.Range(0, clips.Length)];
            source.volume = volume * GameSettings.GetVolume(AudioCategory.Effects);
            source.pitch = 1f + Random.Range(-_pitchJitter, _pitchJitter);
            source.Play();
        }

        private void OnDestroy()
        {
            // Restore anything mid-reaction so a scene unload never leaves a tinted renderer.
            foreach (Reaction reaction in _reactions.Values)
            {
                if (reaction.Target == null)
                {
                    continue;
                }

                foreach (Renderer renderer in reaction.Renderers)
                {
                    if (renderer != null)
                    {
                        renderer.SetPropertyBlock(null);
                    }
                }
            }
        }
    }
}
