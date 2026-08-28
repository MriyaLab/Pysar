using Microsoft.Extensions.DependencyInjection;

namespace Pysar.Export;

public static class ReportExportServiceCollectionExtensions
{
    public static IServiceCollection AddReportExportService(this IServiceCollection services) =>
        services.AddSingleton<IReportExportService, ReportExportService>();
}
