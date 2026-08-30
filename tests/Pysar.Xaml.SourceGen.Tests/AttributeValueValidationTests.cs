using System.Linq;
using Microsoft.CodeAnalysis;
using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

/// <summary>
///     A literal attribute value is converted at run time - <c>ValueConverter.Convert</c> ends in
///     <c>Enum.Parse</c> - so a misspelled enum name compiled cleanly and threw while the report was
///     being rendered. <c>PQX012</c> moves that failure to the build, where the name is already
///     known to be wrong.
/// </summary>
public class AttributeValueValidationTests
{
    private const string Types = "namespace X { public partial class Y { } }";

    private static string Report(string body) => $$"""
        <Report x:Class="X.Y"
                xmlns="https://mriyalab.com/pysar"
                xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml">
            {{body}}
        </Report>
        """;

    private static Diagnostic? InvalidValue(GenResult result) =>
        result.Diagnostics.FirstOrDefault(d => d.Id == "PQX012");

    [Fact]
    public void Misspelled_enum_value_is_reported()
    {
        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", Report("""<PageFormat Orientation="Portrait1" />""")));

        var diagnostic = InvalidValue(result);
        Assert.NotNull(diagnostic);
        Assert.Equal(DiagnosticSeverity.Error, diagnostic!.Severity);
        Assert.Contains("Portrait1", diagnostic.GetMessage());
    }

    [Fact]
    public void Correct_enum_value_is_not_reported()
    {
        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", Report("""<PageFormat Orientation="Portrait" />""")));

        Assert.Null(InvalidValue(result));
    }

    [Fact]
    public void Enum_value_is_matched_case_insensitively()
    {
        // ValueConverter parses with ignoreCase, so validation that rejected this would reject a
        // value the runtime accepts.
        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", Report("""<PageFormat Orientation="portrait" />""")));

        Assert.Null(InvalidValue(result));
    }

    [Fact]
    public void A_non_enum_value_is_left_alone()
    {
        // Sizes, colours and the rest have converters of their own; only enums are checked here.
        var result = GeneratorTestHarness.Run(Types, ("Report.rxaml", Report("""<PageFormat Size="A4" />""")));

        Assert.Null(InvalidValue(result));
    }

    [Fact]
    public void A_binding_is_left_alone()
    {
        // The value is resolved at run time from the data context; it is not an enum literal.
        var result = GeneratorTestHarness.Run(
            Types,
            ("Report.rxaml", Report("""<PageFormat Orientation="{Binding Whatever}" />""")));

        Assert.Null(InvalidValue(result));
    }

    [Fact]
    public void The_squiggle_covers_the_value_rather_than_the_whole_attribute()
    {
        var xaml = Report("""<PageFormat Orientation="Portrait1" />""");

        var diagnostic = InvalidValue(GeneratorTestHarness.Run(Types, ("Report.rxaml", xaml)));

        Assert.NotNull(diagnostic);
        var span = diagnostic!.Location.GetLineSpan();
        var line = xaml.Split('\n')[span.StartLinePosition.Line].TrimEnd('\r');
        Assert.Equal(
            "Portrait1",
            line.Substring(
                span.StartLinePosition.Character,
                span.EndLinePosition.Character - span.StartLinePosition.Character));
    }
}
