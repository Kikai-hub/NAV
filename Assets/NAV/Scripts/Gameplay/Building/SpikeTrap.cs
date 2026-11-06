using System.Collections.Generic;
using NAV.Gameplay.AI;
using NAV.Gameplay.Combat;
using UnityEngine;

namespace NAV.Gameplay.Building
{
    /// <summary>
    /// A trap of sharpened bog-wood stakes with iron points (tier 4): every Check Interval it
    /// looks for creatures in its zone (a box over the stakes) and wounds each one it finds, at
    /// most once per Hit Cooldown each - so a pack running over it bleeds, a lone beast standing on
    /// it keeps getting hurt. Players are never hurt (they built it). Each wound blunts it; after
    /// Max Hits it falls apart (the piece is destroyed - not saved any more). Polls with
    /// OverlapBox instead of trigger events: creatures have no Rigidbody to fire them.
    /// </summary>
    public class SpikeTrap : MonoBehaviour
    {
        [SerializeField] private Vector3 _zoneCenter = new(0f, 0.4f, 0f);
        [SerializeField] private Vector3 _zoneSize = new(1.9f, 0.8f, 1.9f);
        [SerializeField, Min(0f)] private float _damage = 15f;
        [SerializeField, Min(0.1f)] private float _hitCooldown = 1.2f;
        [SerializeField, Min(0.05f)] private float _checkInterval = 0.25f;
        [SerializeField, Min(1)] private int _maxHits = 30;

        private readonly Collider[] _overlaps = new Collider[16];
        private readonly Dictionary<Creature, float> _nextHit = new();
        private BuildingPiece _piece;
        private float _nextCheck;
        private int _hits;

        public int HitsLeft => _maxHits - _hits;

        private void Awake()
        {
            _piece = GetComponent<BuildingPiece>();
        }

        private void Update()
        {
            // A ghost being placed doesn't bite.
            if ((_piece != null && !_piece.IsPlaced) || Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + _checkInterval;
            Vector3 center = transform.TransformPoint(_zoneCenter);
            int count = Physics.OverlapBoxNonAlloc(center, _zoneSize * 0.5f, _overlaps, transform.rotation, ~0, QueryTriggerInteraction.Ignore);
            for (int i = 0; i < count; i++)
            {
                Creature creature = _overlaps[i].GetComponentInParent<Creature>();
                if (creature == null || !creature.IsAlive)
                {
                    continue;
                }

                if (_nextHit.TryGetValue(creature, out float next) && Time.time < next)
                {
                    continue;
                }

                _nextHit[creature] = Time.time + _hitCooldown;
                creature.TakeDamage(_damage);
                Vector3 away = creature.transform.position - transform.position;
                away.y = 0f;
                ((IHitReactive)creature).ReactToHit(away.sqrMagnitude > 0.0001f ? away.normalized : transform.forward);

                if (++_hits >= _maxHits)
                {
                    Debug.Log($"{name} is worn out and falls apart.", this);
                    Destroy(gameObject);
                    return;
                }
            }
        }

        private void OnDrawGizmosSelected()
        {
            Gizmos.matrix = Matrix4x4.TRS(transform.position, transform.rotation, Vector3.one);
            Gizmos.color = new Color(0.8f, 0.2f, 0.1f, 0.4f);
            Gizmos.DrawCube(_zoneCenter, _zoneSize);
        }
    }
}
