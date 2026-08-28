using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

/// <summary>
///     A collection property element is a supported runtime feature (see
///     <c>Pysar.Xaml.Tests.GridDefinitionTests</c>). The compiled path has to agree with it:
///     markup that works today must not change meaning when <c>x:Class</c> is added.
/// </summary>
public class GeneratorCollectionPropertyTests
{
    private const string Head =
        "xmlns=\"https://mriyalab.com/pysar\" "
        + "xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static GenResult Run(string xaml)
        => GeneratorTestHarness.Run("namespace MyApp { public partial class R {} }", ("R.rxaml", xaml));

    private static string Generate(string xaml)
        => Run(xaml).GeneratedSource ?? throw new Xunit.Sdk.XunitException("no generated source");

    [Fact]
    public void ColumnDefinitions_ThreeChildren_AreAllEmitted()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}><DetailBand><Grid>"
            + "<Grid.ColumnDefinitions>"
            + "<ColumnDefinition Width=\"Auto\" />"
            + "<ColumnDefinition Width=\"*\" />"
            + "<ColumnDefinition Width=\"60\" />"
            + "</Grid.ColumnDefinitions>"
            + "</Grid></DetailBand></Report>");

        // Either the emitter builds the collection itself, or it hands the document to the runtime
        // loader. What it must not do is assign an empty collection and drop the children.
        if (source.Contains("ReportXaml.LoadInto(this,"))
            return;

        Assert.Equal(3, CountOccurrences(source, "new global::Pysar.Elements.ColumnDefinition()"));
    }

    [Fact]
    public void ColumnDefinitions_SingleChild_IsEmitted()
    {
        var source = Generate(
            $"<Report x:Class=\"MyApp.R\" {Head}><DetailBand><Grid>"
            + "<Grid.ColumnDefinitions><ColumnDefinition Width=\"Auto\" /></Grid.ColumnDefinitions>"
            + "</Grid></DetailBand></Report>");

        if (source.Contains("ReportXaml.LoadInto(this,"))
            return;

        Assert.Equal(1, CountOccurrences(source, "new global::Pysar.Elements.ColumnDefinition()"));
    }

    [Fact]
    public void PrefixlessCollectionChild_DoesNotKillTheGenerator()
    {
        // <ColumnDefinitions> names a property, not a type: the emitter cannot resolve it. It has to
        // fall back rather than throw, which Roslyn would surface as CS8785 with no XAML location.
        var result = Run(
            $"<Report x:Class=\"MyApp.R\" {Head}><DetailBand><Grid>"
            + "<ColumnDefinitions><ColumnDefinition Width=\"Auto\" /></ColumnDefinitions>"
            + "</Grid></DetailBand></Report>");

        Assert.DoesNotContain(result.Diagnostics, d => d.Id == "CS8785");
        Assert.NotNull(result.GeneratedSource);
        Assert.Contains("ReportXaml.LoadInto(this,", result.GeneratedSource);
    }

    private static int CountOccurrences(string haystack, string needle)
    {
        var count = 0;
        for (var i = haystack.IndexOf(needle, StringComparison.Ordinal);
             i >= 0;
             i = haystack.IndexOf(needle, i + needle.Length, StringComparison.Ordinal))
            count++;

        return count;
    }
}
