using Pysar.Elements;
using Pysar.Sample.Reports;

namespace Pysar.Maui.Sample.Services;

/// <summary>One entry of the report flyout: a display name, how to build the report and its flyout icon.</summary>
public sealed record ReportDescriptor(string Title, string FileName, string FlyoutGlyph, Func<Report> Create)
{
    public static IReadOnlyList<ReportDescriptor> All { get; } =
    [
        new("Invoice", "InvoiceReport.pdf", "file-invoice-dollar", () => new InvoiceReport(Invoice.CreateDesignInstance())),
        new("Annual", "AnnualReport.pdf", "chart-line", () => new AnnualReport(AnnualLedger.CreateDesignInstance())),
        new("Revenue By Customer", "RevenueByCustomer.pdf", "users", () => new RevenueByCustomerReport(RevenueReportData.CreateDesignInstance()))
    ];

    public override string ToString() => Title;
}
