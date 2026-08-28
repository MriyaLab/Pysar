using Microsoft.Extensions.DependencyInjection;
using Pysar.Core;
using Pysar.Core.Abstractions;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Maui;

public static class MauiAppBuilderExtensions
{
    /// <summary>
    ///     Registers Pysar with the application: reports resolve their assets from the application
    ///     package, and <see cref="SkiaReportRenderer"/>, <see cref="IReportExportService"/>,
    ///     <see cref="IReportSharer"/>, and <see cref="IReportPrinter"/> become injectable.
    /// </summary>
    /// <example>
    ///     <code>
    ///     builder
    ///         .UseMauiApp&lt;App&gt;()
    ///         .UsePysar(pysar => pysar
    ///             .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
    ///             .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
    ///     </code>
    /// </example>
    public static MauiAppBuilder UsePysar(this MauiAppBuilder builder, Action<PysarBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        var platformHandler = new MauiReportPlatformHandler();

        // Rendering reads the handler from this ambient state rather than from DI, so it is installed
        // here - before any report can be built - and not when the renderer is first resolved.
        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();
        configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection));

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = renderer;

        builder.Services.AddSingleton<IReportPlatformHandler>(platformHandler);
        builder.Services.AddSingleton(renderer);
        builder.Services.AddSkiaReportExporters();
        builder.Services.AddReportExportService();
        builder.Services.AddSingleton<IReportSharer, MauiReportSharer>();
        builder.Services.AddSingleton<IReportPrinter, MauiReportPrinter>();

        return builder;
    }
}
