using System.Collections.Generic;
using System.Linq;

namespace Hexwex.Core
{
    public enum GameOutcomeKind
    {
        Victory,
        Defeat,
        Shared,
    }

    public enum GameOutcomeReason
    {
        Converter,
        SharedConverter,
        Elimination,
        Ruined,
        NoBuildings,
        RivalConverter,
    }

    public sealed class GameOutcome
    {
        public GameOutcomeKind Kind;
        public GameOutcomeReason Reason;
        /// <summary>The players who won, the human included when the human won.</summary>
        public List<string> WinnerIds;
        public int Turn;
    }

    /// <summary>
    /// How a session ends (<c>core/game-over.ts</c>).
    ///
    /// - Victory. A player who has built the central converter has beaten the
    ///   poison and wins. Players who build it in the same turn win together.
    /// - Defeat. A player is defeated when their stronghold lies in ruins, or when
    ///   they have lost every building they ever had.
    /// - Precedence. A player who has built the converter wins, even if the same
    ///   turn has ruined them. A rival's victory ends the game in the human's
    ///   defeat. A defeated rival is eliminated and the game goes on; once every
    ///   rival is eliminated, the human wins by elimination.
    /// </summary>
    public static class GameOver
    {
        public static int BuildingCount(Player player)
        {
            return player.Hexes.Count(hex => hex.Building.HasValue);
        }

        /// <summary>Why the player is defeated, or <c>null</c> while the player still stands.</summary>
        public static GameOutcomeReason? DefeatReason(Player player)
        {
            HexTile strongholdHex = player.FindHex(player.StrongholdHexId);
            if (strongholdHex != null && StructureHp.IsRuinedStronghold(player, strongholdHex))
            {
                return GameOutcomeReason.Ruined;
            }

            if (player.HasHadBuildings && BuildingCount(player) == 0)
            {
                return GameOutcomeReason.NoBuildings;
            }

            return null;
        }

        /// <summary>
        /// Brings <c>HasHadBuildings</c> and <c>Eliminated</c> up to date, and
        /// returns the outcome, or <c>null</c> while the game goes on.
        /// </summary>
        public static GameOutcome Check(IReadOnlyList<Player> players, string humanId, int turn)
        {
            // A building seen once makes its later loss count.
            foreach (Player player in players)
            {
                if (BuildingCount(player) > 0)
                {
                    player.HasHadBuildings = true;
                }
            }

            List<Player> active = players.Where(player => !player.Eliminated).ToList();
            List<string> winnerIds = active.Where(Buildings.HasConverter).Select(player => player.Id).ToList();
            Player human = active.Find(player => player.Id == humanId);

            if (human != null && winnerIds.Contains(humanId))
            {
                bool isShared = winnerIds.Count > 1;

                return new GameOutcome
                {
                    Kind = isShared ? GameOutcomeKind.Shared : GameOutcomeKind.Victory,
                    Reason = isShared ? GameOutcomeReason.SharedConverter : GameOutcomeReason.Converter,
                    WinnerIds = winnerIds,
                    Turn = turn,
                };
            }

            GameOutcomeReason? humanDefeat = human != null ? DefeatReason(human) : null;
            if (humanDefeat.HasValue)
            {
                return new GameOutcome { Kind = GameOutcomeKind.Defeat, Reason = humanDefeat.Value, WinnerIds = winnerIds, Turn = turn };
            }

            if (winnerIds.Count > 0)
            {
                return new GameOutcome { Kind = GameOutcomeKind.Defeat, Reason = GameOutcomeReason.RivalConverter, WinnerIds = winnerIds, Turn = turn };
            }

            // A defeated rival leaves the game. The game goes on without it.
            foreach (Player player in players)
            {
                if (player.Id != humanId && !player.Eliminated && DefeatReason(player).HasValue)
                {
                    player.Eliminated = true;
                }
            }

            bool hadRivals = players.Any(player => player.Id != humanId);
            bool rivalsLeft = players.Any(player => player.Id != humanId && !player.Eliminated);
            if (hadRivals && !rivalsLeft)
            {
                return new GameOutcome
                {
                    Kind = GameOutcomeKind.Victory,
                    Reason = GameOutcomeReason.Elimination,
                    WinnerIds = new List<string> { humanId },
                    Turn = turn,
                };
            }

            return null;
        }
    }
}
