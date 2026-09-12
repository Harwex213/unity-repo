using System;

/// <summary>
/// An axial coordinate of a flat-top hexagon. The pair (q, r) names the tile and
/// the derived third cube coordinate s always equals -q-r, so the three of them
/// sum to zero. The type is immutable and can be used as a dictionary key.
/// </summary>
public readonly struct HexCoord : IEquatable<HexCoord>
{
    /// <summary>The six neighbour offsets, in clockwise order starting from the east neighbour.</summary>
    private static readonly HexCoord[] DirectionOffsets =
    {
        new HexCoord(1, 0),
        new HexCoord(1, -1),
        new HexCoord(0, -1),
        new HexCoord(-1, 0),
        new HexCoord(-1, 1),
        new HexCoord(0, 1),
    };

    /// <summary>The axial q coordinate, which grows along the column axis.</summary>
    public int Q { get; }

    /// <summary>The axial r coordinate, which grows along the row axis.</summary>
    public int R { get; }

    /// <summary>The derived cube coordinate s. It always equals -q-r.</summary>
    public int S => -Q - R;

    /// <summary>
    /// Creates a coordinate from its axial pair.
    /// </summary>
    /// <param name="q">The axial q coordinate.</param>
    /// <param name="r">The axial r coordinate.</param>
    public HexCoord(int q, int r)
    {
        Q = q;
        R = r;
    }

    /// <summary>The centre of the island, that is (0, 0).</summary>
    public static HexCoord Zero => new HexCoord(0, 0);

    /// <summary>How many neighbour directions a hexagon has.</summary>
    public static int DirectionCount => DirectionOffsets.Length;

    /// <summary>
    /// Returns the offset of one of the six directions.
    /// </summary>
    /// <param name="direction">Direction index from 0 to 5.</param>
    /// <returns>The offset to add to a coordinate to reach that neighbour.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The index is outside 0..5.</exception>
    public static HexCoord Direction(int direction)
    {
        if (direction < 0 || direction >= DirectionOffsets.Length)
        {
            throw new ArgumentOutOfRangeException("direction", direction, "Direction must be in the range 0..5.");
        }

        return DirectionOffsets[direction];
    }

    /// <summary>
    /// Returns the neighbour in one of the six directions.
    /// </summary>
    /// <param name="direction">Direction index from 0 to 5.</param>
    /// <returns>The neighbouring coordinate.</returns>
    public HexCoord Neighbor(int direction) => this + Direction(direction);

    /// <summary>
    /// Returns all six neighbours in direction order.
    /// </summary>
    /// <returns>A fresh array of six coordinates.</returns>
    public HexCoord[] GetNeighbors()
    {
        var result = new HexCoord[DirectionOffsets.Length];
        for (int i = 0; i < DirectionOffsets.Length; i++)
        {
            result[i] = this + DirectionOffsets[i];
        }

        return result;
    }

    /// <summary>
    /// Measures the distance between two coordinates in hexagon steps.
    /// </summary>
    /// <param name="a">First coordinate.</param>
    /// <param name="b">Second coordinate.</param>
    /// <returns>The number of steps needed to walk from a to b.</returns>
    public static int Distance(HexCoord a, HexCoord b)
    {
        int dq = a.Q - b.Q;
        int dr = a.R - b.R;
        int ds = a.S - b.S;
        return (Math.Abs(dq) + Math.Abs(dr) + Math.Abs(ds)) / 2;
    }

    /// <summary>
    /// Measures the distance from this coordinate to another one.
    /// </summary>
    /// <param name="other">The other coordinate.</param>
    /// <returns>The number of steps between them.</returns>
    public int DistanceTo(HexCoord other) => Distance(this, other);

    /// <summary>
    /// Tells whether the other coordinate is one step away.
    /// </summary>
    /// <param name="other">The other coordinate.</param>
    /// <returns>True when the two tiles share an edge.</returns>
    public bool IsNeighborOf(HexCoord other) => Distance(this, other) == 1;

    /// <summary>
    /// Snaps a fractional axial coordinate to the nearest whole hexagon.
    /// </summary>
    /// <param name="q">Fractional q coordinate.</param>
    /// <param name="r">Fractional r coordinate.</param>
    /// <returns>The nearest whole coordinate.</returns>
    public static HexCoord Round(float q, float r)
    {
        float s = -q - r;
        int roundedQ = (int)Math.Round(q);
        int roundedR = (int)Math.Round(r);
        int roundedS = (int)Math.Round(s);

        double diffQ = Math.Abs(roundedQ - q);
        double diffR = Math.Abs(roundedR - r);
        double diffS = Math.Abs(roundedS - s);

        if (diffQ > diffR && diffQ > diffS)
        {
            roundedQ = -roundedR - roundedS;
        }
        else if (diffR > diffS)
        {
            roundedR = -roundedQ - roundedS;
        }

        return new HexCoord(roundedQ, roundedR);
    }

    /// <summary>Adds two coordinates component by component.</summary>
    /// <param name="a">Left coordinate.</param>
    /// <param name="b">Right coordinate.</param>
    /// <returns>The sum.</returns>
    public static HexCoord operator +(HexCoord a, HexCoord b) => new HexCoord(a.Q + b.Q, a.R + b.R);

    /// <summary>Subtracts two coordinates component by component.</summary>
    /// <param name="a">Left coordinate.</param>
    /// <param name="b">Right coordinate.</param>
    /// <returns>The difference.</returns>
    public static HexCoord operator -(HexCoord a, HexCoord b) => new HexCoord(a.Q - b.Q, a.R - b.R);

    /// <summary>Compares two coordinates.</summary>
    /// <param name="a">Left coordinate.</param>
    /// <param name="b">Right coordinate.</param>
    /// <returns>True when both components match.</returns>
    public static bool operator ==(HexCoord a, HexCoord b) => a.Equals(b);

    /// <summary>Compares two coordinates.</summary>
    /// <param name="a">Left coordinate.</param>
    /// <param name="b">Right coordinate.</param>
    /// <returns>True when at least one component differs.</returns>
    public static bool operator !=(HexCoord a, HexCoord b) => !a.Equals(b);

    /// <inheritdoc />
    public bool Equals(HexCoord other) => Q == other.Q && R == other.R;

    /// <inheritdoc />
    public override bool Equals(object obj) => obj is HexCoord other && Equals(other);

    /// <inheritdoc />
    public override int GetHashCode()
    {
        unchecked
        {
            return (Q * 397) ^ R;
        }
    }

    /// <inheritdoc />
    public override string ToString() => string.Format("({0}, {1})", Q, R);
}
