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
        /// <summary>The person left a game the others play on.</summary>
        Left,
    }

    public sealed class GameOutcome
    {
        /// <summary>How the game ended for the first human seat.</summary>
        public GameOutcomeKind Kind;
        public GameOutcomeReason Reason;
        /// <summary>The players who won, the human included when the human won.</summary>
        public List<string> WinnerIds;
        public int Turn;
        /// <summary>Why each human seat that did not win lost.</summary>
        public Dictionary<string, GameOutcomeReason> Reasons = new Dictionary<string, GameOutcomeReason>();

        /// <summary>How the game ended for that player.</summary>
        public GameOutcomeKind KindFor(string playerId)
        {
            if (WinnerIds == null || !WinnerIds.Contains(playerId))
            {
                return GameOutcomeKind.Defeat;
            }

            return WinnerIds.Count > 1 ? GameOutcomeKind.Shared : GameOutcomeKind.Victory;
        }

        /// <summary>Why the game ended so for that player.</summary>
        public GameOutcomeReason ReasonFor(string playerId)
        {
            if (Reasons.TryGetValue(playerId, out GameOutcomeReason reason))
            {
                return reason;
            }

            if (WinnerIds != null && WinnerIds.Contains(playerId))
            {
                if (Reason == GameOutcomeReason.Elimination)
                {
                    return GameOutcomeReason.Elimination;
                }

                return WinnerIds.Count > 1 ? GameOutcomeReason.SharedConverter : GameOutcomeReason.Converter;
            }

            return GameOutcomeReason.RivalConverter;
        }

        /// <summary>The same ending, told from that player's side.</summary>
        public GameOutcome For(string playerId)
        {
            return new GameOutcome { Kind = KindFor(playerId), Reason = ReasonFor(playerId), WinnerIds = WinnerIds, Turn = Turn, Reasons = Reasons };
        }
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
    /// - Several people. A defeated person is eliminated like a rival while
    ///   another person still plays; the game ends in defeat when the last one
    ///   falls. The one player left standing wins by elimination.
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
            return Check(players, new[] { humanId }, turn);
        }

        /// <summary>
        /// As <see cref="Check(IReadOnlyList{Player}, string, int)"/> for any number of
        /// people. The outcome's own kind and reason are the first seat's; every
        /// seat's are in <see cref="GameOutcome.For"/>.
        /// </summary>
        public static GameOutcome Check(IReadOnlyList<Player> players, IReadOnlyList<string> humanIds, int turn)
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
            List<Player> people = active.Where(player => humanIds.Contains(player.Id)).ToList();

            // A converter ends the game for everyone. A person who did not build it
            // lost to it, unless the same turn had already ruined them.
            if (winnerIds.Count > 0)
            {
                Dictionary<string, GameOutcomeReason> reasons = new Dictionary<string, GameOutcomeReason>();
                foreach (Player person in people.Where(person => !winnerIds.Contains(person.Id)))
                {
                    reasons[person.Id] = DefeatReason(person) ?? GameOutcomeReason.RivalConverter;
                }

                return Finish(humanIds[0], winnerIds, reasons, GameOutcomeReason.Converter, turn);
            }

            // The defeated people. While another person plays they only leave the game;
            // when none is left the game ends in their defeat.
            Dictionary<string, GameOutcomeReason> defeated = new Dictionary<string, GameOutcomeReason>();
            foreach (Player person in people)
            {
                GameOutcomeReason? reason = DefeatReason(person);
                if (reason.HasValue)
                {
                    defeated[person.Id] = reason.Value;
                }
            }

            if (people.Count > 0 && defeated.Count == people.Count)
            {
                return Finish(humanIds[0], winnerIds, defeated, GameOutcomeReason.Ruined, turn);
            }

            // A defeated player leaves the game. The game goes on without it.
            foreach (Player player in players)
            {
                if (player.Eliminated)
                {
                    continue;
                }

                GameOutcomeReason? reason = defeated.TryGetValue(player.Id, out GameOutcomeReason known) ? known : DefeatReason(player);
                if (reason.HasValue)
                {
                    player.Eliminated = true;
                    player.EliminatedBy = reason;
                }
            }

            // The one player left standing wins.
            List<Player> standing = players.Where(player => !player.Eliminated).ToList();
            if (players.Count > 1 && standing.Count == 1 && humanIds.Contains(standing[0].Id))
            {
                GameOutcome outcome = Finish(humanIds[0], new List<string> { standing[0].Id }, new Dictionary<string, GameOutcomeReason>(), GameOutcomeReason.Elimination, turn);
                foreach (Player player in players.Where(player => humanIds.Contains(player.Id) && player.EliminatedBy.HasValue))
                {
                    outcome.Reasons[player.Id] = player.EliminatedBy.Value;
                }

                return outcome;
            }

            return null;
        }

        /// <summary>The outcome, its own kind and reason told from the first seat's side.</summary>
        private static GameOutcome Finish(string firstId, List<string> winnerIds, Dictionary<string, GameOutcomeReason> reasons, GameOutcomeReason winReason, int turn)
        {
            GameOutcome outcome = new GameOutcome { WinnerIds = winnerIds, Turn = turn, Reasons = reasons };
            if (winnerIds.Contains(firstId))
            {
                bool isShared = winnerIds.Count > 1;
                outcome.Kind = isShared ? GameOutcomeKind.Shared : GameOutcomeKind.Victory;
                outcome.Reason = winReason == GameOutcomeReason.Elimination ? GameOutcomeReason.Elimination : isShared ? GameOutcomeReason.SharedConverter : GameOutcomeReason.Converter;
            }
            else
            {
                outcome.Kind = GameOutcomeKind.Defeat;
                outcome.Reason = reasons.TryGetValue(firstId, out GameOutcomeReason reason) ? reason : GameOutcomeReason.RivalConverter;
            }

            return outcome;
        }
    }
}
