using Xunit;

namespace Pysar.Architecture.Tests;

public sealed class PackageLayoutTests : IClassFixture<PackFixture>
{
    private readonly PackFixture _pack;

    public PackageLayoutTests(PackFixture pack) => _pack = pack;

    [Fact]
    public void Pysar_ShipsOneMergedAssembly()
    {
        var entries = _pack.EntriesOf("Pysar");

        Assert.Contains("lib/net10.0/Pysar.dll", entries);

        foreach (var merged in new[] { "Core", "Binding", "Elements", "Export", "Skia" })
            Assert.DoesNotContain($"lib/net10.0/Pysar.{merged}.dll", entries);
    }

    [Fact]
    public void Xaml_ShipsLoaderAndGenerator()
    {
        var entries = _pack.EntriesOf("Pysar.Xaml");

        Assert.Contains("lib/net10.0/Pysar.Xaml.dll", entries);
        Assert.Contains("analyzers/dotnet/cs/Pysar.Xaml.SourceGen.dll", entries);
    }

    [Fact]
    public void Xaml_ShipsTheAutoImportedProps()
    {
        // MSBuild auto-imports build/<PackageId>.props. Any other name is silently ignored,
        // and reports would stop reaching the generator with no diagnostic at all.
        Assert.Contains("build/Pysar.Xaml.props", _pack.EntriesOf("Pysar.Xaml"));
    }

    [Fact]
    public void Xaml_DoesNotShipTheModelAsAnAssembly()
    {
        Assert.DoesNotContain(
            "lib/net10.0/Pysar.Xaml.Model.dll",
            _pack.EntriesOf("Pysar.Xaml"));
    }

    [Fact]
    public void Xaml_DependsOnPysarAlone()
    {
        var nuspec = _pack.NuspecOf("Pysar.Xaml");

        Assert.Contains("id=\"Pysar\"", nuspec);

        foreach (var gone in new[] { "Core", "Binding", "Elements", "Export", "Skia", "Xaml.Model", "Xaml.SourceGen" })
            Assert.DoesNotContain($"id=\"Pysar.{gone}\"", nuspec);
    }
}
