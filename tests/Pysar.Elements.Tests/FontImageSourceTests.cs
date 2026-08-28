using Pysar.Core.Structs;
using Xunit;

namespace Pysar.Elements.Tests;

public class FontImageSourceTests
{
    [Fact]
    public void Defaults_MatchMauiSubsetWithReportColor()
    {
        var source = new FontImageSource();

        Assert.Equal(string.Empty, source.Glyph);
        Assert.Equal(string.Empty, source.FontFamily);
        Assert.Equal(30f, source.Size);
        Assert.Equal(Colors.Black, source.Color);
    }

    [Fact]
    public void Clone_CopiesValuesIndependently()
    {
        var source = new FontImageSource
        {
            Glyph = "A",
            FontFamily = "FontAwesomeSolid",
            Size = 44,
            Color = Colors.Red
        };

        var clone = source.Clone();
        clone.Glyph = "B";
        clone.Color = Colors.Green;

        Assert.Equal("A", source.Glyph);
        Assert.Equal(Colors.Red, source.Color);
        Assert.Equal("B", clone.Glyph);
        Assert.Equal(Colors.Green, clone.Color);
        Assert.Equal("FontAwesomeSolid", clone.FontFamily);
        Assert.Equal(44f, clone.Size);
    }

    [Fact]
    public void Clone_CopiesPendingBindingsIndependently()
    {
        var source = new FontImageSource();
        source.SetBinding(FontImageSource.GlyphProperty, "StatusIcon");

        var clone = source.Clone();
        new Pysar.Binding.BindingEngine().ResolveBindings(clone, new { StatusIcon = "X" });

        Assert.Equal("X", clone.Glyph);
        Assert.Equal(string.Empty, source.Glyph);
    }

    [Fact]
    public void ImageClone_DoesNotShareFontImageSource()
    {
        var image = new Image
        {
            Source = new FontImageSource { Glyph = "A" }
        };

        var clone = (Image)image.Clone();
        ((FontImageSource)clone.Source!).Glyph = "B";

        Assert.Equal("A", ((FontImageSource)image.Source!).Glyph);
        Assert.NotSame(image.Source, clone.Source);
    }

    [Fact]
    public void ImageSourceClone_ReturnsIndependentFontImageSource()
    {
        ImageSource source = new FontImageSource { Glyph = "A" };

        var clone = source.Clone();
        ((FontImageSource)clone).Glyph = "B";

        Assert.Equal("A", ((FontImageSource)source).Glyph);
        Assert.IsType<FontImageSource>(clone);
        Assert.NotSame(source, clone);
    }
}
