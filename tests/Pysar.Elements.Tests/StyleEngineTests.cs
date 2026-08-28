using Pysar.Core.Enums;
using Pysar.Elements;
using Xunit;

namespace Pysar.Elements.Tests;

public class StyleEngineTests
{
    [Fact]
    public void Build_AppliesExplicitStyle()
    {
        var h1 = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontSize), Value = "38" },
                new Setter { Member = nameof(Text.FontStyle), Value = "Bold" },
            }
        };

        var report = new Report();
        report.Resources["H1"] = h1;

        var text = new Text { Style = h1 };
        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);

        report.Build();

        Assert.Equal(38f, text.FontSize);
        Assert.Equal(FontStyle.Bold, text.FontStyle);
    }

    [Fact]
    public void Build_AppliesImplicitTargetTypeStyle_WhenStylePropertyNull()
    {
        var report = new Report();
        report.Resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.FontSize), Value = "14" } }
        };

        var text = new Text();
        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);

        report.Build();

        Assert.Equal(14f, text.FontSize);
    }

    [Fact]
    public void Build_AppliesImplicitThenExplicit_WhenStyleSet()
    {
        var report = new Report();
        report.Resources[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontFamily), Value = "Ubuntu" },
                new Setter { Member = nameof(Text.FontSize), Value = "14" },
            }
        };
        var h1 = new Style
        {
            TargetType = typeof(Text),
            Setters =
            {
                new Setter { Member = nameof(Text.FontSize), Value = "38" },
                new Setter { Member = nameof(Text.FontStyle), Value = "Bold" },
            }
        };
        report.Resources["H1"] = h1;

        var text = new Text { Style = h1 };
        var header = new PageHeaderBand();
        header.AddElement(text);
        report.Bands.Add(header);

        report.Build();

        Assert.Equal("Ubuntu", text.FontFamily);
        Assert.Equal(38f, text.FontSize);
        Assert.Equal(FontStyle.Bold, text.FontStyle);
    }
}
