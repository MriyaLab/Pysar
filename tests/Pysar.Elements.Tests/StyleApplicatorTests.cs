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

    [Fact]
    public void WithStyle_LooksUpAppliesAndSetsStyleProperty()
    {
        var resources = new ResourceDictionary();
        resources["H2"] = new Style
        {
            Setters = { new Setter { Member = nameof(Text.FontSize), Value = "24" } }
        };

        var text = new Text().WithStyle(resources, "H2");

        Assert.Same(resources["H2"], text.Style);
        Assert.Equal(24f, text.FontSize);
    }

    [Fact]
    public void WithStyle_AppliesImplicitThenExplicit()
    {
        var resources = new ResourceDictionary();
        resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontFamily), Value = "Ubuntu" },
                new Setter { Member = nameof(Text.FontSize), Value = "14" },
            }
        };
        resources["H1"] = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontSize), Value = "38" },
                new Setter { Member = nameof(Text.FontStyle), Value = "Bold" },
            }
        };

        var text = new Text().WithStyle(resources, "H1");

        Assert.Equal("Ubuntu", text.FontFamily);
        Assert.Equal(38f, text.FontSize);
        Assert.Equal(FontStyle.Bold, text.FontStyle);
    }

    [Fact]
    public void WithStyle_ThrowsWhenKeyNotStyle()
    {
        var resources = new ResourceDictionary();
        resources["Accent"] = Color.FromHex("#C0392B");
        Assert.Throws<InvalidOperationException>(() => new Text().WithStyle(resources, "Accent"));
    }
}
