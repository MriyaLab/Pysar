using System.Reflection;
using Avalonia;
using Pysar.Core;
using Pysar.Skia;

namespace Pysar.Avalonia;

/// <summary>
///     Registers Pysar with the application: reports resolve their assets from the application's
///     Avalonia resources, and <see cref="ReportView"/> renders through a shared
///     <see cref="SkiaReportRenderer"/>.
/// </summary>
/// <example>
///     <code>
///     AppBuilder.Configure&lt;App&gt;()
///         .UsePlatformDetect()
///         .UsePysar(pysar => pysar
///             .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
///             .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
///     </code>
/// </example>
public static class AppBuilderExtensions
{
    /// <summary>
    ///     Registers Pysar with the application.
    /// </summary>
    /// <param name="assemblyName">
    ///     The assembly report assets are packaged under, used to resolve <c>avares://</c> URIs.
    ///     Defaults to the entry assembly's name, which is correct for the common case of a single
    ///     application project holding its own assets; pass an explicit name when assets live in a
    ///     different assembly (a shared resources project, for instance).
    /// </param>
    public static AppBuilder UsePysar(
        this AppBuilder builder, Action<PysarBuilder>? configure = null, string? assemblyName = null)
    {
        ArgumentNullException.ThrowIfNull(builder);

        assemblyName ??= Assembly.GetEntryAssembly()?.GetName().Name
            ?? throw new InvalidOperationException(
                "Could not determine the entry assembly's name; pass assemblyName explicitly.");

        var platformHandler = new AvaloniaReportPlatformHandler(assemblyName);

        // Rendering reads the handler from this ambient state rather than from DI, so it is installed
        // here - before any report can be built - and not when the renderer is first resolved.
        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = renderer;

        // configure typically reads font bytes through AvaloniaAssetFileSystem, which needs
        // Avalonia.Platform.IAssetLoader - a platform service that UsePlatformDetect() has only
        // scheduled, not yet registered, while this method is still running as part of the
        // AppBuilder's fluent chain. Deferring to AfterPlatformServicesSetup runs it once that
        // service (and the rest of the platform) is actually in the locator; calling configure
        // synchronously here throws "Unable to locate 'Avalonia.Platform.IAssetLoader'" instead.
        builder.AfterPlatformServicesSetup(
            _ => configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection)));

        // Avalonia's AppBuilder has no service collection to register the handler and renderer into,
        // unlike MAUI's. An application that needs them beyond what ReportView already wires up -
        // exporting outside the control, resolving assets itself - reaches them here, on the ambient
        // ReportPlatformHandler and ReportViewRenderer.Instance the control itself relies on, rather
        // than through DI.
        return builder;
    }
}
