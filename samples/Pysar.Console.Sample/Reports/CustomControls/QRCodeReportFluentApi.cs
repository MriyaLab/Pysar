using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.CustomControls;

/// <summary>Fluent-API twin of <see cref="QRCodeReport"/>.</summary>
public class QRCodeReportFluentApi
{
    private const string TargetUrl = "https://mriyalab.com/pysar";

    public Report Build()
    {
        var report = new Report
        {
            PageFormat = new PageFormat { Size = PageSize.A4, Orientation = Orientation.Portrait }
        };

        var header = new ReportHeaderBand { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        header.AddElement(new Grid()
            .WithSize(SizeLength.Auto, SizeLength.Auto)
            .WithHorizontalAlignment(Alignment.Center)
            .WithRowDefinitions("Auto, Auto, Auto, Auto")
            .WithRowSpacing(10)
            .AddElement(Label("Custom Control: QR Code", 22, FontStyle.Bold)
                .WithHorizontalAlignment(Alignment.Center), 0, 0)
            .AddElement(Label("A custom element painted by a custom IElementDrawer.", 12)
                .WithHorizontalAlignment(Alignment.Center), 1, 0)
            .AddElement(new QRCode(TargetUrl)
                .WithSize(140, 140)
                .WithMargin(25)
                .WithHorizontalAlignment(Alignment.Center), 2, 0)
            .AddElement(Label("Scan to open", 12)
                .WithHorizontalAlignment(Alignment.Center), 3, 0));
        report.Bands.Add(header);

        var footer = new PageFooterBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-40, 0, -40, -30),
            Padding = new Thickness(25, 0)
        };
        footer.AddElement(new StackPanel()
            .WithOrientation(StackOrientation.Horizontal)
            .WithSize(Size.Auto)
            .WithHorizontalAlignment(Alignment.Center)
            .WithVerticalAlignment(Alignment.Center)
            .AddElements(
            [
                new QRCode(TargetUrl)
                    .WithSize(20, 20)
                    .WithVerticalAlignment(Alignment.Center),
                Label("Pysar Report", 10)
                    .WithPadding(3, 0)
                    .WithVerticalAlignment(Alignment.Center)
            ]));
        report.Bands.Add(footer);

        return report.Build();
    }

    private static Text Label(string content, float size, FontStyle style = FontStyle.Normal) =>
        new Text { Content = content }.WithFont("Kanit", size, Colors.Black, style);
}
