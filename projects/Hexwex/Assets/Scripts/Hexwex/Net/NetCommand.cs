using Hexwex.Core;

namespace Hexwex.Net
{
    /// <summary>The actions a person can take in a game, one per action of <see cref="GameSession"/>.</summary>
    public enum CommandKind : byte
    {
        PlaceStronghold,
        /// <summary>Only the host starts the game.</summary>
        StartGame,
        Build,
        Demolish,
        CleanseSoil,
        SetHexWorkers,
        ChangeArmy,
        Research,
        PickTaxFace,
        Scout,
        MoveIsland,
        EndPhase,
        /// <summary>Ends the sender's battle with the result they fought.</summary>
        EndBattle,
        Resign,
    }

    /// <summary>
    /// One action of one person. A network game is kept in step by playing the
    /// same commands in the same order on every machine: the host numbers them
    /// and sends them to everyone, and each machine applies them to its own copy
    /// of the <see cref="GameSession"/>. The session's own rules refuse what is
    /// not allowed, the same way on every machine.
    /// </summary>
    public sealed class NetCommand
    {
        public CommandKind Kind;
        /// <summary>Whose action it is. The host sets it from the connection it came on.</summary>
        public string PlayerId;
        /// <summary>A hex or cell id: where the action is.</summary>
        public string Target;
        /// <summary>A second hex id: the hex the soil cleansing purifies.</summary>
        public string Second;
        /// <summary>A building, a technology, a face, a count or a delta, by kind.</summary>
        public int Value;
        /// <summary>The battle's result, for <see cref="CommandKind.EndBattle"/>.</summary>
        public CleanupResult Result;

        public static NetCommand PlaceStronghold(string hexId) => new NetCommand { Kind = CommandKind.PlaceStronghold, Target = hexId };
        public static NetCommand StartGame() => new NetCommand { Kind = CommandKind.StartGame };
        public static NetCommand Build(string hexId, BuildingId building) => new NetCommand { Kind = CommandKind.Build, Target = hexId, Value = (int)building };
        public static NetCommand Demolish(string hexId) => new NetCommand { Kind = CommandKind.Demolish, Target = hexId };
        public static NetCommand CleanseSoil(string sacrificeHexId, string purifyHexId) => new NetCommand { Kind = CommandKind.CleanseSoil, Target = sacrificeHexId, Second = purifyHexId };
        public static NetCommand SetHexWorkers(string hexId, int count) => new NetCommand { Kind = CommandKind.SetHexWorkers, Target = hexId, Value = count };
        public static NetCommand ChangeArmy(int delta) => new NetCommand { Kind = CommandKind.ChangeArmy, Value = delta };
        public static NetCommand Research(TechId tech) => new NetCommand { Kind = CommandKind.Research, Value = (int)tech };
        public static NetCommand PickTaxFace(string hexId, int faceIndex) => new NetCommand { Kind = CommandKind.PickTaxFace, Target = hexId, Value = faceIndex };
        public static NetCommand Scout(string cellId) => new NetCommand { Kind = CommandKind.Scout, Target = cellId };
        public static NetCommand MoveIsland(string cellId) => new NetCommand { Kind = CommandKind.MoveIsland, Target = cellId };
        public static NetCommand EndPhase() => new NetCommand { Kind = CommandKind.EndPhase };
        public static NetCommand EndBattle(CleanupResult result) => new NetCommand { Kind = CommandKind.EndBattle, Result = result };
        public static NetCommand Resign() => new NetCommand { Kind = CommandKind.Resign };

        /// <summary>Plays the command on a session. Returns whether the session took it.</summary>
        public bool Apply(GameSession session)
        {
            switch (Kind)
            {
                case CommandKind.PlaceStronghold:
                    return session.PlaceStronghold(PlayerId, Target);
                case CommandKind.StartGame:
                    return session.StartGame();
                case CommandKind.Build:
                    return session.Build(PlayerId, Target, (BuildingId)Value);
                case CommandKind.Demolish:
                    return session.Demolish(PlayerId, Target);
                case CommandKind.CleanseSoil:
                    return session.CleanseSoil(PlayerId, Target, Second);
                case CommandKind.SetHexWorkers:
                    return session.SetHexWorkers(PlayerId, Target, Value);
                case CommandKind.ChangeArmy:
                    return session.ChangeArmy(PlayerId, Value);
                case CommandKind.Research:
                    return session.Research(PlayerId, (TechId)Value);
                case CommandKind.PickTaxFace:
                    return session.PickTaxFace(PlayerId, Target, Value);
                case CommandKind.Scout:
                    return session.Scout(PlayerId, Target);
                case CommandKind.MoveIsland:
                    return session.MoveIsland(PlayerId, Target);
                case CommandKind.EndPhase:
                    return session.EndPhase(PlayerId);
                case CommandKind.EndBattle:
                    return session.EndBattle(PlayerId, Result);
                case CommandKind.Resign:
                    return session.Resign(PlayerId);
                default:
                    return false;
            }
        }

        public override string ToString()
        {
            return Kind + " by " + PlayerId + (Target != null ? " at " + Target : "") + (Value != 0 ? " (" + Value + ")" : "");
        }
    }
}
