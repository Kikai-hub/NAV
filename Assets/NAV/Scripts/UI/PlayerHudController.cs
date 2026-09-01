using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Gameplay.Building;
using NAV.Gameplay.Combat;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// Always-on gameplay HUD (as opposed to InventoryUIController/CraftingUIController, which
    /// are toggleable panels): HP bar, a hotbar mirroring the first HotbarSlotCount slots of
    /// the player's main Inventory, a stamina bar that fades in as soon as stamina starts
    /// draining and stays up (through sprinting/overload and the regen that follows) until it's
    /// fully back to max, and a center-screen aim crosshair. Purely a view over
    /// PlayerHealth/PlayerStamina/PlayerInventory/PlayerInteractor/PlayerCombat/PlayerBuilding -
    /// owns no gameplay state (ARCHITECTURE_v0.1.md's "UI observes gameplay state" rule). The
    /// hotbar has no selection/quick-use yet - that needs an Equipment/item-use system that
    /// doesn't exist yet; for now it's a read-only mirror of the main Inventory panel (dragging
    /// an item into one of its first HotbarSlotCount slots there makes it appear here
    /// automatically, since it's the same underlying Inventory). HotbarSlotCount matches
    /// InventoryPanel's per-row column count (6, from its 400px max-width / 64px-per-slot box)
    /// so the hotbar's width lines up with the inventory grid's.
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class PlayerHudController : MonoBehaviour
    {
        private const int HotbarSlotCount = 6;
        private const float StaminaFullEpsilon = 0.01f;

        [SerializeField] private PlayerHealth _health;
        [SerializeField] private PlayerStamina _stamina;
        [SerializeField] private PlayerInventory _playerInventory;
        [SerializeField] private PlayerInputHandler _inputHandler;
        [SerializeField] private PlayerInteractor _interactor;
        [SerializeField] private PlayerCombat _combat;

        [Tooltip("Optional - only used to tint the crosshair green/red while build mode is active. Crosshair falls back to its normal interact/attack/neutral states if left unassigned.")]
        [SerializeField] private PlayerBuilding _building;

        private UIDocument _document;
        private VisualElement _hotbarContainer;
        private VisualElement _healthFill;
        private VisualElement _staminaBar;
        private VisualElement _staminaFill;
        private VisualElement _crosshair;
        private VisualElement _crosshairRing;
        private VisualElement _crosshairDot;
        private readonly List<VisualElement> _hotbarSlots = new();

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_health == null || _stamina == null || _playerInventory == null || _inputHandler == null || _interactor == null || _combat == null)
            {
                Debug.LogError($"{nameof(PlayerHudController)} on '{name}' is missing a required reference (Health/Stamina/PlayerInventory/InputHandler/Interactor/Combat).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            // Same reasoning as InventoryUIController: query the built UI tree in Start, not
            // Awake/OnEnable, since UIDocument builds rootVisualElement in its own OnEnable and
            // PlayerInventory creates its Inventory in its own Awake - neither is guaranteed to
            // have run yet by this object's Awake/OnEnable, but Start runs after every object's
            // Awake+OnEnable has.
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(PlayerHudController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _hotbarContainer = documentRoot.Q<VisualElement>("hotbar-container");
            _healthFill = documentRoot.Q<VisualElement>("health-bar-fill");
            _staminaBar = documentRoot.Q<VisualElement>("stamina-bar");
            _staminaFill = documentRoot.Q<VisualElement>("stamina-bar-fill");
            _crosshair = documentRoot.Q<VisualElement>("crosshair");
            _crosshairRing = documentRoot.Q<VisualElement>("crosshair-ring");
            _crosshairDot = documentRoot.Q<VisualElement>("crosshair-dot");

            if (_hotbarContainer == null || _healthFill == null || _staminaBar == null || _staminaFill == null
                || _crosshair == null || _crosshairRing == null || _crosshairDot == null)
            {
                Debug.LogError($"{nameof(PlayerHudController)} on '{name}' could not find one or more required elements in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            _health.Changed += RefreshHealth;
            _playerInventory.Inventory.Changed += RefreshHotbar;

            BuildHotbarSlots();
            RefreshHealth();
            RefreshHotbar();
            RefreshStamina();
            RefreshCrosshair();
        }

        private void OnDestroy()
        {
            if (_health != null)
            {
                _health.Changed -= RefreshHealth;
            }

            if (_playerInventory != null && _playerInventory.Inventory != null)
            {
                _playerInventory.Inventory.Changed -= RefreshHotbar;
            }
        }

        private void Update()
        {
            // Stamina has no Changed event (it drains/regens continuously, not on discrete
            // state changes), so it's polled every frame - same pattern PlayerDebugHud already
            // uses for the same field.
            RefreshStamina();
            RefreshCrosshair();
        }

        private void BuildHotbarSlots()
        {
            _hotbarContainer.Clear();
            _hotbarSlots.Clear();

            for (int i = 0; i < HotbarSlotCount; i++)
            {
                var slot = new VisualElement();
                slot.AddToClassList("hotbar-slot");

                var icon = new VisualElement();
                icon.AddToClassList("hotbar-slot__icon");
                slot.Add(icon);

                var quantity = new Label();
                quantity.AddToClassList("hotbar-slot__quantity");
                slot.Add(quantity);

                _hotbarContainer.Add(slot);
                _hotbarSlots.Add(slot);
            }
        }

        private void RefreshHotbar()
        {
            IReadOnlyList<ItemStack> slots = _playerInventory.Inventory.Slots;

            for (int i = 0; i < _hotbarSlots.Count && i < slots.Count; i++)
            {
                VisualElement icon = _hotbarSlots[i].Q<VisualElement>(className: "hotbar-slot__icon");
                Label quantity = _hotbarSlots[i].Q<Label>(className: "hotbar-slot__quantity");

                ItemStack stack = slots[i];
                if (stack.IsEmpty)
                {
                    icon.style.backgroundImage = new StyleBackground();
                    quantity.text = string.Empty;
                }
                else
                {
                    icon.style.backgroundImage = new StyleBackground(stack.Definition.Icon);
                    quantity.text = stack.Quantity > 1 ? stack.Quantity.ToString() : string.Empty;
                }
            }
        }

        private void RefreshHealth()
        {
            float fraction = _health.MaxHealth > 0f ? Mathf.Clamp01(_health.CurrentHealth / _health.MaxHealth) : 0f;
            _healthFill.style.height = Length.Percent(fraction * 100f);
        }

        private void RefreshStamina()
        {
            // Visible from the moment stamina starts draining until it's fully back to max -
            // not just while IsDraining is true - and faded (opacity, animated by the
            // transition on .stamina-bar in PlayerHud.uss) rather than snapped away the
            // instant sprint/overload drain stops.
            bool isFull = _stamina.CurrentStamina >= _stamina.MaxStamina - StaminaFullEpsilon;
            _staminaBar.style.opacity = isFull ? 0f : 1f;

            float fraction = _stamina.MaxStamina > 0f ? Mathf.Clamp01(_stamina.CurrentStamina / _stamina.MaxStamina) : 0f;
            _staminaFill.style.width = Length.Percent(fraction * 100f);
        }

        /// <summary>
        /// Center-screen aim reticle so aiming/interacting/attacking is readable without the F1
        /// debug HUD. Hidden entirely while a modal panel has cursor focus (nothing to aim at -
        /// same MenuOpen signal PlayerInputHandler already uses to freeze the camera). Otherwise
        /// picks one state, most-specific first: build mode's placement validity, then "looking
        /// at something interactable" (PlayerInteractor - gathering/pickup/workbench-adjacent/
        /// demolish), then "looking at something attackable" (PlayerCombat.HasTargetInSight),
        /// then a plain neutral reticle. Only ever reads state other systems already expose -
        /// no new gameplay logic beyond PlayerCombat's small HasTargetInSight query.
        /// </summary>
        private void RefreshCrosshair()
        {
            if (_inputHandler.MenuOpen)
            {
                _crosshair.style.display = DisplayStyle.None;
                return;
            }

            _crosshair.style.display = DisplayStyle.Flex;

            string state;
            if (_building != null && _building.IsBuildModeActive)
            {
                state = _building.CanPlace ? "valid" : "invalid";
            }
            else if (_interactor.CurrentInteractable != null)
            {
                state = "interact";
            }
            else if (_combat.HasTargetInSight)
            {
                state = "attack";
            }
            else
            {
                state = "neutral";
            }

            SetCrosshairState(state);
        }

        private static readonly string[] CrosshairStates = { "neutral", "interact", "attack", "valid", "invalid" };

        private void SetCrosshairState(string state)
        {
            // Only the ring's color changes with aim state - the dot is a fixed white center
            // point (see .crosshair-dot in PlayerHud.uss), so it doesn't need these classes.
            foreach (string candidate in CrosshairStates)
            {
                _crosshairRing.EnableInClassList($"crosshair--{candidate}", candidate == state);
            }
        }
    }
}
