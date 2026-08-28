using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Pysar.Export.Tests;

public class ReportExportServiceCollectionExtensionsTests
{
    [Fact]
    public async Task AddReportExportService_ResolvesAServiceThatDispatchesRegisteredExporters()
    {
        var services = new ServiceCollection();
        services.AddSingleton<IReportExporter>(new FakeExporter(ExportFormat.Pdf, new byte[] { 42 }));
        services.AddReportExportService();

        using var provider = services.BuildServiceProvider();
        var service = provider.GetRequiredService<IReportExportService>();

        var result = await service.ExportAsync(TestReports.Minimal(), ExportFormat.Pdf);

        Assert.Equal(new byte[] { 42 }, result);
    }
}
