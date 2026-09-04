using Pysar.Core.Enums;
using Pysar.Elements;
using Xunit;

namespace Pysar.Xaml.Tests;

/// <summary>
///     Value precedence on the runtime-loader path, asserted after <see cref="Report.Build"/> - the point
///     where <c>StyleEngine</c> runs a second style pass over the already-loaded tree.
///     See docs/superpowers/plans/2026-09-04-value-precedence.md.
/// </summary>
public class ValuePrecedenceXamlTests
{
    private const string Root =
        "xmlns=\"https://mriyalab.com/pysar\" xmlns:x=\"http://schemas.microsoft.com/winfx/2006/xaml\"";

    private static Text LoadSingleText(string body, string resources)
    {
        var xaml = $"<Report {Root}><Report.Resources>{resources}</Report.Resources>"
                   + $"<PageHeaderBand>{body}</PageHeaderBand></Report>";
        var report = ReportXaml.Load(xaml).Build();
        return (Text)report.Bands.OfType<PageHeaderBand>().Single().Children[0];
    }

    private const string ImplicitAndFieldValue =
        "<Style TargetType=\"Text\">"
        + "<Setter Member=\"FontFamily\" Value=\"Ubuntu\"/>"
        + "<Setter Member=\"FontSize\" Value=\"14\"/>"
        + "</Style>"
        + "<Style x:Key=\"FieldValue\" TargetType=\"Text\">"
        + "<Setter Member=\"FontSize\" Value=\"8\"/>"
        + "<Setter Member=\"FontStyle\" Value=\"Bold\"/>"
        + "</Style>";

    [Fact]
    public void LocalAttribute_BeatsImplicitStyle_AfterBuild()
    {
        var text = LoadSingleText(
            "<Text FontFamily=\"LibreBarcode128\"/>",
            ImplicitAndFieldValue);

        Assert.Equal("LibreBarcode128", text.FontFamily);
        Assert.Equal(14f, text.FontSize);   // untouched locally
    }

    [Fact]
    public void LocalAttribute_BeatsExplicitStyle_AfterBuild()
    {
        var text = LoadSingleText(
            "<Text FontSize=\"55\" Style=\"{StaticResource FieldValue}\"/>",
            ImplicitAndFieldValue);

        Assert.Equal(55f, text.FontSize);
        Assert.Equal(FontStyle.Bold, text.FontStyle);   // from the explicit style
    }

    [Fact]
    public void LocalAttribute_EqualToTypeDefault_BeatsStyle_AfterBuild()
    {
        var text = LoadSingleText(
            "<Text FontStyle=\"Normal\" Style=\"{StaticResource FieldValue}\"/>",
            ImplicitAndFieldValue);

        Assert.Equal(FontStyle.Normal, text.FontStyle);
    }

    [Fact]
    public void ExplicitStyle_BeatsImplicitStyle_AfterBuild()
    {
        var text = LoadSingleText(
            "<Text Style=\"{StaticResource FieldValue}\"/>",
            ImplicitAndFieldValue);

        Assert.Equal("Ubuntu", text.FontFamily);   // only the implicit style sets it
        Assert.Equal(8f, text.FontSize);           // explicit style wins
        Assert.Equal(FontStyle.Bold, text.FontStyle);
    }

    [Fact]
    public void Binding_BeatsStyle_AfterBuild()
    {
        var xaml = $"<Report {Root}><Report.Resources>"
                   + "<Style TargetType=\"Text\"><Setter Member=\"Content\" Value=\"from-style\"/></Style>"
                   + "</Report.Resources>"
                   + "<PageHeaderBand><Text Content=\"{Binding Title}\"/></PageHeaderBand></Report>";

        var report = ReportXaml.Load(xaml);
        report.DataContext = new { Title = "from-binding" };
        report.Build();

        var text = (Text)report.Bands.OfType<PageHeaderBand>().Single().Children[0];
        Assert.Equal("from-binding", text.Content);
    }
}
