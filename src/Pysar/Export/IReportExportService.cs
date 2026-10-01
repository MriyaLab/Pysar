using Pysar.Elements;

namespace Pysar.Export;

public interface IReportExportService
{
    /// <summary>Writes <paramref name="report"/> in <paramref name="format"/> into <paramref name="destination"/>.</summary>
    Task ExportAsync(Report report, ExportFormat format, Stream destination, CancellationToken ct = default);

    /// <summary>Convenience overload for callers that want the exported bytes directly.</summary>
    Task<byte[]> ExportAsync(Report report, ExportFormat format, CancellationToken ct = default);

    /// <summary>
    ///     Writes <paramref name="report"/> in <paramref name="format"/>, applying
    ///     <paramref name="options"/>. The exporter rejects an options type it does not own.
    /// </summary>
    Task ExportAsync(
        Report report, ExportFormat format, Stream destination, ExportOptions options, CancellationToken ct = default)
        => throw new NotSupportedException($"{GetType().Name} does not accept export options.");

    /// <summary>Convenience overload for callers that want the exported bytes directly.</summary>
    async Task<byte[]> ExportAsync(
        Report report, ExportFormat format, ExportOptions options, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await ExportAsync(report, format, ms, options, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
