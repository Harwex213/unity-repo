using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>
    /// The planet must match the prototype cell for cell: a nickname is the world
    /// seed. <c>WorldReference.txt</c> holds what <c>core/world-gen.ts</c> itself
    /// produced in a browser for two seeds, and every field of it is compared here.
    /// </summary>
    public sealed class WorldParityTests
    {
        private const string ReferencePath = "Assets/Tests/EditMode/WorldReference.txt";
        private static readonly string[] PlayerIds = { "human", "carribean", "blue", "orange" };

        private static Dictionary<string, string> _reference;

        private static string Reference(int world, string field)
        {
            if (_reference == null)
            {
                _reference = File.ReadAllLines(ReferencePath)
                    .Where(line => line.Length > 0 && line[0] != '#')
                    .ToDictionary(line => line.Substring(0, line.IndexOf('=')), line => line.Substring(line.IndexOf('=') + 1));
            }

            return _reference[world + "." + field];
        }

        private static World Create(int index, out Dictionary<string, string> placement)
        {
            return WorldGen.Create(Reference(index, "seed"), PlayerIds, out placement);
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TheSphereHasThePrototypesCells(int index)
        {
            World world = Create(index, out _);

            Assert.AreEqual(int.Parse(Reference(index, "count")), world.Cells.Length);
            Assert.AreEqual(Reference(index, "sides"), string.Concat(world.Cells.Select(cell => cell.Polygon.Length)));
            Assert.AreEqual(
                Reference(index, "neighbors"),
                string.Join("|", world.Cells.Select(cell => string.Join(".", cell.Neighbors))));

            AssertPoint(Reference(index, "center7"), world.Cells[7].Center, 1e-15);
            AssertPoint(Reference(index, "center300"), world.Cells[300].Center, 1e-15);

            string[] corners = Reference(index, "polygon300").Split(';');
            Assert.AreEqual(corners.Length, world.Cells[300].Polygon.Length);
            for (int corner = 0; corner < corners.Length; corner += 1)
            {
                AssertPoint(corners[corner], world.Cells[300].Polygon[corner], 1e-9);
            }
        }

        [TestCase(0)]
        [TestCase(1)]
        public void TheSameSeedGrowsThePrototypesPlanet(int index)
        {
            World world = Create(index, out Dictionary<string, string> placement);

            Assert.AreEqual(Reference(index, "placement"), string.Join(",", PlayerIds.Select(id => placement[id])));
            Assert.AreEqual(Reference(index, "boss"), world.BossCell.Id);
            Assert.AreEqual(Reference(index, "kinds"), string.Concat(world.Cells.Select(cell => char.ToLowerInvariant(cell.Kind.ToString()[0]))));
            Assert.AreEqual(Reference(index, "counts"), string.Concat(world.Cells.Select(cell => cell.IslandCount)));
            Assert.AreEqual(Reference(index, "biomes"), string.Concat(world.Cells.Select(cell => ((int)cell.Biome).ToString("x"))));
            Assert.AreEqual(Reference(index, "variants"), string.Concat(world.Cells.Select(cell => (char)('a' + cell.Variant))));
            Assert.AreEqual(
                Reference(index, "factions"),
                string.Concat(world.Cells.Select(cell => cell.Faction.HasValue ? ((int)cell.Faction.Value).ToString() : "-")));
            Assert.AreEqual(
                Reference(index, "revealed"),
                string.Join(",", world.Cells.Where(cell => cell.Revealed).Select(cell => cell.Id)));
        }

        [Test]
        public void EveryPlayerStartsOnAHomeCell()
        {
            GameSession session = new GameSession("Mom010");

            foreach (Player player in session.Players)
            {
                WorldCell home = session.World.Get(player.CellId);

                Assert.AreEqual(player.Id, home.OwnerId);
                Assert.AreEqual(CellKind.Island, home.Kind);
                Assert.IsTrue(home.Cleared);
                Assert.AreEqual(0, WorldRules.PendingIslands(home, player));
            }

            WorldCell lair = session.World.BossCell;
            Assert.AreEqual(FactionId.Helios, lair.Faction);
            Assert.AreEqual(BiomeId.Volcano, lair.Biome);
            Assert.IsNull(lair.OwnerId);
        }

        private static void AssertPoint(string expected, Vec3 actual, double tolerance)
        {
            double[] values = expected.Split(',').Select(value => double.Parse(value, CultureInfo.InvariantCulture)).ToArray();

            Assert.AreEqual(values[0], actual.X, tolerance);
            Assert.AreEqual(values[1], actual.Y, tolerance);
            Assert.AreEqual(values[2], actual.Z, tolerance);
        }
    }
}
