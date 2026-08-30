using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.Triggers;

public class PeriodIncomeReportFluentApi
{
    public Report Build()
    {
        var colors = ReportResources.LoadFile(
            Path.Combine(AppContext.BaseDirectory, "Styles", "Colors.rxaml"));
        var darkGray = (Color)colors["DarkGray"]!;
        var lightGray = (Color)colors["LightGray"]!;
        var lightGreen = (Color)colors["LightGreen"]!;
        var positive = (Color)colors["Positive"]!;
        var negative = (Color)colors["Negative"]!;

        var data = PeriodIncome.CreateSample();
        var report = new Report
        {
            DataContext = data,
            PageFormat = new PageFormat { Size = PageSize.A4, Orientation = Orientation.Portrait }
        };

        var header = new ReportHeaderBand
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Margin = new Thickness(0, 0, 0, 15)
        };
        header.AddElement(new Grid()
            .WithRowDefinitions("Auto, Auto")
            .WithRowSpacing(8)
            .AddElement(Label("Income by Company", 22, FontStyle.Bold), 0, 0)
            .AddElement(new Grid()
                .WithColumnDefinitions("*, Auto")
                .WithColumnSpacing(10)
                .AddElement(Label(data.Company, 14), 0, 0)
                .AddElement(Label(data.Period, 14)
                    .WithHorizontalAlignment(Alignment.End), 0, 1), 1, 0));

        report.Detail
            .WithData(data.Items, (item, row) =>
                row.AddElement(BuildItemRow(item, darkGray, lightGray, lightGreen, positive, negative)));
        report.Detail.DetailHeader = BuildItemsHeader();
        report.Detail.DetailFooter = BuildItemsFooter(data, positive, negative);

        report.Bands.Add(header);
        report.Bands.Add(BuildPageFooter(report));
        return report.Build();
    }

    private static Grid BuildItemsHeader() =>
        new Grid()
            .WithSize(SizeLength.Fill, 40)
            .WithMargin(0, 0, 0, 5)
            .WithBorderColor("#CCCCCC")
            .WithBorderThickness(0, 0, 0, 1)
            .WithColumnDefinitions("30, *, 90, 90, 90")
            .AddElement(HeaderCell("#"), 0, 0)
            .AddElement(HeaderCell("Company"), 0, 1)
            .AddElement(HeaderCell("Revenue").WithHorizontalAlignment(Alignment.End), 0, 2)
            .AddElement(HeaderCell("Expense").WithHorizontalAlignment(Alignment.End), 0, 3)
            .AddElement(HeaderCell("Balance").WithHorizontalAlignment(Alignment.End), 0, 4);

    private static Grid BuildItemRow(
        CompanyIncome item,
        Color darkGray,
        Color lightGray,
        Color lightGreen,
        Color positive,
        Color negative)
    {
        var row = new Grid()
            .WithSize(SizeLength.Fill, SizeLength.Auto)
            .WithMinHeight(30)
            .WithBorderColor("#CCCCCC")
            .WithBorderThickness(0, 0, 0, 1)
            .WithColumnDefinitions("30, *, 90, 90, 90");

        row.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.IsOwnCompany),
            Value = "True",
            Setters = { new Setter { Member = nameof(Grid.BackgroundColor), Value = darkGray } }
        });

        return row
            .AddElement(OwnCompanyCell(item.Index.ToString(), lightGray), 0, 0)
            .AddElement(OwnCompanyCell(item.Name, lightGray)
                .WithSize(SizeLength.Fill, SizeLength.Auto)
                .WithVerticalTextAlignment(TextAlignment.Center), 0, 1)
            .AddElement(OwnCompanyCell(item.Revenue.ToString(), lightGray)
                .WithHorizontalAlignment(Alignment.End), 0, 2)
            .AddElement(OwnCompanyCell(item.Expense.ToString(), lightGray)
                .WithHorizontalAlignment(Alignment.End), 0, 3)
            .AddElement(BalanceCell(item.Balance.ToString(), lightGreen, positive, negative)
                .WithHorizontalAlignment(Alignment.End), 0, 4);
    }

    private static Grid BuildItemsFooter(PeriodIncome data, Color positive, Color negative)
    {
        var total = Label(data.Total.ToString(), 14, FontStyle.Bold)
            .WithHorizontalAlignment(Alignment.End)
            .WithVerticalAlignment(Alignment.Center);

        total.Triggers.Add(new DataTrigger
        {
            Binding = nameof(PeriodIncome.Total),
            CompareType = CompareType.GreaterThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = positive } }
        });
        total.Triggers.Add(new DataTrigger
        {
            Binding = nameof(PeriodIncome.Total),
            CompareType = CompareType.LessThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = negative } }
        });

        return new Grid()
            .WithSize(SizeLength.Fill, 35)
            .WithColumnDefinitions("*, 90")
            .WithColumnSpacing(10)
            .AddElement(Label("Total", 14, FontStyle.Bold)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(total, 0, 1);
    }

    private static PageFooterBand BuildPageFooter(Report report)
    {
        var pageNumber = new Text().WithFont("Kanit", 10f, Colors.Black);
        var pageCount = new Text().WithFont("Kanit", 10f, Colors.Black);
        report.WithPageChanged((number, count) =>
        {
            pageNumber.Content = number.ToString();
            pageCount.Content = count.ToString();
        });

        var footer = new PageFooterBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-40, 0, -40, -30),
            Padding = new Thickness(25, 0)
        };
        footer.AddElement(new StackPanel()
            .WithOrientation(StackOrientation.Horizontal)
            .WithSize(Size.Auto)
            .WithHorizontalAlignment(Alignment.End)
            .WithVerticalAlignment(Alignment.Center)
            .AddElements(
            [
                new Text { Content = "Page " }.WithFont("Kanit", 10f, Colors.Black),
                pageNumber,
                new Text { Content = " of " }.WithFont("Kanit", 10f, Colors.Black).WithPadding(3, 0),
                pageCount
            ]));
        return footer;
    }

    private static Text OwnCompanyCell(string content, Color lightGray)
    {
        var text = ItemCell(content);
        text.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.IsOwnCompany),
            Value = "True",
            Setters =
            {
                new Setter { Member = nameof(Text.FontStyle), Value = FontStyle.Bold },
                new Setter { Member = nameof(Text.FontColor), Value = lightGray }
            }
        });
        return text;
    }

    private static Text BalanceCell(string content, Color lightGreen, Color positive, Color negative)
    {
        var text = ItemCell(content);
        text.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.Balance),
            CompareType = CompareType.GreaterThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = positive } }
        });
        text.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.IsOwnCompany),
            Value = "True",
            Setters =
            {
                new Setter { Member = nameof(Text.FontStyle), Value = FontStyle.Bold },
                new Setter { Member = nameof(Text.FontColor), Value = lightGreen }
            }
        });
        text.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.Balance),
            CompareType = CompareType.LessThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = negative } }
        });
        return text;
    }

    private static Text HeaderCell(string content) =>
        Label(content, 14, FontStyle.Bold).WithVerticalAlignment(Alignment.Center);

    private static Text ItemCell(string content) =>
        Label(content, 14).WithVerticalAlignment(Alignment.Center);

    private static Text Label(string content, float size, FontStyle style = FontStyle.Normal) =>
        new Text { Content = content }.WithFont("Kanit", size, Colors.Black, style);
}
