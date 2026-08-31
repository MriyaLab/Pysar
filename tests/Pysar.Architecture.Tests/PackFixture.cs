using System.Diagnostics;
using System.IO.Compression;

namespace Pysar.Architecture.Tests;

/// <summary>
///     Packs the two shipping libraries into a temporary directory once, and exposes the entry
///     names of each produced .nupkg.
/// </summary>
public sealed class PackFixture : IDisposable
{
    private readonly string _outputDir =
        Path.Combine(Path.GetTempPath(), "pysar-pack-" + Guid.NewGuid().ToString("N"));

    public PackFixture()
    {
        Pack("src/Pysar/Pysar.csproj");
        Pack("src/Pysar.Xaml/Pysar.Xaml.csproj");
    }

    public IReadOnlyList<string> EntriesOf(string packageId)
    {
        using var zip = ZipFile.OpenRead(FindNupkg(packageId));
        return zip.Entries.Select(e => e.FullName).ToList();
    }

    public string NuspecOf(string packageId)
    {
        using var zip = ZipFile.OpenRead(FindNupkg(packageId));
        var entry = zip.GetEntry(packageId + ".nuspec")
                    ?? throw new InvalidOperationException($"No {packageId}.nuspec in the package.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    public string ReadTextOf(string packageId, string entryName)
    {
        using var zip = ZipFile.OpenRead(FindNupkg(packageId));
        var entry = zip.GetEntry(entryName)
                    ?? throw new InvalidOperationException($"No {entryName} in the {packageId} package.");
        using var reader = new StreamReader(entry.Open());
        return reader.ReadToEnd();
    }

    /// <summary>
    ///     Resolves the .nupkg for exactly this package id. A plain "&lt;id&gt;.*.nupkg" glob would
    ///     also match a longer id sharing the prefix - Pysar.*.nupkg matches
    ///     Pysar.Xaml.0.1.0.nupkg - and both packages land in the same directory, so the
    ///     tests would silently assert against the wrong package. The version that follows the id
    ///     always starts with a digit, which is what separates the two cases.
    /// </summary>
    private string FindNupkg(string packageId)
    {
        var prefix = packageId + ".";

        var matches = Directory
            .GetFiles(_outputDir, "*.nupkg")
            .Where(p =>
            {
                var name = Path.GetFileName(p);
                return name.StartsWith(prefix, StringComparison.Ordinal)
                       && name.Length > prefix.Length
                       && char.IsDigit(name[prefix.Length]);
            })
            .OrderBy(p => p, StringComparer.Ordinal)
            .ToList();

        return matches.Count switch
        {
            1 => matches[0],
            0 => throw new InvalidOperationException(
                $"No {packageId} .nupkg in {_outputDir}. Found: " +
                string.Join(", ", Directory.GetFiles(_outputDir).Select(Path.GetFileName))),
            _ => throw new InvalidOperationException(
                $"Ambiguous {packageId} .nupkg in {_outputDir}: " +
                string.Join(", ", matches.Select(Path.GetFileName))),
        };
    }

    private void Pack(string projectRelativePath)
    {
        var psi = new ProcessStartInfo("dotnet")
        {
            WorkingDirectory = RepoRoot.Path,
            RedirectStandardOutput = true,
            RedirectStandardError = true,
        };
        psi.ArgumentList.Add("pack");
        psi.ArgumentList.Add(projectRelativePath);
        psi.ArgumentList.Add("-c");
        psi.ArgumentList.Add("Release");
        psi.ArgumentList.Add("-o");
        psi.ArgumentList.Add(_outputDir);

        using var process = Process.Start(psi)!;
        var stdout = process.StandardOutput.ReadToEnd();
        var stderr = process.StandardError.ReadToEnd();
        process.WaitForExit();

        if (process.ExitCode != 0)
            throw new InvalidOperationException(
                $"dotnet pack {projectRelativePath} exited {process.ExitCode}.\n{stdout}\n{stderr}");
    }

    public void Dispose()
    {
        if (Directory.Exists(_outputDir))
            Directory.Delete(_outputDir, recursive: true);
    }
}
