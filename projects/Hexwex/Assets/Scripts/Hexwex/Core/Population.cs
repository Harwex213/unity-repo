using System;

namespace Hexwex.Core
{
    /// <summary>How the population of a player splits up. See <see cref="Population"/>.</summary>
    public readonly struct PopulationSplit
    {
        public readonly int Total;
        public readonly int Army;
        /// <summary>People in buildings.</summary>
        public readonly int Workers;
        public readonly int Idle;

        public PopulationSplit(int total, int army, int workers, int idle)
        {
            Total = total;
            Army = army;
            Workers = workers;
            Idle = idle;
        }
    }

    /// <summary>
    /// The population model, as the prototype's <c>core/population.ts</c> has it.
    /// Every person of the population resource is in exactly one of three states:
    ///
    /// - in the army: <see cref="Player.Army"/> people. They fight in the clear phase.
    /// - in a building: <see cref="HexTile.Workers"/> people on a built hex. A
    ///   building works only when it has all the workers it needs. A building
    ///   without them rolls no die in the tax phase: it pays nothing and leaves no
    ///   toxicity.
    /// - idle: everyone else. Idle people are never stored; they are the remainder.
    ///
    /// So <c>total = idle + army + workers</c>. When the population drops below
    /// <c>army + workers</c> (deaths, madness), the buildings lose workers first,
    /// then the army loses people. The stronghold needs no workers.
    /// </summary>
    public static class Population
    {
        /// <summary>People one building needs to work.</summary>
        public const int WorkersPerBuilding = 1;

        /// <summary>People the hex needs: a building needs workers, an empty hex does not.</summary>
        public static int WorkersNeeded(HexTile hex)
        {
            return hex.Building.HasValue ? WorkersPerBuilding : 0;
        }

        /// <summary>People who really work on the hex. Workers of a removed building count as 0.</summary>
        public static int WorkersOn(HexTile hex)
        {
            return Math.Min(WorkersNeeded(hex), Math.Max(0, hex.Workers));
        }

        /// <summary>A building works when all its workers are in place. An empty hex counts as staffed.</summary>
        public static bool IsStaffed(HexTile hex)
        {
            return WorkersOn(hex) >= WorkersNeeded(hex);
        }

        public static PopulationSplit Of(Player player)
        {
            int workers = 0;
            foreach (HexTile hex in player.Hexes)
            {
                workers += WorkersOn(hex);
            }

            int total = player.Resources[ResourceId.Population];

            return new PopulationSplit(total, player.Army, workers, Math.Max(0, total - player.Army - workers));
        }

        /// <summary>
        /// Restores the rule <c>army + workers &lt;= total</c>. A hex without a building
        /// loses its stale workers. On a shortage the buildings release people from
        /// the last hex up, then the army does.
        /// </summary>
        public static void Settle(Player player)
        {
            int total = Math.Max(0, player.Resources[ResourceId.Population]);
            player.Resources[ResourceId.Population] = total;
            player.Army = Math.Max(0, Math.Min(player.Army, total));

            int workers = 0;
            foreach (HexTile hex in player.Hexes)
            {
                hex.Workers = WorkersOn(hex);
                workers += hex.Workers;
            }

            int excess = player.Army + workers - total;
            for (int index = player.Hexes.Count - 1; index >= 0 && excess > 0; index -= 1)
            {
                HexTile hex = player.Hexes[index];
                int released = Math.Min(excess, hex.Workers);
                hex.Workers -= released;
                excess -= released;
            }

            if (excess > 0)
            {
                player.Army -= excess;
            }
        }

        /// <summary><paramref name="count"/> people die. Idle people die first, then workers, then soldiers.</summary>
        public static void LosePeople(Player player, int count)
        {
            int lost = Math.Min(Math.Max(0, count), player.Resources[ResourceId.Population]);
            player.Resources[ResourceId.Population] -= lost;
            Settle(player);
        }

        /// <summary><paramref name="count"/> soldiers die. They leave the army and the population together.</summary>
        public static void LoseSoldiers(Player player, int count)
        {
            int lost = Math.Min(Math.Max(0, count), player.Army);
            player.Army -= lost;
            player.Resources[ResourceId.Population] -= lost;
            Settle(player);
        }

        /// <summary>Why the hex cannot get <paramref name="count"/> workers, or <c>null</c> when it can. Only idle people can be assigned.</summary>
        public static string StaffRefusal(Player player, string hexId, int count)
        {
            HexTile hex = player.FindHex(hexId);
            if (hex == null || !hex.Building.HasValue)
            {
                return "Здесь нет здания";
            }

            if (count < 0 || count > WorkersNeeded(hex))
            {
                return "Зданию нужно рабочих: " + WorkersNeeded(hex);
            }

            if (count - WorkersOn(hex) > Of(player).Idle)
            {
                return "Нет свободных жителей";
            }

            return null;
        }

        /// <summary>Gives the hex <paramref name="count"/> workers. The caller checks <see cref="StaffRefusal"/> first.</summary>
        public static void SetStaff(Player player, string hexId, int count)
        {
            HexTile hex = player.FindHex(hexId);
            if (hex != null)
            {
                hex.Workers = count;
            }

            Settle(player);
        }

        /// <summary>Idle people fill the buildings that lack workers, in hex order.</summary>
        public static void AutoStaff(Player player)
        {
            int idle = Of(player).Idle;
            if (idle <= 0)
            {
                return;
            }

            foreach (HexTile hex in player.Hexes)
            {
                int added = Math.Min(WorkersNeeded(hex) - WorkersOn(hex), idle);
                if (added <= 0)
                {
                    continue;
                }

                idle -= added;
                hex.Workers = WorkersOn(hex) + added;
            }

            Settle(player);
        }

        /// <summary>Makes the army <paramref name="size"/> people. It takes only idle people and never drops below 0.</summary>
        public static void SetArmySize(Player player, int size)
        {
            PopulationSplit split = Of(player);
            player.Army = Math.Max(0, Math.Min(size, split.Army + split.Idle));
        }
    }
}
