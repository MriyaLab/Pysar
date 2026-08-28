using Pysar.Core.Enums;
using Pysar.Elements;
using Xunit;

namespace Pysar.Xaml.Tests;

public class FlattenedAttributeTests
{
    private const string Ns = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void Load_WidthHeightAndFontFacades()
    {
        var text = XamlTestHost.BuildElement<Text>(
            $"<Text {Ns} Width=\"Fill\" Height=\"60\" FontFamily=\"Ubuntu\" FontSize=\"9\" FontStyle=\"Bold\" FontColor=\"#444444\"/>");

        Assert.True(text.Size.Width.IsFill);
        Assert.Equal(60f, text.Size.Height.Value);
        Assert.Equal("Ubuntu", text.Font.Family);
        Assert.Equal(9f, text.Font.Size);
        Assert.Equal(FontStyle.Bold, text.Font.Style);
    }
}
