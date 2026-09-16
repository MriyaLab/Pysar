using System.Reflection;
using Microsoft.UI.Dispatching;
using Pysar.Core;
using Pysar.Skia;

namespace Pysar.Uno;

/// <summary>
///     The installation sequence behind <see cref="ApplicationExtensions.UsePysar"/>: the ambient
///     platform handler reports resolve assets through, the renderer the report view draws with,
///     and the application's own configuration on top of both.
/// </summary>
/// <remarks>
///     Internal, with the public surface an extension method on
///     <c>Microsoft.UI.Xaml.Application</c> - <see cref="ApplicationExtensions"/>. Uno.WinUI under
///     net10.0 is a reference assembly, so an <c>Application</c> cannot be constructed outside a
///     real host - <c>new Application()</c> throws <c>NotSupportedException</c>. That leaves this
///     seam as the only place tests can reach the registration logic; the extension over it is
///     delegation with a null check.
/// </remarks>
internal static class UnoRegistration
{
    /// <summary>The handler the last registration installed, exposed as <c>PysarUno.PlatformHandler</c>.</summary>
    internal static UnoReportPlatformHandler? Current { get; private set; }

    /// <summary>
    ///     The dispatcher of the thread that called <see cref="Install"/> / <see cref="InstallAsync"/>.
    ///     <c>OnLaunched</c> is the UI thread, so this is the queue macOS print has to hop onto when
    ///     <c>PrintAsync</c> is later awaited off that thread. Null when registration ran somewhere
    ///     that has no queue - tests, or a worker - and the printer then falls back to the caller's.
    /// </summary>
    internal static DispatcherQueue? UiDispatcher { get; private set; }

    /// <summary>
    ///     Registers Pysar and, when asked, suppresses the page's own wheel zoom on a browser head.
    /// </summary>
    /// <param name="suppressBrowserZoom">
    ///     Defaults to <c>true</c> at both public entry points, <see cref="ApplicationExtensions.UsePysar"/>
    ///     and <see cref="ApplicationExtensions.UsePysarAsync"/>: a browser head suppresses its own
    ///     wheel zoom unless the application opts out by passing <c>false</c> there.
    /// </param>
    internal static UnoReportPlatformHandler Install(
        Assembly assetAssembly, Action<PysarBuilder>? configure, bool suppressBrowserZoom)
    {
        ArgumentNullException.ThrowIfNull(assetAssembly);

        var platformHandler = new UnoReportPlatformHandler(assetAssembly);
        UiDispatcher = TryGetCurrentDispatcher();
        InstallAmbientState(platformHandler, configure);

        if (suppressBrowserZoom)
        {
            // Not awaited, and there is nothing to await into from a synchronous registration. The
            // discarded task is not observed here, but that is safe: EnsureInstalledAsync never
            // faults, so there is nothing an observer would have caught anyway. On the browser,
            // though, the import itself is still in flight when this method returns: the listener is
            // not live yet. Harmless - registration runs long before any report is visible - but a
            // wheel event in that window still reaches the page's own zoom.
            _ = BrowserWheelZoom.EnsureInstalledAsync();
        }

        // The handler this call installed, the same object PysarUno.PlatformHandler exposes through
        // Current - two routes to one handler, not two handlers. A method that installs something
        // returning what it installed is the ordinary shape, not an accommodation for anything else.
        return platformHandler;
    }

    /// <summary>
    ///     Registers Pysar, preloads assets from the application package first, for applications that
    ///     ship them as <c>Content</c> rather than <c>EmbeddedResource</c>, and, when asked, suppresses
    ///     the page's own wheel zoom on a browser head.
    /// </summary>
    /// <param name="suppressBrowserZoom">
    ///     See <see cref="Install"/>; reached here through
    ///     <see cref="ApplicationExtensions.UsePysarAsync"/>.
    /// </param>
    internal static async Task<UnoReportPlatformHandler> InstallAsync(
        Assembly assetAssembly,
        IEnumerable<string> preloadPaths,
        Action<PysarBuilder>? configure,
        bool suppressBrowserZoom)
    {
        ArgumentNullException.ThrowIfNull(assetAssembly);
        ArgumentNullException.ThrowIfNull(preloadPaths);

        var platformHandler = new UnoReportPlatformHandler(assetAssembly);
        UiDispatcher = TryGetCurrentDispatcher();

        // Before InstallAmbientState's configure call: a font registered by the callback may be one
        // of the preloaded paths, and AddFont reads it synchronously.
        await platformHandler.Assets.PreloadAsync(preloadPaths).ConfigureAwait(true);

        InstallAmbientState(platformHandler, configure);

        if (suppressBrowserZoom)
            await BrowserWheelZoom.EnsureInstalledAsync().ConfigureAwait(true);

        return platformHandler;
    }

    /// <summary>
    ///     Installs the ambient handler and a renderer over it, assigns <see cref="Current"/>, resets
    ///     the cached <c>PysarUno.ExportService</c>, and runs the application's configuration callback
    ///     against the result. The renderer is not held on the handler - the control reaches it
    ///     through <c>ReportViewRenderer.Instance</c>, and widening
    ///     <see cref="UnoReportPlatformHandler"/> to carry it would duplicate that.
    /// </summary>
    /// <summary>
    ///     The caller's dispatcher, or null when there isn't one. The net10.0 Uno reference
    ///     assembly throws <see cref="NotSupportedException"/> from
    ///     <c>DispatcherQueue.GetForCurrentThread</c> rather than returning null, which is what
    ///     tests hit; a missing queue is the same outcome either way.
    /// </summary>
    internal static DispatcherQueue? TryGetCurrentDispatcher()
    {
        try
        {
            return DispatcherQueue.GetForCurrentThread();
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }

    private static void InstallAmbientState(
        UnoReportPlatformHandler platformHandler, Action<PysarBuilder>? configure)
    {
        // Rendering reads the handler from this ambient state rather than from DI, so it is
        // installed here - before any report can be built - and not when a renderer is first used.
        ReportPlatformHandler.Create(platformHandler);

        var renderer = new SkiaReportRenderer();

        // The control measures reports with the same renderer, so custom drawers reach the viewer.
        ReportViewRenderer.Instance = renderer;

        Current = platformHandler;

        // The previous one was built over the renderer this call has just replaced - see the
        // remarks on PysarUno.ExportService.
        PysarUno.ResetExportService();

        // Unlike Avalonia's AppBuilder there is nothing to defer to here: the asset file system
        // reads manifest resources, which need no platform service to be up first, so a font can be
        // registered in the same call.
        configure?.Invoke(new PysarBuilder(renderer, platformHandler.FontCollection));
    }
}
