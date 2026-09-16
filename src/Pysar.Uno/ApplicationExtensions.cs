using System.Reflection;
using Microsoft.UI.Xaml;
using Pysar.Core;
using Pysar.Skia;

namespace Pysar.Uno;

/// <summary>
///     Registers Pysar with an Uno application: reports resolve their assets from the application's
///     own packaged resources, and <see cref="ReportView"/> renders through a shared
///     <see cref="SkiaReportRenderer"/>. On a browser head this also takes over Ctrl+wheel (and
///     trackpad pinch) zoom on the Uno canvas from the page, unless the application opts out through
///     the <c>suppressBrowserZoom</c> parameter below.
/// </summary>
/// <example>
///     <code>
///     protected override void OnLaunched(LaunchActivatedEventArgs args)
///     {
///         this.UsePysar(typeof(App).Assembly, pysar => pysar
///             .AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu")
///             .AddFont("Fonts/Ubuntu-Bold.ttf", "Ubuntu", FontStyle.Bold));
///         // On a browser head this call also suppresses the page's own Ctrl+wheel zoom over the
///         // Uno canvas; pass suppressBrowserZoom: false to keep it.
///         ...
///     }
///     </code>
/// </example>
/// <remarks>
///     An extension on <c>Application</c> rather than on a host builder, for its own version of the
///     same reason <c>Pysar.Wpf</c> is: an Uno application has no service collection unless it also
///     uses Uno.Extensions, and <c>UnoPlatformHostBuilder</c> exists only in the WebAssembly and
///     Desktop heads - Android, iOS and WinAppSDK start through <c>Application.Start</c>.
///     <c>OnLaunched</c> is the one place every head runs.
/// </remarks>
public static class ApplicationExtensions
{
    /// <summary>Registers Pysar with the application.</summary>
    /// <param name="assetAssembly">
    ///     The assembly report assets are embedded in - typically <c>typeof(App).Assembly</c>, or the
    ///     shared project's assembly when the assets live there. Required, unlike the WPF
    ///     counterpart's assembly name: on the WebAssembly head the entry assembly is the head
    ///     project, not the application project the assets are packaged into.
    /// </param>
    /// <param name="suppressBrowserZoom">
    ///     On a browser head, stops the page zooming when Ctrl (or Command) plus wheel - or the
    ///     trackpad pinch delivered as the same event - is meant for the report. A no-op on every
    ///     other host.
    ///
    ///     Pass <c>false</c> to leave the browser's own zoom alone. The suppression covers the Uno
    ///     canvas and is permanent once installed: Uno draws the whole application into a single
    ///     canvas, so the listener cannot tell the report from the toolbar beside it, and it is never
    ///     removed. Anything on the page outside that canvas - a host page's own markup around the
    ///     Uno application - keeps the browser's zoom. An application that would rather keep the
    ///     browser's zoom on the canvas too - an accessibility feature - opts out here.
    /// </param>
    public static Application UsePysar(
        this Application application,
        Assembly assetAssembly,
        Action<PysarBuilder>? configure = null,
        bool suppressBrowserZoom = true)
    {
        ArgumentNullException.ThrowIfNull(application);

        UnoRegistration.Install(assetAssembly, configure, suppressBrowserZoom);

        return application;
    }

    /// <summary>
    ///     Registers Pysar and preloads assets from <c>Assets/</c> in the application package first,
    ///     for applications that ship them as <c>Content</c> rather than <c>EmbeddedResource</c>.
    /// </summary>
    /// <example>
    ///     <code>
    ///     protected override async void OnLaunched(LaunchActivatedEventArgs args)
    ///     {
    ///         // File on disk: Assets/Fonts/Ubuntu-Regular.ttf
    ///         await this.UsePysarAsync(
    ///             typeof(App).Assembly,
    ///             ["Fonts/Ubuntu-Regular.ttf"],
    ///             pysar => pysar.AddFont("Fonts/Ubuntu-Regular.ttf", "Ubuntu"));
    ///         ...
    ///     }
    ///     </code>
    ///     <c>async void</c> is the intended shape here, not a shortcut: <c>OnLaunched</c> is void and
    ///     offers no other place to hang an <c>await</c> - returning early and continuing on a
    ///     captured continuation is the only option a WinUI override allows. That means an exception
    ///     out of the preload becomes an unhandled crash rather than a faulted <see cref="Task"/> some
    ///     caller can observe, so the application should not let it throw - a missing or unreadable
    ///     preload path should be treated as a startup bug to fix, not a runtime condition to catch.
    /// </example>
    /// <remarks>
    ///     Asynchronous because <c>StorageFile</c> is: on the WebAssembly host an
    ///     <c>ms-appx:///Assets/...</c> read is an HTTP fetch, and blocking on it deadlocks the
    ///     single thread. The preload has to finish before <paramref name="configure"/> runs, because
    ///     <c>AddFont</c> reads synchronously - which is why the two are one call and not two.
    ///
    ///     Returns <see cref="Task{Application}"/> for symmetry with <see cref="UsePysar"/> rather
    ///     than because anything needs the awaited value: called from <c>async void OnLaunched</c> as
    ///     shown above, the result has nowhere to go and nothing in this package reads it back.
    /// </remarks>
    /// <param name="assetAssembly">
    ///     The assembly report assets are embedded in, for paths not covered by
    ///     <paramref name="preloadPaths"/>. See <see cref="UsePysar"/>.
    /// </param>
    /// <param name="preloadPaths">
    ///     Report paths to fetch once at startup (<c>Fonts/...</c>, not <c>Assets/Fonts/...</c>).
    ///     They are read from <c>ms-appx:///Assets/...</c>.
    /// </param>
    /// <param name="suppressBrowserZoom">See <see cref="UsePysar"/>.</param>
    public static async Task<Application> UsePysarAsync(
        this Application application,
        Assembly assetAssembly,
        IEnumerable<string> preloadPaths,
        Action<PysarBuilder>? configure = null,
        bool suppressBrowserZoom = true)
    {
        ArgumentNullException.ThrowIfNull(application);

        await UnoRegistration
            .InstallAsync(assetAssembly, preloadPaths, configure, suppressBrowserZoom)
            .ConfigureAwait(true);

        return application;
    }
}
