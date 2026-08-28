using Microsoft.Extensions.DependencyInjection;
using Pysar.Export;

namespace Pysar.Skia;

public static class SkiaExportServiceCollectionExtensions
{
    public static IServiceCollection AddSkiaReportExporters(this IServiceCollection services) =>
        services.AddSingleton<IReportExporter, PdfReportExporter>();
}
