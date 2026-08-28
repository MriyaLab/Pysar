using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorParityTests
{
    private const string Head =
        "xmlns=\"https://mriyalab.com/pysar\" "
        + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static string Generate(string xaml)
        => GeneratorTestHarness.Run(
               "namespace MyApp { public partial class R {} }",
               ("R.rxaml", xaml))
               .GeneratedSource
           ?? throw new Xunit.Sdk.XunitException("no generated source");

    [Fact]
    public void Binding_WithQuotedCommaFormat_UsesSharedMarkupSemantics()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}>"
            + "<DetailBand><Text Content=\"{Binding Path=Total, StringFormat='Total: {0:N2}, USD'}\"/>"
            + "</DetailBand></Report>");

        Assert.Contains(
            ".SetBinding(global::Pysar.Elements.Text.ContentProperty, \"Total\", \"Total: {0:N2}, USD\")",
            source);
    }

    [Fact]
    public void StaticResource_Value_UsesRuntimeFallback()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}>"
            + "<PageHeaderBand BackgroundColor=\"{StaticResource Brand}\"/>"
            + "</Report>");

        Assert.Contains("ReportXaml.LoadInto(this,", source);
    }

    [Fact]
    public void RuntimeFallback_EmitsSourceBaseDirectory()
    {
        // Built from a rooted path rather than written out as "/tmp/qreport": the generator emits the
        // directory of Path.GetFullPath, which on Windows re-roots a POSIX-looking path onto whatever
        // drive the tests happen to run from.
        var directory = Path.GetFullPath(Path.Combine(Path.GetTempPath(), "qreport"));

        var source = GeneratorTestHarness.Run(
                   "namespace MyApp { public partial class R {} }",
                   (Path.Combine(directory, "R.rxaml"),
                       $"<Report x:Class=\"MyApp.R\" {Head}>"
                       + "<Report.Resources><ResourceDictionary Source=\"R.Resources.xaml\" /></Report.Resources>"
                       + "</Report>"))
               .GeneratedSource
           ?? throw new Xunit.Sdk.XunitException("no generated source");

        Assert.Contains("ReportXaml.LoadInto(this,", source);

        // The directory reaches the generated file as a C# string literal, so separators are escaped.
        Assert.Contains($"\"{directory.Replace("\\", "\\\\")}\"", source);
    }

    [Fact]
    public void MergedResourceDictionary_Report_UsesRuntimeFallback()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}>"
            + "<Report.Resources><ResourceDictionary><ResourceDictionary.MergedDictionaries>"
            + "<ResourceDictionary Source=\"Colors.xaml\" />"
            + "</ResourceDictionary.MergedDictionaries></ResourceDictionary></Report.Resources>"
            + "<PageHeaderBand BackgroundColor=\"{StaticResource LightGray}\"/>"
            + "</Report>");

        Assert.Contains("ReportXaml.LoadInto(this,", source);
        Assert.DoesNotContain("XamlValueConverter.Convert(\"{StaticResource LightGray}\"", source);
    }

    [Fact]
    public void TriggerPropertyElement_UsesRuntimeFallback()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}>"
            + "<DetailBand><Text><Text.Triggers><DataTrigger Binding=\"Enabled\"/>"
            + "</Text.Triggers></Text></DetailBand></Report>");

        Assert.Contains("ReportXaml.LoadInto(this,", source);
    }
}
