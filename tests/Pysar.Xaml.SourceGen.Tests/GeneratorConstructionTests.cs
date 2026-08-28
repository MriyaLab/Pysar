using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorConstructionTests
{
    private const string Head = "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static string Gen(string xaml) =>
        GeneratorTestHarness.Run("namespace MyApp { public partial class R {} }", ("R.rxaml", xaml)).GeneratedSource
        ?? throw new Xunit.Sdk.XunitException("no generated source");

    [Fact]
    public void Simple_Report_Emits_Construction_NotLoadInto()
    {
        var src = Gen($"<Report x:Class=\"MyApp.R\" {Head}>" +
                      "<PageHeaderBand x:Name=\"Header\" BackgroundColor=\"#ECECEC\"/>" +
                      $"<DetailBand DataSource=\"{{Binding Rows}}\">" +
                      $"<Text x:Name=\"Cell\" Content=\"{{Binding Value}}\"/>" +
                      "</DetailBand></Report>");

        Assert.DoesNotContain("LoadInto(this,", src);
        Assert.Contains("new global::Pysar.Elements.PageHeaderBand()", src);
        Assert.Contains("XamlValueConverter.Convert(\"#ECECEC\"", src);
        Assert.Contains(".SetBinding(global::Pysar.Elements.DetailBand.DataSourceProperty, \"Rows\")", src);
        Assert.Contains(".SetBinding(global::Pysar.Elements.Text.ContentProperty, \"Value\")", src);
        Assert.Contains("this.Bands.Set(", src);
        Assert.Contains("this.Header = ", src);
        Assert.Contains(".AddElement(", src);
    }

    [Fact]
    public void Resource_Report_FallsBack_To_LoadInto()
    {
        var src = Gen($"<Report x:Class=\"MyApp.R\" {Head}>" +
                      "<Report.Resources><Color x:Key=\"B\">#111</Color></Report.Resources>" +
                      $"<PageHeaderBand BackgroundColor=\"{{StaticResource B}}\"/></Report>");
        Assert.Contains("LoadInto(this,", src);
    }

    [Fact]
    public void Report_RootWithXName_AssignsItselfToTheField()
    {
        // Regression: EmitReportBody never set this.Root = this (ReportView did), so
        // Source={x:Reference Root} bindings kept a null source and page numbers printed blank.
        var src = Gen($"<Report x:Class=\"MyApp.R\" x:Name=\"Root\" {Head}>" +
                      "<PageFooterBand Height=\"20\">" +
                      "<Text Content=\"{Binding PageNumber, Source={x:Reference Root}}\"/>" +
                      "</PageFooterBand>" +
                      "<DetailBand/></Report>");

        Assert.Contains("this.Root = this;", src);
        Assert.Contains("source: this.Root", src);
        Assert.True(
            src.IndexOf("this.Root = this;", StringComparison.Ordinal)
            < src.IndexOf("source: this.Root", StringComparison.Ordinal),
            "Root must be assigned before deferred Source bindings run.");
    }
}
