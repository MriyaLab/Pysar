using System.Text.Json;
using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     The consumer half of the trimming story: a project that owns reports roots itself, because
///     its converters, custom elements and the data models behind every binding path are reached
///     by string and by nothing else. Nothing in the repository applies a package's build files, so
///     without this the first notice of a defect here is a blank report in someone's browser.
/// </summary>
public sealed class TrimmerRootTargetsTests : IDisposable
{
    private const string DescriptorName = "ILLink.Descriptors.xml";

    private readonly MsBuildProbe _probe = new();

    private static string TargetsPath() => Path.Combine(
        MsBuildProbe.RepositoryRoot(), "src", "Pysar.Xaml", "build", "Pysar.Xaml.targets");

    private static string PropsPath() => Path.Combine(
        MsBuildProbe.RepositoryRoot(), "src", "Pysar.Xaml", "build", "Pysar.Xaml.props");

    public void Dispose() => _probe.Dispose();

    [Fact]
    public void AProjectWithReports_RootsItself()
    {
        WriteProbe(withReport: true);

        Assert.Single(Descriptors());
    }

    [Fact]
    public void TheDescriptorReachesTheAssembly()
    {
        // The item existing is not the same as the resource being compiled in, and the gap between
        // them is silent: hook the target after the compiler's resource inputs are settled and the
        // build still succeeds, still reports the item, and still ships an assembly the trimmer
        // finds nothing in. Only the built dll can tell the two apart.
        WriteProbe(withReport: true);

        _probe.RunTarget("Restore;Build", out var exitCode);
        Assert.Equal(0, exitCode);

        Assert.Contains(
            DescriptorName,
            AssemblyResources.NamesIn(_probe.ReadBytes("bin/Debug/net10.0/probe.dll")));
    }

    [Fact]
    public void TheGeneratedDescriptor_NamesTheProjectsOwnAssembly()
    {
        // The whole file is one assembly name. Get it wrong and the trimmer resolves nothing,
        // warns IL2007 into a log nobody reads, and preserves exactly as much as before.
        WriteProbe(withReport: true);
        var descriptor = Assert.Single(Descriptors());

        var content = _probe.ReadFile(MsBuildProbe.Metadata(descriptor, "Identity"));

        Assert.Contains("""<assembly fullname="probe" />""", content);
    }

    [Fact]
    public void AProjectWithoutReports_IsLeftAlone()
    {
        // A head that only references a report library has nothing of its own to preserve; the
        // library roots itself, and rooting the head too would be size charged for nothing.
        WriteProbe(withReport: false);

        Assert.Empty(Descriptors());
    }

    [Fact]
    public void TheOptOut_IsHonoured()
    {
        WriteProbe(withReport: true, body: """
                                             <PropertyGroup><PysarDisableTrimmerRoots>true</PysarDisableTrimmerRoots></PropertyGroup>
                                           """);

        Assert.Empty(Descriptors());
    }

    [Fact]
    public void AProjectWithItsOwnDescriptor_KeepsIt()
    {
        // Two resources under one logical name do not merge, they fail the compile. A project that
        // has already said what it wants preserved has said it more precisely than this could.
        _probe.WriteFile("MyRoots.xml", "<linker />");
        WriteProbe(withReport: true, body: $"""
                                             <ItemGroup>
                                               <EmbeddedResource Include="MyRoots.xml" LogicalName="{DescriptorName}" />
                                             </ItemGroup>
                                           """);

        var descriptor = Assert.Single(Descriptors());

        Assert.Equal("MyRoots.xml", MsBuildProbe.Metadata(descriptor, "Identity"));
    }

    private JsonElement[] Descriptors() => _probe
        .Entries("EmbeddedResource", afterTarget: "PysarGenerateTrimmerRoots")
        .Where(e => MsBuildProbe.Metadata(e, "LogicalName") == DescriptorName)
        .ToArray();

    private void WriteProbe(bool withReport, string body = "")
    {
        // Through the props rather than declaring @(Pysar) by hand: the glob there is what decides
        // whether a project owns reports, and the two files only work as a pair.
        _probe.WriteProject(TargetsPath(), $"""
                                            {body}
                                              <Import Project="{PropsPath()}" />
                                            """);

        if (withReport)
            _probe.WriteFile("Report.rxaml", "<Report />");
    }
}
