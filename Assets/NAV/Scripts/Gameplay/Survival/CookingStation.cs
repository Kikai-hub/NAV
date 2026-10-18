using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Survival
{
    /// <summary>One thing a CookingStation can cook: Input -> Output after CookSeconds.</summary>
    [Serializable]
    public class CookingRecipe
    {
        [SerializeField] private ItemDefinition _input;
        [SerializeField] private ItemDefinition _output;
        [SerializeField, Min(0.1f)] private float _cookSeconds = 10f;

        public ItemDefinition Input => _input;
        public ItemDefinition Output => _output;
        public float CookSeconds => _cookSeconds;
    }

    /// <summary>
    /// A single-slot cooking spit (Valheim's cooking station, simplified): E with a cookable
    /// item in the inventory puts one on the spit, it cooks for the recipe's CookSeconds, then E
    /// again takes the result (dropped next to the station if the inventory is full). Raycast
    /// IInteractable like ItemPickup/ResourceNode. Lives on a child collider of the Campfire
    /// building piece, so aiming at the spit cooks while aiming at the fire's base still reaches
    /// BuildingPiece's Demolish - PlayerInteractor resolves the nearest IInteractable upward
    /// from the collider it hit. Out of scope for now: burning food left too long, more than
    /// one slot, fuel. Whatever is on the spit is lost if the campfire is demolished.
    /// </summary>
    public class CookingStation : MonoBehaviour, IInteractable
    {
        private enum State { Idle, Cooking, Ready }

        [SerializeField] private List<CookingRecipe> _recipes = new();

        [Tooltip("Optional. Shown while something is cooking.")]
        [SerializeField] private GameObject _cookingVisual;

        [Tooltip("Optional. Shown when the food is ready to take.")]
        [SerializeField] private GameObject _readyVisual;

        [SerializeField] private float _dropDistance = 0.8f;

        private State _state;
        private CookingRecipe _current;
        private float _timer;

        public string InteractionPrompt => _state switch
        {
            State.Cooking => $"Cooking {_current.Output.DisplayName}... {Mathf.CeilToInt(_timer)}s",
            State.Ready => $"Take {_current.Output.DisplayName}",
            _ => _recipes.Count > 0 && _recipes[0].Input != null ? $"Cook ({_recipes[0].Input.DisplayName})" : "Cook",
        };

        private void Awake()
        {
            _recipes.RemoveAll(r => r == null || r.Input == null || r.Output == null);
            if (_recipes.Count == 0)
            {
                Debug.LogError($"{nameof(CookingStation)} on '{name}' has no valid recipes (each needs Input and Output).", this);
                enabled = false;
            }

            RefreshVisuals();
        }

        private void Update()
        {
            if (_state != State.Cooking)
            {
                return;
            }

            _timer -= Time.deltaTime;
            if (_timer <= 0f)
            {
                _state = State.Ready;
                RefreshVisuals();
            }
        }

        public bool CanInteract(GameObject interactor) => enabled;

        public void Interact(GameObject interactor)
        {
            var inventory = interactor.GetComponent<PlayerInventory>();
            if (inventory == null)
            {
                return;
            }

            switch (_state)
            {
                case State.Idle:
                    TryStartCooking(inventory);
                    break;
                case State.Ready:
                    TakeResult(inventory);
                    break;
            }
        }

        private void TryStartCooking(PlayerInventory inventory)
        {
            foreach (CookingRecipe recipe in _recipes)
            {
                if (inventory.Inventory.GetTotalQuantity(recipe.Input) <= 0)
                {
                    continue;
                }

                inventory.Inventory.RemoveItem(recipe.Input, 1);
                _current = recipe;
                _timer = recipe.CookSeconds;
                _state = State.Cooking;
                RefreshVisuals();
                Debug.Log($"{name}: cooking {recipe.Input.DisplayName} -> {recipe.Output.DisplayName} ({recipe.CookSeconds}s).", this);
                return;
            }

            Debug.Log($"{name}: nothing cookable in {inventory.name}'s inventory.", this);
        }

        private void TakeResult(PlayerInventory inventory)
        {
            int leftover = inventory.Inventory.AddItem(_current.Output, 1);
            if (leftover > 0)
            {
                Vector3 dropPosition = transform.position + transform.forward * _dropDistance;
                ItemPickup.SpawnInWorld(_current.Output, leftover, dropPosition, Quaternion.identity, Vector3.zero);
            }

            _current = null;
            _state = State.Idle;
            RefreshVisuals();
        }

        private void RefreshVisuals()
        {
            if (_cookingVisual != null)
            {
                _cookingVisual.SetActive(_state == State.Cooking);
            }

            if (_readyVisual != null)
            {
                _readyVisual.SetActive(_state == State.Ready);
            }
        }
    }
}
