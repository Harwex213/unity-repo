using System.Collections.Generic;

/// <summary>
/// Static helpers that enumerate hexagonal areas. The island is a hexagonal
/// block of tiles around the centre, so a radius is all these helpers need.
/// </summary>
public static class HexGrid
{
    /// <summary>
    /// Counts the tiles of a hexagonal area of the given radius.
    /// </summary>
    /// <param name="radius">Radius in steps. Radius 0 is a single tile.</param>
    /// <returns>The tile count, which is 3*r*r + 3*r + 1.</returns>
    public static int AreaCount(int radius)
    {
        if (radius < 0)
        {
            return 0;
        }

        return (3 * radius * radius) + (3 * radius) + 1;
    }

    /// <summary>
    /// Walks every tile of a hexagonal area around the centre. The order is
    /// stable: q grows first, then r, so two runs always produce the same list.
    /// </summary>
    /// <param name="radius">Radius in steps.</param>
    /// <returns>Every coordinate whose distance to the centre is at most the radius.</returns>
    public static IEnumerable<HexCoord> Area(int radius)
    {
        return Area(HexCoord.Zero, radius);
    }

    /// <summary>
    /// Walks every tile of a hexagonal area around the given centre.
    /// </summary>
    /// <param name="center">Centre of the area.</param>
    /// <param name="radius">Radius in steps.</param>
    /// <returns>Every coordinate whose distance to the centre is at most the radius.</returns>
    public static IEnumerable<HexCoord> Area(HexCoord center, int radius)
    {
        for (int q = -radius; q <= radius; q++)
        {
            int rMin = System.Math.Max(-radius, -q - radius);
            int rMax = System.Math.Min(radius, -q + radius);
            for (int r = rMin; r <= rMax; r++)
            {
                yield return new HexCoord(center.Q + q, center.R + r);
            }
        }
    }

    /// <summary>
    /// Walks the tiles that sit exactly at the given distance from the centre.
    /// </summary>
    /// <param name="center">Centre of the ring.</param>
    /// <param name="radius">Distance from the centre.</param>
    /// <returns>The coordinates of the ring, in clockwise order.</returns>
    public static IEnumerable<HexCoord> Ring(HexCoord center, int radius)
    {
        if (radius <= 0)
        {
            yield return center;
            yield break;
        }

        HexCoord current = center;
        for (int i = 0; i < radius; i++)
        {
            current = current + HexCoord.Direction(4);
        }

        for (int direction = 0; direction < HexCoord.DirectionCount; direction++)
        {
            for (int step = 0; step < radius; step++)
            {
                yield return current;
                current = current + HexCoord.Direction(direction);
            }
        }
    }

    /// <summary>
    /// Tells whether a coordinate lies inside a hexagonal area around the centre.
    /// </summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <param name="radius">Radius of the area.</param>
    /// <returns>True when the coordinate is inside the area.</returns>
    public static bool IsInside(HexCoord coord, int radius)
    {
        return HexCoord.Distance(coord, HexCoord.Zero) <= radius;
    }

    /// <summary>
    /// Tells whether a coordinate sits on the outer border of a hexagonal area.
    /// </summary>
    /// <param name="coord">The coordinate to test.</param>
    /// <param name="radius">Radius of the area.</param>
    /// <returns>True when the coordinate is exactly the radius away from the centre.</returns>
    public static bool IsBorder(HexCoord coord, int radius)
    {
        return HexCoord.Distance(coord, HexCoord.Zero) == radius;
    }
}
