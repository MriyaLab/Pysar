using Microsoft.Extensions.DependencyInjection;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Blazor;

public static class ServiceCollectionExtensions
{
    /// <summary>
    ///     Registers QReport with the application: <see cref="SkiaReportRenderer"/>,
    ///     <see cref="IReportExportService"/>, and <see cref="IReportPrinter"/> become injectable,
    ///     and <see cref="ReportView"/> draws through the same renderer - so a custom drawer added
    ///     in <paramref name="configure"/> reaches the viewer, not only exports.
    /// </summary>
    /// <example>
    ///     <code>
    ///     builder.Services.AddQReport(renderer => renderer.WithDrawer&lt;QRCode&gt;(new QRCodeDrawer()));
    ///     </code>
    /// </example>
    public static IServiceCollection AddQReport(
        this IServiceCollection services, Action<SkiaReportRenderer>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(services);

        var renderer = new SkiaReportRenderer();
        configure?.Invoke(renderer);

        services.AddSingleton(renderer);
        services.AddSkiaReportExporters();
        services.AddReportExportService();

        // Scoped, not singleton: the printer holds a JS module reference, which belongs to the one
        // browser context a scope stands for.
        services.AddScoped<IReportPrinter, BlazorReportPrinter>();

        return services;
    }
}
