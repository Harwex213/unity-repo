using UnityEngine;

/// <summary>
/// Converts between axial hexagon coordinates and world positions. This is the
/// only part of the core that touches UnityEngine types, so the rest of the
/// logic stays plain C#.
/// </summary>
/// <remarks>
/// Hexagons are flat-top. The hexagon radius, measured from the centre to a
/// corner, is <see cref="HexRadius"/>. The board lies on the XZ plane and the
/// Y coordinate of every tile centre is zero.
/// </remarks>
public static class HexLayout
{
    /// <summary>Distance from the centre of a hexagon to any of its corners.</summary>
    public const float HexRadius = 1.0f;

    /// <summary>Square root of three, precomputed for the layout maths.</summary>
    private const float Sqrt3 = 1.7320508f;

    /// <summary>
    /// Converts an axial coordinate to the world position of the tile centre.
    /// </summary>
    /// <param name="coord">The tile coordinate.</param>
    /// <returns>The world position on the XZ plane.</returns>
    public static Vector3 ToWorld(HexCoord coord)
    {
        float x = HexRadius * 1.5f * coord.Q;
        float z = HexRadius * ((Sqrt3 * 0.5f * coord.Q) + (Sqrt3 * coord.R));
        return new Vector3(x, 0f, z);
    }

    /// <summary>
    /// Converts a world position to the coordinate of the tile under it. The Y
    /// component is ignored.
    /// </summary>
    /// <param name="position">A world position on or above the board.</param>
    /// <returns>The coordinate of the nearest tile.</returns>
    public static HexCoord FromWorld(Vector3 position)
    {
        float q = (2f / 3f) * position.x / HexRadius;
        float r = ((-1f / 3f * position.x) + (Sqrt3 / 3f * position.z)) / HexRadius;
        return HexCoord.Round(q, r);
    }

    /// <summary>
    /// Returns the world position of one corner of a tile.
    /// </summary>
    /// <param name="coord">The tile coordinate.</param>
    /// <param name="corner">Corner index from 0 to 5.</param>
    /// <returns>The world position of that corner.</returns>
    public static Vector3 CornerPosition(HexCoord coord, int corner)
    {
        float angle = Mathf.Deg2Rad * 60f * corner;
        Vector3 center = ToWorld(coord);
        return new Vector3(
            center.x + (HexRadius * Mathf.Cos(angle)),
            center.y,
            center.z + (HexRadius * Mathf.Sin(angle)));
    }
}
