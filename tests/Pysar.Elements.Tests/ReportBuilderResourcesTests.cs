using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Xunit;

namespace Pysar.Elements.Tests;

public class ReportBuilderResourcesTests
{
    [Fact]
    public void WithResources_MergesKeysAndTypeStyles()
    {
        var dictionary = new ResourceDictionary();
        dictionary["Accent"] = Color.FromHex("#C0392B");
        dictionary[typeof(Text)] = new Style
        {
            TargetType = typeof(Text),
            Setters = { new Setter { Member = nameof(Text.FontSize), Value = "14" } }
        };

        var report = ReportBuilder.Create("t")
            .WithResources(dictionary)
            .Build();

        Assert.Equal(Color.FromHex("#C0392B"), report.Resources["Accent"]);
        Assert.True(report.Resources.ContainsKey(typeof(Text)));
        Assert.IsType<Style>(report.Resources[typeof(Text)]);
    }

    [Fact]
    public void Configure_MutatesReportBeforeBuild()
    {
        var report = ReportBuilder.Create("t")
            .Configure(r =>
            {
                r.BackgroundColor = Color.FromHex("#F0F1F5");
                r.BorderColor = Color.FromHex("#C0392B");
                r.BorderLineStyle = BorderLineStyle.Solid;
                r.BorderThickness = new Thickness(10);
            })
            .Build();

        Assert.Equal(Color.FromHex("#F0F1F5"), report.BackgroundColor);
        Assert.Equal(Color.FromHex("#C0392B"), report.BorderColor);
        Assert.Equal(BorderLineStyle.Solid, report.BorderLineStyle);
        Assert.Equal(new Thickness(10), report.BorderThickness);
    }
}
