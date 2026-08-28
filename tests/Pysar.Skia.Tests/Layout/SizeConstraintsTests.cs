using Pysar.Core.Structs;
using Pysar.Skia.Layout;
using Xunit;

namespace Pysar.Skia.Tests.Layout;

public class SizeConstraintsTests
{
    [Fact]
    public void Clamp_None_Unchanged()
    {
        var (w, h) = SizeConstraints.Clamp(50, 60, SizeConstraint.None, SizeConstraint.None);
        Assert.Equal(50, w);
        Assert.Equal(60, h);
    }

    [Fact]
    public void Clamp_MinOnly()
    {
        var min = new SizeConstraint(MinMaxLength.Fixed(80), MinMaxLength.None);
        var (w, h) = SizeConstraints.Clamp(50, 10, min, SizeConstraint.None);
        Assert.Equal(80, w);
        Assert.Equal(10, h);
    }

    [Fact]
    public void Clamp_MaxOnly()
    {
        var max = new SizeConstraint(MinMaxLength.Fixed(40), MinMaxLength.Fixed(30));
        var (w, h) = SizeConstraints.Clamp(100, 100, SizeConstraint.None, max);
        Assert.Equal(40, w);
        Assert.Equal(30, h);
    }

    [Fact]
    public void Clamp_Both()
    {
        var min = new SizeConstraint(MinMaxLength.Fixed(10), MinMaxLength.Fixed(10));
        var max = new SizeConstraint(MinMaxLength.Fixed(50), MinMaxLength.Fixed(50));
        var (w, h) = SizeConstraints.Clamp(5, 80, min, max);
        Assert.Equal(10, w);
        Assert.Equal(50, h);
    }
}
