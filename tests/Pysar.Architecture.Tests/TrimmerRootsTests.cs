using System.Text.RegularExpressions;
using System.Xml.Linq;
using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     Report markup reaches types and properties by string alone, so a trimmed publish is free to
///     delete every one of them - Pysar.Elements.Text loses all eleven of its setters and the first
///     report to set Content fails at InitializeComponent. Two descriptors stand between that and a
///     released package, and neither fails loudly on its own: a wrong LogicalName is ignored by the
///     trimmer without a word, and a namespace added to XmlnsDefinition but not to the descriptor
///     goes missing only on the head that publishes trimmed.
/// </summary>
[Collection(PackCollection.Name)]
public sealed class TrimmerRootsTests
{
    private readonly PackFixture _pack;

    public TrimmerRootsTests(PackFixture pack) => _pack = pack;

    private static string DescriptorPath() =>
        Path.Combine(RepoRoot.Path, "src", "Pysar", "ILLink.Descriptors.xml");

    [Fact]
    public void Pysar_ShipsItsDescriptorUnderTheNameTheTrimmerReads()
    {
        // The trimmer reads embedded descriptors by exact resource name. The default name for this
        // file would be Pysar.ILLink.Descriptors.xml, which it skips in silence - the package still
        // builds, installs and publishes, and only the rendered report is wrong.
        var assembly = _pack.ReadBytesOf("Pysar", "lib/net10.0/Pysar.dll");

        Assert.Contains("ILLink.Descriptors.xml", AssemblyResources.NamesIn(assembly));
    }

    [Fact]
    public void EveryNamespaceMarkupCanName_IsPreserved()
    {
        // XmlnsDefinition is the contract: whatever it maps into the default namespace is what a
        // report may name, and XamlTypeResolver resolves it out of this assembly reflectively.
        // Adding a namespace there without adding it here is the silent half of this failure.
        var declared = XmlnsNamespaces();
        var preserved = PreservedNamespaces();

        Assert.NotEmpty(declared);
        Assert.All(declared, ns => Assert.Contains(ns, preserved));
    }

    [Fact]
    public void TheBaseTypesElementsInheritFrom_ArePreserved()
    {
        // A namespace matches exactly here, not by prefix, and Width, Margin, IsVisible and
        // HorizontalAlignment are declared on ReportElement rather than on any element markup
        // names. Preserving only Pysar.Elements moves the failure instead of fixing it: every type
        // resolves, and the first attribute assigned throws Arg_SetMethNotFnd from SetValue.
        Assert.Contains("Pysar.Elements.Base", PreservedNamespaces());
    }

    [Fact]
    public void TheEnumsBehindMarkupAttributes_ArePreserved()
    {
        // Not reachable through XmlnsDefinition - an enum is named as attribute text, never as an
        // element - but ValueConverter turns that text into a value with Enum.Parse, which needs
        // the field names the trimmer would otherwise drop.
        Assert.Contains("Pysar.Core.Enums", PreservedNamespaces());
    }

    [Fact]
    public void TheLayersMarkupCannotReach_StayTrimmable()
    {
        // The descriptor is a cost: everything it names survives in every trimmed application.
        // Binding, Export and Skia are reached from compiled code, which the trimmer follows on
        // its own, so preserving them would buy nothing and charge every browser head for it.
        var preserved = PreservedNamespaces();

        foreach (var layer in new[] { "Pysar.Binding", "Pysar.Export", "Pysar.Skia" })
            Assert.DoesNotContain(layer, preserved);
    }

    /// <summary>The CLR namespaces AssemblyInfo.cs maps into the Pysar XML namespace.</summary>
    private static string[] XmlnsNamespaces()
    {
        var source = File.ReadAllText(Path.Combine(RepoRoot.Path, "src", "Pysar", "AssemblyInfo.cs"));

        return Regex
            .Matches(source, """XmlnsDefinition\(\s*"[^"]*"\s*,\s*"([^"]+)"\s*\)""")
            .Select(m => m.Groups[1].Value)
            .Distinct()
            .ToArray();
    }

    private static string[] PreservedNamespaces() => XDocument
        .Load(DescriptorPath())
        .Descendants("namespace")
        .Select(e => e.Attribute("fullname")!.Value)
        .ToArray();
}
