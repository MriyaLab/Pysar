using Pysar.Export;
using Pysar.Skia;

namespace Pysar.Avalonia;

/// <summary>
///     Public access to the shared renderer and export service registered by
///     <see cref="AppBuilderExtensions.UseQReport"/>.
/// </summary>
public static class QReportAvalonia
{
    private static IReportExportService? _exportService;

    /// <summary>
    ///     The <see cref="SkiaReportRenderer"/> installed by <c>UseQReport</c>, shared with
    ///     <see cref="ReportView"/>.
    /// </summary>
    public static SkiaReportRenderer Renderer => ReportViewRenderer.Instance;

    /// <summary>An <see cref="IReportExportService"/> backed by the shared renderer.</summary>
    public static IReportExportService ExportService
        => _exportService ??= SkiaReportExport.CreateExportService(Renderer);
}
