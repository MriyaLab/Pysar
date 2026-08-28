using Xunit;

namespace Pysar.Export.Tests;

public class ReportExportServiceTests
{
    [Fact]
    public async Task ExportAsync_Stream_DispatchesToMatchingFormat()
    {
        var pdfBytes = new byte[] { 1, 2, 3 };
        var service = new ReportExportService(new IReportExporter[] { new FakeExporter(ExportFormat.Pdf, pdfBytes) });

        using var destination = new MemoryStream();
        await service.ExportAsync(TestReports.Minimal(), ExportFormat.Pdf, destination);

        Assert.Equal(pdfBytes, destination.ToArray());
    }

    [Fact]
    public async Task ExportAsync_Stream_UnregisteredFormat_ThrowsNotSupported()
    {
        var service = new ReportExportService(Array.Empty<IReportExporter>());

        using var destination = new MemoryStream();
        await Assert.ThrowsAsync<NotSupportedException>(
            () => service.ExportAsync(TestReports.Minimal(), ExportFormat.Pdf, destination));
    }

    [Fact]
    public async Task ExportAsync_UnregisteredFormat_NamesTheFormatsThatAreRegistered()
    {
        var service = new ReportExportService(
            new IReportExporter[] { new FakeExporter(ExportFormat.Pdf, [1]) });

        using var destination = new MemoryStream();
        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => service.ExportAsync(TestReports.Minimal(), new ExportFormat("docx"), destination));

        Assert.Contains("docx", error.Message);
        Assert.Contains("pdf", error.Message);
    }

    [Fact]
    public async Task ExportAsync_NoFormatsRegisteredAtAll_SaysSo()
    {
        var service = new ReportExportService(Array.Empty<IReportExporter>());

        using var destination = new MemoryStream();
        var error = await Assert.ThrowsAsync<NotSupportedException>(
            () => service.ExportAsync(TestReports.Minimal(), ExportFormat.Pdf, destination));

        Assert.Contains("no export formats are registered", error.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void TwoExportersForOneFormat_FailWithAMessageNamingTheFormat()
    {
        var error = Assert.Throws<InvalidOperationException>(
            () => new ReportExportService(
                [new FakeExporter(ExportFormat.Pdf, [1]), new FakeExporter(ExportFormat.Pdf, [2])]));

        Assert.Contains("pdf", error.Message);
    }

    [Fact]
    public async Task ExportAsync_Bytes_ReturnsSameContentAsStreamOverload()
    {
        var pdfBytes = new byte[] { 9, 8, 7, 6 };
        var service = new ReportExportService(new IReportExporter[] { new FakeExporter(ExportFormat.Pdf, pdfBytes) });

        var result = await service.ExportAsync(TestReports.Minimal(), ExportFormat.Pdf);

        Assert.Equal(pdfBytes, result);
    }
}
