using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>
    /// - <c>Void</c>: clouds over the sea. Passable, nothing in it.
    /// - <c>Island</c>: wild islands with monsters. Flying in activates them, and
    ///   the cleanup phase fights them.
    /// - <c>Settlement</c>: a neutral town. Passable, no effect yet.
    /// </summary>
    public enum CellKind
    {
        Void,
        Island,
        Settlement,
    }

    /// <summary>A point of the unit sphere, in the prototype's right-handed axes.</summary>
    public readonly struct Vec3
    {
        public readonly double X;
        public readonly double Y;
        public readonly double Z;

        public Vec3(double x, double y, double z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public static Vec3 operator +(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
        }

        public static Vec3 operator -(Vec3 a, Vec3 b)
        {
            return new Vec3(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
        }

        public static Vec3 operator *(Vec3 v, double k)
        {
            return new Vec3(v.X * k, v.Y * k, v.Z * k);
        }

        public static double Dot(Vec3 a, Vec3 b)
        {
            return a.X * b.X + a.Y * b.Y + a.Z * b.Z;
        }

        public static Vec3 Cross(Vec3 a, Vec3 b)
        {
            return new Vec3(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        public Vec3 Normalized()
        {
            double length = Hypot(X, Y, Z);
            if (length == 0)
            {
                length = 1;
            }

            return new Vec3(X / length, Y / length, Z / length);
        }

        /// <summary>
        /// <c>Math.hypot</c> as V8 computes it: scaled by the largest term and summed
        /// with compensation. A plain square root of the sum can differ in the last
        /// bit, and the land threshold is read from these coordinates.
        /// </summary>
        private static double Hypot(double x, double y, double z)
        {
            double ax = Math.Abs(x);
            double ay = Math.Abs(y);
            double az = Math.Abs(z);
            double max = Math.Max(ax, Math.Max(ay, az));
            if (max == 0)
            {
                return 0;
            }

            double sum = 0;
            double compensation = 0;
            Accumulate(ax / max, ref sum, ref compensation);
            Accumulate(ay / max, ref sum, ref compensation);
            Accumulate(az / max, ref sum, ref compensation);

            return Math.Sqrt(sum) * max;
        }

        private static void Accumulate(double n, ref double sum, ref double compensation)
        {
            double summand = n * n - compensation;
            double preliminary = sum + summand;
            compensation = (preliminary - sum) - summand;
            sum = preliminary;
        }
    }

    public sealed class WorldCell
    {
        /// <summary><c>c</c> and the index.</summary>
        public string Id;
        /// <summary>The position of the cell in <see cref="World.Cells"/>.</summary>
        public int Index;
        public Vec3 Center;
        /// <summary>The cell's outline on the unit sphere, wound counter-clockwise.</summary>
        public Vec3[] Polygon;
        /// <summary>The indexes of the cells that touch this one, ascending.</summary>
        public int[] Neighbors;
        public CellKind Kind;
        /// <summary>Which drawing of its kind the prototype's globe shows, and how it is turned.</summary>
        public int Variant;
        /// <summary>What the wild islands here are made of. Only island cells use it.</summary>
        public BiomeId Biome;
        /// <summary>Wild enemy islands waiting in an island cell. Zero everywhere else.</summary>
        public int IslandCount;
        /// <summary>True once the player has flown into this island cell: its fight is on.</summary>
        public bool Activated;
        public bool Revealed;
        /// <summary>The player whose island sits in this cell, if any.</summary>
        public string OwnerId;
        /// <summary>Toxicity left behind here. It never falls, as the spec insists.</summary>
        public int ToxicTrail;
        /// <summary>True once the clearing phase has emptied this cell of enemies.</summary>
        public bool Cleared;
        /// <summary>
        /// The boss's lair. There is one per world, far from every starting island.
        /// Its fight never clears for good: every player who flies in fights the
        /// boss on their own, until they have defeated it (<see cref="Player.BossSlain"/>).
        /// </summary>
        public bool Boss;
        /// <summary>
        /// The faction whose mobs hold the wild islands here. It stays after the
        /// cell is cleared. <c>null</c> for clouds, settlements and the players'
        /// home cells.
        /// </summary>
        public FactionId? Faction;
    }

    public sealed class World
    {
        /// <summary>The seed of the planet surface.</summary>
        public string Seed;
        public WorldCell[] Cells;

        /// <summary>Cell ids are <c>c&lt;index&gt;</c>, so a lookup is an array read, not a search.</summary>
        public WorldCell Get(string cellId)
        {
            if (string.IsNullOrEmpty(cellId) || cellId[0] != 'c' || !int.TryParse(cellId.Substring(1), out int index))
            {
                return null;
            }

            return index >= 0 && index < Cells.Length ? Cells[index] : null;
        }

        /// <summary>The boss's lair, or <c>null</c> for a world without one.</summary>
        public WorldCell BossCell
        {
            get { return Array.Find(Cells, cell => cell.Boss); }
        }
    }

    /// <summary>
    /// The global map (<c>core/world-gen.ts</c>). It is a Goldberg polyhedron:
    /// the dual of an icosahedron whose faces are split into
    /// <see cref="GridFrequency"/>² triangles. That gives 10·f² + 2 cells: 12
    /// pentagons and the rest hexagons. Cells carry unit-length coordinates; the
    /// globe view scales them.
    ///
    /// The order of the random draws is the prototype's. Do not reorder them: the
    /// nickname is the seed of the planet.
    /// </summary>
    public static class WorldGen
    {
        /// <summary>Frequency 7 yields 492 cells.</summary>
        public const int GridFrequency = 7;
        /// <summary>How many art variants of each cell kind the prototype's globe draws.</summary>
        public const int CellVariants = 4;

        /// <summary>Two triangle corners closer than this are the same vertex.</summary>
        private const double WeldEpsilon = 1e-6;
        /// <summary>Rivals start at least this many flights away from each other.</summary>
        private const int MinPlayerDistance = 6;
        /// <summary>
        /// The share of cells that hold land: islands and settlements. The rest is
        /// void, a sea of clouds. Land is clustered by noise into archipelagos.
        /// </summary>
        private const double LandShare = 0.34;
        /// <summary>The share of land cells that hold a neutral settlement instead of wild islands.</summary>
        private const double SettlementShare = 0.12;

        private static readonly int[][] IcosahedronFaces =
        {
            new[] { 0, 11, 5 }, new[] { 0, 5, 1 }, new[] { 0, 1, 7 }, new[] { 0, 7, 10 }, new[] { 0, 10, 11 },
            new[] { 1, 5, 9 }, new[] { 5, 11, 4 }, new[] { 11, 10, 2 }, new[] { 10, 7, 6 }, new[] { 7, 1, 8 },
            new[] { 3, 9, 4 }, new[] { 3, 4, 2 }, new[] { 3, 2, 6 }, new[] { 3, 6, 8 }, new[] { 3, 8, 9 },
            new[] { 4, 9, 5 }, new[] { 2, 4, 11 }, new[] { 6, 2, 10 }, new[] { 8, 6, 7 }, new[] { 9, 8, 1 },
        };

        private static readonly BiomeId[] ColdBiomes = { BiomeId.Taiga, BiomeId.Tundra, BiomeId.Mountains, BiomeId.Cliffs };
        private static readonly BiomeId[] MildWetBiomes = { BiomeId.Forrest, BiomeId.Grassland, BiomeId.Hills, BiomeId.Swamp };
        private static readonly BiomeId[] MildDryBiomes = { BiomeId.Plains, BiomeId.Grassland, BiomeId.Hills, BiomeId.Crater };
        private static readonly BiomeId[] HotWetBiomes = { BiomeId.Rainforest, BiomeId.Swamp, BiomeId.Savanna, BiomeId.Volcano };
        private static readonly BiomeId[] HotDryBiomes = { BiomeId.Desert, BiomeId.Savanna, BiomeId.Badlands, BiomeId.Volcano };

        /// <summary>
        /// Builds the planet and puts every player on a cell. The first id is the
        /// human: the ring around their cell starts revealed.
        /// </summary>
        public static World Create(string seed, IReadOnlyList<string> playerIds, out Dictionary<string, string> placement)
        {
            Rng rng = Rng.FromText(seed + ":world");
            WorldCell[] cells = BuildCells(rng);
            placement = PlacePlayers(cells, playerIds, rng);
            PlaceSettlements(cells, new HashSet<string>(placement.Values), rng);

            Dictionary<string, string> ownerByCell = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> entry in placement)
            {
                ownerByCell[entry.Value] = entry.Key;
            }

            string humanCellId = playerIds.Count > 0 && placement.TryGetValue(playerIds[0], out string placed) ? placed : cells[0].Id;
            WorldCell humanCell = cells[int.Parse(humanCellId.Substring(1))];
            HashSet<int> homeRing = new HashSet<int>(humanCell.Neighbors) { humanCell.Index };
            WorldCell bossCell = PickBossCell(cells, placement.Values.ToList());
            // Factions roll from their own stream, so adding them left the planet itself unchanged.
            Rng factionRng = Rng.FromText(seed + ":factions");

            foreach (WorldCell cell in cells)
            {
                // A player knows the cell they sit in and the ring around it. The
                // rest of the sphere waits for scouting.
                cell.Revealed = homeRing.Contains(cell.Index);

                // Every player starts on an island cell that is already theirs: it
                // is home, with no wild islands left to fight.
                if (ownerByCell.TryGetValue(cell.Id, out string ownerId))
                {
                    cell.Kind = CellKind.Island;
                    cell.OwnerId = ownerId;
                    cell.IslandCount = 0;
                    cell.Activated = true;
                    cell.Cleared = true;

                    continue;
                }

                // The lair holds one island: the boss's own. It is Nexus Arcology,
                // the one city of Helios.
                if (cell == bossCell)
                {
                    cell.Boss = true;
                    cell.IslandCount = 1;
                    cell.Biome = BiomeId.Volcano;
                    cell.Faction = FactionId.Helios;

                    continue;
                }

                // Every other cell is rolled, held or not, so one cell's kind does
                // not shift the rolls of the cells after it.
                double roll = factionRng.Next();
                if (cell.Kind == CellKind.Island && cell.IslandCount > 0)
                {
                    cell.Faction = Factions.Pick(cell.Biome, cell.IslandCount, roll);
                }
            }

            return new World { Seed = seed, Cells = cells };
        }

        /// <summary>Flight counts from one cell to every other one. An unreachable cell reads -1.</summary>
        public static int[] DistancesFrom(IReadOnlyList<WorldCell> cells, int startIndex)
        {
            int[] distances = new int[cells.Count];
            for (int index = 0; index < distances.Length; index += 1)
            {
                distances[index] = -1;
            }

            List<int> queue = new List<int> { startIndex };
            distances[startIndex] = 0;

            for (int head = 0; head < queue.Count; head += 1)
            {
                int current = queue[head];
                foreach (int next in cells[current].Neighbors)
                {
                    if (distances[next] == -1)
                    {
                        distances[next] = distances[current] + 1;
                        queue.Add(next);
                    }
                }
            }

            return distances;
        }

        /// <summary>The twelve icosahedron corners, already on the unit sphere.</summary>
        private static Vec3[] IcosahedronVertices()
        {
            double t = (1 + Math.Sqrt(5)) / 2;

            Vec3[] corners =
            {
                new Vec3(-1, t, 0), new Vec3(1, t, 0), new Vec3(-1, -t, 0), new Vec3(1, -t, 0),
                new Vec3(0, -1, t), new Vec3(0, 1, t), new Vec3(0, -1, -t), new Vec3(0, 1, -t),
                new Vec3(t, 0, -1), new Vec3(t, 0, 1), new Vec3(-t, 0, -1), new Vec3(-t, 0, 1),
            };

            return corners.Select(corner => corner.Normalized()).ToArray();
        }

        /// <summary>
        /// Splits every icosahedron face into f² triangles on the sphere. Points on a
        /// shared edge are computed once per face, so they are welded by distance.
        /// </summary>
        private static void GeodesicSphere(out List<Vec3> vertices, out List<int[]> triangles)
        {
            Vec3[] corners = IcosahedronVertices();
            List<Vec3> welded = new List<Vec3>();
            triangles = new List<int[]>();
            const int steps = GridFrequency;

            int Weld(Vec3 point)
            {
                for (int index = 0; index < welded.Count; index += 1)
                {
                    Vec3 other = welded[index];
                    if (Math.Abs(other.X - point.X) < WeldEpsilon
                        && Math.Abs(other.Y - point.Y) < WeldEpsilon
                        && Math.Abs(other.Z - point.Z) < WeldEpsilon)
                    {
                        return index;
                    }
                }

                welded.Add(point);

                return welded.Count - 1;
            }

            foreach (int[] face in IcosahedronFaces)
            {
                Vec3 a = corners[face[0]];
                Vec3 b = corners[face[1]];
                Vec3 c = corners[face[2]];

                List<int[]> grid = new List<int[]>();
                for (int row = 0; row <= steps; row += 1)
                {
                    int[] line = new int[steps - row + 1];
                    for (int column = 0; column <= steps - row; column += 1)
                    {
                        double weightA = (double)(steps - row - column) / steps;
                        double weightB = (double)column / steps;
                        double weightC = (double)row / steps;
                        line[column] = Weld((a * weightA + b * weightB + c * weightC).Normalized());
                    }

                    grid.Add(line);
                }

                for (int row = 0; row < steps; row += 1)
                {
                    int[] line = grid[row];
                    int[] nextLine = grid[row + 1];

                    for (int column = 0; column < line.Length - 1; column += 1)
                    {
                        triangles.Add(new[] { line[column], line[column + 1], nextLine[column] });

                        if (column + 1 < nextLine.Length)
                        {
                            triangles.Add(new[] { line[column + 1], nextLine[column + 1], nextLine[column] });
                        }
                    }
                }
            }

            vertices = welded;
        }

        /// <summary>Sorts a cell's corners around its centre, so the polygon does not self-cross.</summary>
        private static Vec3[] SortAroundCenter(Vec3 center, List<Vec3> corners)
        {
            Vec3 first = corners.Count > 0 ? corners[0] : new Vec3(1, 0, 0);
            Vec3 reference = (first - center * Vec3.Dot(first, center)).Normalized();
            Vec3 side = Vec3.Cross(center, reference);

            // OrderBy is stable, as the prototype's sort is.
            return corners.OrderBy(corner => Math.Atan2(Vec3.Dot(corner, side), Vec3.Dot(corner, reference))).ToArray();
        }

        /// <summary>A biome for wild islands: cold near the poles, hot near the equator.</summary>
        private static BiomeId IslandBiome(double latitude, double wetness, Rng rng)
        {
            if (latitude > 0.86)
            {
                return wetness > 0.5 ? BiomeId.Tundra : BiomeId.PolarDesert;
            }

            if (latitude > 0.62)
            {
                return rng.Pick(ColdBiomes);
            }

            if (latitude > 0.3)
            {
                return wetness > 0.5 ? rng.Pick(MildWetBiomes) : rng.Pick(MildDryBiomes);
            }

            return wetness > 0.5 ? rng.Pick(HotWetBiomes) : rng.Pick(HotDryBiomes);
        }

        /// <summary>One to three wild islands, mostly one or two. Both draws are always made.</summary>
        private static int RollIslandCount(Rng rng)
        {
            int second = rng.Next() < 0.45 ? 1 : 0;
            int third = rng.Next() < 0.15 ? 1 : 0;

            return 1 + second + third;
        }

        /// <summary>The dual: one cell per vertex of the geodesic sphere.</summary>
        private static WorldCell[] BuildCells(Rng rng)
        {
            GeodesicSphere(out List<Vec3> vertices, out List<int[]> triangles);
            PerlinNoise landNoise = new PerlinNoise(rng);
            PerlinNoise wetNoise = new PerlinNoise(rng);
            List<Vec3>[] corners = vertices.Select(_ => new List<Vec3>()).ToArray();
            SortedSet<int>[] neighbors = vertices.Select(_ => new SortedSet<int>()).ToArray();

            foreach (int[] triangle in triangles)
            {
                Vec3 centroid = (vertices[triangle[0]] + vertices[triangle[1]] + vertices[triangle[2]]).Normalized();

                foreach (int vertex in triangle)
                {
                    corners[vertex].Add(centroid);

                    foreach (int other in triangle)
                    {
                        if (other != vertex)
                        {
                            neighbors[vertex].Add(other);
                        }
                    }
                }
            }

            // Land is the top LandShare of a low-frequency noise, so it clusters.
            double[] landValue = new double[vertices.Count];
            for (int index = 0; index < vertices.Count; index += 1)
            {
                Vec3 v = vertices[index];
                double noise = landNoise.Fbm(v.X * 1.9, v.Y * 1.9, v.Z * 1.9, 4);
                landValue[index] = noise + (rng.Next() - 0.5) * 0.25;
            }

            double[] sorted = landValue.OrderByDescending(value => value).ToArray();
            int thresholdIndex = (int)Math.Floor(LandShare * sorted.Length);
            double threshold = thresholdIndex < sorted.Length ? sorted[thresholdIndex] : 0;

            WorldCell[] cells = new WorldCell[vertices.Count];
            for (int index = 0; index < vertices.Count; index += 1)
            {
                Vec3 center = vertices[index];
                bool isLand = landValue[index] > threshold;
                double wetness = 0.5 + wetNoise.Fbm(center.X * 2.3, center.Y * 2.3, center.Z * 2.3, 3) * 0.5;
                // The draws keep the prototype's order: biome, variant, island count.
                BiomeId biome = IslandBiome(Math.Abs(center.Y), wetness, rng);
                int variant = (int)Math.Floor(rng.Next() * CellVariants * 6);
                int islandCount = isLand ? RollIslandCount(rng) : 0;

                cells[index] = new WorldCell
                {
                    Id = "c" + index,
                    Index = index,
                    Center = center,
                    Polygon = SortAroundCenter(center, corners[index]),
                    Neighbors = neighbors[index].ToArray(),
                    Kind = isLand ? CellKind.Island : CellKind.Void,
                    Variant = variant,
                    Biome = biome,
                    IslandCount = islandCount,
                };
            }

            return cells;
        }

        /// <summary>Turns a share of the land into settlements, never two side by side.</summary>
        private static void PlaceSettlements(WorldCell[] cells, HashSet<string> reserved, Rng rng)
        {
            List<WorldCell> pool = cells.Where(cell => cell.Kind == CellKind.Island && !reserved.Contains(cell.Id)).ToList();
            int target = JsMath.Round(pool.Count * SettlementShare);
            HashSet<int> chosen = new HashSet<int>();

            while (chosen.Count < target && pool.Count > 0)
            {
                int at = rng.NextIndex(pool.Count);
                WorldCell cell = pool[at];
                pool.RemoveAt(at);

                if (!cell.Neighbors.Any(chosen.Contains))
                {
                    chosen.Add(cell.Index);
                }
            }

            foreach (int index in chosen)
            {
                cells[index].Kind = CellKind.Settlement;
                cells[index].IslandCount = 0;
            }
        }

        /// <summary>Puts the players on cells far enough apart to be worth flying between.</summary>
        private static Dictionary<string, string> PlacePlayers(WorldCell[] cells, IReadOnlyList<string> playerIds, Rng rng)
        {
            Dictionary<string, string> placed = new Dictionary<string, string>();
            List<int[]> taken = new List<int[]>();

            foreach (string playerId in playerIds)
            {
                WorldCell cell = null;

                // The spacing relaxes if a crowded sphere cannot honour it.
                for (int spacing = MinPlayerDistance; spacing >= 1 && cell == null; spacing -= 1)
                {
                    int required = spacing;
                    List<WorldCell> free = cells
                        .Where(candidate => candidate.Kind == CellKind.Island && taken.All(distances => distances[candidate.Index] >= required))
                        .ToList();

                    if (free.Count > 0)
                    {
                        cell = rng.Pick(free);
                    }
                }

                WorldCell chosen = cell ?? rng.Pick(cells);
                taken.Add(DistancesFrom(cells, chosen.Index));
                placed[playerId] = chosen.Id;
            }

            return placed;
        }

        /// <summary>
        /// The boss's lair: the island cell farthest from the nearest starting island.
        /// A tie is broken by the cell index, so the same seed always picks the same
        /// cell. Returns <c>null</c> only for a world with no free island cell.
        /// </summary>
        private static WorldCell PickBossCell(WorldCell[] cells, List<string> startCellIds)
        {
            List<int[]> distances = cells
                .Where(cell => startCellIds.Contains(cell.Id))
                .Select(cell => DistancesFrom(cells, cell.Index))
                .ToList();
            WorldCell best = null;
            int bestDistance = -1;

            foreach (WorldCell cell in cells)
            {
                if (cell.Kind != CellKind.Island || startCellIds.Contains(cell.Id))
                {
                    continue;
                }

                int nearest = int.MaxValue;
                foreach (int[] list in distances)
                {
                    nearest = Math.Min(nearest, list[cell.Index]);
                }

                if (nearest > bestDistance)
                {
                    best = cell;
                    bestDistance = nearest;
                }
            }

            return best;
        }
    }
}
