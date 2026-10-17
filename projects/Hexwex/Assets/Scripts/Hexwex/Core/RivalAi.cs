using System;
using System.Linq;

namespace Hexwex.Core
{
    /// <summary>
    /// The rivals' minimal plan for the endgame (<c>core/rival-ai.ts</c>). In the
    /// boss's lair a rival's fight is resolved by a seeded roll that its army
    /// improves. With the trophy in hand it builds the central converter as soon
    /// as it can pay for it. From <see cref="HuntStartTurn"/> on it flies one cell per
    /// turn toward the lair: it senses the lair, so it needs no scouting.
    /// </summary>
    public static class RivalAi
    {
        /// <summary>The rivals build up first and set out for the lair on this turn.</summary>
        public const int HuntStartTurn = 4;

        private const double BossWinBaseChance = 0.3;
        private const double BossWinChancePerArmy = 0.025;
        private const double BossWinMaxChance = 0.85;
        /// <summary>The army a rival loses in a lost fight with the boss.</summary>
        private const int BossLossArmy = 2;

        /// <summary>
        /// The neighbour of <paramref name="fromId"/> that lies on a shortest flight
        /// to <paramref name="targetId"/>. Cells with another island in them are
        /// skipped. Returns <c>null</c> when the island is already there or every
        /// way is blocked.
        /// </summary>
        public static string StepToward(World world, string fromId, string targetId)
        {
            WorldCell from = world.Get(fromId);
            WorldCell target = world.Get(targetId);
            if (from == null || target == null || from == target)
            {
                return null;
            }

            int[] distances = WorldGen.DistancesFrom(world.Cells, target.Index);
            string best = null;
            int bestDistance = int.MaxValue;

            foreach (int neighborIndex in from.Neighbors)
            {
                WorldCell neighbor = world.Cells[neighborIndex];
                if (neighbor.OwnerId != null)
                {
                    continue;
                }

                int distance = distances[neighbor.Index];
                if (distance >= 0 && distance < bestDistance)
                {
                    best = neighbor.Id;
                    bestDistance = distance;
                }
            }

            return best;
        }

        /// <summary>The chance that a rival with this army defeats the boss in one fight.</summary>
        public static double BossWinChance(int army)
        {
            return Math.Min(BossWinMaxChance, BossWinBaseChance + army * BossWinChancePerArmy);
        }

        /// <summary>One seeded fight of a rival against the boss.</summary>
        public static void FightBoss(Player player, Rng rng)
        {
            if (rng.Next() < BossWinChance(player.Army))
            {
                player.BossSlain = true;
                player.Techs += 1;

                return;
            }

            player.Army = Math.Max(0, player.Army - BossLossArmy);
        }

        /// <summary>
        /// Where a rival puts its converter: the cleanest free hex. An island with no
        /// free hex gives up its weakest building. The stronghold is never touched.
        /// </summary>
        public static string PickConverterHex(Player player)
        {
            HexTile[] hexes = player.Hexes.Where(hex => hex.Id != player.StrongholdHexId).ToArray();

            HexTile free = hexes
                .Where(hex => !hex.Building.HasValue)
                .OrderBy(hex => hex.Toxicity)
                .ThenBy(hex => hex.Id, StringComparer.Ordinal)
                .FirstOrDefault();
            if (free != null)
            {
                return free.Id;
            }

            HexTile weakest = hexes
                .OrderBy(hex => hex.Building.HasValue ? Buildings.AverageYieldOn(Buildings.Get(hex.Building.Value), hex.Biome) : 0)
                .ThenBy(hex => hex.Id, StringComparer.Ordinal)
                .FirstOrDefault();

            return weakest?.Id;
        }
    }
}
