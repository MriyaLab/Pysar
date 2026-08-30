using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports.CustomControls;

/// <summary>
///     Sample report with a custom <see cref="QRCode"/> element. The same layout exists as
///     fluent (<c>QRCodeReportFluentApi</c>) and XAML (<c>QRCodeReportXaml</c>) variants.
/// </summary>
public class QRCodeReport
{
    private const string TargetUrl = "https://mriyalab.com/pysar";

    public Report Build()
    {
        var report = new Report
        {
            PageFormat = new PageFormat { Size = PageSize.A4, Orientation = Orientation.Portrait }
        };

        var header = new ReportHeaderBand { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        var content = new Grid
        {
            Size = new Size(SizeLength.Auto, SizeLength.Auto),
            HorizontalAlignment = Alignment.Center,
            RowDefinitions = ParseRows("Auto, Auto, Auto, Auto"),
            RowSpacing = 10
        };
        var title = Label("Custom Control: QR Code", 22, FontStyle.Bold);
        title.HorizontalAlignment = Alignment.Center;
        content.AddElement(title, 0, 0);

        var description = Label("A custom element painted by a custom IElementDrawer.", 12);
        description.HorizontalAlignment = Alignment.Center;
        content.AddElement(description, 1, 0);

        content.AddElement(new QRCode(TargetUrl)
        {
            Size = new Size(140, 140),
            Margin = new Thickness(25),
            HorizontalAlignment = Alignment.Center
        }, 2, 0);

        var hint = Label("Scan to open", 12);
        hint.HorizontalAlignment = Alignment.Center;
        content.AddElement(hint, 3, 0);
        header.AddElement(content);
        report.Bands.Add(header);
        report.Bands.Add(BuildPageFooter());
        return report.Build();
    }

    private static PageFooterBand BuildPageFooter()
    {
        var stack = new StackPanel
        {
            Orientation = StackOrientation.Horizontal,
            Size = Size.Auto,
            HorizontalAlignment = Alignment.Center,
            VerticalAlignment = Alignment.Center
        };
        var caption = Label("Pysar Report", 10);
        caption.Padding = new Thickness(3, 0);
        caption.VerticalAlignment = Alignment.Center;
        stack.AddElements(
        [
            new QRCode(TargetUrl)
            {
                Size = new Size(20, 20),
                VerticalAlignment = Alignment.Center
            },
            caption
        ]);

        var footer = new PageFooterBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-40, 0, -40, -30),
            Padding = new Thickness(25, 0)
        };
        footer.AddElement(stack);
        return footer;
    }

    private static List<RowDefinition> ParseRows(string definitions) =>
        definitions.Split(',').Select(s => new RowDefinition(GridLength.Parse(s))).ToList();

    private static Text Label(string content, float size, FontStyle style = FontStyle.Normal) =>
        new()
        {
            Content = content,
            Font = new Font("Kanit", size, Colors.Black, style)
        };
}
