using Pysar.Core.Abstractions;

namespace Pysar.Sample.Reports;

public partial class AnnualReport
{
    public AnnualReport(AnnualLedger data)
    {
        InitializeComponent();
        DataContext = data;
    }

    public AnnualReport()
    {
        InitializeComponent();
    }
}

/// <summary>A single booking inside a month — the innermost level of the report's data.</summary>
public sealed record LedgerEntry(string Category, string Description, decimal Income, decimal Expense)
{
    public decimal Net => Income - Expense;
}

/// <summary>
///     One month of the year. <see cref="Entries"/> is the nested collection the report's inner
///     repeater expands over, while the totals print in the month's own header row.
/// </summary>
public sealed record MonthSummary(string Name, IReadOnlyList<LedgerEntry> Entries)
{
    public decimal Income => Entries.Sum(entry => entry.Income);

    public decimal Expense => Entries.Sum(entry => entry.Expense);

    public decimal Net => Income - Expense;
}

/// <summary>The report's root record: a year of months, each carrying its own bookings.</summary>
public sealed record AnnualLedger(int Year, string Company, IReadOnlyList<MonthSummary> Months)
    : IDesignTimeCreatable<AnnualLedger>
{
    /// <summary>The year as text, because the header component's parameters are strings.</summary>
    public string YearLabel => Year.ToString();

    public decimal Income => Months.Sum(month => month.Income);

    public decimal Expense => Months.Sum(month => month.Expense);

    public decimal Net => Income - Expense;

    /// <summary>
    ///     Margin as a percentage of income, pre-formatted. A <c>StringFormat</c> cannot be used here
    ///     because it would have to start with <c>{0:N1}</c>, and the markup parser has no escape for a
    ///     leading brace. Zero income yields zero rather than a division error.
    /// </summary>
    public string MarginLabel => $"{(Income == 0 ? 0 : Net / Income * 100):N1} %";

    public string BestMonth => Months.OrderByDescending(month => month.Net).First().Name;

    public string WorstMonth => Months.OrderBy(month => month.Net).First().Name;

    /// <summary>The sample ledger used by both the console application and design-time preview.</summary>
    public static AnnualLedger CreateDesignInstance() => AnnualData.Ledger;
}
