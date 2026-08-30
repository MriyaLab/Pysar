namespace Pysar.Sample.Reports;

/// <summary>
///     The sample ledger for <see cref="AnnualReport"/>. The figures are fixed rather than random so
///     the rendered PDF is byte-stable across runs, which keeps visual comparisons meaningful.
/// </summary>
public static class AnnualData
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

    public static AnnualLedger Ledger { get; } = new(
        2025,
        "Northwind Traders",
        MonthNames.Select(BuildMonth).ToArray());

    private static MonthSummary BuildMonth(string name, int index)
    {
        var figures = Figures[index];

        return new MonthSummary(name, [
            new LedgerEntry("Sales", "Product revenue", figures[0], 0m),
            new LedgerEntry("Services", "Consulting and support", figures[1], 0m),
            new LedgerEntry("Payroll", "Salaries and contributions", 0m, figures[2]),
            new LedgerEntry("Facilities", "Office rent and utilities", 0m, figures[3]),
            new LedgerEntry("Marketing", "Campaigns and events", 0m, figures[4])
        ]);
    }
}
