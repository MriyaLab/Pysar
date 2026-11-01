using Xunit;

namespace Pysar.Architecture.Tests;

public sealed class UnoFontTargetsTests : IDisposable
{
    private readonly MsBuildProbe _probe = new();

    public void Dispose() => _probe.Dispose();

    [Fact]
    public void UnoFont_IsAlsoEmbeddedUnderTheReportsPath()
    {
        var targets = Path.Combine(
            MsBuildProbe.RepositoryRoot(), "src", "Pysar.Uno", "buildTransitive", "Pysar.Uno.targets");

        _probe.WriteProject(targets, """
                                     <ItemGroup>
                                       <UnoFont Include="Assets\Fonts\Cookie-Regular.ttf" />
                                     </ItemGroup>
                                     """);
        _probe.WriteFile(Path.Combine("Assets", "Fonts", "Cookie-Regular.ttf"), "not a real font");

        var entry = Assert.Single(_probe.Entries("EmbeddedResource", "_PysarEmbedUnoFonts"));

        Assert.Equal("Fonts/Cookie-Regular.ttf", MsBuildProbe.Metadata(entry, "LogicalName"));
    }
    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public void UnoFont_AlreadyEmbedded_IsNotEmbeddedAgain(bool unoTargetsFirst)
    {
        var targets = Path.Combine(
            MsBuildProbe.RepositoryRoot(), "src", "Pysar.Uno", "buildTransitive", "Pysar.Uno.targets");
        var assetsTargets = Path.Combine(
            MsBuildProbe.RepositoryRoot(), "src", "Pysar", "buildTransitive", "Pysar.Assets.targets");
        var declarations = """
                           <ItemGroup>
                             <UnoFont Include="Assets/Fonts/Cookie-Regular.ttf" />
                             <ReportAsset Include="Assets/Fonts/Cookie-Regular.ttf" Link="Fonts/Cookie-Regular.ttf" />
                           </ItemGroup>
                           """;
        var firstImport = unoTargetsFirst ? targets : assetsTargets;
        var lastImport = unoTargetsFirst ? assetsTargets : targets;
        _probe.WriteProject(lastImport, declarations + $"<Import Project=\"{firstImport}\" />");
        _probe.WriteFile(Path.Combine("Assets", "Fonts", "Cookie-Regular.ttf"), "not a real font");

        Assert.Single(_probe.Entries("EmbeddedResource", "_PysarEmbedUnoFonts"));
    }

    [Fact]
    public void UnoFont_ExplicitEmbeddedResource_IsNotEmbeddedAgain()
    {
        var targets = Path.Combine(
            MsBuildProbe.RepositoryRoot(), "src", "Pysar.Uno", "buildTransitive", "Pysar.Uno.targets");
        _probe.WriteProject(targets, """
                                     <ItemGroup>
                                       <UnoFont Include="Assets/Fonts/Cookie-Regular.ttf" />
                                       <EmbeddedResource Include="Assets/Fonts/Cookie-Regular.ttf"
                                                         LogicalName="Fonts/Cookie-Regular.ttf" />
                                     </ItemGroup>
                                     """);
        _probe.WriteFile(Path.Combine("Assets", "Fonts", "Cookie-Regular.ttf"), "not a real font");

        Assert.Single(_probe.Entries("EmbeddedResource", "_PysarEmbedUnoFonts"));
    }
}
