using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

/// <summary>
/// Tests of the hexagon maths: neighbours, distance, area enumeration and the
/// conversion between axial coordinates and world positions.
/// </summary>
public class HexMathTests
{
    /// <summary>The three cube coordinates of any hexagon must sum to zero.</summary>
    [Test]
    public void CubeCoordinates_SumToZero()
    {
        var coord = new HexCoord(2, -3);
        Assert.AreEqual(1, coord.S);
        Assert.AreEqual(0, coord.Q + coord.R + coord.S);
    }

    /// <summary>Every hexagon has six neighbours and each of them is one step away.</summary>
    [Test]
    public void Neighbors_AreSixAndOneStepAway()
    {
        var center = new HexCoord(1, -1);
        HexCoord[] neighbors = center.GetNeighbors();

        Assert.AreEqual(6, neighbors.Length);
        var unique = new HashSet<HexCoord>(neighbors);
        Assert.AreEqual(6, unique.Count);

        for (int i = 0; i < neighbors.Length; i++)
        {
            Assert.AreEqual(1, HexCoord.Distance(center, neighbors[i]), "Neighbor " + i + " is not adjacent.");
            Assert.IsTrue(center.IsNeighborOf(neighbors[i]));
        }
    }

    /// <summary>Distance counts hexagon steps, not squares.</summary>
    [Test]
    public void Distance_MatchesKnownValues()
    {
        Assert.AreEqual(0, HexCoord.Distance(HexCoord.Zero, HexCoord.Zero));
        Assert.AreEqual(1, HexCoord.Distance(HexCoord.Zero, new HexCoord(1, 0)));
        Assert.AreEqual(2, HexCoord.Distance(HexCoord.Zero, new HexCoord(2, 0)));
        Assert.AreEqual(3, HexCoord.Distance(HexCoord.Zero, new HexCoord(-3, 1)));
        Assert.AreEqual(4, HexCoord.Distance(new HexCoord(-2, 0), new HexCoord(2, 0)));
        Assert.AreEqual(2, HexCoord.Distance(new HexCoord(1, -2), new HexCoord(0, 0)));
    }

    /// <summary>A hexagonal area of radius 3 holds exactly 37 tiles.</summary>
    [Test]
    public void Area_OfRadiusThree_Holds37Tiles()
    {
        Assert.AreEqual(37, HexGrid.AreaCount(3));

        var tiles = new List<HexCoord>(HexGrid.Area(3));
        Assert.AreEqual(37, tiles.Count);

        var unique = new HashSet<HexCoord>(tiles);
        Assert.AreEqual(37, unique.Count, "The area must not repeat a coordinate.");

        for (int i = 0; i < tiles.Count; i++)
        {
            Assert.LessOrEqual(HexCoord.Distance(tiles[i], HexCoord.Zero), 3);
        }
    }

    /// <summary>Smaller areas match the closed form as well.</summary>
    [Test]
    public void Area_Counts_MatchClosedForm()
    {
        Assert.AreEqual(1, HexGrid.AreaCount(0));
        Assert.AreEqual(7, HexGrid.AreaCount(1));
        Assert.AreEqual(19, HexGrid.AreaCount(2));
    }

    /// <summary>The ring of radius 3 holds 18 tiles and every one of them is a border tile.</summary>
    [Test]
    public void Ring_OfRadiusThree_Holds18BorderTiles()
    {
        var ring = new List<HexCoord>(HexGrid.Ring(HexCoord.Zero, 3));
        Assert.AreEqual(18, ring.Count);

        for (int i = 0; i < ring.Count; i++)
        {
            Assert.IsTrue(HexGrid.IsBorder(ring[i], 3), ring[i] + " should sit on the border.");
        }
    }

    /// <summary>Converting a tile to a world position and back returns the same tile.</summary>
    [Test]
    public void WorldConversion_RoundTrips_ForEveryIslandTile()
    {
        foreach (HexCoord coord in HexGrid.Area(3))
        {
            Vector3 world = HexLayout.ToWorld(coord);
            HexCoord back = HexLayout.FromWorld(world);
            Assert.AreEqual(coord, back, "Round trip failed for " + coord + ".");
        }
    }

    /// <summary>A point close to a tile centre still resolves to that tile.</summary>
    [Test]
    public void WorldConversion_SnapsNearbyPointsToTheSameTile()
    {
        foreach (HexCoord coord in HexGrid.Area(2))
        {
            Vector3 world = HexLayout.ToWorld(coord);
            HexCoord back = HexLayout.FromWorld(world + new Vector3(0.2f, 5f, -0.15f));
            Assert.AreEqual(coord, back, "Nearby point missed " + coord + ".");
        }
    }

    /// <summary>The centre of the island sits at the world origin.</summary>
    [Test]
    public void WorldConversion_PlacesTheCenterAtTheOrigin()
    {
        Vector3 world = HexLayout.ToWorld(HexCoord.Zero);
        Assert.AreEqual(0f, world.x, 0.0001f);
        Assert.AreEqual(0f, world.y, 0.0001f);
        Assert.AreEqual(0f, world.z, 0.0001f);
    }

    /// <summary>Two tiles a step apart sit one hexagon width apart in the world.</summary>
    [Test]
    public void WorldConversion_KeepsNeighborsOneHexagonApart()
    {
        Vector3 center = HexLayout.ToWorld(HexCoord.Zero);
        foreach (HexCoord neighbor in HexCoord.Zero.GetNeighbors())
        {
            float distance = Vector3.Distance(center, HexLayout.ToWorld(neighbor));
            Assert.AreEqual(HexLayout.HexRadius * 1.7320508f, distance, 0.001f);
        }
    }
}
