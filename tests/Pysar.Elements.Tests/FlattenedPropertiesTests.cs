using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class FlattenedPropertiesTests
{
    [Fact]
    public void WidthHeight_FacadeOverSize()
    {
        var t = new Text { Width = SizeLength.Fill, Height = SizeLength.Fixed(60) };

        Assert.True(t.Size.Width.IsFill);
        Assert.Equal(60f, t.Size.Height.Value);
        Assert.True(t.Width.IsFill);
        Assert.Equal(60f, t.Height.Value);
    }

    [Fact]
    public void FontFacades_OverFont()
    {
        var t = new Text
        {
            FontFamily = "Ubuntu",
            FontSize = 9,
            FontStyle = FontStyle.Bold,
            FontColor = Colors.Gray
        };

        Assert.Equal("Ubuntu", t.Font.Family);
        Assert.Equal(9f, t.Font.Size);
        Assert.Equal(FontStyle.Bold, t.Font.Style);
        Assert.Equal(Colors.Gray, t.Font.Color);
        Assert.Equal("Ubuntu", t.FontFamily);
        Assert.Equal(9f, t.FontSize);
    }
}
