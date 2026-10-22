using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using NAV.Gameplay.Combat;
using NAV.Presentation.Combat;

namespace NAV.Editor
{
    /// <summary>
    /// One-click scene setup for HitFeedback: creates Materials/Combat/HitParticle.mat (a copy
    /// of the weather Rain.mat - URP Particles/Unlit, transparent, white base colour, so the
    /// particle colour comes from each particle) if it's missing, then adds a "HitFeedback"
    /// object to the open scene wired to its PlayerCombat. Safe to run again - an existing
    /// HitFeedback is re-wired instead of duplicated. Save the scene afterwards.
    /// </summary>
    internal static class HitFeedbackSetup
    {
        private const string MaterialFolder = "Assets/NAV/Materials/Combat";
        private const string MaterialPath = MaterialFolder + "/HitParticle.mat";
        private const string SourceMaterialPath = "Assets/NAV/Materials/Weather/Rain.mat";

        [MenuItem("NAV/Setup/Add Hit Feedback To Scene")]
        private static void Setup()
        {
            PlayerCombat playerCombat = Object.FindAnyObjectByType<PlayerCombat>(FindObjectsInactive.Include);
            if (playerCombat == null)
            {
                Debug.LogError("HitFeedbackSetup: no PlayerCombat in the open scene - open WorldGenTest first.");
                return;
            }

            Material material = GetOrCreateMaterial();
            if (material == null)
            {
                return;
            }

            HitFeedback feedback = Object.FindAnyObjectByType<HitFeedback>(FindObjectsInactive.Include);
            if (feedback == null)
            {
                var feedbackObject = new GameObject("HitFeedback");
                Undo.RegisterCreatedObjectUndo(feedbackObject, "Add Hit Feedback");
                feedback = feedbackObject.AddComponent<HitFeedback>();
            }

            var serialized = new SerializedObject(feedback);
            serialized.FindProperty("_playerCombat").objectReferenceValue = playerCombat;
            serialized.FindProperty("_particleMaterialTemplate").objectReferenceValue = material;
            serialized.ApplyModifiedProperties();

            EditorSceneManager.MarkSceneDirty(feedback.gameObject.scene);
            Selection.activeGameObject = feedback.gameObject;
            Debug.Log($"HitFeedbackSetup: '{feedback.name}' wired to '{playerCombat.name}' with {MaterialPath}. Save the scene.");
        }

        private static Material GetOrCreateMaterial()
        {
            var material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            if (material != null)
            {
                return material;
            }

            if (!AssetDatabase.IsValidFolder(MaterialFolder))
            {
                AssetDatabase.CreateFolder("Assets/NAV/Materials", "Combat");
            }

            if (!AssetDatabase.CopyAsset(SourceMaterialPath, MaterialPath))
            {
                Debug.LogError($"HitFeedbackSetup: couldn't copy '{SourceMaterialPath}' to '{MaterialPath}'.");
                return null;
            }

            material = AssetDatabase.LoadAssetAtPath<Material>(MaterialPath);
            material.SetColor("_BaseColor", Color.white);
            EditorUtility.SetDirty(material);
            AssetDatabase.SaveAssets();
            return material;
        }
    }
}
