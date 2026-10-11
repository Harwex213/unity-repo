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

        private const string PalettePath = "Assets/Art/Models/Materials.json";
        private const string MaterialFolder = "Assets/Art/Materials/Models";

        [System.Serializable]
        private sealed class PaletteEntry
        {
            public string name;
            public float r;
            public float g;
            public float b;
            public float roughness;
            public float emissionR;
            public float emissionG;
            public float emissionB;
            public float emissionStrength;
        }

        [System.Serializable]
        private sealed class Palette
        {
            public PaletteEntry[] materials;
        }

        /// <summary>
        /// Builds one URP Lit material per entry of the palette the Blender scripts
        /// write. An FBX carries a material's name and little else, so the look is
        /// rebuilt here: Blender keeps a base colour linear and Unity reads a
        /// material colour as gamma, roughness turns into smoothness, and emission
        /// stays linear, scaled by its strength.
        /// </summary>
        private static Dictionary<string, Material> RebuildMaterials()
        {
            Dictionary<string, Material> materials = new Dictionary<string, Material>();
            if (!File.Exists(PalettePath))
            {
                throw new FileNotFoundException("The material palette is missing: run the scripts of art-sources/Hexwex", PalettePath);
            }

            Palette palette = JsonUtility.FromJson<Palette>(File.ReadAllText(PalettePath));
            Directory.CreateDirectory(MaterialFolder);
            Shader lit = Shader.Find("Universal Render Pipeline/Lit");

            foreach (PaletteEntry entry in palette.materials)
            {
                string path = MaterialFolder + "/" + entry.name + ".mat";
                Material material = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (material == null)
                {
                    material = new Material(lit) { name = entry.name };
                    AssetDatabase.CreateAsset(material, path);
                }

                material.shader = lit;
                material.SetColor("_BaseColor", new Color(entry.r, entry.g, entry.b, 1f).gamma);
                material.SetFloat("_Smoothness", 1f - entry.roughness);
                material.SetFloat("_Metallic", 0f);

                if (entry.emissionStrength > 0f)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", new Color(entry.emissionR, entry.emissionG, entry.emissionB, 1f) * entry.emissionStrength);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
                }
                else
                {
                    material.DisableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", Color.black);
                    material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
                }

                EditorUtility.SetDirty(material);
                materials[entry.name] = material;
            }

            AssetDatabase.SaveAssets();

            return materials;
        }

        /// <summary>
        /// Checks that every triangle of a model faces the way its normals say. URP
        /// does not draw a back face, so a triangle wound inward is a hole. Returns
        /// how many triangles were checked and how many face the wrong way.
        /// </summary>
        public static string AuditWinding()
        {
            System.Text.StringBuilder report = new System.Text.StringBuilder();
            foreach (string[] set in Sets)
            {
                ModelImporter importer = (ModelImporter)AssetImporter.GetAtPath(set[0]);
                importer.isReadable = true;
                importer.SaveAndReimport();

                long triangles = 0;
                long against = 0;
                int examined = 0;
                int skipped = 0;
                float worst = 0f;
                GameObject model = AssetDatabase.LoadAssetAtPath<GameObject>(set[0]);
                foreach (MeshFilter filter in model.GetComponentsInChildren<MeshFilter>(true))
                {
                    Mesh mesh = filter.sharedMesh;
                    if (mesh == null || !mesh.isReadable || mesh.normals.Length != mesh.vertexCount)
                    {
                        skipped += 1;

                        continue;
                    }

                    examined += 1;
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    for (int sub = 0; sub < mesh.subMeshCount; sub += 1)
                    {
                        int[] indexes = mesh.GetTriangles(sub);
                        for (int index = 0; index + 2 < indexes.Length; index += 3)
                        {
                            Vector3 a = vertices[indexes[index]];
                            Vector3 geometric = Vector3.Cross(vertices[indexes[index + 1]] - a, vertices[indexes[index + 2]] - a);
                            Vector3 shaded = normals[indexes[index]] + normals[indexes[index + 1]] + normals[indexes[index + 2]];
                            if (geometric.sqrMagnitude < 1e-16f || shaded.sqrMagnitude < 1e-12f)
                            {
                                continue;
                            }

                            triangles += 1;
                            float dot = Vector3.Dot(geometric.normalized, shaded.normalized);
                            if (dot < 0f)
                            {
                                against += 1;
                                worst = Mathf.Min(worst, dot);
                            }
                        }
                    }
                }

                importer.isReadable = false;
                importer.SaveAndReimport();
                report.Append(Path.GetFileName(set[0]) + ": meshes " + examined + " (skipped " + skipped + "), triangles " + triangles + ", against " + against + ", worst dot " + worst.ToString("F2") + "; ");
            }

            Debug.Log("Hexwex winding audit: " + report);

            return report.ToString();
        }

        [MenuItem("Hexwex/Rebuild Model Prefabs")]
        public static void RebuildModelPrefabs()
        {
            AssetDatabase.Refresh();
            Dictionary<string, Material> materials = RebuildMaterials();

            foreach (string[] set in Sets)
            {
                Rebuild(set[0], set[1], set[2], float.Parse(set[3]), materials);
            }

            AssetDatabase.SaveAssets();
        }

        private static void Rebuild(string modelPath, string objectPrefix, string prefabFolder, float turn, Dictionary<string, Material> materials)
        {
            ModelImporter importer = AssetImporter.GetAtPath(modelPath) as ModelImporter;
            if (importer == null)
            {
                throw new FileNotFoundException("The model did not import", modelPath);
            }

            // Only meshes come from the file. Its materials are the ones built from the palette.
            importer.importCameras = false;
            importer.importLights = false;
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.useFileScale = true;
            importer.globalScale = 1f;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            foreach (KeyValuePair<string, Material> entry in materials)
            {
                importer.AddRemap(new AssetImporter.SourceAssetIdentifier(typeof(Material), entry.Key), entry.Value);
            }

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
