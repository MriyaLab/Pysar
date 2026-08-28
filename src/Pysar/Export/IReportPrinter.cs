using Pysar.Elements;

namespace Pysar.Export;

/// <summary>
///     Opens the host print UI for a built report. Every implementation expects a report whose
///     <c>Build()</c> has already been called.
/// </summary>
public interface IReportPrinter
{
    /// <summary>
    ///     Renders <paramref name="report"/> as a vector PDF and presents the OS or browser print
    ///     dialog for the full document. Cancellation applies to rendering; dismissing the dialog
    ///     is not treated as an error.
    /// </summary>
    Task PrintAsync(Report report, CancellationToken cancellationToken = default);
}
