using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class LoadTests
{
    private const string Ns = "xmlns=\"https://mriyalab.com/pysar\"";

    [Fact]
    public void Load_BareReport_ReturnsReport()
    {
        var design = ReportXaml.Load($"<Report {Ns} />");
        Assert.NotNull(design);
        Assert.IsType<Report>(design);
    }
}
