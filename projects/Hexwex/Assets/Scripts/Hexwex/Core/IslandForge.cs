using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>How a recipe's biome numbers are read.</summary>
    public enum BiomeAmountMode
    {
        /// <summary>Relative weights: the island's size is shared out among the biomes in proportion.</summary>
        Share,
        /// <summary>Exact hex counts: the island has that many hexes of each biome, and its size is their sum.</summary>
        Count,
    }

    /// <summary>
    /// One type of island and how many of it to make: its size and shape, which
    /// biomes it is made of and in what amounts, how they lie together, and what
    /// stands on it already.
    /// </summary>
    public sealed class IslandRecipe
    {
        public const int MinSize = 1;
        public const int MaxSize = 200;

        public string Name = "Остров";
        /// <summary>How many islands of this type the set holds.</summary>
        public int Count = 1;
        /// <summary>Hexes per island in <see cref="BiomeAmountMode.Share"/>; ignored in <see cref="BiomeAmountMode.Count"/>.</summary>
        public int Size = 30;
        /// <summary>0 grows a ragged, stringy outline; 1 a round, solid one.</summary>
        public double Compactness = 0.6;
        /// <summary>0 scatters every biome hex by hex; 1 keeps each biome in one region.</summary>
        public double Clumping = 0.6;
        public BiomeAmountMode Mode = BiomeAmountMode.Share;
        /// <summary>A weight or a count per biome, by <see cref="BiomeId"/>.</summary>
        public int[] Biomes = DefaultWeights();
        /// <summary>Makes sure the island has somewhere to mine, to log and to farm. It may change a hex or two.</summary>
        public bool RequireBuildable = true;
        public int ToxicityMin;
        public int ToxicityMax;
        /// <summary>The share of hexes, 0..1, that start with a building that suits their biome.</summary>
        public double BuildingChance;

        /// <summary>The game's own biome weights.</summary>
        public static int[] DefaultWeights()
        {
            return Hexwex.Core.Biomes.All.Select(biome => IslandGen.DefaultBiomeWeight(biome.Id)).ToArray();
        }

        /// <summary>The number of hexes an island of this type has.</summary>
        public int IslandSize
        {
            get
            {
                if (Mode == BiomeAmountMode.Count)
                {
                    int sum = Biomes.Where(amount => amount > 0).Sum();
                    if (sum > 0)
                    {
                        return Math.Min(MaxSize, sum);
                    }
                }

                return Math.Max(MinSize, Math.Min(MaxSize, Size));
            }
        }

        public IslandRecipe Clone()
        {
            IslandRecipe copy = (IslandRecipe)MemberwiseClone();
            copy.Biomes = (int[])Biomes.Clone();

            return copy;
        }
    }

    /// <summary>What a set of islands is made from: a seed and the island types.</summary>
    public sealed class IslandSetSpec
    {
        public string Seed = "Mom010";
        public List<IslandRecipe> Recipes = new List<IslandRecipe>();
    }

    /// <summary>One generated island.</summary>
    public sealed class ForgedIsland
    {
        /// <summary>The recipe's name and the island's number in it: "Лесной 2".</summary>
        public string Name;
        public string Recipe;
        /// <summary>The seed text the island grew from. The same spec always grows the same island.</summary>
        public string Seed;
        public List<HexTile> Hexes = new List<HexTile>();

        /// <summary>How many hexes of each biome, by <see cref="BiomeId"/>.</summary>
        public int[] BiomeCounts()
        {
            int[] counts = new int[Hexwex.Core.Biomes.All.Count];
            foreach (HexTile hex in Hexes)
            {
                counts[(int)hex.Biome] += 1;
            }

            return counts;
        }
    }

    /// <summary>
    /// Grows sets of islands to order, for the island generator. An island grows
    /// in two steps: first its outline, one hex at a time from the middle out,
    /// then its biomes, which are dealt out in the amounts asked for and laid in
    /// regions or scattered. Everything is seeded from the spec, so the same spec
    /// always grows the same islands.
    /// </summary>
    public static class IslandForge
    {
        public static List<ForgedIsland> Generate(IslandSetSpec spec)
        {
            List<ForgedIsland> islands = new List<ForgedIsland>();
            string seed = string.IsNullOrWhiteSpace(spec.Seed) ? GameSession.DefaultNickname : spec.Seed.Trim();
            for (int type = 0; type < spec.Recipes.Count; type += 1)
            {
                IslandRecipe recipe = spec.Recipes[type];
                for (int number = 1; number <= Math.Max(0, recipe.Count); number += 1)
                {
                    string islandSeed = seed + ":" + type + ":" + number;
                    islands.Add(new ForgedIsland
                    {
                        Name = (string.IsNullOrWhiteSpace(recipe.Name) ? "Остров" : recipe.Name.Trim()) + " " + number,
                        Recipe = recipe.Name,
                        Seed = islandSeed,
                        Hexes = Grow(recipe, Rng.FromText(islandSeed)),
                    });
                }
            }

            return islands;
        }

        /// <summary>One island of the recipe.</summary>
        public static List<HexTile> Grow(IslandRecipe recipe, Rng rng)
        {
            List<Axial> cells = Outline(recipe.IslandSize, recipe.Compactness, rng);
            int[] quotas = Quotas(recipe, cells.Count);
            BiomeId[] biomes = Paint(cells, quotas, recipe.Clumping, rng);

            List<HexTile> hexes = new List<HexTile>(cells.Count);
            for (int index = 0; index < cells.Count; index += 1)
            {
                hexes.Add(new HexTile { Id = HexMath.HexId(cells[index].Q, cells[index].R), Q = cells[index].Q, R = cells[index].R, Biome = biomes[index] });
            }

            if (recipe.RequireBuildable)
            {
                IslandGen.EnforceRequiredGroups(hexes, rng);
            }

            int low = Math.Max(0, Math.Min(recipe.ToxicityMin, recipe.ToxicityMax));
            int high = Math.Min(Tax.DeadToxicityPct, Math.Max(recipe.ToxicityMin, recipe.ToxicityMax));
            foreach (HexTile hex in hexes)
            {
                hex.Toxicity = rng.RandomInt(low, high);
                if (recipe.BuildingChance > 0 && rng.Next() < recipe.BuildingChance)
                {
                    List<Building> allowed = Buildings.ForBiome(hex.Biome).FindAll(building => building.Id != BuildingId.Converter);
                    if (allowed.Count > 0)
                    {
                        hex.Building = rng.Pick(allowed).Id;
                        hex.Workers = Population.WorkersPerBuilding;
                    }
                }
            }

            return hexes;
        }

        /// <summary>
        /// The outline: <paramref name="size"/> connected hexes grown from the origin.
        /// Each new hex is drawn from the free hexes that touch the island, weighted
        /// by how many island hexes it touches raised to a power that grows with
        /// <paramref name="compactness"/>: at 0 every free hex is as likely, which
        /// grows arms and bays; at 1 the hexes that fill a notch win, which grows a
        /// round island.
        /// </summary>
        public static List<Axial> Outline(int size, double compactness, Rng rng)
        {
            size = Math.Max(IslandRecipe.MinSize, Math.Min(IslandRecipe.MaxSize, size));
            double power = Math.Max(0, Math.Min(1, compactness)) * 6;
            List<Axial> cells = new List<Axial> { new Axial(0, 0) };
            HashSet<Axial> taken = new HashSet<Axial> { new Axial(0, 0) };

            while (cells.Count < size)
            {
                Dictionary<Axial, int> touching = new Dictionary<Axial, int>();
                foreach (Axial cell in cells)
                {
                    foreach (Axial neighbor in HexMath.Neighbors(cell.Q, cell.R))
                    {
                        if (!taken.Contains(neighbor))
                        {
                            touching[neighbor] = touching.TryGetValue(neighbor, out int count) ? count + 1 : 1;
                        }
                    }
                }

                // A fixed order, so the same seed picks the same hex on every machine.
                List<Axial> frontier = touching.Keys.OrderBy(cell => cell.Q).ThenBy(cell => cell.R).ToList();
                Axial next = rng.PickWeighted(frontier, cell => Math.Pow(touching[cell], power));
                cells.Add(next);
                taken.Add(next);
            }

            return cells;
        }

        /// <summary>How many hexes of each biome an island of <paramref name="size"/> gets, by <see cref="BiomeId"/>.</summary>
        public static int[] Quotas(IslandRecipe recipe, int size)
        {
            int biomeCount = Hexwex.Core.Biomes.All.Count;
            int[] amounts = new int[biomeCount];
            for (int index = 0; index < biomeCount; index += 1)
            {
                amounts[index] = index < recipe.Biomes.Length ? Math.Max(0, recipe.Biomes[index]) : 0;
            }

            if (amounts.Sum() == 0)
            {
                amounts = IslandRecipe.DefaultWeights();
            }

            if (recipe.Mode == BiomeAmountMode.Count && amounts.Sum() == size)
            {
                return amounts;
            }

            // Shares, or counts that had to be cut to the size limit: the largest remainders round up.
            double total = amounts.Sum();
            int[] quotas = new int[biomeCount];
            double[] remainders = new double[biomeCount];
            for (int index = 0; index < biomeCount; index += 1)
            {
                double exact = size * amounts[index] / total;
                quotas[index] = (int)Math.Floor(exact);
                remainders[index] = exact - quotas[index];
            }

            int missing = size - quotas.Sum();
            foreach (int index in Enumerable.Range(0, biomeCount).OrderByDescending(index => remainders[index]).ThenBy(index => index).Take(missing))
            {
                quotas[index] += 1;
            }

            return quotas;
        }

        /// <summary>
        /// Deals the biomes out over the cells. The biggest biome goes first. A
        /// biome starts from a few seed hexes, more of them the lower
        /// <paramref name="clumping"/> is, and grows from its seeds into the free
        /// hexes next to it. At full clumping a biome is one region; at none every
        /// hex of it is a seed of its own, scattered over the island.
        /// </summary>
        public static BiomeId[] Paint(List<Axial> cells, int[] quotas, double clumping, Rng rng)
        {
            clumping = Math.Max(0, Math.Min(1, clumping));
            Dictionary<Axial, int> indexOf = new Dictionary<Axial, int>();
            for (int index = 0; index < cells.Count; index += 1)
            {
                indexOf[cells[index]] = index;
            }

            BiomeId?[] painted = new BiomeId?[cells.Count];
            List<int> free = Enumerable.Range(0, cells.Count).ToList();
            List<int> order = Enumerable.Range(0, quotas.Length).Where(index => quotas[index] > 0).OrderByDescending(index => quotas[index]).ThenBy(index => index).ToList();

            foreach (int biomeIndex in order)
            {
                BiomeId biome = (BiomeId)biomeIndex;
                int left = Math.Min(quotas[biomeIndex], free.Count);
                int seedsLeft = 1 + (int)Math.Round((left - 1) * (1 - clumping));
                List<int> region = new List<int>();

                while (left > 0)
                {
                    List<int> frontier = new List<int>();
                    foreach (int member in region)
                    {
                        foreach (Axial neighbor in HexMath.Neighbors(cells[member].Q, cells[member].R))
                        {
                            if (indexOf.TryGetValue(neighbor, out int candidate) && painted[candidate] == null && !frontier.Contains(candidate))
                            {
                                frontier.Add(candidate);
                            }
                        }
                    }

                    // A new seed while seeds are owed, at the rate they are owed, or when the region is walled in.
                    bool isSeed = frontier.Count == 0 || (seedsLeft > 0 && rng.Next() < (double)seedsLeft / left);
                    int chosen = isSeed ? free[rng.NextIndex(free.Count)] : frontier[rng.NextIndex(frontier.Count)];
                    if (isSeed)
                    {
                        seedsLeft = Math.Max(0, seedsLeft - 1);
                    }

                    painted[chosen] = biome;
                    free.Remove(chosen);
                    region.Add(chosen);
                    left -= 1;
                }
            }

            // Cells no quota reached (only when the quotas fall short of the cells) take the biggest biome.
            BiomeId fallback = order.Count > 0 ? (BiomeId)order[0] : BiomeId.Grassland;

            return painted.Select(biome => biome ?? fallback).ToArray();
        }
    }
}
