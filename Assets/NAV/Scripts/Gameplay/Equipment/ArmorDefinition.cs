using System;
using System.Collections.Generic;
using UnityEngine;

namespace NAV.Gameplay.Equipment
{
    /// <summary>One model shown on the character while the armour is worn.</summary>
    [Serializable]
    public class ArmorWornPart
    {
        [SerializeField] private ArmorBone _bone;
        [Tooltip("Model parented to the bone. Its axes are the character's (+Y up, +Z forward), its origin is the bone's pivot. No colliders.")]
        [SerializeField] private GameObject _prefab;

        public ArmorBone Bone => _bone;
        public GameObject Prefab => _prefab;
    }

    /// <summary>
    /// Armour stats for an item (ItemDefinition.Armor), Valheim style: a slot and an armour
    /// value. The values of all worn pieces add up and reduce every incoming hit (ArmorMath,
    /// applied in PlayerCombat.TakeDamage after block/parry). Worn Parts are purely visual
    /// (ArmorView).
    /// </summary>
    [CreateAssetMenu(fileName = "ArmorDefinition", menuName = "NAV/Combat/Armor Definition")]
    public class ArmorDefinition : ScriptableObject
    {
        [SerializeField] private ArmorSlot _slot;
        [SerializeField, Min(0f)] private float _armor = 1f;
        [SerializeField] private List<ArmorWornPart> _wornParts = new();

        public ArmorSlot Slot => _slot;
        public float Armor => _armor;
        public IReadOnlyList<ArmorWornPart> WornParts => _wornParts;
    }
}
