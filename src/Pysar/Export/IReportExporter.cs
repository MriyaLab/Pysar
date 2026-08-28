using Pysar.Elements;

namespace Pysar.Export;

/// <summary>One format's export strategy. Not public: consumers use <see cref="IReportExportService"/>.</summary>
internal interface IReportExporter
{
    ExportFormat Format { get; }

    Task ExportAsync(Report report, Stream destination, CancellationToken ct = default);
}
