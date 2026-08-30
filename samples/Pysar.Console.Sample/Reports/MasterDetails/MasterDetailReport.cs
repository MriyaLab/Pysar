using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Xaml;

namespace Pysar.Console.Sample.Reports.MasterDetails;

/// <summary>
///     The master-detail report built imperatively with typed builders (no binding paths): the outer
///     WithData loop repeats a month row (the master), and a nested AddGroup repeats each month's entry
///     rows (the detail). DataTrigger keeps the "net" figures red whenever they are negative.
/// </summary>
public class MasterDetailReport
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
            row.Margin = new Thickness(0, 10, 0, 0);
            row.AddElement(BuildMonthHeader(month, lightGray, negative));
            row.AddElement(BuildEntryColumnsHeader(muted));
            row.AddGroup(month.Entries, (entry, entryRow) =>
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
        Color negative)
    {
        // Grids default to Height = Fill and would swallow the whole content zone; Auto keeps the header
        // compact so the detail starts on the same page. The year is end-aligned in its own Auto column.
        var titles = new Grid
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            ColumnDefinitions = ParseColumns("*, Auto"),
            ColumnSpacing = 10,
            RowDefinitions = ParseRows("Auto")
        };
        // The negative spacing pulls the company line up against the title's descenders.
        var titleLines = new StackPanel { Spacing = -5 };
        titleLines.AddElement(Label("Annual Report", 22, FontStyle.Bold, darkGray));
        titleLines.AddElement(Label(data.Company, 14));
        titles.AddElement(titleLines, 0, 0);
        var year = Label(data.YearLabel, 14);
        year.HorizontalAlignment = Alignment.End;
        titles.AddElement(year, 0, 1);

        var atGlance = new Grid
        {
            Size = new Size(SizeLength.Fill, SizeLength.Auto),
            Padding = new Thickness(15),
            BackgroundColor = lightGray,
            ColumnDefinitions = ParseColumns("Auto, 110, Auto, 110, *, Auto, 110"),
            ColumnSpacing = 16,
            RowDefinitions = ParseRows("Auto, Auto, Auto"),
            RowSpacing = 6
        };
        atGlance.AddElement(Label("Year at a glance", 9, FontStyle.Bold, darkGray), 0, 0, 1, 7);
        atGlance.AddElement(Label("Total income:", 9), 1, 0);
        atGlance.AddElement(GlanceValue(Money(data.Income), 9, FontStyle.Bold), 1, 1);
        atGlance.AddElement(Label("Total expenses:", 9), 1, 2);
        atGlance.AddElement(GlanceValue(Money(data.Expense), 9, FontStyle.Bold), 1, 3);
        atGlance.AddElement(Label("Net result:", 9), 1, 5);
        atGlance.AddElement(NetCell(data.Net, nameof(AnnualLedger.Net), 10, FontStyle.Bold, negative), 1, 6);
        atGlance.AddElement(Label("Best month:", 9), 2, 0);
        atGlance.AddElement(GlanceValue(data.BestMonth, 9), 2, 1);
        atGlance.AddElement(Label("Weakest month:", 9), 2, 2);
        atGlance.AddElement(GlanceValue(data.WorstMonth, 9), 2, 3);
        atGlance.AddElement(Label("Margin:", 9), 2, 5);
        atGlance.AddElement(GlanceValue(data.MarginLabel, 9), 2, 6);

        var stack = new StackPanel { Size = new Size(SizeLength.Fill, SizeLength.Auto) };
        stack.AddElement(titles);
        stack.AddElement(atGlance);
        return stack;
    }

    private static Grid BuildDetailHeader(Color accent)
    {
        var grid = new Grid
        {
            Height = 22,
            BackgroundColor = accent,
            ColumnDefinitions = ParseColumns("*"),
            RowDefinitions = ParseRows("22")
        };
        var title = Label("Monthly breakdown", 9, FontStyle.Bold, Colors.White);
        title.Padding = new Thickness(6, 0);
        title.VerticalAlignment = Alignment.Center;
        grid.AddElement(title, 0, 0);
        return grid;
    }

    private static Grid BuildMonthHeader(MonthSummary month, Color lightGray, Color negative)
    {
        var grid = new Grid
        {
            Height = 24,
            Padding = new Thickness(6, 0),
            BackgroundColor = lightGray,
            ColumnDefinitions = ParseColumns("*, 110, 110, 110"),
            ColumnSpacing = 16,
            RowDefinitions = ParseRows("24")
        };
        var name = Label(month.Name, 9, FontStyle.Bold);
        name.VerticalAlignment = Alignment.Center;
        grid.AddElement(name, 0, 0);
        grid.AddElement(EndCell(Money(month.Income), 9, FontStyle.Bold), 0, 1);
        grid.AddElement(EndCell(Money(month.Expense), 9, FontStyle.Bold), 0, 2);
        grid.AddElement(NetCell(month.Net, nameof(MonthSummary.Net), 9, FontStyle.Bold, negative), 0, 3);
        return grid;
    }

    private static Grid BuildEntryColumnsHeader(Color muted)
    {
        var grid = new Grid
        {
            Height = 16,
            Padding = new Thickness(6, 0),
            BorderColor = muted,
            BorderThickness = new Thickness(0, 0, 0, 0.5f),
            ColumnDefinitions = ParseColumns("120, *, 110, 110, 110"),
            ColumnSpacing = 16,
            RowDefinitions = ParseRows("14")
        };
        grid.AddElement(HeaderCell("Category", muted), 0, 0);
        grid.AddElement(HeaderCell("Description", muted), 0, 1);
        grid.AddElement(HeaderCell("Income", muted, Alignment.End), 0, 2);
        grid.AddElement(HeaderCell("Expense", muted, Alignment.End), 0, 3);
        grid.AddElement(HeaderCell("Net", muted, Alignment.End), 0, 4);
        return grid;
    }

    private static Grid BuildEntryRow(LedgerEntry entry, Color lightGray, Color negative)
    {
        var grid = new Grid
        {
            Height = 18,
            Padding = new Thickness(6, 0),
            BorderColor = lightGray,
            BorderThickness = new Thickness(0, 0, 0, 0.5f),
            ColumnDefinitions = ParseColumns("120, *, 110, 110, 110"),
            ColumnSpacing = 16,
            RowDefinitions = ParseRows("18")
        };
        grid.AddElement(ItemCell(entry.Category), 0, 0);
        grid.AddElement(ItemCell(entry.Description), 0, 1);
        grid.AddElement(EndCell(Money(entry.Income), 9), 0, 2);
        grid.AddElement(EndCell(Money(entry.Expense), 9), 0, 3);
        grid.AddElement(NetCell(entry.Net, nameof(LedgerEntry.Net), 9, FontStyle.Normal, negative), 0, 4);
        return grid;
    }

    private static Grid BuildYearFooter(AnnualLedger data, Color darkGray, Color negative)
    {
        var grid = new Grid
        {
            Margin = new Thickness(0, 16, 0, 0),
            Padding = new Thickness(6, 10, 6, 0),
            BorderColor = darkGray,
            BorderThickness = new Thickness(0, 2, 0, 0),
            ColumnDefinitions = ParseColumns("*, 110, 110, 110"),
            ColumnSpacing = 16,
            RowDefinitions = ParseRows("Auto")
        };
        var label = Label("Year total", 9, FontStyle.Bold);
        label.VerticalAlignment = Alignment.Center;
        grid.AddElement(label, 0, 0);
        grid.AddElement(EndCell(Money(data.Income), 9, FontStyle.Bold), 0, 1);
        grid.AddElement(EndCell(Money(data.Expense), 9, FontStyle.Bold), 0, 2);
        grid.AddElement(NetCell(data.Net, nameof(AnnualLedger.Net), 9, FontStyle.Bold, negative), 0, 3);
        return grid;
    }

    /// <summary>
    ///     The running page banner. Its negative margins cancel the page margin so the strip bleeds to
    ///     the paper edges, while the positive bottom margin keeps it clear of the report header.
    /// </summary>
    private static PageHeaderBand BuildPageHeader(AnnualLedger data, Color lightGray)
    {
        var stack = new StackPanel
        {
            Orientation = StackOrientation.Horizontal,
            Size = Size.Auto,
            HorizontalAlignment = Alignment.Start,
            VerticalAlignment = Alignment.Center
        };
        stack.AddElement(new Text
        {
            Content = $"Annual Report - {data.Company}",
            Font = Kanit(10)
        });

        var header = new PageHeaderBand
        {
            Size = new Size(SizeLength.Fill, 30),
            Margin = new Thickness(-50, -50, -50, 15),
            Padding = new Thickness(25, 0),
            BackgroundColor = lightGray
        };
        header.AddElement(stack);
        return header;
    }

    private static PageFooterBand BuildPageFooter(Report report, Color lightGray)
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
            Margin = new Thickness(-50, 15, -50, -50),
            Padding = new Thickness(25, 0),
            BackgroundColor = lightGray
        };
        footer.AddElement(stack);
        return footer;
    }

    private static Text HeaderCell(string content, Color muted, Alignment horizontal = Alignment.Start)
    {
        var cell = Label(content, 8, FontStyle.Bold, muted);
        cell.HorizontalAlignment = horizontal;
        cell.VerticalAlignment = Alignment.Center;
        return cell;
    }

    private static Text ItemCell(string content)
    {
        var cell = Label(content, 9);
        cell.VerticalAlignment = Alignment.Center;
        return cell;
    }

    /// <summary>
    ///     An end-aligned figure in the header block. Unlike the table cells it is not centred: those Auto
    ///     rows size to their tallest cell, and centring a shorter cell inside one would nudge it off the
    ///     label it belongs to.
    /// </summary>
    private static Text GlanceValue(string content, float size, FontStyle style = FontStyle.Normal)
    {
        var cell = Label(content, size, style);
        cell.HorizontalAlignment = Alignment.End;
        return cell;
    }

    private static Text EndCell(string content, float size, FontStyle style = FontStyle.Normal)
    {
        var cell = Label(content, size, style);
        cell.HorizontalAlignment = Alignment.End;
        cell.VerticalAlignment = Alignment.Center;
        return cell;
    }

    /// <summary>A net figure that turns red when its row's record goes negative (evaluated via DataTrigger).</summary>
    private static Text NetCell(decimal value, string netPath, float size, FontStyle style, Color negative)
    {
        var cell = EndCell(SignedMoney(value), size, style);
        cell.Triggers.Add(new DataTrigger
        {
            Binding = netPath,
            CompareType = CompareType.LessThan,
            Value = "0",
            Setters = { new Setter { Member = nameof(Text.FontColor), Value = negative } }
        });
        return cell;
    }

    private static string Money(decimal value) => value.ToString("$#,##0.00");

    private static string SignedMoney(decimal value) => value.ToString("$#,##0.00;-$#,##0.00");

    private static Text Label(
        string content,
        float size,
        FontStyle style = FontStyle.Normal,
        Color? color = null) =>
        new()
        {
            Content = content,
            Font = Kanit(size, style, color)
        };

    private static Font Kanit(float size, FontStyle style = FontStyle.Normal, Color? color = null) =>
        new("Kanit", size, color ?? Colors.Black, style);

    private static List<ColumnDefinition> ParseColumns(string definitions) =>
        definitions.Split(',').Select(s => new ColumnDefinition(GridLength.Parse(s))).ToList();

    private static List<RowDefinition> ParseRows(string definitions) =>
        definitions.Split(',').Select(s => new RowDefinition(GridLength.Parse(s))).ToList();
}
