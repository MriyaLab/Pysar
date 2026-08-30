using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.Styles;

/// <summary>
/// Imperative equivalent of <see cref="BusinessReportWithStyleXaml"/> using Styles/Styles.rxaml.
/// </summary>
public class BusinessReportWithStyle
{
    public Report Build()
    {
        var resources = ReportResources.LoadFile(
            Path.Combine(AppContext.BaseDirectory, "Styles", "Styles.rxaml"));

        var report = new Report
        {
            BackgroundColor = (Color)resources["LightGray"],
            BorderColor = (Color)resources["Accent"],
            BorderLineStyle = BorderLineStyle.Solid,
            BorderThickness = new Thickness(10),
            PageFormat = new PageFormat
            {
                Margin = new Thickness(50, 0),
                Size = PageSize.A4
            }
        };
        MergeResources(report.Resources, resources);

        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            IsClippedToBounds = false,
            RowDefinitions =
            [
                new RowDefinition(GridLength.Fixed(250)),
                new RowDefinition(GridLength.Fixed(150)),
                new RowDefinition(GridLength.Star())
            ]
        };

        var logo = new Image
        {
            Size = new Size(150, 150),
            Source = new FileImageSource("Images/travel.svg"),
            HorizontalAlignment = Alignment.Start,
            VerticalAlignment = Alignment.Center
        };

        var banner = new Frame
        {
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(-50, 0),
            Padding = new Thickness(50, 0),
            BackgroundColor = (Color)resources["Accent"],
            IsClippedToBounds = false
        };
        banner.AddElement(new Text { Content = "BUSINESS STYLED REPORT" }
            .WithSize(350, SizeLength.Fill)
            .WithStyle(report.Resources, "H1"));

        var stack = new StackPanel
        {
            Orientation = StackOrientation.Vertical,
            Size = new Size(SizeLength.Fill, SizeLength.Fill),
            Margin = new Thickness(0, 50),
            Spacing = 5
        };
        stack.AddElements(
        [
            new Text { Content = "YOUR COMPANY'S NAME" }
                .WithStyle(report.Resources, "H2")
                .WithMargin(0, 0, 0, 10),
            new Text { Content = "PREPARED FOR:" }
                .WithStyle(report.Resources, "H3")
                .WithMargin(0, 20, 0, 10),
            new Text { Content = "John Smith" },
            new Text { Content = "Chief Executive Officer" },
            new Text { Content = "PREPARED BY:" }
                .WithStyle(report.Resources, "H3")
                .WithMargin(0, 20, 0, 10),
            new Text { Content = "Adam Brown" },
            new Text { Content = "Chief Information Officer" }
        ]);

        grid.AddElement(logo, 0, 0);
        grid.AddElement(banner, 1, 0);
        grid.AddElement(stack, 2, 0);

        var pageHeader = new PageHeaderBand { IsClippedToBounds = false };
        pageHeader.AddElement(grid);
        report.Bands.Add(pageHeader);

        return report.Build();
    }

    private static void MergeResources(ResourceDictionary target, ResourceDictionary source)
    {
        foreach (var entry in source)
            target[entry.Key] = entry.Value;
    }
}
