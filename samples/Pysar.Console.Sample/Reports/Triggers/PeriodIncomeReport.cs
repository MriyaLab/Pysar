using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.Triggers;

public class PeriodIncomeReport
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

        var meta = new Grid
        {
            ColumnDefinitions = ParseColumns("*, Auto"),
            ColumnSpacing = 10
        };
        meta.AddElement(Label(data.Company, 14), 0, 0);
        var period = Label(data.Period, 14);
        period.HorizontalAlignment = Alignment.End;
        meta.AddElement(period, 0, 1);

        var headerGrid = new Grid
        {
            RowDefinitions = ParseRows("Auto, Auto"),
            RowSpacing = 8
        };
        headerGrid.AddElement(Label("Income by Company", 22, FontStyle.Bold), 0, 0);
        headerGrid.AddElement(meta, 1, 0);

        var header = new ReportHeaderBand
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Margin = new Thickness(0, 0, 0, 15)
        };
        header.AddElement(headerGrid);
        report.Bands.Add(header);

        var detail = report.Detail;
        detail.WithData(data.Items, (item, row) =>
            row.AddElement(BuildItemRow(item, darkGray, lightGray, lightGreen, positive, negative)));
        detail.DetailHeader = BuildItemsHeader();
        detail.DetailFooter = BuildItemsFooter(data, positive, negative);

        report.Bands.Add(BuildPageFooter(report));
        return report.Build();
    }

    private static Grid BuildItemsHeader()
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, 40),
            Margin = new Thickness(0, 0, 0, 5),
            BorderColor = Color.FromHex("#CCCCCC"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions = ParseColumns("30, *, 90, 90, 90")
        };
        grid.AddElement(HeaderCell("#", Alignment.Start), 0, 0);
        grid.AddElement(HeaderCell("Company", Alignment.Start), 0, 1);
        grid.AddElement(HeaderCell("Revenue", Alignment.End), 0, 2);
        grid.AddElement(HeaderCell("Expense", Alignment.End), 0, 3);
        grid.AddElement(HeaderCell("Balance", Alignment.End), 0, 4);
        return grid;
    }

    private static Grid BuildItemRow(
        CompanyIncome item,
        Color darkGray,
        Color lightGray,
        Color lightGreen,
        Color positive,
        Color negative)
    {
        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            MinHeight = 30,
            BorderColor = Color.FromHex("#CCCCCC"),
            BorderThickness = new Thickness(0, 0, 0, 1),
            ColumnDefinitions = ParseColumns("30, *, 90, 90, 90")
        };
        grid.Triggers.Add(new DataTrigger
        {
            Binding = nameof(CompanyIncome.IsOwnCompany),
            Value = "True",
            Setters = { new Setter { Member = nameof(Grid.BackgroundColor), Value = darkGray } }
        });

        var name = OwnCompanyCell(item.Name, Alignment.Start, lightGray);
        name.Size = new Size(SizeLength.Fill, SizeLength.Auto);
        name.VerticalTextAlignment = TextAlignment.Center;

        grid.AddElement(OwnCompanyCell(item.Index.ToString(), Alignment.Start, lightGray), 0, 0);
        grid.AddElement(name, 0, 1);
        grid.AddElement(OwnCompanyCell(item.Revenue.ToString(), Alignment.End, lightGray), 0, 2);
        grid.AddElement(OwnCompanyCell(item.Expense.ToString(), Alignment.End, lightGray), 0, 3);
        grid.AddElement(BalanceCell(item.Balance.ToString(), lightGreen, positive, negative), 0, 4);
        return grid;
    }

    private static Grid BuildItemsFooter(PeriodIncome data, Color positive, Color negative)
    {
        var total = Label(data.Total.ToString(), 14, FontStyle.Bold);
        total.HorizontalAlignment = Alignment.End;
        total.VerticalAlignment = Alignment.Center;
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

        var grid = new Grid
        {
            Size = new Size(SizeLength.Fill, 35),
            ColumnDefinitions = ParseColumns("*, 90"),
            ColumnSpacing = 10
        };
        grid.AddElement(Label("Total", 14, FontStyle.Bold, Alignment.End, Alignment.Center), 0, 0);
        grid.AddElement(total, 0, 1);
        return grid;
    }

    private static PageFooterBand BuildPageFooter(Report report)
    {
        var pageNumber = new Text { Font = Kanit(10) };
        var pageCount = new Text { Font = Kanit(10) };
        report.WithPageChanged((number, count) =>
        {
            pageNumber.Content = number.ToString();
            pageCount.Content = count.ToString();
        });

        var stack = new StackPanel
        {
            Orientation = StackOrientation.Horizontal,
            Size = Size.Auto,
            HorizontalAlignment = Alignment.End,
            VerticalAlignment = Alignment.Center
        };
        stack.AddElements(
        [
            new Text { Content = "Page ", Font = Kanit(10) },
            pageNumber,
            new Text { Content = " of ", Font = Kanit(10), Padding = new Thickness(3, 0) },
            pageCount
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

    private static Text OwnCompanyCell(string content, Alignment horizontal, Color lightGray)
    {
        var text = ItemCell(content, horizontal);
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
        var text = ItemCell(content, Alignment.End);
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

    private static Text HeaderCell(string content, Alignment horizontal) =>
        new()
        {
            Content = content,
            Font = Kanit(14, FontStyle.Bold),
            HorizontalAlignment = horizontal,
            VerticalAlignment = Alignment.Center
        };

    private static Text ItemCell(string content, Alignment horizontal)
    {
        var text = Label(content, 14);
        text.HorizontalAlignment = horizontal;
        text.VerticalAlignment = Alignment.Center;
        return text;
    }

    private static Text Label(
        string content,
        float size,
        FontStyle style = FontStyle.Normal,
        Alignment horizontal = Alignment.Start,
        Alignment vertical = Alignment.Start) =>
        new()
        {
            Content = content,
            Font = Kanit(size, style),
            HorizontalAlignment = horizontal,
            VerticalAlignment = vertical
        };

    private static Font Kanit(float size, FontStyle style = FontStyle.Normal) =>
        new("Kanit", size, Colors.Black, style);

    private static List<ColumnDefinition> ParseColumns(string definitions) =>
        definitions.Split(',').Select(s => new ColumnDefinition(GridLength.Parse(s))).ToList();

    private static List<RowDefinition> ParseRows(string definitions) =>
        definitions.Split(',').Select(s => new RowDefinition(GridLength.Parse(s))).ToList();
}
