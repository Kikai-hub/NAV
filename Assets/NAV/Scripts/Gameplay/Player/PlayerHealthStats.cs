using UnityEngine;

namespace NAV.Gameplay.Player
{
    /// <summary>
    /// Deliberately minimal - just enough data for PlayerHealth to back the new HP HUD bar.
    /// No regen/damage-type/death fields yet: Health/Regeneration/Death are their own separate
    /// Roadmap Phase 6 items, and there are no damage sources at all until Combat (Phase 7)
    /// exists. This is not "Health system" work starting early, just the data PlayerHudController
    /// needs to have something real to show.
    /// </summary>
    [CreateAssetMenu(fileName = "PlayerHealthStats", menuName = "NAV/Player/Health Stats")]
    public class PlayerHealthStats : ScriptableObject
    {
        [SerializeField] private float _maxHealth = 100f;

        public float MaxHealth => _maxHealth;

        private void OnValidate()
        {
            _maxHealth = Mathf.Max(1f, _maxHealth);
        }
    }
}
