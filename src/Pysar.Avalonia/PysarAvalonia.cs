using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Avalonia;

/// <summary>
///     Public access to the shared renderer and export service registered by
///     <see cref="AppBuilderExtensions.UsePysar"/>. Avalonia's AppBuilder has no service collection,
///     so this is how a host reaches them.
/// </summary>
public static class PysarAvalonia
{
    private static IReportExportService? _exportService;

    /// <summary>
    ///     The <see cref="SkiaReportRenderer"/> installed by <c>UsePysar</c>, shared with
    ///     <see cref="ReportView"/>.
    /// </summary>
    public static SkiaReportRenderer Renderer => ReportViewRenderer.Instance;

    /// <summary>An <see cref="IReportExportService"/> backed by the shared renderer.</summary>
    public static IReportExportService ExportService
        => _exportService ??= SkiaReportExport.CreateExportService(Renderer);

    /// <summary>Drops the cached export service, so the next read builds one over the new renderer.</summary>
    internal static void ResetExportService() => _exportService = null;
}