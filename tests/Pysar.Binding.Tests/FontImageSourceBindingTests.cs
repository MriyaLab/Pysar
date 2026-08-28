using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Binding.Tests;

public class FontImageSourceBindingTests
{
    [Fact]
    public void ResolveBindings_ImageSource_WritesGlyphAndColorFromContext()
    {
        var source = new FontImageSource();
        source.SetBinding(FontImageSource.GlyphProperty, "StatusIcon");
        source.SetBinding(FontImageSource.ColorProperty, "StatusColor");
        var image = new Image { Source = source };

        new BindingEngine().ResolveBindings(
            [image],
            new { StatusIcon = "\uf00c", StatusColor = Colors.Green });

        Assert.Equal("\uf00c", source.Glyph);
        Assert.Equal(Colors.Green, source.Color);
    }

    [Fact]
    public void ResolveBindings_ClonedRows_DoNotCrosstalk()
    {
        var template = new Image
        {
            Source = new FontImageSource()
        };
        ((FontImageSource)template.Source!).SetBinding(FontImageSource.GlyphProperty, "StatusIcon");

        var first = (Image)template.Clone();
        var second = (Image)template.Clone();
        first.DataContext = new { StatusIcon = "A" };
        second.DataContext = new { StatusIcon = "B" };

        new BindingEngine().ResolveBindings([first, second]);

        Assert.Equal("A", ((FontImageSource)first.Source!).Glyph);
        Assert.Equal("B", ((FontImageSource)second.Source!).Glyph);
    }

    [Fact]
    public void ResolveBindings_DoesNotResolveParentElementBindings()
    {
        var parent = new Frame();
        parent.SetBinding(Frame.BackgroundColorProperty, "StatusColor");
        var source = new FontImageSource();
        source.SetBinding(FontImageSource.GlyphProperty, "StatusIcon");
        var image = new Image { Source = source, ParentElement = parent };

        new BindingEngine().ResolveBindings(
            [image],
            new { StatusIcon = "A", StatusColor = Colors.Green });

        Assert.Equal("A", source.Glyph);
        Assert.Equal(Colors.Transparent, parent.BackgroundColor);
    }
}
