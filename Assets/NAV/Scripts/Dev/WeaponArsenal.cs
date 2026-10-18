using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Dev
{
    /// <summary>
    /// TEMPORARY (developer request, 2026-10-17): the pack weapons have no resources/recipes
    /// yet, so they are handed out from weapon racks near the spawn instead of being crafted.
    /// Lists the racks - one per weapon class, split so each fits the 20-slot container panel.
    /// Filled by NAV > Items > Generate Weapons From LowPoly Pack; WeaponArsenalSpawner places
    /// the racks. Delete this (and the spawner) once the weapons are craftable.
    /// </summary>
    [CreateAssetMenu(fileName = "WeaponArsenal", menuName = "NAV/Dev/Weapon Arsenal")]
    public class WeaponArsenal : ScriptableObject
    {
        [Serializable]
        public class Rack
        {
            public string Title;
            public List<ItemDefinition> Items = new();
        }

        [SerializeField] private List<Rack> _racks = new();

        public IReadOnlyList<Rack> Racks => _racks;

        public void SetRacks(List<Rack> racks)
        {
            _racks = racks;
        }
    }
}
