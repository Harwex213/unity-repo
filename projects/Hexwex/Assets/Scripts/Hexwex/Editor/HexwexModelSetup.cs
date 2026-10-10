using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

namespace Hexwex.EditorTools
{
    /// <summary>
    /// Turns the models of <c>Assets/Art/Models</c> into the prefabs the game looks
    /// for: <c>Resources/Buildings/&lt;art name&gt;</c>, one per structure, 
    /// <c>Resources/Scenery/&lt;piece&gt;</c>, one per piece of biome scenery, and
    /// <c>Resources/Units/&lt;key&gt;</c>, one per unit and monster of the battle. The
    /// markers of the world map go to <c>Resources/Markers</c>. The
    /// FBX files are built in Blender by the scripts of <c>art-sources/Hexwex</c>;
    /// their objects are named <c>Bld_&lt;art name&gt;</c>, <c>Scn_&lt;piece&gt;</c> and <c>Unit_&lt;key&gt;</c>.
    ///
    /// The prefabs are generated, not hand-edited: run the menu item again after
    /// the FBX changes. A structure with no prefab falls back to the placeholder
    /// made of primitives.
    /// </summary>
    public static class HexwexModelSetup
    {
        /// <summary>
        /// A model file, the prefix of its objects, the folder its prefabs go to, and
        /// a turn in degrees. A model comes in facing +z. A building is turned round,
        /// so that its front faces the camera the island opens with.
        /// </summary>
        private static readonly string[][] Sets =
        {
            new[] { "Assets/Art/Models/Buildings.fbx", "Bld_", "Assets/Resources/Buildings", "180" },
            new[] { "Assets/Art/Models/Scenery.fbx", "Scn_", "Assets/Resources/Scenery", "0" },
            new[] { "Assets/Art/Models/Units.fbx", "Unit_", "Assets/Resources/Units", "0" },
            new[] { "Assets/Art/Models/Markers.fbx", "Mark_", "Assets/Resources/Markers", "0" },
        };

        [MenuItem("Hexwex/Rebuild Model Prefabs")]
        public static void RebuildModelPrefabs()
        {
            AssetDatabase.Refresh();

            foreach (string[] set in Sets)
            {
                Rebuild(set[0], set[1], set[2], float.Parse(set[3]));
            }

            AssetDatabase.SaveAssets();
        }

        private static void Rebuild(string modelPath, string objectPrefix, string prefabFolder, float turn)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("The model did not import", modelPath);
            }

            // Only meshes and their colours come from the file.
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportViaMaterialDescription;
            importer.SaveAndReimport();

            GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(modelPath);
            Directory.CreateDirectory(prefabFolder);
            List<string> made = new List<string>();

            foreach (Transform child in model.transform)
            {
                if (!child.name.StartsWith(objectPrefix))
                {
                    continue;
                }

                string artName = child.name.Substring(objectPrefix.Length);
                GameObject root = new GameObject(artName);
                GameObject instance = Object.Instantiate(child.gameObject, root.transform, false);
                instance.name = "Model";
                // Every model stands on its hex with its middle on the hex centre.
                instance.transform.localPosition = Vector3.zero;
                instance.transform.localRotation = Quaternion.Euler(0f, turn, 0f) * instance.transform.localRotation;

                PrefabUtility.SaveAsPrefabAsset(root, prefabFolder + "/" + artName + ".prefab");
                Object.DestroyImmediate(root);
                made.Add(artName);
            }

            Debug.Log("Hexwex: " + made.Count + " prefabs rebuilt in " + prefabFolder + ": " + string.Join(", ", made));
        }
    }
}
