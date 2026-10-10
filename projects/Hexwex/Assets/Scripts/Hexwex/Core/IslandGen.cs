using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>
    /// Islands are grown, not authored (<c>core/island-gen.ts</c>). A flying
    /// island is a blob of hexes around the origin; biomes grow in clusters so
    /// the map reads as terrain instead of confetti.
    ///
    /// Every draw from the generator is made in the prototype's order. Reordering
    /// a loop here changes every island of every seed.
    /// </summary>
    public static class IslandGen
    {
        public const int IslandRadius = 3;
        public const string HumanPlayerId = "human";

        /// <summary>Hexes on the last ring are dropped this often, which frays the silhouette.</summary>
        private const double RimDropChance = 0.45;
        /// <summary>How much an already placed neighbour pulls the next hex to the same biome.</summary>
        private const double NeighborBias = 2.6;
        private const string HumanColor = "#8fd14f";
        private const double RivalFillChance = 0.55;

        /// <summary>How common each biome is before neighbours are taken into account, by <see cref="BiomeId"/>.</summary>
        private static readonly int[] BiomeWeights = { 10, 9, 8, 5, 4, 6, 5, 4, 2, 4, 3, 2, 1, 7, 5, 4 };

        /// <summary>
        /// A playable island needs somewhere to mine, somewhere to log and somewhere
        /// to farm. Without this the generator can hand out an island where four of
        /// the seven buildings are unbuildable.
        /// </summary>
        private static readonly BiomeId[][] RequiredGroups =
        {
            new[] { BiomeId.Mountains, BiomeId.Volcano, BiomeId.Crater, BiomeId.Cliffs },
            new[] { BiomeId.Forrest, BiomeId.Taiga, BiomeId.Rainforest, BiomeId.Savanna },
            new[] { BiomeId.Grassland, BiomeId.Plains, BiomeId.Hills },
        };

        private static readonly (string id, string nickname, string color)[] Rivals =
        {
            ("carribean", "Carribean Sorcerer", "#4fd1c5"),
            ("blue", "Blue Sorcerer", "#5b9bf5"),
            ("orange", "Orange Sorcerer", "#f59b4c"),
        };

        public static List<HexTile> CreateIsland(Rng rng)
        {
            List<Axial> cells = new List<Axial>();
            foreach (Axial cell in HexMath.Area(IslandRadius))
            {
                bool isRim = Math.Max(Math.Abs(cell.Q), Math.Max(Math.Abs(cell.R), Math.Abs(cell.Q + cell.R))) == IslandRadius;

                // Only a rim cell draws from the generator.
                if (!isRim || rng.Next() > RimDropChance)
                {
                    cells.Add(cell);
                }
            }

            Dictionary<Axial, BiomeId> assigned = new Dictionary<Axial, BiomeId>();
            List<HexTile> hexes = new List<HexTile>();

            foreach (Axial cell in cells)
            {
                BiomeId biome = GrowBiome(rng, cell, assigned);
                assigned[cell] = biome;
                hexes.Add(new HexTile { Id = HexMath.HexId(cell.Q, cell.R), Q = cell.Q, R = cell.R, Biome = biome });
            }

            EnforceRequiredGroups(hexes, rng);

            return hexes;
        }

        /// <summary>
        /// Adds hexes won in the clearing phase. New hexes are stuck to the rim, so
        /// the island stays one piece.
        /// </summary>
        public static void GrowIsland(List<HexTile> hexes, int count, Rng rng)
        {
            for (int added = 0; added < count; added += 1)
            {
                HashSet<Axial> present = new HashSet<Axial>(hexes.Select(hex => hex.Axial));
                // A free cell is listed once per neighbour it touches, as in the
                // prototype: a notch in the rim is likelier to fill than a tip.
                List<Axial> free = hexes
                    .SelectMany(hex => HexMath.Neighbors(hex.Q, hex.R))
                    .Where(cell => !present.Contains(cell))
                    .ToList();

                if (free.Count == 0)
                {
                    break;
                }

                Axial cell = rng.Pick(free);
                Dictionary<Axial, BiomeId> assigned = hexes.ToDictionary(hex => hex.Axial, hex => hex.Biome);
                BiomeId biome = GrowBiome(rng, cell, assigned);

                hexes.Add(new HexTile { Id = HexMath.HexId(cell.Q, cell.R), Q = cell.Q, R = cell.R, Biome = biome });
            }
        }

        /// <summary>The whole roster of a session: the human first, then the three rivals.</summary>
        public static List<Player> CreatePlayers(string nickname)
        {
            Rng humanRng = Rng.FromText(nickname);

            Player human = new Player
            {
                Id = HumanPlayerId,
                Nickname = nickname,
                Color = HumanColor,
                IsHuman = true,
                Hexes = CreateIsland(humanRng),
                Resources = ResourceTable.CreateStartingPool(),
            };

            List<Player> players = new List<Player> { human };
            foreach ((string id, string rivalNickname, string color) in Rivals)
            {
                players.Add(CreateRival(id, rivalNickname, color, nickname));
            }

            return players;
        }

        private static BiomeId GrowBiome(Rng rng, Axial cell, Dictionary<Axial, BiomeId> assigned)
        {
            int[] sameNeighbors = new int[BiomeWeights.Length];
            foreach (Axial neighbor in HexMath.Neighbors(cell.Q, cell.R))
            {
                if (assigned.TryGetValue(neighbor, out BiomeId neighborBiome))
                {
                    sameNeighbors[(int)neighborBiome] += 1;
                }
            }

            return rng.PickWeighted(Biomes.All, candidate =>
            {
                return BiomeWeights[(int)candidate.Id] * (1 + NeighborBias * sameNeighbors[(int)candidate.Id]);
            }).Id;
        }

        /// <summary>Rewrites hexes until every required group is present at least once.</summary>
        private static void EnforceRequiredGroups(List<HexTile> hexes, Rng rng)
        {
            foreach (BiomeId[] group in RequiredGroups)
            {
                if (hexes.Any(hex => Array.IndexOf(group, hex.Biome) >= 0))
                {
                    continue;
                }

                int index = rng.RandomInt(0, hexes.Count - 1);
                hexes[index].Biome = rng.Pick(group);
            }
        }

        /// <summary>Fills part of an island, so a rival's island does not read as unplayed.</summary>
        private static void PopulateIsland(List<HexTile> hexes, Rng rng, double fillChance)
        {
            foreach (HexTile hex in hexes)
            {
                List<Building> allowed = Buildings.ForBiome(hex.Biome);
                if (allowed.Count == 0 || rng.Next() > fillChance)
                {
                    continue;
                }

                hex.Building = rng.Pick(allowed).Id;
                hex.Toxicity = rng.RandomInt(0, 40);
            }
        }

        private static Player CreateRival(string id, string nickname, string color, string seed)
        {
            Rng rng = Rng.FromText(seed + ":" + id);
            List<HexTile> hexes = CreateIsland(rng);
            PopulateIsland(hexes, rng, RivalFillChance);
            int built = hexes.Count(hex => hex.Building.HasValue);

            // The draws below keep the prototype's order: food, mad, army, techs.
            ResourcePool resources = ResourceTable.CreateStartingPool();
            resources[ResourceId.Food] += rng.RandomInt(0, 40);
            resources[ResourceId.Mad] += rng.RandomInt(0, 4);

            return new Player
            {
                Id = id,
                Nickname = nickname,
                Color = color,
                IsHuman = false,
                Hexes = hexes,
                Resources = resources,
                Army = built + rng.RandomInt(0, 8),
                Techs = rng.RandomInt(0, 6),
                HasHadBuildings = built > 0,
            };
        }
    }
}
