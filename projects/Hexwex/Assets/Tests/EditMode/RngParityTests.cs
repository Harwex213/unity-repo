using System;
using NUnit.Framework;

namespace Hexwex.Core.Tests
{
    /// <summary>
    /// The generator must match the prototype bit for bit: a nickname is the world
    /// seed. The expected numbers were produced by running <c>hashSeed</c> and
    /// <c>createRng</c> of <c>core/rng.ts</c> in a JavaScript engine; each draw is
    /// <c>Math.floor(rng() * 2 ** 32)</c>.
    /// </summary>
    public sealed class RngParityTests
    {
        [TestCase("Mom010", 1569757489u, new[] { 1446771559u, 860635108u, 1834935247u, 3962024992u, 190591978u })]
        [TestCase("", 2246822507u, new[] { 3793716294u, 780271188u, 473065545u, 1502093018u, 1777963836u })]
        [TestCase("Mom010:carribean", 923961371u, new[] { 2923136060u, 3300034722u, 2910521873u, 1965549694u, 3042297887u })]
        [TestCase("Mom010:tax:1:human", 2068375005u, new[] { 488343153u, 310540771u, 634969190u, 2513079816u, 3171011644u })]
        [TestCase("Остров", 1497750133u, new[] { 2563152914u, 1681843167u, 712373939u, 2931172662u, 2037372870u })]
        public void SeedAndDrawsMatchThePrototype(string text, uint expectedSeed, uint[] expectedDraws)
        {
            Assert.AreEqual(expectedSeed, Rng.HashSeed(text));

            Rng rng = new Rng(expectedSeed);
            foreach (uint expected in expectedDraws)
            {
                Assert.AreEqual(expected, (uint)Math.Floor(rng.Next() * 4294967296.0));
            }
        }

        [TestCase(2.5, 3)]
        [TestCase(0.5, 1)]
        [TestCase(-0.5, 0)]
        [TestCase(-1.5, -1)]
        [TestCase(4.49, 4)]
        public void RoundFollowsJavaScript(double value, int expected)
        {
            Assert.AreEqual(expected, JsMath.Round(value));
        }

        [Test]
        public void AreaListsTheCentreFirstAndKeepsRingOrder()
        {
            var area = HexMath.Area(1);

            Assert.AreEqual(7, area.Count);
            Assert.AreEqual(new Axial(0, 0), area[0]);
            // The stable sort keeps the q-then-r scan order inside a ring.
            Assert.AreEqual(new Axial(-1, 0), area[1]);
            Assert.AreEqual(new Axial(-1, 1), area[2]);
            Assert.AreEqual(new Axial(1, 0), area[6]);
        }
    }
}
