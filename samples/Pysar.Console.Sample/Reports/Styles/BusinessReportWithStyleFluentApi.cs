using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.Styles;

/// <summary>
/// Fluent equivalent of <see cref="BusinessReportWithStyleXaml"/> using Styles/Styles.rxaml.
/// </summary>
public class BusinessReportWithStyleFluentApi
{
    public Report Build()
    {
        var resources = ReportResources.LoadFile(
            Path.Combine(AppContext.BaseDirectory, "Styles", "Styles.rxaml"));

        return ReportBuilder.Create("Business Styled Report")
            .WithResources(resources)
            .Configure(r =>
            {
                r.BackgroundColor = (Color)resources["LightGray"];
                r.BorderColor = (Color)resources["Accent"];
                r.BorderLineStyle = BorderLineStyle.Solid;
                r.BorderThickness = new Thickness(10);
            })
            .WithPageFormat(new PageFormat
            {
                Margin = new Thickness(50, 0),
                Size = PageSize.A4
            })
            .WithPageHeader(header => header
                .WithIsClippedToBounds(false)
                .AddElement(new Grid()
                    .WithSize(SizeLength.Fill, SizeLength.Fill)
                    .WithIsClippedToBounds(false)
                    .WithRowDefinitions("250, 150, *")
                    .AddElement(new Image { Source = new FileImageSource("Images/travel.svg") }
                        .WithSize(150, 150)
                        .WithHorizontalAlignment(Alignment.Start)
                        .WithVerticalAlignment(Alignment.Center), row: 0, column: 0)
                    .AddElement(new Frame()
                        .WithSize(SizeLength.Fill, SizeLength.Fill)
                        .WithMargin(-50, 0)
                        .WithPadding(50, 0)
                        .WithBackgroundColor((Color)resources["Accent"])
                        .WithIsClippedToBounds(false)
                        .AddElement(new Text { Content = "BUSINESS STYLED REPORT" }
                            .WithSize(350, SizeLength.Fill)
                            .WithStyle(resources, "H1")), row: 1, column: 0)
                    .AddElement(new StackPanel()
                        .WithOrientation(StackOrientation.Vertical)
                        .WithSize(SizeLength.Fill, SizeLength.Fill)
                        .WithMargin(0, 50)
                        .WithSpacing(5)
                        .AddElements(
                        [
                            new Text { Content = "YOUR COMPANY'S NAME" }
                                .WithStyle(resources, "H2")
                                .WithMargin(0, 0, 0, 10),
                            new Text { Content = "PREPARED FOR:" }
                                .WithStyle(resources, "H3")
                                .WithMargin(0, 20, 0, 10),
                            new Text { Content = "John Smith" },
                            new Text { Content = "Chief Executive Officer" },
                            new Text { Content = "PREPARED BY:" }
                                .WithStyle(resources, "H3")
                                .WithMargin(0, 20, 0, 10),
                            new Text { Content = "Adam Brown" },
                            new Text { Content = "Chief Information Officer" }
                        ]), row: 2, column: 0)))
            .Build();
    }
}
