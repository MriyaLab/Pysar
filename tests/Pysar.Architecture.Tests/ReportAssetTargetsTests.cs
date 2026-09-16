using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     One case per row of the translation table in the ReportAsset design: the whole value of the
///     item type is that a wrong translation fails at build time here, not at render time on a
///     device.
/// </summary>
public sealed class ReportAssetTargetsTests : IDisposable
{
    private readonly MsBuildProbe _probe = new();

    private static string TargetsPath() => Path.Combine(
        MsBuildProbe.RepositoryRoot(), "src", "Pysar", "buildTransitive", "Pysar.Assets.targets");

    public void Dispose() => _probe.Dispose();

    [Fact]
    public void ALibrary_EmbedsItsAssetsUnderTheReportsPath()
    {
        WriteProbe("""
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        var entry = Assert.Single(_probe.Entries("EmbeddedResource"));

        Assert.Equal("Fonts/Ubuntu.ttf", MsBuildProbe.Metadata(entry, "LogicalName"));
    }

    [Fact]
    public void ALibrary_MarksItselfAsAnAssetSource()
    {
        WriteProbe("""
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        Assert.Contains(
            "Pysar.Core.Abstractions.PysarAssetSourceAttribute",
            _probe.Identities("AssemblyAttribute"));
    }

    [Fact]
    public void AProjectWithoutAssets_IsNotMarked()
    {
        WriteProbe("");

        Assert.DoesNotContain(
            "Pysar.Core.Abstractions.PysarAssetSourceAttribute",
            _probe.Identities("AssemblyAttribute"));
    }

    [Fact]
    public void Maui_PackagesAssetsIntoResourcesRaw()
    {
        WriteProbe("""
                     <PropertyGroup><UseMaui>true</UseMaui></PropertyGroup>
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        var entry = Assert.Single(_probe.Entries("MauiAsset"));

        Assert.Equal("Fonts/Ubuntu.ttf", MsBuildProbe.Metadata(entry, "LogicalName"));
        Assert.Equal("Resources/Raw/Fonts/Ubuntu.ttf", MsBuildProbe.Metadata(entry, "Link"));
        Assert.Empty(_probe.Entries("EmbeddedResource"));
    }

    [Fact]
    public void Avalonia_PackagesAssetsUnderTheAvaresPathTheReportAsksFor()
    {
        // AvaloniaResource has no LogicalName: its avares:// path is the item's Link.
        WriteProbe("""
                     <PropertyGroup><PysarAssetPackaging>Avalonia</PysarAssetPackaging></PropertyGroup>
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        var entry = Assert.Single(_probe.Entries("AvaloniaResource"));

        Assert.Equal("Fonts/Ubuntu.ttf", MsBuildProbe.Metadata(entry, "Link"));
    }

    [Fact]
    public void AnAvaloniaPackageReference_IsDetectedWithoutAnExplicitProperty()
    {
        WriteProbe("""
                     <ItemGroup><PackageReference Include="Pysar.Avalonia" Version="1.0.0" /></ItemGroup>
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        Assert.Single(_probe.Entries("AvaloniaResource"));
    }

    [Fact]
    public void ALinkedAsset_KeepsThePathTheReportAsksFor()
    {
        WriteProbe("""
                     <ItemGroup>
                       <ReportAsset Include="..\Shared\Fonts\Ubuntu.ttf" Link="Fonts\Ubuntu.ttf" />
                     </ItemGroup>
                   """);

        var entry = Assert.Single(_probe.Entries("EmbeddedResource"));

        Assert.Equal("Fonts/Ubuntu.ttf", MsBuildProbe.Metadata(entry, "LogicalName"));
    }

    [Fact]
    public void CopyToOutput_IsOptIn()
    {
        WriteProbe("""
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        Assert.DoesNotContain("Fonts/Ubuntu.ttf", _probe.Identities("Content"));

        WriteProbe("""
                     <PropertyGroup><PysarAssetCopyToOutput>true</PysarAssetCopyToOutput></PropertyGroup>
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        Assert.Contains("Fonts/Ubuntu.ttf", _probe.Identities("Content"));
    }

    [Fact]
    public void AnUnrecognisedPackagingValue_IsAnError()
    {
        // A typo that silently produced a head with no assets would surface as a blank report on a
        // device, which is the failure this item type exists to remove.
        WriteProbe("""
                     <PropertyGroup><PysarAssetPackaging>Mauii</PysarAssetPackaging></PropertyGroup>
                     <ItemGroup><ReportAsset Include="Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        var output = _probe.RunTarget("PysarValidateReportAssets", out var exitCode);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("PYSAR1002", output);
    }

    [Fact]
    public void AnAssetOutsideTheProjectWithoutALink_IsAnError()
    {
        WriteProbe("""
                     <ItemGroup><ReportAsset Include="..\Shared\Fonts\Ubuntu.ttf" /></ItemGroup>
                   """);

        var output = _probe.RunTarget("PysarValidateReportAssets", out var exitCode);

        Assert.NotEqual(0, exitCode);
        Assert.Contains("PYSAR1003", output);
    }

    private void WriteProbe(string body)
    {
        _probe.WriteProject(TargetsPath(), body);
        _probe.WriteFile(Path.Combine("Fonts", "Ubuntu.ttf"), "not a real font");
    }
}
