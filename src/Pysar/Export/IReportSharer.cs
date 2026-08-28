namespace Pysar.Export;

/// <summary>
///     Offers exported report bytes to the host's share sheet or equivalent. Format-agnostic: it
///     takes the bytes an <see cref="IReportExportService"/> produced, never a report, so a new
///     export format needs no change here.
/// </summary>
public interface IReportSharer
{
    Task ShareAsync(byte[] content, string fileName, string? title = null, CancellationToken ct = default);
}
