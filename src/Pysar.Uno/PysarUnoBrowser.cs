using System.Reflection;
using System.Runtime.InteropServices.JavaScript;
using System.Text;

namespace Pysar.Uno;

/// <summary>
///     The one thing a browser head has to ask for by hand.
/// </summary>
public static class PysarUnoBrowser
{
    private const string ZoomModule = "Pysar.Uno.reportViewZoom";

    private const string ZoomScriptResource = "Pysar.Uno.Scripts.reportViewZoom.js";

    private static Task? _zoomImport;

    /// <summary>
    ///     Makes Ctrl (or Command) plus wheel zoom the report rather than the page, and with it the
    ///     trackpad pinch that Chrome and Safari deliver as the same event. Call once from the
    ///     WebAssembly head's entry point; it does nothing anywhere else.
    /// </summary>
    /// <example>
    ///     <code>
    ///     public static async Task Main(string[] args)
    ///     {
    ///         await PysarUnoBrowser.UseWheelZoomAsync();
    ///         ...
    ///     }
    ///     </code>
    /// </example>
    /// <remarks>
    ///     <see cref="ReportView"/> has always zoomed on a modified wheel and marks the event
    ///     handled. In a browser that is not enough: "handled" is a managed routing flag, while the
    ///     page zoom is a DOM default action only <c>preventDefault</c> can stop. This installs the
    ///     listener that does it.
    ///
    ///     It is a call the application makes rather than something <see cref="ReportView"/> does on
    ///     load, because the script has to be imported and a control cannot await. Keeping it
    ///     explicit also leaves the choice with the application: suppressing the browser's own zoom
    ///     over the canvas is its decision to make, not a control's.
    /// </remarks>
    public static Task UseWheelZoomAsync()
    {
        if (!OperatingSystem.IsBrowser())
            return Task.CompletedTask;

        // Held as the task, so a second call awaits the first import rather than starting another.
        return _zoomImport ??= ImportAsync();
    }

    private static Task ImportAsync()
#pragma warning disable CA1416 // Guarded by OperatingSystem.IsBrowser above; this assembly targets
                               // net10.0, so the analyzer cannot see that the call site is reachable
                               // only on the browser.
        => JSHost.ImportAsync(ZoomModule, BuildScriptUrl());
#pragma warning restore CA1416

    /// <summary>
    ///     The script as a data URL. Embedded and inlined rather than served from wwwroot: static
    ///     web assets need the Razor SDK, and asking the application to copy a script into its own
    ///     head is the duplication this method exists to avoid.
    /// </summary>
    private static string BuildScriptUrl()
    {
        using var stream = typeof(PysarUnoBrowser).Assembly.GetManifestResourceStream(ZoomScriptResource)
            ?? throw new InvalidOperationException($"Embedded script '{ZoomScriptResource}' is missing.");

        using var reader = new StreamReader(stream, Encoding.UTF8);

        // Base64 rather than percent-encoding: it keeps every quote and newline in the script out
        // of the URL's own grammar.
        return "data:text/javascript;base64,"
            + Convert.ToBase64String(Encoding.UTF8.GetBytes(reader.ReadToEnd()));
    }
}
