using System;
using UnityEngine;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Crafting
{
    /// <summary>
    /// One ingredient cost line in a RecipeDefinition: an item + how many of it are consumed.
    /// Plain serializable data, not a ScriptableObject - it only ever exists nested inside a
    /// RecipeDefinition's ingredient list.
    /// </summary>
    [Serializable]
    public class RecipeIngredient
    {
        [SerializeField] private ItemDefinition _item;
        [SerializeField] private int _amount = 1;

        public ItemDefinition Item => _item;
        public int Amount => Mathf.Max(1, _amount);
    }
}
