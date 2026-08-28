using Pysar.Core;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class SizeConstraintTests
{
    [Fact]
    public void SizeConstraint_None_BothNone()
    {
        Assert.True(SizeConstraint.None.Width.IsNone);
        Assert.True(SizeConstraint.None.Height.IsNone);
    }

    [Fact]
    public void ValueConverter_ParsesMinMaxLength()
    {
        Assert.True(ValueConverter.IsConvertible(typeof(MinMaxLength)));
        var v = (MinMaxLength)ValueConverter.Convert("40", typeof(MinMaxLength))!;
        Assert.Equal(MinMaxLength.Fixed(40), v);
    }

    [Fact]
    public void ValueConverter_ParsesSizeConstraint()
    {
        Assert.True(ValueConverter.IsConvertible(typeof(SizeConstraint)));
        var v = (SizeConstraint)ValueConverter.Convert("10,20", typeof(SizeConstraint))!;
        Assert.Equal(MinMaxLength.Fixed(10), v.Width);
        Assert.Equal(MinMaxLength.Fixed(20), v.Height);
    }
}
