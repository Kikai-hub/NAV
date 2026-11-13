using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NAV.Core.Localization;
using NAV.Core.Settings;
using NAV.Gameplay.Building;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Player;
using NAV.Gameplay.Survival;
using NAV.Gameplay.Tutorial;

namespace NAV.UI
{
    /// <summary>
    /// First-steps hints (STEAM_DEMO_PLAN.md stage 3 "onboarding", 2026-11-13): one short hint at a
    /// time on the left of the screen, leading through the demo - move, gather, hammer, workbench,
    /// roof, stone axe, food, tanning rack, the Leshy, pickaxe and copper, smelting, the forge and
    /// copper armour. A hint goes away by itself once the player has done it (checked twice a
    /// second, mostly from what the player has ever owned - PlayerCrafting discovery), with a short
    /// green "done". Steps already done are skipped silently (a loaded world). Progress is saved
    /// with the world (TutorialProgress); Settings > Controls > Show Hints turns them off. Key names
    /// come from the player's own bindings. Builds itself in the game scene - no setup.
    /// </summary>
    public class HintsUI : MonoBehaviour
    {
        private const float CheckSeconds = 0.5f;
        private const float DoneSeconds = 1.6f;
        private const float FirstDelaySeconds = 3f;

        private sealed class Step
        {
            public string Id;
            public Func<string> Text;
            public Func<bool> Done;
        }

        private readonly List<Step> _steps = new();
        private PlayerMotor _player;
        private PlayerCrafting _crafting;
        private PlayerBuilding _building;
        private PlayerFood _food;
        private InputActionAsset _actions;
        private Vector3 _start; // where the player was at the last check
        private float _walked;

        private VisualElement _card;
        private Label _text;
        private Label _count;
        private int _current = -1;
        private float _nextCheck;
        private float _doneUntil = -1f;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (FindAnyObjectByType<HintsUI>() != null)
            {
                return;
            }

            var player = FindAnyObjectByType<PlayerMotor>();
            UIDocument any = FindObjectsByType<UIDocument>(FindObjectsInactive.Include).FirstOrDefault(d => d.panelSettings != null);
            if (player == null || any == null)
            {
                return;
            }

            var host = new GameObject(nameof(HintsUI));
            SceneManager.MoveGameObjectToScene(host, player.gameObject.scene);
            host.AddComponent<HintsUI>().Init(player, any.panelSettings);
        }

        private T Find<T>() where T : Component
        {
            T found = _player.GetComponentInParent<T>();
            if (found == null)
            {
                found = _player.GetComponentInChildren<T>();
            }

            return found != null ? found : FindAnyObjectByType<T>();
        }

        private void Init(PlayerMotor player, PanelSettings panelSettings)
        {
            _player = player;
            _start = player.transform.position;
            _crafting = Find<PlayerCrafting>();
            _building = Find<PlayerBuilding>();
            _food = Find<PlayerFood>();
            var input = Find<PlayerInputHandler>();
            _actions = input != null ? input.Actions : null;

            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = panelSettings;
            document.sortingOrder = 5;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            var style = Resources.Load<StyleSheet>("UI/HintsPanel");
            if (style != null)
            {
                root.styleSheets.Add(style);
            }

            _card = new VisualElement { pickingMode = PickingMode.Ignore };
            _card.AddToClassList("hint-card");
            _card.AddToClassList("nav-window");
            root.Add(_card);
            var header = new VisualElement { pickingMode = PickingMode.Ignore };
            header.AddToClassList("hint-card__header");
            _card.Add(header);
            var title = new Label(Loc.T("Hint")) { pickingMode = PickingMode.Ignore };
            title.AddToClassList("hint-card__title");
            header.Add(title);
            _count = new Label { pickingMode = PickingMode.Ignore };
            _count.AddToClassList("hint-card__count");
            _count.AddToClassList(Loc.NoLocClass);
            header.Add(_count);
            _text = new Label { pickingMode = PickingMode.Ignore };
            _text.AddToClassList("hint-card__text");
            _text.AddToClassList(Loc.NoLocClass); // already in the player's language
            _card.Add(_text);

            BuildSteps();
            _nextCheck = Time.time + FirstDelaySeconds;
            Loc.Changed += Refresh;
        }

        private void OnDestroy()
        {
            Loc.Changed -= Refresh;
        }

        // ---- The steps ----

        private bool Has(string itemId) => _crafting != null && _crafting.DiscoveredItems.Contains(itemId);

        private void BuildSteps()
        {
            Add("move", () => Loc.F("Walk with {0} {1} {2} {3} and look around with the mouse. {4} - sprint, {5} - jump.",
                    Key("Move", "up"), Key("Move", "left"), Key("Move", "down"), Key("Move", "right"), Key("Sprint"), Key("Jump")),
                () => _walked > 15f);
            Add("gather", () => Loc.F("Pick up branches and stones lying on the ground: aim at them and press {0}.", Key("Interact")),
                () => Has("wood") && Has("Rock"));
            Add("hammer", () => Loc.F("Open crafting ({0}) and make a Wood Hammer - everything is built with it.", Key("ToggleCrafting")),
                () => Has("WoodHammer"));
            Add("buildmode", () => Loc.F("Take the hammer in hand: right-click it in the inventory ({0}) or press its hotbar number. Then {1} turns build mode on.",
                    Key("ToggleInventory"), Key("Block")),
                () => (_building != null && _building.IsBuildModeActive) || AnyWorkbench(false));
            Add("workbench", () => Loc.F("In build mode open the build menu ({0}), choose the Workbench and place it with {1}.",
                    Key("ToggleInventory"), Key("Attack")),
                () => AnyWorkbench(false));
            Add("roof", () => Loc.T("A workbench only works under a roof: put walls and a roof over it."),
                () => AnyWorkbench(true));
            Add("axe", () => Loc.F("Use the workbench ({0}) and craft a Stone Axe - it fells trees for logs.", Key("Interact")),
                () => Has("StoneAxe"));
            Add("food", () => Loc.T("Hunt a deer, cook its meat on a Campfire and eat it: right-click the meat in the inventory. Food raises your health."),
                () => (_food != null && _food.ActiveFoods.Count > 0) || Has("CookedMeat"));
            Add("tanning", () => Loc.T("Build a Tanning Rack by the workbench: it raises the workbench to level 2 and turns hides into leather for armour."),
                () => Has("Leather"));
            Add("leshy", () => Loc.T("Felling trees angers the forest, and its master, the Leshy, will come. Defeat him: a stone pickaxe needs his heartwood."),
                () => Has("LeshyHeartwood"));
            Add("copper", () => Loc.T("Craft a Stone Pickaxe and mine copper ore on the rocks of the dense forest."),
                () => Has("CopperOre"));
            Add("smelt", () => Loc.T("Build a Charcoal Kiln and a Bloomery: burn wood into charcoal, then smelt copper ore into bars."),
                () => Has("CopperBar"));
            Add("armour", () => Loc.T("Build a Forge and forge copper armour: a helm, a scale shirt and greaves."),
                () => Has("CopperHelm") && Has("CopperScaleShirt") && Has("CopperGreaves"));
        }

        private void Add(string id, Func<string> text, Func<bool> done)
        {
            _steps.Add(new Step { Id = id, Text = text, Done = done });
        }

        private static bool AnyWorkbench(bool sheltered)
        {
            foreach (Workbench bench in Workbench.AllWorkbenches)
            {
                if (bench != null && bench.Definition != null && bench.Definition.name == "BasicWorkbench" && (!sheltered || bench.IsSheltered))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>The key (or mouse button) bound to an action, as the Settings window shows it.</summary>
        private string Key(string action, string part = null)
        {
            InputAction found = _actions != null ? _actions.FindAction("Player/" + action) : null;
            if (found == null)
            {
                return "?";
            }

            foreach (InputBinding binding in found.bindings)
            {
                if (binding.isComposite || binding.groups == null || !binding.groups.Contains("Keyboard"))
                {
                    continue;
                }

                if (part == null ? binding.isPartOfComposite : !binding.isPartOfComposite || binding.name != part)
                {
                    continue;
                }

                string text = InputControlPath.ToHumanReadableString(binding.effectivePath, InputControlPath.HumanReadableStringOptions.OmitDevice);
                return text switch
                {
                    "Left Button" => Loc.T("LMB"),
                    "Right Button" => Loc.T("RMB"),
                    "Middle Button" => Loc.T("MMB"),
                    "Space" => Loc.T("Space"),
                    "Left Shift" => Loc.T("Left Shift"),
                    "Right Shift" => Loc.T("Right Shift"),
                    "Left Ctrl" => Loc.T("Left Ctrl"),
                    "Escape" => Loc.T("Escape"),
                    // Single letters stay as they are ("W" is also the compass West).
                    _ => text.Length > 1 ? Loc.T(text) : text,
                };
            }

            return "?";
        }

        // ---- Showing ----

        private void Update()
        {
            if (_card == null)
            {
                return;
            }

            bool enabledInSettings = GameSettings.Current.ShowHints;
            if (!enabledInSettings)
            {
                _card.RemoveFromClassList("hint-card--shown");
                return;
            }

            if (_doneUntil > 0f)
            {
                if (Time.time < _doneUntil)
                {
                    return;
                }

                _doneUntil = -1f;
                _card.RemoveFromClassList("hint-card--done");
                _current = -1;
            }

            if (Time.time < _nextCheck)
            {
                return;
            }

            _nextCheck = Time.time + CheckSeconds;

            // Metres actually walked: a jump of more than 10 m between checks is a teleport (the
            // spawn placement, a load, a respawn), not walking.
            Vector3 here = _player.transform.position;
            float moved = Vector3.Distance(here, _start);
            if (moved < 10f)
            {
                _walked += moved;
            }

            _start = here;

            if (_current >= 0)
            {
                Step step = _steps[_current];
                if (SafeDone(step))
                {
                    TutorialProgress.MarkDone(step.Id);
                    _text.text = Loc.T("Done!");
                    _card.AddToClassList("hint-card--done");
                    _doneUntil = Time.time + DoneSeconds;
                }

                return;
            }

            // The next step not done yet - ones already done (a loaded world, or done out of order)
            // are skipped without being shown.
            for (int i = 0; i < _steps.Count; i++)
            {
                Step step = _steps[i];
                if (TutorialProgress.IsDone(step.Id))
                {
                    continue;
                }

                if (SafeDone(step))
                {
                    TutorialProgress.MarkDone(step.Id);
                    continue;
                }

                _current = i;
                Refresh();
                _card.AddToClassList("hint-card--shown");
                return;
            }

            _card.RemoveFromClassList("hint-card--shown"); // all done
        }

        private void Refresh()
        {
            if (_current < 0 || _text == null)
            {
                return;
            }

            _text.text = _steps[_current].Text();
            _count.text = $"{_current + 1}/{_steps.Count}";
        }

        private static bool SafeDone(Step step)
        {
            try
            {
                return step.Done();
            }
            catch (Exception e)
            {
                Debug.LogError($"{nameof(HintsUI)}: the '{step.Id}' check failed ({e.Message}).");
                return false;
            }
        }
    }
}
