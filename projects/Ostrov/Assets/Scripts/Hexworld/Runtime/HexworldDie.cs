/// <summary>
/// One die of the running harvest. It remembers which building rolled it and
/// which face came up, so the view can draw the die over its building and the
/// reroll rule can lock the skulls.
/// </summary>
public sealed class HexworldDie
{
    /// <summary>
    /// Creates a die for a building.
    /// </summary>
    /// <param name="index">Position of the die inside the harvest roll.</param>
    /// <param name="source">Tile whose building rolls this die.</param>
    /// <param name="buildingType">Which building rolls this die.</param>
    public HexworldDie(int index, HexCoord source, HexBuildingType buildingType)
    {
        Index = index;
        Source = source;
        BuildingType = buildingType;
        FaceIndex = 0;
        Face = HexworldDiceFace.Pays(HexworldResources.Zero);
    }

    /// <summary>Position of the die inside the harvest roll. Rerolls address dice by this number.</summary>
    public int Index { get; private set; }

    /// <summary>Tile whose building rolls this die.</summary>
    public HexCoord Source { get; private set; }

    /// <summary>Which building rolls this die.</summary>
    public HexBuildingType BuildingType { get; private set; }

    /// <summary>Which of the six faces came up, counted from zero.</summary>
    public int FaceIndex { get; private set; }

    /// <summary>The face that came up.</summary>
    public HexworldDiceFace Face { get; private set; }

    /// <summary>True when the face shows a skull. A skull die can never be rerolled.</summary>
    public bool IsSkull
    {
        get { return Face.IsSkull; }
    }

    /// <summary>True when the player may still reroll this die.</summary>
    public bool CanReroll
    {
        get { return !Face.IsSkull; }
    }

    /// <summary>
    /// Writes a rolled face onto the die.
    /// </summary>
    /// <param name="faceIndex">Which of the six faces came up.</param>
    /// <param name="face">The face itself.</param>
    public void SetFace(int faceIndex, HexworldDiceFace face)
    {
        FaceIndex = faceIndex;
        Face = face;
    }

    /// <inheritdoc />
    public override string ToString()
    {
        return string.Format("#{0} {1} {2} {3}", Index, BuildingType, Source, Face);
    }
}
