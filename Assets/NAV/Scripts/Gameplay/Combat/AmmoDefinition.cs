using UnityEngine;

namespace NAV.Gameplay.Combat
{
    /// <summary>
    /// What an arrow adds when loosed from a bow (PlayerArchery): its own damage on top of the
    /// bow's, the model that flies (ArrowProjectile - visual only, no collider), and how often it
    /// survives a miss (stuck in the ground or a tree, to be picked up again). Referenced from the
    /// arrow's ItemDefinition (Ammo).
    /// </summary>
    [CreateAssetMenu(fileName = "Ammo", menuName = "NAV/Combat/Ammo Definition")]
    public class AmmoDefinition : ScriptableObject
    {
        [SerializeField, Min(0f)] private float _damage = 10f;
        [Tooltip("The flying arrow (model only): pivot at the tip, the shaft running back along -Z. Its colliders are ignored.")]
        [SerializeField] private GameObject _projectilePrefab;
        [Tooltip("Chance a missed arrow stays whole where it struck, to be picked up again.")]
        [SerializeField, Range(0f, 1f)] private float _recoverChance = 0.6f;

        public float Damage => _damage;
        public GameObject ProjectilePrefab => _projectilePrefab;
        public float RecoverChance => _recoverChance;
    }
}
