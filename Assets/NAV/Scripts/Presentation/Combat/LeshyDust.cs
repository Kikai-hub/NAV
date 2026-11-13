using System.Collections;
using UnityEngine;
using NAV.Gameplay.AI;
using NAV.Gameplay.Bosses;
using NAV.Presentation.Animation;

namespace NAV.Presentation.Combat
{
    /// <summary>
    /// What the Leshy's weight looks like (STEAM_DEMO_PLAN.md stage 3, 2026-11-13; the camera shake
    /// and the sounds are LeshyAudio's): the slam throws up clods of earth and a ring of dust over
    /// the blow's radius; the sweep sheds leaves and bark from his branch arm; the roar shakes leaves
    /// out of his crown; each footstep puffs dust; the fallen body raises a long cloud. Uses the
    /// scene's ImpactEffects (found when first needed - the Leshy is spawned at runtime). Hooks
    /// itself to the components on the same object - no references to set.
    /// </summary>
    [RequireComponent(typeof(LeshyBoss))]
    public class LeshyDust : MonoBehaviour
    {
        [Tooltip("Seconds after death before the body hits the ground (match LeshyAudio's Fall Seconds).")]
        [SerializeField, Min(0f)] private float _fallSeconds = 1.3f;
        [Tooltip("Height of the crown above his feet (m) - where the roar shakes the leaves from.")]
        [SerializeField, Min(0f)] private float _crownHeight = 4.2f;

        private static readonly Color LeafDark = new(0.16f, 0.24f, 0.08f);
        private static readonly Color LeafLight = new(0.42f, 0.5f, 0.16f);
        private static readonly Color BarkDark = new(0.22f, 0.16f, 0.1f);
        private static readonly Color BarkLight = new(0.38f, 0.3f, 0.2f);

        private LeshyBoss _boss;
        private Creature _creature;
        private LegGrounding _legs;
        private ImpactEffects _impacts;
        private readonly RaycastHit[] _hits = new RaycastHit[8];

        private void Awake()
        {
            _boss = GetComponent<LeshyBoss>();
            _creature = GetComponent<Creature>();
            _legs = GetComponent<LegGrounding>();
        }

        private void OnEnable()
        {
            _boss.Struck += HandleStruck;
            _boss.ActionStarted += HandleAction;
            if (_creature != null)
            {
                _creature.Died += HandleDied;
            }

            if (_legs != null)
            {
                _legs.FootPlanted += HandleFootPlanted;
            }
        }

        private void OnDisable()
        {
            _boss.Struck -= HandleStruck;
            _boss.ActionStarted -= HandleAction;
            if (_creature != null)
            {
                _creature.Died -= HandleDied;
            }

            if (_legs != null)
            {
                _legs.FootPlanted -= HandleFootPlanted;
            }
        }

        private bool Ready()
        {
            if (_impacts == null)
            {
                _impacts = FindAnyObjectByType<ImpactEffects>();
            }

            return _impacts != null;
        }

        private void HandleStruck(LeshyAction action, Vector3 centre, float radius)
        {
            if (!Ready())
            {
                return;
            }

            Vector3 ground = Ground(centre);
            switch (action)
            {
                case LeshyAction.Slam:
                    _impacts.GroundSlam(ground, Mathf.Max(1.5f, radius), 1.4f);
                    break;
                case LeshyAction.Sweep:
                    // The branch arm whips past: leaves and bark fly off it, a little dust kicked up.
                    _impacts.Leaves(ground + Vector3.up * 1.4f, Mathf.Max(0.8f, radius * 0.6f), 16, LeafDark, LeafLight);
                    _impacts.Leaves(ground + Vector3.up * 1.0f, 0.6f, 6, BarkDark, BarkLight);
                    _impacts.Footfall(ground, 2f);
                    break;
            }
        }

        private void HandleAction(LeshyAction action)
        {
            if (action == LeshyAction.Roar && Ready())
            {
                _impacts.Leaves(transform.position + Vector3.up * _crownHeight, 1.6f, 26, LeafDark, LeafLight);
            }
        }

        private void HandleFootPlanted(Vector3 point)
        {
            if (_creature != null && _creature.IsAlive && Ready())
            {
                _impacts.Footfall(point, 1.3f);
            }
        }

        private void HandleDied()
        {
            StartCoroutine(FallDust());
        }

        private IEnumerator FallDust()
        {
            yield return new WaitForSeconds(_fallSeconds);
            if (!Ready())
            {
                yield break;
            }

            // A long cloud along where the body lands (it falls forward or back - cover both).
            Vector3 forward = transform.forward;
            forward.y = 0f;
            forward = forward.sqrMagnitude > 0.01f ? forward.normalized : Vector3.forward;
            for (int i = -2; i <= 2; i++)
            {
                _impacts.DustCloud(Ground(transform.position + forward * (i * 1.1f)), 1.4f, 10, new Color(0.56f, 0.49f, 0.38f, 0.7f));
            }

            _impacts.Leaves(transform.position + Vector3.up * 1.5f, 2.5f, 30, LeafDark, LeafLight);
        }

        /// <summary>The ground under a point (ignoring the Leshy's own colliders), or the point.</summary>
        private Vector3 Ground(Vector3 point)
        {
            int count = Physics.RaycastNonAlloc(point + Vector3.up * 3f, Vector3.down, _hits, 8f, ~0, QueryTriggerInteraction.Ignore);
            float best = float.MaxValue;
            Vector3 found = point;
            for (int i = 0; i < count; i++)
            {
                if (_hits[i].collider.transform.IsChildOf(transform) || _hits[i].distance >= best)
                {
                    continue;
                }

                best = _hits[i].distance;
                found = _hits[i].point;
            }

            return found;
        }
    }
}
