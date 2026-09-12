/// <summary>
/// Everything the game tracks per player: the stockpile, the skull counter of
/// the running turn, how many turns the player has taken and the bookkeeping
/// that the Monument victory needs.
/// </summary>
public sealed class HexworldPlayerState
{
    /// <summary>Value that means "this player has never finished a Monument".</summary>
    public const int NoMonumentTurn = -1;

    /// <summary>
    /// Creates a player state.
    /// </summary>
    /// <param name="index">Player index, 0 for the human side and 1 for the AI side.</param>
    /// <param name="startingResources">What the player owns at the start.</param>
    public HexworldPlayerState(int index, HexworldResources startingResources)
    {
        Index = index;
        Resources = startingResources;
        Skulls = 0;
        TurnsTaken = 0;
        MonumentBuiltOnTurn = NoMonumentTurn;
        HasCapturedThisTurn = false;
    }

    /// <summary>Player index. Player 0 is the local side, player 1 is the opponent.</summary>
    public int Index { get; private set; }

    /// <summary>The stockpile. It never holds a negative amount.</summary>
    public HexworldResources Resources { get; set; }

    /// <summary>Skulls rolled during the running turn. The value resets when the turn starts.</summary>
    public int Skulls { get; set; }

    /// <summary>How many turns this player has started so far.</summary>
    public int TurnsTaken { get; set; }

    /// <summary>The value of <see cref="TurnsTaken"/> when the Monument was finished.</summary>
    public int MonumentBuiltOnTurn { get; set; }

    /// <summary>True when the player already captured a tile during the running turn.</summary>
    public bool HasCapturedThisTurn { get; set; }

    /// <summary>True when the player has finished a Monument at some point.</summary>
    public bool HasBuiltMonument
    {
        get { return MonumentBuiltOnTurn != NoMonumentTurn; }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format("Player {0}: {1}, skulls {2}", Index, Resources, Skulls);
    }
}
