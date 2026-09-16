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

    [Fact]
    public void Load_UnknownMarkupExtension_IncludesLineAndColumn()
    {
        var ex = Assert.Throws<XamlException>(
            () => ReportXaml.Load($"<Report {Ns}><DetailBand><Text Content=\"{{Foo Bar}}\"/></DetailBand></Report>"));

        Assert.True(ex.Line > 0);
        Assert.True(ex.Column > 0);
        Assert.StartsWith($"{ex.Line},{ex.Column}:", ex.Message);
    }
}
