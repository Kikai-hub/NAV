using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.AI;

namespace NAV.Gameplay.Bosses
{
    /// <summary>
    /// Marks a creature as a boss - not tied to any particular one (CLAUDE.md "Boss Rule"):
    /// the HUD shows the health bar of the nearest boss that is fighting, and encounters
    /// (ForestWrath) know when theirs is beaten. A boss's own behaviour lives in its own
    /// component next to this one (LeshyBoss).
    /// </summary>
    [RequireComponent(typeof(Creature))]
    public class BossActor : MonoBehaviour
    {
        [Tooltip("Name on the boss health bar. Empty = the creature definition's display name.")]
        [SerializeField] private string _title;

        /// <summary>Every boss alive in the scene.</summary>
        public static readonly List<BossActor> All = new();

        public Creature Creature { get; private set; }
        public string Title => !string.IsNullOrEmpty(_title) ? _title
            : Creature != null && Creature.Definition != null ? Creature.Definition.DisplayName : name;

        /// <summary>Chasing, attacking or busy with its own action (a roar) - the bar shows.</summary>
        public bool IsFighting => Creature != null && Creature.IsAlive && (Creature.IsEngaged || _forcedFightUntil > Time.time);

        /// <summary>In its last, angriest phase - set by its behaviour (the bar turns red).</summary>
        public bool IsEnraged { get; set; }

        private float _forcedFightUntil;

        private void Awake()
        {
            Creature = GetComponent<Creature>();
        }

        private void OnEnable()
        {
            All.Add(this);
        }

        private void OnDisable()
        {
            All.Remove(this);
        }

        /// <summary>Shows the bar for a while although it isn't chasing yet (an entrance roar).</summary>
        public void ShowBarFor(float seconds)
        {
            _forcedFightUntil = Mathf.Max(_forcedFightUntil, Time.time + seconds);
        }

        /// <summary>The nearest fighting boss within maxDistance of a point, or null.</summary>
        public static BossActor NearestFighting(Vector3 point, float maxDistance)
        {
            BossActor best = null;
            float bestSqr = maxDistance * maxDistance;
            foreach (BossActor boss in All)
            {
                if (boss == null || !boss.IsFighting)
                {
                    continue;
                }

                float sqr = (boss.transform.position - point).sqrMagnitude;
                if (sqr <= bestSqr)
                {
                    bestSqr = sqr;
                    best = boss;
                }
            }

            return best;
        }
    }
}
