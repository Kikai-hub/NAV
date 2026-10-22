using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UIElements;
using NAV.UI;

namespace NAV.Editor
{
    /// <summary>
    /// Adds the Settings window to the open scene (MainMenu or the game scene): a "SettingsUI"
    /// object with a UIDocument (NAV_PanelSettings, SettingsPanel.uxml, drawn above the pause
    /// menu) and a SettingsUIController wired to the Input Actions asset, then hands it to the
    /// scene's MainMenuUIController / PauseUIController so their
    /// Settings buttons open it. Re-running re-wires instead of duplicating. Save the scene after.
    /// </summary>
    internal static class SettingsUISetup
    {
        private const string PanelSettingsPath = "Assets/NAV/Scripts/UI/NAV_PanelSettings.asset";
        private const string UxmlPath = "Assets/NAV/Scripts/UI/SettingsPanel.uxml";
        private const string ActionsPath = "Assets/InputSystem_Actions.inputactions";
        private const int SortingOrder = 30;

        [MenuItem("NAV/Setup/Add Settings UI To Scene")]
        private static void Setup()
        {
            SettingsUIController settings = Object.FindAnyObjectByType<SettingsUIController>(FindObjectsInactive.Include);
            if (settings == null)
            {
                var go = new GameObject("SettingsUI");
                Undo.RegisterCreatedObjectUndo(go, "Add Settings UI");
                go.AddComponent<UIDocument>();
                settings = go.AddComponent<SettingsUIController>();
            }

            var document = settings.GetComponent<UIDocument>();
            document.panelSettings = AssetDatabase.LoadAssetAtPath<PanelSettings>(PanelSettingsPath);
            document.visualTreeAsset = AssetDatabase.LoadAssetAtPath<VisualTreeAsset>(UxmlPath);
            document.sortingOrder = SortingOrder;

            var serialized = new SerializedObject(settings);
            serialized.FindProperty("_actions").objectReferenceValue = AssetDatabase.LoadAssetAtPath<InputActionAsset>(ActionsPath);
            serialized.ApplyModifiedProperties();

            string wiredTo = Wire<MainMenuUIController>(settings) ?? Wire<PauseUIController>(settings) ?? "nothing (no main menu or pause menu in this scene)";
            EditorSceneManager.MarkSceneDirty(settings.gameObject.scene);
            Debug.Log($"SettingsUISetup: '{settings.name}' set up and wired to {wiredTo} - save the scene.");
        }

        private static string Wire<T>(SettingsUIController settings) where T : MonoBehaviour
        {
            T owner = Object.FindAnyObjectByType<T>(FindObjectsInactive.Include);
            if (owner == null)
            {
                return null;
            }

            var serialized = new SerializedObject(owner);
            serialized.FindProperty("_settings").objectReferenceValue = settings;
            serialized.ApplyModifiedProperties();
            return $"'{owner.name}' ({typeof(T).Name})";
        }
    }
}
