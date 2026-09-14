using System.Reflection;
using Pysar.Core;
using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Uno;

/// <summary>
///     Registers Pysar with an Uno application: reports resolve their assets from the application's
///     own packaged resources, and <c>ReportView</c> renders through a shared
///     <see cref="SkiaReportRenderer"/>.
/// </summary>
/// <example>
///     <code>
///     protected override void OnLaunched(LaunchActivatedEventArgs args)
///     {
///         PysarUno.Use(typeof(App).Assembly, pysar => pysar
///             .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
///             .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
///         ...
///     }
///     </code>
/// </example>
/// <remarks>
///     A static entry point rather than a service registration: an Uno application is a
///     <c>Microsoft.UI.Xaml.Application</c> and has no service collection of its own unless it also
///     uses Uno.Extensions. This is the shape <c>Pysar.Avalonia</c> takes for the same reason.
/// </remarks>
public static class PysarUno
{
    private static IReportExportService? _exportService;

    /// <summary>
    ///     The <see cref="SkiaReportRenderer"/> installed by <see cref="Use"/>, shared with
    ///     <c>ReportView</c>.
    /// </summary>
    public static SkiaReportRenderer Renderer => ReportViewRenderer.Instance;

    /// <summary>An <see cref="IReportExportService"/> backed by the shared renderer.</summary>
    /// <remarks>
    ///     Built once and held, but dropped by <see cref="Use"/> - an export service pins the
    ///     renderer it was created with, so one cached across a later registration would keep
    ///     exporting through the previous renderer and silently miss the drawers and fonts that
    ///     registration added.
    /// </remarks>
    public static IReportExportService ExportService
        => _exportService ??= SkiaReportExport.CreateExportService(Renderer);

    /// <summary>
    ///     Registers Pysar with the application. Call once during startup, before any report is
    ///     built or any <c>ReportView</c> is shown.
    /// </summary>
    /// <param name="assetAssembly">
    ///     The assembly report assets are embedded in - typically <c>typeof(App).Assembly</c>, or the
    ///     shared project's assembly when the assets live there.
    /// </param>
    public static UnoReportPlatformHandler Use(
        Assembly assetAssembly, Action<PysarBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(assetAssembly);

        var platformHandler = new UnoReportPlatformHandler(assetAssembly);

        // Rendering reads the handler from this ambient state rather than from DI, so it is
        // installed here - before any report can be built - and not when a renderer is first used.
        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = renderer;

        // The previous one was built over the renderer this call has just replaced - see the
        // remarks on ExportService.
        _exportService = null;

        // Unlike Avalonia's AppBuilder there is nothing to defer to here: the asset file system
        // reads manifest resources, which need no platform service to be up first, so a font can be
        // registered in the same call.
        configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection));

        // Returned rather than registered: an application that needs the handler beyond what
        // ReportView wires up - resolving assets itself, preloading ms-appx content - holds on to
        // this, exactly as it would to the AppBuilder's result in Avalonia.
        return platformHandler;
    }

    /// <summary>
    ///     Registers Pysar and preloads assets from the application package first, for applications
    ///     that ship them as <c>Content</c> rather than <c>EmbeddedResource</c>.
    /// </summary>
    public static async Task<UnoReportPlatformHandler> UseAsync(
        Assembly assetAssembly, IEnumerable<string> preloadPaths, Action<PysarBuilder>? configure = null)
    {
        ArgumentNullException.ThrowIfNull(assetAssembly);
        ArgumentNullException.ThrowIfNull(preloadPaths);

        var platformHandler = new UnoReportPlatformHandler(assetAssembly);

        // Before configure: a font registered by the callback may be one of the preloaded paths,
        // and AddFont reads it synchronously.
        await platformHandler.Assets.PreloadAsync(preloadPaths).ConfigureAwait(true);

        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();
        ReportViewRenderer.Instance = renderer;
        _exportService = null;

        configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection));

        return platformHandler;
    }
}

/// <summary>
///     The renderer the control measures reports with. A single instance carries the drawers the
///     application registered through <see cref="PysarUno.Use"/>.
/// </summary>
internal static class ReportViewRenderer
{
    private static SkiaReportRenderer? _instance;

    /// <summary>
    ///     The renderer installed by <c>PysarUno.Use</c>. Throws rather than falling back to an
    ///     unconfigured renderer: without the fonts and asset access <c>Use</c> installs, a report
    ///     renders with substitute fonts and blank images instead of reporting the mistake.
    /// </summary>
    public static SkiaReportRenderer Instance
    {
        get => _instance ?? throw new InvalidOperationException(
            "Call PysarUno.Use during application startup before using a report view or the renderer.");
        set => _instance = value;
    }
}
