using Pysar.Core.Abstractions;

namespace Pysar.Console.Sample.Reports.Triggers;

public sealed record PeriodIncome(
    string Company,
    string Period,
    IReadOnlyList<CompanyIncome> Items) : IDesignTimeCreatable<PeriodIncome>
{
    public decimal Total => Items.Sum(item => item.Balance);

    public static PeriodIncome CreateDesignInstance() => new(
        "Nexus IT Solutions",
        "01 Aug 2026 – 26 Aug 2026",
        [
            new CompanyIncome(1, "Pacific Healthcare Group", 9600m, 7200m, false),
            new CompanyIncome(2, "Harbor Logistics", 4100m, 6350m, false),
            new CompanyIncome(3, "Nexus IT Solutions", 18400m, 12150m, true),
            new CompanyIncome(4, "Apex Manufacturing", 15200m, 9800m, false),
            new CompanyIncome(5, "Redwood Retail", 2800m, 4100m, false)
        ]);

    public static PeriodIncome CreateSample() => new(
        "Nexus IT Solutions",
        "01 Aug 2026 – 26 Aug 2026",
        [
            new CompanyIncome(1, "Nexus IT Solutions", 42850m, 28640m, true),
            new CompanyIncome(2, "Pacific Healthcare Group", 31200m, 18450m, false),
            new CompanyIncome(3, "Harbor Logistics", 9400m, 12870m, false),
            new CompanyIncome(4, "Apex Manufacturing", 18750m, 14200m, false),
            new CompanyIncome(5, "Redwood Retail", 6100m, 8900m, false),
            new CompanyIncome(6, "Summit Consulting", 22400m, 15120m, false),
            new CompanyIncome(7, "Blue Harbor Media", 4300m, 7100m, false),
            new CompanyIncome(8, "Northwind Supplies", 15680m, 13420m, false)
        ]);
}

public sealed record CompanyIncome(
    int Index,
    string Name,
    decimal Revenue,
    decimal Expense,
    bool IsOwnCompany)
{
    public decimal Balance => Revenue - Expense;
}
