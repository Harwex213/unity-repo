using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace Ostrov.FloatingIslandEditor
{
    /// <summary>
    /// The art part of the builder: the shared low-poly material, the sky,
    /// the FBX import settings, and the layout of the island and the backdrop.
    /// The models come from art-sources/Ostrov/FloatingIsland/fi_lowpoly.py.
    /// Every model is flat shaded. Every face maps its UVs to one cell of a
    /// small palette texture, so all models share one material.
    /// </summary>
    public static partial class FloatingIslandSceneBuilder
    {
        const string ArtDir = "Assets/Art/FloatingIsland";
        const string ModelDir = ArtDir + "/Models";
        const string ArtTextureDir = ArtDir + "/Textures";
        const string ArtMaterialDir = ArtDir + "/Materials";
        const string PalettePath = ArtTextureDir + "/T_FI_Palette.png";
        const string PaletteEmissionPath = ArtTextureDir + "/T_FI_PaletteEmission.png";
        const string SkyShaderName = "Ostrov/FloatingIsland/GradientSky";
        const string LowPolySlot = "M_FI_LowPoly";

        // The main island is about 42 m across. Its moss top sits near y = 0.
        // The basalt columns underneath reach down to about y = -30.
        const float IslandTopRadius = 16f;

        // The Blender models face Blender +X/+Y. A 180° yaw turns their lit
        // side (hill, crag, glowing windows) the way this layout expects.
        const float ModelYaw = 180f;

        sealed class Mats
        {
            public Material LowPoly;
            public Material Sky;
        }

        // ------------------------------------------------------------------
        // Materials and FBX import
        // ------------------------------------------------------------------

        static Mats CreateMaterials()
        {
            ConfigurePaletteTexture(PalettePath);
            ConfigurePaletteTexture(PaletteEmissionPath);
            var m = new Mats
            {
                LowPoly = CreateLowPolyMaterial(),
                Sky = CreateSkyMaterial(),
            };
            ConfigureModels(m.LowPoly);
            return m;
        }

        /// <summary>
        /// A palette needs crisp cells: point filter, no mipmaps, no compression.
        /// </summary>
        static void ConfigurePaletteTexture(string path)
        {
            var importer = (TextureImporter)AssetImporter.GetAtPath(path);
            if (importer == null)
                throw new Exception("Нет палитры " + path);
            bool dirty = importer.mipmapEnabled || importer.filterMode != FilterMode.Point ||
                         importer.textureCompression != TextureImporterCompression.Uncompressed ||
                         importer.wrapMode != TextureWrapMode.Clamp || importer.npotScale != TextureImporterNPOTScale.None;
            if (!dirty)
                return;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Point;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.SaveAndReimport();
        }

        static Material CreateLowPolyMaterial()
        {
            var mat = new Material(Shader.Find("Universal Render Pipeline/Lit")) { name = LowPolySlot };
            mat.SetTexture("_BaseMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PalettePath));
            mat.SetColor("_BaseColor", Color.white);
            mat.SetFloat("_Smoothness", 0.12f);
            mat.SetFloat("_Metallic", 0f);
            mat.SetFloat("_SpecularHighlights", 0f);
            mat.EnableKeyword("_SPECULARHIGHLIGHTS_OFF");
            mat.SetFloat("_EnvironmentReflections", 0f);
            mat.EnableKeyword("_ENVIRONMENTREFLECTIONS_OFF");
            // The windows glow through the emission palette. Everything else there is black.
            mat.SetTexture("_EmissionMap", AssetDatabase.LoadAssetAtPath<Texture2D>(PaletteEmissionPath));
            mat.SetColor("_EmissionColor", Color.white * WindowGlowIntensity);
            mat.EnableKeyword("_EMISSION");
            mat.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            mat.enableInstancing = true;
            return SaveOrReplace(mat, $"{ArtMaterialDir}/{LowPolySlot}.mat");
        }

        const float WindowGlowIntensity = 2.2f;

        static Material CreateSkyMaterial()
        {
            var shader = Shader.Find(SkyShaderName);
            if (shader == null)
                throw new Exception("Нет шейдера неба " + SkyShaderName);
            var mat = new Material(shader) { name = "M_FI_Sky" };
            mat.SetColor("_ZenithColor", SkyZenith);
            mat.SetColor("_HorizonColor", SkyHorizon);
            mat.SetColor("_MistColor", SkyMist);
            mat.SetColor("_AbyssColor", SkyAbyss);
            mat.SetFloat("_UpPower", 0.6f);
            mat.SetFloat("_MistEnd", 0.42f);
            mat.SetFloat("_AbyssStart", 0.95f);
            return SaveOrReplace(mat, $"{ArtMaterialDir}/M_FI_Sky.mat");
        }

        /// <summary>
        /// Sets the import options of every FBX in the model folder: authored
        /// flat normals, no imported materials, and the shared low-poly material
        /// in the single slot. The importer keeps the map in its .meta file.
        /// </summary>
        static void ConfigureModels(Material lowPoly)
        {
            foreach (var guid in AssetDatabase.FindAssets("t:Model", new[] { ModelDir }))
            {
                var path = AssetDatabase.GUIDToAssetPath(guid);
                if (AssetImporter.GetAtPath(path) is not ModelImporter importer)
                    continue;

                bool dirty = false;
                void Set<T>(T current, T wanted, Action<T> apply)
                {
                    if (EqualityComparer<T>.Default.Equals(current, wanted))
                        return;
                    apply(wanted);
                    dirty = true;
                }

                Set(importer.materialImportMode, ModelImporterMaterialImportMode.ImportViaMaterialDescription, v => importer.materialImportMode = v);
                Set(importer.materialLocation, ModelImporterMaterialLocation.InPrefab, v => importer.materialLocation = v);
                Set(importer.importNormals, ModelImporterNormals.Import, v => importer.importNormals = v);
                Set(importer.importTangents, ModelImporterTangents.None, v => importer.importTangents = v);
                Set(importer.importAnimation, false, v => importer.importAnimation = v);
                Set(importer.importCameras, false, v => importer.importCameras = v);
                Set(importer.importLights, false, v => importer.importLights = v);
                Set(importer.importBlendShapes, false, v => importer.importBlendShapes = v);
                Set(importer.animationType, ModelImporterAnimationType.None, v => importer.animationType = v);
                Set(importer.meshCompression, ModelImporterMeshCompression.Off, v => importer.meshCompression = v);

                var id = new AssetImporter.SourceAssetIdentifier(typeof(Material), LowPolySlot);
                var map = importer.GetExternalObjectMap();
                foreach (var stale in map.Keys.Where(k => k.type == typeof(Material) && k.name != LowPolySlot).ToList())
                {
                    importer.RemoveRemap(stale);
                    dirty = true;
                }

                if (!map.TryGetValue(id, out var current) || current != lowPoly)
                {
                    importer.AddRemap(id, lowPoly);
                    dirty = true;
                }

                if (dirty)
                    importer.SaveAndReimport();

                // Проверка: у модели не должно остаться встроенных материалов.
                var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
                foreach (var r in model.GetComponentsInChildren<Renderer>(true))
                foreach (var mat in r.sharedMaterials)
                {
                    if (mat != lowPoly)
                        Debug.LogWarning($"[FloatingIslandSceneBuilder] {path}/{r.name}: слот без общего материала ({(mat ? mat.name : "null")})");
                }
            }
        }

        // ------------------------------------------------------------------
        // Остров
        // ------------------------------------------------------------------

        static GameObject Model(string name)
        {
            var path = $"{ModelDir}/{name}.fbx";
            var model = AssetDatabase.LoadAssetAtPath<GameObject>(path);
            if (model == null)
                throw new Exception("Нет модели " + path);
            return model;
        }

        static GameObject Spawn(string model, Transform parent, Vector3 localPos, float yaw, float scale, string name = null)
        {
            var go = (GameObject)PrefabUtility.InstantiatePrefab(Model(model), parent);
            go.name = name ?? model;
            go.transform.localPosition = localPos;
            go.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            go.transform.localScale = Vector3.one * scale;
            return go;
        }

        /// <summary>
        /// Finds the top surface of an island mesh with ray casts. The builder
        /// uses it to put props on the ground and to find the cliff rim.
        /// A temporary mesh collider does the work. It never reaches the scene file.
        /// </summary>
        sealed class SurfaceProbe : IDisposable
        {
            readonly GameObject _go;
            readonly MeshCollider _collider;

            public SurfaceProbe(Mesh mesh)
            {
                _go = new GameObject("~SurfaceProbe") { hideFlags = HideFlags.HideAndDontSave };
                _collider = _go.AddComponent<MeshCollider>();
                _collider.sharedMesh = mesh;
                Physics.SyncTransforms();
            }

            public void SetPose(Vector3 position, float yaw, float scale)
            {
                _go.transform.SetPositionAndRotation(position, Quaternion.Euler(0f, yaw, 0f));
                _go.transform.localScale = Vector3.one * scale;
                Physics.SyncTransforms();
            }

            /// <summary>The highest surface point at (x, z) that faces up, if the top surface is there.</summary>
            public bool Top(float x, float z, out RaycastHit hit)
            {
                var ray = new Ray(new Vector3(x, 80f, z), Vector3.down);
                return _collider.Raycast(ray, out hit, 200f) && hit.point.y > -2.5f && hit.normal.y > 0.35f;
            }

            public float Height(float x, float z, float fallback = 0f)
            {
                return Top(x, z, out var hit) ? hit.point.y : fallback;
            }

            /// <summary>
            /// Walks from <paramref name="from"/> along <paramref name="dir"/> and
            /// returns the last point that is still on the top surface.
            /// </summary>
            public Vector3 Rim(Vector3 from, Vector3 dir, float maxDistance = 60f)
            {
                dir.y = 0f;
                dir.Normalize();
                Vector3 last = from;
                for (float t = 0f; t <= maxDistance; t += 0.2f)
                {
                    var p = from + (dir * t);
                    if (Top(p.x, p.z, out var hit))
                        last = hit.point;
                    else if (t > 2f)
                        break;
                }

                return last;
            }

            public void Dispose()
            {
                Object.DestroyImmediate(_go);
            }
        }

        /// <summary>Where the builder put the island parts. The scene uses it for lights and mist.</summary>
        sealed class IslandLayout
        {
            public Vector3 TowerPosition;
            public Vector3 SecondIslandPosition;
        }

        static Mesh ModelMesh(string name)
        {
            return Model(name).GetComponentInChildren<MeshFilter>().sharedMesh;
        }

        static IslandLayout BuildIsland(Transform visual, Mats m)
        {
            var layout = new IslandLayout();
            var rng = new System.Random(1337);

            // --- Главный остров: мох на связке базальтовых столбов ---
            Spawn("FI_IslandMain", visual, Vector3.zero, ModelYaw, 1f, "IslandMain");
            using var probe = new SurfaceProbe(ModelMesh("FI_IslandMain"));
            probe.SetPose(Vector3.zero, ModelYaw, 1f);

            // --- Замок: башня и обломок стены. Стена идёт от башни к -X. ---
            var towerXZ = new Vector2(5f, -6f);
            layout.TowerPosition = new Vector3(towerXZ.x, probe.Height(towerXZ.x, towerXZ.y), towerXZ.y);
            var castle = Spawn("FI_CastleRuin", visual, layout.TowerPosition, ModelYaw, 1f, "CastleRuin");
            AddWindowLight(castle.transform);

            // --- Мост на второй остров по +Z ---
            const float BridgeHalfSpan = 15f;
            const float Overlap = 3.2f;
            const float BridgeX = -5f;
            var mainRim = probe.Rim(new Vector3(BridgeX, 0f, 0f), Vector3.forward);
            float bridgeZ = mainRim.z + BridgeHalfSpan - Overlap;
            float farEndZ = bridgeZ + BridgeHalfSpan;

            using var smallProbe = new SurfaceProbe(ModelMesh("FI_IslandSmall"));
            const float SecondX = -3f;
            smallProbe.SetPose(new Vector3(SecondX, 0f, 0f), ModelYaw, 1f);
            var secondRim = smallProbe.Rim(new Vector3(BridgeX, 0f, 0f), Vector3.back);
            float secondZ = farEndZ - Overlap - secondRim.z;
            layout.SecondIslandPosition = new Vector3(SecondX, -1.2f, secondZ);
            smallProbe.SetPose(layout.SecondIslandPosition, ModelYaw, 1f);
            float landingY = smallProbe.Height(BridgeX, farEndZ - Overlap, mainRim.y);

            float deckY = Mathf.Max(mainRim.y, landingY) + 0.15f;
            Spawn("FI_ArchBridge", visual, new Vector3(BridgeX, deckY, bridgeZ), 90f, 1f, "ArchBridge");

            // --- Второй остров с обломком башни у моста ---
            var second = new GameObject("SecondIsland").transform;
            second.SetParent(visual, false);
            second.localPosition = layout.SecondIslandPosition;
            Spawn("FI_IslandSmall", second, Vector3.zero, ModelYaw, 1f, "IslandSmall");
            var stubXZ = new Vector2(BridgeX + 6f, farEndZ + 2f);
            SpawnOnTop("FI_TowerStub", second, smallProbe, layout.SecondIslandPosition, stubXZ, 200f, 1f, -0.2f);
            SpawnOnTop("FI_Pine", second, smallProbe, layout.SecondIslandPosition, new Vector2(SecondX - 6f, secondZ + 2f), 30f, 1.1f, -0.2f);
            SpawnOnTop("FI_Pine", second, smallProbe, layout.SecondIslandPosition, new Vector2(SecondX - 3.5f, secondZ + 5.5f), 140f, 0.8f, -0.2f);
            SpawnOnTop("FI_Rock_B", second, smallProbe, layout.SecondIslandPosition, new Vector2(SecondX + 4f, secondZ - 4f), 60f, 1.6f, -0.25f);

            // --- Ели: тёмная группа на склоне холма и одна у края ---
            var trees = new GameObject("Trees").transform;
            trees.SetParent(visual, false);
            foreach (var (xz, yaw, s) in new[]
                     {
                         (new Vector2(12f, 5f), 10f, 1.15f), (new Vector2(9.5f, 8.5f), 80f, 0.9f), (new Vector2(14f, 1.5f), 200f, 0.75f),
                         (new Vector2(-11f, -4f), 120f, 1f), (new Vector2(-9f, -8f), 260f, 0.7f),
                     })
                SpawnOnTop("FI_Pine", trees, probe, Vector3.zero, xz, yaw, s, -0.2f);

            // --- Камни и базальтовые пеньки ---
            var rocks = new GameObject("Rocks").transform;
            rocks.SetParent(visual, false);
            foreach (var (model, xz, yaw, s) in new[]
                     {
                         ("FI_BasaltStub_A", new Vector2(-1f, -11f), 20f, 1.1f), ("FI_BasaltStub_B", new Vector2(-14f, 3f), 140f, 1f),
                         ("FI_BasaltStub_B", new Vector2(9f, -12.5f), 250f, 0.8f),
                         ("FI_Rock_A", new Vector2(-4f, -3f), 30f, 1.9f), ("FI_Rock_B", new Vector2(1f, 8f), 200f, 1.6f),
                         ("FI_Rock_A", new Vector2(-12.5f, -7.5f), 300f, 1.3f), ("FI_Rock_B", new Vector2(10.5f, -2.5f), 100f, 1.4f),
                     })
                SpawnOnTop(model, rocks, probe, Vector3.zero, xz, yaw, s, -0.25f);

            // --- Парящие обломки вокруг ---
            var debris = new GameObject("Debris").transform;
            debris.SetParent(visual, false);
            for (int i = 0; i < 7; i++)
            {
                float a = (i * Mathf.PI * 2f / 7f) + Range(rng, -0.25f, 0.25f);
                float d = Range(rng, 28f, 38f);
                SpawnDebris(debris, rng, new Vector3(Mathf.Cos(a) * d, Range(rng, -16f, -3f), Mathf.Sin(a) * d),
                    i % 2 == 0 ? "FI_Debris_A" : "FI_Debris_B", Range(rng, 0.7f, 1.3f));
            }

            // Пара обломков под мостом.
            SpawnDebris(debris, rng, new Vector3(BridgeX + 7f, -13f, bridgeZ - 2f), "FI_Debris_B", 0.9f);
            SpawnDebris(debris, rng, new Vector3(BridgeX - 8f, -19f, bridgeZ + 5f), "FI_Debris_A", 1.1f);

            return layout;
        }

        static void SpawnDebris(Transform parent, System.Random rng, Vector3 pos, string model, float scale)
        {
            var holder = new GameObject("DebrisChunk").transform;
            holder.SetParent(parent, false);
            holder.localPosition = pos;
            var go = Spawn(model, holder, Vector3.zero, Range(rng, 0f, 360f), scale);
            go.transform.localRotation *= Quaternion.Euler(Range(rng, -12f, 12f), 0f, Range(rng, -12f, 12f));
            holder.gameObject.AddComponent<FloatBob>().Configure(Range(rng, 0.3f, 0.7f), Range(rng, 6f, 11f), 3f, Range(rng, 0f, 10f));
        }

        /// <summary>
        /// Puts a model on the probed ground at (x, z) in Visual space. The
        /// model goes under <paramref name="parent"/>, which sits at <paramref name="center"/>.
        /// </summary>
        static GameObject SpawnOnTop(string model, Transform parent, SurfaceProbe probe, Vector3 center, Vector2 xz, float yaw, float scale, float sink)
        {
            if (!probe.Top(xz.x, xz.y, out var hit))
                Debug.LogWarning($"[FloatingIslandSceneBuilder] {model}: нет земли в ({xz.x}, {xz.y})");
            var pos = new Vector3(xz.x, (hit.collider != null ? hit.point.y : 0f) + (sink * scale), xz.y);
            return Spawn(model, parent, pos - center, yaw, scale);
        }

        /// <summary>
        /// A warm point light in front of the glowing tower windows. The windows
        /// themselves glow through the emission palette and the bloom.
        /// </summary>
        static void AddWindowLight(Transform tower)
        {
            var lampGo = new GameObject("WindowLight");
            lampGo.transform.SetParent(tower, false);
            // In model space the lit windows face Blender -X and -Y, which is +X and +Z here.
            // The 180° yaw of the tower turns them towards the camera.
            lampGo.transform.localPosition = new Vector3(4.2f, 9f, 4.2f);
            var lamp = lampGo.AddComponent<Light>();
            lamp.type = LightType.Point;
            lamp.color = new Color(1f, 0.58f, 0.28f);
            lamp.intensity = 5f;
            lamp.range = 11f;
            lamp.shadows = LightShadows.None;
        }

        // ------------------------------------------------------------------
        // Фон: базальтовые шпили из бездны, дальние островки, обломки
        // ------------------------------------------------------------------

        static void BuildBackdrop(Transform root)
        {
            var rng = new System.Random(4242);

            // Камера смотрит сверху под 42°, поэтому фон виден в основном под островом
            // и за ним. Сетка с разбросом держит в кадре пару дальних силуэтов
            // при любом сдвиге острова.
            const int Cells = 6;
            float cell = BackdropHalfExtent * 2f / Cells;
            int index = 0;
            for (int gx = 0; gx < Cells; gx++)
            for (int gz = 0; gz < Cells; gz++)
            {
                var p = new Vector2(-BackdropHalfExtent + ((gx + Range(rng, 0.15f, 0.85f)) * cell),
                    -BackdropHalfExtent + ((gz + Range(rng, 0.15f, 0.85f)) * cell));
                if (p.magnitude < 70f)
                    continue;

                int kind = index++ % 3;
                var holder = new GameObject(kind == 0 ? "DistantIsland" : kind == 1 ? "BasaltSpire" : "FloatingRock").transform;
                holder.SetParent(root, false);
                switch (kind)
                {
                    case 0:
                        holder.position = new Vector3(p.x, Range(rng, -55f, -20f), p.y);
                        Spawn("FI_IslandSmall", holder, Vector3.zero, Range(rng, 0f, 360f), Range(rng, 0.6f, 1.1f));
                        holder.gameObject.AddComponent<FloatBob>().Configure(Range(rng, 0.5f, 1.2f), Range(rng, 12f, 20f), 0.8f, Range(rng, 0f, 20f));
                        break;
                    case 1:
                        // Шпиль поднимается из бездны и тонет в тумане.
                        holder.position = new Vector3(p.x, Range(rng, -150f, -110f), p.y);
                        Spawn(index % 2 == 0 ? "FI_Spire_A" : "FI_Spire_B", holder, Vector3.zero, Range(rng, 0f, 360f), Range(rng, 1f, 1.5f));
                        break;
                    default:
                        holder.position = new Vector3(p.x, Range(rng, -45f, -8f), p.y);
                        var go = Spawn(index % 2 == 0 ? "FI_Debris_A" : "FI_Debris_B", holder, Vector3.zero, Range(rng, 0f, 360f), Range(rng, 1.5f, 3f));
                        go.transform.localRotation *= Quaternion.Euler(Range(rng, -10f, 10f), 0f, Range(rng, -10f, 10f));
                        holder.gameObject.AddComponent<FloatBob>().Configure(Range(rng, 0.4f, 1.1f), Range(rng, 9f, 17f), 1.2f, Range(rng, 0f, 20f));
                        break;
                }
            }
        }
    }
}
