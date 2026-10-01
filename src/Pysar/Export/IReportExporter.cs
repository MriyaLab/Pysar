using Pysar.Elements;

namespace Pysar.Export;

/// <summary>One format's export strategy. Not public: consumers use <see cref="IReportExportService"/>.</summary>
internal interface IReportExporter
{
    ExportFormat Format { get; }

    Task ExportAsync(Report report, Stream destination, CancellationToken ct = default);

    /// <summary>
    ///     Same as <see cref="ExportAsync(Report, Stream, CancellationToken)"/>, with format-specific
    ///     settings. The default refuses them so an exporter that has none cannot silently drop a request.
    /// </summary>
    Task ExportAsync(Report report, Stream destination, ExportOptions options, CancellationToken ct = default)
        => throw new NotSupportedException($"{GetType().Name} does not accept export options.");
}
