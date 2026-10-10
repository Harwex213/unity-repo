using System;
using System.Collections.Generic;

namespace Hexwex.Core
{
    public enum CellVisibility
    {
        Revealed,
        Frontier,
        Fogged,
    }

    /// <summary>The answer to "may the player scout this cell", with the numbers the panel shows.</summary>
    public readonly struct ScoutCheck
    {
        /// <summary>Why the action is refused, or <c>null</c> when it is allowed.</summary>
        public readonly string Refusal;
        public readonly int Cost;
        /// <summary>Flights from the player's cell, or -1 when unreachable.</summary>
        public readonly int Distance;

        public ScoutCheck(string refusal, int cost, int distance)
        {
            Refusal = refusal;
            Cost = cost;
            Distance = distance;
        }

        public bool Ok
        {
            get { return Refusal == null; }
        }
    }

    /// <summary>
    /// The rules of the scouting phase (<c>core/world-rules.ts</c>), kept pure so
    /// the session, the cell panel and the globe all read the same answer.
    ///
    /// - Fog. A cell is revealed once scouted or visited. An unrevealed cell that
    ///   touches a revealed one is the frontier: the player can scout it. Every
    ///   other cell is fogged: the player sees the land and sea from orbit, but
    ///   nothing on them.
    /// - Scouting. One action reveals one frontier cell. It costs
    ///   <c>ceil(distance / 2)</c> scouting, at least 1, where the distance counts
    ///   flights from the player's cell.
    /// - Moving. One flight per turn, to a neighbouring cell with no other player
    ///   in it, whatever its kind. The flight reveals the cell it lands in.
    ///   Landing in an island cell activates it: its wild islands become the
    ///   cleanup fight at the end of the turn, and every turn after, until they
    ///   are all cleared.
    /// - Void and settlement cells hold no fight. A settlement has no effect yet.
    /// </summary>
    public static class WorldRules
    {
        public static CellVisibility Visibility(World world, WorldCell cell)
        {
            if (cell.Revealed)
            {
                return CellVisibility.Revealed;
            }

            foreach (int neighbor in cell.Neighbors)
            {
                if (world.Cells[neighbor].Revealed)
                {
                    return CellVisibility.Frontier;
                }
            }

            return CellVisibility.Fogged;
        }

        public static int ScoutCost(int distance)
        {
            return Math.Max(1, (int)Math.Ceiling(distance / 2.0));
        }

        public static ScoutCheck CheckScout(World world, string fromCellId, string cellId, int scouting)
        {
            WorldCell from = world.Get(fromCellId);
            WorldCell cell = world.Get(cellId);
            if (from == null || cell == null)
            {
                return new ScoutCheck("Гекс не найден", 0, -1);
            }

            int distance = WorldGen.DistancesFrom(world.Cells, from.Index)[cell.Index];
            int cost = ScoutCost(distance);
            CellVisibility visibility = Visibility(world, cell);

            if (visibility == CellVisibility.Revealed)
            {
                return new ScoutCheck("Гекс уже разведан", cost, distance);
            }

            if (visibility == CellVisibility.Fogged)
            {
                return new ScoutCheck("Слишком далеко: сначала разведайте соседний гекс", cost, distance);
            }

            if (scouting < cost)
            {
                return new ScoutCheck("Не хватает разведки: нужно " + cost, cost, distance);
            }

            return new ScoutCheck(null, cost, distance);
        }

        /// <summary>Why the island cannot fly to the cell, or <c>null</c> when it can.</summary>
        public static string MoveRefusal(World world, string fromCellId, string cellId, bool movedThisTurn)
        {
            WorldCell from = world.Get(fromCellId);
            WorldCell cell = world.Get(cellId);
            if (from == null || cell == null)
            {
                return "Гекс не найден";
            }

            if (cell == from)
            {
                return "Остров уже здесь";
            }

            if (movedThisTurn)
            {
                return "Остров уже перелетал в этом ходу";
            }

            if (Array.IndexOf(from.Neighbors, cell.Index) < 0)
            {
                return "Перелететь можно только в соседний гекс";
            }

            if (cell.OwnerId != null)
            {
                return "Гекс занят другим островом";
            }

            return null;
        }

        /// <summary>
        /// The wild islands the cleanup phase fights in this cell. The boss's lair
        /// stays a fight for every player who has not defeated the boss yet.
        /// </summary>
        public static int PendingIslands(WorldCell cell, Player player)
        {
            if (cell == null || cell.Kind != CellKind.Island || !cell.Activated || cell.Cleared)
            {
                return 0;
            }

            if (cell.Boss && player != null && player.BossSlain)
            {
                return 0;
            }

            return cell.IslandCount;
        }

        /// <summary>The free neighbours the island can fly to this turn.</summary>
        public static List<string> ReachableCellIds(World world, string fromCellId, bool movedThisTurn)
        {
            List<string> reachable = new List<string>();
            WorldCell from = world.Get(fromCellId);
            if (from == null || movedThisTurn)
            {
                return reachable;
            }

            foreach (int neighbor in from.Neighbors)
            {
                if (world.Cells[neighbor].OwnerId == null)
                {
                    reachable.Add(world.Cells[neighbor].Id);
                }
            }

            return reachable;
        }
    }
}
