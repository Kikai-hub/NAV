using NAV.Gameplay.Environment;
using NAV.Gameplay.Items;
using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// A loosed arrow: flies on its own (gravity, a little air drag, much more in water), testing
    /// its path every frame with a ray from where it was to where it is - fast arrows never pass
    /// through a target - and points along its flight.
    ///  - Hits a live IDamageable (not the shooter): deals its damage, the target flinches
    ///    (IHitReactive), the shooter's PlayerCombat raises HitLanded (particles, sound); the arrow
    ///    is spent.
    ///  - Hits anything else (ground, tree, rock, a wall): sticks in it; with the ammo's Recover
    ///    Chance it stays whole and can be picked up (StuckArrow), otherwise it breaks after a moment.
    /// No Rigidbody - created by PlayerArchery with Launch.
    /// </summary>
    public class ArrowProjectile : MonoBehaviour
    {
        private const float AirDrag = 0.02f;
        private const float WaterDrag = 3f;
        private const float MaxFlightSeconds = 12f;
        private const float BrokenArrowSeconds = 1.5f;
        private const float StuckArrowSeconds = 180f;
        // How deep the tip goes into what it strikes.
        private const float Penetration = 0.18f;

        private PlayerCombat _shooter;
        private Transform _shooterRoot;
        private WeaponDefinition _bow;
        private ItemDefinition _arrowItem;
        private float _damage;
        private Vector3 _velocity;
        private float _age;
        private bool _flying;
        private LayerMask _hitLayers;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        public void Launch(PlayerCombat shooter, WeaponDefinition bow, ItemDefinition arrowItem, float damage, Vector3 velocity, LayerMask hitLayers)
        {
            _shooter = shooter;
            _shooterRoot = shooter != null ? shooter.transform : null;
            _bow = bow;
            _arrowItem = arrowItem;
            _damage = damage;
            _velocity = velocity;
            _hitLayers = hitLayers;
            _flying = true;
            transform.rotation = Quaternion.LookRotation(velocity);

            // The model is only a picture: its colliders must not stop anything.
            foreach (Collider collider in GetComponentsInChildren<Collider>())
            {
                collider.enabled = false;
            }
        }

        private void Update()
        {
            if (!_flying)
            {
                return;
            }

            float dt = Time.deltaTime;
            _age += dt;
            if (_age > MaxFlightSeconds)
            {
                Destroy(gameObject);
                return;
            }

            Vector3 from = transform.position;
            bool inWater = WaterBody.TryGetSurface(from, out float surface) && from.y < surface;
            _velocity += Physics.gravity * dt;
            _velocity *= 1f - Mathf.Clamp01((inWater ? WaterDrag : AirDrag) * dt);
            Vector3 to = from + _velocity * dt;

            if (TryHit(from, to, out RaycastHit hit))
            {
                Strike(hit);
                return;
            }

            transform.position = to;
            if (_velocity.sqrMagnitude > 0.01f)
            {
                transform.rotation = Quaternion.LookRotation(_velocity);
            }
        }

        // The nearest thing on the path that isn't the shooter (or this arrow).
        private bool TryHit(Vector3 from, Vector3 to, out RaycastHit nearest)
        {
            nearest = default;
            Vector3 path = to - from;
            float length = path.magnitude;
            if (length < 0.0001f)
            {
                return false;
            }

            int count = Physics.RaycastNonAlloc(from, path / length, _hits, length, _hitLayers, QueryTriggerInteraction.Ignore);
            bool found = false;
            for (int i = 0; i < count; i++)
            {
                Transform other = _hits[i].collider.transform;
                if ((_shooterRoot != null && other.IsChildOf(_shooterRoot)) || other.IsChildOf(transform))
                {
                    continue;
                }

                if (!found || _hits[i].distance < nearest.distance)
                {
                    nearest = _hits[i];
                    found = true;
                }
            }

            return found;
        }

        private void Strike(RaycastHit hit)
        {
            _flying = false;
            Vector3 direction = _velocity.normalized;
            Vector3 away = new Vector3(direction.x, 0f, direction.z);
            away = away.sqrMagnitude > 0.0001f ? away.normalized : Vector3.forward;
            IHitSurface surfaceSource = hit.collider.GetComponentInParent<IHitSurface>();
            HitSurface surface = surfaceSource != null ? surfaceSource.Surface : HitSurface.Other;

            // Stuck in place, the tip a little way in, travelling with what it hit.
            transform.SetPositionAndRotation(hit.point + direction * Penetration - direction * 0.05f, Quaternion.LookRotation(direction));
            transform.SetParent(hit.collider.transform, true);

            IDamageable target = hit.collider.GetComponentInParent<IDamageable>();
            if (target != null && !ReferenceEquals(target, _shooter))
            {
                if (target.IsAlive)
                {
                    target.TakeDamage(_bow != null ? _bow.DamageAgainst(target, _damage) : _damage);
                    (target as IHitReactive)?.ReactToHit(away);
                    _shooter?.ReportProjectileHit(new MeleeHitInfo(hit.collider, hit.point, direction, away, _bow, surface, true, true, !target.IsAlive));
                }

                // In a body: gone with it (not recovered).
                Destroy(gameObject, BrokenArrowSeconds);
                return;
            }

            if (_bow != null && hit.collider.GetComponentInParent<IMeleeHitReceiver>() != null)
            {
                // A thunk into a tree or rock - effects only, an arrow chops nothing.
                _shooter?.ReportProjectileHit(new MeleeHitInfo(hit.collider, hit.point, direction, away, _bow, surface, false, false, false));
            }

            float recover = _arrowItem != null && _arrowItem.Ammo != null ? _arrowItem.Ammo.RecoverChance : 0f;
            if (Random.value < recover)
            {
                StuckArrow.Attach(gameObject, _arrowItem);
                Destroy(gameObject, StuckArrowSeconds);
            }
            else
            {
                Destroy(gameObject, BrokenArrowSeconds);
            }
        }
    }
}
