using System;
using System.Collections.Generic;
using UnityEngine;
using NAV.Gameplay.Interaction;
using NAV.Gameplay.Inventory;
using NAV.Gameplay.Items;

namespace NAV.Gameplay.Crafting
{
    /// <summary>One thing a ProcessingStation turns into another: Amount of Input (+ the
    /// station's fuel) -> Output Amount of Output after Seconds.</summary>
    [Serializable]
    public class ProcessingRecipe
    {
        [SerializeField] private ItemDefinition _input;
        [SerializeField, Min(1)] private int _inputAmount = 1;
        [SerializeField] private ItemDefinition _output;
        [SerializeField, Min(1)] private int _outputAmount = 1;
        [SerializeField, Min(0.1f)] private float _seconds = 15f;

        public ItemDefinition Input => _input;
        public int InputAmount => _inputAmount;
        public ItemDefinition Output => _output;
        public int OutputAmount => _outputAmount;
        public float Seconds => _seconds;
    }

    /// <summary>
    /// A station that works on its own over time, Valheim smelter/kiln style: E loads everything
    /// it accepts from the player's inventory (inputs up to Input Capacity in total, fuel up to
    /// Fuel Capacity), then it processes one input at a time - burning Fuel Per Item of fuel
    /// each, if it uses fuel - and drops each result out of its Output Point as a pickup. The
    /// charcoal kiln (wood -> charcoal), the bloomery (ore + charcoal -> bars) and the tanning
    /// rack (hide -> leather) are all this component with different recipes.
    ///
    /// Loaded inputs and fuel live in Contents (IStoredContents): saved with the building and
    /// spilled when it's demolished. The progress on the current item isn't saved - it restarts
    /// after a load. Lives on a child collider of the building piece (like CookingStation), so
    /// the hammer still reaches the piece itself.
    /// </summary>
    public class ProcessingStation : MonoBehaviour, IInteractable, IStoredContents
    {
        [SerializeField] private string _stationName = "Station";
        [SerializeField] private List<ProcessingRecipe> _recipes = new();

        [Header("Fuel (optional)")]
        [SerializeField] private ItemDefinition _fuel;
        [SerializeField, Min(1)] private int _fuelPerItem = 1;
        [SerializeField, Min(1)] private int _fuelCapacity = 20;

        [Header("Capacity / Output")]
        [Tooltip("How many inputs (all kinds together) it can hold.")]
        [SerializeField, Min(1)] private int _inputCapacity = 10;
        [Tooltip("Where finished items pop out. Defaults to this transform.")]
        [SerializeField] private Transform _outputPoint;
        [SerializeField] private float _outputPush = 1.5f;

        [Header("Visuals (optional)")]
        [Tooltip("Shown while working (fire, glow, smoke).")]
        [SerializeField] private GameObject _workingVisual;
        [Tooltip("Shown while it holds inputs (ore in the hearth, hides on the frame).")]
        [SerializeField] private GameObject _loadedVisual;

        private const int ContentSlots = 8;

        private ProcessingRecipe _current;
        private float _progress;

        public Inventory.Inventory Contents { get; private set; }
        public bool IsWorking => _current != null;

        /// <summary>Raised when an item is finished (for sound/particles).</summary>
        public event Action<ItemDefinition> Produced;

        public string InteractionPrompt
        {
            get
            {
                string queued = $"{QueuedInputs}/{_inputCapacity}";
                string fuel = _fuel != null ? $", {_fuel.DisplayName} {Contents.GetTotalQuantity(_fuel)}/{_fuelCapacity}" : "";
                string status = IsWorking ? $" - making {_current.Output.DisplayName} {Mathf.CeilToInt(_current.Seconds - _progress)}s" : "";
                return $"Load {_stationName} ({queued}{fuel}){status}";
            }
        }

        private int QueuedInputs
        {
            get
            {
                int total = 0;
                foreach (ProcessingRecipe recipe in _recipes)
                {
                    total += Contents.GetTotalQuantity(recipe.Input);
                }

                return total;
            }
        }

        private void Awake()
        {
            _recipes.RemoveAll(r => r == null || r.Input == null || r.Output == null);
            if (_recipes.Count == 0)
            {
                Debug.LogError($"{nameof(ProcessingStation)} on '{name}' has no valid recipes (each needs Input and Output).", this);
                enabled = false;
            }

            Contents = new Inventory.Inventory(ContentSlots);
            Contents.Changed += RefreshVisuals;
            if (_outputPoint == null)
            {
                _outputPoint = transform;
            }

            RefreshVisuals();
        }

        /// <summary>Multiplayer: only one machine (the host) runs the stations, so a furnace
        /// doesn't smelt the same ore on two machines - the others see its contents change.</summary>
        public static bool SimulationSuppressed { get; set; }

        private void Update()
        {
            if (SimulationSuppressed)
            {
                return;
            }

            if (_current == null)
            {
                _current = NextRecipe();
                _progress = 0f;
                RefreshVisuals();
                return;
            }

            _progress += Time.deltaTime;
            if (_progress < _current.Seconds)
            {
                return;
            }

            // Consumed on completion, so an interrupted item (demolished, or reloaded) is never lost.
            if (Contents.GetTotalQuantity(_current.Input) >= _current.InputAmount && HasFuelFor(_current))
            {
                Contents.RemoveItem(_current.Input, _current.InputAmount);
                if (_fuel != null)
                {
                    Contents.RemoveItem(_fuel, _fuelPerItem);
                }

                Vector3 push = (_outputPoint.forward + Vector3.up * 0.5f).normalized * _outputPush;
                ItemPickup.SpawnInWorld(_current.Output, _current.OutputAmount, _outputPoint.position, Quaternion.identity, push);
                Produced?.Invoke(_current.Output);
            }

            _current = null;
            _progress = 0f;
            RefreshVisuals();
        }

        /// <summary>The first recipe it has input (and fuel) for, or null.</summary>
        private ProcessingRecipe NextRecipe()
        {
            foreach (ProcessingRecipe recipe in _recipes)
            {
                if (NAV.Gameplay.Demo.DemoMode.Allows(recipe) && Contents.GetTotalQuantity(recipe.Input) >= recipe.InputAmount && HasFuelFor(recipe))
                {
                    return recipe;
                }
            }

            return null;
        }

        private bool HasFuelFor(ProcessingRecipe recipe)
        {
            return _fuel == null || Contents.GetTotalQuantity(_fuel) >= _fuelPerItem;
        }

        public bool CanInteract(GameObject interactor) => enabled;

        /// <summary>Loads everything it accepts from the interactor's inventory, as far as it has
        /// room.</summary>
        public void Interact(GameObject interactor)
        {
            var playerInventory = interactor.GetComponent<PlayerInventory>();
            if (playerInventory == null || playerInventory.Inventory == null)
            {
                return;
            }

            Inventory.Inventory from = playerInventory.Inventory;
            int loaded = 0;
            foreach (ProcessingRecipe recipe in _recipes)
            {
                if (!NAV.Gameplay.Demo.DemoMode.Allows(recipe))
                {
                    continue; // the demo doesn't smelt what is past it (tin, iron, silver...)
                }

                int room = _inputCapacity - QueuedInputs;
                loaded += Move(from, recipe.Input, Mathf.Min(room, from.GetTotalQuantity(recipe.Input)));
            }

            if (_fuel != null)
            {
                int room = _fuelCapacity - Contents.GetTotalQuantity(_fuel);
                loaded += Move(from, _fuel, Mathf.Min(room, from.GetTotalQuantity(_fuel)));
            }

            Debug.Log(loaded > 0 ? $"{_stationName}: loaded {loaded} item(s)." : $"{_stationName}: nothing to load (or it's full).", this);
        }

        private int Move(Inventory.Inventory from, ItemDefinition item, int amount)
        {
            if (amount <= 0)
            {
                return 0;
            }

            int taken = from.RemoveItem(item, amount);
            int leftover = Contents.AddItem(item, taken);
            if (leftover > 0)
            {
                // No slot left for it here - give it back.
                from.AddItem(item, leftover);
            }

            return taken - leftover;
        }

        public void DropContents(Vector3 center, float scatterRadius, float scatterForce, float upwardForce)
        {
            IReadOnlyList<ItemStack> slots = Contents.Slots;
            for (int i = 0; i < slots.Count; i++)
            {
                ItemStack stack = slots[i];
                if (stack.IsEmpty)
                {
                    continue;
                }

                ItemDefinition item = stack.Definition;
                Contents.RemoveFromSlot(i, stack.Quantity, out int removed);
                Vector2 offset = UnityEngine.Random.insideUnitCircle * scatterRadius;
                Vector3 outward = new Vector3(offset.x, 0f, offset.y);
                Vector3 impulse = (outward.sqrMagnitude > 0.0001f ? outward.normalized : Vector3.forward) * scatterForce + Vector3.up * upwardForce;
                ItemPickup.SpawnInWorld(item, removed, center + outward, Quaternion.identity, impulse);
            }
        }

        private void RefreshVisuals()
        {
            if (_workingVisual != null)
            {
                _workingVisual.SetActive(_current != null);
            }

            if (_loadedVisual != null && Contents != null)
            {
                _loadedVisual.SetActive(QueuedInputs > 0);
            }
        }
    }
}
