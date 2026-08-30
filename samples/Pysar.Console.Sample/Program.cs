using Pysar.Console.Sample;
using Pysar.Console.Sample.Reports;
using Pysar.Console.Sample.Reports.Base;
using Pysar.Console.Sample.Reports.CustomControls;
using Pysar.Console.Sample.Reports.Data;
using Pysar.Console.Sample.Reports.MasterDetails;
using Pysar.Console.Sample.Reports.Styles;
using Pysar.Console.Sample.Reports.Triggers;
using Pysar.Elements;
using Pysar.Skia;
using BusinessReportWithStyleXaml = Pysar.Console.Sample.Reports.Styles.BusinessReportWithStyleXaml;

var renderer = new SkiaReportRenderer();
ReportBootstrap.Initialize(renderer);

var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

var reports = new (string Title, string FileName, Func<Report> Build)[]
{
    ("Business Report", "businessReport.pdf", () => new BusinessReport().Build()),
    ("Business Report (Fluent API)", "businessReportFluent.pdf", () => new BusinessReportFluentApi().Build()),
    ("Business Report (XAML)", "businessReportXaml.pdf", () => new BusinessReportXaml().Build()),
    ("Invoice Report", "invoiceReport.pdf", () => new InvoiceReport().Build()),
    ("Invoice Report (Fluent API)", "invoiceReportFluent.pdf", () => new InvoiceReportFluentApi().Build()),
    ("Invoice Report (XAML)", "invoiceReportXaml.pdf", () => new InvoiceReportXaml().Build()),
    ("Business Report with Style", "businessReportWithStyle.pdf", () => new BusinessReportWithStyle().Build()),
    ("Business Report with Style (Fluent API)", "businessReportWithStyleFluent.pdf", () => new BusinessReportWithStyleFluentApi().Build()),
    ("Business Report with Style (XAML)", "businessReportWithStyleXaml.pdf", () => new BusinessReportWithStyleXaml().Build()),
    ("Period Income with Triggers", "periodIncome.pdf", () => new PeriodIncomeReport().Build()),
    ("Period Income with Triggers (Fluent API)", "periodIncomeFluent.pdf", () => new PeriodIncomeReportFluentApi().Build()),
    ("Period Income with Triggers (XAML)", "periodIncomeXaml.pdf", () => new PeriodIncomeReportXaml().Build()),
    ("QR Code (Custom Control)", "qrCode.pdf", () => new QRCodeReport().Build()),
    ("QR Code (Custom Control, Fluent API)", "qrCodeFluent.pdf", () => new QRCodeReportFluentApi().Build()),
    ("QR Code (Custom Control, XAML)", "qrCodeXaml.pdf", () => new QRCodeReportXaml().Build()),
    ("Master-Detail Report", "masterDetail.pdf", () => new MasterDetailReport().Build()),
    ("Master-Detail Report (Fluent API)", "masterDetailFluent.pdf", () => new MasterDetailReportFluentApi().Build()),
    ("Master-Detail Report (XAML)", "masterDetailXaml.pdf", () => new MasterDetailReportXaml().Build()),
};

while (true)
{
    Console.WriteLine("Select a report to export:");
    for (var i = 0; i < reports.Length; i++)
        Console.WriteLine($"  {i + 1}. {reports[i].Title}");
    Console.WriteLine("  q. Quit");
    Console.Write("> ");

    var input = Console.ReadLine()?.Trim();
    if (string.Equals(input, "q", StringComparison.OrdinalIgnoreCase))
        return;

    if (!int.TryParse(input, out var choice) || choice < 1 || choice > reports.Length)
    {
        Console.WriteLine("Unknown choice.");
        Console.WriteLine();
        continue;
    }

    var selected = reports[choice - 1];
    var path = Path.Combine(desktop, selected.FileName);
    var report = selected.Build();
    await renderer.SavePdfAsync(report, path);
    Console.WriteLine($"Exported {selected.Title} -> {path}");
    Console.WriteLine();
}
