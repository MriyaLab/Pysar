using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;

namespace Pysar.Console.Sample.Reports;

public class BusinessReportFluentApi
{
    public Report Build()
    {
        return ReportBuilder.Create("Business Report")
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
                    .AddElement(new Image { Source = new FileImageSource("Images/world.svg") }
                        .WithSize(100, 100)
                        .WithVerticalAlignment(Alignment.Center)
                        .WithHorizontalAlignment(Alignment.Start), row: 0, column: 0)
                    .AddElement(new Frame()
                        .WithIsClippedToBounds(false)
                        .WithBackgroundColor(Colors.Chocolate)
                        .WithMargin(-50, 0)
                        .WithSize(SizeLength.Fill, SizeLength.Fill)
                        .AddElement(new Text { Content = "BUSINESS REPORT"}
                            .At(50, -10)
                            .WithSize(SizeLength.Fixed(200), SizeLength.Fill)
                            .WithLineHeight(1f)
                            .WithFont("Kanit", 38f, Colors.White, FontStyle.Bold)), row: 1, column: 0)
                    .AddElement(new StackPanel()
                        .WithOrientation(StackOrientation.Vertical)
                        .WithSize(SizeLength.Fill, SizeLength.Fill)
                        .WithMargin(0, 50)
                        .WithSpacing(5)
                        .AddElements(
                        [
                            Label("YOUR COMPANY'S NAME", 24f, FontStyle.Bold, bottom: 10),
                            Label("PREPARED FOR:", 18f, FontStyle.Bold, top: 20, bottom: 10),
                            Label("John Smith", 14f),
                            Label("Chief Executive Officer", 14f),
                            Label("PREPARED BY:", 18f, FontStyle.Bold, top: 20, bottom: 10),
                            Label("Adam Brown", 14f),
                            Label("Chief Information Officer", 14f)
                        ]), row: 2, column: 0)
                ))
            .Build();
    }

    private static Text Label(
        string content,
        float fontSize,
        FontStyle style = FontStyle.Normal,
        float top = 0,
        float bottom = 0) =>
        new Text { Content = content }
            .WithFont("Kanit", fontSize, Colors.Black, style)
            .WithMargin(0, top, 0, bottom);
}
