using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorReportViewTests
{
    private const string Head =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    [Fact]
    public void ReportView_Root_EmitsPartialClassAndAddsChildren()
    {
        var src = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Header {} }",
            ("Header.rxaml", $"<ReportView x:Class=\"MyApp.Header\" {Head}>" +
                            "<Text Content=\"hi\"/></ReportView>")).GeneratedSource!;

        Assert.Contains("partial class Header : global::Pysar.Elements.ReportView", src);
        Assert.Contains(".AddElement(", src);
        Assert.DoesNotContain("Bands.Set", src);
    }

    [Fact]
    public void ReportView_RootWithXName_AssignsItselfToTheField()
    {
        var src = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Header {} }",
            ("Header.rxaml", $"<ReportView x:Class=\"MyApp.Header\" x:Name=\"root\" {Head}>" +
                            "<Text Content=\"hi\"/></ReportView>")).GeneratedSource!;

        Assert.Contains("this.root = this;", src);
    }

    [Fact]
    public void ReportView_WithResources_UsesRuntimeFallback()
    {
        var src = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Header {} }",
            ("Header.rxaml", $"<ReportView x:Class=\"MyApp.Header\" {Head}>" +
                            "<ReportView.Resources><Text x:Key=\"t\" Content=\"hi\"/></ReportView.Resources>" +
                            "<Text Content=\"hi\"/></ReportView>")).GeneratedSource!;

        Assert.Contains("ReportXaml.LoadInto(this,", src);
    }

    [Fact]
    public void IllegalRoot_ReportsDiagnostic()
    {
        var result = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Panel {} }",
            ("Panel.rxaml", $"<StackPanel x:Class=\"MyApp.Panel\" {Head}><Text Content=\"hi\"/></StackPanel>"));

        Assert.Contains(result.Diagnostics, d => d.Id == "PQX006");
    }

    [Fact]
    public void SourceBinding_EmittedAfterTheTreeIsBuilt()
    {
        var src = GeneratorTestHarness.Run(
            @"namespace MyApp {
                public partial class Header {
                    public static global::Pysar.Binding.BindableProperty TitleProperty { get; } =
                        global::Pysar.Binding.BindableProperty.Create(""Title"", typeof(string), typeof(Header), string.Empty);
                    public string Title { get => (string)GetValue(TitleProperty)!; set => SetValue(TitleProperty, value); }
                } }",
            ("Header.rxaml", $"<ReportView x:Class=\"MyApp.Header\" x:Name=\"root\" {Head}>" +
                            "<Text Content=\"{Binding Title, Source={x:Reference root}}\"/></ReportView>")).GeneratedSource!;

        Assert.Contains("this.root = this;", src);
        Assert.Contains("source: this.root", src);

        // The binding must be emitted after the element carrying it joins the tree.
        Assert.True(src.IndexOf("AddElement(", StringComparison.Ordinal)
                    < src.IndexOf("source: this.root", StringComparison.Ordinal));
    }
}
