using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorEmitTests
{
    private const string Xaml = """
    <Report x:Class="MyApp.SalesReport"
            xmlns="https://mriyalab.com/pysar"
            xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
      <PageHeaderBand x:Name="header"/>
      <DetailBand>
        <Text x:Name="title"/>
      </DetailBand>
    </Report>
    """;

    private static string Generate() =>
        GeneratorTestHarness.Run("namespace MyApp { public partial class SalesReport {} }",
            ("SalesReport.rxaml", Xaml)).GeneratedSource
        ?? throw new Xunit.Sdk.XunitException("no generated source");

    [Fact]
    public void Emits_Partial_With_Base_Fields_And_InitializeComponent()
    {
        var src = Generate();
        Assert.Contains("partial class SalesReport", src);
        Assert.Contains(": global::Pysar.Elements.Report", src);
        Assert.Contains("global::Pysar.Elements.PageHeaderBand header", src);
        Assert.Contains("global::Pysar.Elements.Text title", src);
        Assert.Contains("void InitializeComponent()", src);
        // This XAML uses no resources/styles, so it now compiles to construction C#
        // rather than the runtime LoadInto path.
        Assert.Contains("new global::Pysar.Elements.PageHeaderBand()", src);
        Assert.Contains("this.header = ", src);
        Assert.Contains("this.title = ", src);
    }
}
