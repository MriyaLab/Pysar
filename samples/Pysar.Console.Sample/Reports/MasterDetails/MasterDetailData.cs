using Pysar.Core.Abstractions;

namespace Pysar.Console.Sample.Reports.MasterDetails;

/// <summary>A single booking line inside a month - the innermost (detail) level of the report.</summary>
public sealed record LedgerEntry(string Category, string Description, decimal Income, decimal Expense)
{
    public decimal Net => Income - Expense;
}

/// <summary>
///     One month of the year - the outer (master) level. <see cref="Entries"/> is the nested collection
///     the report's inner repeater expands over, while the totals print in the month's own header row.
/// </summary>
public sealed record MonthSummary(string Name, IReadOnlyList<LedgerEntry> Entries)
{
    public decimal Income => Entries.Sum(entry => entry.Income);

    public decimal Expense => Entries.Sum(entry => entry.Expense);

    public decimal Net => Income - Expense;
}

/// <summary>The report's root record: a year of months, each carrying its own bookings.</summary>
public sealed record AnnualLedger(
    int Year,
    string Company,
    IReadOnlyList<MonthSummary> Months) : IDesignTimeCreatable<AnnualLedger>
{
    /// <summary>The year as text, because the header belongs to a plain <see cref="Text"/>.</summary>
    public string YearLabel => Year.ToString();

    public decimal Income => Months.Sum(month => month.Income);

    public decimal Expense => Months.Sum(month => month.Expense);

    public decimal Net => Income - Expense;

    public string BestMonth => Months.MaxBy(month => month.Net)?.Name ?? string.Empty;

    public string WorstMonth => Months.MinBy(month => month.Net)?.Name ?? string.Empty;

    /// <summary>
    ///     Margin as a percentage of income, pre-formatted. A <c>StringFormat</c> would have to start with
    ///     a leading <c>{{0:N1}}</c>, which the markup parser cannot escape. Zero income yields zero rather
    ///     than a division error.
    /// </summary>
    public string MarginLabel => $"{(Income == 0 ? 0 : Net / Income * 100):N1} %";

    /// <summary>The design-time preview uses a 3-month subset; the sample renders the full year.</summary>
    public static AnnualLedger CreateDesignInstance() => AnnualData.Sample(6);

    public static AnnualLedger CreateSample() => AnnualData.Sample(12);
}

/// <summary>
///     The sample ledger for the master-detail reports. The figures are fixed rather than random so the
///     rendered PDF is byte-stable across runs, which keeps visual comparisons meaningful.
/// </summary>
internal static class AnnualData
{
    private static readonly string[] MonthNames =
    [
        "January", "February", "March", "April", "May", "June",
        "July", "August", "September", "October", "November", "December"
    ];

    // One row per month: product revenue, consulting revenue, salaries, rent, marketing.
    // Chosen so the year has a weak start, a strong autumn, and two loss-making months.
    private static readonly decimal[][] Figures =
    [
        [ 42_500, 12_000, 31_000,  8_400,  9_600 ],
        [ 38_900, 14_500, 31_000,  8_400,  7_200 ],
        [ 51_300, 18_200, 33_500,  8_400, 12_800 ],
        [ 47_600, 11_400, 33_500,  8_400,  6_500 ],
        [ 55_100, 21_700, 33_500,  8_900, 14_300 ],
        [ 61_400, 24_300, 35_000,  8_900, 15_100 ],
        [ 33_200,  9_800, 35_000,  8_900, 11_400 ],
        [ 29_700,  7_600, 35_000,  8_900, 10_200 ],
        [ 68_900, 27_500, 36_800,  9_400, 16_700 ],
        [ 72_300, 31_200, 36_800,  9_400, 18_900 ],
        [ 64_800, 25_900, 36_800,  9_400, 21_400 ],
        [ 79_500, 34_600, 42_100,  9_400, 24_800 ]
    ];

    public static AnnualLedger Sample(int months) => new(
        2025,
        "You'r Company Name",
        MonthNames
            .Take(months)
            .Select((name, index) => BuildMonth(name, Figures[index]))
            .ToArray());

    private static MonthSummary BuildMonth(string name, decimal[] figures) => new(name, [
        new LedgerEntry("Sales", "Product revenue", figures[0], 0m),
        new LedgerEntry("Services", "Consulting and support", figures[1], 0m),
        new LedgerEntry("Payroll", "Salaries and contributions", 0m, figures[2]),
        new LedgerEntry("Facilities", "Office rent and utilities", 0m, figures[3]),
        new LedgerEntry("Marketing", "Campaigns and events", 0m, figures[4])
    ]);
}
