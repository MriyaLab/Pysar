using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

public class GeneratorSmokeTests
{
    [Fact]
    public void NoXaml_ProducesNoOutput()
    {
        var result = GeneratorTestHarness.Run("namespace X { public partial class Y {} }");
        Assert.Null(result.GeneratedSource);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void XClass_IsCanonicalAndProducesNoCompatibilityWarning()
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
        Assert.DoesNotContain(result.Diagnostics, diagnostic => diagnostic.Id == "PQX005");
    }

    [Fact]
    public void XClass_In2009Namespace_IsRecognizedAndGenerates()
    {
        const string xaml = """
            <Report x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2009/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml));

        Assert.NotNull(result.GeneratedSource);
    }

    [Fact]
    public void LegacyCodeBehind_RemainsSupportedWithWarning()
    {
        const string xaml = """
            <Report CodeBehind="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml));

        Assert.NotNull(result.GeneratedSource);
        Assert.Contains(result.Diagnostics, diagnostic =>
            diagnostic.Id == "PQX005"
            && diagnostic.Severity == Microsoft.CodeAnalysis.DiagnosticSeverity.Warning);
    }

    [Fact]
    public void CodeBehindAndXClass_ReportConflict()
    {
        const string xaml = """
            <Report CodeBehind="X.Y"
                    x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml));

        Assert.Null(result.GeneratedSource);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PQX004");
    }

    [Fact]
    public void A_xaml_File_IsNotAReport_EvenWhenItsContentWouldParse()
    {
        // The extension is the whole membership test. A host framework's App.xaml sits in the same
        // project and carries x:Class too, so nothing about the content can be relied on here.
        const string xaml = """
            <Report x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.xaml", xaml));

        Assert.Null(result.GeneratedSource);
        Assert.Empty(result.Diagnostics);
    }

    [Fact]
    public void A_rxaml_File_WithAForeignRoot_IsReported_NotSkipped()
    {
        // Once .rxaml is ours alone, a foreign root in one is a mistake the author wants to hear
        // about. Silently skipping it would surface later as a missing InitializeComponent.
        const string xaml = """
            <ContentPage x:Class="X.Y"
                    xmlns="http://schemas.microsoft.com/dotnet/maui"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            ("Report.rxaml", xaml));

        Assert.Null(result.GeneratedSource);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PQX002");
    }

    [Fact]
    public void WithoutTheXamlRuntime_TheMissingReferenceIsNamed()
    {
        // Generated code always calls into Pysar.Xaml, but the generator ships as an
        // analyzer with its dependencies suppressed, so nothing pulls that assembly in. Left
        // undiagnosed this arrives as CS0234 inside code the author never wrote.
        const string xaml = """
            <Report x:Class="X.Y"
                    xmlns="https://mriyalab.com/pysar"
                    xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml" />
            """;

        var result = GeneratorTestHarness.Run(
            "namespace X { public partial class Y {} }",
            withXamlRuntime: false,
            ("Report.rxaml", xaml));

        Assert.Null(result.GeneratedSource);
        Assert.Contains(result.Diagnostics, diagnostic => diagnostic.Id == "PQX007");
    }
}
