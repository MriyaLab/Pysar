using Pysar.Elements;

namespace Pysar.Export;

public interface IReportExportService
{
    /// <summary>Writes <paramref name="report"/> in <paramref name="format"/> into <paramref name="destination"/>.</summary>
    Task ExportAsync(Report report, ExportFormat format, Stream destination, CancellationToken ct = default);

    /// <summary>Convenience overload for callers that want the exported bytes directly.</summary>
    Task<byte[]> ExportAsync(Report report, ExportFormat format, CancellationToken ct = default);
}
