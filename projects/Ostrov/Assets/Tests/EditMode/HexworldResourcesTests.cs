using NUnit.Framework;

/// <summary>
/// Tests of the resource bundle: arithmetic, the affordability check and the
/// clamp that keeps a stockpile out of the negative.
/// </summary>
public class HexworldResourcesTests
{
    /// <summary>Addition works amount by amount.</summary>
    [Test]
    public void Addition_AddsEveryAmount()
    {
        var a = new HexworldResources(1, 2, 3, 4, 5);
        var b = new HexworldResources(10, 20, 30, 40, 50);

        HexworldResources sum = a + b;

        Assert.AreEqual(11, sum.Food);
        Assert.AreEqual(22, sum.Wood);
        Assert.AreEqual(33, sum.Stone);
        Assert.AreEqual(44, sum.Culture);
        Assert.AreEqual(55, sum.Soldiers);
    }

    /// <summary>Subtraction works amount by amount and may go negative.</summary>
    [Test]
    public void Subtraction_MayGoNegative()
    {
        var a = new HexworldResources(1, 1, 1, 1, 1);
        var b = new HexworldResources(2, 0, 0, 0, 3);

        HexworldResources difference = a - b;

        Assert.AreEqual(-1, difference.Food);
        Assert.AreEqual(1, difference.Wood);
        Assert.AreEqual(-2, difference.Soldiers);
    }

    /// <summary>A stockpile covers a price when it holds enough of every kind.</summary>
    [Test]
    public void Covers_IsTrue_WhenEveryAmountIsEnough()
    {
        var stock = new HexworldResources(5, 5, 3, 0, 2);

        Assert.IsTrue(stock.Covers(HexworldResources.FromWood(3)));
        Assert.IsTrue(stock.Covers(new HexworldResources(0, 2, 1, 0, 0)));
        Assert.IsTrue(stock.Covers(stock));
    }

    /// <summary>A single missing amount is enough to refuse a price.</summary>
    [Test]
    public void Covers_IsFalse_WhenOneAmountIsShort()
    {
        var stock = new HexworldResources(5, 5, 3, 0, 2);

        Assert.IsFalse(stock.Covers(new HexworldResources(0, 4, 4, 0, 0)));
        Assert.IsFalse(stock.Covers(HexworldResources.FromCulture(1)));
        Assert.IsFalse(stock.Covers(HexworldResources.FromSoldiers(3)));
    }

    /// <summary>Clamping raises every negative amount to zero and leaves the rest alone.</summary>
    [Test]
    public void ClampedToZero_RaisesNegativeAmounts()
    {
        var value = new HexworldResources(-3, 2, -1, 0, 4);

        HexworldResources clamped = value.ClampedToZero();

        Assert.AreEqual(0, clamped.Food);
        Assert.AreEqual(2, clamped.Wood);
        Assert.AreEqual(0, clamped.Stone);
        Assert.AreEqual(0, clamped.Culture);
        Assert.AreEqual(4, clamped.Soldiers);
    }

    /// <summary>Reading an amount by kind matches the property of that kind.</summary>
    [Test]
    public void Get_ReadsTheAmountOfThatKind()
    {
        var value = new HexworldResources(1, 2, 3, 4, 5);

        Assert.AreEqual(1, value.Get(HexworldResourceType.Food));
        Assert.AreEqual(2, value.Get(HexworldResourceType.Wood));
        Assert.AreEqual(3, value.Get(HexworldResourceType.Stone));
        Assert.AreEqual(4, value.Get(HexworldResourceType.Culture));
        Assert.AreEqual(5, value.Get(HexworldResourceType.Soldiers));
        Assert.AreEqual(15, value.Total);
    }

    /// <summary>Replacing one amount leaves the other four untouched.</summary>
    [Test]
    public void With_ReplacesOneAmountOnly()
    {
        var value = new HexworldResources(1, 2, 3, 4, 5);

        HexworldResources changed = value.With(HexworldResourceType.Stone, 9);

        Assert.AreEqual(9, changed.Stone);
        Assert.AreEqual(1, changed.Food);
        Assert.AreEqual(5, changed.Soldiers);
    }

    /// <summary>Equality compares all five amounts.</summary>
    [Test]
    public void Equality_ComparesEveryAmount()
    {
        var a = new HexworldResources(1, 2, 3, 4, 5);
        var b = new HexworldResources(1, 2, 3, 4, 5);
        var c = new HexworldResources(1, 2, 3, 4, 6);

        Assert.IsTrue(a == b);
        Assert.IsFalse(a == c);
        Assert.IsTrue(a != c);
        Assert.AreEqual(a.GetHashCode(), b.GetHashCode());
        Assert.IsTrue(HexworldResources.Zero.IsZero);
    }
}
