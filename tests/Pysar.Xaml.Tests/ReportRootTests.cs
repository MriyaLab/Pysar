using Pysar.Core.Enums;
using Pysar.Elements;
using Pysar.Xaml;
using Xunit;

namespace Pysar.Xaml.Tests;

public class ReportRootTests
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void Report_PageFormatChild_SetsPageFormat()
    {
        var design = ReportXaml.Load(
            $"<Report {Root}><PageFormat Size=\"A4\" Orientation=\"Landscape\" Margin=\"30\"/></Report>");
        Assert.Equal(PageSize.A4, design.PageFormat.Size);
        Assert.Equal(Orientation.Landscape, design.PageFormat.Orientation);
    }

    [Fact]
    public void Report_BandChildren_PopulateBands()
    {
        var design = ReportXaml.Load($"<Report {Root}><PageHeaderBand/><DetailBand/></Report>");
        Assert.NotNull(design.PageHeader);
        Assert.NotNull(design.Detail);
    }

    [Fact]
    public void Report_XName_Captured()
    {
        var result = new XamlLoaderTestAccess().LoadWithNames(
            $"<Report {Root}><PageHeaderBand x:Name=\"header\"/></Report>");
        Assert.True(result.Names.ContainsKey("header"));
        Assert.IsType<PageHeaderBand>(result.Names["header"]);
    }
}
