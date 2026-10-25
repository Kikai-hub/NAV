using UnityEngine;

namespace NAV.Presentation.Combat
{
    /// <summary>
    /// The particle side of hits and gathering, by material: wood chips + sawdust, stone shards +
    /// dust (+ sparks from a pick), blood droplets that leave fading splats on the ground, a short
    /// impact flash, falling leaves/needles and dust clouds for felled trees. A handful of
    /// pre-built particle systems fed with Emit (no per-hit allocations). HitFeedback decides
    /// when; this decides what it looks like. Materials and meshes come from
    /// NAV/Setup/Add Hit Feedback To Scene (HitFeedbackSetup).
    /// </summary>
    public class ImpactEffects : MonoBehaviour
    {
        [Header("Materials")]
        [Tooltip("Lit, vertex-coloured, opaque (URP Particles/Simple Lit) - chips, shards, leaves.")]
        [SerializeField] private Material _chunkMaterial;
        [Tooltip("Soft round alpha-blended sprite - dust, mist, droplets.")]
        [SerializeField] private Material _softMaterial;
        [Tooltip("Additive - sparks and the impact flash.")]
        [SerializeField] private Material _additiveMaterial;
        [Tooltip("Irregular splat sprite - blood on the ground.")]
        [SerializeField] private Material _splatMaterial;

        [Header("Meshes")]
        [SerializeField] private Mesh _chipMesh;
        [SerializeField] private Mesh _shardMesh;

        private ParticleSystem _chips;
        private ParticleSystem _shards;
        private ParticleSystem _leaves;
        private ParticleSystem _dust;
        private ParticleSystem _droplets;
        private ParticleSystem _splats;
        private ParticleSystem _sparks;
        private ParticleSystem _flash;
        private ParticleSystem.EmitParams _emit;

        private static readonly Color WoodLight = new(0.86f, 0.72f, 0.48f);
        private static readonly Color WoodDark = new(0.55f, 0.37f, 0.2f);
        private static readonly Color StoneLight = new(0.66f, 0.65f, 0.62f);
        private static readonly Color StoneDark = new(0.36f, 0.36f, 0.38f);
        private static readonly Color BloodLight = new(0.75f, 0.03f, 0.04f);
        private static readonly Color BloodDark = new(0.42f, 0.01f, 0.02f);
        private static readonly Color SparkHot = new(1f, 0.85f, 0.5f);
        private static readonly Color SparkCool = new(1f, 0.5f, 0.15f);

        private void Awake()
        {
            _chips = CreateMeshSystem("Wood Chips", _chipMesh, 1.6f, 300);
            _shards = CreateMeshSystem("Stone Shards", _shardMesh, 1.8f, 300);
            _leaves = CreateMeshSystem("Leaves", _chipMesh, 0.18f, 500);
            // Leaves flutter: drag and a little sideways noise instead of dropping like stones.
            ParticleSystem.LimitVelocityOverLifetimeModule drag = _leaves.limitVelocityOverLifetime;
            drag.enabled = true;
            drag.drag = 1.2f;
            ParticleSystem.NoiseModule noise = _leaves.noise;
            noise.enabled = true;
            noise.strength = 0.6f;
            noise.frequency = 0.6f;

            _dust = CreateBillboardSystem("Dust", _softMaterial, -0.05f, 300, ParticleSystemRenderMode.Billboard);
            ParticleSystem.SizeOverLifetimeModule dustSize = _dust.sizeOverLifetime;
            dustSize.enabled = true;
            dustSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.5f, 1f, 1.6f));
            FadeOut(_dust);

            _droplets = CreateBillboardSystem("Blood Droplets", _softMaterial, 1.6f, 300, ParticleSystemRenderMode.Stretch);
            _droplets.GetComponent<ParticleSystemRenderer>().velocityScale = 0.04f;
            EnableCollision(_droplets, 0.1f, 0f, 1f);

            _splats = CreateBillboardSystem("Blood Splats", _splatMaterial, 0f, 200, ParticleSystemRenderMode.HorizontalBillboard);
            FadeOut(_splats);
            // A sub-emitter is driven by its parent only: as a child of the droplets and not
            // playing on its own (its burst would otherwise fire at its own position).
            _splats.transform.SetParent(_droplets.transform, false);
            _splats.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            // A droplet that hits something becomes a splat where it landed.
            ParticleSystem.SubEmittersModule sub = _droplets.subEmitters;
            sub.enabled = true;
            sub.AddSubEmitter(_splats, ParticleSystemSubEmitterType.Collision, ParticleSystemSubEmitterProperties.InheritNothing, 0.5f);
            ParticleSystem.MainModule splatMain = _splats.main;
            splatMain.startLifetime = new ParticleSystem.MinMaxCurve(5f, 8f);
            splatMain.startSize = new ParticleSystem.MinMaxCurve(0.12f, 0.3f);
            splatMain.startSpeed = 0f;
            splatMain.startRotation = new ParticleSystem.MinMaxCurve(0f, Mathf.PI * 2f);
            splatMain.startColor = new ParticleSystem.MinMaxGradient(BloodDark, BloodLight * 0.8f);
            ParticleSystem.EmissionModule splatEmission = _splats.emission;
            splatEmission.enabled = true;
            splatEmission.rateOverTime = 0f;
            splatEmission.SetBursts(new[] { new ParticleSystem.Burst(0f, 1) });

            _sparks = CreateBillboardSystem("Sparks", _additiveMaterial, 0.7f, 200, ParticleSystemRenderMode.Stretch);
            _sparks.GetComponent<ParticleSystemRenderer>().velocityScale = 0.06f;
            EnableCollision(_sparks, 0.4f, 0.3f, 0f);

            _flash = CreateBillboardSystem("Impact Flash", _additiveMaterial, 0f, 30, ParticleSystemRenderMode.Billboard);
            ParticleSystem.SizeOverLifetimeModule flashSize = _flash.sizeOverLifetime;
            flashSize.enabled = true;
            flashSize.size = new ParticleSystem.MinMaxCurve(1f, AnimationCurve.EaseInOut(0f, 0.6f, 1f, 1.3f));
            FadeOut(_flash);
        }

        // ---- What each material looks like when struck ----

        /// <summary>Axe into wood: chips flying back off the cut and a puff of sawdust.</summary>
        public void Wood(Vector3 point, Vector3 back, float strength = 1f)
        {
            EmitChunks(_chips, point, back, Mathf.RoundToInt(10 * strength), WoodDark, WoodLight, new Vector3(0.06f, 0.01f, 0.025f), 3.2f);
            EmitDust(point, Mathf.RoundToInt(4 * strength), WoodLight * 0.9f, 0.35f, 0.5f);
            Flash(point, 0.35f * strength);
        }

        /// <summary>Pick into rock: shards, grey dust and - for a proper pick blow - sparks.</summary>
        public void Stone(Vector3 point, Vector3 back, float strength = 1f, bool sparks = true)
        {
            EmitChunks(_shards, point, back, Mathf.RoundToInt(9 * strength), StoneDark, StoneLight, new Vector3(0.06f, 0.05f, 0.06f), 3.5f);
            EmitDust(point, Mathf.RoundToInt(6 * strength), StoneLight, 0.4f, 0.6f);
            if (sparks)
            {
                Sparks(point, back, Mathf.RoundToInt(7 * strength));
            }

            Flash(point, 0.4f * strength);
        }

        /// <summary>A blow to a creature (or the player): droplets along the blow that stain the
        /// ground where they land, and a little red mist.</summary>
        public void Flesh(Vector3 point, Vector3 along, float strength = 1f)
        {
            Vector3 dir = Safe(along);
            int count = Mathf.RoundToInt(14 * strength);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (dir * 0.7f + Random.insideUnitSphere * 0.55f + Vector3.up * 0.45f).normalized * Random.Range(1.5f, 4.2f);
                Emit(_droplets, point, velocity, Color.Lerp(BloodDark, BloodLight, Random.value), Random.Range(0.05f, 0.1f), Random.Range(0.5f, 0.9f));
            }

            EmitDust(point, Mathf.RoundToInt(3 * strength), BloodLight * 0.7f, 0.25f, 0.35f);
            Flash(point, 0.3f * strength);
        }

        /// <summary>Glancing off (wrong tool, bare surface): a few dull flecks.</summary>
        public void Dull(Vector3 point, Vector3 back)
        {
            EmitChunks(_shards, point, back, 3, StoneDark, StoneLight, new Vector3(0.03f, 0.03f, 0.03f), 2f);
            EmitDust(point, 2, StoneLight, 0.2f, 0.4f);
        }

        /// <summary>Weapon on weapon: a fan of sparks and a bright flash.</summary>
        public void Parry(Vector3 point, Vector3 outward)
        {
            Sparks(point, outward, 16);
            Flash(point, 0.9f);
        }

        /// <summary>A blow caught on a shield: splinters and a small flash.</summary>
        public void Block(Vector3 point, Vector3 outward)
        {
            EmitChunks(_chips, point, outward, 6, WoodDark, WoodLight, new Vector3(0.06f, 0.01f, 0.03f), 2.5f);
            Flash(point, 0.45f);
        }

        /// <summary>Leaves or needles shaken loose (a felled crown): they drift down slowly.</summary>
        public void Leaves(Vector3 centre, float radius, int count, Color dark, Color light)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 position = centre + Random.insideUnitSphere * radius;
                Vector3 velocity = Random.insideUnitSphere * 1.2f + Vector3.up * 0.6f;
                _emit.position = position;
                _emit.velocity = velocity;
                _emit.startColor = Color.Lerp(dark, light, Random.value);
                _emit.startSize3D = new Vector3(0.09f, 0.01f, 0.05f) * Random.Range(0.7f, 1.4f);
                _emit.startLifetime = Random.Range(2.5f, 4f);
                _emit.rotation3D = Random.insideUnitSphere * 360f;
                _emit.angularVelocity3D = Random.insideUnitSphere * 300f;
                _leaves.Emit(_emit, 1);
            }

            ResetEmit();
        }

        /// <summary>A cloud of dust rolling out from a point (a trunk hitting the ground).</summary>
        public void DustCloud(Vector3 centre, float radius, int count, Color color)
        {
            for (int i = 0; i < count; i++)
            {
                Vector2 ring = Random.insideUnitCircle * radius;
                Vector3 position = centre + new Vector3(ring.x, Random.Range(0f, 0.3f), ring.y);
                Vector3 velocity = new Vector3(ring.x, 0.35f, ring.y).normalized * Random.Range(0.4f, 1.3f);
                Emit(_dust, position, velocity, color, Random.Range(0.6f, 1.3f), Random.Range(1.2f, 2.2f));
            }
        }

        // ---- Building blocks ----

        private void EmitChunks(ParticleSystem system, Vector3 point, Vector3 back, int count, Color dark, Color light, Vector3 size, float speed)
        {
            Vector3 dir = Safe(back);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (dir * 0.6f + Random.insideUnitSphere * 0.6f + Vector3.up * 0.7f).normalized * (speed * Random.Range(0.45f, 1f));
                _emit.position = point;
                _emit.velocity = velocity;
                _emit.startColor = Color.Lerp(dark, light, Random.value);
                _emit.startSize3D = size * Random.Range(0.6f, 1.5f);
                _emit.startLifetime = Random.Range(1.2f, 2.2f);
                _emit.rotation3D = Random.insideUnitSphere * 360f;
                _emit.angularVelocity3D = Random.insideUnitSphere * 720f;
                system.Emit(_emit, 1);
            }

            ResetEmit();
        }

        private void EmitDust(Vector3 point, int count, Color color, float size, float lifetime)
        {
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = Random.insideUnitSphere * 0.5f + Vector3.up * 0.25f;
                Emit(_dust, point + Random.insideUnitSphere * 0.08f, velocity, color, size * Random.Range(0.7f, 1.3f), lifetime * Random.Range(0.8f, 1.4f));
            }
        }

        private void Sparks(Vector3 point, Vector3 dir, int count)
        {
            Vector3 along = Safe(dir);
            for (int i = 0; i < count; i++)
            {
                Vector3 velocity = (along * 0.6f + Random.insideUnitSphere * 0.8f + Vector3.up * 0.3f).normalized * Random.Range(3f, 7f);
                Emit(_sparks, point, velocity, Color.Lerp(SparkCool, SparkHot, Random.value), Random.Range(0.025f, 0.05f), Random.Range(0.2f, 0.45f));
            }
        }

        private void Flash(Vector3 point, float size)
        {
            Emit(_flash, point, Vector3.zero, new Color(1f, 0.92f, 0.75f, 0.9f), size, 0.09f);
        }

        private void Emit(ParticleSystem system, Vector3 position, Vector3 velocity, Color color, float size, float lifetime)
        {
            _emit.position = position;
            _emit.velocity = velocity;
            _emit.startColor = color;
            _emit.startSize = size;
            _emit.startLifetime = lifetime;
            _emit.rotation = Random.Range(0f, 360f);
            system.Emit(_emit, 1);
            ResetEmit();
        }

        private void ResetEmit()
        {
            _emit = new ParticleSystem.EmitParams();
        }

        private static Vector3 Safe(Vector3 direction)
        {
            return direction.sqrMagnitude > 0.0001f ? direction.normalized : Vector3.up;
        }

        // ---- Particle system setup ----

        private ParticleSystem CreateMeshSystem(string systemName, Mesh mesh, float gravity, int maxParticles)
        {
            ParticleSystem system = CreateSystem(systemName, gravity, maxParticles);
            ParticleSystem.MainModule main = system.main;
            main.startSize3D = true;
            main.startRotation3D = true;
            EnableCollision(system, 0.5f, 0.25f, 0f);

            // Shrink at the very end instead of popping out.
            ParticleSystem.SizeOverLifetimeModule size = system.sizeOverLifetime;
            size.enabled = true;
            var curve = new AnimationCurve(new Keyframe(0f, 1f), new Keyframe(0.8f, 1f), new Keyframe(1f, 0f));
            size.size = new ParticleSystem.MinMaxCurve(1f, curve);

            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = ParticleSystemRenderMode.Mesh;
            renderer.mesh = mesh;
            renderer.alignment = ParticleSystemRenderSpace.Local;
            renderer.sharedMaterial = _chunkMaterial;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.On;
            return system;
        }

        private ParticleSystem CreateBillboardSystem(string systemName, Material material, float gravity, int maxParticles, ParticleSystemRenderMode mode)
        {
            ParticleSystem system = CreateSystem(systemName, gravity, maxParticles);
            var renderer = system.GetComponent<ParticleSystemRenderer>();
            renderer.renderMode = mode;
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            return system;
        }

        private ParticleSystem CreateSystem(string systemName, float gravity, int maxParticles)
        {
            var go = new GameObject(systemName);
            go.transform.SetParent(transform, false);
            var system = go.AddComponent<ParticleSystem>();
            system.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);

            ParticleSystem.MainModule main = system.main;
            main.loop = true;
            main.playOnAwake = false;
            main.simulationSpace = ParticleSystemSimulationSpace.World;
            main.gravityModifier = gravity;
            main.maxParticles = maxParticles;

            ParticleSystem.EmissionModule emission = system.emission;
            emission.enabled = false;
            ParticleSystem.ShapeModule shape = system.shape;
            shape.enabled = false;

            system.Play();
            return system;
        }

        private static void EnableCollision(ParticleSystem system, float dampen, float bounce, float lifetimeLoss)
        {
            ParticleSystem.CollisionModule collision = system.collision;
            collision.enabled = true;
            collision.type = ParticleSystemCollisionType.World;
            collision.mode = ParticleSystemCollisionMode.Collision3D;
            collision.quality = ParticleSystemCollisionQuality.Medium;
            collision.dampen = dampen;
            collision.bounce = bounce;
            collision.lifetimeLoss = lifetimeLoss;
            collision.sendCollisionMessages = false;
        }

        private static void FadeOut(ParticleSystem system)
        {
            ParticleSystem.ColorOverLifetimeModule color = system.colorOverLifetime;
            color.enabled = true;
            var gradient = new Gradient();
            gradient.SetKeys(
                new[] { new GradientColorKey(Color.white, 0f), new GradientColorKey(Color.white, 1f) },
                new[] { new GradientAlphaKey(1f, 0f), new GradientAlphaKey(0.8f, 0.6f), new GradientAlphaKey(0f, 1f) });
            color.color = gradient;
        }
    }
}
