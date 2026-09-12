using System;
using System.Collections.Generic;

/// <summary>
/// The full data of one building kind: what it costs, what its die shows and
/// how it behaves during upkeep and disasters. Definitions are plain data and
/// live in <see cref="HexworldConfig"/>, so balance edits never touch logic.
/// </summary>
public sealed class HexworldBuildingDefinition
{
    /// <summary>
    /// Creates a definition.
    /// </summary>
    /// <param name="type">Which building this describes.</param>
    /// <param name="cost">Price paid during the build phase.</param>
    /// <param name="flatIncome">Income paid every harvest regardless of the die.</param>
    /// <param name="faces">The six die faces, or null when the building rolls no die.</param>
    /// <param name="isBuildable">True when a player may build it during the build phase.</param>
    /// <param name="isDestructibleByDisaster">True when a skull disaster may destroy it.</param>
    /// <param name="requiresUpkeep">True when it eats food during upkeep.</param>
    /// <exception cref="ArgumentException">The face list is neither null nor exactly six long.</exception>
    public HexworldBuildingDefinition(
        HexBuildingType type,
        HexworldResources cost,
        HexworldResources flatIncome,
        IReadOnlyList<HexworldDiceFace> faces,
        bool isBuildable,
        bool isDestructibleByDisaster,
        bool requiresUpkeep)
    {
        if (faces != null && faces.Count != 0 && faces.Count != 6)
        {
            throw new ArgumentException("A building die must have exactly six faces.", "faces");
        }

        Type = type;
        Cost = cost;
        FlatIncome = flatIncome;
        Faces = faces ?? Array.Empty<HexworldDiceFace>();
        IsBuildable = isBuildable;
        IsDestructibleByDisaster = isDestructibleByDisaster;
        RequiresUpkeep = requiresUpkeep;
    }

    /// <summary>Which building this definition describes.</summary>
    public HexBuildingType Type { get; private set; }

    /// <summary>Price paid once, during the build phase.</summary>
    public HexworldResources Cost { get; private set; }

    /// <summary>Income paid every harvest on top of the die result.</summary>
    public HexworldResources FlatIncome { get; private set; }

    /// <summary>The six die faces. Empty when the building rolls no die.</summary>
    public IReadOnlyList<HexworldDiceFace> Faces { get; private set; }

    /// <summary>True when a player may build this during the build phase.</summary>
    public bool IsBuildable { get; private set; }

    /// <summary>True when a skull disaster may pick this building for destruction.</summary>
    public bool IsDestructibleByDisaster { get; private set; }

    /// <summary>True when the building eats food during upkeep.</summary>
    public bool RequiresUpkeep { get; private set; }

    /// <summary>True when the building contributes a die to the harvest roll.</summary>
    public bool ProducesDice
    {
        get { return Faces != null && Faces.Count == 6; }
    }
}
