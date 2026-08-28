using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class MinMaxLengthTests
{
    [Fact]
    public void None_IsNone()
    {
        Assert.True(MinMaxLength.None.IsNone);
        Assert.False(MinMaxLength.None.IsFixed);
    }

    [Fact]
    public void Fixed_StoresNonNegativeValue()
    {
        var m = MinMaxLength.Fixed(80f);
        Assert.True(m.IsFixed);
        Assert.Equal(80f, m.Value);
    }

    [Fact]
    public void Fixed_Negative_BecomesZero()
    {
        Assert.Equal(0f, MinMaxLength.Fixed(-5f).Value);
    }

    [Theory]
    [InlineData("None")]
    [InlineData("none")]
    [InlineData("")]
    public void Parse_NoneTokens(string text)
    {
        Assert.True(MinMaxLength.Parse(text).IsNone);
    }

    [Fact]
    public void Parse_Number_IsFixed()
    {
        var m = MinMaxLength.Parse("120.5");
        Assert.True(m.IsFixed);
        Assert.Equal(120.5f, m.Value);
    }

    [Fact]
    public void Parse_Invalid_Throws()
    {
        Assert.Throws<FormatException>(() => MinMaxLength.Parse("Auto"));
    }

    [Fact]
    public void ToString_RoundTripsFixed()
    {
        var s = MinMaxLength.Fixed(12f).ToString();
        Assert.Equal("12", s);
        Assert.Equal(MinMaxLength.Fixed(12f), MinMaxLength.Parse(s));
    }
}
