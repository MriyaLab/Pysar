using System.Diagnostics;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace Pysar.Uno;

/// <summary>
///     Keeps a browser head from zooming the page when a wheel notch is meant for the report.
/// </summary>
/// <remarks>
///     <see cref="ReportView"/> has always zoomed on a modified wheel and marks the event handled.
///     In a browser that is not enough: "handled" is a managed routing flag, while the page zoom is
///     a DOM default action only <c>preventDefault</c> can stop. This imports the script that does
///     it.
///
///     Installed by registration rather than by the control: <see cref="ReportView"/> cannot exist
///     without registration - <c>ReportViewRenderer.Instance</c> throws - so registration is
///     guaranteed to come first, and is strictly earlier than the control's <c>Loaded</c>. The
///     application opts out through <c>UsePysar(..., suppressBrowserZoom: false)</c>, because the
///     suppression, once installed, covers the Uno canvas permanently: Uno draws the whole
///     application into one canvas, so it cannot tell the report from the toolbar beside it, and
///     nothing outside that canvas is touched - a host page's own markup around the Uno application
///     keeps its zoom. The module exports nothing to call back into either (that needs [JSImport],
///     which needs a browser target framework, which this package cannot have without moving to
///     Uno.Sdk and pinning an Uno version), so there is no handle to remove the listener with.
/// </remarks>
internal static class BrowserWheelZoom
{
    private const string ZoomModule = "Pysar.Uno.reportViewZoom";

    private const string ZoomScriptResource = "Pysar.Uno.Scripts.reportViewZoom.js";

    private static Task? _zoomImport;

    /// <summary>
    ///     Imports the wheel listener once. A no-op anywhere but a browser, and the same completed
    ///     task on every call after the first.
    /// </summary>
    /// <remarks>
    ///     Never faults, so a synchronous caller can discard it and an asynchronous one can await it
    ///     without guarding. An import that fails leaves the browser zooming the page - the
    ///     behaviour without the script at all - and failing application startup over a convenience
    ///     would be the worse outcome.
    ///
    ///     A failed import is cached exactly as permanently as a successful one: nothing here ever
    ///     retries, not even across a second <c>UnoRegistration.Install</c> call, which deliberately
    ///     rebuilds the renderer and resets the cached export service. That is deliberate, not an
    ///     oversight symmetric with those resets: a successful import installs a page-wide listener
    ///     that outlives any re-registration, so there is nothing to redo, and the one failure a
    ///     retry could help with - the script missing from the package - is still missing on the
    ///     next attempt, so retrying would just repeat the same failed import.
    /// </remarks>
    internal static Task EnsureInstalledAsync()
    {
        if (!OperatingSystem.IsBrowser())
            return Task.CompletedTask;

        // Held as the task, so a second call awaits the first import rather than starting another.
        // Because ImportAsync swallows, what is cached is always a task that completes. The ??=
        // itself is not thread-safe against two callers both observing null - it only holds here
        // because the IsBrowser guard above confines this statement to the browser's single-threaded
        // Uno host; do not lift this pattern onto a path that can run on a desktop thread pool.
        return _zoomImport ??= ImportAsync();
    }

    /// <summary>
    ///     Async rather than an expression body returning <c>JSHost.ImportAsync</c> directly, which
    ///     is what makes the catch total: inside an async method a synchronous throw from
    ///     <see cref="BuildScriptUrl"/> - a missing embedded script - becomes a faulted task like
    ///     any other, instead of propagating out of the caller before a task exists.
    /// </summary>
    private static async Task ImportAsync()
    {
        try
        {
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsBrowser in EnsureInstalledAsync; this
                               // assembly targets net10.0, so the analyzer cannot see that the call
                               // site is reachable only on the browser.
            await JSHost.ImportAsync(ZoomModule, BuildScriptUrl()).ConfigureAwait(true);
#pragma warning restore CA1416
        }
        catch (Exception ex)
        {
            // See the remarks on EnsureInstalledAsync: the report still zooms, the page zooms with
            // it, and the application starts. Caught broadly because the failure modes are not one
            // type - a missing manifest resource, a module the browser refuses to evaluate, and
            // whatever the host's own interop raises are all the same outcome here.
            //
            // Traced rather than rethrown, so the symptom "wheel zoom does nothing on WASM" has
            // something to grep for instead of failing silently at a breakpoint nobody set. Still
            // Debug-only: a shipped Release application degrades exactly as silently as before this
            // trace existed - this is a development aid, not error reporting.
            Debug.WriteLine($"[Pysar.Uno] Wheel-zoom script did not import; the browser keeps its own page zoom. {ex}");
        }
    }

    /// <summary>
    ///     The script as a data URL. Embedded and inlined rather than served from wwwroot: static
    ///     web assets need the Razor SDK, and asking the application to copy a script into its own
    ///     head is the duplication this type exists to avoid.
    /// </summary>
    private static string BuildScriptUrl()
    {
        using var stream = typeof(BrowserWheelZoom).Assembly.GetManifestResourceStream(ZoomScriptResource)
            ?? throw new InvalidOperationException($"Embedded script '{ZoomScriptResource}' is missing.");

        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Base64 rather than percent-encoding: it keeps every quote and newline in the script out
        // of the URL's own grammar.
        return "data:text/javascript;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
    }
}
