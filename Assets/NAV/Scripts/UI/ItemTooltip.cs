using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Items;

namespace NAV.UI
{
    /// <summary>
    /// The parchment item tooltip (NAVTheme .nav-tooltip): icon, name, description, weight,
    /// armour/damage when relevant, and what right-click does. One instance per panel, added to
    /// that panel's root and shown next to the hovered slot. Pure view - built in code because
    /// both the inventory and the container panel use it.
    /// </summary>
    public sealed class ItemTooltip
    {
        private const float Gap = 6f;

        private readonly VisualElement _root;
        private readonly VisualElement _card;
        private readonly VisualElement _icon;
        private readonly Label _name;
        private readonly Label _description;
        private readonly Label _stats;
        private readonly Label _hint;

        public ItemTooltip(VisualElement root)
        {
            _root = root;
            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.AddToClassList("nav-tooltip");

            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("nav-tooltip__header");
            var iconFrame = new VisualElement { pickingMode = PickingMode.Ignore };
            iconFrame.AddToClassList("nav-tooltip__icon");
            _icon = new VisualElement { pickingMode = PickingMode.Ignore };
            _icon.AddToClassList("nav-tooltip__icon-image");
            iconFrame.Add(_icon);
            header.Add(iconFrame);
            _name = NewLabel("nav-tooltip__name");
            header.Add(_name);
            _card.Add(header);

            _description = NewLabel("nav-tooltip__description");
            _card.Add(_description);
            _stats = NewLabel("nav-tooltip__line");
            _card.Add(_stats);
            _hint = NewLabel("nav-tooltip__hint");
            _card.Add(_hint);

            _card.style.display = DisplayStyle.None;
            _root.Add(_card);
        }

        /// <summary>Shows the item next to the slot (to its right, or left if that would leave
        /// the screen). hint: what right-click does, or null.</summary>
        public void Show(ItemDefinition item, VisualElement slot, string hint)
        {
            if (item == null)
            {
                Hide();
                return;
            }

            _icon.style.backgroundImage = new StyleBackground(item.Icon);
            _name.text = item.DisplayName;
            _description.text = item.Description;
            _description.style.display = string.IsNullOrEmpty(item.Description) ? DisplayStyle.None : DisplayStyle.Flex;

            string stats = $"Weight: {item.Weight:0.##} kg";
            if (item.IsArmor)
            {
                stats += $"\nArmour: {item.Armor.Armor:0.#} ({item.Armor.Slot})";
            }
            else if (item.Weapon != null)
            {
                stats += $"\nDamage: {item.Weapon.Damage:0.#}";
            }

            _stats.text = stats;
            _hint.text = hint ?? string.Empty;
            _hint.style.display = string.IsNullOrEmpty(hint) ? DisplayStyle.None : DisplayStyle.Flex;

            _card.style.display = DisplayStyle.Flex;
            _card.BringToFront();

            // Position in the root's space; flip to the slot's left near the right edge.
            Rect slotRect = slot.ChangeCoordinatesTo(_root, slot.contentRect);
            float width = _card.resolvedStyle.width > 0f ? _card.resolvedStyle.width : 230f;
            float left = slotRect.xMax + Gap;
            if (left + width > _root.layout.width)
            {
                left = slotRect.xMin - Gap - width;
            }

            _card.style.left = Mathf.Max(0f, left);
            _card.style.top = Mathf.Max(0f, slotRect.yMin);
        }

        public void Hide()
        {
            _card.style.display = DisplayStyle.None;
        }

        /// <summary>What right-click does with this item in the player's own inventory.</summary>
        public static string UseHint(ItemDefinition item)
        {
            if (item == null)
            {
                return null;
            }

            if (item.IsEdible)
            {
                return "Right-click to eat";
            }

            return item.IsArmor || item.IsEquippable ? "Right-click to equip" : null;
        }

        private static Label NewLabel(string className)
        {
            var label = new Label { pickingMode = PickingMode.Ignore };
            label.AddToClassList(className);
            return label;
        }
    }
}
