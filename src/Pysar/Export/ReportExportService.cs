using Pysar.Elements;

namespace Pysar.Export;

internal sealed class ReportExportService : IReportExportService
{
    private readonly IReadOnlyDictionary<ExportFormat, IReportExporter> _exporters;

    public ReportExportService(IEnumerable<IReportExporter> exporters)
    {
        ArgumentNullException.ThrowIfNull(exporters);

        var byFormat = new Dictionary<ExportFormat, IReportExporter>();
        foreach (var exporter in exporters)
        {
            // Two registrations for one format is a composition mistake, and the caller needs to be
            // told which format collided - the framework's own duplicate-key message names neither.
            if (!byFormat.TryAdd(exporter.Format, exporter))
                throw new InvalidOperationException(
                    $"Two exporters are registered for format '{exporter.Format}': " +
                    $"{byFormat[exporter.Format].GetType().Name} and {exporter.GetType().Name}.");
        }

        _exporters = byFormat;
    }

    public Task ExportAsync(Report report, ExportFormat format, Stream destination, CancellationToken ct = default)
    {
        ArgumentNullException.ThrowIfNull(report);
        ArgumentNullException.ThrowIfNull(destination);

        if (!_exporters.TryGetValue(format, out var exporter))
            throw new NotSupportedException(DescribeMissingFormat(format));

        return exporter.ExportAsync(report, destination, ct);
    }

    /// <summary>
    ///     Names what is registered rather than which registration API to call: the format's own
    ///     package owns that, and it is not necessarily one this assembly can name.
    /// </summary>
    private string DescribeMissingFormat(ExportFormat format)
    {
        if (_exporters.Count == 0)
            return $"Cannot export '{format}': no export formats are registered.";

        var available = string.Join(", ", _exporters.Keys.Select(f => f.Id).Order());

        return $"Cannot export '{format}': no exporter is registered for it. Registered formats: {available}.";
    }

    public async Task<byte[]> ExportAsync(Report report, ExportFormat format, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await ExportAsync(report, format, ms, ct).ConfigureAwait(false);
        return ms.ToArray();
    }
}
