using System.Collections.Generic;
using UnityEditor;
using UnityEngine;
using NAV.Gameplay.Combat;

namespace NAV.Editor
{
    /// <summary>
    /// NAV > Items > Fit Blade Grips Below Guard. WeaponPackGenerator placed bladed weapons as if
    /// the pack's pivot sat at the guard (grip = halfway down the handle), but most pack swords
    /// pivot at the bottom of the handle - so the fist landed by the pommel with up to 20 cm of
    /// handle between it and the guard, and the sword looked pinched in the fingertips.
    /// Here each sword, greatsword and dagger held prefab is measured: the handle's width near the
    /// grip, then (going up) the first place the cross-section jumps - the guard. The model and
    /// its MeleeHitbox move along the handle so the hand socket (the root) sits FistHalfWidth
    /// below the guard: the fist rests right under the crossguard, as a sword is held. The greatsword's
    /// second hand goes further down the handle (HandGrip). Idempotent - re-run any time;
    /// WeaponPackGenerator runs it after generating.
    /// </summary>
    internal static class WeaponGripFitter
    {
        private const string HeldRoot = "Assets/NAV/Prefabs/Item/LowPoly/";
        private static readonly string[] BladeFolders = { "Sword", "Greatsword", "Dagger" };

        // Half the hero's fist across the grip (~9 cm) plus a little clearance from the guard.
        private const float FistHalfWidth = 0.05f;
        private const float Slice = 0.01f;

        [MenuItem("NAV/Items/Fit Blade Grips Below Guard")]
        private static void FitMenu()
        {
            int moved = FitAll();
            Debug.Log($"WeaponGripFitter: {moved} bladed held prefabs moved so the fist sits under the guard.");
        }

        internal static int FitAll()
        {
            int moved = 0;
            foreach (string folder in BladeFolders)
            {
                foreach (string guid in AssetDatabase.FindAssets("t:Prefab", new[] { HeldRoot + folder }))
                {
                    string path = AssetDatabase.GUIDToAssetPath(guid);
                    if (path.EndsWith("_held.prefab") && Fit(path))
                    {
                        moved++;
                    }
                }
            }

            AssetDatabase.SaveAssets();
            return moved;
        }

        private static bool Fit(string path)
        {
            GameObject root = PrefabUtility.LoadPrefabContents(path);
            try
            {
                Transform model = root.transform.Find("Model");
                if (model == null || !TryFindGuard(root.transform, out float guard))
                {
                    Debug.LogWarning($"WeaponGripFitter: no Model / no guard found in {path} - left as it was.");
                    return false;
                }

                float delta = guard - FistHalfWidth;
                if (Mathf.Abs(delta) < 0.002f)
                {
                    return false;
                }

                model.localPosition -= new Vector3(0f, delta, 0f);
                var hitbox = root.GetComponent<MeleeHitbox>();
                if (hitbox != null)
                {
                    var serialized = new SerializedObject(hitbox);
                    serialized.FindProperty("_start").vector3Value -= new Vector3(0f, delta, 0f);
                    serialized.FindProperty("_end").vector3Value -= new Vector3(0f, delta, 0f);
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }

                PrefabUtility.SaveAsPrefabAsset(root, path);
                return true;
            }
            finally
            {
                PrefabUtility.UnloadPrefabContents(root);
            }
        }

        /// <summary>Cross-section half-width per 1 cm slice along the root's Y (the handle axis),
        /// sampled along every triangle edge so long boxes count in every slice they cross. The
        /// handle width is taken around the lowest third of the handle; the guard is the first
        /// slice above the pommel region at least twice as wide as that (and over 4 cm).</summary>
        private static bool TryFindGuard(Transform root, out float guard)
        {
            guard = 0f;
            var width = new SortedDictionary<int, float>();
            foreach (MeshFilter filter in root.GetComponentsInChildren<MeshFilter>())
            {
                Mesh mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Vector3[] vertices = mesh.vertices;
                int[] triangles = mesh.triangles;
                for (int t = 0; t < triangles.Length; t += 3)
                {
                    for (int e = 0; e < 3; e++)
                    {
                        Vector3 a = root.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[t + e]]));
                        Vector3 b = root.InverseTransformPoint(filter.transform.TransformPoint(vertices[triangles[t + (e + 1) % 3]]));
                        int steps = Mathf.Clamp(Mathf.CeilToInt(Vector3.Distance(a, b) / (Slice * 0.5f)), 1, 400);
                        for (int s = 0; s <= steps; s++)
                        {
                            Vector3 point = Vector3.Lerp(a, b, (float)s / steps);
                            int slice = Mathf.FloorToInt(point.y / Slice);
                            float half = Mathf.Max(Mathf.Abs(point.x), Mathf.Abs(point.z));
                            width[slice] = width.TryGetValue(slice, out float known) ? Mathf.Max(known, half) : half;
                        }
                    }
                }
            }

            if (width.Count < 5)
            {
                return false;
            }

            var slices = new List<int>(width.Keys);
            int bottom = slices[0];
            // Handle width: the narrowest slice in the 3-8 cm above the bottom (past the pommel);
            // the guard is searched from there up, so a wide pommel below it never counts.
            float handle = float.MaxValue;
            int handleSlice = bottom;
            for (int i = bottom + 3; i <= bottom + 8; i++)
            {
                if (width.TryGetValue(i, out float w) && w < handle)
                {
                    handle = w;
                    handleSlice = i;
                }
            }

            if (handle == float.MaxValue)
            {
                return false;
            }

            for (int i = handleSlice + 1; i <= slices[slices.Count - 1]; i++)
            {
                if (width.TryGetValue(i, out float w) && w > handle * 2f && w > 0.04f)
                {
                    guard = i * Slice;
                    return true;
                }
            }

            return false;
        }
    }
}
