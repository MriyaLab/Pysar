using Pysar.Export;
using Pysar.Skia;
using Xunit;

namespace Pysar.Uno.Tests;

/// <summary>
///     Only argument validation and the contract are covered: every path past those launches a
///     print panel or a shell process, which a test run must not do.
/// </summary>
public class UnoReportPrinterTests
{
    [Fact]
    public void IsAReportPrinter()
        => Assert.IsAssignableFrom<IReportPrinter>(new UnoReportPrinter(new SkiaReportRenderer()));

    [Fact]
    public void RejectsAMissingRenderer()
        => Assert.Throws<ArgumentNullException>(() => new UnoReportPrinter(null!));

    [Fact]
    public async Task RejectsAMissingReport()
    {
        var printer = new UnoReportPrinter(new SkiaReportRenderer());

        await Assert.ThrowsAsync<ArgumentNullException>(() => printer.PrintAsync(null!));
    }
}
