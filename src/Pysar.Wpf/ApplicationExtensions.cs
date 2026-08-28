using System.Reflection;
using System.Windows;
using Pysar.Core;
using Pysar.Skia;

namespace Pysar.Wpf;

/// <summary>
///     Registers Pysar with the application: reports resolve their assets from the application's
///     resources, and the report view renders through a shared <see cref="SkiaReportRenderer"/>.
/// </summary>
/// <example>
///     <code>
///     public partial class App : Application
///     {
///         protected override void OnStartup(StartupEventArgs e)
///         {
///             base.OnStartup(e);
///             this.UsePysar(pysar => pysar
///                 .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
///                 .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
///         }
///     }
///     </code>
/// </example>
public static class ApplicationExtensions
{
    /// <summary>
    ///     Registers Pysar with the application.
    /// </summary>
    /// <param name="assemblyName">
    ///     The assembly report assets are packaged under, used to resolve pack URIs and manifest
    ///     resources. Defaults to the entry assembly's name, which is correct for the common case of
    ///     a single application project holding its own assets; pass an explicit name when assets
    ///     live in a different assembly (a shared resources project, for instance).
    /// </param>
    public static Application UsePysar(
        this Application application,
        Action<PysarBuilder>? configure = null,
        string? assemblyName = null)
    {
        ArgumentNullException.ThrowIfNull(application);

        assemblyName ??= Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException(
                "Could not determine the entry assembly's name; pass assemblyName explicitly.");

        var platformHandler = new WpfReportPlatformHandler(assemblyName);

        // Rendering reads the handler from this ambient state rather than from DI, so it is installed
        // here - before any report can be built - and not when the renderer is first resolved.
        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = renderer;

        // Pack/manifest asset access does not need a deferred platform-service hook; configure runs
        // synchronously so fonts are registered before the first report is built.
        configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection));

        return application;
    }
}
