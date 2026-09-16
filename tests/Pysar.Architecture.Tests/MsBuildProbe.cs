using System.Diagnostics;
using System.Text.Json;
using Xunit;

namespace Pysar.Architecture.Tests;

/// <summary>
///     A temporary project that imports one of Pysar's shipped MSBuild files, evaluated by real
///     MSBuild. Nothing else evaluates them: the repository's own projects never apply a package's
///     build files, so a defect in one reaches a user with no unit test noticing.
/// </summary>
internal sealed class MsBuildProbe : IDisposable
{
    private readonly string _dir = Directory.CreateTempSubdirectory("pysar-targets-").FullName;

    public void Dispose() => Directory.Delete(_dir, recursive: true);

    /// <summary>Writes a project importing <paramref name="importPath"/>, with <paramref name="body"/> inside it.</summary>
    public void WriteProject(string importPath, string body) => File.WriteAllText(
        Path.Combine(_dir, "probe.csproj"),
        $"""
         <Project Sdk="Microsoft.NET.Sdk">
           <PropertyGroup><TargetFramework>net10.0</TargetFramework></PropertyGroup>
         {body}
           <Import Project="{importPath}" />
         </Project>
         """);

    public void WriteFile(string relativePath, string content)
    {
        var full = Path.Combine(_dir, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        File.WriteAllText(full, content);
    }

    /// <summary>The repository root, found by walking up to Pysar.sln.</summary>
    public static string RepositoryRoot() => RepoRoot.Path;

    /// <summary>Identities of <paramref name="itemType"/> after evaluation, forward-slashed.</summary>
    public string[] Identities(string itemType)
        => Entries(itemType).Select(e => Normalise(e.GetProperty("Identity").GetString()!)).ToArray();

    /// <summary>Every evaluated item of <paramref name="itemType"/>, metadata included.</summary>
    public JsonElement[] Entries(string itemType)
    {
        var output = Run([$"-getItem:{itemType}"], out var exitCode);
        Assert.True(exitCode == 0, $"msbuild failed:\n{output}");

        using var document = JsonDocument.Parse(Json(output));
        return document.RootElement.GetProperty("Items").TryGetProperty(itemType, out var items)
            ? items.EnumerateArray().Select(e => e.Clone()).ToArray()
            : [];
    }

    /// <summary>Runs a target and returns its output, whether or not it succeeded.</summary>
    public string RunTarget(string target, out int exitCode) => Run([$"-t:{target}"], out exitCode);

    /// <summary>Metadata of an item, forward-slashed - MSBuild reports the host's separator.</summary>
    public static string Metadata(JsonElement entry, string name)
        => entry.TryGetProperty(name, out var value) ? Normalise(value.GetString() ?? "") : "";

    private string Run(string[] arguments, out int exitCode)
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
            info.ArgumentList.Add(argument);

        using var process = Process.Start(info)!;
        var output = process.StandardOutput.ReadToEnd();
        var error = process.StandardError.ReadToEnd();
        process.WaitForExit();

        exitCode = process.ExitCode;
        return output + error;
    }

    /// <summary>Trims any MSBuild preamble ahead of the JSON document.</summary>
    private static string Json(string output) => output[output.IndexOf('{')..];

    private static string Normalise(string value) => value.Replace('\\', '/');
}
