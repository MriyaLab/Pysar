using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class CornerRadiusTests
{
    [Fact]
    public void Uniform_FillsEveryCorner()
    {
        var radius = new CornerRadius(12);

        Assert.Equal(12f, radius.TopLeft);
        Assert.Equal(12f, radius.TopRight);
        Assert.Equal(12f, radius.BottomLeft);
        Assert.Equal(12f, radius.BottomRight);
    }

    [Fact]
    public void FourValues_MapInMauiOrder()
    {
        var radius = new CornerRadius(5, 10, 20, 30);

        Assert.Equal(5f, radius.TopLeft);
        Assert.Equal(10f, radius.TopRight);
        Assert.Equal(20f, radius.BottomLeft);
        Assert.Equal(30f, radius.BottomRight);
    }

    [Fact]
    public void Zero_IsZero() => Assert.True(CornerRadius.Zero.IsZero);

    [Fact]
    public void Negative_IsZero() => Assert.True(new CornerRadius(-4).IsZero);

    [Fact]
    public void SinglePositiveCorner_IsNotZero()
        => Assert.False(new CornerRadius(0, 0, 0, 1).IsZero);
}
