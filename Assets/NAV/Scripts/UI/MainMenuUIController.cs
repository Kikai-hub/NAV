using System;
using System.Globalization;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Core;
using NAV.Core.Save;

namespace NAV.UI
{
    /// <summary>
    /// The game's entry point screen. Play switches to the Create World view (world name + a
    /// seed field, editable or left at its script-generated random value, plus a Randomize
    /// reroll); Load shows a scrollable list of existing saves (from SaveSystem.ListSaves) to
    /// continue one, each behind a confirmation dialog before it actually loads/deletes; Quit
    /// exits. Create World and a confirmed Load entry both hand off to the same
    /// LoadingScreenUIController/SceneLoader flow - SaveSystem.PendingLoadSaveId (which save) and
    /// PendingNewGameName/PendingNewGameSeed (new game's chosen name/seed) are how that choice
    /// survives the scene load into WorldGenTest, where SaveManager reads them (see SaveManager's
    /// own doc comment for why static fields, not a persistent GameObject, carry this).
    /// </summary>
    [RequireComponent(typeof(UIDocument))]
    public class MainMenuUIController : MonoBehaviour
    {
        [SerializeField] private string _gameSceneName = "WorldGenTest";
        [SerializeField] private SceneLoader _sceneLoader;
        [SerializeField] private LoadingScreenUIController _loadingScreen;
        [Tooltip("Optional - the Settings window; without it the Settings button is hidden.")]
        [SerializeField] private SettingsUIController _settings;
        [Tooltip("Optional - the online parts (Multiplayer window, Create World's Online toggle); without it the Multiplayer button is hidden.")]
        [SerializeField] private MultiplayerMenuUIController _multiplayer;

        private UIDocument _document;
        private VisualElement _root;
        private VisualElement _mainMenuPanel;
        private VisualElement _createWorldPanel;
        private TextField _worldNameField;
        private TextField _worldSeedField;
        private VisualElement _loadGamePanel;
        private ScrollView _saveListView;
        private VisualElement _saveListEmptyLabel;
        private VisualElement _confirmDialogRoot;
        private Label _confirmDialogMessage;
        private Button _confirmConfirmButton;
        private Button _confirmCancelButton;

        private Action _pendingConfirmAction;

        private void Awake()
        {
            _document = GetComponent<UIDocument>();

            if (_sceneLoader == null || _loadingScreen == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' is missing a required reference (Scene Loader/Loading Screen).", this);
                enabled = false;
            }
        }

        private void Start()
        {
            VisualElement documentRoot = _document.rootVisualElement;
            if (documentRoot == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' has no rootVisualElement - check that its UIDocument has both Panel Settings and a Source Asset assigned.", this);
                enabled = false;
                return;
            }

            _root = documentRoot.Q<VisualElement>("main-menu-root");
            _mainMenuPanel = documentRoot.Q<VisualElement>("main-menu-panel");
            _createWorldPanel = documentRoot.Q<VisualElement>("create-world-panel");
            _worldNameField = documentRoot.Q<TextField>("world-name-field");
            _worldSeedField = documentRoot.Q<TextField>("world-seed-field");
            _loadGamePanel = documentRoot.Q<VisualElement>("load-game-panel");
            _saveListView = documentRoot.Q<ScrollView>("save-list");
            _saveListEmptyLabel = documentRoot.Q<VisualElement>("save-list-empty-label");
            Label versionLabel = documentRoot.Q<Label>("version-label");
            if (versionLabel != null)
            {
                versionLabel.text = $"v{Application.version}";
            }
            _confirmDialogRoot = documentRoot.Q<VisualElement>("confirm-dialog-root");
            _confirmDialogMessage = documentRoot.Q<Label>("confirm-dialog-message");
            _confirmConfirmButton = documentRoot.Q<Button>("confirm-dialog-confirm-button");
            _confirmCancelButton = documentRoot.Q<Button>("confirm-dialog-cancel-button");
            var playButton = documentRoot.Q<Button>("play-button");
            var createWorldButton = documentRoot.Q<Button>("create-world-button");
            var createWorldBackButton = documentRoot.Q<Button>("create-world-back-button");
            var randomizeSeedButton = documentRoot.Q<Button>("randomize-seed-button");
            var loadButton = documentRoot.Q<Button>("load-button");
            var loadBackButton = documentRoot.Q<Button>("load-back-button");
            var quitButton = documentRoot.Q<Button>("quit-button");

            if (_root == null || _mainMenuPanel == null || _createWorldPanel == null || _worldNameField == null
                || _worldSeedField == null || _loadGamePanel == null || _saveListView == null
                || _saveListEmptyLabel == null || _confirmDialogRoot == null || _confirmDialogMessage == null
                || _confirmConfirmButton == null || _confirmCancelButton == null || playButton == null
                || createWorldButton == null || createWorldBackButton == null || randomizeSeedButton == null
                || loadButton == null || loadBackButton == null || quitButton == null)
            {
                Debug.LogError($"{nameof(MainMenuUIController)} on '{name}' could not find one or more required elements in its UIDocument's source asset.", this);
                enabled = false;
                return;
            }

            playButton.clicked += ShowCreateWorldPanel;
            createWorldButton.clicked += HandleCreateWorldClicked;
            createWorldBackButton.clicked += ShowMainMenuPanel;
            randomizeSeedButton.clicked += HandleRandomizeSeedClicked;
            loadButton.clicked += HandleLoadClicked;
            loadBackButton.clicked += ShowMainMenuPanel;
            quitButton.clicked += HandleQuitClicked;

            var settingsButton = documentRoot.Q<Button>("settings-button");
            if (settingsButton != null && _settings != null)
            {
                settingsButton.clicked += () =>
                {
                    _mainMenuPanel.style.display = DisplayStyle.None;
                    _settings.Open(ShowMainMenuPanel);
                };
            }
            else if (settingsButton != null)
            {
                settingsButton.style.display = DisplayStyle.None;
            }
            _confirmConfirmButton.clicked += HandleConfirmClicked;
            _confirmCancelButton.clicked += HideConfirmDialog;

            var multiplayerButton = documentRoot.Q<Button>("multiplayer-button");
            if (multiplayerButton != null && _multiplayer == null)
            {
                multiplayerButton.style.display = DisplayStyle.None;
                var onlineToggle = documentRoot.Q<VisualElement>("online-toggle");
                if (onlineToggle != null)
                {
                    onlineToggle.style.display = DisplayStyle.None;
                }
            }

            ShowMainMenuPanel();
        }

        private void HandleLoadClicked()
        {
            ShowLoadGamePanel();
        }

        private void ShowMainMenuPanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.Flex;
            _createWorldPanel.style.display = DisplayStyle.None;
            _loadGamePanel.style.display = DisplayStyle.None;
        }

        /// <summary>Back to the title buttons (the Multiplayer window's Back).</summary>
        public void ShowMainMenu() => ShowMainMenuPanel();

        /// <summary>Hides this controller's panels (the Multiplayer window opens over them).</summary>
        public void HideAllPanels()
        {
            _mainMenuPanel.style.display = DisplayStyle.None;
            _createWorldPanel.style.display = DisplayStyle.None;
            _loadGamePanel.style.display = DisplayStyle.None;
        }

        /// <summary>Starts a new game from a name and a seed - the Create World button, and the
        /// online flow once a room is created or joined (its seed from the room).</summary>
        public void LaunchNewGame(string worldName, int seed)
        {
            SaveSystem.PendingLoadSaveId = null;
            SaveSystem.PendingNewGameName = worldName;
            SaveSystem.PendingNewGameSeed = seed;
            StartLoadingGameScene();
        }

        private void ShowCreateWorldPanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.None;
            _createWorldPanel.style.display = DisplayStyle.Flex;

            _worldNameField.value = "New World";
            _worldSeedField.value = new System.Random().Next().ToString(CultureInfo.InvariantCulture);
        }

        private void HandleRandomizeSeedClicked()
        {
            _worldSeedField.value = new System.Random().Next().ToString(CultureInfo.InvariantCulture);
        }

        private void HandleCreateWorldClicked()
        {
            string worldName = string.IsNullOrWhiteSpace(_worldNameField.value) ? "New World" : _worldNameField.value.Trim();
            int seed = ResolveSeed(_worldSeedField.value);

            if (_multiplayer != null && _multiplayer.OnlineChosen)
            {
                // Online: the room is opened first; the world loads once it exists.
                _multiplayer.HostWorld(worldName, seed);
                return;
            }

            LaunchNewGame(worldName, seed);
        }

        /// <summary>A blank/whitespace seed field means "keep it random" - rolls a fresh one. A
        /// numeric seed is used as-is. Any other text (e.g. a word instead of a number, same as
        /// Valheim's own seed field) is hashed into a stable int via StableHash so it still
        /// produces a deterministic, reproducible world.</summary>
        private static int ResolveSeed(string text)
        {
            if (string.IsNullOrWhiteSpace(text))
            {
                return new System.Random().Next();
            }

            string trimmed = text.Trim();
            return int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out int numericSeed)
                ? numericSeed
                : StableHash(trimmed);
        }

        /// <summary>A simple FNV-1a-style hash - deterministic across runs/machines, unlike
        /// string.GetHashCode() (randomized per process by .NET's hash-randomization since it's
        /// meant for hash tables, not stable identifiers) - the world seed must reproduce the
        /// same result every time the same text is typed.</summary>
        private static int StableHash(string text)
        {
            unchecked
            {
                int hash = 23;
                foreach (char c in text)
                {
                    hash = hash * 31 + c;
                }

                return hash;
            }
        }

        private void ShowLoadGamePanel()
        {
            _mainMenuPanel.style.display = DisplayStyle.None;
            _loadGamePanel.style.display = DisplayStyle.Flex;

            BuildSaveList();
        }

        private void BuildSaveList()
        {
            _saveListView.Clear();

            var saves = SaveSystem.ListSaves();
            _saveListEmptyLabel.style.display = saves.Count == 0 ? DisplayStyle.Flex : DisplayStyle.None;

            foreach (SaveGameData save in saves)
            {
                var item = new VisualElement();
                item.AddToClassList("save-list-item");

                var info = new VisualElement();
                info.AddToClassList("save-list-item__info");

                string displayName = string.IsNullOrWhiteSpace(save.WorldName) ? "Unnamed World" : save.WorldName;

                DateTime savedAt = DateTimeOffset.FromUnixTimeSeconds(save.SavedAtUnixSeconds).LocalDateTime;
                var nameLabel = new Label(displayName);
                nameLabel.AddToClassList("save-list-item__name");
                info.Add(nameLabel);

                var seedLabel = new Label($"Seed: {save.WorldSeed}");
                seedLabel.AddToClassList("save-list-item__seed");
                info.Add(seedLabel);

                bool fromNewerGame = SaveSystem.IsFromNewerGame(save);
                bool outdatedWorld = !fromNewerGame && SaveSystem.IsWorldGenerationOutdated(save);

                // Right side: the date, and a gold badge for saves that need attention.
                var meta = new VisualElement { pickingMode = PickingMode.Ignore };
                meta.AddToClassList("save-list-item__meta");
                var dateLabel = new Label(savedAt.ToString("dd.MM.yyyy   HH:mm", CultureInfo.InvariantCulture));
                dateLabel.AddToClassList("save-list-item__date");
                meta.Add(dateLabel);
                if (fromNewerGame || outdatedWorld)
                {
                    var badge = new Label(fromNewerGame ? "saved by a newer version" : "older world version");
                    badge.AddToClassList("save-list-item__badge");
                    meta.Add(badge);
                }

                info.Add(meta);

                string capturedId = save.SaveId;
                string capturedName = displayName;
                info.RegisterCallback<ClickEvent>(_ => HandleSaveEntryClicked(capturedId, capturedName, fromNewerGame, outdatedWorld));
                item.Add(info);

                var deleteButton = new Button(() => HandleDeleteClicked(capturedId, capturedName)) { text = string.Empty, tooltip = "Delete" };
                deleteButton.AddToClassList("save-list-item__delete-button");
                item.Add(deleteButton);

                _saveListView.Add(item);
            }
        }

        private void HandleSaveEntryClicked(string saveId, string worldName, bool fromNewerGame, bool outdatedWorld)
        {
            if (fromNewerGame)
            {
                // Can't be read safely by this build - the dialog only explains; confirming does nothing.
                ShowConfirm($"'{worldName}' was saved by a newer version of the game and can't be loaded.", () => { });
                return;
            }

            string message = outdatedWorld
                ? $"Load '{worldName}'? It was made with an older world version: the terrain may have changed, buildings may be out of place and gathered resources will grow back."
                : $"Load '{worldName}'?";
            ShowConfirm(message, () =>
            {
                SaveSystem.PendingLoadSaveId = saveId;
                StartLoadingGameScene();
            });
        }

        private void HandleDeleteClicked(string saveId, string worldName)
        {
            ShowConfirm($"Delete '{worldName}'? This cannot be undone.", () =>
            {
                SaveSystem.DeleteSave(saveId);
                BuildSaveList();
            });
        }

        private void ShowConfirm(string message, Action onConfirm)
        {
            _confirmDialogMessage.text = message;
            _pendingConfirmAction = onConfirm;
            _confirmDialogRoot.style.display = DisplayStyle.Flex;
        }

        private void HideConfirmDialog()
        {
            _pendingConfirmAction = null;
            _confirmDialogRoot.style.display = DisplayStyle.None;
        }

        private void HandleConfirmClicked()
        {
            Action action = _pendingConfirmAction;
            HideConfirmDialog();
            action?.Invoke();
        }

        private void StartLoadingGameScene()
        {
            _root.style.display = DisplayStyle.None;
            _loadingScreen.Show();
            _sceneLoader.LoadScene(_gameSceneName);
        }

        private static void HandleQuitClicked()
        {
#if UNITY_EDITOR
            UnityEditor.EditorApplication.isPlaying = false;
#else
            Application.Quit();
#endif
        }
    }
}
