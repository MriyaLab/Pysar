using Pysar.Export;

namespace Pysar.Skia;

public static class SkiaReportExport
{
    public static IReportExportService CreateExportService(SkiaReportRenderer renderer) =>
        new ReportExportService(new IReportExporter[] { new PdfReportExporter(renderer) });
}
