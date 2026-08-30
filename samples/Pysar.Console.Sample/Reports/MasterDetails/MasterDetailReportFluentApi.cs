using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.MasterDetails;

/// <summary>
///     The master-detail report expressed with the report-building fluent surface: every element is
///     configured through chained With* calls, groups repeat through <c>AddGroup</c>, and the "net"
///     figures turn red through DataTrigger when their record goes negative.
/// </summary>
public class MasterDetailReportFluentApi
{
    public Report Build()
    {
        var colors = ReportResources.LoadFile(
            Path.Combine(AppContext.BaseDirectory, "Styles", "Colors.rxaml"));
        var darkGray = (Color)colors["DarkGray"]!;
        var lightGray = (Color)colors["LightGray"]!;
        var muted = (Color)colors["Muted"]!;
        var accent = (Color)colors["Accent"]!;
        var negative = (Color)colors["Negative"]!;

        var data = AnnualLedger.CreateSample();
        var report = new Report
        {
            DataContext = data,
            PageFormat = new PageFormat
            {
                Size = PageSize.A4,
                Orientation = Orientation.Landscape,
                Margin = new Thickness(50)
            }
        };

        var header = new ReportHeaderBand
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Margin = new Thickness(0, 0, 0, 5)
        };
        header.AddElement(BuildDocumentHeader(data, darkGray, lightGray, negative));

        var detail = report.Detail;
        detail.WithRepeatDetailHeader();
        detail.DetailHeader = BuildDetailHeader(accent);
        detail.WithData(data.Months, (month, row) =>
        {
            // The row container is the month block itself, so its margin is what separates the blocks.
            row.WithMargin(0, 10, 0, 0)
                .AddElement(BuildMonthHeader(month, lightGray, negative))
                .AddElement(BuildEntryColumnsHeader(muted))
                .AddGroup(month.Entries, (entry, entryRow) =>
                    entryRow.AddElement(BuildEntryRow(entry, lightGray, negative)));
        });
        detail.DetailFooter = BuildYearFooter(data, darkGray, negative);

        report.Bands.Add(BuildPageHeader(data, lightGray));
        report.Bands.Add(header);
        report.Bands.Add(BuildPageFooter(report, lightGray));
        return report.Build();
    }

    private static StackPanel BuildDocumentHeader(
        AnnualLedger data,
        Color darkGray,
        Color lightGray,
        Color negative) =>
        new StackPanel()
            .WithSize(SizeLength.Fill, SizeLength.Auto)
            .AddElement(new Grid()
                .WithSize(SizeLength.Fill, SizeLength.Auto)
                .WithColumnDefinitions("*, Auto")
                .WithColumnSpacing(10)
                .WithRowDefinitions("Auto")
                // The negative spacing pulls the company line up against the title's descenders.
                .AddElement(new StackPanel()
                    .WithSpacing(-5)
                    .AddElement(Label("Annual Report", 22, FontStyle.Bold, darkGray))
                    .AddElement(Label(data.Company, 14)), 0, 0)
                .AddElement(Label(data.YearLabel, 14)
                    .WithHorizontalAlignment(Alignment.End), 0, 1))
            .AddElement(new Grid()
                .WithSize(SizeLength.Fill, SizeLength.Auto)
                .WithPadding(15)
                .WithBackgroundColor(lightGray)
                .WithColumnDefinitions("Auto, 110, Auto, 110, *, Auto, 110")
                .WithColumnSpacing(16)
                .WithRowDefinitions("Auto, Auto, Auto")
                .WithRowSpacing(6)
                .AddElement(Label("Year at a glance", 9, FontStyle.Bold, darkGray), 0, 0, 1, 7)
                .AddElement(Label("Total income:", 9), 1, 0)
                .AddElement(MoneyLabel(data.Income, 9, FontStyle.Bold), 1, 1)
                .AddElement(Label("Total expenses:", 9), 1, 2)
                .AddElement(MoneyLabel(data.Expense, 9, FontStyle.Bold), 1, 3)
                .AddElement(Label("Net result:", 9), 1, 5)
                .AddElement(NetLabel(data.Net, nameof(AnnualLedger.Net), 10, FontStyle.Bold, negative), 1, 6)
                .AddElement(Label("Best month:", 9), 2, 0)
                .AddElement(Label(data.BestMonth, 9).WithHorizontalAlignment(Alignment.End), 2, 1)
                .AddElement(Label("Weakest month:", 9), 2, 2)
                .AddElement(Label(data.WorstMonth, 9).WithHorizontalAlignment(Alignment.End), 2, 3)
                .AddElement(Label("Margin:", 9), 2, 5)
                .AddElement(Label(data.MarginLabel, 9).WithHorizontalAlignment(Alignment.End), 2, 6));

    private static Grid BuildDetailHeader(Color accent) =>
        new Grid()
            .WithSize(SizeLength.Fill, 22)
            .WithBackgroundColor(accent)
            .WithColumnDefinitions("*")
            .WithRowDefinitions("22")
            .AddElement(Label("Monthly breakdown", 9, FontStyle.Bold, Colors.White)
                .WithPadding(6, 0)
                .WithVerticalAlignment(Alignment.Center), 0, 0);

    private static Grid BuildMonthHeader(MonthSummary month, Color lightGray, Color negative) =>
        new Grid()
            .WithSize(SizeLength.Fill, 24)
            .WithPadding(6, 0)
            .WithBackgroundColor(lightGray)
            .WithColumnDefinitions("*, 110, 110, 110")
            .WithColumnSpacing(16)
            .WithRowDefinitions("24")
            .AddElement(Label(month.Name, 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(MoneyLabel(month.Income, 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 1)
            .AddElement(MoneyLabel(month.Expense, 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 2)
            .AddElement(NetLabel(month.Net, nameof(MonthSummary.Net), 9, FontStyle.Bold, negative), 0, 3);

    private static Grid BuildEntryColumnsHeader(Color muted) =>
        new Grid()
            .WithSize(SizeLength.Fill, 16)
            .WithPadding(6, 0)
            .WithBorderColor(muted)
            .WithBorderThickness(0, 0, 0, 0.5f)
            .WithColumnDefinitions("120, *, 110, 110, 110")
            .WithColumnSpacing(16)
            .WithRowDefinitions("14")
            .AddElement(Label("Category", 8, FontStyle.Bold, muted)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(Label("Description", 8, FontStyle.Bold, muted)
                .WithVerticalAlignment(Alignment.Center), 0, 1)
            .AddElement(Label("Income", 8, FontStyle.Bold, muted)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 2)
            .AddElement(Label("Expense", 8, FontStyle.Bold, muted)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 3)
            .AddElement(Label("Net", 8, FontStyle.Bold, muted)
                .WithHorizontalAlignment(Alignment.End)
                .WithVerticalAlignment(Alignment.Center), 0, 4);

    private static Grid BuildEntryRow(LedgerEntry entry, Color lightGray, Color negative) =>
        new Grid()
            .WithSize(SizeLength.Fill, 18)
            .WithPadding(6, 0)
            .WithBorderColor(lightGray)
            .WithBorderThickness(0, 0, 0, 0.5f)
            .WithColumnDefinitions("120, *, 110, 110, 110")
            .WithColumnSpacing(16)
            .WithRowDefinitions("18")
            .AddElement(Label(entry.Category, 9)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(Label(entry.Description, 9)
                .WithVerticalAlignment(Alignment.Center), 0, 1)
            .AddElement(MoneyLabel(entry.Income, 9)
                .WithVerticalAlignment(Alignment.Center), 0, 2)
            .AddElement(MoneyLabel(entry.Expense, 9)
                .WithVerticalAlignment(Alignment.Center), 0, 3)
            .AddElement(NetLabel(entry.Net, nameof(LedgerEntry.Net), 9, FontStyle.Normal, negative), 0, 4);

    private static Grid BuildYearFooter(AnnualLedger data, Color darkGray, Color negative) =>
        new Grid()
            .WithMargin(0, 16, 0, 0)
            .WithPadding(6, 10, 6, 0)
            .WithBorderColor(darkGray)
            .WithBorderThickness(0, 2, 0, 0)
            .WithColumnDefinitions("*, 110, 110, 110")
            .WithColumnSpacing(16)
            .WithRowDefinitions("Auto")
            .AddElement(Label("Year total", 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 0)
            .AddElement(MoneyLabel(data.Income, 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 1)
            .AddElement(MoneyLabel(data.Expense, 9, FontStyle.Bold)
                .WithVerticalAlignment(Alignment.Center), 0, 2)
            .AddElement(NetLabel(data.Net, nameof(AnnualLedger.Net), 9, FontStyle.Bold, negative), 0, 3);

    /// <summary>
    ///     The running page banner. Its negative margins cancel the page margin so the strip bleeds to
    ///     the paper edges, while the positive bottom margin keeps it clear of the report header.
    /// </summary>
    private static PageHeaderBand BuildPageHeader(AnnualLedger data, Color lightGray)
    {
        var header = new PageHeaderBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-50, -50, -50, 15),
            Padding = new Thickness(25, 0),
            BackgroundColor = lightGray
        };
        header.AddElement(new StackPanel()
            .WithOrientation(StackOrientation.Horizontal)
            .WithSize(Size.Auto)
            .WithHorizontalAlignment(Alignment.Start)
            .WithVerticalAlignment(Alignment.Center)
            .AddElement(Label($"Annual Report - {data.Company}", 10)));
        return header;
    }

    private static PageFooterBand BuildPageFooter(Report report, Color lightGray)
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
            Margin = new Thickness(-50, 15, -50, -50),
            Padding = new Thickness(25, 0),
            BackgroundColor = lightGray
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

    private static Text MoneyLabel(decimal value, float size, FontStyle style = FontStyle.Normal) =>
        Label(value.ToString("$#,##0.00"), size, style).WithHorizontalAlignment(Alignment.End);

    /// <summary>A net figure that turns red when its row's record goes negative (evaluated via DataTrigger).</summary>
    private static Text NetLabel(decimal value, string netPath, float size, FontStyle style, Color negative)
    {
        var label = MoneyLabel(value, size, style).WithVerticalAlignment(Alignment.Center);
        label.Triggers.Add(new DataTrigger
        {
            Binding = netPath,
            CompareType = CompareType.LessThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = negative } }
        });
        return label;
    }

    private static Text Label(string content, float size, FontStyle style = FontStyle.Normal, Color? color = null) =>
        new Text { Content = content }.WithFont("Kanit", size, color ?? Colors.Black, style);
}