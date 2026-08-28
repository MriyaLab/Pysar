using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Pysar.Xaml.SourceGen.Tests;

/// <summary>
///     Evaluates the props file the NuGet package ships, using real MSBuild.
///     Nothing else does: the repository's own projects reference the generator with
///     OutputItemType="Analyzer", which never applies a package's build/*.props, so three
///     separate defects in this file reached a user without any unit test noticing.
/// </summary>
public sealed class ReportItemGlobTests : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pysar-props-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    [Fact]
    public void TheGlob_TakesReports_AndLeavesTheHostFrameworksXamlAlone()
    {
        WriteProbeProject();
        File.WriteAllText(Path.Combine(_dir, "Invoice.rxaml"), "<Report/>");
        File.WriteAllText(Path.Combine(_dir, "App.xaml"), "<Application/>");
        Directory.CreateDirectory(Path.Combine(_dir, "Styles"));
        File.WriteAllText(Path.Combine(_dir, "Styles", "Colors.rxaml"), "<ResourceDictionary/>");

        var items = Identities("Pysar");

        Assert.Contains("Invoice.rxaml", items);
        Assert.Contains("Styles/Colors.rxaml", items);
        // The entire point of the migration: a host framework's markup is not ours.
        Assert.DoesNotContain("App.xaml", items);
    }

    [Fact]
    public void TheGlob_IsProjectedIntoAdditionalFiles()
    {
        // A generator sees nothing that is not an AdditionalFile; Pysar alone would be inert.
        WriteProbeProject();
        File.WriteAllText(Path.Combine(_dir, "Invoice.rxaml"), "<Report/>");

        Assert.Contains("Invoice.rxaml", Identities("AdditionalFiles"));
    }

    [Fact]
    public void CodeBehind_IsNestedUnderItsReport()
    {
        // No IDE knows the .rxaml.cs suffix, so the nesting has to come from MSBuild metadata.
        WriteProbeProject();
        File.WriteAllText(Path.Combine(_dir, "Invoice.rxaml"), "<Report/>");
        File.WriteAllText(Path.Combine(_dir, "Invoice.rxaml.cs"), "public partial class Invoice {}");

        var json = Run("-getItem:Compile");
        using var document = JsonDocument.Parse(Json(json));
        var entry = document.RootElement.GetProperty("Items").GetProperty("Compile")
            .EnumerateArray()
            .Single(e => Normalise(e.GetProperty("Identity").GetString()!) == "Invoice.rxaml.cs");

        Assert.Equal("Invoice.rxaml", entry.GetProperty("DependentUpon").GetString());
    }

    [Fact]
    public void TheOptOut_LeavesTheGroupEmpty()
    {
        WriteProbeProject();
        File.WriteAllText(Path.Combine(_dir, "Invoice.rxaml"), "<Report/>");

        Assert.Empty(Identities("Pysar", "-p:EnableDefaultReportItems=false"));
    }

    private void WriteProbeProject() => File.WriteAllText(
        Path.Combine(_dir, "probe.csproj"),
        $"""
         <Project Sdk="Microsoft.NET.Sdk">
           <Import Project="{PropsPath()}" />
           <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
         </Project>
         """);

    private static string PropsPath()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null
               && !File.Exists(Path.Combine(directory.FullName, "Pysar.sln")))
            directory = directory.Parent;

        Assert.NotNull(directory);
        return Path.Combine(
            directory!.FullName, "src", "Pysar.Xaml", "build",
            "Pysar.Xaml.props");
    }

    private string[] Identities(string itemType, string? extraArgument = null)
    {
        var json = Json(Run($"-getItem:{itemType}", extraArgument));
        using var document = JsonDocument.Parse(json);
        return document.RootElement.GetProperty("Items").TryGetProperty(itemType, out var items)
            ? items.EnumerateArray()
                .Select(e => Normalise(e.GetProperty("Identity").GetString()!))
                .ToArray()
            : [];
    }

    private string Run(params string?[] arguments)
    {
        var info = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = _dir,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        info.ArgumentList.Add("msbuild");
        info.ArgumentList.Add("probe.csproj");
        foreach (var argument in arguments)
            if (!string.IsNullOrEmpty(argument))
                info.ArgumentList.Add(argument);

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        Assert.True(process.ExitCode == 0, $"msbuild failed:\n{output}\n{error}");
        return output;
    }

    /// <summary>Trims any MSBuild preamble ahead of the JSON document.</summary>
    private static string Json(string output) => output[output.IndexOf('{')..];

    /// <summary>MSBuild reports item identities with the host's separator; CI runs Windows too.</summary>
    private static string Normalise(string identity) => identity.Replace('\\', '/');
}
