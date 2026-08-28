using System.Globalization;
using Pysar.Console.Sample;
using Pysar.Console.Sample.Reports;
using Pysar.Skia;

CultureInfo.CurrentCulture = CultureInfo.InvariantCulture;

var renderer = new SkiaReportRenderer();
ReportBootstrap.Initialize(renderer);

var desktop = Environment.GetFolderPath(Environment.SpecialFolder.Desktop);

var businessReport = new BusinessReport().Build();
var businessReportFluent = new BusinessReportFluent().Build();
var businessReportXaml = new BusinessReportXaml().Build();

await renderer.SavePdfAsync(businessReport, Path.Combine(desktop, "businessReport.pdf"));
await renderer.SavePdfAsync(businessReportFluent, Path.Combine(desktop, "businessReportFluent.pdf"));
await renderer.SavePdfAsync(businessReportXaml, Path.Combine(desktop, "businessReportXaml.pdf"));

Console.WriteLine($"PDF -> {Path.Combine(desktop, "businessReport.pdf")}");
Console.WriteLine($"PDF -> {Path.Combine(desktop, "businessReportFluent.pdf")}");
Console.WriteLine($"PDF -> {Path.Combine(desktop, "businessReportXaml.pdf")}");
