using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.UIElements;
using NAV.Core.Save;
using NAV.Gameplay.Player;
using NAV.Presentation.Map;
using NAV.UI;
using NAV.World.Generation;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > UI > Set Up Chat, Console and Map - wires the three HUD additions of 2026-11-10 into
    /// WorldGenTest: a "HudExtras" object with ChatUI (solo and online), DevConsoleUI (the `
    /// key, Settings > Controls > Developer) and MapUI (minimap + M), a MapExploration (the
    /// explored map, saved by SaveManager). All on the HUD's panel settings. Removes the old
    /// online-only chat from the Network object. Re-runnable.
    /// </summary>
    internal static class HudExtrasSetup
    {
        private const string UiFolder = "Assets/NAV/Scripts/UI/";

        [MenuItem("NAV/UI/Set Up Chat, Console and Map")]
        internal static void Build()
        {
            if (EditorApplication.isPlaying)
            {
                Debug.LogError("HudExtrasSetup: exit Play Mode first.");
                return;
            }

            var hud = Object.FindAnyObjectByType<PlayerHudController>(FindObjectsInactive.Include);
            var player = Object.FindAnyObjectByType<PlayerMotor>(FindObjectsInactive.Include);
            var world = Object.FindAnyObjectByType<WorldGenerator>(FindObjectsInactive.Include);
            var save = Object.FindAnyObjectByType<SaveManager>(FindObjectsInactive.Include);
            if (hud == null || player == null || world == null)
            {
                Debug.LogError("HudExtrasSetup: open WorldGenTest (HUD, player, world generator).");
                return;
            }

            PanelSettings panel = hud.GetComponent<UIDocument>().panelSettings;
            var input = player.GetComponent<PlayerInputHandler>();
            var items = AssetDatabase.LoadAssetAtPath<ItemDatabase>("Assets/NAV/ScriptableObjects/Core/DefaultItemDatabase.asset");

            GameObject extras = GameObject.Find("HudExtras") ?? new GameObject("HudExtras");

            var exploration = Child<MapExploration>(extras, "MapExploration");
            Set(exploration, "_world", world);
            Set(exploration, "_player", player.transform);

            var chat = Child<ChatUI>(extras, "Chat");
            Set(chat, "_panelSettings", panel);
            Set(chat, "_style", AssetDatabase.LoadAssetAtPath<StyleSheet>(UiFolder + "ChatPanel.uss"));
            Set(chat, "_input", input);

            var console = Child<DevConsoleUI>(extras, "DevConsole");
            Set(console, "_panelSettings", panel);
            Set(console, "_style", AssetDatabase.LoadAssetAtPath<StyleSheet>(UiFolder + "DevConsolePanel.uss"));
            Set(console, "_input", input);
            Set(console, "_items", items);
            var creatures = new SerializedObject(console);
            SerializedProperty list = creatures.FindProperty("_creatures");
            string[] guids = AssetDatabase.FindAssets("t:Prefab", new[] { "Assets/NAV/Prefabs/Creatures", "Assets/NAV/Prefabs/Bosses" });
            list.arraySize = guids.Length;
            for (int i = 0; i < guids.Length; i++)
            {
                list.GetArrayElementAtIndex(i).objectReferenceValue = AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guids[i]));
            }

            creatures.ApplyModifiedPropertiesWithoutUndo();

            var map = Child<MapUI>(extras, "Map");
            Set(map, "_panelSettings", panel);
            Set(map, "_style", AssetDatabase.LoadAssetAtPath<StyleSheet>(UiFolder + "MapPanel.uss"));
            Set(map, "_exploration", exploration);
            Set(map, "_input", input);

            if (save != null)
            {
                Set(save, "_map", exploration);
            }

            // The old online-only chat (its script is gone) leaves a missing component behind.
            foreach (GameObject go in Object.FindObjectsByType<GameObject>(FindObjectsInactive.Include, FindObjectsSortMode.None).Where(g => g.name == "Network"))
            {
                GameObjectUtility.RemoveMonoBehavioursWithMissingScript(go);
            }

            EditorSceneManager.MarkSceneDirty(extras.scene);
            EditorSceneManager.SaveScene(extras.scene);
            Debug.Log("HudExtrasSetup: chat, developer console and map wired into the scene.");
        }

        private static T Child<T>(GameObject parent, string name) where T : Component
        {
            Transform child = parent.transform.Find(name);
            if (child == null)
            {
                child = new GameObject(name).transform;
                child.SetParent(parent.transform, false);
            }

            return child.GetComponent<T>() ?? child.gameObject.AddComponent<T>();
        }

        private static void Set(Object target, string field, Object value)
        {
            var serialized = new SerializedObject(target);
            serialized.FindProperty(field).objectReferenceValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }
    }
}
