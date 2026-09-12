/// <summary>
/// The full record of one capture attempt. The view reads it to play the fight
/// animation and the UI reads it to write the combat log.
/// </summary>
public sealed class HexworldCombatResult
{
    /// <summary>
    /// Creates a record of a capture attempt.
    /// </summary>
    /// <param name="attacker">Player index of the attacker.</param>
    /// <param name="defender">Player index of the defender, or -1 for a neutral tile.</param>
    /// <param name="coord">The tile under attack.</param>
    /// <param name="soldiersSpent">How many soldiers the attacker paid.</param>
    /// <param name="attackRoll">The attacker die, or 0 for a neutral tile.</param>
    /// <param name="defenseRoll">The defender die, or 0 for a neutral tile.</param>
    /// <param name="attackPower">Total attack strength.</param>
    /// <param name="defensePower">Total defence strength.</param>
    /// <param name="captured">True when the tile changed hands.</param>
    /// <param name="destroyedBuilding">Building that died with the tile, or None.</param>
    public HexworldCombatResult(
        int attacker,
        int defender,
        HexCoord coord,
        int soldiersSpent,
        int attackRoll,
        int defenseRoll,
        int attackPower,
        int defensePower,
        bool captured,
        HexBuildingType destroyedBuilding)
    {
        Attacker = attacker;
        Defender = defender;
        Coord = coord;
        SoldiersSpent = soldiersSpent;
        AttackRoll = attackRoll;
        DefenseRoll = defenseRoll;
        AttackPower = attackPower;
        DefensePower = defensePower;
        Captured = captured;
        DestroyedBuilding = destroyedBuilding;
    }

    /// <summary>Player index of the attacker.</summary>
    public int Attacker { get; private set; }

    /// <summary>Player index of the defender, or -1 when the tile was neutral.</summary>
    public int Defender { get; private set; }

    /// <summary>The tile under attack.</summary>
    public HexCoord Coord { get; private set; }

    /// <summary>How many soldiers the attacker paid. They are spent win or lose.</summary>
    public int SoldiersSpent { get; private set; }

    /// <summary>The attacker die. It is zero when the tile was neutral and no fight happened.</summary>
    public int AttackRoll { get; private set; }

    /// <summary>The defender die. It is zero when the tile was neutral and no fight happened.</summary>
    public int DefenseRoll { get; private set; }

    /// <summary>Soldiers plus the attacker die.</summary>
    public int AttackPower { get; private set; }

    /// <summary>Base defence, building bonus, barracks bonus and the defender die.</summary>
    public int DefensePower { get; private set; }

    /// <summary>True when the tile changed hands.</summary>
    public bool Captured { get; private set; }

    /// <summary>The building that died with the tile, or None when there was none.</summary>
    public HexBuildingType DestroyedBuilding { get; private set; }

    /// <summary>True when the target was a neutral tile, which is taken without a fight.</summary>
    public bool WasNeutral
    {
        get { return Defender == HexworldConfig.NeutralOwner; }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(
            "P{0} -> {1}: {2} vs {3}, {4}",
            Attacker,
            Coord,
            AttackPower,
            DefensePower,
            Captured ? "captured" : "repelled");
    }
}
