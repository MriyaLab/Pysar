using Pysar.Export;
using Xunit;

namespace Pysar.Uno.Tests;

/// <summary>
///     Only argument validation and the contract are covered: every path past those launches a
///     share sheet, a download, or a shell process, which a test run must not do.
/// </summary>
public class UnoReportSharerTests
{
    [Fact]
    public void IsAReportSharer()
        => Assert.IsAssignableFrom<IReportSharer>(new UnoReportSharer());

    [Fact]
    public async Task RejectsMissingContent()
        => await Assert.ThrowsAsync<ArgumentNullException>(
            () => new UnoReportSharer().ShareAsync(null!, "report.pdf"));

    [Fact]
    public async Task RejectsAnEmptyFileName()
        => await Assert.ThrowsAsync<ArgumentException>(
            () => new UnoReportSharer().ShareAsync([1], ""));
}
