using System.Collections.Generic;
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
        private static Material _ground;
        /// <summary>The plain material of the scenery being built, for what a piece throws off: smoke, ash.</summary>
        private static Material _sceneryMaterial;
        private static bool _groundLooked;

        /// <summary>
        /// The material of the land itself: the tops and sides of the hexes and the
        /// rock under them. It takes a colour per renderer like the plain material,
        /// and draws turf, soil and rock out of it. <c>null</c> when its shader is
        /// missing; the caller then uses the plain material.
        /// </summary>
        public static Material Ground
        {
            get
            {
                if (!_groundLooked)
                {
                    _groundLooked = true;
                    Shader shader = Resources.Load<Shader>("Shaders/HexwexGround");
                    _ground = shader != null ? new Material(shader) { name = "M_Ground" } : null;
                }

                return _ground;
            }
        }

        public static Color Hex(string rgb)
        {
            return ColorUtility.TryParseHtmlString(rgb, out Color color) ? color : Color.magenta;
        }

        private static readonly int GroundId = Shader.PropertyToID("_Ground");

        /// <summary>
        /// The kind of ground the land shader draws on a biome's hexes: 0 turf, 1 sand,
        /// 2 snow, 3 bare rock, 4 mud, 5 forest floor, 6 sunbaked earth, 7 ash.
        /// </summary>
        public static float GroundKind(BiomeId biome)
        {
            switch (biome)
            {
                case BiomeId.Desert: return 1f;
                case BiomeId.PolarDesert: return 2f;
                case BiomeId.Mountains:
                case BiomeId.Cliffs:
                case BiomeId.Crater: return 3f;
                case BiomeId.Swamp: return 4f;
                case BiomeId.Taiga:
                case BiomeId.Forrest:
                case BiomeId.Rainforest: return 5f;
                case BiomeId.Badlands: return 6f;
                case BiomeId.Volcano: return 7f;
                default: return 0f;
            }
        }

        /// <summary>Paints the top of a hex: its colour and its kind of ground.</summary>
        public static void PaintGround(Renderer renderer, Color color, BiomeId biome, int materialIndex = 0)
        {
            renderer.GetPropertyBlock(Block, materialIndex);
            Block.SetColor(BaseColorId, color);
            Block.SetFloat(GroundId, GroundKind(biome));
            renderer.SetPropertyBlock(Block, materialIndex);
        }

        /// <summary>
        /// The taiga, in one of five kinds so that no two hexes of it look planted alike: a dark
        /// spruce forest, a bright pine forest on dry sand and lichen, a larch forest gone gold, a
        /// mixed forest round an old stone pine, and spruce under snow. Each is a close Civilization
        /// tile: trees of every age shoulder to shoulder over moss, berries and windfall.
        /// </summary>
        private static void FillTaiga(Transform t, Rng rng)
        {
            Layer Maybe(string name, double chance, float room, float min, float max) =>
                new Layer(name, rng.Next() < chance ? 1 : 0, room, min, max);

            switch (Mathf.Min(4, (int)(rng.Next() * 5)))
            {
                case 0:
                    // Dark spruce forest: blue-black spires close together, little light reaching the moss.
                    Fill(t, rng,
                        new Layer("moss", 5, 0.22f, 0.9f, 1.4f),
                        new Layer("spruce_a", 5, 0.19f, 0.85f, 1.15f),
                        new Layer("spruce_b", 5, 0.15f, 0.85f, 1.2f),
                        new Layer("fir_b", 3, 0.15f, 0.85f, 1.1f),
                        new Layer("fir_young", 4, 0.09f, 0.8f, 1.3f),
                        Maybe("fir_dead", 0.6, 0.1f, 0.8f, 1.1f),
                        Maybe("log", 0.7, 0.26f, 0.8f, 1f),
                        Maybe("anthill", 0.5, 0.12f, 0.9f, 1.2f),
                        new Layer("mushrooms", 3, 0.06f, 0.8f, 1.2f),
                        new Layer("bilberry", 4, 0.1f, 0.8f, 1.2f),
                        new Layer("grass_dark", 6, 0.05f, 0.7f, 1.2f));
                    break;
                case 1:
                    // Pine forest: tall orange trunks with their crowns high, light falling on lichen and dry grass, junipers below.
                    Fill(t, rng,
                        new Layer("turf_dry", 3, 0.22f, 0.9f, 1.3f),
                        new Layer("scots_a", 4, 0.19f, 0.85f, 1.1f),
                        new Layer("scots_b", 4, 0.16f, 0.85f, 1.15f),
                        new Layer("fir_young", 3, 0.09f, 0.8f, 1.3f),
                        new Layer("juniper", 3, 0.09f, 0.85f, 1.3f),
                        new Layer("juniper_b", 2, 0.09f, 0.85f, 1.3f),
                        Maybe("anthill", 0.7, 0.12f, 0.9f, 1.2f),
                        Maybe("stump", 0.6, 0.1f, 0.8f, 1.1f),
                        new Layer("lichen_a", 2, 0.12f, 0.8f, 1.1f),
                        new Layer("cowberry", 4, 0.1f, 0.8f, 1.2f),
                        new Layer("straw", 6, 0.05f, 0.7f, 1.1f),
                        new Layer("pebbles", 1, 0.12f, 0.8f, 1.1f));
                    break;
                case 2:
                    // Larch forest in autumn: gold over the dark of the few firs between them.
                    Fill(t, rng,
                        new Layer("moss", 4, 0.22f, 0.9f, 1.4f),
                        new Layer("larch_gold", 5, 0.17f, 0.85f, 1.15f),
                        new Layer("larch_gold_b", 4, 0.13f, 0.85f, 1.2f),
                        new Layer("larch", 2, 0.16f, 0.85f, 1.1f),
                        new Layer("fir_c", 3, 0.13f, 0.85f, 1.15f),
                        Maybe("log", 0.5, 0.26f, 0.8f, 1f),
                        new Layer("shrub_a", 2, 0.1f, 0.8f, 1.1f),
                        new Layer("mushrooms", 2, 0.06f, 0.8f, 1.2f),
                        new Layer("cowberry", 3, 0.1f, 0.8f, 1.2f),
                        new Layer("grass_tall_dry", 2, 0.1f, 0.7f, 1f),
                        new Layer("grass_dark", 6, 0.05f, 0.7f, 1.2f));
                    break;
                case 3:
                    // Mixed taiga round an old stone pine: firs, spruce, a pine and a larch of every age.
                    Fill(t, rng,
                        new Layer("moss", 4, 0.22f, 0.9f, 1.4f),
                        new Layer("cedar", 1, 0.26f, 0.9f, 1.1f),
                        new Layer("fir_a", 2, 0.2f, 0.85f, 1.1f),
                        new Layer("spruce_a", 2, 0.18f, 0.85f, 1.1f),
                        new Layer("scots_a", 2, 0.17f, 0.85f, 1.1f),
                        new Layer("larch", 2, 0.15f, 0.85f, 1.1f),
                        new Layer("fir_b", 3, 0.15f, 0.85f, 1.15f),
                        new Layer("fir_young", 4, 0.09f, 0.8f, 1.3f),
                        Maybe("fir_dead", 0.5, 0.1f, 0.8f, 1.1f),
                        Maybe("log", 0.6, 0.26f, 0.8f, 1f),
                        new Layer("juniper", 2, 0.09f, 0.85f, 1.2f),
                        new Layer("stump", 1, 0.1f, 0.8f, 1.1f),
                        new Layer("mushrooms", 2, 0.06f, 0.8f, 1.2f),
                        new Layer("bilberry", 3, 0.1f, 0.8f, 1.2f),
                        new Layer("grass_dark", 6, 0.05f, 0.7f, 1.2f));
                    break;
                default:
                    // Spruce under snow: white-capped tiers, drifts between the trunks, the dark of a fallen tree.
                    Fill(t, rng,
                        new Layer("snow_a", 3, 0.24f, 0.9f, 1.3f),
                        new Layer("snow_b", 2, 0.22f, 0.9f, 1.3f),
                        new Layer("spruce_snow", 5, 0.18f, 0.85f, 1.15f),
                        new Layer("spruce_snow_b", 6, 0.13f, 0.85f, 1.25f),
                        new Layer("fir_c", 2, 0.13f, 0.85f, 1.1f),
                        Maybe("fir_dead", 0.6, 0.1f, 0.8f, 1.1f),
                        Maybe("log", 0.5, 0.26f, 0.8f, 1f),
                        new Layer("snow_rock_a", 1, 0.1f, 0.7f, 1f),
                        new Layer("juniper_b", 2, 0.09f, 0.85f, 1.2f),
                        new Layer("grass_dark", 4, 0.05f, 0.7f, 1.1f));
                    break;
            }
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
            _sceneryMaterial = material;
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
                    // A leafy wood: broad crowns of every age, one turning gold, young trees, ferns and berries under them.
                    Fill(t, rng,
                        new Layer("moss", 3, 0.22f, 0.9f, 1.4f),
                        new Layer("oak_tall", 3, 0.26f, 0.85f, 1.1f),
                        new Layer("beech", 4, 0.22f, 0.85f, 1.15f),
                        new Layer(rng.Next() < 0.5 ? "tree_a" : "tree_b", 3, 0.2f, 0.8f, 1.1f),
                        new Layer("tree_autumn", rng.Next() < 0.6 ? 1 : 0, 0.22f, 0.85f, 1.05f),
                        new Layer("birch_b", 2, 0.16f, 0.8f, 1.05f),
                        new Layer("sapling", 4, 0.1f, 0.8f, 1.3f),
                        new Layer("log", rng.Next() < 0.5 ? 1 : 0, 0.24f, 0.8f, 1f),
                        new Layer("fern_big", 5, 0.1f, 0.8f, 1.2f),
                        new Layer("bush_berry", 2, 0.12f, 0.7f, 1f),
                        new Layer("mushrooms", 2, 0.06f, 0.8f, 1.1f),
                        new Layer("grass_dark", 8, 0.05f, 0.7f, 1.2f),
                        new Layer("flower_white", 2, 0.05f, 0.8f, 1.1f));
                    break;
                case BiomeId.Taiga:
                    FillTaiga(t, rng);
                    break;
                case BiomeId.Rainforest:
                    // The jungle in layers: giants of the canopy over palms and banana, tangled undergrowth of ferns and blooms below.
                    Fill(t, rng,
                        new Layer("moss", 3, 0.22f, 0.9f, 1.4f),
                        new Layer(rng.Next() < 0.5 ? "kapok_a" : "kapok_b", 2, 0.34f, 0.85f, 1.1f),
                        new Layer(rng.Next() < 0.5 ? "jungle_a" : "jungle_b", 4, 0.22f, 0.85f, 1.15f),
                        new Layer("palm", 2, 0.16f, 0.8f, 1.1f),
                        new Layer("banana", 4, 0.14f, 0.85f, 1.2f),
                        new Layer("fern_jungle", 7, 0.1f, 0.85f, 1.3f),
                        new Layer("bloom", 4, 0.1f, 0.8f, 1.2f),
                        new Layer("fern", 4, 0.08f, 0.8f, 1.2f),
                        new Layer("mushrooms", 1, 0.06f, 0.8f, 1.1f),
                        new Layer("grass_lush", 8, 0.06f, 0.7f, 1.1f));
                    break;
                case BiomeId.Savanna:
                    // Burnt gold grass to the knee, one great umbrella tree and a smaller one, a termite mound, a pile of warm rocks.
                    Fill(t, rng,
                        new Layer("turf_dry", 4, 0.24f, 0.9f, 1.4f),
                        new Layer("acacia_big", 1, 0.5f, 0.85f, 1.05f),
                        new Layer("acacia", rng.Next() < 0.7 ? 1 : 0, 0.4f, 0.75f, 0.95f),
                        new Layer("termite", rng.Next() < 0.7 ? 1 : 0, 0.18f, 0.8f, 1.2f),
                        new Layer("kopje", rng.Next() < 0.5 ? 1 : 0, 0.28f, 0.8f, 1.1f),
                        new Layer("bush_dry", 3, 0.14f, 0.8f, 1.2f),
                        new Layer("grass_tall", 12, 0.09f, 0.75f, 1.25f),
                        new Layer("grass_tall_dry", 8, 0.09f, 0.7f, 1.1f),
                        new Layer("straw", 6, 0.05f, 0.8f, 1.3f),
                        new Layer("pebbles_red", 1, 0.12f, 0.8f, 1.1f));
                    break;
                case BiomeId.Mountains:
                    // A whole range on the hex, its foot in scree and a few dark firs that climbed as high as they could.
                    PieceTurned(t, rng.Next() < 0.5 ? "massif_a" : "massif_b", Vector3.zero, 0.95f + (float)rng.Next() * 0.15f, (float)rng.Next() * 360f);
                    // The range itself stands in the middle: the rest keeps to its foot.
                    FillAround(t, rng, 0.5f,
                        new Layer("fir_c", 4, 0.12f, 0.7f, 1f),
                        new Layer("fir_young", 4, 0.08f, 0.7f, 1.1f),
                        new Layer("stone_a", 2, 0.16f, 0.8f, 1.1f),
                        new Layer("pebbles", 3, 0.1f, 0.9f, 1.4f),
                        new Layer("grass_dark", 6, 0.05f, 0.6f, 1f));
                    break;
                case BiomeId.Volcano:
                    // The cone fills the hex; at its foot lava pools, thrown rocks, ash and the burnt spikes of trees.
                    PieceTurned(t, "volcano_big", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    FillAround(t, rng, 0.76f,
                        new Layer("lava_pool", 1, 0.2f, 0.8f, 1f),
                        new Layer("basalt_a", 2, 0.12f, 0.8f, 1.1f),
                        new Layer("basalt_b", 2, 0.12f, 0.8f, 1.1f),
                        new Layer("charred_a", 2, 0.08f, 0.8f, 1.2f),
                        new Layer("charred_b", 2, 0.08f, 0.8f, 1.2f),
                        new Layer("ash", 3, 0.14f, 0.8f, 1.2f));
                    break;
                case BiomeId.Hills:
                    // Rolling green rises across the whole hex, rock breaking through, a few trees and bushes on the slopes.
                    PieceTurned(t, rng.Next() < 0.5 ? "hills_a" : "hills_b", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    Fill(t, rng,
                        new Layer("outcrop_a", 1, 0.26f, 0.8f, 1.1f),
                        new Layer("outcrop_b", rng.Next() < 0.6 ? 1 : 0, 0.24f, 0.8f, 1.1f),
                        new Layer("oak", rng.Next() < 0.6 ? 1 : 0, 0.34f, 0.65f, 0.85f),
                        new Layer("birch_b", rng.Next() < 0.5 ? 1 : 0, 0.22f, 0.8f, 1f),
                        new Layer("bush_berry", 2, 0.14f, 0.7f, 1f),
                        new Layer("stone_b", 1, 0.14f, 0.7f, 1f),
                        new Layer("grass_lush_b", 8, 0.08f, 0.7f, 1.1f),
                        new Layer("grass_b", 12, 0.05f, 0.8f, 1.3f),
                        new Layer("flower", 3, 0.05f, 0.9f, 1.2f),
                        new Layer("pebbles", 2, 0.1f, 0.8f, 1.2f));
                    break;
                case BiomeId.Cliffs:
                    // An escarpment: a shelf of bedded rock fills the back of the hex and breaks off in a sheer face,
                    // turf and firs on its top, fallen blocks at its foot; the open ground in front has scree, stacks and hardy firs.
                    // A block of rock stands in the middle, sheer on every side, a meadow with small firs on its top;
                    // round it, out to the edge of the hex, stones, small firs and grass.
                    PieceTurned(t, rng.Next() < 0.5 ? "cliff_island_a" : "cliff_island_b", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    FillAround(t, rng, 0.58f,
                        new Layer("stone_a", 3, 0.14f, 0.6f, 0.9f),
                        new Layer("stone_b", 2, 0.14f, 0.6f, 0.9f),
                        new Layer("fir_young", 5, 0.08f, 0.6f, 0.95f),
                        new Layer("fir_c", 2, 0.1f, 0.5f, 0.7f),
                        new Layer("shrub_a", 2, 0.08f, 0.7f, 1f),
                        new Layer("pebbles", 5, 0.08f, 0.8f, 1.2f),
                        new Layer("moss", 3, 0.14f, 0.7f, 1f),
                        new Layer("grass_dark", 12, 0.04f, 0.6f, 1f),
                        new Layer("grass", 6, 0.04f, 0.6f, 1f));
                    RimStones(t, rng);
                    break;
                case BiomeId.Crater:
                    // The bowl fills the hex; it is turned by a sixth at random so no two craters break the same way.
                    PieceTurned(t, "crater_rim", Vector3.zero, 1f, rng.RandomInt(0, 5) * 60f);
                    for (int index = 0; index < 2; index += 1)
                    {
                        float angle = (float)rng.Next() * Mathf.PI * 2f;
                        float reach = 0.24f + (float)rng.Next() * 0.12f;
                        Piece(t, rng, "vent", new Vector3(Mathf.Cos(angle), 0f, Mathf.Sin(angle)) * reach, 0.8f, 1.2f);
                    }

                    break;
                case BiomeId.Badlands:
                    // Banded red ridges across the hex, a mesa, hoodoos with their cap stones, dead brush and bones.
                    PieceTurned(t, rng.Next() < 0.5 ? "strata_a" : "strata_b", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    Fill(t, rng,
                        new Layer(rng.Next() < 0.5 ? "mesa_a" : "mesa_b", 1, 0.3f, 0.85f, 1.15f),
                        new Layer("hoodoo_a", 2, 0.12f, 0.8f, 1.2f),
                        new Layer("hoodoo_b", 2, 0.12f, 0.8f, 1.2f),
                        new Layer("bones", rng.Next() < 0.4 ? 1 : 0, 0.14f, 0.8f, 1f),
                        new Layer("bush_dead", 4, 0.1f, 0.8f, 1.3f),
                        new Layer("bush_dry", 2, 0.1f, 0.6f, 0.9f),
                        new Layer("pebbles_red", 4, 0.1f, 0.8f, 1.3f),
                        new Layer("straw", 5, 0.05f, 0.6f, 1f));
                    break;
                case BiomeId.Tundra:
                    // Low cold ground: lichen on split stones, drifts of old snow, cotton grass, mats of dwarf birch, a stunted fir or two.
                    Fill(t, rng,
                        new Layer("moss", 4, 0.22f, 0.9f, 1.4f),
                        new Layer(rng.Next() < 0.5 ? "snow_a" : "snow_b", 3, 0.24f, 0.9f, 1.3f),
                        new Layer("lichen_a", 2, 0.2f, 0.8f, 1.2f),
                        new Layer("lichen_b", 2, 0.2f, 0.8f, 1.2f),
                        new Layer("stone_a", 1, 0.2f, 0.8f, 1.1f),
                        new Layer("fir_young", rng.Next() < 0.6 ? 2 : 0, 0.1f, 0.6f, 0.9f),
                        new Layer("shrub_a", 4, 0.12f, 0.8f, 1.3f),
                        new Layer("shrub_b", 3, 0.12f, 0.8f, 1.3f),
                        new Layer("cotton_grass", 10, 0.06f, 0.8f, 1.3f),
                        new Layer("grass_dark", 6, 0.05f, 0.6f, 1f),
                        new Layer("pebbles", 2, 0.1f, 0.8f, 1.2f));
                    break;
                case BiomeId.PolarDesert:
                    // A field of wind-packed snow with a pressure ridge, seracs of blue ice, a frozen pool, rocks showing dark through the white.
                    PieceTurned(t, rng.Next() < 0.5 ? "snowfield_a" : "snowfield_b", Vector3.zero, 1f, rng.RandomInt(0, 5) * 60f);
                    Fill(t, rng,
                        new Layer("frozen_pool", rng.Next() < 0.5 ? 1 : 0, 0.34f, 0.85f, 1.05f),
                        new Layer("serac_a", 2, 0.16f, 0.8f, 1.2f),
                        new Layer("serac_b", 2, 0.16f, 0.8f, 1.2f),
                        new Layer(rng.Next() < 0.5 ? "ice_a" : "ice_b", 2, 0.16f, 0.7f, 1.1f),
                        new Layer("snow_rock_a", 2, 0.14f, 0.8f, 1.2f),
                        new Layer("snow_rock_b", 2, 0.14f, 0.8f, 1.2f),
                        new Layer(rng.Next() < 0.5 ? "snow_a" : "snow_b", 4, 0.18f, 0.9f, 1.3f),
                        new Layer("pebbles", 2, 0.1f, 0.7f, 1f));
                    break;
                case BiomeId.Desert:
                    // Dunes across the hex, a carved sandstone stack, cacti, bones; now and then an oasis under palms.
                    PieceTurned(t, rng.Next() < 0.5 ? "dunes_a" : "dunes_b", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    bool hasOasis = rng.Next() < 0.3;
                    Fill(t, rng,
                        new Layer("oasis", hasOasis ? 1 : 0, 0.4f, 0.9f, 1.05f),
                        new Layer("palm", hasOasis ? 2 : 0, 0.12f, 0.85f, 1.1f),
                        new Layer(rng.Next() < 0.5 ? "sandstone_a" : "sandstone_b", rng.Next() < 0.7 ? 1 : 0, 0.24f, 0.8f, 1.2f),
                        new Layer("cactus", 2, 0.14f, 0.8f, 1.2f),
                        new Layer("cactus_barrel", 3, 0.1f, 0.8f, 1.2f),
                        new Layer("bones", rng.Next() < 0.5 ? 1 : 0, 0.14f, 0.8f, 1f),
                        new Layer("bush_dry", 3, 0.1f, 0.6f, 0.9f),
                        new Layer("pebbles_red", 2, 0.1f, 0.7f, 1f));
                    break;
                case BiomeId.Swamp:
                    // Dark water in pools that run together, cypresses on their roots, cattails and reeds thick at the edges, a dead snag, a rotten log.
                    PieceTurned(t, rng.Next() < 0.5 ? "bog_a" : "bog_b", Vector3.zero, 1f, (float)rng.Next() * 360f);
                    Fill(t, rng,
                        new Layer(rng.Next() < 0.5 ? "cypress_a" : "cypress_b", 2, 0.26f, 0.85f, 1.1f),
                        new Layer("snag", 1, 0.14f, 0.9f, 1.2f),
                        new Layer("log", rng.Next() < 0.5 ? 1 : 0, 0.24f, 0.8f, 1f),
                        new Layer("cattails", 5, 0.12f, 0.8f, 1.2f),
                        new Layer("reeds", 6, 0.08f, 0.9f, 1.3f),
                        new Layer("mushrooms", 2, 0.06f, 0.8f, 1.1f),
                        new Layer("grass_dark", 10, 0.05f, 0.7f, 1.1f),
                        new Layer("moss", 3, 0.16f, 0.8f, 1.2f));
                    break;
                case BiomeId.Grassland:
                    // Rich grass to the waist, drifts of wildflowers, a pair of birches, a berry bush, now and then a pond.
                    Fill(t, rng,
                        new Layer("turf", 4, 0.24f, 0.9f, 1.4f),
                        new Layer("pond", rng.Next() < 0.35 ? 1 : 0, 0.34f, 0.85f, 1.05f),
                        new Layer(rng.Next() < 0.5 ? "birch_a" : "birch_b", rng.Next() < 0.7 ? 2 : 1, 0.26f, 0.85f, 1.15f),
                        new Layer("bush_berry", 2, 0.16f, 0.8f, 1.15f),
                        new Layer("flowers_warm", 3, 0.14f, 0.85f, 1.3f),
                        new Layer("flowers_cool", 2, 0.14f, 0.85f, 1.3f),
                        new Layer("grass_lush", 12, 0.09f, 0.75f, 1.25f),
                        new Layer("grass_lush_b", 8, 0.09f, 0.75f, 1.2f),
                        new Layer("grass", 12, 0.05f, 0.8f, 1.4f),
                        new Layer("flower", 2, 0.05f, 0.9f, 1.2f),
                        new Layer("flower_white", 2, 0.05f, 0.9f, 1.2f),
                        new Layer("pebbles", 1, 0.12f, 0.7f, 1f));
                    break;
                case BiomeId.Plains:
                    // Open dry steppe: patches of ripe grain, turf, a lone tree, hay left out, grass everywhere between.
                    Fill(t, rng,
                        new Layer("turf", 2, 0.24f, 0.9f, 1.4f),
                        new Layer("turf_dry", 3, 0.24f, 0.9f, 1.4f),
                        new Layer(rng.Next() < 0.5 ? "wheat_a" : "wheat_b", 2, 0.36f, 0.85f, 1.1f),
                        new Layer("oak", rng.Next() < 0.5 ? 1 : 0, 0.4f, 0.75f, 0.95f),
                        new Layer("hay", rng.Next() < 0.4 ? 1 : 0, 0.2f, 0.9f, 1.1f),
                        new Layer("bush_dry", 2, 0.14f, 0.8f, 1.1f),
                        new Layer("grass_tall", 6, 0.09f, 0.7f, 1.1f),
                        new Layer("straw", 16, 0.05f, 0.8f, 1.4f),
                        new Layer("flower", 2, 0.05f, 0.9f, 1.2f),
                        new Layer("flower_red", 2, 0.05f, 0.9f, 1.2f),
                        new Layer("pebbles", 1, 0.12f, 0.7f, 1.1f));
                    break;
            }

            return true;
        }

        /// <summary>One kind of piece to fill a hex with: how many, how much room each needs, how big it may be.</summary>
        private readonly struct Layer
        {
            public readonly string Name;
            public readonly int Count;
            public readonly float Room;
            public readonly float MinSize;
            public readonly float MaxSize;

            public Layer(string name, int count, float room, float minSize, float maxSize)
            {
                Name = name;
                Count = count;
                Room = room;
                MinSize = minSize;
                MaxSize = maxSize;
            }
        }

        /// <summary>The corner radius of the area a hex's pieces may stand in.</summary>
        private const float FillRadius = 0.86f;

        /// <summary>
        /// Fills a hex the way a Civilization tile is filled: many pieces packed
        /// across the whole hexagon, not a few in a circle, none standing in
        /// another. The layers go in order, the largest first, so the trees find
        /// room before the grass fills what is left; a piece keeps its own room
        /// clear, ground cover only half of it. The draws come from the hex's own
        /// generator, so the same hex looks the same every time.
        /// </summary>
        private static void Fill(Transform parent, Rng rng, params Layer[] layers)
        {
            FillAround(parent, rng, 0f, layers);
        }

        /// <summary>As <see cref="Fill"/>, keeping clear of a round middle of that radius, where something big already stands.</summary>
        private static void FillAround(Transform parent, Rng rng, float keepOut, params Layer[] layers)
        {
            FillWhere(parent, rng, at => new Vector2(at.x, at.z).sqrMagnitude >= keepOut * keepOut, layers);
        }

        /// <summary>
        /// As <see cref="Fill"/>, on the open ground in front of a cliff that stands at that turn:
        /// the cliff's shelf fills the back of the hex, and nothing may stand in it.
        /// </summary>
        private static void FillFront(Transform parent, Rng rng, float yaw, params Layer[] layers)
        {
            GameObject front = new GameObject("Front");
            front.transform.SetParent(parent, false);
            front.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            FillWhere(front.transform, rng, at => at.z > 0.16f, layers);
        }

        /// <summary>
        /// A broken ring of stones along the edge of the hex: boulders, pebbles and lichened
        /// rocks walk the border, so the hex reads as rough ground to its very rim.
        /// </summary>
        private static void RimStones(Transform parent, Rng rng)
        {
            GameObject rim = new GameObject("Rim");
            rim.transform.SetParent(parent, false);
            string[] kinds = { "stone_a", "stone_b", "pebbles", "stone_b", "stone_a", "pebbles", "lichen_a" };
            float radius = FillRadius * 0.97f;
            for (int side = 0; side < 6; side += 1)
            {
                // Pointy-topped: the corners stand at 30, 90, 150 ... degrees.
                float a = (30f + side * 60f) * Mathf.Deg2Rad;
                float b = (90f + side * 60f) * Mathf.Deg2Rad;
                Vector3 from = new Vector3(Mathf.Cos(a), 0f, Mathf.Sin(a)) * radius;
                Vector3 to = new Vector3(Mathf.Cos(b), 0f, Mathf.Sin(b)) * radius;
                const int perSide = 8;
                for (int index = 0; index < perSide; index += 1)
                {
                    float along = (index + 0.2f + (float)rng.Next() * 0.6f) / perSide;
                    // Pulled in a little, by a different amount each time, so the line is not ruled.
                    Vector3 at = Vector3.Lerp(from, to, along) * (0.9f + (float)rng.Next() * 0.07f);
                    string kind = kinds[(int)((float)rng.Next() * kinds.Length) % kinds.Length];
                    bool big = kind.StartsWith("stone");
                    // Now and then a gap, as a real wall of scree has.
                    if (rng.Next() < 0.12)
                    {
                        continue;
                    }

                    Piece(rim.transform, rng, kind, at, big ? 0.35f : 0.6f, big ? 0.6f : 0.9f);
                }
            }
        }

        private static void FillWhere(Transform parent, Rng rng, System.Func<Vector3, bool> isOpen, params Layer[] layers)
        {
            List<Vector4> taken = new List<Vector4>();
            foreach (Layer layer in layers)
            {
                for (int index = 0; index < layer.Count; index += 1)
                {
                    for (int attempt = 0; attempt < 24; attempt += 1)
                    {
                        Vector3 at = PointInHex(rng, FillRadius - layer.Room * 0.35f);
                        bool isFree = isOpen(at);
                        foreach (Vector4 other in taken)
                        {
                            float room = (layer.Room + other.z) * 0.5f;
                            if ((new Vector2(at.x, at.z) - new Vector2(other.x, other.y)).sqrMagnitude < room * room)
                            {
                                isFree = false;

                                break;
                            }
                        }

                        if (!isFree)
                        {
                            continue;
                        }

                        Piece(parent, rng, layer.Name, at, layer.MinSize, layer.MaxSize);
                        taken.Add(new Vector4(at.x, at.z, layer.Room, 0f));

                        break;
                    }
                }
            }
        }

        /// <summary>A point inside a pointy-topped hexagon of that corner radius, every point as likely.</summary>
        private static Vector3 PointInHex(Rng rng, float radius)
        {
            float half = radius * 0.8660254f;
            for (int attempt = 0; attempt < 16; attempt += 1)
            {
                float x = ((float)rng.Next() * 2f - 1f) * half;
                float z = ((float)rng.Next() * 2f - 1f) * radius;
                if (Mathf.Abs(z) + Mathf.Abs(x) * 0.5773503f <= radius)
                {
                    return new Vector3(x, 0f, z);
                }
            }

            return Vector3.zero;
        }

        /// <summary>A piece at a set turn and size, for one that fills the hex and must line up with it.</summary>
        private static void PieceTurned(Transform parent, string name, Vector3 position, float size, float yaw)
        {
            GameObject prefab = SceneryPrefab(name);
            if (prefab == null)
            {
                return;
            }

            Transform made = Object.Instantiate(prefab, parent, false).transform;
            made.name = name;
            made.localPosition = position;
            made.localRotation = Quaternion.Euler(0f, yaw, 0f);
            made.localScale = Vector3.one * size;

            // A piece that has life of its own, as the volcano's smoke and fire, gets it here.
            if (name == "volcano_big" && _sceneryMaterial != null)
            {
                made.gameObject.AddComponent<BuildingLife>().Bind(_sceneryMaterial);
            }
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
                // What turns, flies and smokes on it.
                instance.AddComponent<BuildingLife>().Bind(material);

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

        /// <summary>
        /// Turns a standing building into its ruin. A modelled building is made in
        /// the stages it was built in, so it comes apart the same way: the roof and
        /// the fittings are gone, the walls stand broken to a third of their height
        /// and out of true, and rubble lies heaped where they fell. A building with
        /// no stages is sunk and knocked over, as before.
        /// </summary>
        public static void MakeRuin(GameObject structure, Material material)
        {
            bool hasStages = false;
            foreach (Transform part in structure.GetComponentsInChildren<Transform>(true))
            {
                if (part.name.EndsWith("__S2") || part.name.EndsWith("__S3"))
                {
                    part.gameObject.SetActive(false);
                    hasStages = true;
                }
                else if (part.name.Contains("__") && !part.name.EndsWith("__S0") && !part.name.EndsWith("__S1"))
                {
                    // What turned and flew on the building is gone with its roof.
                    part.gameObject.SetActive(false);
                }
                else if (part.name.EndsWith("__S1"))
                {
                    Vector3 scale = part.localScale;
                    part.localScale = new Vector3(scale.x, scale.y * 0.36f, scale.z);
                    part.localRotation = part.localRotation * Quaternion.Euler(2.5f, -3f, 0f);
                    hasStages = true;
                }
            }

            if (!hasStages)
            {
                structure.transform.localScale = new Vector3(1f, 0.35f, 1f);
                structure.transform.localRotation = Quaternion.Euler(6f, 0f, -8f);

                return;
            }

            GameObject rubble = new GameObject("Rubble");
            rubble.transform.SetParent(structure.transform, false);
            Transform t = rubble.transform;
            Rng rng = Rng.FromText("rubble:" + structure.transform.parent.name);
            Color[] colors = { Hex("#8b8d93"), Hex("#5b5d66"), Hex("#cdbf9c"), Hex("#4a3324"), Hex("#3a2a40") };
            for (int index = 0; index < 16; index += 1)
            {
                float angle = (float)rng.Next() * Mathf.PI * 2f;
                float reach = 0.12f + Mathf.Sqrt((float)rng.Next()) * 0.46f;
                // The heap is highest in the middle, where the roof came down.
                float size = Mathf.Lerp(0.2f, 0.08f, reach / 0.58f) * (0.7f + (float)rng.Next() * 0.6f);
                Transform stone = Part(t, Cube, material, new Vector3(Mathf.Cos(angle) * reach, size * 0.3f, Mathf.Sin(angle) * reach),
                    new Vector3(size * 1.3f, size * 0.7f, size), colors[index % colors.Length] * (0.7f + (float)rng.Next() * 0.3f));
                stone.localRotation = Quaternion.Euler((float)rng.Next() * 40f - 20f, (float)rng.Next() * 360f, (float)rng.Next() * 40f - 20f);
            }

            // Two charred beams lie across the heap.
            for (int index = 0; index < 2; index += 1)
            {
                Transform beam = Part(t, Cube, material, new Vector3(index == 0 ? -0.12f : 0.16f, 0.16f, index == 0 ? 0.08f : -0.1f), new Vector3(0.05f, 0.05f, 0.62f), Hex("#241a14"));
                beam.localRotation = Quaternion.Euler(index == 0 ? 18f : -12f, index == 0 ? 35f : 110f, 0f);
            }
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
