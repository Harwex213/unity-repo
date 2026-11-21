using System.Collections.Generic;
using System.IO;
using System.Linq;
using Hexwex.View;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>The island generator: sizes, biome amounts, shape, determinism, and the file it saves.</summary>
    public sealed class IslandForgeTests
    {
        private static string Describe(ForgedIsland island)
        {
            return island.Name + "|" + string.Join(" ", island.Hexes.Select(hex => hex.Id + ":" + hex.Biome + ":" + hex.Toxicity + ":" + hex.Building));
        }

        private static bool IsConnected(List<HexTile> hexes)
        {
            HashSet<string> ids = new HashSet<string>(hexes.Select(hex => hex.Id));
            HashSet<string> reached = new HashSet<string> { hexes[0].Id };
            Queue<HexTile> queue = new Queue<HexTile>(new[] { hexes[0] });
            Dictionary<string, HexTile> byId = hexes.ToDictionary(hex => hex.Id);
            while (queue.Count > 0)
            {
                HexTile hex = queue.Dequeue();
                foreach (Axial neighbor in HexMath.Neighbors(hex.Q, hex.R))
                {
                    string id = HexMath.HexId(neighbor.Q, neighbor.R);
                    if (ids.Contains(id) && reached.Add(id))
                    {
                        queue.Enqueue(byId[id]);
                    }
                }
            }

            return reached.Count == hexes.Count;
        }

        [Test]
        public void BiomesAreListedInEnumOrder()
        {
            for (int index = 0; index < Biomes.All.Count; index += 1)
            {
                Assert.AreEqual((BiomeId)index, Biomes.All[index].Id);
            }
        }

        [Test]
        public void EveryTypeGivesItsNumberOfIslandsOfItsSize()
        {
            IslandSetSpec spec = new IslandSetSpec { Seed = "test" };
            spec.Recipes.Add(new IslandRecipe { Name = "Малый", Count = 3, Size = 12 });
            spec.Recipes.Add(new IslandRecipe { Name = "Большой", Count = 2, Size = 60, Compactness = 0.1 });

            List<ForgedIsland> islands = IslandForge.Generate(spec);

            Assert.AreEqual(5, islands.Count);
            CollectionAssert.AreEqual(new[] { "Малый 1", "Малый 2", "Малый 3", "Большой 1", "Большой 2" }, islands.Select(island => island.Name));
            Assert.IsTrue(islands.Take(3).All(island => island.Hexes.Count == 12));
            Assert.IsTrue(islands.Skip(3).All(island => island.Hexes.Count == 60));
            foreach (ForgedIsland island in islands)
            {
                Assert.IsTrue(IsConnected(island.Hexes), island.Name + " is in pieces");
                Assert.AreEqual(island.Hexes.Count, island.Hexes.Select(hex => hex.Id).Distinct().Count(), island.Name + " has a hex twice");
            }
        }

        [Test]
        public void ExactCountsAreKeptHexForHex()
        {
            IslandRecipe recipe = new IslandRecipe { Mode = BiomeAmountMode.Count, Biomes = new int[Biomes.All.Count], RequireBuildable = false, Clumping = 0.3 };
            recipe.Biomes[(int)BiomeId.Forrest] = 7;
            recipe.Biomes[(int)BiomeId.Mountains] = 4;
            recipe.Biomes[(int)BiomeId.Swamp] = 2;

            List<HexTile> hexes = IslandForge.Grow(recipe, Rng.FromText("counts"));

            Assert.AreEqual(13, hexes.Count);
            Assert.AreEqual(7, hexes.Count(hex => hex.Biome == BiomeId.Forrest));
            Assert.AreEqual(4, hexes.Count(hex => hex.Biome == BiomeId.Mountains));
            Assert.AreEqual(2, hexes.Count(hex => hex.Biome == BiomeId.Swamp));
        }

        [Test]
        public void SharesSplitTheSizeInProportion()
        {
            IslandRecipe recipe = new IslandRecipe { Size = 40, Biomes = new int[Biomes.All.Count] };
            recipe.Biomes[(int)BiomeId.Desert] = 3;
            recipe.Biomes[(int)BiomeId.Savanna] = 1;

            int[] quotas = IslandForge.Quotas(recipe, 40);

            Assert.AreEqual(30, quotas[(int)BiomeId.Desert]);
            Assert.AreEqual(10, quotas[(int)BiomeId.Savanna]);
            Assert.AreEqual(40, quotas.Sum());
        }

        [Test]
        public void FullClumpingKeepsABiomeInOneRegion()
        {
            IslandRecipe recipe = new IslandRecipe { Mode = BiomeAmountMode.Count, Biomes = new int[Biomes.All.Count], RequireBuildable = false, Clumping = 1, Compactness = 1 };
            recipe.Biomes[(int)BiomeId.Taiga] = 20;
            recipe.Biomes[(int)BiomeId.Plains] = 20;

            List<HexTile> hexes = IslandForge.Grow(recipe, Rng.FromText("clumps"));

            // The first biome dealt grows as one region from one seed.
            Assert.IsTrue(IsConnected(hexes.Where(hex => hex.Biome == BiomeId.Plains || hex.Biome == BiomeId.Taiga).Where(hex => hex.Biome == hexes.GroupBy(h => h.Biome).OrderByDescending(g => g.Count()).ThenBy(g => (int)g.Key).First().Key).ToList()));
        }

        [Test]
        public void ToxicityAndBuildingsFollowTheRecipe()
        {
            IslandRecipe recipe = new IslandRecipe { Size = 50, ToxicityMin = 20, ToxicityMax = 35, BuildingChance = 1 };

            List<HexTile> hexes = IslandForge.Grow(recipe, Rng.FromText("built"));

            Assert.IsTrue(hexes.All(hex => hex.Toxicity >= 20 && hex.Toxicity <= 35));
            List<HexTile> built = hexes.Where(hex => hex.Building.HasValue).ToList();
            Assert.IsNotEmpty(built);
            Assert.IsTrue(built.All(hex => Buildings.CanBuildOn(Buildings.Get(hex.Building.Value), hex.Biome)));
            Assert.IsFalse(built.Any(hex => hex.Building == BuildingId.Converter));
            Assert.IsTrue(built.All(hex => hex.Workers == Population.WorkersPerBuilding));
        }

        [Test]
        public void TheSameSpecGrowsTheSameIslands()
        {
            IslandSetSpec spec = new IslandSetSpec { Seed = "same" };
            spec.Recipes.Add(new IslandRecipe { Count = 4, Size = 25, BuildingChance = 0.3, ToxicityMax = 40 });

            CollectionAssert.AreEqual(IslandForge.Generate(spec).Select(Describe), IslandForge.Generate(spec).Select(Describe));

            spec.Seed = "other";
            CollectionAssert.AreNotEqual(IslandForge.Generate(new IslandSetSpec { Seed = "same", Recipes = spec.Recipes }).Select(Describe), IslandForge.Generate(spec).Select(Describe));
        }

        [Test]
        public void ASetSurvivesItsFile()
        {
            IslandSetSpec spec = new IslandSetSpec { Seed = "file" };
            spec.Recipes.Add(new IslandRecipe { Name = "Лесной", Count = 2, Size = 20, Mode = BiomeAmountMode.Share, BuildingChance = 0.4, ToxicityMin = 5, ToxicityMax = 15 });
            IslandRecipe exact = new IslandRecipe { Name = "Горный", Count = 1, Mode = BiomeAmountMode.Count, Biomes = new int[Biomes.All.Count], Compactness = 0.2, Clumping = 0.9, RequireBuildable = false };
            exact.Biomes[(int)BiomeId.Mountains] = 9;
            exact.Biomes[(int)BiomeId.Cliffs] = 3;
            spec.Recipes.Add(exact);
            List<ForgedIsland> islands = IslandForge.Generate(spec);

            string json = IslandSetFile.ToJson("test", spec, islands);
            IslandSetFile.FromJson(json, out IslandSetSpec readSpec, out List<ForgedIsland> readIslands);

            StringAssert.Contains("\"biome\": \"mountains\"", json);
            Assert.AreEqual("file", readSpec.Seed);
            Assert.AreEqual(2, readSpec.Recipes.Count);
            Assert.AreEqual(BiomeAmountMode.Count, readSpec.Recipes[1].Mode);
            CollectionAssert.AreEqual(exact.Biomes, readSpec.Recipes[1].Biomes);
            CollectionAssert.AreEqual(islands.Select(Describe), readIslands.Select(Describe));

            // The settings read back grow the very same islands.
            CollectionAssert.AreEqual(islands.Select(Describe), IslandForge.Generate(readSpec).Select(Describe));
        }

        [Test]
        public void ABrokenFileIsRefused()
        {
            Assert.Throws<InvalidDataException>(() => IslandSetFile.FromJson("{ not json", out _, out _));
            string unknown = "{\"version\":1,\"settings\":{\"seed\":\"x\",\"recipes\":[]},\"islands\":[{\"name\":\"a\",\"hexes\":[{\"q\":0,\"r\":0,\"biome\":\"lava-sea\"}]}]}";
            Assert.Throws<InvalidDataException>(() => IslandSetFile.FromJson(unknown, out _, out _));
        }

        [Test]
        public void FileNamesAreMadeSafe()
        {
            Assert.AreEqual("Wild_islands_v2", IslandSetFile.SafeName("Wild islands v2"));
            Assert.AreEqual("islands", IslandSetFile.SafeName("../.."));
        }
    }
}
