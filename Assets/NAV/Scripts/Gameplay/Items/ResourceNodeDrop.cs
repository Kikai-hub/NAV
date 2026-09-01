using System;
using UnityEngine;

namespace NAV.Gameplay.Items
{
    /// <summary>
    /// One possible yield from a fully depleted resource node: an item, how much of it, and
    /// the independent chance (0-1) it's included when the node is depleted. A node lists one
    /// or more of these so felling/exhausting it can scatter several different items at once -
    /// e.g. a tree mostly yields Wood (chance 1) but occasionally also yields Resin (a lower
    /// chance). Plain serializable data, same shape/role as RecipeIngredient but for gathering
    /// instead of crafting costs - kept as its own type rather than reusing RecipeIngredient
    /// since Chance has no meaning there.
    /// </summary>
    [Serializable]
    public class ResourceNodeDrop
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _amount = 1;
        [SerializeField, Range(0f, 1f)] private float _chance = 1f;

        public ItemDefinition Item => _item;
        public int Amount => Mathf.Max(1, _amount);
        public float Chance => Mathf.Clamp01(_chance);
    }
}
