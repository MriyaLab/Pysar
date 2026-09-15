using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Uno;

/// <summary>
///     Public access to what <see cref="ApplicationExtensions.UsePysar"/> installed - the renderer,
///     the export service and the platform handler - the role <c>PysarWpf</c> plays for WPF.
/// </summary>
public static class PysarUno
{
    private static IReportExportService? _exportService;

    /// <summary>
    ///     The <see cref="SkiaReportRenderer"/> installed by
    ///     <see cref="ApplicationExtensions.UsePysar"/>, shared with <c>ReportView</c>.
    /// </summary>
    public static SkiaReportRenderer Renderer => ReportViewRenderer.Instance;

    /// <summary>An <see cref="IReportExportService"/> backed by the shared renderer.</summary>
    /// <remarks>
    ///     Built once and held, but dropped by <see cref="ApplicationExtensions.UsePysar"/> - an
    ///     export service pins the renderer it was created with, so one cached across a later
    ///     registration would keep exporting through the previous renderer and silently miss the
    ///     drawers and fonts that registration added.
    /// </remarks>
    public static IReportExportService ExportService
        => _exportService ??= SkiaReportExport.CreateExportService(Renderer);

    /// <summary>The handler the last registration installed.</summary>
    /// <remarks>
    ///     An Uno application has no service collection to resolve this from - see the remarks on
    ///     <see cref="ApplicationExtensions"/> - so this is how it is reached: for an application
    ///     that needs it beyond what <c>ReportView</c> wires up, resolving assets itself, preloading
    ///     ms-appx content. Throws rather than returning null: reading it before registration is the
    ///     same mistake as rendering before registration.
    /// </remarks>
    public static UnoReportPlatformHandler PlatformHandler
        => UnoRegistration.Current ?? throw new InvalidOperationException(
            "Call Application.UsePysar during application startup before reading the platform handler.");

    /// <summary>Drops the cached export service, so the next read builds one over the new renderer.</summary>
    internal static void ResetExportService() => _exportService = null;
}
