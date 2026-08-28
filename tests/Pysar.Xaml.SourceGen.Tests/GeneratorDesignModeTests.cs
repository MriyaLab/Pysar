using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorDesignModeTests
{
    [Fact]
    public void InitializeComponent_ReturnsEarlyInDesignMode()
    {
        const string xaml = """
            <Report x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml));

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(
            "if (global::Pysar.Core.ReportDesignMode.IsEnabled) return;",
            result.GeneratedSource);
    }

    [Fact]
    public void DesignModeGuard_IsTheFirstStatementOfInitializeComponent()
    {
        const string xaml = """
            <Report x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var generated = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml)).GeneratedSource!;

        var bodyStart = generated.IndexOf("private void InitializeComponent()", StringComparison.Ordinal);
        var guard = generated.IndexOf("ReportDesignMode.IsEnabled", StringComparison.Ordinal);
        var firstBrace = generated.IndexOf('{', bodyStart);

        Assert.InRange(guard, firstBrace, firstBrace + 120);
    }

    [Fact]
    public void ReportView_DoesNotEmitTheDesignModeGuard()
    {
        // A component is only ever built by its own InitializeComponent - nothing repopulates it the
        // way PreviewRenderer repopulates a root report from disk. Guarding it would leave the
        // preview with an empty view.
        var src = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Header {} }",
            ("Header.rxaml",
                "<ReportView x:Class=\"MyApp.Header\" xmlns=\"https://mriyalab.com/pysar\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"><Text Content=\"hi\"/></ReportView>"))
            .GeneratedSource!;

        Assert.DoesNotContain("ReportDesignMode.IsEnabled", src);
        Assert.Contains(".AddElement(", src);
    }

    [Fact]
    public void ReportView_UsingTheRuntimeFallback_DoesNotEmitTheDesignModeGuard()
    {
        // Resources push the view onto the runtime-loader path; it must still build in design mode.
        var src = GeneratorTestHarness.Run(
            "namespace MyApp { public partial class Header {} }",
            ("Header.rxaml",
                "<ReportView x:Class=\"MyApp.Header\" xmlns=\"https://mriyalab.com/pysar\" " +
                "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\">" +
                "<ReportView.Resources><Text x:Key=\"t\" Content=\"hi\"/></ReportView.Resources>" +
                "<Text Content=\"hi\"/></ReportView>"))
            .GeneratedSource!;

        Assert.DoesNotContain("ReportDesignMode.IsEnabled", src);
        Assert.Contains("ReportXaml.LoadInto(this,", src);
    }
}
