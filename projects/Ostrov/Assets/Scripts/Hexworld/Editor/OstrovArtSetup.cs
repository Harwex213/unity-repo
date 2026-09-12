using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace Ostrov.HexworldEditor
{
    /// <summary>
    /// Настройка арта: импорт FBX, URP-материалы, префабы визуала.
    /// Запуск: меню Ostrov/Art или -executeMethod Ostrov.HexworldEditor.OstrovArtSetup.RunAll
    /// </summary>
    public static class OstrovArtSetup
    {
        const string MaterialsDir = "Assets/Art/Materials";
        const string ModelsDir = "Assets/Art/Models";
        const string TilesPrefabDir = "Assets/Prefabs/Tiles";
        const string BuildingsPrefabDir = "Assets/Prefabs/Buildings";

        const string HexTilesFbx = ModelsDir + "/HexTiles.fbx";
        const string BuildingsFbx = ModelsDir + "/Buildings.fbx";

        struct MatDef
        {
            public string Name;
            public string Hex;
            public float Metallic;
            public float Smoothness;

            public MatDef(string name, string hex, float metallic, float smoothness)
            {
                Name = name;
                Hex = hex;
                Metallic = metallic;
                Smoothness = smoothness;
            }
        }

        /// <summary>Материалы, которые лежат внутри FBX и привязываются к мешам.</summary>
        static readonly MatDef[] ModelMaterials =
        {
            new MatDef("M_Grass",     "#79C24B", 0f, 0.12f),
            new MatDef("M_Dirt",      "#8A6A47", 0f, 0.10f),
            new MatDef("M_Stone",     "#A9A6B5", 0f, 0.15f),
            new MatDef("M_Rock_Dark", "#6E6B7A", 0f, 0.15f),
            new MatDef("M_Wood",      "#8B5E3C", 0f, 0.12f),
            new MatDef("M_Foliage",   "#3E8E4F", 0f, 0.12f),
            new MatDef("M_Wall",      "#E8D6AE", 0f, 0.12f),
            new MatDef("M_Roof",      "#7B4A8C", 0f, 0.15f),
            new MatDef("M_Metal",     "#E0A62E", 1f, 0.60f),
            new MatDef("M_Cloth",     "#E3657A", 0f, 0.20f),
        };

        /// <summary>Материалы подсветки владения и выделения. В FBX их нет.</summary>
        static readonly MatDef[] ExtraMaterials =
        {
            new MatDef("M_OwnerPlayer", "#3D7DD8", 0f, 0.15f),
            new MatDef("M_OwnerAi",     "#C9453D", 0f, 0.15f),
            new MatDef("M_Highlight",   "#F2C53D", 0f, 0.20f),
        };

        static IEnumerable<MatDef> AllMaterials => ModelMaterials.Concat(ExtraMaterials);

        static readonly string[] TileObjects = { "Hex_Grass", "Hex_Forest", "Hex_Stone", "Hex_Rubble" };

        static readonly string[] BuildingObjects =
        {
            "Bld_Cottage", "Bld_Farm", "Bld_Quarry", "Bld_Church",
            "Bld_Barracks", "Bld_Castle", "Bld_Monument"
        };

        [MenuItem("Ostrov/Art/Run Full Art Setup")]
        public static void RunAll()
        {
            try
            {
                EnsureFolders();
                CreateMaterials();
                ConfigureModel(HexTilesFbx);
                ConfigureModel(BuildingsFbx);
                AssetDatabase.Refresh();
                ReportMaterialBindings(HexTilesFbx);
                ReportMaterialBindings(BuildingsFbx);
                BuildPrefabs();
                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
                Debug.Log("[OstrovArtSetup] DONE");
            }
            catch (Exception e)
            {
                Debug.LogError("[OstrovArtSetup] FAILED: " + e);
                throw;
            }
        }

        // ---------- папки ----------

        static void EnsureFolders()
        {
            EnsureFolder("Assets/Art");
            EnsureFolder(MaterialsDir);
            EnsureFolder("Assets/Prefabs");
            EnsureFolder(TilesPrefabDir);
            EnsureFolder(BuildingsPrefabDir);
        }

        static void EnsureFolder(string path)
        {
            if (AssetDatabase.IsValidFolder(path))
                return;
            var parent = Path.GetDirectoryName(path).Replace('\\', '/');
            var leaf = Path.GetFileName(path);
            EnsureFolder(parent);
            AssetDatabase.CreateFolder(parent, leaf);
        }

        // ---------- материалы ----------

        static Shader LitShader
        {
            get
            {
                var s = Shader.Find("Universal Render Pipeline/Lit");
                if (s == null)
                    throw new Exception("Шейдер 'Universal Render Pipeline/Lit' не найден.");
                return s;
            }
        }

        public static void CreateMaterials()
        {
            int count = 0;
            foreach (var def in AllMaterials)
            {
                count++;
                var path = $"{MaterialsDir}/{def.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(path);
                if (mat == null)
                {
                    mat = new Material(LitShader);
                    AssetDatabase.CreateAsset(mat, path);
                }
                else if (mat.shader != LitShader)
                {
                    mat.shader = LitShader;
                }

                if (!ColorUtility.TryParseHtmlString(def.Hex, out var color))
                    throw new Exception("Плохой цвет: " + def.Hex);

                mat.SetColor("_BaseColor", color);
                if (mat.HasProperty("_Color"))
                    mat.SetColor("_Color", color);
                mat.SetFloat("_Metallic", def.Metallic);
                mat.SetFloat("_Smoothness", def.Smoothness);
                mat.SetFloat("_WorkflowMode", 1f); // Metallic
                mat.SetFloat("_Surface", 0f);      // Opaque
                mat.SetFloat("_SpecularHighlights", 1f);
                EditorUtility.SetDirty(mat);
            }

            AssetDatabase.SaveAssets();
            Debug.Log($"[OstrovArtSetup] Материалов обработано: {count}");
        }

        // ---------- импорт моделей ----------

        public static void ConfigureModel(string fbxPath)
        {
            var importer = AssetImporter.GetAtPath(fbxPath) as ModelImporter;
            if (importer == null)
                throw new Exception("Не ModelImporter: " + fbxPath);

            importer.globalScale = 1f;
            importer.useFileScale = true;                    // Convert Units
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.None;
            importer.importBlendShapes = false;
            importer.importVisibility = false;
            importer.importCameras = false;
            importer.importLights = false;
            importer.preserveHierarchy = false;
            importer.addCollider = false;                    // коллайдеры не генерировать
            importer.isReadable = false;                     // Read/Write off
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.meshOptimizationFlags = MeshOptimizationFlags.Everything;
            importer.weldVertices = false;                   // не сваривать — нужно плоское затенение
            importer.importAnimation = false;
            importer.animationType = ModelImporterAnimationType.None;
            importer.importConstraints = false;
            importer.materialImportMode = ModelImporterMaterialImportMode.ImportStandard;
            // InPrefab + remap — это и есть результат Extract Materials:
            // материалы лежат отдельными ассетами в Assets/Art/Materials и привязаны к FBX.
            importer.materialLocation = ModelImporterMaterialLocation.InPrefab;
            importer.materialName = ModelImporterMaterialName.BasedOnMaterialName;
            importer.materialSearch = ModelImporterMaterialSearch.Everywhere;

            // Лишние привязки от прошлых запусков убираем.
            foreach (var pair in importer.GetExternalObjectMap().ToArray())
            {
                if (pair.Key.type == typeof(Material) && !ModelMaterials.Any(m => m.Name == pair.Key.name))
                    importer.RemoveRemap(pair.Key);
            }

            // Явная привязка каждого материала FBX к нашему ассету в Assets/Art/Materials.
            foreach (var def in ModelMaterials)
            {
                var matPath = $"{MaterialsDir}/{def.Name}.mat";
                var mat = AssetDatabase.LoadAssetAtPath<Material>(matPath);
                if (mat == null)
                    continue;
                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), def.Name);
                importer.AddRemap(id, mat);
            }

            importer.SaveAndReimport();
            Debug.Log("[OstrovArtSetup] Импорт настроен: " + fbxPath);
        }

        static void ReportMaterialBindings(string fbxPath)
        {
            var root = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (root == null)
                throw new Exception("FBX не загрузился: " + fbxPath);

            var missing = new List<string>();
            foreach (var r in root.GetComponentsInChildren<MeshRenderer>(true))
            {
                foreach (var m in r.sharedMaterials)
                {
                    if (m == null)
                    {
                        missing.Add(r.name + " -> null");
                        continue;
                    }
                    var p = AssetDatabase.GetAssetPath(m);
                    if (!p.StartsWith(MaterialsDir))
                        missing.Add($"{r.name} -> {m.name} @ {p}");
                }
            }

            if (missing.Count > 0)
                Debug.LogError($"[OstrovArtSetup] Материалы не привязаны в {fbxPath}:\n" + string.Join("\n", missing));
            else
                Debug.Log($"[OstrovArtSetup] Все материалы {fbxPath} указывают в {MaterialsDir}");
        }

        // ---------- префабы ----------

        public static void BuildPrefabs()
        {
            foreach (var objName in TileObjects)
            {
                var prefabName = objName.Replace("Hex_", "Tile_");
                MakePrefab(HexTilesFbx, objName, $"{TilesPrefabDir}/{prefabName}.prefab", true);
            }

            foreach (var objName in BuildingObjects)
                MakePrefab(BuildingsFbx, objName, $"{BuildingsPrefabDir}/{objName}.prefab", false);
        }

        static void MakePrefab(string fbxPath, string objectName, string prefabPath, bool withCollider)
        {
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(fbxPath);
            if (model == null)
                throw new Exception("FBX не загрузился: " + fbxPath);

            var instance = (GameObject)PrefabUtility.InstantiatePrefab(model);
            try
            {
                PrefabUtility.UnpackPrefabInstance(instance, PrefabUnpackMode.Completely,
                    InteractionMode.AutomatedAction);

                Transform target = null;
                foreach (var t in instance.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == objectName)
                    {
                        target = t;
                        break;
                    }
                }

                if (target == null)
                    throw new Exception($"Объект '{objectName}' не найден в {fbxPath}");

                if (target.localPosition.sqrMagnitude > 1e-6f ||
                    Quaternion.Angle(target.localRotation, Quaternion.identity) > 0.01f ||
                    (target.localScale - Vector3.one).sqrMagnitude > 1e-6f)
                {
                    Debug.LogWarning($"[OstrovArtSetup] {objectName}: трансформ не единичный " +
                                     $"(pos {target.localPosition}, rot {target.localEulerAngles}, scale {target.localScale})");
                }

                target.SetParent(null, false);
                target.localPosition = Vector3.zero;
                target.localRotation = Quaternion.identity;
                target.localScale = Vector3.one;

                var go = target.gameObject;
                go.name = Path.GetFileNameWithoutExtension(prefabPath);
                go.isStatic = false;

                if (withCollider)
                {
                    var mf = go.GetComponent<MeshFilter>();
                    if (mf == null || mf.sharedMesh == null)
                        throw new Exception("Нет меша для коллайдера: " + objectName);
                    var mc = go.GetComponent<MeshCollider>();
                    if (mc == null)
                        mc = go.AddComponent<MeshCollider>();
                    mc.sharedMesh = mf.sharedMesh;
                    mc.convex = false;
                }

                PrefabUtility.SaveAsPrefabAsset(go, prefabPath, out var ok);
                if (!ok)
                    throw new Exception("Не удалось сохранить префаб: " + prefabPath);

                var bounds = CalcBounds(go);
                Debug.Log($"[OstrovArtSetup] {prefabPath}: bounds center {bounds.center} size {bounds.size}");

                UnityEngine.Object.DestroyImmediate(go);
            }
            finally
            {
                if (instance != null)
                    UnityEngine.Object.DestroyImmediate(instance);
            }
        }

        static Bounds CalcBounds(GameObject go)
        {
            var renderers = go.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
                return new Bounds(go.transform.position, Vector3.zero);
            var b = renderers[0].bounds;
            foreach (var r in renderers.Skip(1))
                b.Encapsulate(r.bounds);
            return b;
        }
    }
}
