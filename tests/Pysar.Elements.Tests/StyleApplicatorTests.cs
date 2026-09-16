using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Elements.Tests;

public class StyleApplicatorTests
{
    [Fact]
    public void Apply_SetsTypedAndStringSetters()
    {
        var text = new Text();
        var style = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontSize), Value = "24" },
                new Setter { Member = nameof(Text.FontStyle), Value = "Bold" },
                new Setter { Member = nameof(Text.FontColor), Value = "#3E4351" },
            }
        };

        StyleApplicator.Apply(text, style);

        Assert.Equal(24f, text.FontSize);
        Assert.Equal(FontStyle.Bold, text.FontStyle);
        Assert.Equal(Color.FromHex("#3E4351"), text.FontColor);
    }

    [Fact]
    public void Apply_SetsPreTypedColor()
    {
        var text = new Text();
        var style = new Style
        {
            Setters =
            {
                new Setter { Member = nameof(Text.FontColor), Value = Color.FromHex("#3E4351") },
            }
        };

        StyleApplicator.Apply(text, style);

        Assert.Equal(Color.FromHex("#3E4351"), text.FontColor);
    }

    [Fact]
    public void Apply_TargetTypeMismatch_Throws()
    {
        var text = new Text();
        var style = new Style
        {
            TargetType = typeof(Image),
            Setters =
            {
                new Setter { Member = nameof(Text.FontSize), Value = "12" },
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StyleApplicator.Apply(text, style));

        Assert.Contains(nameof(Image), ex.Message);
        Assert.Contains(nameof(Text), ex.Message);
    }

    [Fact]
    public void Apply_MissingProperty_Throws()
    {
        var text = new Text();
        var style = new Style
        {
            Setters =
            {
                new Setter { Member = "DoesNotExist", Value = "x" },
            }
        };

        var ex = Assert.Throws<InvalidOperationException>(() =>
            StyleApplicator.Apply(text, style));

        Assert.Contains("DoesNotExist", ex.Message);
    }
}
