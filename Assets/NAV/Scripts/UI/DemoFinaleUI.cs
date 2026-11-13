using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.UIElements;
using NAV.Gameplay.Crafting;
using NAV.Gameplay.Demo;
using NAV.Gameplay.Items;
using NAV.Gameplay.Player;

namespace NAV.UI
{
    /// <summary>
    /// The end-of-demo screen (STEAM_DEMO_PLAN.md stage 1): shown once, at the craft that
    /// completes the demo's finale set (DemoConfig.FinaleItems - the copper armour, which needs
    /// the Leshy's heartwood first). Thanks, wishlist and feedback links, and "keep playing" -
    /// the world stays open. Only in the demo; it adds itself to any scene with a player, so
    /// no scene needs wiring. Built in code like the developer console.
    /// </summary>
    public class DemoFinaleUI : MonoBehaviour
    {
        private PlayerCrafting _crafting;
        private PlayerInputHandler _input;
        private VisualElement _root;
        private bool _shown;
        private bool _open;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.AfterSceneLoad)]
        private static void Bootstrap()
        {
            SceneManager.sceneLoaded -= HandleSceneLoaded;
            SceneManager.sceneLoaded += HandleSceneLoaded;
            HandleSceneLoaded(SceneManager.GetActiveScene(), LoadSceneMode.Single);
        }

        private static void HandleSceneLoaded(Scene scene, LoadSceneMode mode)
        {
            if (!DemoMode.IsActive || DemoMode.Config.PanelSettings == null || FindAnyObjectByType<DemoFinaleUI>() != null)
            {
                return;
            }

            var crafting = FindAnyObjectByType<PlayerCrafting>();
            if (crafting == null)
            {
                return;
            }

            var host = new GameObject(nameof(DemoFinaleUI));
            SceneManager.MoveGameObjectToScene(host, crafting.gameObject.scene);
            host.AddComponent<DemoFinaleUI>().Init(crafting);
        }

        private void Init(PlayerCrafting crafting)
        {
            _crafting = crafting;
            _input = crafting.GetComponent<PlayerInputHandler>();
            if (_input == null)
            {
                _input = FindAnyObjectByType<PlayerInputHandler>();
            }
            _crafting.Crafted += HandleCrafted;
            BuildPanel();
        }

        private void OnDestroy()
        {
            if (_crafting != null)
            {
                _crafting.Crafted -= HandleCrafted;
            }

            if (_open)
            {
                Close();
            }
        }

        private void HandleCrafted(RecipeDefinition recipe, bool firstOfItsKind)
        {
            if (_shown || !firstOfItsKind || !IsFinaleItem(recipe.OutputItem) || !OwnsWholeFinaleSet(recipe.OutputItem))
            {
                return;
            }

            _shown = true;
            Open();
        }

        private static bool IsFinaleItem(ItemDefinition item)
        {
            foreach (ItemDefinition finale in DemoMode.Config.FinaleItems)
            {
                if (finale == item)
                {
                    return true;
                }
            }

            return false;
        }

        private bool OwnsWholeFinaleSet(ItemDefinition justCrafted)
        {
            foreach (ItemDefinition finale in DemoMode.Config.FinaleItems)
            {
                if (finale != null && finale != justCrafted && !_crafting.IsDiscovered(finale))
                {
                    return false;
                }
            }

            return true;
        }

        private void BuildPanel()
        {
            DemoConfig config = DemoMode.Config;
            var document = gameObject.AddComponent<UIDocument>();
            document.panelSettings = config.PanelSettings;
            document.sortingOrder = 80;
            VisualElement root = document.rootVisualElement;
            root.pickingMode = PickingMode.Ignore;
            if (config.FinaleStyle != null)
            {
                root.styleSheets.Add(config.FinaleStyle);
            }

            _root = new VisualElement();
            _root.AddToClassList("demo-root");
            _root.style.display = DisplayStyle.None;
            root.Add(_root);

            var panel = new VisualElement();
            panel.AddToClassList("demo-panel");
            _root.Add(panel);

            AddLabel(panel, NAV.Core.Localization.Loc.T("Thank you for playing!"), "demo-title");
            var divider = new VisualElement();
            divider.AddToClassList("demo-divider");
            divider.AddToClassList("nav-divider");
            panel.Add(divider);
            AddLabel(panel, NAV.Core.Localization.Loc.T("You have reached the end of the NAV demo: the Leshy is defeated and your first copper armour is forged."), "demo-text");
            AddLabel(panel, NAV.Core.Localization.Loc.T("In the full game the road goes on - to the sea islands, the swamp, the mountains and the Black Forest, with new bosses on the way."), "demo-text");

            if (!string.IsNullOrWhiteSpace(config.WishlistUrl))
            {
                AddButton(panel, NAV.Core.Localization.Loc.T("Add to Wishlist"), "demo-button--main", () => Application.OpenURL(config.WishlistUrl));
            }

            if (!string.IsNullOrWhiteSpace(config.FeedbackUrl))
            {
                AddButton(panel, NAV.Core.Localization.Loc.T("Share Feedback"), null, () => Application.OpenURL(config.FeedbackUrl));
            }

            AddButton(panel, NAV.Core.Localization.Loc.T("Keep Playing"), null, Close);
        }

        private static void AddLabel(VisualElement parent, string text, string cssClass)
        {
            var label = new Label(text);
            label.AddToClassList(cssClass);
            label.AddToClassList("nav-text");
            parent.Add(label);
        }

        private static void AddButton(VisualElement parent, string text, string extraClass, System.Action onClick)
        {
            var button = new Button(onClick) { text = text };
            button.AddToClassList("demo-button");
            button.AddToClassList("nav-button");
            if (extraClass != null)
            {
                button.AddToClassList(extraClass);
            }

            parent.Add(button);
        }

        private void Open()
        {
            _open = true;
            _root.style.display = DisplayStyle.Flex;
            if (_input != null)
            {
                _input.SetMenuOpen(true);
            }

            Time.timeScale = 0f;
        }

        private void Close()
        {
            if (!_open)
            {
                return;
            }

            _open = false;
            if (_root != null)
            {
                _root.style.display = DisplayStyle.None;
            }

            if (_input != null)
            {
                _input.SetMenuOpen(false);
            }

            Time.timeScale = 1f;
        }
    }
}
