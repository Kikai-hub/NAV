using System.Collections.Generic;
using Photon.Pun;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Core.Save;
using NAV.Gameplay.Player;
using NAV.Network;
using NAV.Presentation.Animation;
using NAV.Presentation.Equipment;
using NAV.UI;
using NAV.World.TimeOfDay;
using NAV.World.Weather;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Network > Build Network Player (Photon PUN 2 multiplayer):
    ///   - Resources/NetworkPlayer.prefab - what other players see of a player: a copy of the
    ///     scene's hero (model, controller, hand grip, ragdoll, held item and armour views) without
    ///     the scripts that drive the local character (PlayerAnimator, FootGrounding,
    ///     PlayerRagdoll), on a root with a PhotonView observing NetworkAvatar, a body capsule
    ///     (PvP hits, bumping) and a name tag; its HeldItemView/ArmorView show a
    ///     NetworkEquipmentMirror instead of PlayerEquipment;
    ///   - WorldGenTest: a "Network" object with NetworkGameManager;
    ///   - MainMenu: MultiplayerMenuUIController next to MainMenuUIController (the Multiplayer
    ///     button and Create World's Online toggle).
    /// Re-run after changing the hero (its model, ragdoll or grip). Saves both scenes.
    /// </summary>
    internal static class NetworkSetup
    {
        private const string GameScene = "Assets/Scenes/WorldGenTest.unity";
        private const string MenuScene = "Assets/Scenes/MainMenu.unity";
        private const string PrefabFolder = "Assets/NAV/Resources";
        private const string PrefabPath = PrefabFolder + "/NetworkPlayer.prefab";
        private const string ItemDatabasePath = "Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset";

        [MenuItem("NAV/Network/Build Network Player")]
        private static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("NetworkSetup: exit Play Mode first.");
                return;
            }

            for (int i = 0; i < UnityEngine.SceneManagement.SceneManager.sceneCount; i++)
            {
                if (UnityEngine.SceneManagement.SceneManager.GetSceneAt(i).isDirty)
                {
                    Debug.LogError("NetworkSetup: save (or discard) the open scene's changes first - this setup opens and saves WorldGenTest and MainMenu.");
                    return;
                }
            }

            EditorSceneManager.OpenScene(GameScene);
            if (!BuildAvatarPrefab() || !WireGameScene())
            {
                return;
            }

            WireMenuScene();
            EditorSceneManager.OpenScene(GameScene);
            Debug.Log("NetworkSetup: NetworkPlayer prefab, the game scene's NetworkGameManager and the main menu's Multiplayer controller are set up.");
        }

        private static bool BuildAvatarPrefab()
        {
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            HeldItemView heroView = player != null ? player.GetComponentInChildren<HeldItemView>(true) : null;
            var database = AssetDatabase.LoadAssetAtPath<ItemDatabase>(ItemDatabasePath);
            if (heroView == null || database == null)
            {
                Debug.LogError("NetworkSetup: no player hero (HeldItemView under PlayerMotor) in WorldGenTest, or no item database.");
                return false;
            }

            TreeFellingSetup.EnsureFolder(PrefabFolder);
            var root = new GameObject("NetworkPlayer");
            try
            {
                GameObject hero = Object.Instantiate(heroView.gameObject, root.transform, false);
                hero.name = "Hero";
                hero.transform.localPosition = heroView.transform.localPosition;
                hero.transform.localRotation = heroView.transform.localRotation;

                // Only the local character's drivers go; views, grip and ragdoll stay.
                foreach (Behaviour driver in new Behaviour[] { hero.GetComponent<PlayerAnimator>(), hero.GetComponent<FootGrounding>(), hero.GetComponent<PlayerRagdoll>() })
                {
                    if (driver != null)
                    {
                        Object.DestroyImmediate(driver);
                    }
                }

                var animator = hero.GetComponent<Animator>();
                animator.applyRootMotion = false;
                animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

                NetworkEquipmentMirror mirror = hero.AddComponent<NetworkEquipmentMirror>();
                SetReference(hero.GetComponent<HeldItemView>(), "_equipment", mirror);
                SetReference(hero.GetComponent<ArmorView>(), "_equipment", mirror);

                var body = root.AddComponent<CapsuleCollider>();
                body.center = new Vector3(0f, 0.9f, 0f);
                body.height = 1.8f;
                body.radius = 0.32f;

                var ragdoll = hero.GetComponent<Ragdoll>();
                if (ragdoll != null)
                {
                    var serialized = new SerializedObject(ragdoll);
                    SerializedProperty off = serialized.FindProperty("_collidersOffWhileActive");
                    off.arraySize = 1;
                    off.GetArrayElementAtIndex(0).objectReferenceValue = body;
                    SerializedProperty posers = serialized.FindProperty("_disableWhileActive");
                    var grip = hero.GetComponent<HandGrip>();
                    posers.arraySize = grip != null ? 1 : 0;
                    if (grip != null)
                    {
                        posers.GetArrayElementAtIndex(0).objectReferenceValue = grip;
                    }

                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                var tag = new GameObject("NameTag");
                tag.transform.SetParent(root.transform, false);
                tag.transform.localPosition = new Vector3(0f, 2.15f, 0f);
                var text = tag.AddComponent<TextMesh>();
                text.text = "Player";
                text.fontSize = 48;
                text.characterSize = 0.035f;
                text.anchor = TextAnchor.MiddleCenter;
                text.alignment = TextAlignment.Center;
                text.color = new Color(1f, 0.93f, 0.78f);

                PhotonView view = root.AddComponent<PhotonView>();
                NetworkAvatar avatar = root.AddComponent<NetworkAvatar>();
                view.Synchronization = ViewSynchronization.UnreliableOnChange;
                view.OwnershipTransfer = OwnershipOption.Fixed;
                view.ObservedComponents = new List<Component> { avatar };

                var avatarSerialized = new SerializedObject(avatar);
                avatarSerialized.FindProperty("_model").objectReferenceValue = hero;
                avatarSerialized.FindProperty("_animator").objectReferenceValue = animator;
                avatarSerialized.FindProperty("_ragdoll").objectReferenceValue = ragdoll;
                avatarSerialized.FindProperty("_equipmentMirror").objectReferenceValue = mirror;
                avatarSerialized.FindProperty("_itemDatabase").objectReferenceValue = database;
                avatarSerialized.FindProperty("_body").objectReferenceValue = body;
                avatarSerialized.FindProperty("_nameTag").objectReferenceValue = text;
                avatarSerialized.ApplyModifiedPropertiesWithoutUndo();

                PrefabUtility.SaveAsPrefabAsset(root, PrefabPath);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }

            return true;
        }

        private static void SetReference(Object target, string property, Object value)
        {
            if (target == null)
            {
                return;
            }

            var serialized = new SerializedObject(target);
            serialized.FindProperty(property).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private static bool WireGameScene()
        {
            PlayerMotor player = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            NetworkGameManager manager = Object.FindAnyObjectByType<NetworkGameManager>(FindObjectsInactive.Include);
            if (manager == null)
            {
                manager = new GameObject("Network").AddComponent<NetworkGameManager>();
            }

            var serialized = new SerializedObject(manager);
            serialized.FindProperty("_playerMotor").objectReferenceValue = player;
            serialized.FindProperty("_clock").objectReferenceValue = Object.FindAnyObjectByType<WorldClock>(FindObjectsInactive.Include);
            serialized.FindProperty("_weather").objectReferenceValue = Object.FindAnyObjectByType<WeatherSystem>(FindObjectsInactive.Include);
            serialized.ApplyModifiedPropertiesWithoutUndo();

            // World changes (buildings, gathered resources, chat).
            NetworkWorldSync world = manager.GetComponent<NetworkWorldSync>() ?? manager.gameObject.AddComponent<NetworkWorldSync>();
            var worldSerialized = new SerializedObject(world);
            worldSerialized.FindProperty("_world").objectReferenceValue = Object.FindAnyObjectByType<NAV.World.Generation.WorldGenerator>(FindObjectsInactive.Include);
            worldSerialized.FindProperty("_building").objectReferenceValue = Object.FindAnyObjectByType<NAV.Gameplay.Building.PlayerBuilding>(FindObjectsInactive.Include);
            worldSerialized.FindProperty("_pieces").objectReferenceValue = AssetDatabase.LoadAssetAtPath<BuildingPieceDatabase>("Assets/NAV/ScriptableObjects/Core/BuildingPieceDatabase.asset");
            worldSerialized.FindProperty("_forestWrath").objectReferenceValue = Object.FindAnyObjectByType<NAV.World.Encounters.ForestWrath>(FindObjectsInactive.Include);
            worldSerialized.ApplyModifiedPropertiesWithoutUndo();

            var worldGenerator = Object.FindAnyObjectByType<NAV.World.Generation.WorldGenerator>(FindObjectsInactive.Include);
            var itemDatabase = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset");

            // Gathering: one shared count per tree/rock, trunks and logs shown on every machine.
            NetworkResourceSync resources = manager.GetComponent<NetworkResourceSync>() ?? manager.gameObject.AddComponent<NetworkResourceSync>();
            var resourceSerialized = new SerializedObject(resources);
            resourceSerialized.FindProperty("_world").objectReferenceValue = worldGenerator;
            SerializedProperty definitions = resourceSerialized.FindProperty("_definitions");
            string[] definitionGuids = AssetDatabase.FindAssets("t:ResourceNodeDefinition");
            definitions.arraySize = definitionGuids.Length;
            for (int i = 0; i < definitionGuids.Length; i++)
            {
                definitions.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<NAV.Gameplay.Items.ResourceNodeDefinition>(AssetDatabase.GUIDToAssetPath(definitionGuids[i]));
            }

            resourceSerialized.ApplyModifiedPropertiesWithoutUndo();

            // Items on the ground: shared, picked up once.
            NetworkItemSync items = manager.GetComponent<NetworkItemSync>() ?? manager.gameObject.AddComponent<NetworkItemSync>();
            SetReference(items, "_items", itemDatabase);

            // Chests, stations and doors.
            NetworkContainerSync containers = manager.GetComponent<NetworkContainerSync>() ?? manager.gameObject.AddComponent<NetworkContainerSync>();
            SetReference(containers, "_items", itemDatabase);

            // Creatures: every prefab under Prefabs/Creatures and Prefabs/Bosses can be copied from the network.
            NetworkCreatureSync creatures = manager.GetComponent<NetworkCreatureSync>() ?? manager.gameObject.AddComponent<NetworkCreatureSync>();
            var creatureSerialized = new SerializedObject(creatures);
            SerializedProperty prefabs = creatureSerialized.FindProperty("_creaturePrefabs");
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/NAV/Prefabs/Creatures", "Assets/NAV/Prefabs/Bosses" });
            prefabs.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                prefabs.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            creatureSerialized.FindProperty("_player").objectReferenceValue = player;
            creatureSerialized.ApplyModifiedPropertiesWithoutUndo();

            // The chat is the HUD's (UI ChatUI, solo and online - NAV > UI > Set Up Chat, Console and Map);
            // the network only carries its lines (NAV.Core.ChatChannel).

            EditorSceneManager.MarkSceneDirty(manager.gameObject.scene);
            return EditorSceneManager.SaveScene(manager.gameObject.scene);
        }

        private static void WireMenuScene()
        {
            EditorSceneManager.OpenScene(MenuScene);
            MainMenuUIController menu = Object.FindAnyObjectByType<MainMenuUIController>(FindObjectsInactive.Include);
            if (menu == null)
            {
                Debug.LogError("NetworkSetup: no MainMenuUIController in the MainMenu scene - the Multiplayer button stays hidden.");
                return;
            }

            MultiplayerMenuUIController multiplayer = menu.GetComponent<MultiplayerMenuUIController>() ?? menu.gameObject.AddComponent<MultiplayerMenuUIController>();
            SetReference(multiplayer, "_mainMenu", menu);
            SetReference(menu, "_multiplayer", multiplayer);
            EditorSceneManager.MarkSceneDirty(menu.gameObject.scene);
            EditorSceneManager.SaveScene(menu.gameObject.scene);
        }
    }
}
