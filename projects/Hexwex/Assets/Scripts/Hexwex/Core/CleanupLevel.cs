using System;
using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    public enum CleanupSide
    {
        Player,
        Enemy,
    }

    /// <summary>An enemy island either drifts in place or sails at the player.</summary>
    public enum IslandBehavior
    {
        Player,
        Drift,
        Approach,
    }

    /// <summary>One hex of a level island, in the island's own axial coordinates.</summary>
    public sealed class CleanupHex
    {
        public string Id;
        public int Q;
        public int R;
        public BiomeId Biome;
        public BuildingId? Building;
        public bool Stronghold;
        public int Toxicity;
        public bool Dead;
        /// <summary>Hit points of the building or stronghold on the hex. 0 and 0 for an empty hex.</summary>
        public int Hp;
        public int MaxHp;

        public CleanupHex Clone()
        {
            return (CleanupHex)MemberwiseClone();
        }
    }

    public sealed class IslandSpec
    {
        public string Id;
        public string Label;
        public CleanupSide Side;
        public IslandBehavior Behavior;
        public List<CleanupHex> Hexes;
        /// <summary>World position of the island's axial origin.</summary>
        public double X;
        public double Y;
        /// <summary>Monsters that start on the island. Empty for the player's island.</summary>
        public List<Enemy> Garrison = new List<Enemy>();
    }

    /// <summary>The map border: a rectangle centred on the world origin.</summary>
    public readonly struct LevelBounds
    {
        public readonly double HalfWidth;
        public readonly double HalfHeight;

        public LevelBounds(double halfWidth, double halfHeight)
        {
            HalfWidth = halfWidth;
            HalfHeight = halfHeight;
        }
    }

    public sealed class LevelSpec
    {
        public int Tier;
        public LevelBounds Bounds;
        /// <summary>Multiplier on monster hp and damage.</summary>
        public double Growth;
        public List<IslandSpec> Islands;
        public List<Unit> Roster;
        /// <summary>The player's mana when the battle begins. The skills spend it.</summary>
        public int Mana;
    }

    public sealed class LevelSetup
    {
        public Player Player;
        public int Turn;
        /// <summary>Enemy islands waiting in the cell. Zero for a cleared cell.</summary>
        public int IslandCount;
        public BiomeId CellBiome;
        public int ToxicTrail;
        public List<Unit> Roster;
        /// <summary>The cell is the boss's lair: the level is its one big island.</summary>
        public bool Boss;
    }

    /// <summary>
    /// A hex that joined the player's island in battle, in the player's own axial
    /// coordinates: the island keeps exactly the shape it had at the end of the battle.
    /// </summary>
    public sealed class AnnexedHex
    {
        public int Q;
        public int R;
        public BiomeId Biome;
        public int Toxicity;
    }

    /// <summary>Centre of an island's hexes and the distance from it to its farthest corner.</summary>
    public readonly struct IslandExtent
    {
        public readonly double Cx;
        public readonly double Cy;
        public readonly double Radius;

        public IslandExtent(double cx, double cy, double radius)
        {
            Cx = cx;
            Cy = cy;
            Radius = radius;
        }
    }

    /// <summary>
    /// The level of the cleanup phase (<c>core/cleanup-level.ts</c>): the player's
    /// own island and a few enemy hex islands around it. This file only lays the
    /// level out; <see cref="CleanupSim"/> runs it.
    ///
    /// The raid tier sets how hard the level is. It grows every two turns, and a
    /// cell with a heavy toxic trail adds to it: the poison draws monsters.
    /// </summary>
    public static class CleanupLevel
    {
        public const string BossLabel = "Логово Повелителя Мора";

        private const int MaxTier = 6;
        private const int TurnsPerTier = 2;
        /// <summary>Toxic trail worth one extra tier.</summary>
        private const int TrailPerTier = 200;
        /// <summary>Toxic trail at which the hexes of an enemy island reach full pollution.</summary>
        private const double TrailFull = 400;
        private const double MaxTrailToxicityPct = 60;
        private const int MaxEnemyHexes = 19;
        private const int MaxGarrison = 10;
        /// <summary>Monster hp and damage grow this much per turn, so late raids hit harder.</summary>
        private const double EnemyGrowthPerTurn = 0.12;
        /// <summary>How strongly an enemy island takes after the biome of its world cell.</summary>
        private const double CellBiomeWeight = 10;
        /// <summary>How much a placed neighbour pulls the next hex to the same biome.</summary>
        private const double NeighborBias = 2.6;
        /// <summary>Open water between the player's island and the nearest enemy island.</summary>
        private const double SpawnGapMin = 300;
        private const double SpawnGapSpread = 260;
        private const double SpawnGapBetween = 160;
        private const double ApproachChancePerTier = 0.2;
        /// <summary>Open sky between the outermost island and the map border.</summary>
        private const double BoundsMargin = 760;
        private const double MinHalfWidth = 1900;
        private const double MinHalfHeight = 1400;
        private const double MaxApproachChance = 0.8;
        private const int BossGarrison = 8;
        /// <summary>
        /// The lair does not grow with the turns, unlike a raid: the boss is a fixed
        /// goal. A raid reaches this strength around turn 5.
        /// </summary>
        private const double BossGrowth = 1.5;

        private static readonly string[] BossGuard = { "ogre", "vampire", "witch", "zombie", "skeleton" };
        private static readonly string[] LowPool = { "wolf", "spider", "leech", "bat" };
        private static readonly string[] MidPool = { "wolf", "spider", "skeleton", "zombie", "bat", "moth" };
        private static readonly string[] HighPool = { "skeleton", "zombie", "ogre", "witch", "vampire", "moth", "bat" };

        public static int RaidTier(int turn, int toxicTrail)
        {
            int byTurn = (turn - 1) / TurnsPerTier;
            int byTrail = toxicTrail / TrailPerTier;

            return Math.Min(MaxTier, Math.Max(1, 1 + byTurn + byTrail));
        }

        public static IslandExtent Extent(IReadOnlyList<CleanupHex> hexes)
        {
            double sumX = 0;
            double sumY = 0;
            double[] xs = new double[hexes.Count];
            double[] ys = new double[hexes.Count];
            for (int index = 0; index < hexes.Count; index += 1)
            {
                HexMath.HexToPixel(hexes[index].Q, hexes[index].R, HexMath.HexSize, out xs[index], out ys[index]);
                sumX += xs[index];
                sumY += ys[index];
            }

            double cx = sumX / Math.Max(1, hexes.Count);
            double cy = sumY / Math.Max(1, hexes.Count);
            double radius = 0;
            for (int index = 0; index < hexes.Count; index += 1)
            {
                radius = Math.Max(radius, CleanupMath.Hypot(xs[index] - cx, ys[index] - cy));
            }

            return new IslandExtent(cx, cy, radius + HexMath.HexSize);
        }

        /// <summary>
        /// Lays the level out. The player's island sits at the world origin. Enemy
        /// islands stand on a ring around it, spread by angle, so none overlaps another.
        /// </summary>
        public static LevelSpec Create(LevelSetup setup, Rng rng)
        {
            bool isBoss = setup.Boss;
            int tier = isBoss ? MaxTier : RaidTier(setup.Turn, setup.ToxicTrail);
            double growth = isBoss ? BossGrowth : 1 + (setup.Turn - 1) * EnemyGrowthPerTurn;
            string[] pool = isBoss ? BossGuard : tier <= 1 ? LowPool : tier <= 3 ? MidPool : HighPool;
            List<CleanupHex> ownHexes = PlayerHexes(setup.Player);
            IslandExtent own = Extent(ownHexes);
            List<IslandSpec> islands = new List<IslandSpec>
            {
                new IslandSpec
                {
                    Id = "player",
                    Label = setup.Player.Nickname,
                    Side = CleanupSide.Player,
                    Behavior = IslandBehavior.Player,
                    Hexes = ownHexes,
                    X = -own.Cx,
                    Y = -own.Cy,
                },
            };

            List<double[]> rings = new List<double[]> { new[] { 0.0, 0.0, own.Radius } };
            double startAngle = rng.Next() * Math.PI * 2;
            int count = isBoss ? 1 : Math.Max(0, setup.IslandCount);

            for (int index = 0; index < count; index += 1)
            {
                int size = isBoss ? MaxEnemyHexes : Math.Min(MaxEnemyHexes, 5 + tier * 2 + rng.RandomInt(0, 3));
                List<CleanupHex> hexes = GrowEnemyHexes(rng, size, setup.CellBiome, setup.ToxicTrail);
                IslandExtent extent = Extent(hexes);
                int garrisonSize = isBoss ? BossGarrison : Math.Min(MaxGarrison, 2 + tier + rng.RandomInt(0, 1));
                List<Enemy> garrison = new List<Enemy>();
                // The boss stands first, so it takes the island's first node.
                if (isBoss)
                {
                    garrison.Add(Units.GetEnemy("plague_lord"));
                }

                for (int guard = 0; guard < garrisonSize; guard += 1)
                {
                    garrison.Add(Units.GetEnemy(rng.Pick(pool)));
                }

                double angle = startAngle + (double)index / count * Math.PI * 2 + (rng.Next() - 0.5) * 0.5;
                double distance = own.Radius + extent.Radius + SpawnGapMin + rng.Next() * SpawnGapSpread;
                double x = Math.Cos(angle) * distance;
                double y = Math.Sin(angle) * distance;

                // Pushes the island outwards until it clears every island placed before it.
                while (rings.Any(ring => CleanupMath.Hypot(ring[0] - x, ring[1] - y) < ring[2] + extent.Radius + SpawnGapBetween))
                {
                    distance += HexMath.HexSize;
                    x = Math.Cos(angle) * distance;
                    y = Math.Sin(angle) * distance;
                }

                rings.Add(new[] { x, y, extent.Radius });

                double approachChance = Math.Min(MaxApproachChance, ApproachChancePerTier * tier);

                islands.Add(new IslandSpec
                {
                    Id = "enemy-" + index,
                    Label = isBoss ? BossLabel : "Остров " + (index + 1),
                    Side = CleanupSide.Enemy,
                    // The lair does not sail: the player has to come to it.
                    Behavior = !isBoss && rng.Next() < approachChance ? IslandBehavior.Approach : IslandBehavior.Drift,
                    Hexes = hexes,
                    X = x - extent.Cx,
                    Y = y - extent.Cy,
                    Garrison = garrison,
                });
            }

            // The border leaves the same open margin beyond the outermost island on
            // every side, so no island starts inside the plume band.
            double reachX = rings.Max(ring => Math.Abs(ring[0]) + ring[2]);
            double reachY = rings.Max(ring => Math.Abs(ring[1]) + ring[2]);

            return new LevelSpec
            {
                Tier = tier,
                Growth = growth,
                Bounds = new LevelBounds(Math.Max(MinHalfWidth, reachX + BoundsMargin), Math.Max(MinHalfHeight, reachY + BoundsMargin)),
                Islands = islands,
                Roster = setup.Roster,
                Mana = setup.Player.Resources[ResourceId.Mana],
            };
        }

        /// <summary>
        /// Adds the hexes that joined in battle at exactly their battle coordinates.
        /// A hex id that is somehow already taken is skipped, never moved.
        /// </summary>
        public static void JoinAnnexed(Player player, IEnumerable<AnnexedHex> gains)
        {
            HashSet<string> taken = new HashSet<string>(player.Hexes.Select(hex => hex.Id));

            foreach (AnnexedHex gain in gains)
            {
                string id = HexMath.HexId(gain.Q, gain.R);
                if (taken.Add(id))
                {
                    player.Hexes.Add(new HexTile { Id = id, Q = gain.Q, R = gain.R, Biome = gain.Biome, Toxicity = gain.Toxicity });
                }
            }
        }

        private static List<CleanupHex> PlayerHexes(Player player)
        {
            List<CleanupHex> hexes = new List<CleanupHex>();
            foreach (HexTile hex in player.Hexes)
            {
                bool hasStructure = StructureHp.TryGet(player, hex, out _, out int hp, out int max);

                hexes.Add(new CleanupHex
                {
                    Id = hex.Id,
                    Q = hex.Q,
                    R = hex.R,
                    Biome = hex.Biome,
                    Building = hex.Building,
                    Stronghold = hex.Id == player.StrongholdHexId,
                    Toxicity = hex.Toxicity,
                    Dead = Tax.IsDead(hex),
                    Hp = hasStructure ? hp : 0,
                    MaxHp = hasStructure ? max : 0,
                });
            }

            return hexes;
        }

        /// <summary>
        /// Grows an enemy island as a compact blob. A free cell with more placed
        /// neighbours is more likely to be taken, so the island has few thin arms.
        /// </summary>
        private static List<CleanupHex> GrowEnemyHexes(Rng rng, int size, BiomeId cellBiome, int trail)
        {
            List<CleanupHex> placed = new List<CleanupHex>();
            Dictionary<Axial, CleanupHex> byCell = new Dictionary<Axial, CleanupHex>();
            double trailToxicity = Math.Min(trail, TrailFull) / TrailFull * MaxTrailToxicityPct;

            void Place(int q, int r)
            {
                int[] same = new int[Biomes.All.Count];
                foreach (Axial cell in HexMath.Neighbors(q, r))
                {
                    if (byCell.TryGetValue(cell, out CleanupHex neighbor))
                    {
                        same[(int)neighbor.Biome] += 1;
                    }
                }

                // The draws keep the prototype's order: toxicity, then the biome.
                int toxicity = JsMath.Round(Math.Min(90, Math.Max(0, trailToxicity + rng.Next() * 18)));
                BiomeId biome = rng.PickWeighted(Biomes.All, candidate =>
                {
                    double weight = candidate.Id == cellBiome ? CellBiomeWeight : 1;

                    return weight * (1 + NeighborBias * same[(int)candidate.Id]);
                }).Id;

                CleanupHex hex = new CleanupHex
                {
                    Id = HexMath.HexId(q, r),
                    Q = q,
                    R = r,
                    Biome = biome,
                    Toxicity = toxicity,
                    Dead = Tax.IsDead(new HexTile { Toxicity = toxicity }),
                };
                placed.Add(hex);
                byCell[new Axial(q, r)] = hex;
            }

            Place(0, 0);

            while (placed.Count < size)
            {
                // The frontier keeps the order in which its cells were first seen.
                List<Axial> frontier = new List<Axial>();
                Dictionary<Axial, int> weights = new Dictionary<Axial, int>();

                foreach (CleanupHex hex in placed)
                {
                    foreach (Axial cell in HexMath.Neighbors(hex.Q, hex.R))
                    {
                        if (byCell.ContainsKey(cell))
                        {
                            continue;
                        }

                        if (!weights.ContainsKey(cell))
                        {
                            frontier.Add(cell);
                            weights[cell] = 0;
                        }

                        weights[cell] += 1;
                    }
                }

                Axial next = rng.PickWeighted(frontier, cell => (double)weights[cell] * weights[cell]);
                Place(next.Q, next.R);
            }

            return placed;
        }
    }

    public static class CleanupMath
    {
        public static double Hypot(double x, double y)
        {
            return Math.Sqrt(x * x + y * y);
        }
    }
}
