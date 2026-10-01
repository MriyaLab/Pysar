using System.Text;
using Pysar.Core.Enums;
using Pysar.Core.Structs;
using Pysar.Elements;
using Pysar.Export;
using Xunit;

namespace Pysar.Skia.Tests.Rendering;

public class PdfReportExporterTests
{
    private static string Latin1(byte[] bytes) => Encoding.Latin1.GetString(bytes);

    private static Report MinimalReport() =>
        ReportBuilder.Create("Doc")
            .WithPageFormat(new PageFormat { Margin = new Thickness(10), Size = PageSize.A4 })
            .WithDetail(b => b.AddText("Hello", t => t.WithSize(SizeLength.Fill, SizeLength.Fixed(20))))
            .Build();

    [Fact]
    public void Format_IsPdf()
    {
        var exporter = new PdfReportExporter(new SkiaReportRenderer());

        Assert.Equal(ExportFormat.Pdf, exporter.Format);
    }

    [Fact]
    public async Task ExportAsync_ProducesValidPdf()
    {
        var exporter = new PdfReportExporter(new SkiaReportRenderer());

        using var ms = new MemoryStream();
        await exporter.ExportAsync(MinimalReport(), ms);

        var bytes = ms.ToArray();
        Assert.True(bytes.Length > 0);
        Assert.Equal("%PDF", Latin1(bytes[..4]));
    }

    [Fact]
    public async Task ExportAsync_PdfA_WritesPdfA2bPartIdentification()
    {
        var service = SkiaReportExport.CreateExportService(new SkiaReportRenderer());

        using var ms = new MemoryStream();
        await service.ExportAsync(MinimalReport(), ExportFormat.Pdf, ms, new PdfExportOptions { PdfA = true });

        Assert.Contains("pdfaid:part>2", Latin1(ms.ToArray()));
    }

    [Fact]
    public async Task ExportAsync_ForeignOptions_NamesTheFormat()
    {
        var exporter = new PdfReportExporter(new SkiaReportRenderer());
        using var ms = new MemoryStream();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => exporter.ExportAsync(MinimalReport(), ms, new ForeignExportOptions()));

        Assert.Contains("pdf", error.Message);
    }

    [Fact]
    public async Task ExportService_ForeignOptions_NamesTheFormat()
    {
        var service = SkiaReportExport.CreateExportService(new SkiaReportRenderer());
        using var ms = new MemoryStream();

        var error = await Assert.ThrowsAsync<ArgumentException>(
            () => service.ExportAsync(MinimalReport(), ExportFormat.Pdf, ms, new ForeignExportOptions()));

        Assert.Contains("pdf", error.Message);
    }

    private sealed record ForeignExportOptions : ExportOptions;
}
