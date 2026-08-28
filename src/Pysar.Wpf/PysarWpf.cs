using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Wpf;

/// <summary>
///     Public access to the shared renderer and export service registered by
///     <see cref="ApplicationExtensions.UsePysar"/>.
/// </summary>
public static class PysarWpf
{
    private static IReportExportService? _exportService;

    /// <summary>
    ///     The <see cref="SkiaReportRenderer"/> installed by <c>UsePysar</c>, shared with
    ///     the report view control.
    /// </summary>
    public static SkiaReportRenderer Renderer => ReportViewRenderer.Instance;

    /// <summary>An <see cref="IReportExportService"/> backed by the shared renderer.</summary>
    public static IReportExportService ExportService
        => _exportService ??= SkiaReportExport.CreateExportService(Renderer);
}
