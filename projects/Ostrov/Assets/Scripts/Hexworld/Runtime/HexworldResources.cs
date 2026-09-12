using System;

/// <summary>
/// An immutable bundle of the five game resources. The same type describes a
/// stockpile, a price, a die face yield and a per-turn income, so all of them
/// share one arithmetic.
/// </summary>
public readonly struct HexworldResources : IEquatable<HexworldResources>
{
    /// <summary>Food amount.</summary>
    public int Food { get; }

    /// <summary>Wood amount.</summary>
    public int Wood { get; }

    /// <summary>Stone amount.</summary>
    public int Stone { get; }

    /// <summary>Culture amount.</summary>
    public int Culture { get; }

    /// <summary>Soldier amount.</summary>
    public int Soldiers { get; }

    /// <summary>
    /// Creates a bundle from explicit amounts.
    /// </summary>
    /// <param name="food">Food amount.</param>
    /// <param name="wood">Wood amount.</param>
    /// <param name="stone">Stone amount.</param>
    /// <param name="culture">Culture amount.</param>
    /// <param name="soldiers">Soldier amount.</param>
    public HexworldResources(int food, int wood, int stone, int culture, int soldiers)
    {
        Food = food;
        Wood = wood;
        Stone = stone;
        Culture = culture;
        Soldiers = soldiers;
    }

    /// <summary>An empty bundle. Every amount is zero.</summary>
    public static HexworldResources Zero => new HexworldResources(0, 0, 0, 0, 0);

    /// <summary>Sum of all five amounts. Used as a crude "how good is this" score.</summary>
    public int Total => Food + Wood + Stone + Culture + Soldiers;

    /// <summary>True when every amount is zero.</summary>
    public bool IsZero => Food == 0 && Wood == 0 && Stone == 0 && Culture == 0 && Soldiers == 0;

    /// <summary>Creates a bundle that holds only food.</summary>
    /// <param name="amount">Food amount.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources FromFood(int amount) => new HexworldResources(amount, 0, 0, 0, 0);

    /// <summary>Creates a bundle that holds only wood.</summary>
    /// <param name="amount">Wood amount.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources FromWood(int amount) => new HexworldResources(0, amount, 0, 0, 0);

    /// <summary>Creates a bundle that holds only stone.</summary>
    /// <param name="amount">Stone amount.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources FromStone(int amount) => new HexworldResources(0, 0, amount, 0, 0);

    /// <summary>Creates a bundle that holds only culture.</summary>
    /// <param name="amount">Culture amount.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources FromCulture(int amount) => new HexworldResources(0, 0, 0, amount, 0);

    /// <summary>Creates a bundle that holds only soldiers.</summary>
    /// <param name="amount">Soldier amount.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources FromSoldiers(int amount) => new HexworldResources(0, 0, 0, 0, amount);

    /// <summary>
    /// Creates a bundle that holds a single resource kind.
    /// </summary>
    /// <param name="type">Which resource to fill.</param>
    /// <param name="amount">How much of it.</param>
    /// <returns>The bundle.</returns>
    public static HexworldResources Single(HexworldResourceType type, int amount)
    {
        switch (type)
        {
            case HexworldResourceType.Food: return FromFood(amount);
            case HexworldResourceType.Wood: return FromWood(amount);
            case HexworldResourceType.Stone: return FromStone(amount);
            case HexworldResourceType.Culture: return FromCulture(amount);
            case HexworldResourceType.Soldiers: return FromSoldiers(amount);
            default: return Zero;
        }
    }

    /// <summary>
    /// Reads one amount by its resource kind.
    /// </summary>
    /// <param name="type">Which resource to read.</param>
    /// <returns>The amount held for that resource.</returns>
    public int Get(HexworldResourceType type)
    {
        switch (type)
        {
            case HexworldResourceType.Food: return Food;
            case HexworldResourceType.Wood: return Wood;
            case HexworldResourceType.Stone: return Stone;
            case HexworldResourceType.Culture: return Culture;
            case HexworldResourceType.Soldiers: return Soldiers;
            default: return 0;
        }
    }

    /// <summary>
    /// Returns a copy with one amount replaced.
    /// </summary>
    /// <param name="type">Which resource to replace.</param>
    /// <param name="amount">The new amount.</param>
    /// <returns>The changed copy.</returns>
    public HexworldResources With(HexworldResourceType type, int amount)
    {
        switch (type)
        {
            case HexworldResourceType.Food: return new HexworldResources(amount, Wood, Stone, Culture, Soldiers);
            case HexworldResourceType.Wood: return new HexworldResources(Food, amount, Stone, Culture, Soldiers);
            case HexworldResourceType.Stone: return new HexworldResources(Food, Wood, amount, Culture, Soldiers);
            case HexworldResourceType.Culture: return new HexworldResources(Food, Wood, Stone, amount, Soldiers);
            case HexworldResourceType.Soldiers: return new HexworldResources(Food, Wood, Stone, Culture, amount);
            default: return this;
        }
    }

    /// <summary>
    /// Tells whether this stockpile pays the given price in full.
    /// </summary>
    /// <param name="cost">The price to pay.</param>
    /// <returns>True when every amount of the price is available.</returns>
    public bool Covers(HexworldResources cost)
    {
        return Food >= cost.Food
               && Wood >= cost.Wood
               && Stone >= cost.Stone
               && Culture >= cost.Culture
               && Soldiers >= cost.Soldiers;
    }

    /// <summary>
    /// Returns a copy where every negative amount is raised to zero.
    /// </summary>
    /// <returns>The clamped copy.</returns>
    public HexworldResources ClampedToZero()
    {
        return new HexworldResources(
            Food < 0 ? 0 : Food,
            Wood < 0 ? 0 : Wood,
            Stone < 0 ? 0 : Stone,
            Culture < 0 ? 0 : Culture,
            Soldiers < 0 ? 0 : Soldiers);
    }

    /// <summary>Adds two bundles amount by amount.</summary>
    /// <param name="a">Left bundle.</param>
    /// <param name="b">Right bundle.</param>
    /// <returns>The sum.</returns>
    public static HexworldResources operator +(HexworldResources a, HexworldResources b)
    {
        return new HexworldResources(
            a.Food + b.Food,
            a.Wood + b.Wood,
            a.Stone + b.Stone,
            a.Culture + b.Culture,
            a.Soldiers + b.Soldiers);
    }

    /// <summary>Subtracts two bundles amount by amount. The result may go negative.</summary>
    /// <param name="a">Left bundle.</param>
    /// <param name="b">Right bundle.</param>
    /// <returns>The difference.</returns>
    public static HexworldResources operator -(HexworldResources a, HexworldResources b)
    {
        return new HexworldResources(
            a.Food - b.Food,
            a.Wood - b.Wood,
            a.Stone - b.Stone,
            a.Culture - b.Culture,
            a.Soldiers - b.Soldiers);
    }

    /// <summary>Multiplies every amount by a whole factor.</summary>
    /// <param name="a">The bundle.</param>
    /// <param name="factor">The factor.</param>
    /// <returns>The scaled bundle.</returns>
    public static HexworldResources operator *(HexworldResources a, int factor)
    {
        return new HexworldResources(
            a.Food * factor,
            a.Wood * factor,
            a.Stone * factor,
            a.Culture * factor,
            a.Soldiers * factor);
    }

    /// <summary>Compares two bundles amount by amount.</summary>
    /// <param name="a">Left bundle.</param>
    /// <param name="b">Right bundle.</param>
    /// <returns>True when all five amounts match.</returns>
    public static bool operator ==(HexworldResources a, HexworldResources b) => a.Equals(b);

    /// <summary>Compares two bundles amount by amount.</summary>
    /// <param name="a">Left bundle.</param>
    /// <param name="b">Right bundle.</param>
    /// <returns>True when at least one amount differs.</returns>
    public static bool operator !=(HexworldResources a, HexworldResources b) => !a.Equals(b);

    /// <inheritdoc />
    public bool Equals(HexworldResources other)
    {
        return Food == other.Food
               && Wood == other.Wood
               && Stone == other.Stone
               && Culture == other.Culture
               && Soldiers == other.Soldiers;
    }

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is HexworldResources other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            int hash = Food;
            hash = (hash * 397) ^ Wood;
            hash = (hash * 397) ^ Stone;
            hash = (hash * 397) ^ Culture;
            hash = (hash * 397) ^ Soldiers;
            return hash;
        }
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format(
            "F{0} W{1} S{2} C{3} A{4}", Food, Wood, Stone, Culture, Soldiers);
    }
}
