using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class SizeConstraintNormalizeTests
{
    [Fact]
    public void Default_MinMax_AreNone()
    {
        var t = new Text();
        Assert.Equal(SizeConstraint.None, t.MinSize);
        Assert.Equal(SizeConstraint.None, t.MaxSize);
    }

    [Fact]
    public void MinWidth_Facade_WritesMinSize()
    {
        var t = new Text();
        t.MinWidth = MinMaxLength.Fixed(80);
        Assert.Equal(MinMaxLength.Fixed(80), t.MinSize.Width);
        Assert.True(t.MinSize.Height.IsNone);
    }

    [Fact]
    public void SettingMaxBelowMin_NormalizesMaxUpToMin()
    {
        var t = new Text();
        t.MinWidth = 100;
        t.MaxWidth = 40;
        Assert.Equal(MinMaxLength.Fixed(100), t.MinWidth);
        Assert.Equal(MinMaxLength.Fixed(100), t.MaxWidth);
    }

    [Fact]
    public void SettingMinAboveMax_NormalizesMaxUpToMin()
    {
        var t = new Text();
        t.MaxHeight = 50;
        t.MinHeight = 80;
        Assert.Equal(MinMaxLength.Fixed(80), t.MinHeight);
        Assert.Equal(MinMaxLength.Fixed(80), t.MaxHeight);
    }

    [Fact]
    public void WithMinWidth_Fluent()
    {
        var t = new Text().WithMinWidth(12f).WithMaxHeight(40f);
        Assert.Equal(MinMaxLength.Fixed(12), t.MinWidth);
        Assert.Equal(MinMaxLength.Fixed(40), t.MaxHeight);
    }
}
