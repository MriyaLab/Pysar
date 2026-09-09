using Pysar.Core.Abstractions;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class FrameCornerRadiusTests
{
    [Fact]
    public void Default_IsZero() => Assert.Equal(CornerRadius.Zero, new Frame().CornerRadius);

    [Fact]
    public void Frame_IsRoundedElement() => Assert.IsAssignableFrom<IRoundedElement>(new Frame());

    [Fact]
    public void WithCornerRadius_Uniform_SetsAllCorners()
    {
        var frame = new Frame();

        Assert.Same(frame, frame.WithCornerRadius(8));
        Assert.Equal(new CornerRadius(8), frame.CornerRadius);
    }

    [Fact]
    public void WithCornerRadius_PerCorner_SetsInMauiOrder()
    {
        var frame = new Frame().WithCornerRadius(5, 10, 20, 30);

        Assert.Equal(new CornerRadius(5, 10, 20, 30), frame.CornerRadius);
    }

    [Fact]
    public void Clone_CarriesCornerRadius()
    {
        var frame = new Frame { CornerRadius = new CornerRadius(5, 10, 20, 30) };

        var clone = (Frame)frame.Clone();

        Assert.Equal(new CornerRadius(5, 10, 20, 30), clone.CornerRadius);
    }
}
