using Hexwex.Core;
using UnityEngine;

namespace Hexwex.View
{
    /// <summary>
    /// The models of what stands on a hex: the scenery of each biome, the eight
    /// buildings and the stronghold. Each comes from a prefab of
    /// <c>Resources/Scenery</c> or <c>Resources/Buildings</c> when it is there, and
    /// is put together from primitives when it is not. The prototype draws a 2D
    /// sprite per building; a modelled prefab at
    /// <c>Resources/Buildings/&lt;art name&gt;</c> replaces the placeholder of the same
    /// name without a code change.
    /// </summary>
    public static class Props
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly MaterialPropertyBlock Block = new MaterialPropertyBlock();

        private static readonly System.Collections.Generic.Dictionary<string, GameObject> SceneryPrefabs = new System.Collections.Generic.Dictionary<string, GameObject>();

        private static Mesh _cube;
        private static Mesh _sphere;
        private static Mesh _cylinder;

        public static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString(rgb, out Color color) ? color : Color.magenta;
        }

        public static void Paint(Renderer renderer, Color color, int materialIndex = 0)
        {
            renderer.GetPropertyBlock(Block, materialIndex);
            Block.SetColor(BaseColorId, color);
            renderer.SetPropertyBlock(Block, materialIndex);
        }

        /// <summary>How tall the hex of a biome stands, in hex radii.</summary>
        public static float BiomeHeight(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Mountains: return 0.8f;
                case BiomeId.Volcano: return 0.7f;
                case BiomeId.Cliffs: return 0.65f;
                case BiomeId.Hills: return 0.5f;
                case BiomeId.Taiga: return 0.42f;
                case BiomeId.Rainforest: return 0.4f;
                case BiomeId.Forrest: return 0.4f;
                case BiomeId.Tundra: return 0.36f;
                case BiomeId.PolarDesert: return 0.36f;
                case BiomeId.Grassland: return 0.34f;
                case BiomeId.Savanna: return 0.32f;
                case BiomeId.Plains: return 0.3f;
                case BiomeId.Badlands: return 0.3f;
                case BiomeId.Desert: return 0.28f;
                case BiomeId.Crater: return 0.22f;
                default: return 0.2f;
            }
        }

        /// <summary>
        /// The scenery of an empty hex. It is seeded by the hex id, so a hex looks
        /// the same every time it is drawn.
        /// </summary>
        public static GameObject CreateScenery(HexTile hex, Material material, Transform parent)
        {
            GameObject root = new GameObject("Scenery");
            root.transform.SetParent(parent, false);
            Rng rng = Rng.FromText("scenery:" + hex.Id + ":" + hex.Biome);
            Transform t = root.transform;

            if (ModelScenery(hex, rng, t))
            {
                return root;
            }

            switch (hex.Biome)
            {
                case BiomeId.Forrest:
                    Scatter(rng, 5, 0.62f, p => Tree(t, material, p, 0.16f, 0.5f, Hex("#2c5e31")));
                    break;
                case BiomeId.Taiga:
                    Scatter(rng, 5, 0.62f, p => Tree(t, material, p, 0.12f, 0.62f, Hex("#234a3a")));
                    break;
                case BiomeId.Rainforest:
                    Scatter(rng, 7, 0.66f, p => Tree(t, material, p, 0.2f, 0.45f, Hex("#1f6146")));
                    break;
                case BiomeId.Savanna:
                    Scatter(rng, 2, 0.5f, p =>
                    {
                        Part(t, Cylinder, material, p + Vector3.up * 0.14f, new Vector3(0.05f, 0.14f, 0.05f), Hex("#6b5230"));
                        Part(t, Sphere, material, p + Vector3.up * 0.32f, new Vector3(0.42f, 0.12f, 0.42f), Hex("#8a8f3a"));
                    });
                    break;
                case BiomeId.Mountains:
                    Scatter(rng, 3, 0.4f, p =>
                    {
                        float height = 0.7f + (float)rng.Next() * 0.5f;
                        Part(t, HexMeshes.Cone, material, p, new Vector3(0.42f, height, 0.42f), Hex("#7b7d85"));
                        Part(t, HexMeshes.Cone, material, p + Vector3.up * height * 0.62f, new Vector3(0.17f, height * 0.4f, 0.17f), Hex("#f1f4f7"));
                    });
                    break;
                case BiomeId.Volcano:
                    Part(t, HexMeshes.Cone, material, Vector3.zero, new Vector3(0.72f, 0.75f, 0.72f), Hex("#4a2522"));
                    Part(t, Sphere, material, Vector3.up * 0.62f, new Vector3(0.3f, 0.2f, 0.3f), Hex("#ff6a2b"));
                    break;
                case BiomeId.Hills:
                    Scatter(rng, 3, 0.45f, p => Part(t, Sphere, material, p, new Vector3(0.6f, 0.3f, 0.6f), Hex("#7d8c45")));
                    break;
                case BiomeId.Cliffs:
                    Scatter(rng, 3, 0.5f, p => Rock(t, material, rng, p, 0.3f, 0.55f, Hex("#6c7582")));
                    break;
                case BiomeId.Crater:
                    for (int index = 0; index < 7; index += 1)
                    {
                        float angle = index / 7f * Mathf.PI * 2f;
                        Rock(t, material, rng, new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * 0.55f, 0.2f, 0.2f, Hex("#5d544d"));
                    }

                    break;
                case BiomeId.Badlands:
                    Scatter(rng, 4, 0.6f, p => Rock(t, material, rng, p, 0.18f, 0.22f, Hex("#7d4630")));
                    break;
                case BiomeId.Tundra:
                    Scatter(rng, 4, 0.6f, p => Rock(t, material, rng, p, 0.12f, 0.1f, Hex("#6f8279")));
                    break;
                case BiomeId.PolarDesert:
                    Scatter(rng, 3, 0.55f, p => Rock(t, material, rng, p, 0.2f, 0.3f, Hex("#eef6fa")));
                    break;
                case BiomeId.Desert:
                    Scatter(rng, 2, 0.4f, p => Part(t, Sphere, material, p, new Vector3(0.75f, 0.16f, 0.45f), Hex("#e6cd8b")));
                    break;
                case BiomeId.Swamp:
                    Scatter(rng, 3, 0.55f, p => Part(t, Cylinder, material, p + Vector3.up * 0.005f, new Vector3(0.36f, 0.005f, 0.3f), Hex("#39452a")));
                    Scatter(rng, 2, 0.6f, p => Tree(t, material, p, 0.1f, 0.3f, Hex("#4a5a2c")));
                    break;
                case BiomeId.Grassland:
                    Scatter(rng, 6, 0.7f, p => Part(t, HexMeshes.Cone, material, p, new Vector3(0.05f, 0.14f, 0.05f), Hex("#8cc968")));
                    break;
            }

            return root;
        }

        /// <summary>
        /// The scenery of an empty hex from the modelled pieces of
        /// <c>Resources/Scenery</c>: a few of them scattered over the hex, turned and
        /// sized at random, the same way every time for the same hex. Returns
        /// <c>false</c> when the pieces are not there, and the primitives stand in.
        /// </summary>
        private static bool ModelScenery(HexTile hex, Rng rng, Transform t)
        {
            if (SceneryPrefab("grass") == null)
            {
                return false;
            }

            switch (hex.Biome)
            {
                case BiomeId.Forrest:
                    Scatter(rng, 6, 0.64f, p => Piece(t, rng, rng.Next() < 0.5 ? "tree_a" : "tree_b", p, 0.85f, 1.2f));
                    break;
                case BiomeId.Taiga:
                    Scatter(rng, 6, 0.64f, p => Piece(t, rng, rng.Next() < 0.5 ? "pine_a" : "pine_b", p, 0.8f, 1.15f));
                    break;
                case BiomeId.Rainforest:
                    Scatter(rng, 5, 0.6f, p => Piece(t, rng, rng.Next() < 0.5 ? "jungle_a" : "jungle_b", p, 0.85f, 1.15f));
                    Scatter(rng, 3, 0.66f, p => Piece(t, rng, "fern", p, 0.8f, 1.2f));
                    break;
                case BiomeId.Savanna:
                    Scatter(rng, 2, 0.46f, p => Piece(t, rng, "acacia", p, 0.9f, 1.2f));
                    Scatter(rng, 3, 0.66f, p => Piece(t, rng, "bush_dry", p, 0.8f, 1.3f));
                    break;
                case BiomeId.Mountains:
                    Piece(t, rng, "peak_a", Vector3.zero, 1f, 1.15f);
                    Scatter(rng, 2, 0.52f, p => Piece(t, rng, "peak_b", p, 0.7f, 1f));
                    break;
                case BiomeId.Volcano:
                    Piece(t, rng, "volcano", Vector3.zero, 1f, 1.05f);
                    break;
                case BiomeId.Hills:
                    Scatter(rng, 1, 0.2f, p => Piece(t, rng, "hill_a", p, 0.95f, 1.15f));
                    Scatter(rng, 3, 0.56f, p => Piece(t, rng, "hill_b", p, 0.8f, 1.1f));
                    break;
                case BiomeId.Cliffs:
                    Scatter(rng, 3, 0.46f, p => Piece(t, rng, rng.Next() < 0.5 ? "cliff_a" : "cliff_b", p, 0.9f, 1.3f));
                    break;
                case BiomeId.Crater:
                    Piece(t, rng, "crater", Vector3.zero, 1f, 1f);
                    break;
                case BiomeId.Badlands:
                    Scatter(rng, 4, 0.58f, p => Piece(t, rng, rng.Next() < 0.5 ? "mesa_a" : "mesa_b", p, 0.85f, 1.25f));
                    break;
                case BiomeId.Tundra:
                    Scatter(rng, 3, 0.6f, p => Piece(t, rng, rng.Next() < 0.5 ? "stone_a" : "stone_b", p, 0.8f, 1.3f));
                    Scatter(rng, 3, 0.66f, p => Piece(t, rng, "bush_moss", p, 0.8f, 1.2f));
                    break;
                case BiomeId.PolarDesert:
                    Scatter(rng, 3, 0.54f, p => Piece(t, rng, rng.Next() < 0.5 ? "ice_a" : "ice_b", p, 0.8f, 1.3f));
                    break;
                case BiomeId.Desert:
                    Scatter(rng, 2, 0.4f, p => Piece(t, rng, rng.Next() < 0.5 ? "dune_a" : "dune_b", p, 0.9f, 1.15f));
                    Scatter(rng, 1, 0.6f, p => Piece(t, rng, "cactus", p, 0.9f, 1.3f));
                    break;
                case BiomeId.Swamp:
                    Scatter(rng, 2, 0.44f, p => Piece(t, rng, "pool", p, 0.8f, 1.1f));
                    Scatter(rng, 3, 0.62f, p => Piece(t, rng, "reeds", p, 0.9f, 1.3f));
                    Scatter(rng, 1, 0.5f, p => Piece(t, rng, "snag", p, 0.9f, 1.2f));
                    break;
                case BiomeId.Grassland:
                    Scatter(rng, 8, 0.72f, p => Piece(t, rng, "grass", p, 0.8f, 1.4f));
                    Scatter(rng, 3, 0.68f, p => Piece(t, rng, "flower", p, 0.9f, 1.3f));
                    break;
                case BiomeId.Plains:
                    Scatter(rng, 7, 0.72f, p => Piece(t, rng, "straw", p, 0.8f, 1.4f));
                    break;
            }

            return true;
        }

        private static void Piece(Transform parent, Rng rng, string name, Vector3 position, float minSize, float maxSize)
        {
            GameObject prefab = SceneryPrefab(name);
            // The draws are made even when a piece is missing, so the rest of the hex does not shift.
            float yaw = (float)rng.Next() * 360f;
            float size = Mathf.Lerp(minSize, maxSize, (float)rng.Next());
            if (prefab == null)
            {
                return;
            }

            Transform made = Object.Instantiate(prefab, parent, false).transform;
            made.name = name;
            made.localPosition = position;
            made.localRotation = Quaternion.Euler(0f, yaw, 0f);
            made.localScale = Vector3.one * size;
        }

        private static GameObject SceneryPrefab(string name)
        {
            if (!SceneryPrefabs.TryGetValue(name, out GameObject prefab))
            {
                prefab = Resources.Load<GameObject>("Scenery/" + name);
                SceneryPrefabs[name] = prefab;
            }

            return prefab;
        }

        /// <summary>What stands on a hex: a modelled prefab when one exists, or the placeholder.</summary>
        public static GameObject CreateStructure(StructureKind kind, Material material, Transform parent)
        {
            string artName = kind == StructureKind.Stronghold ? Stronghold.ArtName : Buildings.Get((BuildingId)(int)kind).ArtName;
            GameObject prefab = Resources.Load<GameObject>("Buildings/" + artName);
            if (prefab != null)
            {
                GameObject instance = Object.Instantiate(prefab, parent, false);
                instance.name = "Structure";

                return instance;
            }

            GameObject root = new GameObject("Structure");
            root.transform.SetParent(parent, false);
            Transform t = root.transform;
            Color wall = Hex("#d9cdb4");
            Color roof = Hex("#a5482f");
            Color wood = Hex("#7b5a36");
            Color stone = Hex("#8b8d93");

            switch (kind)
            {
                case StructureKind.Stronghold:
                    Box(t, material, new Vector3(0f, 0.18f, 0f), new Vector3(0.9f, 0.36f, 0.9f), stone);
                    Part(t, Cylinder, material, new Vector3(0f, 0.7f, 0f), new Vector3(0.44f, 0.36f, 0.44f), Hex("#a2a4ab"));
                    Part(t, HexMeshes.Cone, material, new Vector3(0f, 1.06f, 0f), new Vector3(0.3f, 0.42f, 0.3f), Hex("#3f66b0"));
                    for (int index = 0; index < 4; index += 1)
                    {
                        float x = index % 2 == 0 ? -0.4f : 0.4f;
                        float z = index < 2 ? -0.4f : 0.4f;
                        Box(t, material, new Vector3(x, 0.42f, z), new Vector3(0.18f, 0.2f, 0.18f), stone);
                    }

                    break;
                case StructureKind.Farm:
                    Box(t, material, new Vector3(0f, 0.02f, 0f), new Vector3(1.1f, 0.04f, 0.9f), Hex("#c9a94a"));
                    House(t, material, new Vector3(-0.25f, 0f, 0.15f), 0.36f, Hex("#b5452f"), wood);
                    Part(t, Cylinder, material, new Vector3(0.32f, 0.2f, -0.2f), new Vector3(0.18f, 0.2f, 0.18f), Hex("#d8d2c2"));
                    break;
                case StructureKind.Mine:
                    Part(t, HexMeshes.Cone, material, Vector3.zero, new Vector3(0.6f, 0.55f, 0.6f), Hex("#5a5c62"));
                    Box(t, material, new Vector3(0f, 0.16f, 0.42f), new Vector3(0.3f, 0.32f, 0.2f), Hex("#2a2420"));
                    Box(t, material, new Vector3(0f, 0.34f, 0.44f), new Vector3(0.4f, 0.06f, 0.24f), wood);
                    break;
                case StructureKind.Sawmill:
                    House(t, material, new Vector3(-0.15f, 0f, 0f), 0.44f, wood, Hex("#9a7648"));
                    for (int index = 0; index < 3; index += 1)
                    {
                        Transform log = Part(t, Cylinder, material, new Vector3(0.42f, 0.07f + index * 0.02f, -0.2f + index * 0.2f), new Vector3(0.1f, 0.3f, 0.1f), Hex("#a9824f"));
                        log.localRotation = Quaternion.Euler(0f, 0f, 90f);
                    }

                    break;
                case StructureKind.Village:
                    House(t, material, new Vector3(-0.32f, 0f, -0.2f), 0.3f, roof, wall);
                    House(t, material, new Vector3(0.3f, 0f, -0.1f), 0.34f, roof, wall);
                    House(t, material, new Vector3(-0.02f, 0f, 0.36f), 0.28f, roof, wall);
                    break;
                case StructureKind.MasonsGuild:
                    Box(t, material, new Vector3(0f, 0.22f, 0f), new Vector3(0.8f, 0.44f, 0.6f), stone);
                    Box(t, material, new Vector3(0f, 0.5f, 0f), new Vector3(0.9f, 0.12f, 0.7f), Hex("#6f7178"));
                    Part(t, Cylinder, material, new Vector3(-0.3f, 0.3f, 0.36f), new Vector3(0.1f, 0.3f, 0.1f), wall);
                    Part(t, Cylinder, material, new Vector3(0.3f, 0.3f, 0.36f), new Vector3(0.1f, 0.3f, 0.1f), wall);
                    break;
                case StructureKind.Observatory:
                    Part(t, Cylinder, material, new Vector3(0f, 0.3f, 0f), new Vector3(0.5f, 0.3f, 0.5f), wall);
                    Part(t, Sphere, material, new Vector3(0f, 0.6f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Hex("#5c7fb8"));
                    Transform scope = Part(t, Cylinder, material, new Vector3(0.14f, 0.82f, 0f), new Vector3(0.08f, 0.22f, 0.08f), Hex("#2b2f3a"));
                    scope.localRotation = Quaternion.Euler(0f, 0f, -40f);
                    break;
                case StructureKind.University:
                    Box(t, material, new Vector3(0f, 0.22f, 0f), new Vector3(1f, 0.44f, 0.5f), wall);
                    Box(t, material, new Vector3(0f, 0.5f, 0f), new Vector3(1.06f, 0.12f, 0.56f), Hex("#3f5fa0"));
                    Part(t, Cylinder, material, new Vector3(0f, 0.66f, 0f), new Vector3(0.2f, 0.26f, 0.2f), wall);
                    Part(t, HexMeshes.Cone, material, new Vector3(0f, 0.92f, 0f), new Vector3(0.16f, 0.28f, 0.16f), Hex("#3f5fa0"));
                    break;
                default:
                    Part(t, Cylinder, material, new Vector3(0f, 0.12f, 0f), new Vector3(0.9f, 0.12f, 0.9f), Hex("#4b5560"));
                    Part(t, Cylinder, material, new Vector3(0f, 0.7f, 0f), new Vector3(0.34f, 0.5f, 0.34f), Hex("#7f8a96"));
                    Part(t, Sphere, material, new Vector3(0f, 1.3f, 0f), new Vector3(0.5f, 0.5f, 0.5f), Hex("#58e0c2"));
                    break;
            }

            return root;
        }

        public static Mesh Cube
        {
            get { return _cube != null ? _cube : _cube = PrimitiveMesh(PrimitiveType.Cube); }
        }

        public static Mesh Sphere
        {
            get { return _sphere != null ? _sphere : _sphere = PrimitiveMesh(PrimitiveType.Sphere); }
        }

        public static Mesh Cylinder
        {
            get { return _cylinder != null ? _cylinder : _cylinder = PrimitiveMesh(PrimitiveType.Cylinder); }
        }

        private static Mesh PrimitiveMesh(PrimitiveType type)
        {
            GameObject primitive = GameObject.CreatePrimitive(type);
            Mesh mesh = primitive.GetComponent<MeshFilter>().sharedMesh;
            if (Application.isPlaying)
            {
                Object.Destroy(primitive);
            }
            else
            {
                Object.DestroyImmediate(primitive);
            }

            return mesh;
        }

        public static Transform Part(Transform parent, Mesh mesh, Material material, Vector3 position, Vector3 scale, Color color)
        {
            GameObject part = new GameObject(mesh.name);
            part.transform.SetParent(parent, false);
            part.transform.localPosition = position;
            part.transform.localScale = scale;
            part.AddComponent<MeshFilter>().sharedMesh = mesh;
            MeshRenderer renderer = part.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            Paint(renderer, color);

            return part.transform;
        }

        private static void Box(Transform parent, Material material, Vector3 center, Vector3 size, Color color)
        {
            Part(parent, Cube, material, center, size, color);
        }

        private static void House(Transform parent, Material material, Vector3 position, float size, Color roofColor, Color wallColor)
        {
            Box(parent, material, position + Vector3.up * size * 0.35f, new Vector3(size, size * 0.7f, size), wallColor);
            Transform roof = Part(parent, HexMeshes.Cone, material, position + Vector3.up * size * 0.7f, new Vector3(size * 0.8f, size * 0.6f, size * 0.8f), roofColor);
            roof.localRotation = Quaternion.Euler(0f, 22.5f, 0f);
        }

        private static void Tree(Transform parent, Material material, Vector3 position, float radius, float height, Color color)
        {
            Part(parent, Cylinder, material, position + Vector3.up * height * 0.15f, new Vector3(radius * 0.3f, height * 0.15f, radius * 0.3f), Hex("#5b4630"));
            Part(parent, HexMeshes.Cone, material, position + Vector3.up * height * 0.25f, new Vector3(radius, height * 0.75f, radius), color);
        }

        public static void Rock(Transform parent, Material material, Rng rng, Vector3 position, float width, float height, Color color)
        {
            float scale = 0.7f + (float)rng.Next() * 0.6f;
            Transform rock = Part(parent, Cube, material, position + Vector3.up * height * scale * 0.4f, new Vector3(width, height, width) * scale, color);
            rock.localRotation = Quaternion.Euler((float)rng.Next() * 20f - 10f, (float)rng.Next() * 360f, (float)rng.Next() * 20f - 10f);
        }

        private static void Scatter(Rng rng, int count, float radius, System.Action<Vector3> place)
        {
            for (int index = 0; index < count; index += 1)
            {
                float angle = (float)rng.Next() * Mathf.PI * 2f;
                float distance = Mathf.Sqrt((float)rng.Next()) * radius;
                place(new Vector3(Mathf.Cos(angle) * distance, 0f, Mathf.Sin(angle) * distance));
            }
        }
    }
}
