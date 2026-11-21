using Hexwex.Core;
using Hexwex.Net;

namespace Hexwex.View
{
    /// <summary>
    /// The actions the screen takes, for the seat played on this machine. In a
    /// single-player game they go straight to the session. In a network game they
    /// go to the host, and take effect when the host's numbered command comes back:
    /// the screen redraws on <see cref="GameSession.Changed"/> either way.
    ///
    /// The battle is not here: only the person who fights it runs its ticks and
    /// casts, on this machine's copy, and its result goes out when the phase ends.
    /// </summary>
    public sealed class GameActions
    {
        private readonly GameRoot _game;

        public GameActions(GameRoot game)
        {
            _game = game;
        }

        private GameSession Session
        {
            get { return _game.Session; }
        }

        /// <summary>
        /// Plays the command now, or sends it to the host. Offline the answer is the
        /// session's own; online it is only whether the command went out.
        /// </summary>
        private bool Do(NetCommand command)
        {
            NetGame net = _game.Net;
            if (net != null)
            {
                net.Submit(command);

                return !net.IsClosed;
            }

            command.PlayerId = _game.MyId;

            return command.Apply(Session);
        }

        /// <summary>Only the host starts a network game.</summary>
        public bool StartGame()
        {
            NetGame net = _game.Net;
            if (net != null && !net.IsHost)
            {
                return false;
            }

            return Do(NetCommand.StartGame());
        }

        public bool PlaceStronghold(string hexId) => Do(NetCommand.PlaceStronghold(hexId));
        public bool Build(string hexId, BuildingId building) => Do(NetCommand.Build(hexId, building));
        public bool Demolish(string hexId) => Do(NetCommand.Demolish(hexId));
        public bool CleanseSoil(string sacrificeHexId, string purifyHexId) => Do(NetCommand.CleanseSoil(sacrificeHexId, purifyHexId));
        public bool SetHexWorkers(string hexId, int count) => Do(NetCommand.SetHexWorkers(hexId, count));
        public bool ChangeArmy(int delta) => Do(NetCommand.ChangeArmy(delta));
        public bool Research(TechId tech) => Do(NetCommand.Research(tech));
        public bool PickTaxFace(string hexId, int faceIndex) => Do(NetCommand.PickTaxFace(hexId, faceIndex));
        public bool Scout(string cellId) => Do(NetCommand.Scout(cellId));
        public bool MoveIsland(string cellId) => Do(NetCommand.MoveIsland(cellId));

        /// <summary>
        /// Ends the phase for this seat. A battle that has ended goes out with its
        /// result, so the other machines can take it without fighting it.
        /// </summary>
        public bool EndPhase()
        {
            Seat seat = _game.MySeat;
            if (Session.Phase == Phase.Clear && seat != null && seat.Battle != null && seat.BattleResult != null)
            {
                return Do(NetCommand.EndBattle(seat.BattleResult));
            }

            return Do(NetCommand.EndPhase());
        }

        /// <summary>Runs the ticks of this seat's own battle, here only.</summary>
        public void StepBattle(int ticks)
        {
            Session.StepBattle(_game.MyId, ticks);
        }

        /// <summary>Casts at this seat's own battle, here only.</summary>
        public bool CastShatter(int islandIndex, int hexIndex)
        {
            return Session.CastShatter(_game.MyId, islandIndex, hexIndex);
        }
    }
}
