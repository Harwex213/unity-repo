using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public sealed class Building
    {
        public BuildingId Id;
        /// <summary>The prototype's id, which also names the art (<c>masons-guild.png</c> for <c>masons_guild</c>).</summary>
        public string Key;
        public string Label;
        /// <summary>The resource this building exists for, shown as its purpose in the UI.</summary>
        public ResourceId Yields;
        public BuildCost Cost;
        /// <summary>
        /// A trophy building is unlocked by defeating the boss, not by research. It
        /// is left out of the hex suggestions and of the rivals' starting islands.
        /// </summary>
        public bool Trophy;
        /// <summary>Faces every copy of this building has, whatever it stands on.</summary>
        public Face[] BaseFaces;
        /// <summary>The extra face a biome adds. A biome missing here cannot host the building.</summary>
        public Dictionary<BiomeId, Face> BiomeFaces;

        /// <summary>The file name of the building's icon and hex sprite in the prototype's assets.</summary>
        public string ArtName
        {
            get { return Key.Replace('_', '-'); }
        }
    }

    /// <summary>
    /// The seven buildings of the spec with their yield tables, and the converter
    /// (<c>core/buildings.ts</c>). A building is a die: <c>BaseFaces</c> are the
    /// faces every copy has, and the biome it stands on adds one more face.
    /// </summary>
    public static class Buildings
    {
        /// <summary>The prototype's order. The rivals' starting islands pick from it by index.</summary>
        public static readonly IReadOnlyList<Building> All = CreateAll();

        private static readonly Dictionary<BuildingId, Building> ById = Index(All);

        public static Building Get(BuildingId id)
        {
            return ById[id];
        }

        /// <summary>A building can stand on a biome only if the spec gives it a face there.</summary>
        public static bool CanBuildOn(Building building, BiomeId biomeId)
        {
            return building.BiomeFaces.ContainsKey(biomeId);
        }

        /// <summary>The regular buildings a biome can host. Trophy buildings are left out.</summary>
        public static List<Building> ForBiome(BiomeId biomeId)
        {
            List<Building> allowed = new List<Building>();
            for (int index = 0; index < All.Count; index += 1)
            {
                if (!All[index].Trophy && CanBuildOn(All[index], biomeId))
                {
                    allowed.Add(All[index]);
                }
            }

            return allowed;
        }

        /// <summary>The player owns a central converter: their buildings leave no toxicity.</summary>
        public static bool HasConverter(Player player)
        {
            for (int index = 0; index < player.Hexes.Count; index += 1)
            {
                if (player.Hexes[index].Building == BuildingId.Converter)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>A trophy building is locked until its owner has defeated the boss.</summary>
        public static bool IsUnlocked(Player player, Building building)
        {
            return !building.Trophy || player.BossSlain;
        }

        /// <summary>The die a building becomes once it stands on a biome: base faces plus one.</summary>
        public static Face[] FacesOn(Building building, BiomeId biomeId)
        {
            if (!building.BiomeFaces.TryGetValue(biomeId, out Face biomeFace))
            {
                return building.BaseFaces;
            }

            Face[] faces = new Face[building.BaseFaces.Length + 1];
            Array.Copy(building.BaseFaces, faces, building.BaseFaces.Length);
            faces[faces.Length - 1] = biomeFace;

            return faces;
        }

        /// <summary>The mean amount a die pays per roll, before the hex's toxicity.</summary>
        public static double AverageAmount(IReadOnlyList<Face> faces)
        {
            if (faces.Count == 0)
            {
                return 0;
            }

            double sum = 0;
            for (int index = 0; index < faces.Count; index += 1)
            {
                sum += faces[index].Amount;
            }

            return sum / faces.Count;
        }

        /// <summary>The mean toxicity a die leaves per roll, in face points.</summary>
        public static double AverageToxicity(IReadOnlyList<Face> faces)
        {
            if (faces.Count == 0)
            {
                return 0;
            }

            double sum = 0;
            for (int index = 0; index < faces.Count; index += 1)
            {
                sum += faces[index].Toxicity;
            }

            return sum / faces.Count;
        }

        public static double AverageYieldOn(Building building, BiomeId biomeId)
        {
            return AverageAmount(FacesOn(building, biomeId));
        }

        /// <summary>What the hex panel suggests: the allowed building with the richest die.</summary>
        public static Building BestForBiome(BiomeId biomeId)
        {
            List<Building> allowed = ForBiome(biomeId);
            Building best = null;

            for (int index = 0; index < allowed.Count; index += 1)
            {
                if (best == null || AverageYieldOn(allowed[index], biomeId) > AverageYieldOn(best, biomeId))
                {
                    best = allowed[index];
                }
            }

            return best;
        }

        /// <summary>The price after technologies. Masonry takes a stone off every building.</summary>
        public static BuildCost EffectiveCost(Building building, int stoneDiscount)
        {
            return new BuildCost(Math.Max(0, building.Cost.Stone - stoneDiscount), building.Cost.Wood, building.Cost.Hammers);
        }

        public static bool CanAfford(ResourcePool pool, Building building, int stoneDiscount)
        {
            BuildCost cost = EffectiveCost(building, stoneDiscount);

            return pool[ResourceId.Stone] >= cost.Stone && pool[ResourceId.Wood] >= cost.Wood && pool[ResourceId.Hammers] >= cost.Hammers;
        }

        private static Dictionary<BuildingId, Building> Index(IReadOnlyList<Building> buildings)
        {
            Dictionary<BuildingId, Building> byId = new Dictionary<BuildingId, Building>();
            for (int index = 0; index < buildings.Count; index += 1)
            {
                byId[buildings[index].Id] = buildings[index];
            }

            return byId;
        }

        /// <summary>
        /// Village, masons guild, observatory and university are written in the spec
        /// with the same numbers on the same eight biomes; only the resource differs.
        /// </summary>
        private static Face[] SettlementBaseFaces(ResourceId resource)
        {
            return new[]
            {
                new Face(resource, 1, 0),
                new Face(resource, 2, 2),
                new Face(resource, 3, 3),
                new Face(resource, 1, 1),
            };
        }

        private static Dictionary<BiomeId, Face> SettlementBiomeFaces(ResourceId resource)
        {
            return new Dictionary<BiomeId, Face>
            {
                { BiomeId.Grassland, new Face(resource, 3, 1) },
                { BiomeId.Plains, new Face(resource, 2, 1) },
                { BiomeId.Desert, new Face(resource, 1, 2) },
                { BiomeId.Tundra, new Face(resource, 2, 2) },
                { BiomeId.PolarDesert, new Face(resource, 1, 3) },
                { BiomeId.Swamp, new Face(resource, 2, 3) },
                { BiomeId.Badlands, new Face(resource, 1, 4) },
                { BiomeId.Cliffs, new Face(resource, 1, 0) },
            };
        }

        private static IReadOnlyList<Building> CreateAll()
        {
            // The converter stands on any biome, and no face leaves toxicity.
            Dictionary<BiomeId, Face> converterFaces = new Dictionary<BiomeId, Face>();
            foreach (BiomeId biomeId in Enum.GetValues(typeof(BiomeId)))
            {
                converterFaces[biomeId] = new Face(ResourceId.Mana, 2, 0);
            }

            return new[]
            {
                new Building
                {
                    Id = BuildingId.Farm,
                    Key = "farm",
                    Label = "Ферма",
                    Yields = ResourceId.Food,
                    Cost = new BuildCost(1, 3, 1),
                    BaseFaces = new[] { new Face(ResourceId.Food, 1, 0), new Face(ResourceId.Food, 5, 0), new Face(ResourceId.Food, 3, 0), new Face(ResourceId.Food, 3, 0) },
                    BiomeFaces = new Dictionary<BiomeId, Face>
                    {
                        { BiomeId.Grassland, new Face(ResourceId.Food, 4, 1) },
                        { BiomeId.Plains, new Face(ResourceId.Food, 2, 0) },
                        { BiomeId.Tundra, new Face(ResourceId.Food, 2, 0) },
                        { BiomeId.Swamp, new Face(ResourceId.Food, 5, 3) },
                        { BiomeId.Hills, new Face(ResourceId.Food, 3, 1) },
                    },
                },
                new Building
                {
                    Id = BuildingId.Mine,
                    Key = "mine",
                    Label = "Рудник",
                    Yields = ResourceId.Stone,
                    Cost = new BuildCost(3, 2, 2),
                    BaseFaces = new[] { new Face(ResourceId.Stone, 1, 0), new Face(ResourceId.Stone, 5, 3), new Face(ResourceId.Stone, 3, 2), new Face(ResourceId.Stone, 3, 1) },
                    BiomeFaces = new Dictionary<BiomeId, Face>
                    {
                        { BiomeId.Mountains, new Face(ResourceId.Stone, 7, 3) },
                        { BiomeId.Volcano, new Face(ResourceId.Stone, 10, 5) },
                        { BiomeId.Crater, new Face(ResourceId.Stone, 3, 1) },
                        { BiomeId.Cliffs, new Face(ResourceId.Stone, 4, 1) },
                        { BiomeId.Swamp, new Face(ResourceId.Stone, 2, 1) },
                    },
                },
                new Building
                {
                    Id = BuildingId.Sawmill,
                    Key = "sawmill",
                    Label = "Лесопилка",
                    Yields = ResourceId.Wood,
                    Cost = new BuildCost(2, 3, 1),
                    BaseFaces = new[] { new Face(ResourceId.Wood, 1, 0), new Face(ResourceId.Wood, 5, 3), new Face(ResourceId.Wood, 3, 2), new Face(ResourceId.Wood, 3, 1) },
                    BiomeFaces = new Dictionary<BiomeId, Face>
                    {
                        { BiomeId.Forrest, new Face(ResourceId.Wood, 5, 1) },
                        { BiomeId.Savanna, new Face(ResourceId.Wood, 2, 0) },
                        { BiomeId.Rainforest, new Face(ResourceId.Wood, 8, 4) },
                        { BiomeId.Taiga, new Face(ResourceId.Wood, 5, 2) },
                    },
                },
                new Building
                {
                    Id = BuildingId.Village,
                    Key = "village",
                    Label = "Деревня",
                    Yields = ResourceId.Population,
                    Cost = new BuildCost(3, 3, 1),
                    BaseFaces = SettlementBaseFaces(ResourceId.Population),
                    BiomeFaces = SettlementBiomeFaces(ResourceId.Population),
                },
                new Building
                {
                    Id = BuildingId.MasonsGuild,
                    Key = "masons_guild",
                    Label = "Гильдия масонов",
                    Yields = ResourceId.Hammers,
                    Cost = new BuildCost(4, 2, 2),
                    BaseFaces = SettlementBaseFaces(ResourceId.Hammers),
                    BiomeFaces = SettlementBiomeFaces(ResourceId.Hammers),
                },
                new Building
                {
                    Id = BuildingId.Observatory,
                    Key = "observatory",
                    Label = "Обсерватория",
                    Yields = ResourceId.Scouting,
                    Cost = new BuildCost(3, 3, 3),
                    BaseFaces = SettlementBaseFaces(ResourceId.Scouting),
                    BiomeFaces = SettlementBiomeFaces(ResourceId.Scouting),
                },
                new Building
                {
                    Id = BuildingId.University,
                    Key = "university",
                    Label = "Университет",
                    Yields = ResourceId.Science,
                    Cost = new BuildCost(4, 4, 3),
                    BaseFaces = SettlementBaseFaces(ResourceId.Science),
                    BiomeFaces = SettlementBiomeFaces(ResourceId.Science),
                },
                new Building
                {
                    // The win condition. It turns the toxicity of every building of its
                    // owner off, and building it is the victory over the poison.
                    Id = BuildingId.Converter,
                    Key = "converter",
                    Label = "Центральный конвертер",
                    Yields = ResourceId.Mana,
                    Cost = new BuildCost(10, 10, 6),
                    Trophy = true,
                    BaseFaces = new[] { new Face(ResourceId.Mana, 1, 0), new Face(ResourceId.Mana, 2, 0), new Face(ResourceId.Mana, 3, 0), new Face(ResourceId.Mana, 2, 0) },
                    BiomeFaces = converterFaces,
                },
            };
        }
    }
}
